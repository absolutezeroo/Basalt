namespace Basalt.Core
{
    /// <summary>
    /// Defines how a node's texture alpha channel is handled during rendering.
    /// Mirrors Luanti's <c>AlphaMode</c> in <c>luanti/src/nodedef.h</c>.
    /// </summary>
    public enum AlphaMode : byte
    {
        /// <summary>Alpha blending for semi-transparent textures (water, glass).</summary>
        Blend = 0,

        /// <summary>Binary alpha: fully opaque or fully transparent (leaves, flowers).</summary>
        Clip = 1,

        /// <summary>Fully opaque, alpha channel is ignored (stone, dirt).</summary>
        Opaque = 2,
    }
}