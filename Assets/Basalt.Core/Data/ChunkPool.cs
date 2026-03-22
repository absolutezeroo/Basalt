using System;
using System.Runtime.CompilerServices;
using Unity.Collections;

namespace Basalt.Core
{
    /// <summary>
    /// Pre-allocated pool of chunk node data stored in a single flat NativeArray.
    /// Avoids per-chunk allocation/deallocation for zero GC in the game loop.
    /// </summary>
    /// <remarks>
    /// Memory layout: one contiguous <c>NativeArray&lt;uint&gt;</c> of size
    /// <c>poolSize * NODES_PER_BLOCK</c>. Each slot occupies 16 KiB (4096 × 4 bytes).
    /// A free-list stack tracks available slots. Generation counters detect stale handles.
    /// </remarks>
    public class ChunkPool : IDisposable
    {
        private NativeArray<uint> _nodeData;
        private NativeArray<int> _generations;
        private NativeArray<int> _freeStack;
        private int _freeCount;
        private int _poolSize;

        /// <summary>Gets the number of available (unrented) slots.</summary>
        public int FreeCount => _freeCount;

        /// <summary>Gets the number of currently rented slots.</summary>
        public int UsedCount => _poolSize - _freeCount;

        /// <summary>Gets the total capacity of the pool.</summary>
        public int Capacity => _poolSize;

        /// <summary>
        /// Creates a new chunk pool with the specified number of pre-allocated slots.
        /// </summary>
        /// <param name="poolSize">Number of chunk slots to pre-allocate.</param>
        public ChunkPool(int poolSize)
        {
            _poolSize = poolSize;
            _nodeData = new NativeArray<uint>(
                poolSize * BasaltConstants.NODES_PER_BLOCK, Allocator.Persistent);
            _generations = new NativeArray<int>(poolSize, Allocator.Persistent);
            _freeStack = new NativeArray<int>(poolSize, Allocator.Persistent);

            // Fill free stack with all slot indices (top of stack = index 0)
            for (int i = 0; i < poolSize; i++)
            {
                _freeStack[i] = poolSize - 1 - i;
            }

            _freeCount = poolSize;
        }

        /// <summary>
        /// Rents a chunk slot from the pool. The slot is filled with CONTENT_AIR.
        /// </summary>
        /// <param name="handle">The handle to the rented slot, or Invalid if pool is exhausted.</param>
        /// <returns>True if a slot was available, false if the pool is full.</returns>
        public bool TryRent(out ChunkHandle handle)
        {
            if (_freeCount == 0)
            {
                handle = ChunkHandle.Invalid;

                return false;
            }

            _freeCount--;
            int index = _freeStack[_freeCount];

            int generation = _generations[index] + 1;
            _generations[index] = generation;

            // Fill with air
            uint airPacked = MapNode.Pack(BasaltConstants.CONTENT_AIR, 0, 0);
            int start = index * BasaltConstants.NODES_PER_BLOCK;

            for (int i = 0; i < BasaltConstants.NODES_PER_BLOCK; i++)
            {
                _nodeData[start + i] = airPacked;
            }

            handle = new ChunkHandle(index, generation);

            return true;
        }

        /// <summary>
        /// Returns a chunk slot to the pool for reuse.
        /// The handle becomes stale after this call.
        /// </summary>
        /// <param name="handle">The handle obtained from TryRent.</param>
        public void Return(ChunkHandle handle)
        {
            if (!IsHandleValid(handle))
            {
                return;
            }

            _freeStack[_freeCount] = handle.Index;
            _freeCount++;
        }

        /// <summary>
        /// Checks whether a handle is still valid (not stale, not out of range).
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public bool IsHandleValid(ChunkHandle handle)
        {
            return handle.Index >= 0
                && handle.Index < _poolSize
                && _generations[handle.Index] == handle.Generation;
        }

        /// <summary>
        /// Gets the node data sub-array for a given chunk handle.
        /// The returned array is a view into the pool's flat buffer (no allocation).
        /// </summary>
        /// <param name="handle">A valid handle obtained from TryRent.</param>
        /// <returns>A NativeArray view of 4096 packed node values.</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public NativeArray<uint> GetNodes(ChunkHandle handle)
        {
            return _nodeData.GetSubArray(
                handle.Index * BasaltConstants.NODES_PER_BLOCK,
                BasaltConstants.NODES_PER_BLOCK);
        }

        public void Dispose()
        {
            if (_nodeData.IsCreated)
            {
                _nodeData.Dispose();
            }

            if (_generations.IsCreated)
            {
                _generations.Dispose();
            }

            if (_freeStack.IsCreated)
            {
                _freeStack.Dispose();
            }
        }
    }
}