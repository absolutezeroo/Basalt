using System.Runtime.CompilerServices;
using Basalt.Core;
using Unity.Burst;
using Unity.Collections;

namespace Basalt.Meshing
{
    /// <summary>
    /// Static utility methods for per-vertex ambient occlusion in the greedy meshing pipeline.
    /// All methods are Burst-compatible, allocation-free, and inlineable.
    /// </summary>
    /// <remarks>
    /// Implements the standard voxel AO algorithm from 0fps.net:
    ///   - Each face vertex samples 3 neighboring voxels (two sides + one diagonal corner).
    ///   - AO level = 0 (fully occluded) when both sides are solid.
    ///   - AO level = 3 (fully lit) when all three neighbors are clear.
    ///   - Formula: if (side1 AND side2) => 0, else => 3 - (side1 + side2 + corner).
    ///
    /// Packing convention: bits [2k+1 : 2k] hold ao_k for vertex k (k=0..3).
    /// This matches the vertex ordering in <see cref="GreedyMeshJob.GetQuadVertex"/>.
    ///
    /// Reference: https://0fps.net/2013/07/03/ambient-occlusion-for-minecraft-like-worlds/
    /// Luanti equivalent: <c>mapblock_mesh.cpp</c> getSmoothLightSolid/getSmoothLightTransparent.
    /// </remarks>
    [BurstCompile]
    public static class AmbientOcclusionUtils
    {
        /// <summary>
        /// Computes AO level for one vertex given its three neighbor solid flags.
        /// </summary>
        /// <param name="side1">1 if the first tangent neighbor is solid, 0 otherwise.</param>
        /// <param name="side2">1 if the second tangent neighbor is solid, 0 otherwise.</param>
        /// <param name="corner">1 if the diagonal corner neighbor is solid, 0 otherwise.</param>
        /// <returns>AO level in [0, 3]. 0 = fully occluded, 3 = no occlusion.</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static byte VertexAO(int side1, int side2, int corner)
        {
            if (side1 == 1 && side2 == 1)
            {
                return 0;
            }

            return (byte)(3 - (side1 + side2 + corner));
        }

        /// <summary>
        /// Returns 1 if the node at the given chunk-local position has Solidness at least 2, else 0.
        /// Returns 0 for out-of-range content IDs (treated as non-solid).
        /// </summary>
        /// <param name="paddedNodes">The 18-cubed padded neighborhood buffer.</param>
        /// <param name="nodeDefs">Node definitions indexed by content_id.</param>
        /// <param name="x">Chunk-local X coordinate (range [-1..16]).</param>
        /// <param name="y">Chunk-local Y coordinate (range [-1..16]).</param>
        /// <param name="z">Chunk-local Z coordinate (range [-1..16]).</param>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static int IsSolid(NativeArray<uint> paddedNodes,
            NativeArray<NodeDefinition> nodeDefs, int x, int y, int z)
        {
            ushort content = (ushort)(paddedNodes[ChunkNeighborhood.PaddedIndex(x, y, z)] >> 16);

            if (content >= nodeDefs.Length)
            {
                return 0;
            }

            return nodeDefs[content].Solidness >= 2 ? 1 : 0;
        }

