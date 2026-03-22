# Guide de Démarrage Claude Code — Basalt

> Copie-colle ces prompts dans Claude Code dans l'ordre.
> Chaque prompt correspond à une étape logique.

---

## Étape 0 — Premier lancement (une seule fois)

Ouvre Claude Code dans le dossier racine de ton projet Unity Basalt (vide ou fraîchement créé), et colle :

```
Lis le fichier CLAUDE.md à la racine du projet. C'est ton contexte complet.
Puis lis docs/architecture.md pour l'architecture et docs/epics/epic-01-voxel-core.md pour les premières stories.
La source de référence Luanti est dans reference/luanti/ — c'est ta source de vérité.

Ne code rien encore. Confirme-moi que tu as compris :
1. Le bitpacking des nœuds (content u16 + param1 u8 + param2 u8)
2. La structure des assemblies (11 asmdef séparées)
3. Que le hot path doit être zéro GC, Burst-compatible, blittable uniquement
4. Que la taille des chunks est 16³ (MAP_BLOCKSIZE = 16)
```

---

## Étape 1 — Scaffolding du projet

Une fois que Claude Code a confirmé sa compréhension :

```
Crée le scaffolding complet du projet Unity selon docs/architecture.md :

1. Crée tous les dossiers sous Assets/ (Basalt.Core/, Basalt.WorldGen/, etc.)
2. Crée chaque fichier .asmdef avec les bonnes dépendances (voir le graphe dans architecture.md)
3. Crée un .gitignore Unity standard
4. Crée un README.md basique pour le repo

Ne crée aucune classe de gameplay encore — uniquement la structure vide avec les asmdef.
Assure-toi que chaque asmdef référence correctement ses dépendances 
et que Burst/Collections/Mathematics sont dans les bons asmdef.
```

---

## Étape 2 — Epic 1, Story 1.1 : MapBlock

```
Implémente la Story 1.1 de docs/epics/epic-01-voxel-core.md : Structure de données MapBlock.

Crée dans Basalt.Core/Data/ :

- MapNode.cs : struct blittable avec les méthodes Pack/Unpack statiques 
  (voir le code exact dans CLAUDE.md section "Données Voxel Fondamentales")
- BasaltConstants.cs : toutes les constantes Luanti (MAP_BLOCKSIZE, CONTENT_AIR, etc.)
- MapBlock.cs : struct contenant un NativeArray<uint> de 4096 éléments 
  avec méthodes GetNode(x,y,z) et SetNode(x,y,z, node)
- CoordinateUtils.cs : conversions WorldPos↔ChunkPos↔LocalPos dans Basalt.Core/Coordinates/

Tout doit être Burst-compatible et blittable. Aucun string, aucune classe, aucun managed type.
Ajoute les [BurstCompile] où c'est pertinent.
Vérifie les critères d'acceptation de la story avant de considérer que c'est fini.
```

---

## Étape 3 — Epic 1, Story 1.3 : Coordonnées

```
Implémente la Story 1.3 : Système de coordonnées monde.

Enrichis CoordinateUtils.cs avec :
- WorldToChunk(int3 worldPos) → int3 chunkPos
- WorldToLocal(int3 worldPos) → int3 localPos  
- ChunkToWorld(int3 chunkPos) → int3 worldPos (coin min du chunk)
- NodeIndex(int x, int y, int z) → int (index linéaire dans le NativeArray)
- IsValidChunkPos(int3 pos) → bool (dans les limites ±31000)

Attention : les coordonnées négatives doivent fonctionner correctement 
(division entière en C# arrondit vers zéro, pas vers le bas — il faut gérer ça).
Teste mentalement avec la position (-1, 0, 0) → chunk (-1,0,0) local (15,0,0).
```

---

## Étape 4 — Epic 1, Story 1.4 : NodeDefinition et Registre

