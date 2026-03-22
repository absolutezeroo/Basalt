using System.Runtime.CompilerServices;
using Basalt.Core;
using Unity.Burst;
using Unity.Collections;
using Unity.Jobs;
using Unity.Mathematics;

namespace Basalt.Meshing
{
    /// <summary>
    /// Burst-compiled job that generates geometry for a single chunk using binary greedy meshing.
    /// Combines face culling and quad merging in a single pass over 6 face directions x 16 slices.
    /// </summary>
    /// <remarks>
    /// Replaces FaceCullingJob (Story 2.1). Algorithm per face direction per slice:
    ///   1. Build 16-uint bitmask layer: one bit per node, set if the face is visible.
    ///   2. For each column, scan rows for the first remaining visible node via <c>math.tzcnt</c>.
    ///   3. Extract the longest contiguous run of same-content nodes as a greedy span.
    ///   4. Extend the span across adjacent columns with matching content and visibility.
    ///   5. Emit one merged quad, clear consumed bits. Repeat until all bits consumed.
    ///
    /// Merge key: content_id only (Story 2.2). Story 2.3 extends to include per-vertex AO.
    ///
    /// Face culling rule (matches Luanti <c>content_mapblock.cpp</c>):
    ///   A face is rendered when the node has geometry AND the neighbor's Solidness is less than 2.
    ///
    /// This job is an IJob because the output is a shared NativeList.
    /// Story 2.5 migrates to IJobParallelFor with MeshDataArray.
    /// </remarks>
    [BurstCompile]
    public struct GreedyMeshJob : IJob
    {
        /// <summary>Packed node data for the 18 cubed padded neighborhood (5832 elements).</summary>
        [ReadOnly]
        public NativeArray<uint> PaddedNodes;

        /// <summary>Node definitions indexed by content_id for culling and tile lookups.</summary>
        [ReadOnly]
        public NativeArray<NodeDefinition> NodeDefs;

        /// <summary>Output vertex list. Caller must allocate and clear before scheduling.</summary>
        public NativeList<VoxelVertex> OutputVertices;

        /// <summary>Output triangle index list. Caller must allocate and clear before scheduling.</summary>
        public NativeList<int> OutputIndices;

        /// <summary>Runs all 6 face direction passes with greedy merging.</summary>
        public void Execute()
        {
            for (int faceDir = 0; faceDir < 6; faceDir++)
            {
                ProcessFaceDirection(faceDir);
            }
        }

        /// <summary>Runs all 16 slice layers for the given face direction and accumulates quads.</summary>
        private void ProcessFaceDirection(int faceDir)
        {
            var buffers = new GreedySliceBuffers(Allocator.TempJob);

            for (int sliceDepth = 0; sliceDepth < BasaltConstants.MAP_BLOCKSIZE; sliceDepth++)
            {
                BuildVisibilityMasks(faceDir, sliceDepth, ref buffers);
                NativeArray<uint>.Copy(buffers.VisibleFaceMasks, buffers.RemainingMasks);
                MergeSliceIntoQuads(faceDir, sliceDepth, ref buffers);
            }

            buffers.Dispose();
        }

        /// <summary>Populates VisibleFaceMasks and ContentGrid for one depth slice.</summary>
        private void BuildVisibilityMasks(int faceDir, int sliceDepth,
            ref GreedySliceBuffers buffers)
        {
            // Neighbor delta is constant for the entire face direction
            GetNeighborDelta(faceDir, out int dx, out int dy, out int dz);

            for (int col = 0; col < BasaltConstants.MAP_BLOCKSIZE; col++)
            {
                uint rowMask = 0;

                for (int row = 0; row < BasaltConstants.MAP_BLOCKSIZE; row++)
                {
                    ToWorldCoords(faceDir, sliceDepth, row, col,
                        out int x, out int y, out int z);

                    int centerIdx = ChunkNeighborhood.PaddedIndex(x, y, z);
                    ushort contentId = (ushort)(PaddedNodes[centerIdx] >> 16);

                    if (contentId >= NodeDefs.Length)
                    {
                        continue;
                    }

                    NodeDefinition nodeDef = NodeDefs[contentId];

                    if (nodeDef.HasNoGeometry())
                    {
                        continue;
                    }

                    // Luanti: content_mapblock.cpp — face is visible when neighbor solidness < 2
                    int neighborIdx = ChunkNeighborhood.PaddedIndex(x + dx, y + dy, z + dz);
                    ushort neighborContent = (ushort)(PaddedNodes[neighborIdx] >> 16);

                    if (neighborContent < NodeDefs.Length
                        && NodeDefs[neighborContent].Solidness >= 2)
                    {
                        continue;
                    }

                    rowMask |= 1u << row;
                    buffers.ContentGrid[col * BasaltConstants.MAP_BLOCKSIZE + row] = contentId;
                }

                buffers.VisibleFaceMasks[col] = rowMask;
            }
        }

