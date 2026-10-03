# Tidewater — village, caméra appariée et écart lumineux

Référence figée : `dgreenheck/tidewater@1438b1abfcaee3267092b75573014f4d9b4a983c`.
La capture source `E:\OpenRPG_Forge\.tidewater-integration\baseline-003\village.png`
est à 1920×1080, caméra `(42, 14, -90)`, quaternion
`(-0,0998334166, 0, 0, 0,9950041653)`, FOV 62°, temps source 2 s.
La fixture native `preview-scene-bundle-001/scene` avait la même position mais
une rotation opposée, face à la mer : sa capture statique
`graphics-baseline-village-007.png` ne qualifie donc pas le village.

Une copie diagnostique, uniquement sur E: et sans modification de la fixture
originale, réutilise ses 388 fichiers de données par liens matériels et reprend
exactement la pose de caméra source. L'exécutable du paquet local 007 quitte
avec le code 0 et produit
`E:\OpenRPG_Forge\.tidewater-integration\graphics-baseline-inland-with-grass-007.png`.
Le premier essai a échoué avec le code 101 : l'oracle de cellules d'herbe avait
été exporté pour la vue vers la mer et son assertion rejetait la nouvelle pose.
Une capture intermédiaire sans herbe est conservée sous
`graphics-baseline-inland-no-grass-007.png`. Pour la capture finale, les trois
listes d'herbe de la seule copie diagnostique ont été renouvelées à partir de la
sélection **native** à cette caméra : 9, 28 et 90 cellules. Le fichier source
`preview-scene-bundle-001/scene/grass.json` est resté byte-identique. Le journal
de la capture finale ne contient ni panique ni erreur GPU, mais ces listes ne
sont **pas** un oracle indépendant de la référence JavaScript. La copie garde
les limitations `buffers_and_resident_selection_reused_from_seaward_fixture`
et `grass_cells_derived_from_native_selection_not_upstream_oracle`.

La composition générale du village et du relief est proche à cette caméra.
À pas de 8 pixels et avec la même région 1920×1080, luminance sRGB pondérée
`0,2126 R + 0,7152 G + 0,0722 B` :

| Région (pixels) | Référence | Forge 007 | Forge / référence |
|---|---:|---:|---:|
| colline, x 400–1500 / y 100–330 | 60,35 | 26,41 | 0,438 |
| maisons, x 200–1700 / y 380–620 | 106,84 | 57,11 | 0,534 |
| premier plan, x 450–1550 / y 780–1030 | 157,06 | 73,57 | 0,468 |

Cette mesure établit un écart visible de luminosité pour **ces deux captures**,
pas une parité ou un diagnostic isolé du seul éclairage. L'exposition et le
post-traitement du menu de promenade ne s'appliquent pas au mode de capture
statique ; la sélection de végétation n'a pas été réexportée et celle d'herbe
n'a pas été comparée à un oracle source pour la nouvelle caméra. Il faut
régénérer les oracles/caches de cette vue et
comparer à réglages temporels et d'exposition appariés avant de qualifier
TW-002 ou d'attribuer l'écart à un effet précis.

La [capture 021 avec la chaîne graphique active](2026-09-29-village-postfx-pair.md)
remplace cette ancienne paire comme mesure de luminosité du village. Les
valeurs 007 ci-dessus décrivent seulement le diagnostic sans post-traitement.
