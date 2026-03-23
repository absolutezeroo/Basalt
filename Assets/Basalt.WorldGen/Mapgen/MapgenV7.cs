using System;
using Unity.Collections;
using Unity.Jobs;
using Unity.Mathematics;

namespace Basalt.WorldGen
{
    /// <summary>
    /// Orchestrator for Mapgen V7 terrain generation.
    /// Owns all persistent noise buffers and schedules the full noise + terrain job pipeline.
    /// </summary>
    /// <remarks>
    /// Lifecycle:
    ///   1. Construct once per world with <see cref="MapgenV7(int)"/> or <see cref="MapgenV7(MapgenV7Params)"/>.
    ///   2. Call <see cref="Initialize"/> with resolved content IDs from NodeRegistry.
    ///   3. Per mapchunk: call <see cref="Generate"/> to receive a <see cref="JobHandle"/>.
    ///   4. Caller completes the handle (next frame or on demand), then reads the VoxelManipulator.
    ///   5. Dispose the VoxelManipulator when the caller has extracted MapBlock data.
    ///   6. Dispose this instance when the world unloads.
    ///
    /// No MonoBehaviour — lives in Basalt.WorldGen. Callers in Basalt.Server or Basalt.Client
    /// manage the lifetime.
    ///
    /// Source: <c>luanti/src/mapgen/mapgen_v7.cpp</c>.
    /// </remarks>
    public sealed class MapgenV7 : IMapgen
    {
        private MapgenV7Params _params;
        private MapgenV7NoiseChannels _channels;
        private MapgenFeatures _features;
        private bool _initialized;

        /// <summary>Gets the current generation parameters.</summary>
        public MapgenV7Params Params => _params;

        /// <summary>
        /// Creates a new Mapgen V7 instance with default noise parameters.
        /// Call <see cref="Initialize"/> before the first <see cref="Generate"/> call.
        /// </summary>
        /// <param name="seed">The world seed.</param>
        public MapgenV7(int seed)
        {
            _params = MapgenV7Params.CreateDefault(seed);
            _channels = new MapgenV7NoiseChannels();
        }

        /// <summary>
        /// Creates a new Mapgen V7 instance with custom parameters.
        /// Call <see cref="Initialize"/> before the first <see cref="Generate"/> call.
        /// </summary>
        public MapgenV7(MapgenV7Params parameters)
        {
            _params = parameters;
            _channels = new MapgenV7NoiseChannels();
        }

        /// <summary>
        /// Finalizes content ID binding. Must be called once after the NodeRegistry is populated.
        /// </summary>
        /// <param name="contentStone">Resolved content ID for stone.</param>
        /// <param name="contentWater">Resolved content ID for water source.</param>
        public void Initialize(ushort contentStone, ushort contentWater)
        {
            _params.ContentStone = contentStone;
            _params.ContentWater = contentWater;
            _initialized = true;
        }

        /// <inheritdoc/>
        public void SetFeatures(MapgenFeatures features)
        {
            _features = features;
        }

