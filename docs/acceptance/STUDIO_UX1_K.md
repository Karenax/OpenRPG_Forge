# Acceptation Studio — UX1-K Animation initiale

État local au 16 août 2026 : **fonctionnel et qualifié** pour `ORF-PLC-007`. Ce lot rend l’état
visuel initial et ses transitions directement éditables dans l’unique fenêtre `Forge.Studio`. Il ne
qualifie aucune autre route placeable ni une distribution publique.

## Preuves communes

- build `Forge.Studio` : zéro avertissement et zéro erreur ;
- tests ciblés : Application R093 1/1, RPG Kit Placeable Operations 5/5, ModuleFormat 1/1, MCP 1/1
  et conservation des 66 fenêtres historiques 2/2, soit 10/10 ;
- formatage limité aux sources UX1-K réussi puis vérifié sans changement ;
- portail intégré `scripts/Verify.ps1` réussi : 1 947 tests .NET, 39 tests Python, build sans
  avertissement, graphe Roslyn déterministe à 8 734 nœuds et 56 519 relations, puis smokes Studio,
  Runtime, réseau, serveur, assets et MCP jusqu’à `OPENRPG_FORGE_VERIFICATION=PASS` ;
- smoke Godot 4.7.1 .NET du Studio :
  `OPENRPG_FORGE_STUDIO_UX1K_PASS initial_state=true six_states=true animations_overrides=true
  interactable=true transitions=true transition_preview=true dry_run=true command_parity=true
  instance_selection=true preservation=true runtime_projection=true script_read=true
  optional_required=true undo_redo=true persistence=true invalid_blocked=true human_no_uuid=true
  ui=true ui_transaction=true project_restored=true windows=1` ;
- le même lancement conserve UX1-H, UX1-I et UX1-J au vert puis termine par
  `OPENRPG_FORGE_STUDIO_SHELL_PASS sections=8 pages=36 windows=1` ;
- le smoke contractuel crée un workspace temporaire avec deux placeables et deux instances, refuse
  les machines incomplètes ou ambiguës, compare le dry-run direct à la commande canonique, applique,
  annule, rétablit, sauvegarde puis rouvre ;
- le smoke UI crée réellement une machine d’état dans Forgotten Forest, applique puis annule en
  restaurant exactement le projet courant ;
- captures et journaux Forward+ inspectés dans `artifacts/verification/studio-shell/ux1-k/` :
  `initial-fhd.png` en 1920×1080, `states-qhd.png` en 2560×1440 et `transition-4k.png` en
  3840×2160, chacune accompagnée de `.png.log`.

Les trois captures utilisent Godot 4.7.1, Vulkan Forward+, une Radeon RX 7800 XT, 278 assets, zéro
fallback et une seule fenêtre. Full HD montre le choix initial et les six cartes d’état avec les
actions principales visibles. QHD montre les six apparences, leur droit d’interaction et le début
des transitions ; les huit actions placeable tiennent dans l’en-tête. 4K montre deux transitions,
leurs durées et conditions, les actions d’ajout et d’aperçu, ainsi que le résultat humain
`Fermé → Actif · 0,65 s` sans coupe.

### ORF-PLC-007

La page **Objets & interactions** expose désormais **Animation initiale** à côté des autres fiches
placeable. `Ctrl+K` fournit aussi **Configurer l’animation initiale**. Les deux entrées ouvrent le
même document canonique, sans UUID, hash ni fichier généré.

Le formulaire choisit l’état affiché au chargement parmi **Au repos**, **Ouvert**, **Fermé**,
**Brisé**, **Actif** et **Personnalisé**. Chaque carte édite son animation, son apparence de surcharge
et l’autorisation des interactions. Les états supplémentaires déjà écrits sont conservés. Les
transitions sont ajoutables et retirables par cartes : départ et arrivée humains, déclencheur,
durée et condition facultative. **Prévisualiser la première transition** affiche immédiatement le
passage et sa durée sans écrire le module.

**Prévisualiser** appelle `SetWorldFeatureCatalogHandler` avec `DryRun=true`. Le résumé montre l’état
initial, le nombre d’états et de transitions, la politique runtime et les instances réelles. Le
panneau d’aperçu explique explicitement que les mêmes données sont validées pour le runtime.
**Appliquer au module** réutilise le handler à la révision courante et rejoint
`ProjectCommandHistory`, donc undo/redo et sauvegarde explicite restent atomiques.

Le domaine exige les six états, un état initial résolu, des identités uniques et cachées, une
animation et une apparence par état, puis des transitions référant des états connus avec un
déclencheur et une durée finie entre 0 et 3 600 secondes. Les diagnostics Studio sont français et ne
montrent ni UUID ni chemin interne. Une machine déjà présente ne peut pas être retirée
accidentellement depuis cette fiche. L’exigence runtime est synchronisée avec la fiche Conteneur,
car les deux éditent le même profil d’opérations canonique.

`PlaceableOperationsRuntimeAdapter` projette état initial, animations, apparences, interactions et
transitions dans le RPG Kit. Le smoke exécute réellement `closed + power-on + power.available`,
obtient `active` et une durée de `0,65 s`. `WorldFeatureScriptService`, donc la ressource MCP
filtrée, expose `initialVisualState`, `visualStates`, `visualTransitions`,
`visualStateAnimations`, `visualStateOverrides`, `interactableVisualStates` et
`visualTransitionTriggers` ; le plan MCP reste un dry-run. L’absence optionnelle des opérations
produit un repli propre et leur absence requise produit
`WORLD_PLACEABLE_OPERATIONS_REQUIRED_MISSING`.

Les données hors périmètre restent conservées : identité et provenance du profil, faction,
événements réactifs, textes, tags, apparence, collision, rôle statique, composants, scripts,
overrides, conteneur, dégâts, protection, verrou et placeables voisins. Les références d’animation
des nouveaux états sont fusionnées sans retirer celles déjà écrites, et la machine complète survit
au round-trip.

## Limites honnêtes

- ce lot qualifie seulement `ORF-PLC-007` ; destructibilité, protection plot et verrouillage restent
  portés par leurs routes dédiées ;
- l’aperçu montre le passage sémantique et le runtime exécute sa transition déterministe ; UX1-K ne
  crée ni asset ni clip d’animation et ne promet pas de lecture GLTF pour un identifiant arbitraire ;
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
& .\scripts\Open-Studio.ps1 -Page placement -Evidence ux1-k-initial -Headless -Smoke -Wait
& .\scripts\Open-Studio.ps1 -Page placement -Evidence ux1-k-initial -Resolution 1920x1080 `
  -Capture artifacts/verification/studio-shell/ux1-k/initial-fhd.png -Wait
& .\scripts\Open-Studio.ps1 -Page placement -Evidence ux1-k-states -Resolution 2560x1440 `
  -Capture artifacts/verification/studio-shell/ux1-k/states-qhd.png -Wait
& .\scripts\Open-Studio.ps1 -Page placement -Evidence ux1-k-transition -Resolution 3840x2160 `
  -Capture artifacts/verification/studio-shell/ux1-k/transition-4k.png -Wait
& .\scripts\Update-StudioAuroraAccessibilityPlan.ps1 -Check
```

Avec UX0-A à UX1-K, 55 fonctions disposent d’une preuve Studio spécifique ; les 662 autres lignes
restent explicitement `planned`.
