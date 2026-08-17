# Acceptation Studio — UX1-AI Validation canonique du trigger

État local au 17 août 2026 : **fonctionnel et qualifié** pour `ORF-TRG-020`. La fiche
**Contrôler un déclencheur avant publication** édite le contexte utilisé par le même validateur
canonique que le catalogue et la commande projet.

## Preuves communes

- portail incrémental réussi sur `placement` / `ux1-ai-trigger-validation` en 64,99 s, résumé dans
  `artifacts/verification/studio-increment/ux1-ai-trigger-validation.summary.json` ;
- tests ciblés : Domain 1/1, Application 3/3, ModuleFormat 1/1 et MCP 1/1, soit 6/6 ;
- smoke réussi : sélection explicite, Foundation exigée, création et suppression optionnelle du
  contexte, caractère requis, résolution runtime, dry-run, cible/script/portail/couche invalides
  bloqués, fallback optionnel, diagnostics groupés, garde révision plus fingerprint, commande,
  undo/redo, round-trip et préservation des voisins ;
- capture FHD dans
  `artifacts/verification/studio-increment/ux1-ai-trigger-validation-forward-plus-fhd.png`, puis
  QHD/4K dans `artifacts/verification/studio-shell/ux1-milestone/ux1-ai-*.png` : les quatre listes,
  sept groupes de verdict et actions restent lisibles ; les destinations héritées inconnues sont
  masquées et protégées, sans GUID brut ;
- portail transversal `scripts/Verify.ps1` réussi au jalon UX1-AG/AH/AI/AJ : 1 988 tests .NET,
  39 tests Python, builds sans avertissement, graphe Roslyn déterministe à 9 132 nœuds et
  61 393 relations sans diagnostic, puis smokes Studio, Toolset, Runtime, Forward+, MCP et réseau.

### ORF-TRG-020

Le contexte déclare cibles, scripts, destinations de portail et couches accessibles connus. Les
destinations résolues sont choisies par un libellé humain ; une référence héritée inconnue reste
masquée, non supprimable et strictement préservée. Le verdict est groupé en Fondation & politique,
Géométrie, Filtres & accessibilité, Actions & cibles, Portail & destination, Scripts & événements et
Contexte runtime.

## Limites honnêtes

- une Foundation existante est requise et n'est pas créée par ce lot ;
- contexte désactivé et optionnel signifie fallback explicite avec catégories dépendantes
  « Non évalué » ; contexte absent mais requis devient un blocage, jamais un faux verdict conforme ;
- seuls le contexte de validation et son caractère requis sont modifiés ; géométrie, filtres,
  actions, Portal, automatisation et narration restent préservés ;
- les identifiants métier de cible, script et couche restent visibles ; seuls les UUID de
  destinations sont remplacés par des libellés humains ou masqués lorsqu'ils sont inconnus ;
- la preuve est isolée et nettoyée ; elle ne déclenche aucun script, portail ni runtime réel.

## Reproduction

```powershell
$projects = @(
  'tests/Forge.Domain.Tests/Forge.Domain.Tests.csproj'
  'tests/Forge.Application.Tests/Forge.Application.Tests.csproj'
  'tests/Forge.ModuleFormat.Tests/Forge.ModuleFormat.Tests.csproj'
  'tests/Forge.Mcp.Tests/Forge.Mcp.Tests.csproj'
)
$filter = 'FullyQualifiedName~R049_TriggerValidation|' +
  'FullyQualifiedName~Command_supports_preview_apply_history_and_typed_script_query|' +
  'FullyQualifiedName~Command_rejects_revision_identity_and_invalid_script_contracts|' +
  'FullyQualifiedName~Aggregate_and_workspace_round_trips_preserve_all_world_feature_families|' +
  'FullyQualifiedName~Trigger_foundation_resource_is_filtered_typed_and_plan_is_dry_run'
& .\scripts\Verify-StudioIncrement.ps1 -Page placement -Evidence ux1-ai-trigger-validation `
  -TestProject $projects -TestFilter $filter `
  -Capture artifacts/verification/studio-increment/ux1-ai-trigger-validation-forward-plus-fhd.png
```
