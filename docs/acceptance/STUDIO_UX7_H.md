# Acceptation Studio — UX7-H Régression visuelle Runtime

État local au 22 août 2026 : **fonctionnel et qualifié** pour une route UX7.
Au jalon UX7-H, UX7 atteignait **42/43** routes accessibles dans Studio.

La page `quality` expose une surface pleine taille à sélection explicite du module. Elle lance le
vrai `Forge.Runtime` avec le binaire Godot 4.7.1 .NET officiel, mais lui fournit une copie temporaire
du workspace. Les huit combinaisons résolution/caméra/qualité produisent références et candidats
PNG, revues JSON et rapport global. Le Studio lit les résultats, vérifie leur exhaustivité, les rend
navigables, puis supprime l'isolat.

## Route couverte

<a id="orf-qa-016"></a>
- **ORF-QA-016 — Tests visuels** : `PerformanceQualityService.PlanVisualReviews` fournit le plan
  canonique et `GameRoot --g11s-visual-suite` exécute réellement deux résolutions, deux caméras et
  deux niveaux de qualité. Les huit erreurs moyennes restent sous la tolérance persistée ; chaque
  cas possède une référence PNG, un candidat PNG et une revue JSON, plus un rapport global.

## Runtime, isolation et annulation

- Le Studio exige `Godot 4.7.1.stable.mono.official` et un `Forge.Runtime` compilé ; il n'utilise pas
  un rendu ou un faux résultat interne au Studio.
- Le workspace source est recopié sous une racine temporaire et seul ce chemin est passé à
  `--workspace` ; les 25 artefacts et le journal Runtime restent sous `.forge` dans cet isolat.
- L'annulation démarre un vrai second processus Runtime, annule son attente, tue son arbre de
  processus puis vérifie la libération de la racine temporaire.
- Projet, historique, révision, journal, dirty, undo/redo, zone active, sélection et fichier source
  restent identiques avant et après le run.
- La navigation humaine présente le verdict global et les huit lignes résolution/caméra/qualité,
  avec erreur observée, tolérance et statut.

## Preuves exécutées

- build `Forge.Studio` Debug : **0 avertissement, 0 erreur** ;
- build `Forge.Runtime` Debug : **0 avertissement, 0 erreur** ;
- tests .NET ciblés : **13/13** — Domain 3, Application 5, ModuleFormat 5 ;
- smoke Godot global : marker `OPENRPG_FORGE_STUDIO_UX7_H_PASS` avec tous les sous-invariants à
  `true`, puis approbations candidates, evidence et shell global PASS ;
- capture Forward+ **1920×1080** sur Crypte des Cendres réel : cartes QA/Tracy, résultat, huit cas,
  frontière, statut et actions lisibles sans clipping ;
- portail incrémental : **476,34 s** — build 1,15 s, tests 1,52/2,63/2,88 s, smoke
  334,97 s, capture 26,31 s, matrice 0,89 s, génération du graphe 54,31 s et contrôle intégral
  51,62 s ;
- matrice après promotion : **717 lignes**, **36 pages**, 580 fonctionnelles / 137 planifiées ;
- graphe Roslyn frais et déterministe : **11 020 nœuds**, **84 332 relations**, **0 diagnostic**.

Artefacts :

- `artifacts/verification/studio-increment/ux7-h-visual-regression-forward-plus-fhd.png` ;
- `artifacts/verification/studio-increment/ux7-h-visual-regression.summary.json` ;
- `artifacts/verification/studio-shell/studio-interactive.log`.

## Limite Tracy explicite

Au jalon UX7-H, **ORF-EE-029 — Profiler Tracy** restait planifié : une capture Tracy native exige
un moteur Godot construit avec `profiler=tracy` et un capture server de version identique. UX7-H
affiche toujours ce prérequis et ne renomme jamais son JSON Chrome/Perfetto en capture Tracy.
La route est désormais qualifiée séparément par UX7-I grâce à la configuration canonique, au vrai
fallback OpenTelemetry et au diagnostic natif explicite ; voir
`docs/acceptance/STUDIO_UX7_I.md`. Aucun compteur GPU constructeur n'est revendiqué.

Le portail Studio demeure un incrément local avec `full_gate=false`; il ne remplace ni
`scripts/Verify.ps1` global ni une qualification de distribution.

## Reproduction

```powershell
dotnet build apps/Forge.Runtime/Forge.Runtime.csproj --configuration Debug --no-restore --nologo

$projects = @(
  'tests\Forge.Domain.Tests\Forge.Domain.Tests.csproj'
  'tests\Forge.Application.Tests\Forge.Application.Tests.csproj'
  'tests\Forge.ModuleFormat.Tests\Forge.ModuleFormat.Tests.csproj'
)
$filter = 'FullyQualifiedName~QualityAssuranceProfileTests|' +
  'FullyQualifiedName~ProjectQualityServicesTests|' +
  'FullyQualifiedName~WorkspaceQualityLabServiceTests'

.\scripts\Verify-StudioIncrement.ps1 `
  -Page quality `
  -Evidence ux7-h-visual-regression `
  -TestProject $projects `
  -TestFilter $filter `
  -Capture 'artifacts\verification\studio-increment\ux7-h-visual-regression-forward-plus-fhd.png'
```
