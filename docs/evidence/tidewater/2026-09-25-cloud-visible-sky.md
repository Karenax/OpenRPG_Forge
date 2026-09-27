# Nuages visibles dans le ciel de la scène native

Référence `1438b1abfcaee3267092b75573014f4d9b4a983c`. Données locales sous
`E:/OpenRPG_Forge/.tidewater-integration/`.

L'implémentation de calcul est partagée dans
`examples/tidewater_cloud_weather_probe/mod.rs`. Le probe headless devient un
wrapper ; la scène appelle `install` avec ses propres handles atmosphériques.
Un seul appareil Bevy et un seul calcul atmosphérique alimentent les nuages.
Le fond du ciel échantillonne l'historique final et, hors de la vue reconstruite,
le panorama, via les fonctions originales `cloudsSampleView`/`cloudsSample`.
La composition originale est `base * transmission + lumière diffusée`.

Le matériau reçoit le même bloc de paramètres d'échantillonnage que la source.
La préparation vérifie position, quaternion et FOV ; l'installation vérifie
la cohérence du bloc avec l'acquisition et des paramètres atmosphériques.
Le mode/fixture incompatible est refusé avant le rendu.

## Captures et mesures

`cloud-island-source-001` : acquisition originale sur la pose île de
`baseline-003/island.json`. `cloud-sky-island-001/002` : 232 fichiers préparés,
002 ajoute les assertions explicites quaternion/FOV, sorties visuelles inchangées.
Caméra [120,440,780], FOV 62°, 1920×1080, état figé à 2 secondes.

La référence `render-source-vegetation.mjs --shadows=ENV --cloud-sky=ACQUISITION`
utilise le renderer et les fonctions Sky/SkyProClouds originaux, avec les
textures d'historique/panorama acquises. C'est une référence de composition
isolée, pas une nouvelle qualification du démarrage complet d'App.

Les captures source et native ont été inspectées. Les premières captures
village `source-cloud-sky-001/002` n'exposent pas le ciel ; elles sont exclues
de la preuve visuelle des nuages. La première omet en plus les ombres d'objets.

`cloud-sky-scene-comparison-001.json` compare :

- Source : `source-cloud-sky-island-001.png`, répétée en 002.
- Natif : `native-cloud-sky-island-003.png`, identique à la répétition 002.
- Effet supérieur à 3 niveaux : 83 525 pixels source, 83 461 natifs.
- Région source dilatée d'un pixel : 91 038 pixels.
- Erreur moyenne par canal : 0,134299/255 ; P99 maximum canal : 1/255 ; max : 4/255.
- Moyenne sur toute la scène : 0,134742/255, incluant les écarts préexistants.

La répétition source diffère en un pixel de terrain (1166,717), canal vert
79→80. Sa région d'effet des nuages est identique. Le comparateur mesure cette
variation au lieu de déclarer la scène source strictement reproductible.

SHA-256 capture native finale :
`e58904a96122e7ff467dc140d1edcec0aca2583b8368e0d28c3d6ea6d2dd7f51`.
SHA-256 source 001 :
`b26ab59983318b20e63a6f3b340d12b1a9dc64d18f948bfd8401a8ea934fef9c`.

## Régressions

- Ciel sans nuages `native-cloud-sky-disabled-001.png` strictement identique
  au candidat île précédent, SHA `74a1aad1ea6c5d2f0afbc8b80f5d2a2d795037709618ffdd05ff1a7e5a133d83`.
- `cloud-shared-graph-regression-001.json` : panorama headless identique après
  extraction du module partagé ; entrées toujours sans résultats source.
- `cloud-sky-invalid-mode-001.log` : fixture avec nuages refusée en mode ciel
  clair, code 1, sans capture.
- Clippy ciblé des deux exemples `-D warnings` et compilation passent.

Binaire scène : `8baa9fe8fabd2c3fe93ab545b83246b8d8013d3afcbe8026ace3b8b0b2e916f4`.
Binaire headless : `059a340a5e754858dbd3ba6a7f60e5930f1055f5314cd705e89cc6989b80f77c`.

## Portée

Cette vue isole le ciel : elle n'inclut pas le matériau océan ; le fond sombre
sous l'horizon n'est pas une preuve d'eau. Réflexions de l'eau, environnement
recalculé, réception des ombres de nuages, paramètres animés natifs et panorama
en mouvement restent ouverts. Les champs soleil/irradiance du Frame sont encore
acquis. Aucune parité globale, performance, navigateur ou Studio déclarée.
Travail préexistant conservé, aucune publication.
