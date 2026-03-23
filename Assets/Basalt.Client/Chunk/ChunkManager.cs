using System.Collections.Generic;
using Basalt.Core;
using Basalt.WorldGen;
using Unity.Collections;
using Unity.Mathematics;
using UnityEngine;

namespace Basalt.Client
{
    /// <summary>
    /// Manages the lifecycle of voxel chunks around the player: loading, unloading,
    /// meshing, and rendering. Creates GameObjects with MeshFilter and MeshRenderer
    /// for each visible chunk.
    /// </summary>
    /// <remarks>
    /// Lives in Basalt.Client as a MonoBehaviour. Zero GC allocation in the hot-path
    /// Update loop (aside from the one-time batch mesh array in ApplyAndDispose).
    ///
    /// Pipeline per frame:
    /// 1. Update: ProcessUnloads → ProcessLoads → MeshApplier.Tick
    /// 2. LateUpdate: (reserved for future render-order sorting)
    ///
    /// Chunks beyond <c>drawDistance + UNLOAD_HYSTERESIS</c> are recycled into the pool.
    /// </remarks>
    public class ChunkManager : MonoBehaviour
    {
        private const int UNLOAD_HYSTERESIS = 2;

        [Header("Chunk Settings")]
        [SerializeField] private int _drawDistance = 8;
        [SerializeField] private int _maxLoadsPerFrame = 4;
        [SerializeField] private int _maxUnloadsPerFrame = 4;
        [SerializeField] private int _maxConcurrentMeshes = 16;

        [Header("WorldGen")]
        [SerializeField] private int _worldSeed = 12345;

        [Header("References")]
        [SerializeField] private Transform _playerTransform;
        [SerializeField] private RenderingBootstrapper _renderingBootstrapper;

        private ChunkPool _pool;
        private NativeHashMap<int3, ChunkHandle> _activeChunks;
        private NativeArray<int3> _spiralOffsets;

        private ChunkMeshApplier _meshApplier;
        private MeshPool _meshPool;
        private Dictionary<int3, GameObject> _chunkObjects;
        private Material _voxelMaterial;
        private NativeArray<NodeDefinition> _nodeDefs;

        private WorldGenChunkProvider _worldGenProvider;

        private BiomeRegistry _biomeRegistry;
        private OreRegistry _oreRegistry;
        private DecorationRegistry _decoRegistry;
        private MapgenFeatures _features;

        private int3 _currentCenter;
        private int _loadIndex;
        private bool _initialized;

        /// <summary>Gets the number of currently loaded chunks.</summary>
        public int ActiveCount => _activeChunks.IsCreated ? _activeChunks.Count : 0;

        /// <summary>Gets the current draw distance in chunks.</summary>
        public int DrawDistance => _drawDistance;

        private void Awake()
        {
            _spiralOffsets = ComputeSpiralOffsets(_drawDistance);

            int poolSize = _spiralOffsets.Length + 128;
            _pool = new ChunkPool(poolSize);
            _activeChunks = new NativeHashMap<int3, ChunkHandle>(poolSize, Allocator.Persistent);
            _chunkObjects = new Dictionary<int3, GameObject>(poolSize);

            _meshApplier = new ChunkMeshApplier(_maxConcurrentMeshes);
            _meshPool = new MeshPool(poolSize);

            // Force initial load on first frame
            _currentCenter = new int3(int.MaxValue);
            _loadIndex = 0;
            _initialized = true;
        }

        private void Start()
        {
            // RenderingBootstrapper.Awake runs before this (execution order)
            if (_renderingBootstrapper != null)
            {
                _voxelMaterial = _renderingBootstrapper.VoxelMaterial;
                _nodeDefs = _renderingBootstrapper.NodeRegistry.RuntimeDefsNative;

                InitializeWorldGen(_renderingBootstrapper.NodeRegistry);
            }
        }

