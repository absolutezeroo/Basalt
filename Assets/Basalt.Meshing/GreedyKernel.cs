using System.Runtime.CompilerServices;
using Basalt.Core;
using Unity.Burst;
using Unity.Collections;
using Unity.Mathematics;

namespace Basalt.Meshing
{
    /// <summary>
    /// Shared Burst-compatible greedy meshing algorithm used by both
    /// <see cref="GreedyCountJob"/> and <see cref="GreedyWriteJob"/>.
    /// </summary>
    /// <remarks>
    /// Extracted from <see cref="GreedyMeshJob"/> (Story 2.2/2.3) to support the
    /// double-pass pipeline (Story 2.5): a count pass determines exact vertex/index
    /// counts, then a write pass fills pre-sized <c>Mesh.MeshData</c> buffers.
    ///
    /// The algorithm is identical in both passes. The only difference is in
    /// <see cref="EmitOrCountQuad"/>: count mode increments cursors, write mode
    /// writes vertices and indices at cursor positions.
    ///
    /// All methods are static, allocation-free (except <see cref="GreedySliceBuffers"/>),
    /// and safe for Burst compilation.
    /// </remarks>
    [BurstCompile]
    public static class GreedyKernel
    {
        /// <summary>Count-only mode: increments cursors without writing geometry.</summary>
        public const byte MODE_COUNT = 0;

        /// <summary>Write mode: emits vertices and indices into pre-sized NativeArrays.</summary>
        public const byte MODE_WRITE = 1;

        /// <summary>
        /// Runs the full greedy meshing algorithm in count mode.
        /// Populates <paramref name="outCounts"/> with [0]=vertexCount, [1]=indexCount.
        /// </summary>
        /// <param name="paddedNodes">18-cubed padded neighborhood buffer (5832 elements).</param>
        /// <param name="nodeDefs">Node definitions indexed by content_id.</param>
        /// <param name="outCounts">2-element array to receive vertex and index counts.</param>
        public static void RunCount(
            NativeArray<uint> paddedNodes,
            NativeArray<NodeDefinition> nodeDefs,
            NativeArray<int> outCounts)
        {
            int vertexCursor = 0;
            int indexCursor = 0;

            ProcessAllFaces(
                paddedNodes, nodeDefs, MODE_COUNT,
                default, default,
                ref vertexCursor, ref indexCursor);

            outCounts[0] = vertexCursor;
            outCounts[1] = indexCursor;
        }

        /// <summary>
        /// Runs the full greedy meshing algorithm in write mode.
        /// Writes vertices and indices into the pre-sized output arrays.
        /// </summary>
        /// <param name="paddedNodes">18-cubed padded neighborhood buffer (5832 elements).</param>
        /// <param name="nodeDefs">Node definitions indexed by content_id.</param>
        /// <param name="outputVertices">Pre-sized vertex array (from MeshData).</param>
        /// <param name="outputIndices">Pre-sized index array (from MeshData).</param>
        public static void RunWrite(
            NativeArray<uint> paddedNodes,
            NativeArray<NodeDefinition> nodeDefs,
            NativeArray<VoxelVertex> outputVertices,
            NativeArray<int> outputIndices)
        {
            int vertexCursor = 0;
            int indexCursor = 0;

            ProcessAllFaces(
                paddedNodes, nodeDefs, MODE_WRITE,
                outputVertices, outputIndices,
                ref vertexCursor, ref indexCursor);
        }

        private static void ProcessAllFaces(
            NativeArray<uint> paddedNodes,
            NativeArray<NodeDefinition> nodeDefs,
            byte mode,
            NativeArray<VoxelVertex> outputVertices,
            NativeArray<int> outputIndices,
            ref int vertexCursor,
            ref int indexCursor)
        {
            for (int faceDir = 0; faceDir < 6; faceDir++)
            {
                ProcessFaceDirection(
                    paddedNodes, nodeDefs, faceDir, mode,
                    outputVertices, outputIndices,
                    ref vertexCursor, ref indexCursor);
            }
        }

