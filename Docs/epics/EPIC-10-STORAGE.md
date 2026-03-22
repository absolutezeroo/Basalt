# Epic 10 : Stockage & Persistence

> **Priorité :** 🟠 Haute · **Durée :** 3-4 semaines · **PRD :** FR-12  
> **Dépendances :** Epic 1

## Objectif

Le monde est sauvegardé et rechargeable entre les sessions.

---

### Story 10.1 : Backend SQLite avec sérialisation de chunks

**En tant que** joueur, **je veux** que mon monde soit sauvegardé automatiquement et rechargeable, **afin que** ma progression ne soit jamais perdue.

**Critères d'acceptation :**
- **Given** un chunk modifié, **When** autosave (5s), **Then** chunk sérialisé + écrit en SQLite
- **Given** un monde sauvegardé, **When** relance du jeu, **Then** état identique à la sauvegarde
- **Given** 100 chunks dirty, **When** batch save, **Then** pas de freeze (async via `Awaitable`)

**Complexité :** L (5-8 jours) · **Stack :** SQLite4Unity3d, schema `(pos INTEGER PRIMARY KEY, data BLOB)`, zlib

---

### Story 10.2 : Node metadata persistence

**En tant que** moddeur Lua, **je veux** que les métadonnées des nœuds (inventaires coffres, état machines) soient sauvegardées, **afin que** les données customs persistent.

**Critères d'acceptation :**
- **Given** un coffre avec items, **When** save + reload, **Then** coffre contient les mêmes items
- **Given** `meta:set_string('owner','player1')`, **When** reload, **Then** `meta:get_string('owner')` = `'player1'`

**Complexité :** M (3-5 jours) · **Stack :** `NodeMetaRef` sérialisé avec le chunk, dict string→string + inventaires

---

### Story 10.3 : Configuration monde (world.mt)

**En tant que** joueur, **je veux** configurer les paramètres du monde (mapgen, seed, mods actifs), **afin de** personnaliser mon expérience.

**Critères d'acceptation :**
- **Given** `world.mt` avec `mg_name=v7` et `seed=12345`, **When** le monde charge, **Then** mapgen v7 avec ce seed
- **Given** la liste des mods activés, **When** le monde démarre, **Then** seuls ces mods sont chargés

**Complexité :** S (1-2 jours) · **Stack :** Parser key=value simple, `WorldConfig` struct