        private void InitializeWorldGen(NodeRegistry registry)
        {
            ushort contentStone = registry.GetIdByName("default:stone");
            ushort contentWater = registry.GetIdByName("default:water_source");

            // ---- Biome Registry ----
            _biomeRegistry = new BiomeRegistry();

            _biomeRegistry.Register("default:grassland", new BiomeDef
            {
                NodeTop = "default:dirt_with_grass",
                NodeFiller = "default:dirt",
                NodeStone = "default:stone",
                NodeWater = "default:water_source",
                DepthTop = 1,
                DepthFiller = 3,
                HeatPoint = 50f,
                HumidityPoint = 50f,
            });

            _biomeRegistry.Register("default:desert", new BiomeDef
            {
                NodeTop = "default:sand",
                NodeFiller = "default:sand",
                NodeStone = "default:sandstone",
                NodeWater = "default:water_source",
                DepthTop = 1,
                DepthFiller = 3,
                HeatPoint = 90f,
                HumidityPoint = 20f,
            });

            _biomeRegistry.Bake(registry);

            // ---- Ore Registry ----
            _oreRegistry = new OreRegistry();

            _oreRegistry.Register("default:stone_with_coal", new OreDef
            {
                Type = OreType.Scatter,
                NodeOre = "default:cobble",
                NodeWherein = new List<string> { "default:stone" },
                ClustScarcity = 8 * 8 * 8,
                ClustNumOres = 9,
                ClustSize = 3,
                YMin = -31007,
                YMax = 64,
            });

            _oreRegistry.Bake(registry, _biomeRegistry);

            // ---- Decoration Registry ----
            _decoRegistry = new DecorationRegistry();
            _decoRegistry.Bake(registry, _biomeRegistry);

            // ---- Features Pipeline ----
            _features = new MapgenFeatures(_worldSeed);
            _features.Initialize(
                _biomeRegistry,
                _oreRegistry,
                _decoRegistry,
                registry,
                contentStone,
                contentWater,
                contentWater, // river water (reuse water_source for now)
                MapgenV7Constants.DEFAULT_WATER_LEVEL);

            // ---- Mapgen ----
            var mapgen = new MapgenFlat(_worldSeed);
            mapgen.Initialize(contentStone, contentWater);
            mapgen.SetFeatures(_features);

            _worldGenProvider = new WorldGenChunkProvider(mapgen);

            Debug.Log(
                $"[Basalt] WorldGen initialized: MapgenFlat seed={_worldSeed}, " +
                $"stone={contentStone}, water={contentWater}, " +
                $"biomes={_biomeRegistry.Count}, ores={_oreRegistry.Count}, " +
                $"decos={_decoRegistry.Count}");
        }

        private void Update()
        {
            if (!_initialized || _playerTransform == null || !_nodeDefs.IsCreated)
            {
                return;
            }

            int3 playerChunkPos = CoordinateUtils.WorldToChunk(new int3(
                (int)math.floor(_playerTransform.position.x),
                (int)math.floor(_playerTransform.position.y),
                (int)math.floor(_playerTransform.position.z)
            ));

            if (!math.all(playerChunkPos == _currentCenter))
            {
                _currentCenter = playerChunkPos;
                _loadIndex = 0;
            }

            ProcessUnloads();
            ProcessLoads();

            // Advance meshing pipeline for all in-flight requests
            _meshApplier.Tick(_pool, _activeChunks, _nodeDefs);
        }

        /// <summary>
        /// Unloads chunks that are beyond drawDistance + hysteresis from the player.
        /// Cancels any in-flight meshing, destroys GameObjects, returns resources.
        /// </summary>
        private void ProcessUnloads()
        {
            int unloadRadiusSq = (_drawDistance + UNLOAD_HYSTERESIS)
                               * (_drawDistance + UNLOAD_HYSTERESIS);
            int unloaded = 0;

            NativeArray<int3> keys = _activeChunks.GetKeyArray(Allocator.Temp);

            for (int i = 0; i < keys.Length && unloaded < _maxUnloadsPerFrame; i++)
            {
                int3 diff = keys[i] - _currentCenter;
                int distSq = math.dot(diff, diff);

                if (distSq > unloadRadiusSq)
                {
                    int3 chunkPos = keys[i];

                    // Defer unload if chunk is part of an active MeshDataArray batch.
                    // The batch must complete before the mesh can be safely returned.
                    if (_meshApplier.HasBatchRequest(chunkPos))
                    {
                        continue;
                    }

                    // Cancel any in-flight meshing for this chunk
                    _meshApplier.Cancel(chunkPos);

                    // Destroy the chunk's GameObject and return mesh to pool
                    if (_chunkObjects.TryGetValue(chunkPos, out GameObject chunkObj))
                    {
                        MeshFilter mf = chunkObj.GetComponent<MeshFilter>();

                        if (mf != null && mf.sharedMesh != null)
                        {
                            _meshPool.Return(mf.sharedMesh);
                            mf.sharedMesh = null;
                        }

                        Destroy(chunkObj);
                        _chunkObjects.Remove(chunkPos);
                    }

                    // Return chunk data to pool
                    ChunkHandle handle = _activeChunks[chunkPos];
                    _pool.Return(handle);
                    _activeChunks.Remove(chunkPos);
                    unloaded++;
                }
            }

            keys.Dispose();

            // Evict cached mapchunk data for fully-unloaded mapchunks
            if (unloaded > 0)
            {
                _worldGenProvider?.EvictUnused(_activeChunks);
            }
        }

