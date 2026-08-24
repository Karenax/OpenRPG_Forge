# Acceptation Studio — UX2-A Politiques de zone vivante

État local au 17 août 2026 : **fonctionnel et qualifié** pour `ORF-AREA-023`,
`ORF-AREA-025` et `ORF-AREA-026`. La fiche **Rythmes, perception et repos**, accessible depuis la
page **Monde vivant** (`living-world`), édite les collections complètes de la zone explicitement
sélectionnée puis les applique dans une seule transaction area-only canonique.

## Preuves communes

- portail incrémental `scripts/Verify-StudioIncrement.ps1` réussi en **75,69 s** sur la page
  `living-world` et la preuve `ux2-a-area-living-world`, avec résumé dans
  `artifacts/verification/studio-increment/ux2-a-area-living-world.summary.json` ;
- tests du portail : Domain 3/3, Application 5/5 et ModuleFormat 3/3, soit **11/11** ; le test
  RpgKit.Domain `Area_modifiers_change_creature_senses_by_kind_and_condition` passe séparément
  **1/1** ;
- détail chronométré du résumé : build Studio Debug 7,61 s, Domain 3,60 s, Application 4,48 s,
  ModuleFormat 4,93 s, smoke Studio headless 49,39 s, capture Forward+ FHD 4,86 s et contrôle de
  matrice 0,77 s ;
- smoke Studio headless réussi : sélection explicite, scripts réels, collections complètes,
  valeurs numériques hors grille préservées, invalidités heartbeat/perception/repos bloquées,
  dry-run pur, diff de perception humain, repos dormant honnête, révisions, empreintes et
  approbations périmées refusées, commande canonique, undo/redo, sauvegarde/réouverture,
  préservation de la zone cible et de ses voisines, preuve isolée nettoyée, session source et
  surface d'origine restaurées ;
- capture Forward+ Full HD inspectée dans
  `artifacts/verification/studio-increment/ux2-a-area-living-world-forward-plus-fhd.png`
  (1920×1080), puis QHD et 4K dans
  `artifacts/verification/studio-shell/ux2-5-milestone/ux2-a-qhd.png` (2560×1440) et
  `artifacts/verification/studio-shell/ux2-5-milestone/ux2-a-4k.png` (3840×2160). Le contenu reste
  scrollable, les libellés et valeurs sont lisibles sans clipping, et le bandeau fixe conserve
  l'état de la preuve ainsi que les actions principales visibles ;
- les trois journaux `.png.log` confirment Forward+, la taille exacte demandée, l'ouverture de
  Forgotten Forest, la préparation de la preuve et `OPENRPG_FORGE_STUDIO_SHELL_PASS` avec une
  seule fenêtre.

### ORF-AREA-023

**Événement heartbeat de zone** expose chaque rythme local par nom humain, cadence, budget,
activation, suspension hors observation, exigence runtime, version d'API, point d'entrée et script
réel du projet. L'aperçu calcule la politique du scheduler sans exécuter de handler ni inventer un
script. L'application remplace uniquement la politique de la zone par
`SetAreaRuntimePoliciesHandler`.

### ORF-AREA-025

**Modificateurs de perception** expose le profil de règles et la collection stable des
modificateurs par sens, multiplicateur de portée, variation de confiance et condition. L'aperçu
présente un diff humain explicite en plus du patch canonique. Le test RpgKit.Domain séparé prouve
que le service de perception applique effectivement le multiplicateur et la condition au calcul de
détection ; le Studio ne simule aucun effet visuel.

### ORF-AREA-026

**Interdiction de repos** expose la permission autorisée, interdite ou conditionnelle, la raison,
la condition, le cooldown, le rayon sécurisé et la collection complète des volumes prioritaires.
Lorsque le cycle de repos du projet est désactivé, la règle locale reste fidèlement authorée mais
dormante : l'édition ne l'active pas implicitement.

## Limites honnêtes

- aucune zone n'est choisie silencieusement et le parcours produit ne synthétise ni politique ni
  script manquant ; les scripts proposés proviennent du projet ouvert ;
