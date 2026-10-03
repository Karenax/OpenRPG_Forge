# Tidewater — paire visuelle côte/ciel avec post-traitement

Une capture de l'application source WebGPU épinglée
`dgreenheck/tidewater@1438b1abfcaee3267092b75573014f4d9b4a983c`
et une capture du runtime Rust du paquet Windows `023` utilisent la pose
`(42, 14, -90)`, la rotation source mesurée à `6,1e-17` près, un FOV de 62° et
une image de 1920 × 1080. La source est à 16,2 h, azimut 0, exposition 0,55,
échelle de rendu 1, avec post-traitement et auto-exposition actifs après
120 pas de simulation de 1/60 s. Son état, les empreintes des clones et le
mode de capture sont dans
`E:\OpenRPG_Forge\.tidewater-integration\source-seaward-clear004-20260929-021753\metadata.json`.

| Capture | Fichier | SHA-256 |
|---|---|---|
| Source | `E:\OpenRPG_Forge\.tidewater-integration\source-seaward-clear004-20260929-021753\seaward-clear004.png` | `db8c7019315897be00c8018591bbf0ac05949b11b1be8fe77dcd417529868a95` |
| Rust | `E:\OpenRPG_Forge\.tidewater-integration\paired-seaward-postfx-auto-20260929-001.png` | `f1eff0925d8b9fb721794819d81fea765818dd4d3b1e15be18db2fb9a7afb193` |

La capture Rust utilise le bundle `data` du paquet `023`, avec les couches
océan, rivage, embruns, nuages, végétation et bâtiments. Le profil
`paired-seaward-postfx-profile-20260929-001` (SHA-256
`283122301432C16ADA8F41D5943E706D67147B1E32B1DC93DBFD9F3D8FB609C5`)
fixe `auto_exposure=1`, `temporal_aa=0`, `exposure_ev=0.4` ; les autres réglages
gardent les valeurs du menu. `FORGE_TIDEWATER_STATIC_POSTFX=1` active HDR/ACES
et les effets de la promenade sur cette capture statique. Le profil et sa
valeur EV sont une calibration visuelle du village, pas une équivalence
numérique de l'exposition source.

`scripts/tidewater/Compare-PairedCapture.ps1 -Preset Seaward -Step 8` vérifie
les signatures et dimensions PNG, puis échantillonne les mêmes coordonnées
sur les deux images. Le reçu est
`E:\OpenRPG_Forge\.tidewater-integration\paired-seaward-postfx-auto-20260929-001.metrics.json`.
La luminance est `0,2126 R + 0,7152 G + 0,0722 B` sur sRGB 8 bits.

| Région | Luminance source | Luminance Rust | Écart absolu moyen |
|---|---:|---:|---:|
| Ciel, x 400–1600 / y 0–330 | 117,79 | 109,21 | 8,97 |
| Eau ouverte, x 1000–1650 / y 380–550 | 102,31 | 99,98 | 6,08 |
| Plage, x 500–1500 / y 740–1050 | 122,27 | 125,44 | 14,16 |
| Image entière | 127,14 | 125,85 | 10,19 |

À l'inspection, les grands volumes de la côte, du ponton, de l'océan et des
nuages se correspondent. Le ciel source est plus clair et légèrement plus
chaud, surtout en haut de l'image ; la moyenne proche de l'image entière ne
doit pas masquer cet écart local. La plage Rust omet la petite échoppe en bois
au premier plan gauche, ainsi que des débris et autres détails. Leur
raccordement à la scène native doit être vérifié à partir des assets source,
sans remplacement inventé. Les formes fines de l'eau, les ombres et la netteté
restent différentes.

La paire établit une comparaison **fixe** de la révision épinglée. Le site
public actuel a évolué depuis ce commit, notamment sur le TAAU, la brume,
le soleil à travers les nuages et la rive. La source utilise son accumulation
temporelle ; le TAA Bevy reste désactivé dans cette paire. Les lectures GPU
asynchrones, la précompilation et la phase du vent/eau ne sont pas qualifiées
comme une répétition image par image. Les valeurs moyennes et l'erreur par
pixel ne constituent ni une parité visuelle, ni un benchmark de performance.
