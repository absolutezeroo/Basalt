using Unity.Burst;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;
using Unity.Jobs;
using Unity.Mathematics;

namespace Basalt.WorldGen
{
    /// <summary>
    /// Accumulates one octave's interpolated values into the running result buffer.
    /// When processing the last octave, also applies the final offset and scale transform.
    /// </summary>
    /// <remarks>
    /// Matches Luanti's <c>Noise::updateResults()</c> and the final offset/scale pass
    /// in <c>noiseMap2D/noiseMap3D</c>.
    /// Source: <c>luanti/src/noise.cpp</c> lines 724-750.
    ///
    /// Schedule once per octave, depending on the Phase 2 job for that octave.
    /// </remarks>
    [BurstCompile]
    public struct NoiseAccumulateJob : IJob
    {
        /// <summary>Interpolated values for this octave (Phase 2 output).</summary>
        [ReadOnly]
        public NativeArray<float> ValueBuf;

        /// <summary>Running result buffer (accumulated across octaves).</summary>
        public NativeArray<float> ResultBuf;

        /// <summary>Per-element running gain (initialized to 1.0 when persistence map is active).</summary>
        public NativeArray<float> GmapBuf;

        /// <summary>
        /// Optional persistence map. Length 0 or uninitialized = not used.
        /// Safety restriction disabled because this field may be <c>default</c> when
        /// <see cref="HasPersistence"/> is 0; access is guarded at runtime.
        /// </summary>
        [ReadOnly]
        [NativeDisableContainerSafetyRestriction]
        public NativeArray<float> PersistenceMap;

        /// <summary>Scalar gain for this octave (persist^octave).</summary>
        public float G;

        /// <summary>Number of elements in the grid (Sx*Sy or Sx*Sy*Sz).</summary>
        public int BufSize;

        /// <summary>Whether NOISE_FLAG_ABSVALUE is set (0=false, 1=true).</summary>
        public byte AbsValue;

        /// <summary>Whether a persistence map is active (0=false, 1=true).</summary>
        public byte HasPersistence;

        /// <summary>Whether this is the last octave (0=false, 1=true).</summary>
        public byte IsLastOctave;

        /// <summary>NoiseParams.Offset — applied only on the last octave.</summary>
        public float Offset;

        /// <summary>NoiseParams.Scale — applied only on the last octave.</summary>
        public float Scale;

        public void Execute()
        {
            NoiseKernel.AccumulateOctave(
                ValueBuf, ResultBuf, GmapBuf, PersistenceMap,
                G, BufSize,
                AbsValue != 0, HasPersistence != 0);

            if (IsLastOctave != 0)
            {
                // Luanti: only applies transform when offset or scale are non-trivial
                bool needsTransform = math.abs(Offset) > 0.00001f
                                   || math.abs(Scale - 1.0f) > 0.00001f;

                if (needsTransform)
                {
                    NoiseKernel.ApplyOffsetScale(ResultBuf, BufSize, Offset, Scale);
                }
            }
        }
    }
}
