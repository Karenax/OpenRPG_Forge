# FishStandStall — acquisition source déterministe

L'acquisition technique locale est disponible sous
`E:/OpenRPG_Forge/.tidewater-integration/fish-stand-source-export-001`.
Elle provient du checkout Tidewater propre au commit
`1438b1abfcaee3267092b75573014f4d9b4a983c`; le checkout upstream n'a pas été
modifié.

`export-fish-stand.mjs` appelle hors du clone la fonction source
`buildStallKit`, exportée temporairement dans un module dérivé jetable. La
géométrie résultante est exactement celle de `FishStandStall`, placée par la
source à `x = 49,9`, `z = -74,6`, `yaw = 1,45`, avec la hauteur
`terrain.heightAt(x,z)`.

## Contenu vérifié

- 32 333 sommets et 40 259 triangles.
- Buffers source : `position`, `normal`, `uv`, `color`, `aLayer`, `aUV2` et
  `indices`, tous finis et avec indices dans les bornes.
- SHA-256 de `position.f32le.bin` :
  `ac5b337738cb776d4ef0f203a751552d5ad2339d093b6a4aec6dc674d6598248`.
- SHA-256 de `indices.u32le.bin` :
  `d8e9de0aab7d757115148494e2b32a712c0db57ce8ac38f3ec0fb80c6b3f14cf`.
- 64 fichiers sources conservés et hachés : `FishStand.js`, `StallKit.js`,
  `props.bin`, `props.json`, l'ensemble des textures et `signs.png`, ainsi que
  les licences MIT Tidewater et crédits CC0 Poly Haven.
- Une seconde exportation indépendante dans
  `fish-stand-source-export-002` a les mêmes manifestes de géométrie et de
  sources ; les deux passent `export-fish-stand.mjs --verify`.

## Limite d'intégration

L'export reste `source-acquired-not-transposed`. `StallKit.js` emploie un
matériau WGSL Tidewater dédié à des texture arrays et aux attributs `aLayer` et
`aUV2`. Le matériau Bevy du village ne transporte pas ce contrat. Un simple
mesh ou GLB perdrait ses couches, ses props et son atlas, et ne constituerait
pas une équivalence visuelle. Aucun faux GLB, aucun index Studio, aucun
raccord runtime, bundle nouveau, capture GPU, commit ou publication n'est donc
revendiqué ici.
