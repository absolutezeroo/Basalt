using System;
using System.Collections.Generic;
using Basalt.Core;
using Basalt.Meshing;
using Unity.Collections;
using Unity.Jobs;
using Unity.Mathematics;
using UnityEngine;
using UnityEngine.Rendering;

namespace Basalt.Client
{
    /// <summary>
    /// Coordinates the multi-phase meshing pipeline for all in-flight chunks:
    /// NeighborhoodBuildJob → GreedyCountJob → GreedyWriteJob → ApplyAndDisposeWritableMeshData.
    /// </summary>
    /// <remarks>
    /// Called from <see cref="ChunkManager"/> each frame. Advances each
    /// <see cref="MeshRequest"/> through its <see cref="MeshRequestPhase"/> stages.
    ///
    /// Batching: all chunks whose count pass completes in the same frame share a single
    /// <c>Mesh.AllocateWritableMeshData(N)</c> call. Write jobs are scheduled against
    /// the allocated MeshData slots. On the following frame, all completed writes are
    /// applied in one <c>Mesh.ApplyAndDisposeWritableMeshData</c> call.
    ///
    /// Pipeline timing (steady state, one chunk):
    ///   Frame N:   NeighborhoodBuildJob scheduled
    ///   Frame N+1: CountJob scheduled (depends on NeighborhoodBuildJob)
    ///   Frame N+2: Count complete → AllocateWritableMeshData → WriteJob scheduled
    ///   Frame N+3: Write complete → ApplyAndDisposeWritableMeshData
    /// </remarks>
    internal sealed class ChunkMeshApplier : IDisposable
    {
        private readonly List<MeshRequest> _activeRequests;
        private readonly PaddedNeighborhoodPool _neighborhoodPool;
        private readonly int _maxBatchSize;

        // Batch state: held between AllocateWritableMeshData and ApplyAndDispose
        private Mesh.MeshDataArray _pendingDataArray;
        private bool _hasPendingBatch;
        private readonly List<int> _pendingBatchIndices;

        // Pre-allocated list to avoid per-frame Mesh[] GC allocation
        private readonly List<Mesh> _meshScratch;

        // Dummy array passed for absent neighbors to satisfy job safety checks
        private NativeArray<uint> _dummyNodeArray;

        /// <summary>Gets the number of currently in-flight meshing requests.</summary>
        public int ActiveCount => _activeRequests.Count;

        /// <summary>
        /// Creates a new mesh applier with the specified maximum concurrent meshes.
        /// </summary>
        /// <param name="maxConcurrent">Maximum chunks meshing simultaneously.</param>
        public ChunkMeshApplier(int maxConcurrent)
        {
            _activeRequests = new List<MeshRequest>(maxConcurrent);
            _neighborhoodPool = new PaddedNeighborhoodPool(maxConcurrent);
            _maxBatchSize = maxConcurrent;
            _pendingBatchIndices = new List<int>(maxConcurrent);
            _meshScratch = new List<Mesh>(maxConcurrent);
            _dummyNodeArray = new NativeArray<uint>(
                BasaltConstants.NODES_PER_BLOCK, Allocator.Persistent);
        }

        /// <summary>
        /// Enqueues a new mesh request. The request must have its ChunkPosition, Handle,
        /// TargetMesh, and ChunkObject fields set. Phase must be Idle.
        /// </summary>
        public void Enqueue(MeshRequest request)
        {
            request.Phase = MeshRequestPhase.Idle;
            request.PaddedBufferSlot = -1;
            request.BatchSlotIndex = -1;
            _activeRequests.Add(request);
        }

        /// <summary>
        /// Advances all active requests through their pipeline phases.
        /// Call from ChunkManager.Update() each frame.
        /// </summary>
        /// <param name="chunkPool">Pool for node data access.</param>
        /// <param name="activeChunks">Active chunk positions for neighbor lookups.</param>
        /// <param name="nodeDefs">Baked node definitions for Burst jobs.</param>
        public void Tick(
            ChunkPool chunkPool,
            NativeHashMap<int3, ChunkHandle> activeChunks,
            NativeArray<NodeDefinition> nodeDefs)
        {
            AdvanceIdleToNeighborhood(chunkPool, activeChunks);
            AdvanceNeighborhoodToCount(nodeDefs);
            AdvanceCountToWrite(nodeDefs);
            FlushApply();
        }

