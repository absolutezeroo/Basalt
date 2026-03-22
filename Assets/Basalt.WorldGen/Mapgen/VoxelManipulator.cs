using System;
using System.Runtime.CompilerServices;
using Unity.Burst;
using Unity.Collections;
using Unity.Mathematics;

namespace Basalt.WorldGen
{
    /// <summary>
    /// Contiguous node buffer for one mapchunk generation volume (80x82x80),
    /// including 1-node vertical overgeneration on each side.
    /// </summary>
    /// <remarks>
    /// Layout: x varies fastest, then y, then z.
    /// Index = dx + dy * Sx + dz * Sx * Sy
    ///   where Sx=80, Sy=82, Sz=80 and dx/dy/dz are offsets from <see cref="MinPos"/>.
    ///
    /// MinPos.y = chunkOrigin.y - OVERGEN_SIZE, so local y=0 is the bottom overgeneration
    /// layer, local y=1 is the first real mapchunk node, and local y=81 is the top
    /// overgeneration layer.
    ///
    /// Matches Luanti's VoxelManipulator semantics.
    /// Source: <c>luanti/src/voxel.h</c>.
    /// </remarks>
    [BurstCompile]
    public struct VoxelManipulator : IDisposable
    {
        /// <summary>World position of the minimum-corner node (includes overgeneration).</summary>
        public int3 MinPos;

        /// <summary>World position of the maximum-corner node (inclusive, includes overgeneration).</summary>
        public int3 MaxPos;

        /// <summary>
        /// Flat node buffer, length = Sx * Sy * Sz.
        /// Each element is a bitpacked uint: [31..16] content, [15..8] param1, [7..0] param2.
        /// </summary>
        public NativeArray<uint> Nodes;

        /// <summary>
        /// Surface heightmap output, size Sx * Sz (80 x 80).
        /// Index = dx + dz * Sx. Filled during terrain generation.
        /// </summary>
        public NativeArray<short> Heightmap;

        /// <summary>Buffer X-extent (always 80).</summary>
        public const int Sx = MapgenV7Constants.MAPCHUNK_SIZE;

        /// <summary>Buffer Y-extent including overgeneration (always 82).</summary>
        public const int Sy = MapgenV7Constants.VM_HEIGHT;

        /// <summary>Buffer Z-extent (always 80).</summary>
        public const int Sz = MapgenV7Constants.MAPCHUNK_SIZE;

        /// <summary>Gets whether the buffers have been allocated.</summary>
        public bool IsCreated => Nodes.IsCreated;

        /// <summary>
        /// Allocates node and heightmap buffers for a mapchunk.
        /// MinPos is set to (chunkOrigin.x, chunkOrigin.y - OVERGEN_SIZE, chunkOrigin.z).
        /// </summary>
        /// <param name="chunkOrigin">
        /// World position of the mapchunk's minimum corner (first real node, not overgeneration).
        /// </param>
        /// <param name="allocator">Memory allocator (typically <c>Allocator.TempJob</c>).</param>
        public VoxelManipulator(int3 chunkOrigin, Allocator allocator)
        {
            MinPos = new int3(
                chunkOrigin.x,
                chunkOrigin.y - MapgenV7Constants.OVERGEN_SIZE,
                chunkOrigin.z);

            MaxPos = new int3(
                chunkOrigin.x + Sx - 1,
                chunkOrigin.y + MapgenV7Constants.MAPCHUNK_SIZE + MapgenV7Constants.OVERGEN_SIZE - 1,
                chunkOrigin.z + Sz - 1);

            Nodes = new NativeArray<uint>(MapgenV7Constants.VM_NODE_COUNT, allocator);
            Heightmap = new NativeArray<short>(Sx * Sz, allocator);
        }

        /// <summary>
        /// Returns the flat buffer index for a world position.
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public int WorldToIndex(int3 worldPos)
        {
            int dx = worldPos.x - MinPos.x;
            int dy = worldPos.y - MinPos.y;
            int dz = worldPos.z - MinPos.z;

            return dx + dy * Sx + dz * Sx * Sy;
        }

        /// <summary>
        /// Returns the flat buffer index for local offsets from MinPos.
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static int LocalToIndex(int dx, int dy, int dz)
        {
            return dx + dy * Sx + dz * Sx * Sy;
        }

        /// <summary>
        /// Returns the heightmap index for a local X/Z offset from the mapchunk XZ origin.
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static int HeightmapIndex(int dx, int dz)
        {
            return dx + dz * Sx;
        }

        /// <summary>
        /// Gets the packed node value at a world position.
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public uint GetNode(int3 worldPos)
        {
            return Nodes[WorldToIndex(worldPos)];
        }

        /// <summary>
        /// Sets the packed node value at a world position.
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void SetNode(int3 worldPos, uint packed)
        {
            Nodes[WorldToIndex(worldPos)] = packed;
        }

        /// <summary>
        /// Disposes the node and heightmap buffers.
        /// </summary>
        public void Dispose()
        {
            if (Nodes.IsCreated)
            {
                Nodes.Dispose();
            }

            if (Heightmap.IsCreated)
            {
                Heightmap.Dispose();
            }
        }
    }
}
