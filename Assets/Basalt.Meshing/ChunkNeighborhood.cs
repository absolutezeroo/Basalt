using System.Runtime.CompilerServices;
using Basalt.Core;
using Unity.Burst;
using Unity.Collections;

namespace Basalt.Meshing
{
    /// <summary>
    /// An 18x18x18 padded voxel buffer covering a center chunk plus a one-node-thick shell
    /// from each of the six face-adjacent neighbors.
    /// </summary>
    /// <remarks>
    /// Layout: index = (x+1) + (y+1)*18 + (z+1)*324, where chunk-local coords range [-1..16].
    /// Positions [0..17] on each axis map to [-1..16] in chunk-local space.
    /// The center chunk occupies [1..16] on each axis.
    /// Nodes outside any loaded neighbor are filled with CONTENT_IGNORE.
    ///
    /// This struct owns no native memory. It wraps a caller-owned NativeArray of 5832 elements.
    /// Use <see cref="ChunkNeighborhoodBuilder"/> to populate it.
    ///
    /// Design mirrors Luanti's VoxelArea padding in <c>luanti/src/voxel.h</c>.
    /// </remarks>
    [BurstCompile]
    public readonly struct ChunkNeighborhood
    {
        /// <summary>Total node count in the padded buffer (18 cubed = 5832).</summary>
        public const int PADDED_SIZE = 18 * 18 * 18;

        /// <summary>Side length of the padded buffer.</summary>
        public const int PADDED_DIM = 18;

        /// <summary>Stride for Y axis in the padded buffer.</summary>
        public const int PADDED_STRIDE_Y = 18;

        /// <summary>Stride for Z axis in the padded buffer (18 squared = 324).</summary>
        public const int PADDED_STRIDE_Z = 324;

        /// <summary>Packed node data for the padded 18 cubed region.</summary>
        [ReadOnly]
        public readonly NativeArray<uint> Nodes;

        /// <summary>Initializes the neighborhood wrapping an existing 5832-element array.</summary>
        public ChunkNeighborhood(NativeArray<uint> nodes)
        {
            Nodes = nodes;
        }

        /// <summary>
        /// Computes the flat index for a position in padded space.
        /// Input range: x, y, z in [-1..16] (chunk-local coordinates).
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static int PaddedIndex(int x, int y, int z)
            => (x + 1) + (y + 1) * PADDED_STRIDE_Y + (z + 1) * PADDED_STRIDE_Z;

        /// <summary>
        /// Gets the packed node uint at the given chunk-local position (range [-1..16]).
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public uint GetPacked(int x, int y, int z)
            => Nodes[PaddedIndex(x, y, z)];

        /// <summary>
        /// Gets the content ID at the given chunk-local position (range [-1..16]).
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public ushort GetContent(int x, int y, int z)
            => (ushort)(Nodes[PaddedIndex(x, y, z)] >> 16);
    }
}