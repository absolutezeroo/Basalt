using System.Runtime.CompilerServices;
using Unity.Burst;
using Unity.Collections;
using Unity.Mathematics;

namespace Basalt.WorldGen
{
    /// <summary>
    /// All pure noise math, Burst-compiled and allocation-free.
    /// Contains the hash primitives, interpolation, single-point fractal evaluation,
    /// and grid-phase helpers used by the noise job pipeline.
    /// </summary>
    /// <remarks>
    /// Porting notes:
    ///   - All hash arithmetic uses <c>unchecked</c> to replicate C++ unsigned overflow.
    ///   - <see cref="MyFloor"/> replicates Luanti's <c>myfloor(x)</c> macro, which differs
    ///     from <c>math.floor()</c> for exact negative integers:
    ///     <c>MyFloor(-2.0f) = -3</c>, whereas <c>(int)math.floor(-2.0f) = -2</c>.
    ///   - <see cref="NoiseConstants.NOISE_MAGIC_SEED"/> is unsigned; the signed seed is cast
    ///     to uint before multiplication so C# does not sign-extend into the upper bits.
    ///   Source: <c>luanti/src/noise.cpp</c>.
    /// </remarks>
    [BurstCompile]
    public static class NoiseKernel
    {
        /// <summary>
        /// Core 2D integer hash returning a float in [-1, 1].
        /// Exact port of Luanti's <c>noise2d()</c>.
        /// </summary>
        /// <remarks>
        /// Source: <c>luanti/src/noise.cpp</c> lines 174-181.
        /// </remarks>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static float Hash2D(int x, int y, int seed)
        {
            unchecked
            {
                uint n = (NoiseConstants.NOISE_MAGIC_X * (uint)x
                        + NoiseConstants.NOISE_MAGIC_Y * (uint)y
                        + NoiseConstants.NOISE_MAGIC_SEED * (uint)seed) & 0x7FFFFFFFU;
                n = (n >> 13) ^ n;
                n = (n * (n * n * 60493U + 19990303U) + 1376312589U) & 0x7FFFFFFFU;

                return 1.0f - (float)(int)n / 0x40000000;
            }
        }

        /// <summary>
        /// Core 3D integer hash returning a float in [-1, 1].
        /// Exact port of Luanti's <c>noise3d()</c>.
        /// </summary>
        /// <remarks>
        /// Source: <c>luanti/src/noise.cpp</c> lines 184-191.
        /// </remarks>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static float Hash3D(int x, int y, int z, int seed)
        {
            unchecked
            {
                uint n = (NoiseConstants.NOISE_MAGIC_X * (uint)x
                        + NoiseConstants.NOISE_MAGIC_Y * (uint)y
                        + NoiseConstants.NOISE_MAGIC_Z * (uint)z
                        + NoiseConstants.NOISE_MAGIC_SEED * (uint)seed) & 0x7FFFFFFFU;
                n = (n >> 13) ^ n;
                n = (n * (n * n * 60493U + 19990303U) + 1376312589U) & 0x7FFFFFFFU;

                return 1.0f - (float)(int)n / 0x40000000;
            }
        }

        /// <summary>
        /// Floor toward negative infinity — replicates Luanti's <c>myfloor(x)</c> macro.
        /// </summary>
        /// <remarks>
        /// Differs from <c>(int)math.floor(x)</c> for exact negative integers:
        /// <c>MyFloor(-2.0f) = -3</c>, whereas <c>(int)math.floor(-2.0f) = -2</c>.
        /// Source: <c>luanti/src/noise.cpp</c> line 41.
        /// </remarks>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static int MyFloor(float x)
        {
            return x < 0f ? (int)x - 1 : (int)x;
        }

        /// <summary>
        /// Quintic s-curve: <c>6t^5 - 15t^4 + 10t^3</c>.
        /// First and second derivatives are zero at t=0 and t=1.
        /// </summary>
        /// <remarks>
        /// Source: <c>luanti/src/noise.h</c> — <c>inline float easeCurve(float t)</c>.
        /// </remarks>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static float EaseCurve(float t)
        {
            return t * t * t * (t * (6.0f * t - 15.0f) + 10.0f);
        }

        /// <summary>Linear interpolation: <c>v0 + (v1 - v0) * t</c>.</summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static float Lerp(float v0, float v1, float t)
        {
            return v0 + (v1 - v0) * t;
        }

        /// <summary>
        /// Bilinear interpolation with optional quintic easing.
        /// Corner naming: v[x][y] where 0=low, 1=high.
        /// </summary>
        /// <remarks>
        /// Source: <c>luanti/src/noise.cpp</c> — <c>biLinearInterpolation()</c>.
        /// </remarks>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static float BiLerp(float v00, float v10, float v01, float v11,
            float x, float y, bool eased)
        {
            if (eased)
            {
                x = EaseCurve(x);
                y = EaseCurve(y);
            }

            float u = Lerp(v00, v10, x);
            float v = Lerp(v01, v11, x);

            return Lerp(u, v, y);
        }

