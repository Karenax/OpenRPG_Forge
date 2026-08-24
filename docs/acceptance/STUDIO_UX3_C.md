# Studio UX3-C — découverte, miniatures et dépôt

Qualification Studio de quatre routes Palette sur la page `blueprints`. La surface « Découverte
de blueprints » sépare explicitement deux transactions : les favoris/récents personnels du profil
workspace, puis le dépôt d'une instance dans le projet. Chacune garde son aperçu, son approbation,
son historique et sa persistance propres.

<a id="orf-pal-009"></a>
## ORF-PAL-009

Les favoris personnels et les blueprints récemment utilisés sont affichés par nom humain et mis à
jour via `EditorWorkspaceProfilePlanner`. Leur aperçu et leur application ne modifient pas le projet.
Undo/redo et la persistance du profil workspace sont exercés séparément de la transaction de dépôt.

<a id="orf-pal-010"></a>
## ORF-PAL-010

La carte sélectionnée montre les métadonnées de prévisualisation gouvernées. Une image locale valide
est réellement décodée et rendue ; un fichier absent ou invalide produit un fallback sémantique
explicite, sans crash ni chargement implicite d'asset. La preuve positive utilise uniquement une
miniature PNG temporaire créée dans le workspace isolé.

<a id="orf-pal-011"></a>
## ORF-PAL-011

La recherche combine texte, vue, type de blueprint, kit, style, licence, compatibilité et tags. Les
résultats parcourent le catalogue complet, gardent l'ordre canonique et n'exposent aucun UUID. Les
filtres vides ou incompatibles sont rendus honnêtement, sans synthétiser de résultat produit.

<a id="orf-pal-012"></a>
## ORF-PAL-012

Chaque carte fournit un vrai payload Godot `Dictionary` versionné. La cible de dépôt le parse par la
même voie que `_CanDropData`/`_DropData`, puis `BlueprintDropPlanner` valide le blueprint, la zone
active, la cible et la correction de grille. `PlaceRepeatedEntities` reste l'autorité canonique de
dry-run et d'application ; l'instance n'est créée qu'après approbation fraîche.

## Transactions et préservation

- sélection explicite de la carte et de la zone active ; aucun premier élément implicite ;
- approbations indépendantes pour le profil workspace et le placement projet ;
- révision, empreinte SHA-256 du projet et empreinte binaire typée du brouillon recontrôlées à
  l'application ;
- dry-run sans mutation, refus d'une source ou approbation périmée, puis apply/undo/redo/save-reopen ;
- autres blueprints, zones, entités existantes, tails de la zone cible, module, dialogues, quêtes et
  profil workspace préservés ;
- preuve riche dans un workspace temporaire isolé, avec source, historique, dirty state, sélection,
  approbations et surface restaurés, puis ressources supprimées de façon bornée.

## Validation locale

- build Studio Debug : **0 avertissement, 0 erreur** ;
- Application : **19/19** tests ciblés ;
- ModuleFormat : **3/3** tests ciblés ;
- Studio.Core : **1/1** test ciblé ;
- smoke global : `OPENRPG_FORGE_STUDIO_UX3_C_PASS`, approbations globales et shell PASS ;
- capture Forward+ **1920×1080** inspectée : miniature réelle, recherche/filtres, cartes défilables,
  favoris, récents, diff, diagnostics, cible drag-and-drop et actions des deux transactions visibles,
  sans UUID ni clipping bloquant ;
- portail incrémental : build, **23/23** tests, smoke, capture FHD et matrice en **153,00 s** ;
- résumé machine :
  `artifacts/verification/studio-increment/ux3-c-blueprint-discovery.summary.json` ;
- capture :
  `artifacts/verification/studio-increment/ux3-c-blueprint-discovery-forward-plus-fhd.png` ;
- `full_gate=false` reste explicite : cette preuve incrémentale ne remplace pas le portail
  transversal complet.

## Limites honnêtes

Studio indexe, filtre et persiste des métadonnées, des préférences et une instance de blueprint. Il
ne charge ni asset, prefab ou scène, n'exécute aucun script, acteur, moteur, serveur ou transport
réseau. La miniature positive appartient uniquement à la preuve temporaire ; le module réel reste
affiché honnêtement avec son fallback lorsqu'aucune prévisualisation locale gouvernée n'existe.

## Reproduction

```powershell
$projects = @(
  'tests/Forge.Application.Tests/Forge.Application.Tests.csproj'
  'tests/Forge.ModuleFormat.Tests/Forge.ModuleFormat.Tests.csproj'
  'tests/Forge.Studio.Core.Tests/Forge.Studio.Core.Tests.csproj'
)
$filter = 'FullyQualifiedName~BlueprintLibraryIndexTests|' +
  'FullyQualifiedName~BlueprintDropPlannerTests|' +
  'FullyQualifiedName~EditorWorkspaceProfilePlannerTests|' +
  'FullyQualifiedName~BlueprintAuthoringPersistenceTests'
& .\scripts\Verify-StudioIncrement.ps1 -Page blueprints `
  -Evidence ux3-c-blueprint-discovery `
  -TestProject $projects -TestFilter $filter `
  -Capture artifacts/verification/studio-increment/ux3-c-blueprint-discovery-forward-plus-fhd.png
```
