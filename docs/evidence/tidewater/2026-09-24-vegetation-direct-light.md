# Tidewater — lumière directe de la végétation

Qualification diagnostique locale, source épinglée
`1438b1abfcaee3267092b75573014f4d9b4a983c`. Ce résultat ne clôture pas TW-006.

L'adaptateur provisoire Bevy ignorait `specularIntensity` et `translucency`
calculés par les matériaux source. `prepare-direct-lighting.py` reprend les
quatre fonctions BRDF source et leur somme directe, y compris Fresnel F90 et
transmission. La normale finale est normalisée comme dans MeshShader source.
Le mode natif `--vegetation-direct` isole cette voie, sans terrain ni village.

L'oracle `render-source-vegetation.mjs --direct` utilise le shadeSurface original
avec un soleil blanc unitaire, direction normalisée (0.3, 0.6, -0.7), environnement
nul et aucun végétal projetant d'ombre. Il ne réutilise pas l'adaptateur natif.
La cuisson originale des atlas précède la neutralisation de l'environnement.
Captures 1920×1080, instant 2 s, caméra fixe ; sortie sRGB 8 bits.

| Mesure sur l'union des pixels non noirs | Village | Forêt |
| --- | ---: | ---: |
| Pixels de l'union | 575353 | 1031630 |
| RGB exactement identiques | 570147 | 840746 |
| Erreur moyenne par canal /255 | 0.015624 | 0.339026 |
| P99 du maximum par canal /255 | 0 | 21 |
| Maximum /255 | 65 | 95 |
| Désaccord non-noir/noir | 177 | 5358 |

Le masque non noir dépend ici de la couleur : ces désaccords ne mesurent pas
uniquement la couverture géométrique. Les deux paires ont été inspectées.
La forêt conserve des écarts ; aucune identité pixel à pixel n'est revendiquée.

Artefacts sous `E:\OpenRPG_Forge\.tidewater-integration` :

- Fixtures `direct-lighting-002`, `direct-lighting-canopy-001`.
- Village : `source-vegetation-direct-001.png`, `native-vegetation-direct-002.png`,
  `vegetation-direct-comparison-002.json`.
- Forêt : `source-canopy-direct-001.png`, `native-canopy-direct-001.png`,
  `canopy-direct-comparison-001.json`.
- Construction et Clippy strict réussis : `direct-lighting-build.log`,
  `direct-lighting-clippy.log`.
- Binaire SHA-256 :
  `2237bc2495aadeca4d6f1479cc0aac78820242c0ef0fc83ecb5c9d14b932c75a`.
- Régression silhouette : `direct-silhouette-regression-001.png`, identique à
  `native-vegetation-mask-002.png`, SHA-256
  `41eb983364664237003d05cb9f51d8e87c8406a531bbd3487add761f68ea9069`.
- Fixture sans programme source : refus code 1, `invalid-direct-001.log`.

Restent à intégrer : lumière réelle du soleil, environnement diffus/spéculaire,
ombres, rebonds, exposition et traitements temporels. Le soleil unitaire est
un contrôle isolé, pas l'éclairage final de la carte. Terrain/village utilisent
encore leur éclairage provisoire. Aucune qualification Studio, navigateur,
performance ou publication n'est apportée par cet incrément.