        /// <summary>Iterates remaining visible bits and emits merged greedy quads for one slice.</summary>
        private void MergeSliceIntoQuads(int faceDir, int sliceDepth,
            ref GreedySliceBuffers buffers)
        {
            for (int col = 0; col < BasaltConstants.MAP_BLOCKSIZE; col++)
            {
                uint remaining = buffers.RemainingMasks[col];

                while (remaining != 0)
                {
                    int startRow = math.tzcnt(remaining);

                    ushort mergeKey = buffers.ContentGrid[
                        col * BasaltConstants.MAP_BLOCKSIZE + startRow];

                    // Find the longest run of consecutive visible rows with the same content
                    int spanLength = FindGreedySpan(col, startRow, mergeKey, ref buffers);
                    uint spanMask = GreedyBitMaskUtils.BuildSpanMask(spanLength) << startRow;

                    // Extend across adjacent columns while content and visibility match
                    int colHeight = 1 + ExtendAcrossColumns(
                        col, spanMask, mergeKey, ref buffers);

                    // Clear consumed bits from all covered columns
                    for (int c = col; c < col + colHeight; c++)
                    {
                        buffers.RemainingMasks[c] = buffers.RemainingMasks[c] & ~spanMask;
                    }

                    EmitGreedyQuad(faceDir, sliceDepth, startRow, col,
                        spanLength, colHeight, mergeKey);

                    // Refresh remaining after clearing
                    remaining = buffers.RemainingMasks[col];
                }
            }
        }

        /// <summary>
        /// Counts how many consecutive rows from <paramref name="startRow"/> in the given column
        /// are both visible and share the same content_id.
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private int FindGreedySpan(int col, int startRow, ushort mergeKey,
            ref GreedySliceBuffers buffers)
        {
            // Maximum possible span from bitmask alone (consecutive set bits)
            uint shifted = buffers.RemainingMasks[col] >> startRow;
            int maxSpan = GreedyBitMaskUtils.ExtractSpanLength(shifted);

            // Narrow the span by checking content_id match
            int gridBase = col * BasaltConstants.MAP_BLOCKSIZE;

            for (int i = 1; i < maxSpan; i++)
            {
                if (buffers.ContentGrid[gridBase + startRow + i] != mergeKey)
                {
                    return i;
                }
            }

            return maxSpan;
        }

        /// <summary>
        /// Counts how many columns after <paramref name="startCol"/> can extend the greedy quad.
        /// Returns the number of additional columns (0 if no extension possible).
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private int ExtendAcrossColumns(int startCol, uint spanMask, ushort mergeKey,
            ref GreedySliceBuffers buffers)
        {
            int extra = 0;

            for (int nextCol = startCol + 1; nextCol < BasaltConstants.MAP_BLOCKSIZE; nextCol++)
            {
                uint nextRemaining = buffers.RemainingMasks[nextCol];

                if (!GreedyBitMaskUtils.RowContainsSpan(nextRemaining, spanMask))
                {
                    break;
                }

                // All visibility bits match — verify content_id for each row in the span
                if (!AllContentMatch(nextCol, spanMask, mergeKey, ref buffers))
                {
                    break;
                }

                extra++;
            }

            return extra;
        }

        /// <summary>
        /// Checks that every row in the contiguous span indicated by <paramref name="spanMask"/>
        /// has a content_id matching <paramref name="mergeKey"/>.
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private bool AllContentMatch(int col, uint spanMask, ushort mergeKey,
            ref GreedySliceBuffers buffers)
        {
            int gridBase = col * BasaltConstants.MAP_BLOCKSIZE;
            int startRow = math.tzcnt(spanMask);
            int count = math.countbits(spanMask);

            for (int i = 0; i < count; i++)
            {
                if (buffers.ContentGrid[gridBase + startRow + i] != mergeKey)
                {
                    return false;
                }
            }

            return true;
        }

        /// <summary>Writes 4 vertices and 6 indices for a merged quad into the output lists.</summary>
        private void EmitGreedyQuad(int faceDir, int sliceDepth,
            int startRow, int startCol, int spanLength, int colHeight, ushort contentId)
        {
            int baseVertex = OutputVertices.Length;

            NodeDefinition nodeDef = NodeDefs[contentId];
            ushort tileIndex = GetTileForFace(faceDir, nodeDef);
            float3 normal = GetNormal(faceDir);
            const byte ao = 3; // Story 2.3 replaces with per-vertex AO

            for (int v = 0; v < 4; v++)
            {
                float3 pos = GetQuadVertex(
                    faceDir, v, sliceDepth, startRow, startCol, spanLength, colHeight);

                OutputVertices.Add(new VoxelVertex(pos, normal, tileIndex, ao));
            }

            // CCW winding — Unity left-handed front-face convention
            OutputIndices.Add(baseVertex + 0);
            OutputIndices.Add(baseVertex + 1);
            OutputIndices.Add(baseVertex + 2);
            OutputIndices.Add(baseVertex + 0);
            OutputIndices.Add(baseVertex + 2);
            OutputIndices.Add(baseVertex + 3);
        }

