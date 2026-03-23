using System;
using Unity.Collections;
using Unity.Jobs;
using Unity.Mathematics;

namespace Basalt.WorldGen
{
    /// <summary>
    /// Common contract for all mapgen implementations.
    /// </summary>
    /// <remarks>
    /// Lifecycle:
    ///   1. Construct the concrete mapgen (e.g. <see cref="MapgenV7"/> or <see cref="MapgenFlat"/>).
    ///   2. Call <see cref="Initialize"/> once after NodeRegistry is populated.
    ///   3. Per mapchunk: call <see cref="Generate"/> to receive a <see cref="JobHandle"/>.
    ///   4. Caller completes the handle (next frame or on demand), then reads the VoxelManipulator.
    ///   5. Dispose the VoxelManipulator once MapBlock data has been extracted.
    ///   6. Dispose this instance when the world unloads.
    ///
    /// No MonoBehaviour — lives in Basalt.WorldGen.
    /// </remarks>
    public interface IMapgen : IDisposable
    {
        /// <summary>
        /// Finalizes content ID binding. Must be called once after the NodeRegistry is populated.
        /// </summary>
        /// <param name="contentStone">Resolved content ID for stone.</param>
        /// <param name="contentWater">Resolved content ID for water source.</param>
        void Initialize(ushort contentStone, ushort contentWater);

        /// <summary>
        /// Schedules the full noise and terrain fill pipeline for one mapchunk.
        /// Returns a <see cref="JobHandle"/> that the caller completes at its own pace.
        /// </summary>
        /// <param name="chunkOrigin">
        /// World position of the mapchunk's minimum corner node (not counting overgeneration).
        /// </param>
        /// <param name="vmAllocator">
        /// Allocator for the returned VoxelManipulator. Use <c>Allocator.TempJob</c>
        /// when the caller completes the handle within 4 frames.
        /// </param>
        /// <param name="vm">The allocated VoxelManipulator that will receive the terrain data.</param>
        /// <returns>
        /// A JobHandle for the entire pipeline. Must be completed before reading <paramref name="vm"/>.
        /// </returns>
        JobHandle Generate(int3 chunkOrigin, Allocator vmAllocator, out VoxelManipulator vm);
    }
}
