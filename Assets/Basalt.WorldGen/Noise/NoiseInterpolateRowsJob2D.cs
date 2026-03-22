using Unity.Burst;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;
using Unity.Jobs;

namespace Basalt.WorldGen
{
    /// <summary>
    /// Phase 2 of the 2D grid noise pipeline: interpolates one row of the output grid
    /// per <see cref="IJobParallelFor"/> index. Each row is independent — no synchronization needed.
    /// </summary>
    /// <remarks>
    /// Schedule with index range [0, Sy) and batchCount = 1 for maximum parallelism.
    /// Each index writes to a disjoint slice of <see cref="ValueBuf"/> so no
    /// <c>NativeArray.AsParallelWriter</c> is needed.
    ///
    /// Source: <c>luanti/src/noise.cpp</c> — <c>Noise::valueMap2D()</c> interpolation sweep.
    /// </remarks>
    [BurstCompile]
    public struct NoiseInterpolateRowsJob2D : IJobParallelFor
    {
        /// <summary>Read-only noise lattice from Phase 1.</summary>
        [ReadOnly]
        public NativeArray<float> NoiseBuf;

        /// <summary>
        /// Output value buffer. Each row index writes a disjoint slice [rowJ*Sx .. (rowJ+1)*Sx-1].
        /// </summary>
        [NativeDisableParallelForRestriction]
        public NativeArray<float> ValueBuf;

        /// <summary>Lattice width (from Phase 1 metadata).</summary>
        [ReadOnly]
        public NativeArray<int> Nlx;

        /// <summary>Fractional X offset at grid origin (from Phase 1 metadata).</summary>
        [ReadOnly]
        public NativeArray<float> OrigU;

        /// <summary>Fractional Y offset at grid origin (from Phase 1 metadata).</summary>
        [ReadOnly]
        public NativeArray<float> OrigV;

        /// <summary>X step per sample in lattice space (from Phase 1 metadata).</summary>
        [ReadOnly]
        public NativeArray<float> StepX;

        /// <summary>Y step per sample in lattice space (from Phase 1 metadata).</summary>
        [ReadOnly]
        public NativeArray<float> StepY;

        /// <summary>Output grid width.</summary>
        public int Sx;

        /// <summary>Whether to apply quintic easing (0=false, 1=true). Byte for blittability.</summary>
        public byte Eased;

        public void Execute(int rowJ)
        {
            NoiseKernel.InterpolateRow2D(
                NoiseBuf, ValueBuf,
                rowJ,
                Sx, Nlx[0],
                OrigU[0], OrigV[0],
                StepX[0], StepY[0],
                Eased != 0);
        }
    }
}
