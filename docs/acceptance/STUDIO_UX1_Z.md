# Acceptation Studio — UX1-Z Réactions et physique du placeable

État local au 16 août 2026 : **fonctionnel et qualifié** pour `ORF-PLC-021`, `ORF-PLC-022`,
`ORF-PLC-023` et `ORF-PLC-024`. Le lot expose les réactions d'attaque/capacité, le rythme borné,
l'événement personnalisé et la politique physique depuis **Objets & interactions**. Il ne prétend
pas exécuter un moteur physique ou une réplication réseau dans le Studio.

## Preuves communes

- portail incrémental `scripts/Verify-StudioIncrement.ps1` réussi sur la page `placement` et la
  preuve `ux1-z-physics`, avec résumé dans
  `artifacts/verification/studio-increment/ux1-z-physics.summary.json` ;
- tests ciblés : Domain 1/1, Application 3/3 et ModuleFormat 1/1, soit 5/5 ;
- smoke Studio headless réussi : quatre aperçus séparés, invalidités cadence/budget/namespace/masse,
  refus de révision périmée, création sûre d'un profil absent, préservation, undo/redo et
  sauvegarde/réouverture ;
- capture Forward+ Full HD inspectée dans
  `artifacts/verification/studio-shell/ux1-z/physics-fhd.png` : le formulaire scrollable et le
  panneau fixe **Contrats avant application** gardent l'aperçu et les actions visibles ;
- portail transversal `scripts/Verify.ps1` réussi au jalon intégré UX1-X/Y/Z : 1 980 tests .NET,
  39 tests Python, builds sans avertissement, graphe Roslyn déterministe à 8 959 nœuds,
  59 067 relations et 0 diagnostic, puis smokes Studio, Runtime, Forward+, MCP et réseau réussis.

### ORF-PLC-021

`OnPhysicalAttacked` expose séparément ciblage et résolution, script, ordre avant/après et autorité.
L'aperçu de section passe par le contrat canonique sans modifier le module.

### ORF-PLC-022

`OnSpellCastAt` est présenté comme une capacité générique ciblée puis impactée, sans imposer un
système de sorts particulier. Les deux liaisons restent indépendantes et validées ensemble.

### ORF-PLC-023

Heartbeat et événement `custom:domaine:nom` utilisent cadence, budget, suspension hors observation
et namespace bornés. Désactiver le scheduler conserve exactement ses valeurs dormantes.

### ORF-PLC-024

La politique physique expose corps statique/rigide/cinématique, masse, contraintes, réplication,
sommeil, conversion statique et autorité. Le contrôle de masse représente exactement les centièmes
valides ; l'aperçu annonce explicitement qu'il s'agit d'une politique, sans moteur ni réseau simulé.

## Limites honnêtes

- un profil absent est créé avec une fondation minimale sans asset visuel inventé ;
- Foundation, Operations, SecurityEvents, CompositionEvents, FactionOwnership et Conversation sont
  préservés hors surface ;
- la sélection d'un blueprint Placeable reste obligatoire et aucun premier objet n'est choisi
  silencieusement ;
- l'exécution physique et la réplication réelles appartiennent au runtime, pas au dry-run Studio.

## Reproduction

```powershell
$projects = @(
  'tests/Forge.Domain.Tests/Forge.Domain.Tests.csproj'
  'tests/Forge.Application.Tests/Forge.Application.Tests.csproj'
  'tests/Forge.ModuleFormat.Tests/Forge.ModuleFormat.Tests.csproj'
)
$filter = 'FullyQualifiedName~G13O_PlaceableReactivePhysics|' +
  'FullyQualifiedName~Command_supports_preview_apply_history|' +
  'FullyQualifiedName~Command_rejects_revision_identity|' +
  'FullyQualifiedName~R047_PlaceableValidation|' +
  'FullyQualifiedName~Aggregate_and_workspace_round_trips'
& .\scripts\Verify-StudioIncrement.ps1 -Page placement -Evidence ux1-z-physics `
  -TestProject $projects -TestFilter $filter `
  -Capture artifacts/verification/studio-shell/ux1-z/physics-fhd.png
```
