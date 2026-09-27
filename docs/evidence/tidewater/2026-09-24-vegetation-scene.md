# Tidewater — végétation proche et lointaine

Les six groupes exportés sont raccordés au prototype natif : palmiers,
bananiers, sous-bois (jeunes palmiers/fougères), monsteras, grandes feuilles
(oreilles d'éléphant/héliconias/strelitzias) et canopée (arbres/arbustes).
Ils représentent les 65 383 enregistrements source, huit niveaux de géométrie
et trois programmes de matériau WGSL. Les maillages, matériaux et textures
sont partagés ; chaque instance adresse les valeurs iPos/iDat et la matrice
f32 d'origine, sans décomposition/recomposition de cette dernière.

## Sélection et portée

La sélection Rust initiale utilise les rayons horizontaux, limites de cellules
et rayons propres aux instances du source. Elle conserve l'ordre de parcours
de grille avant le tri stable des distances arrondies en f32. Le niveau lointain
exclut les palmiers certainement couverts par le niveau proche et utilise les
seaux de six mètres pour la canopée. Le shader conserve ses fenêtres de
distance et sa déformation à temps 2 s, vent (1,0), vitesse 6, frame 124.

La sélection est calculée pour une caméra fixe, sans mise à jour en mouvement.
Les nombres sélectionnés correspondent aux oracles indépendants sur les trois
vues contrôlées. Cela ne qualifie pas tous les identifiants, toutes les positions
possibles de caméra ni l'ordonnancement temporel du source.

Fixtures finales locales sous `E:/OpenRPG_Forge/.tidewater-integration/` :
`vegetation-scene-002` (village), `vegetation-canopy-001` et
`vegetation-island-001`. Leurs données dérivent de source-017.
`vegetation-scene-001` est l'étape intermédiaire avant l'exclusion/tri lointain.

## Comparaison indépendante des silhouettes

Le source intact reconstruit terrain, fondations du village et végétation, puis
rend uniquement ses huit niveaux sur une cible dédiée. Le prototype natif masque
terrain/village et restitue le même masque géométrique/alpha, sans MSAA.

| Vue | Pixels dans l'union | Pixels différents | IoU | Différences au-delà d'un pixel |
|---|---:|---:|---:|---:|
| Village | 583 937 | 152 | 0,9997396979 | 3 |
| Forêt proche | 1 321 350 | 2 793 | 0,9978862527 | 11 |
| Île | 79 266 | 23 | 0,9997098378 | 0 |

Rapports : `vegetation-coverage-final.json`,
`vegetation-canopy-coverage-001.json`, `vegetation-island-coverage-001.json`.
Ces écarts sont mesurés, pas automatiquement acceptés comme identité visuelle.
Pas de comparaison d'éclairage, d'ombres, d'occlusion par le reste de la scène,
de couleurs ni de séquence animée dans ces métriques.

Au village, les niveaux proches sélectionnent respectivement 65, 83, 73, 3,
48 et 4 instances ; les niveaux lointains 206 palmiers et 18 343 éléments de
canopée. En forêt : 0, 25, 938, 85, 0 et 361 proches, 233/18 343 lointains.
Depuis l'île : aucun proche, 233/18 343 lointains. Comptages concordants natif/source.

## Contrôles et artefacts

- Clippy strict : `vegetation-scene-clippy-003.log`, réussi.
- Construction : `vegetation-scene-build-final.log`, réussie.
- Binaire SHA-256 :
  `b32eeeb1390adc42225dba896fe0568a32a4a23d0e802382c419a14ed3060cfb`.
- Captures de surface village et forêt inspectées. Village final :
  `native-vegetation-scene-final.png`, SHA-256
  `efc23005bc3986bbf286212dbb2adbfdad09b2791f6842de2a56528359daf81e`.
  Forêt : `native-vegetation-canopy-surface-001.png`.
- Arbre proche isolé : `native-tree-mask-regression-006.png`, inchangé,
  `49be5ddcbfcb3b7c66ec0bc3a13debeb4e78a2c7f75571477e7ac556f36021ae`.
- Albédo village : `native-village-albedo-regression-002.png`, inchangé,
  `fcca3024af8de8d14ebeb0f62f5a04485fc4e8b577335de67b4928a8ec9b58b2`.
- Référence de géométrie inexistante, hash du descripteur recalculé : refus
  sémantique avant GPU, code 1 et aucune capture (`vegetation-invalid-001.log`).
- Premier oracle interrompu avant capture : ressources d'ombres non initialisées
  lors de la cuisson d'imposteurs. Le harnais crée désormais `SunShadows` avant
  la cuisson, comme le harnais précédent ; aucun fichier source modifié.

## Travail restant

Le rendu de surface reste nettement trop sombre/contrasté, avec reflets et
crénelage visibles : l'éclairage Bevy provisoire ne reproduit pas le ciel,
la transmission du feuillage, les ombres et le traitement temporel source.
Ces images ne sont pas une preuve de fidélité finale. L'herbe, les autres décors,
l'océan et les effets restent absents. Les transitions en déplacement doivent
être raccordées et qualifiées. Aucun asset Studio ni contrat canonique de
végétation n'est créé par cette fixture de développement. Pas de benchmark,
qualification navigateur ou publication Git ; tous les critères TW restent ouverts.
