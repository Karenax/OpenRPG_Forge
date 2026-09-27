# Terrain dans les passes de profondeur natives

Le prototype `tidewater_object_shadow_probe --terrain` ajoute le relief aux
trois couches d'ombre du village. Rust sélectionne les nœuds CDLOD depuis
la caméra principale et les bornes du raster canonique vérifié ; le GPU
effectue le snapping, le morphing et l'échantillonnage des hauteurs avec les
fonctions source de `terrain-lod.wgsl`. Les trois cascades utilisent la même
position de caméra principale pour le morphing, comme Tidewater.

La grille 40×40 est instanciée (1 681 sommets, 9 600 indices), avec 60 nœuds
sur la vue village et 18 sur la vue île. Aucune carte d'ombre préexistante
n'est chargée. La passe conserve Depth32Float, culling arrière et biais
constant 2 / pente 1,5 sur le device Bevy. L'oracle source est isolé par
`--terrain-village-shadows=ENV_EXPORT` ; les plantes ne projettent pas ici.

## Résultats

Sur les deux caméras, la couverture des six couches est identique.

| Vue / couche | Texels occupés | Profondeurs différentes | Écart maximal normalisé |
|---|---:|---:|---:|
| Village 0 | 1 260 338 | 0 | 0 |
| Village 1 | 2 470 405 | 800 | 5,9604645e-8 |
| Village 2 | 1 782 566 | 1 196 | 4,5299530e-6 |
| Île 0 | 0 | 0 | 0 |
| Île 1 | 0 | 0 | 0 |
| Île 2 | 1 036 972 | 0 | 0 |

L'image éloignée est exacte à l'octet. Les écarts proches sont mesurés, non
expliqués définitivement : ne pas les attribuer sans preuve au backend ou
les transformer en validation du filtre. Source Dawn/D3D12 et natif
Bevy/Vulkan utilisent le même GPU AMD mais des compilations différentes.
Les deux couches vides de la vue éloignée n'apportent pas une preuve de
profondeur occupée.

Build et Clippy strict réussis. Ancienne passe village sans terrain inchangée.
Un terrain au décalage de base modifié, dont le manifeste est rehashé, est
refusé sémantiquement avant GPU : ce prototype ne doit pas afficher une base
originale en prétendant appliquer des retouches non encore raccordées.

## Preuves locales

Racine `E:\OpenRPG_Forge\.tidewater-integration` :

- `source-terrain-village-depth-001.png` / `source-terrain-island-depth-001.png`,
  leurs `.shadows.json` et couches `.cascade-N.r32.bin`.
- `native-terrain-village-depth-001.bin`, SHA-256
  `1dbce19e8b15d745710d92c945e6689ae48270e4b988130c5be1fac34dc4867f`.
- `native-terrain-island-depth-001.bin`, SHA-256
  `c0c69616403299006b6000918e3dd85dfd1f4d8280cde693fd6e7a08f40dd379`.
- `terrain-village-depth-comparison-001.json`, `terrain-island-depth-comparison-001.json`.
- `terrain-disabled-village-regression.bin`, SHA-256
  `f9e1e99b41b1456ebe5679ed24afd3b1f79c4ffcf0cecda49ce23ba1482bdd4d`.
- `native-terrain-invalid.log` (code 1), `terrain-depth-edited-invalid-001`.
- `terrain-depth-clippy-002.log`, `terrain-depth-build-001.log`.
- Binaire SHA-256
  `fcf337ca29931e771344d9ff7898329d2bf0fde8a28055cec012277c58c4aea7`.

Les masques des plantes, le filtrage PCSS/PCF et la réception dans la scène
assemblée restent ouverts, ainsi que l'animation, Studio et la qualification
WebGPU visuelle. Aucun verdict de parité complète ni publication.
