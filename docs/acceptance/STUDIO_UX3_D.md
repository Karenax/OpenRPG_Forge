# Studio UX3-D — dépendances, provenance et licence

Qualification Studio de `ORF-PAL-015` et `ORF-PAL-016` sur les pages `blueprints` et `assets`.
La surface « Gouvernance des blueprints » conserve deux transactions explicites : le manifeste de
dépendances via `UpsertBlueprintHandler`, puis la provenance distribuable via
`ConfigureBlueprintPaletteHandler`. Chacune possède son aperçu sec, son approbation liée à la
révision et aux empreintes, son historique et sa persistance.

<a id="orf-pal-015"></a>
## ORF-PAL-015

Le manifeste accepte des chemins relatifs au workspace et des URI `pack://` par ligne, ainsi qu'un
budget plateforme explicite. `WorkspaceBuildGraphBuilder` calcule la fermeture transitive sur les
blueprints et ressources réellement présents. Une dépendance locale absente ou un package non
installé bloque l'aperçu et, pour un blueprint utilisé, bloque désormais aussi le build avant toute
écriture de package. La preuve couvre ce refus, la fermeture déterministe et la préservation des
autres métadonnées d'authoring.

<a id="orf-pal-016"></a>
## ORF-PAL-016

Source, auteur, licence SPDX, SHA-256, preuve de permission, origine IA et modifications déclarées
sont édités sans exposer d'UUID. `BlueprintDropPlanner` reproduit la décision canonique
d'éligibilité au package et `DistributionComplianceValidator` confirme la distribution Shipping.
Une licence, une permission ou une empreinte invalide reste bloquante ; aucun package n'est produit
par l'aperçu Studio.

## Transactions et préservation

- sélection humaine explicite d'un blueprint Projet ; les définitions Standard restent visibles et
  protégées en lecture seule ;
- approbations indépendantes pour dépendances et provenance ; après une application, l'autre bloc
  doit être rechargé et prévisualisé sur la nouvelle révision ;
- garde de révision, empreinte SHA-256 du projet et empreinte binaire typée du brouillon recontrôlées
  juste avant chaque application ;
- dry-run sans mutation, refus des sources et approbations périmées, puis apply, undo/redo et
  save/reopen canoniques ;
- blueprints voisins, instances, zones, module, dialogues, quêtes et champs non édités préservés ;
- preuve enrichie dans un workspace temporaire isolé, avec projet, historique, dirty state,
  sélection, approbations et surface source inchangés, puis ressources libérées.

## Validation locale

- build Studio Debug : **0 avertissement, 0 erreur** ;
- Domain : **18/18** tests ciblés ;
- Application : **13/13** tests ciblés ;
- ModuleFormat : **14/14** tests ciblés, dont le refus pré-build d'une dépendance déclarée absente ;
- smoke global : `OPENRPG_FORGE_STUDIO_UX3_D_PASS`, approbations globales et shell PASS ;
- portail incrémental : build, **45/45** tests, smoke, capture FHD et matrice en **191,90 s** ;
- captures Forward+ **1920×1080** inspectées sur les deux onglets : champs, diff, diagnostics,
  limites, approbations et actions restent visibles sans UUID ni clipping bloquant ;
- résumé machine :
  `artifacts/verification/studio-increment/ux3-d-blueprint-governance.summary.json` ;
- captures :
  `artifacts/verification/studio-increment/ux3-d-blueprint-governance-forward-plus-fhd.png` et
  `artifacts/verification/studio-increment/ux3-d-dependencies-forward-plus-fhd.png` ;
- `full_gate=false` et `graph_check=false` restent explicites pour le portail incrémental ; le graphe
  Roslyn est régénéré et contrôlé séparément à la clôture documentaire de l'incrément.

## Limites honnêtes

Studio construit et valide un graphe de métadonnées. Il ne charge aucun asset ou prefab, n'exécute
aucun script, ne crée aucun package et ne lance aucun acteur, moteur, serveur ou transport réseau.
Le module réel conserve honnêtement ses dépendances ou sa provenance absentes/incomplètes ; les
données riches appartiennent uniquement à la copie canonique isolée de preuve.

## Reproduction

```powershell
$projects = @(
  'tests/Forge.Domain.Tests/Forge.Domain.Tests.csproj'
  'tests/Forge.Application.Tests/Forge.Application.Tests.csproj'
  'tests/Forge.ModuleFormat.Tests/Forge.ModuleFormat.Tests.csproj'
)
$filter = 'FullyQualifiedName~BlueprintAuthoringMetadataTests|' +
  'FullyQualifiedName~BlueprintProvenanceTests|' +
  'FullyQualifiedName~DistributionComplianceValidatorTests|' +
  'FullyQualifiedName~BlueprintCatalogTests|' +
  'FullyQualifiedName~WorkspaceBuildGraphTests|' +
  'FullyQualifiedName~ContentWorkflowTests|' +
  'FullyQualifiedName~BlueprintAuthoringPersistenceTests'
& .\scripts\Verify-StudioIncrement.ps1 -Page assets `
  -Evidence ux3-d-blueprint-governance `
  -TestProject $projects -TestFilter $filter `
  -Capture artifacts/verification/studio-increment/ux3-d-blueprint-governance-forward-plus-fhd.png

& .\scripts\Open-Studio.ps1 -Page blueprints -Evidence ux3-d-dependencies `
  -Resolution 1920x1080 -SkipBuild -Wait `
  -Capture artifacts/verification/studio-increment/ux3-d-dependencies-forward-plus-fhd.png
```
