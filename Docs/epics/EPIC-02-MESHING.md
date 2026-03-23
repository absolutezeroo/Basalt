# Epic 2 : Meshing Pipeline

> **Priorité :** 🔴 Critique · **Durée :** 5-7 semaines · **PRD :** FR-04  
> **Dépendances :** Epic 1

## Objectif

Transformer les données voxel en meshes rendus à l'écran avec ambient occlusion et textures correctes. Le greedy meshing gère les blocs cubiques (`NDT_NORMAL`). Les drawtypes non-cubiques sont ajoutés en Story 2.6.

---

### Story 2.1 : Greedy Meshing Burst-compilé avec face culling

**En tant que** joueur, **je veux** que seules les faces visibles soient rendues et fusionnées en quads, **afin de** minimiser les triangles et maximiser le framerate.

**Critères d'acceptation :**
- **Given** deux blocs opaques adjacents, **When** le mesh est généré, **Then** la face partagée n'existe pas
- **Given** un chunk 16³ plein, **When** le mesh est généré, **Then** seules les 6×16² = 1536 faces extérieures existent
- **Given** un mur de 16×16 blocs identiques, **When** le mesh est généré, **Then** il contient un seul quad au lieu de 512
- **Given** un chunk standard, **When** le mesh est généré avec Burst, **Then** le temps est < 200μs

**Complexité :** L (5-8 jours) · **Stack :** Binary greedy meshing, Burst, `Solidness >= 2` pour le culling

---

### Story 2.2 : Ambient Occlusion par vertex

**En tant que** joueur, **je veux** des ombres douces dans les coins entre les blocs, **afin que** le monde ait de la profondeur visuelle.

**Critères d'acceptation :**
- **Given** un sommet avec 2 voisins + 1 coin, **When** l'AO est calculé, **Then** la valeur est 0
- **Given** un sommet sans voisins, **When** l'AO est calculé, **Then** la valeur est 3
- **Given** un quad avec AO asymétrique, **When** le mesh est généré, **Then** la diagonale est flippée
- **Given** deux faces adjacentes avec des AO différents, **When** le greedy merge évalue, **Then** elles ne sont PAS fusionnées

**Complexité :** M (3-5 jours) · **Stack :** Technique 0fps.net, merge key = `contentId << 8 | aoPacked`

---

### Story 2.3 : Texture Arrays et chargement PNG

**En tant que** joueur, **je veux** que chaque bloc affiche ses textures correctement, **afin que** le monde soit visuellement fidèle.

**Critères d'acceptation :**
- **Given** les PNGs chargées depuis `mods/default/textures/`, **When** packées, **Then** `Texture2DArray` 16×16 sans artefacts
- **Given** un quad greedy-mergé de 4×3, **When** les UVs sont émises, **Then** la texture tile 4×3 fois

**Complexité :** M (3-5 jours) · **Stack :** `Texture2DArray`, `Texture2D.LoadImage()`, résolution native 16×16

---

### Story 2.4 : Shader Voxel HLSL

**En tant que** développeur du moteur, **je veux** un shader URP en HLSL avec texture array, lighting dual et AO, **afin que** le rendu soit correct.

**Critères d'acceptation :**
- **Given** un vertex avec tile index, **When** le shader sample, **Then** `SAMPLE_TEXTURE2D_ARRAY` avec le bon index
- **Given** les vertex colors (R=day, G=night, B=AO), **When** le cycle jour/nuit change, **Then** blend correct
- **Given** un quad avec UVs > 1.0, **When** le shader sample, **Then** la texture tile (wrap repeat)

**Complexité :** M (3-5 jours) · **Stack :** HLSL `.shader` (pas ShaderGraph), URP, `Texture2DArray`

---

### Story 2.5 : Pipeline MeshDataArray multi-threadé

**En tant que** développeur du moteur, **je veux** que le meshing soit off-main-thread via `MeshDataArray`, **afin que** le thread principal ne soit jamais bloqué.

**Critères d'acceptation :**
- **Given** 64 chunks à re-mesher, **When** les jobs sont schedulés, **Then** < 1ms main thread pour ApplyAndDispose
- **Given** le Profiler, **When** j'analyse une frame, **Then** zéro `GC.Alloc` dans le pipeline de meshing
- **Given** un budget de N meshes/frame, **When** le budget est atteint, **Then** report à la frame suivante

**Complexité :** L (5-8 jours) · **Stack :** `Mesh.AllocateWritableMeshData`, budget par frame

---

### Story 2.6 : Drawtypes non-cubiques

**En tant que** joueur, **je veux** voir des fleurs, de l'eau, du verre, des feuilles et des escaliers, **afin que** le monde ne soit pas uniquement des cubes pleins.

**Critères d'acceptation :**
- **Given** un nœud `NDT_PLANTLIKE`, **When** le mesh est généré, **Then** deux quads croisés en X avec la bonne texture
- **Given** un nœud `NDT_LIQUID` source, **When** le mesh est généré, **Then** quad supérieur abaissé (level 7/8), faces exposées à l'air uniquement
- **Given** un nœud `NDT_GLASSLIKE` adjacent à un autre glasslike, **When** le mesh est généré, **Then** face interne culled
- **Given** un nœud `NDT_ALLFACES` (feuilles), **When** le mesh est généré, **Then** 6 faces toujours rendues
- **Given** un nœud `NDT_NODEBOX`, **When** le mesh est généré, **Then** géométrie correspond aux boxes de la `NodeDefinition`

**Drawtypes MVP :** `NDT_PLANTLIKE`, `NDT_LIQUID`, `NDT_FLOWINGLIQUID`, `NDT_GLASSLIKE`, `NDT_ALLFACES`, `NDT_TORCHLIKE`, `NDT_NODEBOX`

**Drawtypes post-MVP :** `NDT_SIGNLIKE`, `NDT_FENCELIKE`, `NDT_RAILLIKE`, `NDT_FIRELIKE`, `NDT_GLASSLIKE_FRAMED`, `NDT_MESH`, `NDT_PLANTLIKE_ROOTED`, `NDT_CONNECTED`

**Complexité :** XL (8-13 jours) · **Stack :** Switch sur `DrawType` dans le meshing job, geometry emitters par drawtype

**Dépendances :** Epic 5 (les nœuds non-cubiques doivent être enregistrés via Lua). Implémentée **après** Story 5.2.
