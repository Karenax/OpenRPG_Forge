# Acceptation Studio — UX1-S Trésor initial

État local au 16 août 2026 : **fonctionnel et qualifié** pour `ORF-PLC-015`. Ce lot rend la table de
trésor initial d’un placeable directement éditable et prévisualisable dans l’unique fenêtre
`Forge.Studio`. Il ne qualifie ni les événements d’ouverture `ORF-PLC-016`, ni une distribution
publique.

## Preuves communes

- build `Forge.Studio` : zéro avertissement et zéro erreur ;
- portail intégré `scripts/Verify.ps1` : `OPENRPG_FORGE_VERIFICATION=PASS`, 1 972 tests .NET,
  39 tests Python et graphe Roslyn déterministe à 8 857 nœuds, 57 792 relations, 0 diagnostic ;
- tests ciblés : adaptateur RPG Kit 7/7, validation Application 1/1, round-trip ModuleFormat 1/1 et
  ressource MCP 1/1, soit 10/10 ;
- smoke Godot 4.7.1 .NET du Studio :
  `OPENRPG_FORGE_STUDIO_UX1S_PASS table_entries=true timing_seed=true
  generated_state_preserved=true explicit_reset_guard=true loot_preview=true dry_run=true
  command_parity=true instance_selection=true preservation=true runtime_projection=true
  script_read=true optional_required=true stable_entry_selection=true undo_redo=true
  persistence=true invalid_blocked=true human_no_uuid=true ui=true ui_transaction=true
  project_restored=true windows=1` ;
- le même lancement garde UX0-A à UX1-R au vert et termine par
  `OPENRPG_FORGE_STUDIO_SHELL_PASS sections=8 pages=36 windows=1` ;
- le smoke contractuel emploie un workspace temporaire avec deux instances, un placeable voisin,
  deux blueprints Objet, deux lignes de table, un piège voisin à préserver et un état déjà généré ;
- le smoke UI applique la fiche au vrai projet Crypte des Cendres uniquement en mémoire, l’annule puis
  vérifie la restauration exacte du projet ; aucun trésor de preuve n’est sauvegardé ;
- captures Forward+ inspectées dans `artifacts/verification/studio-shell/ux1-s/` :
  `table-fhd.png` et `open-fhd.png` en 1920×1080, `reset-qhd.png` en 2560×1440 et
  `preview-4k.png` en 3840×2160, chacune accompagnée de son journal `.png.log`.

Les quatre captures utilisent Godot 4.7.1 .NET, Vulkan Forward+, une Radeon RX 7800 XT, 278 assets,
zéro fallback et une seule fenêtre. Elles montrent le blueprint réel **Décor de la forêt**, le visuel
**Floor Wood Large**, l’objet réel **Graine de lune**, les catégories et quantités, la graine 4242,
les moments ouverture/réinitialisation et le résultat déterministe à droite. La table FHD présente
deux lignes manipulées sans identifiant technique ; QHD et 4K gardent les contrôles centraux et
l’aperçu simultanément lisibles.

La persistance canonique et le garde d’état généré sont qualifiés sur le workspace synthétique afin
de ne pas modifier le contenu authored. Les captures réelles prouvent l’accès humain et le rendu,
pas la présence préalable d’un trésor sauvegardé dans Crypte des Cendres.

### ORF-PLC-015

La page **Objets & interactions** expose désormais **Trésor** après **Piège**. `Ctrl+K` fournit aussi
**Composer le trésor initial**. Les deux entrées ouvrent la même fiche sans UUID, hash ni chemin de
fichier dans la surface humaine.

L’auteur choisit les objets par leur nom, puis règle catégorie, poids relatif, quantité minimale et
maximale, qualité minimale, condition et unicité. Les lignes sont ajoutées, capturées et retirées par
leur identité interne stable dans le brouillon, jamais par leur position dans la liste. Le domaine
reste lié aux définitions d’objets canoniques et le validateur refuse objet absent, table vide, poids
non positif, catégorie vide et bornes de quantité incohérentes.

Le tirage peut intervenir **au chargement**, **à l’ouverture** ou **à la réinitialisation**. La graine
est explicite et le choix de conserver l’état généré est visible. Un profil existant conserve par
défaut sa génération et ses résultats sauvegardés. Si la table, le moment, la graine ou la politique
de conservation change alors qu’un état existe, le Studio refuse l’opération jusqu’à ce que l’auteur
coche **Réinitialiser explicitement l’état généré**. Cette action remet résultats et génération à
zéro ; elle n’est jamais implicite.

