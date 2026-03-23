# Style Guide — Basalt

> **Version :** 1.0 · **Date :** Mars 2026  
> **Sources :** Unity C# Style Guide (Unity 6 edition), Microsoft Framework Design Guidelines, Burst/Jobs best practices  
> **Règle d'or :** La lisibilité prime sur la brièveté. Le code doit respirer.

---

## 1. Formatage Général

### Indentation & Espacement

```csharp
// 4 espaces, pas de tabs
// Allman style — accolades sur leur propre ligne, toujours
if (condition)
{
    DoSomething();
}

// TOUJOURS des accolades, même pour une seule ligne
// ✅ OUI
if (isActive)
{
    Activate();
}

// ❌ NON
if (isActive)
    Activate();
if (isActive) Activate();
```

### Espacement Vertical

Le code doit **respirer**. Chaque concept logique est séparé par une ligne vide.

```csharp
public struct ChunkManager : IDisposable
{
    private const int MAX_CHUNKS_PER_FRAME = 4;
    private const int POOL_INITIAL_SIZE = 512;

    private NativeHashMap<int3, ChunkHandle> _activeChunks;
    private NativeQueue<int3> _loadQueue;
    private NativeQueue<int3> _unloadQueue;

    public int ActiveCount => _activeChunks.Count();
    public int DrawDistance { get; set; }

    public void Initialize(int drawDistance) { ... }

    public void UpdateAroundPlayer(int3 playerChunkPos) { ... }

    private void EnqueueChunksInSpiral(int3 center) { ... }

    private void ProcessLoadQueue() { ... }

    public void Dispose() { ... }
}
```

L'ordre des membres et les lignes vides suffisent à structurer le fichier. Le code parle de lui-même.

**Règles de respiration :**

- **1 ligne vide** entre chaque champ public d'une struct (avec son `<summary>` au-dessus)
- **1 ligne vide** entre chaque méthode
- **1 ligne vide** entre les groupes logiques de champs (constantes, puis fields, puis propriétés)
- **1 ligne vide** avant un `return` en fin de méthode longue (5+ lignes)
- **1 ligne vide** avant et après un bloc `if`/`for`/`while` significatif dans un corps de méthode
- **0 ligne vide** entre un `<summary>` et le membre qu'il documente
- **Jamais** 2 lignes vides consécutives
- **Pas de commentaires de section** — l'ordre des membres et l'espacement suffisent

### Longueur des Lignes

- **Cible :** 100 caractères
- **Maximum :** 120 caractères
- **Au-delà :** couper avec indentation de continuation (+4 espaces)

```csharp
// Coupure de ligne propre — aligner sur le paramètre ou +4 espaces
NodeDefinition def = registry.RegisterNode(
    "default:stone",
    drawType: DrawType.Normal,
    tiles: new[] { "default_stone.png" },
    groups: new GroupSet(cracky: 3)
);
```

### Ordre des Membres dans un Fichier

```
1. Constantes et static readonly
2. Champs (privés d'abord, sérialisés ensuite)
3. Propriétés
4. Constructeurs / Initialisation
5. API publique (méthodes public)
6. Méthodes internes (internal)
7. Méthodes privées
8. Callbacks Unity (Awake, Start, Update — si MonoBehaviour)
9. Interface implementations (IDisposable, etc.)
10. Types imbriqués (nested structs, enums)
```

---

## 2. Nommage

### Convention Générale

