using System;
using Unity.Collections;

namespace Basalt.WorldGen
{
    /// <summary>
    /// Owned NativeArray buffers for one 3D fractal noise grid.
    /// Allocated once per noise channel with <c>Allocator.Persistent</c>;
    /// reused every mapgen call without re-allocation.
    /// </summary>
    /// <remarks>
    /// Same design as <see cref="NoiseBuffer2D"/> extended to three dimensions.
    /// <see cref="PersistenceMap"/> is externally owned — the caller allocates, fills, and
    /// disposes it. This struct does NOT dispose PersistenceMap.
    /// </remarks>
    public struct NoiseBuffer3D : IDisposable
    {
        /// <summary>Output grid width in samples.</summary>
        public readonly int Sx;

        /// <summary>Output grid height in samples.</summary>
        public readonly int Sy;

        /// <summary>Output grid depth in samples.</summary>
        public readonly int Sz;

        /// <summary>Integer-corner noise lattice (Phase 1 output). Sized for the coarsest octave.</summary>
        public NativeArray<float> NoiseBuf;

        /// <summary>Interpolated value grid, size Sx * Sy * Sz (Phase 2 output).</summary>
        public NativeArray<float> ValueBuf;

        /// <summary>Accumulated fractal sum, size Sx * Sy * Sz (zeroed before each map call).</summary>
        public NativeArray<float> ResultBuf;

        /// <summary>Per-element running gain for persistence maps, size Sx * Sy * Sz.</summary>
        public NativeArray<float> GmapBuf;

        /// <summary>
        /// Optional spatially-varying persistence map provided by the mapgen.
        /// Length 0 or uninitialized = not used (scalar persistence from NoiseParams.Persist).
        /// Externally owned — NOT disposed by this struct.
        /// </summary>
        public NativeArray<float> PersistenceMap;

        /// <summary>
        /// Allocates all buffers except PersistenceMap (externally owned).
        /// </summary>
        /// <param name="sx">Output grid width.</param>
        /// <param name="sy">Output grid height.</param>
        /// <param name="sz">Output grid depth.</param>
        /// <param name="noiseBufCapacity">
        /// Lattice buffer capacity. Safe formula: <c>(sx + 2) * (sy + 2) * (sz + 2)</c>
        /// when spread >= 1 world unit per sample.
        /// </param>
        /// <param name="allocator">Memory allocator (typically <c>Allocator.Persistent</c>).</param>
        public NoiseBuffer3D(int sx, int sy, int sz, int noiseBufCapacity, Allocator allocator)
        {
            Sx = sx;
            Sy = sy;
            Sz = sz;
            int bufSize = sx * sy * sz;
            NoiseBuf = new NativeArray<float>(noiseBufCapacity, allocator);
            ValueBuf = new NativeArray<float>(bufSize, allocator);
            ResultBuf = new NativeArray<float>(bufSize, allocator);
            GmapBuf = new NativeArray<float>(bufSize, allocator);
            PersistenceMap = default;
        }

        /// <summary>Gets whether the buffers have been allocated.</summary>
        public bool IsCreated => ResultBuf.IsCreated;

        /// <summary>
        /// Disposes all internally owned buffers.
        /// Does NOT dispose PersistenceMap (externally owned).
        /// </summary>
        public void Dispose()
        {
            if (NoiseBuf.IsCreated)
            {
                NoiseBuf.Dispose();
            }

            if (ValueBuf.IsCreated)
            {
                ValueBuf.Dispose();
            }

            if (ResultBuf.IsCreated)
            {
                ResultBuf.Dispose();
            }

            if (GmapBuf.IsCreated)
            {
                GmapBuf.Dispose();
            }
        }
    }
}
