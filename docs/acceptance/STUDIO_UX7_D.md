# Acceptation Studio — UX7-D Performance et profilage

État local au 21 août 2026 : **fonctionnel et qualifié** pour trois routes UX7.
UX7 atteint ainsi **22/43** routes accessibles dans Studio.

La page `quality` expose un laboratoire plein écran à sélection explicite du module. Le run copie le
workspace dans un répertoire temporaire borné, exécute trois analyses annulables, rend leurs résultats
navigables, puis supprime l’isolat. Un seuil de budget dépassé reste un verdict visible : il n’est pas
confondu avec l’échec technique du runner.

## Routes couvertes

<a id="orf-qa-017"></a>
- **ORF-QA-017 — Benchmarks de zone** : `PerformanceQualityService.Benchmark` produit neuf mesures
  intérieur, extérieur et stress. La reconstruction de navigation est réellement chronométrée ; frame,
  mémoire, textures, script et réseau restent explicitement des modèles déterministes.
<a id="orf-qa-018"></a>
- **ORF-QA-018 — Budgets par plateforme** : les neuf combinaisons scénarios × desktop/mobile/serveur
  sont évaluées avec leurs seuils d’avertissement et de blocage configurés.
<a id="orf-qa-019"></a>
- **ORF-QA-019 — Profilage intégré** : `WorkspacePerformanceLabService` et
  `IntegratedQualityProfiler` produisent cinq spans éditeur/runtime/serveur et trois artefacts confinés :
  rapport de performance, manifeste visuel et trace JSON Chrome/Perfetto.

## Isolation, annulation et navigation

- La sélection est liée à une empreinte SHA-256 du projet sérialisé et de tous les fichiers source du
  workspace, hors sorties `.forge`, `.godot`, `bin` et `obj`.
- Le `CancellationToken` est contrôlé entre les trois analyses ; le smoke annule après le benchmark et
  vérifie qu’un seul résultat subsiste puis que la copie temporaire est libérée.
- Deux runs complets produisent la même empreinte fonctionnelle ; les durées réelles des spans et de la
  navigation ne rendent pas cette preuve artificiellement instable.
- Projet, historique, révision, journal, dirty, undo/redo, zone active, sélection et fichier source restent
  identiques. Les trois chemins d’artefacts sont résolus et vérifiés sous la racine temporaire.
- Les workbenches UX7-B, UX7-C et UX7-D sont mutuellement exclusifs et le smoke restaure page, surface et
  approbations préexistantes.

## Preuves exécutées

- build `Forge.Studio` Debug : **0 avertissement, 0 erreur** ;
- tests ciblés : **9/9** — Domain 3, Application 5, ModuleFormat 1 ;
- smoke Godot global : marker `OPENRPG_FORGE_STUDIO_UX7_D_PASS` avec les treize sous-invariants à `true` ;
- approbations candidates, evidence et shell global : PASS ;
- portail incrémental : **342,47 s** — build 1,34 s, tests 2,90/3,96/3,73 s, smoke 312,11 s,
  capture 17,09 s et matrice 1,04 s ;
- matrice avant promotion : **717 lignes**, **36 pages**, 557 fonctionnelles / 160 planifiées, PASS ;
- capture Forward+ **1920×1080** sur Forgotten Forest réel : trois cartes et trois résultats visibles,
  détail du profilage, sélection, statut et actions entièrement accessibles, sans clipping bloquant.

Artefacts :

- `artifacts/verification/studio-increment/ux7-d-performance-lab.summary.json` ;
- `artifacts/verification/studio-increment/ux7-d-performance-lab-forward-plus-fhd.png` ;
- `artifacts/verification/studio-shell/studio-interactive.log`.

## Limites honnêtes

Cette qualification ne promeut pas **ORF-QA-016** : la suite de captures visuelles réelles reste privée au
Runtime et n’est pas encore un launcher Studio canonique. Elle ne promeut pas **ORF-EE-029** : la trace
produite est un JSON Chrome/Perfetto, pas une capture Tracy native. Aucun compteur GPU constructeur,
driver, socket, pair distant ni frame réellement rendue n’est mesuré par ce laboratoire. Le portail a
`full_gate=false` et ne revendique ni `Verify.ps1` global ni contrôle de graphe.

## Reproduction

```powershell
$projects = @(
  'tests\Forge.Domain.Tests\Forge.Domain.Tests.csproj'
  'tests\Forge.Application.Tests\Forge.Application.Tests.csproj'
  'tests\Forge.ModuleFormat.Tests\Forge.ModuleFormat.Tests.csproj'
)
$filter = 'FullyQualifiedName~QualityAssuranceProfileTests|' +
  'FullyQualifiedName~ProjectQualityServicesTests|' +
  'FullyQualifiedName~WorkspacePerformanceLabServiceTests'

.\scripts\Verify-StudioIncrement.ps1 `
  -Page quality `
  -Evidence ux7-d-performance-lab `
  -TestProject $projects `
  -TestFilter $filter `
  -Capture 'artifacts\verification\studio-increment\ux7-d-performance-lab-forward-plus-fhd.png'
```