- il s'agit d'authoring et d'évaluation de politiques : aucun script n'est exécuté, aucun gizmo,
  effet visuel, moteur, navigation, entrée viewport, dispatch réseau ou état réseau n'est simulé ;
- la commande est strictement area-only : le module, les autres zones et les autres racines ne
  sont jamais modifiés ; Cinematics, Rules — dont Combat — et Experience de la zone cible sont
  conservés exactement ;
- la désactivation du repos au niveau projet reste intacte et une politique locale dormante ne
  constitue pas une exécution runtime ;
- le dry-run et les captures utilisent une session temporaire isolée, nettoyée à la fermeture ;
  projet, historique, sélection, approbation et état sale de la session source restent inchangés ;
- `ORF-AREA-024`, `ORF-AREA-027`, la navigation UX2 et toute manipulation directe du viewport ne
  sont pas promus par ce lot ;
- le résumé est volontairement incrémental : `matrix_check=true`, `graph_check=false` et
  `full_gate=false`. Il ne constitue pas à lui seul le compte rendu du portail transversal du
  jalon.

## Reproduction

Depuis la racine du dépôt, avec les dépendances déjà restaurées :

```powershell
$workspace = 'apps/Forge.Runtime/content/forgotten-forest'
$projects = @(
  'tests/Forge.Domain.Tests/Forge.Domain.Tests.csproj'
  'tests/Forge.Application.Tests/Forge.Application.Tests.csproj'
  'tests/Forge.ModuleFormat.Tests/Forge.ModuleFormat.Tests.csproj'
)
$filter = 'FullyQualifiedName~Validator_reports_duplicate_names_and_out_of_bounds_rest_volumes|' +
  'FullyQualifiedName~Ux0GHeartbeatAndRestBindingsRequireSafeRelativeScriptsWhenMarkedRequired|' +
  'FullyQualifiedName~Progression_curve_and_perception_keys_are_validated|' +
  'FullyQualifiedName~Module_and_area_policies_support_dry_run_apply_and_stable_patches|' +
  'FullyQualifiedName~Area_policy_rejects_an_owner_identity_mismatch|' +
  'FullyQualifiedName~Heartbeats_are_budgeted_suspended_and_backlog_bounded|' +
  'FullyQualifiedName~Rest_lifecycle_applies_volume_security_completion_and_cooldown|' +
  'FullyQualifiedName~Difficulty_mutators_compose_and_perception_is_sense_condition_driven|' +
  'FullyQualifiedName~RoundTrip_PreservesDimensionsKindEntitiesAndPortalLinks|' +
  'FullyQualifiedName~Deserialize_MigratesPreRuntimePolicyDocumentWithStableSafeDefaults|' +
  'FullyQualifiedName~Open_MigratesPreRuntimePolicyWorkspaceAndAreaDocumentsDeterministically'

& .\scripts\Verify-StudioIncrement.ps1 `
  -Page living-world `
  -Evidence ux2-a-area-living-world `
  -Workspace $workspace `
  -TestProject $projects `
  -TestFilter $filter `
  -Capture artifacts/verification/studio-increment/ux2-a-area-living-world-forward-plus-fhd.png

dotnet test tests/Forge.RpgKit.Domain.Tests/Forge.RpgKit.Domain.Tests.csproj `
  --configuration Debug --no-restore --logger 'console;verbosity=minimal' `
  --filter 'FullyQualifiedName~Area_modifiers_change_creature_senses_by_kind_and_condition'

& .\scripts\Open-Studio.ps1 -Page living-world -Evidence ux2-a-area-living-world `
  -Workspace $workspace -Resolution 2560x1440 `
  -Capture artifacts/verification/studio-shell/ux2-5-milestone/ux2-a-qhd.png -Wait -SkipBuild
& .\scripts\Open-Studio.ps1 -Page living-world -Evidence ux2-a-area-living-world `
  -Workspace $workspace -Resolution 3840x2160 `
  -Capture artifacts/verification/studio-shell/ux2-5-milestone/ux2-a-4k.png -Wait -SkipBuild
```
