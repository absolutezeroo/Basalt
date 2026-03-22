using System.Runtime.CompilerServices;
using Unity.Burst;

namespace Basalt.Core
{
    /// <summary>
    /// Represents a single voxel node with content type and parameters.
    /// Layout mirrors Luanti's MapNode: content_t (u16) + param1 (u8) + param2 (u8).
    /// </summary>
    /// <remarks>
    /// Packed into a single uint32 for Burst compatibility.
    /// Bit layout: [31..16] content, [15..8] param1, [7..0] param2.
    /// Source: <c>luanti/src/mapnode.h</c>.
    /// </remarks>
    [BurstCompile]
    public readonly struct MapNode
    {
        /// <summary>Raw packed data containing content, param1, and param2.</summary>
        public readonly uint Packed;

        /// <summary>Gets the node type identifier (0-65535).</summary>
        public ushort Content
        {
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            get => (ushort)(Packed >> 16);
        }

        /// <summary>Gets the lighting data (bits 0-3 night, bits 4-7 day).</summary>
        public byte Param1
        {
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            get => (byte)((Packed >> 8) & 0xFF);
        }

        /// <summary>Gets the rotation or auxiliary data.</summary>
        public byte Param2
        {
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            get => (byte)(Packed & 0xFF);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public MapNode(ushort content, byte param1 = 0, byte param2 = 0)
        {
            Packed = Pack(content, param1, param2);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public MapNode(uint packed)
        {
            Packed = packed;
        }

        /// <summary>
        /// Packs content, param1, and param2 into a single uint32.
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static uint Pack(ushort content, byte param1, byte param2)
            => ((uint)content << 16) | ((uint)param1 << 8) | param2;

        /// <summary>
        /// Unpacks a uint32 into its content, param1, and param2 components.
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void Unpack(uint packed, out ushort content, out byte param1, out byte param2)
        {
            content = (ushort)(packed >> 16);
            param1 = (byte)((packed >> 8) & 0xFF);
            param2 = (byte)(packed & 0xFF);
        }

        /// <summary>Returns a node representing air (content=126, param1=0, param2=0).</summary>
        public static MapNode Air
        {
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            get => new(BasaltConstants.CONTENT_AIR);
        }

        /// <summary>Returns a node representing ignore (content=127, param1=0, param2=0).</summary>
        public static MapNode Ignore
        {
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            get => new(BasaltConstants.CONTENT_IGNORE);
        }
    }
}