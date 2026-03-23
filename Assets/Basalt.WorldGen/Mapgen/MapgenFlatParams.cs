namespace Basalt.WorldGen
{
    /// <summary>
    /// Blittable configuration for Mapgen Flat.
    /// Contains the terrain noise parameters, feature flags, and content IDs.
    /// Passed by value into Burst jobs — no heap allocation required.
    /// </summary>
    /// <remarks>
    /// Source: <c>luanti/src/mapgen/mapgen_flat.h</c> — struct MapgenFlatParams.
    /// All booleans use <c>byte</c> (0/1) for Burst blittability in NativeContainers.
    /// </remarks>
    public struct MapgenFlatParams
    {
        /// <summary>Noise params for the terrain shape variation channel (lakes/hills).</summary>
        public NoiseParams Terrain;

        /// <summary>Map seed passed to all noise functions.</summary>
        public int Seed;

        /// <summary>Base flat surface world Y coordinate.</summary>
        public int GroundLevel;

        /// <summary>Water surface world Y coordinate.</summary>
        public int WaterLevel;

        /// <summary>Noise value below which stone level is depressed to form a lake.</summary>
        public float LakeThreshold;

        /// <summary>Depression multiplier: stone_level -= (threshold - noise) * steepness.</summary>
        public float LakeSteepness;

        /// <summary>Noise value above which stone level is raised to form a hill.</summary>
        public float HillThreshold;

        /// <summary>Elevation multiplier: stone_level += (noise - threshold) * steepness.</summary>
        public float HillSteepness;

        /// <summary>Whether lake generation is active (0=false, 1=true).</summary>
        public byte EnableLakes;

        /// <summary>Whether hill generation is active (0=false, 1=true).</summary>
        public byte EnableHills;

        /// <summary>Content ID for stone nodes.</summary>
        public ushort ContentStone;

        /// <summary>Content ID for water source nodes.</summary>
        public ushort ContentWater;

        /// <summary>Content ID for air nodes.</summary>
        public ushort ContentAir;

        /// <summary>
        /// Creates a <see cref="MapgenFlatParams"/> with all Luanti defaults applied.
        /// Content IDs default to reserved values and must be overwritten
        /// after NodeRegistry lookup via <see cref="MapgenFlat.Initialize"/>.
        /// </summary>
        /// <param name="seed">The world seed.</param>
        public static MapgenFlatParams CreateDefault(int seed)
        {
            return new MapgenFlatParams
            {
                Terrain = MapgenFlatConstants.DefaultTerrain,
                Seed = seed,
                GroundLevel = MapgenFlatConstants.DEFAULT_GROUND_LEVEL,
                WaterLevel = MapgenV7Constants.DEFAULT_WATER_LEVEL,
                LakeThreshold = MapgenFlatConstants.DEFAULT_LAKE_THRESHOLD,
                LakeSteepness = MapgenFlatConstants.DEFAULT_LAKE_STEEPNESS,
                HillThreshold = MapgenFlatConstants.DEFAULT_HILL_THRESHOLD,
                HillSteepness = MapgenFlatConstants.DEFAULT_HILL_STEEPNESS,
                EnableLakes = MapgenFlatConstants.DEFAULT_ENABLE_LAKES,
                EnableHills = MapgenFlatConstants.DEFAULT_ENABLE_HILLS,
                ContentStone = Basalt.Core.BasaltConstants.CONTENT_UNKNOWN,
                ContentWater = Basalt.Core.BasaltConstants.CONTENT_UNKNOWN,
                ContentAir = Basalt.Core.BasaltConstants.CONTENT_AIR,
            };
        }
    }
}
