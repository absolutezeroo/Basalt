using Unity.Mathematics;

namespace Basalt.WorldGen
{
    /// <summary>
    /// Default constants for Mapgen Flat.
    /// All values match Luanti's <c>mapgen_flat.cpp</c> defaults exactly.
    /// </summary>
    /// <remarks>
    /// Source: <c>luanti/src/mapgen/mapgen_flat.cpp</c> — constructor default params.
    /// Source: <c>luanti/src/mapgen/mapgen_flat.h</c> — MapgenFlatParams struct.
    ///
    /// Mapchunk geometry is identical to Mapgen V7 (5x5x5 MapBlocks, 1-node overgeneration).
    /// Shared geometry constants are accessed via <see cref="MapgenV7Constants"/>.
    /// </remarks>
    public static class MapgenFlatConstants
    {
        /// <summary>
        /// Flat surface Y coordinate in world space (nodes).
        /// Source: <c>luanti/src/mapgen/mapgen_flat.h</c> line 20 — <c>ground_level = 8</c>.
        /// </summary>
        public const int DEFAULT_GROUND_LEVEL = 8;

        /// <summary>
        /// Noise value below which a column is depressed to form a lake.
        /// Source: <c>luanti/src/mapgen/mapgen_flat.h</c> line 21 — <c>lake_threshold = -0.45</c>.
        /// </summary>
        public const float DEFAULT_LAKE_THRESHOLD = -0.45f;

        /// <summary>
        /// Steepness multiplier for lake depth below ground level.
        /// Source: <c>luanti/src/mapgen/mapgen_flat.h</c> line 22 — <c>lake_steepness = 48</c>.
        /// </summary>
        public const float DEFAULT_LAKE_STEEPNESS = 48.0f;

        /// <summary>
        /// Noise value above which a column is raised to form a hill.
        /// Source: <c>luanti/src/mapgen/mapgen_flat.h</c> line 23 — <c>hill_threshold = 0.45</c>.
        /// </summary>
        public const float DEFAULT_HILL_THRESHOLD = 0.45f;

        /// <summary>
        /// Steepness multiplier for hill height above ground level.
        /// Source: <c>luanti/src/mapgen/mapgen_flat.h</c> line 24 — <c>hill_steepness = 64</c>.
        /// </summary>
        public const float DEFAULT_HILL_STEEPNESS = 64.0f;

        /// <summary>
        /// Luanti default: lakes disabled by default (spflags = 0).
        /// Source: <c>luanti/src/mapgen/mapgen_flat.cpp</c> line 154 — <c>setDefault("mgflat_spflags", ..., 0)</c>.
        /// </summary>
        public const byte DEFAULT_ENABLE_LAKES = 0;

        /// <summary>
        /// Luanti default: hills disabled by default (spflags = 0).
        /// Source: <c>luanti/src/mapgen/mapgen_flat.cpp</c> line 154 — <c>setDefault("mgflat_spflags", ..., 0)</c>.
        /// </summary>
        public const byte DEFAULT_ENABLE_HILLS = 0;

        /// <summary>
        /// Terrain noise parameters for flat world shape variation (lakes/hills).
        /// (offset=0, scale=1, spread=(600,600,600), seed=7244, octaves=5, persist=0.6, lacunarity=2.0)
        /// Source: <c>luanti/src/mapgen/mapgen_flat.cpp</c> line 82 — <c>np_terrain</c>.
        /// </summary>
        public static readonly NoiseParams DefaultTerrain = new NoiseParams(
            0.0f, 1.0f, new float3(600f, 600f, 600f), 7244, 5, 0.6f, 2.0f);
    }
}
