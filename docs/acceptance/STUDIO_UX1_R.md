# Acceptation Studio — UX1-R Piège

État local au 16 août 2026 : **fonctionnel et qualifié** pour `ORF-PLC-014`. Ce lot rend le piège
d’un placeable directement éditable et prévisualisable dans l’unique fenêtre `Forge.Studio`. Il
sépare le contrat de piège des autres événements historiquement regroupés dans le même profil et ne
qualifie ni le trésor initial `ORF-PLC-015`, ni une distribution publique.

## Preuves communes

- build complet `OpenRPGForge.sln` : zéro avertissement et zéro erreur ;
- `dotnet format --verify-no-changes` : réussi ;
- tests ciblés : adaptateur RPG Kit 6/6, validation Application 1/1, persistance ModuleFormat 6/6,
  ressource MCP 1/1 et conservation des 66 fenêtres historiques 2/2, soit 16/16 ;
- smoke Godot 4.7.1 .NET du Studio :
  `OPENRPG_FORGE_STUDIO_UX1R_PASS trigger_payload=true detect_disarm=true reset_one_shot=true
  ownership_vfx_friendly=true trap_preview=true dry_run=true command_parity=true
  instance_selection=true preservation=true runtime_projection=true script_read=true
  optional_required=true undo_redo=true persistence=true invalid_blocked=true human_no_uuid=true
  ui=true ui_transaction=true project_restored=true windows=1` ;
- le même lancement garde les lots Studio antérieurs au vert et termine par
  `OPENRPG_FORGE_STUDIO_SHELL_PASS sections=8 pages=36 windows=1` ;
- le smoke contractuel emploie un workspace temporaire avec deux instances, un placeable voisin et
  un profil d’événements caché à préserver ; il exerce détection, désamorçage, déclenchement allié et
  hostile, réarmement, usage unique, dry-run, commande, undo/redo, sauvegarde et réouverture ;
- le smoke UI applique la fiche au vrai projet Forgotten Forest uniquement en mémoire, l’annule puis
  vérifie la restauration exacte du projet ; aucun piège de preuve n’est sauvegardé ;
- captures Forward+ inspectées dans `artifacts/verification/studio-shell/ux1-r/` :
  `detection-fhd.png` et `friendly-fhd.png` en 1920×1080, `disarm-qhd.png` en 2560×1440 et
  `one-shot-4k.png` en 3840×2160, chacune accompagnée de son journal `.png.log`.

Les quatre captures utilisent Godot 4.7.1 .NET, Vulkan Forward+, une Radeon RX 7800 XT, 278 assets,
zéro fallback et une seule fenêtre. Elles montrent le blueprint réel **Décor de la forêt**, le visuel
**Floor Wood Large**, les champs humains du piège et l’aperçu calculé. Full HD rend la détection et la
politique alliée lisibles ; QHD confirme le désamorçage ; 4K montre l’usage unique. La persistance
canonique est qualifiée sur le workspace synthétique afin de ne pas modifier le contenu authored.

### ORF-PLC-014

La page **Objets & interactions** expose désormais **Piège** après **Clé consommée**. `Ctrl+K`
fournit aussi **Configurer le piège**. Les deux entrées ouvrent la même fiche sans UUID, hash ni chemin
de fichier dans la surface humaine.

L’auteur règle le déclencheur, l’effet, les difficultés de détection et de désamorçage, le délai de
réarmement, le propriétaire, la faction alliée ignorée, le VFX, l’état armé, l’usage unique et
l’exigence au runtime. Les erreurs restent rattachées à des libellés métier français.

**Prévisualiser le piège** permet de simuler une perception insuffisante ou suffisante, un
désamorçage réussi ou raté, le passage d’un allié ou d’un adversaire et le réarmement. L’aperçu
utilise `PlaceableSecurityEventsRuntimeAdapter` et le même `TrapComponent` que le runtime. Il ne
déclenche aucun effet dans la scène et n’écrit pas le module.

**Prévisualiser** passe par `SetWorldFeatureCatalogHandler` avec `DryRun=true`. **Appliquer au
module** réutilise le handler à la révision courante et `ProjectCommandHistory` ; l’opération reste
atomique, annulable et rétablissable. La sauvegarde explicite puis la réouverture conservent chaque
champ. Un profil existant garde son identité et ses sous-profils de butin, ouverture/fermeture,
verrouillage et changement de contenu ; une fiche piège seule n’invente aucun de ces sous-profils.

