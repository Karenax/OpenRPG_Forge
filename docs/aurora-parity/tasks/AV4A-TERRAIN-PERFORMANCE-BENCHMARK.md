# AV4A — Benchmark terrain multi-biome

## Périmètre

Cette tranche ferme le dernier critère AV4A sur la carte extérieure représentative Asset Vault
128 × 128 m. Les mesures proviennent de la reconstruction réelle dans le moteur 3D headless ; elles
ne remplacent pas la source canonique du terrain et n’autorisent pas encore une extension 512 × 512 m.

## Instrumentation livrée

- `GodotTerrainAdapter` mesure plan, mesh, collisions, features, scatter, total et allocations ;
- `GodotSurfaceNavigationAdapter` mesure construction du mesh de navigation et allocations ;
- le runtime mesure working set et heap avant/après la reconstruction, puis chronomètre la requête
  moteur du chemin camp-sanctuaire ;
- une commande `EditTerrainBrush` est appliquée sur un `ProjectState` isolé pour mesurer la sculpture
  sans écrire dans le module ni altérer le smoke jouable ;
- `GodotAssetModelLibrary` compte assets gouvernés, niveaux LOD et octets stockés ; les groupes
  MultiMesh dont la distance de visibilité active le culling sont comptés ;
- `TerrainProductionBenchmark` évalue seize checks bornés et produit un rapport JSON schéma v1 ;
- `scripts/Verify.ps1` exige le marker runtime, le rapport, ses dimensions et zéro check en échec.

## Mesure de référence

Passage local headless du 5 août 2026 :

- reconstruction : 68,806 ms ; collisions : 5,367 ms ; navigation : 10,778 ms ;
- sculpture transactionnelle : 3,604 ms ; chemin moteur de 72 points : 23,649 ms ;
- allocations gérées : 1 737 328 octets ; delta working set : 17 506 304 octets ;
- 1 089 échantillons/vertices, 2 048 triangles, 181 formes de collision ;
- 1 990 polygones de navigation, 58 triangles bloqués et un lien de pont ;
- 22 assets, 66 niveaux LOD, 357 instances scatter et trois groupes de culling.

Les budgets restent volontairement supérieurs à cette référence pour absorber une machine plus
lente, tout en bloquant une régression d’ordre de grandeur avant l’extension de la carte.

## Preuves ciblées

- 13 tests `Forge.EngineAdapter.Tests` passent ; deux scénarios nouveaux couvrent tous les budgets
  et les diagnostics d’une reconstruction trop lente avec couverture LOD incomplète ;
- `Forge.Runtime` compile en Release sans avertissement ;
- le smoke réel émet `OPENRPG_FORGE_AV4A_TERRAIN_BENCHMARK_PASS area=128x128` et écrit
  `artifacts/verification/asset-vault-terrain-benchmark.json`.

## Critère de passage

AV4A est fermé lorsque le rapport reste vert dans le gate global, que le graphe est frais et que le
commit est publié sur `main`. La tranche suivante est AV5 — livraison, métriques consolidées et
finitions artistiques gouvernées.
