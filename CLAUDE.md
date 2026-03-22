# CLAUDE.md — Basalt

> Ce fichier est lu automatiquement par Claude Code à chaque session.
> Il définit le contexte, les conventions et les règles du projet.

## Identité du Projet

**Basalt** est un moteur de jeu voxel Unity 6.4 (6000.4) reproduisant à l'identique Luanti (ex-Minetest). Le projet suit la méthode BMAD. Toute la planification est dans `docs/`.

- **Moteur cible :** Unity 6.4 (C# uniquement, pas de plugins natifs)
- **Render Pipeline :** URP (Universal Render Pipeline) — jamais HDRP ni Built-in
- **Objectif :** Compatibilité fonctionnelle avec l'API Lua de Luanti (~80% MVP)
- **Licence :** À définir (probablement LGPL-2.1 comme Luanti ou MIT)

## Documentation de Référence

```
Docs/
├── product-brief.md       # Vision produit
├── prd.md                 # 12 FR, 10 NFR, contraintes
├── architecture.md        # 12 ADR, threading, assemblies
├── epics.md               # Vue d'ensemble 10 epics
├── roadmap.md             # 5 phases, 5 milestones
├── risks.md               # 8 risques identifiés
└── epics/                 # Stories détaillées par epic
```

Consulter `Docs/architecture.md` avant toute décision structurelle.
Consulter `Docs/prd.md` pour vérifier si une feature est dans le périmètre.

## Architecture (Résumé)

### Assemblies

Le projet est découpé en assemblies indépendantes (`.asmdef`). **Ne jamais créer de dépendance circulaire.**

```
Basalt.Core         → Burst, Collections, Mathematics
Basalt.WorldGen     → Core, Burst, Jobs
Basalt.Meshing      → Core, Burst, Jobs
Basalt.Lighting     → Core, Burst, Jobs
Basalt.Network      → Core, Unity Transport
Basalt.Storage      → Core, SQLite4Unity3d
Basalt.Scripting    → Core, MoonSharp
Basalt.Client       → Core, Meshing, Lighting, URP
Basalt.Server       → Core, Scripting, WorldGen
Basalt.GUI          → Core, UI Toolkit
Basalt.Entities     → Core, Entities
```

### Threading Model

| Thread | Responsabilités |
|--------|----------------|
| **Main** | Orchestration chunks, `Mesh.ApplyAndDisposeWritableMeshData()`, rendu URP, input, callbacks Lua |
| **Job Workers** (Burst) | Bruit Perlin, meshing, propagation lumière BFS, collision meshes |
| **Background** (Awaitable) | Save/Load SQLite, réseau, sérialisation/compression zlib |
| **Server** | Game loop (60 ticks/s), ABMs, simulation entités, callbacks Lua serveur |

### Données Voxel Fondamentales

Chaque nœud = **4 octets** dans un `NativeArray<uint>` bitpacké :

```csharp
// Bitpacking identique à Luanti
// [31..16] content_t (u16)  —  type de nœud
// [15..8]  param1    (u8)   —  éclairage (bits 0-3 night, bits 4-7 day)
// [7..0]   param2    (u8)   —  rotation/couleur/niveau liquide

public static uint Pack(ushort content, byte param1, byte param2)
    => ((uint)content << 16) | ((uint)param1 << 8) | param2;

public static void Unpack(uint packed, out ushort content, out byte param1, out byte param2)
{
    content = (ushort)(packed >> 16);
    param1  = (byte)((packed >> 8) & 0xFF);
    param2  = (byte)(packed & 0xFF);
}
```

**Constantes Luanti à respecter :**

```csharp
public const int MAP_BLOCKSIZE = 16;              // Nœuds par axe par chunk
public const int NODES_PER_BLOCK = 4096;           // 16³
public const int MAX_MAP_GENERATION_LIMIT = 31007; // Portée monde par axe
public const ushort CONTENT_AIR = 126;
public const ushort CONTENT_IGNORE = 127;
public const ushort CONTENT_UNKNOWN = 125;
public const ushort CONTENT_MAX = 65535;           // u16 max
```

**Indexation nœud dans un chunk :**

```csharp
// Index linéaire = x + y * 16 + z * 256  (x varie le plus vite)
public static int NodeIndex(int x, int y, int z) => x + (y << 4) + (z << 8);
```

## Conventions de Code

### Nommage C#

| Élément | Convention | Exemple |
|---------|-----------|---------|
| Namespace | `Basalt.{Assembly}` | `Basalt.Core`, `Basalt.Meshing` |
| Classe / Struct | PascalCase | `ChunkManager`, `NodeDefinition` |
| Interface | I + PascalCase | `IChunkProvider`, `IMapGenerator` |
| Méthode publique | PascalCase | `GetNode()`, `SetNode()` |
| Méthode privée | PascalCase | `PropagateLight()` |
| Champ privé | _camelCase | `_chunkPool`, `_dirtyFlags` |
| Champ public (struct) | PascalCase | `Content`, `Param1` |
| Constante | UPPER_SNAKE | `MAP_BLOCKSIZE`, `CONTENT_AIR` |
| Variable locale | camelCase | `chunkPos`, `nodeIndex` |
| Job struct | PascalCase + Job | `MeshingJob`, `LightPropagationJob` |
| Propriété | PascalCase | `DrawDistance { get; set; }` |
| Événement | On + PascalCase | `OnChunkLoaded`, `OnNodeChanged` |
| Enum | PascalCase | `DrawType.Normal`, `ParamType.Light` |
| Fichier | = nom du type | `ChunkManager.cs`, `MeshingJob.cs` |

### Règles Strictes

1. **Zéro allocation GC dans le hot path.** Pas de `new`, `string`, `List<T>`, `Dictionary`, LINQ, lambda, boxing dans :
   - Meshing jobs
   - Light propagation jobs
   - WorldGen jobs
   - Tick serveur (boucle ABM)
   - ChunkManager update

2. **NativeContainers partout dans le hot path.** Utiliser `NativeArray<T>`, `NativeHashMap<K,V>`, `NativeQueue<T>`, `NativeList<T>` avec `Allocator.Persistent` ou `Allocator.TempJob`.

3. **Burst-compatible = blittable.** Toute struct utilisée dans un job Burst doit être 100% blittable :
   - Pas de `string` → utiliser `FixedString32Bytes` ou un `int` ID
   - Pas de `class` → uniquement `struct`
   - Pas de `bool` dans les NativeContainers → utiliser `byte` (0/1)
   - Pas de référence managée → uniquement types valeur

4. **Jobs schedulés, jamais `.Complete()` immédiat.** Scheduler les jobs et les compléter frame suivante ou via dépendances. Seule exception : `Mesh.ApplyAndDisposeWritableMeshData()` qui doit être main-thread.

5. **Pas de MonoBehaviour dans Core/WorldGen/Meshing/Lighting.** Ces assemblies sont purement data-oriented. Les MonoBehaviours vivent dans `Basalt.Client` et `Basalt.Server`.

6. **UI Toolkit uniquement** pour toute l'interface. Pas d'UGUI, pas d'OnGUI, pas d'IMGUI en production.

7. **Awaitable pour l'async.** Utiliser `Awaitable.BackgroundThreadAsync()` et `Awaitable.MainThreadAsync()`. Pas de `Task.Run()`, pas de `Thread` manuel.

### Conventions Luanti à Respecter

Le but est la **parité fonctionnelle** avec Luanti. Quand un comportement est ambigu, le comportement de Luanti fait foi.

- Les noms de nœuds suivent le format `modname:nodename` (ex: `default:stone`)
- Les groupes sont des `Dictionary<string, int>` (ex: `cracky=3`, `oddly_breakable_by_hand=1`)
- Les positions monde sont en **entiers** (int3), pas en float. Y = haut.
- Le cycle jour/nuit dure 20 minutes (72s par phase de jour = 1000 unités de time-of-day)
- `param1` encode TOUJOURS l'éclairage (sauf si `paramtype = "none"`)
- `param2` encode la rotation via `facedir` (0-23) ou `wallmounted` (0-5)
- Les formspecs utilisent des coordonnées en unités de grille (1 unité ≈ 1 slot d'inventaire)

### API Lua — Priorités d'Implémentation

**T1 — Utilisé par Minetest Game (implémenter en premier) :**
- `core.register_node()`, `register_craftitem()`, `register_tool()`
- `core.register_craft()` (shaped, shapeless, cooking, fuel)
- `core.register_abm()`, `register_lbm()`
- `core.register_on_dignode()`, `register_on_placenode()`
- `core.register_globalstep()`
- `core.register_on_generated()`
- `core.register_chatcommand()`
- `core.get_node()`, `set_node()`, `remove_node()`
- `core.find_nodes_in_area()`
- `core.get_meta()` → `NodeMetaRef`
- `core.get_inventory()` → `InvRef`
- `core.chat_send_player()`, `chat_send_all()`
- `minetest.get_player_by_name()`
- `ItemStack` methods

**T2 — Top 50 mods (implémenter après T1 solide) :**
- `core.register_entity()`
- `core.register_decoration()`, `register_ore()`, `register_biome()`
- `core.register_on_joinplayer()`, `register_on_leaveplayer()`
- `core.add_entity()`, `core.add_item()`
- `VoxelManip` API
- `core.after()` (timer)
- `core.register_on_player_receive_fields()`
- `core.show_formspec()`

**T3 — Reste (post-MVP uniquement) :**
- Rollback, bans, HTTP API, async environment, schematic placement
- `core.raycast()`, pathfinder, `spawn_tree()`
- Minimap, camera API, skybox API avancée

## Structure des Dossiers Unity

```
Assets/
├── Basalt.Core/
│   ├── Data/                  # Structs blittables (MapNode, NodeDef, etc.)
│   ├── Constants/             # Constantes moteur
│   ├── Coordinates/           # Conversions position monde/chunk/nœud
│   ├── Registry/              # Registre de nœuds, items, outils
│   └── Basalt.Core.asmdef
├── Basalt.WorldGen/
│   ├── Noise/                 # Perlin noise Burst-compatible
│   ├── Mapgen/                # MapgenV7, MapgenFlat, etc.
│   ├── Features/              # Biomes, ores, decorations, caves, trees
│   └── Basalt.WorldGen.asmdef
├── Basalt.Meshing/
│   ├── Jobs/                  # MeshingJob, GreedyMeshJob
│   ├── AmbientOcclusion/      # Calcul AO par vertex
│   └── Basalt.Meshing.asmdef
├── Basalt.Lighting/
│   ├── Jobs/                  # LightPropagationJob
│   ├── SmoothLighting/        # Interpolation vertex
│   └── Basalt.Lighting.asmdef
├── Basalt.Scripting/
│   ├── Runtime/               # MoonSharp setup, sandbox config
│   ├── API/                   # Bindings C#↔Lua (RegisterNode, etc.)
│   ├── ModManager/            # Chargement, dépendances, exécution
│   └── Basalt.Scripting.asmdef
├── Basalt.Client/
│   ├── Chunk/                 # ChunkManager, ChunkRenderer
│   ├── Player/                # PlayerController, Camera
│   ├── Rendering/             # Texture arrays, materials
│   └── Basalt.Client.asmdef
├── Basalt.Server/
│   ├── GameLoop/              # ServerTick, ABMRunner
│   ├── Objects/               # ServerActiveObject bridge
│   └── Basalt.Server.asmdef
├── Basalt.GUI/
│   ├── Formspec/              # FormspecParser, element renderers
│   ├── HUD/                   # Hotbar, health, crosshair
│   └── Basalt.GUI.asmdef
├── Basalt.Entities/
│   ├── Components/            # IComponentData structs
│   ├── Systems/               # ECS systems
│   └── Basalt.Entities.asmdef
├── Basalt.Network/
│   ├── Protocol/              # Packet definitions, channels
│   ├── Transport/             # Unity Transport wrapper
│   └── Basalt.Network.asmdef
├── Basalt.Storage/
│   ├── SQLite/                # Database wrapper
│   ├── Serialization/         # Chunk serialization, zlib
│   └── Basalt.Storage.asmdef
├── Shaders/
│   ├── Voxel.shadergraph      # Shader principal voxel (texture array + dual light + AO)
│   ├── Sky.shadergraph         # Skybox procédural jour/nuit
│   └── Water.shadergraph       # Eau animée
└── Plugins/
    ├── MoonSharp/              # DLL MoonSharp
    └── SQLite/                 # SQLite4Unity3d
```

## Packages Unity Requis

```json
{
  "com.unity.burst": "1.8.x",
  "com.unity.collections": "2.x",
  "com.unity.mathematics": "1.3.x",
  "com.unity.entities": "1.4.x",
  "com.unity.entities.graphics": "1.4.x",
  "com.unity.render-pipelines.universal": "17.x",
  "com.unity.transport": "2.x",
  "com.unity.ui": "2.x",
  "com.unity.addressables": "2.x"
}
```

## Commandes Utiles

```bash
# Lancer les tests
dotnet test Basalt.Core.Tests

# Profiler un build
# Dans Unity : Window > Analysis > Profiler > attach to player

# Vérifier zéro GC dans le hot path
# Profiler > CPU > GC.Alloc column = 0 pour Meshing/Lighting/WorldGen

# Référence Luanti — l'API Lua complète
# https://api.luanti.org/
# ou localement : References/Luanti/doc/lua_api.md (12 336 lignes)
```

## Quand Tu Hésites

1. **"Est-ce dans le périmètre ?"** → Lis `docs/prd.md` section 6 (Hors Périmètre)
2. **"Quelle techno utiliser ?"** → Lis `docs/architecture.md` (12 ADR documentées)
3. **"Comment Luanti fait ça ?"** → Cherche dans le code source Luanti cloné (`luanti/src/`)
4. **"C'est T1, T2 ou T3 ?"** → Vérifie si Minetest Game l'utilise. Si oui → T1.
5. **"Faut-il un MonoBehaviour ?"** → Non, sauf dans `Basalt.Client` ou `Basalt.Server`.
6. **"Faut-il allouer sur le heap ?"** → Non. Si tu penses que oui, trouve une alternative avec NativeContainers.
7. **"Est-ce que ça doit être Burst-compatible ?"** → Si c'est dans le hot path (meshing, lighting, worldgen, tick), oui obligatoirement.
