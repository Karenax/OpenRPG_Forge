# Acceptation Studio — UX1-X Fondation de porte

État local au 16 août 2026 : **fonctionnel et qualifié** pour `ORF-DOOR-001`,
`ORF-DOOR-002` et `ORF-DOOR-003`. Ce lot expose la fondation d'une porte depuis la page
**Objets & interactions** de l'unique fenêtre `Forge.Studio`. Il ne qualifie ni les accès, pièges,
portails et liens, ni la navigation UX2, ni une distribution publique.

## Preuves communes

- portail incrémental `scripts/Verify-StudioIncrement.ps1` réussi sur la page `placement` et la
  preuve `ux1-x-interaction`, avec résumé dans
  `artifacts/verification/studio-increment/ux1-x-interaction.summary.json` ;
- tests ciblés : Domain 1/1, Application 3/3 et ModuleFormat 1/1, soit 5/5 ;
- smoke Studio headless réussi sur Forgotten Forest : sélection explicite, dry-run, application,
  refus d'une approbation périmée, undo/redo, sauvegarde/réouverture et restauration du projet ;
- capture Forward+ Full HD inspectée dans
  `artifacts/verification/studio-shell/ux1-x/interaction-fhd.png` : les trois sections
  **Placement**, **États** et **Ouverture**, l'aperçu et les actions principales restent lisibles ;
- portail transversal `scripts/Verify.ps1` réussi au jalon intégré UX1-X/Y/Z : 1 980 tests .NET,
  39 tests Python, builds sans avertissement, graphe Roslyn déterministe à 8 959 nœuds,
  59 067 relations et 0 diagnostic, puis smokes Studio, Runtime, Forward+, MCP et réseau réussis.

### ORF-DOOR-001

La fiche exige une porte choisie explicitement. Elle propose placement libre ou ancrage
d'architecture, kit, type d'ouverture et ajustements nommés. L'aperçu ne modifie pas le module et
la commande remplace le profil à sa place sans réordonner le catalogue.

### ORF-DOOR-002

L'auteur choisit l'état initial ouvert, fermé ou verrouillé. Les neuf états canoniques et leurs
transitions restent présents et l'aperçu montre le résultat en vocabulaire humain.

### ORF-DOOR-003

Portée, côté, dégagement, autorité, capacité de file et message d'échec sont éditables ensemble.
Les valeurs au quart de mètre restent représentables dans l'UI. Le résolveur canonique prouve les
états, côtés, files, transitions et retours ; les autres bornes sont refusées avant application.

## Limites honnêtes

- `ORF-DOOR-004` et les routes Door suivantes restent `planned` ;
- le validateur exige une politique Navigation : si elle manque, le lot crée uniquement une valeur
  interne valide avec visualisation désactivée. Cette donnée n'est ni exposée ni comptée comme
  `ORF-DOOR-019` ;
- Portal, Links, AccessEvents, Damage, Loading, Events, Dialogue, Faction, Assembly, overrides et
  matériaux restent préservés mais ne sont pas éditables ici ;
- l'aperçu valide une politique canonique ; il ne simule pas un navmesh ou une réplication réseau.

## Reproduction

```powershell
$projects = @(
  'tests/Forge.Domain.Tests/Forge.Domain.Tests.csproj'
  'tests/Forge.Application.Tests/Forge.Application.Tests.csproj'
  'tests/Forge.ModuleFormat.Tests/Forge.ModuleFormat.Tests.csproj'
)
$filter = 'FullyQualifiedName~R040_DoorFoundation|' +
  'FullyQualifiedName~Command_supports_preview_apply_history|' +
  'FullyQualifiedName~Command_rejects_revision_identity|' +
  'FullyQualifiedName~R041_DoorValidation|' +
  'FullyQualifiedName~Aggregate_and_workspace_round_trips'
& .\scripts\Verify-StudioIncrement.ps1 -Page placement -Evidence ux1-x-interaction `
  -TestProject $projects -TestFilter $filter `
  -Capture artifacts/verification/studio-shell/ux1-x/interaction-fhd.png
```