        private static void ProcessFaceDirection(
            NativeArray<uint> paddedNodes,
            NativeArray<NodeDefinition> nodeDefs,
            int faceDir,
            byte mode,
            NativeArray<VoxelVertex> outputVertices,
            NativeArray<int> outputIndices,
            ref int vertexCursor,
            ref int indexCursor)
        {
            var buffers = new GreedySliceBuffers(Allocator.Temp);

            for (int sliceDepth = 0; sliceDepth < BasaltConstants.MAP_BLOCKSIZE; sliceDepth++)
            {
                BuildVisibilityMasks(paddedNodes, nodeDefs, faceDir, sliceDepth, ref buffers);
                NativeArray<uint>.Copy(buffers.VisibleFaceMasks, buffers.RemainingMasks);
                MergeSliceIntoQuads(
                    paddedNodes, nodeDefs, faceDir, sliceDepth, ref buffers,
                    mode, outputVertices, outputIndices,
                    ref vertexCursor, ref indexCursor);
            }

            buffers.Dispose();
        }

        private static void BuildVisibilityMasks(
            NativeArray<uint> paddedNodes,
            NativeArray<NodeDefinition> nodeDefs,
            int faceDir, int sliceDepth,
            ref GreedySliceBuffers buffers)
        {
            GetNeighborDelta(faceDir, out int dx, out int dy, out int dz);

            for (int col = 0; col < BasaltConstants.MAP_BLOCKSIZE; col++)
            {
                uint rowMask = 0;

                for (int row = 0; row < BasaltConstants.MAP_BLOCKSIZE; row++)
                {
                    ToWorldCoords(faceDir, sliceDepth, row, col,
                        out int x, out int y, out int z);

                    int centerIdx = ChunkNeighborhood.PaddedIndex(x, y, z);
                    ushort contentId = (ushort)(paddedNodes[centerIdx] >> 16);

                    if (contentId >= nodeDefs.Length)
                    {
                        continue;
                    }

                    NodeDefinition nodeDef = nodeDefs[contentId];

                    if (nodeDef.HasNoGeometry())
                    {
                        continue;
                    }

                    // Luanti: content_mapblock.cpp — face is visible when neighbor solidness < 2
                    int neighborIdx = ChunkNeighborhood.PaddedIndex(x + dx, y + dy, z + dz);
                    ushort neighborContent = (ushort)(paddedNodes[neighborIdx] >> 16);

                    if (neighborContent < nodeDefs.Length
                        && nodeDefs[neighborContent].Solidness >= 2)
                    {
                        continue;
                    }

                    rowMask |= 1u << row;
                    int gridIdx = col * BasaltConstants.MAP_BLOCKSIZE + row;
                    buffers.ContentGrid[gridIdx] = contentId;

                    AmbientOcclusionUtils.ComputeFaceAO(
                        paddedNodes, nodeDefs, x, y, z, faceDir,
                        out byte ao0, out byte ao1, out byte ao2, out byte ao3);
                    buffers.AoGrid[gridIdx] =
                        AmbientOcclusionUtils.PackAO4(ao0, ao1, ao2, ao3);
                }

                buffers.VisibleFaceMasks[col] = rowMask;
            }
        }

