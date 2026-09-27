# Calcul natif des ombres de nuages

Source intacte `1438b1abfcaee3267092b75573014f4d9b4a983c`.
Artefacts : `E:/OpenRPG_Forge/.tidewater-integration/`.

L'acquisition `cloud-source-005/006` complète les données précédentes avec
les LUTs transmittance/sky-view, les paramètres atmosphériques des 120 étapes
et neuf modules de fonctions, dont les fonctions publiques d'échantillonnage
des nuages et de réception de leurs ombres. Les 694 fichiers sont identiques
entre acquisitions ; les 37 sorties d'environnement restent identiques à
`environment-village-003`. Rapport `cloud-source-verification-002.json`.

`tidewater_cloud_weather_probe ... --shadow` charge le bruit original 64³
et ses sept mipmaps sur l'appareil Bevy. La couverture nuageuse est calculée
par les deux premières passes natives. Le programme original d'ombres lit
cette couverture, échantillonne la densité sur 16 points et intègre la
transmission. La carte 256² est initialisée entièrement au premier pas, puis
un quart des lignes est actualisé à chaque pas suivant, comme dans la source.
Chaque pas possède ses propres buffers immuables pour éviter que les 120
dispatches ne lisent tous les dernières valeurs.

Les uniformes de caméra, soleil et vent sont rejoués depuis l'acquisition.
Aucune carte d'ombres source ni résultat de couverture n'est téléversé.
Les LUTs atmosphériques ne sont pas utilisées par cette passe d'ombres ; elles
préparent l'acquisition appariée du rendu volumétrique restant à porter.

`native-cloud-shadow-001/002` produisent des résultats identiques.
Rapport `cloud-shadow-comparison-001.json` :

- 65 536 valeurs finies, bornées entre 0 et 1, avec ombres effectivement présentes.
- 26 419 valeurs différentes de la source en f32.
- Erreur absolue moyenne : `2.7402379032537283e-7`.
- Erreur absolue au percentile 99 : `3.0994415283203125e-6`.
- Erreur maximale : `0.0010405182838439941`.
- SHA-256 : `429f76514d13228fdb965040a054a5b700a14219952fdb8d81860e02f7b7aebb`.

`cloud-shadow-weather-regression-001.json` confirme les résultats inchangés
de couverture/bornes et les 4 096 contrôles indépendants de conservativité.
Compilation et Clippy ciblés `-D warnings` passent.
Binaire final utilisé pour 002 :
`ebf60e226e4e580e9514c2bedb46fe9f5f3d4a27c9b03c394ad41bc24f5328c1`.

Cette preuve concerne le calcul de la texture d'ombres. La réception par terrain,
village, végétation, eau et embruns reste ouverte, ainsi que le contrôleur natif
de nuages animé, le volume visible, la reprojection et le panorama. Aucune
parité finale de carte, performance interactive ou qualification Studio n'est
déclarée. Travail existant conservé ; aucune publication.
