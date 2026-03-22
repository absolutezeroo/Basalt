using System;
using System.Runtime.CompilerServices;
using Unity.Burst;
using Unity.Collections;

namespace Basalt.Core
{
    /// <summary>
    /// A 16³ block of voxel nodes, matching Luanti's MapBlock.
    /// Stores 4096 nodes as bitpacked uint in a NativeArray.
    /// </summary>
    /// <remarks>
    /// Memory: exactly 16,384 bytes of node data per block (4096 × 4 bytes).
    /// Source: <c>luanti/src/mapblock.h</c>.
    /// </remarks>
    [BurstCompile]
    public struct MapBlock : IDisposable
    {
        /// <summary>Packed node data for all 4096 nodes in this block.</summary>
        public NativeArray<uint> Nodes;

        /// <summary>
        /// Allocates a new MapBlock with 4096 uninitialized nodes.
        /// </summary>
        public MapBlock(Allocator allocator)
        {
            Nodes = new NativeArray<uint>(BasaltConstants.NODES_PER_BLOCK, allocator);
        }

        /// <summary>
        /// Allocates a new MapBlock and fills all nodes with the given content ID.
        /// </summary>
        /// <param name="allocator">The memory allocator to use.</param>
        /// <param name="fillContent">The content ID to fill every node with.</param>
        public MapBlock(Allocator allocator, ushort fillContent)
        {
            Nodes = new NativeArray<uint>(BasaltConstants.NODES_PER_BLOCK, allocator);
            uint packed = MapNode.Pack(fillContent, 0, 0);

            for (int i = 0; i < BasaltConstants.NODES_PER_BLOCK; i++)
            {
                Nodes[i] = packed;
            }
        }

        /// <summary>
        /// Gets the node at the given local coordinates within this block.
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public MapNode GetNode(int x, int y, int z)
        {
            return new MapNode(Nodes[CoordinateUtils.NodeIndex(x, y, z)]);
        }

        /// <summary>
        /// Sets the node at the given local coordinates within this block.
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void SetNode(int x, int y, int z, MapNode node)
        {
            Nodes[CoordinateUtils.NodeIndex(x, y, z)] = node.Packed;
        }

        /// <summary>
        /// Gets the node at the given linear index (0-4095).
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public MapNode GetNodeByIndex(int index)
        {
            return new MapNode(Nodes[index]);
        }

        /// <summary>
        /// Sets the node at the given linear index (0-4095).
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void SetNodeByIndex(int index, MapNode node)
        {
            Nodes[index] = node.Packed;
        }

        /// <summary>Gets whether the underlying NativeArray has been allocated.</summary>
        public bool IsCreated => Nodes.IsCreated;

        public void Dispose()
        {
            if (Nodes.IsCreated)
            {
                Nodes.Dispose();
            }
        }
    }
}