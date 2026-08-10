# P0 R003 — diagnostics, fixtures et acceptation prouvée

Lot stable : `R003` / `FC-QA-P0-03` — quatre fonctions, aucune fonction hors lot.

## Périmètre et source auditée

- `ORF-QA-021` — Diagnostics cliquables.
- `ORF-QA-026` — Malicious module fixtures.
- `ORF-QA-029` — Coverage Aurora-parity.
- `ORF-QA-030` — Acceptance gates.
- Source immuable relue intégralement pour ces sections :
  `Divers/OpenRPG_Forge_Aurora_Feature_Dossier/docs/requirements/26_QA.md`, règles de domaine et
  sections 021, 026, 029 et 030.
- Dépendances réévaluées : `AI`, `BUILD`, `CONTENT`, `SCRIPT`, `SHELL`. R001/R002 fournissent le
  build reproductible, les fixtures de référence et la gouvernance nécessaires.

## Audit avant implémentation

| Fonction | Présent | Limite réelle à fermer |
|---|---|---|
| QA-021 | diagnostic v1 stable, navigation document/UUID/propriété, quick-fix transactionnel, CLI/MCP et smoke Toolset | catégories et profil actif non configurables dans le modèle canonique |
| QA-026 | traversal, zip bomb, DLL/hash/doublons, boucle infinie et fuzz borné sur sept surfaces | absence d’un catalogue versionné couvrant explicitement bad refs, huge arrays, injection text et invalid assets |
| QA-029 | couverture générée des 717 IDs, statuts, preuves et tests d’unicité/fraîcheur | contrat de couverture non exposé comme configuration canonique persistante et validée |
| QA-030 | gates G0–G13, backlog stable et portail intégré | règles test/documentation/preuve non exposées comme configuration canonique persistante et validée |

## Contrat minimal de fermeture

- étendre `QualityAssuranceProfile` par quatre politiques v1 à UUID/provenance stables ;
- conserver l’unique commande `SetRuntimeExtensionsCommand` pour preview, application, undo/redo,
  persistance et MCP ;
- exposer les quatre politiques dans l’écran Extensions runtime, avec état explicite et erreurs ;
- charger des valeurs sûres et déterministes lorsque l’extension optionnelle manque ;
- refuser avant build les catégories, familles, statuts, chemins, nombres ou gates invalides avec
  quatre codes et chemins stables ;
- versionner un catalogue de sept fixtures malveillantes et vérifier leurs preuves sans modifier
  les sources ;
- vérifier localement que le tracker contient exactement les 717 fonctions, que chaque statut est
  autorisé et que toute fonction `Tested` possède une preuve ;
- vérifier que la matrice de gates et le programme P0 imposent les tests et la documentation liés.

## Non-objectifs

- aucune fonction P1/P2 ;
- aucun lot AV7–AV10 ;
- aucune promotion automatique d’autres fonctions ;
- aucun shell arbitraire MCP et aucune publication distante.

## Matrice des 20 critères d’acceptation

| Fonction | Configuration sans généré | Save/close/reload | Invalide refusé | UI = commande | Undo/redo |
|---|---|---|---|---|---|
| QA-021 | profil, catégories, sévérités, quick-fix | profil JSON exact | `qa.diagnostics.profile_invalid` | handler partagé | historique existant renforcé |
| QA-026 | catalogue, sept familles et limites | profil JSON exact | `qa.malicious_fixtures.profile_invalid` | handler partagé | historique existant renforcé |
| QA-029 | tracker, statuts, total et preuves | profil JSON exact | `qa.aurora_coverage.profile_invalid` | handler partagé | historique existant renforcé |
| QA-030 | matrice, gates et preuves liées | profil JSON exact | `qa.acceptance_gates.profile_invalid` | handler partagé | historique existant renforcé |

## Preuves attendues

1. tests Domain des valeurs par défaut et des quatre diagnostics localisés ;
2. test Application preview/apply/undo/redo du profil enrichi ;
3. tests ModuleFormat de round-trip, migration sûre et audit du dépôt/catalogue ;
4. ressource et plan MCP structurés, sans shell ;
5. smoke Toolset et capture visuelle inspectée de la configuration QA ;
6. CLI `quality run`, audit tracker/gates et fixtures malveillantes en lecture seule ;
7. tracker/backlog/docs/graphe frais puis `scripts/Verify.ps1` vert avant R004.

## Résultats avant promotion

- tests ciblés : Domain 3, Application 2, ModuleFormat 10 et MCP 1, tous réussis ;
- audit CLI en lecture seule : `OPENRPG_FORGE_QA_ACCEPTANCE_PASS fixtures=7 features=717 gates=717` ;
- smoke Toolset isolé : `OPENRPG_FORGE_TOOLSET_R003_QA_PASS`, avec persistance et historique restauré ;
- preuve visuelle inspectée : `docs/aurora-parity/evidence/r003-qa-acceptance-ui.png` (1480 × 900),
  onglets sans débordement et quatre politiques lisibles ;
- autorités pré-promotion fraîches : tracker/backlog/docs et graphe de 5 221 nœuds,
  30 369 relations, zéro diagnostic.

## Résultats de clôture

- promotion strictement limitée aux quatre IDs du lot : tracker à 221 `Tested`, 419 `Implemented`,
  77 `Researched`, 177 fonctions P0 et 47 lots P0 encore ouverts ;
- autorités post-promotion fraîches : documentation, backlog, tracker et graphe de 5 221 nœuds,
  30 369 relations et zéro diagnostic ;
- portail post-promotion : 27 projets compilés sans avertissement, 1 015 tests .NET, 6 tests Python
  généraux, 33 tests Python assets, audits QA/tracker/gates, smokes Toolset/runtime/3D/packages/MCP
  et `OPENRPG_FORGE_VERIFICATION=PASS`.

Le lot `R003` est clos. Le prochain lot autorisé est `R004` / `FC-AUDIO-P0-01`.
