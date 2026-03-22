# Roadmap — Basalt Voxel Engine

> **Auteur :** Clayton · **Date :** Mars 2026 · **Statut :** Draft  
> **Input :** [Epics](./epics.md) · [Architecture](./architecture.md)

---

## Vue d'Ensemble

```
 M1    M2    M3    M4    M5    M6    M7    M8    M9    M10   M11   M12   M13   M14   M15+
 ├─────┼─────┼─────┼─────┼─────┼─────┼─────┼─────┼─────┼─────┼─────┼─────┼─────┼─────┤
 │◀──── Phase 1 ────────▶│◀──── Phase 2 ───────▶│◀────── Phase 3 ──────────▶│◀─ P4 ──▶│P5…
 │   Silent World    🎯M1│    Lua Lives     🎯M2│       Playable        🎯M3│Connected│Parity
 │   Epics 1-4           │    Epic 5             │   Epics 6,7,8,10         │  Epic 9 │
 └─────────────────────────────────────────────────────────────────────────────────────┘
```

---

## Phase 1 — Le Moteur Silencieux (Mois 1-4)

**Objectif :** Un monde voxel procédural rendu à l'écran avec éclairage. Aucun gameplay — juste un terrain généré, texturé, éclairé.

| Période | Epic | Livrables | Résultat visible |
|---------|------|-----------|------------------|
| M1 Sem 1-2 | Epic 1 | Structure MapBlock, coordonnées, NodeDef registre | Cubes placeholder à l'écran |
| M1 Sem 3-4 | Epic 1 | ChunkManager, pool, chargement autour du joueur | Monde infini de cubes |
| M2 Sem 1-3 | Epic 2 | Face culling, greedy meshing Burst, MeshDataArray | Terrain wireframe optimisé |
| M2 Sem 4 | Epic 2 | AO vertex, texture arrays, matériaux URP | Terrain texturé avec AO |
| M3 Sem 1-4 | Epic 3 | Bruit Perlin, mapgen v7, biomes, minerais, grottes | Terrain réaliste généré |
| M4 Sem 1-2 | Epic 4 | Propagation BFS dual-channel, smooth lighting | Éclairage complet jour/nuit |
| M4 Sem 3-4 | Epic 4 | Cycle jour/nuit, skybox, camera fly-through | **🎯 MILESTONE M1** |

### 🎯 Milestone M1 — "Silent World"

> Un monde voxel infini, texturé, éclairé, avec génération procédurale v7 et cycle jour/nuit. Démo fly-through jouable. Cible performance : 60 FPS @ 12 chunks.

**Critères de validation :**
- [ ] Monde infini qui charge/décharge sans fuite mémoire
- [ ] Greedy meshing Burst < 200μs/chunk
- [ ] Terrain v7 avec biomes, grottes, minerais
- [ ] Éclairage dual smooth fonctionnel
- [ ] 0 GC.Alloc dans le hot path

---

## Phase 2 — Le Cerveau Lua (Mois 5-7)

**Objectif :** Le moteur de scripting est opérationnel. Les nœuds, items et recettes sont définis en Lua. Point de bascule du projet.

| Période | Epic | Livrables | Résultat visible |
|---------|------|-----------|------------------|
| M5 Sem 1-2 | Epic 5 | Runtime MoonSharp, sandbox, ModManager, deps | Mods chargés au boot |
| M5 Sem 3-4 | Epic 5 | API `register_node()`, `register_craftitem()`, `register_tool()` | Blocs définis en Lua |
| M6 Sem 1-2 | Epic 5 | Callbacks (globalstep, on_generated, on_dig, on_place) | Événements Lua fonctionnels |
| M6 Sem 3-4 | Epic 5 | `register_craft()`, ABMs, LBMs | Crafting + monde dynamique |
| M7 Sem 1-2 | Epic 5 | API monde (get_node, set_node, find_nodes_in_area) | Manipulation monde via Lua |
| M7 Sem 3-4 | Epic 5 | Tests avec sous-ensemble Minetest Game | **🎯 MILESTONE M2** |

