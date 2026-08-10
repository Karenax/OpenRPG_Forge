# P0 R035 — build du module et console de diagnostics

Lot stable : `R035` / `FC-SHELL-P0-04` — deux fonctions, aucune fonction hors lot.

## Périmètre et source auditée

- `ORF-SHELL-019` — Build du module.
- `ORF-SHELL-020` — Messages, erreurs et statut.
- Source immuable relue intégralement : sections 1229–1359 de
  `Divers/OpenRPG_Forge_Aurora_Feature_Dossier/docs/requirements/01_SHELL.md`.

## Audit avant fermeture

| Fonction | Déjà présent | Limite réelle à fermer |
|---|---|---|
| Build du module | profil UUID/schéma/provenance validé et persistant, graphe ressources/scripts/assets, dépendances automatiques, cache ForgeScript adressé par contenu, plan incrémental, validation canonique avant build, packaging `.orpmod` reproductible, rapports CLI/MCP, UI spécialisée et historique | aucune lacune de domaine : l’action d’export visible du shell exécute déjà le même `ModulePackager`; il reste à réunir la preuve R035 et le rapport de palier sans créer une seconde pipeline |
| Messages, erreurs et statut | diagnostics stables et cliquables, chemin/UUID/propriété/source, corrélation de révision, quick fixes transactionnels, profils de catégories/sévérités persistants, logs structurés avec confidentialité et rétention | la console du shell n’expose pas encore le filtrage catégorie/sévérité/texte/corrélation et ne peut pas exporter un bundle déterministe avec les logs du playtest |

## Contrat minimal de fermeture

- conserver `ModulePackager` comme unique commande canonique de build, avec validation, compilation/packaging,
  graphe incrémental, cache et rapport reproductible ;
- classifier les diagnostics Creator et fournir une requête pure par texte, catégorie, sévérité et corrélation ;
- rendre ces filtres visibles dans la console, avec états vide/erreur et navigation inchangée ;
- exporter atomiquement un bundle versionné, déterministe et expurgé contenant diagnostics structurés et logs runtime ;
- prouver l’égalité UI/domaine, l’identité stable, les chemins sûrs, la confidentialité, la persistance des profils
  existants, les interfaces CLI/MCP, le smoke Toolset et le portail global.

## Non-objectifs

- nouvelle chaîne de build parallèle, télémétrie distante, R036+, P1/P2, AV7–AV10, commit ou push Git.

## Matrice de fermeture

| Fonction | Critères à fermer | Preuves reproductibles |
|---|---|---|
| `ORF-SHELL-019` | validation avant build, scripts/assets, cache, dépendances, build incrémental, package reproductible, rapport, UI/CLI/MCP | `WorkspaceBuildGraphTests`, `ModulePackageTests`, `ForgeProjectAgentServiceTests`, `RunBuildPipelineSmokeTest`, marqueurs `G712` et `R035` |
| `ORF-SHELL-020` | diagnostics UUID/schéma/provenance, catégories et sévérités, filtres, corrélation, navigation, quick fixes, logs runtime et export sûr | `CreatorShellModelTests`, `CreatorDiagnosticBundleExporterTests`, console `ForgeCreatorShell`, smoke R035 |

## Validation de palier

- tests ciblés : 11 Creator et 2 bundles diagnostics ;
- suites complètes : Domain 320, Application 340, ModuleFormat 264 et MCP 70 ;
- capture UI 2 048 × 1 096 inspectée : filtres, état erreur/avertissement et export visibles dans
  `docs/aurora-parity/evidence/r035-shell-diagnostics.png` ;
- tracker/backlog : 355 `Tested`, 357 `Implemented`, 5 `Researched`, 15 lots P0 ouverts ;
- graphe Roslyn frais et déterministe : 6 081 nœuds, 37 343 relations, zéro diagnostic ;
- portail intégré : 27 projets, zéro vulnérabilité/avertissement/erreur, 1 299 tests .NET,
  39 tests Python, tous les smokes et `OPENRPG_FORGE_VERIFICATION=PASS` ;
- arrêt strict après R035, sans commit ni push.
