using Unity.Burst;

namespace Basalt.WorldGen
{
    /// <summary>
    /// Blittable ore definition for Burst job access.
    /// All 6 ore types share one struct; unused fields cost ~200 bytes per ore (negligible).
    /// Created by <see cref="OreRegistry.Bake"/> from <see cref="OreDef"/>.
    /// </summary>
    [BurstCompile]
    public struct OreDefRuntime
    {
        /// <summary>Ore placement algorithm.</summary>
        public OreType Type;

        /// <summary>Content ID of the ore node to place.</summary>
        public ushort ContentOre;

        /// <summary>Ore param2 value written with the node.</summary>
        public byte OreParam2;

        /// <summary>Ore flags (see <see cref="OreFlags"/>).</summary>
        public uint Flags;

        // ---- Flat-packed wherein array (offset+count into shared NativeArray) ----

        /// <summary>Start index into the flat wherein content ID array.</summary>
        public int WhereinOffset;

        /// <summary>Number of wherein content IDs.</summary>
        public int WhereinCount;

        // ---- Flat-packed biomes array (offset+count into shared NativeArray) ----

        /// <summary>Start index into the flat biome index array.</summary>
        public int BiomesOffset;

        /// <summary>Number of biome indices. 0 = no biome filter.</summary>
        public int BiomesCount;

        // ---- Common placement params ----

        /// <summary>Volume per cluster (Scatter/Blob) or scarcity per node (Stratum).</summary>
        public int ClustScarcity;

        /// <summary>Ores per cluster volume cell (Scatter).</summary>
        public short ClustNumOres;

        /// <summary>Cluster cube side length (Scatter/Blob).</summary>
        public short ClustSize;

        /// <summary>Minimum Y coordinate.</summary>
        public int YMin;

        /// <summary>Maximum Y coordinate.</summary>
        public int YMax;

        /// <summary>Noise threshold for placement.</summary>
        public float NThresh;

        /// <summary>Noise parameters for the primary noise.</summary>
        public NoiseParams NoiseParams;

        // ---- Sheet-specific ----

        /// <summary>Sheet: minimum column height.</summary>
        public ushort ColumnHeightMin;

        /// <summary>Sheet: maximum column height.</summary>
        public ushort ColumnHeightMax;

        /// <summary>Sheet: midpoint factor (0.0-1.0).</summary>
        public float ColumnMidpointFactor;

        // ---- Puff-specific ----

        /// <summary>Puff: noise params for top extent.</summary>
        public NoiseParams NoisePuffTop;

        /// <summary>Puff: noise params for bottom extent.</summary>
        public NoiseParams NoisePuffBottom;

        // ---- Vein-specific ----

        /// <summary>Vein: random factor.</summary>
        public float RandomFactor;

        // ---- Stratum-specific ----

        /// <summary>Stratum: noise params for thickness variation.</summary>
        public NoiseParams NoiseStratumThickness;

        /// <summary>Stratum: fixed thickness.</summary>
        public ushort StratumThickness;
    }
}
