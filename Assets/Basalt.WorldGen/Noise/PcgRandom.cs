using System.Runtime.CompilerServices;

namespace Basalt.WorldGen
{
    /// <summary>
    /// PCG-XSH-RR pseudorandom number generator matching Luanti's <c>PcgRandom</c>.
    /// High quality 32-bit output from 128-bit state. Value type — copy to save state.
    /// </summary>
    /// <remarks>
    /// Source: <c>luanti/src/noise.h</c> — <c>PcgRandom</c> class declaration.
    /// Source: <c>luanti/src/noise.cpp</c> — <c>PcgRandom::seed()</c> and <c>PcgRandom::next()</c>.
    /// </remarks>
    public struct PcgRandom
    {
        /// <summary>Minimum signed value representable by this PRNG's range methods.</summary>
        public const int RANDOM_MIN = int.MinValue;

        /// <summary>Maximum signed value representable by this PRNG's range methods.</summary>
        public const int RANDOM_MAX = int.MaxValue;

        /// <summary>Full unsigned range of <see cref="Next"/>.</summary>
        public const uint RANDOM_RANGE = uint.MaxValue;

        private ulong _state;
        private ulong _inc;

        /// <summary>
        /// Creates a PcgRandom with the given state and optional sequence selector.
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public PcgRandom(ulong state = NoiseConstants.PCG_DEFAULT_STATE,
                         ulong seq = NoiseConstants.PCG_DEFAULT_SEQ)
        {
            _state = 0UL;
            _inc = (seq << 1) | 1UL;
            Next();

            unchecked
            {
                _state += state;
            }

            Next();
        }

        /// <summary>Returns the next 32-bit value (full range).</summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public uint Next()
        {
            unchecked
            {
                ulong oldstate = _state;
                _state = oldstate * NoiseConstants.PCG_MULTIPLIER + _inc;
                uint xorshifted = (uint)(((oldstate >> 18) ^ oldstate) >> 27);
                int rot = (int)(oldstate >> 59);

                return (xorshifted >> rot) | (xorshifted << ((-rot) & 31));
            }
        }

        /// <summary>
        /// Returns a uniformly distributed value in [0, bound) using rejection sampling.
        /// </summary>
        /// <remarks>
        /// Matches Luanti's <c>PcgRandom::range(u32 bound)</c> which eliminates modulo bias.
        /// </remarks>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public uint Range(uint bound)
        {
            if (bound == 0)
            {
                return Next();
            }

            if (bound == 1)
            {
                return 0;
            }

            unchecked
            {
                uint threshold = (uint)(-(int)bound) % bound;
                uint r;

                do
                {
                    r = Next();
                } while (r < threshold);

                return r % bound;
            }
        }

        /// <summary>Returns a value in [min, max] inclusive.</summary>
        /// <remarks>
        /// Luanti uses <c>(s64)max - (s64)min + 1</c> to avoid signed 32-bit overflow.
        /// Source: <c>luanti/src/noise.cpp</c> line 127.
        /// </remarks>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public int Range(int min, int max)
        {
            uint bound = (uint)((long)max - (long)min + 1);

            return (int)Range(bound) + min;
        }

        /// <summary>
        /// Returns a value from a normal-like distribution using Irwin-Hall approximation.
        /// </summary>
        /// <remarks>
        /// Matches Luanti's <c>PcgRandom::randNormalDist()</c>.
        /// </remarks>
        public int RandNormalDist(int min, int max, int numTrials = 6)
        {
            int accum = 0;

            for (int i = 0; i < numTrials; i++)
            {
                accum += Range(min, max);
            }

            // Luanti uses myround((float)accum / num_trials): round half away from zero
            float avg = (float)accum / numTrials;

            return avg < 0f ? (int)(avg - 0.5f) : (int)(avg + 0.5f);
        }

        /// <summary>Gets the two-element state for serialization.</summary>
        public void GetState(out ulong state, out ulong inc)
        {
            state = _state;
            inc = _inc;
        }

        /// <summary>Sets the two-element state from deserialization.</summary>
        public void SetState(ulong state, ulong inc)
        {
            _state = state;
            _inc = inc;
        }
    }
}