        /// <summary>
        /// Loads chunks from the spiral queue, closest to the player first.
        /// Creates a GameObject with MeshFilter + MeshRenderer and enqueues meshing.
        /// </summary>
        private void ProcessLoads()
        {
            int loaded = 0;

            while (_loadIndex < _spiralOffsets.Length && loaded < _maxLoadsPerFrame)
            {
                int3 chunkPos = _currentCenter + _spiralOffsets[_loadIndex];
                _loadIndex++;

                if (_activeChunks.ContainsKey(chunkPos))
                {
                    continue;
                }

                if (!CoordinateUtils.IsValidChunkPos(chunkPos))
                {
                    continue;
                }

                if (!_pool.TryRent(out ChunkHandle handle))
                {
                    break;
                }

                _activeChunks.Add(chunkPos, handle);

                // Fill chunk with worldgen data before meshing
                _worldGenProvider?.FillChunk(_pool, handle, chunkPos);

                // Create chunk GameObject with rendering components
                if (!_meshPool.TryRent(out Mesh mesh))
                {
                    _activeChunks.Remove(chunkPos);
                    _pool.Return(handle);

                    break;
                }

                GameObject chunkObj = CreateChunkObject(chunkPos, mesh);
                _chunkObjects[chunkPos] = chunkObj;

                // Enqueue meshing request (only if not already in flight)
                if (!_meshApplier.HasActiveRequest(chunkPos))
                {
                    _meshApplier.Enqueue(new MeshRequest
                    {
                        ChunkPosition = chunkPos,
                        Handle = handle,
                        TargetMesh = mesh,
                        ChunkObject = chunkObj,
                    });
                }

                loaded++;
            }
        }

        private GameObject CreateChunkObject(int3 chunkPos, Mesh mesh)
        {
            int3 worldMin = CoordinateUtils.ChunkToWorld(chunkPos);

            var go = new GameObject($"Chunk_{chunkPos.x}_{chunkPos.y}_{chunkPos.z}");
            go.transform.SetParent(transform, false);
            go.transform.position = new Vector3(worldMin.x, worldMin.y, worldMin.z);

            MeshFilter mf = go.AddComponent<MeshFilter>();
            mf.sharedMesh = mesh;

            MeshRenderer mr = go.AddComponent<MeshRenderer>();
            mr.sharedMaterial = _voxelMaterial;
            mr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.TwoSided;

            return go;
        }

        /// <summary>
        /// Computes spiral offsets sorted by distance from origin (closest first).
        /// Uses a spherical radius to avoid loading corner chunks.
        /// </summary>
        private static NativeArray<int3> ComputeSpiralOffsets(int radius)
        {
            int radiusSq = radius * radius;
            List<int3> offsets = new();

            for (int y = -radius; y <= radius; y++)
            {
                for (int z = -radius; z <= radius; z++)
                {
                    for (int x = -radius; x <= radius; x++)
                    {
                        int distSq = x * x + y * y + z * z;

                        if (distSq <= radiusSq)
                        {
                            offsets.Add(new int3(x, y, z));
                        }
                    }
                }
            }

            offsets.Sort((a, b) =>
            {
                int distA = a.x * a.x + a.y * a.y + a.z * a.z;
                int distB = b.x * b.x + b.y * b.y + b.z * b.z;

                return distA.CompareTo(distB);
            });

            NativeArray<int3> result = new NativeArray<int3>(offsets.Count, Allocator.Persistent);

            for (int i = 0; i < offsets.Count; i++)
            {
                result[i] = offsets[i];
            }

            return result;
        }

        private void OnDestroy()
        {
            _worldGenProvider?.Dispose();
            _features?.Dispose();
            _biomeRegistry?.Dispose();
            _oreRegistry?.Dispose();
            _decoRegistry?.Dispose();
            _meshApplier?.Dispose();

            if (_chunkObjects != null)
            {
                foreach (KeyValuePair<int3, GameObject> kvp in _chunkObjects)
                {
                    if (kvp.Value != null)
                    {
                        Destroy(kvp.Value);
                    }
                }

                _chunkObjects.Clear();
            }

            _meshPool?.Dispose();

            if (_activeChunks.IsCreated)
            {
                NativeArray<ChunkHandle> values = _activeChunks.GetValueArray(Allocator.Temp);

                for (int i = 0; i < values.Length; i++)
                {
                    _pool.Return(values[i]);
                }

                values.Dispose();
                _activeChunks.Dispose();
            }

            if (_spiralOffsets.IsCreated)
            {
                _spiralOffsets.Dispose();
            }

            _pool?.Dispose();
        }

        private void OnDrawGizmos()
        {
            if (!_initialized || !_activeChunks.IsCreated)
            {
                return;
            }

            Gizmos.color = new Color(0.2f, 0.8f, 0.2f, 0.15f);
            Vector3 chunkSize = Vector3.one * BasaltConstants.MAP_BLOCKSIZE;

            NativeArray<int3> keys = _activeChunks.GetKeyArray(Allocator.Temp);

            for (int i = 0; i < keys.Length; i++)
            {
                int3 worldMin = CoordinateUtils.ChunkToWorld(keys[i]);
                Vector3 center = new Vector3(worldMin.x, worldMin.y, worldMin.z) + chunkSize * 0.5f;
                Gizmos.DrawWireCube(center, chunkSize);
            }

            keys.Dispose();
        }
    }
}
