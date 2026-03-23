# Epic 6 : Gameplay Fondamental

> **Priorité :** 🟠 Haute · **Durée :** 8-10 semaines · **PRD :** FR-08  
> **Dépendances :** Epic 1, Epic 2, Epic 4 (Story 4.4), Epic 5

## Objectif

Un joueur peut se déplacer, casser/poser des blocs, gérer son inventaire, crafter, utiliser des fourneaux, et subir des dégâts.

---

### Story 6.1 : Contrôleur joueur (mouvement, collision, caméra)

**En tant que** joueur, **je veux** me déplacer fluidement (marcher, sauter, nager, voler), **afin que** l'exploration soit agréable.

**Critères d'acceptation :**
- **Given** un terrain plat, **When** j'avance, **Then** vitesse = 4 blocs/s (défaut Luanti)
- **Given** un trou de 1 bloc, **When** je marche dessus, **Then** je tombe (collision AABB)
- **Given** le mode fly (privilège), **When** espace/shift, **Then** monte/descends librement
- **Given** le joueur dans l'eau, **When** il se déplace, **Then** vitesse réduite, peut nager vers le haut avec jump

**Complexité :** L (5-8 jours) · **Stack :** CharacterController custom, AABB vs voxel grid

---

### Story 6.2 : Système dig/place

**En tant que** joueur, **je veux** casser des blocs et en poser, **afin de** modifier le monde.

**Critères d'acceptation :**
- **Given** un bloc cracky=3 et une pioche stone, **When** clic gauche maintenu, **Then** le bloc casse après le dig_time calculé
- **Given** un bloc en main, **When** clic droit sur une face, **Then** le bloc est posé sur cette face
- **Given** les callbacks Lua `on_dig`/`on_place`, **When** action, **Then** callbacks appelés
- **Given** `set_node` après dig, **When** le nœud change, **Then** la lumière est re-propagée (Story 4.4) et le chunk re-meshé

**Complexité :** L (5-8 jours) · **Stack :** Raycast voxel DDA, dig time formula Luanti

**Ref Luanti :** `References/luanti/src/tool.cpp` — `getDigParams()`

---

### Story 6.3 : Inventaire joueur

**En tant que** joueur, **je veux** un inventaire (hotbar 8 + grille 32), **afin de** collecter et organiser mes ressources.

**Critères d'acceptation :**
- **Given** un bloc cassé, **When** l'item est collecté, **Then** il apparaît dans le premier slot libre
- **Given** un stack d'items déplacé, **When** posé, **Then** règles de stacking respectées (max_stack_size)
- **Given** le joueur, **When** j'ouvre l'inventaire, **Then** le formspec inventaire s'affiche via le système formspec (Epic 8)

**Complexité :** M (3-5 jours) · **Stack :** `InvRef`, `ItemStack` struct, `ListName` indexing

---

### Story 6.4 : Table de crafting et cuisson

**En tant que** joueur, **je veux** crafter des items et cuire des minerais, **afin de** progresser dans le jeu.

**Critères d'acceptation :**
- **Given** un pattern pioche 3×3, **When** je prends le résultat, **Then** ingrédients consommés
- **Given** un fourneau avec minerai + charbon, **When** le temps est écoulé, **Then** le lingot apparaît
- **Given** le fourneau, **When** il cuit, **Then** il utilise un NodeTimer (Story 5.9) pour le timing

**Complexité :** M (3-5 jours) · **Stack :** `CraftManager`, formspec crafting grid, furnace via NodeTimer

---

### Story 6.5 : Node Timers engine-side

**En tant que** développeur du moteur, **je veux** que le serveur exécute les node timers chaque tick, **afin que** fourneaux, machines et plantes qui poussent fonctionnent.

**Critères d'acceptation :**
- **Given** un node timer actif à `timeout=10.0`, **When** 10s s'écoulent côté serveur, **Then** `on_timer(pos, elapsed)` est appelé en Lua
- **Given** `on_timer` qui retourne `true`, **When** le callback finit, **Then** le timer redémarre (boucle)
- **Given** `on_timer` qui retourne `false`, **When** le callback finit, **Then** le timer est supprimé
- **Given** 100 timers actifs, **When** le serveur tick, **Then** le temps de traitement est < 1ms

**Complexité :** M (3-5 jours) · **Stack :** `NodeTimerList` itéré dans le server tick, callbacks Lua

**Note :** L'API Lua `NodeTimerRef` est dans la Story 5.9. Cette story est le moteur engine-side qui exécute les timers.

---

### Story 6.6 : Tool Wear

**En tant que** joueur, **je veux** que mes outils s'usent à chaque utilisation, **afin que** la collecte de ressources ait un coût.

**Critères d'acceptation :**
- **Given** une pioche en bois (wear=0, max_uses calculé via `tool_capabilities.groupcaps`), **When** je casse un bloc, **Then** le wear augmente proportionnellement
- **Given** un outil avec wear >= 65535, **When** je casse un bloc, **Then** l'outil est détruit (disparaît de l'inventaire)
- **Given** `ItemStack:get_wear()`, **When** appelé, **Then** retourne la valeur 0-65535
- **Given** le wear d'un outil, **When** l'outil est affiché dans l'inventaire, **Then** une barre de durabilité colorée est visible

**Complexité :** M (3-5 jours) · **Stack :** `wear` field dans `ItemStack`, calcul via `ToolCapabilities`, barre visuelle dans le HUD

**Ref Luanti :** `References/luanti/src/tool.h:122` — `u32 wear`

---

### Story 6.7 : Health, Fall Damage et Drowning

**En tant que** joueur, **je veux** avoir des points de vie, subir des dégâts de chute et me noyer sous l'eau, **afin que** le jeu ait des risques.

**Critères d'acceptation :**
- **Given** le joueur avec HP = 20 (défaut), **When** il prend des dégâts, **Then** les HP diminuent et la barre de vie se met à jour
- **Given** HP <= 0, **When** le joueur meurt, **Then** il respawn au spawn point avec HP = 20 et son inventaire est droppé
- **Given** une chute de plus de ~3 blocs, **When** le joueur atterrit, **Then** il subit des dégâts proportionnels à la vitesse d'impact (formule Luanti : `damage = (speed/jump_speed)² - tolerance`)
- **Given** le joueur sous l'eau, **When** son breath (défaut 11) atteint 0, **Then** il subit 2 HP de dégâts par seconde
- **Given** le joueur qui sort la tête de l'eau, **When** il respire, **Then** son breath remonte progressivement
- **Given** un nœud avec le group `fall_damage_add_percent`, **When** le joueur atterrit dessus, **Then** les dégâts sont modifiés en conséquence

**Complexité :** L (5-8 jours) · **Stack :** `PlayerHP`, `PlayerBreath`, `PlayerHPChangeReason` (punch, fall, drowning, node_damage), death/respawn

**Ref Luanti :** `References/luanti/src/server/player_sao.h`, `clientenvironment.cpp:189`
