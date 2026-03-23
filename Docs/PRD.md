# PRD — Basalt Voxel Engine

> **Auteur :** Clayton · **Date :** Mars 2026 · **Statut :** Mis à jour post-audit  
> **Input :** [Product Brief](./PRODUCT-BRIEF.MD) · [Audit](./AUDIT-MISSING-FEATURES.MD)

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
Le monde est composé de MapBlocks de 16×16×16 nœuds, chargés/déchargés dynamiquement autour du joueur. Portée ±31 000 blocs par axe.

### FR-02 · Génération Procédurale
MVP : mapgens v7 et flat avec biomes, minerais (scatter, blob), décorations (simple, schematic), grottes (noise intersection, randomwalk, caverns), arbres via schematics `.mts`, dust top nodes, liquid update. Post-MVP : v5, v6, Carpathian, Valleys, Fractal, Singlenode.

### FR-03 · Éclairage Dual Jour/Nuit
Deux canaux (4 bits chacun dans param1). Propagation BFS initiale + re-propagation dynamique au changement de nœud (BFS inverse + forward). Smooth lighting. Cycle jour/nuit.

### FR-04 · Rendu Voxel Performant
Greedy meshing Burst + MeshDataArray. Face culling via Solidness. AO par vertex. Texture arrays 16×16. Shader HLSL URP. Drawtypes MVP : normal, plantlike, liquid, flowingliquid, glasslike, allfaces, torchlike, nodebox. Target : 60 FPS @ 12 chunks.

### FR-05 · Système de Modding Lua
Runtime MoonSharp sandboxé. ModManager avec résolution de dépendances. API couvrant : register_node/craftitem/tool, register_entity, register_craft (shaped/shapeless/cooking/fuel), register_biome/ore/decoration, register_chatcommand, register_privilege.

### FR-06 · Callbacks Moteur
globalstep, on_generated, on_dignode, on_placenode, on_joinplayer, on_leaveplayer, on_player_receive_fields, on_player_hpchange.

### FR-07 · ABMs et LBMs
ABMs périodiques sur blocs actifs. LBMs au chargement de blocs.

### FR-08 · Gameplay Joueur
Mouvement AABB (marcher, sauter, nager, fly). Dig/place avec dig-time par groupes/outils. Inventaire (hotbar 8 + grille 32). Crafting. Tool wear (0-65535). Health (HP 20, fall damage, drowning). Node timers (fourneaux, machines). Death/respawn.

### FR-09 · Entités
Entités Lua via register_entity(). ObjectRef API complète (get_pos, set_velocity, set_hp, punch, remove, set_properties, set_animation). ItemEntity builtin. Physique basique (gravité, AABB). Static objects persistence dans les MapBlocks.

### FR-10 · Interface Formspec
Parser formspec (20 éléments MVP). HUD système (hotbar, health, breath, crosshair, custom HUD elements). Chat + chatcommands.

### FR-11 · Réseau & Multijoueur
Protocole UDP custom sur Unity Transport, 3 canaux. Architecture client/serveur. Authentification SRP avec database séparée. Synchronisation monde, entités, inventaires. Transfert de médias. Chat.

### FR-12 · Stockage Persistant
SQLite backend. Sérialisation MapBlock complète (flags, nœuds, node metadata, static objects, node timers, name-id mapping). Compression zlib. Player data persistence (position, HP, breath, inventaire, look direction). Auth database. World config (world.mt).

### FR-13 · APIs Lua Utilitaires (MVP)
NodeMetaRef (get/set string/int/float, get_inventory, mark_as_private). InvRef (add_item, remove_item, get_list, get_size). ItemStack methods (get_name, get_count, get_wear, take_item). PlayerRef (get_pos, set_pos, get_hp, set_hp, get_inventory, get_player_control). NodeTimerRef (start, stop, get_timeout, get_elapsed). core.after(). core.get_modpath(). core.get_worldpath(). core.chat_send_player/all(). core.log(). core.serialize/deserialize(). Privilege system.

### FR-14 · Audio (post-MVP)
core.sound_play(). Son positionnel. Chargement OGG depuis mods.

### FR-15 · Particules (post-MVP)
core.add_particle(). core.add_particlespawner().

## 4. Exigences Non-Fonctionnelles

| ID | Catégorie | Exigence | Métrique |
|----|-----------|----------|----------|
| NFR-01 | Performance | 60 FPS stable avec 12 chunks draw distance | 95th percentile |
| NFR-02 | Performance | Meshing chunk < 200μs (Burst) | Moyenne sur 1000 chunks |
| NFR-03 | Performance | Zéro GC dans le hot path | GC.Alloc = 0 |
| NFR-04 | Mémoire | < 2 Go RAM pour 500 chunks | Profiler mémoire |
| NFR-05 | Startup | Chargement mods < 5s pour Minetest Game | Chrono au boot |
| NFR-06 | Réseau | Latence < 200ms pour dig/place multi | Round-trip |
| NFR-07 | Save | Autosave < 50ms pour 100 chunks dirty (async) | Profiler I/O |
| NFR-08 | Compatibilité | ≥ 80% de l'API Lua documentée | Tests automatisés |
| NFR-09 | Plateforme | Windows, Linux, macOS (MVP). Android post-MVP | Build + smoke tests |
| NFR-10 | Maintenabilité | Assemblies séparées, 0 dépendance circulaire | asmdef graph |

## 5. Contraintes Techniques

- Unity 6.4 (6000.4) — version cible figée
- C# uniquement (pas de plugins natifs) pour la portabilité IL2CPP/WebGL
- MoonSharp comme runtime Lua
- URP uniquement (pas de HDRP)
- Pas de Netcode for GameObjects (inadapté au streaming voxel)

## 6. Hors Périmètre (Explicite)

- Compatibilité binaire avec les sauvegardes Luanti existantes (objectif stretch)
- Compatibilité réseau avec les serveurs Luanti natifs (objectif stretch)
- VR/AR
- Ray tracing (post v1.0)
- Éditeur de mods intégré
- Texture modifiers ([combine:, [colorize:, etc.) — post-MVP
- VoxelManipulator API — post-MVP
- Rollback system — post-MVP
- Detached inventories — post-MVP
