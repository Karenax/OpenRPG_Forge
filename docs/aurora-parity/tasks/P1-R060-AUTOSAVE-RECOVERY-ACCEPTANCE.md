# P1 R060 — Autosauvegarde et récupération après interruption

## Périmètre

- `ORF-SHELL-024` — autosave versionné, journal de commandes, détection d’une session interrompue,
  snapshots contrôlés par hash, comparaison puis restauration complète ou sélective.
- Dépendance traversante minimale : `BUILD` pour la validation du profil avant écriture/export.
- Un seul lot actif ; R061, P2, AV7–AV10, commit et push restent hors périmètre.

## Audit intégral des critères source

| Critère source | État avant R060 | Limite réelle à fermer et preuve attendue |
|---|---|---|
| Domaine canonique UUID/schéma/provenance | Présent | conserver `CrashRecoveryPolicy` et typer la sélection de restauration |
| Autosave versionné | Présent | conserver écriture atomique, hash, révision et rétention bornée |
| Journal de commandes | Présent à traverser | toute configuration et restauration passe par `ProjectCommandHistory` |
| Récupération après crash | Partiel | détecter une session précédente non close par marqueur local atomique |
| Snapshots intègres | Présent | inspection, quarantaine, confinement et refus du hash altéré |
| Comparaison avant restauration | Partiel | séparer aperçu et application, afficher sections et compteurs avant/après |
| Restauration sélective | Manquant | choisir module, blueprints, zones, dialogues et quêtes avec validation croisée |
| Interface sans JSON | Partiel | exposer politique, état session, sélection, aperçu, erreurs et actions explicites |
| Commande et historique | Manquant pour restore | dry-run/apply atomiques, patch sémantique, undo/redo du document canonique |
| Persistance exacte | Partiel | round-trip explicite d’une politique configurée sans perte |
| CLI/build | Présent à traverser | profil invalide refusé par le validateur projet avant build |
| MCP et agents | Présent à traverser | ressource runtime filtrée et plan/apply/revert sans pilotage UI |
| Runtime optionnel/requis | Partiel | absence optionnelle ignorée et absence requise diagnostiquée |

## Non-objectifs

- sauvegarde cloud, synchronisation multi-utilisateur ou résolution de conflits distribuée ;
- fusion champ par champ arbitraire à l’intérieur d’un graphe ou d’un script ;
- restauration automatique sans confirmation humaine ;
- modification du format canonique des sauvegardes de partie runtime.

## Validations prévues

- tests Domain/Application/ModuleFormat/MCP ciblés puis suites complètes ;
- smoke Toolset R060 couvrant politique, session interrompue, snapshot, comparaison, sélection,
  dry-run/apply, undo/redo, corruption et persistance ;
- smoke runtime optionnel/requis ;
- capture UI R060 inspectée avec politique, sections et état vide/erreur explicites ;
- tracker/backlog/docs/graphe frais puis `scripts/Verify.ps1` avant R061.

## Résultat de clôture

- `ORF-SHELL-024` est `Tested` individuellement, sans promotion hors R060.
- La politique conserve UUID, schéma et provenance ; intervalle, rétention, proposition à l’ouverture,
  quarantaine et comparaison obligatoire sont configurables sans JSON par la commande canonique.
- Un marqueur atomique local distingue première ouverture, fermeture propre, session encore ouverte
  et marqueur corrompu ; une interruption propose la récupération même sans snapshot disponible.
- Chaque snapshot reste hors `project.json`, porte révision et SHA-256, suit un journal versionné,
  une rétention bornée, un confinement de chemin et une quarantaine des copies altérées.
- L’auteur compare avant/après puis choisit projet/module, blueprints, zones, dialogues et quêtes ;
  le merge est validé avant mutation et s’applique en mémoire par dry-run/apply avec undo/redo.
- La ressource et les plans MCP runtime-extension exposent la même politique avec
  plan/apply/revert ; le runtime distingue absence optionnelle et extension requise manquante.
- La preuve visuelle inspectée est `docs/aurora-parity/evidence/r060-autosave-recovery-ui.png` en
  1180×760 ; politique, snapshots réels, cinq sections, session et actions y sont entièrement visibles.
- Les suites complètes passent à 1 429 tests .NET et 39 tests Python. Le graphe Roslyn est frais et
  déterministe à 6 536 nœuds / 41 002 relations.
- Le portail intégré termine sur `OPENRPG_FORGE_VERIFICATION=PASS` après smokes Toolset/runtime,
  MCP, Blender, assets, packaging, réseau et sauvegarde/reprise.
