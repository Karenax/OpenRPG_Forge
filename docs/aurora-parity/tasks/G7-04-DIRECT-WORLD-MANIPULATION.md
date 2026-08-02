# G7-04 — Manipulation directe du Monde

Date d’exécution : 2 août 2026.

## Capacités bornées

- `ORF-SHELL-010` — retour visuel de placement invalide ;
- `ORF-SHELL-011` — transformation avec gizmos ;
- `ORF-SHELL-013` — undo et redo complets ;
- `ORF-PAL-012` — drag-and-drop.

## Résultat livré

Le viewport Monde possède une palette compacte dont les blueprints peuvent être glissés directement
sur le sol. La position projetée est alignée sur la grille puis validée par un service applicatif
indépendant du moteur : existence de la zone et du blueprint, limites de l’aire éditable et cellule
déjà occupée. Un fantôme vert ou rouge et un message codé expliquent le résultat avant mutation.

Un clic sélectionne une représentation et affiche un gizmo Forge rouge/bleu. Le glisser prépare une
translation X/Z ; la commande `transform_entity` n’est appliquée qu’au relâchement valide. Le dépôt
utilise `place_entity`. Les deux chemins conservent donc révision, patch, inverse, audit et historique
du projet canonique au lieu de modifier seulement les nœuds du moteur.

## Preuves

- `tests/Forge.Application.Tests/Spatial/PlacementValidatorTests.cs` couvre snap, cellule occupée,
  auto-exclusion de l’entité déplacée, limites et référence de blueprint manquante ;
- `OPENRPG_FORGE_TOOLSET_G704_DIRECT_PASS drag=placed gizmo=moved invalid=blocked history=restored`
  prouve dépôt, gizmo, refus visuel et séquence undo/redo dans l’éditeur headless ;
- la solution compile sans avertissement et les 148 tests .NET ainsi que les 22 tests Python sont
  verts avant le smoke du Toolset.

## Limites conservées

- le gizmo couvre la translation libre sur X/Z, pas encore les poignées d’axes contraints, rotation,
  échelle, pivot, repère local/global, alignements ou saisie multi-sélection ;
- la validation traite grille, limites et occupation simple, sans collision de forme, socket,
  navigation, licence, budget d’asset ou correction avancée ;
- le drag-and-drop cible le viewport, pas encore graphes, inventaires, propriétés ou packages ;
- l’historique applicatif est partagé mais n’est pas encore ponté vers `EditorUndoRedoManager` pour
  toutes les surfaces de l’éditeur sous-jacent ;
- la capture visuelle automatisée reste à rejouer à cause du rattachement de fenêtre Windows déjà
  documenté ; le scénario fonctionnel headless est vert.

Ces limites maintiennent les quatre capacités au statut `Implemented` partiel.
