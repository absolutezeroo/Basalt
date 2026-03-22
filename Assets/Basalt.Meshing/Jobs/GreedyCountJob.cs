using Basalt.Core;
using Unity.Burst;
using Unity.Collections;
using Unity.Jobs;

namespace Basalt.Meshing
{
    /// <summary>
    /// First pass of the double-pass meshing pipeline: runs the full greedy algorithm
    /// in count-only mode to determine exact vertex and index counts.
    /// </summary>
    /// <remarks>
    /// The output counts are used to allocate a correctly-sized <c>Mesh.MeshData</c>
    /// before scheduling <see cref="GreedyWriteJob"/>. This eliminates the need for
    /// dynamically-growing NativeLists and enables direct GPU buffer writes.
    ///
    /// The algorithm is identical to <see cref="GreedyWriteJob"/> — both delegate
    /// to <see cref="GreedyKernel"/>. The count pass skips vertex/index emission,
    /// incrementing counters instead.
    /// </remarks>
    [BurstCompile]
    public struct GreedyCountJob : IJob
    {
        /// <summary>Packed node data for the 18-cubed padded neighborhood (5832 elements).</summary>
        [ReadOnly]
        public NativeArray<uint> PaddedNodes;

        /// <summary>Node definitions indexed by content_id for culling and tile lookups.</summary>
        [ReadOnly]
        public NativeArray<NodeDefinition> NodeDefs;

        /// <summary>
        /// Output counts: [0] = vertex count, [1] = index count.
        /// Must be pre-allocated with length 2.
        /// </summary>
        [WriteOnly]
        public NativeArray<int> OutCounts;

        /// <summary>Runs the greedy algorithm in count-only mode.</summary>
        public void Execute()
        {
            GreedyKernel.RunCount(PaddedNodes, NodeDefs, OutCounts);
        }
    }
}
