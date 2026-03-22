using System;
using Basalt.Meshing;
using Unity.Collections;

namespace Basalt.Client
{
    /// <summary>
    /// Pre-allocated pool of 18-cubed padded neighborhood buffers.
    /// Each buffer is used during a single chunk's meshing pipeline (neighborhood → count → write)
    /// and returned when the pipeline completes.
    /// </summary>
    /// <remarks>
    /// Uses the same flat-buffer + free-list pattern as <see cref="Basalt.Core.ChunkPool"/>.
    /// All memory is allocated once at startup; <see cref="GetBuffer"/> returns zero-allocation
    /// sub-array views into the flat buffer.
    ///
    /// Typical pool size: max concurrent in-flight meshes (default 16).
    /// Memory cost: poolSize * 5832 * 4 bytes (e.g. 16 * 23,328 = 373 KiB).
    /// </remarks>
    internal sealed class PaddedNeighborhoodPool : IDisposable
    {
        private NativeArray<uint> _data;
        private NativeArray<int> _freeStack;
        private int _freeCount;
        private readonly int _poolSize;

        /// <summary>Gets the total number of slots in the pool.</summary>
        public int Capacity => _poolSize;

        /// <summary>Gets the number of currently available slots.</summary>
        public int FreeCount => _freeCount;

        /// <summary>
        /// Creates a new pool with the specified number of padded buffer slots.
        /// </summary>
        /// <param name="poolSize">Number of concurrent padded buffers to support.</param>
        public PaddedNeighborhoodPool(int poolSize)
        {
            _poolSize = poolSize;
            _data = new NativeArray<uint>(
                poolSize * ChunkNeighborhood.PADDED_SIZE, Allocator.Persistent);
            _freeStack = new NativeArray<int>(poolSize, Allocator.Persistent);

            for (int i = 0; i < poolSize; i++)
            {
                _freeStack[i] = poolSize - 1 - i;
            }

            _freeCount = poolSize;
        }

        /// <summary>
        /// Rents a padded buffer slot from the pool.
        /// </summary>
        /// <param name="slot">The slot index, or -1 if the pool is exhausted.</param>
        /// <returns>True if a slot was available.</returns>
        public bool TryRent(out int slot)
        {
            if (_freeCount == 0)
            {
                slot = -1;

                return false;
            }

            _freeCount--;
            slot = _freeStack[_freeCount];

            return true;
        }

        /// <summary>
        /// Returns a padded buffer slot to the pool for reuse.
        /// </summary>
        /// <param name="slot">The slot index obtained from <see cref="TryRent"/>.</param>
        public void Return(int slot)
        {
            _freeStack[_freeCount] = slot;
            _freeCount++;
        }

        /// <summary>
        /// Gets the padded buffer NativeArray view for a given slot.
        /// The returned array is a zero-allocation sub-array view.
        /// </summary>
        /// <param name="slot">The slot index obtained from <see cref="TryRent"/>.</param>
        /// <returns>A NativeArray view of 5832 packed node values.</returns>
        public NativeArray<uint> GetBuffer(int slot)
        {
            return _data.GetSubArray(
                slot * ChunkNeighborhood.PADDED_SIZE,
                ChunkNeighborhood.PADDED_SIZE);
        }

        /// <summary>Disposes all native memory.</summary>
        public void Dispose()
        {
            if (_data.IsCreated)
            {
                _data.Dispose();
            }

            if (_freeStack.IsCreated)
            {
                _freeStack.Dispose();
            }
        }
    }
}
