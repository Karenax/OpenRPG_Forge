# Tidewater — nouvelle paire village dans le Playtest Studio

La comparaison utilise la capture PNG 1920 × 1080 de la révision source
`1438b1abfcaee3267092b75573014f4d9b4a983c` à la caméra Village,
`E:/OpenRPG_Forge/.tidewater-integration/baseline-003/village.png`.
Le candidat Studio isolé `studio-tidewater-pier-candidate-093` a pour SHA-256
`e12d220bde50c45c59ae59e909f14f093491c604f6bf6f69645bcab300b4bd4f`.
Le [reçu de capture](../../../../.tidewater-integration/studio-tidewater-village-pier-pair-094/tidewater-studio-village-reference.json)
confirme 1920 × 1080, la caméra de diagnostic, le chargement de la zone et du
Playtest, l'océan FFT et les nuages actifs. Il ne démontre pas la parité
temporelle : source à 2,00 s, Studio à 3,3975 s ; la végétation, le ciel et
l'eau peuvent donc être à des états différents.

| Région, erreur absolue moyenne de luminance sRGB | Studio 0,55 EV | Studio 0,40 EV |
|---|---:|---:|
| Image entière | 16,86 | 13,10 |
| Relief boisé | 11,27 | 8,73 |
| Maisons | 23,97 | 18,90 |
| Premier plan | 17,01 | 12,86 |

Mesure : un pixel sur huit par axe, même outil
`scripts/tidewater/Compare-PairedCapture.ps1 -Preset Village`.
[Reçu 0,55 EV](../../../../.tidewater-integration/studio-tidewater-village-pier-pair-094/compare-055.json) ·
[reçu 0,40 EV](../../../../.tidewater-integration/studio-tidewater-village-pier-pair-094/compare-040.json) ·
[capture Studio 0,40 EV](../../../../.tidewater-integration/studio-tidewater-village-pier-pair-094/tidewater-studio-village-calibrated-040.png).
À 0,40 EV, la source est à 109,23 et le Studio à 117,76 de luminance moyenne :
l'image Studio reste trop claire. L'erreur entière mesurée dans la paire
précédente était 14,02 à la même exposition, contre 13,10 ici ; la capture
actuelle contient aussi la passerelle et le quai. Le seul chiffre ne permet
pas d'attribuer le gain à un composant précis ni de conclure à une parité.

À l'examen des deux images, les silhouettes des bâtiments et des reliefs sont
proches. La passerelle est désormais visible au premier plan, mais les petits
objets de place manquent encore, notamment les deux bancs peints, le lampadaire
et les tonneaux. Leur construction source est distincte des 23 bâtiments.
La source dispose aussi d'une reconstruction temporelle TAAU avec historique,
profondeur et vitesse ; la vue Studio emploie actuellement le FXAA et la
netteté réglable. Cela contribue possiblement à l'aspect plus dur ou instable,
sans que cette paire statique isole son effet. Il faut intégrer le décor
manquant, apparier le temps/météo, puis refaire la comparaison et la revue
visuelle avant de conclure sur les effets.

La référence est l'engagement épinglé, pas le `main` public plus récent.
L'essai est un candidat local `BUILD_ONLY`, sans publication ni validation
utilisateur de la parité visuelle.
