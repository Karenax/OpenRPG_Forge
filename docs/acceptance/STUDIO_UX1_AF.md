# Acceptation Studio — UX1-AF Plans de zone assistés

État local au 17 août 2026 : **fonctionnel et qualifié** pour `ORF-AI-014` et `ORF-AI-015`.
La page spécialisée **Assistant / Plans & approbation** prépare d'abord un document de plan
gouverné, puis prévisualise et applique séparément sa construction géométrique.

## Preuves communes

- portail incrémental `scripts/Verify-StudioIncrement.ps1` réussi sur la page `plans` et la preuve
  `ux1-af-assisted-area-plan` en 75,68 s, avec résumé dans
  `artifacts/verification/studio-increment/ux1-af-assisted-area-plan.summary.json` ;
- tests ciblés : Domain 2/2, Application 10/10, ModuleFormat 3/3 et MCP 2/2, soit 17/17 ;
- smoke Studio headless réussi : sélection explicite, confidentialité/provenance, plan et
  construction en dry-run, deux historiques indépendants, permissions calculées sur le catalogue
  final, gardes de révision projet/catalogue, réponses asynchrones périmées bloquées, undo/redo,
  sauvegarde/réouverture, source intacte et ressources temporaires libérées ;
- captures Forward+ Full HD, QHD et 4K inspectées dans
  `artifacts/verification/studio-shell/ux1-af/` : formulaire, politique de confidentialité, diff
  sémantique, deux aperçus et deux groupes d'actions restent entièrement lisibles ;
- portail transversal `scripts/Verify.ps1` réussi au jalon UX1-AD/AE/AF : 1 980 tests .NET,
  39 tests Python, builds sans avertissement, graphe Roslyn déterministe à 9 059 nœuds et
  60 404 relations sans diagnostic, puis smokes Studio, Runtime, Forward+, MCP et réseau.

### ORF-AI-014

Zone, kit, titre, archétype, dimensions, seed, contraintes, objectifs et approbation sont choisis
explicitement. L'aperçu montre topologie, coût, provenance, empreinte du prompt, diff sémantique et
confidentialité sans écrire le catalogue ni la géométrie. L'application persiste uniquement le
document de plan dans son historique dédié.

### ORF-AI-015

Un plan approuvé et non construit est sélectionné explicitement. Le dry-run de construction montre
placements, navigation, éclairage, patch et inverse ; l'application passe par l'historique projet,
reste atomiquement annulable et refuse double application, permission insuffisante ou révision
périmée.

## Limites honnêtes

- la preuve est entièrement hors ligne avec `DeterministicOffline`, réseau interdit et contenu
  projet masqué ; elle ne qualifie aucun fournisseur externe réel ;
- les deux historiques sont volontairement séparés : annuler le document de plan ne prétend pas
  annuler une géométrie, et inversement ;
- Crypte des Cendres contient déjà des plans construits ; la preuve utilise donc un workspace
  temporaire isolé, supprimé après capture, et contrôle que la source n'a reçu ni plan ni commande ;
- la construction applique les commandes géométriques canoniques, mais ne revendique pas de session
  multi-utilisateur ni de réseau vivant.

## Reproduction

```powershell
$projects = @(
  'tests/Forge.Domain.Tests/Forge.Domain.Tests.csproj'
  'tests/Forge.Application.Tests/Forge.Application.Tests.csproj'
  'tests/Forge.ModuleFormat.Tests/Forge.ModuleFormat.Tests.csproj'
  'tests/Forge.Mcp.Tests/Forge.Mcp.Tests.csproj'
)
$filter = 'FullyQualifiedName~AssistedAreaPlanTests|' +
  'FullyQualifiedName~AssistedAreaPlanWorkflowTests|' +
  'FullyQualifiedName~AssistedAreaBuildWorkflowTests|' +
  'FullyQualifiedName~AssistedAreaPlanStoreTests|' +
  'FullyQualifiedName~AssistedAreaBuildMcpRequiresApprovalAndPersistsAtomicPlacementsWithRevert|' +
  'FullyQualifiedName~AssistedAreaPlanMcpProposesPersistsAndRevertsWithoutGeometryMutation'
& .\scripts\Verify-StudioIncrement.ps1 -Page plans -Evidence ux1-af-assisted-area-plan `
  -TestProject $projects -TestFilter $filter `
  -Capture artifacts/verification/studio-shell/ux1-af/ux1-af-assisted-area-plan-fhd.png
```
