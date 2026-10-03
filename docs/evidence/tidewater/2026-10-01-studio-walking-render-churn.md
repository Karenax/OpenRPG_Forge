# Tidewater — coût du déplacement en plein écran

Suite : [cache d'éclairage des plantes, candidat 115](2026-10-01-studio-plant-light-cache.md).

Suite des [matériaux Studio, candidat 107](2026-09-30-studio-cloud-material-churn.md).
La référence reste `1438b1abfcaee3267092b75573014f4d9b4a983c`.
Les premières acquisitions ont commencé le 30 septembre au soir.

## Limite révélée par le déplacement

Les fenêtres du rapport précédent mesuraient une caméra immobile avant et
après une brève marche. Elles ne prouvaient pas une cadence de 107–137
images/s pendant le déplacement continu.

Le [reçu 109](../../../../.tidewater-integration/studio-tidewater-fullscreen-traversal-109/tidewater-fullscreen-traversal.json)
confirme un canvas réel de 1920 × 1080, un déplacement de
(53,60 ; −72,85) à (56,46 ; −96,53) m sur les axes X/Z, et les trois
cascades restant actives dans la génération 1. La vue est celle de la plage
vers le village. La cadence rAF passe de **143,01 à l'arrêt à 20,50 en marche** ;
p95 de 7,00 à 55,60 ms et 1 % low de 88,11 à 14,40 images/s. L'exposition
reste à +0,10 EV, les effets par défaut et la capture de pointeur restent
actifs. Ces compteurs ne donnent pas une cadence de présentation GPU.

La [répétition instrumentée 110](../../../../.tidewater-integration/studio-tidewater-fullscreen-traversal-profile-110/tidewater-fullscreen-traversal.json)
retrouve 143,00 images/s à l'arrêt et 18,52 pendant la marche. Son
[profil CPU](../../../../.tidewater-integration/studio-tidewater-fullscreen-traversal-profile-110/studio-cpu-profile.json)
couvre 5 394,90 ms : spécialisation des matériaux 1 220,18 ms (22,62 %),
mise en file 861,34 ms (15,97 %), appels `writeBuffer` 369,26 ms (6,84 %).
La sélection végétale et le packing des vertices apparaissent également
parmi les coûts principaux.

Le [comptage WebGPU](../../../../.tidewater-integration/studio-tidewater-fullscreen-traversal-profile-110/studio-webgpu-writes.json)
relève 105 images encodées, avec 100 écritures dans chacun de deux slabs
de vertices et 200 dans un slab d'indices. Le code du terrain et de l'océan
recréait effectivement un maillage pour chaque pose de caméra différente,
y compris lorsque les tuiles sélectionnées étaient identiques.

Les deux smokes complets sont **échoués**, et leurs reçus restent conservés :
109 expire dans l'attente d'un rechargement auditable après retour en édition ;
110 échoue lors de la restauration d'un audit déjà absent. La forêt n'a
pas été mesurée dans ces deux séries. Ces échecs du parcours n'annulent pas
les données déjà acquises, mais interdisent de les présenter comme des
validations complètes du harnais ou de la forêt.

## Conservation exacte des maillages, candidat 112

`ViewGeometry` expose la liste ordonnée des tuiles et la construction de
leur maillage. Le Playtest Studio conserve sa dernière sélection du terrain ;
le récepteur géométrique de l'océan conserve également sa sélection. Les
maillages sont remplacés seulement si cette liste exacte change. Le calcul
de sélection reste exécuté avec la vraie caméra et l'ordre de la source
est conservé ; aucune distance, tuile, résolution ou déformation GPU n'est
réduite. Le passage hors champ conserve la gestion de visibilité.

Les [quatre contrôles CPU](../../../../.tidewater-integration/resident-geometry-cpu-source-112.run.log)
passent : sélection répétée sans nouvel asset, nouvelle sélection avec
vertices et indices conformes à la source, vue vide puis restauration,
et changement d'ordre non ignoré. Le
[harness](../../../../.tidewater-integration/resident-geometry-cpu-source-112.rs)
utilise Bevy réel, le CDLOD canonique directement depuis son fichier source
et les fonctions d'océan extraites du fichier courant. Ce contrôle ne
remplace pas une exécution GPU ni un build natif complet du moteur.

La compilation WASM release/offline/locked termine en 6 min 58 s ; le shell
Tauri termine en 19,22 s. Le
[reçu 112](../../../../.tidewater-integration/studio-tidewater-resident-geometry-candidate-112/build-proof.json)
épingle l'exécutable
`3161b5f3c2c645422d77fee065a3188e5267fb76bd0ded8b2a11df31713bef6a`
et le WASM
`34fbbf3664170cc07b0140a3e8b71c45db1508357be498c4c246ba64f349ac1c`.
Le reçu demeure `BUILD_ONLY` ; aucun gain de marche n'est acquis par la
compilation ou les tests CPU seuls.

## Parcours forêt 113 : fonctionnel, cadence insuffisante

Le [smoke 113](../../../../.tidewater-integration/studio-tidewater-resident-geometry-forest-113/fish-stand-studio-smoke-ledger.json)
passe, retour à l'édition compris. Il démarre directement avec le contrôle
temporaire « Jouer à cet endroit », X = −120, Z = −340. Il conserve les
contrôles de placement, sauvegarde, réouverture, déplacement, FFT, optique
de l'océan, ressac et sources intactes : `source_preserved=true`. Le
[harness local](../../../../.tidewater-integration/studio-forest-walking-profile-113.ps1)
utilise le parcours établi ; il ajoute une disposition diagnostique du
canvas en 1920 × 1080 et des mesures pendant les touches réellement tenues.
Ce n'est pas la livraison d'une commande plein écran du produit.

Le [reçu détaillé](../../../../.tidewater-integration/studio-tidewater-resident-geometry-forest-113/tidewater-forest-walking.json)
contient douze segments de cinq secondes, W/D/S/A répétés trois fois. La
[synthèse](../../../../.tidewater-integration/studio-tidewater-resident-geometry-forest-113/forest-walking-summary.json)
relève 60,90 s de mesures et au moins 257,65 m sur X/Z, somme des distances
entre positions échantillonnées et non intégrale du trajet. Les douze fins
de segment sont au sol ; la génération d'ombres reste à 1, les trois
cascades avancent et aucune récupération n'est comptée. Les captures
[début](../../../../.tidewater-integration/studio-tidewater-resident-geometry-forest-113/fullscreen-forest-start.png)
et [fin](../../../../.tidewater-integration/studio-tidewater-resident-geometry-forest-113/fullscreen-forest-end.png)
ont été inspectées : relief proche, arbres, fougères, ombres et ciel restent
présents pendant une descente d'environ 48 m d'altitude. Il ne s'agit pas
d'une paire de fidélité avec caméra et temps source identiques.

La cadence rAF est **23,75 images/s à l'arrêt dans cette forêt**. Le premier
segment instrumenté donne 19,43 ; les onze suivants donnent **21,42–22,74**,
avec p95 de 48,70–55,50 ms. La moyenne pondérée des douze segments est
21,76 images/s. La forêt est donc fonctionnellement parcourue mais sa
performance reste insuffisante ; cette preuve ne vaut pas qualification à
60 images/s ni benchmark natif.

Le [profil de marche en forêt](../../../../.tidewater-integration/studio-tidewater-resident-geometry-forest-113/studio-cpu-profile.json)
sur 5 449,66 ms retrouve 1 399,01 ms de spécialisation des matériaux (25,67 %)
et 978,24 ms de mise en file (17,95 %). Le
[comptage des écritures](../../../../.tidewater-integration/studio-tidewater-resident-geometry-forest-113/studio-webgpu-writes.json)
relève 110 images encodées, avec 54 et 4 écritures dans les deux slabs de
vertices, et 58 écritures d'indices. La conservation évite donc des
renouvellements, mais la préparation des matériaux reste le coût principal
mesuré. Les scènes 110 et 113 diffèrent ; leurs cadences ne constituent pas
une comparaison avant/après à scène identique.

## Régression visuelle 114

La [capture Village du 112](../../../../.tidewater-integration/studio-tidewater-resident-geometry-village-114/tidewater-studio-village-reference.json)
passe en 1920 × 1080 avec source intacte et trois cascades animées. L'image
à +0,10 EV a été inspectée : les ombres demeurent, sans retour de la grande
zone sombre corrigée précédemment. La
[comparaison](../../../../.tidewater-integration/studio-tidewater-resident-geometry-village-114/comparison-010-00.json)
donne 9,52/255 d'erreur absolue de luminance et une moyenne de 108,49 contre
109,23 pour la source, proches du 107. Les états temporels restent différents.

Le formatage ciblé et le contrôle des espaces passent. Les essais graphiques
sont terminés et leurs processus sont fermés. Restent ouverts la préparation
des matériaux pendant le mouvement, la fluidité de la forêt, la parité animée,
le benchmark complet natif/WebGPU et les autres fonctions du plan.
