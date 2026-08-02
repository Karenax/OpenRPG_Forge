# Synthèse exécutive — parité de capacités Aurora

Ce dossier transforme l’étude d’Aurora en exigences directement exploitables pour OpenRPG Forge.

## Résultat principal

L’écosystème Aurora n’est pas seulement un éditeur de niveaux. Il combine :

- un **Toolset** de construction de modules ;
- des **blueprints** et instances pour chaque famille d’objet ;
- un **éditeur de conversations** conditionnelles ;
- un **journal de quêtes** ;
- un **langage événementiel C-like** ;
- des systèmes de **factions, compagnons, marchands, rencontres, pièges et transitions** ;
- un **runtime multijoueur** ;
- un **Dungeon Master Client** capable d’intervenir en direct ;
- des formats et outils de **contenu personnalisé** ;
- puis, avec Enhanced Edition et la communauté, des capacités modernes de rendu, UI scriptable, JSON, distribution, serveur persistant, pipelines Git et Blender.

Le catalogue contient **717 exigences granulaires** réparties en **26 domaines**. Chaque exigence indique la référence Aurora, l’adaptation Forge, la priorité, la couche, les données, l’UI, le contrat ForgeScript, le contrat MCP, les validations et les critères d’acceptation.

## Architecture produit résultante

```text
OpenRPG Forge Core
├── projets, zones, entités, blueprints et assets
├── éditeur 3D, commandes, undo/redo et build
├── mouvement 3D, physique, caméra et interaction
├── sauvegarde, packages et ForgeScript
└── tests, validation et MCP de base

Forge RPG Kit
├── créatures et règles
├── factions et réputation
├── dialogues et quêtes
├── objets, inventaire, loot et artisanat
├── rencontres, magasins et compagnons
└── UI RPG

Forge Network Kit
├── serveur autoritaire
├── parties, personnages et réplication
├── synchronisation de contenu
├── persistance et administration
└── observabilité

Forge Live Director Kit
├── client GM
├── création à chaud
├── possession et impersonation
├── commandes de groupes et joueurs
└── audit

Forge AI
├── ressources MCP
├── commandes sémantiques
├── plans/dry-run/transactions
├── génération de contenu
└── agents de test
```

## Séquence recommandée

1. maintenir le vertical slice existant ;
2. consolider Core project/area/palette/commands ;
3. livrer ForgeScript sûr ;
4. livrer dialogues + quêtes ;
5. compléter créatures/items/placeables/doors/triggers/encounters ;
6. intégrer MCP transactionnel ;
7. ajouter RPG party/merchant/faction ;
8. construire Network Kit ;
9. construire Live Director ;
10. seulement ensuite poursuivre les extensions généralistes, véhicules, espace et génération 3D avancée.

## Décision de gouvernance

Le terme « parité Aurora » signifie :

> un auteur peut réaliser l’intention fonctionnelle sans dépendre de Neverwinter Nights, de ses formats ou de ses règles propriétaires.

Il ne signifie pas compatibilité binaire, import automatique d’assets protégés, reproduction de NWScript ou copie de l’interface.
