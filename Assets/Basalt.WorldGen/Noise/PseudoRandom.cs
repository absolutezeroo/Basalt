using System.Runtime.CompilerServices;

namespace Basalt.WorldGen
{
    /// <summary>
    /// Linear congruential PRNG matching Luanti's <c>PseudoRandom</c>.
    /// Value type — copy the struct to save/restore state.
    /// </summary>
    /// <remarks>
    /// Formula: <c>m_next = (uint)m_next * 1103515245 + 12345</c> (unsigned overflow).
    /// Output: <c>(uint)(m_next / 65536) % 32768</c>, range [0, 32767].
    /// Source: <c>luanti/src/noise.h</c> — <c>PseudoRandom</c> class.
    /// </remarks>
    public struct PseudoRandom
    {
        /// <summary>Maximum value returned by <see cref="Next"/>.</summary>
        public const uint RANDOM_RANGE = 32767U;

        private int _next;

        /// <summary>Creates a PseudoRandom with the given seed.</summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public PseudoRandom(int seed)
        {
            _next = seed;
        }

        /// <summary>
        /// Returns the next value in [0, 32767].
        /// </summary>
        /// <remarks>
        /// Luanti uses signed division (<c>m_next / 65536</c>) for backwards compatibility.
        /// The static_cast to uint happens around the multiply, but the division is signed.
        /// Source: <c>luanti/src/noise.h</c> — <c>PseudoRandom::next()</c>.
        /// </remarks>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public uint Next()
        {
            unchecked
            {
                // Luanti: m_next = static_cast<u32>(m_next) * 1103515245U + 12345U;
                _next = (int)((uint)_next * 1103515245U + 12345U);

                // Luanti: return static_cast<u32>(m_next / 65536) % (RANDOM_RANGE + 1U);
                // Signed division is required due to backwards compatibility
                return (uint)(_next / 65536) % (RANDOM_RANGE + 1U);
            }
        }

        /// <summary>Returns a value in [min, max] inclusive.</summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public int Range(int min, int max)
        {
            return (int)(Next() % (uint)(max - min + 1)) + min;
        }

        /// <summary>Returns the raw internal state (for serialization).</summary>
        public int State
        {
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            get => _next;
        }
    }
}
