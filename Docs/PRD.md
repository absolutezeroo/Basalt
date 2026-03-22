# PRD — Basalt Voxel Engine

> **Auteur :** Clayton · **Date :** Mars 2026 · **Statut :** Draft  
> **Input :** [Product Brief](./product-brief.md)

---

## 1. Objectif Produit

Reproduire à l'identique le moteur Luanti (ex-Minetest) dans Unity 6.4 en exploitant Burst/Jobs, ECS, URP et MoonSharp pour atteindre ou dépasser les performances du moteur C++ original.

## 2. Personas

| Persona | Description | Besoin principal |
|---------|-------------|------------------|
| **Le Joueur** | Joue à Minetest Game ou à des jeux voxel custom | Monde infini, fluide, avec contenu varié |
| **Le Moddeur** | Développe des mods Lua pour Luanti | API Lua compatible, documentation, debugging |
| **Le Game Creator** | Crée un jeu complet sur la plateforme | Moteur stable, API riche, performances prévisibles |
| **Le Dev Contributeur** | Contribue au moteur open-source | Code C# propre, architecture modulaire, tests |

## 3. Exigences Fonctionnelles

### FR-01 · Monde Voxel Infini
Le monde est composé de MapBlocks de 16×16×16 nœuds, chargés/déchargés dynamiquement autour du joueur. La portée du monde s'étend à ±31 000 blocs par axe, conforme à Luanti.

### FR-02 · Génération Procédurale
Au minimum 2 mapgens fonctionnels (v7 et flat) avec support des biomes, minerais, décorations, grottes et arbres. Les 6 autres mapgens (v5, v6, Carpathian, Valleys, Fractal, Singlenode) sont post-MVP.

### FR-03 · Éclairage Dual Jour/Nuit
Deux canaux de lumière (4 bits chacun dans param1) : lumière du jour et lumière artificielle. Propagation BFS. Smooth lighting par interpolation vertex. Cycle jour/nuit visuel.

### FR-04 · Rendu Voxel Performant
Pipeline de meshing multi-threadé (Burst + Jobs + MeshDataArray). Face culling entre nœuds opaques. Greedy meshing. Ambient occlusion par vertex. Texture arrays. Target : 60 FPS @ 12 chunks draw distance sur GPU milieu de gamme.

### FR-05 · Système de Modding Lua
Runtime MoonSharp sandboxé compatible Lua 5.2. ModManager avec résolution de dépendances par tri topologique. API `core.register_*()` couvrant les nœuds, items, outils, entités, recettes, biomes, minerais, décorations, chatcommands.

### FR-06 · Callbacks Moteur
Callbacks Lua pour tous les événements moteur : `globalstep`, `on_generated`, `on_dignode`, `on_placenode`, `on_joinplayer`, `on_leaveplayer`, `on_player_receive_fields`, etc.

### FR-07 · Active Block Modifiers (ABMs) et LBMs
ABMs exécutés périodiquement sur les blocs actifs (ex : herbe qui pousse). LBMs exécutés au chargement d'un bloc (ex : migration de nœuds obsolètes).

### FR-08 · Gameplay Joueur
Mouvement AABB-based (marcher, sauter, nager, fly). Système dig/place avec dig-time calculé selon groupes/outils. Inventaire joueur (hotbar 8 + grille 32). Crafting (shaped, shapeless, cooking, fuel).

### FR-09 · Entités
Entités scriptées en Lua (mobs, objets lâchés) via `core.register_entity()`. Propriétés visuelles, physiques et callbacks (`on_step`, `on_punch`, `on_activate`). ItemEntity builtin pour le pickup.

### FR-10 · Interface Formspec
Parser de formspecs (chaînes de caractères → UI). Minimum 20 types d'éléments : `size`, `list`, `button`, `field`, `label`, `image`, `dropdown`, `checkbox`, `textarea`, `scrollbar`, `tabheader`, `model`, `box`, `item_image`, `image_button`, `textlist`, `table`, `tooltip`, `style`, `real_coordinates`.

### FR-11 · Réseau & Multijoueur
Protocole UDP custom sur Unity Transport avec 3 canaux (fiable ordonné, fiable non-ordonné, non-fiable). Architecture client/serveur (même en solo). Synchronisation monde, entités, inventaires. Authentification. Chat.

### FR-12 · Stockage Persistant
Backend SQLite. Sérialisation des chunks (nœuds + node metadata + timers). Compression zlib. Autosave configurable. Schema compatible import Luanti (objectif stretch).

## 4. Exigences Non-Fonctionnelles

| ID | Catégorie | Exigence | Métrique |
|----|-----------|----------|----------|
| NFR-01 | Performance | 60 FPS stable avec 12 chunks draw distance | Profiler Unity, 95th percentile |
| NFR-02 | Performance | Meshing d'un chunk < 200μs (Burst) | Profiler, moyenne sur 1000 chunks |
| NFR-03 | Performance | Zéro allocation GC dans le hot path (meshing, lighting, tick) | GC.Alloc = 0 dans Profiler |
| NFR-04 | Mémoire | < 2 Go RAM pour 500 chunks chargés | Profiler mémoire Unity |
| NFR-05 | Startup | Chargement mods < 5s pour Minetest Game (~30 mods) | Chrono au boot |
| NFR-06 | Réseau | Latence perception < 200ms pour dig/place en multijoueur | Mesure round-trip |
| NFR-07 | Save | Autosave < 50ms pour 100 chunks dirty (async) | Profiler I/O |
| NFR-08 | Compatibilité | ≥ 80% de l'API Lua documentée de Luanti | Couverture par tests automatisés |
| NFR-09 | Plateforme | Windows, Linux, macOS (MVP). Android (post-MVP) | Build + smoke tests |
| NFR-10 | Maintenabilité | Assemblies séparées, 0 dépendance circulaire | asmdef graph Unity |

## 5. Contraintes Techniques

- Unity 6.4 (6000.4) — version cible figée
- C# uniquement (pas de plugins natifs) pour la portabilité IL2CPP/WebGL
- MoonSharp comme runtime Lua (pas de LuaJIT/NLua — pas cross-platform)
- URP uniquement (pas de HDRP — trop lourd pour du voxel)
- Pas de Netcode for GameObjects (inadapté au streaming voxel)

## 6. Hors Périmètre (Explicite)

- Compatibilité binaire avec les sauvegardes Luanti existantes (objectif stretch)
- Compatibilité réseau avec les serveurs Luanti natifs (objectif stretch)
- VR/AR
- Ray tracing (post v1.0)
- Éditeur de mods intégré
