# ADR-0058 — Preuves spatiales canoniques pour les cartes générées

Date : 2026-08-29
Statut : accepté
UUID : `d15a0000-0000-4000-8000-000000000058`
Schéma : v1
Provenance : `author:openrpg-forge`

## Contexte

Une scène de génération pouvait être visuellement promue tout en laissant le terrain métier désactivé.
L'authoring assisté ne possédait alors ni hauteur canonique ni carte de végétation : un client externe
devait deviner `Y` depuis la scène, pouvait choisir une clairière involontaire, faire léviter les
bâtiments et accepter des rôles d'assets incohérents. Des contrôles de collision après placement ne
suffisent pas, car la décision de site est déjà mauvaise et n'est pas reproductible.

## Décision

- Chaque nouvelle promotion WorldGeneration porte une surface de placement régulière et une occupation
  environnementale versionnées, bornées et liées par SHA-256 au rapport de validation.
- Le baker provider-neutral extrait ces preuves du mesh final et des MultiMeshes artistiques. La
  promotion les valide, les persiste dans `AreaWorldProfile` et refuse toute preuve partielle.
- `FindTerrainPlacementCandidates` est le seul service public de recherche de site assistée. Il évalue
  l'emprise complète, la variation du terrain, la densité voisine normalisée par hectare et
  l'intersection environnementale. Une demande de végétation doit déclarer cette densité et ne peut
  descendre sous le plancher canonique de 6 instances/ha.
- Les plans extérieurs interdisent `ExplicitTransform`; la construction recalcule `Y` depuis la surface
  promue et bloque une emprise trop pentue, hors limites ou occupée par de la végétation.
- L'intent de végétation est contrôlé autour de chaque nœud de contenu, pas seulement au centre moyen
  du plan, afin qu'une moitié nue de la carte ne soit pas masquée par une zone dense éloignée.
- Les tags sémantiques sont des contrats : `npc`/`villager` exigent un blueprint `Creature` et
  `village-house` exige une composition contenant `foundation`, `wall` et `roof`.
- Les anciens manifestes de promotion restent lisibles, mais une nouvelle promotion ne peut être créée
  sans les deux preuves spatiales. Aucune règle ne dépend d'un module, d'un biome ou d'un kit précis.

## Preuves

- Domaine : `tests/Forge.Domain.Tests/Areas/TerrainPlacementSurfaceDefinitionTests.cs`.
- Construction et rôles : `tests/Forge.Application.Tests/Assistant/TerrainAwareAssistedAreaBuildTests.cs`
  et `AssistedAreaBuildWorkflowTests.cs`.
- Promotion et réouverture : `tests/Forge.Application.Tests/WorldGeneration/WorldGenerationWorkflowTests.cs`,
  `tests/Forge.ModuleFormat.Tests/Workspaces/WorldGenerationStoreTests.cs` et
  `tests/Forge.Mcp.Tests/Projects/WorldGenerationAgentServiceTests.cs`.
- Godot : `tests/Godot/WorldGenerationNativeSmoke.gd`, qualifications addon et référence 2 km.
- Référence Windows Godot 4.7.1 :
  `artifacts/world-generation/reference-2km/godot-flow/v284231f0/durable-placement-20260829-ref-c/`.

## Conséquences

La création assistée échoue plus tôt et avec un diagnostic stable quand le monde ne fournit pas les
preuves nécessaires. Les rapports gagnent un volume proportionnel à l'échantillonnage du terrain et
aux cellules environnementales ; des cellules de quatre mètres conservent une précision utile sans
embarquer chaque instance. Les scènes restent la représentation de rendu et les preuves spatiales la
représentation métier compacte : aucune lecture opportuniste du texte `.tscn` n'est autorisée.

## Remplacement

Cette décision complète l'ADR-0043 pour la construction extérieure et l'ADR-0050 pour la promotion
WorldGeneration. Elle ne remplace pas leur cycle d'approbation, de dry-run et de réversibilité.
