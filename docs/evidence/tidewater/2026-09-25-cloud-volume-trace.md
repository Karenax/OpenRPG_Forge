# Première passe volumétrique des nuages dans Bevy

Source `1438b1abfcaee3267092b75573014f4d9b4a983c`, acquisition
`cloud-source-005`, artefacts sous `E:/OpenRPG_Forge/.tidewater-integration/`.

Le mode `tidewater_cloud_weather_probe CLOUD_SOURCE NEW_OUTPUT --trace`
exécute les passes météo/bornes/ombres existantes, puis le shader original
`Clouds Trace` à l'image 120. Il partage le bruit 3D et le sampler répété avec
les ombres. Les sorties météo et bornes sont directement utilisées sur le GPU.
Les textures source de radiance et de profondeur ne sont jamais chargées.

Les LUTs transmittance/sky-view et leurs paramètres proviennent explicitement
de la source pour cette comparaison isolée. Le manifeste distingue
`source_results_uploaded: true` (atmosphère),
`source_atmosphere_luts_uploaded: true` et
`source_cloud_results_uploaded: false`. L'étape ultérieure devra remplacer
ces entrées par les ressources du graphe atmosphérique Bevy existant.

Le shader calcule lumière diffusée, opacité, profondeur en kilomètres et coût
normalisé à 240×135, pour l'acquisition FHD 1920×1080. Les dimensions d'uniformes,
le viewport FHD, les hashes, la constance des réglages et la finitude des
paramètres atmosphériques sont contrôlés. Les readbacks enlèvent le padding
de 128 octets par ligne, de 2048 à 1920 octets utiles.

## Résultats

`native-cloud-trace-003/004` : sorties identiques, toutes finies, opacité dans
[0,1], nuages effectivement présents. Binaire final :
`82bcec7a6ba781048dfb97b0a29c2d3e108110678a99d1155ba8f39ec81db6c0`.
Les premiers essais 001/002 ont les mêmes résultats numériques mais précèdent
la clarification des champs de provenance du manifeste.

Rapport `cloud-trace-comparison-002.json` :

| Donnée | Valeur |
|---|---|
| Texels de radiance/opacité différents | 3436 / 32400 |
| Erreur moyenne RGB | 1,19e-5 à 1,65e-5 |
| Erreur maximale RGB | 0,01416015625 |
| Erreur maximale opacité | 0,021484375 |
| Texels de métadonnées différents | 10 / 32400 |
| Erreur maximale profondeur | 0,125 km, soit 125 m |
| Écart maximal coût normalisé | 0,3515625 |

Radiance SHA-256 :
`843b8ec5ba141d35c32d8a6c5649d910571a2b957364c2ca943d4ac05340fdf5`.
Métadonnées SHA-256 :
`5570faaf2f6b3966590f19e1a9c95e949d285199479d7c31ef04b82e81a8f3c6`.

`cloud-trace-shadow-regression-001.json` et
`cloud-trace-weather-regression-001.json` confirment les sorties précédentes
inchangées. Compilation et Clippy ciblés `-D warnings` passent.

## Limites

La moyenne faible ne résout pas les écarts locaux de profondeur et d'opacité.
Il reste à les diagnostiquer et à qualifier la reconstruction temporelle,
le panorama, l'atmosphère native, la caméra mobile et la composition avec
terrain/eau/objets. Ce résultat ne prouve pas une carte visuellement identique
ni une simulation autonome. Aucune qualification Studio, navigateur ou
performance ; aucune publication.
