using UnityEngine;
using UnityEngine.Rendering;

namespace Basalt.Client
{
    /// <summary>
    /// Shared vertex layout descriptor for voxel chunk meshes.
    /// Matches the <see cref="Basalt.Meshing.VoxelVertex"/> struct layout (40 bytes).
    /// </summary>
    internal static class VoxelVertexLayout
    {
        /// <summary>
        /// Vertex attribute descriptors for the voxel mesh format.
        /// Used by both <c>SetVertexBufferParams</c> and <c>MeshData.SetVertexBufferParams</c>.
        /// </summary>
        internal static readonly VertexAttributeDescriptor[] Descriptors =
        {
            new(VertexAttribute.Position, VertexAttributeFormat.Float32, 3),
            new(VertexAttribute.Normal, VertexAttributeFormat.Float32, 3),
            new(VertexAttribute.TexCoord0, VertexAttributeFormat.Float32, 2),
            new(VertexAttribute.TexCoord1, VertexAttributeFormat.Float32, 2),
        };
    }
}