| Élément | Convention | Préfixe | Exemple |
|---------|-----------|---------|---------|
| Namespace | PascalCase | `Basalt.` | `Basalt.Core`, `Basalt.Meshing` |
| Classe | PascalCase | — | `ChunkManager`, `NodeRegistry` |
| Struct | PascalCase | — | `MapNode`, `ChunkHandle` |
| Interface | PascalCase | `I` | `IChunkProvider`, `IMapGenerator` |
| Enum | PascalCase | — | `DrawType`, `ParamType` |
| Valeur d'enum | PascalCase | — | `DrawType.Normal`, `DrawType.Plantlike` |
| Méthode | PascalCase | — | `GetNode()`, `PropagateLight()` |
| Propriété | PascalCase | — | `DrawDistance`, `ActiveCount` |
| Événement | PascalCase | `On` | `OnChunkLoaded`, `OnNodeChanged` |
| Delegate | PascalCase | — | `ChunkLoadedHandler` |
| Champ public (struct) | PascalCase | — | `Content`, `Param1`, `Param2` |
| Champ privé | camelCase | `_` | `_chunkPool`, `_dirtyFlags` |
| Champ private static | camelCase | `s_` | `s_instance`, `s_sharedRegistry` |
| Paramètre | camelCase | — | `chunkPos`, `nodeIndex`, `drawDistance` |
| Variable locale | camelCase | — | `neighborCount`, `lightValue` |
| Constante | UPPER_SNAKE | — | `MAP_BLOCKSIZE`, `CONTENT_AIR` |
| Static readonly | PascalCase | — | `DefaultNoiseParams`, `EmptyChunk` |
| Job struct | PascalCase | `...Job` | `MeshingJob`, `LightBfsJob` |
| Type paramètre | `T` + Pascal | `T` | `TValue`, `TNode` |
| Fichier | = nom du type | — | `ChunkManager.cs` |
| Dossier | PascalCase | — | `WorldGen/`, `Mapgen/` |

### Nommage — Règles Impératives

```csharp
// ✅ Noms descriptifs — ne jamais abréger (sauf acronymes connus)
int neighborCount;           // pas "nbrCnt"
float3 worldPosition;        // pas "wPos"
ushort contentId;             // pas "cid"
NodeDefinition nodeDef;       // "Def" est un abrégé accepté (= Definition)

// ✅ Booléens : toujours une question qui se lit naturellement
bool isActive;
bool hasNeighbor;
bool canPropagate;
bool shouldUnload;

// ✅ Collections : nom pluriel
NativeArray<MapNode> nodes;
NativeList<int3> dirtyChunks;
Dictionary<string, ushort> nameToContentId;

// ✅ Méthodes : verbe d'action
void LoadChunk(int3 pos) { ... }
bool TryGetNode(int3 pos, out MapNode node) { ... }
int CalculateDigTime(NodeDefinition def, ToolDefinition tool) { ... }

// ✅ Pattern Try— pour les opérations qui peuvent échouer
bool TryGetChunk(int3 pos, out ChunkHandle handle) { ... }
bool TryParseFormspec(string spec, out FormspecLayout layout) { ... }

// ❌ Éviter les préfixes/suffixes de type
string strName;              // non → string name;
int iCount;                  // non → int count;
List<Node> nodeList;         // non → List<Node> nodes;
```

### Noms de Fichiers et Dossiers

```
Un fichier = un type public.
Le fichier porte exactement le nom du type.

✅ MapNode.cs         → contient struct MapNode
✅ ChunkManager.cs    → contient class ChunkManager
✅ DrawType.cs        → contient enum DrawType
✅ MeshingJob.cs      → contient struct MeshingJob

❌ Helpers.cs          → trop vague
❌ Utils.cs            → non, créer des fichiers spécifiques (CoordinateUtils.cs, BitUtils.cs)
❌ Types.cs            → non, un fichier par type
```

---

## 3. Documentation

### Principes

- **Toute API publique est documentée.** Pas d'exception.
- **Chaque champ public d'une struct a son `<summary>`.** Avec une ligne vide entre chaque champ — le code respire.
- **Les types privés/internes** sont documentés si leur rôle n'est pas évident.
- **Le `<summary>` est une seule phrase** qui commence par un verbe à la 3e personne.
- **Les commentaires en ligne** expliquent le *pourquoi*, jamais le *quoi*.
- **Pas de commentaires de fermeture** (`} // end if`) — le code doit être assez court pour que ce soit inutile.
- **Langue :** Anglais pour tout le code et les doc comments.

### Modèles de Documentation XML

