# Epic 10 : Stockage & Persistence

> **Priorité :** 🟠 Haute · **Durée :** 5-7 semaines · **PRD :** FR-12  
> **Dépendances :** Epic 1, Epic 5 (NodeMetaRef), Epic 7 (Static Objects)

## Objectif

Le monde complet est sauvegardé et rechargeable : terrain, métadonnées, timers, entités, et données joueur.

---

### Story 10.1 : Backend SQLite avec sérialisation complète des MapBlocks

**En tant que** joueur, **je veux** que le monde soit sauvegardé automatiquement, **afin que** ma progression ne soit jamais perdue.

**Critères d'acceptation :**
- **Given** un chunk modifié, **When** autosave (5s), **Then** chunk sérialisé + écrit en SQLite
- **Given** un monde sauvegardé, **When** relance du jeu, **Then** état identique
- **Given** 100 chunks dirty, **When** batch save, **Then** pas de freeze (async via `Awaitable`)
- **Given** le format de sérialisation d'un MapBlock, **When** il est écrit, **Then** il contient dans cet ordre :
  1. Flags (u8) — `is_underground`, `day_night_differs`, `lighting_complete`, `generated`
  2. Content width (u8 = 2) + params width (u8 = 2)
  3. Node data (4096 × 4 octets, compressé zlib)
  4. Node metadata (sérialisé)
  5. Static objects (sérialisés, voir Story 10.5)
  6. Node timers (sérialisés)
  7. Name-ID mapping (content_id ↔ node name pour la portabilité)

**Complexité :** L (5-8 jours) · **Stack :** SQLite4Unity3d, schema `(pos INTEGER PRIMARY KEY, data BLOB)`, zlib, name-id mapping

**Ref Luanti :** `References/luanti/src/mapblock.cpp` — `serialize()` / `deSerialize()`, `References/luanti/doc/world_format.md`

---

### Story 10.2 : Node metadata persistence

**En tant que** moddeur Lua, **je veux** que les métadonnées des nœuds persistent, **afin que** coffres, panneaux et machines gardent leur état.

**Critères d'acceptation :**
- **Given** un coffre avec items, **When** save + reload, **Then** coffre contient les mêmes items
- **Given** `meta:set_string('owner','player1')`, **When** reload, **Then** `meta:get_string('owner')` = `'player1'`
- **Given** un nœud avec inventaire (via `meta:get_inventory()`), **When** sérialisé, **Then** les listes d'inventaire sont incluses dans les métadonnées
- **Given** un champ marqué `mark_as_private`, **When** envoyé au client, **Then** le champ est exclu

**Complexité :** M (3-5 jours) · **Stack :** `NodeMetadata` sérialisé dans le MapBlock, dict string→string + inventaires

---

### Story 10.3 : Configuration monde (world.mt)

**En tant que** joueur, **je veux** configurer les paramètres du monde, **afin de** personnaliser mon expérience.

**Critères d'acceptation :**
- **Given** `world.mt` avec `mg_name=v7` et `seed=12345`, **When** le monde charge, **Then** mapgen v7 avec ce seed
- **Given** la liste des mods activés, **When** le monde démarre, **Then** seuls ces mods sont chargés

**Complexité :** S (1-2 jours) · **Stack :** Parser key=value, `WorldConfig` struct

---

### Story 10.4 : Player data persistence

**En tant que** joueur, **je veux** retrouver ma position, mes HP, mon inventaire et mes privilèges quand je reviens dans le monde, **afin de** ne pas tout perdre à chaque session.

**Critères d'acceptation :**
- **Given** un joueur à la position (100, 50, -200) avec 15 HP et 8 de breath, **When** le monde est sauvegardé, **Then** ces valeurs sont stockées dans SQLite
- **Given** un joueur qui revient, **When** il rejoint, **Then** il respawn à sa position sauvegardée avec ses HP, breath, et inventaire intacts
- **Given** l'inventaire du joueur avec 3 stacks, **When** sauvegardé, **Then** les 3 stacks sont restaurés au prochain login
- **Given** le look direction (pitch + yaw), **When** sauvegardé et rechargé, **Then** le joueur regarde dans la même direction

**Données persistées :**
- Position (x, y, z) — float
- Look direction (pitch, yaw) — float
- HP — u16
- Breath — u16
- Inventaire complet (toutes les listes) — sérialisé

**Complexité :** M (3-5 jours) · **Stack :** Table SQLite `players` (name, pitch, yaw, posX, posY, posZ, hp, breath) + table `player_inventories`

**Ref Luanti :** `References/luanti/src/database/database-sqlite3.cpp` — `savePlayer()` / `loadPlayer()`

---

### Story 10.5 : Static objects dans les MapBlocks

**En tant que** joueur, **je veux** que les mobs et items au sol soient sauvegardés avec les chunks, **afin que** les entités ne disparaissent pas entre les sessions.

**Critères d'acceptation :**
- **Given** un mob dans un chunk, **When** le chunk est sauvegardé, **Then** le mob est sérialisé comme `StaticObject` dans le blob du MapBlock
- **Given** un chunk avec des static objects, **When** il est rechargé, **Then** les entités sont recréées via `on_activate(self, staticdata, dtime_s)`
- **Given** le format StaticObject, **When** sérialisé, **Then** il contient : type (u8), position (v3f), data (string de `get_staticdata()`)
- **Given** un chunk déchargé depuis 300 secondes, **When** rechargé, **Then** `dtime_s = 300` est passé à `on_activate`

**Complexité :** M (3-5 jours) · **Stack :** `StaticObjectList` dans le MapBlock, sérialisation/désérialisation, bridge avec Epic 7

**Ref Luanti :** `References/luanti/src/staticobject.h`

---

### Story 10.6 : Node timers persistence

**En tant que** joueur, **je veux** que les timers de fourneaux et machines survivent au save/load, **afin que** les processus en cours ne soient pas interrompus.

**Critères d'acceptation :**
- **Given** un fourneau avec un timer actif (timeout=10, elapsed=6), **When** le chunk est sauvegardé, **Then** le timer est inclus dans le MapBlock
- **Given** le chunk rechargé, **When** les timers sont désérialisés, **Then** le fourneau reprend avec elapsed=6 (+ dtime_s depuis le save)
- **Given** le format NodeTimerList, **When** sérialisé, **Then** chaque timer contient : position (v3s16), timeout (f32), elapsed (f32)

**Complexité :** S (1-2 jours) · **Stack :** `NodeTimerList` sérialisé dans le MapBlock data, juste après les static objects

**Ref Luanti :** `References/luanti/src/nodetimer.h`