        /// <summary>
        /// Cancels and cleans up all requests for a given chunk position.
        /// Called when a chunk is unloaded while meshing is in flight.
        /// </summary>
        /// <remarks>
        /// Requests in the <see cref="MeshRequestPhase.Writing"/> or
        /// <see cref="MeshRequestPhase.ReadyToApply"/> phase are part of a shared
        /// <c>MeshDataArray</c> batch and cannot be removed mid-batch. Use
        /// <see cref="HasBatchRequest"/> to check before calling.
        /// </remarks>
        public void Cancel(int3 chunkPosition)
        {
            for (int i = _activeRequests.Count - 1; i >= 0; i--)
            {
                MeshRequest request = _activeRequests[i];

                if (!math.all(request.ChunkPosition == chunkPosition))
                {
                    continue;
                }

                CancelRequest(ref request);
                _activeRequests.RemoveAt(i);
            }
        }

        /// <summary>
        /// Checks whether a meshing request is active for the given position.
        /// </summary>
        public bool HasActiveRequest(int3 chunkPosition)
        {
            for (int i = 0; i < _activeRequests.Count; i++)
            {
                if (math.all(_activeRequests[i].ChunkPosition == chunkPosition))
                {
                    return true;
                }
            }

            return false;
        }

        /// <summary>
        /// Checks whether the given chunk position has a request that is part of an
        /// active <c>MeshDataArray</c> batch (Writing or ReadyToApply phase).
        /// Chunks with batch requests must not be unloaded until the batch completes.
        /// </summary>
        public bool HasBatchRequest(int3 chunkPosition)
        {
            for (int i = 0; i < _activeRequests.Count; i++)
            {
                MeshRequest request = _activeRequests[i];

                if (math.all(request.ChunkPosition == chunkPosition)
                    && (request.Phase == MeshRequestPhase.Writing
                        || request.Phase == MeshRequestPhase.ReadyToApply))
                {
                    return true;
                }
            }

            return false;
        }

        private void AdvanceIdleToNeighborhood(
            ChunkPool chunkPool,
            NativeHashMap<int3, ChunkHandle> activeChunks)
        {
            for (int i = 0; i < _activeRequests.Count; i++)
            {
                MeshRequest request = _activeRequests[i];

                if (request.Phase != MeshRequestPhase.Idle)
                {
                    continue;
                }

                if (!_neighborhoodPool.TryRent(out int slot))
                {
                    // Pool exhausted — will retry next frame
                    break;
                }

                request.PaddedBufferSlot = slot;
                request.CountResult = new NativeArray<int>(2, Allocator.Persistent);

                NativeArray<uint> paddedBuffer = _neighborhoodPool.GetBuffer(slot);
                NativeArray<uint> centerNodes = chunkPool.GetNodes(request.Handle);

                // Look up neighbor chunk data
                int3 pos = request.ChunkPosition;
                LookupNeighbor(chunkPool, activeChunks, pos + new int3(1, 0, 0),
                    _dummyNodeArray, out NativeArray<uint> nPosX, out byte hasPosX);
                LookupNeighbor(chunkPool, activeChunks, pos + new int3(-1, 0, 0),
                    _dummyNodeArray, out NativeArray<uint> nNegX, out byte hasNegX);
                LookupNeighbor(chunkPool, activeChunks, pos + new int3(0, 1, 0),
                    _dummyNodeArray, out NativeArray<uint> nPosY, out byte hasPosY);
                LookupNeighbor(chunkPool, activeChunks, pos + new int3(0, -1, 0),
                    _dummyNodeArray, out NativeArray<uint> nNegY, out byte hasNegY);
                LookupNeighbor(chunkPool, activeChunks, pos + new int3(0, 0, 1),
                    _dummyNodeArray, out NativeArray<uint> nPosZ, out byte hasPosZ);
                LookupNeighbor(chunkPool, activeChunks, pos + new int3(0, 0, -1),
                    _dummyNodeArray, out NativeArray<uint> nNegZ, out byte hasNegZ);

                var job = new NeighborhoodBuildJob
                {
                    CenterNodes = centerNodes,
                    NeighborPosX = nPosX,
                    NeighborNegX = nNegX,
                    NeighborPosY = nPosY,
                    NeighborNegY = nNegY,
                    NeighborPosZ = nPosZ,
                    NeighborNegZ = nNegZ,
                    HasNeighborPosX = hasPosX,
                    HasNeighborNegX = hasNegX,
                    HasNeighborPosY = hasPosY,
                    HasNeighborNegY = hasNegY,
                    HasNeighborPosZ = hasPosZ,
                    HasNeighborNegZ = hasNegZ,
                    Destination = paddedBuffer,
                };

                request.NeighborhoodHandle = job.Schedule();
                request.Phase = MeshRequestPhase.Neighborhood;
                _activeRequests[i] = request;
            }
        }