```csharp
/// <summary>
/// Represents a single voxel node with content type and parameters.
/// Layout mirrors Luanti's MapNode: content_t (u16) + param1 (u8) + param2 (u8).
/// </summary>
/// <remarks>
/// Packed into a single uint32 for Burst compatibility.
/// Bit layout: [31..16] content, [15..8] param1, [7..0] param2.
/// </remarks>
public readonly struct MapNode
{
    /// <summary>Raw packed data containing content, param1, and param2.</summary>
    public readonly uint Packed;

    /// <summary>Gets the node type identifier (0-65535).</summary>
    public ushort Content => (ushort)(Packed >> 16);

    /// <summary>Gets the lighting data (bits 0-3 night, bits 4-7 day).</summary>
    public byte Param1 => (byte)((Packed >> 8) & 0xFF);

    /// <summary>Gets the rotation or auxiliary data.</summary>
    public byte Param2 => (byte)(Packed & 0xFF);

    public MapNode(ushort content, byte param1 = 0, byte param2 = 0)
    {
        Packed = ((uint)content << 16) | ((uint)param1 << 8) | param2;
    }
}

/// <summary>
/// Converts a world-space integer position to its containing chunk position.
/// </summary>
/// <param name="worldPos">The world position in node coordinates.</param>
/// <returns>The chunk position containing the given world position.</returns>
/// <remarks>
/// Uses arithmetic shift (not division) to handle negative coordinates correctly.
/// Luanti equivalent: <c>getContainerPos()</c> in <c>src/util/numeric.h</c>.
/// </remarks>
public static int3 WorldToChunk(int3 worldPos)
{
    ...
}

/// <summary>
/// Gets the number of currently active (loaded) chunks.
/// </summary>
public int ActiveCount => _activeChunks.Count();

/// <summary>
/// Defines how a node is visually rendered in the world.
/// Mirrors Luanti's <c>NodeDrawType</c> in <c>src/nodedef.h</c>.
/// </summary>
public enum DrawType
{
    /// <summary>A standard cubic block with 6 faces.</summary>
    Normal = 0,

    /// <summary>A flowing or source liquid (water, lava).</summary>
    Liquid = 3,

    /// <summary>A flat crossed-plane (flowers, grass).</summary>
    Plantlike = 7,

    ...
}

/// <summary>
/// Burst-compiled job that generates the mesh for a single chunk
/// using binary greedy meshing.
/// </summary>
/// <remarks>
/// Reads from a 3x3x3 neighborhood of chunk data (center + 26 neighbors)
/// to handle face culling at chunk boundaries.
/// Output is written to a <see cref="Mesh.MeshData"/> via the writable mesh API.
/// </remarks>
[BurstCompile]
public struct MeshingJob : IJobParallelFor
{
    /// <summary>Packed node data for the center chunk (4096 elements).</summary>
    [ReadOnly] public NativeArray<uint> CenterNodes;

    /// <summary>Node definitions indexed by content_id.</summary>
    [ReadOnly] public NativeArray<NodeDefinition> NodeDefs;

    /// <summary>Writable mesh output for vertex and index data.</summary>
    public Mesh.MeshData OutputMesh;

    public void Execute(int faceIndex) { ... }
}
```

### Commentaires en Ligne

```csharp
// ✅ Expliquer le POURQUOI, pas le QUOI
// Arithmetic shift handles negative coords correctly (C# division truncates toward zero)
int chunkX = worldX >> 4;

// ✅ Référencer Luanti quand on reproduit un comportement
// Luanti: voxelalgorithms.cpp:220 — sunlight propagates down until blocked
if (nodeDef.SunlightPropagates)
{
    lightValue = LIGHT_SUN;
}

// ✅ Avertissements de performance
// PERF: This runs on the main thread — keep under 1ms budget
Mesh.ApplyAndDisposeWritableMeshData(meshDataArray, meshes);

// ✅ TODO avec contexte
// TODO(Epic-5): Replace hardcoded groups with Lua-defined group registry
var groups = new GroupSet(cracky: 3);

// ❌ Commentaire inutile — le code est déjà clair
// Increment the counter
counter++;

// ❌ Commentaire qui paraphrase le code
// Set the position to the player position
position = playerPosition;
```

