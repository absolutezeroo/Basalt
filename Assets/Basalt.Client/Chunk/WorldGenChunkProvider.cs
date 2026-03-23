using System;
using System.Collections.Generic;
using Basalt.Core;
using Basalt.WorldGen;
using Unity.Collections;
using Unity.Jobs;
using Unity.Mathematics;

namespace Basalt.Client
{
    /// <summary>
    /// Bridges WorldGen output into the ChunkPool asynchronously. When a MapBlock is
    /// requested, the containing mapchunk (5×5×5 MapBlocks) is queued for generation.
    /// Generation runs as deferred Burst jobs completed on subsequent frames. Completed
    /// data is cached so that sibling MapBlocks do not trigger redundant generation.
    /// </summary>
    /// <remarks>
    /// Pipeline per frame (<see cref="Tick"/>):
    ///   1. Check in-flight mapchunk jobs for completion (budget: <c>maxCompletionsPerFrame</c>).
    ///   2. Schedule queued mapchunk generation (cap: <c>maxConcurrentJobs</c>).
    ///
    /// Callers extract ready chunk data via <see cref="TryFillChunk"/> after <see cref="Tick"/>.
    ///
    /// <b>Never calls <c>JobHandle.Complete()</c> on the same frame a job is scheduled.</b>
    /// Jobs are checked via <see cref="JobHandle.IsCompleted"/> and only completed on
    /// subsequent frames.
    /// </remarks>
    public class WorldGenChunkProvider : IDisposable
    {
        private readonly IMapgen[] _mapgens;
        private readonly Stack<int> _freeSlots;
        private readonly int _maxCompletionsPerFrame;

        /// <summary>
        /// Completed mapchunk cache, keyed by mapchunk position.
        /// Values are persistent NativeArrays of packed node data, taken directly from
        /// the VoxelManipulator on completion (zero-copy ownership transfer).
        /// </summary>
        private readonly Dictionary<int3, NativeArray<uint>> _cache;

        /// <summary>In-flight mapchunk generation jobs, keyed by mapchunk position.</summary>
        private readonly Dictionary<int3, InFlightMapchunk> _inFlight;

        /// <summary>
        /// FIFO queue of mapchunk positions waiting to be scheduled.
        /// Entries are added by <see cref="RequestGeneration"/> and consumed by
        /// <see cref="ProcessSchedulingQueue"/>.
        /// </summary>
        private readonly List<int3> _schedulingQueue;

        /// <summary>Scratch buffer for eviction keys (reused each frame).</summary>
        private readonly List<int3> _evictBuffer;

        /// <summary>Scratch buffer for completed mapchunk keys (reused each frame).</summary>
        private readonly List<int3> _completionScratch;

        private const int BLOCKS_PER_CHUNK = MapgenV7Constants.MAPCHUNK_BLOCKS;

        private struct InFlightMapchunk
        {
            public JobHandle Handle;
            public VoxelManipulator VM;
            public int MapgenSlot;
        }

        /// <summary>Number of mapchunk generation jobs currently in flight.</summary>
        public int InFlightCount => _inFlight.Count;

        /// <summary>Number of mapchunks waiting in the scheduling queue.</summary>
        public int QueuedCount => _schedulingQueue.Count;

        /// <summary>
        /// Creates a new async provider backed by a pool of independent mapgen instances.
        /// Each mapgen owns its own noise buffers, enabling true parallel generation
        /// (one mapgen per in-flight mapchunk, like Luanti's one-mapgen-per-EmergeThread).
        /// </summary>
        /// <param name="mapgens">
        /// An array of initialized <see cref="IMapgen"/> instances. The array length defines
        /// the maximum concurrency. Each instance must have its own <see cref="MapgenFeatures"/>.
        /// </param>
        /// <param name="maxCompletionsPerFrame">
        /// Maximum number of mapchunk completions processed per <see cref="Tick"/> call.
        /// Controls main-thread CPU spent on job completion and buffer ownership transfer.
        /// </param>
        public WorldGenChunkProvider(
            IMapgen[] mapgens,
            int maxCompletionsPerFrame = 2)
        {
            _mapgens = mapgens;
            _maxCompletionsPerFrame = maxCompletionsPerFrame;

            _freeSlots = new Stack<int>(mapgens.Length);
            for (int i = mapgens.Length - 1; i >= 0; i--)
            {
                _freeSlots.Push(i);
            }

            _cache = new Dictionary<int3, NativeArray<uint>>(64);
            _inFlight = new Dictionary<int3, InFlightMapchunk>(mapgens.Length);
            _schedulingQueue = new List<int3>(32);
            _evictBuffer = new List<int3>(16);
            _completionScratch = new List<int3>(mapgens.Length);
        }

