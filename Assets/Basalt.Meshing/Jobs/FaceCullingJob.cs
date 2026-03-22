using System.Runtime.CompilerServices;
using Basalt.Core;
using Unity.Burst;
using Unity.Collections;
using Unity.Jobs;
using Unity.Mathematics;

namespace Basalt.Meshing
{
    /// <summary>
    /// Burst-compiled job that generates geometry for all visible faces in a single chunk.
    /// Performs face culling by checking whether each neighbor's solidness occludes the face.
    /// </summary>
    /// <remarks>
    /// Input: 5832-element padded neighborhood (18 cubed) + NodeDefinition table.
    /// Output: VoxelVertex and int index lists via caller-provided NativeLists.
    ///
    /// Face culling rule (matches Luanti <c>content_mapblock.cpp</c>):
    /// A face between node A and neighbor B is rendered when:
    ///   A has geometry AND neighbor B's Solidness is less than 2.
    ///
    /// Only DrawType.Normal nodes emit geometry in Story 2.1.
    /// DrawType.Airlike nodes are fully skipped.
    ///
    /// This job is an IJob (not IJobParallelFor) because the output is a shared
    /// NativeList. Story 2.5 will migrate to IJobParallelFor with pre-counted offsets.
    /// </remarks>
    [BurstCompile]
    public struct FaceCullingJob : IJob
    {
        /// <summary>Packed node data for the 18 cubed padded neighborhood (5832 elements).</summary>
        [ReadOnly]
        public NativeArray<uint> PaddedNodes;

        /// <summary>Node definitions indexed by content_id for culling lookups.</summary>
        [ReadOnly]
        public NativeArray<NodeDefinition> NodeDefs;

        /// <summary>Output vertex list. Caller must allocate before scheduling.</summary>
        public NativeList<VoxelVertex> OutputVertices;

        /// <summary>Output triangle index list. Caller must allocate before scheduling.</summary>
        public NativeList<int> OutputIndices;

        /// <summary>Iterates every node in the center chunk and emits geometry for visible faces.</summary>
        public void Execute()
        {
            for (int z = 0; z < BasaltConstants.MAP_BLOCKSIZE; z++)
            {
                for (int y = 0; y < BasaltConstants.MAP_BLOCKSIZE; y++)
                {
                    for (int x = 0; x < BasaltConstants.MAP_BLOCKSIZE; x++)
                    {
                        ProcessNode(x, y, z);
                    }
                }
            }
        }