        private void AdvanceNeighborhoodToCount(NativeArray<NodeDefinition> nodeDefs)
        {
            for (int i = 0; i < _activeRequests.Count; i++)
            {
                MeshRequest request = _activeRequests[i];

                if (request.Phase != MeshRequestPhase.Neighborhood)
                {
                    continue;
                }

                if (!request.NeighborhoodHandle.IsCompleted)
                {
                    continue;
                }

                request.NeighborhoodHandle.Complete();

                var countJob = new GreedyCountJob
                {
                    PaddedNodes = _neighborhoodPool.GetBuffer(request.PaddedBufferSlot),
                    NodeDefs = nodeDefs,
                    OutCounts = request.CountResult,
                };

                request.CountHandle = countJob.Schedule();
                request.Phase = MeshRequestPhase.Counting;
                _activeRequests[i] = request;
            }
        }

        private void AdvanceCountToWrite(NativeArray<NodeDefinition> nodeDefs)
        {
            // Collect all requests whose count job completed this frame
            _pendingBatchIndices.Clear();

            for (int i = 0; i < _activeRequests.Count; i++)
            {
                MeshRequest request = _activeRequests[i];

                if (request.Phase != MeshRequestPhase.Counting)
                {
                    continue;
                }

                if (!request.CountHandle.IsCompleted)
                {
                    continue;
                }

                request.CountHandle.Complete();

                int vertexCount = request.CountResult[0];

                // Empty chunks: skip MeshDataArray, clear mesh directly
                if (vertexCount == 0)
                {
                    request.TargetMesh.Clear();
                    ReturnNeighborhoodSlot(ref request);
                    DisposeCountResult(ref request);
                    request.Phase = MeshRequestPhase.Idle;
                    _activeRequests.RemoveAt(i);
                    i--;

                    continue;
                }

                request.Phase = MeshRequestPhase.CountComplete;
                _activeRequests[i] = request;
                _pendingBatchIndices.Add(i);

                if (_pendingBatchIndices.Count >= _maxBatchSize)
                {
                    break;
                }
            }

            if (_pendingBatchIndices.Count == 0 || _hasPendingBatch)
            {
                return;
            }

            // Allocate a single MeshDataArray for the entire batch
            int batchSize = _pendingBatchIndices.Count;
            _pendingDataArray = Mesh.AllocateWritableMeshData(batchSize);
            _hasPendingBatch = true;

            for (int b = 0; b < batchSize; b++)
            {
                int reqIdx = _pendingBatchIndices[b];
                MeshRequest request = _activeRequests[reqIdx];

                int vertexCount = request.CountResult[0];
                int indexCount = request.CountResult[1];

                Mesh.MeshData meshData = _pendingDataArray[b];
                meshData.SetVertexBufferParams(vertexCount, VoxelVertexLayout.Descriptors);
                meshData.SetIndexBufferParams(indexCount, IndexFormat.UInt32);
                meshData.subMeshCount = 1;

                var writeJob = new GreedyWriteJob
                {
                    PaddedNodes = _neighborhoodPool.GetBuffer(request.PaddedBufferSlot),
                    NodeDefs = nodeDefs,
                    OutputVertices = meshData.GetVertexData<VoxelVertex>(0),
                    OutputIndices = meshData.GetIndexData<int>(),
                };

                request.WriteHandle = writeJob.Schedule();
                request.BatchSlotIndex = b;
                request.Phase = MeshRequestPhase.Writing;
                _activeRequests[reqIdx] = request;
            }
        }

