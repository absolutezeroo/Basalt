using Unity.Burst;
using Unity.Collections;
using Unity.Jobs;

namespace Basalt.WorldGen
{
    /// <summary>
    /// Evaluates fractal 3D noise at a single point.
    /// Delegates to <see cref="NoiseKernel.Fractal3D"/>.
    /// </summary>
    [BurstCompile]
    public struct NoiseFractal3DJob : IJob
    {
        /// <summary>Noise parameters defining octave accumulation behaviour.</summary>
        public NoiseParams Params;

        /// <summary>World X coordinate.</summary>
        public float X;

        /// <summary>World Y coordinate.</summary>
        public float Y;

        /// <summary>World Z coordinate.</summary>
        public float Z;

        /// <summary>Map seed (combined with NoiseParams.Seed internally).</summary>
        public int MapSeed;

        /// <summary>Single-element output array receiving the noise value.</summary>
        [WriteOnly]
        public NativeArray<float> Result;

        public void Execute()
        {
            Result[0] = NoiseKernel.Fractal3D(in Params, X, Y, Z, MapSeed);
        }
    }
}
