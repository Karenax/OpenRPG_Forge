# Acceptation Studio — UX1-AE Découverte et accès du trigger

État local au 17 août 2026 : **fonctionnel et qualifié** pour `ORF-TRG-005`,
`ORF-TRG-008`, `ORF-TRG-012` et `ORF-TRG-013`. Le lot réunit secret/révélation, clic/présentation
et justificatif dans une surface humaine depuis **Objets & interactions**.

## Preuves communes

- portail incrémental `scripts/Verify-StudioIncrement.ps1` réussi sur la page `placement` et la
  preuve `ux1-ae-discovery-access` en 68,12 s, avec résumé dans
  `artifacts/verification/studio-increment/ux1-ae-discovery-access.summary.json` ;
- tests ciblés : Domain 1/1, Application 2/2, ModuleFormat 1/1 et MCP 1/1, soit 5/5 ;
- smoke Studio headless réussi : sélection explicite, Foundation exigée, quatre résultats runtime,
  invalidités nommées, script borné par Validation, doubles gardes de révision, commande,
  préservation, undo/redo, round-trip structurel et preuve isolée nettoyée ;
- captures Forward+ Full HD, QHD et 4K inspectées dans
  `artifacts/verification/studio-shell/ux1-ae/` : les trois onglets, les messages d'aperçu et toutes
  les actions restent lisibles sans débordement ;
- portail transversal `scripts/Verify.ps1` réussi au jalon UX1-AD/AE/AF : 1 980 tests .NET,
  39 tests Python, builds sans avertissement, graphe Roslyn déterministe à 9 059 nœuds et
  60 404 relations sans diagnostic, puis smokes Studio, Runtime, Forward+, MCP et réseau.

### ORF-TRG-005

Sens de détection, difficulté, cible de révélation, permanence, portée et feedback composent le
trigger secret. L'aperçu distingue secret caché et secret révélé sans modifier le catalogue.

### ORF-TRG-008

Le clic expose un verbe et un script ForgeScript existant. Le script doit appartenir à
`Foundation.Validation.KnownScriptIds` ; une valeur hors allowlist est refusée au lieu d'altérer la
politique de validation cachée.

### ORF-TRG-012

L'accès accepte un objet-clé du projet ou un justificatif métier nommé, avec consommation
explicitement activée et feedback de refus. L'aperçu distingue justificatif manquant, permission
manquante et accès autorisé.

### ORF-TRG-013

Icône de curseur, surbrillance, texte d'invite, visibilité et permission sont édités avec le clic.
La présentation reste liée à la même InteractionPolicy afin d'éviter deux sources concurrentes.

## Limites honnêtes

- un trigger sans Foundation reste bloqué et renvoie vers UX1-Y ; aucune Foundation n'est créée par
  le parcours produit ;
- Portal, Validation, Advanced, automatisation, narration et autres triggers sont préservés mais non
  édités par ce lot ;
- Crypte des Cendres n'ayant pas de profil Trigger qualifiable, la capture utilise une session
  temporaire isolée qui reste vivante jusqu'à la preuve puis est supprimée ;
- la simulation locale exerce les décisions canoniques, sans scène 3D ni transport réseau réel.

## Reproduction

```powershell
$projects = @(
  'tests/Forge.Domain.Tests/Forge.Domain.Tests.csproj'
  'tests/Forge.Application.Tests/Forge.Application.Tests.csproj'
  'tests/Forge.ModuleFormat.Tests/Forge.ModuleFormat.Tests.csproj'
  'tests/Forge.Mcp.Tests/Forge.Mcp.Tests.csproj'
)
$filter = 'FullyQualifiedName~G13R_TriggerDiscoveryAccess_RequiresDiscoveryCredentialAndPermission|' +
  'FullyQualifiedName~R048_TriggerValidationCoversIdentityGeometryPolicyActionsPortalTransformEventsAndRequirement|' +
  'FullyQualifiedName~R049_TriggerValidationRejectsSelfIntersectionContradictoryFiltersTargetsPortalScriptsAndAccessibility|' +
  'FullyQualifiedName~Aggregate_and_workspace_round_trips_preserve_all_world_feature_families|' +
  'FullyQualifiedName~Trigger_foundation_resource_is_filtered_typed_and_plan_is_dry_run'
& .\scripts\Verify-StudioIncrement.ps1 -Page placement -Evidence ux1-ae-discovery-access `
  -TestProject $projects -TestFilter $filter `
  -Capture artifacts/verification/studio-shell/ux1-ae/ux1-ae-discovery-access-fhd.png
```
