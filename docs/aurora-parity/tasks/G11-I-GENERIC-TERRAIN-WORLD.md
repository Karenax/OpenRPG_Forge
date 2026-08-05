# G11-I — Terrain générique, biomes et navigation volumétrique

## Périmètre

- `ORF-AREA-004` — terrain de base ;
- `ORF-AREA-005` — features de terrain ;
- `ORF-AREA-032` — navigation volumétrique ;
- `ORF-WPT-011` — routes aériennes ;
- `ORF-EE-023` — chargement de zone amélioré.

## Résultat livré

- profil de monde canonique versionné par zone, indépendant du moteur, avec providers `None`,
  `Flat`, `Heightmap`, `Voxel` et `Mesh`, dimensions, résolution, seed, hauteurs, couches de matériau
  et trous ;
- terrain extérieur `Heightmap` déterministe par défaut, matériaux sol/herbe/roche et couches de
  biomes prairie/roche ;
- routes, rivières, ponts, murs et autres features paramétriques, avec validation des limites et des
  croisements pont/route/rivière ;
- volumes optionnels de vol, nage et espace, graphe A* 3D déterministe, coûts, obstacles, politique
  d'évitement et données d'overlay ;
- routes aériennes avec altitude, courbes, inclinaison et politique d'obstacle ;
- coordinateur de chargement asynchrone avec dépendances, prélecture, progression, annulation,
  concurrence bornée, cache à budget et mesures de durée ;
- adapter commun Creator/runtime qui génère un `ArrayMesh`, sa collision statique et les overlays
  de features, volumes et routes ;
- fenêtre **Terrain & volumes** appliquant le profil complet par une commande transactionnelle avec
  dry-run, audit, undo et redo ;
- formats zone `1.4.0`, projet `0.6.11` et workspace `1.5.0`, avec migrations et workspaces fournis
  réellement migrés.

## Preuves

- `AreaWorldProfileTests` couvre terrain, matériaux, biomes, trous, redimensionnement et validation ;
- `VolumetricNavigationTests` couvre le graphe, les coûts, les obstacles et une route 3D ;
- `SetAreaWorldProfileHandlerTests` couvre dry-run, mutation, audit, undo et redo ;
- `AreaStreamingCoordinatorTests` couvre prélecture, cache, budget, progression et annulation ;
- `TerrainBuildPlanBuilderTests` couvre mesh déterministe, trous, biomes et features ;
- les tests de sérialisation et de session couvrent les migrations des trois formats ;
- smoke Toolset `OPENRPG_FORGE_TOOLSET_G11I_TERRAIN_PASS` : édition réelle, 81 sommets, 126
  triangles, un trou, deux biomes, trois features, trois volumes, route A* 3D, persistance et
  restauration d'historique ;
- smoke runtime `OPENRPG_FORGE_RUNTIME_G11I_TERRAIN_PASS` : lecture du workspace `1.5.0`,
  reconstruction et chemin de streaming mesuré.

## Limites explicites

- `Heightmap` et `Flat` possèdent un rendu natif complet. `Voxel` et `Mesh` sont des contrats
  persistants avec repli déterministe ; leur chargement géométrique spécialisé reste à livrer ;
- les features sont rendues par segments 3D fonctionnels ; tessellation de spline, intersections
  routières et composition artistique de production ne sont pas revendiquées ;
- le pathfinding volumétrique est disponible et testable, mais le pilotage d'un acteur volant, la
  navigation de foule et l'évitement dynamique runtime restent à brancher ;
- les routes aériennes sont persistées et visualisées, sans contrôleur d'animation bancaire ;
- le coordinateur charge des ressources bornées d'une zone. Le monde ouvert massif et le streaming
  réseau ne font pas partie du MVP ;
- la sculpture/peinture interactive et la carte Forgotten Forest curatée restent postérieures à la
  fondation générique.

## Critère de passage

G11-I est fermé lorsque tests ciblés, smokes Toolset/runtime, migrations, tracker, graphe et
`scripts/Verify.ps1` sont verts sur le même état Git.
