# Epic 3 : Génération Procédurale

> **Priorité :** 🔴 Critique · **Durée :** 8-10 semaines · **PRD :** FR-02  
> **Dépendances :** Epic 1

## Objectif

Générer un terrain réaliste et varié de manière procédurale. Reproduire le pipeline complet de `MapgenV7::makeChunk()` : terrain → biomes → grottes → minerais → arbres/décorations → poussière → liquides.

---

### Story 3.1 : Bibliothèque de bruit Perlin Burst-compatible

**En tant que** développeur du moteur, **je veux** des fonctions de bruit Perlin 2D/3D reproduisant le comportement de Luanti, **afin que** les mapgens produisent des terrains identiques pour un même seed.

**Critères d'acceptation :**
- **Given** un seed et des coordonnées identiques à Luanti, **When** je génère du bruit, **Then** les valeurs correspondent à ±0.001
- **Given** une grille de 80³ points, **When** le bruit 3D est calculé avec Burst, **Then** le temps est < 5ms

**Complexité :** L (5-8 jours) · **Stack :** Struct blittable `NoiseParams`, Burst, `Unity.Mathematics`

**Ref Luanti :** `References/luanti/src/noise.cpp`, `noise.h`

---

### Story 3.2 : Mapgen v7 (terrain principal)

**En tant que** joueur, **je veux** un terrain varié avec montagnes, plaines et océans, **afin que** l'exploration soit intéressante.

**Critères d'acceptation :**
- **Given** le mapgen v7 activé, **When** un mapchunk 80³ est généré, **Then** il contient terrain de base + ridges + montagnes
- **Given** les biomes enregistrés, **When** le terrain est généré, **Then** les nœuds de surface correspondent au biome (herbe, sable, neige)
- **Given** le pipeline v7, **When** un mapchunk est généré, **Then** les phases s'exécutent dans l'ordre : `generateTerrain` → `updateHeightmap` → `generateBiomes`

**Complexité :** XL (8-13 jours) · **Stack :** Mapchunk 5×5×5 MapBlocks, heightmap 2D + bruit 3D

**Ref Luanti :** `References/luanti/src/mapgen/mapgen_v7.cpp:299` — `makeChunk()`

---

### Story 3.3 : Mapgen flat

**En tant que** moddeur, **je veux** un mode monde plat, **afin de** tester mes mods rapidement.

**Critères d'acceptation :**
- **Given** le mapgen flat activé, **When** un chunk est généré, **Then** il contient stone/dirt/dirt_with_grass à la hauteur configurée

**Complexité :** S (1-2 jours)

---

### Story 3.4 : Système de biomes, minerais et décorations

**En tant que** joueur, **je veux** des biomes distincts, des minerais souterrains et des décorations en surface, **afin que** le monde ait de la diversité.

**Critères d'acceptation :**
- **Given** un biome `desert` enregistré avec heat/humidity ranges, **When** le mapchunk est dans une zone chaude/sèche, **Then** le sol est du sable
- **Given** `core.register_ore({ore_type="scatter", ...})`, **When** le mapchunk est généré, **Then** les minerais scatter apparaissent selon clust_scarcity, clust_size, y_min, y_max
- **Given** `core.register_decoration({deco_type="simple", ...})`, **When** la surface est générée, **Then** les décorations simples (herbe, fleurs) sont placées

**Ore types MVP :** `ORE_SCATTER`, `ORE_BLOB`. Post-MVP : `ORE_SHEET`, `ORE_PUFF`, `ORE_VEIN`, `ORE_STRATUM`.

**Decoration types MVP :** `DECO_SIMPLE`, `DECO_SCHEMATIC` (nécessaire pour les arbres, voir Story 3.7).

**Complexité :** XL (8-13 jours) · **Stack :** `BiomeDef`, `OreDef`, `DecorationDef`, heat/humidity noise maps

**Ref Luanti :** `References/luanti/src/mapgen/mg_biome.h`, `mg_ore.h`, `mg_decoration.h`

