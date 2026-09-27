# Ciel clair et disque solaire natifs

Référence Tidewater `1438b1abfcaee3267092b75573014f4d9b4a983c`.
Preuves locales sous `E:/OpenRPG_Forge/.tidewater-integration/`.
Prototype Bevy natif Vulkan, AMD RX 7800 XT ; deux vues fixes de jour,
1920×1080, caméra à 440 m, temps figé à 2 s.

## Implémentation

Le calcul atmosphérique est partagé dans
`examples/tidewater_atmosphere_probe/mod.rs`. Le prototype de scène l'installe
sur son propre device et attend sa disponibilité avant la capture. Le nouveau
matériau `tidewater_plant_probe/sky.rs` échantillonne ces LUTs avec les fonctions
WGSL de ciel/transmission originales. Un rectangle plein écran reconstruit le
rayon depuis la caméra Bevy ; sa profondeur reverse-Z nulle et le test Equal
réservent le ciel aux pixels sans géométrie. Le disque solaire est inclus.

`export-atmosphere.mjs` exporte aussi les modules nécessaires et leur provenance.
`prepare-sky.py` vérifie fichiers, pin, soleil et hauteur avant assemblage.
Le mode natif `--sky` exige cette fixture. La référence indépendante utilise
`render-source-vegetation.mjs --shadows=ENV --clear-sky` et les composants
Atmosphere/Sky originaux. Aucun nuage n'est dessiné dans cette comparaison.

## Mesures

| Vue | Référence | Candidat final | Erreur RGB moyenne /255 | P99 erreur max canal | Maximum |
|---|---|---|---:|---:|---:|
| Île | source-sky-island-001.png | native-sky-island-003.png | 0,1154 | 2 | 135 |
| Soleil | source-sky-sun-001.png | native-sky-sun-002.png | 0,03154 | 1 | 78 |

Rapports : `sky-island-comparison-002.json`, `sky-sun-comparison-001.json`.
Les deux vues ont été inspectées. Ce sont des mesures, pas une parité exacte.
Le ciel sous l'horizon reste un fond atmosphérique ; il ne représente pas l'eau.

SHA-256 du candidat île final :
`74a1aad1ea6c5d2f0afbc8b80f5d2a2d795037709618ffdd05ff1a7e5a133d83`.
SHA-256 du candidat soleil :
`1cd22e954dc2bd322c02d7f39f35f044a6368e21c90b8f49b8e515b07faef91a`.

## Régressions et corrections

- `sky-build-003.log` : compilation finale réussie.
- `sky-clippy-002.log` : Clippy réussi pour les trois exemples scène,
  atmosphère et ombres d'objets.
- `sky-regression-001.json` : village sans ciel strictement identique à
  `native-object-filter-002.png` ; quatre sorties de l'atmosphère partagée
  strictement identiques à `native-atmosphere-stable-island-001/`.
- La nouvelle capture île diffère de la précédente en un pixel (389,803),
  maximum 10 niveaux, moyenne RGBA 0,00000354. Cette variation demeure ouverte ;
  aucune répétabilité bit à bit de la scène n'est revendiquée.
- Les deux incompatibilités fixture/mode sont rejetées avant GPU, code 1,
  aucun PNG créé (`sky-invalid-mode-001/002.log`).
- Le premier shader assemblé plaçait une directive diagnostic après des
  déclarations : corrigé dans la préparation, fixture finale `sky-island-002`.
- La vue solaire ne sélectionne aucun terrain. Le premier essai a exposé un
  buffer vide dans Bevy ; les passes de terrain et d'ombre ignorent désormais
  les sélections vides. La capture solaire finale termine normalement.

Binaire scène final SHA-256 :
`9cded408d4f5d186f5387c00fa1c0a58d6bffa0979891007b798698cd38f3f01`.
Binaire atmosphère SHA-256 :
`ac10dd701b16ea9719a6776ed317e6767a6f64672969d6c54ed2b79aba7e6559`.

## Limites

L'éclairage des surfaces utilise encore le cube/SH acquis. Nuages, génération
native de cet environnement, océan, post-traitement, temporel, caméra animée et
nuit restent à réaliser ou qualifier. Ce prototype ne livre pas encore une
carte Studio éditable ni un package autonome. TW-009 et tous les critères
d'intégration complète restent ouverts. Aucune publication.
