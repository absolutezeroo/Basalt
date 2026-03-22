using System;
using System.Runtime.CompilerServices;
using Unity.Burst;
using Unity.Collections;

namespace Basalt.Core
{
    /// <summary>
    /// A 16³ block of voxel nodes, matching Luanti's MapBlock.
    /// Stores 4096 nodes as bitpacked uint in a NativeArray.
    /// Memory: exactly 16,384 bytes of node data per block.
    /// </summary>
    [BurstCompile]
    public struct MapBlock : IDisposable
    {
        public NativeArray<uint> Nodes;

        /// <summary>
        /// Allocates a new MapBlock with 4096 nodes.
        /// </summary>
        public MapBlock(Allocator allocator)
        {
            Nodes = new NativeArray<uint>(BasaltConstants.NODES_PER_BLOCK, allocator);
        }

        /// <summary>
        /// Allocates a new MapBlock and fills all nodes with the given content ID.
        /// </summary>
        public MapBlock(Allocator allocator, ushort fillContent)
        {
            Nodes = new NativeArray<uint>(BasaltConstants.NODES_PER_BLOCK, allocator);
            uint packed = MapNode.Pack(fillContent, 0, 0);
            for (int i = 0; i < BasaltConstants.NODES_PER_BLOCK; i++)
                Nodes[i] = packed;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public MapNode GetNode(int x, int y, int z)
        {
            return new MapNode(Nodes[CoordinateUtils.NodeIndex(x, y, z)]);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void SetNode(int x, int y, int z, MapNode node)
        {
            Nodes[CoordinateUtils.NodeIndex(x, y, z)] = node.Raw;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public MapNode GetNodeByIndex(int index)
        {
            return new MapNode(Nodes[index]);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void SetNodeByIndex(int index, MapNode node)
        {
            Nodes[index] = node.Raw;
        }

        public bool IsCreated => Nodes.IsCreated;

        public void Dispose()
        {
            if (Nodes.IsCreated)
                Nodes.Dispose();
        }
    }
}