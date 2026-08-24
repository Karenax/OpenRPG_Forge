# Acceptation Studio — UX1-L Destructibilité

État local au 16 août 2026 : **fonctionnel et qualifié** pour `ORF-PLC-008`. Ce lot rend la santé,
l’armure, les résistances, les étapes visuelles, la destruction, les débris et la réparation
directement éditables dans l’unique fenêtre `Forge.Studio`. Il ne qualifie aucune autre route
placeable ni une distribution publique.

## Preuves communes

- build `Forge.Studio` : zéro avertissement et zéro erreur ;
- tests ciblés : Application R093 1/1, RPG Kit Placeable Operations 5/5, ModuleFormat 1/1, MCP 1/1
  et conservation des 66 fenêtres historiques 2/2, soit 10/10 ;
- formatage limité aux sources UX1-L réussi puis vérifié sans changement ;
- smoke Godot 4.7.1 .NET du Studio :
  `OPENRPG_FORGE_STUDIO_UX1L_PASS health_armor=true resistances=true visual_stages=true
  destruction_debris=true repair=true damage_preview=true dry_run=true command_parity=true
  instance_selection=true preservation=true runtime_projection=true script_read=true
  optional_required=true undo_redo=true persistence=true invalid_blocked=true human_no_uuid=true
  ui=true ui_transaction=true project_restored=true windows=1` ;
- le même lancement conserve UX1-H, UX1-I, UX1-J et UX1-K au vert, prépare une preuve valide puis
  termine par `OPENRPG_FORGE_STUDIO_SHELL_PASS sections=8 pages=36 windows=1` ;
- le smoke contractuel crée un workspace temporaire avec deux placeables et deux instances, refuse
  les profils incomplets ou ambigus, compare le dry-run direct à la commande canonique, applique,
  annule, rétablit, sauvegarde puis rouvre ;
- le smoke UI crée réellement un profil destructible dans Forgotten Forest, applique puis annule en
  restaurant exactement le projet courant ;
- captures et journaux Forward+ inspectés dans `artifacts/verification/studio-shell/ux1-l/` :
  `health-fhd.png` en 1920×1080, `resistances-qhd.png` en 2560×1440 et `destruction-4k.png` en
  3840×2160, chacune accompagnée de `.png.log`.

Les trois captures utilisent Godot 4.7.1, Vulkan Forward+, une Radeon RX 7800 XT, 278 assets, zéro
fallback et une seule fenêtre. Full HD montre 180 PV maximum, 140 PV courants, 12 points d’armure et
les résistances par nom humain. QHD montre les trois résistances, les trois étapes visuelles et les
neuf actions placeable dans l’en-tête. 4K montre l’effet de destruction, les débris, la réparation et
un aperçu de 500 dégâts de feu menant à `0/180`, détruit, avec effet et débris, sans coupe.

### ORF-PLC-008

La page **Objets & interactions** expose désormais **Dégâts** à côté des autres fiches placeable.
`Ctrl+K` fournit aussi **Configurer la destructibilité**. Les deux entrées ouvrent le même document
canonique, sans UUID, hash ni fichier généré.

Le formulaire édite les points de vie maximum et courants ainsi que l’armure. Les résistances sont
des lignes ajoutables et supprimables associant un type de dégâts humain à un pourcentage. Les étapes
visuelles sont elles aussi ajoutables et supprimables par seuil de santé. L’effet de destruction, la
définition de débris, la réparabilité et son coût complètent la fiche. Les résistances et étapes déjà
écrites au-delà des lignes présentées par défaut sont conservées.

**Aperçu des dégâts et de la réparation** applique localement un type et une quantité de dégâts. Il
affiche les PV restants, l’étape visuelle ou l’état détruit, puis l’effet, les débris et la possibilité
de réparer, sans écrire le module. **Prévisualiser** appelle `SetWorldFeatureCatalogHandler` avec
`DryRun=true`. **Appliquer au module** réutilise le handler à la révision courante et rejoint
`ProjectCommandHistory`, donc undo/redo et sauvegarde explicite restent atomiques.