### 🎯 Milestone M2 — "Lua Lives"

> Les définitions de nœuds de Minetest Game sont chargées depuis les scripts Lua originaux. Le monde affiche les vrais blocs avec les vraies textures.

**Critères de validation :**
- [ ] ModManager charge 30+ mods avec résolution de dépendances
- [ ] 80+ nœuds du mod `default` enregistrés et rendus
- [ ] Callbacks globalstep et on_generated fonctionnels
- [ ] ABMs actifs (herbe qui pousse)
- [ ] Recettes de craft chargées depuis Lua

---

## Phase 3 — Le Gameplay (Mois 8-11)

**Objectif :** Un jeu jouable en solo. Le joueur peut se déplacer, casser/poser, crafter, voir des mobs, interagir avec les formspecs.

| Période | Epic | Livrables | Résultat visible |
|---------|------|-----------|------------------|
| M8 Sem 1-4 | Epic 6 | Joueur (mouvement, collision), dig/place, inventaire, crafting | Gameplay de base |
| M9 Sem 1-3 | Epic 7 | Entités ECS, bridge Lua, ItemEntity, pickup | Items + entités dans le monde |
| M9 S4 - M10 S2 | Epic 8 | Parser formspec, 20 éléments core, HUD | Coffres, fourneaux, menus |
| M10 Sem 3-4 | Epic 10 | SQLite backend, save/load, autosave, metadata | Monde persistant |
| M11 Sem 1-4 | Intégration | Test Minetest Game complet, bugfix, polish | **🎯 MILESTONE M3** |

### 🎯 Milestone M3 — "Playable"

> Un joueur peut survivre dans Basalt : miner, crafter des outils, construire, stocker dans des coffres, fondre dans des fourneaux. Le monde se sauvegarde.

**Critères de validation :**
- [ ] Boucle gameplay complète (dig → collect → craft → place)
- [ ] Inventaire joueur + hotbar fonctionnels
- [ ] Coffres et fourneaux via formspecs
- [ ] Entités items au sol + pickup
- [ ] Save/load monde SQLite sans perte de données
- [ ] Minetest Game jouable en solo

---

## Phase 4 — Le Multijoueur (Mois 12-14)

**Objectif :** Le mode multijoueur est fonctionnel. 2-10 joueurs sur un serveur.

| Période | Epic | Livrables | Résultat visible |
|---------|------|-----------|------------------|
| M12 Sem 1-4 | Epic 9 | Protocole UDP, 3 canaux, authentification, sérialisation | Connexion client/serveur |
| M13 Sem 1-4 | Epic 9 | Sync monde, sync entités, sync inventaires, chat | Multijoueur fonctionnel |
| M14 Sem 1-4 | Polish | Optimisations réseau, latence, tests charge, bugfix | **🎯 MILESTONE M4** |

### 🎯 Milestone M4 — "Connected"

> 2-10 joueurs peuvent jouer ensemble sur un serveur Basalt.

**Critères de validation :**
- [ ] Connexion stable UDP avec auth
- [ ] Chunks streamés au client sans corruption
- [ ] Modifications d'un joueur visibles par les autres < 200ms
- [ ] Chat fonctionnel
- [ ] Mods Lua exécutés côté serveur uniquement

---

## Phase 5 — Parité & Polish (Mois 15-18+)

**Objectif :** Rapprocher Basalt de la parité complète avec Luanti.

| Période | Focus | Livrables |
|---------|-------|-----------|
| M15-16 | Mapgens | v5, v6, Carpathian, Valleys, Fractal, Singlenode (8/8) |
| M16-17 | Formspecs | Éléments avancés : model, tabheader, scrollbar containers (40/40) |
| M17 | VoxelManip | API VoxelManip complète pour mapgen Lua custom |
| M17-18 | Shaders | Dynamic shadows, bloom, volumetric light, tonemapping |
| M18+ | Écosystème | ContentDB browser intégré, support Android |

### 🎯 Milestone M5 — "Parity"

> Basalt atteint 90%+ de compatibilité API Lua Luanti. La majorité des mods ContentDB fonctionnent sans modification.
