# Epic 2 : Meshing Pipeline

> **Priorité :** 🔴 Critique · **Durée :** 4-6 semaines · **PRD :** FR-04  
> **Dépendances :** Epic 1

## Objectif

Transformer les données voxel en meshes rendus à l'écran avec ambient occlusion et textures correctes.

---

### Story 2.1 : Face culling entre nœuds adjacents

**En tant que** joueur, **je veux** que seules les faces visibles soient rendues, **afin de** ne pas gaspiller de triangles sur des faces cachées.

**Critères d'acceptation :**
- **Given** deux blocs opaques adjacents, **When** le mesh est généré, **Then** la face partagée n'existe pas
- **Given** un bloc opaque adjacent à l'air, **When** le mesh est généré, **Then** la face du bloc opaque est présente
- **Given** un chunk 16³ plein, **When** le mesh est généré, **Then** seules les 6×16² = 1536 faces extérieures existent

**Complexité :** M (3-5 jours) · **Stack :** `IJobParallelFor`, Burst, accès voisins 3×3×3

---

### Story 2.2 : Greedy Meshing Burst-compilé

**En tant que** joueur, **je veux** que les faces identiques adjacentes soient fusionnées en quads plus grands, **afin de** minimiser les triangles et maximiser le framerate.

**Critères d'acceptation :**
- **Given** un mur de 16×16 blocs identiques, **When** le mesh est généré, **Then** il contient un seul quad (2 triangles) au lieu de 512
- **Given** un chunk standard, **When** le mesh est généré avec Burst, **Then** le temps est < 200μs sur CPU moderne

**Complexité :** L (5-8 jours) · **Stack :** Binary greedy meshing, NativeArray bitmasks, `[BurstCompile]`

---

### Story 2.3 : Ambient Occlusion par vertex

**En tant que** joueur, **je veux** des ombres douces dans les coins et recoins entre les blocs, **afin que** le monde ait de la profondeur visuelle sans coût runtime.

**Critères d'acceptation :**
- **Given** un sommet avec 2 voisins latéraux + 1 coin, **When** l'AO est calculé, **Then** la valeur est 0 (le plus sombre)
- **Given** un sommet sans voisins, **When** l'AO est calculé, **Then** la valeur est 3 (pleine luminosité)
- **Given** un quad avec AO asymétrique, **When** le mesh est généré, **Then** la diagonale du quad est flippée (correction anisotropie)

**Complexité :** M (3-5 jours) · **Stack :** Technique 0fps.net, 4 états AO, quad flipping

---

### Story 2.4 : Texture Arrays et matériaux URP

**En tant que** joueur, **je veux** que chaque type de bloc affiche ses textures correctement sans bleeding, **afin que** le monde soit visuellement fidèle.

**Critères d'acceptation :**
- **Given** 256 textures de blocs, **When** elles sont packées, **Then** elles forment un `Texture2DArray` sans artefacts de mipmapping
- **Given** un bloc avec 6 faces différentes, **When** il est rendu, **Then** chaque face affiche la bonne texture via l'index du texture array

**Complexité :** M (3-5 jours) · **Stack :** `Texture2DArray`, Shader Graph URP, vertex data

---

### Story 2.5 : Pipeline MeshDataArray multi-threadé

**En tant que** développeur du moteur, **je veux** que la génération de meshes soit entièrement off-main-thread via `MeshDataArray`, **afin que** le thread principal ne soit jamais bloqué par le meshing.

**Critères d'acceptation :**
- **Given** 64 chunks à re-mesher, **When** les jobs sont schedulés, **Then** le main thread ne stall pas (< 1ms pour ApplyAndDispose)
- **Given** le Profiler Unity, **When** j'analyse une frame, **Then** zéro `GC.Alloc` provient du pipeline de meshing

**Complexité :** L (5-8 jours) · **Stack :** `Mesh.AllocateWritableMeshData`, `IJobParallelFor`, double-passe (count+write)
