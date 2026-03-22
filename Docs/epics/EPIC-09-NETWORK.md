# Epic 9 : Réseau & Multijoueur

> **Priorité :** 🟡 Moyenne · **Durée :** 6-8 semaines · **PRD :** FR-11  
> **Dépendances :** Epic 1, Epic 5, Epic 6

## Objectif

Plusieurs joueurs peuvent jouer ensemble sur un serveur dédié ou en mode hôte.

---

### Story 9.1 : Protocole UDP avec couche de fiabilité

**En tant que** développeur du moteur, **je veux** un protocole UDP avec 3 canaux (fiable ordonné, fiable non-ordonné, non-fiable), **afin que** les données critiques soient garanties et les données fréquentes soient rapides.

**Critères d'acceptation :**
- **Given** un paquet fiable perdu, **When** pas d'ACK dans 500ms, **Then** retransmission automatique
- **Given** le canal non-fiable, **When** 100 paquets position envoyés, **Then** zéro overhead d'acquittement

**Complexité :** XL (8-13 jours) · **Stack :** Unity Transport Layer, channels custom, sérialisation binaire

---

### Story 9.2 : Authentification et gestion des joueurs

**En tant que** administrateur de serveur, **je veux** que les joueurs s'authentifient avec un nom et mot de passe, **afin de** protéger les comptes et privilèges.

**Critères d'acceptation :**
- **Given** un nouveau joueur, **When** première connexion, **Then** compte créé avec hash SRP du mot de passe
- **Given** un joueur banni, **When** tentative de connexion, **Then** refusé avec message

**Complexité :** L (5-8 jours) · **Stack :** SRP auth (comme Luanti), ban list, privilege system

---

### Story 9.3 : Synchronisation du monde

**En tant que** joueur client, **je veux** recevoir les chunks du serveur et voir les modifications des autres, **afin que** le monde soit cohérent.

**Critères d'acceptation :**
- **Given** un client connecté, **When** je me déplace, **Then** le serveur envoie les chunks dans ma draw distance
- **Given** un autre joueur qui casse un bloc, **When** à portée, **Then** modification visible en < 200ms

**Complexité :** XL (8-13 jours) · **Stack :** Chunk serialization + zlib, delta updates

---

### Story 9.4 : Transfert de médias

**En tant que** joueur client, **je veux** recevoir automatiquement les textures et sons des mods du serveur, **afin de** voir le contenu custom sans installation manuelle.

**Critères d'acceptation :**
- **Given** un serveur avec 50 textures custom, **When** un client se connecte, **Then** toutes les textures sont transférées avant le spawn
- **Given** un serveur HTTP configuré, **When** le client détecte le support, **Then** les médias sont téléchargés par HTTP (plus rapide)

**Complexité :** L (5-8 jours) · **Stack :** Media hash index, HTTP fetch optionnel, `Texture2D.LoadImage()`
