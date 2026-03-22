using System;
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
    /// Orchestrates the meshing pipeline for a single chunk: builds the padded neighborhood,
    /// schedules the FaceCullingJob, and applies the result to a Unity Mesh on the main thread.
    /// </summary>
    /// <remarks>
    /// Lifecycle per chunk re-mesh:
    /// 1. Call <see cref="ScheduleMesh"/> — builds neighborhood, schedules job.
    /// 2. Next frame: call <see cref="TryApplyMesh"/> — completes job if ready, uploads to GPU.
    ///
    /// This class is NOT a MonoBehaviour. It is owned by the chunk management system.
    /// One instance can be reused across remeshes via <see cref="ScheduleMesh"/>.
    ///
    /// Story 2.5 note: ScheduleMesh will be refactored to use Mesh.AllocateWritableMeshData
    /// and a double-pass (count + write) IJobParallelFor. The public interface stays identical.
    /// </remarks>
    public sealed class ChunkMeshBuilder : IDisposable
    {
        private NativeArray<uint> _paddedBuffer;
        private MeshOutput _output;
        private JobHandle _jobHandle;
        private bool _jobScheduled;

        /// <summary>Gets whether a meshing job is currently in flight.</summary>
        public bool IsJobScheduled => _jobScheduled;

        /// <summary>
        /// Creates a new mesh builder with persistent allocations for the padded buffer and output.
        /// </summary>
        public ChunkMeshBuilder()
        {
            _paddedBuffer = new NativeArray<uint>(
                ChunkNeighborhood.PADDED_SIZE, Allocator.Persistent);
            _output = new MeshOutput(Allocator.Persistent);
        }

        /// <summary>
        /// Builds the padded neighborhood and schedules the face culling job.
        /// </summary>
        /// <param name="centerNodes">The 4096-element node data for the center chunk.</param>
        /// <param name="neighborPosX">+X neighbor nodes, or default if not loaded.</param>
        /// <param name="neighborNegX">-X neighbor nodes, or default if not loaded.</param>
        /// <param name="neighborPosY">+Y neighbor nodes, or default if not loaded.</param>
        /// <param name="neighborNegY">-Y neighbor nodes, or default if not loaded.</param>
        /// <param name="neighborPosZ">+Z neighbor nodes, or default if not loaded.</param>
        /// <param name="neighborNegZ">-Z neighbor nodes, or default if not loaded.</param>
        /// <param name="nodeDefs">Baked node definition array from NodeRegistry.</param>
        /// <param name="dependency">Optional upstream job dependency.</param>
        /// <returns>The JobHandle for the scheduled meshing job.</returns>
        public JobHandle ScheduleMesh(
            NativeArray<uint> centerNodes,
            NativeArray<uint> neighborPosX,
            NativeArray<uint> neighborNegX,
            NativeArray<uint> neighborPosY,
            NativeArray<uint> neighborNegY,
            NativeArray<uint> neighborPosZ,
            NativeArray<uint> neighborNegZ,
            NativeArray<NodeDefinition> nodeDefs,
            JobHandle dependency = default)
        {
            // PERF: Force-complete any in-flight job before reusing the shared output buffers.
            // This is a documented exception to the "never .Complete() immediately" rule because
            // the builder reuses persistent NativeLists that cannot be written by two jobs.
            // In normal play, the caller should not schedule a new mesh while one is in flight.
            if (_jobScheduled)
            {
                _jobHandle.Complete();
                _jobScheduled = false;
            }

            _output.Clear();

            ChunkNeighborhoodBuilder.Build(
                _paddedBuffer,
                centerNodes,
                neighborPosX, neighborNegX,
                neighborPosY, neighborNegY,
                neighborPosZ, neighborNegZ);

            var job = new FaceCullingJob
            {
                PaddedNodes = _paddedBuffer,
                NodeDefs = nodeDefs,
                OutputVertices = _output.Vertices,
                OutputIndices = _output.Indices,
            };

            _jobHandle = job.Schedule(dependency);
            _jobScheduled = true;

            return _jobHandle;
        }

        /// <summary>
        /// Checks whether the meshing job is complete. If so, uploads mesh data to the GPU.
        /// </summary>
        /// <param name="targetMesh">The Unity Mesh to receive the new geometry.</param>
        /// <returns>True if the mesh was successfully updated this call.</returns>
        /// <remarks>
        /// Must be called from the main thread (Mesh API constraint).
        /// Returns false if no job is scheduled or the job is not yet complete.
        /// </remarks>
        public bool TryApplyMesh(Mesh targetMesh)
        {
            if (!_jobScheduled || !_jobHandle.IsCompleted)
            {
                return false;
            }

            // PERF: This runs on the main thread — keep under 1ms total for all chunks per frame
            _jobHandle.Complete();
            _jobScheduled = false;

            UploadMesh(targetMesh);

            return true;
        }

        private void UploadMesh(Mesh targetMesh)
        {
            targetMesh.Clear();

            if (!_output.HasData)
            {
                return;
            }

            int vertexCount = _output.Vertices.Length;
            int indexCount = _output.Indices.Length;

            targetMesh.SetVertexBufferParams(vertexCount, new[]
            {
                new VertexAttributeDescriptor(
                    VertexAttribute.Position, VertexAttributeFormat.Float32, 3),
                new VertexAttributeDescriptor(
                    VertexAttribute.Normal, VertexAttributeFormat.Float32, 3),
                new VertexAttributeDescriptor(
                    VertexAttribute.TexCoord0, VertexAttributeFormat.Float32, 2),
            });

            targetMesh.SetVertexBufferData(
                _output.Vertices.AsArray(),
                0, 0, vertexCount, 0,
                MeshUpdateFlags.DontValidateIndices | MeshUpdateFlags.DontResetBoneBounds);

            targetMesh.SetIndexBufferParams(indexCount, IndexFormat.UInt32);
            targetMesh.SetIndexBufferData(
                _output.Indices.AsArray(),
                0, 0, indexCount,
                MeshUpdateFlags.DontValidateIndices | MeshUpdateFlags.DontResetBoneBounds);

            targetMesh.SetSubMesh(0,
                new SubMeshDescriptor(0, indexCount),
                MeshUpdateFlags.DontRecalculateBounds);

            // Bounds: axis-aligned box covering a full 16-node chunk
            targetMesh.bounds = new Bounds(
                new Vector3(8f, 8f, 8f),
                new Vector3(16f, 16f, 16f));
        }

        /// <summary>Completes any in-flight job and releases all persistent native allocations.</summary>
        public void Dispose()
        {
            if (_jobScheduled)
            {
                _jobHandle.Complete();
                _jobScheduled = false;
            }

            _output?.Dispose();

            if (_paddedBuffer.IsCreated)
            {
                _paddedBuffer.Dispose();
            }
        }
    }
}