---

## 4. Règles Burst / Jobs / Performance

### Structs Blittables

```csharp
// ✅ Tout dans un job Burst doit être blittable
// ✅ Chaque champ documenté — le code respire
public struct NodeDefinition
{
    /// <summary>Unique identifier for this node type.</summary>
    public ushort ContentId;

    /// <summary>Visual rendering mode. Enum backed by int, blittable.</summary>
    public DrawType Type;

    /// <summary>Light emitted by this node (0-15). 0 = no light.</summary>
    public byte LightSource;

    /// <summary>How param1 is interpreted. Typically light data.</summary>
    public byte Param1Type;

    /// <summary>How param2 is interpreted (facedir, wallmounted, color).</summary>
    public byte Param2Type;

    /// <summary>Packed boolean flags: walkable, pointable, diggable, etc.</summary>
    public byte Flags;
}

// ❌ INTERDIT dans un contexte Burst
public struct BadNode
{
    public string Name;            // managed → pas blittable
    public List<int> Neighbors;    // managed → pas blittable
    public bool IsActive;          // bool OK dans struct, INTERDIT dans NativeContainer
}
```

### NativeContainers — Règles

```csharp
// ✅ Toujours spécifier l'Allocator explicitement
var nodes = new NativeArray<uint>(4096, Allocator.Persistent);
var queue = new NativeQueue<int3>(Allocator.TempJob);

// ✅ [ReadOnly] et [WriteOnly] sur les champs de job
[BurstCompile]
public struct LightBfsJob : IJob
{
    [ReadOnly] public NativeArray<uint> Nodes;
    [WriteOnly] public NativeArray<byte> LightMap;
    public NativeQueue<int3> Queue;  // read+write → pas d'attribut
    
    public void Execute() { ... }
}

// ✅ Dispose systématique — jamais oublier
public void Dispose()
{
    if (_activeChunks.IsCreated) _activeChunks.Dispose();
    if (_loadQueue.IsCreated) _loadQueue.Dispose();
}

// ❌ JAMAIS de NativeContainer dans un NativeContainer
NativeArray<NativeArray<int>> nested;  // INTERDIT — pas blittable

// ✅ Alternative : un gros NativeArray flat + indexation manuelle
NativeArray<uint> allChunkData;  // NODES_PER_BLOCK * maxChunks éléments
int GetNodeIndex(int chunkIndex, int localIndex) 
    => chunkIndex * NODES_PER_BLOCK + localIndex;
```

### Jobs — Pattern

```csharp
// ✅ Pattern recommandé pour un job de meshing
public JobHandle ScheduleMeshingJob(int chunkIndex, JobHandle dependency)
{
    var job = new MeshingJob
    {
        CenterNodes = GetChunkData(chunkIndex),
        NodeDefs = _nodeRegistry.RuntimeDefs,
        OutputVertices = _vertexBuffer,
        OutputTriangles = _triangleBuffer,
        OutputVertexCount = _vertexCountOutput,
    };
    
    return job.Schedule(dependency);
}

// ✅ Ne JAMAIS appeler .Complete() dans la même frame sauf nécessité absolue
// Scheduler → compléter frame suivante ou via dépendance de job
_meshHandle = ScheduleMeshingJob(idx, _worldGenHandle);

// Plus tard dans le frame ou frame suivante :
_meshHandle.Complete();
ApplyMeshData();
```

### Directives Burst

