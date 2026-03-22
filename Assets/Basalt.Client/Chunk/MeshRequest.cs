using Basalt.Core;
using Unity.Collections;
using Unity.Jobs;
using Unity.Mathematics;
using UnityEngine;

namespace Basalt.Client
{
    /// <summary>
    /// Tracks the in-flight meshing pipeline state for a single chunk.
    /// Progresses through <see cref="MeshRequestPhase"/> as jobs complete.
    /// </summary>
    /// <remarks>
    /// Lifetime: created when a chunk is loaded, destroyed when the chunk is unloaded
    /// or when the meshing pipeline completes and transitions to idle.
    ///
    /// The <c>CountResult</c> NativeArray must be allocated by the caller with
    /// <c>Allocator.Persistent</c> and disposed when the request is cancelled or
    /// the pipeline completes.
    /// </remarks>
    internal struct MeshRequest
    {
        /// <summary>Chunk position in chunk-space coordinates.</summary>
        public int3 ChunkPosition;

        /// <summary>Handle into the ChunkPool for node data access.</summary>
        public ChunkHandle Handle;

        /// <summary>Slot index in the PaddedNeighborhoodPool (-1 if not rented).</summary>
        public int PaddedBufferSlot;

        /// <summary>
        /// 2-element NativeArray: [0]=vertexCount, [1]=indexCount.
        /// Allocated with Allocator.Persistent, disposed on completion or cancellation.
        /// </summary>
        public NativeArray<int> CountResult;

        /// <summary>Unity Mesh to receive the generated geometry.</summary>
        public Mesh TargetMesh;

        /// <summary>GameObject that holds the MeshFilter and MeshRenderer.</summary>
        public GameObject ChunkObject;

        /// <summary>Index into the current frame's MeshDataArray (-1 if not in a batch).</summary>
        public int BatchSlotIndex;

        /// <summary>Job handle for the neighborhood build pass.</summary>
        public JobHandle NeighborhoodHandle;

        /// <summary>Job handle for the count pass.</summary>
        public JobHandle CountHandle;

        /// <summary>Job handle for the write pass.</summary>
        public JobHandle WriteHandle;

        /// <summary>Current pipeline phase.</summary>
        public MeshRequestPhase Phase;
    }

    /// <summary>
    /// Pipeline phases for a chunk meshing request.
    /// </summary>
    internal enum MeshRequestPhase : byte
    {
        /// <summary>No meshing in progress.</summary>
        Idle = 0,

        /// <summary>NeighborhoodBuildJob is scheduled and running.</summary>
        Neighborhood = 1,

        /// <summary>GreedyCountJob is scheduled and running.</summary>
        Counting = 2,

        /// <summary>Count result is ready; waiting to be batched into a MeshDataArray.</summary>
        CountComplete = 3,

        /// <summary>GreedyWriteJob is scheduled, writing into MeshData buffers.</summary>
        Writing = 4,

        /// <summary>Write complete; ready for ApplyAndDisposeWritableMeshData.</summary>
        ReadyToApply = 5,
    }
}
