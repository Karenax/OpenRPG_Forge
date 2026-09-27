# Tidewater — éclairage commun terrain, village et végétation

Source intacte `1438b1abfcaee3267092b75573014f4d9b4a983c`.
Le prototype natif applique maintenant le même soleil et le même environnement
acquis aux trois familles de matériaux. L'environnement est une ressource GPU
partagée ; les systèmes d'initialisation ordonnent sa création avant celle des
matériaux. Les anciennes fixtures gardent leur programme existant.

`prepare-scene-lighting.py` étend les fonctions directes, SH, DFG et
multiscattering déjà contrôlées à la surface terrain et aux sept matériaux du
village. Intensité d'environnement, occlusion matériau, émissif et alpha sont
conservés ; intensité spéculaire par défaut 1, comme la surface originale.
Le mode `--scene-lighting` refuse les fixtures sans les trois groupes et leur
programme d'éclairage vérifié. MSAA désactivé, sortie sRGB, aucun post-traitement
de fidélité finale revendiqué.

L'oracle `render-source-vegetation.mjs --scene=ENV_EXPORT` assemble les objets
Terrain, Village et Vegetation originaux et le shadeSurface original avec les
mêmes données de ciel. Il désactive les ombres de scène et la modulation
d'ombre propre au terrain pour isoler l'éclairage actuellement raccordé.
Les poissons décoratifs du village sont exclus des deux côtés. Cette
configuration diagnostique diffère de l'App complète.

| Mesure (union des pixels non noirs) | Village | Île |
| --- | ---: | ---: |
| Pixels de l'union | 2073584 | 1139232 |
| RGB identiques | 1258058 | 890607 |
| Erreur moyenne par canal /255 | 0.464934 | 0.166189 |
| P99 du maximum par canal /255 | 8 | 3 |
| Maximum /255 | 182 | 135 |
| Désaccord noir/non-noir | 0 | 0 |

Les quatre images ont été inspectées. Les écarts résiduels de couleur restent
présents ; aucune identité pixel à pixel ou fidélité à la baseline App n'est
déclarée. Le fond marin visible sur la vue île est le terrain sans océan.

## Preuves

Sous `E:\OpenRPG_Forge\.tidewater-integration` :

- Fixtures : `scene-lighting-001`, `scene-lighting-island-001`.
- Village : `source-scene-lighting-001.png`, `native-scene-lighting-001.png`,
  `scene-lighting-comparison-001.json`. SHA-256 natif :
  `42a003b53791264ed4d01f50db0e7e16ffa57f210f4942095b44260128fc621c`.
- Île : `source-scene-island-001.png`, `native-scene-island-001.png`,
  `scene-island-comparison-001.json`. SHA-256 natif :
  `0f6a2bf62207a4b37090a6f7cb15a5a8bd222b28d006e555134e60c9d73f24aa`.
- Clippy strict et build réussis : `scene-lighting-clippy.log`,
  `scene-lighting-build.log`. Binaire :
  `67b60f48f74af5d93b8a73ec5d620c3ca56ef5f1e1a862842c504f65487a7f5d`.
- Albédo village inchangé : `scene-village-albedo-regression.png`,
  `fcca3024af8de8d14ebeb0f62f5a04485fc4e8b577335de67b4928a8ec9b58b2`.
- Végétation éclairée seule inchangée : `scene-vegetation-regression.png`,
  `a50afea5754b6940f76cbcbdd17ee90114b07deb58b76819640e0d5e593b1129`.
- Fixture sans programme commun : code 1 avant GPU, `scene-incomplete.log`.

## Suite requise

Ombres du relief et des objets, modulation des nuages, rebonds, lumières
locales, ciel visible/dynamique, eau, herbe, décors complémentaires et
post-traitements restent ouverts. Les caméras et l'environnement sont figés.
Ni édition Studio, ni qualification navigateur/performance, ni packaging
ou publication ne sont apportés par cet incrément. TW-006/009/010 restent ouverts.
