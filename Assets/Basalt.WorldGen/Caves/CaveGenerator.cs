using System;
using Basalt.Core;
using Unity.Collections;
using Unity.Jobs;
using Unity.Mathematics;

namespace Basalt.WorldGen
{
    /// <summary>
    /// Orchestrates the full cave generation pipeline: noise intersection caves,
    /// caverns, and random walk caves. Owns persistent noise buffers and the
    /// near-cavern cross-job signal.
    /// </summary>
    /// <remarks>
    /// Owned by <see cref="MapgenFeatures"/>. Called from
    /// <see cref="MapgenFeatures.ScheduleFeatures"/> before the biome stage,
    /// matching Luanti's MapgenV7::makeChunk() order (lines 332-351).
    ///
    /// Pipeline:
    ///   1. Schedule Cave1 + Cave2 + Cavern 3D noise maps in parallel
    ///   2. CavesNoiseIntersectionJob (depends on Cave1 + Cave2)
    ///   3. CavernsNoiseJob (depends on Cavern + step 2, writes NearCavern)
    ///   4. CavesRandomWalkJob (depends on step 3, reads NearCavern)
    ///
    /// Source: <c>luanti/src/mapgen/mapgen.cpp</c> — generateCavesNoiseIntersection(),
    /// generateCavernsNoise(), generateCavesRandomWalk().
    /// </remarks>
    public sealed class CaveGenerator : IDisposable
    {
        private CaveNoiseChannels _channels;
        private NativeArray<byte> _nearCavernResult;
        private CaveParams _params;
        private NativeArray<NodeDefinition> _nodeDefs;
        private JobHandle _lastHandle;
        private bool _initialized;

        public CaveGenerator(int seed)
        {
            _params = CaveParams.CreateDefault(seed);
            _channels = new CaveNoiseChannels();
            _nearCavernResult = new NativeArray<byte>(1, Allocator.Persistent);
        }

        /// <summary>
        /// Binds content IDs and node definitions. Call once after registries are baked.
        /// </summary>
        public void Initialize(
            NativeArray<NodeDefinition> nodeDefs,
            ushort contentAir,
            ushort contentWater,
            ushort contentStone,
            int waterLevel)
        {
            _nodeDefs = nodeDefs;
            _params.ContentAir = contentAir;
            _params.ContentWater = contentWater;
            _params.ContentStone = contentStone;
            _params.WaterLevel = waterLevel;
            _initialized = true;
        }