        private static void MergeSliceIntoQuads(
            NativeArray<uint> paddedNodes,
            NativeArray<NodeDefinition> nodeDefs,
            int faceDir, int sliceDepth,
            ref GreedySliceBuffers buffers,
            byte mode,
            NativeArray<VoxelVertex> outputVertices,
            NativeArray<int> outputIndices,
            ref int vertexCursor,
            ref int indexCursor)
        {
            for (int col = 0; col < BasaltConstants.MAP_BLOCKSIZE; col++)
            {
                uint remaining = buffers.RemainingMasks[col];

                while (remaining != 0)
                {
                    int startRow = math.tzcnt(remaining);

                    int mergeIdx = col * BasaltConstants.MAP_BLOCKSIZE + startRow;
                    var mergeKey = new GreedyMergeKey(
                        buffers.ContentGrid[mergeIdx],
                        buffers.AoGrid[mergeIdx]);

                    int spanLength = FindGreedySpan(col, startRow, mergeKey, ref buffers);
                    uint spanMask = GreedyBitMaskUtils.BuildSpanMask(spanLength) << startRow;

                    int colHeight = 1 + ExtendAcrossColumns(
                        col, spanMask, mergeKey, ref buffers);

                    for (int c = col; c < col + colHeight; c++)
                    {
                        buffers.RemainingMasks[c] = buffers.RemainingMasks[c] & ~spanMask;
                    }

                    EmitOrCountQuad(
                        paddedNodes, nodeDefs, faceDir, sliceDepth, startRow, col,
                        spanLength, colHeight, mergeKey,
                        mode, outputVertices, outputIndices,
                        ref vertexCursor, ref indexCursor);

                    remaining = buffers.RemainingMasks[col];
                }
            }
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static int FindGreedySpan(int col, int startRow, GreedyMergeKey mergeKey,
            ref GreedySliceBuffers buffers)
        {
            uint shifted = buffers.RemainingMasks[col] >> startRow;
            int maxSpan = GreedyBitMaskUtils.ExtractSpanLength(shifted);
            int gridBase = col * BasaltConstants.MAP_BLOCKSIZE;

            for (int i = 1; i < maxSpan; i++)
            {
                int cellIdx = gridBase + startRow + i;
                var candidateKey = new GreedyMergeKey(
                    buffers.ContentGrid[cellIdx],
                    buffers.AoGrid[cellIdx]);

                if (!candidateKey.Equals(mergeKey))
                {
                    return i;
                }
            }

            return maxSpan;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static int ExtendAcrossColumns(int startCol, uint spanMask,
            GreedyMergeKey mergeKey, ref GreedySliceBuffers buffers)
        {
            int extra = 0;

            for (int nextCol = startCol + 1; nextCol < BasaltConstants.MAP_BLOCKSIZE; nextCol++)
            {
                uint nextRemaining = buffers.RemainingMasks[nextCol];

                if (!GreedyBitMaskUtils.RowContainsSpan(nextRemaining, spanMask))
                {
                    break;
                }

                if (!AllContentMatch(nextCol, spanMask, mergeKey, ref buffers))
                {
                    break;
                }

                extra++;
            }

            return extra;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static bool AllContentMatch(int col, uint spanMask, GreedyMergeKey mergeKey,
            ref GreedySliceBuffers buffers)
        {
            int gridBase = col * BasaltConstants.MAP_BLOCKSIZE;
            int startRow = math.tzcnt(spanMask);
            int count = math.countbits(spanMask);

            for (int i = 0; i < count; i++)
            {
                int cellIdx = gridBase + startRow + i;
                var candidateKey = new GreedyMergeKey(
                    buffers.ContentGrid[cellIdx],
                    buffers.AoGrid[cellIdx]);

                if (!candidateKey.Equals(mergeKey))
                {
                    return false;
                }
            }

            return true;
        }

        /// <summary>
        /// In count mode, increments cursors by 4 vertices and 6 indices.
        /// In write mode, writes the full quad geometry at the cursor positions.
        /// </summary>
        private static void EmitOrCountQuad(
            NativeArray<uint> paddedNodes,
            NativeArray<NodeDefinition> nodeDefs,
            int faceDir, int sliceDepth,
            int startRow, int startCol, int spanLength, int colHeight,
            GreedyMergeKey mergeKey,
            byte mode,
            NativeArray<VoxelVertex> outputVertices,
            NativeArray<int> outputIndices,
            ref int vertexCursor,
            ref int indexCursor)
        {
            if (mode == MODE_COUNT)
            {
                vertexCursor += 4;
                indexCursor += 6;

                return;
            }

            int baseVertex = vertexCursor;

            NodeDefinition nodeDef = nodeDefs[mergeKey.ContentId];
            ushort tileIndex = GetTileForFace(faceDir, nodeDef);
            float3 normal = GetNormal(faceDir);

            AmbientOcclusionUtils.UnpackAO4(mergeKey.PackedAO,
                out byte ao0, out byte ao1, out byte ao2, out byte ao3);

            // Extract light from source node's param1 (day = upper nibble, night = lower nibble)
            ToWorldCoords(faceDir, sliceDepth, startRow, startCol,
                out int sx, out int sy, out int sz);
            int srcIdx = ChunkNeighborhood.PaddedIndex(sx, sy, sz);
            byte param1 = (byte)((paddedNodes[srcIdx] >> 8) & 0xFF);
            byte dayLight = (byte)((param1 >> 4) & 0xF);
            byte nightLight = (byte)(param1 & 0xF);

            for (int v = 0; v < 4; v++)
            {
                float3 pos = GetQuadVertex(
                    faceDir, v, sliceDepth, startRow, startCol, spanLength, colHeight);
                float2 uv = GetQuadUV(faceDir, v, spanLength, colHeight);

                byte ao = v switch { 0 => ao0, 1 => ao1, 2 => ao2, _ => ao3 };
                outputVertices[vertexCursor++] =
                    new VoxelVertex(pos, normal, uv, tileIndex, ao, dayLight, nightLight);
            }

            // Anisotropy fix: flip quad diagonal when AO is asymmetric
            bool flip = (ao0 + ao2) != (ao1 + ao3);

            if (!flip)
            {
                outputIndices[indexCursor++] = baseVertex + 0;
                outputIndices[indexCursor++] = baseVertex + 1;
                outputIndices[indexCursor++] = baseVertex + 2;
                outputIndices[indexCursor++] = baseVertex + 0;
                outputIndices[indexCursor++] = baseVertex + 2;
                outputIndices[indexCursor++] = baseVertex + 3;
            }
            else
            {
                outputIndices[indexCursor++] = baseVertex + 1;
                outputIndices[indexCursor++] = baseVertex + 2;
                outputIndices[indexCursor++] = baseVertex + 3;
                outputIndices[indexCursor++] = baseVertex + 1;
                outputIndices[indexCursor++] = baseVertex + 3;
                outputIndices[indexCursor++] = baseVertex + 0;
            }
        }

        // --- Geometry helpers (unchanged from GreedyMeshJob) ---

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal static void ToWorldCoords(int faceDir, int sliceDepth, int row, int col,
            out int x, out int y, out int z)
        {
            switch (faceDir)
            {
                case 0:
                case 1:
                    x = col; y = sliceDepth; z = row;
                    break;
                case 2:
                case 3:
                    x = sliceDepth; y = row; z = col;
                    break;
                default:
                    x = col; y = row; z = sliceDepth;
                    break;
            }
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal static void GetNeighborDelta(int faceDir, out int dx, out int dy, out int dz)
        {
            switch (faceDir)
            {
                case 0:  dx = 0; dy = 1; dz = 0; break;
                case 1:  dx = 0; dy = -1; dz = 0; break;
                case 2:  dx = 1; dy = 0; dz = 0; break;
                case 3:  dx = -1; dy = 0; dz = 0; break;
                case 4:  dx = 0; dy = 0; dz = 1; break;
                default: dx = 0; dy = 0; dz = -1; break;
            }
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal static float3 GetNormal(int faceDir)
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

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal static ushort GetTileForFace(int faceDir, NodeDefinition nodeDef)
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

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal static float2 GetQuadUV(int faceDir, int v, int spanLength, int colHeight)
        {
            float edgeV = faceDir <= 1 ? (float)spanLength : (float)colHeight;
            float edgeU = faceDir <= 1 ? (float)colHeight : (float)spanLength;

            return v switch
            {
                0 => new float2(0f, 0f),
                1 => new float2(0f, edgeV),
                2 => new float2(edgeU, edgeV),
                _ => new float2(edgeU, 0f),
            };
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal static float3 GetQuadVertex(int faceDir, int v,
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
                case 0:
                    return v switch
                    {
                        0 => new float3(c0, s1, r0),
                        1 => new float3(c0, s1, r1),
                        2 => new float3(c1, s1, r1),
                        _ => new float3(c1, s1, r0),
                    };
                case 1:
                    return v switch
                    {
                        0 => new float3(c0, s0, r1),
                        1 => new float3(c0, s0, r0),
                        2 => new float3(c1, s0, r0),
                        _ => new float3(c1, s0, r1),
                    };
                case 2:
                    return v switch
                    {
                        0 => new float3(s1, r0, c1),
                        1 => new float3(s1, r0, c0),
                        2 => new float3(s1, r1, c0),
                        _ => new float3(s1, r1, c1),
                    };
                case 3:
                    return v switch
                    {
                        0 => new float3(s0, r0, c0),
                        1 => new float3(s0, r0, c1),
                        2 => new float3(s0, r1, c1),
                        _ => new float3(s0, r1, c0),
                    };
                case 4:
                    return v switch
                    {
                        0 => new float3(c0, r0, s1),
                        1 => new float3(c1, r0, s1),
                        2 => new float3(c1, r1, s1),
                        _ => new float3(c0, r1, s1),
                    };
                default:
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
