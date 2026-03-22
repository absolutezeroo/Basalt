# Epic 8 : Interface & Formspecs

> **Priorité :** 🟠 Haute · **Durée :** 4-6 semaines · **PRD :** FR-10  
> **Dépendances :** Epic 5, Epic 6

## Objectif

L'interface utilisateur reproduit le système formspec de Luanti pour tous les menus de jeu.

---

### Story 8.1 : Parser formspec → UI Toolkit

**En tant que** moddeur Lua, **je veux** que mes formspec strings soient rendues comme UI interactive, **afin que** mes mods affichent des interfaces (coffres, fourneaux, machines).

**Critères d'acceptation :**
- **Given** `'size[8,9]list[context;main;0,0;8,4;]'`, **When** parsé, **Then** panneau 8×9 avec grille d'inventaire 8×4
- **Given** les 20 éléments formspec les plus utilisés, **When** parsés, **Then** éléments UI Toolkit correspondants produits

**Complexité :** XL (8-13 jours) · **Stack :** Parser C# state-machine, `VisualElement`, callback `on_receive_fields`

---

### Story 8.2 : HUD système

**En tant que** joueur, **je veux** voir ma barre de vie, hotbar, crosshair, et HUD custom des mods, **afin d'**avoir les informations essentielles à l'écran.

**Critères d'acceptation :**
- **Given** la hotbar 8 slots, **When** je scroll, **Then** le slot sélectionné change
- **Given** `core.hud_add(player, {type='image', ...})`, **When** le mod ajoute un HUD, **Then** il s'affiche

**Complexité :** L (5-8 jours) · **Stack :** UI Toolkit overlay, `HudElement` struct

---

### Story 8.3 : Chat et commandes

**En tant que** joueur, **je veux** taper des messages et des commandes (/give, /teleport, /time), **afin de** communiquer et administrer.

**Critères d'acceptation :**
- **Given** T pressé, **When** je tape un message, **Then** il est envoyé à tous les joueurs
- **Given** `/giveme default:stone 99`, **When** j'ai le privilège `give`, **Then** 99 stone dans mon inventaire

**Complexité :** M (3-5 jours) · **Stack :** `core.register_chatcommand`, privilege system