        /// <summary>
        /// Trilinear interpolation with optional quintic easing.
        /// Corner naming: v[x][y][z] where 0=low, 1=high.
        /// </summary>
        /// <remarks>
        /// Note: easing is applied to all three axes here, then bilinear is called without easing
        /// (easing is already applied). Matches Luanti's <c>triLinearInterpolation()</c>.
        /// Source: <c>luanti/src/noise.cpp</c> lines 223-238.
        /// </remarks>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static float TriLerp(
            float v000, float v100, float v010, float v110,
            float v001, float v101, float v011, float v111,
            float x, float y, float z, bool eased)
        {
            if (eased)
            {
                x = EaseCurve(x);
                y = EaseCurve(y);
                z = EaseCurve(z);
            }

            // Luanti: biLinearInterpolation with eased=false for both Z faces
            float u = BiLerp(v000, v100, v010, v110, x, y, false);
            float v = BiLerp(v001, v101, v011, v111, x, y, false);

            return Lerp(u, v, z);
        }

        /// <summary>
        /// Single-point 2D value noise (bilinear interpolation of 4 hash samples).
        /// </summary>
        /// <remarks>
        /// Source: <c>luanti/src/noise.cpp</c> — <c>noise2d_value()</c>.
        /// </remarks>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static float Value2D(float x, float y, int seed, bool eased)
        {
            int x0 = MyFloor(x);
            int y0 = MyFloor(y);
            float xl = x - x0;
            float yl = y - y0;

            float v00 = Hash2D(x0, y0, seed);
            float v10 = Hash2D(x0 + 1, y0, seed);
            float v01 = Hash2D(x0, y0 + 1, seed);
            float v11 = Hash2D(x0 + 1, y0 + 1, seed);

            return BiLerp(v00, v10, v01, v11, xl, yl, eased);
        }

        /// <summary>
        /// Single-point 3D value noise (trilinear interpolation of 8 hash samples).
        /// </summary>
        /// <remarks>
        /// Source: <c>luanti/src/noise.cpp</c> — <c>noise3d_value()</c>.
        /// </remarks>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static float Value3D(float x, float y, float z, int seed, bool eased)
        {
            int x0 = MyFloor(x);
            int y0 = MyFloor(y);
            int z0 = MyFloor(z);
            float xl = x - x0;
            float yl = y - y0;
            float zl = z - z0;

            float v000 = Hash3D(x0, y0, z0, seed);
            float v100 = Hash3D(x0 + 1, y0, z0, seed);
            float v010 = Hash3D(x0, y0 + 1, z0, seed);
            float v110 = Hash3D(x0 + 1, y0 + 1, z0, seed);
            float v001 = Hash3D(x0, y0, z0 + 1, seed);
            float v101 = Hash3D(x0 + 1, y0, z0 + 1, seed);
            float v011 = Hash3D(x0, y0 + 1, z0 + 1, seed);
            float v111 = Hash3D(x0 + 1, y0 + 1, z0 + 1, seed);

            return TriLerp(v000, v100, v010, v110, v001, v101, v011, v111, xl, yl, zl, eased);
        }

        /// <summary>
        /// Single-point 2D fractal noise with octave accumulation.
        /// </summary>
        /// <remarks>
        /// Source: <c>luanti/src/noise.cpp</c> — <c>NoiseFractal2D()</c> lines 314-337.
        /// </remarks>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static float Fractal2D(in NoiseParams np, float x, float y, int mapSeed)
        {
            float a = 0f;
            float f = 1.0f;
            float g = 1.0f;
            int seed = mapSeed + np.Seed;
            bool eased = np.IsEased2D;

            x /= np.Spread.x;
            y /= np.Spread.y;

            for (int i = 0; i < np.Octaves; i++)
            {
                float noiseval = Value2D(x * f, y * f, seed + i, eased);

                if (np.IsAbsValue)
                {
                    noiseval = math.abs(noiseval);
                }

                a += g * noiseval;
                f *= np.Lacunarity;
                g *= np.Persist;
            }

            return np.Offset + a * np.Scale;
        }

        /// <summary>
        /// Single-point 3D fractal noise with octave accumulation.
        /// </summary>
        /// <remarks>
        /// Source: <c>luanti/src/noise.cpp</c> — <c>NoiseFractal3D()</c> lines 340-364.
        /// Note: 3D uses only NOISE_FLAG_EASED (not NOISE_FLAG_DEFAULTS) for easing check.
        /// </remarks>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static float Fractal3D(in NoiseParams np, float x, float y, float z, int mapSeed)
        {
            float a = 0f;
            float f = 1.0f;
            float g = 1.0f;
            int seed = mapSeed + np.Seed;
            bool eased = np.IsEased3D;

            x /= np.Spread.x;
            y /= np.Spread.y;
            z /= np.Spread.z;

            for (int i = 0; i < np.Octaves; i++)
            {
                float noiseval = Value3D(x * f, y * f, z * f, seed + i, eased);

                if (np.IsAbsValue)
                {
                    noiseval = math.abs(noiseval);
                }

                a += g * noiseval;
                f *= np.Lacunarity;
                g *= np.Persist;
            }

            return np.Offset + a * np.Scale;
        }

        /// <summary>
        /// Fills the integer-corner noise lattice for a 2D grid (Phase 1).
        /// </summary>
        /// <remarks>
        /// Matches the lattice-fill loop in Luanti's <c>Noise::valueMap2D()</c>.
        /// Source: <c>luanti/src/noise.cpp</c> lines 517-519.
        /// </remarks>
        public static void FillLattice2D(
            NativeArray<float> noiseBuf,
            int x0, int y0,
            int nlx, int nly,
            int seed)
        {
            int index = 0;

            for (int j = 0; j < nly; j++)
            {
                for (int i = 0; i < nlx; i++)
                {
                    noiseBuf[index++] = Hash2D(x0 + i, y0 + j, seed);
                }
            }
        }

        /// <summary>
        /// Fills the integer-corner noise lattice for a 3D grid (Phase 1).
        /// </summary>
        /// <remarks>
        /// Source: <c>luanti/src/noise.cpp</c> lines 586-589.
        /// </remarks>
        public static void FillLattice3D(
            NativeArray<float> noiseBuf,
            int x0, int y0, int z0,
            int nlx, int nly, int nlz,
            int seed)
        {
            int index = 0;

            for (int k = 0; k < nlz; k++)
            {
                for (int j = 0; j < nly; j++)
                {
                    for (int i = 0; i < nlx; i++)
                    {
                        noiseBuf[index++] = Hash3D(x0 + i, y0 + j, z0 + k, seed);
                    }
                }
            }
        }

        /// <summary>
        /// Interpolates one row of a 2D noise grid from the pre-filled lattice (Phase 2).
        /// Designed to be called per-row from <see cref="NoiseInterpolateRowsJob2D"/>.
        /// </summary>
        /// <remarks>
        /// Matches the inner sweep of Luanti's <c>Noise::valueMap2D()</c>.
        /// Source: <c>luanti/src/noise.cpp</c> lines 522-553.
        /// </remarks>
        public static void InterpolateRow2D(
            NativeArray<float> noiseBuf,
            NativeArray<float> valueBuf,
            int rowJ,
            int sx, int nlx,
            float origU, float origV,
            float stepX, float stepY,
            bool eased)
        {
            // Reproduce Luanti's sequential accumulation to avoid floating-point divergence
            // at cell boundaries. A multiply-based shortcut (origV + rowJ * stepY) can select
            // the wrong lattice row due to FP rounding differences vs sequential addition.
            float v = origV;
            int noisey = 0;

            for (int step = 0; step < rowJ; step++)
            {
                v += stepY;

                if (v >= 1.0f)
                {
                    v -= 1.0f;
                    noisey++;
                }
            }

            float u = origU;
            int noisex = 0;
            int outBase = rowJ * sx;

            float v00 = noiseBuf[noisey * nlx];
            float v10 = noiseBuf[noisey * nlx + 1];
            float v01 = noiseBuf[(noisey + 1) * nlx];
            float v11 = noiseBuf[(noisey + 1) * nlx + 1];

            for (int i = 0; i < sx; i++)
            {
                valueBuf[outBase + i] = BiLerp(v00, v10, v01, v11, u, v, eased);

                u += stepX;

                if (u >= 1.0f)
                {
                    u -= 1.0f;
                    noisex++;
                    v00 = v10;
                    v01 = v11;
                    v10 = noiseBuf[noisey * nlx + noisex + 1];
                    v11 = noiseBuf[(noisey + 1) * nlx + noisex + 1];
                }
            }
        }

        /// <summary>
        /// Interpolates one XY-plane of a 3D noise grid from the pre-filled lattice (Phase 2).
        /// Designed to be called per-plane from <see cref="NoiseInterpolatePlanesJob3D"/>.
        /// </summary>
        /// <remarks>
        /// Matches the inner sweep of Luanti's <c>Noise::valueMap3D()</c>.
        /// Source: <c>luanti/src/noise.cpp</c> lines 595-644.
        /// </remarks>
        public static void InterpolatePlane3D(
            NativeArray<float> noiseBuf,
            NativeArray<float> valueBuf,
            int planeK,
            int sx, int sy, int nlx, int nly,
            float origU, float origV, float origW,
            float stepX, float stepY, float stepZ,
            bool eased)
        {
            // Reproduce Luanti's sequential accumulation for Z to avoid FP divergence
            float w = origW;
            int noisez = 0;

            for (int step = 0; step < planeK; step++)
            {
                w += stepZ;

                if (w >= 1.0f)
                {
                    w -= 1.0f;
                    noisez++;
                }
            }

            int planeBase = planeK * sx * sy;

            float v = origV;
            int noisey = 0;

            for (int j = 0; j < sy; j++)
            {
                float u = origU;
                int noisex = 0;
                int rowBase = planeBase + j * sx;

                int baseZ0 = (noisez * nly + noisey) * nlx;
                int baseZ0y1 = (noisez * nly + noisey + 1) * nlx;
                int baseZ1 = ((noisez + 1) * nly + noisey) * nlx;
                int baseZ1y1 = ((noisez + 1) * nly + noisey + 1) * nlx;

                float v000 = noiseBuf[baseZ0];
                float v100 = noiseBuf[baseZ0 + 1];
                float v010 = noiseBuf[baseZ0y1];
                float v110 = noiseBuf[baseZ0y1 + 1];
                float v001 = noiseBuf[baseZ1];
                float v101 = noiseBuf[baseZ1 + 1];
                float v011 = noiseBuf[baseZ1y1];
                float v111 = noiseBuf[baseZ1y1 + 1];

                for (int i = 0; i < sx; i++)
                {
                    valueBuf[rowBase + i] = TriLerp(
                        v000, v100, v010, v110,
                        v001, v101, v011, v111,
                        u, v, w, eased);

                    u += stepX;

                    if (u >= 1.0f)
                    {
                        u -= 1.0f;
                        noisex++;
                        int nx1 = noisex + 1;
                        v000 = v100;
                        v010 = v110;
                        v001 = v101;
                        v011 = v111;
                        v100 = noiseBuf[(noisez * nly + noisey) * nlx + nx1];
                        v110 = noiseBuf[(noisez * nly + noisey + 1) * nlx + nx1];
                        v101 = noiseBuf[((noisez + 1) * nly + noisey) * nlx + nx1];
                        v111 = noiseBuf[((noisez + 1) * nly + noisey + 1) * nlx + nx1];
                    }
                }

                v += stepY;

                if (v >= 1.0f)
                {
                    v -= 1.0f;
                    noisey++;
                }
            }
        }

        /// <summary>
        /// Accumulates one octave's valueBuf into resultBuf, optionally using a persistence map.
        /// </summary>
        /// <remarks>
        /// Matches Luanti's <c>Noise::updateResults()</c>.
        /// Source: <c>luanti/src/noise.cpp</c> lines 724-750.
        /// </remarks>
        public static void AccumulateOctave(
            NativeArray<float> valueBuf,
            NativeArray<float> resultBuf,
            NativeArray<float> gmapBuf,
            NativeArray<float> persistenceMap,
            float g,
            int bufSize,
            bool absValue,
            bool hasPersistenceMap)
        {
            // Luanti: four branches for abs/persist combinations (performance optimization)
            if (absValue)
            {
                if (hasPersistenceMap)
                {
                    for (int i = 0; i < bufSize; i++)
                    {
                        resultBuf[i] += gmapBuf[i] * math.abs(valueBuf[i]);
                        gmapBuf[i] *= persistenceMap[i];
                    }
                }
                else
                {
                    for (int i = 0; i < bufSize; i++)
                    {
                        resultBuf[i] += g * math.abs(valueBuf[i]);
                    }
                }
            }
            else
            {
                if (hasPersistenceMap)
                {
                    for (int i = 0; i < bufSize; i++)
                    {
                        resultBuf[i] += gmapBuf[i] * valueBuf[i];
                        gmapBuf[i] *= persistenceMap[i];
                    }
                }
                else
                {
                    for (int i = 0; i < bufSize; i++)
                    {
                        resultBuf[i] += g * valueBuf[i];
                    }
                }
            }
        }

        /// <summary>
        /// Applies offset and scale to the final result buffer.
        /// </summary>
        /// <remarks>
        /// Matches the final pass in Luanti's <c>Noise::noiseMap2D()</c> and <c>noiseMap3D()</c>.
        /// Only called when offset != 0 or scale != 1 (matches Luanti's threshold check).
        /// </remarks>
        public static void ApplyOffsetScale(
            NativeArray<float> resultBuf,
            int bufSize,
            float offset,
            float scale)
        {
            for (int i = 0; i < bufSize; i++)
            {
                resultBuf[i] = resultBuf[i] * scale + offset;
            }
        }
    }
}
