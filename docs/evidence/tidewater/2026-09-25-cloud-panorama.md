# Panorama natif des nuages

Source intacte `1438b1abfcaee3267092b75573014f4d9b4a983c`, acquisition
`cloud-source-005`. Artefacts sous `E:/OpenRPG_Forge/.tidewater-integration/`.

Le mode `--history` calcule désormais le panorama original 512×160 dans une
texture RGBA16 initialisée à zéro. Le premier dispatch couvre toute la texture ;
les 119 suivants actualisent chacun un sous-ensemble de la grille 4×4, avec
les indices originaux de chaque image. L'alpha contient la transmission,
contrairement à la texture de vue qui stocke l'opacité.

Le programme original `Clouds Panorama` partage bruit volumique, météo/bornes
natives, paramètres de frame et éclairage avec le marching de vue. Le pipeline
est créé une fois. Les LUTs atmosphériques et les paramètres temporels sont
encore acquis de la source ; aucun panorama ni historique de nuages source
n'est chargé. La réception par le ciel, le cube d'environnement et l'eau est
une étape distincte, encore ouverte.

`native-cloud-panorama-001/002/003` : répétition exacte. Le manifeste 003 précise
correctement les limites après ajout de cette passe. Binaire final :
`ae33af5da5b88623027c452c152e74834cacd1144a614cf5f3a93214981aeba0`.

Rapport `cloud-panorama-comparison-002.json` :

- 81 920 texels, valeurs finies, transmission dans [0,1], zones claires et nuageuses présentes.
- 12 504 texels différents de Tidewater.
- Erreur moyenne RGB : 1,14e-5 à 2,95e-5 ; maximum RGB : 0,017578125.
- Erreur moyenne de transmission : 3,7831e-7 ; maximum : 0,00439453125.
- Percentile 99 RGB : 0,00048828125 par canal.
- SHA-256 : `f3958167390a12f2cda2a7d7843ca784f04af7a9c39f9fee6da06bbc06a7975c`.

`cloud-panorama-history-regression-001.json` confirme que les quatre sorties
d'historique restent identiques à `native-cloud-history-004`. Compilation et
Clippy ciblés `-D warnings` passent.

Les six passes de calcul originales sont désormais exécutées dans ce probe
Bevy : météo, bornes, ombres, marching de vue, reconstruction et panorama.
Cela ne vaut pas intégration produit : atmosphère native, contrôleur interactif,
caméra mobile, consommateurs de scène, diagnostic des écarts et performance
restent à qualifier. Aucune parité finale de carte, Studio ou navigateur ;
aucune publication.
