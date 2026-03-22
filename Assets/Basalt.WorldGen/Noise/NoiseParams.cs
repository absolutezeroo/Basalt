using System.Runtime.CompilerServices;
using Unity.Burst;
using Unity.Mathematics;

namespace Basalt.WorldGen
{
    /// <summary>
    /// Blittable noise parameter set, mirroring Luanti's <c>NoiseParams</c>.
    /// Passed by value into Burst jobs — no heap allocation required.
    /// </summary>
    /// <remarks>
    /// Source: <c>luanti/src/noise.h</c> — struct NoiseParams (lines 119-143).
    ///
    /// Default flag <see cref="NoiseConstants.NOISE_FLAG_DEFAULTS"/> enables easing for 2D
    /// but not 3D, matching Luanti behaviour exactly.
    /// </remarks>
    [BurstCompile]
    public readonly struct NoiseParams
    {
        /// <summary>Constant offset added to the final noise result after scale.</summary>
        public readonly float Offset;

        /// <summary>Amplitude multiplier applied to the accumulated octave sum.</summary>
        public readonly float Scale;

        /// <summary>Feature size in world units per axis (larger = smoother terrain).</summary>
        public readonly float3 Spread;

        /// <summary>
        /// Per-noise seed mixed with the mapgen seed. Signed to match Luanti's <c>s32 seed</c>.
        /// </summary>
        public readonly int Seed;

        /// <summary>Number of fractal octaves to accumulate.</summary>
        public readonly ushort Octaves;

        /// <summary>Persistence: amplitude multiplier between successive octaves (typically 0.5-0.7).</summary>
        public readonly float Persist;

        /// <summary>Lacunarity: frequency multiplier between successive octaves (typically 2.0).</summary>
        public readonly float Lacunarity;

        /// <summary>
        /// Bit flags controlling noise behaviour. See <see cref="NoiseConstants"/> NOISE_FLAG_* constants.
        /// Default: <see cref="NoiseConstants.NOISE_FLAG_DEFAULTS"/>.
        /// </summary>
        public readonly uint Flags;

        /// <summary>Creates a fully specified NoiseParams.</summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public NoiseParams(float offset, float scale, float3 spread, int seed,
            ushort octaves, float persist, float lacunarity,
            uint flags = NoiseConstants.NOISE_FLAG_DEFAULTS)
        {
            Offset = offset;
            Scale = scale;
            Spread = spread;
            Seed = seed;
            Octaves = octaves;
            Persist = persist;
            Lacunarity = lacunarity;
            Flags = flags;
        }

        /// <summary>
        /// Returns true when 2D easing (quintic s-curve) is enabled.
        /// Matches Luanti: <c>np.flags &amp; (NOISE_FLAG_DEFAULTS | NOISE_FLAG_EASED)</c>.
        /// </summary>
        public bool IsEased2D
        {
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            get => (Flags & (NoiseConstants.NOISE_FLAG_DEFAULTS | NoiseConstants.NOISE_FLAG_EASED)) != 0;
        }

        /// <summary>
        /// Returns true when 3D easing (quintic s-curve) is enabled.
        /// Matches Luanti: <c>np.flags &amp; NOISE_FLAG_EASED</c> (DEFAULTS alone does NOT enable 3D easing).
        /// </summary>
        public bool IsEased3D
        {
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            get => (Flags & NoiseConstants.NOISE_FLAG_EASED) != 0;
        }

        /// <summary>Returns true when NOISE_FLAG_ABSVALUE is set.</summary>
        public bool IsAbsValue
        {
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            get => (Flags & NoiseConstants.NOISE_FLAG_ABSVALUE) != 0;
        }

        /// <summary>
        /// Returns a NoiseParams with Luanti's default values:
        /// offset=0, scale=1, spread=(250,250,250), seed=12345, octaves=3, persist=0.6, lacunarity=2.
        /// </summary>
        public static NoiseParams Default
        {
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            get => new NoiseParams(0f, 1f, new float3(250f, 250f, 250f), 12345, 3, 0.6f, 2.0f);
        }
    }
}