        /// <summary>
        /// Schedules the full noise and terrain fill pipeline for one mapchunk.
        /// Returns a <see cref="JobHandle"/> that the caller completes at its own pace.
        /// </summary>
        /// <param name="chunkOrigin">
        /// World position of the mapchunk's minimum corner node (not counting overgeneration).
        /// For a mapchunk at block position (0,0,0): chunkOrigin = (0, 0, 0).
        /// For a mapchunk at block position (5,0,0): chunkOrigin = (80, 0, 0).
        /// </param>
        /// <param name="vmAllocator">
        /// Allocator for the returned VoxelManipulator. Use <c>Allocator.TempJob</c>
        /// when the caller completes the handle within 4 frames.
        /// </param>
        /// <param name="vm">The allocated VoxelManipulator that will receive the terrain data.</param>
        /// <returns>A JobHandle for the entire pipeline. Complete before reading <paramref name="vm"/>.</returns>
        /// <exception cref="InvalidOperationException">
        /// Thrown if <see cref="Initialize"/> has not been called.
        /// </exception>
        public JobHandle Generate(int3 chunkOrigin, Allocator vmAllocator, out VoxelManipulator vm)
        {
            if (!_initialized)
            {
                throw new InvalidOperationException(
                    "MapgenV7.Initialize() must be called before Generate().");
            }

            vm = new VoxelManipulator(chunkOrigin, vmAllocator);

            // World-space origin of the XZ footprint (same for 2D and 3D)
            float originX = chunkOrigin.x;
            float originZ = chunkOrigin.z;

            // World-space Y origin for 3D noise includes the overgeneration layer
            float originY = chunkOrigin.y - MapgenV7Constants.OVERGEN_SIZE;

            // ---- Stage 1: Schedule terrain_persist (2D) ----
            JobHandle persistHandle = ScheduleNoiseMap2D(
                _channels.TerrainPersist, _channels.MetaTerrainPersist,
                in _params.TerrainPersist, originX, originZ, _params.Seed,
                default);

            // ---- Stage 1b: Copy persist result into both persistence map arrays ----
            var copyPersistJob = new CopyNoiseResultJob
            {
                Source = _channels.TerrainPersist.ResultBuf,
                DestA = _channels.PersistBase,
                DestB = _channels.PersistAlt,
                BufSize = MapgenV7Constants.MAPCHUNK_SIZE * MapgenV7Constants.MAPCHUNK_SIZE,
            };
            JobHandle copyHandle = copyPersistJob.Schedule(persistHandle);

            // ---- Stage 2: terrain_base and terrain_alt (depend on persist copy) ----
            JobHandle baseHandle = ScheduleNoiseMap2D(
                _channels.TerrainBase, _channels.MetaTerrainBase,
                in _params.TerrainBase, originX, originZ, _params.Seed,
                copyHandle);

            JobHandle altHandle = ScheduleNoiseMap2D(
                _channels.TerrainAlt, _channels.MetaTerrainAlt,
                in _params.TerrainAlt, originX, originZ, _params.Seed,
                copyHandle);

            // ---- Stage 2 parallel: height_select ----
            JobHandle heightSelectHandle = ScheduleNoiseMap2D(
                _channels.HeightSelect, _channels.MetaHeightSelect,
                in _params.HeightSelect, originX, originZ, _params.Seed,
                default);

            // ---- Mountain noise (if enabled) ----
            JobHandle mountHeightHandle = default;
            JobHandle mountainHandle = default;
            if (_params.EnableMountains != 0)
            {
                mountHeightHandle = ScheduleNoiseMap2D(
                    _channels.MountHeight, _channels.MetaMountHeight,
                    in _params.MountHeight, originX, originZ, _params.Seed,
                    default);

                // Luanti: noise_mountain->noiseMap3D(node_min.X, node_min.Y - 1, node_min.Z)
                mountainHandle = ScheduleNoiseMap3D(
                    _channels.Mountain, _channels.MetaMountain,
                    in _params.Mountain, originX, originY, originZ, _params.Seed,
                    default);
            }

            // ---- Ridge noise (if enabled) ----
            JobHandle ridgeUwaterHandle = default;
            JobHandle ridgeHandle = default;
            if (_params.EnableRidges != 0)
            {
                ridgeUwaterHandle = ScheduleNoiseMap2D(
                    _channels.RidgeUwater, _channels.MetaRidgeUwater,
                    in _params.RidgeUwater, originX, originZ, _params.Seed,
                    default);

                ridgeHandle = ScheduleNoiseMap3D(
                    _channels.Ridge, _channels.MetaRidge,
                    in _params.Ridge, originX, originY, originZ, _params.Seed,
                    default);
            }

            // ---- Stage 4: Wait for all noise, then run terrain fill ----
            JobHandle allNoise = JobHandle.CombineDependencies(
                JobHandle.CombineDependencies(baseHandle, altHandle, heightSelectHandle),
                JobHandle.CombineDependencies(mountHeightHandle, ridgeUwaterHandle, mountainHandle),
                ridgeHandle);

            var terrainJob = new MapgenV7TerrainJob
            {
                TerrainBaseResult = _channels.TerrainBase.ResultBuf,
                TerrainAltResult = _channels.TerrainAlt.ResultBuf,
                HeightSelectResult = _channels.HeightSelect.ResultBuf,
                MountHeightResult = _channels.MountHeight.ResultBuf,
                RidgeUwaterResult = _channels.RidgeUwater.ResultBuf,
                MountainResult = _channels.Mountain.ResultBuf,
                RidgeResult = _channels.Ridge.ResultBuf,
                Nodes = vm.Nodes,
                Heightmap = vm.Heightmap,
                Params = _params,
                MinY = vm.MinPos.y,
                MaxY = vm.MaxPos.y,
            };

            JobHandle terrainHandle = terrainJob.Schedule(allNoise);

            // Chain post-terrain feature pipeline if attached
            if (_features != null)
            {
                return _features.ScheduleFeatures(chunkOrigin, vm, terrainHandle);
            }

            return terrainHandle;
        }

        /// <summary>
        /// Schedules the full three-phase 2D noise pipeline for one noise channel.
        /// Delegates to <see cref="NoiseMapScheduler.ScheduleMap2D"/>.
        /// </summary>
        private static JobHandle ScheduleNoiseMap2D(
            NoiseBuffer2D buffer,
            NoiseLatticeMetadata2D meta,
            in NoiseParams np,
            float originX, float originZ,
            int mapSeed,
            JobHandle dependency)
        {
            return NoiseMapScheduler.ScheduleMap2D(buffer, meta, in np, originX, originZ, mapSeed, dependency);
        }

        /// <summary>
        /// Schedules the full three-phase 3D noise pipeline for one noise channel.
        /// Delegates to <see cref="NoiseMapScheduler.ScheduleMap3D"/>.
        /// </summary>
        private static JobHandle ScheduleNoiseMap3D(
            NoiseBuffer3D buffer,
            NoiseLatticeMetadata3D meta,
            in NoiseParams np,
            float originX, float originY, float originZ,
            int mapSeed,
            JobHandle dependency)
        {
            return NoiseMapScheduler.ScheduleMap3D(buffer, meta, in np, originX, originY, originZ, mapSeed, dependency);
        }

        /// <summary>
        /// Disposes the owned feature pipeline and all persistent noise buffers.
        /// </summary>
        public void Dispose()
        {
            _features?.Dispose();
            _features = null;
            _channels?.Dispose();
            _channels = null;
        }
    }
}
