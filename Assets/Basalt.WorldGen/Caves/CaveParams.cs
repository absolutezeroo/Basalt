using Unity.Burst;
using Unity.Mathematics;

namespace Basalt.WorldGen
{
    /// <summary>
    /// Blittable configuration for cave generation, mirroring Luanti's MapgenV7 cave parameters.
    /// Passed by value into Burst jobs.
    /// </summary>
    /// <remarks>
    /// Source: <c>luanti/src/mapgen/mapgen_v7.h</c> — cave-related fields of MapgenV7Params.
    /// Source: <c>luanti/src/mapgen/mapgen_v7.cpp</c> — default values (lines 30-39, 148-150).
    /// </remarks>
    [BurstCompile]
    public struct CaveParams
    {
        /// <summary>3D noise for the first cave density field.</summary>
        public NoiseParams NpCave1;

        /// <summary>3D noise for the second cave density field.</summary>
        public NoiseParams NpCave2;

        /// <summary>
        /// Cave tunnel width threshold. Tunnels form where contour(cave1) * contour(cave2) > CaveWidth.
        /// Higher values = narrower tunnels. Values >= 10 disable noise intersection caves.
        /// Default: 0.09.
        /// </summary>
        public float CaveWidth;

        /// <summary>3D noise for large cavern generation.</summary>
        public NoiseParams NpCavern;

        /// <summary>
        /// Y coordinate above which caverns are disabled. Caverns only generate at or below this level.
        /// Default: -256.
        /// </summary>
        public int CavernLimit;

        /// <summary>
        /// Vertical range over which cavern amplitude tapers from 1.0 to 0.0 above CavernLimit.
        /// Default: 256.
        /// </summary>
        public int CavernTaper;

        /// <summary>
        /// Noise threshold above which caverns are carved. Higher = smaller/rarer caverns.
        /// Default: 0.7.
        /// </summary>
        public float CavernThreshold;

        /// <summary>Whether cavern generation is enabled (0=false, 1=true). Maps to MGV7_CAVERNS flag.</summary>
        public byte EnableCaverns;

        /// <summary>
        /// Y coordinate below which large random walk caves can generate. Default: -33.
        /// </summary>
        public int LargeCaveDepth;

        /// <summary>Minimum number of small random walk caves per mapchunk. Default: 0.</summary>
        public int SmallCaveNumMin;

        /// <summary>Maximum number of small random walk caves per mapchunk. Default: 0.</summary>
        public int SmallCaveNumMax;

        /// <summary>Minimum number of large random walk caves per mapchunk. Default: 0.</summary>
        public int LargeCaveNumMin;

        /// <summary>Maximum number of large random walk caves per mapchunk. Default: 2.</summary>
        public int LargeCaveNumMax;

        /// <summary>
        /// Probability that a large random walk cave is flooded with liquid (0.0 - 1.0).
        /// Default: 0.5.
        /// </summary>
        public float LargeCaveFlooded;

        /// <summary>World seed.</summary>
        public int Seed;

        /// <summary>Content ID for air nodes.</summary>
        public ushort ContentAir;

        /// <summary>Content ID for water source nodes.</summary>
        public ushort ContentWater;

        /// <summary>Content ID for stone nodes (used as fallback in tunnel roofs).</summary>
        public ushort ContentStone;

        /// <summary>Water surface world Y coordinate.</summary>
        public int WaterLevel;

        /// <summary>
        /// Creates a CaveParams with all Luanti MapgenV7 defaults.
        /// Content IDs must be overwritten after NodeRegistry lookup.
        /// </summary>
        public static CaveParams CreateDefault(int seed)
        {
            return new CaveParams
            {
                // Luanti mapgen_v7.cpp line 149-150
                NpCave1 = new NoiseParams(0f, 12f, new float3(61f, 61f, 61f), 52534, 3, 0.5f, 2.0f),
                NpCave2 = new NoiseParams(0f, 12f, new float3(67f, 67f, 67f), 10325, 3, 0.5f, 2.0f),
                CaveWidth = 0.09f,

                // Luanti mapgen_v7.cpp line 148
                NpCavern = new NoiseParams(0f, 1f, new float3(384f, 128f, 384f), 723, 5, 0.63f, 2.0f),
                CavernLimit = -256,
                CavernTaper = 256,
                CavernThreshold = 0.7f,
                EnableCaverns = 1,

                // Luanti mapgen_v7.h lines 31-36
                LargeCaveDepth = -33,
                SmallCaveNumMin = 0,
                SmallCaveNumMax = 0,
                LargeCaveNumMin = 0,
                LargeCaveNumMax = 2,
                LargeCaveFlooded = 0.5f,

                Seed = seed,
                ContentAir = Basalt.Core.BasaltConstants.CONTENT_AIR,
                ContentWater = Basalt.Core.BasaltConstants.CONTENT_UNKNOWN,
                ContentStone = Basalt.Core.BasaltConstants.CONTENT_UNKNOWN,
                WaterLevel = MapgenV7Constants.DEFAULT_WATER_LEVEL,
            };
        }
    }
}
