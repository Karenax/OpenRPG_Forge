# Tidewater — évolution visuelle de la source publique

L'intégration et les oracles locaux utilisent
`dgreenheck/tidewater@1438b1abfcaee3267092b75573014f4d9b4a983c`.
Le `main` public vérifié le 29 septembre 2026 est
`4811ba48d795197de5621985f404e765c0b7c0ef`, également présent dans
`E:\OpenRPG_Forge\.tidewater-integration\live-source-20260928`.
Une comparaison du code entre ces deux révisions montre des changements de
rendu qui interdisent d'assimiler les mesures du paquet Forge 021 à la démo
publique actuelle :

| Sous-système | Changement dans le `main` récent | Conséquence pour Forge |
|---|---|---|
| Anticrénelage | `PostFX.aaMode` reste `taa` par défaut ; `TemporalUpscale.js` ajoute une reconstruction et une accumulation inspirées de FSR2, avec verrous pour détails fins, historique de luminance, adaptation au mouvement et au masque d'eau. L'interface règle désormais 0/2/4/8/16 positions sous-pixel, 4 par défaut. `AntiAlias.js` ajoute aussi FXAA/SMAA, sans les choisir par défaut. | Le TAA Bevy exposé dans Forge reste expérimental et désactivé par défaut ; ses réglages et son résultat ne reproduisent pas ce TAAU. |
| Ciel et nuages | `Sky.js` compose le disque solaire séparément et le module par la transmission nuageuse ; le module nuageux principal reste `SkyProClouds`. | Refaire une vue appariée avec ciel avant d'attribuer l'écart aux seuls nuages. |
| Brume | `AirHaze.js` ajoute un historique temporel aux rayons et corrige la remontée de résolution pour réduire scintillement et points parasites ; les valeurs initiales de densité/brume restent proches. | La brume Forge réglable ne possède pas encore ce traitement temporel. |
| Eau et rive | `WaterMaterial.js`, `RefractionPass.js` et `ShoreWaves.js` changent la profondeur utilisée pour les reflets/réfractions et font varier film, écume et ménisque entre montée et retrait du ressac. | La parité de la ligne d'eau et des reflets doit être réévaluée sur la nouvelle source. |
| Ombres de contact | L'installation de `ContactShadows.js` est retirée du rendu courant. | Comparer les ombres sur images appariées avant toute adaptation des ombres Forge. |

Les changements visibles de `Terrain.js` et `VegMaterials.js` sont surtout des
évaluations conditionnelles et du cache ; aucune nouvelle géométrie ou valeur
initiale de végétation n'est établie par cette lecture. Bloom, saturation,
contraste, grain et netteté gardent leurs valeurs initiales dans `PostFX.js`.

La [paire village HDR/ACES](2026-09-29-village-postfx-pair.md) démontre une
luminosité globale rapprochée **pour la révision figée**. Une paire côte/ciel
directement comparable n'a pas été produite : `baseline-003/coast.png` a une
caméra `(53,6 ; 4,4477 ; -77)` tandis que le fixture seaward clear004 est à
`(42 ; 14 ; -90)`, et le banc WebGPU disponible ne rend pas la scène complète
avec ces effets. Il faut une nouvelle acquisition à caméra, heure, météo et
chaîne temporelle appariées pour juger la fidélité à la version récente.
