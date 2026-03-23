using System;
using System.Collections.Generic;
using Basalt.Core;
using Unity.Collections;

namespace Basalt.WorldGen
{
    /// <summary>
    /// Registry for ore definitions. Register ores by name, then Bake() to produce
    /// NativeArrays for Burst job access. Variable-length data (wherein, biomes) is
    /// flat-packed with offset+count indexing.
    /// </summary>
    public class OreRegistry : IDisposable
    {
        private readonly List<string> _names = new();
        private readonly List<OreDef> _defs = new();

        private NativeArray<OreDefRuntime> _runtimeDefs;
        private NativeArray<ushort> _whereinFlat;
        private NativeArray<ushort> _biomesFlat;
        private bool _baked;

        /// <summary>
        /// Registers a new ore definition. Must be called before Bake().
        /// </summary>
        public void Register(string name, OreDef def)
        {
            if (_baked)
                throw new InvalidOperationException("Cannot register ores after Bake().");

            _names.Add(name);
            _defs.Add(def);
        }

        /// <summary>
        /// Bakes all registered ores into NativeArrays, resolving names to content/biome IDs.
        /// </summary>
        public void Bake(NodeRegistry nodeRegistry, BiomeRegistry biomeRegistry)
        {
            if (_baked)
                return;

            // First pass: compute total flat array sizes
            int totalWherein = 0;
            int totalBiomes = 0;
            for (int i = 0; i < _defs.Count; i++)
            {
                totalWherein += _defs[i].NodeWherein.Count;
                totalBiomes += _defs[i].Biomes.Count;
            }

            _runtimeDefs = new NativeArray<OreDefRuntime>(
                Math.Max(_defs.Count, 1), Allocator.Persistent, NativeArrayOptions.UninitializedMemory);
            _whereinFlat = new NativeArray<ushort>(
                Math.Max(totalWherein, 1), Allocator.Persistent, NativeArrayOptions.UninitializedMemory);
            _biomesFlat = new NativeArray<ushort>(
                Math.Max(totalBiomes, 1), Allocator.Persistent, NativeArrayOptions.UninitializedMemory);

            int whereinCursor = 0;
            int biomesCursor = 0;

            for (int i = 0; i < _defs.Count; i++)
            {
                OreDef def = _defs[i];

                int wOffset = whereinCursor;
                for (int w = 0; w < def.NodeWherein.Count; w++)
                {
                    _whereinFlat[whereinCursor++] = nodeRegistry.GetIdByName(def.NodeWherein[w]);
                }

                int bOffset = biomesCursor;
                for (int b = 0; b < def.Biomes.Count; b++)
                {
                    _biomesFlat[biomesCursor++] = biomeRegistry.GetIndexByName(def.Biomes[b]);
                }

                _runtimeDefs[i] = new OreDefRuntime
                {
                    Type = def.Type,
                    ContentOre = nodeRegistry.GetIdByName(def.NodeOre),
                    OreParam2 = def.OreParam2,
                    Flags = def.Flags,
                    WhereinOffset = wOffset,
                    WhereinCount = def.NodeWherein.Count,
                    BiomesOffset = bOffset,
                    BiomesCount = def.Biomes.Count,
                    ClustScarcity = def.ClustScarcity,
                    ClustNumOres = def.ClustNumOres,
                    ClustSize = def.ClustSize,
                    YMin = def.YMin,
                    YMax = def.YMax,
                    NThresh = def.NThresh,
                    NoiseParams = def.NoiseParams,
                    ColumnHeightMin = def.ColumnHeightMin,
                    ColumnHeightMax = def.ColumnHeightMax,
                    ColumnMidpointFactor = def.ColumnMidpointFactor,
                    NoisePuffTop = def.NoisePuffTop,
                    NoisePuffBottom = def.NoisePuffBottom,
                    RandomFactor = def.RandomFactor,
                    NoiseStratumThickness = def.NoiseStratumThickness,
                    StratumThickness = def.StratumThickness,
                };
            }

            _baked = true;
        }

        /// <summary>Gets the number of registered ores.</summary>
        public int Count => _defs.Count;

        /// <summary>Returns the baked ore definitions array.</summary>
        public NativeArray<OreDefRuntime> RuntimeDefs
        {
            get
            {
                if (!_baked)
                    throw new InvalidOperationException("Call Bake() before accessing RuntimeDefs.");
                return _runtimeDefs;
            }
        }

        /// <summary>Returns the flat-packed wherein content IDs.</summary>
        public NativeArray<ushort> WhereinFlat
        {
            get
            {
                if (!_baked)
                    throw new InvalidOperationException("Call Bake() before accessing WhereinFlat.");
                return _whereinFlat;
            }
        }

        /// <summary>Returns the flat-packed biome indices.</summary>
        public NativeArray<ushort> BiomesFlat
        {
            get
            {
                if (!_baked)
                    throw new InvalidOperationException("Call Bake() before accessing BiomesFlat.");
                return _biomesFlat;
            }
        }

        public void Dispose()
        {
            if (_runtimeDefs.IsCreated) _runtimeDefs.Dispose();
            if (_whereinFlat.IsCreated) _whereinFlat.Dispose();
            if (_biomesFlat.IsCreated) _biomesFlat.Dispose();
        }
    }
}
