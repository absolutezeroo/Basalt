# Epic 5 : Moteur de Scripting Lua

> **Priorité :** 🔴 Critique · **Durée :** 12-16 semaines · **PRD :** FR-05, FR-06, FR-07  
> **Dépendances :** Epic 1

## Objectif

Exécuter des mods Lua compatibles Luanti dans Unity via MoonSharp. Point de bascule du projet — après cet epic, tout le gameplay est piloté par les mods.

---

### Story 5.1 : Runtime MoonSharp et sandbox

**En tant que** développeur du moteur, **je veux** un environnement Lua sandboxé, **afin que** les mods ne puissent pas accéder au filesystem ou crasher le moteur.

**Critères d'acceptation :**
- **Given** MoonSharp en `Preset_SoftSandbox`, **When** un mod tente `os.execute()`, **Then** erreur Lua
- **Given** `AutoYieldCounter=100000`, **When** un mod entre en boucle infinie, **Then** interruption automatique

**Complexité :** M (3-5 jours) · **Stack :** MoonSharp `Script`, `CoreModules`, `AutoYieldCounter`

---

### Story 5.2 : API core.register_node() et register_item()

**En tant que** moddeur Lua, **je veux** enregistrer des blocs, items et outils avec toutes leurs propriétés, **afin que** mes mods ajoutent du contenu.

**Critères d'acceptation :**
- **Given** `core.register_node('mymod:stone', {tiles={'stone.png'}, groups={cracky=3}, drawtype="normal"})`, **When** le moteur traite, **Then** une `NodeDefinition` correcte est créée
- **Given** `core.register_craftitem('mymod:stick', {inventory_image='stick.png'})`, **When** enregistré, **Then** l'item existe dans le registre
- **Given** `core.register_tool('mymod:pick', {tool_capabilities={...}})`, **When** enregistré, **Then** l'outil a ses `ToolCapabilities` (groupcaps, damage_groups, punch_attack_uses)
- **Given** 500 nœuds enregistrés, **When** le jeu démarre, **Then** tous disponibles et registre figé

**Complexité :** XL (8-13 jours) · **Stack :** MoonSharp C#↔Lua bindings, `Dictionary<string, NodeDefinition>`

---

### Story 5.3 : ModManager — chargement, résolution, exécution

**En tant que** moddeur Lua, **je veux** que mes mods avec dépendances soient chargés dans le bon ordre, **afin qu**'un mod dépendant de `default` soit chargé après.

**Critères d'acceptation :**
- **Given** 3 mods (A→B→C), **When** le ModManager charge, **Then** l'ordre est C → B → A
- **Given** un mod avec `optional_depends` manquant, **When** chargement, **Then** charge sans erreur
- **Given** une dépendance circulaire, **When** analyse, **Then** erreur explicite au démarrage

**Complexité :** L (5-8 jours) · **Stack :** Tri topologique, parsing `mod.conf`

---

### Story 5.4 : API core.register_craft()

**En tant que** moddeur Lua, **je veux** définir des recettes de crafting, **afin que** les joueurs puissent fabriquer des objets.

**Critères d'acceptation :**
- **Given** une recette shaped 3×3, **When** un joueur place les items, **Then** le résultat est produit
- **Given** une recette cooking avec cooktime=3, **When** un item est dans un fourneau, **Then** après 3s le résultat apparaît
- **Given** une recette fuel avec burntime=40, **When** du charbon est dans le slot fuel, **Then** il brûle pendant 40s

**Complexité :** L (5-8 jours) · **Stack :** `CraftRecipe` (shaped, shapeless, cooking, fuel), pattern matching

---

### Story 5.5 : Callbacks système

**En tant que** moddeur Lua, **je veux** des callbacks pour tous les événements du jeu, **afin que** mes mods réagissent dynamiquement.

