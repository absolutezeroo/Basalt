# Product Brief : Basalt

> **Auteur :** Clayton · **Date :** Mars 2026 · **Statut :** Approuvé

---

## Executive Summary

Basalt est un moteur de jeu voxel open-world développé dans Unity 6.4, reproduisant à l'identique les fonctionnalités de Luanti (anciennement Minetest) : monde infini composé de blocs 16×16×16, génération procédurale multi-algorithmes, éclairage dual jour/nuit par propagation BFS, architecture client/serveur native, et surtout un système de modding Lua complet compatible avec l'écosystème ContentDB de Luanti.

Le projet exploite les capacités haute performance de Unity 6.4 — Burst Compiler (100-150× vs C# managé), Job System parallèle, `Mesh.MeshDataArray` pour le meshing multi-threadé, ECS natif, GPU Resident Drawer, et URP — pour atteindre ou dépasser les performances du moteur C++ original tout en bénéficiant de l'écosystème d'outils et du support multiplateforme de Unity.

## Le Problème

Luanti repose sur IrrlichtMt, un fork vieillissant d'Irrlicht limité à OpenGL, sans pipeline de rendu moderne (pas de PBR, pas de post-processing avancé, pas de Vulkan). Le moteur C++ rend les contributions difficiles pour les développeurs non-spécialisés. Il n'existe aucun portage fonctionnel de Luanti vers un moteur commercial moderne — la seule tentative (`Safebox36/Minetest-Unity`) contient un unique commit abandonné.

## La Solution

Basalt reconstruit l'intégralité de Luanti dans Unity 6.4, composant par composant, en utilisant les équivalents natifs Unity pour chaque sous-système :

- **Burst/Jobs** pour le voxel core
- **MoonSharp** pour le scripting Lua
- **Unity Transport** pour le réseau
- **SQLite** pour le stockage
- **URP** pour le rendu
- **UI Toolkit** pour les formspecs

L'objectif est la compatibilité fonctionnelle complète avec l'API Lua documentée de Luanti (12 336 lignes de documentation).

## Ce Qui Rend Basalt Différent

- Performance native C++ via Burst Compiler sur une plateforme C# accessible
- Pipeline de rendu moderne (URP, PBR, Shader Graph, post-processing)
- Support multiplateforme natif (Windows, Linux, macOS, Android, iOS, WebGL)
- Écosystème d'outils Unity (Profiler, Frame Debugger, Physics Debugger)
- ECS natif pour les entités de gameplay (mobs, items, particules)

## Qui Utilise Basalt

**Joueurs Luanti** — Retrouvent leur expérience avec un rendu modernisé et des performances améliorées.

**Moddeurs Lua** — Portent leurs mods existants ou en créent de nouveaux avec la même API familière.

**Développeurs Unity** — Disposent d'un moteur voxel professionnel comme base pour leurs propres jeux.

## Critères de Succès

- Exécuter Minetest Game (le jeu de référence Luanti) sans modification majeure des mods
- Atteindre 60 FPS avec 12 chunks de draw distance sur hardware milieu de gamme
- Connecter un client Basalt à un serveur Luanti existant (compatibilité protocole)
- Supporter au minimum 80% des fonctions de l'API Lua documentée

## Périmètre v1.0 (MVP)

**Inclus :** Monde voxel infini, 2 mapgens (v7 + flat), éclairage dual, multijoueur basique, API Lua core (`register_node`, `register_entity`, `register_craft`, ABMs, formspecs basiques), sauvegarde SQLite.

**Exclus v1.0 :** Les 6 autres mapgens, compatibilité protocole réseau Luanti, ContentDB intégré, support mobile, VoxelManipulator complet.

## Vision Long Terme

Basalt devient la référence pour le développement de jeux voxel moddables dans Unity : un moteur open-source robuste, performant, compatible avec l'écosystème Luanti existant, tout en ouvrant la porte à des rendus next-gen (ray tracing, GI, volumétrique) impossibles sur IrrlichtMt.
