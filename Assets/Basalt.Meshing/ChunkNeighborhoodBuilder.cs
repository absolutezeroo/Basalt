using Basalt.Core;
using Unity.Collections;

namespace Basalt.Meshing
{
    /// <summary>
    /// Builds a <see cref="ChunkNeighborhood"/> by copying center chunk data and
    /// one-node-thick slabs from the six face-adjacent neighbor chunks.
    /// </summary>
    /// <remarks>
    /// The destination NativeArray must be pre-allocated with
    /// <see cref="ChunkNeighborhood.PADDED_SIZE"/> elements.
    /// Neighbor chunks that are not loaded fill with CONTENT_IGNORE.
    /// Design mirrors Luanti's VoxelArea in <c>luanti/src/voxel.h</c>.
    /// </remarks>
    public static class ChunkNeighborhoodBuilder
    {
        /// <summary>
        /// Fills a 5832-element destination array from the center chunk and its six neighbors.
        /// </summary>
        /// <param name="destination">Pre-allocated NativeArray of length PADDED_SIZE.</param>
        /// <param name="center">The 4096-element node data for the center chunk.</param>
        /// <param name="neighborPosX">+X neighbor nodes (4096), or default if not loaded.</param>
        /// <param name="neighborNegX">-X neighbor nodes (4096), or default if not loaded.</param>
        /// <param name="neighborPosY">+Y neighbor nodes (4096), or default if not loaded.</param>
        /// <param name="neighborNegY">-Y neighbor nodes (4096), or default if not loaded.</param>
        /// <param name="neighborPosZ">+Z neighbor nodes (4096), or default if not loaded.</param>
        /// <param name="neighborNegZ">-Z neighbor nodes (4096), or default if not loaded.</param>
        public static void Build(
            NativeArray<uint> destination,
            NativeArray<uint> center,
            NativeArray<uint> neighborPosX,
            NativeArray<uint> neighborNegX,
            NativeArray<uint> neighborPosY,
            NativeArray<uint> neighborNegY,
            NativeArray<uint> neighborPosZ,
            NativeArray<uint> neighborNegZ)
        {
            uint ignorePacked = MapNode.Pack(BasaltConstants.CONTENT_IGNORE, 0, 0);

            // Fill entire buffer with CONTENT_IGNORE first
            for (int i = 0; i < ChunkNeighborhood.PADDED_SIZE; i++)
            {
                destination[i] = ignorePacked;
            }

            // Copy center chunk (16x16x16)
            for (int z = 0; z < BasaltConstants.MAP_BLOCKSIZE; z++)
            {
                for (int y = 0; y < BasaltConstants.MAP_BLOCKSIZE; y++)
                {
                    int srcStart = CoordinateUtils.NodeIndex(0, y, z);
                    int dstStart = ChunkNeighborhood.PaddedIndex(0, y, z);

                    NativeArray<uint>.Copy(
                        center, srcStart,
                        destination, dstStart,
                        BasaltConstants.MAP_BLOCKSIZE);
                }
            }

            // Copy +X face neighbor (x=0 slab of neighbor becomes x=16 in padded)
            CopyFaceSlabX(destination, neighborPosX,
                srcX: 0, dstLocalX: BasaltConstants.MAP_BLOCKSIZE);

            // Copy -X face neighbor (x=15 slab of neighbor becomes x=-1 in padded)
            CopyFaceSlabX(destination, neighborNegX,
                srcX: BasaltConstants.MAP_BLOCKSIZE - 1, dstLocalX: -1);

            // Copy +Y face neighbor (y=0 slab of neighbor becomes y=16 in padded)
            CopyFaceSlabY(destination, neighborPosY,
                srcY: 0, dstLocalY: BasaltConstants.MAP_BLOCKSIZE);

            // Copy -Y face neighbor (y=15 slab of neighbor becomes y=-1 in padded)
            CopyFaceSlabY(destination, neighborNegY,
                srcY: BasaltConstants.MAP_BLOCKSIZE - 1, dstLocalY: -1);

            // Copy +Z face neighbor (z=0 slab of neighbor becomes z=16 in padded)
            CopyFaceSlabZ(destination, neighborPosZ,
                srcZ: 0, dstLocalZ: BasaltConstants.MAP_BLOCKSIZE);

            // Copy -Z face neighbor (z=15 slab of neighbor becomes z=-1 in padded)
            CopyFaceSlabZ(destination, neighborNegZ,
                srcZ: BasaltConstants.MAP_BLOCKSIZE - 1, dstLocalZ: -1);
        }

        private static void CopyFaceSlabX(
            NativeArray<uint> destination,
            NativeArray<uint> neighborNodes,
            int srcX, int dstLocalX)
        {
            if (!neighborNodes.IsCreated)
            {
                return;
            }

            for (int z = 0; z < BasaltConstants.MAP_BLOCKSIZE; z++)
            {
                for (int y = 0; y < BasaltConstants.MAP_BLOCKSIZE; y++)
                {
                    int srcIdx = CoordinateUtils.NodeIndex(srcX, y, z);
                    int dstIdx = ChunkNeighborhood.PaddedIndex(dstLocalX, y, z);
                    destination[dstIdx] = neighborNodes[srcIdx];
                }
            }
        }

        private static void CopyFaceSlabY(
            NativeArray<uint> destination,
            NativeArray<uint> neighborNodes,
            int srcY, int dstLocalY)
        {
            if (!neighborNodes.IsCreated)
            {
                return;
            }

            for (int z = 0; z < BasaltConstants.MAP_BLOCKSIZE; z++)
            {
                for (int x = 0; x < BasaltConstants.MAP_BLOCKSIZE; x++)
                {
                    int srcIdx = CoordinateUtils.NodeIndex(x, srcY, z);
                    int dstIdx = ChunkNeighborhood.PaddedIndex(x, dstLocalY, z);
                    destination[dstIdx] = neighborNodes[srcIdx];
                }
            }
        }

        private static void CopyFaceSlabZ(
            NativeArray<uint> destination,
            NativeArray<uint> neighborNodes,
            int srcZ, int dstLocalZ)
        {
            if (!neighborNodes.IsCreated)
            {
                return;
            }

            for (int y = 0; y < BasaltConstants.MAP_BLOCKSIZE; y++)
            {
                for (int x = 0; x < BasaltConstants.MAP_BLOCKSIZE; x++)
                {
                    int srcIdx = CoordinateUtils.NodeIndex(x, y, srcZ);
                    int dstIdx = ChunkNeighborhood.PaddedIndex(x, y, dstLocalZ);
                    destination[dstIdx] = neighborNodes[srcIdx];
                }
            }
        }
    }
}