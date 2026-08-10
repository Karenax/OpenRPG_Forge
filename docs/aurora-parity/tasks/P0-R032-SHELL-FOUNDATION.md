# P0 R032 — fondation du shell Creator

Lot stable : `R032` / `FC-SHELL-P0-01` — cinq fonctions, aucune fonction hors lot.

## Périmètre et source auditée

- `ORF-SHELL-001` — Disposition multi-panneaux du Toolset.
- `ORF-SHELL-002` — Explorateur hiérarchique du projet.
- `ORF-SHELL-003` — Vue 3D principale.
- `ORF-SHELL-004` — Palette contextuelle.
- `ORF-SHELL-005` — Palettes Standard et Custom.
- Source immuable relue intégralement : sections 56–380 de
  `Divers/OpenRPG_Forge_Aurora_Feature_Dossier/docs/requirements/01_SHELL.md`.

## Audit avant fermeture

| Fonction | Déjà présent | Limite réelle à fermer |
|---|---|---|
| Disposition multi-panneaux | shell redimensionnable explorer/viewport/contexte, inspecteur, diagnostics, playtests, douze workspaces et sélection partagée | dimensions, visibilité et surfaces palette/assistant ne sont pas réunies dans un profil canonique configurable et restauré |
| Explorateur hiérarchique | arbre virtuel stable par zones, entités, types de blueprints, dialogues et quêtes | aucune vue sélectionnable par dossier, tag, dépendance ou statut de validation |
| Vue 3D principale | viewport intégré, caméra complète, édition directe, rendu, navigation, collisions, budgets et playtest | les états édition/simulation/éclairage/navigation/collisions/performance ne forment pas un mode explicite visible et persisté |
| Palette contextuelle | recherche, origine, favoris projet/personnels, récents, auteur, source, licence et type | filtres kit, style, compatibilité et tags absents du contrat et de l'écran |
| Palettes Standard et Custom | les cinq origines pack/projet/généré/temporaire/favori, lecture seule Standard, remplacement gouverné, favoris personnels et récents sont maintenant livrés par R030/R031 | rattacher cette distinction au profil/filtres unifiés et prouver à nouveau découverte, persistance, historique et état vide/erreur |

## Contrat minimal de fermeture

- ajouter au profil d'espace de travail un contrat shell v1 avec UUID, schéma, provenance, dimensions,
  visibilités, vue explorateur, mode viewport et filtre de palette ;
- appliquer ces réglages au shell, à l'explorateur, au viewport et au catalogue sans mutation parallèle ;
- construire les six vues virtuelles de l'explorateur avec clés uniques et chemins sélectionnables ;
- étendre les métadonnées auteur des blueprints à kit/style/compatibilité/tags et filtrer réellement ;
- prouver commande/dry-run, undo/redo, round-trip, diagnostics localisés, smoke Toolset et portail global.

## Non-objectifs

- sélection/gizmos/undo globaux de R033, recherche et build de R034/R035, R036+, P1/P2, AV7–AV10,
  commit ou push Git.

## Fermeture prouvée

Les cinq fonctions sont `Tested`. Le profil d'espace de travail schéma v3 contient désormais un
contrat shell v1 identifié et sourcé : tailles et visibilité des panneaux, six regroupements de
l'explorateur, six modes explicites du viewport et filtre contextuel de palette. L'écran Creator
modifie ce contrat avec validation localisée, aperçu dry-run, application, annuler/rétablir et
round-trip ; le shell, l'arbre, le viewport et le catalogue consomment exactement le même état.

Les métadonnées auteur de blueprint portent kit, styles, compatibilité et tags et les combinent avec
type, licence et favoris personnels. Les origines pack installé, projet, généré, favori/récent et
temporaire restent distinctes et gouvernées. Preuves ciblées : 11 tests Domain, 23 Application et 3
ModuleFormat ; suites complètes concernées à 319/328/256 ; build des 27 projets sans avertissement ;
smoke Toolset `OPENRPG_FORGE_TOOLSET_R032_SHELL_FOUNDATION_PASS`. Le portail intégré final est
consigné dans `STATUS.md` après régénération du tracker, du backlog et du graphe.
