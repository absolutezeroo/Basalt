using System;
using Unity.Collections;

namespace Basalt.WorldGen
{
    /// <summary>
    /// Single-element NativeArray holders for 2D lattice phase-1 outputs.
    /// Allocated once with <c>Allocator.Persistent</c>, written each octave pass,
    /// read by phase-2 interpolation jobs.
    /// </summary>
    public struct NoiseLatticeMetadata2D : IDisposable
    {
        /// <summary>Computed lattice width.</summary>
        public NativeArray<int> Nlx;

        /// <summary>Computed lattice height.</summary>
        public NativeArray<int> Nly;

        /// <summary>Fractional X offset at grid origin.</summary>
        public NativeArray<float> OrigU;

        /// <summary>Fractional Y offset at grid origin.</summary>
        public NativeArray<float> OrigV;

        /// <summary>X step per sample in lattice space.</summary>
        public NativeArray<float> StepX;

        /// <summary>Y step per sample in lattice space.</summary>
        public NativeArray<float> StepY;

        /// <summary>
        /// Allocates all six single-element arrays.
        /// </summary>
        public NoiseLatticeMetadata2D(Allocator allocator)
        {
            Nlx = new NativeArray<int>(1, allocator);
            Nly = new NativeArray<int>(1, allocator);
            OrigU = new NativeArray<float>(1, allocator);
            OrigV = new NativeArray<float>(1, allocator);
            StepX = new NativeArray<float>(1, allocator);
            StepY = new NativeArray<float>(1, allocator);
        }

        /// <summary>Disposes all owned arrays.</summary>
        public void Dispose()
        {
            if (Nlx.IsCreated)
            {
                Nlx.Dispose();
            }

            if (Nly.IsCreated)
            {
                Nly.Dispose();
            }

            if (OrigU.IsCreated)
            {
                OrigU.Dispose();
            }

            if (OrigV.IsCreated)
            {
                OrigV.Dispose();
            }

            if (StepX.IsCreated)
            {
                StepX.Dispose();
            }

            if (StepY.IsCreated)
            {
                StepY.Dispose();
            }
        }
    }
}
