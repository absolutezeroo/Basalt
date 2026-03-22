# Architecture — Basalt Voxel Engine

> **Auteur :** Clayton · **Date :** Mars 2026 · **Statut :** Draft  
> **Input :** [PRD](./prd.md)

---

## Décisions Architecturales (ADR)

### ADR-001 · Render Pipeline → URP

**Contexte :** Unity propose Built-in, URP et HDRP.  
**Décision :** URP (Universal Render Pipeline).  
**Justification :** Meilleur ratio performance/compatibilité. HDRP trop lourd pour du voxel. GPU Resident Drawer réduit les draw calls de ~50%. Cross-platform (mobile inclus post-MVP).

### ADR-002 · Données Voxel → NativeArray\<uint\> bitpacké

**Contexte :** Chaque nœud Luanti = content_t (u16) + param1 (u8) + param2 (u8) = 4 octets.  
**Décision :** `NativeArray<uint>` avec bitpacking (16+8+8 dans un uint32).  
**Justification :** Compatible Burst/Jobs, layout identique à Luanti, zéro allocation GC, accès vectorisable.

### ADR-003 · Taille des Chunks → 16×16×16 (parité Luanti)

**Contexte :** Les projets Unity voxel utilisent souvent 32³. Luanti utilise 16³.  
**Décision :** 16×16×16 nœuds par MapBlock.  
**Justification :** Parité exacte avec le format MapBlock Luanti. Permet la compatibilité save-game future. Plus granulaire pour le culling. 4096 nœuds × 4 octets = 16 Ko par chunk.

### ADR-004 · Meshing → Binary Greedy Meshing + Burst

**Contexte :** Le meshing per-face de Luanti est simple mais non optimal.  
**Décision :** Binary greedy meshing dans `IJobParallelFor` Burst-compilé.  
**Justification :** ~100μs/chunk vs ~6ms sans Burst. `MeshDataArray` pour écriture off-main-thread. Pipeline double-passe (count → write).

### ADR-005 · Scripting Lua → MoonSharp

