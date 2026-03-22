using Unity.Burst;
using Unity.Collections;
using Unity.Jobs;

namespace Basalt.WorldGen
{
    /// <summary>
    /// Evaluates fractal 2D noise at a single point.
    /// Delegates to <see cref="NoiseKernel.Fractal2D"/>.
    /// </summary>
    [BurstCompile]
    public struct NoiseFractal2DJob : IJob
    {
        /// <summary>Noise parameters defining octave accumulation behaviour.</summary>
        public NoiseParams Params;

        /// <summary>World X coordinate.</summary>
        public float X;

        /// <summary>World Y coordinate.</summary>
        public float Y;

        /// <summary>Map seed (combined with NoiseParams.Seed internally).</summary>
        public int MapSeed;

        /// <summary>Single-element output array receiving the noise value.</summary>
        [WriteOnly]
        public NativeArray<float> Result;

        public void Execute()
        {
            Result[0] = NoiseKernel.Fractal2D(in Params, X, Y, MapSeed);
        }
    }
}
