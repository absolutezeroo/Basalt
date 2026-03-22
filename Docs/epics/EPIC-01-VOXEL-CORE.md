# Epic 1 : Voxel Core & Chunk Engine

> **Priorité :** 🔴 Critique · **Durée :** 6-8 semaines · **PRD :** FR-01  
> **Dépendances :** Aucune (premier epic)

## Objectif

Disposer d'un monde voxel chargeable/déchargeable avec un système de chunks fonctionnel visible à l'écran (cubes colorés placeholders).

---

### Story 1.1 : Structure de données MapBlock

**En tant que** développeur du moteur,  
**je veux** une structure MapBlock de 16³ nœuds avec content_t (u16) + param1 (u8) + param2 (u8) dans un `NativeArray<uint>` bitpacké,  
**afin de** garantir la compatibilité Burst/Jobs et la fidélité au format Luanti.

**Critères d'acceptation :**
- **Given** un `NativeArray<uint>` de 4096 éléments, **When** j'accède au nœud (x,y,z), **Then** l'index = `x + y*16 + z*256`
- **Given** un uint bitpacké, **When** j'extrais content/param1/param2, **Then** les valeurs correspondent aux champs Luanti
- **Given** 1000 MapBlocks alloués, **When** je mesure la mémoire, **Then** chaque bloc consomme exactement 16 384 octets de données nœud

**Complexité :** M (3-5 jours) · **Stack :** `NativeArray<uint>`, Burst, `Unity.Mathematics`

---

### Story 1.2 : ChunkManager et pool de chunks

**En tant que** joueur,  
**je veux** que le monde charge les chunks autour de moi et décharge ceux éloignés,  
**afin de** pouvoir explorer un monde infini sans crash mémoire.

**Critères d'acceptation :**
- **Given** un joueur à la position P, **When** la draw distance est de 8 chunks, **Then** tous les chunks dans un rayon de 8 sont chargés
- **Given** un chunk à distance > draw_distance + 2, **When** le tick suivant s'exécute, **Then** le chunk est déchargé et recyclé dans le pool
- **Given** 500 chunks chargés simultanément, **When** je profiler la frame, **Then** zéro allocation GC par frame sur le ChunkManager

**Complexité :** L (5-8 jours) · **Stack :** `NativeHashMap<int3, ChunkHandle>`, `Allocator.Persistent`

---

### Story 1.3 : Système de coordonnées monde

**En tant que** développeur du moteur,  
**je veux** un système de coordonnées conforme à Luanti (Y-up, ±31000 blocs par axe),  
**afin de** garantir des conversions monde/chunk/nœud sans ambiguïté.

**Critères d'acceptation :**
- **Given** une position monde (48, 120, -32), **When** je convertis en chunk + local, **Then** chunk=(3,7,-2) et local=(0,8,0)
- **Given** une position chunk (1999, 1999, 1999), **When** je vérifie la validité, **Then** elle est acceptée (dans la limite ±31000)

**Complexité :** S (1-2 jours) · **Stack :** `Unity.Mathematics int3`, fonctions utilitaires statiques

---

### Story 1.4 : NodeDefinition et registre de nœuds

**En tant que** développeur du moteur,  
**je veux** un registre central de définitions de nœuds (drawtype, paramtype, groups, tiles),  
**afin que** chaque type de bloc ait ses propriétés accessibles en O(1) par content_id.

**Critères d'acceptation :**
- **Given** le registre initialisé, **When** j'enregistre `default:stone` avec drawtype=normal, **Then** il reçoit un content_id unique
- **Given** un content_id, **When** je lookup dans le registre, **Then** j'obtiens la `NodeDefinition` complète en O(1) via un `NativeArray` indexé

**Complexité :** M (3-5 jours) · **Stack :** `ScriptableObject` pour l'éditeur, `NativeArray<NodeDef>` pour le runtime Burst
