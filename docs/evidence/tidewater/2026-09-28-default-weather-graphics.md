# Tidewater — météo du paquet et comparaison graphique appariée

Référence de cette qualification : `dgreenheck/tidewater@1438b1abfcaee3267092b75573014f4d9b4a983c`, capture sans perte `baseline-003/village.png` à 1920×1080. La comparaison porte sur la même pose de caméra et sur le prototype natif ; elle ne qualifie pas la parité de la scène complète.

## Cause du village trop sombre

Les bundles `preview-scene-bundle-001/002` contenaient les entrées de `cloud-overcast-source-001` (`cloud_coverage = 0,85`) à la place de l'acquisition normale `cloud-seaward-source-001`. Les paramètres publics de caméra étaient compatibles, ce qui laissait le paquet démarrer, mais la carte d'ombre calculée sur GPU obscurcissait presque toute l'île. Les données figées de l'ancien paquet sont conservées pour diagnostic.

À la pose du village, le readback natif donne une transmission centrale de `0,00857589` pour l'ancien nuage contre `1,0` pour la météo normale. La carte native normale et l'oracle GPU source diffèrent en moyenne de `2,67 × 10⁻⁷` par texel R32Float, écart maximum `0,000689`. Le calcul natif de la carte n'est donc pas la cause de cet assombrissement : c'était le choix des entrées du bundle. Les A/B isolant les facteurs du soleil montrent que l'ombre du relief et celle des objets ont peu d'effet sur cette différence, contrairement à celle des nuages.

`preview-scene-bundle-clear-004` reprend la scène 002, met à jour son empreinte d'acquisition et contient les entrées natives issues de la météo normale. Son manifeste vérifié contient 1 542 fichiers, 295 672 776 octets, SHA-256 `10ec1ce5404e90125b887dc2f9eb2c6719339d6d9ae67b484ba5803d0efaec4c`. Aucun résultat GPU pré-calculé de nuage n'est embarqué ; la carte est régénérée par le moteur. La construction Windows refuse maintenant une scène dont l'empreinte ne correspond pas à `clouds/environment.json`, ainsi qu'une couverture supérieure à 0,6 ; le rejet de l'ancien bundle couvert a été vérifié avant toute création de sortie.

## Réglages et image

Le probe statique peut désormais appliquer la même chaîne graphique que la promenade (HDR, ACES, adaptation GPU, bloom, netteté, vignette). Cette voie ne modifie pas le rendu normal sans `FORGE_TIDEWATER_STATIC_POSTFX`. La comparaison antérieure « HDR/ACES » n'activait pas l'adaptation et ne permettait donc pas de conclure sur le rendu complet.

Avec la météo normale, le profil automatique initial `+1,0 EV` surexpose le village. À `+0,4 EV`, la moyenne RGB des maisons est `(109,7 ; 111,9 ; 74,4)` contre `(110,5 ; 110,0 ; 76,9)` dans la référence ; le sol central est `(146,1 ; 136,6 ; 96,4)` contre `(146,0 ; 134,9 ; 96,1)`. Cette exposition est une calibration empirique du pipeline Bevy, pas une conversion directe du multiplicateur d'exposition de Three.js.

À cette exposition, la netteté CAS native de `0,45` accentuait davantage les détails que le RCAS source. La valeur `0,20` réduit l'erreur moyenne sur l'image appariée. FXAA, activé par défaut et désactivé automatiquement quand le TAA expérimental est demandé, améliore encore la comparaison : erreur absolue moyenne RGB `17,6/16,5/15,1` sans FXAA contre `13,1/12,2/12,7` avec FXAA. Les captures A/B diffèrent sur 1 733 920 pixels. Le menu `H` / `F10` expose FXAA ; `R` remet les valeurs par défaut et les sauvegarde.

Les captures restent visibles sous `.tidewater-integration/graphics-village-fxaa-on-001.png`, `...-off-001.png`, et le cas météo initial sous `graphics-overcast-cloud-readback-001.png`. Le parcours `--walk-smoke` sur le nouveau bundle a terminé avec `pass=true`, carte et nuages actifs ; `clouds.submitted=320`, étape `440`, historique réinitialisé une fois. La comparaison visuelle directe montre encore des différences de végétation, de surfaces, de lissage temporel et de composition. Les effets AO écran, flare, grain et la parité TAAU/WebGPU restent ouverts.

Le candidat Windows `OpenRPG-Forge-Aube-Tidewater-Win64-20260928-013.zip` (422 245 185 octets, SHA-256 `853b5ca3155684447b682147ceedde86c8d84b1dd5bc15c19f9c4c81248c20ff`) contient le lanceur Aube vérifié, la nouvelle scène et le runtime avec FXAA. Les 2 508 entrées du ZIP passent le contrôle CRC. Depuis le dossier empaqueté, `--walk-smoke` retourne `pass=true`, `backend_stable=true`, `cadence_pass=true` et 314 étapes nuageuses soumises ; aucune erreur GPU n'apparaît dans le journal. La notice mentionne FXAA et la remise à zéro des paramètres. La qualification sur une autre machine et la revue des notices de licences restent ouvertes.
