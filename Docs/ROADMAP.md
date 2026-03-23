# Roadmap — Basalt Voxel Engine

> **Auteur :** Clayton · **Date :** Mars 2026 · **Statut :** Mis à jour post-audit  
> **Input :** [Epics](./EPICS.MD) · [Architecture](./ARCHITECTURE.MD) · [Audit](./AUDIT-MISSING-FEATURES.MD)

---

## Vue d'Ensemble

```
 M1    M2    M3    M4    M5    M6    M7    M8    M9   M10   M11   M12   M13   M14  M15+
 ├─────┼─────┼─────┼─────┼─────┼─────┼─────┼─────┼─────┼─────┼─────┼─────┼─────┼─────┤
 │◀─── Phase 1 ──────────▶│◀──── Phase 2 ──────────────▶│◀────── Phase 3 ─────────▶│P4…
 │   Silent World     🎯M1│       Lua Lives         🎯M2│      Playable         🎯M3│
 │   Epics 1,2,3,4        │       Epic 5                 │  Epics 2.6,6,7,8,10      │
 └─────────────────────────────────────────────────────────────────────────────────────┘
```

**Durée totale estimée : 15-20 mois** (développeur solo, 60 stories, post-audit)

---

## Phase 1 — Le Moteur Silencieux (Mois 1-5)

**Objectif :** Un monde voxel procédural avec arbres, grottes, éclairage, cycle jour/nuit. Aucun gameplay — démo fly-through.

| Période | Epic | Livrables | Résultat visible |
|---------|------|-----------|------------------|
| M1 S1-2 | Epic 1 | MapBlock, coordonnées, NodeDef registre | Cubes placeholder |
| M1 S3-4 | Epic 1 | ChunkManager, pool, chargement spirale | Monde infini de cubes |
| M2 S1-3 | Epic 2 | Greedy meshing Burst, AO vertex | Terrain wireframe optimisé |
| M2 S4 | Epic 2 | Texture arrays, shader HLSL, pipeline MeshDataArray | Terrain texturé avec AO |
| M3 S1-2 | Epic 3 | Bruit Perlin, mapgen v7 | Terrain réaliste |
| M3 S3-4 | Epic 3 | Mapgen flat, biomes, minerais, décorations | Diversité terrain |
| M4 S1-2 | Epic 3 | Pipeline worldgen asynchrone, grottes | Terrain fluide + grottes |
| M4 S3-4 | Epic 3 | Arbres (schematics .mts), dust/liquids | Monde vivant avec arbres |
| M5 S1-2 | Epic 4 | Propagation BFS dual, smooth lighting | Éclairage complet |
| M5 S3-4 | Epic 4 | Cycle jour/nuit, re-propagation dynamique | **🎯 MILESTONE M1** |

### 🎯 Milestone M1 — "Silent World"

> Monde voxel infini avec terrain v7, arbres, grottes, biomes, éclairage dual jour/nuit avec re-propagation dynamique. Démo fly-through jouable.

**Critères de validation :**
- [ ] Monde infini qui charge/décharge sans fuite mémoire
- [ ] Greedy meshing Burst < 200μs/chunk
- [ ] Terrain v7 avec biomes, grottes, minerais, arbres
- [ ] Éclairage dual smooth + re-propagation au changement de nœud
- [ ] Pipeline async worldgen → meshing → GPU upload avec budget/frame
- [ ] 60 FPS @ 12 chunks draw distance
- [ ] 0 GC.Alloc dans le hot path

---

## Phase 2 — Le Cerveau Lua (Mois 6-9)

**Objectif :** Le moteur de scripting est opérationnel. Nœuds, items, outils, recettes, callbacks, timers, métadonnées, privilèges définis en Lua.

| Période | Epic | Livrables | Résultat visible |
|---------|------|-----------|------------------|
| M6 S1-2 | Epic 5 | Runtime MoonSharp, sandbox, ModManager | Mods chargés au boot |
| M6 S3-4 | Epic 5 | register_node/item/tool, register_craft | Blocs et items en Lua |
| M7 S1-2 | Epic 5 | Callbacks (globalstep, on_dig, on_generated, on_join) | Événements Lua |
| M7 S3-4 | Epic 5 | ABMs, LBMs, API monde (get_node, set_node) | Monde dynamique |
| M8 S1-2 | Epic 5 | NodeMetaRef, InvRef, ItemStack, NodeTimerRef | Coffres et fourneaux en Lua |
| M8 S3-4 | Epic 5 | PlayerRef, privileges, chatcommands | Joueur accessible en Lua |
| M9 S1-2 | Epic 5 | Utilitaires (core.after, get_modpath, chat, serialize) | Mods complets |
| M9 S3-4 | Epic 5 + Epic 2.6 | Tests Minetest Game + drawtypes non-cubiques | **🎯 MILESTONE M2** |

### 🎯 Milestone M2 — "Lua Lives"

> Les mods Minetest Game sont chargés depuis les scripts Lua originaux. Le monde affiche les vrais blocs, fleurs, arbres, verre, eau.