        /// <summary>
        /// Computes the 4 per-vertex AO levels for a face at node (x, y, z) in the given direction.
        /// Vertex indices match the ordering in <see cref="GreedyMeshJob.GetQuadVertex"/>.
        /// </summary>
        /// <remarks>
        /// Each vertex corner samples 8 neighbor positions on the face plane (2 sides + 1 corner
        /// per vertex, 4 sides + 4 corners total). The face plane is offset by the face normal.
        /// All sample positions fall within [-1..16], guaranteed by the 18-cubed padded buffer.
        /// </remarks>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void ComputeFaceAO(
            NativeArray<uint> paddedNodes,
            NativeArray<NodeDefinition> nodeDefs,
            int x, int y, int z, int faceDir,
            out byte ao0, out byte ao1, out byte ao2, out byte ao3)
        {
            switch (faceDir)
            {
                case 0: // PosY — face plane at y+1, tangents X and Z
                {
                    int py = y + 1;
                    int xm = IsSolid(paddedNodes, nodeDefs, x - 1, py, z);
                    int xp = IsSolid(paddedNodes, nodeDefs, x + 1, py, z);
                    int zm = IsSolid(paddedNodes, nodeDefs, x, py, z - 1);
                    int zp = IsSolid(paddedNodes, nodeDefs, x, py, z + 1);
                    int xmzm = IsSolid(paddedNodes, nodeDefs, x - 1, py, z - 1);
                    int xmzp = IsSolid(paddedNodes, nodeDefs, x - 1, py, z + 1);
                    int xpzm = IsSolid(paddedNodes, nodeDefs, x + 1, py, z - 1);
                    int xpzp = IsSolid(paddedNodes, nodeDefs, x + 1, py, z + 1);

                    // v0=(x, y+1, z)      v1=(x, y+1, z+1)
                    // v2=(x+1, y+1, z+1)  v3=(x+1, y+1, z)
                    ao0 = VertexAO(xm, zm, xmzm);
                    ao1 = VertexAO(xm, zp, xmzp);
                    ao2 = VertexAO(xp, zp, xpzp);
                    ao3 = VertexAO(xp, zm, xpzm);
                    break;
                }
                case 1: // NegY — face plane at y, AO samples one step in -Y normal direction
                {
                    int py = y - 1;
                    int xm = IsSolid(paddedNodes, nodeDefs, x - 1, py, z);
                    int xp = IsSolid(paddedNodes, nodeDefs, x + 1, py, z);
                    int zm = IsSolid(paddedNodes, nodeDefs, x, py, z - 1);
                    int zp = IsSolid(paddedNodes, nodeDefs, x, py, z + 1);
                    int xmzm = IsSolid(paddedNodes, nodeDefs, x - 1, py, z - 1);
                    int xmzp = IsSolid(paddedNodes, nodeDefs, x - 1, py, z + 1);
                    int xpzm = IsSolid(paddedNodes, nodeDefs, x + 1, py, z - 1);
                    int xpzp = IsSolid(paddedNodes, nodeDefs, x + 1, py, z + 1);

                    // v0=(x, y, z+1)    v1=(x, y, z)
                    // v2=(x+1, y, z)    v3=(x+1, y, z+1)
                    ao0 = VertexAO(xm, zp, xmzp);
                    ao1 = VertexAO(xm, zm, xmzm);
                    ao2 = VertexAO(xp, zm, xpzm);
                    ao3 = VertexAO(xp, zp, xpzp);
                    break;
                }
                case 2: // PosX — face plane at x+1, tangents Y and Z
                {
                    int px = x + 1;
                    int ym = IsSolid(paddedNodes, nodeDefs, px, y - 1, z);
                    int yp = IsSolid(paddedNodes, nodeDefs, px, y + 1, z);
                    int zm = IsSolid(paddedNodes, nodeDefs, px, y, z - 1);
                    int zp = IsSolid(paddedNodes, nodeDefs, px, y, z + 1);
                    int ymzm = IsSolid(paddedNodes, nodeDefs, px, y - 1, z - 1);
                    int ymzp = IsSolid(paddedNodes, nodeDefs, px, y - 1, z + 1);
                    int ypzm = IsSolid(paddedNodes, nodeDefs, px, y + 1, z - 1);
                    int ypzp = IsSolid(paddedNodes, nodeDefs, px, y + 1, z + 1);

                    // v0=(x+1, y, z+1)    v1=(x+1, y, z)
                    // v2=(x+1, y+1, z)    v3=(x+1, y+1, z+1)
                    ao0 = VertexAO(ym, zp, ymzp);
                    ao1 = VertexAO(ym, zm, ymzm);
                    ao2 = VertexAO(yp, zm, ypzm);
                    ao3 = VertexAO(yp, zp, ypzp);
                    break;
                }
                case 3: // NegX — face plane at x, AO samples one step in -X normal direction
                {
                    int px = x - 1;
                    int ym = IsSolid(paddedNodes, nodeDefs, px, y - 1, z);
                    int yp = IsSolid(paddedNodes, nodeDefs, px, y + 1, z);
                    int zm = IsSolid(paddedNodes, nodeDefs, px, y, z - 1);
                    int zp = IsSolid(paddedNodes, nodeDefs, px, y, z + 1);
                    int ymzm = IsSolid(paddedNodes, nodeDefs, px, y - 1, z - 1);
                    int ymzp = IsSolid(paddedNodes, nodeDefs, px, y - 1, z + 1);
                    int ypzm = IsSolid(paddedNodes, nodeDefs, px, y + 1, z - 1);
                    int ypzp = IsSolid(paddedNodes, nodeDefs, px, y + 1, z + 1);

                    // v0=(x, y, z)        v1=(x, y, z+1)
                    // v2=(x, y+1, z+1)    v3=(x, y+1, z)
                    ao0 = VertexAO(ym, zm, ymzm);
                    ao1 = VertexAO(ym, zp, ymzp);
                    ao2 = VertexAO(yp, zp, ypzp);
                    ao3 = VertexAO(yp, zm, ypzm);
                    break;
                }
                case 4: // PosZ — face plane at z+1, tangents X and Y
                {
                    int pz = z + 1;
                    int xm = IsSolid(paddedNodes, nodeDefs, x - 1, y, pz);
                    int xp = IsSolid(paddedNodes, nodeDefs, x + 1, y, pz);
                    int ym = IsSolid(paddedNodes, nodeDefs, x, y - 1, pz);
                    int yp = IsSolid(paddedNodes, nodeDefs, x, y + 1, pz);
                    int xmym = IsSolid(paddedNodes, nodeDefs, x - 1, y - 1, pz);
                    int xmyp = IsSolid(paddedNodes, nodeDefs, x - 1, y + 1, pz);
                    int xpym = IsSolid(paddedNodes, nodeDefs, x + 1, y - 1, pz);
                    int xpyp = IsSolid(paddedNodes, nodeDefs, x + 1, y + 1, pz);

                    // v0=(x, y, z+1)      v1=(x+1, y, z+1)
                    // v2=(x+1, y+1, z+1)  v3=(x, y+1, z+1)
                    ao0 = VertexAO(xm, ym, xmym);
                    ao1 = VertexAO(xp, ym, xpym);
                    ao2 = VertexAO(xp, yp, xpyp);
                    ao3 = VertexAO(xm, yp, xmyp);
                    break;
                }
                default: // NegZ (case 5) — face plane at z, AO samples one step in -Z normal direction
                {
                    int pz = z - 1;
                    int xm = IsSolid(paddedNodes, nodeDefs, x - 1, y, pz);
                    int xp = IsSolid(paddedNodes, nodeDefs, x + 1, y, pz);
                    int ym = IsSolid(paddedNodes, nodeDefs, x, y - 1, pz);
                    int yp = IsSolid(paddedNodes, nodeDefs, x, y + 1, pz);
                    int xmym = IsSolid(paddedNodes, nodeDefs, x - 1, y - 1, pz);
                    int xmyp = IsSolid(paddedNodes, nodeDefs, x - 1, y + 1, pz);
                    int xpym = IsSolid(paddedNodes, nodeDefs, x + 1, y - 1, pz);
                    int xpyp = IsSolid(paddedNodes, nodeDefs, x + 1, y + 1, pz);

                    // v0=(x+1, y, z)      v1=(x, y, z)
                    // v2=(x, y+1, z)      v3=(x+1, y+1, z)
                    ao0 = VertexAO(xp, ym, xpym);
                    ao1 = VertexAO(xm, ym, xmym);
                    ao2 = VertexAO(xm, yp, xmyp);
                    ao3 = VertexAO(xp, yp, xpyp);
                    break;
                }
            }
        }

        /// <summary>
        /// Packs four 2-bit AO values into one byte.
        /// Bits [1:0]=ao0, [3:2]=ao1, [5:4]=ao2, [7:6]=ao3.
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static byte PackAO4(byte ao0, byte ao1, byte ao2, byte ao3)
        {
            return (byte)((ao0 & 0x3)
                | ((ao1 & 0x3) << 2)
                | ((ao2 & 0x3) << 4)
                | ((ao3 & 0x3) << 6));
        }

        /// <summary>
        /// Unpacks a byte written by <see cref="PackAO4"/> into 4 AO values (each 0-3).
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void UnpackAO4(byte packed,
            out byte ao0, out byte ao1, out byte ao2, out byte ao3)
        {
            ao0 = (byte)(packed & 0x3);
            ao1 = (byte)((packed >> 2) & 0x3);
            ao2 = (byte)((packed >> 4) & 0x3);
            ao3 = (byte)((packed >> 6) & 0x3);
        }
    }
}
