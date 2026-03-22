using System;
using Basalt.Core;
using Unity.Collections;

namespace Basalt.Meshing
{
    /// <summary>
    /// Per-face scratch buffers used during greedy meshing of slice layers.
    /// </summary>
    /// <remarks>
    /// Lifecycle: allocated once per face direction with <c>Allocator.Temp</c>,
    /// reused across all 16 slice layers for that face, and disposed before the next face.
    ///
    /// Four buffers:
    ///   <see cref="VisibleFaceMasks"/> — one uint per column, bit N set if row N has a visible face.
    ///   <see cref="RemainingMasks"/> — mutable copy with bits cleared as quads consume them.
    ///   <see cref="ContentGrid"/> — content_id of each visible node in the slice (16x16).
    ///   <see cref="AoGrid"/> — packed per-vertex AO byte for each visible node face (16x16).
    /// </remarks>
    public struct GreedySliceBuffers : IDisposable
    {
        /// <summary>One uint per column; bit N set if the node at row N has a visible face.</summary>
        public NativeArray<uint> VisibleFaceMasks;

        /// <summary>Copy of VisibleFaceMasks with bits cleared as greedy quads consume them.</summary>
        public NativeArray<uint> RemainingMasks;

        /// <summary>
        /// Content_id for each visible node in the slice. Indexed as [col * 16 + row].
        /// Only valid at positions where the corresponding bit in VisibleFaceMasks is set.
        /// </summary>
        public NativeArray<ushort> ContentGrid;

        /// <summary>
        /// Packed AO byte for each visible node face in the slice. Indexed as [col * 16 + row].
        /// Bits [2k+1 : 2k] hold ao_k for vertex k (k=0..3) matching GetQuadVertex vertex order.
        /// Only valid at positions where the corresponding bit in VisibleFaceMasks is set.
        /// </summary>
        public NativeArray<byte> AoGrid;

        /// <summary>Allocates all scratch arrays with the given allocator.</summary>
        public GreedySliceBuffers(Allocator allocator)
        {
            VisibleFaceMasks = new NativeArray<uint>(
                BasaltConstants.MAP_BLOCKSIZE, allocator);
            RemainingMasks = new NativeArray<uint>(
                BasaltConstants.MAP_BLOCKSIZE, allocator);
            ContentGrid = new NativeArray<ushort>(
                BasaltConstants.MAP_BLOCKSIZE * BasaltConstants.MAP_BLOCKSIZE, allocator);
            AoGrid = new NativeArray<byte>(
                BasaltConstants.MAP_BLOCKSIZE * BasaltConstants.MAP_BLOCKSIZE, allocator);
        }

        /// <summary>Disposes all scratch arrays and releases native memory.</summary>
        public void Dispose()
        {
            if (VisibleFaceMasks.IsCreated)
            {
                VisibleFaceMasks.Dispose();
            }

            if (RemainingMasks.IsCreated)
            {
                RemainingMasks.Dispose();
            }

            if (ContentGrid.IsCreated)
            {
                ContentGrid.Dispose();
            }

            if (AoGrid.IsCreated)
            {
                AoGrid.Dispose();
            }
        }
    }
}