**Critères d'acceptation :**
- **Given** `core.register_globalstep(func)`, **When** serveur tick, **Then** `func(dtime)` appelée
- **Given** `core.register_on_dignode(func)`, **When** joueur casse un bloc, **Then** `func(pos, oldnode, digger)` appelée
- **Given** `core.register_on_placenode(func)`, **When** joueur pose un bloc, **Then** `func(pos, newnode, placer, itemstack, pointed_thing)` appelée
- **Given** `core.register_on_generated(func)`, **When** mapchunk généré, **Then** `func(minp, maxp, blockseed)` appelée
- **Given** `core.register_on_joinplayer(func)`, **When** joueur rejoint, **Then** `func(player, last_login)` appelée
- **Given** `core.register_on_leaveplayer(func)`, **When** joueur quitte, **Then** `func(player, timed_out)` appelée

**Complexité :** L (5-8 jours) · **Stack :** Listes de `DynValue`, invocation moteur C#

---

### Story 5.6 : ABMs et LBMs

**En tant que** moddeur Lua, **je veux** que certains blocs se transforment périodiquement, **afin que** le monde évolue dynamiquement.

**Critères d'acceptation :**
- **Given** un ABM sur `default:dirt` avec `neighbors=default:water`, interval=10, **When** tick toutes les 10s, **Then** transformation
- **Given** `chance=5`, **When** l'ABM évalue, **Then** chaque nœud a 1/5 de chance
- **Given** un LBM sur un ancien nœud, **When** le chunk charge, **Then** le LBM s'exécute une fois

**Complexité :** L (5-8 jours) · **Stack :** `ABMDef`, itération active blocks, random sampling

---

### Story 5.7 : API monde (get_node, set_node, find_nodes_in_area)

**En tant que** moddeur Lua, **je veux** lire et modifier le monde depuis Lua, **afin que** mes mods interagissent avec le terrain.

**Critères d'acceptation :**
- **Given** `core.get_node({x=0,y=0,z=0})`, **When** nœud existe, **Then** retourne `{name="default:stone", param1=15, param2=0}`
- **Given** `core.set_node(pos, {name="air"})`, **When** appelé, **Then** nœud remplacé, chunk dirty, lumière re-propagée
- **Given** `core.find_nodes_in_area(minp, maxp, "default:stone")`, **When** 50 stone, **Then** retourne 50 positions
- **Given** `core.remove_node(pos)`, **When** appelé, **Then** équivalent à `set_node(pos, {name="air"})`

**Complexité :** M (3-5 jours) · **Stack :** Accès `NativeArray`, dirty flagging, light re-propagation trigger

---

### Story 5.8 : NodeMetaRef, InvRef et ItemStack

**En tant que** moddeur Lua, **je veux** stocker des données et des inventaires sur les nœuds, **afin que** coffres, fourneaux et machines fonctionnent.

**Critères d'acceptation :**
- **Given** `core.get_meta(pos)`, **When** appelé, **Then** retourne un `NodeMetaRef` avec `get_string()`, `set_string()`, `get_int()`, `set_int()`, `get_float()`, `set_float()`
- **Given** `meta:get_inventory()`, **When** appelé, **Then** retourne un `InvRef` avec les listes d'inventaire du nœud
- **Given** un `InvRef`, **When** j'appelle `add_item("main", ItemStack("default:stone 10"))`, **Then** 10 stone sont ajoutées
- **Given** un `InvRef`, **When** j'appelle `get_list("main")`, **Then** je reçois la liste des `ItemStack` du slot
- **Given** un `ItemStack`, **When** j'appelle `get_name()`, `get_count()`, `get_wear()`, `take_item(5)`, **Then** les valeurs sont correctes et le stack est muté
- **Given** `meta:mark_as_private("password")`, **When** le nœud est envoyé au client, **Then** le champ "password" n'est PAS envoyé

**Complexité :** XL (8-13 jours) · **Stack :** `NodeMetadata` (dict string→string + inventaires), `InvRef` wrapper, `ItemStack` Lua binding

**Ref Luanti :** `References/luanti/src/script/lua_api/l_nodemeta.h`, `l_inventory.h`, `l_item.h`

---

### Story 5.9 : Node Timers (NodeTimerRef)

**En tant que** moddeur Lua, **je veux** déclencher des callbacks périodiques sur un nœud, **afin que** fourneaux, machines et plantes qui poussent fonctionnent.

