# Studio UX3-A — fondation des blueprints et palettes

Qualification Studio des cinq routes R030 sur la page `blueprints`. La surface « Blueprints et
palettes » réutilise les définitions, instances, palettes et commandes canoniques existantes ; elle
n'introduit aucun second catalogue et ne modifie pas l'ancien plugin.

<a id="orf-pal-001"></a>
## ORF-PAL-001

Un blueprint Projet est sélectionné explicitement puis édité comme définition réutilisable. Nom,
clé, version sémantique, namespace, paramètres exposés, dépendances et budget plateforme passent
par `UpsertBlueprintHandler`. Le dry-run, la révision et deux empreintes SHA-256 empêchent une
application sur un projet ou un brouillon périmé. Les profils gameplay, la composition, le kit, la
provenance et les champs hors surface sont conservés.

<a id="orf-pal-002"></a>
## ORF-PAL-002

La surface inventorie les instances réelles par zone et par blueprint, avec un libellé humain et le
nombre de surcharges. La preuve résout l'instance effective via `BlueprintPaletteScriptService` ;
elle ne place, ne déplace et n'exécute aucune instance dans Studio.

<a id="orf-pal-003"></a>
## ORF-PAL-003

Le parcours « Créer depuis une instance existante » appelle uniquement
`CreateBlueprintFromEntityHandler`. Il crée une définition Projet puis retargete l'unique instance
sélectionnée. Transform, tags et surcharges restent identiques ; l'exigence de version est épinglée
sur la nouvelle version. Undo/redo et save/reopen restaurent puis reproduisent exactement la
transaction.

<a id="orf-pal-006"></a>
## ORF-PAL-006

La palette Standard expose les définitions installées et leur usage sans afficher d'UUID. Toute
tentative d'édition est refusée par le handler canonique et la surface reste en lecture seule. Les
chemins techniques contenant une identité stable sont gardés en mémoire sous forme protégée et
réémis sans réécriture.

<a id="orf-pal-007"></a>
## ORF-PAL-007

La palette Projet expose les définitions éditables, leur famille, leur version et leur nombre de
références. La création dérivée apparaît dans ce même catalogue après application ; aucune palette
locale parallèle n'est entretenue par Godot.

## Transaction et préservation

- sélection explicite de la définition ou de l'instance ; aucun premier élément implicite ;
- dry-run pur, application canonique, historique global, undo/redo et save/reopen ;
- garde de révision, empreinte du projet et empreinte binaire typée du brouillon approuvé ;
- conservation des blueprints voisins, zones voisines, entités voisines, dialogues, quêtes et module ;
- workspace temporaire isolé, clairement marqué fallback canonique lorsque Forgotten Forest ne
  contient pas de palette Standard installée ;
- projet, historique, dirty state, sélection, approbation et surface source restaurés exactement ;
- suppression bornée du workspace temporaire.

## Validation locale

- build Studio Debug : **0 avertissement, 0 erreur** ;
- Domain : **8/8** tests `BlueprintAuthoringMetadataTests` ;
- Application : **13/13** tests `BlueprintCatalogTests` ;
- ModuleFormat : **1/1** test `BlueprintAuthoringPersistenceTests` ;
- smoke Studio : `OPENRPG_FORGE_STUDIO_UX3_A_PASS`, approbations globales et shell PASS ;
- captures Forward+ **1920×1080** inspectées : définition, instance réellement sélectionnée et
  palettes Standard/Projet, sans UUID ni action masquée ;
- jalon hybride inspecté aussi à **2560×1440** et **3840×2160** : structure, diff, diagnostics et
  footer restent visibles sans clipping ;
- portail incrémental final : build, **22/22** tests, smoke, capture FHD et matrice en
  **144,41 s** ;
- résumé machine :
  `artifacts/verification/studio-increment/ux3-a-blueprint-foundation.summary.json` ;
- captures complémentaires :
  `artifacts/verification/studio-increment/ux3-a-instance-forward-plus-fhd.png`,
  `artifacts/verification/studio-increment/ux3-a-palettes-forward-plus-fhd.png`,
  `artifacts/verification/studio-shell/ux3-milestone/ux3-a-qhd.png` et
  `artifacts/verification/studio-shell/ux3-milestone/ux3-a-4k.png` ;
- graphe Roslyn régénéré : **10 762 nœuds**, **81 221 relations**, **0 diagnostic**, puis
  `check --full` déterministe ;
- `full_gate=false` reste explicite : cette preuve incrémentale ne remplace pas le portail complet.

## Limites honnêtes

Studio édite et persiste des données. Il ne charge aucun asset, n'instancie aucun prefab, ne place
aucun objet, n'exécute aucun script, scène, acteur, moteur, serveur ou transport réseau. Les chemins
de manifeste et d'aperçu contenant une identité technique restent protégés et non éditables dans ce
lot. Recherche avancée, comparaison, collections, favoris et placement restent hors UX3-A.

## Reproduction

```powershell
$projects = @(
  'tests/Forge.Domain.Tests/Forge.Domain.Tests.csproj'
  'tests/Forge.Application.Tests/Forge.Application.Tests.csproj'
  'tests/Forge.ModuleFormat.Tests/Forge.ModuleFormat.Tests.csproj'
)
$filter = 'FullyQualifiedName~BlueprintAuthoringMetadataTests|' +
  'FullyQualifiedName~BlueprintCatalogTests|' +
  'FullyQualifiedName~BlueprintAuthoringPersistenceTests'
& .\scripts\Verify-StudioIncrement.ps1 -Page blueprints `
  -Evidence ux3-a-blueprint-foundation `
  -Workspace apps/Forge.Runtime/content/forgotten-forest `
  -TestProject $projects -TestFilter $filter `
  -Capture artifacts/verification/studio-increment/ux3-a-blueprint-foundation-forward-plus-fhd.png
```
