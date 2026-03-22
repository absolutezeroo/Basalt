# Epic 5 : Moteur de Scripting Lua

> **Priorité :** 🔴 Critique · **Durée :** 8-12 semaines · **PRD :** FR-05, FR-06, FR-07  
> **Dépendances :** Epic 1 (pour les NodeDefs)

## Objectif

Exécuter des mods Lua compatibles Luanti dans le moteur Unity via MoonSharp. C'est l'epic le plus complexe et le point de bascule du projet — après celui-ci, tout le gameplay est piloté par les mods.

---

### Story 5.1 : Runtime MoonSharp et sandbox

**En tant que** développeur du moteur, **je veux** un environnement Lua sandboxé avec les modules autorisés uniquement, **afin que** les mods ne puissent pas accéder au filesystem ou crasher le moteur.

**Critères d'acceptation :**
- **Given** MoonSharp en `Preset_SoftSandbox`, **When** un mod tente `os.execute()`, **Then** erreur Lua
- **Given** `AutoYieldCounter=100000`, **When** un mod entre en boucle infinie, **Then** interruption automatique

**Complexité :** M (3-5 jours) · **Stack :** MoonSharp `Script`, `CoreModules`, `AutoYieldCounter`

---

### Story 5.2 : API core.register_node()

**En tant que** moddeur Lua, **je veux** enregistrer de nouveaux types de blocs avec toutes leurs propriétés, **afin que** mes mods ajoutent du contenu au monde.

**Critères d'acceptation :**
- **Given** `core.register_node('mymod:stone', {tiles={'stone.png'}, groups={cracky=3}})`, **When** le moteur traite l'enregistrement, **Then** une `NodeDefinition` est créée avec drawtype, tiles, et groups corrects
- **Given** 500 nœuds enregistrés, **When** le jeu démarre, **Then** tous sont disponibles et le registre est figé

**Complexité :** XL (8-13 jours) · **Stack :** MoonSharp C#↔Lua bindings, `Dictionary<string, NodeDefinition>`

---

### Story 5.3 : ModManager — chargement, résolution, exécution

**En tant que** moddeur Lua, **je veux** que mes mods avec dépendances soient chargés dans le bon ordre, **afin que** un mod dépendant de `default` soit chargé après celui-ci.

**Critères d'acceptation :**
- **Given** 3 mods (A→B→C), **When** le ModManager charge, **Then** l'ordre est C → B → A
- **Given** un mod avec `optional_depends` manquant, **When** le chargement s'exécute, **Then** le mod charge sans erreur
- **Given** une dépendance circulaire, **When** le ModManager analyse, **Then** erreur explicite au démarrage

**Complexité :** L (5-8 jours) · **Stack :** Tri topologique, parsing `mod.conf`

---

### Story 5.4 : API core.register_craft()

**En tant que** moddeur Lua, **je veux** définir des recettes de crafting (shaped, shapeless, cooking, fuel), **afin que** les joueurs puissent fabriquer des objets.

**Critères d'acceptation :**
- **Given** une recette shaped 3×3, **When** un joueur place les items correctement, **Then** le résultat est produit
- **Given** une recette cooking, **When** un item est dans un fourneau, **Then** après le temps défini, le résultat apparaît

**Complexité :** L (5-8 jours) · **Stack :** `CraftRecipe` structs, pattern matching, `CraftManager`

---

### Story 5.5 : Callbacks système (globalstep, on_generated, on_dignode, etc.)

**En tant que** moddeur Lua, **je veux** enregistrer des fonctions appelées par le moteur à chaque tick, génération, ou action joueur, **afin que** mes mods puissent réagir à tous les événements du jeu.

**Critères d'acceptation :**
- **Given** `core.register_globalstep(func)`, **When** le serveur tick, **Then** `func(dtime)` est appelée
- **Given** `core.register_on_dignode(func)`, **When** un joueur casse un bloc, **Then** `func(pos, oldnode, digger)` est appelée
- **Given** `core.register_on_generated(func)`, **When** un mapchunk est généré, **Then** `func(minp, maxp, blockseed)` est appelée

**Complexité :** L (5-8 jours) · **Stack :** Listes de `DynValue`, invocation moteur C#

---

### Story 5.6 : ABMs et LBMs

**En tant que** moddeur Lua, **je veux** que certains blocs se transforment périodiquement (herbe, lave), **afin que** le monde évolue dynamiquement.

**Critères d'acceptation :**
- **Given** un ABM sur `default:dirt` avec `neighbors=default:water`, interval=10, **When** le serveur tick toutes les 10s, **Then** les dirt adjacents à l'eau se transforment
- **Given** `chance=5`, **When** l'ABM est évalué, **Then** chaque nœud éligible a 1/5 de chance d'être transformé

**Complexité :** L (5-8 jours) · **Stack :** `ABMDef`, itération active blocks, random sampling

---

### Story 5.7 : API monde (get_node, set_node, find_nodes_in_area)

**En tant que** moddeur Lua, **je veux** lire et modifier le monde depuis Lua, **afin que** mes mods puissent interagir avec le terrain.

**Critères d'acceptation :**
- **Given** `core.get_node({x=0,y=0,z=0})`, **When** un nœud existe, **Then** retourne `{name="default:stone", param1=15, param2=0}`
- **Given** `core.set_node(pos, {name="air"})`, **When** appelé, **Then** le nœud est remplacé et le mesh est marqué dirty
- **Given** `core.find_nodes_in_area(minp, maxp, "default:stone")`, **When** la zone contient 50 stone, **Then** retourne les 50 positions

**Complexité :** M (3-5 jours) · **Stack :** Accès direct `NativeArray`, dirty flagging, batch operations
