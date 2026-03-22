# Epic 4 : Système d'Éclairage

> **Priorité :** 🟠 Haute · **Durée :** 3-4 semaines · **PRD :** FR-03  
> **Dépendances :** Epic 1, Epic 2

## Objectif

Éclairer le monde avec un système dual jour/nuit identique à Luanti, avec smooth lighting.

---

### Story 4.1 : Propagation de lumière BFS dual-channel

**En tant que** joueur, **je veux** que la lumière du soleil pénètre depuis le ciel et que les torches éclairent les zones sombres, **afin que** le monde soit lisible visuellement.

**Critères d'acceptation :**
- **Given** le soleil au zénith, **When** la lumière est propagée, **Then** tous les blocs exposés au ciel ont daylight = 15
- **Given** une torche (light_source=13), **When** la lumière est propagée, **Then** elle décroît de 1 par bloc sur 13 blocs
- **Given** param1 d'un nœud, **When** j'extrais les deux channels, **Then** bits 0-3 = night light, bits 4-7 = day light

**Complexité :** L (5-8 jours) · **Stack :** BFS dans `NativeQueue<int3>`, Burst job, port de `voxelalgorithms.cpp`

---

### Story 4.2 : Smooth lighting par interpolation de vertex

**En tant que** joueur, **je veux** des transitions douces de luminosité entre les blocs, **afin que** l'éclairage ne soit pas en marches d'escalier.

**Critères d'acceptation :**
- **Given** un sommet partagé entre 4 blocs, **When** la couleur vertex est calculée, **Then** elle est la moyenne des 4 valeurs adjacentes
- **Given** un shader URP, **When** le time-of-day change, **Then** le blend day/night s'interpole en temps réel

**Complexité :** M (3-5 jours) · **Stack :** Vertex colors RGBA (R=day, G=night, B=AO), shader URP custom

---

### Story 4.3 : Cycle jour/nuit

**En tant que** joueur, **je veux** que le ciel change de couleur et la luminosité varie au fil du temps, **afin que** le monde ait un rythme naturel.

**Critères d'acceptation :**
- **Given** un cycle de 20 minutes, **When** le temps passe, **Then** le facteur day_night_ratio varie de 0 à 1000
- **Given** le facteur courant, **When** le shader s'exécute, **Then** il blend entre les deux channels de lumière

**Complexité :** M (3-5 jours) · **Stack :** Uniform shader `_TimeOfDay`, skybox procédural gradient
