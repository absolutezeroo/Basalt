# Epic 4 : Système d'Éclairage

> **Priorité :** 🟠 Haute · **Durée :** 4-6 semaines · **PRD :** FR-03  
> **Dépendances :** Epic 1, Epic 2

## Objectif

Éclairer le monde avec un système dual jour/nuit identique à Luanti, avec smooth lighting et re-propagation dynamique au changement de nœud.

---

### Story 4.1 : Propagation de lumière BFS dual-channel

**En tant que** joueur, **je veux** que la lumière du soleil pénètre depuis le ciel et que les torches éclairent les grottes, **afin que** le monde soit lisible visuellement.

**Critères d'acceptation :**
- **Given** le soleil au zénith, **When** la lumière est propagée, **Then** tous les blocs exposés au ciel ont daylight = 15
- **Given** une torche (light_source=13), **When** la lumière est propagée, **Then** elle décroît de 1 par bloc sur 13 blocs
- **Given** param1 d'un nœud, **When** j'extrais les deux channels, **Then** bits 0-3 = night light, bits 4-7 = day light

**Complexité :** L (5-8 jours) · **Stack :** BFS dans `NativeQueue<int3>`, Burst job

**Ref Luanti :** `References/luanti/src/voxelalgorithms.cpp`

---

### Story 4.2 : Smooth lighting par interpolation de vertex

**En tant que** joueur, **je veux** des transitions douces de luminosité entre les blocs, **afin que** l'éclairage ne soit pas en marches d'escalier.

**Critères d'acceptation :**
- **Given** un sommet partagé entre 4 blocs, **When** la couleur vertex est calculée, **Then** elle est la moyenne des 4 valeurs adjacentes
- **Given** un shader URP, **When** le time-of-day change, **Then** le blend day/night s'interpole en temps réel

**Complexité :** M (3-5 jours) · **Stack :** Vertex colors RGBA (R=day, G=night, B=AO), shader URP

---

### Story 4.3 : Cycle jour/nuit

**En tant que** joueur, **je veux** que le ciel change de couleur et la luminosité varie, **afin que** le monde ait un rythme naturel.

**Critères d'acceptation :**
- **Given** un cycle de 20 minutes, **When** le temps passe, **Then** le facteur day_night_ratio varie de 0 à 1000
- **Given** le facteur courant, **When** le shader s'exécute, **Then** il blend entre les deux channels de lumière

**Complexité :** M (3-5 jours) · **Stack :** Uniform shader `_TimeOfDay`, skybox procédural gradient

---

### Story 4.4 : Re-propagation de lumière au changement de nœud

**En tant que** joueur, **je veux** que casser un bloc dans une grotte laisse passer la lumière et que poser une torche éclaire la zone, **afin que** l'éclairage soit toujours correct après mes modifications du monde.

**Critères d'acceptation :**
- **Given** un bloc opaque cassé dans une grotte exposée au ciel, **When** le nœud est retiré, **Then** la lumière du jour se propage dans le trou en < 1 frame
- **Given** une torche posée dans l'obscurité, **When** le nœud est placé, **Then** la lumière artificielle (light_source=13) se propage correctement
- **Given** une torche retirée, **When** le nœud est cassé, **Then** la lumière est d'abord retirée (BFS inverse) puis re-propagée depuis les sources voisines
- **Given** un changement de nœud à la frontière d'un chunk, **When** la lumière est re-propagée, **Then** les chunks voisins sont aussi mis à jour et re-meshés

**Complexité :** L (5-8 jours) · **Stack :** BFS inverse (unspread) + BFS forward (spread), dirty flagging des chunks affectés, re-meshing automatique

**Ref Luanti :** `References/luanti/src/voxelalgorithms.cpp` — `unspreadLight()` + `spreadLight()`

**Dépendances :** Nécessaire avant l'Epic 6 (dig/place). Sans cette story, casser/poser des blocs crée des artefacts d'éclairage permanents.