`PlaceableSecurityEventsProfile` accepte désormais ses cinq sous-profils indépendamment. Cette
évolution est rétrocompatible : les anciens agrégats complets restent valides, tandis qu’un piège
seul devient canonique. Le validateur refuse les identités ou valeurs invalides et produit
`world.placeable.trap_required_missing` si un piège exigé manque. Le drapeau
`TrapRequiredAtRuntime` demeure faux par défaut pour les anciens modules.

`PlaceableSecurityEventsRuntimeAdapter.ResolveTrap` et `BuildTrap` projettent le piège sans exiger le
butin ni les événements voisins. Détection, désamorçage, déclenchement, réarmement, appartenance, VFX
et politique alliée restent typés. `WorldFeatureScriptService`, donc la ressource MCP filtrée, expose
`trapTrigger`, `trapPayload`, `trapDetection`, `trapDisarm`, `trapResetSeconds`, `trapOwner`,
`trapVisualEffect`, `trapFriendlyPolicy`, `trapArmed`, `trapOneShot` et `trapRequiredAtRuntime`. Le
plan MCP demeure un dry-run.

## Limites honnêtes

- ce lot qualifie seulement `ORF-PLC-014` ; le trésor initial `ORF-PLC-015` et les autres événements
  placeable conservent leurs routes dédiées ;
- l’aperçu calcule le résultat du piège mais ne remplace pas l’application autoritaire de dégâts,
  d’effets ou de réplication par le runtime ;
- Forgotten Forest fournit le catalogue et la scène réels, pas un piège authored nouvellement
  sauvegardé ; cette absence initiale est présentée explicitement dans l’interface ;
- l’application demeure en mémoire jusqu’à l’enregistrement explicite ;
- la migration des 66 fenêtres, l’export autonome Windows/Linux et le retrait du plugin historique
  restent hors de ce lot.

## Reproduction

```powershell
dotnet build OpenRPGForge.sln --no-restore
dotnet format OpenRPGForge.sln --no-restore --verify-no-changes
dotnet test tests/Forge.RpgKit.Domain.Tests/Forge.RpgKit.Domain.Tests.csproj --no-build `
  --filter "FullyQualifiedName~PlaceableSecurityEventsRuntimeAdapterTests"
dotnet test tests/Forge.Application.Tests/Forge.Application.Tests.csproj --no-build `
  --filter "FullyQualifiedName~R094_PlaceableSecurityEventsValidationLocalizesTrapLootLifecycleDeltaAndRequirementFailures"
dotnet test tests/Forge.ModuleFormat.Tests/Forge.ModuleFormat.Tests.csproj --no-build `
  --filter "FullyQualifiedName~WorldFeaturePersistenceTests"
dotnet test tests/Forge.Mcp.Tests/Forge.Mcp.Tests.csproj --no-build `
  --filter "FullyQualifiedName~Placeable_foundation_resource_is_filtered_typed_and_plan_is_dry_run"
dotnet test tests/Forge.Studio.Core.Tests/Forge.Studio.Core.Tests.csproj --no-build `
  --filter "FullyQualifiedName~LegacyWindowMigrationTests"
& .\scripts\Open-Studio.ps1 -Page placement -Evidence ux1-r-trigger -Headless -Smoke -Wait
& .\scripts\Open-Studio.ps1 -Page placement -Evidence ux1-r-detection -Resolution 1920x1080 `
  -Capture artifacts/verification/studio-shell/ux1-r/detection-fhd.png -Wait
& .\scripts\Open-Studio.ps1 -Page placement -Evidence ux1-r-friendly -Resolution 1920x1080 `
  -Capture artifacts/verification/studio-shell/ux1-r/friendly-fhd.png -Wait
& .\scripts\Open-Studio.ps1 -Page placement -Evidence ux1-r-disarm -Resolution 2560x1440 `
  -Capture artifacts/verification/studio-shell/ux1-r/disarm-qhd.png -Wait
& .\scripts\Open-Studio.ps1 -Page placement -Evidence ux1-r-one-shot -Resolution 3840x2160 `
  -Capture artifacts/verification/studio-shell/ux1-r/one-shot-4k.png -Wait
& .\scripts\Update-StudioAuroraAccessibilityPlan.ps1 -Check
```

Avec UX0-A à UX1-R, 62 fonctions disposent d’une preuve Studio spécifique ; les 655 autres lignes
restent explicitement `planned`.
