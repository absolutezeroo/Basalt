using System.Runtime.CompilerServices;
using Unity.Mathematics;

namespace Basalt.Meshing
{
    /// <summary>
    /// Pure bitmask utility methods for the binary greedy meshing algorithm.
    /// All methods are static, allocation-free, and safe to call from Burst jobs.
    /// </summary>
    /// <remarks>
    /// Each 16-node row is represented as a uint bitmask where bit N (from LSB) is set
    /// when node N along that row has a visible face. Bits 16-31 are always zero.
    /// </remarks>
    public static class GreedyBitMaskUtils
    {
        /// <summary>
        /// Returns the length of the leading run of set bits starting at bit 0.
        /// </summary>
        /// <param name="mask">A bitmask where set bits represent visible nodes.</param>
        /// <returns>The number of consecutive set bits from the LSB, or 0 if bit 0 is clear.</returns>
        /// <remarks>
        /// Uses <c>math.tzcnt</c> on the inverted mask. Example: 0b1111 returns 4.
        /// </remarks>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static int ExtractSpanLength(uint mask)
        {
            // ~mask flips all bits; tzcnt counts trailing zeros = length of the leading 1-run.
            // When mask is 0, ~mask is 0xFFFFFFFF, tzcnt returns 0. Correct.
            // When mask is 0xFFFF, ~mask is 0xFFFF0000, tzcnt returns 16. Correct.
            return math.tzcnt(~mask);
        }

        /// <summary>
        /// Builds a mask with exactly <paramref name="length"/> consecutive bits set from the LSB.
        /// </summary>
        /// <param name="length">Number of bits to set (1-16).</param>
        /// <returns>A mask with bits [0..length-1] set and all others clear.</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static uint BuildSpanMask(int length)
        {
            return (1u << length) - 1u;
        }

        /// <summary>
        /// Returns true when all bits set in <paramref name="spanMask"/> are also set in
        /// <paramref name="rowMask"/>, meaning an adjacent column can extend the greedy quad.
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static bool RowContainsSpan(uint rowMask, uint spanMask)
        {
            return (rowMask & spanMask) == spanMask;
        }
    }
}
