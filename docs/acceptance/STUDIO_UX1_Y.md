# Acceptation Studio — UX1-Y Fondation polygonale de trigger

État local au 16 août 2026 : **fonctionnel et qualifié** pour `ORF-TRG-001`, `ORF-TRG-002`,
`ORF-TRG-006` et `ORF-TRG-007`. Le lot rend un trigger polygonal, sa politique générique et ses
événements d'entrée/sortie accessibles depuis **Objets & interactions**. Il ne qualifie ni les
portails UX2, ni les volumes 3D avancés, ni une distribution publique.

## Preuves communes

- portail incrémental `scripts/Verify-StudioIncrement.ps1` réussi sur la page `placement` et la
  preuve `ux1-y-foundation`, avec résumé dans
  `artifacts/verification/studio-increment/ux1-y-foundation.summary.json` ;
- tests ciblés : Domain 2/2, Application 4/4, ModuleFormat 1/1 et MCP 1/1, soit 8/8 ;
- smoke Studio headless réussi : sélection explicite, géométrie/politique/liaisons invalides
  refusées, brouillon et approbation périmés refusés, commande, undo/redo, round-trip et
  préservation des profils voisins ;
- captures Forward+ inspectées en Full HD, QHD et 4K dans
  `artifacts/verification/studio-shell/ux1-y/`. Le contrôle correctif confirme un contenu
  réellement scrollable, l'aperçu X/Z à échelle uniforme et toutes les actions principales
  visibles ;
- portail transversal `scripts/Verify.ps1` réussi au jalon intégré UX1-X/Y/Z : 1 980 tests .NET,
  39 tests Python, builds sans avertissement, graphe Roslyn déterministe à 8 959 nœuds,
  59 067 relations et 0 diagnostic, puis smokes Studio, Runtime, Forward+, MCP et réseau réussis.

### ORF-TRG-001

Le contour extérieur et les trous se composent par sommets X/Z nommés, avec hauteur et pas de
grille. L'aperçu 2D conserve les proportions par une échelle uniforme. Les intersections et
géométries insuffisantes sont refusées avant le bus de commandes.

### ORF-TRG-002

Le formulaire expose filtres, tags requis/exclus, calques, conditions, délai, politique de tir,
actions typées et transformation. Une fondation absente est créée sans modifier le projet lors de
l'ouverture ou du dry-run.

### ORF-TRG-006

`OnEnter` conserve la liaison canonique `VolumeEntered` et la version ForgeScript. Les scripts avant
et après sont facultatifs : les champs UI blancs redeviennent `null`, jamais une référence vide.

### ORF-TRG-007

`OnExit` suit le même contrat avec `VolumeExited`. L'aperçu, l'application et la lecture MCP restent
liés au même profil et à la même révision.

## Limites honnêtes

- la surface UX1-Y édite uniquement `ExtrudedPolygon2D` ; Box, Sphere, Capsule et ConvexHull sont
  conservés en lecture seule sans conversion ;
- aucun gizmo 3D n'est revendiqué : l'aperçu est une empreinte X/Z 2D explicite ;
- Portal, Validation, Advanced, overrides et parent de transformation sont préservés mais non
  exposés ; `ORF-TRG-003` et `ORF-TRG-004` restent `planned` ;
- le dry-run valide le contrat du trigger, sans exécuter un script ou une transition runtime réelle.

## Reproduction

```powershell
$projects = @(
  'tests/Forge.Domain.Tests/Forge.Domain.Tests.csproj'
  'tests/Forge.Application.Tests/Forge.Application.Tests.csproj'
  'tests/Forge.ModuleFormat.Tests/Forge.ModuleFormat.Tests.csproj'
  'tests/Forge.Mcp.Tests/Forge.Mcp.Tests.csproj'
)
$filter = 'FullyQualifiedName~R048_Trigger|' +
  'FullyQualifiedName~R049_Trigger|' +
  'FullyQualifiedName~Command_supports_preview_apply_history|' +
  'FullyQualifiedName~Command_rejects_revision_identity|' +
  'FullyQualifiedName~Aggregate_and_workspace_round_trips|' +
  'FullyQualifiedName~Trigger_foundation_resource'
& .\scripts\Verify-StudioIncrement.ps1 -Page placement -Evidence ux1-y-foundation `
  -TestProject $projects -TestFilter $filter `
  -Capture artifacts/verification/studio-shell/ux1-y/foundation-fhd.png
```
