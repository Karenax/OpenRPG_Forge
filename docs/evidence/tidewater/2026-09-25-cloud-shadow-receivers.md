# Réception des ombres nuageuses dans la scène native

Le champ 256² R32Float du graphe de nuages Bevy est partagé avec les matériaux
du terrain (dont CDLOD), du village, de la végétation/herbe, de l'eau, des
déferlantes et des embruns. Échantillonnage bilinéaire manuel, atténuation au
bord et force viennent du module original `cloudsShadow`. La modulation porte
sur l'éclairage solaire direct ; elle ne remplace pas l'environnement ambiant.

`prepare-cloud-shadow-scene.py` prépare ces six shaders depuis une fixture
de reflets nuageux. Le script vérifie hashes et paramètres publics identiques
à l'acquisition. Les matériaux opaques, y compris les clones CDLOD, reçoivent
les handles partagés après Startup. Le mode reste `--water-spray-clouds`.

## Cas qui exerce réellement les récepteurs

La vue côtière initiale, couverture 0,49, est claire au sol : carte d'ombre
centrale à 1 et aucun pixel source modifié de plus de 3 niveaux. Cette capture
`source-cloud-shadow-001.png` ne qualifie donc pas l'effet visible.

Un cas distinct `cloud-overcast-reference-001.json` conserve caméra, soleil,
vent et temps, et utilise le contrôle original `clouds.coverage.value=0.85`.
Le nouvel argument JSON `cloud_coverage` de l'exporteur est validé dans [0,1]
et consigné dans le manifeste. La référence principale n'est pas modifiée.

Sous `E:/OpenRPG_Forge/.tidewater-integration/`, les acquisitions
`cloud-overcast-source-001/002` ont 694 fichiers identiques, vérifiés ainsi
que les 37 fichiers d'environnement de `cloud-overcast-environment-001`.
Les fixtures `cloud-overcast-water-001` et `cloud-overcast-shadow-001` isolent
la réception des ombres, à ciel/reflets identiques. Vue FHD à 2 secondes.

Source retenue : `source-cloud-overcast-shadow-002.png`, répétition 003.
Native : `native-cloud-overcast-shadow-001.png`, répétition 002.
Les deux images ont été inspectées. La première source 001 est exclue :
elle passait `clouds` au constructeur WaterMaterial qui ignore cet argument.
La capture corrigée fixe `material.clouds` avant sa première compilation,
comme l'application originale. Déferlantes et embruns acceptent bien cet
argument dans leur constructeur.

## Mesures

`cloud-shadow-scene-comparison-001.json` :

- effet >3 niveaux : 1 256 943 pixels source, 1 256 934 natifs ;
- région dilatée : 1 300 520 pixels ;
- erreur moyenne par canal : 0,196812/255 ; P99 du maximum : 4/255 ; max : 109/255 ;
- moyenne scène complète : 0,164096/255 ;
- bande eau avec ponton/rochers : 0,267026/255, P99 4/255, max 109/255 ;
- répétitions source et native strictement identiques.

Ces erreurs incluent les écarts géométriques et de rendu préexistants ; elles
ne constituent pas une parité globale. SHA-256 native :
`579e78405e5d4886faaa96a79ee6042a9de3b4a3f4c412d109888255f8082d64`.
Source : `1ee7ae3b8e6365adb7fa9f1522c12dfc4e3a3194833dcac58e3650c10595904b`.
Binaire : `edd39116a9b979f94bcd1c683e067fa13c4069d372a981865b743603450cb277`.

## Régressions et limites

Clippy strict et compilation passent (`cloud-shadow-clippy-003.log`,
`cloud-shadow-build-001.log`). `cloud-shadow-state-regression-001.json` vérifie
la répétition des 21 buffers FFT et des cinq états crêtes/compteurs/dépôts/rivage.
Les 7 014 particules actives sont identiques par identité entre répétitions
et candidat précédent ; l'ordre atomique des emplacements peut varier.
`native-cloud-shadow-disabled-001.png` conserve exactement le rendu de reflets
précédent sans réception des ombres.
`native-cloud-shadow-clear-regression-001.png`, sans ressource de nuages,
est également identique à `native-spray-autonomous-001.png`.

Le champ est calculé nativement à partir des paramètres acquis. Pose fixe,
éclairage ambiant de la fixture antérieure, contrôleur temporel et soleil
encore acquis : environnement dynamique, longues animations, éclairage
indirect complet, Studio, performance et navigateur restent à qualifier.
Pas de clôture de fidélité complète ou de publication Git.
