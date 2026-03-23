using System;
using System.Collections.Generic;
using Basalt.Core;
using Unity.Collections;

namespace Basalt.WorldGen
{
    /// <summary>
    /// Registry for biome definitions. Register biomes by name, then Bake() to produce
    /// a NativeArray of <see cref="BiomeDefRuntime"/> for Burst job access.
    /// </summary>
    /// <remarks>
    /// Follows the same Register/Bake lifecycle as <see cref="NodeRegistry"/>.
    /// Index 0 is reserved for the default biome (fallback when no biome matches).
    /// </remarks>
    public class BiomeRegistry : IDisposable
    {
        private readonly List<string> _names = new();
        private readonly List<BiomeDef> _defs = new();
        private readonly Dictionary<string, ushort> _nameToIndex = new();

        private NativeArray<BiomeDefRuntime> _runtimeDefs;
        private bool _baked;

        /// <summary>
        /// Creates a BiomeRegistry with a default biome at index 0.
        /// The default biome uses stone for all surfaces (no replacement).
        /// </summary>
        public BiomeRegistry()
        {
            // Index 0 = default biome (BIOME_NONE in Luanti)
            RegisterInternal("__default", new BiomeDef());
        }

        /// <summary>
        /// Registers a new biome. Returns the assigned biome index.
        /// Must be called before Bake().
        /// </summary>
        public ushort Register(string name, BiomeDef def)
        {
            if (_baked)
                throw new InvalidOperationException("Cannot register biomes after Bake().");
            if (_nameToIndex.ContainsKey(name))
                throw new ArgumentException($"Biome '{name}' is already registered.", nameof(name));

            return RegisterInternal(name, def);
        }

        private ushort RegisterInternal(string name, BiomeDef def)
        {
            ushort index = (ushort)_defs.Count;
            _names.Add(name);
            _defs.Add(def);
            _nameToIndex[name] = index;
            return index;
        }

        /// <summary>
        /// Bakes all registered biomes into a NativeArray, resolving node names to content IDs.
        /// </summary>
        public void Bake(NodeRegistry nodeRegistry)
        {
            if (_baked)
                return;

            _runtimeDefs = new NativeArray<BiomeDefRuntime>(
                _defs.Count, Allocator.Persistent, NativeArrayOptions.UninitializedMemory);

            for (int i = 0; i < _defs.Count; i++)
            {
                BiomeDef def = _defs[i];
                _runtimeDefs[i] = new BiomeDefRuntime
                {
                    Index = (ushort)i,
                    ContentTop = ResolveOrIgnore(nodeRegistry, def.NodeTop),
                    ContentFiller = ResolveOrIgnore(nodeRegistry, def.NodeFiller),
                    ContentStone = ResolveOrIgnore(nodeRegistry, def.NodeStone),
                    ContentWaterTop = ResolveOrIgnore(nodeRegistry, def.NodeWaterTop),
                    ContentWater = ResolveOrIgnore(nodeRegistry, def.NodeWater),
                    ContentRiverWater = ResolveOrIgnore(nodeRegistry, def.NodeRiverWater),
                    ContentRiverbed = ResolveOrIgnore(nodeRegistry, def.NodeRiverbed),
                    ContentDust = ResolveOrIgnore(nodeRegistry, def.NodeDust),
                    DepthTop = def.DepthTop,
                    DepthFiller = def.DepthFiller,
                    DepthWaterTop = def.DepthWaterTop,
                    DepthRiverbed = def.DepthRiverbed,
                    YMin = def.YMin,
                    YMax = def.YMax,
                    HeatPoint = def.HeatPoint,
                    HumidityPoint = def.HumidityPoint,
                    VerticalBlend = def.VerticalBlend,
                    Weight = def.Weight,
                };
            }

            _baked = true;
        }

        /// <summary>Gets the biome index for a name, or 0 (default) if not found.</summary>
        public ushort GetIndexByName(string name)
        {
            return _nameToIndex.TryGetValue(name, out ushort index) ? index : (ushort)0;
        }

        /// <summary>Gets the biome name for an index.</summary>
        public string GetNameByIndex(ushort index)
        {
            return index < _names.Count ? _names[index] : "__default";
        }

        /// <summary>Gets the number of registered biomes (including default).</summary>
        public int Count => _defs.Count;

        /// <summary>Returns the baked NativeArray for Burst job access.</summary>
        public NativeArray<BiomeDefRuntime> RuntimeDefs
        {
            get
            {
                if (!_baked)
                    throw new InvalidOperationException("Call Bake() before accessing RuntimeDefs.");
                return _runtimeDefs;
            }
        }

        private static ushort ResolveOrIgnore(NodeRegistry registry, string name)
        {
            if (string.IsNullOrEmpty(name))
                return BasaltConstants.CONTENT_IGNORE;
            return registry.GetIdByName(name);
        }

        public void Dispose()
        {
            if (_runtimeDefs.IsCreated)
                _runtimeDefs.Dispose();
        }
    }
}
