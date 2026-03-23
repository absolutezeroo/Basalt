using Unity.Burst;

namespace Basalt.WorldGen
{
    /// <summary>
    /// Blittable biome definition for Burst job access.
    /// Created by <see cref="BiomeRegistry.Bake"/> from <see cref="BiomeDef"/>.
    /// </summary>
    /// <remarks>
    /// All string node names are resolved to content IDs.
    /// CONTENT_IGNORE (127) means "not specified" — the job skips replacement for that slot.
    /// </remarks>
    [BurstCompile]
    public struct BiomeDefRuntime
    {
        /// <summary>Registry index of this biome (matches biomemap values).</summary>
        public ushort Index;

        /// <summary>Surface node content ID.</summary>
        public ushort ContentTop;

        /// <summary>Filler node content ID.</summary>
        public ushort ContentFiller;

        /// <summary>Deep stone content ID.</summary>
        public ushort ContentStone;

        /// <summary>Top water layer content ID.</summary>
        public ushort ContentWaterTop;

        /// <summary>Deeper water content ID.</summary>
        public ushort ContentWater;

        /// <summary>River water content ID.</summary>
        public ushort ContentRiverWater;

        /// <summary>Riverbed content ID.</summary>
        public ushort ContentRiverbed;

        /// <summary>Dust content ID (CONTENT_IGNORE = no dust).</summary>
        public ushort ContentDust;

        /// <summary>Number of top nodes.</summary>
        public short DepthTop;

        /// <summary>Number of filler nodes (base, before noise modulation).</summary>
        public short DepthFiller;

        /// <summary>Number of top water nodes.</summary>
        public short DepthWaterTop;

        /// <summary>Number of riverbed nodes.</summary>
        public short DepthRiverbed;

        /// <summary>Minimum Y for this biome.</summary>
        public int YMin;

        /// <summary>Maximum Y for this biome.</summary>
        public int YMax;

        /// <summary>Heat point for Voronoi selection.</summary>
        public float HeatPoint;

        /// <summary>Humidity point for Voronoi selection.</summary>
        public float HumidityPoint;

        /// <summary>Vertical blend range above YMax.</summary>
        public short VerticalBlend;

        /// <summary>Selection weight (dist /= weight).</summary>
        public float Weight;
    }
}
