# Epics — Basalt Voxel Engine

> **Auteur :** Clayton · **Date :** Mars 2026 · **Statut :** Draft  
> **Input :** [PRD](./prd.md) · [Architecture](./architecture.md)

---

## Vue d'Ensemble

10 epics ordonnés par valeur utilisateur croissante. Chaque epic est **autonome** et livre une fonctionnalité complète. L'ordre respecte les dépendances techniques naturelles.

| # | Epic | Objectif | Durée | Priorité | PRD |
|---|------|----------|-------|----------|-----|
| 1 | [Voxel Core & Chunk Engine](./epics/epic-01-voxel-core.md) | Fondation : données voxel, chunks, chargement/déchargement | 6-8 sem. | 🔴 Critique | FR-01 |
| 2 | [Meshing Pipeline](./epics/epic-02-meshing.md) | Rendu des chunks : greedy meshing, AO, texture arrays | 4-6 sem. | 🔴 Critique | FR-04 |
| 3 | [Génération Procédurale](./epics/epic-03-worldgen.md) | Mapgen v7 + flat, biomes, minerais, grottes, arbres | 6-8 sem. | 🔴 Critique | FR-02 |
| 4 | [Système d'Éclairage](./epics/epic-04-lighting.md) | Lumière dual jour/nuit, propagation BFS, smooth lighting | 3-4 sem. | 🟠 Haute | FR-03 |
| 5 | [Moteur de Scripting Lua](./epics/epic-05-lua.md) | MoonSharp, sandbox, API core, ModManager, register_* | 8-12 sem. | 🔴 Critique | FR-05, FR-06, FR-07 |
| 6 | [Gameplay Fondamental](./epics/epic-06-gameplay.md) | Joueur, inventaire, crafting, dig/place, physique | 6-8 sem. | 🟠 Haute | FR-08 |
| 7 | [Entités & Mobs](./epics/epic-07-entities.md) | ECS entités, SAO bridge, définitions Lua, IA basique | 4-6 sem. | 🟠 Haute | FR-09 |
| 8 | [Interface & Formspecs](./epics/epic-08-gui.md) | Parser formspec, UI Toolkit, HUD, inventaire visuel | 4-6 sem. | 🟠 Haute | FR-10 |
| 9 | [Réseau & Multijoueur](./epics/epic-09-network.md) | Protocole UDP, client/serveur, sync monde, auth | 6-8 sem. | 🟡 Moyenne | FR-11 |
| 10 | [Stockage & Persistence](./epics/epic-10-storage.md) | SQLite backend, save/load monde, sérialisation, compression | 3-4 sem. | 🟠 Haute | FR-12 |

**Durée totale estimée :** 50-70 semaines (~12-16 mois) pour un développeur solo expérimenté.

## FR Coverage Map

| Exigence | Epic(s) | Couverture |
|----------|---------|------------|
| FR-01 Monde Voxel Infini | Epic 1 | ✅ Complète |
| FR-02 Génération Procédurale | Epic 3 | ✅ v7 + flat (MVP), 6 autres post-MVP |
| FR-03 Éclairage Dual | Epic 4 | ✅ Complète |
| FR-04 Rendu Performant | Epic 2 | ✅ Complète |
| FR-05 Modding Lua | Epic 5 | ✅ API core (80%+ MVP) |
| FR-06 Callbacks Moteur | Epic 5 | ✅ Complète |
| FR-07 ABMs/LBMs | Epic 5 | ✅ Complète |
| FR-08 Gameplay Joueur | Epic 6 | ✅ Complète |
| FR-09 Entités | Epic 7 | ✅ Complète |
| FR-10 Formspecs | Epic 8 | ✅ 20/40 éléments MVP |
| FR-11 Réseau | Epic 9 | ✅ Complète |
| FR-12 Stockage | Epic 10 | ✅ Complète |

## Notes

- Les epics 1-4 forment le **"moteur silencieux"** (rendu voxel fonctionnel sans gameplay).
- L'epic 5 (Lua) **débloque tout le gameplay** via les mods — c'est le point de bascule du projet.
- L'epic 10 (Stockage) peut être parallélisé avec les epics 6-8 car il n'en dépend pas directement.
