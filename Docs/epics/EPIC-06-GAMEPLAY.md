# Epic 6 : Gameplay Fondamental

> **Priorité :** 🟠 Haute · **Durée :** 6-8 semaines · **PRD :** FR-08  
> **Dépendances :** Epic 1, Epic 2, Epic 5

## Objectif

Un joueur peut se déplacer, casser/poser des blocs, gérer son inventaire et crafter.

---

### Story 6.1 : Contrôleur joueur (mouvement, collision, caméra)

**En tant que** joueur, **je veux** me déplacer fluidement (marcher, sauter, nager, voler), **afin que** l'exploration soit agréable.

**Critères d'acceptation :**
- **Given** un terrain plat, **When** j'avance, **Then** vitesse = 4 blocs/s (défaut Luanti)
- **Given** un trou de 1 bloc, **When** je marche dessus, **Then** je tombe (collision AABB correcte)
- **Given** le mode fly, **When** j'appuie espace/shift, **Then** je monte/descends librement

**Complexité :** L (5-8 jours) · **Stack :** CharacterController custom, AABB vs voxel grid

---

### Story 6.2 : Système dig/place

**En tant que** joueur, **je veux** casser des blocs (clic gauche) et en poser (clic droit), **afin de** modifier le monde.

**Critères d'acceptation :**
- **Given** un bloc cracky=3 et une pioche stone, **When** je maintiens clic gauche, **Then** le bloc casse après le dig_time calculé
- **Given** un bloc en main, **When** clic droit sur une face, **Then** le bloc est posé sur cette face
- **Given** des callbacks Lua on_dig/on_place, **When** l'action se produit, **Then** les callbacks sont appelés

**Complexité :** L (5-8 jours) · **Stack :** Raycast voxel DDA, dig time formula Luanti

---

### Story 6.3 : Inventaire joueur

**En tant que** joueur, **je veux** un inventaire (hotbar 8 + grille 32), **afin de** collecter et organiser mes ressources.

**Critères d'acceptation :**
- **Given** un bloc cassé, **When** l'item est collecté, **Then** il apparaît dans le premier slot libre
- **Given** un stack d'items déplacé, **When** je le pose, **Then** les règles de stacking sont respectées

**Complexité :** M (3-5 jours) · **Stack :** `InvRef`, `ItemStack` struct, `ListName` indexing

---

### Story 6.4 : Table de crafting et cuisson

**En tant que** joueur, **je veux** crafter des items et cuire des minerais, **afin de** progresser dans le jeu.

**Critères d'acceptation :**
- **Given** un pattern pioche 3×3, **When** je prends le résultat, **Then** les ingrédients sont consommés
- **Given** un fourneau avec minerai + charbon, **When** le temps est écoulé, **Then** le lingot apparaît

**Complexité :** M (3-5 jours) · **Stack :** `CraftManager`, formspec crafting grid, furnace node metadata timer
