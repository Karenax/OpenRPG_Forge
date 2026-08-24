# Acceptation Studio — UX7-G Laboratoire playtest

État local au 21 août 2026 : **fonctionnel et qualifié** pour cinq routes UX7.
UX7 atteint ainsi **41/43** routes accessibles dans Studio.

La page `playtest` expose un laboratoire plein écran à sélection explicite du module. Chaque run crée
une copie temporaire du workspace, exerce cinq parcours de test déterministes, rend leurs résultats
navigables, puis supprime l'isolat. Le projet ouvert, son historique, sa sélection et ses approbations
restent inchangés.

## Routes couvertes

<a id="orf-shell-018"></a>
- **ORF-SHELL-018 — Aperçu et mode test** : `PlaytestSnapshotStore` capture un snapshot versionné,
  `PlaytestLaunchManifest` résout l'entrée et le profil, puis la preuve recharge et retire l'artefact
  sans toucher au module ouvert.
<a id="orf-area-036"></a>
- **ORF-AREA-036 — Play From Here** : la zone d'entrée est choisie explicitement et
  `SetAreaPlaytestHandler` prouve dry-run, révision périmée, application, undo, redo et round-trip.
<a id="orf-game-029"></a>
- **ORF-GAME-029 — Replay deterministic test** : `GameplaySessionServices` enregistre huit frames,
  rejoue deux fois le même état, localise une divergence injectée et produit un manifeste minimal
  sans donnée personnelle.
<a id="orf-ai-025"></a>
- **ORF-AI-025 — Agent de test** : `AssistantScenarioTestService` exécute deux fois le scénario
  gouverné, sépare le verdict du contenu de la validité du runner et exige actions autorisées et
  citations traçables.
<a id="orf-ai-028"></a>
- **ORF-AI-028 — Simulation de combat** : `CombatBalanceSimulationService` exécute des matchups
  seedés sur les blueprints combat réels du module ; un fallback canonique n'existe que dans
  l'isolat si moins de deux archétypes sont disponibles.

## Isolation, annulation et navigation

- La sélection est liée à une empreinte SHA-256 du projet sérialisé et du workspace source.
- L'annulation est exercée après le premier parcours ; les quatre suivants ne sont pas produits et
  la copie temporaire est libérée.
- Deux runs complets rendent la même empreinte fonctionnelle.
- Projet, historique, révision, journal, dirty, undo/redo, zone active, sélection, fichier source et
  approbations restent identiques.
- Snapshots, manifestes, replays et rapports restent sous la racine temporaire ; aucun artefact n'est
  écrit dans le module ouvert.

## Preuves exécutées

- build `Forge.Studio` Debug : **0 avertissement, 0 erreur** ;
- tests .NET ciblés : **47/47** — Domain 13, Application 17, ModuleFormat 14, MCP 3 ;
- smoke Godot global : marker `OPENRPG_FORGE_STUDIO_UX7_G_PASS` avec tous les sous-invariants à `true` ;
- approbations candidates, evidence et shell global : PASS ;
- portail incrémental : **309,83 s** — build 1,12 s, tests 1,61/2,56/4,58/8,37 s,
  smoke 279,44 s, capture 11,26 s et matrice 0,84 s ;
- matrice avant promotion : **717 lignes**, **36 pages**, 574 fonctionnelles / 143 planifiées, PASS ;
- capture Forward+ **1920×1080** sur Crypte des Cendres réel : cinq cartes, cinq résultats, détail de
  simulation de combat, frontière, statut et actions entièrement lisibles, sans clipping.

Artefacts :

- `artifacts/verification/studio-increment/ux7-g-playtest-lab.summary.json` ;
- `artifacts/verification/studio-increment/ux7-g-playtest-lab-forward-plus-fhd.png` ;
- `artifacts/verification/studio-shell/studio-interactive.log`.

## Limites honnêtes

Le laboratoire prouve des contrats et simulations déterministes dans une copie locale. Il ne lance
pas le Runtime, n'instancie aucun acteur, monde, caméra, physique ou rendu, et n'ouvre aucun socket ou
serveur. L'agent ne joue pas le module ; la simulation de combat ne remplace ni l'IA live ni l'autorité
réseau. Le portail a `full_gate=false`, `graph_check=false` et ne revendique ni `Verify.ps1` global ni
contrôle du graphe.

## Reproduction

```powershell
$projects = @(
  'tests\Forge.Domain.Tests\Forge.Domain.Tests.csproj'
  'tests\Forge.Application.Tests\Forge.Application.Tests.csproj'
  'tests\Forge.ModuleFormat.Tests\Forge.ModuleFormat.Tests.csproj'
  'tests\Forge.Mcp.Tests\Forge.Mcp.Tests.csproj'
)
$filter = 'FullyQualifiedName~AreaRuntimeValidationTests|' +
  'FullyQualifiedName~GameplaySessionProfileTests|' +
  'FullyQualifiedName~AssistantWorkspaceProfileTests|' +
  'FullyQualifiedName~AreaRuntimeValidationWorkflowTests|' +
  'FullyQualifiedName~GameplaySessionServicesTests|' +
  'FullyQualifiedName~GameplaySessionScriptServiceTests|' +
  'FullyQualifiedName~ProjectQualityServicesTests|' +
  'FullyQualifiedName~CombatBalanceSimulationServiceTests|' +
  'FullyQualifiedName~PlaytestSnapshotStoreTests|' +
  'FullyQualifiedName~AssistantWorkspaceProfileStoreTests|' +
  'FullyQualifiedName~AssistantTestAutomationTests|' +
  'FullyQualifiedName~CombatSimulationAgentServiceTests'

.\scripts\Verify-StudioIncrement.ps1 `
  -Page playtest `
  -Evidence ux7-g-playtest-lab `
  -TestProject $projects `
  -TestFilter $filter `
  -Capture 'artifacts\verification\studio-increment\ux7-g-playtest-lab-forward-plus-fhd.png'
```
