using Unity.Burst;
using Unity.Collections;
using Unity.Jobs;

namespace Basalt.WorldGen
{
    /// <summary>
    /// Phase 1 of the 3D grid noise pipeline: fills the integer-corner hash lattice
    /// for a single octave. Must complete before <see cref="NoiseInterpolatePlanesJob3D"/>.
    /// </summary>
    /// <remarks>
    /// Source: <c>luanti/src/noise.cpp</c> — <c>Noise::valueMap3D()</c> lattice fill section.
    /// </remarks>
    [BurstCompile]
    public struct NoiseFillLatticeJob3D : IJob
    {
        /// <summary>World X coordinate of the grid origin (already divided by spread).</summary>
        public float X;

        /// <summary>World Y coordinate of the grid origin (already divided by spread).</summary>
        public float Y;

        /// <summary>World Z coordinate of the grid origin (already divided by spread).</summary>
        public float Z;

        /// <summary>Current octave frequency factor.</summary>
        public float F;

        /// <summary>Per-axis spread X.</summary>
        public float SpreadX;

        /// <summary>Per-axis spread Y.</summary>
        public float SpreadY;

        /// <summary>Per-axis spread Z.</summary>
        public float SpreadZ;

        /// <summary>Combined seed for this octave (mapSeed + np.Seed + octave).</summary>
        public int Seed;

        /// <summary>Output grid width.</summary>
        public int Sx;

        /// <summary>Output grid height.</summary>
        public int Sy;

        /// <summary>Output grid depth.</summary>
        public int Sz;

        /// <summary>Destination buffer for the integer-corner hash lattice.</summary>
        [WriteOnly]
        public NativeArray<float> NoiseBuf;

        /// <summary>Single-element output: computed lattice width.</summary>
        [WriteOnly]
        public NativeArray<int> OutNlx;

        /// <summary>Single-element output: computed lattice height.</summary>
        [WriteOnly]
        public NativeArray<int> OutNly;

        /// <summary>Single-element output: computed lattice depth.</summary>
        [WriteOnly]
        public NativeArray<int> OutNlz;

        /// <summary>Single-element output: fractional X offset.</summary>
        [WriteOnly]
        public NativeArray<float> OutOrigU;

        /// <summary>Single-element output: fractional Y offset.</summary>
        [WriteOnly]
        public NativeArray<float> OutOrigV;

        /// <summary>Single-element output: fractional Z offset.</summary>
        [WriteOnly]
        public NativeArray<float> OutOrigW;

        /// <summary>Single-element output: X step per sample in lattice space.</summary>
        [WriteOnly]
        public NativeArray<float> OutStepX;

        /// <summary>Single-element output: Y step per sample in lattice space.</summary>
        [WriteOnly]
        public NativeArray<float> OutStepY;

        /// <summary>Single-element output: Z step per sample in lattice space.</summary>
        [WriteOnly]
        public NativeArray<float> OutStepZ;

        public void Execute()
        {
            float x = X * F;
            float y = Y * F;
            float z = Z * F;
            float stepX = F / SpreadX;
            float stepY = F / SpreadY;
            float stepZ = F / SpreadZ;

            // Luanti uses std::floor for valueMap3D
            int x0 = (int)Unity.Mathematics.math.floor(x);
            int y0 = (int)Unity.Mathematics.math.floor(y);
            int z0 = (int)Unity.Mathematics.math.floor(z);
            float u = x - x0;
            float v = y - y0;
            float w = z - z0;

            int nlx = (int)(u + Sx * stepX) + 2;
            int nly = (int)(v + Sy * stepY) + 2;
            int nlz = (int)(w + Sz * stepZ) + 2;

            NoiseKernel.FillLattice3D(NoiseBuf, x0, y0, z0, nlx, nly, nlz, Seed);

            OutNlx[0] = nlx;
            OutNly[0] = nly;
            OutNlz[0] = nlz;
            OutOrigU[0] = u;
            OutOrigV[0] = v;
            OutOrigW[0] = w;
            OutStepX[0] = stepX;
            OutStepY[0] = stepY;
            OutStepZ[0] = stepZ;
        }
    }
}
