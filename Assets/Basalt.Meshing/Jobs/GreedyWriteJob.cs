using Basalt.Core;
using Unity.Burst;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;
using Unity.Jobs;

namespace Basalt.Meshing
{
    /// <summary>
    /// Second pass of the double-pass meshing pipeline: runs the full greedy algorithm
    /// and writes vertices and indices into pre-sized NativeArrays from <c>Mesh.MeshData</c>.
    /// </summary>
    /// <remarks>
    /// The output arrays are obtained from <c>meshData.GetVertexData&lt;VoxelVertex&gt;()</c>
    /// and <c>meshData.GetIndexData&lt;int&gt;()</c> on the main thread before scheduling.
    /// Their sizes match the counts produced by <see cref="GreedyCountJob"/>.
    ///
    /// This job writes directly into GPU-uploadable buffers, enabling
    /// <c>Mesh.ApplyAndDisposeWritableMeshData</c> without intermediate copies.
    /// </remarks>
    [BurstCompile]
    public struct GreedyWriteJob : IJob
    {
        /// <summary>Packed node data for the 18-cubed padded neighborhood (5832 elements).</summary>
        [ReadOnly]
        [NativeDisableContainerSafetyRestriction]
        public NativeArray<uint> PaddedNodes;

        /// <summary>Node definitions indexed by content_id for culling and tile lookups.</summary>
        [ReadOnly]
        public NativeArray<NodeDefinition> NodeDefs;

        /// <summary>Pre-sized vertex output array from Mesh.MeshData.</summary>
        [NativeDisableContainerSafetyRestriction]
        public NativeArray<VoxelVertex> OutputVertices;

        /// <summary>Pre-sized index output array from Mesh.MeshData.</summary>
        [NativeDisableContainerSafetyRestriction]
        public NativeArray<int> OutputIndices;

        /// <summary>Runs the greedy algorithm in write mode.</summary>
        public void Execute()
        {
            GreedyKernel.RunWrite(PaddedNodes, NodeDefs, OutputVertices, OutputIndices);
        }
    }
}