```csharp
// ✅ [BurstCompile] sur le struct du job, pas sur les méthodes individuelles
[BurstCompile(CompileSynchronously = false)]
public struct WorldGenNoiseJob : IJobParallelFor { ... }

// ✅ Utiliser Unity.Mathematics partout dans les jobs
// float3 au lieu de Vector3, int3 au lieu de Vector3Int, math.sqrt au lieu de Mathf.Sqrt
float3 offset = new float3(x, y, z) * noiseScale;
float value = noise.cnoise(offset);

// ✅ Éviter les branches dans les boucles chaudes — utiliser math.select
int lightVal = math.select(0, LIGHT_SUN, nodeDef.SunlightPropagates);

// ❌ INTERDIT dans un contexte Burst
try { } catch { }          // pas de try/catch
string.Format(...)          // pas de string
Debug.Log(...)              // pas de managed calls
new MyClass()               // pas de classes
virtualMethod()             // pas de virtual/interface dispatch
```

---

## 5. Classes et Structs

### Quand Utiliser Struct vs Class

```
STRUCT quand :
  - Données petites (< 64 octets idéalement)
  - Pas d'héritage
  - Sémantique de valeur (copie)
  - Utilisé dans des NativeContainers ou des jobs
  - Exemples : MapNode, ChunkHandle, NodeDefinition, ItemStack, int3

CLASS quand :
  - Logique complexe avec état mutable
  - Lifetime management (IDisposable avec des NativeContainers)
  - Singleton ou manager
  - MonoBehaviour (obligatoirement class)
  - Exemples : ChunkManager, NodeRegistry, ModManager, FormspecParser
```

### Struct Readonly

```csharp
// ✅ Si la struct est immutable, marquer readonly
public readonly struct MapNode
{
    /// <summary>Raw packed data containing content, param1, and param2.</summary>
    public readonly uint Packed;

    /// <summary>Gets the node type identifier (0-65535).</summary>
    public ushort Content => (ushort)(Packed >> 16);

    /// <summary>Gets the lighting data (bits 0-3 night, bits 4-7 day).</summary>
    public byte Param1 => (byte)((Packed >> 8) & 0xFF);

    /// <summary>Gets the rotation or auxiliary data.</summary>
    public byte Param2 => (byte)(Packed & 0xFF);

    public MapNode(ushort content, byte param1 = 0, byte param2 = 0)
    {
        Packed = ((uint)content << 16) | ((uint)param1 << 8) | param2;
    }
}
```

### Modificateurs d'Accès

```csharp
// ✅ Toujours explicite — ne jamais omettre le modificateur
private int _count;           // pas juste "int _count;"
public void Initialize() { }  // pas juste "void Initialize();"
internal static int s_poolSize;

// ✅ Ordre des modificateurs : access, static, override/virtual/abstract, readonly
public static readonly int MaxChunks = 1024;
private static int s_instanceCount;
protected virtual void OnChunkLoaded(int3 pos) { }
```

---

## 6. Patterns Spécifiques Basalt

### MonoBehaviour (Client & Server uniquement)

```csharp
/// <summary>
/// Manages the lifecycle of voxel chunks around the player camera.
/// Responsible for loading, unloading, and prioritizing chunk updates.
/// </summary>
public class ChunkRenderer : MonoBehaviour
{
    [Header("Chunk Settings")]
    [SerializeField] private int _drawDistance = 8;
    [SerializeField] private int _maxLoadsPerFrame = 4;

    [Header("References")]
    [SerializeField] private Material _voxelMaterial;

    private ChunkManager _chunkManager;
    private int3 _lastPlayerChunkPos;

    private void Awake()
    {
        _chunkManager = new ChunkManager(_drawDistance);
    }

    private void Update()
    {
        int3 currentChunkPos = CoordinateUtils.WorldToChunk(GetPlayerPosition());

        if (!currentChunkPos.Equals(_lastPlayerChunkPos))
        {
            _chunkManager.UpdateAroundPlayer(currentChunkPos);
            _lastPlayerChunkPos = currentChunkPos;
        }

        _chunkManager.ProcessQueues(_maxLoadsPerFrame);
    }

    private void OnDestroy()
    {
        _chunkManager?.Dispose();
    }
}
```

### ScriptableObject (Définitions éditeur)

