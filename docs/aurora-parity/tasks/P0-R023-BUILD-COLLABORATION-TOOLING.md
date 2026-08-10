# P0 R023 — outillage de build collaboratif

Lot stable : `R023` / `FC-BUILD-P0-02` — cinq fonctions, aucune fonction hors lot.

## Périmètre et sources auditées

- `ORF-BUILD-006` — Cache jetable.
- `ORF-BUILD-008` — Normalisation des documents.
- `ORF-BUILD-009` — Migrations.
- `ORF-BUILD-010` — Références inverses.
- `ORF-BUILD-011` — Diff sémantique.
- Source immuable relue intégralement : sections correspondantes de
  `Divers/OpenRPG_Forge_Aurora_Feature_Dossier/docs/requirements/25_BUILD.md`.

## Matrice d’audit avant implémentation

| Critère source | Présent | Limite réelle à fermer | Preuve attendue |
|---|---|---|---|
| Cache explicite, adressé par contenu, invalidable, reconstructible et jamais canonique | `.forge`, état de build et cache de package hashé existent | Pas de politique canonique globale ni de purge/reconstruction structurée | Profil v1, inventaire hashé, invalidation confinée, cache absent sans impact source/runtime |
| Sérialiseur canonique, LF, ordre de propriétés, nombres et `forge normalize` | Les stores JSON sont déterministes et le projet possède un sérialiseur canonique | Pas de commande normalize ni de rapport preview/apply | Normaliseur JSON borné, preview sans écriture, écriture atomique, rapport texte/JSON |
| Migrations versionnées, dry-run, sauvegarde, idempotence, fixtures et downgrade si possible | Migrations de lecture et migrateur de workspace existent | Pas de plan versionné transversal, backup ni preuve explicite d'idempotence/downgrade | Catalogue de pas, plan dry-run, sauvegarde avant apply, seconde exécution vide, downgrade déclaré |
| Graphe indexé et reconstructible, `forge references`, UI et MCP, références inverses | Graphe de build avant et graphe agent typé couvrent de nombreux documents | CLI n'émet que les dépendances avant et le cache/configuration ne sont pas explicites | Index source stable forward/reverse, recherche de dépendants, rebuild sans source dérivée requise, UI/MCP |
| Diff domaine zones, graphes, entités, packages et scripts en texte/JSON | Patches métier et diff agent R021 existent | `forge diff` n'est qu'un delta de build et ne décrit pas les domaines demandés | Snapshot sémantique typé, added/removed/modified ordonné, texte et JSON, CLI/MCP/UI |
| Configuration UI, round-trip, diagnostics, commande unique, undo/redo | Profil de build R001 et écran pipeline sont transactionnels | Les politiques ci-dessus ne sont pas encore configurables ni fingerprintées | Sous-profil canonique, contrôles visibles, legacy sûr, même handler, undo/redo et capture |

## Contrat minimal de fermeture

- sous-profil de collaboration build v1 attaché au profil existant avec UUID, schéma, provenance et
  politiques cache/normalisation/migration/références/diff ;
- opérations sans shell arbitraire, confinées au workspace et séparant preview de apply ;
- cache reconstructible par hash et supprimable sans affecter les documents canoniques ;
- normalisation JSON déterministe LF/UTF-8 sans BOM, propriété ordonnée et écriture atomique ;
- migration planifiée avec sauvegarde, idempotence et downgrade explicitement supporté ou refusé ;
- index forward/reverse et diff sémantique multi-domaines disponibles en CLI, MCP et éditeur ;
- configuration persistante par la commande de profil existante, validation localisée, undo/redo,
  capture et smokes Toolset/runtime.

## Risques et tests exacts

- chemin cache absolu, traversal, symlink/reparse, collision avec une source et purge hors racine ;
- JSON invalide, BOM/CRLF, ordre instable, nombres non finis, preview qui écrit ou second passage différent ;
- migration inconnue, future schema, backup manquant, apply partiel, seconde exécution non vide,
  downgrade indisponible ;
- référence manquante, cycle, nœud inconnu, ordre instable et index supprimé/reconstruit ;
- diff ajouté/supprimé/modifié sur zone, graphe, entité, package et script, formats texte/JSON ;
- profil legacy sans collaboration, round-trip exact, commande dry-run/apply/undo/redo, MCP borné,
  marqueurs Toolset/runtime, capture inspectée et portail global.

## Non-objectifs

- import/export P1, cloud, Git distant, P1/P2, AV7–AV10, commit ou push Git.

## Clôture vérifiée

Les cinq fonctions ont été réauditées séparément contre toutes les phrases de la source. Les limites
réelles ont été fermées sans modifier les fonctions P1/P2 :

| Feature | Preuve spécifique de fermeture |
|---|---|
| `ORF-BUILD-006` | Le profil v1 impose un cache explicite, adressé par contenu, borné, invalidable, jamais canonique et reconstructible. L'index SHA-256 et la purge confinée ne modifient aucun document source. |
| `ORF-BUILD-008` | `forge normalize` prévisualise par défaut puis applique une transaction multi-document restaurée sur erreur : JSON canonique, ordre stable, nombres stables, UTF-8 sans BOM et LF. Le second passage est vide. |
| `ORF-BUILD-009` | `forge migrate` produit un plan sans écriture, refuse les downgrades non supportés, borne les sauvegardes, restaure sur erreur/non-idempotence et prouve une seconde lecture stable. |
| `ORF-BUILD-010` | Le même graphe UUID reconstruit les dépendances avant et inverses, avec ordre/limites stables et accès éditeur, CLI JSON et MCP sans shell. |
| `ORF-BUILD-011` | Le snapshot jetable et le diff stable couvrent `Added/Removed/Modified` pour zones, graphes dialogue/quête, entités, packages et scripts, en texte et JSON via CLI/MCP/UI. |

Preuves exécutées :

- Domain `WorkspaceBuildProfileValidatorTests` : 4/4 ;
- Application `WorkspaceBuildProfileCommandTests` : 4/4, dont preview/apply/undo/redo ;
- ModuleFormat collaboration + round-trip/legacy du profil : 11/11 ;
- MCP `BuildAgentServiceTests` : 1/1 ;
- CLI réelle sur `crypt-of-ashes` : `normalize`, `migrate`, `references`, `diff`, sorties JSON et code 0,
  sans `--apply` ;
- capture inspectée : `docs/aurora-parity/evidence/r023-build-collaboration-ui.png`, 1440×980,
  champs, politiques, références inverses, diff, états et boutons visibles sans débordement ;
- smokes : `OPENRPG_FORGE_TOOLSET_R023_BUILD_COLLABORATION_PASS` et
  `OPENRPG_FORGE_RUNTIME_R023_BUILD_COLLABORATION_PASS` ;
- portail intégré : 27 projets, 0 avertissement/erreur, 1 203 tests .NET, 6 tests Python généraux,
  33 tests Python assets, graphe déterministe à 5 834 nœuds / 35 065 relations / 0 diagnostic et
  `OPENRPG_FORGE_VERIFICATION=PASS`.

Décision : `ORF-BUILD-006`, `008`, `009`, `010` et `011` peuvent chacune passer de `Implemented`
à `Tested`. Aucun autre statut n'est promu par ce lot.
