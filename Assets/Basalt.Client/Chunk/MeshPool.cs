using System;
using System.Collections.Generic;
using UnityEngine;

namespace Basalt.Client
{
    /// <summary>
    /// Managed pool of Unity <see cref="Mesh"/> objects for chunk rendering.
    /// Pre-creates all meshes at startup to avoid per-frame GC allocations
    /// from <c>new Mesh()</c>.
    /// </summary>
    /// <remarks>
    /// Each rented mesh is reused across remeshes. Meshes are returned on chunk unload.
    /// Meshes are marked as <c>MarkDynamic()</c> to hint to Unity that their data
    /// changes frequently.
    /// </remarks>
    internal sealed class MeshPool : IDisposable
    {
        private readonly Stack<Mesh> _available;
        private int _capacity;

        /// <summary>Gets the number of available meshes.</summary>
        public int FreeCount => _available.Count;

        /// <summary>
        /// Creates a mesh pool with the specified capacity.
        /// All meshes are pre-created.
        /// </summary>
        /// <param name="capacity">Total number of meshes to pre-create.</param>
        public MeshPool(int capacity)
        {
            _capacity = capacity;
            _available = new Stack<Mesh>(capacity);

            for (int i = 0; i < capacity; i++)
            {
                var mesh = new Mesh { name = $"ChunkMesh_{i}" };
                mesh.MarkDynamic();
                _available.Push(mesh);
            }
        }

        /// <summary>
        /// Rents a mesh from the pool.
        /// </summary>
        /// <param name="mesh">The rented mesh, or null if the pool is exhausted.</param>
        /// <returns>True if a mesh was available.</returns>
        public bool TryRent(out Mesh mesh)
        {
            if (_available.Count == 0)
            {
                mesh = null;

                return false;
            }

            mesh = _available.Pop();

            return true;
        }

        /// <summary>
        /// Returns a mesh to the pool. The mesh is cleared before return.
        /// </summary>
        /// <param name="mesh">The mesh obtained from <see cref="TryRent"/>.</param>
        public void Return(Mesh mesh)
        {
            mesh.Clear();
            _available.Push(mesh);
        }

        /// <summary>Destroys all pooled meshes.</summary>
        public void Dispose()
        {
            while (_available.Count > 0)
            {
                Mesh mesh = _available.Pop();

                if (mesh != null)
                {
                    UnityEngine.Object.Destroy(mesh);
                }
            }
        }
    }
}
