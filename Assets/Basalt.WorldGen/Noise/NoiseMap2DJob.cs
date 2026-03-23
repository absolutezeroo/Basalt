using Unity.Burst;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;
using Unity.Jobs;
using Unity.Mathematics;

namespace Basalt.WorldGen
{
    /// <summary>
    /// Monolithic 2D fractal noise map generation — one job per noise channel.
    /// Exact port of Luanti's <c>Noise::noiseMap2D()</c>: loops octaves internally,
    /// computing lattice metadata, filling the hash lattice, interpolating rows,
    /// accumulating octave results, and applying offset/scale.
    /// </summary>
    /// <remarks>
    /// Replaces the previous three-phase pipeline (NoiseFillLatticeJob +
    /// NoiseInterpolateRowsJob2D + NoiseAccumulateJob) that scheduled 1 + 3×octaves
    /// jobs per channel. All math delegates to existing <see cref="NoiseKernel"/> methods
    /// so output is bit-identical.
    ///
    /// Source: <c>luanti/src/noise.cpp</c> lines 649-682 — <c>Noise::noiseMap2D()</c>.
    /// </remarks>
    [BurstCompile]
    public struct NoiseMap2DJob : IJob
    {
        /// <summary>Noise parameters for this channel.</summary>
        public NoiseParams Np;

        /// <summary>Grid origin X, already divided by spread.x.</summary>
        public float OriginX;

        /// <summary>Grid origin Z, already divided by spread.z (mapped to 2D Y axis).</summary>
        public float OriginZ;

        /// <summary>Combined map seed (mapSeed, not yet combined with np.Seed).</summary>
        public int MapSeed;

        /// <summary>Output grid width in samples.</summary>
        public int Sx;

        /// <summary>Output grid height in samples.</summary>
        public int Sy;

        /// <summary>Integer-corner hash lattice scratch buffer.</summary>
        public NativeArray<float> NoiseBuf;

        /// <summary>Per-sample interpolated value scratch buffer (Sx × Sy).</summary>
        public NativeArray<float> ValueBuf;

        /// <summary>Accumulated fractal result output (Sx × Sy).</summary>
        public NativeArray<float> ResultBuf;

        /// <summary>Per-element running gain for persistence maps (Sx × Sy).</summary>
        public NativeArray<float> GmapBuf;

        /// <summary>
        /// Optional spatially-varying persistence map. Default/empty = not used.
        /// Safety restriction disabled because this may be <c>default</c> when unused.
        /// </summary>
        [NativeDisableContainerSafetyRestriction]
        public NativeArray<float> PersistenceMap;

        public void Execute()
        {
            int bufSize = Sx * Sy;
            bool hasPersistMap = PersistenceMap.IsCreated && PersistenceMap.Length > 0;
            bool absValue = Np.IsAbsValue;
            bool eased = Np.IsEased2D;

            // Clear ResultBuf; init GmapBuf to 1.0 if persistence map active
            if (hasPersistMap)
            {
                for (int i = 0; i < bufSize; i++)
                {
                    ResultBuf[i] = 0f;
                    GmapBuf[i] = 1f;
                }
            }
            else
            {
                for (int i = 0; i < bufSize; i++)
                {
                    ResultBuf[i] = 0f;
                }
            }

            float f = 1f;
            float g = 1f;

            for (int octave = 0; octave < Np.Octaves; octave++)
            {
                int octaveSeed = MapSeed + Np.Seed + octave;

                // ---- Lattice metadata (inlined from NoiseFillLatticeJob.Execute) ----
                float x = OriginX * f;
                float y = OriginZ * f;
                float stepX = f / Np.Spread.x;
                float stepY = f / Np.Spread.z;

                int x0 = (int)math.floor(x);
                int y0 = (int)math.floor(y);
                float origU = x - x0;
                float origV = y - y0;

                int nlx = (int)(origU + Sx * stepX) + 2;
                int nly = (int)(origV + Sy * stepY) + 2;

                // ---- Phase 1: Fill lattice ----
                NoiseKernel.FillLattice2D(NoiseBuf, x0, y0, nlx, nly, octaveSeed);

                // ---- Phase 2: Interpolate all rows ----
                for (int j = 0; j < Sy; j++)
                {
                    NoiseKernel.InterpolateRow2D(
                        NoiseBuf, ValueBuf,
                        j, Sx, nlx,
                        origU, origV,
                        stepX, stepY,
                        eased);
                }

                // ---- Phase 3: Accumulate octave ----
                NoiseKernel.AccumulateOctave(
                    ValueBuf, ResultBuf, GmapBuf, PersistenceMap,
                    g, bufSize, absValue, hasPersistMap);

                f *= Np.Lacunarity;
                g *= Np.Persist;
            }

            // ---- Final: Apply offset and scale ----
            bool needsTransform = math.abs(Np.Offset) > 0.00001f
                               || math.abs(Np.Scale - 1f) > 0.00001f;

            if (needsTransform)
            {
                NoiseKernel.ApplyOffsetScale(ResultBuf, bufSize, Np.Offset, Np.Scale);
            }
        }
    }
}
