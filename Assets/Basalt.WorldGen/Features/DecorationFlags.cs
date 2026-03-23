namespace Basalt.WorldGen
{
    /// <summary>
    /// Decoration placement flags matching Luanti's DECO_* constants.
    /// </summary>
    /// <remarks>
    /// Source: <c>luanti/src/mapgen/mg_decoration.h</c>.
    /// </remarks>
    public static class DecorationFlags
    {
        /// <summary>Center the decoration on the X axis.</summary>
        public const uint DECO_PLACE_CENTER_X = 0x01;

        /// <summary>Center the decoration on the Y axis.</summary>
        public const uint DECO_PLACE_CENTER_Y = 0x02;

        /// <summary>Center the decoration on the Z axis.</summary>
        public const uint DECO_PLACE_CENTER_Z = 0x04;

        /// <summary>Use 2D noise instead of fill_ratio for density.</summary>
        public const uint DECO_USE_NOISE = 0x08;

        /// <summary>Force placement even into non-air nodes.</summary>
        public const uint DECO_FORCE_PLACEMENT = 0x10;

        /// <summary>Place on liquid surfaces instead of solid ground.</summary>
        public const uint DECO_LIQUID_SURFACE = 0x20;

        /// <summary>Place on all floor surfaces found in the column.</summary>
        public const uint DECO_ALL_FLOORS = 0x40;

        /// <summary>Place on all ceiling surfaces found in the column.</summary>
        public const uint DECO_ALL_CEILINGS = 0x80;
    }
}
