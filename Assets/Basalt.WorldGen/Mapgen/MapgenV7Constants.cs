using Unity.Mathematics;

namespace Basalt.WorldGen
{
    /// <summary>
    /// Geometry and default noise constants for Mapgen V7.
    /// All values match Luanti's <c>mapgen_v7.cpp</c> defaults exactly.
    /// </summary>
    /// <remarks>
    /// Source: <c>luanti/src/mapgen/mapgen_v7.cpp</c> — constructor default params (lines 137-151).
    /// Source: <c>luanti/src/mapgen/mapgen_v7.h</c> — MapgenV7Params struct.
    ///
    /// Mapchunk layout: 5x5x5 MapBlocks = 80 nodes per axis, plus 1 node of vertical
    /// overgeneration on each side (y-1 and y+80) for mountain/ridge blending at borders.
    /// </remarks>
    public static class MapgenV7Constants
    {
        /// <summary>Number of MapBlocks per axis in a mapchunk (Luanti default: 5).</summary>
        public const int MAPCHUNK_BLOCKS = 5;

        /// <summary>Nodes per mapchunk axis (5 x 16 = 80).</summary>
        public const int MAPCHUNK_SIZE = MAPCHUNK_BLOCKS * Basalt.Core.BasaltConstants.MAP_BLOCKSIZE;

        /// <summary>
        /// Nodes of overgeneration added on each vertical side (bottom and top).
        /// Required for mountain and ridge noise to blend correctly at chunk borders.
        /// </summary>
        public const int OVERGEN_SIZE = 1;

        /// <summary>
        /// Total node height of the VoxelManipulator buffer including overgeneration.
        /// = MAPCHUNK_SIZE + 2 * OVERGEN_SIZE = 82.
        /// </summary>
        public const int VM_HEIGHT = MAPCHUNK_SIZE + 2 * OVERGEN_SIZE;

        /// <summary>Total node count in the VoxelManipulator buffer (80 x 82 x 80 = 524800).</summary>
        public const int VM_NODE_COUNT = MAPCHUNK_SIZE * VM_HEIGHT * MAPCHUNK_SIZE;

        /// <summary>
        /// Luanti default water level in world Y coordinates.
        /// Source: <c>luanti/src/mapgen/mapgen.h</c> line 127 — <c>water_level = 1</c>.
        /// </summary>
        public const int DEFAULT_WATER_LEVEL = 1;

        /// <summary>
        /// Y coordinate below which mountains have zero height gradient.
        /// Prevents mountains from generating below the ocean floor.
        /// Source: <c>luanti/src/mapgen/mapgen_v7.h</c> line 22 — <c>mount_zero_level = 0</c>.
        /// </summary>
        public const int DEFAULT_MOUNT_ZERO_LEVEL = 0;

        /// <summary>Luanti default feature flags: mountains and ridges enabled, floatlands disabled.</summary>
        public const byte DEFAULT_ENABLE_MOUNTAINS = 1;

        /// <summary>Luanti default feature flags: ridges (rivers) enabled.</summary>
        public const byte DEFAULT_ENABLE_RIDGES = 1;

        // ---- Default NoiseParams ----
        // Source: luanti/src/mapgen/mapgen_v7.cpp lines 138-151

        /// <summary>
        /// terrain_base: large-scale terrain elevation.
        /// (offset=4, scale=70, spread=(600,600,600), seed=82341, octaves=5, persist=0.6, lacunarity=2.0)
        /// </summary>
        public static readonly NoiseParams DefaultTerrainBase = new NoiseParams(
            4.0f, 70.0f, new float3(600f, 600f, 600f), 82341, 5, 0.6f, 2.0f);

        /// <summary>
        /// terrain_alt: alternate (flatter) terrain elevation blended with terrain_base.
        /// (offset=4, scale=25, spread=(600,600,600), seed=5934, octaves=5, persist=0.6, lacunarity=2.0)
        /// </summary>
        public static readonly NoiseParams DefaultTerrainAlt = new NoiseParams(
            4.0f, 25.0f, new float3(600f, 600f, 600f), 5934, 5, 0.6f, 2.0f);

        /// <summary>
        /// terrain_persist: per-column persistence variation for terrain_base and terrain_alt.
        /// (offset=0.6, scale=0.1, spread=(2000,2000,2000), seed=539, octaves=3, persist=0.6, lacunarity=2.0)
        /// </summary>
        public static readonly NoiseParams DefaultTerrainPersist = new NoiseParams(
            0.6f, 0.1f, new float3(2000f, 2000f, 2000f), 539, 3, 0.6f, 2.0f);

        /// <summary>
        /// height_select: blend weight between terrain_base and terrain_alt.
        /// (offset=-8, scale=16, spread=(500,500,500), seed=4213, octaves=6, persist=0.7, lacunarity=2.0)
        /// </summary>
        public static readonly NoiseParams DefaultHeightSelect = new NoiseParams(
            -8.0f, 16.0f, new float3(500f, 500f, 500f), 4213, 6, 0.7f, 2.0f);

        /// <summary>
        /// mount_height: maximum mountain height per column.
        /// (offset=256, scale=112, spread=(1000,1000,1000), seed=72449, octaves=3, persist=0.6, lacunarity=2.0)
        /// </summary>
        public static readonly NoiseParams DefaultMountHeight = new NoiseParams(
            256.0f, 112.0f, new float3(1000f, 1000f, 1000f), 72449, 3, 0.6f, 2.0f);

        /// <summary>
        /// ridge_uwater: 2D noise for river channel position detection.
        /// (offset=0, scale=1, spread=(1000,1000,1000), seed=85039, octaves=5, persist=0.6, lacunarity=2.0)
        /// </summary>
        public static readonly NoiseParams DefaultRidgeUwater = new NoiseParams(
            0.0f, 1.0f, new float3(1000f, 1000f, 1000f), 85039, 5, 0.6f, 2.0f);

        /// <summary>
        /// mountain: 3D density field for mountain terrain.
        /// (offset=-0.6, scale=1, spread=(250,350,250), seed=5333, octaves=5, persist=0.63, lacunarity=2.0)
        /// </summary>
        public static readonly NoiseParams DefaultMountain = new NoiseParams(
            -0.6f, 1.0f, new float3(250f, 350f, 250f), 5333, 5, 0.63f, 2.0f);

        /// <summary>
        /// ridge: 3D density field for river channel carving.
        /// (offset=0, scale=1, spread=(100,100,100), seed=6467, octaves=4, persist=0.75, lacunarity=2.0)
        /// </summary>
        public static readonly NoiseParams DefaultRidge = new NoiseParams(
            0.0f, 1.0f, new float3(100f, 100f, 100f), 6467, 4, 0.75f, 2.0f);
    }
}
