namespace Basalt.Core
{
    /// <summary>
    /// Engine constants matching Luanti (ex-Minetest) values exactly.
    /// </summary>
    /// <remarks>
    /// Source: <c>luanti/src/constants.h</c> and <c>luanti/src/mapnode.h</c>.
    /// </remarks>
    public static class BasaltConstants
    {
        /// <summary>Number of nodes per axis per MapBlock.</summary>
        public const int MAP_BLOCKSIZE = 16;

        /// <summary>Total nodes per MapBlock (16³).</summary>
        public const int NODES_PER_BLOCK = 4096;

        /// <summary>Maximum world extent per axis in node coordinates.</summary>
        public const int MAX_MAP_GENERATION_LIMIT = 31007;

        /// <summary>Maximum world extent per axis in MapBlock (chunk) coordinates.</summary>
        /// <remarks>
        /// Luanti equivalent: <c>max_limit_bp</c> in <c>luanti/src/mapblock.h</c>.
        /// </remarks>
        public const int MAX_MAP_GENERATION_LIMIT_BP = MAX_MAP_GENERATION_LIMIT / MAP_BLOCKSIZE;

        /// <summary>Reserved content ID for unknown/unregistered nodes.</summary>
        public const ushort CONTENT_UNKNOWN = 125;

        /// <summary>Reserved content ID for air (empty space).</summary>
        public const ushort CONTENT_AIR = 126;

        /// <summary>Reserved content ID for ignored/unloaded areas.</summary>
        public const ushort CONTENT_IGNORE = 127;

        /// <summary>Maximum valid content ID (u16 max).</summary>
        public const ushort CONTENT_MAX = 65535;

        /// <summary>Maximum artificial light level (0-14).</summary>
        public const byte LIGHT_MAX = 14;

        /// <summary>Sunlight level (special value above LIGHT_MAX).</summary>
        public const byte LIGHT_SUN = 15;

        /// <summary>Number of time-of-day ticks in a full day cycle.</summary>
        public const int TIME_OF_DAY_RANGE = 24000;

        /// <summary>Duration of a full day/night cycle in seconds (20 minutes).</summary>
        public const float DAY_NIGHT_CYCLE_SECONDS = 1200f;
    }
}
