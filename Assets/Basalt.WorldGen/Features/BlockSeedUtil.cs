using System.Runtime.CompilerServices;
using Unity.Mathematics;

namespace Basalt.WorldGen
{
    /// <summary>
    /// Block seed computation matching Luanti's <c>getBlockSeed2()</c>.
    /// Used by ore and decoration placement to derive deterministic per-chunk seeds.
    /// </summary>
    /// <remarks>
    /// Source: <c>luanti/src/mapgen/mapgen.h</c> — <c>getBlockSeed2()</c>.
    /// </remarks>
    public static class BlockSeedUtil
    {
        /// <summary>
        /// Computes a deterministic block seed from a block position and world seed.
        /// Matches Luanti's <c>getBlockSeed2(v3s16 p, s32 seed)</c>.
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static uint GetBlockSeed2(int3 p, int seed)
        {
            unchecked
            {
                uint n = (uint)(
                    (uint)p.x * 1619 +
                    (uint)p.y * 31337 +
                    (uint)p.z * 52591 +
                    (uint)seed * 1013);
                n = (n >> 13) ^ n;
                return n * (n * n * 60493 + 19990303) + 1376312589;
            }
        }
    }
}
