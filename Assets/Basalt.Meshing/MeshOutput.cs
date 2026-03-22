using System;
using Unity.Collections;

namespace Basalt.Meshing
{
    /// <summary>
    /// Owns the NativeLists that receive vertices and triangles from a meshing job.
    /// Lifecycle: allocate before scheduling, read after completion, dispose always.
    /// </summary>
    /// <remarks>
    /// Uses NativeList because mesh output size is unknown before the job runs.
    /// Story 2.5 will replace NativeLists with pre-sized NativeArrays via Mesh.MeshData
    /// after a counting pre-pass (double-pass pattern from ADR-004).
    ///
    /// Worst case per chunk: 16x16x6 = 1536 faces, 6144 vertices, 9216 indices.
    /// </remarks>
    public sealed class MeshOutput : IDisposable
    {
        /// <summary>Initial NativeList capacity sized for a typical partial chunk.</summary>
        public const int INITIAL_VERTEX_CAPACITY = 2048;

        /// <summary>Initial NativeList capacity for triangle indices.</summary>
        public const int INITIAL_INDEX_CAPACITY = 3072;

        /// <summary>Vertex data written by the meshing job.</summary>
        public NativeList<VoxelVertex> Vertices;

        /// <summary>Triangle index data written by the meshing job (32-bit).</summary>
        public NativeList<int> Indices;

        /// <summary>Gets whether any vertex data has been written.</summary>
        public bool HasData => Vertices.Length > 0;

        /// <summary>
        /// Allocates the output lists with the given allocator.
        /// </summary>
        public MeshOutput(Allocator allocator)
        {
            Vertices = new NativeList<VoxelVertex>(INITIAL_VERTEX_CAPACITY, allocator);
            Indices = new NativeList<int>(INITIAL_INDEX_CAPACITY, allocator);
        }

        /// <summary>Resets both lists to length zero without releasing memory.</summary>
        public void Clear()
        {
            Vertices.Clear();
            Indices.Clear();
        }

        /// <summary>Disposes the vertex and index NativeLists and releases native memory.</summary>
        public void Dispose()
        {
            if (Vertices.IsCreated)
            {
                Vertices.Dispose();
            }

            if (Indices.IsCreated)
            {
                Indices.Dispose();
            }
        }
    }
}
