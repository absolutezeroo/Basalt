using System.Runtime.CompilerServices;

namespace Basalt.Core
{
    /// <summary>
    /// Blittable definition of a node type, accessible from Burst jobs via content_id lookup.
    /// All bool-like fields use byte (0/1) for NativeContainer compatibility.
    /// </summary>
    /// <remarks>
    /// Mirrors a subset of Luanti's <c>ContentFeatures</c> in <c>luanti/src/nodedef.h</c>.
    /// Groups are stored externally in a flat array because Dictionary is not blittable.
    /// </remarks>
    public struct NodeDefinition
    {
        /// <summary>
        /// Pre-Bake sentinel: let <see cref="Basalt.Core.NodeRegistry.Bake"/> auto-compute
        /// <see cref="IsGroundContent"/> based on solidness and liquid type.
        /// This is the struct-default value (0).
        /// </summary>
        public const byte IS_GROUND_CONTENT_AUTO = 0;

        /// <summary>
        /// Pre-Bake value: explicitly mark this node as ground content (caves can carve it).
        /// </summary>
        public const byte IS_GROUND_CONTENT_TRUE = 1;

        /// <summary>
        /// Pre-Bake value: explicitly mark this node as NOT ground content,
        /// even if it would otherwise qualify (solid, non-liquid).
        /// After Bake() this is normalized to 0.
        /// </summary>
        public const byte IS_GROUND_CONTENT_FALSE = 2;

        /// <summary>Unique identifier for this node type (0-65535).</summary>
        public ushort ContentId;

        /// <summary>Visual rendering mode (Normal, Plantlike, Liquid, etc.).</summary>
        public DrawType Drawtype;

        /// <summary>How param1 is interpreted (None or Light).</summary>
        public ParamType Paramtype;

        /// <summary>How param2 is interpreted (FaceDir, WallMounted, Color, etc.).</summary>
        public ParamType2 Paramtype2;

        /// <summary>Light emitted by this node (0-14). 0 = no light.</summary>
        public byte LightSource;

        /// <summary>Texture alpha handling mode (Blend, Clip, Opaque).</summary>
        public AlphaMode Alpha;

        /// <summary>Texture array index for the top face (+Y).</summary>
        public ushort TileTop;

        /// <summary>Texture array index for the bottom face (-Y).</summary>
        public ushort TileBottom;

        /// <summary>Texture array index for the right face (+X).</summary>
        public ushort TileRight;

        /// <summary>Texture array index for the left face (-X).</summary>
        public ushort TileLeft;

        /// <summary>Texture array index for the front face (+Z).</summary>
        public ushort TileFront;

        /// <summary>Texture array index for the back face (-Z).</summary>
        public ushort TileBack;

        /// <summary>Whether sunlight passes through this node (0/1).</summary>
        public byte SunlightPropagates;

        /// <summary>Whether the player collides with this node (0/1).</summary>
        public byte Walkable;

        /// <summary>Whether the player can point at (highlight) this node (0/1).</summary>
        public byte Pointable;

        /// <summary>Whether this node can be dug/mined (0/1).</summary>
        public byte Diggable;

        /// <summary>Whether the player can climb this node like a ladder (0/1).</summary>
        public byte Climbable;

        /// <summary>Whether other nodes can be placed in this node's position (0/1).</summary>
        public byte BuildableTo;

        /// <summary>
        /// Whether cave generation can excavate this node.
        /// Ground content nodes (stone, dirt, gravel, etc.) are replaced with air during cave generation.
        /// Mirrors Luanti's <c>is_ground_content</c> in <c>luanti/src/nodedef.h</c>.
        /// </summary>
        /// <remarks>
        /// Before <see cref="Basalt.Core.NodeRegistry.Bake"/>: tri-state —
        /// <see cref="IS_GROUND_CONTENT_AUTO"/> (0, struct default) = auto-compute,
        /// <see cref="IS_GROUND_CONTENT_TRUE"/> (1) = explicitly yes,
        /// <see cref="IS_GROUND_CONTENT_FALSE"/> (2) = explicitly no.
        /// After Bake(): normalized to 0 (not ground) or 1 (ground).
        /// </remarks>
        public byte IsGroundContent;

        /// <summary>Liquid behavior type (None, Flowing, Source).</summary>
        public LiquidType Liquid;

        /// <summary>Liquid viscosity level (0-7). Higher = slower flow.</summary>
        public byte LiquidViscosity;

        /// <summary>Start index into the flat GroupEntry array.</summary>
        public int GroupsOffset;

        /// <summary>Number of group entries for this node.</summary>
        public int GroupsCount;

        /// <summary>Bitmask of sides that connect to neighbors (for connected nodeboxes).</summary>
        public byte ConnectSides;

        /// <summary>Damage per second dealt to a player standing inside this node.</summary>
        public byte DamagePerSecond;

        /// <summary>
        /// Physical solidness for face culling (0 = transparent, 1 = partial/liquid, 2 = fully opaque).
        /// Computed from Drawtype during Bake(). Mirrors Luanti's solidness in <c>luanti/src/nodedef.h</c>.
        /// </summary>
        public byte Solidness;

        /// <summary>
        /// Visual solidness for determining whether this node's own faces occlude neighbors (0 or 1).
        /// Computed from Drawtype during Bake(). Mirrors Luanti's visual_solidness.
        /// </summary>
        public byte VisualSolidness;

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public bool IsSunlightPropagating() => SunlightPropagates != 0;

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public bool IsWalkable() => Walkable != 0;

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public bool IsLiquid() => Liquid != LiquidType.None;

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public bool IsDrawnAsNormal() => Drawtype == DrawType.Normal;

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public bool IsAirlike() => Drawtype == DrawType.Airlike;

        /// <summary>Returns true when this node fully occludes adjacent faces from view.</summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public bool IsVisuallyOpaque() => VisualSolidness != 0;

        /// <summary>Returns true when this node should never contribute any geometry.</summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public bool HasNoGeometry() => Drawtype == DrawType.Airlike;
    }

    /// <summary>
    /// A single group entry (e.g. cracky=3). Stored in a flat array
    /// because Dictionary is not blittable for Burst job access.
    /// </summary>
    public struct GroupEntry
    {
        /// <summary>Group name resolved to an integer ID by the managed-side registry.</summary>
        public int GroupId;

        /// <summary>Group rating value (e.g. 3 for cracky=3).</summary>
        public int Value;
    }
}