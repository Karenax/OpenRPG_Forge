# Tidewater — lumière rebond et comparaison village

Référence figée : `dgreenheck/tidewater@1438b1abfcaee3267092b75573014f4d9b4a983c`.
`scripts/tidewater/export-ground-bounce.mjs` exécute le calcul GPU original de
`src/materials/GroundBounce.js` sur le terrain, au soleil et à l'heure de la
fixture village. Deux acquisitions indépendantes donnent les mêmes SHA-256 :
carte brute `46c1a9bbc8955ae870460e5ec69bbea597fbfc53b58d7d8d401556b8a02c7e33`,
carte filtrée `70e99b2c01613f1f8b064d462a88e5ff8717d557b9248810efe4ae089a561843`.
Chaque carte mesure 512² texels RGBA16F ; les valeurs sont finies. Le paquet
`preview-scene-bundle-002` transporte la carte et son manifeste vérifiés
(`package-preview-scene.py --verify` : 1 542 entrées, SHA du manifeste
`d01e8b84f48aa1e2bafd9bdf71dec7448abf27fcc59898197c55296975ede5c6`).

Le matériau village natif lit la carte, interpole les quatre texels et ajoute
l'irradiance aux matériaux de bâtiments après les ombres et avant l'étalonnage.
Le réglage jouable « Lumière rebond » varie de 0 à 2, vaut 1 par défaut et est
sauvegardé avec les autres réglages graphiques. Les anciennes fixtures sans
carte restent utilisables. Les copies diagnostiques `graphics-village-camera-bounce-on-20260928`
et `...-off-20260928` ne diffèrent que par la force 1 ou 0. Leur rendu GPU
statique réussit, sans erreur de validation. La comparaison A/B trouve
111 616 pixels modifiés sur 1920×1080, au plus 5 niveaux sRGB par canal ;
la différence moyenne dans les maisons est +0,053 niveau. Le parcours
`--walk-smoke` sur le bundle 002 passe également et crée sa capture.

La référence `baseline-003/village.png` et la capture native ont la même pose
de caméra. Une capture native supplémentaire active explicitement HDR,
ACES filmic, +1 EV, saturation 1,06 et contraste 1,04 sur le mode statique.
Luminance moyenne sRGB, échantillonnée tous les 8 pixels :

| Région | Source | Native SDR | Native HDR/ACES |
|---|---:|---:|---:|
| Colline | 60,35 | 26,41 | 14,69 |
| Maisons | 106,84 | 57,15 | 51,01 |
| Avant-plan | 157,06 | 73,59 | 74,24 |

Cette capture HDR sans adaptation était incomplète, et le bundle employait
par erreur une météo couverte à 85 %. Le diagnostic complémentaire a isolé
la carte d'ombre des nuages, reconstruit le paquet avec la météo normale et
comparé la chaîne graphique complète sur la même pose :
[preuve corrigée](2026-09-28-default-weather-graphics.md).
Les matériaux du terrain
et des plantes ne reçoivent pas encore cette lumière rebond ; elle ne suit pas
encore le soleil mobile, et la scène Studio n'expose pas encore cette carte.
Des écarts demeurent dans les surfaces, la végétation, les effets écran et la
composition temporelle.

Contrôle amont du 28 septembre : `main` sur GitHub est à
`4811ba48d795197de5621985f404e765c0b7c0ef`, soit 40 commits après la
référence figée. `GroundBounce.js` n'a pas changé dans cet intervalle, mais
le post-traitement, le terrain, la végétation et le ponton ont changé. La
capture appariée ci-dessus qualifie donc la référence figée ; une nouvelle
baseline du site courant est nécessaire avant de parler de parité actuelle.

Le paquet Windows local `OpenRPG-Forge-Aube-Tidewater-Win64-20260928-010.zip`
embarque cette carte. Depuis le paquet extrait, le parcours `--walk-smoke`
retourne `pass=true`. La fenêtre interactive montre « Lumière rebond » et un
clic sur « + » écrit `bounce_light = 1,05` dans un profil isolé.
Ce paquet 010 conservait la météo couverte ; il ne représente plus le candidat
graphique corrigé.