        /// <summary>
        /// Ensures the mapchunk containing <paramref name="chunkPos"/> is scheduled for
        /// generation if not already cached, in flight, or queued. Non-blocking; the chunk
        /// will be available for extraction via <see cref="TryFillChunk"/> on a future frame.
        /// </summary>
        /// <param name="chunkPos">
        /// The chunk position (MapBlock coordinates, not world coordinates).
        /// </param>
        public void RequestGeneration(int3 chunkPos)
        {
            int3 mapchunkPos = MapBlockToMapchunk(chunkPos);

            if (_cache.ContainsKey(mapchunkPos) || _inFlight.ContainsKey(mapchunkPos))
            {
                return;
            }

            for (int i = 0; i < _schedulingQueue.Count; i++)
            {
                if (math.all(_schedulingQueue[i] == mapchunkPos))
                {
                    return;
                }
            }

            _schedulingQueue.Add(mapchunkPos);
        }

        /// <summary>
        /// Advances the async worldgen pipeline. Call once per frame from the main thread.
        /// Checks in-flight jobs for completion and schedules new generation from the queue.
        /// </summary>
        public void Tick()
        {
            ProcessCompletions();
            ProcessSchedulingQueue();
        }

        /// <summary>
        /// Attempts to extract worldgen data for a chunk into the pool.
        /// Returns <c>true</c> if the containing mapchunk is cached and extraction succeeded.
        /// Returns <c>false</c> if the mapchunk is still generating or not yet scheduled.
        /// </summary>
        /// <param name="pool">The chunk pool containing the target slot.</param>
        /// <param name="handle">A valid handle to the chunk slot to fill.</param>
        /// <param name="chunkPos">
        /// The chunk position (MapBlock coordinates, not world coordinates).
        /// </param>
        public bool TryFillChunk(ChunkPool pool, ChunkHandle handle, int3 chunkPos)
        {
            int3 mapchunkPos = MapBlockToMapchunk(chunkPos);

            if (!_cache.TryGetValue(mapchunkPos, out NativeArray<uint> vmNodes))
            {
                return false;
            }

            ExtractMapBlock(vmNodes, mapchunkPos, chunkPos, pool, handle);
            return true;
        }

        /// <summary>
        /// Evicts cached mapchunk data that is no longer needed.
        /// Call periodically to free memory from mapchunks whose MapBlocks have all been unloaded.
        /// </summary>
        /// <param name="activeChunks">Set of currently active chunk positions.</param>
        public void EvictUnused(NativeHashMap<int3, ChunkHandle> activeChunks)
        {
            _evictBuffer.Clear();

            foreach (int3 mapchunkPos in _cache.Keys)
            {
                if (!HasActiveBlockInMapchunk(mapchunkPos, activeChunks))
                {
                    _evictBuffer.Add(mapchunkPos);
                }
            }

            if (_evictBuffer.Count == 0)
            {
                return;
            }

            for (int i = 0; i < _evictBuffer.Count; i++)
            {
                NativeArray<uint> nodes = _cache[_evictBuffer[i]];

                if (nodes.IsCreated)
                {
                    nodes.Dispose();
                }

                _cache.Remove(_evictBuffer[i]);
            }
        }

        /// <summary>
        /// Completes all in-flight jobs and releases all native resources.
        /// </summary>
        public void Dispose()
        {
            foreach (var kvp in _inFlight)
            {
                kvp.Value.Handle.Complete();
                kvp.Value.VM.Dispose();
            }

            _inFlight.Clear();

            foreach (NativeArray<uint> nodes in _cache.Values)
            {
                if (nodes.IsCreated)
                {
                    nodes.Dispose();
                }
            }

            _cache.Clear();
            _schedulingQueue.Clear();

            if (_mapgens != null)
            {
                for (int i = 0; i < _mapgens.Length; i++)
                {
                    _mapgens[i]?.Dispose();
                }
            }
        }

        /// <summary>
        /// Checks in-flight mapchunk jobs for completion. Completed jobs have their
        /// node buffers transferred to the cache (zero-copy ownership transfer).
        /// Only <see cref="JobHandle.IsCompleted"/> jobs are finalized — never same-frame.
        /// </summary>
        private void ProcessCompletions()
        {
            _completionScratch.Clear();

            foreach (var kvp in _inFlight)
            {
                if (kvp.Value.Handle.IsCompleted)
                {
                    _completionScratch.Add(kvp.Key);
                }
            }

            int budget = _maxCompletionsPerFrame;

            for (int i = 0; i < _completionScratch.Count && budget > 0; i++)
            {
                int3 mapchunkPos = _completionScratch[i];
                InFlightMapchunk flight = _inFlight[mapchunkPos];

                flight.Handle.Complete();

                // Return the mapgen slot to the pool now that its jobs are done.
                _freeSlots.Push(flight.MapgenSlot);

                // Transfer ownership of the VM node buffer to the cache.
                // This avoids a ~2 MB NativeArray copy per mapchunk.
                _cache[mapchunkPos] = flight.VM.Nodes;

                // Dispose only the heightmap; Nodes are now owned by _cache.
                if (flight.VM.Heightmap.IsCreated)
                {
                    flight.VM.Heightmap.Dispose();
                }

                _inFlight.Remove(mapchunkPos);
                budget--;
            }
        }

