# Tidewater — hauteurs du terrain édité dans le rendu de l'eau

Le prototype natif applique désormais les corrections canoniques de hauteur
aux appels `terrainHeightAt` des shaders de géométrie et d'optique de l'océan.
La texture de corrections aux coins reste distincte du raster source et est
partagée entre les deux matériaux. L'élévation globale est également appliquée.
Les normales et l'AO utilisent la texture partagée du lot précédent.

Le chemin sans correction conserve le shader original intact : ajouter zéro
dans une fonction adaptée produisait un écart reproductible de 20 pixels,
maximum 2/255, après Undo. Les deux contrôles stricts initiaux ont donc échoué
(`relief-water-terrain-proof-001` et `002`), sans assouplir leur assertion.

## Preuves

Racine : `E:/OpenRPG_Forge/.tidewater-integration/`.

- Compilation et Clippy strict réussis pour `tidewater_plant_probe`.
- Après correction, `relief-water-terrain-proof-003` passe l'assertion stricte :
  Undo restitue zéro pixel différent de la référence FHD, SHA-256
  `579e78405e5d4886faaa96a79ee6042a9de3b4a3f4c412d109888255f8082d64`.
  Le rendu local édité reste identique aux deux essais précédents, SHA-256
  `7a330204ca07030452db88009d1ec5b0ea8acdce6a2c6418b2ff04652bc24de1`.
  Binaire final :
  `f5a3f65b58647ac7b7c23c459497dc02f9e1246dfd458b665e673763500a5dfb`.
- Corrections locales : 203 plantes suivent toujours le relief dans les deux
  passes ; normales et AO conservent leurs hashes qualifiés précédemment.
- `water-terrain-comparison-001.json` : le rendu local évolue sur 322 pixels
  par rapport au lot AO ; le rendu global +3 m évolue sur 132070 pixels.
  Ces comparaisons précèdent la correction du chemin sans retouche.
  Les buffers d'ombres proches et lointaines restent identiques dans les deux cas.
- `native-global-water-terrain-002.png`, inspectée visuellement : profondeur et
  intersections d'eau réagissent au terrain relevé. Les bâtiments restent
  partiellement enterrés et le rivage simulé reste issu de l'état source.
  Binaire de cet essai :
  `920ac8b92bc96021444a0d86d7f4a3fca120ebc82e28989f5c97d7068773fe6c`.
- Le premier essai global (`001.log`) a subi une perte du device Vulkan,
  puis le second s'est terminé normalement. La cause du pilote n'est pas établie ;
  cela ne constitue pas une qualification de stabilité prolongée.

## Limites

Le champ de propagation côtier, les entrées de ShoreSim, les stations de
déferlement et la bathymétrie des embruns ne sont pas encore recalculés depuis
le relief édité. Ce lot ne valide donc pas une côte entièrement réactive.
Le raccordement Studio interactif, les placements manuels, les bâtiments,
la parité complète et WebGPU restent ouverts. Aucun commit ni publication.
