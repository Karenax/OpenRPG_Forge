# Tidewater — sélection adaptative et déplacement GPU

Source figée : `1438b1abfcaee3267092b75573014f4d9b4a983c`.
MIT original conservé dans `third_party/tidewater/LICENSE`.
Toutes les données volumineuses citées sont sous
`E:/OpenRPG_Forge/.tidewater-integration/`.

## Sélection CPU

`forge_world::cdlod` est indépendant du moteur. Il reprend le parcours du
quadtree, la distinction entre tuile hors portée et hors frustum, le repli de
quart de tuile sur le niveau du parent et le tri stable de proximité.
L'épuisement du quota échoue explicitement, sans résultat partiel silencieux.
`RasterLodBounds` reprend la pyramide source et ses marges -1/+2 m ; ces bornes
de rendu ne constituent pas une qualification des collisions. Les corrections
auteur élargissent ces bornes par leurs extrema globaux.

L'oracle est calculé par le code upstream intact, après fondations du village :
`cdlod-oracle-002.json`, SHA-256
`67fac1515b310f364bd6b67b254033ae7bb2fe94dac2e72c03a4abbe14622d1c`.
Six caméras de référence et 41 positions d'un trajet côtier : **47 cas**,
**2 845 tuiles dans le même ordre**, **405 bornes**, **9 lignes de morphing**
identiques. Le lecteur de cet exemple active la lecture JSON float-roundtrip ;
les paramètres de morphing sont aussi exportés par bits binary64 pour éviter
de confondre arrondi du parseur et différence algorithmique.

Tests ciblés : repli sur niveau parent, ordre des distances égales, rejet hors
frustum sans réapparition, quotas et entrées invalides, bord du raster et
élargissement par les retouches auteur. Les quatre tests d'auteur raster et
trois tests CDLOD passent.

## Rendu natif exécuté

`terrain_lod::LodMaterial` étend le matériau de surface déjà adapté. La texture
R32Float conserve les octets de hauteur. Les fonctions WGSL originales de
hauteur, accrochage à la grille et transition CDLOD sont reprises sans réécriture.
La grille 40×40 conserve les diagonales alternées. Les tuiles sont sélectionnées
par Rust depuis la pose native : aucun tableau de tuiles oracle n'est rejoué.

Pour ce diagnostic, chaque grille est développée dans un mesh commun ;
l'instanciation de production reste à réaliser. Le sélecteur fonctionne sur une
caméra fixe et le shader refuse les retouches que ce chemin GPU ne sait pas
encore appliquer. Les fixtures antérieures gardent leur chemin de grille fixe.

GPU natif effectivement journalisé : AMD Radeon RX 7800 XT, Vulkan,
pilote 26.8.1 (LLPC). Fixtures `coast-lod-probe-002`, `island-lod-probe-002`.
Les deux moteurs rendent le terrain blanc sur fond noir, sans végétation,
éclairage ou MSAA, pour comparer les contours uniquement.

| Vue | Tuiles | Triangles | Pixels couverts | Pixels différents |
|---|---:|---:|---:|---:|
| Côte | 62 | 198 400 | 1 125 218 | 0 |
| Île | 18 | 57 600 | 1 135 728 | 0 |

Résultats : `coast-lod-coverage-001.json`, `island-lod-coverage-001.json`.
Oracles : `source-coast-lod-mask-002.png`, `source-island-lod-mask-001.png`.
Captures natives : `native-coast-lod-mask-001.png`, `native-island-lod-mask-001.png`.
Les SHA distincts des PNG source et natifs reflètent leurs encodages ; les masques
décodés sont identiques. Pas de tolérance spatiale utilisée pour ce résultat.

`native-coast-lod-surface-001.png` inspectée : terrain texturé adaptatif et
canopée présents ; fonds marins exposés, océan absent, éclairage Bevy provisoire.

## Portée restante

Une silhouette identique ne prouve pas les hauteurs intérieures, la transition
animée sans fissure, l'égalité du matériau ou la scène entière. Le frustum CPU
emploie le plan lointain fini source ; la projection GPU Bevy demeure celle de
Bevy. Ce test ne qualifie ni WebGPU navigateur ni plusieurs adaptateurs.

Restent notamment : instanciation et sélection continue, corrections GPU,
source lumineuse/ombres/humidité, eau/ciel/village/récif, végétation complète,
résolution des blobs de zone, Rapier et édition Studio. Aucune ligne TW close,
aucune parité générale ni publication Git.

## Vérifications finales

- 423 tests projet/monde réussis (`cdlod-domain-tests.log`).
- Clippy strict sur toutes les cibles de `forge_world`, puis exemple natif
  (`cdlod-native-clippy-final.log`) : réussi.
- Compilation WASM de `forge_world` et `forge_project` réussie
  (`cdlod-domain-wasm.log`) ; ce n'est pas une exécution du renderer navigateur.
- Oracle CPU rejoué sans différence (`cdlod-oracle-verify-final.log`).
- Binaire final `tidewater_plant_probe.exe` :
  `7ac240708658dd236be0e15b1e55eb00e52327a9f297d48d3ada866b29e4f8b5`.
- Exécution finale côtière, masque et surface : codes 0, masque identique au
  premier et à la source (`coast-lod-coverage-final.json`). Surface finale
  `native-coast-lod-surface-002.png` inspectée, identique à 001, SHA
  `90c91f23be0c263f1cf7dff9821d180e3e7241098f4149af954ac1b9b30bd800`.
- Régression feuillu proche `native-tree-mask-regression-003.png` identique :
  `49be5ddcbfcb3b7c66ec0bc3a13debeb4e78a2c7f75571477e7ac556f36021ae`.
- Source de référence restée propre ; branche Forge `Rust` conservée.
  Tous les processus de ces captures se sont terminés.
