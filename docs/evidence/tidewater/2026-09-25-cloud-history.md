# Historique temporel des nuages calculé dans Bevy

Source intacte `1438b1abfcaee3267092b75573014f4d9b4a983c`, acquisition
`cloud-source-005`. Artefacts sous `E:/OpenRPG_Forge/.tidewater-integration/`.

Le mode `--history` du probe météo exécute 120 fois le marching original puis
la reconstruction temporelle originale, avec deux paires couleur/métadonnées
960×540 initialisées à zéro. Aucun historique source n'est chargé. Chaque
nouvelle passe lit l'historique natif précédent. La paire 0 contient l'image
119 et la paire 1 l'image 120. Bruit, météo, pipelines et tables atmosphériques
sont partagés entre les pas ; les buffers d'uniformes de chaque pas restent
distincts. La dernière sortie de marching conserve les résultats déjà mesurés.

Les paramètres des 120 pas viennent encore de l'acquisition. Les LUTs
atmosphériques restent celles de Tidewater ; la constance des réglages et des
paramètres atmosphériques sur tous les pas est vérifiée, ainsi que l'identité
des LUTs au début et à la fin de l'acquisition. Ceci prépare le raccordement au
graphe atmosphérique natif mais ne le remplace pas.

## Preuves

`native-cloud-history-001` est un essai rejeté : le lot GPU plus long a révélé
plusieurs événements readback déjà en vol pour une même entité. Le premier
écrivait le fichier, les suivants tentaient de l'écrire à nouveau. Le probe
enregistre maintenant les sorties déjà reçues et ignore leurs duplications.

`native-cloud-history-002/003/004` terminent correctement avec sorties identiques.
004 ajoute les contrôles de constance et utilise le binaire final
`0b0a66c9429a9cc5b393033ea0056936d4679d17a699ce2d533b568f4d64d730`.
Compilation et Clippy ciblés `-D warnings` passent.

Rapport `cloud-history-comparison-002.json`, paire finale 1 :

- Couleur/opacité : 77 530 texels différents sur 518 400.
- Erreur moyenne RGB : 0,0001113 à 0,0001349 ; maximale : 0,04174805.
- Erreur moyenne opacité : 0,00006241 ; maximale : 0,05175781.
- Métadonnées : 282 texels différents ; profondeur courante max 0,125 km,
  profondeur historique max 0,875 km, coût normalisé max 0,3515625.
- Couleur SHA-256 : `3207fe60d33103e90fac89643ce696d37a6a7a182b7dca8890738200f92adb5a`.
- Métadonnées SHA-256 : `ce51651ffb376aa8f532d805ed3dbc98d2434e943d474148abe8f4fefefb859a`.

`cloud-history-trace-regression-001.json` confirme la dernière sortie de
marching identique au probe isolé `native-cloud-trace-003`. Les écarts de
profondeur sont réels et ne sont pas masqués par les moyennes de couleur.

## Travail restant

Ce lot est encodé pour qualification sur GPU, sans cadence interactive acquise.
Il reste à diagnostiquer les écarts, porter le panorama, brancher l'atmosphère
native, piloter les paramètres/coupes caméra en Rust, puis composer les nuages
et leurs ombres dans la carte. Aucun verdict de parité visuelle finale,
performance, navigateur, Studio ou distribution. Aucune publication.
