# Acceptation Studio — UX1-T Ouverture et fermeture

État local au 16 août 2026 : **fonctionnel et qualifié** pour `ORF-PLC-016`. Ce lot rend les
événements d’ouverture et de fermeture d’un placeable directement configurables et
prévisualisables dans l’unique fenêtre `Forge.Studio`. Il ne qualifie ni les événements de
verrouillage `ORF-PLC-017`, ni une distribution publique.

## Preuves communes

- portail incrémental `scripts/Verify-StudioIncrement.ps1` réussi sur la page `placement` et la
  preuve `ux1-t-close` ; son résumé horodaté par l’exécution est conservé dans
  `artifacts/verification/studio-increment/ux1-t-close.summary.json` ;
- tests ciblés : adaptateur RPG Kit 7/7, validation Application 1/1, round-trip ModuleFormat 1/1 et
  ressource MCP 1/1, soit 10/10 ;
- portail intégré `scripts/Verify.ps1` requis par l’évolution du contrat persistant/runtime/MCP :
  `OPENRPG_FORGE_VERIFICATION=PASS`, 1 972 tests .NET, 39 tests Python, build sans avertissement et
  graphe Roslyn déterministe à 8 866 nœuds, 57 910 relations, 0 diagnostic ;
- smoke Godot 4.7.1 .NET du Studio :
  `OPENRPG_FORGE_STUDIO_UX1T_PASS bindings_api=true canonical_payload=true
  open_close_preview=true dry_run=true command_parity=true instance_selection=true
  preservation=true runtime_projection=true script_read=true optional_required=true
  undo_redo=true persistence=true invalid_blocked=true human_no_uuid=true ui=true
  ui_transaction=true project_restored=true windows=1` ;
- le même lancement garde UX0-A à UX1-S au vert et termine par
  `OPENRPG_FORGE_STUDIO_SHELL_PASS sections=8 pages=36 windows=1` ;
- le smoke contractuel emploie un workspace temporaire avec deux instances du blueprint ciblé, un
  placeable voisin et un piège voisin à préserver ;
- le smoke UI applique la fiche au vrai projet Crypte des Cendres uniquement en mémoire, l’annule et
  vérifie la restauration exacte du projet ; aucun événement de preuve n’est sauvegardé ;
- capture Forward+ inspectée dans `artifacts/verification/studio-shell/ux1-t/close-fhd.png`, avec son
  journal `close-fhd.png.log`.

La capture 1920×1080 utilise Godot 4.7.1 .NET, Vulkan Forward+, une Radeon RX 7800 XT, 278 assets,
zéro fallback et une seule fenêtre. Elle montre la version ForgeScript 1.1.0, les liaisons humaines
**CoffreOuvert** et **CoffreFerme**, les quatre champs de contexte obligatoires, la situation
**Fermeture réussie** et son résultat lisible, sans UUID, hash ni chemin technique. Les actions de
prévisualisation et d’application restent visibles et aucun contrôle central ne se chevauche.

La persistance canonique est qualifiée sur le workspace synthétique afin de ne pas modifier le
contenu authored. La capture réelle prouve l’accès humain et le rendu, pas la présence préalable de
liaisons sauvegardées dans Crypte des Cendres. UX1-T ne changeant ni layout, ni viewport, ni densité,
les preuves QHD et 4K restent réservées au prochain jalon multi-lots.

### ORF-PLC-016

La page **Objets & interactions** expose désormais **Ouverture / fermeture** après **Trésor**.
`Ctrl+K` fournit aussi **Configurer ouverture et fermeture**. Les deux entrées ouvrent la même fiche
sans identifiant technique dans la surface humaine.

L’auteur configure la version de l’API ForgeScript, la liaison publiée à l’ouverture et celle publiée
à la fermeture. Le contrat canonique exige acteur, résultat, état précédent et autorité ; désactiver
l’un de ces quatre champs ou vider une liaison bloque l’aperçu et l’application avec un diagnostic
localisé. L’état vide explique comment activer la capacité.

