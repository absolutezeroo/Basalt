using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using Unity.Mathematics;

namespace Basalt.Meshing
{
    /// <summary>
    /// Blittable vertex format for voxel mesh output.
    /// Contains position, UV, texture index, face direction, and AO placeholder.
    /// </summary>
    /// <remarks>
    /// Layout: 24 bytes per vertex, blittable for NativeList storage and GPU upload.
    /// Designed for standard Unity Mesh API (SetVertexBufferData).
    /// Story 2.4 may replace this with a packed format when the custom voxel shader is written.
    /// </remarks>
    [StructLayout(LayoutKind.Sequential)]
    public struct VoxelVertex
    {
        /// <summary>Vertex position in chunk-local space (origin = chunk minimum corner).</summary>
        public float3 Position;

        /// <summary>Face normal direction (outward-facing).</summary>
        public float3 Normal;

        /// <summary>
        /// Packed data: texture array index in X, AO level (0-3) in Y.
        /// Reserved for Story 2.3 (AO) and Story 2.4 (texture arrays).
        /// </summary>
        public float2 Data;

        /// <summary>
        /// Creates a vertex with the given position, normal, texture index, and AO level.
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public VoxelVertex(float3 position, float3 normal, ushort textureIndex, byte aoLevel)
        {
            Position = position;
            Normal = normal;
            Data = new float2(textureIndex, aoLevel);
        }
    }
}