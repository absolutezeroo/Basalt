using Unity.Mathematics;

namespace Basalt.Meshing
{
    /// <summary>
    /// Static lookup tables for face geometry, normals, and neighbor offsets.
    /// All arrays are indexed by <see cref="FaceDirection"/> (0-5).
    /// </summary>
    /// <remarks>
    /// WARNING: These are managed arrays. Do NOT access from Burst-compiled jobs.
    /// Jobs use inlined switch expressions (see <see cref="GreedyMeshJob"/>) instead.
    /// These tables serve as the canonical reference and are used on the managed side only.
    ///
    /// Quad vertex winding: counter-clockwise when viewed from outside
    /// (Unity left-handed front-face convention).
    /// Vertex order per quad: v0, v1, v2, v3 forming two CCW triangles (0,1,2) and (0,2,3).
    /// </remarks>
    public static class MeshingTables
    {
        /// <summary>
        /// Neighbor offset for each face direction, indexed by FaceDirection.
        /// </summary>
        public static readonly int3[] NeighborOffsets = new int3[6]
        {
            new int3(0, 1, 0),    // PosY (top)
            new int3(0, -1, 0),   // NegY (bottom)
            new int3(1, 0, 0),    // PosX (right)
            new int3(-1, 0, 0),   // NegX (left)
            new int3(0, 0, 1),    // PosZ (front)
            new int3(0, 0, -1),   // NegZ (back)
        };

        /// <summary>
        /// Face normals for each face direction, indexed by FaceDirection.
        /// </summary>
        public static readonly float3[] Normals = new float3[6]
        {
            new float3(0, 1, 0),    // PosY
            new float3(0, -1, 0),   // NegY
            new float3(1, 0, 0),    // PosX
            new float3(-1, 0, 0),   // NegX
            new float3(0, 0, 1),    // PosZ
            new float3(0, 0, -1),   // NegZ
        };

        /// <summary>
        /// Four vertex positions per face (6 faces x 4 vertices = 24 entries).
        /// Positions are offsets from the node origin in node units (0 or 1).
        /// Indexed as [faceDir * 4 + vertexIndex].
        /// Winding is counter-clockwise when viewed from outside (Unity left-handed front-face convention).
        /// </summary>
        public static readonly float3[] FaceVertices = new float3[24]
        {
            // PosY (top, y=1 plane) — viewed from above, CCW
            new float3(0, 1, 0),
            new float3(0, 1, 1),
            new float3(1, 1, 1),
            new float3(1, 1, 0),

            // NegY (bottom, y=0 plane) — viewed from below, CCW
            new float3(0, 0, 1),
            new float3(0, 0, 0),
            new float3(1, 0, 0),
            new float3(1, 0, 1),

            // PosX (right, x=1 plane) — viewed from +X, CCW
            new float3(1, 0, 1),
            new float3(1, 0, 0),
            new float3(1, 1, 0),
            new float3(1, 1, 1),

            // NegX (left, x=0 plane) — viewed from -X, CCW
            new float3(0, 0, 0),
            new float3(0, 0, 1),
            new float3(0, 1, 1),
            new float3(0, 1, 0),

            // PosZ (front, z=1 plane) — viewed from +Z, CCW
            new float3(0, 0, 1),
            new float3(1, 0, 1),
            new float3(1, 1, 1),
            new float3(0, 1, 1),

            // NegZ (back, z=0 plane) — viewed from -Z, CCW
            new float3(1, 0, 0),
            new float3(0, 0, 0),
            new float3(0, 1, 0),
            new float3(1, 1, 0),
        };
    }
}