Le domaine exige une santé maximum positive et bornée, une santé courante comprise entre zéro et le
maximum, une armure finie et positive ou nulle, au moins une résistance typée unique entre 0 et 100 %,
au moins une étape visuelle unique entre 0 et 100 %, ainsi qu’un effet et des débris nommés. Un coût
de réparation négatif ou non fini est refusé. Les diagnostics Studio sont français et ne montrent ni
UUID ni chemin interne. Un profil déjà présent ne peut pas être retiré accidentellement depuis cette
fiche. L’exigence runtime est synchronisée avec les autres opérations placeable canoniques.

`PlaceableOperationsRuntimeAdapter` projette le profil dans le RPG Kit. Le smoke applique réellement
100 dégâts de feu à 180 PV avec 12 d’armure et 40 % de résistance, obtient 92 PV et l’étape 75 %, puis
500 dégâts de feu pour déclencher destruction et débris ; une réparation remonte ensuite la santé à
25 PV. `WorldFeatureScriptService`, donc la ressource MCP filtrée, expose `maximumHealth`,
`currentHealth`, `damageArmor`, `damageResistances`, `visualDamageStages`, `destructionEffect`,
`debrisDefinition`, `repairable` et `repairCost` ; le plan MCP reste un dry-run. L’absence optionnelle
des opérations produit un repli propre et leur absence requise produit le diagnostic canonique.

Les données hors périmètre restent conservées : identité et provenance du profil, faction,
événements réactifs, textes, tags, apparence, collision, rôle statique, composants, scripts,
overrides, conteneur, machine d’état, protection, verrou et placeables voisins. Un profil inchangé
conserve même son objet canonique, et le profil complet survit au round-trip.

## Limites honnêtes

- ce lot qualifie seulement `ORF-PLC-008` ; la protection plot `ORF-PLC-009`, les événements et le
  verrouillage restent portés par leurs routes dédiées ;
- les étapes sont des seuils sémantiques projetés au runtime ; UX1-L ne crée ni asset ni variante de
  modèle arbitraire pour chaque seuil ;
- l’application demeure en mémoire jusqu’à l’enregistrement explicite ;
- la migration des 66 fenêtres, l’export autonome Windows/Linux et le retrait du plugin historique
  restent hors de ce lot.

## Reproduction

```powershell
dotnet test tests/Forge.Application.Tests/Forge.Application.Tests.csproj --no-restore `
  --filter "FullyQualifiedName~R093_PlaceableOperationsValidation"
dotnet test tests/Forge.RpgKit.Domain.Tests/Forge.RpgKit.Domain.Tests.csproj --no-restore `
  --filter "FullyQualifiedName~PlaceableOperationsRuntimeAdapterTests"
dotnet test tests/Forge.ModuleFormat.Tests/Forge.ModuleFormat.Tests.csproj --no-restore `
  --filter "FullyQualifiedName~WorldFeaturePersistenceTests.Aggregate_and_workspace_round_trips"
dotnet test tests/Forge.Mcp.Tests/Forge.Mcp.Tests.csproj --no-restore `
  --filter "FullyQualifiedName~Placeable_foundation_resource_is_filtered_typed_and_plan_is_dry_run"
dotnet test tests/Forge.Studio.Core.Tests/Forge.Studio.Core.Tests.csproj --no-restore `
  --filter "FullyQualifiedName~LegacyWindowMigrationTests"
& .\scripts\Open-Studio.ps1 -Page placement -Evidence ux1-l-health -Headless -Smoke -Wait
& .\scripts\Open-Studio.ps1 -Page placement -Evidence ux1-l-health -Resolution 1920x1080 `
  -Capture artifacts/verification/studio-shell/ux1-l/health-fhd.png -Wait
& .\scripts\Open-Studio.ps1 -Page placement -Evidence ux1-l-resistances -Resolution 2560x1440 `
  -Capture artifacts/verification/studio-shell/ux1-l/resistances-qhd.png -Wait
& .\scripts\Open-Studio.ps1 -Page placement -Evidence ux1-l-destruction -Resolution 3840x2160 `
  -Capture artifacts/verification/studio-shell/ux1-l/destruction-4k.png -Wait
& .\scripts\Update-StudioAuroraAccessibilityPlan.ps1 -Check
```

Avec UX0-A à UX1-L, 56 fonctions disposent d’une preuve Studio spécifique ; les 661 autres lignes
restent explicitement `planned`.
