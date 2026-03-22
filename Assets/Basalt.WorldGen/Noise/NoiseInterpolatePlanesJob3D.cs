using Unity.Burst;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;
using Unity.Jobs;

namespace Basalt.WorldGen
{
    /// <summary>
    /// Phase 2 of the 3D grid noise pipeline: interpolates one XY-plane of the output grid
    /// per <see cref="IJobParallelFor"/> index. Each plane is independent — no synchronization needed.
    /// </summary>
    /// <remarks>
    /// Schedule with index range [0, Sz) and batchCount = 1 for maximum parallelism.
    /// Each index writes to a disjoint slice of <see cref="ValueBuf"/>.
    ///
    /// Source: <c>luanti/src/noise.cpp</c> — <c>Noise::valueMap3D()</c> interpolation sweep.
    /// </remarks>
    [BurstCompile]
    public struct NoiseInterpolatePlanesJob3D : IJobParallelFor
    {
        /// <summary>Read-only noise lattice from Phase 1.</summary>
        [ReadOnly]
        public NativeArray<float> NoiseBuf;

        /// <summary>
        /// Output value buffer. Each plane index writes a disjoint slice
        /// [planeK*Sx*Sy .. (planeK+1)*Sx*Sy-1].
        /// </summary>
        [NativeDisableParallelForRestriction]
        public NativeArray<float> ValueBuf;

        /// <summary>Lattice width (from Phase 1 metadata).</summary>
        [ReadOnly]
        public NativeArray<int> Nlx;

        /// <summary>Lattice height (from Phase 1 metadata).</summary>
        [ReadOnly]
        public NativeArray<int> Nly;

        /// <summary>Fractional X offset at grid origin.</summary>
        [ReadOnly]
        public NativeArray<float> OrigU;

        /// <summary>Fractional Y offset at grid origin.</summary>
        [ReadOnly]
        public NativeArray<float> OrigV;

        /// <summary>Fractional Z offset at grid origin.</summary>
        [ReadOnly]
        public NativeArray<float> OrigW;

        /// <summary>X step per sample in lattice space.</summary>
        [ReadOnly]
        public NativeArray<float> StepX;

        /// <summary>Y step per sample in lattice space.</summary>
        [ReadOnly]
        public NativeArray<float> StepY;

        /// <summary>Z step per sample in lattice space.</summary>
        [ReadOnly]
        public NativeArray<float> StepZ;

        /// <summary>Output grid width.</summary>
        public int Sx;

        /// <summary>Output grid height.</summary>
        public int Sy;

        /// <summary>Whether to apply quintic easing (0=false, 1=true). Byte for blittability.</summary>
        public byte Eased;

        public void Execute(int planeK)
        {
            NoiseKernel.InterpolatePlane3D(
                NoiseBuf, ValueBuf,
                planeK,
                Sx, Sy, Nlx[0], Nly[0],
                OrigU[0], OrigV[0], OrigW[0],
                StepX[0], StepY[0], StepZ[0],
                Eased != 0);
        }
    }
}
