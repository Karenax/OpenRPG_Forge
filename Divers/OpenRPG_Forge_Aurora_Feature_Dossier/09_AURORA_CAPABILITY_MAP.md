# Carte globale des capacités

Le catalogue contient **717 exigences** dans **26 domaines**.

| Code | Domaine | Couche | Total | P0 | P1 | P2 | P3 | P4 |
|---|---|---|---:|---:|---:|---:|---:|---:|
| `SHELL` | Coque du Toolset et ergonomie d’édition | Forge Core | 24 | 17 | 6 | 1 | 0 | 0 |
| `PROJECT` | Projet, module et cycle de vie | Forge Core | 25 | 11 | 8 | 6 | 0 | 0 |
| `AREA` | Zones, construction 3D et environnement | Forge Core | 37 | 9 | 16 | 12 | 0 | 0 |
| `PALETTE` | Palettes, blueprints, prefabs et catalogue d’assets | Forge Core | 18 | 8 | 8 | 2 | 0 | 0 |
| `CREATURE` | Créatures, PNJ et agents | Forge RPG Kit | 49 | 8 | 20 | 17 | 4 | 0 |
| `FACTION` | Factions, réputation et hostilité | Forge RPG Kit | 14 | 1 | 9 | 3 | 1 | 0 |
| `DIALOGUE` | Conversations, dialogues et narration interactive | Forge RPG Kit | 32 | 10 | 15 | 6 | 1 | 0 |
| `QUEST` | Journal, quêtes, objectifs et Plot Wizard | Forge RPG Kit | 25 | 8 | 12 | 5 | 0 | 0 |
| `ITEM` | Objets, inventaire, équipement, butin et artisanat | Forge RPG Kit | 32 | 4 | 13 | 13 | 2 | 0 |
| `PLACEABLE` | Placeables, conteneurs, pièges et objets interactifs | Forge Core + RPG Kit | 27 | 7 | 13 | 6 | 1 | 0 |
| `DOOR` | Portes, portails, serrures et transitions | Forge Core + RPG Kit | 21 | 7 | 6 | 7 | 1 | 0 |
| `TRIGGER` | Triggers, volumes, secrets et zones d’interaction | Forge Core + RPG Kit | 20 | 6 | 4 | 9 | 1 | 0 |
| `ENCOUNTER` | Rencontres, spawns et vagues | Forge RPG Kit | 20 | 3 | 11 | 5 | 1 | 0 |
| `WAYPOINT` | Waypoints, patrouilles, ancres et navigation | Forge Core | 17 | 4 | 9 | 3 | 1 | 0 |
| `AUDIO` | Audio, musique, ambiance et voix | Forge Core | 20 | 5 | 11 | 4 | 0 | 0 |
| `MERCHANT` | Marchands, magasins et économie | Forge RPG Kit | 19 | 2 | 8 | 7 | 2 | 0 |
| `COMPANION` | Compagnons, groupes et contrôle de parti | Forge RPG Kit | 24 | 1 | 13 | 10 | 0 | 0 |
| `SCRIPT` | ForgeScript, événements et API de gameplay | Forge Core + Kits | 41 | 16 | 21 | 3 | 1 | 0 |
| `GAMEPLAY` | Runtime général, mouvement, caméra, interaction et sauvegarde | Forge Core | 29 | 7 | 10 | 8 | 4 | 0 |
| `DM` | Maître de jeu en direct et administration de session | Forge Live Director Kit | 37 | 0 | 5 | 27 | 5 | 0 |
| `MULTI` | Multijoueur, serveur et mondes persistants | Forge Network Kit | 36 | 0 | 13 | 17 | 5 | 1 |
| `CONTENT` | Contenu personnalisé, paquets, localisation et pipeline d’assets | Forge Core | 31 | 14 | 12 | 4 | 1 | 0 |
| `EE` | Capacités modernes inspirées d’Enhanced Edition | Forge Core + Kits | 31 | 3 | 19 | 9 | 0 | 0 |
| `AI` | IA de création et MCP transactionnel | Forge AI | 33 | 14 | 15 | 3 | 1 | 0 |
| `BUILD` | Build, collaboration, versionnement et automation | Forge Core Tooling | 25 | 17 | 6 | 2 | 0 | 0 |
| `QA` | Validation, tests, profilage, accessibilité et qualité | Forge Core Tooling | 30 | 14 | 15 | 1 | 0 | 0 |

## Lecture architecturale

### Forge Core

SHELL, PROJECT, AREA, PALETTE, PLACEABLE partiel, DOOR partiel, TRIGGER partiel, WAYPOINT, AUDIO, SCRIPT, GAMEPLAY, CONTENT, BUILD et QA.

### Forge RPG Kit

CREATURE, FACTION, DIALOGUE, QUEST, ITEM, rencontres, magasins, compagnons et parties des objets interactifs.

### Forge Network Kit

MULTI et politiques réseau des autres domaines.

### Forge Live Director Kit

DM, construit sur Network Kit et les commandes de domaine.

### Forge AI

AI, construit sur l’index, les commandes, la validation et les tests existants.

## Couverture recherchée

- **P0** : fondations nécessaires avant de parler de parité.
- **P1** : expérience Aurora moderne complète pour le premier produit RPG.
- **P2** : fonctions avancées, EE et persistent-world.
- **P3/P4** : extensions générales, outils studio ou recherche longue durée.
