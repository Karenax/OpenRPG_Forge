# Nuages : reflets dans l'océan et les déferlantes

Le mode natif `--water-spray-clouds OCEAN SHORE SPRAY CLOUD` partage le
panorama calculé par les six passes Bevy avec le ciel, le matériau océan et
la lèvre des déferlantes. FFT, rivage et embruns restent calculés nativement,
avec le contrôleur de budget natif. Les fonctions de composition et
d'échantillonnage viennent de Tidewater épinglé à `1438b1ab`.

## Entrées et reproduction

Sous `E:/OpenRPG_Forge/.tidewater-integration/` :

- `cloud-seaward-reference-001.json` : caméra [42,14,-90], FOV 62°,
  vent global 6, direction [1,0], repris du Frame de l'eau.
- `cloud-seaward-source-001/002` : 694 fichiers identiques ; les 37 résultats
  d'environnement correspondent à `cloud-seaward-environment-001`.
- `cloud-water-seaward-001` : fixture préparée par `prepare-cloud-sky.py`
  puis `prepare-cloud-water.py`, vérification des hashes, pose et vent.
- Source `source-cloud-water-001/002.png` : renderer original, historique
  et panorama acquis, `--water-spray`, ombres `environment-village-003`.
- Natif `native-cloud-water-001/002.png` : même fixture, calcul atmosphère
  et nuages dans Bevy, entrées océan/rivage/embruns sans résultats source.

Les captures source et native ont été inspectées. Chacune de leurs répétitions
est strictement identique. Binaire scène SHA-256 :
`2f696962a794e4b3af3ed3f09fa9c94a11d7c50754ae79ae2338dda5525f277b`.

## Mesures et régressions

`cloud-water-scene-comparison-001.json` : région d'effet 305 995 pixels,
erreur moyenne par canal 0,167551/255, P99 du maximum par pixel 3/255,
maximum 111/255. Moyenne scène 0,197174/255. La bande écran [0,360,1920,620]
donne 0,279444/255, P99 6/255 et maximum 117/255 ; elle inclut ponton et
rochers, ce n'est pas un masque d'eau. Ces mesures incluent les écarts
préexistants. Le maximum global 127/255 en (777,997) existait sans nuages.

Capture native SHA-256 :
`6e082564df3380e8887e9ad4e0c35bc0cab99ec0b6a1e41805aa644e33bfa52e`.
Source : `05ffe4a15f4df0438ef1dd3d7ab20c57f21e563011ec09697b5d32cc5c8d3b15`.

`cloud-water-state-regression-001.json` : les 21 buffers FFT sont inchangés,
ainsi que crêtes, compteurs, dépôts et état du rivage. Les buffers de particules
diffèrent par ordre atomique des emplacements ; leurs 7 014 particules actives
sont exactement identiques après appariement par identité avec le candidat
sans nuages (`cloud-water-particle-regression-001.json`). La répétition native
est également identique par identité. Le comparateur complet d'historique a
validé cet appariement avant de refuser son invocation sans les deux arguments
de bases ShoreSim ; aucun rapport complet de ce comparateur n'est revendiqué.

`cloud-water-budget-verification-001.json` vérifie 119 retours causaux, les
120 budgets reconstruits indépendamment et les 14 843 émissions natives.
`native-cloud-water-disabled-001.png` est strictement identique au candidat
sans nuages `native-spray-autonomous-001.png`. Clippy strict et compilation
de l'exemple passent.

## Limites

Pose fixe à 2 secondes, paramètres temporels/soleil/irradiance encore acquis.
Environnement d'éclairage de scène conservé du candidat précédent ; réception
des ombres nuageuses désactivée. Le calcul partagé et les reflets sont raccordés,
mais environnement animé, ombres reçues, séquences longues, performance,
WebGPU navigateur et édition Studio restent ouverts. Pas de parité globale,
de clôture de gate ou de publication Git.