        /// <summary>
        /// Schedules the full cave generation pipeline for one mapchunk.
        /// </summary>
        /// <param name="chunkOrigin">World-space min corner of the mapchunk.</param>
        /// <param name="vm">The VoxelManipulator populated by the terrain job.</param>
        /// <param name="stoneSurfaceMaxY">Conservative upper bound for stone surface Y.</param>
        /// <param name="blockseed">Per-mapchunk deterministic seed.</param>
        /// <param name="dependency">JobHandle from the terrain generation pass.</param>
        /// <returns>Final JobHandle encompassing all cave passes.</returns>
        public JobHandle ScheduleCaves(
            int3 chunkOrigin,
            VoxelManipulator vm,
            int stoneSurfaceMaxY,
            uint blockseed,
            JobHandle dependency)
        {
            if (!_initialized)
            {
                throw new InvalidOperationException("CaveGenerator.Initialize() must be called first.");
            }

            // Ensure previous generation's jobs complete before reusing persistent buffers
            JobHandle previousHandle = _lastHandle;
            JobHandle combinedDep = JobHandle.CombineDependencies(dependency, previousHandle);

            float originX = chunkOrigin.x;
            float originZ = chunkOrigin.z;
            // 3D noise Y origin includes overgeneration (same as terrain noise)
            float originY = chunkOrigin.y - MapgenV7Constants.OVERGEN_SIZE;

            int3 nmin = chunkOrigin;
            int3 nmax = new int3(
                chunkOrigin.x + MapgenV7Constants.MAPCHUNK_SIZE - 1,
                chunkOrigin.y + MapgenV7Constants.MAPCHUNK_SIZE - 1,
                chunkOrigin.z + MapgenV7Constants.MAPCHUNK_SIZE - 1);

            // ---- Stage 1: Schedule 3 noise maps in parallel ----
            JobHandle cave1Handle = NoiseMapScheduler.ScheduleMap3D(
                _channels.Cave1, _channels.MetaCave1,
                in _params.NpCave1, originX, originY, originZ, _params.Seed,
                combinedDep);

            JobHandle cave2Handle = NoiseMapScheduler.ScheduleMap3D(
                _channels.Cave2, _channels.MetaCave2,
                in _params.NpCave2, originX, originY, originZ, _params.Seed,
                combinedDep);

            JobHandle cavernHandle = default;
            if (_params.EnableCaverns != 0)
            {
                cavernHandle = NoiseMapScheduler.ScheduleMap3D(
                    _channels.Cavern, _channels.MetaCavern,
                    in _params.NpCavern, originX, originY, originZ, _params.Seed,
                    combinedDep);
            }

            // ---- Stage 2: CavesNoiseIntersection (depends on cave1 + cave2 + terrain) ----
            JobHandle noiseIntersectionDep = JobHandle.CombineDependencies(
                cave1Handle, cave2Handle, dependency);

            var noiseIntersectionJob = new CavesNoiseIntersectionJob
            {
                Nodes = vm.Nodes,
                Cave1Result = _channels.Cave1.ResultBuf,
                Cave2Result = _channels.Cave2.ResultBuf,
                NodeDefs = _nodeDefs,
                VmMinPos = vm.MinPos,
                VmMaxPos = vm.MaxPos,
                Nmin = nmin,
                Nmax = nmax,
                CaveWidth = _params.CaveWidth,
                ContentAir = _params.ContentAir,
                StoneSurfaceMaxY = stoneSurfaceMaxY,
            };
            JobHandle noiseIntersectionHandle = noiseIntersectionJob.Schedule(noiseIntersectionDep);

            // ---- Stage 3: CavernsNoise (depends on cavern noise + noise intersection) ----
            JobHandle cavernsHandle;
            if (_params.EnableCaverns != 0)
            {
                JobHandle cavernsDep = JobHandle.CombineDependencies(
                    cavernHandle, noiseIntersectionHandle);

                var cavernsJob = new CavernsNoiseJob
                {
                    Nodes = vm.Nodes,
                    CavernResult = _channels.Cavern.ResultBuf,
                    NodeDefs = _nodeDefs,
                    NearCavernOut = _nearCavernResult,
                    VmMinPos = vm.MinPos,
                    VmMaxPos = vm.MaxPos,
                    Nmin = nmin,
                    Nmax = nmax,
                    CavernLimit = _params.CavernLimit,
                    CavernTaper = _params.CavernTaper,
                    CavernThreshold = _params.CavernThreshold,
                    ContentAir = _params.ContentAir,
                    StoneSurfaceMaxY = stoneSurfaceMaxY,
                };
                cavernsHandle = cavernsJob.Schedule(cavernsDep);
            }
            else
            {
                // No caverns: clear the near-cavern flag and chain after noise intersection
                _nearCavernResult[0] = 0;
                cavernsHandle = noiseIntersectionHandle;
            }

            // ---- Stage 4: CavesRandomWalk (depends on caverns) ----
            var randomWalkJob = new CavesRandomWalkJob
            {
                Nodes = vm.Nodes,
                Heightmap = vm.Heightmap,
                NodeDefs = _nodeDefs,
                NearCavern = _nearCavernResult,
                VmMinPos = vm.MinPos,
                VmMaxPos = vm.MaxPos,
                Nmin = nmin,
                Nmax = nmax,
                LargeCaveDepth = _params.LargeCaveDepth,
                SmallCaveNumMin = _params.SmallCaveNumMin,
                SmallCaveNumMax = _params.SmallCaveNumMax,
                LargeCaveNumMin = _params.LargeCaveNumMin,
                LargeCaveNumMax = _params.LargeCaveNumMax,
                LargeCaveFlooded = _params.LargeCaveFlooded,
                Blockseed = blockseed,
                MapSeed = _params.Seed,
                ContentAir = _params.ContentAir,
                ContentWater = _params.ContentWater,
                WaterLevel = _params.WaterLevel,
                StoneSurfaceMaxY = stoneSurfaceMaxY,
            };
            _lastHandle = randomWalkJob.Schedule(cavernsHandle);

            return _lastHandle;
        }

        public void Dispose()
        {
            _lastHandle.Complete();

            _channels?.Dispose();
            _channels = null;

            if (_nearCavernResult.IsCreated)
            {
                _nearCavernResult.Dispose();
            }
        }
    }
}