namespace Basalt.Core
{
    /// <summary>
    /// Defines how a node is visually rendered in the world.
    /// Mirrors Luanti's <c>NodeDrawType</c> in <c>luanti/src/nodedef.h</c>.
    /// </summary>
    public enum DrawType : byte
    {
        /// <summary>A standard cubic block with 6 faces.</summary>
        Normal = 0,

        /// <summary>Invisible node, nothing is drawn.</summary>
        Airlike = 1,

        /// <summary>A liquid source block. Does not draw faces toward same liquid type.</summary>
        Liquid = 2,

        /// <summary>A flowing liquid with varying height levels.</summary>
        FlowingLiquid = 3,

        /// <summary>Glass-like block, does not draw faces toward same type.</summary>
        Glasslike = 4,

        /// <summary>Leaves-like, draws all faces regardless of neighbors.</summary>
        Allfaces = 5,

        /// <summary>Allfaces when fancy graphics enabled, Normal otherwise.</summary>
        AllfacesOptional = 6,

        /// <summary>Single plane perpendicular to the surface it sits on (torches).</summary>
        Torchlike = 7,

        /// <summary>Single plane parallel to the surface it sits on (signs).</summary>
        Signlike = 8,

        /// <summary>Two vertical planes in an X shape diagonal to XZ axes (flowers, grass).</summary>
        Plantlike = 9,

        /// <summary>Fence posts that connect to neighboring fence posts.</summary>
        Fencelike = 10,

        /// <summary>Selects appropriate junction texture to connect like rails.</summary>
        Raillike = 11,

        /// <summary>Custom Lua-definable structure of multiple cuboids.</summary>
        Nodebox = 12,

        /// <summary>Glass-like with connected frames and visible faces.</summary>
        GlasslikeFramed = 13,

        /// <summary>Faces drawn slightly rotated only on neighboring nodes (fire).</summary>
        Firelike = 14,

        /// <summary>GlasslikeFramed when fancy graphics enabled, Glasslike otherwise.</summary>
        GlasslikeFramedOptional = 15,

        /// <summary>Uses custom static meshes for rendering.</summary>
        Mesh = 16,

        /// <summary>Combined plantlike-on-solid: solid base with plant growing on top.</summary>
        PlantlikeRooted = 17,
    }
}