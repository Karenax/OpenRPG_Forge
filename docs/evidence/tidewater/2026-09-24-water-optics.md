# Matériau optique d'eau dans Bevy

La surface native exécute maintenant les fonctions originales de WaterMaterial
et WaterSurface : normales FFT et rides proches, Fresnel diélectrique, reflet
du ciel et réflexion à l'écran, spéculaire solaire, réfraction vers le relief,
absorption/diffusion et mélange de l'écume. Elle utilise les textures FFT,
les LUTs atmosphériques, les ombres et les copies de scène du device Bevy.
Le terrain et les normales proviennent de la scène après aplanissement du village.

La texture d'écume statique et ses onze mips sont acquis de Tidewater ; ils
ne sont pas recalculés dans ce prototype natif. Les paramètres de matériau,
de ciel et les marques de baleine inactives sont les valeurs de construction
exportées. Les constantes Frame proviennent de la référence appariée, y compris
l'irradiance ambiante par défaut ; les matrices de caméra et sa position sont
remplacées par les valeurs Bevy. Ce n'est pas encore une mise à jour interactive
des paramètres météo, de l'environnement ou de la caméra.

## Adaptation

- `prepare-water-optics.py` conserve les fonctions WGSL originales, raccorde
  les bindings natifs et retire seulement les branches explicitement désactivées
  WATER_HULL et WATER_REFRACTION (passe séparée). La réfraction avec la copie
  opaque reste exécutée, comme dans cette configuration source.
- Le champ `Frame.view` est renommé `sourceView`, sans changer son layout,
  pour éviter une collision de noms avec l'import `view` de naga_oil.
- `water_optics.rs` remplace le matériau de contrôle sur le maillage vérifié.
  Le spectre expose ses 224 octets de paramètres natifs sans modifier son calcul.
  Les 720 octets Frame conservent leur représentation binaire, y compris les u32.
- Les samplers anisotropes utilisent 8× comme la source. L'eau reste double face,
  écrit sa profondeur et se dessine dans la passe transmissive.

## Captures appariées

Artifacts sous `E:/OpenRPG_Forge/.tidewater-integration/`.

| Vue | Écart RGB moyen image /255 | Écart moyen intérieur eau /255 | P99 intérieur | Maximum intérieur |
|---|---:|---:|---:|---:|
| Village vers la mer | 0,1767794 | 0,2348889 | 5 | 112 |
| Île | 0,1474971 | 0,1609472 | 2 | 43 |

Sources : `source-water-optics-001.png` et `source-water-optics-island-001.png`.
Natifs inspectés : `native-water-optics-002.png` et
`native-water-optics-island-001.png`. Fixtures finales
`water-optics-seaward-002` et `water-optics-island-001`.
Rapports : `water-optics-seaward-comparison-001.json` et
`water-optics-island-comparison-001.json`. Les différences maximales sur
l'image entière sont respectivement 127 et 135 : la parité exacte n'est
pas acquise. Les intérieurs sont les masques géométriques source érodés
d'un pixel (430 517 et 1 414 391 pixels), pas des identifiants d'objet.
La référence et le natif travaillent ici avec une copie opaque 8 bits sRGB,
sans la chaîne HDR/temporelle finale de l'application.

SHA-256 des PNG natifs :

- Mer : `adc8b7e2d7ef1c902a3faeed304116b322c909b1e226194b79cc000198e61f0f`.
- Île : `adbdfc310c4b5bbe49d438b4543b8181170ffb12cd89d54fbeec40ff3fa9b886`.

## Vérifications

- Répétition de la vue mer : PNG identique, zéro pixel différent.
- Régression du matériau géométrique : PNG identique au précédent.
- Les 21 sorties FFT de chaque capture et de la régression correspondent
  bit à bit à `native-ocean-default-fft-frame120-003`.
- Une fixture optique lancée en mode géométrique est refusée avant rendu,
  code 1, aucune image créée (`water-optics-invalid-mode-001.log`).
- Compilation `water-optics-build-001.log` et Clippy des deux exemples
  `-D warnings`, `water-optics-clippy-001.log`, réussis.
- Rapport consolidé `water-optics-regression-001.json`.
- Binaire scène :
  `e280bfe95c56a1ef498110d3c3aef57a64ea5c51658d5406c6f3440652a8e5b6`.

L'essai natif 001 a révélé la collision d'identifiant Frame/import ; il est
exclu des preuves. Les captures finales terminent avec code 0.

## Travail restant

Cette comparaison porte sur deux vues diurnes fixes après 120 pas du spectre
par défaut. Le chemin d'écume est exécuté, mais la mer calme ne qualifie pas
son apparence en tempête. Les vagues de côte, le ressac, la simulation et l'écume
de plage, les sillages, la passe de réfraction des seuls objets immergés,
la réflexion planaire, les nuages, les coques et l'immersion restent ouverts.
Le code sous-marin du shader est présent mais non exercé par ces caméras.
La boucle interactive, le HDR/temporel, le produit/Studio et les qualifications
WebGPU et performances restent également ouverts. Aucun critère TW complet
n'est clôturé et aucune publication n'est effectuée.
