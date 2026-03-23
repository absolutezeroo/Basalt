using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using Unity.Mathematics;

namespace Basalt.Meshing
{
    /// <summary>
    /// Blittable vertex format for voxel mesh output.
    /// Contains position, normal, tiling UV, texture array index, AO, and light levels.
    /// </summary>
    /// <remarks>
    /// Layout: 48 bytes per vertex, blittable for NativeList storage and GPU upload.
    /// Designed for standard Unity Mesh API (SetVertexBufferData).
    ///
    /// GPU attribute mapping:
    ///   POSITION  = Position (float3, 12 bytes, offset  0)
    ///   NORMAL    = Normal   (float3, 12 bytes, offset 12)
    ///   TEXCOORD0 = UV       (float2,  8 bytes, offset 24) — tiling UVs for greedy quads
    ///   TEXCOORD1 = Data     (float4, 16 bytes, offset 32) — x=tileIndex, y=aoLevel, z=dayLight, w=nightLight
    /// </remarks>
    [StructLayout(LayoutKind.Sequential)]
    public struct VoxelVertex
    {
        /// <summary>Vertex position in chunk-local space (origin = chunk minimum corner).</summary>
        public float3 Position;

        /// <summary>Face normal direction (outward-facing).</summary>
        public float3 Normal;

        /// <summary>
        /// Tiling UV coordinates for greedy-merged quads.
        /// A 4x3 merged quad has UV ranging from (0,0) to (4,3), tiling the texture
        /// at each integer boundary via TextureWrapMode.Repeat.
        /// </summary>
        public float2 UV;

        /// <summary>
        /// Packed data channel.
        /// X = texture array slice index (integer stored as float, recovered via round()).
        /// Y = ambient occlusion level (0-3, 0=dark, 3=bright).
        /// Z = day light level (0-15, from param1 upper nibble).
        /// W = night light level (0-15, from param1 lower nibble).
        /// </summary>
        public float4 Data;

        /// <summary>
        /// Creates a vertex with the given position, normal, UV, texture index, AO, and light levels.
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public VoxelVertex(float3 position, float3 normal, float2 uv,
            ushort textureIndex, byte aoLevel, byte dayLight, byte nightLight)
        {
            Position = position;
            Normal = normal;
            UV = uv;
            Data = new float4(textureIndex, aoLevel, dayLight, nightLight);
        }
    }
}
