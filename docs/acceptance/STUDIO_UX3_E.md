# Studio UX3-E — compatibilité artistique et budgets d’asset

Qualification Studio de `ORF-PAL-017` et `ORF-PAL-018` sur la page `assets`. La surface
« Qualité artistique des assets » garde deux transactions explicites : le profil artistique du
module et de l’asset via `SetArtisticStyleProfileHandler`, puis le budget du blueprint via
`ConfigureBlueprintPrefabHandler`.

<a id="orf-pal-017"></a>
## ORF-PAL-017

Le Studio sélectionne explicitement un blueprint, un GLB local et une texture PNG du workspace.
`ArtisticAssetMetricExtractor` lit réellement les bornes `POSITION` et les dimensions PNG pour
proposer échelle, densité texel et famille PBR. L’auteur conserve le contrôle de l’époque, du
réalisme et de la palette. `ArtisticCompatibilityValidator` rend les six écarts localisés :
échelle, densité, shader, époque, réalisme et palette. Ces écarts restent des avertissements
artistiques, conformément au contrat produit ; ils ne sont pas maquillés en erreurs de schéma.

<a id="orf-pal-018"></a>
## ORF-PAL-018

Le budget couvre les sept axes canoniques : triangles, matériaux, textures, mémoire, animations,
LOD et collisions. Chaque plateforme possède ses limites et son choix « Bloquer le build ».
`BlueprintPrefabRuntimeService` distingue conforme, avertissement et bloquant ;
`BlueprintPrefabValidator` et le chemin de sérialisation du package refusent un dépassement marqué
bloquant avant écriture du build.

## Transactions et préservation

- sélection humaine explicite ; les blueprints Standard restent analysables mais leur budget est
  protégé en lecture seule ;
- approbations séparées pour profil artistique et budget, chacune liée à la révision, à l’empreinte
  du projet et à une empreinte binaire typée du brouillon ;
- dry-run sans mutation, recontrôle juste avant apply, refus des sources et approbations périmées,
  puis apply, undo/redo et save/reopen canoniques ;
- module, authoring, champs cachés du blueprint, blueprints voisins et autres racines du projet
  préservés ;
- preuve riche dans un workspace temporaire isolé, la source réelle restant inchangée, puis
  suppression vérifiée des ressources temporaires.

## Validation locale

- build Studio Debug : **0 avertissement, 0 erreur** ;
- Domain : **3/3** tests ciblés ;
- Application : **3/3** tests ciblés ;
- ModuleFormat : **5/5** tests ciblés ;
- smoke global : `OPENRPG_FORGE_STUDIO_UX3_E_PASS`, approbations globales et shell PASS ;
- portail incrémental : build, **11/11** tests, smoke, capture FHD et matrice en **156,86 s** ;
- deux captures Forward+ **1920×1080** inspectées : les deux onglets, les six diagnostics, les sept
  axes, les approbations, les actions et le footer restent visibles sans UUID ni clipping bloquant ;
- résumé machine :
  `artifacts/verification/studio-increment/ux3-e-asset-quality.summary.json` ;
- captures :
  `artifacts/verification/studio-increment/ux3-e-asset-quality-forward-plus-fhd.png` et
  `artifacts/verification/studio-increment/ux3-e-budget-forward-plus-fhd.png` ;
- `full_gate=false` et `graph_check=false` restent explicites pour ce portail incrémental ; le
  graphe Roslyn est régénéré et contrôlé séparément après la promotion documentaire.

## Limites honnêtes

La mesure lit des documents GLB/PNG locaux, mais n’instancie aucun modèle, matériau, scène ou GPU.
Le Studio ne mesure pas le temps de rendu, ne construit aucun package, n’exécute aucun script,
acteur, moteur, serveur ou transport réseau. Les modules réels sans asset artistique ou budget
authoré restent dans un état vide explicite ; le contenu riche de preuve appartient uniquement à
la copie canonique isolée.

## Reproduction

```powershell
$projects = @(
  'tests/Forge.Domain.Tests/Forge.Domain.Tests.csproj'
  'tests/Forge.Application.Tests/Forge.Application.Tests.csproj'
  'tests/Forge.ModuleFormat.Tests/Forge.ModuleFormat.Tests.csproj'
)
$filter = 'FullyQualifiedName~ArtisticStyleProfileTests|' +
  'FullyQualifiedName~PrefabAuthoringProfileTests|' +
  'FullyQualifiedName~SetArtisticStyleProfileHandlerTests|' +
  'FullyQualifiedName~BlueprintPrefabWorkflowTests|' +
  'FullyQualifiedName~ArtisticAssetMetricExtractorTests|' +
  'FullyQualifiedName~BlueprintAuthoringPersistenceTests|' +
  'FullyQualifiedName~SerializeAndDeserialize_RoundTripsArtisticCompatibilityProfile|' +
  'FullyQualifiedName~Deserialize_MigratesPreArtisticStyleFormatDeterministically'
& .\scripts\Verify-StudioIncrement.ps1 -Page assets `
  -Evidence ux3-e-asset-quality `
  -TestProject $projects -TestFilter $filter `
  -Capture artifacts/verification/studio-increment/ux3-e-asset-quality-forward-plus-fhd.png

& .\scripts\Open-Studio.ps1 -Page assets -Evidence ux3-e-budget `
  -Resolution 1920x1080 -SkipBuild -Wait `
  -Capture artifacts/verification/studio-increment/ux3-e-budget-forward-plus-fhd.png
```
