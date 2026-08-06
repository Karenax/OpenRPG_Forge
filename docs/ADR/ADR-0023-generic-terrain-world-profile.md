# ADR-0023 — Terrain générique comme donnée canonique de zone

## Statut

Accepté le 5 août 2026.

## Contexte

OpenRPG Forge doit permettre de construire des extérieurs variés sans lier une carte à un biome,
un plug-in de terrain ou une scène du moteur. Une grille d'altitude convient aux collines et aux
vallées, mais pas aux grottes, tunnels et surplombs. Le Creator et le runtime doivent reconstruire
exactement le même monde, tandis que les migrations, validations et commandes restent testables
hors du moteur 3D.

## Décision

- `AreaWorldProfile` est la source de vérité versionnée. Il contient le terrain, ses matériaux, les
  biomes, les features paramétriques, les volumes de navigation, les routes aériennes et le profil
  de chargement.
- X/Z forment le plan horizontal et Y représente l'altitude. Les unités sont métriques.
- Un provider canonique peut être `None`, `Flat`, `Heightmap`, `Voxel` ou `Mesh`. G11-I rend
  réellement `Heightmap` et `Flat`; `Voxel` et `Mesh` conservent un contrat sérialisable et un repli
  déterministe jusqu'à leurs adapters spécialisés.
- Les biomes sont des couches qui échantillonnent altitude, pente, matériau et densité. Ils ne
  possèdent ni ne dupliquent le terrain.
- Les grottes et surplombs utilisent des trous déclarés dans le terrain et une géométrie 3D séparée.
- Routes, rivières, ponts et murs sont des features versionnées. Leur rendu moteur est dérivé et
  jetable.
- Les volumes de vol, nage et espace sont transformés en graphe déterministe avec coûts, obstacles
  et diagnostics. Les routes aériennes restent distinctes de ce graphe.
- Toute mutation d'auteur passe par une commande révisionnée, validée, prévisualisable, annulable et
  rejouable. Les nœuds, meshes, collisions et overlays du moteur ne sont jamais persistés.
- Le chargement asynchrone emploie des chemins confinés au module, une prélecture bornée, un cache à
  budget et l'annulation. Aucun service distant ni Blender n'est requis au runtime.

## Conséquences

L'adapter interne suffit au prototype fonctionnel et évite d'ajouter une dépendance de production à
Terrain3D. Un provider externe pourra être ajouté plus tard derrière `ITerrainProvider`, après preuve
de licence, compatibilité, migration et reproductibilité. La sculpture et la peinture interactives,
la tessellation de splines de production, le suivi d'une route par un acteur volant et les
benchmarks de grandes cartes restent des incréments séparés.

## Vérification postérieure AV4A

Le 5 août 2026, la tranche extérieure représentative 128 × 128 m a reçu un benchmark runtime
versionné. Il mesure mémoire, reconstruction, collisions, navigation, sculpture, LOD et culling sans
changer la décision architecturale : le terrain canonique reste indépendant de l’adapter moteur.
L’extension à une carte plus grande demeure conditionnée au respect des budgets enregistrés.
