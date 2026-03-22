using Unity.Burst;

namespace Basalt.WorldGen
{
    /// <summary>
    /// Blittable configuration for Mapgen V7.
    /// Contains all noise parameters, feature flags, and content IDs.
    /// Passed by value into Burst jobs — no heap allocation required.
    /// </summary>
    /// <remarks>
    /// Source: <c>luanti/src/mapgen/mapgen_v7.h</c> — struct MapgenV7Params.
    /// All booleans use <c>byte</c> (0/1) for Burst blittability in NativeContainers.
    /// </remarks>
    [BurstCompile]
    public struct MapgenV7Params
    {
        /// <summary>Noise params for the base terrain height layer.</summary>
        public NoiseParams TerrainBase;

        /// <summary>Noise params for the alternate (flatter) terrain height layer.</summary>
        public NoiseParams TerrainAlt;

        /// <summary>Noise params for per-column persistence variation.</summary>
        public NoiseParams TerrainPersist;

        /// <summary>Noise params for blending between base and alt terrain layers.</summary>
        public NoiseParams HeightSelect;

        /// <summary>Noise params for maximum mountain height per column.</summary>
        public NoiseParams MountHeight;

        /// <summary>Noise params for ridge underwater detection (river carving).</summary>
        public NoiseParams RidgeUwater;

        /// <summary>3D noise params for mountain solid-density field.</summary>
        public NoiseParams Mountain;

        /// <summary>3D noise params for ridge solid-density field (river carving).</summary>
        public NoiseParams Ridge;

        /// <summary>Whether mountain generation is active (0=false, 1=true).</summary>
        public byte EnableMountains;

        /// <summary>Whether ridge/river generation is active (0=false, 1=true).</summary>
        public byte EnableRidges;

        /// <summary>Map seed passed to all noise functions.</summary>
        public int Seed;

        /// <summary>Water surface world Y coordinate.</summary>
        public int WaterLevel;

        /// <summary>
        /// Y coordinate below which mountain density gradient is zero.
        /// Prevents mountains from bulging below the ocean floor.
        /// Source: <c>luanti/src/mapgen/mapgen_v7.h</c> line 22.
        /// </summary>
        public int MountZeroLevel;

        /// <summary>Content ID for stone nodes.</summary>
        public ushort ContentStone;

        /// <summary>Content ID for water source nodes.</summary>
        public ushort ContentWater;

        /// <summary>Content ID for air nodes.</summary>
        public ushort ContentAir;

        /// <summary>
        /// Creates a <see cref="MapgenV7Params"/> with all Luanti defaults applied.
        /// Content IDs default to reserved values and must be overwritten
        /// after NodeRegistry lookup via <see cref="MapgenV7.Initialize"/>.
        /// </summary>
        /// <param name="seed">The world seed.</param>
        public static MapgenV7Params CreateDefault(int seed)
        {
            return new MapgenV7Params
            {
                TerrainBase = MapgenV7Constants.DefaultTerrainBase,
                TerrainAlt = MapgenV7Constants.DefaultTerrainAlt,
                TerrainPersist = MapgenV7Constants.DefaultTerrainPersist,
                HeightSelect = MapgenV7Constants.DefaultHeightSelect,
                MountHeight = MapgenV7Constants.DefaultMountHeight,
                RidgeUwater = MapgenV7Constants.DefaultRidgeUwater,
                Mountain = MapgenV7Constants.DefaultMountain,
                Ridge = MapgenV7Constants.DefaultRidge,
                EnableMountains = MapgenV7Constants.DEFAULT_ENABLE_MOUNTAINS,
                EnableRidges = MapgenV7Constants.DEFAULT_ENABLE_RIDGES,
                Seed = seed,
                WaterLevel = MapgenV7Constants.DEFAULT_WATER_LEVEL,
                MountZeroLevel = MapgenV7Constants.DEFAULT_MOUNT_ZERO_LEVEL,
                ContentStone = Basalt.Core.BasaltConstants.CONTENT_UNKNOWN,
                ContentWater = Basalt.Core.BasaltConstants.CONTENT_UNKNOWN,
                ContentAir = Basalt.Core.BasaltConstants.CONTENT_AIR,
            };
        }
    }
}
