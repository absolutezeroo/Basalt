using System.Runtime.CompilerServices;
using Unity.Burst;

namespace Basalt.Core
{
    /// <summary>
    /// A single voxel node stored as a bitpacked uint32.
    /// Layout matches Luanti exactly: [31..16] content_t (u16), [15..8] param1 (u8), [7..0] param2 (u8).
    /// </summary>
    [BurstCompile]
    public struct MapNode
    {
        public uint Raw;

        public ushort Content
        {
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            get => (ushort)(Raw >> 16);
        }

        public byte Param1
        {
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            get => (byte)((Raw >> 8) & 0xFF);
        }

        public byte Param2
        {
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            get => (byte)(Raw & 0xFF);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public MapNode(ushort content, byte param1, byte param2)
        {
            Raw = Pack(content, param1, param2);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public MapNode(uint raw)
        {
            Raw = raw;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static uint Pack(ushort content, byte param1, byte param2)
            => ((uint)content << 16) | ((uint)param1 << 8) | param2;

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
            get => new MapNode(BasaltConstants.CONTENT_AIR, 0, 0);
        }

        /// <summary>Returns a node representing ignore (content=127, param1=0, param2=0).</summary>
        public static MapNode Ignore
        {
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            get => new MapNode(BasaltConstants.CONTENT_IGNORE, 0, 0);
        }
    }
}
