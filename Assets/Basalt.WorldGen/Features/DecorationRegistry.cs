using System;
using System.Collections.Generic;
using Basalt.Core;
using Unity.Collections;

namespace Basalt.WorldGen
{
    /// <summary>
    /// Registry for decoration definitions. Register decorations by name, then Bake()
    /// to produce NativeArrays for Burst job access. Variable-length data is flat-packed.
    /// </summary>
    public class DecorationRegistry : IDisposable
    {
        private readonly List<string> _names = new();
        private readonly List<DecorationDef> _defs = new();
        private readonly List<SchematicData> _schematics = new();
        private readonly List<uint> _schematicNodes = new();
        private readonly List<byte> _schematicProbs = new();
        private readonly List<byte> _schematicSliceProbs = new();

        private NativeArray<DecorationDefRuntime> _runtimeDefs;
        private NativeArray<ushort> _placeOnFlat;
        private NativeArray<ushort> _biomesFlat;
        private NativeArray<ushort> _spawnByFlat;
        private NativeArray<ushort> _decosFlat;
        private NativeArray<SchematicData> _runtimeSchematics;
        private NativeArray<uint> _schematicNodesFlat;
        private NativeArray<byte> _schematicProbsFlat;
        private NativeArray<byte> _schematicSliceProbsFlat;
        private bool _baked;

        /// <summary>
        /// Registers a new decoration definition. Must be called before Bake().
        /// </summary>
        public void Register(string name, DecorationDef def)
        {
            if (_baked)
                throw new InvalidOperationException("Cannot register decorations after Bake().");

            _names.Add(name);
            _defs.Add(def);
        }

        /// <summary>
        /// Registers schematic data for use by Schematic-type decorations.
        /// Returns the schematic index to assign to <see cref="DecorationDef.SchematicIndex"/>.
        /// </summary>
        public int RegisterSchematic(SchematicData data, uint[] nodes, byte[] probs, byte[] sliceProbs)
        {
            if (_baked)
                throw new InvalidOperationException("Cannot register schematics after Bake().");

            int index = _schematics.Count;

            data.NodesOffset = _schematicNodes.Count;
            data.NodesCount = nodes.Length;
            data.ProbsOffset = _schematicProbs.Count;
            data.SliceProbsOffset = _schematicSliceProbs.Count;
            data.SliceProbsCount = sliceProbs.Length;

            _schematics.Add(data);
            _schematicNodes.AddRange(nodes);
            _schematicProbs.AddRange(probs);
            _schematicSliceProbs.AddRange(sliceProbs);

            return index;
        }