**Critères de validation :**
- [ ] ModManager charge 30+ mods avec résolution de dépendances
- [ ] 80+ nœuds du mod `default` enregistrés et rendus
- [ ] Drawtypes plantlike, liquid, glasslike, allfaces, nodebox fonctionnels
- [ ] NodeMetaRef et InvRef fonctionnels (coffres, fourneaux)
- [ ] NodeTimerRef fonctionnel (fourneau qui cuit)
- [ ] ABMs actifs (herbe qui pousse)
- [ ] Privilege system et chatcommands fonctionnels
- [ ] Recettes de craft chargées depuis Lua

---

## Phase 3 — Le Gameplay (Mois 10-14)

**Objectif :** Un jeu jouable en solo complet. Mouvement, dig/place, inventaire, crafting, HP, mobs, formspecs, save/load.

| Période | Epic | Livrables | Résultat visible |
|---------|------|-----------|------------------|
| M10 S1-4 | Epic 6 | Joueur (mouvement, collision), dig/place, inventaire | Gameplay de base |
| M11 S1-2 | Epic 6 | Crafting, node timers engine, tool wear | Crafting fonctionnel |
| M11 S3-4 | Epic 6 | Health, fall damage, drowning | Joueur mortel |
| M12 S1-3 | Epic 7 | Entités ECS, bridge Lua, ItemEntity, physique | Mobs et items au sol |
| M12 S4 - M13 S1 | Epic 7 | ObjectRef API, static objects | Mobs persistants |
| M13 S2-4 | Epic 8 | Parser formspec 20 éléments, HUD, chat | Interfaces de jeu |
| M14 S1-2 | Epic 10 | SQLite complet, player data, node timers persistence | Monde persistant |
| M14 S3-4 | Intégration | Test Minetest Game complet, bugfix | **🎯 MILESTONE M3** |

### 🎯 Milestone M3 — "Playable"

> Un joueur peut survivre : miner, crafter, construire, stocker, cuire, mourir, respawn. Le monde et le joueur persistent.

**Critères de validation :**
- [ ] Boucle gameplay complète (dig → collect → craft → place)
- [ ] Tool wear fonctionnel (outils se cassent)
- [ ] HP + fall damage + drowning
- [ ] Fourneaux fonctionnels (NodeTimer + formspec)
- [ ] Coffres fonctionnels (NodeMetaRef + InvRef + formspec)
- [ ] Entités items au sol + pickup + stack merging
- [ ] Mobs scriptés en Lua avec persistence (static objects)
- [ ] Save/load monde complet (terrain + metadata + timers + entities + player)
- [ ] Minetest Game jouable en solo

---

## Phase 4 — Le Multijoueur (Mois 15-17)

**Objectif :** Multijoueur fonctionnel. 2-10 joueurs sur un serveur.

| Période | Epic | Livrables | Résultat visible |
|---------|------|-----------|------------------|
| M15 S1-4 | Epic 9 | Protocole UDP, auth, auth database | Connexion client/serveur |
| M16 S1-4 | Epic 9 | Sync monde, sync entités/inventaires, médias | Multijoueur fonctionnel |
| M17 S1-4 | Polish | Optimisations réseau, latence, bugfix | **🎯 MILESTONE M4** |

### 🎯 Milestone M4 — "Connected"

> 2-10 joueurs ensemble sur un serveur Basalt.

**Critères de validation :**
- [ ] Connexion stable UDP avec auth SRP + persistence
- [ ] Chunks streamés au client sans corruption
- [ ] Modifications d'un joueur visibles par les autres < 200ms
- [ ] Chat et chatcommands multijoueur
- [ ] Mods Lua exécutés côté serveur uniquement

---

## Phase 5 — Parité & Polish (Mois 18+)

**Objectif :** Rapprocher Basalt de la parité complète avec Luanti.

| Période | Focus | Livrables |
|---------|-------|-----------|
| M18-19 | Mapgens | v5, v6, Carpathian, Valleys, Fractal, Singlenode (8/8) |
| M19-20 | Formspecs | Éléments avancés : model, tabheader, scrollbar containers (40/40) |
| M20 | Audio | core.sound_play(), son positionnel, chargement OGG depuis mods |
| M20-21 | Particules | core.add_particle(), core.add_particlespawner() |
| M21 | VoxelManip | API VoxelManip complète pour mapgen Lua custom |
| M21-22 | Drawtypes | NDT_MESH, NDT_GLASSLIKE_FRAMED, NDT_CONNECTED, NDT_FENCELIKE |
| M22 | Texture modifiers | [combine:, [colorize:, [crack:, [transform: |
| M22-23 | Shaders | Dynamic shadows, bloom, volumetric light |
| M23+ | Écosystème | ContentDB browser intégré, support Android |

### 🎯 Milestone M5 — "Parity"

> Basalt atteint 90%+ de compatibilité API Lua. La majorité des mods ContentDB fonctionnent.
