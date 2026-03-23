using System.Runtime.CompilerServices;
using Unity.Burst;
using Unity.Mathematics;

namespace Basalt.Core
{
    /// <summary>
    /// Coordinate conversion utilities matching Luanti conventions.
    /// Y-up, ±31007 blocks per axis. Index order: x varies fastest.
    /// </summary>
    /// <remarks>
    /// Luanti equivalent: <c>getContainerPos()</c> and related functions
    /// in <c>luanti/src/util/numeric.h</c>.
    /// </remarks>
    [BurstCompile]
    public static class CoordinateUtils
    {
        /// <summary>
        /// Computes the linear index of a node within a 16³ MapBlock.
        /// </summary>
        /// <remarks>
        /// Index = x + y*16 + z*256 (x varies fastest, then y, then z).
        /// </remarks>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static int NodeIndex(int x, int y, int z)
            => x + (y << 4) + (z << 8);

        /// <summary>
        /// Converts a world position to its containing chunk position.
        /// </summary>
        /// <param name="worldPos">The world position in node coordinates.</param>
        /// <returns>The chunk position containing the given world position.</returns>
        /// <remarks>
        /// Uses floor division (not C# truncation) so negative coordinates work correctly.
        /// Example: world (-1,0,0) → chunk (-1,0,0).
        /// </remarks>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static int3 WorldToChunk(int3 worldPos)
        {
            return new int3(
                FloorDiv(worldPos.x, BasaltConstants.MAP_BLOCKSIZE),
                FloorDiv(worldPos.y, BasaltConstants.MAP_BLOCKSIZE),
                FloorDiv(worldPos.z, BasaltConstants.MAP_BLOCKSIZE)
            );
        }

        /// <summary>
        /// Converts a world position to local position within its chunk (0..15 per axis).
        /// </summary>
        /// <param name="worldPos">The world position in node coordinates.</param>
        /// <returns>The local position within the containing chunk.</returns>
        /// <remarks>
        /// Uses floor modulo so negative coordinates work correctly.
        /// Example: world (-1,0,0) → local (15,0,0).
        /// </remarks>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static int3 WorldToLocal(int3 worldPos)
        {
            return new int3(
                FloorMod(worldPos.x, BasaltConstants.MAP_BLOCKSIZE),
                FloorMod(worldPos.y, BasaltConstants.MAP_BLOCKSIZE),
                FloorMod(worldPos.z, BasaltConstants.MAP_BLOCKSIZE)
            );
        }

        /// <summary>
        /// Converts a chunk position to the world position of its minimum corner (local 0,0,0).
        /// </summary>
        /// <param name="chunkPos">The chunk position.</param>
        /// <returns>The world position of the chunk's minimum corner.</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static int3 ChunkToWorld(int3 chunkPos)
        {
            return chunkPos * BasaltConstants.MAP_BLOCKSIZE;
        }

        /// <summary>
        /// Checks whether a chunk position is within the valid world limits (±1937 on each axis).
        /// </summary>
        /// <param name="pos">The chunk position (MapBlock coordinates) to validate.</param>
        /// <returns>True if the position is within world bounds.</returns>
        /// <remarks>
        /// Luanti equivalent: <c>blockpos_over_max_limit()</c> in <c>luanti/src/mapblock.h</c>.
        /// The limit is <c>MAX_MAP_GENERATION_LIMIT / MAP_BLOCKSIZE = 1937</c>.
        /// </remarks>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static bool IsValidChunkPos(int3 pos)
        {
            return pos.x >= -BasaltConstants.MAX_MAP_GENERATION_LIMIT_BP
                && pos.x <= BasaltConstants.MAX_MAP_GENERATION_LIMIT_BP
                && pos.y >= -BasaltConstants.MAX_MAP_GENERATION_LIMIT_BP
                && pos.y <= BasaltConstants.MAX_MAP_GENERATION_LIMIT_BP
                && pos.z >= -BasaltConstants.MAX_MAP_GENERATION_LIMIT_BP
                && pos.z <= BasaltConstants.MAX_MAP_GENERATION_LIMIT_BP;
        }

        /// <summary>
        /// Floor division: always rounds toward negative infinity.
        /// </summary>
        /// <remarks>
        /// C# integer division truncates toward zero, which gives wrong results for negative coords.
        /// Example: FloorDiv(-1, 16) = -1 (not 0 like C# default).
        /// </remarks>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static int FloorDiv(int a, int b)
        {
            return a / b - (a % b < 0 ? 1 : 0);
        }

        /// <summary>
        /// Floor modulo: always returns a non-negative result in [0, b).
        /// </summary>
        /// <remarks>
        /// Example: FloorMod(-1, 16) = 15 (not -1 like C# default).
        /// </remarks>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static int FloorMod(int a, int b)
        {
            int r = a % b;

            return r < 0 ? r + b : r;
        }
    }
}