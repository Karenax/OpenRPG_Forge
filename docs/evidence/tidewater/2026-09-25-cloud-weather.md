# Couverture nuageuse native et acquisition SkyProClouds

Source intacte `1438b1abfcaee3267092b75573014f4d9b4a983c`.
Artefacts dans `E:/OpenRPG_Forge/.tidewater-integration/`.

`App.js` active `SkyProClouds` par défaut ; `Clouds` n'est utilisé qu'avec
`oldClouds`. L'acquisition étend le rejeu d'environnement existant, sans changer
ses paramètres ni sa caméra village. Elle utilise le même appareil WebGPU source.

`cloud-source-003/004` : 560 fichiers identiques, hashes recalculés. Les 37
sorties SH/cubemap correspondent exactement à `environment-village-003`.
Rapport `cloud-source-verification-001.json` : 120 étapes, alternance des deux
historiques, horloge et invalidation initiale vérifiées, dimensions et valeurs
finies contrôlées. Les six programmes complets sont weather, bounds, trace,
temporal, panorama et shadow. Dix textures sont acquises aux pas 1 et 120.
Les sept mipmaps 3D du bruit 64³ et le bruit bleu 64² sont conservés.

La capture conserve le miroir `_last` des uniformes réellement envoyés au GPU,
et refuse un écart avec les valeurs CPU après update. Chaque pas possède sa
copie indépendante. COPY_SRC est ajouté uniquement aux dix textures de nuages
avant leur allocation ; aucune fonction de calcul source n'est remplacée.
Les essais 001/002 précèdent cette assertion de miroir ; 003/004 font foi.

Les notices LICENSE, CREDITS.md et public/clouds/LICENSING.md sont conservées.
CREDITS attribue le bruit et la technique de nuages à DRG sous MIT ; la notice
générique des textures est conservée sans attribuer une licence CC0 aux bruits.

## Calcul natif

`tidewater_cloud_weather_probe` exécute les deux premiers programmes originaux
sur l'appareil Bevy/wgpu. Il lit uniquement les deux WGSL et les paramètres de
réglage ; aucune carte météo ni aucun résultat source n'est téléversé.
La carte 1024² produite alimente directement le calcul des bornes 64².
Les paramètres et shaders sont vérifiés par hash avant démarrage de Bevy.

`native-cloud-weather-001/002/003` : résultats identiques entre exécutions.
Le binaire final 003, après correction Clippy, est
`7e981c862182dcb2d6297f73d09ead2b8ae64690ba9b18ea372424a7b84d3506`.
`cloud-weather-comparison-002.json` mesure :

| Sortie | Texels différents | Erreur maximale sur 255 | SHA-256 natif |
|---|---:|---:|---|
| Couverture 1024² | 5 | 1 | `a14b5ca5d3a8a6a29b3625536156b8481047ead345f381217be0646ba1458a63` |
| Bornes 64² | 0 | 0 | `27d314df41816a4624e84cf919a96b7fd0b06fd73fad518d633f5784a461966a` |

Le comparateur reconstruit indépendamment les 4 096 maxima conservatifs,
y compris les voisins bilinéaires et les raccords répétés de la texture.
Compilation et Clippy ciblés `-D warnings` passent. Aucun changement des
modules produit existants, aucune publication.

## Portée restante

Ce probe n'affiche pas encore de nuages dans la carte native. Marching volumique,
reprojection temporelle, panorama, ombres, couplage atmosphère/reflets et mouvement
de caméra restent à porter. Les cinq différences empêchent d'affirmer une
identité binaire de la couverture. Aucun verdict de parité finale, performance,
WebGPU navigateur ou Studio n'est acquis.
