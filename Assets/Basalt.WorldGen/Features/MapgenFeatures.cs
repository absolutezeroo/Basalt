using System;
using Basalt.Core;
using Unity.Collections;
using Unity.Jobs;
using Unity.Mathematics;

namespace Basalt.WorldGen
{
    /// <summary>
    /// Orchestrates the full post-terrain feature pipeline: caves → biome noise → biome surface
    /// replacement → ore placement → decoration placement → dust top nodes.
    /// </summary>
    /// <remarks>
    /// Owned by the mapgen. Call <see cref="Initialize"/> after registries are baked,
    /// then <see cref="ScheduleFeatures"/> after each terrain job to chain the feature passes.
    ///
    /// Pipeline: terrain → caves (noise intersection + caverns + random walk) → biome noise
    ///           (4x 2D) → combine → GenerateBiomes → PlaceAllOres → PlaceAllDecorations
    ///           → DustTopNodes
    /// </remarks>
    public sealed class MapgenFeatures : IDisposable
    {
        private MapgenFeaturesNoiseChannels _channels;
        private CaveGenerator _caveGenerator;
        private bool _initialized;

        // Noise params (Luanti defaults)
        private NoiseParams _npHeat;
        private NoiseParams _npHumidity;
        private NoiseParams _npHeatBlend;
        private NoiseParams _npHumidityBlend;
        private NoiseParams _npFillerDepth;

        // Baked registry data (not owned, just referenced)
        private NativeArray<BiomeDefRuntime> _biomes;
        private NativeArray<OreDefRuntime> _ores;
        private NativeArray<ushort> _oreWherein;
        private NativeArray<ushort> _oreBiomes;
        private NativeArray<DecorationDefRuntime> _decos;
        private NativeArray<ushort> _decoPlaceOn;
        private NativeArray<ushort> _decoBiomes;
        private NativeArray<ushort> _decoSpawnBy;
        private NativeArray<ushort> _decoDecos;
        private NativeArray<SchematicData> _schematics;
        private NativeArray<uint> _schematicNodes;
        private NativeArray<byte> _schematicProbs;
        private NativeArray<byte> _schematicSliceProbs;
        private NativeArray<NodeDefinition> _nodeDefs;

        private int _biomeCount;
        private int _oreCount;
        private int _decoCount;

        private ushort _contentStone;
        private ushort _contentWater;
        private ushort _contentRiverWater;
        private int _waterLevel;
        private int _seed;

        public MapgenFeatures(int seed)
        {
            _seed = seed;
            _channels = new MapgenFeaturesNoiseChannels();
            _caveGenerator = new CaveGenerator(seed);

            // Luanti BiomeParamsOriginal defaults
            _npHeat = new NoiseParams(50f, 50f, new float3(1000f, 1000f, 1000f), 5349, 3, 0.5f, 2.0f);
            _npHumidity = new NoiseParams(50f, 50f, new float3(1000f, 1000f, 1000f), 842, 3, 0.5f, 2.0f);
            _npHeatBlend = new NoiseParams(0f, 1.5f, new float3(8f, 8f, 8f), 13, 2, 1.0f, 2.0f);
            _npHumidityBlend = new NoiseParams(0f, 1.5f, new float3(8f, 8f, 8f), 90003, 2, 1.0f, 2.0f);
            _npFillerDepth = new NoiseParams(0f, 1.2f, new float3(150f, 150f, 150f), 261, 3, 0.7f, 2.0f);
        }

