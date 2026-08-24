# Acceptation Studio — UX1-F Décor statique

État local au 16 août 2026 : **fonctionnel et qualifié** pour `ORF-PLC-002`. Ce lot rend le rôle
statique du `PlaceableWorldProfile.FoundationPolicy` directement accessible dans l’unique fenêtre
`Forge.Studio`. Il ne qualifie pas les autres routes placeable ni une distribution publique.

## Preuves communes

- build `Forge.Studio` : zéro avertissement et zéro erreur ;
- tests ciblés : Domain 1/1, Application 1/1, RPG Kit 3/3, ModuleFormat 1/1 et MCP 1/1, soit 7/7 ;
- smoke Godot 4.7.1 .NET du Studio :
  `OPENRPG_FORGE_STUDIO_UX1F_PASS static_role=true optimization=true optional_collision=true
  merge_guard=true optimization_guard=true dry_run=true command_parity=true
  instance_selection=true preservation=true runtime=true undo_redo=true persistence=true
  human_no_uuid=true ui=true ui_transaction=true project_restored=true windows=1` ;
- le même lancement termine par `OPENRPG_FORGE_STUDIO_SHELL_PASS sections=8 pages=36 windows=1` ;
- le smoke contractuel crée un workspace temporaire avec un décor statique et deux instances,
  compare le résultat de l’interface à la commande canonique, applique, annule, rétablit, sauvegarde
  et rouvre le document ;
- le smoke UI applique une fusion sans collision par instance depuis l’overlay réel de Forgotten
  Forest, puis annule et restaure exactement le projet courant ;
- journal headless : `artifacts/verification/studio-shell/ux1-f/headless-smoke.log` ;
- captures et journaux Forward+ inspectés dans `artifacts/verification/studio-shell/ux1-f/` :
  `static-fhd.png` en 1920×1080, `merge-qhd.png` en 2560×1440 et `collisionless-4k.png` en
  3840×2160.

Les trois captures conservent le choix du blueprint et du visuel, l’état vide, le formulaire
défilant, l’aperçu et toutes les actions. Full HD montre l’optimisation répétable avec collision par
instance, QHD montre la fusion sûre sans collision par instance et 4K montre une collision globale
désactivée. Le scroll reste explicite et aucun contrôle principal n’est coupé.

### ORF-PLC-002

La page **Objets & interactions** expose désormais **Décor statique** à côté de **Blueprint de
décor**. `Ctrl+K` fournit également **Configurer un décor statique**. Cette entrée ouvre directement
le rôle non utilisable ; les réglages d’interaction sont masqués et les contrôles de regroupement,
instanciation, occlusion, fusion et collision par instance sont prioritaires.

Une aide visible annonce les deux invariants : au moins le regroupement ou l’instanciation doit
rester actif, et une géométrie fusionnée ne peut pas conserver de collision par instance.
**Prévisualiser** appelle `SetWorldFeatureCatalogHandler` avec `DryRun=true`, puis affiche un résumé
d’impact de rendu qui nomme chaque stratégie active. La collision du placeable reste optionnelle et
désactive alors aussi l’obstacle de navigation. **Voir les instances en 3D** synchronise les
instances réelles du blueprint avec la sélection et le gizmo du viewport.

L’application utilise le même `SetWorldFeatureCatalogHandler` à la révision courante et enregistre
le résultat dans `ProjectCommandHistory`. Le smoke compare explicitement la `StaticPropPolicy` et
la `CollisionDefinition` produites par le brouillon à celles de la commande de domaine. Undo/redo
restaurent successivement les états ; sauvegarde, fermeture et réouverture conservent le rôle, les
options de rendu et la collision exacte.

Les données qui ne sont pas affichées dans ce mode restent conservées : localisation anglaise,
meshes et matériaux, couche physique, scripts, parent, animation de transformation, validation
runtime, faction, credential et politique physique. Le runtime résout la fondation statique ; une
fondation optionnelle absente utilise le repli compatible, tandis qu’une fondation requise absente
produit `WORLD_PLACEABLE_FOUNDATION_REQUIRED_MISSING`.

Deux invalidités sont rejouées avant toute mutation avec un message français sans UUID, hash ni
chemin interne : décor sans regroupement ni instanciation, et fusion avec collision par instance.
La ressource MCP filtrée et son dry-run restent ceux du contrat canonique R046 déjà testé.

## Limites honnêtes

- ce lot qualifie seulement `ORF-PLC-002` ; l’objet utilisable, l’identité/apparence et la
  transformation précise restent `planned` sous `ORF-PLC-003`, `ORF-PLC-005` et `ORF-PLC-006` ;
- la fusion est une politique canonique gouvernée consommée par le pipeline placeable existant ;
  ce lot Studio n’ajoute pas un second pipeline de géométrie ni une mutation directe des nœuds
  Godot ;
- l’application demeure en mémoire jusqu’à l’enregistrement explicite ;
- la migration des 66 fenêtres, l’export autonome Windows/Linux et le retrait du plugin historique
  restent hors de ce lot.

## Reproduction

```powershell
dotnet test tests/Forge.Domain.Tests/Forge.Domain.Tests.csproj --no-restore --nologo `
  --filter "FullyQualifiedName~R046_PlaceableFoundation"
dotnet test tests/Forge.Application.Tests/Forge.Application.Tests.csproj --no-restore --nologo `
  --filter "FullyQualifiedName~R046_PlaceableValidation"
dotnet test tests/Forge.RpgKit.Domain.Tests/Forge.RpgKit.Domain.Tests.csproj --no-restore --nologo `
  --filter "FullyQualifiedName~PlaceableFoundationTests"
dotnet test tests/Forge.ModuleFormat.Tests/Forge.ModuleFormat.Tests.csproj --no-restore --nologo `
  --filter "FullyQualifiedName~Aggregate_and_workspace_round_trips_preserve_all_world_feature_families"
dotnet test tests/Forge.Mcp.Tests/Forge.Mcp.Tests.csproj --no-restore --nologo `
  --filter "FullyQualifiedName~Placeable_foundation_resource"
& .\scripts\Open-Studio.ps1 -Page placement -Evidence ux1-f-static -Headless -Smoke -Wait
& .\scripts\Open-Studio.ps1 -Page placement -Evidence ux1-f-static -Resolution 1920x1080 `
  -Capture artifacts/verification/studio-shell/ux1-f/static-fhd.png -Wait
& .\scripts\Open-Studio.ps1 -Page placement -Evidence ux1-f-merge -Resolution 2560x1440 `
  -Capture artifacts/verification/studio-shell/ux1-f/merge-qhd.png -Wait
& .\scripts\Open-Studio.ps1 -Page placement -Evidence ux1-f-collisionless -Resolution 3840x2160 `
  -Capture artifacts/verification/studio-shell/ux1-f/collisionless-4k.png -Wait
& .\scripts\Update-StudioAuroraAccessibilityPlan.ps1 -Check
```

Avec UX0-A à UX1-F, 50 fonctions disposent d’une preuve Studio spécifique ; les 667 autres lignes
restent explicitement `planned`.
