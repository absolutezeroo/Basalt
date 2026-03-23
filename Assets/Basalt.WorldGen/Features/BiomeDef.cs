namespace Basalt.WorldGen
{
    /// <summary>
    /// Managed biome definition used during registration (before Bake).
    /// String node names are resolved to content IDs during <see cref="BiomeRegistry.Bake"/>.
    /// </summary>
    /// <remarks>
    /// Mirrors Luanti's <c>Biome</c> class in <c>luanti/src/mapgen/mg_biome.h</c>.
    /// </remarks>
    public class BiomeDef
    {
        /// <summary>Surface node name (e.g. "default:dirt_with_grass").</summary>
        public string NodeTop = "";

        /// <summary>Filler node below surface (e.g. "default:dirt").</summary>
        public string NodeFiller = "";

        /// <summary>Deep stone replacement (e.g. "default:stone").</summary>
        public string NodeStone = "";

        /// <summary>Top water layer node (e.g. "default:water_source").</summary>
        public string NodeWaterTop = "";

        /// <summary>Deeper water node.</summary>
        public string NodeWater = "";

        /// <summary>River water node.</summary>
        public string NodeRiverWater = "";

        /// <summary>Riverbed node (e.g. "default:sand").</summary>
        public string NodeRiverbed = "";

        /// <summary>Dust node placed on exposed surfaces (e.g. "default:snow").</summary>
        public string NodeDust = "";

        /// <summary>Number of top nodes to place.</summary>
        public short DepthTop = 1;

        /// <summary>Number of filler nodes below top. Noise-modulated at runtime.</summary>
        public short DepthFiller = 3;

        /// <summary>Number of top water nodes.</summary>
        public short DepthWaterTop = 0;

        /// <summary>Number of riverbed nodes.</summary>
        public short DepthRiverbed = 2;

        /// <summary>Minimum world position for this biome.</summary>
        public int YMin = -31007;

        /// <summary>Maximum world position for this biome.</summary>
        public int YMax = 31007;

        /// <summary>Heat value for Voronoi selection (0-100 typical).</summary>
        public float HeatPoint = 50f;

        /// <summary>Humidity value for Voronoi selection (0-100 typical).</summary>
        public float HumidityPoint = 50f;

        /// <summary>Vertical blend range above YMax for smooth transitions.</summary>
        public short VerticalBlend = 0;

        /// <summary>Selection weight (higher = more likely to win ties). Default 1.</summary>
        public float Weight = 1f;
    }
}