**Critères d'acceptation :**
- **Given** `core.get_node_timer(pos):start(10.0)`, **When** 10s s'écoulent, **Then** le callback `on_timer(pos, elapsed)` de la NodeDefinition est appelé
- **Given** un timer actif, **When** `timer:stop()` est appelé, **Then** le timer est annulé
- **Given** `timer:get_timeout()` et `timer:get_elapsed()`, **When** appelés, **Then** retournent les valeurs correctes
- **Given** un chunk avec des timers actifs, **When** le chunk est sauvegardé, **Then** les timers sont sérialisés (voir Epic 10)

**Complexité :** L (5-8 jours) · **Stack :** `NodeTimerList` par chunk, tick serveur itère les timers actifs, callbacks Lua

**Ref Luanti :** `References/luanti/src/nodetimer.h`, `script/lua_api/l_nodetimer.h`

---

### Story 5.10 : PlayerRef API

**En tant que** moddeur Lua, **je veux** accéder aux propriétés et méthodes du joueur, **afin que** mes mods gèrent la santé, la position, et l'inventaire du joueur.

**Critères d'acceptation :**
- **Given** `core.get_player_by_name("singleplayer")`, **When** joueur existe, **Then** retourne un `PlayerRef`
- **Given** un `PlayerRef`, **When** j'appelle `get_player_name()`, `get_pos()`, `get_hp()`, `get_breath()`, `get_inventory()`, **Then** les valeurs sont correctes
- **Given** `player:set_pos(pos)`, **When** appelé, **Then** le joueur est téléporté
- **Given** `player:set_hp(10, {type="punch"})`, **When** appelé, **Then** les HP changent et le callback `on_player_hpchange` est invoqué
- **Given** `player:get_player_control()`, **When** appelé, **Then** retourne les touches pressées (up, down, left, right, jump, sneak, dig, place)

**Complexité :** L (5-8 jours) · **Stack :** `PlayerRef` Lua binding, bridge vers PlayerController C#

**Ref Luanti :** `References/luanti/src/script/lua_api/l_object.h` — section Player-only methods

---

### Story 5.11 : Privilege system et chatcommands

**En tant que** administrateur de serveur, **je veux** un système de privilèges, **afin de** contrôler qui peut fly, give, teleport, etc.

**Critères d'acceptation :**
- **Given** `core.register_privilege("my_priv", {description="..."})`, **When** enregistré, **Then** le privilège existe
- **Given** `core.check_player_privs(player, {fly=true})`, **When** le joueur a le priv fly, **Then** retourne true
- **Given** `core.register_chatcommand("spawn", {privs={teleport=true}, func=...})`, **When** un joueur sans `teleport` tape `/spawn`, **Then** erreur "insufficient privileges"
- **Given** les privilèges par défaut (`interact`, `shout`), **When** un nouveau joueur rejoint, **Then** il a ces privilèges

**Complexité :** M (3-5 jours) · **Stack :** `PrivilegeManager`, `ChatCommandRegistry`, vérification avant exécution

---

### Story 5.12 : Utilitaires Lua essentiels

**En tant que** moddeur Lua, **je veux** les fonctions utilitaires de base, **afin que** mes mods fonctionnent sans recoder les basics.

**Critères d'acceptation :**
- **Given** `core.after(5.0, func, arg1)`, **When** 5s s'écoulent, **Then** `func(arg1)` est appelée
- **Given** `core.get_modpath("default")`, **When** le mod existe, **Then** retourne le chemin absolu du dossier
- **Given** `core.get_worldpath()`, **When** appelé, **Then** retourne le chemin du monde actuel
- **Given** `core.chat_send_player(name, msg)`, **When** appelé, **Then** le joueur reçoit le message dans le chat
- **Given** `core.chat_send_all(msg)`, **When** appelé, **Then** tous les joueurs reçoivent le message
- **Given** `core.log("action", "message")`, **When** appelé, **Then** le message apparaît dans la console Unity avec le bon log level
- **Given** `core.serialize(table)` et `core.deserialize(string)`, **When** appelés, **Then** round-trip correct

**Complexité :** M (3-5 jours) · **Stack :** Timer queue pour `core.after()`, path resolution, chat bridge