        /// <summary>
        /// Maps (faceDir, sliceDepth, row, col) to chunk-local (x, y, z) for PaddedNodes access.
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static void ToWorldCoords(int faceDir, int sliceDepth, int row, int col,
            out int x, out int y, out int z)
        {
            switch (faceDir)
            {
                case 0:
                case 1:
                    // PosY / NegY — slice=Y, row=Z, col=X
                    x = col; y = sliceDepth; z = row;
                    break;
                case 2:
                case 3:
                    // PosX / NegX — slice=X, row=Y, col=Z
                    x = sliceDepth; y = row; z = col;
                    break;
                default:
                    // PosZ / NegZ — slice=Z, row=Y, col=X
                    x = col; y = row; z = sliceDepth;
                    break;
            }
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static void GetNeighborDelta(int faceDir, out int dx, out int dy, out int dz)
        {
            switch (faceDir)
            {
                case 0:  dx = 0; dy = 1; dz = 0; break;  // PosY
                case 1:  dx = 0; dy = -1; dz = 0; break;  // NegY
                case 2:  dx = 1; dy = 0; dz = 0; break;  // PosX
                case 3:  dx = -1; dy = 0; dz = 0; break;  // NegX
                case 4:  dx = 0; dy = 0; dz = 1; break;  // PosZ
                default: dx = 0; dy = 0; dz = -1; break;  // NegZ
            }
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static float3 GetNormal(int faceDir)
        {
            return faceDir switch
            {
                0 => new float3(0, 1, 0),
                1 => new float3(0, -1, 0),
                2 => new float3(1, 0, 0),
                3 => new float3(-1, 0, 0),
                4 => new float3(0, 0, 1),
                _ => new float3(0, 0, -1),
            };
        }

        /// <summary>Luanti: face index to tile mapping from nodedef.cpp.</summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static ushort GetTileForFace(int faceDir, NodeDefinition nodeDef)
        {
            return faceDir switch
            {
                0 => nodeDef.TileTop,
                1 => nodeDef.TileBottom,
                2 => nodeDef.TileRight,
                3 => nodeDef.TileLeft,
                4 => nodeDef.TileFront,
                _ => nodeDef.TileBack,
            };
        }

        /// <summary>
        /// Returns the world-space position of corner <paramref name="v"/> (0-3)
        /// for a merged quad. Vertex ordering preserves CCW winding from outside
        /// for all 6 face directions, matching FaceCullingJob single-node vertices.
        /// </summary>
        /// <remarks>
        /// Derived from FaceCullingJob.GetFaceVertex by substituting unit offsets with:
        ///   row dimension: 0 -> startRow, 1 -> startRow + spanLength
        ///   col dimension: 0 -> startCol, 1 -> startCol + colHeight
        ///   normal plane:  sliceDepth (neg faces) or sliceDepth + 1 (pos faces)
        /// </remarks>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static float3 GetQuadVertex(int faceDir, int v,
            int sliceDepth, int startRow, int startCol, int spanLength, int colHeight)
        {
            float s0 = sliceDepth;
            float s1 = sliceDepth + 1;
            float r0 = startRow;
            float r1 = startRow + spanLength;
            float c0 = startCol;
            float c1 = startCol + colHeight;

            switch (faceDir)
            {
                case 0: // PosY — plane Y=s1, col=X, row=Z
                    return v switch
                    {
                        0 => new float3(c0, s1, r0),
                        1 => new float3(c0, s1, r1),
                        2 => new float3(c1, s1, r1),
                        _ => new float3(c1, s1, r0),
                    };
                case 1: // NegY — plane Y=s0, col=X, row=Z
                    return v switch
                    {
                        0 => new float3(c0, s0, r1),
                        1 => new float3(c0, s0, r0),
                        2 => new float3(c1, s0, r0),
                        _ => new float3(c1, s0, r1),
                    };
                case 2: // PosX — plane X=s1, row=Y, col=Z
                    return v switch
                    {
                        0 => new float3(s1, r0, c1),
                        1 => new float3(s1, r0, c0),
                        2 => new float3(s1, r1, c0),
                        _ => new float3(s1, r1, c1),
                    };
                case 3: // NegX — plane X=s0, row=Y, col=Z
                    return v switch
                    {
                        0 => new float3(s0, r0, c0),
                        1 => new float3(s0, r0, c1),
                        2 => new float3(s0, r1, c1),
                        _ => new float3(s0, r1, c0),
                    };
                case 4: // PosZ — plane Z=s1, row=Y, col=X
                    return v switch
                    {
                        0 => new float3(c0, r0, s1),
                        1 => new float3(c1, r0, s1),
                        2 => new float3(c1, r1, s1),
                        _ => new float3(c0, r1, s1),
                    };
                default: // NegZ — plane Z=s0, row=Y, col=X
                    return v switch
                    {
                        0 => new float3(c1, r0, s0),
                        1 => new float3(c0, r0, s0),
                        2 => new float3(c0, r1, s0),
                        _ => new float3(c1, r1, s0),
                    };
            }
        }
    }
}
