# P0 R033 — sélection et manipulation directe

Lot stable : `R033` / `FC-SHELL-P0-02` — cinq fonctions, aucune fonction hors lot.

## Périmètre et source auditée

- `ORF-SHELL-006` — Sélection et inspection.
- `ORF-SHELL-007` — Navigation caméra d'édition.
- `ORF-SHELL-010` — Retour visuel de placement invalide.
- `ORF-SHELL-011` — Transformation avec gizmos.
- `ORF-SHELL-013` — Undo et redo complets.
- Source immuable relue intégralement : sections 381–506, 641–769 et 839–899 de
  `Divers/OpenRPG_Forge_Aurora_Feature_Dossier/docs/requirements/01_SHELL.md`.

## Audit avant fermeture

| Fonction | Déjà présent | Limite réelle à fermer |
|---|---|---|
| Sélection et inspection | contexte partagé simple/multiple, verrouillage, navigation arbre/diagnostics, sélection viewport et inspecteur simple | la sélection n'a pas de contrat UUID/schéma/provenance, cadre/focus explicite ni projection des propriétés communes et commande multi-objet |
| Caméra d'édition | profil canonique, souris, vitesse adaptative, focus, vues orthographiques, bookmarks, WASD, persistance, dry-run et historique | bindings encore codés dans le viewport, déplacement pilote sans axe vertical et bookmarks non partageables |
| Placement invalide | fantôme vert/rouge, grille, limites, collision, code et correction bornée | sockets, budget de blueprint et recherche automatique d'une cellule libre absents du même validateur |
| Gizmos | translation X/Z, rotation 90°, échelle uniforme/non uniforme, saisie numérique, grille et commandes atomiques | pas de contrat unifié translation/rotation/échelle/pivot, repère local/global ni alignements multi-objet |
| Undo/redo | historique canonique partagé avec patches inverses pour les commandes projet et historiques spécialisés | entrées sans enveloppe sérialisable UUID/schéma/provenance et absence de pont avec l'historique natif de l'éditeur |

## Contrat minimal de fermeture

- ajouter des contrats versionnés de sélection/inspection et de manipulation transform, avec projection des
  propriétés communes et batch atomique multi-entité ;
- rendre bindings, mode pilote vertical et partage des bookmarks configurables dans le profil caméra ;
- étendre le validateur de placement aux sockets, budget et correction automatique déterministe ;
- conserver un journal sérialisable des commandes et synchroniser les commandes projet avec l'historique
  undo/redo natif du Toolset sans muter la scène seule ;
- prouver dry-run, diagnostics localisés, persistance, interface, undo/redo, smoke Toolset et portail global.

## Non-objectifs

- clipboard/recherche/build de R034/R035, R036+, P1/P2, AV7–AV10, commit ou push Git.

## Fermeture et preuves

Les cinq limites ont été fermées sans ouvrir le lot suivant :

- la sélection viewport accepte Ctrl pour agréger plusieurs entités, conserve focus, cadrage et
  verrouillage, et projette dans l'inspecteur les propriétés communes ou mixtes ;
- le profil caméra porte un mode pilote libre, sept bindings configurables, l'axe vertical et le
  choix de partage des bookmarks, tout en conservant son historique et sa persistance dédiés ;
- le même validateur retourne une cause stable pour limites, collision, grille, socket et budget,
  puis propose une cellule libre déterministe sans l'appliquer silencieusement ;
- une commande versionnée unique applique translation, rotation, échelle, pivot, repère, snaps,
  saisie numérique et alignements à une sélection complète de façon atomique ;
- les commandes projet humaines et assistées rejoignent le journal sérialisable et le pont
  `EditorUndoRedoManager`, avec patches inverses et persistance atomique.

Preuves exécutées : suites Domain/Application/ModuleFormat à 320/336/258 tests, formatage Roslyn,
smoke Toolset `OPENRPG_FORGE_TOOLSET_R033_DIRECT_MANIPULATION_PASS`, régénération du tracker, du
backlog, de la documentation et du graphe, puis portail `scripts/Verify.ps1` terminé sur
`OPENRPG_FORGE_VERIFICATION=PASS`.
