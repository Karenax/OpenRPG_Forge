# ADR-0049 — Frontière entre Forge et les jeux modules

Date : 25 août 2026
Statut : accepté

## Contexte

La première zone de Solaren a révélé plusieurs besoins légitimes : réglage fin du rendu, pluie
verticale, garde au sol de la caméra, vitesse du personnage et sélection explicite des clips
d'animation. Une première implémentation les avait en partie placés dans des branches runtime et
dans un générateur `author-solaren` de `Forge.Application`/`Forge.Cli`. Le jeu et l'outil devenaient
alors bidirectionnellement dépendants : changer le village pouvait modifier le cœur et rendre le
graphe d'architecture périmé, tandis qu'une régénération pouvait écraser les retouches du Creator.

## Décision

- Forge fournit des contrats génériques, versionnés et éditables : profils de rendu, précipitations,
  caméra, mouvement, animation, terrain, environnement et autres capacités réutilisables.
- Un jeu module référence ces contrats et porte toutes ses valeurs dans son workspace. Le workspace
  sauvegardé par le Creator autonome est sa source de vérité.
- Le runtime partagé interprète uniquement les contrats. Il ne contient aucune condition de production
  fondée sur `solaren`, sur un slug de jeu, sur un tag réservé ou sur le nom d'un pack d'assets.
- Si un jeu nécessite une capacité absente, celle-ci est ajoutée verticalement au domaine, à la
  validation, aux adapters/runtime et au Creator, puis testée comme capacité générique.
- Les générateurs de vrais jeux spécifiques au cœur sont interdits. Les scripts propres à un jeu
  peuvent orchestrer les commandes publiques de validation, de préparation d'assets et de packaging,
  mais ils ne peuvent pas devenir une deuxième source de vérité de sa carte.
- Les workspaces sous `apps/Forge.Runtime/content/` sont exclus du graphe d'architecture. Leur porte
  de validation est `forge validate`, complétée par le build du package et les playtests du module.
  Les changements de contrats, schémas, runtime et Creator restent indexés dans le graphe.
- Un test ou une fixture visuelle peut nommer Solaren ou la Crypte afin de vérifier le contenu attendu.
  Cette exception concerne uniquement l'assertion de test et n'autorise aucun comportement produit
  spécifique dans le chemin d'exécution normal.

## Application initiale

- suppression du service `SolarenModuleAuthoringService`, de la commande `author-solaren` et de
  l'installateur spécifique associé ;
- remplacement des branches Solaren par des profils génériques de rendu, précipitations, caméra,
  mouvement et clips d'animation exposés dans Forge Studio ;
- conservation de la Crypte des Cendres comme workspace de régression indépendant ;
- validation et packaging de Solaren directement depuis son workspace canonique.

## Conséquences

Créer ou retoucher un village, une météo ou un équilibrage de jeu ne demande plus de modifier le
cœur de Forge. Une évolution fonctionnelle réellement nouvelle coûte davantage qu'un branchement
ponctuel, car elle doit être conçue comme contrat réutilisable et recevoir une interface Creator,
mais elle reste testable, sérialisable et disponible pour tous les modules. Le graphe décrit ainsi
l'architecture du produit et non chaque placement d'herbe d'un jeu.

## Remplacement

Cette décision ne remplace aucun ADR. Elle précise l'architecture obligatoire et complète
l'ADR-0045 sur le Creator autonome.
