# P0 R036 — échelle, build incrémental et pathfinding amélioré

Lot stable : `R036` / `FC-EE-P0-01` — trois fonctions, aucune fonction hors lot.

## Périmètre et sources auditées

- `ORF-EE-002` — Mise à l’échelle dans le Toolset.
- `ORF-EE-004` — Optimisation Build Module.
- `ORF-EE-028` — Pathfinding amélioré.
- Source immuable relue intégralement : sections 133–195, 267–329 et 1887–1949 de
  `Divers/OpenRPG_Forge_Aurora_Feature_Dossier/docs/requirements/23_EE.md`.
- Dépendances AREA, BUILD, QA et SHELL réévaluées : R001/R023/R027/R028/R029/R032/R033/R035
  fournissent les documents, commandes, validations, diagnostics, navigation, build et shell requis.

## Audit avant fermeture

| Fonction | Déjà présent | Limite réelle fermée par R036 |
|---|---|---|
| Mise à l’échelle | `TransformValue` canonique, commande dry-run/apply atomique multi-entités, bornes 0,05–100, validation géométrique, persistance, inspecteur, Ctrl+molette et undo/redo | gizmo 3D sélectionnable X/Y/Z/XYZ, reconstruction explicite collision/navigation et plan MCP filtré/dry-run/apply/revert |
| Build Module | profil UUID/schéma/provenance, graphe de dépendances, caches scripts/assets adressés, plan incrémental, package reproductible, UI/CLI/MCP | ordonnanceur borné parallèle, matérialisation confinée et ordre de sortie déterministe prouvé dans le graphe et le packaging |
| Pathfinding | A* déterministe, obstacles dynamiques, profils agents, récupération, budgets, instrumentation, rebuild partiel, cache et UI | réunir les preuves de l’adapter Godot, du runtime polygonal et du benchmark intérieur/extérieur/stress déjà livrés, puis les rendre obligatoires dans les smokes R036 |

## Matrice critère → preuve

| Critère source | Échelle | Build | Pathfinding |
|---|---|---|---|
| domaine canonique, UUID, schéma, provenance | entité/transform UUID dans le projet et journal de commande v1 | profil de build et graphe v1 | document navigation, surface, agents et liens versionnés |
| UI visible, vide/erreur | inspecteur numérique et gizmo X/Y/Z/XYZ, retour de placement | fenêtre Scripts & build, statut sans projet et diagnostic d’analyse | atelier Navigation, overlay, état sans endpoints et diagnostics |
| mutation unique et validation avant build | `ScaleEntitiesHandler`, bornes, géométrie et diagnostic stable | `SetWorkspaceBuildProfileHandler` et validation/package canonique | `SetAreaNavigationHandler` et validation zone/projet |
| ForgeScript sûr | transform consommé comme donnée canonique, sans nœud moteur ni fichier exposé | build non consultable au runtime, donc aucune API script redondante | handles et requêtes navigation typés livrés avec R009/R029 |
| MCP filtré et dry-run | `GetEntityScales` et plan scale apply/revert | profil/read/plan build de R023/R025 | ressource runtime de zone et plan navigation de R029 |
| transactions et round-trip | commande, historique, save/reload et plan MCP | historique du profil, cache jetable hors document | commande/historique/persistance navigation |
| adaptation obligatoire | uniform/non-uniform, 4 poignées, contraintes, multi-edit, rebuild collision/nav | graphe, incrémental, caches scripts/assets, tâches parallèles, sortie stable | adapter Godot, obstacles, quatre profils, recovery et benchmarks |

## Non-objectifs

- ajouter Recast comme dépendance obligatoire alors que l’adapter Godot satisfait le contrat ;
- réécrire l’évitement local ou la locomotion continue, ouvrir une cache distante, P1/P2, AV7–AV10,
  R037+, commit ou push Git.

## Preuves reproductibles

- tests ciblés : `DeterministicBuildTaskSchedulerTests`, `WorkspaceBuildGraphTests`,
  `ScaleEntitiesHandlerTests`, `NavigationPathfinderTests`, tests adapter/navigation et plan MCP scale ;
- marqueur Toolset `OPENRPG_FORGE_TOOLSET_R036_EE_PASS` et marqueur runtime
  `OPENRPG_FORGE_RUNTIME_R036_EE_PASS` obligatoires dans `scripts/Verify.ps1` ;
- captures inspectées :
  `docs/aurora-parity/evidence/r036-ee-scale-ui.png` montre la sélection, les valeurs non uniformes,
  les quatre axes/poignées et l’aide directe ;
  `docs/aurora-parity/evidence/r036-ee-build-ui.png` montre le graphe, le plan incrémental, deux tâches,
  la borne de quatre tâches parallèles et l’empreinte d’ordre déterministe ;
- benchmark navigation déterministe via `PerformanceQualityService`/`WorkspacePerformanceLabService`
  et métriques de reconstruction/requête émises par le runtime Asset Vault ;
- tracker, backlog, graphe Roslyn et portail intégré régénérés avant ouverture de R037.

## Validation de palier

- tracker/backlog : 358 `Tested`, 354 `Implemented`, 5 `Researched`, 156/196 P0 closes et 14 lots P0 ouverts ;
- suites complètes : Domain 320, Application 341, ModuleFormat 266, EngineAdapter 20 et MCP 71 ;
- graphe Roslyn frais : 6 090 nœuds, 37 396 relations, zéro diagnostic ;
- portail intégré : 27 projets sans vulnérabilité, avertissement ni erreur, 1 303 tests .NET,
  39 tests Python, smokes Toolset/runtime/packages/terrain et `OPENRPG_FORGE_VERIFICATION=PASS` ;
- aucune promotion hors des trois IDs audités, aucun commit ni push.
