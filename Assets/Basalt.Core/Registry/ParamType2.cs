namespace Basalt.Core
{
    /// <summary>
    /// Defines how param2 is interpreted for a node.
    /// Mirrors Luanti's <c>ContentParamType2</c> in <c>luanti/src/nodedef.h</c>.
    /// </summary>
    public enum ParamType2 : byte
    {
        /// <summary>param2 is not used.</summary>
        None = 0,

        /// <summary>Full 8-bit param2 usage.</summary>
        Full = 1,

        /// <summary>Flowing liquid level and direction.</summary>
        FlowingLiquid = 2,

        /// <summary>Direction for chests and furnaces (0-23 with axis rotation).</summary>
        FaceDir = 3,

        /// <summary>Direction for signs and torches (0-5).</summary>
        WallMounted = 4,

        /// <summary>Block level, similar to flowing liquid.</summary>
        Leveled = 5,

        /// <summary>2D rotation in degrees.</summary>
        DegRotate = 6,

        /// <summary>Mesh options for plantlike nodes.</summary>
        MeshOptions = 7,

        /// <summary>Palette color index (0-255).</summary>
        Color = 8,

        /// <summary>3 bits palette index + facedir rotation.</summary>
        ColoredFaceDir = 9,

        /// <summary>5 bits palette index + wallmounted direction.</summary>
        ColoredWallMounted = 10,

        /// <summary>Internal liquid level for glasslike framed (0-63).</summary>
        GlasslikeLiquidLevel = 11,

        /// <summary>3 bits palette index + degrotate.</summary>
        ColoredDegRotate = 12,

        /// <summary>Simplified 4-direction rotation for chests.</summary>
        FourDir = 13,

        /// <summary>6 bits palette index + 4dir rotation.</summary>
        ColoredFourDir = 14,
    }
}