        /// <summary>
        /// Binds baked registry data and content IDs. Call once after all registries are baked.
        /// </summary>
        public void Initialize(
            BiomeRegistry biomeRegistry,
            OreRegistry oreRegistry,
            DecorationRegistry decoRegistry,
            NodeRegistry nodeRegistry,
            ushort contentStone,
            ushort contentWater,
            ushort contentRiverWater,
            int waterLevel)
        {
            _biomes = biomeRegistry.RuntimeDefs;
            _biomeCount = biomeRegistry.Count;

            _ores = oreRegistry.RuntimeDefs;
            _oreWherein = oreRegistry.WhereinFlat;
            _oreBiomes = oreRegistry.BiomesFlat;
            _oreCount = oreRegistry.Count;

            _decos = decoRegistry.RuntimeDefs;
            _decoPlaceOn = decoRegistry.PlaceOnFlat;
            _decoBiomes = decoRegistry.BiomesFlat;
            _decoSpawnBy = decoRegistry.SpawnByFlat;
            _decoDecos = decoRegistry.DecosFlat;
            _schematics = decoRegistry.RuntimeSchematics;
            _schematicNodes = decoRegistry.SchematicNodesFlat;
            _schematicProbs = decoRegistry.SchematicProbsFlat;
            _schematicSliceProbs = decoRegistry.SchematicSliceProbsFlat;
            _decoCount = decoRegistry.Count;

            _nodeDefs = nodeRegistry.RuntimeDefsNative;

            _contentStone = contentStone;
            _contentWater = contentWater;
            _contentRiverWater = contentRiverWater;
            _waterLevel = waterLevel;

            _caveGenerator.Initialize(_nodeDefs, BasaltConstants.CONTENT_AIR,
                contentWater, contentStone, waterLevel);

            _initialized = true;
        }

