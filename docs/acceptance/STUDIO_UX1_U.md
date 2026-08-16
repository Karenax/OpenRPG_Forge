# Acceptation Studio — UX1-U Verrouillage et déverrouillage

État local au 16 août 2026 : **fonctionnel et qualifié** pour `ORF-PLC-017`. Ce lot rend les
événements publiés avant et après un changement de verrou directement configurables et
prévisualisables dans l’unique fenêtre `Forge.Studio`. Il ne qualifie ni `OnUsed` `ORF-PLC-018`, ni
les événements de contenu suivants, ni une distribution publique.

## Preuves communes

- portail incrémental `scripts/Verify-StudioIncrement.ps1` réussi sur la page `placement` et la
  preuve `ux1-u-veto` ; son résumé horodaté par l’exécution est conservé dans
  `artifacts/verification/studio-increment/ux1-u-veto.summary.json` ;
- tests ciblés : adaptateur RPG Kit 7/7, validation Application 1/1, round-trip ModuleFormat 1/1 et
  ressource MCP 1/1, soit 10/10 ;
- portail intégré `scripts/Verify.ps1` requis par l’évolution du contrat persistant/runtime/MCP :
  `OPENRPG_FORGE_VERIFICATION=PASS`, 1 972 tests .NET, 39 tests Python, build sans avertissement et
  graphe Roslyn final déterministe à 8 877 nœuds, 58 036 relations, 0 diagnostic ;
- smoke Godot 4.7.1 .NET du Studio :
  `OPENRPG_FORGE_STUDIO_UX1U_PASS bindings_api=true canonical_payload=true
  before_after_preview=true controlled_veto=true dry_run=true command_parity=true
  instance_selection=true preservation=true runtime_projection=true script_read=true
  optional_required=true undo_redo=true persistence=true invalid_blocked=true human_no_uuid=true
  ui=true ui_transaction=true project_restored=true windows=1` ;
- le même lancement garde UX0-A à UX1-T au vert et termine par
  `OPENRPG_FORGE_STUDIO_SHELL_PASS sections=8 pages=36 windows=1` ;
- le smoke contractuel emploie un workspace temporaire avec deux instances du blueprint ciblé, un
  placeable voisin, un verrou canonique et des événements de conteneur voisins à préserver ;
- le smoke UI applique la fiche au vrai projet Forgotten Forest uniquement en mémoire, l’annule et
  vérifie la restauration exacte du projet ; aucun événement de preuve n’est sauvegardé ;
- capture Forward+ inspectée dans `artifacts/verification/studio-shell/ux1-u/veto-fhd.png`, avec son
  journal `veto-fhd.png.log`.

La capture 1920×1080 utilise Godot 4.7.1 .NET, Vulkan Forward+, une Radeon RX 7800 XT, 278 assets,
zéro fallback et une seule fenêtre. Elle montre la version ForgeScript 1.1.0, les liaisons humaines
**VerrouAvant** et **VerrouApres**, méthode, justificatif, acteur et veto contrôlé, la situation
**Veto contrôlé avant changement** et son résultat lisible. Aucun UUID, empreinte ou chemin
technique n’apparaît dans la surface humaine et l’intitulé de l’aperçu reste entièrement visible.

La persistance canonique est qualifiée sur le workspace synthétique afin de ne pas modifier le
contenu authored. La capture réelle prouve l’accès humain et le rendu, pas la présence préalable de
liaisons sauvegardées dans Forgotten Forest. UX1-U ne changeant ni layout, ni viewport, ni densité,
les preuves QHD et 4K restent réservées au prochain jalon multi-lots.

### ORF-PLC-017

La page **Objets & interactions** expose désormais **Verrouiller / déverrouiller** à côté de la
configuration de verrou UX1-P. `Ctrl+K` fournit aussi **Configurer verrouillage et
déverrouillage**. Les deux entrées ouvrent la même fiche et activent explicitement le composant
**Verrou** nécessaire, sans confondre son mécanisme avec ses événements de cycle de vie.

L’auteur configure la version de l’API ForgeScript, la liaison publiée avant le changement et celle
publiée après. Le contrat canonique exige méthode, justificatif, acteur et veto contrôlé ; désactiver
l’une de ces quatre garanties, vider une liaison ou exiger des événements désactivés bloque l’aperçu
et l’application avec un diagnostic localisé. L’état vide explique comment activer la capacité.

**Prévisualiser le changement** simule au choix une transition acceptée ou un veto narratif avant
mutation. Il appelle `PlaceableSecurityEventsRuntimeAdapter.ChangeLockState`, publie
`LockStateChanging`, puis `LockStateChanged` seulement si la transition est acceptée. Le veto expose
sa raison, ne publie aucun événement après et conserve l’état verrouillé. L’aperçu ne change aucun
objet de scène et n’écrit aucun module.

**Prévisualiser** passe par `SetWorldFeatureCatalogHandler` avec `DryRun=true`. **Appliquer au
module** réutilise le handler à la révision courante et `ProjectCommandHistory` ; l’opération reste
atomique, annulable et rétablissable. La sauvegarde explicite puis la réouverture conservent les deux
liaisons, la version d’API, le payload, le veto et la politique runtime. L’identité du placeable, des
profils opérations/sécurité, du verrou, des événements de conteneur voisins, des autres placeables et
des blueprints demeure inchangée.

`LockEventsRequiredAtRuntime` est indépendant des exigences du piège, du trésor et des événements de
conteneur, et reste faux par défaut pour les anciens modules. Le validateur produit
`world.placeable.lock_events_required_missing` si ces événements sont exigés mais absents.
`ResolveLockEvents` et `BuildLockEvents` projettent le sous-profil sans exiger piège, trésor,
événements de conteneur ou changement de contenu ; `ChangeLockState` utilise cette projection
indépendante tout en exigeant le verrou opérationnel réellement concerné.

`WorldFeatureScriptService`, donc la ressource MCP filtrée, expose les deux bindings, la version de
l’API, le contrat `method-credential-actor-controlled-veto` et
`lockEventsRequiredAtRuntime`. La mutation MCP demeure une commande gouvernée disponible en dry-run,
sans voie d’écriture directe.

## Limites honnêtes

- ce lot qualifie seulement `ORF-PLC-017` ; `OnUsed`, `OnDisturbed` et les autres routes placeable
  conservent leurs lots dédiés ;
- l’aperçu construit et inspecte le dispatch typé mais n’exécute pas un handler ForgeScript ni une
  mutation de scène ; ces effets relèvent du runtime autoritaire ;
- Forgotten Forest fournit le catalogue et la scène réels, pas des liaisons de preuve nouvellement
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
& .\scripts\Verify-StudioIncrement.ps1 -Page placement -Evidence ux1-u-veto `
  -TestProject $projects -TestFilter $filter `
  -Capture artifacts/verification/studio-shell/ux1-u/veto-fhd.png -CheckGraph
& .\scripts\Verify.ps1
& .\scripts\Update-StudioAuroraAccessibilityPlan.ps1 -Check
```

Avec UX0-A à UX1-U, 65 fonctions disposent d’une preuve Studio spécifique ; les 652 autres lignes
restent explicitement `planned`. Aucun lot suivant n’est entamé.
