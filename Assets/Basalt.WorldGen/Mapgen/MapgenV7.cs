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
    public sealed class MapgenV7 : IDisposable
    {
        private MapgenV7Params _params;
        private MapgenV7NoiseChannels _channels;
        private JobHandle _lastHandle;
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

            // Ensure the previous generation's jobs are complete before reusing
            // the shared persistent noise buffers. This prevents write-after-read
            // races when Generate() is called sequentially for multiple chunks.
            JobHandle previousHandle = _lastHandle;

            // World-space origin of the XZ footprint (same for 2D and 3D)
            float originX = chunkOrigin.x;
            float originZ = chunkOrigin.z;

            // World-space Y origin for 3D noise includes the overgeneration layer
            float originY = chunkOrigin.y - MapgenV7Constants.OVERGEN_SIZE;

            // ---- Stage 1: Schedule terrain_persist (2D) ----
            JobHandle persistHandle = ScheduleNoiseMap2D(
                _channels.TerrainPersist, _channels.MetaTerrainPersist,
                in _params.TerrainPersist, originX, originZ, _params.Seed,
                previousHandle);

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
                previousHandle);

            // ---- Mountain noise (if enabled) ----
            JobHandle mountHeightHandle = default;
            JobHandle mountainHandle = default;
            if (_params.EnableMountains != 0)
            {
                mountHeightHandle = ScheduleNoiseMap2D(
                    _channels.MountHeight, _channels.MetaMountHeight,
                    in _params.MountHeight, originX, originZ, _params.Seed,
                    previousHandle);

                // Luanti: noise_mountain->noiseMap3D(node_min.X, node_min.Y - 1, node_min.Z)
                mountainHandle = ScheduleNoiseMap3D(
                    _channels.Mountain, _channels.MetaMountain,
                    in _params.Mountain, originX, originY, originZ, _params.Seed,
                    previousHandle);
            }

            // ---- Ridge noise (if enabled) ----
            JobHandle ridgeUwaterHandle = default;
            JobHandle ridgeHandle = default;
            if (_params.EnableRidges != 0)
            {
                ridgeUwaterHandle = ScheduleNoiseMap2D(
                    _channels.RidgeUwater, _channels.MetaRidgeUwater,
                    in _params.RidgeUwater, originX, originZ, _params.Seed,
                    previousHandle);

                ridgeHandle = ScheduleNoiseMap3D(
                    _channels.Ridge, _channels.MetaRidge,
                    in _params.Ridge, originX, originY, originZ, _params.Seed,
                    previousHandle);
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

            _lastHandle = terrainJob.Schedule(allNoise);
            return _lastHandle;
        }

        /// <summary>
        /// Schedules the full three-phase 2D noise pipeline (fill lattice, interpolate rows,
        /// accumulate octaves) for one noise channel.
        /// </summary>
        /// <remarks>
        /// For 2D terrain noise, Luanti maps XZ world coordinates to the 2D grid:
        /// NoiseFillLatticeJob.X = originX / spread.x, NoiseFillLatticeJob.Y = originZ / spread.z.
        /// </remarks>
        private static JobHandle ScheduleNoiseMap2D(
            NoiseBuffer2D buffer,
            NoiseLatticeMetadata2D meta,
            in NoiseParams np,
            float originX, float originZ,
            int mapSeed,
            JobHandle dependency)
        {
            bool hasPersistMap = buffer.PersistenceMap.IsCreated && buffer.PersistenceMap.Length > 0;

            // Zero the result and gmap buffers before accumulation
            var clearJob = new ClearNoiseBufferJob
            {
                ResultBuf = buffer.ResultBuf,
                GmapBuf = buffer.GmapBuf,
                BufSize = buffer.Sx * buffer.Sy,
                HasPersistenceMap = (byte)(hasPersistMap ? 1 : 0),
            };
            JobHandle clearHandle = clearJob.Schedule(dependency);

            float f = 1.0f;
            float g = 1.0f;
            JobHandle octaveHandle = clearHandle;

            for (int octave = 0; octave < np.Octaves; octave++)
            {
                int octaveSeed = mapSeed + np.Seed + octave;
                bool isLast = octave == np.Octaves - 1;

                // Phase 1: Fill integer-corner hash lattice
                var fillJob = new NoiseFillLatticeJob
                {
                    X = originX / np.Spread.x,
                    Y = originZ / np.Spread.z,
                    F = f,
                    SpreadX = np.Spread.x,
                    SpreadY = np.Spread.z,
                    Seed = octaveSeed,
                    Sx = buffer.Sx,
                    Sy = buffer.Sy,
                    NoiseBuf = buffer.NoiseBuf,
                    OutNlx = meta.Nlx,
                    OutNly = meta.Nly,
                    OutOrigU = meta.OrigU,
                    OutOrigV = meta.OrigV,
                    OutStepX = meta.StepX,
                    OutStepY = meta.StepY,
                };
                JobHandle fillHandle = fillJob.Schedule(octaveHandle);

                // Phase 2: Interpolate rows in parallel
                var interpJob = new NoiseInterpolateRowsJob2D
                {
                    NoiseBuf = buffer.NoiseBuf,
                    ValueBuf = buffer.ValueBuf,
                    Nlx = meta.Nlx,
                    OrigU = meta.OrigU,
                    OrigV = meta.OrigV,
                    StepX = meta.StepX,
                    StepY = meta.StepY,
                    Sx = buffer.Sx,
                    Eased = (byte)(np.IsEased2D ? 1 : 0),
                };
                JobHandle interpHandle = interpJob.Schedule(buffer.Sy, 1, fillHandle);

                // Phase 3: Accumulate octave into result buffer
                var accumulateJob = new NoiseAccumulateJob
                {
                    ValueBuf = buffer.ValueBuf,
                    ResultBuf = buffer.ResultBuf,
                    GmapBuf = buffer.GmapBuf,
                    PersistenceMap = hasPersistMap ? buffer.PersistenceMap : default,
                    G = g,
                    BufSize = buffer.Sx * buffer.Sy,
                    AbsValue = (byte)(np.IsAbsValue ? 1 : 0),
                    HasPersistence = (byte)(hasPersistMap ? 1 : 0),
                    IsLastOctave = (byte)(isLast ? 1 : 0),
                    Offset = np.Offset,
                    Scale = np.Scale,
                };
                octaveHandle = accumulateJob.Schedule(interpHandle);

                f *= np.Lacunarity;
                g *= np.Persist;
            }

            return octaveHandle;
        }

        /// <summary>
        /// Schedules the full three-phase 3D noise pipeline for one noise channel.
        /// </summary>
        private static JobHandle ScheduleNoiseMap3D(
            NoiseBuffer3D buffer,
            NoiseLatticeMetadata3D meta,
            in NoiseParams np,
            float originX, float originY, float originZ,
            int mapSeed,
            JobHandle dependency)
        {
            var clearJob = new ClearNoiseBufferJob
            {
                ResultBuf = buffer.ResultBuf,
                GmapBuf = buffer.GmapBuf,
                BufSize = buffer.Sx * buffer.Sy * buffer.Sz,
                HasPersistenceMap = 0,
            };
            JobHandle clearHandle = clearJob.Schedule(dependency);

            float f = 1.0f;
            float g = 1.0f;
            JobHandle octaveHandle = clearHandle;

            for (int octave = 0; octave < np.Octaves; octave++)
            {
                int octaveSeed = mapSeed + np.Seed + octave;
                bool isLast = octave == np.Octaves - 1;

                // Phase 1: Fill 3D integer-corner hash lattice
                var fillJob = new NoiseFillLatticeJob3D
                {
                    X = originX / np.Spread.x,
                    Y = originY / np.Spread.y,
                    Z = originZ / np.Spread.z,
                    F = f,
                    SpreadX = np.Spread.x,
                    SpreadY = np.Spread.y,
                    SpreadZ = np.Spread.z,
                    Seed = octaveSeed,
                    Sx = buffer.Sx,
                    Sy = buffer.Sy,
                    Sz = buffer.Sz,
                    NoiseBuf = buffer.NoiseBuf,
                    OutNlx = meta.Nlx,
                    OutNly = meta.Nly,
                    OutNlz = meta.Nlz,
                    OutOrigU = meta.OrigU,
                    OutOrigV = meta.OrigV,
                    OutOrigW = meta.OrigW,
                    OutStepX = meta.StepX,
                    OutStepY = meta.StepY,
                    OutStepZ = meta.StepZ,
                };
                JobHandle fillHandle = fillJob.Schedule(octaveHandle);

                // Phase 2: Interpolate planes in parallel
                var interpJob = new NoiseInterpolatePlanesJob3D
                {
                    NoiseBuf = buffer.NoiseBuf,
                    ValueBuf = buffer.ValueBuf,
                    Nlx = meta.Nlx,
                    Nly = meta.Nly,
                    OrigU = meta.OrigU,
                    OrigV = meta.OrigV,
                    OrigW = meta.OrigW,
                    StepX = meta.StepX,
                    StepY = meta.StepY,
                    StepZ = meta.StepZ,
                    Sx = buffer.Sx,
                    Sy = buffer.Sy,
                    Eased = (byte)(np.IsEased3D ? 1 : 0),
                };
                JobHandle interpHandle = interpJob.Schedule(buffer.Sz, 1, fillHandle);

                // Phase 3: Accumulate octave
                var accumulateJob = new NoiseAccumulateJob
                {
                    ValueBuf = buffer.ValueBuf,
                    ResultBuf = buffer.ResultBuf,
                    GmapBuf = buffer.GmapBuf,
                    PersistenceMap = default,
                    G = g,
                    BufSize = buffer.Sx * buffer.Sy * buffer.Sz,
                    AbsValue = (byte)(np.IsAbsValue ? 1 : 0),
                    HasPersistence = 0,
                    IsLastOctave = (byte)(isLast ? 1 : 0),
                    Offset = np.Offset,
                    Scale = np.Scale,
                };
                octaveHandle = accumulateJob.Schedule(interpHandle);

                f *= np.Lacunarity;
                g *= np.Persist;
            }

            return octaveHandle;
        }

        /// <summary>
        /// Disposes all persistent noise buffers.
        /// </summary>
        public void Dispose()
        {
            _channels?.Dispose();
            _channels = null;
        }
    }
}