**Prévisualiser le trésor** appelle le même `LootSpawner` et la même `LootTable` que le runtime. Un
mauvais moment produit `LootSpawnSkipped`, le bon moment effectue un tirage déterministe et un
spawner persistant restaure ensuite ses résultats. L’aperçu n’écrit ni le module ni l’inventaire.

**Prévisualiser** passe par `SetWorldFeatureCatalogHandler` avec `DryRun=true`. **Appliquer au
module** réutilise le handler à la révision courante et `ProjectCommandHistory` ; l’opération reste
atomique, annulable et rétablissable. La sauvegarde explicite puis la réouverture conservent la table,
le moment, la graine, la politique et l’état. L’identité du placeable, du profil d’événements, du
piège voisin, des autres placeables et des blueprints demeure inchangée.

`LootRequiredAtRuntime` est indépendant de `TrapRequiredAtRuntime` et reste faux par défaut pour les
anciens modules. Le validateur produit `world.placeable.loot_required_missing` si une table exigée
manque. `PlaceableSecurityEventsRuntimeAdapter.ResolveLoot` et `BuildLoot` projettent le trésor sans
exiger piège, événements de conteneur, verrouillage ou changement de contenu ; `Spawn` utilise cette
projection indépendante.

`WorldFeatureScriptService`, donc la ressource MCP filtrée, conserve `lootSpawner` et `lootEntries`
et expose aussi `lootTableId`, `lootTiming`, `lootSeed`, `lootPersistGeneratedState`,
`lootGeneration`, `lootSavedResults`, `lootTableEntries` et `lootRequiredAtRuntime`. Le plan MCP
demeure un dry-run.

## Limites honnêtes

- ce lot qualifie seulement `ORF-PLC-015` ; les événements OnOpen/OnClose et les autres routes
  placeable conservent leurs lots dédiés ;
- l’aperçu calcule les résultats mais ne les insère pas dans un inventaire de scène ; cette mutation
  reste du ressort du runtime autoritaire ;
- Crypte des Cendres fournit le catalogue et la scène réels, pas un trésor authored nouvellement
  sauvegardé ; cette absence initiale est présentée explicitement dans l’interface ;
- l’application demeure en mémoire jusqu’à l’enregistrement explicite ;
- la migration des 66 fenêtres, l’export autonome Windows/Linux et le retrait du plugin historique
  restent hors de ce lot.

## Reproduction

```powershell
dotnet build OpenRPGForge.sln --no-restore
dotnet format OpenRPGForge.sln --no-restore --verify-no-changes
& .\scripts\Verify.ps1
dotnet test tests/Forge.RpgKit.Domain.Tests/Forge.RpgKit.Domain.Tests.csproj --no-build `
  --filter "FullyQualifiedName~PlaceableSecurityEventsRuntimeAdapterTests"
dotnet test tests/Forge.Application.Tests/Forge.Application.Tests.csproj --no-build `
  --filter "FullyQualifiedName~R094_PlaceableSecurityEventsValidation"
dotnet test tests/Forge.ModuleFormat.Tests/Forge.ModuleFormat.Tests.csproj --no-build `
  --filter "FullyQualifiedName~Aggregate_and_workspace_round_trips_preserve_all_world_feature_families"
dotnet test tests/Forge.Mcp.Tests/Forge.Mcp.Tests.csproj --no-build `
  --filter "FullyQualifiedName~World_feature_resource_is_filtered_bounded_and_mutation_remains_dry_run"
& .\scripts\Open-Studio.ps1 -Page placement -Evidence ux1-s-reset -Headless -Smoke -Wait
& .\scripts\Open-Studio.ps1 -Page placement -Evidence ux1-s-table -Resolution 1920x1080 `
  -Capture artifacts/verification/studio-shell/ux1-s/table-fhd.png -Wait
& .\scripts\Open-Studio.ps1 -Page placement -Evidence ux1-s-open -Resolution 1920x1080 `
  -Capture artifacts/verification/studio-shell/ux1-s/open-fhd.png -Wait
& .\scripts\Open-Studio.ps1 -Page placement -Evidence ux1-s-reset -Resolution 2560x1440 `
  -Capture artifacts/verification/studio-shell/ux1-s/reset-qhd.png -Wait
& .\scripts\Open-Studio.ps1 -Page placement -Evidence ux1-s-preview -Resolution 3840x2160 `
  -Capture artifacts/verification/studio-shell/ux1-s/preview-4k.png -Wait
& .\scripts\Update-StudioAuroraAccessibilityPlan.ps1 -Check
```

Avec UX0-A à UX1-S, 63 fonctions disposent d’une preuve Studio spécifique ; les 654 autres lignes
restent explicitement `planned`.
