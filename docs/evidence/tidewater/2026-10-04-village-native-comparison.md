# Tidewater — comparaison Village du candidat natif du 4 octobre

Le candidat `studio-scene-camera-20261004-v1` (exécutable SHA-256
`5d6468ce6744066511cd5b58f8205d0ab3516ddeaf9572690c7ee63216be7d0b`)
est capturé sur une copie intacte de la fixture. Aucun placement n'est ajouté
pour cette comparaison. Les 26 fichiers sont relus après fermeture et
correspondent aux pins de préparation. La session 99778 termine avec code 0.
Le premier essai, interrompu par le délai CDP, reste conservé en échec.

La capture et son audit indépendant sont dans
`.tidewater-integration/studio-scene-camera-library-20261004-v1/` :
`village-current-v2.png`, `village-capture-audit-v2.json` et
`village-comparison-v2.json`. Les fichiers natifs originaux restent dans
`F:/OpenRPG_Forge/.build/studio-scene-camera-20261004-v1/village-capture-v2/`.

La référence est `baseline-003/village.png`, source
`1438b1abfcaee3267092b75573014f4d9b4a983c`, SHA-256
`01e15da853e17fadaf4ce23ab90b45c66c2c59734abb254cdb6a7d609f82b142`.
La nouvelle image a SHA-256
`4545653e909fd9143329bacafd6b12afd54e5efb42f985a7cdcaa8302bd3e271`.
Les deux PNG mesurent 1920×1080. Le receiver vivant confirme la position
caméra `(42,14,-90)` ; le diagnostic Village existant fixe rotation et FOV
62°, concordants avec la recette source inspectée. L'état public confirme
exposition +0,10 EV, soleil 16,20 h et simulation active. L'atlas contient
148 parties opaques, trois cascades et des passes encodées.

## Mesures de l'image

`Compare-PairedCapture.ps1 -Preset Village`, échantillon tous les huit pixels :

| Région | Luminance source | Luminance native | Erreur absolue moyenne |
|---|---:|---:|---:|
| Colline | 60,3536 | 59,8617 | 6,2255 |
| Maisons | 106,8445 | 108,5252 | 12,9672 |
| Premier plan | 157,0620 | 151,7977 | 8,3575 |
| Image complète | 109,2250 | 108,5543 | 9,3405 |

La proximité des moyennes ne prouve pas l'équivalence. Le rendu conserve
des différences visibles de matières, éclairage et accessoires. Les ombres
des palmiers et bâtiments sont visibles ; leur présence ne qualifie pas
la limite de distance de tous les casters.

## Écarts qui déterminent la suite

L'inspection des deux PNG montre notamment des accessoires de cour absents
dans la capture native : clôtures, linge, caisses/tonneaux et séchoirs.
La source figée `src/world/Village.js::_villageProps` produit explicitement
ces éléments : quatre clôtures (lignes 409–412), trois cordes à linge
(426–428), un séchoir à poissons et un séchoir à filets (431–435), seize
instances de petits objets (443–451), une barque sur tréteaux et un seau
(465–482). Les accessoires de plage sont produits séparément par
`_beachProps` ; les poissons utilisent également `FishProps`.

La qualification historique de la place couvre seulement deux bancs,
un lampadaire et deux tonneaux. Elle ne prouve pas la conversion de tous
les accessoires du village. La suite doit établir leurs identités,
maillages, matériaux, collisions et placements éditables en conservant
leurs générateurs déterministes et la source intacte. Il ne faut pas
compenser leur absence par une calibration d'exposition ou un décor opaque
non éditable. Le bateau jouable et les barques décoratives gardent des
contrats distincts.

La caméra est appariée, mais les horloges ne le sont pas : source 2 s,
statut Studio 2,5923 s. Les états animés peuvent différer. Ce reçu ne
qualifie donc ni la parité temporelle, ni les autres vues/heures, ni les
performances, ni la livraison. L'objectif complet reste actif.
