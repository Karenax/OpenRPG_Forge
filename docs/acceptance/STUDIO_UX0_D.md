# Acceptation Studio — UX0-D Placer et réutiliser sans friction

État local au 15 août 2026 : **fonctionnel et qualifié** pour les cinq identifiants de ce lot.
Cette acceptation porte sur les routes actives du Studio autonome. Elle ne promeut aucune page encore
planifiée, ne qualifie pas les 66 fenêtres historiques et ne vaut ni release ni publication Git.

## Preuves communes

- builds Studio et Toolset : zéro avertissement, zéro erreur ;
- tests ciblés : Domain 22/22, Application 35/35 et ModuleFormat 52/52, soit 109/109 ;
- smoke Godot 4.7.1 .NET :
  `OPENRPG_FORGE_STUDIO_UX0D_PASS palette_views=7 standard_custom=true filters=6 favorite=true
  named=true distributions=3 alignments=3 brush=true preview_no_mutation=true correction=true
  invalid_collision_socket_budget=true grouped_undo=true clipboard_copy_cut_paste=true
  independent_copy=true duplicate_group=true ui_ghost=true human_no_uuid=true windows=1
  persisted=false` ;
- rendu Forward+ réel de Clairval : 291 entités, 278 assets gouvernés et zéro fallback ; palette,
  placement valide/invalide, presse-papiers et brosse ont été inspectés en Full HD ou QHD ;
- graphe Roslyn généré depuis les sources puis contrôlé déterministe : 8 527 nœuds,
  53 769 relations et zéro diagnostic ;
- portail intégré : `scripts/Verify.ps1` termine par `OPENRPG_FORGE_VERIFICATION=PASS`, avec
  1 917 tests .NET, 39 tests Python et zéro paquet vulnérable ;
- préférences de workspace préservées : les SHA-256 de `build-graph.json`, `editor-camera.json`,
  `editor-profile.json`, `editor-profiles.json` et `semantic-snapshot.json` restent identiques ;
  `editor-keymap.json` reste absent ;
- journal headless : `artifacts/verification/studio-shell/ux0-d/headless-smoke.log` ;
- journal Forward+ : `artifacts/verification/studio-shell/ux0-d/forwardplus.log` ;
- captures : `palette-fhd.png`, `placement-qhd.png`, `invalid-fhd.png`, `clipboard-qhd.png` et
  `brush-fhd.png` dans `artifacts/verification/studio-shell/ux0-d/`.

Le smoke headless utilise volontairement le repli GL et ses modèles de secours ; il ne sert pas de
preuve d’asset. La preuve spatiale qualifiante est le journal Forward+ avec `fallback_assets=0`.

### ORF-SHELL-004

L’inspecteur **Ajouter un objet** est devenu une palette contextuelle intégrée à la fenêtre produit.
Il propose sept vues typées, la recherche et six familles de filtres nommés : type, kit, style,
licence, compatibilité et tags, auxquelles s’ajoute **Favoris seulement**. La route est
`StudioAddEntityInspector` → `StudioSessionController.BuildBlueprintPalette` →
`BlueprintPaletteCatalog.Filter`. Les filtres sont appliqués par commande de profil et ne sont
stockés atomiquement que lorsque la personne choisit **Enregistrer les réglages**.

### ORF-SHELL-005

Chaque carte indique sa provenance et son caractère modifiable ou protégé sans exposer de chemin,
hash ou identifiant. Les contenus Standard restent en lecture seule ; les favoris personnels,
récents, contenus du projet, générés, temporaires et palettes nommées restent distincts. Ajouter ou
retirer un favori et enregistrer ou supprimer une palette nommée passent par le profil de workspace,
son historique et sa persistance explicite. Forgotten Forest ne contient actuellement aucun élément
`InstalledPack` : la vue existe et son état vide est honnête, sans inventer de contenu.

### ORF-SHELL-009

Le panneau permet ligne, grille, dispersion ou brosse, avec quantité, espacement, colonnes, graine,
alignement, décalage de surface, raccord et budget. L’aperçu ne modifie pas le projet. Le trajet est
`StudioAddEntityInspector` ou `StudioWorldViewport` → `RepeatedPlacementPlanner` →
`PlacementValidator` → dry-run `PasteEntitiesHandler` → une unique `PasteEntitiesCommand`.
Un clic applique tout le lot comme une seule opération annulable ; un trait de brosse est également
groupé, et le mode continu arme le trait suivant sans sauvegarder implicitement le module.

### ORF-SHELL-010

Les poses valides ont un fantôme vert, les poses refusées un fantôme rouge et un message français
proposant une correction déterministe. Collision, grille, raccord absent et budget cumulé sont
contrôlés avant mutation par `PlacementValidator`, puis rejoués par le handler au moment de la
commande. Le message de collision ne divulgue plus l’UUID de l’objet occupant.

### ORF-SHELL-014

Le menu contextuel et les raccourcis `Ctrl+C`, `Ctrl+X`, `Ctrl+V` et `Ctrl+D` opèrent sur toute la
sélection. `SemanticClipboard`, `CutEntitiesHandler` et `PasteEntitiesHandler` préservent les champs
non affichés. Le collage propose **instances** ou **copies indépendantes** ; ces dernières reçoivent
de nouveaux objets et blueprints et leurs références internes sont remappées. Couper, coller et
dupliquer sont des opérations groupées avec undo/redo. Le presse-papiers est sémantique et interne au
Studio ; aucune interopérabilité avec le presse-papiers du système d’exploitation n’est revendiquée.
Les anciens profils à treize commandes sont migrés en mémoire vers quatorze commandes sans perdre
leurs raccourcis existants.

## Reproduction

```powershell
dotnet test tests/Forge.Domain.Tests/Forge.Domain.Tests.csproj --filter `
  'FullyQualifiedName~EditorKeymapProfileTests|FullyQualifiedName~BlueprintProvenanceTests|FullyQualifiedName~BlueprintAuthoringMetadataTests'
dotnet test tests/Forge.Application.Tests/Forge.Application.Tests.csproj --filter `
  'FullyQualifiedName~BlueprintCatalogTests|FullyQualifiedName~RepeatedPlacementPlannerTests|FullyQualifiedName~PlacementValidatorTests|FullyQualifiedName~SemanticClipboardWorkflowTests|FullyQualifiedName~EditorWorkspaceProfilePlannerTests'
dotnet test tests/Forge.ModuleFormat.Tests/Forge.ModuleFormat.Tests.csproj --filter `
  'FullyQualifiedName~EditorKeymapProfileStoreTests|FullyQualifiedName~EditorWorkspaceProfileStoreTests|FullyQualifiedName~BlueprintAuthoringPersistenceTests|FullyQualifiedName~ProjectJsonSerializerTests'
& .\scripts\Open-Studio.ps1 -Headless -Smoke -Evidence ux0-d -Wait
& .\scripts\Open-Studio.ps1 -Resolution 1920x1080 -Evidence ux0-d-palette `
  -Capture artifacts/verification/studio-shell/ux0-d/palette-fhd.png -Wait
& .\scripts\Open-Studio.ps1 -Resolution 2560x1440 -Evidence ux0-d-placement `
  -Capture artifacts/verification/studio-shell/ux0-d/placement-qhd.png -Wait
& .\scripts\Update-StudioAuroraAccessibilityPlan.ps1 -Check
& .\scripts\Verify.ps1
```

Avec UX0-A, UX0-B et UX0-C, vingt fonctions disposent désormais d’une preuve spécifique ; les
697 autres lignes restent explicitement `planned`.
