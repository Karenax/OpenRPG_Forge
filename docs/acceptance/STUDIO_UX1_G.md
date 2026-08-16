# Acceptation Studio — UX1-G Objet utilisable

État local au 16 août 2026 : **fonctionnel et qualifié** pour `ORF-PLC-003`. Ce lot rend
l’interaction d’un placeable directement éditable dans l’unique fenêtre `Forge.Studio`. Il ne
qualifie pas les autres routes placeable ni une distribution publique.

## Preuves communes

- build `Forge.Studio` : zéro avertissement et zéro erreur ;
- tests ciblés : Domain 2/2, Application 2/2, RPG Kit 3/3, ModuleFormat 1/1 et MCP 1/1, soit 9/9 ;
- smoke Godot 4.7.1 .NET du Studio :
  `OPENRPG_FORGE_STUDIO_UX1G_PASS interactable_role=true verbs=true range_focus=true
  authority_conditions=true on_used=true dry_run=true command_parity=true
  instance_selection=true preservation=true runtime_event=true optional_required=true
  undo_redo=true persistence=true invalid_blocked=true human_no_uuid=true ui=true
  ui_transaction=true project_restored=true windows=1` ;
- le même lancement termine par `OPENRPG_FORGE_STUDIO_SHELL_PASS sections=8 pages=36 windows=1` ;
- le smoke contractuel crée un workspace temporaire avec un objet utilisable et deux instances,
  compare le brouillon direct à la commande canonique, applique, annule, rétablit, sauvegarde et
  rouvre le document ;
- le smoke UI applique deux actions et `OnUsed` depuis l’overlay réel de Forgotten Forest, puis
  annule et restaure exactement le projet courant ;
- journal headless : `artifacts/verification/studio-shell/ux1-g/headless-smoke.log` ;
- captures et journaux Forward+ inspectés dans `artifacts/verification/studio-shell/ux1-g/` :
  `actions-fhd.png` en 1920×1080, `focus-qhd.png` en 2560×1440 et `event-4k.png` en 3840×2160.

Les trois captures chargent 278 assets sans fallback et conservent une fenêtre unique. Full HD
montre les deux cartes d’actions, leurs événements et leurs conditions. QHD montre portée, angle,
durée, focus et autorité. 4K montre les conditions globales et le contrat `OnUsed` complet. Le
formulaire reste explicitement défilant et les boutons de prévisualisation, application, undo et
redo ne sont pas coupés.

### ORF-PLC-003

La page **Objets & interactions** expose désormais **Objet utilisable** à côté de **Blueprint de
décor** et **Décor statique**. `Ctrl+K` fournit également **Configurer un objet utilisable**. Cette
entrée ouvre directement le rôle interactif et place les actions proposées au joueur au premier
plan, sans UUID ni chemin de document.

Chaque action est une carte ajoutable ou retirable avec nom visible, événement et condition. La
fiche édite aussi portée au pas de 0,05 m, angle, durée, ciblage requis, autorité serveur/
propriétaire/client et conditions globales. Le bloc **Événement d’utilisation** active `OnUsed`,
ses scripts avant/après et son exigence au runtime. Désactiver `OnUsed` désactive aussi l’exigence
runtime dans l’interface afin d’éviter de fabriquer volontairement un état contradictoire.

**Prévisualiser** appelle `SetWorldFeatureCatalogHandler` avec `DryRun=true`. Le résumé nomme le
nombre d’actions, portée, angle, ciblage, durée, autorité, conditions et état de `OnUsed`, sans écrire
le module. **Voir les instances en 3D** synchronise les instances réelles du blueprint avec la
sélection et le gizmo. L’application réutilise le même handler à la révision courante et enregistre
le résultat dans `ProjectCommandHistory`.

