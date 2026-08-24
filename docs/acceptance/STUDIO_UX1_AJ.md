# Acceptation Studio — UX1-AJ Validation canonique de porte

État local au 17 août 2026 : **fonctionnel et qualifié** pour `ORF-DOOR-021`. La fiche
**Qualifier le contexte runtime d'une porte** projette les références réellement authorées et
applique le même verdict canonique dans le Studio, la commande projet et le prébuild.

## Preuves communes

- portail incrémental réussi sur `placement` / `ux1-aj-door-validation` en 82,17 s, résumé dans
  `artifacts/verification/studio-increment/ux1-aj-door-validation.summary.json` ;
- tests ciblés : RpgKit.Domain 10/10, Application 3/3, ModuleFormat 4/4 et MCP 1/1, soit 18/18 ;
- smoke réussi : sélection explicite, Foundation et Navigation exigées, suggestions humaines,
  destinations héritées protégées, contexte optionnel non évalué, contexte requis absent bloqué,
  références destination/animation/rejet/collision/credential/script invalides, liens orphelins ou
  inverses bloqués, dry-run, doubles gardes, commande, undo/redo, suppression optionnelle,
  round-trip, préservation des catalogues voisins et diagnostics structurés ;
- capture FHD dans
  `artifacts/verification/studio-increment/ux1-aj-door-validation-forward-plus-fhd.png`, puis QHD/4K
  dans `artifacts/verification/studio-shell/ux1-milestone/ux1-aj-*.png` : suggestions, provenance,
  collision authorée distincte, groupes de verdict et actions restent lisibles sans clipping ;
- portail transversal `scripts/Verify.ps1` réussi au jalon UX1-AG/AH/AI/AJ : 1 988 tests .NET,
  39 tests Python, builds sans avertissement, graphe Roslyn déterministe à 9 132 nœuds et
  61 393 relations sans diagnostic, puis smokes Studio, Toolset, Runtime, Forward+, MCP et réseau.

### ORF-DOOR-021

Le contexte qualifie destinations, animations — y compris l'animation de refus — profils de
collision, identifiants d'accès et scripts. Les suggestions proviennent des composants et manifests
réellement authorés, avec libellés humains et slugs pour désambiguïser les homonymes. La collision
authorée par la Foundation reste distincte du catalogue des collisions connues. Le validateur
signale aussi liens orphelins et liens retour incohérents, puis groupe le verdict en Fondation &
navigation, Destinations, Animations, Collisions, Identifiants d'accès, Scripts et Contexte runtime.

## Limites honnêtes

- Foundation et Navigation existantes sont des prérequis ; la fiche ne les crée pas, ne les édite
  pas et ne qualifie pas `ORF-DOOR-019` ;
- absent et optionnel signifie « Non évalué » ; absent et requis bloque la commande et le prébuild ;
- seuls la politique de validation et son caractère requis sont modifiés ; accès, événements,
  Portal, liens, Foundation, Navigation et autres profils restent préservés ;
- les suggestions reflètent les sources authorées disponibles et leur provenance ; elles ne
  constituent ni téléchargement d'asset ni validation d'un moteur externe ;
- le verdict est canonique mais ne simule ni moteur, ni navigation vivante, ni réseau ;
- la capture utilise une session temporaire isolée, supprimée à la fermeture, sans modifier le
  projet source ni son historique.

## Reproduction

```powershell
$projects = @(
  'tests/Forge.RpgKit.Domain.Tests/Forge.RpgKit.Domain.Tests.csproj'
  'tests/Forge.Application.Tests/Forge.Application.Tests.csproj'
  'tests/Forge.ModuleFormat.Tests/Forge.ModuleFormat.Tests.csproj'
  'tests/Forge.Mcp.Tests/Forge.Mcp.Tests.csproj'
)
$filter = 'FullyQualifiedName~DoorWorldValidationServiceTests|' +
  'FullyQualifiedName~DoorRuntimeServiceTests|' +
  'FullyQualifiedName~DoorFoundationTests|' +
  'FullyQualifiedName~Ux1Aj_DoorRuntimeValidationPolicyIsTransactionalProjectedAndStructurallyGuarded|' +
  'FullyQualifiedName~Command_supports_preview_apply_history_and_typed_script_query|' +
  'FullyQualifiedName~Command_rejects_revision_identity_and_invalid_script_contracts|' +
  'FullyQualifiedName~Authored_door_runtime_semantics_are_blocked_by_the_canonical_prebuild_gate|' +
  'FullyQualifiedName~Ux1AjExportRejectsRequiredDoorSemanticsWhenReferencesAreOptional|' +
  'FullyQualifiedName~Previous_door_validation_formats_default_to_optional_unqualified_context|' +
  'FullyQualifiedName~Aggregate_and_workspace_round_trips_preserve_all_world_feature_families|' +
  'FullyQualifiedName~Ux1Aj_DoorValidationContextIsProjectedAndMcpPreviewCannotBypassCanonicalValidation'
& .\scripts\Verify-StudioIncrement.ps1 -Page placement -Evidence ux1-aj-door-validation `
  -TestProject $projects -TestFilter $filter `
  -Capture artifacts/verification/studio-increment/ux1-aj-door-validation-forward-plus-fhd.png
```
