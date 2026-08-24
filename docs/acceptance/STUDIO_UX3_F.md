# Studio UX3-F — navigateur, import et aller-retour Blender

Qualification Studio de `ORF-CONT-013`, `ORF-CONT-014`, `ORF-CONT-020`, `ORF-CONT-021` et
`ORF-CONT-022` sur la page `assets`. La surface « Navigateur de contenu & aller-retour d’asset »
sépare le profil d’import gouverné de la configuration d’édition externe : chaque document possède
sa révision, son empreinte, son approbation et son historique propres.

<a id="orf-cont-013"></a>
## ORF-CONT-013

Le navigateur unifié expose les textures du module et du catalogue par nom, source, licence,
provenance et disponibilité. Les catégories de matériaux et les sources PNG, JPG, JPEG, WebP,
TIFF, EXR ou KTX2 sont reconnues comme textures. Le profil KTX2 conserve espace colorimétrique,
compression, dimension, densité texel et canaux nommés ; l’aperçu exécute uniquement
`AssetImportPlanner.Plan`.

<a id="orf-cont-014"></a>
## ORF-CONT-014

Les modèles 3D gouvernés sont recherchables et filtrables sans afficher leur UUID. Le profil GLB
porte rig, animations, collisions, points d’attache et ratios LOD ordonnés. La source locale et le
manifeste sont exigés pour préparer un aller-retour externe ; le Studio ne charge ni scène ni
modèle dans cette preuve.

<a id="orf-cont-020"></a>
## ORF-CONT-020

`UnifiedContentBrowser` fusionne les ressources du module et les 673 assets catalogués observés
dans la preuve, avec recherche textuelle, facettes texture/modèle, état local, gouvernance et
actions canoniques. La sélection reste explicite et les chemins complets, hashes et identifiants
internes demeurent protégés.

<a id="orf-cont-021"></a>
## ORF-CONT-021

La configuration d’édition externe conserve source, sortie confinée, provenance de modification,
surveillance, opérations allowlistées et plateformes. Le dry-run valide le document, matérialise
une requête worker uniquement dans le workspace temporaire et résout un plan de changement par le
watcher. L’application, l’annulation et le rétablissement restaurent ensemble configuration et
requête dérivée.

<a id="orf-cont-022"></a>
## ORF-CONT-022

Le pont Blender est une enveloppe typée : validation, normalisation, collisions, LOD, export GLB,
vignette et audit qualité sont des opérations fermées. Aucun Python libre, shell, réseau ou
processus Blender n’est lancé par l’aperçu ou par le smoke. `BlenderWorkerRequestStore` demeure
l’autorité du document confié à un worker externe ultérieur.

## Transactions et préservation

- révision et empreinte persistée du store, plus empreinte binaire typée du brouillon approuvé ;
- dry-run sans mutation, recontrôle à l’application, refus des sources et approbations périmées ;
- commandes canoniques `set_asset_processing_profile` et `set_asset_external_edit` ;
- undo/redo et save/reopen séparés pour profil d’import et édition externe ;
- profil voisin, configuration externe voisine, projet, historique projet, sélection et approbations
  de la session source préservés ;
- asset local réel copié dans un workspace temporaire ; lorsqu’aucun profil/manifeste réel complet
  n’est disponible, leur fallback canonique est explicitement étiqueté et ne quitte jamais l’isolat ;
- nettoyage du workspace temporaire exigé par le gate.

## Validation locale

- build Studio Debug : **0 avertissement, 0 erreur** ;
- portail incrémental : Domain **5/5**, Application **15/15**, ModuleFormat **12/12**, soit
  **32/32** tests ciblés ;
- smoke global : `OPENRPG_FORGE_STUDIO_UX3_F_PASS`, evidence préparée et shell PASS ;
- portail incrémental : build, tests, smoke, capture FHD et matrice en **157,96 s** ;
- trois captures Forward+ **1920×1080** inspectées : navigateur, profil d’import et Blender restent
  lisibles, avec diagnostics, approbations, actions et footer visibles sans UUID ni clipping ;
- résumé machine :
  `artifacts/verification/studio-increment/ux3-f-content-browser.summary.json` ;
- captures :
  `artifacts/verification/studio-increment/ux3-f-content-browser-fhd.png`,
  `artifacts/verification/studio-increment/ux3-f-import-profile-fhd.png` et
  `artifacts/verification/studio-increment/ux3-f-blender-fhd.png` ;
- `full_gate=false` et `graph_check=false` restent explicites pour ce portail incrémental ; le
  graphe Roslyn est régénéré et contrôlé séparément après la promotion documentaire.

## Limites honnêtes

Le Studio recherche, valide, planifie et persiste. Il ne télécharge aucun asset, ne transforme ni
réimporte aucun fichier, ne charge aucune texture ou scène 3D, ne lance pas Blender, ne produit pas
de package et n’exécute aucun script, acteur, moteur, serveur ou transport réseau. Le watcher est
exercé sur une copie et retourne un plan typé ; il ne surveille pas la source réelle pendant la
preuve.

## Reproduction

```powershell
$projects = @(
  'tests/Forge.Domain.Tests/Forge.Domain.Tests.csproj'
  'tests/Forge.Application.Tests/Forge.Application.Tests.csproj'
  'tests/Forge.ModuleFormat.Tests/Forge.ModuleFormat.Tests.csproj'
)
$filter = 'FullyQualifiedName~AssetExternalEditTests|' +
  'FullyQualifiedName~ContentResourceRegistryTests|' +
  'FullyQualifiedName~AssetProcessingProfileCommandTests|' +
  'FullyQualifiedName~AssetExternalEditCommandTests|' +
  'FullyQualifiedName~AssetExternalEditWatchServiceTests|' +
  'FullyQualifiedName~AssetCatalogBrowserTests|' +
  'FullyQualifiedName~UnifiedContentBrowserTests|' +
  'FullyQualifiedName~AssetProcessingProfileStoreTests|' +
  'FullyQualifiedName~AssetExternalEditStoreTests|' +
  'FullyQualifiedName~BlenderWorkerRequestStoreTests|' +
  'FullyQualifiedName~AssetRoundTripFinalizerTests'
& .\scripts\Verify-StudioIncrement.ps1 -Page assets `
  -Evidence ux3-f-content-browser `
  -Workspace apps/Forge.Runtime/content/crypt-of-ashes `
  -TestProject $projects -TestFilter $filter `
  -Capture artifacts/verification/studio-increment/ux3-f-content-browser-fhd.png

& .\scripts\Open-Studio.ps1 -Page assets -Evidence ux3-f-import-profile `
  -Workspace apps/Forge.Runtime/content/crypt-of-ashes `
  -Resolution 1920x1080 -SkipBuild -Wait `
  -Capture artifacts/verification/studio-increment/ux3-f-import-profile-fhd.png

& .\scripts\Open-Studio.ps1 -Page assets -Evidence ux3-f-blender `
  -Workspace apps/Forge.Runtime/content/crypt-of-ashes `
  -Resolution 1920x1080 -SkipBuild -Wait `
  -Capture artifacts/verification/studio-increment/ux3-f-blender-fhd.png
```
