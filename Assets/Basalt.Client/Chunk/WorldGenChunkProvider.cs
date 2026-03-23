using System;
using System.Collections.Generic;
using Basalt.Core;
using Basalt.WorldGen;
using Unity.Collections;
using Unity.Mathematics;
using UnityEngine;

namespace Basalt.Client
{
    /// <summary>
    /// Bridges WorldGen output into the ChunkPool. When a MapBlock is requested,
    /// generates the containing mapchunk (5x5x5 MapBlocks) and extracts the 16³ node
    /// sub-block into the pool.
    /// </summary>
    /// <remarks>
    /// Caches generated mapchunk data so that sibling MapBlocks within the same mapchunk
    /// do not trigger redundant generation. The cache is keyed by mapchunk position
    /// (floor-divided by 5).
    ///
    /// This class calls <c>JobHandle.Complete()</c> synchronously. For production use,
    /// the generation should be deferred across frames. This synchronous approach is
    /// acceptable for the initial visual test.
    /// </remarks>
    public class WorldGenChunkProvider : IDisposable
    {
        private readonly IMapgen _mapgen;
        private readonly Dictionary<int3, NativeArray<uint>> _cache;

        private const int BLOCKS_PER_CHUNK = MapgenV7Constants.MAPCHUNK_BLOCKS; // 5

        /// <summary>
        /// Creates a new provider that uses the specified mapgen to fill chunk data.
        /// </summary>
        /// <param name="mapgen">
        /// An initialized <see cref="IMapgen"/> instance. The caller must have already called
        /// <see cref="IMapgen.Initialize"/> with resolved content IDs.
        /// </param>
        public WorldGenChunkProvider(IMapgen mapgen)
        {
            _mapgen = mapgen;
            _cache = new Dictionary<int3, NativeArray<uint>>();
        }

        /// <summary>
        /// Fills a chunk slot in the pool with worldgen data for the given chunk position.
        /// </summary>
        /// <param name="pool">The chunk pool containing the target slot.</param>
        /// <param name="handle">A valid handle to the chunk slot to fill.</param>
        /// <param name="chunkPos">
        /// The chunk position (MapBlock coordinates, not world coordinates).
        /// </param>
        public void FillChunk(ChunkPool pool, ChunkHandle handle, int3 chunkPos)
        {
            int3 mapchunkPos = MapBlockToMapchunk(chunkPos);

            if (!_cache.TryGetValue(mapchunkPos, out NativeArray<uint> vmNodes))
            {
                vmNodes = GenerateMapchunk(mapchunkPos);
                _cache[mapchunkPos] = vmNodes;
            }

            ExtractMapBlock(vmNodes, mapchunkPos, chunkPos, pool, handle);
        }

        /// <summary>
        /// Evicts cached mapchunk data that is no longer needed.
        /// Call periodically to free memory from mapchunks whose MapBlocks have all been unloaded.
        /// </summary>
        /// <param name="activeChunks">Set of currently active chunk positions.</param>
        public void EvictUnused(NativeHashMap<int3, ChunkHandle> activeChunks)
        {
            // Collect mapchunk keys to remove
            List<int3> toRemove = null;

            foreach (int3 mapchunkPos in _cache.Keys)
            {
                if (!HasActiveBlockInMapchunk(mapchunkPos, activeChunks))
                {
                    toRemove ??= new List<int3>();
                    toRemove.Add(mapchunkPos);
                }
            }

            if (toRemove == null)
            {
                return;
            }

            for (int i = 0; i < toRemove.Count; i++)
            {
                NativeArray<uint> nodes = _cache[toRemove[i]];

                if (nodes.IsCreated)
                {
                    nodes.Dispose();
                }

                _cache.Remove(toRemove[i]);
            }
        }

        /// <summary>
        /// Disposes all cached mapchunk data and the mapgen instance.
        /// </summary>
        public void Dispose()
        {
            foreach (NativeArray<uint> nodes in _cache.Values)
            {
                if (nodes.IsCreated)
                {
                    nodes.Dispose();
                }
            }

            _cache.Clear();
            _mapgen?.Dispose();
        }

        /// <summary>
        /// Generates a mapchunk and returns a persistent copy of the VM node buffer.
        /// </summary>
        private NativeArray<uint> GenerateMapchunk(int3 mapchunkPos)
        {
            // chunkOrigin is the world-space min corner of the mapchunk (not overgeneration)
            int3 chunkOrigin = mapchunkPos * MapgenV7Constants.MAPCHUNK_SIZE;

            var jobHandle = _mapgen.Generate(chunkOrigin, Allocator.TempJob, out VoxelManipulator vm);
            jobHandle.Complete();

            // Copy VM nodes into a persistent buffer so we can dispose the TempJob VM
            var persistent = new NativeArray<uint>(vm.Nodes.Length, Allocator.Persistent);
            NativeArray<uint>.Copy(vm.Nodes, persistent);

            vm.Dispose();

            return persistent;
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
