# Acceptation Studio — UX1-J Transformation précise

État local au 16 août 2026 : **fonctionnel et qualifié** pour `ORF-PLC-006`. Ce lot rend la
transformation complète d’un placeable directement éditable dans l’unique fenêtre `Forge.Studio`.
Il ne qualifie aucune autre route placeable ni une distribution publique.

## Preuves communes

- build `Forge.Studio` : zéro avertissement et zéro erreur ;
- tests ciblés : Application R046 1/1, Domain R046 1/1, RPG Kit 3/3, ModuleFormat 1/1 et MCP 1/1,
  soit 7/7, plus 2/2 tests de conservation des 66 fenêtres historiques ;
- formatage de la solution limité aux sources UX1-J réussi puis vérifié sans changement ;
- smoke Godot 4.7.1 .NET du Studio :
  `OPENRPG_FORGE_STUDIO_UX1J_PASS coordinates=true pivot_snap=true parentage=true surface=true
  animation=true geometry_guard=true dry_run=true command_parity=true instance_selection=true
  preservation=true runtime_projection=true script_read=true optional_required=true undo_redo=true
  persistence=true invalid_blocked=true human_no_uuid=true ui=true ui_transaction=true
  project_restored=true windows=1` ;
- le même lancement termine par `OPENRPG_FORGE_STUDIO_SHELL_PASS sections=8 pages=36 windows=1` ;
- le smoke contractuel crée un workspace temporaire avec un placeable, un parent et deux instances,
  refuse les transformations invalides, compare la construction directe à la commande canonique,
  applique, annule, rétablit, sauvegarde et rouvre ;
- le smoke UI édite réellement position, rotation, échelle, pivot, grille, surface et animation dans
  Forgotten Forest, applique puis annule en restaurant exactement le projet courant ;
- captures et journaux Forward+ inspectés dans `artifacts/verification/studio-shell/ux1-j/` :
  `transform-fhd.png` en 1920×1080, `pivot-qhd.png` en 2560×1440 et
  `parent-animation-4k.png` en 3840×2160, chacune accompagnée de `.png.log`.

Les trois captures chargent 278 assets sans fallback, conservent une fenêtre unique et finissent
avec un code de sortie nul. Full HD montre les coordonnées complètes et le résumé prévisualisé. QHD
centre le pivot, la grille, le parent et l’alignement. 4K conserve la transformation, le sélecteur de
parent, l’animation et les actions principales sans coupe. Le formulaire reste explicitement
défilant ; les sept entrées placeable tiennent dans l’en-tête QHD.

### ORF-PLC-006

La page **Objets & interactions** expose désormais **Transformation** à côté des autres fiches
placeable. `Ctrl+K` fournit aussi **Régler la transformation précise**. Les deux entrées ouvrent le
même document canonique, sans UUID, hash ni fichier généré.

Le formulaire édite position, rotation, échelle, pivot et pas de grille avec une précision au
centième. Le parent est choisi par son nom parmi les autres placeables valides ; la racine reste un
choix explicite. L’alignement à la surface et l’animation de transformation complètent la fiche.
**Sélectionner les instances** synchronise les instances réelles du blueprint avec le gizmo 3D.

**Prévisualiser** appelle `SetWorldFeatureCatalogHandler` avec `DryRun=true`. Le résumé montre la
pose demandée, le parent humain, l’alignement, l’animation et la position résolue par la grille sans
écrire le module. **Appliquer au module** réutilise le même handler à la révision courante et rejoint
`ProjectCommandHistory`, donc undo/redo et sauvegarde explicite restent atomiques.

Le domaine refuse les coordonnées hors limites, échelles nulles ou excessives, pas de grille
invalide, parent absent, auto-parentage et cycles hiérarchiques. Une animation saisie est fusionnée
dans la politique de validation runtime sans retirer les références déjà écrites. Les diagnostics
Studio sont français et ne montrent ni UUID ni chemin interne.

