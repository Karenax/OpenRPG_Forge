# Acceptation Studio — UX1-AB Dégâts, chargement et événements de porte

État local au 17 août 2026 : **fonctionnel et qualifié** pour `ORF-DOOR-007`,
`ORF-DOOR-010`, `ORF-DOOR-014` et `ORF-DOOR-015`. Le lot expose trois sections humaines depuis
**Objets & interactions** sans ouvrir ni promouvoir les accès, portails, liens ou la navigation.

## Preuves communes

- portail incrémental `scripts/Verify-StudioIncrement.ps1` réussi sur la page `placement` et la
  preuve `ux1-ab-events` en 55,42 s, avec résumé dans
  `artifacts/verification/studio-increment/ux1-ab-events.summary.json` ;
- tests ciblés : Domain 1/1 et ModuleFormat 1/1, soit 2/2 ;
- smoke Studio headless réussi : sélection explicite, simulations dégâts/événements, refus de
  révision périmée, fusion des liaisons, commande, préservation, undo/redo, round-trip et transaction
  UI dans une session temporaire ;
- captures Forward+ Full HD, QHD et 4K inspectées dans
  `artifacts/verification/studio-shell/ux1-ab/` : les onglets **Solidité**, **Chargement** et
  **Événements**, l'aperçu et les actions restent visibles sans débordement ;
- portail transversal `scripts/Verify.ps1` réussi au jalon intégré UX1-AA/AB/AC : 1 980 tests .NET,
  39 tests Python, builds sans avertissement, graphe Roslyn déterministe et smokes moteur complets.

### ORF-DOOR-007

Points de structure, dureté, protection plot, apparences endommagée/brisée, recalcul du passage,
débris et service de réparation composent la politique de dégâts. L'aperçu de brèche reste sans
écriture et la navigation voisine n'est pas éditée.

### ORF-DOOR-010

Image, titre, conseil, durée minimale et repli sur la destination sont configurables ensemble.
Le repli est vérifié séparément et aucun portail n'est créé pour fournir l'écran de chargement.

### ORF-DOOR-014

OnDamaged et OnDeath disposent chacun d'une activation et d'un script explicite. Les liaisons
`damage-applied` et `entity-destroyed` sont autoritaires côté serveur et les événements voisins
restent les mêmes objets dans le même ordre relatif.

### ORF-DOOR-015

L'attaque physique distingue ciblage avant résolution et résultat après résolution. Les deux
liaisons sont fusionnées sans toucher à `ability-impacted`, heartbeat ou aux événements personnalisés.

## Limites honnêtes

- AccessEvents, Foundation, Portal, Links, Dialogue, Faction, Assembly, matériaux et overrides sont
  préservés mais non éditables dans ce lot ;
- une nouvelle fiche de porte prend sa place dans l'ordre canonique trié du catalogue, et non une
  position d'insertion arbitraire ;
- le dry-run valide et résout les contrats de dégâts/événements ; il ne simule pas un navmesh ni une
  transition réseau réelle ;
- `ORF-DOOR-004` à `006`, `011` à `013` et les autres routes Door restent `planned`.

## Reproduction

```powershell
$projects = @(
  'tests/Forge.Domain.Tests/Forge.Domain.Tests.csproj'
  'tests/Forge.ModuleFormat.Tests/Forge.ModuleFormat.Tests.csproj'
)
$filter = 'FullyQualifiedName~G13P_DoorDamageLoading_ProducesBreachAndNavigationUpdate|' +
  'FullyQualifiedName~Aggregate_and_workspace_round_trips_preserve_all_world_feature_families'
& .\scripts\Verify-StudioIncrement.ps1 -Page placement -Evidence ux1-ab-events `
  -TestProject $projects -TestFilter $filter `
  -Capture artifacts/verification/studio-shell/ux1-ab/ux1-ab-events-fhd.png
```
