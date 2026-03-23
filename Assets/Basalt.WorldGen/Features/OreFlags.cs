namespace Basalt.WorldGen
{
    /// <summary>
    /// Ore placement flags matching Luanti's OREFLAG_* constants.
    /// </summary>
    /// <remarks>
    /// Source: <c>luanti/src/mapgen/mg_ore.h</c>.
    /// </remarks>
    public static class OreFlags
    {
        /// <summary>Non-functional, kept for compatibility with Luanti API.</summary>
        public const uint OREFLAG_ABSHEIGHT = 0x01;

        /// <summary>When set, puff ore does not taper near the noise threshold.</summary>
        public const uint OREFLAG_PUFF_CLIFFS = 0x02;

        /// <summary>When set, puff ore swaps y0/y1 if y0 > y1 instead of skipping.</summary>
        public const uint OREFLAG_PUFF_ADDITIVE = 0x04;

        /// <summary>Use 3D noise to filter scatter clusters or 2D noise for sheet/stratum center Y.</summary>
        public const uint OREFLAG_USE_NOISE = 0x08;

        /// <summary>Use a second noise for stratum thickness variation.</summary>
        public const uint OREFLAG_USE_NOISE2 = 0x10;
    }
}