`PlaceableOperationsRuntimeAdapter` projette la transformation complète dans le modèle RPG Kit.
`WorldFeatureRuntimeResolver` fournit la pose alignée sur la grille et un overlay invariant contenant
position, rotation, échelle, pivot, grille, parent, surface et animation. `WorldFeatureScriptService`,
donc la ressource MCP filtrée, expose les mêmes propriétés ; le plan MCP reste un dry-run. L’absence
optionnelle de la fondation produit un repli propre et son absence requise produit
`WORLD_PLACEABLE_FOUNDATION_REQUIRED_MISSING`.

Les données hors périmètre restent conservées : identité du profil et de la fondation, faction,
événements réactifs, textes, tags, apparence, collision, rôle statique, composants, scripts,
overrides, opérations et placeables voisins. Le parent et l’animation survivent aussi au round-trip.

## Limites honnêtes

- ce lot qualifie seulement `ORF-PLC-006` ; l’état initial et l’animation restent `planned` sous
  `ORF-PLC-007` ;
- le gizmo 3D manipule les instances sélectionnées tandis que cette fiche édite la transformation du
  blueprint ; UX1-J ne crée ni asset ni animation ;
- l’application demeure en mémoire jusqu’à l’enregistrement explicite ;
- la migration des 66 fenêtres, l’export autonome Windows/Linux et le retrait du plugin historique
  restent hors de ce lot.

## Reproduction

```powershell
dotnet test tests/Forge.Application.Tests/Forge.Application.Tests.csproj --no-restore `
  --filter "FullyQualifiedName~R046_PlaceableValidation"
dotnet test tests/Forge.Domain.Tests/Forge.Domain.Tests.csproj --no-restore `
  --filter "FullyQualifiedName~R046_PlaceableFoundation"
dotnet test tests/Forge.RpgKit.Domain.Tests/Forge.RpgKit.Domain.Tests.csproj --no-restore `
  --filter "FullyQualifiedName~PlaceableFoundationTests"
dotnet test tests/Forge.ModuleFormat.Tests/Forge.ModuleFormat.Tests.csproj --no-restore `
  --filter "FullyQualifiedName~WorldFeaturePersistenceTests.Aggregate_and_workspace_round_trips"
dotnet test tests/Forge.Mcp.Tests/Forge.Mcp.Tests.csproj --no-restore `
  --filter "FullyQualifiedName~Placeable_foundation_resource_is_filtered_typed_and_plan_is_dry_run"
dotnet format OpenRPGForge.sln --no-restore --verify-no-changes `
  --include apps/Forge.Studio src/Forge.Application/WorldFeatures/WorldFeatureScriptService.cs `
  src/Forge.Domain/WorldFeatures tests/Forge.Mcp.Tests/Projects/ForgeProjectAgentServiceTests.cs
& .\scripts\Open-Studio.ps1 -Page placement -Evidence ux1-j-transform -Headless -Smoke -Wait
& .\scripts\Open-Studio.ps1 -Page placement -Evidence ux1-j-transform -Resolution 1920x1080 `
  -Capture artifacts/verification/studio-shell/ux1-j/transform-fhd.png -Wait
& .\scripts\Open-Studio.ps1 -Page placement -Evidence ux1-j-pivot -Resolution 2560x1440 `
  -Capture artifacts/verification/studio-shell/ux1-j/pivot-qhd.png -Wait
& .\scripts\Open-Studio.ps1 -Page placement -Evidence ux1-j-animation -Resolution 3840x2160 `
  -Capture artifacts/verification/studio-shell/ux1-j/parent-animation-4k.png -Wait
& .\scripts\Update-StudioAuroraAccessibilityPlan.ps1 -Check
```

Avec UX0-A à UX1-J, 54 fonctions disposent d’une preuve Studio spécifique ; les 663 autres lignes
restent explicitement `planned`.
