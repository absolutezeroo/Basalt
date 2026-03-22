# Epic 3 : Génération Procédurale

> **Priorité :** 🔴 Critique · **Durée :** 6-8 semaines · **PRD :** FR-02  
> **Dépendances :** Epic 1

## Objectif

Générer un terrain réaliste et varié de manière procédurale avec biomes, grottes, minerais et arbres.

---

### Story 3.1 : Bibliothèque de bruit Perlin Burst-compatible

**En tant que** développeur du moteur, **je veux** des fonctions de bruit Perlin 2D/3D Burst-compilées reproduisant le comportement de Luanti, **afin que** les mapgens produisent des terrains identiques pour un même seed.

**Critères d'acceptation :**
- **Given** un seed et des coordonnées identiques à Luanti, **When** je génère du bruit, **Then** les valeurs correspondent à ±0.001
- **Given** une grille de 80³ points, **When** le bruit 3D est calculé avec Burst, **Then** le temps est < 5ms

**Complexité :** L (5-8 jours) · **Stack :** Struct blittable `NoiseParams`, Burst, `Unity.Mathematics`

---

### Story 3.2 : Mapgen v7 (terrain principal)

**En tant que** joueur, **je veux** un terrain varié avec montagnes, plaines, rivières et océans, **afin que** l'exploration soit intéressante.

**Critères d'acceptation :**
- **Given** le mapgen v7 activé, **When** un mapchunk 80³ est généré, **Then** il contient terrain de base + ridges + montagnes
- **Given** les biomes enregistrés, **When** le terrain est généré, **Then** les nœuds de surface correspondent au biome

**Complexité :** XL (8-13 jours) · **Stack :** Mapchunk 5×5×5 MapBlocks, heightmap 2D + bruit 3D

---

### Story 3.3 : Mapgen flat

**En tant que** moddeur, **je veux** un mode monde plat pour le test et la construction, **afin de** tester mes mods rapidement.

**Critères d'acceptation :**
- **Given** le mapgen flat activé, **When** un chunk est généré, **Then** il contient stone/dirt/dirt_with_grass à la hauteur configurée

**Complexité :** S (1-2 jours)

---

### Story 3.4 : Système de biomes, minerais et décorations

**En tant que** joueur, **je veux** des biomes distincts avec des minerais souterrains et des arbres en surface, **afin que** le monde ait de la diversité.

**Critères d'acceptation :**
- **Given** un biome `desert` enregistré, **When** je suis dans une zone chaude, **Then** le sol est du sable avec des cactus
- **Given** `core.register_ore({...})`, **When** le mapchunk est généré, **Then** les minerais apparaissent selon les paramètres
- **Given** `core.register_decoration({...})`, **When** la surface est générée, **Then** les décorations sont placées

**Complexité :** XL (8-13 jours) · **Stack :** `BiomeDef`, `OreDef`, `DecorationDef` via ScriptableObjects + NativeArrays

---

### Story 3.5 : Génération de grottes

**En tant que** joueur, **je veux** des systèmes de grottes naturelles sous le terrain, **afin que** l'exploration souterraine soit possible.

**Critères d'acceptation :**
- **Given** le mapgen v7, **When** un chunk souterrain est généré, **Then** des grottes creusées par bruit 3D sont présentes
- **Given** une grotte proche de la surface, **When** elle perce le terrain, **Then** l'ouverture est naturelle

**Complexité :** L (5-8 jours) · **Stack :** Bruit 3D avec threshold, CaveGen job dédié