---

### Story 3.5 : Pipeline WorldGen asynchrone

**En tant que** joueur, **je veux** que la génération de terrain ne freeze pas le jeu, **afin que** l'exploration soit fluide.

**Critères d'acceptation :**
- **Given** le joueur qui se déplace rapidement, **When** 20 chunks doivent être générés, **Then** le framerate ne descend pas sous 30 FPS
- **Given** un budget de N chunks worldgen par frame (défaut 2), **When** le budget est atteint, **Then** les chunks restants sont reportés à la frame suivante
- **Given** un job worldgen schedulé à la frame F, **When** il n'est pas terminé, **Then** il est Complete() à F+1 ou plus tard (jamais de Complete() immédiat)
- **Given** le pipeline complet, **When** un chunk est demandé, **Then** il passe par : request → worldgen job → meshing job → GPU upload, chaque étage avec son propre budget

**Complexité :** M (3-5 jours) · **Stack :** `JobHandle` différé, queue de priorité par distance, budget par frame

---

### Story 3.6 : Génération de grottes

**En tant que** joueur, **je veux** des grottes naturelles sous le terrain, **afin que** l'exploration souterraine soit possible.

**Critères d'acceptation :**
- **Given** le mapgen v7, **When** un chunk souterrain est généré, **Then** des grottes par bruit 3D (noise intersection) sont présentes
- **Given** les cavernes activées (`MGV7_CAVERNS`), **When** le chunk est assez profond, **Then** de grandes cavernes apparaissent
- **Given** une grotte proche de la surface, **When** elle perce le terrain, **Then** l'ouverture est naturelle

**Complexité :** L (5-8 jours) · **Stack :** Noise intersection caves + randomwalk caves + caverns

**Ref Luanti :** `References/luanti/src/mapgen/cavegen.cpp`

---

### Story 3.7 : Arbres et schematics

**En tant que** joueur, **je veux** des arbres sur le terrain, **afin que** le monde ne soit pas chauve et ressemble à Luanti.

**Critères d'acceptation :**
- **Given** un biome forêt, **When** le terrain est généré, **Then** des arbres apparaissent via le système de décorations
- **Given** un schematic `.mts` d'arbre (apple tree, jungle tree, pine tree), **When** il est placé, **Then** la structure complète (tronc + branches + feuilles) est correctement instanciée dans le monde
- **Given** le loader de schematics, **When** il lit un fichier `.mts`, **Then** il parse le header MTSM, la taille, le name-id mapping, et les nœuds avec probabilités
- **Given** `DECO_SCHEMATIC` dans `register_decoration`, **When** le placement est calculé, **Then** le schematic est placé avec la rotation aléatoire si configurée

**Complexité :** L (5-8 jours) · **Stack :** Loader `.mts` (format binaire MTSCHEM v4), placement via DecorationManager, rotation 0/90/180/270

**Ref Luanti :** `References/luanti/src/mapgen/mg_schematic.cpp`, `treegen.cpp`

---

### Story 3.8 : Dust top nodes et liquid update

**En tant que** joueur, **je veux** que le terrain ait une couche de poussière réaliste et que les liquides remplissent les poches naturellement.

**Critères d'acceptation :**
- **Given** le biome desert, **When** `dustTopNodes()` s'exécute, **Then** une couche de `desert_sand` couvre les surfaces exposées
- **Given** une poche de lave/eau dans le terrain, **When** `updateLiquid()` s'exécute après la génération, **Then** les liquides sont flaggés pour propagation future
- **Given** un lac en surface, **When** le terrain est généré sous le water_level du biome, **Then** les espaces vides sont remplis d'eau

**Complexité :** M (3-5 jours) · **Stack :** `dustTopNodes()`, `updateLiquid()`, water_level par biome

**Ref Luanti :** `References/luanti/src/mapgen/mapgen.cpp` — `dustTopNodes()`, `updateLiquid()`
