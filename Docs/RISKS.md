# Risques & Mitigations — Basalt

> **Auteur :** Clayton · **Date :** Mars 2026

---

## Matrice des Risques

| # | Risque | Sévérité | Probabilité | Mitigation | Surveillance |
|---|--------|----------|-------------|------------|--------------|
| R1 | **Performance Lua** — MoonSharp est 5-10× plus lent que LuaJIT. Les mods faisant du calcul intensif (mapgen Lua, IA mobs) seront significativement plus lents. | 🔴 Critique | Haute | Réserver Lua aux callbacks événementiels, jamais dans les boucles chaudes. Cacher les DynValue. Profiler tôt. Envisager Lua-CSharp si Unity supporte .NET 8+ via CoreCLR. | Dès Epic 5 — benchmark de 1000 `register_node()` calls |
| R2 | **GC Pressure** — Même avec NativeContainers, certaines opérations Unity passent par des allocations managées. Le Large Object Heap de .NET est un ennemi connu des moteurs voxel C#. | 🟠 Haute | Haute | NativeContainers partout dans le hot path. Pooling agressif (chunks, meshes, jobs). Aucune allocation dans les jobs Burst. Profiler le GC chaque sprint. | Continu — `GC.Alloc = 0` dans le Profiler pour le hot path |
| R3 | **Compatibilité mods** — Même avec une API Lua fidèle, les mods peuvent dépendre de comportements subtils, timing, ou features non documentées du moteur C++. La compatibilité à 100% est irréaliste. | 🟠 Haute | Très haute | Tests d'intégration continus avec les 20 mods ContentDB les plus populaires. Objectif réaliste de 80% (pas 100%). Documenter les incompatibilités connues. | Phase 2+ — suite de tests automatisés par mod |
| R4 | **Scope creep** — L'API Lua de Luanti fait 12 336 lignes de documentation, 35+ catégories, des centaines de fonctions. La tentation d'implémenter "juste une de plus" est permanente. | 🟠 Haute | Très haute | Implémenter par tiers de priorité : **T1** = utilisé par Minetest Game (~40% de l'API), **T2** = top 50 mods (~30%), **T3** = reste (~30%). Ne JAMAIS implémenter T3 avant que T1 soit solide et testé. | Chaque sprint — revoir la priorisation T1/T2/T3 |
| R5 | **Fatigue développeur solo** — 12-18 mois de développement sur un projet de cette envergure est épuisant. Hytale avait 150 employés et a été annulé. | 🟡 Moyenne | Haute | Milestones courtes (4 mois max) avec résultats visibles et démo-ables. Open-source dès Phase 1 pour attirer des contributeurs. Chaque phase livre quelque chose de "jouable" ou "montrable". | Continu — si 2 sprints consécutifs sans progrès visible, réévaluer le scope |
| R6 | **Évolution Unity 6.x** — Unity pourrait introduire des breaking changes dans les APIs utilisées (Mesh API, ECS, Transport). | 🟢 Basse | Basse | Rester sur Unity 6.4 tant qu'il est supporté. Ne pas chasser les betas. Isoler les APIs Unity derrière des abstractions (interfaces C#). | Veille sur les release notes Unity trimestriellement |
| R7 | **Upload GPU goulot** — `Mesh.ApplyAndDisposeWritableMeshData()` est obligatoirement main-thread. Si trop de chunks sont re-meshés simultanément, le framerate drop. | 🟡 Moyenne | Haute | Limiter les uploads à N chunks/frame (budget de 2ms). File d'attente de priorité (distance au joueur). Étaler les re-meshing sur plusieurs frames. | Dès Epic 2 — mesurer le coût d'ApplyAndDispose |
| R8 | **Taille du monde** — Le système de coordonnées ±31000 blocs implique des positions larges. Les float32 manquent de précision loin de l'origine (> 10 000 blocs). | 🟡 Moyenne | Moyenne | Utiliser des coordonnées entières (int3) partout dans le moteur. Ne convertir en float que pour le rendu, avec recentrage relatif à la caméra (floating origin pattern). | Dès Epic 1 — tester à position (20000, 0, 20000) |

## Actions Immédiates

1. **Benchmark MoonSharp dès le prototype** (R1) — Avant d'investir 8-12 semaines dans l'Epic 5, valider avec un micro-benchmark que 500 `register_node()` + 10 000 `core.get_node()` par seconde est acceptable.
2. **Établir le profiling GC dès l'Epic 1** (R2) — Chaque PR doit passer un test "0 GC.Alloc in hot path" avant merge.
3. **Définir la liste T1 de l'API Lua** (R4) — Avant de commencer l'Epic 5, lister exactement les fonctions utilisées par Minetest Game et les marquer comme T1.
