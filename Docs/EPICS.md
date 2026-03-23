# Epics — Basalt Voxel Engine

> **Auteur :** Clayton · **Date :** Mars 2026 · **Statut :** En cours  
> **Input :** [PRD](./PRD.MD) · [Architecture](./ARCHITECTURE.MD) · [Audit](./AUDIT-MISSING-FEATURES.MD)

---

## Vue d'Ensemble

10 epics ordonnés par valeur utilisateur croissante. Chaque epic est **autonome** et livre une fonctionnalité complète. Mis à jour suite à l'audit de parité Luanti.

| # | Epic | Stories | Durée | Priorité | PRD |
|---|------|---------|-------|----------|-----|
| 1 | [Voxel Core & Chunk Engine](./Epics/EPIC-01-VOXEL-CORE.MD) | 4 | 6-8 sem. | 🔴 Critique | FR-01 |
| 2 | [Meshing Pipeline](./Epics/EPIC-02-MESHING.MD) | 6 | 5-7 sem. | 🔴 Critique | FR-04 |
| 3 | [Génération Procédurale](./Epics/EPIC-03-WORLDGEN.MD) | 8 | 8-10 sem. | 🔴 Critique | FR-02 |
| 4 | [Système d'Éclairage](./Epics/EPIC-04-LIGHTING.MD) | 4 | 4-6 sem. | 🟠 Haute | FR-03 |
| 5 | [Moteur de Scripting Lua](./Epics/EPIC-05-LUA.MD) | 12 | 12-16 sem. | 🔴 Critique | FR-05, FR-06, FR-07 |
| 6 | [Gameplay Fondamental](./Epics/EPIC-06-GAMEPLAY.MD) | 7 | 8-10 sem. | 🟠 Haute | FR-08 |
| 7 | [Entités & Mobs](./Epics/EPIC-07-ENTITIES.MD) | 5 | 6-8 sem. | 🟠 Haute | FR-09 |
| 8 | [Interface & Formspecs](./Epics/EPIC-08-GUI.MD) | 3 | 4-6 sem. | 🟠 Haute | FR-10 |
| 9 | [Réseau & Multijoueur](./Epics/EPIC-09-NETWORK.MD) | 5 | 6-8 sem. | 🟡 Moyenne | FR-11 |
| 10 | [Stockage & Persistence](./Epics/EPIC-10-STORAGE.MD) | 6 | 5-7 sem. | 🟠 Haute | FR-12 |

**Total : 60 stories · 64-86 semaines (~15-20 mois) pour un développeur solo expérimenté**

---

## FR Coverage Map

| Exigence | Epic(s) | Couverture |
|----------|---------|------------|
| FR-01 Monde Voxel Infini | Epic 1 | ✅ Complète |
| FR-02 Génération Procédurale | Epic 3 | ✅ v7 + flat + biomes + grottes + arbres + schematics |
| FR-03 Éclairage Dual | Epic 4 | ✅ Complète (propagation initiale + re-propagation dynamique) |
| FR-04 Rendu Performant | Epic 2 | ✅ Greedy meshing + AO + 7 drawtypes MVP |
| FR-05 Modding Lua | Epic 5 | ✅ 12 stories couvrant l'API T1 complète |
| FR-06 Callbacks Moteur | Epic 5 (5.5) | ✅ Complète |
| FR-07 ABMs/LBMs | Epic 5 (5.6) | ✅ Complète |
| FR-08 Gameplay Joueur | Epic 6 | ✅ Mouvement + dig/place + inventaire + crafting + HP + tool wear |
| FR-09 Entités | Epic 7 | ✅ ECS + ObjectRef API + static objects persistence |
| FR-10 Formspecs | Epic 8 | ✅ 20/40 éléments MVP |
| FR-11 Réseau | Epic 9 | ✅ UDP + auth + sync + médias |
| FR-12 Stockage | Epic 10 | ✅ MapBlock complet + player data + static objects + node timers |

---

## Dépendances entre Epics

```
Epic 1 (Core) ──────┬──→ Epic 2 (Meshing) ──→ Epic 2.6 (Drawtypes, après Epic 5.2)
                     ├──→ Epic 3 (WorldGen)
                     ├──→ Epic 4 (Lighting) ──→ Epic 4.4 (avant Epic 6)
                     ├──→ Epic 5 (Lua) ──┬──→ Epic 6 (Gameplay)
                     │                   ├──→ Epic 7 (Entities)
                     │                   ├──→ Epic 8 (GUI)
                     │                   └──→ Epic 9 (Network)
                     └──→ Epic 10 (Storage, parallélisable dès Epic 1)
```

**Points critiques :**
- Story 4.4 (light re-propagation) **doit** être terminée avant Story 6.2 (dig/place)
- Story 2.6 (drawtypes non-cubiques) **dépend** de Story 5.2 (register_node avec drawtype)
- Story 10.5 (static objects) **dépend** de Story 7.5 (entity serialization)
- Story 10.6 (node timers persistence) **dépend** de Story 5.9 (NodeTimerRef)

## Notes

- Les epics 1-4 forment le **"moteur silencieux"** (rendu voxel sans gameplay).
- L'epic 5 (Lua) **débloque tout le gameplay** — c'est le point de bascule.
- L'epic 10 (Storage) peut être parallélisé : Stories 10.1-10.3 dès l'Epic 1, Stories 10.4-10.6 après les Epics 5-7.
