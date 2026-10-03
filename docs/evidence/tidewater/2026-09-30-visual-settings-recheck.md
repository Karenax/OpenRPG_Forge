# Tidewater — contrôle du rendu et des paramètres graphiques

Le site public `https://dgreenheck.github.io/tidewater/` et le dépôt
`dgreenheck/tidewater` ont été revérifiés le 30 septembre 2026. La tête publique
reste `4811ba48d795197de5621985f404e765c0b7c0ef`. Le projet source emploie
son propre moteur WebGPU/WGSL, sans Three.js. La conversion Forge utilise Rust,
Bevy et WebGPU ; la langue d'implémentation ne garantit pas une meilleure image.

La capture fournie par le propriétaire montre un village source plus fini : ciel
et végétation mieux fondus, transitions lumineuses plus douces, détails du quai
et des matériaux plus riches. La capture n'a pas le même point de vue ni la même
heure qu'une capture Forge ; ce verdict reste visuel. La
[paire village](2026-09-30-studio-village-grass-pair.md) du commit source figé
`1438b1abfcaee3267092b75573014f4d9b4a983c` garde une différence moyenne
de 14,02 niveaux de luminance sRGB par pixel échantillonné après calibration
Studio à 0,40 EV. Cette paire précède l'import éditable des passerelles et ne
mesure donc pas le candidat actuel. La [nouvelle paire du candidat 093](2026-09-30-studio-village-current-pair.md)
mesure 13,10 niveaux au même réglage, tout en montrant encore une image Studio
plus claire et plusieurs accessoires de place absents. La
[paire côte native](2026-09-29-graphics-and-fish-stand-025.md) affiche des
luminosités globales voisines, mais des détails plus durs dans Forge. Aucun de
ces chiffres ne qualifie la parité du site public plus récent.

Les dix commandes visibles dans l'onglet « Effects » de la capture source sont
déjà présentes et reliées au rendu Rust : occlusion ambiante, lumière rebond,
netteté, flou de mouvement, bloom, flare solaire, saturation, contraste,
vignette et grain. Le menu natif `H`/`F10` en compte quinze et conserve les
valeurs sur disque ; le [candidat 026](2026-09-29-windows-playtest-package-026.md)
a vérifié son ouverture, sa remise à zéro et la relecture du profil après
relance. Le [Playtest Studio](2026-09-30-studio-graphics-controls.md) en expose
quatorze, dont les dix effets de la capture, avec
[persistance locale](2026-09-30-studio-graphics-persistence.md). Le Studio
n'expose pas le TAA expérimental du binaire natif. Les autres onglets source
(mer, heure, météo et qualité) ne forment pas encore un panneau équivalent.

Les curseurs identiques ne produisent pas forcément le même rendu. Tidewater
public utilise un TAAU propre au projet, avec reconstruction temporelle et
traitement de l'eau ; Forge garde le FXAA par défaut et son TAA Bevy reste
expérimental et arrêté par défaut après [essai A/B](2026-09-28-temporal-aa.md).
La netteté source vaut 0,45, contre 0,20 dans Forge, mais les algorithmes sont
différents : recopier 0,45 sans nouvelle image A/B n'établirait pas la parité.
Les matières, les ombres et les éléments du quai demandent aussi une comparaison
au même cadrage. La prochaine preuve visuelle doit apparier caméra, heure,
météo, résolution, contenu et convergence temporelle sur le site public et sur
un nouveau Playtest Forge.
