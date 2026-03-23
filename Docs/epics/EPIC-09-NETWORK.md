# Epic 9 : Réseau & Multijoueur

> **Priorité :** 🟡 Moyenne · **Durée :** 6-8 semaines · **PRD :** FR-11  
> **Dépendances :** Epic 1, Epic 5, Epic 6

## Objectif

Plusieurs joueurs peuvent jouer ensemble sur un serveur dédié ou en mode hôte.

---

### Story 9.1 : Protocole UDP avec couche de fiabilité

**En tant que** développeur du moteur, **je veux** un protocole UDP avec 3 canaux, **afin que** les données critiques soient garanties et les données fréquentes soient rapides.

**Critères d'acceptation :**
- **Given** un paquet fiable perdu, **When** pas d'ACK dans 500ms, **Then** retransmission automatique
- **Given** le canal non-fiable, **When** 100 paquets position envoyés, **Then** zéro overhead d'ACK

**Complexité :** XL (8-13 jours) · **Stack :** Unity Transport Layer, channels custom, sérialisation binaire

---

### Story 9.2 : Authentification et gestion des joueurs

**En tant que** administrateur de serveur, **je veux** que les joueurs s'authentifient, **afin de** protéger les comptes et privilèges.

**Critères d'acceptation :**
- **Given** un nouveau joueur, **When** première connexion, **Then** compte créé avec hash SRP du mot de passe
- **Given** un joueur existant, **When** connexion avec bon mot de passe, **Then** accès autorisé et privilèges chargés
- **Given** un joueur banni, **When** tentative de connexion, **Then** refusé avec message
- **Given** la base auth, **When** le serveur sauvegarde, **Then** les données sont dans une table SQLite séparée (`auth` + `user_privileges`), distincte de la world database
- **Given** les privilèges par défaut configurés (`interact`, `shout`), **When** un nouveau joueur rejoint, **Then** il reçoit ces privilèges

**Complexité :** L (5-8 jours) · **Stack :** SRP auth, SQLite table `auth` (name, password_hash) + `user_privileges` (name, privilege), ban list

**Ref Luanti :** `References/luanti/src/database/database-sqlite3.h` — `AuthDatabaseSQLite3`

---

### Story 9.3 : Synchronisation du monde

**En tant que** joueur client, **je veux** recevoir les chunks et voir les modifications des autres, **afin que** le monde soit cohérent.

**Critères d'acceptation :**
- **Given** un client connecté, **When** je me déplace, **Then** le serveur envoie les chunks dans ma draw distance
- **Given** un autre joueur qui casse un bloc, **When** à portée, **Then** modification visible en < 200ms
- **Given** un chunk envoyé, **When** reçu, **Then** les nœuds sont désérialisés et le mesh est généré

**Complexité :** XL (8-13 jours) · **Stack :** Chunk serialization + zlib, delta updates

---

### Story 9.4 : Transfert de médias

**En tant que** joueur client, **je veux** recevoir automatiquement les textures et sons des mods, **afin de** voir le contenu custom.

**Critères d'acceptation :**
- **Given** un serveur avec 50 textures custom, **When** un client se connecte, **Then** toutes sont transférées avant le spawn
- **Given** un serveur HTTP configuré, **When** le client détecte le support, **Then** les médias sont téléchargés par HTTP

**Complexité :** L (5-8 jours) · **Stack :** Media hash index, HTTP fetch optionnel, `Texture2D.LoadImage()`

---

### Story 9.5 : Synchronisation des entités et inventaires

**En tant que** joueur, **je veux** voir les mobs et les changements d'inventaire des autres joueurs, **afin que** le multijoueur soit cohérent.

**Critères d'acceptation :**
- **Given** un mob visible par 2 joueurs, **When** il se déplace côté serveur, **Then** les deux clients voient le mouvement
- **Given** un joueur qui prend un item dans un coffre, **When** l'inventaire du coffre change, **Then** les autres joueurs qui ont le formspec ouvert voient la mise à jour
- **Given** un joueur qui pose un bloc, **When** l'action est validée par le serveur, **Then** tous les joueurs à portée voient le nouveau bloc

**Complexité :** L (5-8 jours) · **Stack :** Entity state sync, inventory change notifications, `TOCLIENT_BLOCKDATA`
