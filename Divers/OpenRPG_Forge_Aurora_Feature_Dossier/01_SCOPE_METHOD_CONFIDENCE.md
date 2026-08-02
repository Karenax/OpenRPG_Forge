# Périmètre, méthode et niveau de confiance

**Version du dossier : 1.0**  
**Date de recherche : 2026-08-02**

## 1. Question traitée

Ce dossier répond à la question suivante : quelles capacités de création, d’exécution, de scripting, de multijoueur et de direction en direct ont constitué l’écosystème Aurora de Neverwinter Nights, et comment les réinterpréter proprement dans OpenRPG Forge ?

Il ne cherche pas à reproduire l’interface pixel par pixel, les formats propriétaires, les règles D&D, les noms d’API NWScript ou les assets de Neverwinter Nights. Il définit une **parité de capacités**, pas une copie.

## 2. Pourquoi « exhaustif » possède une limite

Aurora permet d’associer des scripts à de nombreux événements et NWScript expose une grande API. Une combinaison de scripts peut créer une quantité ouverte de comportements. Il est donc impossible de dresser une liste finie de toutes les aventures ou mécaniques imaginables.

L’inventaire est considéré complet au niveau utile pour l’ingénierie lorsqu’il couvre :

- la coque du Toolset et ses workflows ;
- les catégories d’objets éditables ;
- leurs groupes de propriétés ;
- les événements documentés ;
- les assistants officiels ;
- le journal, les conversations, les factions et les compagnons ;
- le Dungeon Master Client ;
- les familles d’API de script ;
- les évolutions significatives d’Enhanced Edition ;
- les fonctions de production devenues standard grâce à l’écosystème communautaire ;
- les modernisations indispensables pour un produit de 2026.

## 3. Niveaux de source

| Niveau | Signification | Usage dans la spécification |
|---|---|---|
| A | Manuel ou patch officiel | Fait de référence pour la capacité native ou EE |
| B | Manuel communautaire détaillé et recoupé | Détail de propriétés historiques lorsque le manuel officiel est trop synthétique |
| C | Outil/projet communautaire documenté | Inspiration d’écosystème; ne pas appeler cela une fonction native d’Aurora |
| D | Conception OpenRPG Forge | Modernisation ou capacité nouvelle, clairement étiquetée |

## 4. Découpage historique

### Aurora original

Toolset de modules, construction de zones par tileset, palettes de blueprints, créatures, conversations, journal, objets, placeables, portes, rencontres, sons, triggers, waypoints, marchands, scripts événementiels et Dungeon Master Client. Sources principales : S01 et S03.

### Extensions officielles

Shadows of Undrentide et Hordes of the Underdark ont ajouté ou popularisé assistants de scénario et de magasin, waypoint walking, bulk import/export, sous-systèmes de trésor, compagnons et commutateurs de comportement. Sources : S02 et sections correspondantes de S03.

### Enhanced Edition

L’EE a amélioré Toolset, rendu, pathfinding, transformations, LOD, distribution de contenu, NUI, JSON, sérialisation, création dynamique, classes data-driven, profiling, logs et qualité de vie. Sources : S04 à S14.

### Écosystème communautaire

NWNX, Nasher, neverwinter.nim, NeverBlender, Radoub et les frameworks d’événements montrent les besoins réels de persistent worlds, collaboration Git, formats textuels, pipeline Blender et extensibilité serveur. Sources : S16 à S20 et S23. Ces fonctions ne sont pas attribuées au Toolset natif.

## 5. Règles de traduction vers Forge

1. **Capacité, pas copie.** Reprendre le besoin de l’auteur, pas le nom, le format ou l’implémentation propriétaire.
2. **Core minimal et Kits.** La scène, les entités, le mouvement, les assets, les scripts et les sauvegardes appartiennent au Core. Dialogues, quêtes, inventaires, factions et règles appartiennent au RPG Kit. Multijoueur, Live Director et IA sont des kits/services séparés.
3. **Données canoniques indépendantes de Godot.** Les ressources Godot sont des adaptateurs ou caches.
4. **Commandes uniques.** UI, CLI et MCP utilisent les mêmes commandes de domaine.
5. **Script sandboxé.** ForgeScript ne reçoit que les fonctions enregistrées et versionnées.
6. **Multijoueur autoritaire.** Le serveur valide toutes les mutations sensibles.
7. **Droits connus.** Aucun asset ou contenu NWN/CEP n’est redistribué sans droit explicite.
8. **Preuves.** Une feature n’est terminée qu’avec tests et lien de preuve dans le tracker.

## 6. Signification des priorités

- **P0** — Fondation ou parité indispensable à la première tranche verticale. Ne pas repousser sans ADR.
- **P1** — Fonction majeure nécessaire à un Toolset RPG complet et crédible.
- **P2** — Fonction avancée d’Aurora/EE ou modernisation importante après le vertical slice.
- **P3** — Extension facultative, spécialisation de kit ou fonction de production avancée.
- **P4** — Recherche longue durée; explicitement hors des premiers jalons.

## 7. Utilisation correcte par Codex

Le catalogue n’est pas un ordre de développer toutes les fonctionnalités en une passe. Codex doit sélectionner un epic borné, lire les features associées, produire un ADR si nécessaire, implémenter les contrats minimaux, exécuter les tests, puis mettre à jour la couverture.