```
Implémente la Story 1.4 : NodeDefinition et registre de nœuds.

Crée dans Basalt.Core/Registry/ :

- DrawType.cs : enum avec les 22 drawtypes de Luanti (Normal, Liquid, Plantlike, Mesh, 
  Nodebox, Glasslike, GlasslikeFramed, Allfaces, AllFacesOptional, Torchlike, Signlike, 
  Fencelike, Firelike, Raillike, etc.)
- ParamType.cs : enum (None, Light)
- ParamType2.cs : enum (None, FaceDir, WallMounted, Color, etc.)
- NodeDefinition.cs : struct blittable avec content_id, drawtype, paramtype, paramtype2,
  light_source, sunlight_propagates, walkable, pointable, diggable, groups (via un index 
  vers une table séparée pour les groups car Dictionary n'est pas blittable)
- NodeRegistry.cs : classe qui maintient un NativeArray<NodeDefinition> indexé par content_id 
  et un Dictionary<string, ushort> pour le mapping nom→id (côté managé uniquement)

Le NativeArray<NodeDefinition> est le registre "runtime" accessible depuis les jobs Burst.
Le Dictionary<string, ushort> est le registre "managé" utilisé par le Lua et l'initialisation.
```

---

## Étape 5 — Epic 1, Story 1.2 : ChunkManager

```
Implémente la Story 1.2 : ChunkManager et pool de chunks.

Crée dans Basalt.Client/Chunk/ :

- ChunkHandle.cs : struct légère avec l'index dans le pool + generation counter (pour détecter les handles stale)
- ChunkPool.cs : pré-alloue N MapBlocks dans un grand NativeArray. Méthodes Rent() et Return().
  Pas de new/delete à chaque chunk. Allocator.Persistent.
- ChunkManager.cs : MonoBehaviour (c'est dans Client, c'est autorisé) qui :
  - Maintient un NativeHashMap<int3, ChunkHandle> des chunks actifs
  - Chaque frame, calcule les chunks à charger/décharger selon la position du joueur
  - Charge en spirale depuis le joueur vers l'extérieur (chunks les plus proches d'abord)
  - Budget de N chunks chargés/déchargés par frame pour lisser la charge
  - Zéro allocation GC dans Update()

Pour l'instant les chunks chargés sont remplis de CONTENT_AIR — la worldgen viendra à l'Epic 3.
Ajoute un gizmo dans OnDrawGizmos() pour visualiser les chunks chargés dans la Scene View.
```

---

## Prompt Générique pour les Stories Suivantes

Pour chaque nouvelle story, utilise ce pattern :

```
Implémente la Story X.Y de docs/epics/epic-XX-{nom}.md : {titre de la story}.

Lis d'abord les critères d'acceptation de la story.
Lis CLAUDE.md pour les conventions.
Vérifie dans quelle assembly ce code doit aller.
Si tu as un doute sur le comportement attendu, cherche dans reference/luanti/src/.
Assure-toi que c'est Burst-compatible si c'est dans le hot path.
Zéro allocation GC.
Vérifie chaque critère d'acceptation avant de considérer la story terminée.
```

---

## Commandes Rapides à Garder Sous la Main

```
# Quand Claude Code dérive ou oublie le contexte :
Relis CLAUDE.md. Tu dérives du projet.

# Quand tu veux vérifier la conformité Luanti :
Regarde comment Luanti implémente ça dans luanti/src/{fichier}.cpp

# Quand tu veux passer à la story suivante :
La story X.Y est terminée. Passe à la story X.Z de docs/epics/epic-XX.md.

# Quand tu veux un résumé de ce qui est fait :
Fais un état des lieux : quelles stories sont terminées, lesquelles restent dans l'epic en cours.

# Quand tu veux une review :
Review le code actuel de Basalt.{Assembly}. Vérifie : 
zéro GC dans le hot path, blittable, conventions CLAUDE.md respectées.
```

---

## Tips

- **Une story à la fois.** Ne demande jamais 3 stories en un prompt — Claude Code va bâcler.
- **Vérifie les AC.** Après chaque story, relis les critères d'acceptation et demande à Claude Code de les vérifier un par un.
- **Profiler tôt.** Dès que le ChunkManager + Meshing marchent, profile. Les problèmes de perf détectés tard sont 10× plus chers à fixer.
- **Commit par story.** Un commit = une story terminée. Message format : `feat(core): Story 1.1 — MapBlock data structure`
- **Le CLAUDE.md est vivant.** Mets-le à jour quand tu prends une décision qui n'y est pas encore.
