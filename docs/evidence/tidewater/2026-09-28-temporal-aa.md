# Tidewater — anticrénelage temporel dans la promenade native

Référence figée : `dgreenheck/tidewater@1438b1abfcaee3267092b75573014f4d9b4a983c`.
`src/post/TemporalUpscale.js` rééchantillonne l'historique, emploie les vecteurs
de mouvement et la profondeur, et reçoit explicitement le masque de l'eau. Le
code source conserve l'historique sur l'eau lorsque le rejet par profondeur
produirait une image brute scintillante (`TemporalUpscale.js`, vers 228–278).
Le `TemporalAntiAliasing` standard de Bevy 0.19 ne reproduit pas cette logique
spécifique à Tidewater. L'absence de ce traitement est une explication plausible
de l'écart, pas une causalité isolée par la présente mesure.

La promenade Forge expose désormais `TAA (expérimental)` dans `H` / `F10`.
Il est **désactivé par défaut** : un profil sauvegardé avant l'ajout du champ
charge également cette valeur par défaut. L'activation configure la caméra
avec la passe temporelle Bevy ; la désactivation retire aussi ses composants
de jitter, de biais mip, de prépasses profondeur et mouvement.

Deux profils isolés ne diffèrent que par `temporal_aa = 1` ou `0`. À la même
pose initiale, après 35 s de rendu et sur deux répétitions par mode :

| Région, gradient spatial moyen 8 bits | TAA actif | TAA arrêté |
|---|---:|---:|
| océan, x 1060–1530 / y 580–650 | 4,89 / 4,84 | 14,39 / 14,46 |
| rive, x 1050–1510 / y 625–690 | 4,13 / 4,21 | 7,96 / 7,94 |
| maison | 11,07 | 16,78 |
| ponton | 11,50 | 19,97 |

Le gradient est la moyenne des écarts absolus horizontaux et verticaux de la
luminance sRGB, échantillonnée un pixel sur deux. Il mesure la perte de détail
spatial dans ces captures et **pas** la stabilité temporelle en mouvement.
Les captures `E:\OpenRPG_Forge\.tidewater-integration\graphics-taa-ab-on-window.png`
et `...\graphics-taa-ab-off-window.png` montrent un océan, une rive et des
textures plus mous avec le TAA standard. Les secondes prises sont conservées
sous `...-first.png`. Les quatre parcours ont quitté avec code 0, sans panique
ni erreur de validation GPU dans les journaux.

Après avoir remis le défaut à `0`, la recompilation native a réussi avec
`rust-lld` sous Windows. Le parcours `--walk-smoke` du binaire final a produit
`TIDEWATER_WALK_PASS` (8,994 m, 302 ticks au sol). Sous un profil utilisateur
neuf, la capture interactive
`E:\OpenRPG_Forge\.tidewater-integration\graphics-taa-ab-default-window.png`
affiche `TAA (expérimental) : Arrêt` ainsi que les sept autres réglages ;
la fenêtre a quitté avec code 0. Le profil de vérification est isolé sur E:.

Le paquet Windows local `OpenRPG-Forge-Aube-Tidewater-Win64-20260928-008.zip`
contient 2 505 entrées et mesure 421 493 432 octets (SHA-256
`b7f739a718a9e10dd581f3f15f39f64eaa896e2fe6c9fd17820a123f06b7c9cb`).
Le moteur copié a le même SHA-256 que le moteur compilé,
`aca5d0ec8ede3f52c99a34f7208c22c8a43babb4bd252ec64e677477cef70106`.
Le lanceur Aube répond `FORGE_LAUNCHER_AUBE_FRONTEND_READY`. Depuis le dossier
empaqueté, le parcours `--walk-smoke` a produit `TIDEWATER_WALK_PASS` (8,994 m,
311 ticks au sol) ; une session interactive sous un autre profil neuf a affiché
les huit réglages, dont `TAA (expérimental) : Arrêt`, dans
`E:\OpenRPG_Forge\.tidewater-integration\graphics-package-008-default-window.png`
et a quitté avec code 0. La collecte des notices Rust signale encore 42
paquets sans texte de licence local, à qualifier avant toute diffusion externe.

Cette A/B ne démontre ni une parité avec le TAAU WebGPU de Tidewater ni que TAA éteint
soit visuellement équivalent à la référence. Pour la parité, il reste à porter
le traitement de l'historique et du masque d'eau, apparier les vues et évaluer
le scintillement sur une séquence animée. L'écart de luminosité et de végétation
du village est suivi séparément dans `2026-09-28-village-camera-pair.md`.

Audit du 29 septembre : le chemin de surface statique garde `Msaa::Sample4` et
peut désactiver silencieusement le TAA Bevy, qui exige `Msaa::Off`. L'A/B ci-dessus
utilise bien `--walk`, dont la configuration force `Msaa::Off`. Elle prouve une
différence spatiale du TAA natif, mais aucune stabilité temporelle ni fidélité
des vecteurs de mouvement des matériaux terrain, village, plantes et eau.
