using System;
using Unity.Collections;
using Unity.Jobs;
using Unity.Mathematics;

namespace Basalt.WorldGen
{
    /// <summary>
    /// Orchestrator for Mapgen Flat terrain generation.
    /// Owns the persistent terrain noise buffer and schedules the noise + terrain job pipeline.
    /// </summary>
    /// <remarks>
    /// Lifecycle:
    ///   1. Construct once per world with <see cref="MapgenFlat(int)"/> or <see cref="MapgenFlat(MapgenFlatParams)"/>.
    ///   2. Call <see cref="Initialize"/> with resolved content IDs from NodeRegistry.
    ///   3. Per mapchunk: call <see cref="Generate"/> to receive a <see cref="JobHandle"/>.
    ///   4. Caller completes the handle (next frame or on demand), then reads the VoxelManipulator.
    ///   5. Dispose the VoxelManipulator when the caller has extracted MapBlock data.
    ///   6. Dispose this instance when the world unloads.
    ///
    /// No MonoBehaviour — lives in Basalt.WorldGen. Callers in Basalt.Server or Basalt.Client
    /// manage the lifetime.
    ///
    /// Source: <c>luanti/src/mapgen/mapgen_flat.cpp</c>.
    /// </remarks>
    public sealed class MapgenFlat : IMapgen
    {
        private MapgenFlatParams _params;
        private MapgenFlatNoiseChannels _channels;
        private JobHandle _lastHandle;
        private bool _initialized;
        private bool _useNoise;

        /// <summary>Gets the current generation parameters.</summary>
        public MapgenFlatParams Params => _params;

        /// <summary>
        /// Creates a new Mapgen Flat instance with default parameters.
        /// Call <see cref="Initialize"/> before the first <see cref="Generate"/> call.
        /// </summary>
        /// <param name="seed">The world seed.</param>
        public MapgenFlat(int seed)
        {
            _params = MapgenFlatParams.CreateDefault(seed);
            _channels = new MapgenFlatNoiseChannels();
            _useNoise = _params.EnableLakes != 0 || _params.EnableHills != 0;
        }

        /// <summary>
        /// Creates a new Mapgen Flat instance with custom parameters.
        /// Call <see cref="Initialize"/> before the first <see cref="Generate"/> call.
        /// </summary>
        public MapgenFlat(MapgenFlatParams parameters)
        {
            _params = parameters;
            _channels = new MapgenFlatNoiseChannels();
            _useNoise = _params.EnableLakes != 0 || _params.EnableHills != 0;
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
        /// Schedules the terrain fill pipeline for one mapchunk.
        /// Returns a <see cref="JobHandle"/> that the caller completes at its own pace.
        /// </summary>
        /// <param name="chunkOrigin">
        /// World position of the mapchunk's minimum corner node (not counting overgeneration).
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
                    "MapgenFlat.Initialize() must be called before Generate().");
            }

            vm = new VoxelManipulator(chunkOrigin, vmAllocator);

            // Ensure the previous generation's jobs are complete before reusing
            // the shared persistent noise buffers.
            JobHandle previousHandle = _lastHandle;

            // Schedule terrain noise only when lakes or hills are enabled.
            // Luanti: mapgen_flat.cpp line 285 — noise_terrain->noiseMap2D only if use_noise
            JobHandle noiseHandle;
            if (_useNoise)
            {
                float originX = chunkOrigin.x;
                float originZ = chunkOrigin.z;

                noiseHandle = NoiseMapScheduler.ScheduleMap2D(
                    _channels.Terrain, _channels.MetaTerrain,
                    in _params.Terrain, originX, originZ, _params.Seed,
                    previousHandle);
            }
            else
            {
                noiseHandle = previousHandle;
            }

            var terrainJob = new MapgenFlatTerrainJob
            {
                TerrainResult = _channels.Terrain.ResultBuf,
                Nodes = vm.Nodes,
                Heightmap = vm.Heightmap,
                Params = _params,
                MinY = vm.MinPos.y,
                MaxY = vm.MaxPos.y,
            };

            _lastHandle = terrainJob.Schedule(noiseHandle);

            return _lastHandle;
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
