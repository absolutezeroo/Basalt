# Epic 7 : Entités & Mobs

> **Priorité :** 🟠 Haute · **Durée :** 6-8 semaines · **PRD :** FR-09  
> **Dépendances :** Epic 1, Epic 5

## Objectif

Les mobs et objets lâchés existent dans le monde comme entités scriptables en Lua, avec persistence au save/load.

---

### Story 7.1 : Système d'entités ECS avec bridge Lua

**En tant que** moddeur Lua, **je veux** enregistrer des entités via `core.register_entity()`, **afin que** mes créatures et objets scriptés existent dans le monde.

**Critères d'acceptation :**
- **Given** `core.register_entity('mymod:sheep', {visual="mesh", mesh="sheep.obj", textures={"sheep.png"}, on_step=func})`, **When** spawnée, **Then** Entity ECS créée avec composants visuels
- **Given** une entité active, **When** `on_step` appelé, **Then** `func(self, dtime)` reçoit les propriétés
- **Given** `core.add_entity(pos, "mymod:sheep")`, **When** appelé en Lua, **Then** l'entité apparaît à `pos`

**Complexité :** XL (8-13 jours) · **Stack :** Entities 1.4+, `IComponentData`, `Entities.Graphics`

---

### Story 7.2 : ItemEntity (objets lâchés) et collecte

**En tant que** joueur, **je veux** que les blocs cassés tombent au sol et soient ramassables, **afin que** la boucle dig→collect→craft fonctionne.

**Critères d'acceptation :**
- **Given** un bloc cassé, **When** l'item drop, **Then** `__builtin:item` apparaît avec gravité
- **Given** un item au sol et joueur à < 1.5 blocs, **When** à portée, **Then** collecté dans l'inventaire
- **Given** un item au sol, **When** un autre item identique est droppé à proximité, **Then** les stacks fusionnent (si < max_stack_size)

**Complexité :** L (5-8 jours) · **Stack :** Prefab item entity, collision sphere, pickup timer, stack merging

---

### Story 7.3 : Physique des entités

**En tant que** développeur du moteur, **je veux** une physique basique (gravité, collision voxel), **afin que** les mobs se comportent de manière crédible.

**Critères d'acceptation :**
- **Given** une entité en l'air, **When** pas de support, **Then** elle tombe avec gravité (-9.81)
- **Given** une entité qui marche, **When** mur de blocs, **Then** bloquée (AABB collision)
- **Given** `object:set_velocity(vel)`, **When** appelé, **Then** l'entité se déplace à cette vitesse
- **Given** `object:set_acceleration(acc)`, **When** appelé, **Then** l'accélération est appliquée chaque tick

**Complexité :** M (3-5 jours) · **Stack :** AABB vs voxel grid, velocity integration

---

### Story 7.4 : ObjectRef API Lua

**En tant que** moddeur Lua, **je veux** manipuler les entités depuis Lua, **afin de** contrôler les mobs, leur vie, et leur comportement.

**Critères d'acceptation :**
- **Given** un `ObjectRef`, **When** j'appelle `get_pos()`, `set_pos(pos)`, **Then** position lue/modifiée
- **Given** `object:set_velocity(vel)`, `object:get_velocity()`, **Then** vitesse modifiée/lue
- **Given** `object:set_hp(hp)`, `object:get_hp()`, **Then** HP modifiés/lus
- **Given** `object:punch(puncher, time_from_last_punch, tool_caps)`, **When** appelé, **Then** dégâts calculés et `on_punch` callback invoqué
- **Given** `object:remove()`, **When** appelé, **Then** l'entité est détruite
- **Given** `object:get_luaentity()`, **When** c'est une LuaEntity, **Then** retourne la table Lua de l'entité
- **Given** `object:set_properties({visual="mesh", mesh="new.obj"})`, **When** appelé, **Then** les propriétés visuelles sont mises à jour
- **Given** `object:set_animation(frame_range, speed, blend)`, **When** appelé, **Then** l'animation joue

**Complexité :** L (5-8 jours) · **Stack :** `ObjectRef` Lua binding, bridge vers ECS components

**Ref Luanti :** `References/luanti/src/script/lua_api/l_object.h`

---

### Story 7.5 : Static objects (persistence des entités)

**En tant que** joueur, **je veux** que les mobs et items au sol soient sauvegardés quand un chunk est déchargé, **afin que** les entités ne disparaissent pas.

**Critères d'acceptation :**
- **Given** un mob dans un chunk, **When** le chunk est déchargé (joueur s'éloigne), **Then** le mob est sérialisé comme `StaticObject` dans le MapBlock
- **Given** un chunk avec des static objects, **When** le chunk est rechargé, **Then** les mobs sont recréés à leurs positions avec leur état Lua
- **Given** `get_staticdata()` défini sur l'entité, **When** le mob est sérialisé, **Then** la valeur retournée est stockée
- **Given** un `StaticObject` stocké, **When** le mob est recréé, **Then** `on_activate(self, staticdata, dtime_s)` est appelé avec les données sauvegardées et le temps écoulé

**Complexité :** L (5-8 jours) · **Stack :** `StaticObject` struct, sérialisation dans le MapBlock data, `on_activate`/`get_staticdata`

**Ref Luanti :** `References/luanti/src/staticobject.h`, `serverenvironment.cpp`