```csharp
/// <summary>
/// Editor-time definition of a node type. Baked into runtime NodeDefinition at startup.
/// </summary>
[CreateAssetMenu(fileName = "NewNode", menuName = "Basalt/Node Definition")]
public class NodeDefinitionAsset : ScriptableObject
{
    [Header("Identity")]
    public string ModName = "default";
    public string NodeName = "stone";
    
    [Header("Rendering")]
    public DrawType DrawType = DrawType.Normal;
    public Texture2D[] Tiles;
    
    [Header("Properties")]
    [Range(0, 15)] public int LightSource;
    public bool SunlightPropagates;
    public bool Walkable = true;
    
    /// <summary>
    /// Gets the full registered name in <c>modname:nodename</c> format.
    /// </summary>
    public string FullName => $"{ModName}:{NodeName}";
}
```

### Référencement Luanti

Quand le code reproduit un comportement spécifique de Luanti, toujours référencer le fichier source :

```csharp
/// <summary>
/// Calculates dig time using Luanti's formula.
/// </summary>
/// <remarks>
/// Port of <c>getDigParams()</c> from <c>References/luanti/src/tool.cpp:130</c>.
/// Formula: time = hardness * scale / (toolSpeed * toolDigLevel)
/// </remarks>
public static float CalculateDigTime(
    NodeDefinition node,
    ToolDefinition tool,
    int groupRating)
{
    // Luanti: tool.cpp:142 — base dig time calculation
    float hardness = node.Groups.GetRating(tool.GroupCaps.GroupName);
    float speed = tool.GroupCaps.GetSpeed(groupRating);
    
    return hardness / speed;
}
```

---

## 7. Gestion des Erreurs

```csharp
// ✅ Assertions dans les builds debug pour les invariants
[Conditional("UNITY_ASSERTIONS")]
public static void AssertValidChunkPos(int3 pos)
{
    Assert.IsTrue(
        math.all(math.abs(pos) <= MAX_MAP_GENERATION_LIMIT),
        $"Chunk position {pos} exceeds world limits"
    );
}

// ✅ Pattern Guard Clause — retour rapide en haut de méthode
public NodeDefinition GetNodeDef(ushort contentId)
{
    if (contentId >= _definitions.Length)
    {
        return _definitions[CONTENT_UNKNOWN];
    }
    
    return _definitions[contentId];
}

// ✅ Exceptions avec message utile pour les erreurs de configuration (pas dans le hot path)
public void RegisterNode(string name, NodeDefinition def)
{
    if (string.IsNullOrEmpty(name))
    {
        throw new ArgumentException("Node name cannot be null or empty.", nameof(name));
    }
    
    if (!name.Contains(':'))
    {
        throw new ArgumentException(
            $"Node name '{name}' must be in 'modname:nodename' format.",
            nameof(name)
        );
    }
    
    ...
}

// ❌ JAMAIS d'exception dans le hot path — utiliser des valeurs de retour
// ❌ JAMAIS de try/catch dans du code Burst
```

---

## 8. Résumé — Aide-Mémoire

```
✅ TOUJOURS
├── Accolades Allman sur leur propre ligne
├── Modificateurs d'accès explicites (jamais implicite)
├── XML doc <summary> sur toute API publique, y compris chaque champ de struct
├── 1 ligne vide entre chaque champ documenté (le code respire)
├── NativeContainers + Burst dans le hot path
├── Dispose() pour tout NativeContainer
├── readonly struct si immutable
├── Référencer le fichier Luanti source quand on porte du code
├── Noms descriptifs sans abréviation
├── Une ligne vide entre les méthodes
└── Un type par fichier

❌ JAMAIS
├── string, List<T>, Dictionary, LINQ, lambda dans un job Burst
├── try/catch dans du code Burst
├── new MyClass() dans le hot path
├── bool dans un NativeContainer (utiliser byte)
├── MonoBehaviour dans Core/WorldGen/Meshing/Lighting
├── NativeContainer imbriqué dans un NativeContainer
├── .Complete() immédiat sur un job (sauf nécessité documentée)
├── 2 lignes vides consécutives
├── Commentaires qui paraphrasent le code
└── Abréviation non standard (nbrCnt, wPos, cid)
```