        private void FlushApply()
        {
            if (!_hasPendingBatch)
            {
                return;
            }

            // Check if all write jobs in the batch are complete
            bool allComplete = true;

            for (int i = 0; i < _activeRequests.Count; i++)
            {
                MeshRequest request = _activeRequests[i];

                if (request.Phase != MeshRequestPhase.Writing)
                {
                    continue;
                }

                if (!request.WriteHandle.IsCompleted)
                {
                    allComplete = false;

                    break;
                }
            }

            if (!allComplete)
            {
                return;
            }

            // Complete all write jobs and set SubMesh descriptors
            _meshScratch.Clear();

            for (int i = 0; i < _activeRequests.Count; i++)
            {
                MeshRequest request = _activeRequests[i];

                if (request.Phase != MeshRequestPhase.Writing)
                {
                    continue;
                }

                request.WriteHandle.Complete();

                int indexCount = request.CountResult[1];
                int batchSlot = request.BatchSlotIndex;

                _pendingDataArray[batchSlot].SetSubMesh(0,
                    new SubMeshDescriptor(0, indexCount),
                    MeshUpdateFlags.DontRecalculateBounds
                    | MeshUpdateFlags.DontValidateIndices);

                _meshScratch.Add(request.TargetMesh);

                request.Phase = MeshRequestPhase.ReadyToApply;
                _activeRequests[i] = request;
            }

            // PERF: Single batched GPU upload — this is the performance target of Story 2.5
            Mesh.ApplyAndDisposeWritableMeshData(
                _pendingDataArray, _meshScratch,
                MeshUpdateFlags.DontRecalculateBounds
                | MeshUpdateFlags.DontValidateIndices
                | MeshUpdateFlags.DontResetBoneBounds);

            _hasPendingBatch = false;

            // Finalize: set bounds, release resources, remove completed requests
            for (int i = _activeRequests.Count - 1; i >= 0; i--)
            {
                MeshRequest request = _activeRequests[i];

                if (request.Phase != MeshRequestPhase.ReadyToApply)
                {
                    continue;
                }

                request.TargetMesh.bounds = new Bounds(
                    new Vector3(8f, 8f, 8f),
                    new Vector3(16f, 16f, 16f));

                ReturnNeighborhoodSlot(ref request);
                DisposeCountResult(ref request);
                _activeRequests.RemoveAt(i);
            }
        }

        private void CancelRequest(ref MeshRequest request)
        {
            // PERF: Force-complete any in-flight jobs to safely release native memory
            switch (request.Phase)
            {
                case MeshRequestPhase.Writing:
                    request.WriteHandle.Complete();
                    goto case MeshRequestPhase.CountComplete;

                case MeshRequestPhase.CountComplete:
                case MeshRequestPhase.Counting:
                    request.CountHandle.Complete();
                    goto case MeshRequestPhase.Neighborhood;

                case MeshRequestPhase.Neighborhood:
                    request.NeighborhoodHandle.Complete();
                    break;
            }

            ReturnNeighborhoodSlot(ref request);
            DisposeCountResult(ref request);
        }

        private void ReturnNeighborhoodSlot(ref MeshRequest request)
        {
            if (request.PaddedBufferSlot >= 0)
            {
                _neighborhoodPool.Return(request.PaddedBufferSlot);
                request.PaddedBufferSlot = -1;
            }
        }

        private static void DisposeCountResult(ref MeshRequest request)
        {
            if (request.CountResult.IsCreated)
            {
                request.CountResult.Dispose();
                request.CountResult = default;
            }
        }

        private static void LookupNeighbor(
            ChunkPool chunkPool,
            NativeHashMap<int3, ChunkHandle> activeChunks,
            int3 neighborPos,
            NativeArray<uint> fallback,
            out NativeArray<uint> nodes,
            out byte hasNeighbor)
        {
            if (activeChunks.TryGetValue(neighborPos, out ChunkHandle handle)
                && chunkPool.IsHandleValid(handle))
            {
                nodes = chunkPool.GetNodes(handle);
                hasNeighbor = 1;
            }
            else
            {
                nodes = fallback;
                hasNeighbor = 0;
            }
        }

        /// <summary>
        /// Completes all in-flight jobs and releases all native resources.
        /// </summary>
        public void Dispose()
        {
            // Cancel all active requests
            for (int i = 0; i < _activeRequests.Count; i++)
            {
                MeshRequest request = _activeRequests[i];
                CancelRequest(ref request);
            }

            _activeRequests.Clear();

            // Dispose pending batch if held
            if (_hasPendingBatch)
            {
                _pendingDataArray.Dispose();
                _hasPendingBatch = false;
            }

            _neighborhoodPool?.Dispose();

            if (_dummyNodeArray.IsCreated)
            {
                _dummyNodeArray.Dispose();
            }
        }
    }
}
