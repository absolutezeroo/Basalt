using Basalt.Core;
using Unity.Burst;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;
using Unity.Jobs;

namespace Basalt.Meshing
{
    /// <summary>
    /// Burst-compiled job that builds the 18-cubed padded neighborhood buffer
    /// from a center chunk and its six face-adjacent neighbors.
    /// </summary>
    /// <remarks>
    /// Replaces the synchronous <see cref="ChunkNeighborhoodBuilder.Build"/> method
    /// for off-main-thread execution.
    ///
    /// Unloaded neighbors are indicated by <c>HasNeighbor* = 0</c>. Their border slabs
    /// are filled with CONTENT_IGNORE, matching Luanti's VoxelArea behavior.
    ///
    /// The <c>Has*</c> flags replace <c>NativeArray.IsCreated</c> checks because
    /// Burst jobs cannot call <c>IsCreated</c> on arrays passed from managed code.
    /// </remarks>
    [BurstCompile]
    public struct NeighborhoodBuildJob : IJob
    {
        // Safety restriction disabled on all ChunkPool-backed sub-arrays: the pool
        // stores every slot in a single contiguous NativeArray, so GetSubArray() views
        // alias the same parent.  Unity's safety system cannot distinguish non-overlapping
        // regions and would block concurrent reads here vs. writes in ChunkPool.TryRent().
        // The pool guarantees slots never overlap, so this is safe.

        /// <summary>Node data for the center chunk (4096 elements).</summary>
        [ReadOnly]
        [NativeDisableContainerSafetyRestriction]
        public NativeArray<uint> CenterNodes;

        /// <summary>+X face-adjacent neighbor node data (4096 elements).</summary>
        [ReadOnly]
        [NativeDisableContainerSafetyRestriction]
        public NativeArray<uint> NeighborPosX;

        /// <summary>-X face-adjacent neighbor node data (4096 elements).</summary>
        [ReadOnly]
        [NativeDisableContainerSafetyRestriction]
        public NativeArray<uint> NeighborNegX;

        /// <summary>+Y face-adjacent neighbor node data (4096 elements).</summary>
        [ReadOnly]
        [NativeDisableContainerSafetyRestriction]
        public NativeArray<uint> NeighborPosY;

        /// <summary>-Y face-adjacent neighbor node data (4096 elements).</summary>
        [ReadOnly]
        [NativeDisableContainerSafetyRestriction]
        public NativeArray<uint> NeighborNegY;

        /// <summary>+Z face-adjacent neighbor node data (4096 elements).</summary>
        [ReadOnly]
        [NativeDisableContainerSafetyRestriction]
        public NativeArray<uint> NeighborPosZ;

        /// <summary>-Z face-adjacent neighbor node data (4096 elements).</summary>
        [ReadOnly]
        [NativeDisableContainerSafetyRestriction]
        public NativeArray<uint> NeighborNegZ;

        /// <summary>1 if +X neighbor is loaded, 0 otherwise.</summary>
        public byte HasNeighborPosX;

        /// <summary>1 if -X neighbor is loaded, 0 otherwise.</summary>
        public byte HasNeighborNegX;

        /// <summary>1 if +Y neighbor is loaded, 0 otherwise.</summary>
        public byte HasNeighborPosY;

        /// <summary>1 if -Y neighbor is loaded, 0 otherwise.</summary>
        public byte HasNeighborNegY;

        /// <summary>1 if +Z neighbor is loaded, 0 otherwise.</summary>
        public byte HasNeighborPosZ;

        /// <summary>1 if -Z neighbor is loaded, 0 otherwise.</summary>
        public byte HasNeighborNegZ;

        /// <summary>Pre-allocated destination buffer (5832 elements).</summary>
        [WriteOnly]
        [NativeDisableContainerSafetyRestriction]
        public NativeArray<uint> Destination;