        /// <summary>
        /// Schedules the full feature pipeline for one mapchunk, chained after the terrain job.
        /// Returns the final JobHandle encompassing all feature passes.
        /// </summary>
        /// <param name="chunkOrigin">World-space min corner of the mapchunk.</param>
        /// <param name="vm">The VoxelManipulator populated by the terrain job.</param>
        /// <param name="terrainHandle">JobHandle from the terrain generation pass.</param>
        /// <returns>Final JobHandle. Complete before reading vm.</returns>
        public JobHandle ScheduleFeatures(int3 chunkOrigin, VoxelManipulator vm, JobHandle terrainHandle)
        {
            if (!_initialized)
            {
                throw new InvalidOperationException("MapgenFeatures.Initialize() must be called first.");
            }

            // Each MapgenFeatures instance is used by exactly one mapgen from the pool,
            // so no previous-handle serialization is needed — the pool guarantees
            // this instance is idle before Generate() is called again.
            JobHandle combinedDep = terrainHandle;

            float originX = chunkOrigin.x;
            float originZ = chunkOrigin.z;

            const int bufSize = MapgenV7Constants.MAPCHUNK_SIZE * MapgenV7Constants.MAPCHUNK_SIZE;

            // ---- Stage 1: Schedule 5 noise maps (heat, humidity, blends, filler_depth) ----
            JobHandle heatHandle = NoiseMapScheduler.ScheduleMap2D(
                _channels.Heat,
                in _npHeat, originX, originZ, _seed, combinedDep);

            JobHandle humidityHandle = NoiseMapScheduler.ScheduleMap2D(
                _channels.Humidity,
                in _npHumidity, originX, originZ, _seed, combinedDep);

            JobHandle heatBlendHandle = NoiseMapScheduler.ScheduleMap2D(
                _channels.HeatBlend,
                in _npHeatBlend, originX, originZ, _seed, combinedDep);

            JobHandle humidityBlendHandle = NoiseMapScheduler.ScheduleMap2D(
                _channels.HumidityBlend,
                in _npHumidityBlend, originX, originZ, _seed, combinedDep);

            JobHandle fillerHandle = NoiseMapScheduler.ScheduleMap2D(
                _channels.FillerDepth,
                in _npFillerDepth, originX, originZ, _seed, combinedDep);

            // ---- Stage 1b: Schedule cave generation (runs in parallel with biome noise) ----
            // Caves must complete before biomes, matching Luanti's pipeline order.
            int3 nmin = new int3(chunkOrigin.x, chunkOrigin.y, chunkOrigin.z);
            int3 nmax = new int3(
                chunkOrigin.x + MapgenV7Constants.MAPCHUNK_SIZE - 1,
                chunkOrigin.y + MapgenV7Constants.MAPCHUNK_SIZE - 1,
                chunkOrigin.z + MapgenV7Constants.MAPCHUNK_SIZE - 1);

            uint blockseed = BlockSeedUtil.GetBlockSeed2(nmin, _seed);

            JobHandle caveHandle = _caveGenerator.ScheduleCaves(
                chunkOrigin, vm, blockseed, combinedDep);

            // ---- Stage 2: Combine heat+blend, humidity+blend ----
            JobHandle noiseReady = JobHandle.CombineDependencies(
                JobHandle.CombineDependencies(heatHandle, humidityHandle, heatBlendHandle),
                humidityBlendHandle);

            var combineJob = new BiomeNoiseJob
            {
                HeatResult = _channels.Heat.ResultBuf,
                HumidityResult = _channels.Humidity.ResultBuf,
                HeatBlendResult = _channels.HeatBlend.ResultBuf,
                HumidityBlendResult = _channels.HumidityBlend.ResultBuf,
                Heatmap = _channels.Heatmap,
                Humidmap = _channels.Humidmap,
                BufSize = bufSize,
            };
            JobHandle combineHandle = combineJob.Schedule(noiseReady);

            // ---- Stage 3: GenerateBiomes (needs caves + combined noise + filler_depth) ----
            JobHandle biomeDep = JobHandle.CombineDependencies(
                JobHandle.CombineDependencies(combineHandle, fillerHandle, terrainHandle),
                caveHandle);

            var biomesJob = new GenerateBiomesJob
            {
                Nodes = vm.Nodes,
                Heightmap = vm.Heightmap,
                Heatmap = _channels.Heatmap,
                Humidmap = _channels.Humidmap,
                FillerDepthResult = _channels.FillerDepth.ResultBuf,
                Biomemap = _channels.Biomemap,
                Biomes = _biomes,
                BiomeCount = _biomeCount,
                ContentStone = _contentStone,
                ContentWater = _contentWater,
                ContentRiverWater = _contentRiverWater,
                WaterLevel = _waterLevel,
                NodeMinX = vm.MinPos.x,
                NodeMinY = vm.MinPos.y,
                NodeMinZ = vm.MinPos.z,
                NodeMaxY = vm.MaxPos.y,
            };
            JobHandle biomesHandle = biomesJob.Schedule(biomeDep);

            // ---- Stage 4: PlaceAllOres ----
            var oresJob = new PlaceAllOresJob
            {
                Nodes = vm.Nodes,
                Biomemap = _channels.Biomemap,
                Ores = _ores,
                OreCount = _oreCount,
                Wherein = _oreWherein,
                OreBiomes = _oreBiomes,
                VmMinPos = vm.MinPos,
                VmMaxPos = vm.MaxPos,
                Nmin = nmin,
                Nmax = nmax,
                MapSeed = _seed,
                Blockseed = blockseed,
            };
            JobHandle oresHandle = oresJob.Schedule(biomesHandle);

            // ---- Stage 5: PlaceAllDecorations ----
            var decosJob = new PlaceAllDecorationsJob
            {
                Nodes = vm.Nodes,
                Heightmap = vm.Heightmap,
                Biomemap = _channels.Biomemap,
                Decos = _decos,
                DecoCount = _decoCount,
                PlaceOn = _decoPlaceOn,
                DecoBiomes = _decoBiomes,
                SpawnBy = _decoSpawnBy,
                DecoContents = _decoDecos,
                Schematics = _schematics,
                SchematicNodes = _schematicNodes,
                SchematicProbs = _schematicProbs,
                SchematicSliceProbs = _schematicSliceProbs,
                VmMinPos = vm.MinPos,
                VmMaxPos = vm.MaxPos,
                Nmin = nmin,
                Nmax = nmax,
                MapSeed = _seed,
                Blockseed = blockseed,
            };
            JobHandle decosHandle = decosJob.Schedule(oresHandle);

            // ---- Stage 6: DustTopNodes ----
            var dustJob = new DustTopNodesJob
            {
                Nodes = vm.Nodes,
                Biomemap = _channels.Biomemap,
                Biomes = _biomes,
                BiomeCount = _biomeCount,
                NodeDefs = _nodeDefs,
                VmMinPos = vm.MinPos,
                VmMaxPos = vm.MaxPos,
                WaterLevel = _waterLevel,
            };
            JobHandle dustHandle = dustJob.Schedule(decosHandle);

            return dustHandle;
        }

        public void Dispose()
        {
            _caveGenerator?.Dispose();
            _caveGenerator = null;

            _channels?.Dispose();
            _channels = null;
        }
    }
}
