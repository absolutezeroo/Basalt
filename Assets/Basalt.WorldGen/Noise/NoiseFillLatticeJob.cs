using Unity.Burst;
using Unity.Collections;
using Unity.Jobs;

namespace Basalt.WorldGen
{
    /// <summary>
    /// Phase 1 of the 2D grid noise pipeline: fills the integer-corner hash lattice
    /// for a single octave. Must complete before <see cref="NoiseInterpolateRowsJob2D"/>.
    /// </summary>
    /// <remarks>
    /// Computes the lattice origin and dimensions from the grid position, frequency, and spread,
    /// then fills <see cref="NoiseBuf"/> with hash values and writes metadata for Phase 2.
    /// Source: <c>luanti/src/noise.cpp</c> — <c>Noise::valueMap2D()</c> lattice fill section.
    /// </remarks>
    [BurstCompile]
    public struct NoiseFillLatticeJob : IJob
    {
        /// <summary>World X coordinate of the grid origin (already divided by spread).</summary>
        public float X;

        /// <summary>World Y coordinate of the grid origin (already divided by spread).</summary>
        public float Y;

        /// <summary>Current octave frequency factor.</summary>
        public float F;

        /// <summary>X-axis spread value (world units per noise feature on X).</summary>
        public float SpreadX;

        /// <summary>Y-axis spread value (world units per noise feature on Y).</summary>
        public float SpreadY;

        /// <summary>Combined seed for this octave (mapSeed + np.Seed + octave).</summary>
        public int Seed;

        /// <summary>Output grid width.</summary>
        public int Sx;

        /// <summary>Output grid height.</summary>
        public int Sy;

        /// <summary>Destination buffer for the integer-corner hash lattice.</summary>
        [WriteOnly]
        public NativeArray<float> NoiseBuf;

        /// <summary>Single-element output: computed lattice width.</summary>
        [WriteOnly]
        public NativeArray<int> OutNlx;

        /// <summary>Single-element output: computed lattice height.</summary>
        [WriteOnly]
        public NativeArray<int> OutNly;

        /// <summary>Single-element output: fractional X offset at grid origin.</summary>
        [WriteOnly]
        public NativeArray<float> OutOrigU;

        /// <summary>Single-element output: fractional Y offset at grid origin.</summary>
        [WriteOnly]
        public NativeArray<float> OutOrigV;

        /// <summary>Single-element output: X step per sample in lattice space.</summary>
        [WriteOnly]
        public NativeArray<float> OutStepX;

        /// <summary>Single-element output: Y step per sample in lattice space.</summary>
        [WriteOnly]
        public NativeArray<float> OutStepY;

        public void Execute()
        {
            // Luanti: valueMap2D receives (x * f, y * f, f / spread.X, f / spread.Y, seed)
            float x = X * F;
            float y = Y * F;
            float stepX = F / SpreadX;
            float stepY = F / SpreadY;

            // Luanti uses std::floor for valueMap2D (not myfloor)
            int x0 = (int)Unity.Mathematics.math.floor(x);
            int y0 = (int)Unity.Mathematics.math.floor(y);
            float u = x - x0;
            float v = y - y0;

            int nlx = (int)(u + Sx * stepX) + 2;
            int nly = (int)(v + Sy * stepY) + 2;

            NoiseKernel.FillLattice2D(NoiseBuf, x0, y0, nlx, nly, Seed);

            OutNlx[0] = nlx;
            OutNly[0] = nly;
            OutOrigU[0] = u;
            OutOrigV[0] = v;
            OutStepX[0] = stepX;
            OutStepY[0] = stepY;
        }
    }
}