        /// <summary>Fills the padded destination buffer from center and neighbor chunks.</summary>
        public void Execute()
        {
            uint ignorePacked = MapNode.Pack(BasaltConstants.CONTENT_IGNORE, 0, 0);

            // Fill entire buffer with CONTENT_IGNORE
            for (int i = 0; i < ChunkNeighborhood.PADDED_SIZE; i++)
            {
                Destination[i] = ignorePacked;
            }

            // Copy center chunk (16x16x16)
            for (int z = 0; z < BasaltConstants.MAP_BLOCKSIZE; z++)
            {
                for (int y = 0; y < BasaltConstants.MAP_BLOCKSIZE; y++)
                {
                    int srcStart = CoordinateUtils.NodeIndex(0, y, z);
                    int dstStart = ChunkNeighborhood.PaddedIndex(0, y, z);

                    NativeArray<uint>.Copy(
                        CenterNodes, srcStart,
                        Destination, dstStart,
                        BasaltConstants.MAP_BLOCKSIZE);
                }
            }

            // Copy face-adjacent neighbor slabs
            if (HasNeighborPosX == 1)
            {
                CopyFaceSlabX(NeighborPosX, srcX: 0,
                    dstLocalX: BasaltConstants.MAP_BLOCKSIZE);
            }

            if (HasNeighborNegX == 1)
            {
                CopyFaceSlabX(NeighborNegX,
                    srcX: BasaltConstants.MAP_BLOCKSIZE - 1, dstLocalX: -1);
            }

            if (HasNeighborPosY == 1)
            {
                CopyFaceSlabY(NeighborPosY, srcY: 0,
                    dstLocalY: BasaltConstants.MAP_BLOCKSIZE);
            }

            if (HasNeighborNegY == 1)
            {
                CopyFaceSlabY(NeighborNegY,
                    srcY: BasaltConstants.MAP_BLOCKSIZE - 1, dstLocalY: -1);
            }

            if (HasNeighborPosZ == 1)
            {
                CopyFaceSlabZ(NeighborPosZ, srcZ: 0,
                    dstLocalZ: BasaltConstants.MAP_BLOCKSIZE);
            }

            if (HasNeighborNegZ == 1)
            {
                CopyFaceSlabZ(NeighborNegZ,
                    srcZ: BasaltConstants.MAP_BLOCKSIZE - 1, dstLocalZ: -1);
            }
        }

        private void CopyFaceSlabX(NativeArray<uint> neighbor, int srcX, int dstLocalX)
        {
            for (int z = 0; z < BasaltConstants.MAP_BLOCKSIZE; z++)
            {
                for (int y = 0; y < BasaltConstants.MAP_BLOCKSIZE; y++)
                {
                    int srcIdx = CoordinateUtils.NodeIndex(srcX, y, z);
                    int dstIdx = ChunkNeighborhood.PaddedIndex(dstLocalX, y, z);
                    Destination[dstIdx] = neighbor[srcIdx];
                }
            }
        }

        private void CopyFaceSlabY(NativeArray<uint> neighbor, int srcY, int dstLocalY)
        {
            for (int z = 0; z < BasaltConstants.MAP_BLOCKSIZE; z++)
            {
                for (int x = 0; x < BasaltConstants.MAP_BLOCKSIZE; x++)
                {
                    int srcIdx = CoordinateUtils.NodeIndex(x, srcY, z);
                    int dstIdx = ChunkNeighborhood.PaddedIndex(x, dstLocalY, z);
                    Destination[dstIdx] = neighbor[srcIdx];
                }
            }
        }

        private void CopyFaceSlabZ(NativeArray<uint> neighbor, int srcZ, int dstLocalZ)
        {
            for (int y = 0; y < BasaltConstants.MAP_BLOCKSIZE; y++)
            {
                for (int x = 0; x < BasaltConstants.MAP_BLOCKSIZE; x++)
                {
                    int srcIdx = CoordinateUtils.NodeIndex(x, y, srcZ);
                    int dstIdx = ChunkNeighborhood.PaddedIndex(x, y, dstLocalZ);
                    Destination[dstIdx] = neighbor[srcIdx];
                }
            }
        }
    }
}
