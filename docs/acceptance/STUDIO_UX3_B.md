# Studio UX3-B — propagation, migration et prefabs

Qualification Studio de cinq routes Palette R057/R058 sur la page `blueprints`. La surface
« Bibliothèque avancée de blueprints » sépare explicitement la mise à jour/migration et la
configuration des variantes/compositions en deux transactions, deux dry-runs et deux approbations.

<a id="orf-pal-004"></a>
## ORF-PAL-004

Une instance source est choisie explicitement. Ses surcharges sont exposées par libellé humain et
seules celles cochées deviennent des valeurs partagées de la définition. `BlueprintUpdatePlanner`
calcule le diff, le bump SemVer et l'impact de chaque instance ; `UpdateBlueprintFromEntityHandler`
reste l'unique autorité d'application. L'aperçu ne modifie ni définition ni instance.

<a id="orf-pal-005"></a>
## ORF-PAL-005

La migration montre toutes les instances concernées et permet des exclusions explicites. Les pins
exacts migrés deviennent la nouvelle version exacte ; une exclusion qui ne serait plus compatible
est élargie en contrainte `>= version précédente`, donc elle reste exclue du repin exact sans rendre
le document impossible à sauvegarder. Les transforms, tags et surcharges d'instances restent
inchangés. Undo/redo et save/reopen couvrent la transaction complète.

<a id="orf-pal-008"></a>
## ORF-PAL-008

Les chemins auteur hiérarchiques sont édités sans JSON, un chemin humain par ligne. L'index
canonique reconstruit les catégories parentes virtuelles sans second catalogue ni cache détenu par
Godot. Les catégories, collections et métadonnées hors surface conservent leur ordre et leur valeur.

<a id="orf-pal-013"></a>
## ORF-PAL-013

Une variante peut être activée avec un blueprint de base de la même famille et une collection
ordonnée de surcharges Composant, Matériau, Statistique ou Script. La résolution est effectuée par
`BlueprintPrefabRuntimeService` sur une copie immuable. Le budget d'asset existant reste protégé et
n'est pas réécrit par UX3-B.

<a id="orf-pal-014"></a>
## ORF-PAL-014

La composition expose des nœuds ordonnés, un parent humain, un point d'assemblage et la transform
complète position/quaternion/échelle. Règles, paramètres, scripts gouvernés et collections auteur
sont persistés par `ConfigureBlueprintPrefabHandler`. La hiérarchie invalide, la référence absente
et le cycle sont refusés avant application.

## Transactions et préservation

- sélection explicite du blueprint et de l'instance source ; aucun premier élément implicite ;
- deux approbations indépendantes : modifier le prefab n'annule plus le plan de propagation frais ;
- tout apply refait son dry-run avec révision, empreinte SHA-256 du projet et empreinte binaire
  typée du brouillon approuvé ;
- appliquer une transaction invalide honnêtement l'approbation devenue périmée de l'autre ;
- double undo/redo et save/reopen prouvent l'ordre propagation puis prefab ;
- définition voisine, profils gameplay, budget d'asset, zones, entités, module, dialogues et quêtes
  sont préservés ;
- preuve riche dans un workspace temporaire canonique, source/historique/dirty state/sélection/
  approbations/surface inchangés et cleanup mesuré.

## Validation locale

- build Studio Debug : **0 avertissement, 0 erreur** ;
- Domain : **10/10** tests `BlueprintAuthoringMetadataTests` et `PrefabAuthoringProfileTests` ;
- Application : **6/6** tests `BlueprintUpdateWorkflowTests`, `BlueprintPrefabWorkflowTests` et
  `BlueprintLibraryIndexTests` ;
- ModuleFormat : **1/1** test `BlueprintAuthoringPersistenceTests` ;
- smoke global : `OPENRPG_FORGE_STUDIO_UX3_B_PASS`, approbations globales et shell PASS ;
- capture Forward+ **1920×1080** propagation inspectée : sélection, exclusions, catégories, diff,
  diagnostics et actions visibles sans UUID ni clipping ;
- capture Forward+ **1920×1080** prefab inspectée : variante, composition, transform complète,
  collections, footer et barre de défilement explicite ;
- portail incrémental : build, **17/17** tests, smoke, capture FHD et matrice en **146,04 s** ;
- résumé machine :
  `artifacts/verification/studio-increment/ux3-b-blueprint-library.summary.json` ;
- captures : `artifacts/verification/studio-shell/ux3-b-fhd.png` et
  `artifacts/verification/studio-shell/ux3-b-prefabs-fhd.png` ;
- `full_gate=false` reste explicite : cette preuve ne remplace pas le portail transversal complet.

## Limites honnêtes

Studio édite et persiste des profils. Il ne charge aucun asset, n'instancie ni ne place aucun
prefab, ne construit aucune scène, n'exécute aucun script, moteur, acteur, serveur ou transport
réseau. La résolution de variante/composition est une projection pure. Les migrations de packs
distants et la coexistence matérielle de plusieurs versions d'un même blueprint restent hors lot ;
l'exclusion UX3-B conserve donc une contrainte compatible plutôt qu'une copie cachée de l'ancienne
définition.

## Reproduction

```powershell
$projects = @(
  'tests/Forge.Domain.Tests/Forge.Domain.Tests.csproj'
  'tests/Forge.Application.Tests/Forge.Application.Tests.csproj'
  'tests/Forge.ModuleFormat.Tests/Forge.ModuleFormat.Tests.csproj'
)
$filter = 'FullyQualifiedName~BlueprintAuthoringMetadataTests|' +
  'FullyQualifiedName~PrefabAuthoringProfileTests|' +
  'FullyQualifiedName~BlueprintUpdateWorkflowTests|' +
  'FullyQualifiedName~BlueprintPrefabWorkflowTests|' +
  'FullyQualifiedName~BlueprintLibraryIndexTests|' +
  'FullyQualifiedName~BlueprintAuthoringPersistenceTests'
& .\scripts\Verify-StudioIncrement.ps1 -Page blueprints `
  -Evidence ux3-b-blueprint-library `
  -TestProject $projects -TestFilter $filter `
  -Capture artifacts/verification/studio-shell/ux3-b-fhd.png
```
