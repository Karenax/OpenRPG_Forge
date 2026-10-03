# Tidewater — comparaison du village avec les effets actifs

Référence figée `dgreenheck/tidewater@1438b1abfcaee3267092b75573014f4d9b4a983c` :
`E:\OpenRPG_Forge\.tidewater-integration\baseline-003\village.png`
(SHA-256 `01E15DA853E17FADAF4CE23AB90B45C66C2C59734ABB254CDB6A7D609F82B142`).
Capture native avec le runtime extrait du paquet Windows 021 :
`E:\OpenRPG_Forge\.tidewater-integration\paired-village-postfx-auto-20260929-001.png`
(SHA-256 `03F33130F8C001C9ECF0C72256E1929485A11C2CF328362C5D82AFC17E726E8D`).
Les deux images mesurent 1920 × 1080 pixels et utilisent la même pose de
caméra vers le village. La capture native exploite la fixture
`graphics-village-camera-bounce-on-20260928`, les couches océan/rivage/embruns/
nuages de `preview-scene-bundle-clear-004` et le profil isolé
`paired-village-postfx-profile-20260929-001` (`auto_exposure=1`,
`temporal_aa=0`, `exposure_ev=0.4`).
`FORGE_TIDEWATER_STATIC_POSTFX=1` installe sur la caméra de capture la chaîne
HDR/ACES et les effets du mode jouable. La source utilise son auto-exposition
et une exposition de 0,55 ; les algorithmes et les unités d'exposition des
deux moteurs ne sont pas identiques.

Luminance sRGB pondérée `0,2126 R + 0,7152 G + 0,0722 B`, un échantillon tous
les 8 pixels, mêmes rectangles sur les deux images. Calcul reproductible avec
`scripts/tidewater/Compare-PairedCapture.ps1 -Preset Village` ; reçu
`E:\OpenRPG_Forge\.tidewater-integration\paired-village-postfx-auto-20260929-001.metrics-v2.json` :

| Région (pixels) | Source | Forge 021 | Écart absolu moyen par échantillon |
|---|---:|---:|---:|
| Colline, x 400–1500 / y 100–330 | 60,35 | 60,92 | 6,15 |
| Maisons, x 200–1700 / y 380–620 | 106,84 | 110,62 | 11,07 |
| Premier plan, x 450–1550 / y 780–1030 | 157,06 | 154,16 | 8,04 |
| Image entière | 109,23 | 109,62 | 8,37 |

Les moyennes lumineuses sont maintenant proches ; l'ancien écart de presque
50 % provenait surtout d'une comparaison où la capture native n'activait pas
sa chaîne HDR/ACES et coupait l'adaptation d'exposition. Ce constat ne signifie
pas que les rendus sont identiques : à l'inspection, le natif présente des
contours et de l'herbe plus marqués, ainsi que des détails/ombres de végétation
différents. La source emploie son TAAU temporel ; le TAA Bevy reste désactivé
dans cette capture. La météo, le vent, le contenu dynamique et la convergence
temporelle ne sont pas encore appariés image par image. La caméra village
n'expose pas assez de ciel pour qualifier les nuages et la finition atmosphérique
qui motivent la demande initiale. Le `main` public observé le 29 septembre est
déjà à `4811ba48d795197de5621985f404e765c0b7c0ef` ; cette paire ne
qualifie pas ses changements récents de rendu.
