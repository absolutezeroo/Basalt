# Epic 7 : Entités & Mobs

> **Priorité :** 🟠 Haute · **Durée :** 4-6 semaines · **PRD :** FR-09  
> **Dépendances :** Epic 1, Epic 5

## Objectif

Les mobs et objets lâchés existent dans le monde comme entités scriptables en Lua.

---

### Story 7.1 : Système d'entités ECS avec bridge Lua

**En tant que** moddeur Lua, **je veux** enregistrer des entités via `core.register_entity()`, **afin que** mes créatures et objets scriptés existent dans le monde.

**Critères d'acceptation :**
- **Given** `core.register_entity('mymod:sheep', {on_step=func})`, **When** spawnée, **Then** Entity ECS créée avec composants visuels
- **Given** une entité active, **When** `on_step` est appelé, **Then** `func(self, dtime)` reçoit les bonnes propriétés

**Complexité :** XL (8-13 jours) · **Stack :** Entities 1.4+, `IComponentData`, `Entities.Graphics`

---

### Story 7.2 : ItemEntity (objets lâchés) et collecte

**En tant que** joueur, **je veux** que les blocs cassés tombent au sol et soient ramassables, **afin que** la boucle dig→collect→craft fonctionne.

**Critères d'acceptation :**
- **Given** un bloc cassé, **When** l'item drop, **Then** `__builtin:item` apparaît avec gravité
- **Given** un item au sol et joueur à < 1.5 blocs, **When** à portée, **Then** collecté dans l'inventaire

**Complexité :** L (5-8 jours) · **Stack :** Prefab item entity, collision sphere, pickup timer

---

### Story 7.3 : Physique des entités

**En tant que** développeur du moteur, **je veux** que les entités aient une physique basique (gravité, collision voxel, knockback), **afin que** les mobs se comportent de manière crédible.

**Critères d'acceptation :**
- **Given** une entité en l'air, **When** pas de support, **Then** elle tombe avec gravité (-9.81)
- **Given** une entité qui marche, **When** elle atteint un mur de blocs, **Then** elle est bloquée (AABB collision)

**Complexité :** M (3-5 jours) · **Stack :** AABB vs voxel grid, velocity integration, pas de PhysX
