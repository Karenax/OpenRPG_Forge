# Acceptation Studio — UX1-AH Automatisation du trigger

État local au 17 août 2026 : **fonctionnel et qualifié** pour `ORF-TRG-009`, `ORF-TRG-010` et
`ORF-TRG-018`. La fiche **Heartbeat, événement et contrôle MJ** configure une politique
d'automatisation bornée sans ouvrir de session Live Director.

## Preuves communes

- portail incrémental réussi sur `placement` / `ux1-ah-trigger-automation-director` en 69,34 s,
  résumé dans
  `artifacts/verification/studio-increment/ux1-ah-trigger-automation-director.summary.json` ;
- tests ciblés : Domain 1/1, Application 3/3, ModuleFormat 1/1 et MCP 1/1, soit 6/6 ;
- smoke réussi : sélection explicite, labels homonymes désambiguïsés, Foundation exigée, scheduler,
  événement personnalisé, contrôle MJ, politique runtime sans dispatch live, dry-run, bornes et
  namespace invalides bloqués, doubles gardes de révision, commande, undo/redo, round-trip,
  préservation complète des voisins et preuve isolée nettoyée ;
- capture FHD dans
  `artifacts/verification/studio-increment/ux1-ah-trigger-automation-director-forward-plus-fhd.png`,
  puis QHD/4K dans `artifacts/verification/studio-shell/ux1-milestone/ux1-ah-*.png` : les trois onglets
  et l'avertissement Live Director restent lisibles sans débordement ;
- portail transversal `scripts/Verify.ps1` réussi au jalon UX1-AG/AH/AI/AJ : 1 988 tests .NET,
  39 tests Python, builds sans avertissement, graphe Roslyn déterministe à 9 132 nœuds et
  61 393 relations sans diagnostic, puis smokes Studio, Toolset, Runtime, Forward+, MCP et réseau.

### ORF-TRG-009

Le Heartbeat expose activation, cadence, budget par minute, exigence d'occupation et regroupement
des occupants. L'aperçu vérifie absence d'occupant, plafond de budget et pulsation permise, mais ne
démarre aucun timer.

### ORF-TRG-010

L'événement personnalisé reçoit un espace de noms stable en minuscules séparées par des points. Les
espaces invalides sont refusés et aucun script n'est exécuté pendant l'aperçu.

### ORF-TRG-018

Le contrôle MJ borne entrée, sortie et déclenchement manuel, avec cible explicite et audit serveur
obligatoires. Il décrit ce que le serveur pourra autoriser ; aucune commande Live Director n'est
envoyée.

## Limites honnêtes

- une Foundation de trigger existante est obligatoire et n'est jamais créée par cette fiche ;
- secret, clic, accès, Portal, Validation, Advanced et politiques UX2 restent préservés mais non
  édités ;
- les valeurs canoniques hors bornes représentables sont refusées avant chargement, sans clamp ni
  réécriture silencieuse ;
- la preuve utilise une session temporaire isolée et libérée à la fermeture ; projet, approbation,
  historique et sélection de la session source restent inchangés ;
- le resolver pur qualifie la politique, sans timer, cible réelle, session MJ ou dispatch live.

## Reproduction

```powershell
$projects = @(
  'tests/Forge.Domain.Tests/Forge.Domain.Tests.csproj'
  'tests/Forge.Application.Tests/Forge.Application.Tests.csproj'
  'tests/Forge.ModuleFormat.Tests/Forge.ModuleFormat.Tests.csproj'
  'tests/Forge.Mcp.Tests/Forge.Mcp.Tests.csproj'
)
$filter = 'FullyQualifiedName~G13S_TriggerAutomationNarrativeDirector_ProducesAuditedEffects|' +
  'FullyQualifiedName~Command_supports_preview_apply_history_and_typed_script_query|' +
  'FullyQualifiedName~R048_TriggerValidationCoversIdentityGeometryPolicyActionsPortalTransformEventsAndRequirement|' +
  'FullyQualifiedName~R049_TriggerValidationRejectsSelfIntersectionContradictoryFiltersTargetsPortalScriptsAndAccessibility|' +
  'FullyQualifiedName~Aggregate_and_workspace_round_trips_preserve_all_world_feature_families|' +
  'FullyQualifiedName~Trigger_foundation_resource_is_filtered_typed_and_plan_is_dry_run'
& .\scripts\Verify-StudioIncrement.ps1 -Page placement `
  -Evidence ux1-ah-trigger-automation-director `
  -TestProject $projects -TestFilter $filter `
  -Capture artifacts/verification/studio-increment/ux1-ah-trigger-automation-director-forward-plus-fhd.png
```
