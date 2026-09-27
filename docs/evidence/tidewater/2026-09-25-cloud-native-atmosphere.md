# Nuages alimentés par l'atmosphère calculée dans Bevy

Source `1438b1abfcaee3267092b75573014f4d9b4a983c`. Artefacts sous
`E:/OpenRPG_Forge/.tidewater-integration/`.

Le mode `--native-atmosphere ATMOSPHERE_FIXTURE` installe le module atmosphérique
Bevy existant dans le même RenderApp. Le calcul des nuages attend ses ressources
et son encodage, puis consomme directement les textures transmittance/sky-view.
Aucun second appareil GPU, readback intermédiaire ou rechargement de LUT depuis
un résultat source n'est utilisé. Les paramètres atmosphériques calculés par
le module natif doivent correspondre exactement aux paramètres d'acquisition
des nuages. La correction de bord atmosphérique déjà qualifiée est conservée.

La donnée Frame des nuages reste un rejeu : caméra, temps, vent, couleur solaire
et irradiance y sont encore acquis de Tidewater. La suppression des LUTs source
ne signifie donc pas encore un contrôleur d'environnement entièrement autonome.

## Entrées sans résultats et vérifications

`prepare-native-cloud-inputs.py` produit :

- `cloud-native-inputs-001` : 495 fichiers, shaders, bruit original et uniformes.
- `cloud-atmosphere-native-inputs-001` : six fichiers, quatre shaders, paramètres
  et manifeste original. Aucune LUT ni image calculée.

Les manifests originaux sont conservés pour leur hash de provenance ; les
résultats qu'ils indexent ne sont pas matérialisés dans ces packs.
`native-cloud-atmosphere-002/003` terminent les 120 étapes avec ces seules
entrées. Les sorties se répètent exactement. Le manifeste indique
`native_atmosphere: true`, `source_atmosphere_luts_uploaded: false` et
`source_results_uploaded: false`.

Rapports `cloud-native-atmosphere-panorama-001.json` et
`cloud-native-atmosphere-history-001.json` :

| Sortie | Erreur moyenne RGB | Erreur maximale RGB | Erreur alpha maximale |
|---|---|---:|---:|
| Panorama | 1,97e-5 à 2,73e-5 | 0,017578125 | 0,00439453125 |
| Historique final | 0,0001158 à 0,0001587 | 0,0419921875 | 0,0517578125 |

Le panorama contient 12 685 texels différents de la source ; l'historique final,
93 877. Les métadonnées de profondeur conservent les écarts précédemment mesurés,
dont 0,875 km au maximum sur la profondeur historique. Ce ne sont pas des
critères de parité satisfaits.

SHA-256 panorama natif :
`5adc9863fa543895b69ab1967b0006f42c5449255f63e6e70910eb663c9f222e`.
SHA-256 historique final couleur :
`155110c64d9db89cfbc2f898ac12a5935994b17613d98382db680f2ba0255a36`.
Binaire final :
`06912b79940e22f5e6f634012842cf252c38375c03a313d4824c1046a00a9a18`.

L'ancien mode à LUTs source conserve son panorama exactement
(`cloud-native-atmosphere-paired-regression-001.json`). Ce mode rejette le pack
réduit sans LUTs avec code 1 (`cloud-missing-lut-rejected-001.log`), confirmant
la distinction des chemins. Compilation et Clippy ciblés `-D warnings` passent.

## Limites

Restent : paramètres natifs animés, lumière solaire/irradiance du Frame natives,
composition ciel/reflets et réception des ombres dans la scène, diagnostic des
écarts et qualification caméra mobile/performance/navigateur. Aucun verdict
de parité finale, Studio ou distribution ; aucune publication.