Le smoke compare les `InteractionVerb`, `InteractionPolicy` et `OnUsedPolicy` produits par le
brouillon à ceux de la commande appliquée. Il exécute ensuite `ExecutePlaceableUse` et vérifie acteur,
cible, résultat, événement, version ForgeScript et scripts gouvernés. L’absence optionnelle de
`OnUsed` produit `WORLD_PLACEABLE_ON_USED_UNAVAILABLE`; son absence alors qu’il est requis produit
`WORLD_PLACEABLE_ON_USED_REQUIRED_MISSING`.

Les données non affichées dans ce mode restent conservées : localisation anglaise, meshes,
matériaux et VFX, couche physique, scripts voisins, parent, animation, validation runtime, overrides,
faction, credential, événements réactifs et politique physique. Undo/redo restaurent les références
exactes ; sauvegarde, fermeture et réouverture conservent actions, interaction et `OnUsed`.

Trois invalidités sont rejouées avant toute mutation avec des messages français sans UUID, hash ni
chemin interne : aucune action, portée nulle et `OnUsed` requis mais absent. La ressource MCP filtrée
et son dry-run restent ceux du contrat canonique R046/R047 déjà testé.

## Limites honnêtes

- ce lot qualifie seulement `ORF-PLC-003` ; conteneur, identité/apparence et transformation précise
  restent `planned` sous `ORF-PLC-004`, `ORF-PLC-005` et `ORF-PLC-006` ;
- `OnUsed` réutilise le contrat canonique et les routes ForgeScript existantes ; ce lot n’ajoute pas
  un second moteur d’événements ni une mutation directe des nœuds Godot ;
- l’application demeure en mémoire jusqu’à l’enregistrement explicite ;
- la migration des 66 fenêtres, l’export autonome Windows/Linux et le retrait du plugin historique
  restent hors de ce lot.

## Reproduction

```powershell
dotnet test tests/Forge.Domain.Tests/Forge.Domain.Tests.csproj --no-restore --nologo `
  --filter "FullyQualifiedName~R046_PlaceableFoundation|FullyQualifiedName~R047_OnUsed"
dotnet test tests/Forge.Application.Tests/Forge.Application.Tests.csproj --no-restore --nologo `
  --filter "FullyQualifiedName~R046_PlaceableValidation|FullyQualifiedName~R047_PlaceableValidation"
dotnet test tests/Forge.RpgKit.Domain.Tests/Forge.RpgKit.Domain.Tests.csproj --no-restore --nologo `
  --filter "FullyQualifiedName~PlaceableFoundationTests"
dotnet test tests/Forge.ModuleFormat.Tests/Forge.ModuleFormat.Tests.csproj --no-restore --nologo `
  --filter "FullyQualifiedName~Aggregate_and_workspace_round_trips_preserve_all_world_feature_families"
dotnet test tests/Forge.Mcp.Tests/Forge.Mcp.Tests.csproj --no-restore --nologo `
  --filter "FullyQualifiedName~Placeable_foundation_resource"
& .\scripts\Open-Studio.ps1 -Page placement -Evidence ux1-g-actions -Headless -Smoke -Wait
& .\scripts\Open-Studio.ps1 -Page placement -Evidence ux1-g-actions -Resolution 1920x1080 `
  -Capture artifacts/verification/studio-shell/ux1-g/actions-fhd.png -Wait
& .\scripts\Open-Studio.ps1 -Page placement -Evidence ux1-g-focus -Resolution 2560x1440 `
  -Capture artifacts/verification/studio-shell/ux1-g/focus-qhd.png -Wait
& .\scripts\Open-Studio.ps1 -Page placement -Evidence ux1-g-event -Resolution 3840x2160 `
  -Capture artifacts/verification/studio-shell/ux1-g/event-4k.png -Wait
& .\scripts\Update-StudioAuroraAccessibilityPlan.ps1 -Check
```

Avec UX0-A à UX1-G, 51 fonctions disposent d’une preuve Studio spécifique ; les 666 autres lignes
restent explicitement `planned`.