**Contexte :** Options Lua en C# : MoonSharp, NLua, xLua, Lua-CSharp.  
**Décision :** MoonSharp (C# pur, Lua 5.2 compatible à 99%).  
**Justification :** Cross-platform (IL2CPP, WebGL, iOS). Sandboxing natif via `CoreModules`. Coroutines préemptives (`AutoYieldCounter`). Production-proven (Off Grid, uLua). Fork Enhanced disponible pour réduire allocations.

### ADR-006 · Réseau → Unity Transport Layer (custom)

**Contexte :** Netcode for GameObjects est limité à 1300 octets par NetworkVariable.  
**Décision :** Protocole UDP custom sur Unity Transport Layer.  
**Justification :** Streaming de chunks entiers (16 Ko+ compressés). 3 canaux comme Luanti. Contrôle total du protocole de sérialisation.

### ADR-007 · Stockage → SQLite via SQLite4Unity3d

**Contexte :** Luanti utilise SQLite par défaut.  
**Décision :** SQLite4Unity3d (wrapper C# de SQLite).  
**Justification :** Même backend que Luanti. Schema compatible pour import/export futur. Robuste, ACID, un fichier par monde.

### ADR-008 · Éclairage → BFS flood-fill + vertex colors

**Contexte :** Luanti utilise un éclairage logiciel (pas de lightmaps GPU).  
**Décision :** Propagation BFS dans des Jobs Burst. Deux canaux dans vertex colors (R=day, G=night, B=AO).  
**Justification :** Parité exacte avec Luanti. Shader URP custom pour blending temps-dépendant. Le calcul est off-main-thread.

### ADR-009 · Entités → ECS (Entities 1.4+ natif en 6.4)

**Contexte :** Les entités Luanti (mobs, items) sont des ServerActiveObjects en C++ avec logique Lua.  
**Décision :** ECS Unity (package noyau en 6.4) pour le stockage/rendu, avec bridge vers MoonSharp pour la logique Lua.  
**Justification :** `Entities.Graphics` pour le rendu massif. `IComponentData` pour les propriétés blittables. La logique Lua reste dans des callbacks invoqués depuis les systèmes ECS.

### ADR-010 · GUI → UI Toolkit

**Contexte :** Les formspecs Luanti sont des chaînes textuelles parsées en UI.  
**Décision :** Parser C# → éléments UI Toolkit.  
**Justification :** UI Toolkit est le système UI moderne d'Unity (vs UGUI deprecated). Data binding natif. UXML pour les templates. Better batching.

### ADR-011 · Assets Mods → Addressables + chargement runtime

**Contexte :** Les mods Luanti fournissent textures (PNG), modèles (OBJ), sons (OGG).  
**Décision :** `Texture2D.LoadImage()` pour les textures, importers runtime pour OBJ/glTF, `AudioClip` pour les sons. Addressables pour les assets du moteur de base.  
**Justification :** Chargement dynamique depuis le filesystem. Pas besoin d'AssetBundles pour les mods utilisateur.

### ADR-012 · Async I/O → Awaitable (Unity 6 natif)

**Contexte :** Le save/load et le réseau nécessitent du background threading.  
**Décision :** `Awaitable.BackgroundThreadAsync()` pour les opérations I/O, `Awaitable.MainThreadAsync()` pour l'upload GPU.  
**Justification :** API native Unity 6, pas besoin de `Task.Run()` ni de SynchronizationContext custom. Pattern propre et lisible.

---

## Modèle de Threading

```
┌─────────────────────────────────────────────────────────────┐
│  MAIN THREAD                                                 │
│  - Orchestration chunks (load/unload decisions)              │
│  - Mesh.ApplyAndDisposeWritableMeshData()                    │
│  - Rendu URP + GPU Resident Drawer                           │
│  - Input                                                     │
│  - Exécution callbacks Lua (MoonSharp single-threaded)       │
├─────────────────────────────────────────────────────────────┤
│  JOB WORKERS (Burst-compiled, N = CPU cores)                 │
│  - Génération bruit Perlin (IJobParallelFor)                 │
│  - Meshing chunks (MeshDataArray write)                      │
│  - Propagation lumière BFS                                   │
│  - Génération collision meshes                               │
├─────────────────────────────────────────────────────────────┤
│  BACKGROUND THREADS (Awaitable)                              │
│  - Save/Load SQLite                                          │
│  - Réseau send/receive (Unity Transport)                     │
│  - Sérialisation + compression zlib                          │
├─────────────────────────────────────────────────────────────┤
│  SERVER THREAD (si mode hôte ou dédié)                       │
│  - Game loop serveur (60 ticks/s)                            │
│  - ABMs / LBMs                                               │
│  - Simulation physique entités                               │
│  - Callbacks Lua serveur (globalstep, on_generated, etc.)    │
└─────────────────────────────────────────────────────────────┘
```

---

## Structure du Projet Unity

```
Basalt/
├── Assets/
│   ├── Basalt.Core/              # Données voxel, MapBlock, NodeDef, constantes
│   │   └── Basalt.Core.asmdef    # deps: Burst, Collections, Mathematics
│   ├── Basalt.WorldGen/          # 8 mapgens, bruit Perlin, biomes, minerais
│   │   └── Basalt.WorldGen.asmdef # deps: Core, Burst, Jobs
│   ├── Basalt.Meshing/           # Pipeline meshing, greedy mesh, AO
│   │   └── Basalt.Meshing.asmdef  # deps: Core, Burst, Jobs
│   ├── Basalt.Lighting/          # Propagation BFS, smooth lighting
│   │   └── Basalt.Lighting.asmdef # deps: Core, Burst, Jobs
│   ├── Basalt.Network/           # Protocole UDP, sérialisation, channels
│   │   └── Basalt.Network.asmdef  # deps: Core, Unity Transport
│   ├── Basalt.Storage/           # SQLite backend, sérialisation monde
│   │   └── Basalt.Storage.asmdef  # deps: Core, SQLite4Unity3d
│   ├── Basalt.Scripting/         # Runtime MoonSharp, API Lua, ModManager
│   │   └── Basalt.Scripting.asmdef # deps: Core, MoonSharp
│   ├── Basalt.Client/            # Rendu URP, chunks visuels, shaders, audio
│   │   └── Basalt.Client.asmdef   # deps: Core, Meshing, Lighting, URP
│   ├── Basalt.Server/            # Game loop serveur, ABMs, entités
│   │   └── Basalt.Server.asmdef   # deps: Core, Scripting, WorldGen
│   ├── Basalt.GUI/               # Parser formspec, UI Toolkit, HUD
│   │   └── Basalt.GUI.asmdef      # deps: Core, UI Toolkit
│   ├── Basalt.Entities/          # ECS mobs/items, EntityDef, SAO bridge
│   │   └── Basalt.Entities.asmdef # deps: Core, Entities
│   ├── Shaders/                  # URP shaders (voxel, sky, water, foliage)
│   ├── Resources/                # Assets par défaut du moteur
│   └── Plugins/                  # MoonSharp DLL, SQLite native
├── mods/                         # Dossier mods utilisateur (hors Assets/)
│   └── default/                  # Minetest Game 'default' mod (test)
├── docs/                         # Ce dossier
├── Packages/                     # Unity packages (URP, Burst, etc.)
└── ProjectSettings/
```

### Graphe de Dépendances des Assemblies

```
                    ┌──────────┐
                    │  Core    │
                    └────┬─────┘
           ┌─────────┬──┴──┬─────────┬──────────┐
           │         │     │         │          │
      ┌────▼──┐ ┌───▼──┐ ┌▼───────┐ ┌▼────────┐ ┌▼────────┐
      │Meshing│ │Light │ │WorldGen│ │Storage  │ │Network  │
      └───┬───┘ └──┬───┘ └───┬────┘ └─────────┘ └─────────┘
          │        │         │
      ┌───▼────────▼───┐  ┌─▼──────────┐
      │   Client       │  │  Server     │
      └───────┬────────┘  └──┬──────────┘
              │              │
         ┌────▼──┐     ┌────▼─────┐
         │  GUI  │     │Scripting │
         └───────┘     └────┬─────┘
                            │
                       ┌────▼─────┐
                       │ Entities │
                       └──────────┘
```
