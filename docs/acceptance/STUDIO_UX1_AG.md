# Acceptation Studio — UX1-AG Accès et événements de porte

État local au 17 août 2026 : **fonctionnel et qualifié** pour `ORF-DOOR-004`,
`ORF-DOOR-005`, `ORF-DOOR-006`, `ORF-DOOR-012` et `ORF-DOOR-013`. La fiche
**Accès & événements** réunit verrou, refus, piège et transitions terminales dans une surface
humaine depuis **Objets & interactions**.

## Preuves communes

- portail incrémental `scripts/Verify-StudioIncrement.ps1` réussi sur `placement` / `ux1-ag-events`
  en 75,98 s, avec résumé dans
  `artifacts/verification/studio-increment/ux1-ag-events.summary.json` ;
- tests ciblés : Domain 6/6, RpgKit.Domain 8/8, Application 3/3 et ModuleFormat 1/1,
  soit 18/18 ;
- smoke Studio headless réussi : sélection explicite, scaffold neuf requis et préservation du
  scaffold existant, verrou et credentials, refus, piège, événements terminaux, dry-run, doubles
  gardes de révision, commande canonique, undo/redo, round-trip, invalidités bloquées, libellés
  humains et transaction UI isolée sans altérer la session source ;
- capture Forward+ Full HD inspectée dans
  `artifacts/verification/studio-increment/ux1-ag-events-forward-plus-fhd.png`, complétée par
  `artifacts/verification/studio-shell/ux1-milestone/ux1-ag-qhd.png` et `ux1-ag-4k.png` : les trois
  onglets, l'aperçu et les actions restent lisibles sans clipping ;
- portail transversal `scripts/Verify.ps1` réussi au jalon UX1-AG/AH/AI/AJ : 1 988 tests .NET,
  39 tests Python, builds sans avertissement, graphe Roslyn déterministe à 9 132 nœuds et
  61 393 relations sans diagnostic, puis smokes Studio, Toolset, Runtime, Forward+, MCP et réseau.

### ORF-DOOR-004

État du verrou, clés ou credentials, compétence, difficulté, crochetage, reverrouillage, blocage,
consommation et permissions alimentent une seule politique d'accès canonique. L'aperçu exerce
l'autorisation et le refus sans écrire le catalogue.

### ORF-DOOR-005

Le refus d'ouverture expose version ForgeScript, messages français et anglais, son, animation et
script. Les traductions existantes non éditées restent conservées.

### ORF-DOOR-006

Le piège regroupe déclencheur, effet, détection, désamorçage, réarmement, propriétaire, effet visuel,
politique alliée, état armé, usage unique, côtés et événements. L'aperçu vérifie la décision
canonique sans déclencher de scène réelle.

### ORF-DOOR-012

`OnOpen` et `OnClose` sont liés aux transitions terminales `DoorOpened` et `DoorClosed`, avec version
ForgeScript explicite : aucun événement prématuré n'est présenté comme valide.

### ORF-DOOR-013

`OnLock` et `OnUnlock` peuvent être activés et rendus obligatoires au runtime. Les bindings
`DoorLocked` et `DoorUnlocked` restent eux aussi attachés aux transitions terminales.

## Limites honnêtes

- un `PortalUse` canonique minimal est créé en interne uniquement pour satisfaire l'agrégat neuf ;
  il n'est ni exposé, ni simulé, ni revendiqué et ne qualifie pas `ORF-DOOR-011` ;
- un `PortalUse` existant est préservé exactement ; Foundation, Navigation, validation et les autres
  composants voisins ne sont pas édités par ce lot ;
- les décisions de verrou, refus, piège et événement sont exercées par les services canoniques,
  sans moteur 3D, navigation vivante ni transport réseau ;
- la transaction apply/undo/redo de preuve s'exécute dans une session temporaire isolée ; projet,
  révision, journal et piles undo/redo de la session source restent inchangés.

## Reproduction

```powershell
$projects = @(
  'tests/Forge.Domain.Tests/Forge.Domain.Tests.csproj'
  'tests/Forge.RpgKit.Domain.Tests/Forge.RpgKit.Domain.Tests.csproj'
  'tests/Forge.Application.Tests/Forge.Application.Tests.csproj'
  'tests/Forge.ModuleFormat.Tests/Forge.ModuleFormat.Tests.csproj'
)
$filter = 'FullyQualifiedName~DoorAccessEventProfileTests|' +
  'FullyQualifiedName~DoorAccessEventFoundationServiceTests|' +
  'FullyQualifiedName~DoorEventServiceTests|' +
  'FullyQualifiedName~R081_|' +
  'FullyQualifiedName~R082_|' +
  'FullyQualifiedName~Aggregate_and_workspace_round_trips_preserve_all_world_feature_families'
& .\scripts\Verify-StudioIncrement.ps1 -Page placement -Evidence ux1-ag-events `
  -TestProject $projects -TestFilter $filter `
  -Capture artifacts/verification/studio-increment/ux1-ag-events-forward-plus-fhd.png
```