        /// <summary>
        /// Bakes all registered decorations into NativeArrays, resolving names.
        /// </summary>
        public void Bake(NodeRegistry nodeRegistry, BiomeRegistry biomeRegistry)
        {
            if (_baked)
                return;

            int totalPlaceOn = 0;
            int totalBiomes = 0;
            int totalSpawnBy = 0;
            int totalDecos = 0;

            for (int i = 0; i < _defs.Count; i++)
            {
                totalPlaceOn += _defs[i].PlaceOn.Count;
                totalBiomes += _defs[i].Biomes.Count;
                totalSpawnBy += _defs[i].SpawnBy.Count;
                totalDecos += _defs[i].Decorations.Count;
            }

            _runtimeDefs = new NativeArray<DecorationDefRuntime>(
                Math.Max(_defs.Count, 1), Allocator.Persistent, NativeArrayOptions.UninitializedMemory);
            _placeOnFlat = new NativeArray<ushort>(
                Math.Max(totalPlaceOn, 1), Allocator.Persistent, NativeArrayOptions.UninitializedMemory);
            _biomesFlat = new NativeArray<ushort>(
                Math.Max(totalBiomes, 1), Allocator.Persistent, NativeArrayOptions.UninitializedMemory);
            _spawnByFlat = new NativeArray<ushort>(
                Math.Max(totalSpawnBy, 1), Allocator.Persistent, NativeArrayOptions.UninitializedMemory);
            _decosFlat = new NativeArray<ushort>(
                Math.Max(totalDecos, 1), Allocator.Persistent, NativeArrayOptions.UninitializedMemory);

            int placeOnCursor = 0;
            int biomesCursor = 0;
            int spawnByCursor = 0;
            int decosCursor = 0;

            for (int i = 0; i < _defs.Count; i++)
            {
                DecorationDef def = _defs[i];

                int poOffset = placeOnCursor;
                for (int j = 0; j < def.PlaceOn.Count; j++)
                    _placeOnFlat[placeOnCursor++] = nodeRegistry.GetIdByName(def.PlaceOn[j]);

                int bOffset = biomesCursor;
                for (int j = 0; j < def.Biomes.Count; j++)
                    _biomesFlat[biomesCursor++] = biomeRegistry.GetIndexByName(def.Biomes[j]);

                int sbOffset = spawnByCursor;
                for (int j = 0; j < def.SpawnBy.Count; j++)
                    _spawnByFlat[spawnByCursor++] = nodeRegistry.GetIdByName(def.SpawnBy[j]);

                int dOffset = decosCursor;
                for (int j = 0; j < def.Decorations.Count; j++)
                    _decosFlat[decosCursor++] = nodeRegistry.GetIdByName(def.Decorations[j]);

                _runtimeDefs[i] = new DecorationDefRuntime
                {
                    Type = def.Type,
                    Flags = def.Flags,
                    PlaceOnOffset = poOffset,
                    PlaceOnCount = def.PlaceOn.Count,
                    BiomesOffset = bOffset,
                    BiomesCount = def.Biomes.Count,
                    SpawnByOffset = sbOffset,
                    SpawnByCount = def.SpawnBy.Count,
                    Sidelen = def.Sidelen,
                    YMin = def.YMin,
                    YMax = def.YMax,
                    FillRatio = def.FillRatio,
                    NoiseParams = def.NoiseParams,
                    NSpawnBy = def.NSpawnBy,
                    PlaceOffsetY = def.PlaceOffsetY,
                    DecosOffset = dOffset,
                    DecosCount = def.Decorations.Count,
                    DecoHeight = def.DecoHeight,
                    DecoHeightMax = def.DecoHeightMax,
                    DecoParam2 = def.DecoParam2,
                    DecoParam2Max = def.DecoParam2Max,
                    SchematicIndex = def.SchematicIndex,
                    Rotation = def.Rotation,
                };
            }

            // Bake schematics
            _runtimeSchematics = new NativeArray<SchematicData>(
                Math.Max(_schematics.Count, 1), Allocator.Persistent, NativeArrayOptions.UninitializedMemory);
            for (int i = 0; i < _schematics.Count; i++)
                _runtimeSchematics[i] = _schematics[i];

            _schematicNodesFlat = new NativeArray<uint>(
                Math.Max(_schematicNodes.Count, 1), Allocator.Persistent, NativeArrayOptions.UninitializedMemory);
            for (int i = 0; i < _schematicNodes.Count; i++)
                _schematicNodesFlat[i] = _schematicNodes[i];

            _schematicProbsFlat = new NativeArray<byte>(
                Math.Max(_schematicProbs.Count, 1), Allocator.Persistent, NativeArrayOptions.UninitializedMemory);
            for (int i = 0; i < _schematicProbs.Count; i++)
                _schematicProbsFlat[i] = _schematicProbs[i];

            _schematicSliceProbsFlat = new NativeArray<byte>(
                Math.Max(_schematicSliceProbs.Count, 1), Allocator.Persistent, NativeArrayOptions.UninitializedMemory);
            for (int i = 0; i < _schematicSliceProbs.Count; i++)
                _schematicSliceProbsFlat[i] = _schematicSliceProbs[i];

            _baked = true;
        }

        /// <summary>Gets the number of registered decorations.</summary>
        public int Count => _defs.Count;

        public NativeArray<DecorationDefRuntime> RuntimeDefs =>
            _baked ? _runtimeDefs : throw new InvalidOperationException("Call Bake() first.");

        public NativeArray<ushort> PlaceOnFlat =>
            _baked ? _placeOnFlat : throw new InvalidOperationException("Call Bake() first.");

        public NativeArray<ushort> BiomesFlat =>
            _baked ? _biomesFlat : throw new InvalidOperationException("Call Bake() first.");

        public NativeArray<ushort> SpawnByFlat =>
            _baked ? _spawnByFlat : throw new InvalidOperationException("Call Bake() first.");

        public NativeArray<ushort> DecosFlat =>
            _baked ? _decosFlat : throw new InvalidOperationException("Call Bake() first.");

        public NativeArray<SchematicData> RuntimeSchematics =>
            _baked ? _runtimeSchematics : throw new InvalidOperationException("Call Bake() first.");

        public NativeArray<uint> SchematicNodesFlat =>
            _baked ? _schematicNodesFlat : throw new InvalidOperationException("Call Bake() first.");

        public NativeArray<byte> SchematicProbsFlat =>
            _baked ? _schematicProbsFlat : throw new InvalidOperationException("Call Bake() first.");

        public NativeArray<byte> SchematicSliceProbsFlat =>
            _baked ? _schematicSliceProbsFlat : throw new InvalidOperationException("Call Bake() first.");

        public void Dispose()
        {
            if (_runtimeDefs.IsCreated) _runtimeDefs.Dispose();
            if (_placeOnFlat.IsCreated) _placeOnFlat.Dispose();
            if (_biomesFlat.IsCreated) _biomesFlat.Dispose();
            if (_spawnByFlat.IsCreated) _spawnByFlat.Dispose();
            if (_decosFlat.IsCreated) _decosFlat.Dispose();
            if (_runtimeSchematics.IsCreated) _runtimeSchematics.Dispose();
            if (_schematicNodesFlat.IsCreated) _schematicNodesFlat.Dispose();
            if (_schematicProbsFlat.IsCreated) _schematicProbsFlat.Dispose();
            if (_schematicSliceProbsFlat.IsCreated) _schematicSliceProbsFlat.Dispose();
        }
    }
}
