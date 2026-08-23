# Acceptation Forge Studio — UX6-T

Date de qualification : 21 août 2026.

UX6-T rend accessibles les deux profils modernes déjà canoniques dans
`ModernRuntimeToolingProfile`. La surface **AOE & NUI 2.0** vit sur la page Runtime et sépare
l’indicateur de zone d’effet du toolkit NUI, tout en les appliquant ensemble par
`SetRuntimeExtensionsHandler` après aperçu sec et approbation.

## ORF-EE-022

### Indicateur de zone d’effet

- sélection complète des formes cercle, cône, rectangle et ligne, sans identifiant technique ;
- portée maximale de 0,5 à 250 m et écart vertical de 0 à 100 m, saisis sans arrondi silencieux ;
- couverture, confirmation serveur, palettes standard/deutéranopie/tritanopie/contraste élevé et
  couleurs RGB/RGBA ;
- paramètres d’aperçu distincts pour forme, portée, rayon, largeur, angle et confirmation ;
- `AreaEffectPreviewService` exerce réellement en mémoire filtrage géométrique, hauteur,
  couverture, ciblabilité et décision serveur sur des candidats temporaires ;
- aucun mesh Godot, décalque, raycast, acteur du module ou échange réseau n’est créé dans Studio.

## ORF-EE-024

### Améliorations NUI

- version `2.0` protégée et persistée avec l’identité existante du profil ;
- capacités explicites : layout responsive, formulaires, listes, canvas, accessibilité,
  localisation et inspecteur développeur ;
- inventaire humain du nombre de documents et widgets runtime existants, strictement en lecture ;
- les règles, documents UI/JSON, bindings, événements et snapshots voisins restent inchangés ;
- aucun `RuntimeDeclarativeUi`, `Control` Godot, binding JSON ou événement joueur n’est instancié
  par l’aperçu Studio.

## Transaction et préservation

Le brouillon est lié à la révision et à une empreinte SHA-256 binaire, typée et préfixée par
longueur. L’aperçu appelle la commande en `DryRun`, puis l’application refait ce dry-run avant
l’unique mutation. Undo, redo, sauvegarde et réouverture sont prouvés sur un workspace temporaire
copiant le module réel.

La reconstruction remplace uniquement `ModernTooling.AreaEffectIndicator` et
`ModernTooling.UiToolkit`. Copie runtime, compilateur, documentation, qualité, readiness,
PostLaunch, règles, documents UI/JSON, snapshot et toutes les racines voisines du projet sont
préservés. Les approbations UX6 voisines, la zone/sélection, l’historique, la révision et l’état
dirty de la source sont restaurés et comparés.

## Validation obtenue

- build Studio Debug : **0 avertissement, 0 erreur** ;
- tests ciblés : **15/15** — Domain 3, Application 5, ModuleFormat 7 ;
- smoke global : `OPENRPG_FORGE_STUDIO_UX6_T_PASS`, avec invalidités forme/portée/couleur/version,
  dry-run, résolutions AOE/NUI, révision et approbation obsolètes, commande canonique, undo/redo,
  round-trip, voisins, texte humain, transaction UI, libération des ressources, source inchangée et
  surface restaurée ;
- portail incrémental : **147,77 s**, build, tests, smoke global, capture Forward+ FHD et contrôle
  des 717 lignes de matrice ; `full_gate=false` reste explicite ;
- résumé machine :
  `artifacts/verification/studio-increment/ux6-t-aoe.summary.json` ;
- deux captures Forward+ 1920×1080 inspectées :
  `ux6-t-aoe-forward-plus-fhd.png` et `ux6-t-nui-forward-plus-fhd.png`.

Les deux onglets, les couleurs, l’aperçu runtime, le diff humain et les cinq actions
transactionnelles sont visibles sans clipping. Aucun UUID n’est affiché et les limites runtime
restent visibles dans chaque onglet.

## Reproduction

```powershell
$projects = @(
  'tests/Forge.Domain.Tests/Forge.Domain.Tests.csproj'
  'tests/Forge.Application.Tests/Forge.Application.Tests.csproj'
  'tests/Forge.ModuleFormat.Tests/Forge.ModuleFormat.Tests.csproj'
)
$filter = 'FullyQualifiedName~ModuleRuntimeExtensionProfileTests|' +
  'FullyQualifiedName~ModernRuntimeToolingTests|' +
  'FullyQualifiedName~SetRuntimeExtensionsHandlerTests|' +
  'FullyQualifiedName~RuntimeExtensionPersistenceTests'
& .\scripts\Verify-StudioIncrement.ps1 -Page runtime `
  -Evidence ux6-t-aoe -TestProject $projects -TestFilter $filter `
  -Capture artifacts/verification/studio-increment/ux6-t-aoe-forward-plus-fhd.png
& .\scripts\Open-Studio.ps1 -Page runtime -Evidence ux6-t-nui -Resolution 1920x1080 `
  -Capture artifacts/verification/studio-increment/ux6-t-nui-forward-plus-fhd.png -Wait -SkipBuild
```
