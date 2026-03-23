using System.Collections.Generic;

namespace Basalt.WorldGen
{
    /// <summary>
    /// Managed ore definition used during registration (before Bake).
    /// String node names and biome names are resolved during <see cref="OreRegistry.Bake"/>.
    /// </summary>
    /// <remarks>
    /// Mirrors Luanti's <c>Ore</c> class in <c>luanti/src/mapgen/mg_ore.h</c>.
    /// All 6 ore types share the same def; type-specific fields are only used by their type.
    /// </remarks>
    public class OreDef
    {
        /// <summary>Ore placement algorithm.</summary>
        public OreType Type = OreType.Scatter;

        /// <summary>Node name to place (e.g. "default:stone_with_coal").</summary>
        public string NodeOre = "";

        /// <summary>Nodes that can be replaced by this ore (e.g. ["default:stone"]).</summary>
        public List<string> NodeWherein = new();

        /// <summary>Biome names this ore is restricted to. Empty = all biomes.</summary>
        public List<string> Biomes = new();

        /// <summary>Volume per cluster (volume / clust_scarcity = num clusters).</summary>
        public int ClustScarcity = 8 * 8 * 8;

        /// <summary>Number of ore nodes per cluster volume cell.</summary>
        public short ClustNumOres = 8;

        /// <summary>Cluster cube side length.</summary>
        public short ClustSize = 3;

        /// <summary>Minimum Y coordinate.</summary>
        public int YMin = -31007;

        /// <summary>Maximum Y coordinate.</summary>
        public int YMax = 31007;

        /// <summary>Ore param2 value.</summary>
        public byte OreParam2 = 0;

        /// <summary>Ore flags (see <see cref="OreFlags"/>).</summary>
        public uint Flags = 0;

        /// <summary>Noise threshold for placement.</summary>
        public float NThresh = 0f;

        /// <summary>Noise parameters for noise-based placement.</summary>
        public NoiseParams NoiseParams = NoiseParams.Default;

        // ---- Sheet-specific ----

        /// <summary>Sheet: minimum column height.</summary>
        public ushort ColumnHeightMin = 1;

        /// <summary>Sheet: maximum column height.</summary>
        public ushort ColumnHeightMax = 0;

        /// <summary>Sheet: midpoint factor (0.0-1.0).</summary>
        public float ColumnMidpointFactor = 0.5f;

        // ---- Puff-specific ----

        /// <summary>Puff: noise params for top extent.</summary>
        public NoiseParams NoisePuffTop = NoiseParams.Default;

        /// <summary>Puff: noise params for bottom extent.</summary>
        public NoiseParams NoisePuffBottom = NoiseParams.Default;

        // ---- Vein-specific ----

        /// <summary>Vein: random factor added to contour product.</summary>
        public float RandomFactor = 0f;

        // ---- Stratum-specific ----

        /// <summary>Stratum: noise params for thickness variation.</summary>
        public NoiseParams NoiseStratumThickness = NoiseParams.Default;

        /// <summary>Stratum: fixed thickness when not using noise2.</summary>
        public ushort StratumThickness = 8;
    }
}
