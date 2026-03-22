namespace Basalt.Core
{
    /// <summary>
    /// Engine constants matching Luanti (ex-Minetest) values exactly.
    /// </summary>
    public static class BasaltConstants
    {
        // MapBlock dimensions
        public const int MAP_BLOCKSIZE = 16;
        public const int NODES_PER_BLOCK = 4096; // 16 * 16 * 16

        // World limits (matches Luanti MAX_MAP_GENERATION_LIMIT)
        public const int MAX_MAP_GENERATION_LIMIT = 31007;

        // Reserved content IDs (matches Luanti content_t values)
        public const ushort CONTENT_UNKNOWN = 125;
        public const ushort CONTENT_AIR = 126;
        public const ushort CONTENT_IGNORE = 127;
        public const ushort CONTENT_MAX = 65535; // u16 max

        // Lighting
        public const byte LIGHT_MAX = 14;
        public const byte LIGHT_SUN = 15;

        // Day/night cycle (matches Luanti: 20 minutes total)
        public const int TIME_OF_DAY_RANGE = 24000;
        public const float DAY_NIGHT_CYCLE_SECONDS = 1200f; // 20 minutes
    }
}