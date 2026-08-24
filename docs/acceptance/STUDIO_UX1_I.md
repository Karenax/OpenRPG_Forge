# Acceptation Studio — UX1-I Identité et apparence

État local au 16 août 2026 : **fonctionnel et qualifié** pour `ORF-PLC-005`. Ce lot rend l’identité
localisée et la définition visuelle d’un placeable directement éditables dans l’unique fenêtre
`Forge.Studio`. Il ne qualifie aucune autre route placeable ni une distribution publique.

## Preuves communes

- build `Forge.Studio` : zéro avertissement et zéro erreur ;
- tests ciblés : Application R046 1/1, adaptateur RPG Kit 5/5, ModuleFormat 5/5 et MCP 1/1,
  soit 12/12, plus 2/2 tests de conservation des 66 fenêtres historiques ;
- formatage de la solution limité aux sources UX1-I réussi ;
- smoke Godot 4.7.1 .NET du Studio :
  `OPENRPG_FORGE_STUDIO_UX1I_PASS identity=true localization=true appearance=true metadata=true
  dry_run=true command_parity=true instance_selection=true preservation=true
  runtime_projection=true script_read=true optional_required=true undo_redo=true persistence=true
  invalid_blocked=true human_no_uuid=true ui=true ui_transaction=true project_restored=true windows=1` ;
- le même lancement termine par `OPENRPG_FORGE_STUDIO_SHELL_PASS sections=8 pages=36 windows=1` ;
- le smoke contractuel crée un workspace temporaire avec deux instances, modifie une fiche existante,
  compare le profil direct à la commande canonique, applique, annule, rétablit, sauvegarde et rouvre ;
- le smoke UI applique identité, langues et apparence depuis l’overlay réel de Crypte des Cendres,
  puis annule et restaure exactement le projet courant ;
- captures et journaux Forward+ inspectés dans `artifacts/verification/studio-shell/ux1-i/` :
  `identity-fhd.png` en 1920×1080, `localization-qhd.png` en 2560×1440 et `appearance-4k.png`
  en 3840×2160, chacune accompagnée de `.png.log`.

Les trois captures chargent 278 assets sans fallback, conservent une fenêtre unique et finissent
avec un code de sortie nul. Full HD montre l’identité principale, les deux traductions et l’aperçu.
QHD centre les cartes de langue, tags et notes. 4K montre meshes, matériaux, icône, portrait et VFX.
Le formulaire reste explicitement défilant et les actions principales ne sont pas coupées.

### ORF-PLC-005

La page **Objets & interactions** expose désormais **Identité & apparence** à côté des autres fiches
placeable. `Ctrl+K` fournit aussi **Configurer l’identité et l’apparence**. L’entrée ouvre le même
document canonique, sans UUID, hash ni fichier généré.

Le formulaire édite le nom principal français, le nom court, l’invitation, des cartes de traduction
ajoutables ou supprimables, les tags, les notes d’auteur, l’asset visuel principal, les meshes,
matériaux, icône, portrait et effets visuels. Un état vide explicite dérive une apparence sûre du
visuel le plus utilisé ; une fiche existante recharge toutes ses valeurs sans aplatir les langues.

**Prévisualiser** appelle `SetWorldFeatureCatalogHandler` avec `DryRun=true`. Le résumé expose le
nombre de langues, tags, meshes, matériaux et VFX sans écrire le module. **Voir les instances en 3D**
synchronise les instances réelles du blueprint avec la sélection. L’application réutilise le même
handler à la révision courante et rejoint `ProjectCommandHistory`.

Le smoke compare le `FoundationPolicy` produit directement à celui de la commande appliquée.
`PlaceableOperationsRuntimeAdapter` projette ensuite slug, textes localisés, tags, notes, meshes,
matériaux, icône, portrait et VFX dans le modèle RPG Kit. `WorldFeatureScriptService`, donc la
ressource MCP filtrée, expose les mêmes références par handle typé. L’absence optionnelle de la
fondation produit un repli propre ; son absence lorsqu’elle est requise produit
`WORLD_PLACEABLE_FOUNDATION_REQUIRED_MISSING`.

Les données hors périmètre restent conservées : identité du profil, faction, événements réactifs,
collision, rôle statique, scripts, transformation, animation, overrides, validation runtime,
machine d’états et protection. Retirer une traduction la retire réellement du document ; les autres
langues, ordres de ressources et identités stables survivent à undo/redo et au round-trip.

Trois invalidités sont refusées avant mutation avec des messages français sans UUID, hash ni chemin
interne : langue dupliquée, liste de meshes vide et portrait vide.

## Limites honnêtes

- ce lot qualifie seulement `ORF-PLC-005` ; transformation précise et état initial d’animation
  restent `planned` sous `ORF-PLC-006` et `ORF-PLC-007` ;
- l’asset 3D principal est choisi par nom humain ; les références visuelles détaillées restent des
  références gouvernées existantes, UX1-I ne crée pas leurs fichiers ;
- l’application demeure en mémoire jusqu’à l’enregistrement explicite ;
- la migration des 66 fenêtres, l’export autonome Windows/Linux et le retrait du plugin historique
  restent hors de ce lot.

## Reproduction

```powershell
dotnet test tests/Forge.Application.Tests/Forge.Application.Tests.csproj --no-restore `
  --filter "FullyQualifiedName~R046_PlaceableValidation"
dotnet test tests/Forge.RpgKit.Domain.Tests/Forge.RpgKit.Domain.Tests.csproj --no-restore `
  --filter "FullyQualifiedName~PlaceableOperationsRuntimeAdapterTests"
dotnet test tests/Forge.ModuleFormat.Tests/Forge.ModuleFormat.Tests.csproj --no-restore `
  --filter "FullyQualifiedName~WorldFeaturePersistenceTests"
dotnet test tests/Forge.Mcp.Tests/Forge.Mcp.Tests.csproj --no-restore `
  --filter "FullyQualifiedName~Placeable_foundation_resource_is_filtered_typed_and_plan_is_dry_run"
dotnet format OpenRPGForge.sln --no-restore --verify-no-changes `
  --include apps/Forge.Studio src/Forge.Application/WorldFeatures/WorldFeatureScriptService.cs `
  tests/Forge.Mcp.Tests/Projects/ForgeProjectAgentServiceTests.cs
& .\scripts\Open-Studio.ps1 -Page placement -Evidence ux1-i-identity -Headless -Smoke -Wait
& .\scripts\Open-Studio.ps1 -Page placement -Evidence ux1-i-identity -Resolution 1920x1080 `
  -Capture artifacts/verification/studio-shell/ux1-i/identity-fhd.png -Wait
& .\scripts\Open-Studio.ps1 -Page placement -Evidence ux1-i-localization -Resolution 2560x1440 `
  -Capture artifacts/verification/studio-shell/ux1-i/localization-qhd.png -Wait
& .\scripts\Open-Studio.ps1 -Page placement -Evidence ux1-i-appearance -Resolution 3840x2160 `
  -Capture artifacts/verification/studio-shell/ux1-i/appearance-4k.png -Wait
& .\scripts\Update-StudioAuroraAccessibilityPlan.ps1 -Check
```

Avec UX0-A à UX1-I, 53 fonctions disposent d’une preuve Studio spécifique ; les 664 autres lignes
restent explicitement `planned`.