**Prévisualiser l’événement** simule au choix une ouverture ou une fermeture réussie. Il appelle
`PlaceableSecurityEventsRuntimeAdapter.ContainerStateChanged`, retourne la liaison réellement
choisie et le payload typé `ContainerOpened` ou `ContainerClosed`, avec acteur, résultat, ancien état
et autorité serveur. L’aperçu n’ouvre ni ne ferme aucun objet de scène et n’écrit aucun module.

**Prévisualiser** passe par `SetWorldFeatureCatalogHandler` avec `DryRun=true`. **Appliquer au
module** réutilise le handler à la révision courante et `ProjectCommandHistory` ; l’opération reste
atomique, annulable et rétablissable. La sauvegarde explicite puis la réouverture conservent les deux
liaisons, la version d’API, le payload et la politique runtime. L’identité du placeable, du profil de
sécurité, du piège voisin, des autres placeables et des blueprints demeure inchangée.

`ContainerEventsRequiredAtRuntime` est indépendant de `TrapRequiredAtRuntime` et
`LootRequiredAtRuntime`, et reste faux par défaut pour les anciens modules. Le validateur produit
`world.placeable.container_events_required_missing` si des événements exigés manquent.
`ResolveContainerEvents` et `BuildContainerEvents` projettent le sous-profil sans exiger piège,
trésor, verrouillage ou événement de contenu ; `ContainerStateChanged` utilise cette projection
indépendante.

`WorldFeatureScriptService`, donc la ressource MCP filtrée, expose les deux bindings, la version de
l’API, le contrat de payload et `containerEventsRequiredAtRuntime`. La mutation MCP demeure une
commande gouvernée disponible en dry-run, sans voie d’écriture directe.

## Limites honnêtes

- ce lot qualifie seulement `ORF-PLC-016` ; les événements OnLock/OnUnlock et les autres routes
  placeable conservent leurs lots dédiés ;
- l’aperçu construit et inspecte le dispatch typé mais n’exécute pas un handler ForgeScript ni une
  mutation de scène ; ces effets relèvent du runtime autoritaire ;
- Crypte des Cendres fournit le catalogue et la scène réels, pas des liaisons de preuve nouvellement
  sauvegardées ; cette absence initiale est présentée explicitement dans l’interface ;
- l’application demeure en mémoire jusqu’à l’enregistrement explicite ;
- le portail complet est une qualification locale, pas une release signée ou publiée ;
- la migration des autres routes, l’export autonome Windows/Linux et le retrait du plugin historique
  restent hors de ce lot.

## Reproduction

```powershell
$projects = @(
  'tests/Forge.RpgKit.Domain.Tests/Forge.RpgKit.Domain.Tests.csproj'
  'tests/Forge.Application.Tests/Forge.Application.Tests.csproj'
  'tests/Forge.ModuleFormat.Tests/Forge.ModuleFormat.Tests.csproj'
  'tests/Forge.Mcp.Tests/Forge.Mcp.Tests.csproj'
)
$filter = 'FullyQualifiedName~PlaceableSecurityEventsRuntimeAdapterTests|' +
  'FullyQualifiedName~R094_PlaceableSecurityEventsValidation|' +
  'FullyQualifiedName~Aggregate_and_workspace_round_trips_preserve_all_world_feature_families|' +
  'FullyQualifiedName~Placeable_foundation_resource_is_filtered_typed_and_plan_is_dry_run'
& .\scripts\Verify-StudioIncrement.ps1 -Page placement -Evidence ux1-t-close `
  -TestProject $projects -TestFilter $filter `
  -Capture artifacts/verification/studio-shell/ux1-t/close-fhd.png -CheckGraph
& .\scripts\Verify.ps1
& .\scripts\Update-StudioAuroraAccessibilityPlan.ps1 -Check
```

Avec UX0-A à UX1-T, 64 fonctions disposent d’une preuve Studio spécifique ; les 653 autres lignes
restent explicitement `planned`. Aucun lot suivant n’est entamé.
