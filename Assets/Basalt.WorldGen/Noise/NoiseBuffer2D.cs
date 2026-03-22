using System;
using Unity.Collections;

namespace Basalt.WorldGen
{
    /// <summary>
    /// Owned NativeArray buffers for one 2D fractal noise grid.
    /// Allocated once per noise channel with <c>Allocator.Persistent</c>;
    /// reused every mapgen call without re-allocation.
    /// </summary>
    /// <remarks>
    /// Buffer layout matches Luanti's <c>Noise</c> class:
    ///   <see cref="NoiseBuf"/>  — integer-corner hash lattice (Phase 1 output).
    ///   <see cref="ValueBuf"/>  — interpolated values per output sample (Phase 2 output).
    ///   <see cref="ResultBuf"/> — accumulated octave sum (final output).
    ///   <see cref="GmapBuf"/>   — per-element running gain for persistence maps.
    ///
    /// <see cref="PersistenceMap"/> is externally owned — the caller allocates, fills, and
    /// disposes it. This struct does NOT dispose PersistenceMap.
    /// </remarks>
    public struct NoiseBuffer2D : IDisposable
    {
        /// <summary>Output grid width in samples.</summary>
        public readonly int Sx;

        /// <summary>Output grid height in samples.</summary>
        public readonly int Sy;

        /// <summary>Integer-corner noise lattice (Phase 1 output). Sized for the coarsest octave.</summary>
        public NativeArray<float> NoiseBuf;

        /// <summary>Interpolated value grid, size Sx * Sy (Phase 2 output).</summary>
        public NativeArray<float> ValueBuf;

        /// <summary>Accumulated fractal sum, size Sx * Sy (zeroed before each map call).</summary>
        public NativeArray<float> ResultBuf;

        /// <summary>Per-element running gain for persistence maps, size Sx * Sy.</summary>
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
        /// <param name="noiseBufCapacity">
        /// Lattice buffer capacity. Safe formula: <c>(sx + 2) * (sy + 2)</c>
        /// when spread >= 1 world unit per sample.
        /// </param>
        /// <param name="allocator">Memory allocator (typically <c>Allocator.Persistent</c>).</param>
        public NoiseBuffer2D(int sx, int sy, int noiseBufCapacity, Allocator allocator)
        {
            Sx = sx;
            Sy = sy;
            NoiseBuf = new NativeArray<float>(noiseBufCapacity, allocator);
            ValueBuf = new NativeArray<float>(sx * sy, allocator);
            ResultBuf = new NativeArray<float>(sx * sy, allocator);
            GmapBuf = new NativeArray<float>(sx * sy, allocator);
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