        /// <summary>
        /// Schedules mapchunk generation jobs from the FIFO queue while free mapgen slots
        /// are available. Each scheduled job uses <see cref="Allocator.Persistent"/> for the
        /// VoxelManipulator so it remains valid across frames until completion.
        /// </summary>
        private void ProcessSchedulingQueue()
        {
            int i = 0;

            while (i < _schedulingQueue.Count && _freeSlots.Count > 0)
            {
                int3 mapchunkPos = _schedulingQueue[i];

                // Skip if already completed while waiting in queue
                if (_cache.ContainsKey(mapchunkPos) || _inFlight.ContainsKey(mapchunkPos))
                {
                    _schedulingQueue.RemoveAt(i);
                    continue;
                }

                int slot = _freeSlots.Pop();

                int3 chunkOrigin = mapchunkPos * MapgenV7Constants.MAPCHUNK_SIZE;
                var handle = _mapgens[slot].Generate(
                    chunkOrigin, Allocator.Persistent, out VoxelManipulator vm);

                _inFlight[mapchunkPos] = new InFlightMapchunk
                {
                    Handle = handle,
                    VM = vm,
                    MapgenSlot = slot,
                };

                _schedulingQueue.RemoveAt(i);
            }
        }

        /// <summary>
        /// Extracts one 16³ MapBlock from cached VM data into the chunk pool.
        /// </summary>
        private static void ExtractMapBlock(
            NativeArray<uint> vmNodes,
            int3 mapchunkPos,
            int3 chunkPos,
            ChunkPool pool,
            ChunkHandle handle)
        {
            NativeArray<uint> poolNodes = pool.GetNodes(handle);

            // Local offset of this MapBlock within the mapchunk (0..4 per axis)
            int3 localBlock = chunkPos - mapchunkPos * BLOCKS_PER_CHUNK;

            // Starting node offset within the VM buffer
            // The VM covers [chunkOrigin.x .. +80) in X/Z, [chunkOrigin.y - 1 .. +81) in Y
            // localBlock * 16 gives the offset in nodes from the mapchunk origin
            // Add +1 in Y for the overgeneration layer at the bottom
            int baseX = localBlock.x * BasaltConstants.MAP_BLOCKSIZE;
            int baseY = localBlock.y * BasaltConstants.MAP_BLOCKSIZE + MapgenV7Constants.OVERGEN_SIZE;
            int baseZ = localBlock.z * BasaltConstants.MAP_BLOCKSIZE;

            const int vmSx = VoxelManipulator.Sx; // 80
            const int vmSy = VoxelManipulator.Sy; // 82

            for (int z = 0; z < BasaltConstants.MAP_BLOCKSIZE; z++)
            {
                for (int y = 0; y < BasaltConstants.MAP_BLOCKSIZE; y++)
                {
                    for (int x = 0; x < BasaltConstants.MAP_BLOCKSIZE; x++)
                    {
                        int vmIndex = (baseX + x)
                                    + (baseY + y) * vmSx
                                    + (baseZ + z) * vmSx * vmSy;

                        int poolIndex = CoordinateUtils.NodeIndex(x, y, z);

                        poolNodes[poolIndex] = vmNodes[vmIndex];
                    }
                }
            }
        }

        /// <summary>
        /// Converts a MapBlock chunk position to the mapchunk position that contains it.
        /// Uses floor division so negative coordinates are handled correctly.
        /// </summary>
        private static int3 MapBlockToMapchunk(int3 chunkPos)
        {
            return new int3(
                CoordinateUtils.FloorDiv(chunkPos.x, BLOCKS_PER_CHUNK),
                CoordinateUtils.FloorDiv(chunkPos.y, BLOCKS_PER_CHUNK),
                CoordinateUtils.FloorDiv(chunkPos.z, BLOCKS_PER_CHUNK));
        }

        /// <summary>
        /// Checks whether any active MapBlock falls within the given mapchunk.
        /// </summary>
        private static bool HasActiveBlockInMapchunk(
            int3 mapchunkPos,
            NativeHashMap<int3, ChunkHandle> activeChunks)
        {
            int3 minBlock = mapchunkPos * BLOCKS_PER_CHUNK;
            int3 maxBlock = minBlock + new int3(BLOCKS_PER_CHUNK - 1);

            for (int z = minBlock.z; z <= maxBlock.z; z++)
            {
                for (int y = minBlock.y; y <= maxBlock.y; y++)
                {
                    for (int x = minBlock.x; x <= maxBlock.x; x++)
                    {
                        if (activeChunks.ContainsKey(new int3(x, y, z)))
                        {
                            return true;
                        }
                    }
                }
            }

            return false;
        }
    }
}