        private void ProcessNode(int x, int y, int z)
        {
            int paddedIdx = ChunkNeighborhood.PaddedIndex(x, y, z);
            ushort contentId = (ushort)(PaddedNodes[paddedIdx] >> 16);

            if (contentId >= NodeDefs.Length)
            {
                return;
            }

            NodeDefinition nodeDef = NodeDefs[contentId];

            if (nodeDef.HasNoGeometry())
            {
                return;
            }

            // Check each of 6 face directions
            // Luanti: content_mapblock.cpp — drawSolidNode() face loop
            CheckFace(x, y, z, 0, 1, 0, 0, nodeDef);   // PosY (top)
            CheckFace(x, y, z, 0, -1, 0, 1, nodeDef);   // NegY (bottom)
            CheckFace(x, y, z, 1, 0, 0, 2, nodeDef);    // PosX (right)
            CheckFace(x, y, z, -1, 0, 0, 3, nodeDef);   // NegX (left)
            CheckFace(x, y, z, 0, 0, 1, 4, nodeDef);    // PosZ (front)
            CheckFace(x, y, z, 0, 0, -1, 5, nodeDef);   // NegZ (back)
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private void CheckFace(int x, int y, int z, int dx, int dy, int dz, int faceIndex,
            NodeDefinition nodeDef)
        {
            int nx = x + dx;
            int ny = y + dy;
            int nz = z + dz;

            int neighborPaddedIdx = ChunkNeighborhood.PaddedIndex(nx, ny, nz);
            ushort neighborContentId = (ushort)(PaddedNodes[neighborPaddedIdx] >> 16);

            // If neighbor has solidness >= 2, the face is fully occluded
            // Luanti: content_mapblock.cpp — if f2.solidness == 2 → continue (skip face)
            if (neighborContentId < NodeDefs.Length)
            {
                if (NodeDefs[neighborContentId].Solidness >= 2)
                {
                    return;
                }
            }

            EmitFace(x, y, z, faceIndex, nodeDef);
        }

        private void EmitFace(int x, int y, int z, int faceIndex, NodeDefinition nodeDef)
        {
            int baseVertex = OutputVertices.Length;
            int vertexBase = faceIndex * 4;

            ushort tileIndex = GetTileForFace(faceIndex, nodeDef);
            float3 normal = GetNormal(faceIndex);
            byte ao = 3;

            for (int v = 0; v < 4; v++)
            {
                float3 vertexOffset = GetFaceVertex(vertexBase + v);

                OutputVertices.Add(new VoxelVertex(
                    new float3(x + vertexOffset.x, y + vertexOffset.y, z + vertexOffset.z),
                    normal,
                    tileIndex,
                    ao));
            }

            // CCW winding from outside — Unity left-handed front-face convention
            OutputIndices.Add(baseVertex + 0);
            OutputIndices.Add(baseVertex + 1);
            OutputIndices.Add(baseVertex + 2);
            OutputIndices.Add(baseVertex + 0);
            OutputIndices.Add(baseVertex + 2);
            OutputIndices.Add(baseVertex + 3);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static ushort GetTileForFace(int faceIndex, NodeDefinition nodeDef)
        {
            // Luanti: face → tile mapping from nodedef.cpp
            return faceIndex switch
            {
                0 => nodeDef.TileTop,
                1 => nodeDef.TileBottom,
                2 => nodeDef.TileRight,
                3 => nodeDef.TileLeft,
                4 => nodeDef.TileFront,
                _ => nodeDef.TileBack,
            };
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static float3 GetNormal(int faceIndex)
        {
            return faceIndex switch
            {
                0 => new float3(0, 1, 0),
                1 => new float3(0, -1, 0),
                2 => new float3(1, 0, 0),
                3 => new float3(-1, 0, 0),
                4 => new float3(0, 0, 1),
                _ => new float3(0, 0, -1),
            };
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static float3 GetFaceVertex(int index)
        {
            // Inlined vertex positions for Burst (no managed static array access)
            // PosY (top, y=1 plane) — CCW from outside (Unity left-handed convention)
            // NegY (bottom, y=0 plane) — CCW from outside (Unity left-handed convention)
            // PosX (right, x=1 plane) — CCW from outside (Unity left-handed convention)
            // NegX (left, x=0 plane) — CCW from outside (Unity left-handed convention)
            // PosZ (front, z=1 plane) — CCW from outside (Unity left-handed convention)
            // NegZ (back, z=0 plane) — CCW from outside (Unity left-handed convention)
            return index switch
            {
                // PosY
                0 => new float3(0, 1, 0),
                1 => new float3(0, 1, 1),
                2 => new float3(1, 1, 1),
                3 => new float3(1, 1, 0),
                // NegY
                4 => new float3(0, 0, 1),
                5 => new float3(0, 0, 0),
                6 => new float3(1, 0, 0),
                7 => new float3(1, 0, 1),
                // PosX
                8 => new float3(1, 0, 1),
                9 => new float3(1, 0, 0),
                10 => new float3(1, 1, 0),
                11 => new float3(1, 1, 1),
                // NegX
                12 => new float3(0, 0, 0),
                13 => new float3(0, 0, 1),
                14 => new float3(0, 1, 1),
                15 => new float3(0, 1, 0),
                // PosZ
                16 => new float3(0, 0, 1),
                17 => new float3(1, 0, 1),
                18 => new float3(1, 1, 1),
                19 => new float3(0, 1, 1),
                // NegZ
                20 => new float3(1, 0, 0),
                21 => new float3(0, 0, 0),
                22 => new float3(0, 1, 0),
                _ => new float3(1, 1, 0),
            };
        }
    }
}