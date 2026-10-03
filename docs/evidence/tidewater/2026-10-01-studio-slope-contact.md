# Tidewater — contact de la capsule sur les pentes

Suite : [milieu sous-marin et comparaisons figées dans Studio](2026-10-01-studio-immersion.md).

Suite du [parcours de nage Studio](2026-10-01-studio-swimming.md). Le parcours
de forêt 132 avait révélé une vitesse verticale de −216,145 m/s alors que
l'ancre restait à 0,126 m du terrain. Le smoke d'interface ne qualifiait donc
pas la locomotion.

## Reproduction et correction

Le test ECS utilise le vrai KCC Rapier, le même profil de capsule et les
updates fixes à 60 Hz. Sur une rampe à 40°, Rapier confirme le contact ;
l'adaptation précédente le perd parce que le pied source est à plus de
6 cm du terrain. Le [test avant correction](../../../../.tidewater-integration/studio-slope-test-before-133-repair.log)
échoue avec un contact Rapier vrai, un contact source faux, une vitesse Y de
−14,87849 m/s et un dégagement de 0,104666 m.

`tidewater_player.rs` utilise maintenant l'ancre résolue par Rapier comme
support du pied source lorsque le KCC confirme un contact non glissant et
que la vitesse verticale n'est pas positive. La capsule conserve son
dégagement sur pente ; la hauteur réelle du terrain/raycast reste séparée.
La normalisation existante de la petite peau de contact sur terrain plat est
conservée. Les règles de locomotion source ne sont pas modifiées.

Les diagnostics Studio distinguent désormais le contact source historique,
le contact physique publié et le contact/glissement Rapier avant adaptation.
Le drapeau source historique de marche peut rester vrai pendant la nage : le
contact physique publié est celui à utiliser pour ce mode.

Le premier essai de fixture était invalide : capsule initialement enfoncée
dans la rampe et raster plat. Il reste dans le [journal initial](../../../../.tidewater-integration/studio-slope-test-before-133.log).
Une autre exécution a détecté le décalage d'un demi-échantillon dans le raster
de la fixture inversée ; il a été corrigé conformément aux coordonnées de
cellules centrées avant la validation finale. Ces échecs sont conservés et ne
sont pas présentés comme des preuves de comportement correct.

## Validation native

Les [dix tests Tidewater](../../../../.tidewater-integration/studio-slope-test-after-133-repair.log)
passent. Sur les rampes à +40° et −40°, le contact source et Rapier reste vrai,
la vitesse verticale vaut zéro et le dégagement vaut environ 0,104653 m.
Après installation du contact, le déplacement à l'arrêt reste inférieur à
2 mm pendant les 90 updates suivants. Le saut s'élève de plus de 40 cm, puis
retombe ; une pente à 60° glisse et conserve sa gravité.

La [régression finale de contrôleur](../../../../.tidewater-integration/studio-slope-player-regression-final-133.log)
passe : **30 tests, zéro échec**. Elle comprend le contrôleur générique, les
contacts, marches, sauts, pentes, identités Rapier, transitions marche/nage et
échelles Tidewater. Il s'agit de tests physiques natifs, pas d'une gate globale
ni d'une preuve de parité de toute l'île.

Le [build WebGPU final](../../../../.tidewater-integration/studio-slope-build-final-134.log)
passe avec `--no-default-features --features web`, en 7 min 38 s. Les
[empreintes finales](../../../../.tidewater-integration/studio-slope-inputs-final-134.json)
identifient les sources utilisées. Une première version construite avait
introduit deux diagnostics Clippy supplémentaires ; les observations physiques
ont été regroupées et le calcul de support extrait, puis les trente tests ont
été rejoués. Le [contrôle moteur final](../../../../.tidewater-integration/studio-slope-engine-clippy-final-134.log)
reste en échec à 64 diagnostics préexistants, sans diagnostic dans
`tidewater_player.rs`. Le contrôle avec dépendances s'arrête sur deux erreurs
dans `forge_project` ; il n'est pas vert non plus.

Le [contrôle Studio final](../../../../.tidewater-integration/studio-slope-studio-clippy-final-134.log)
reste rouge à 31 diagnostics. Aucun lint n'a été désactivé.

## Candidat Studio 134

L'[empaquetage](../../../../.tidewater-integration/studio-slope-package-134.log)
passe ; le [reçu](../../../../.tidewater-integration/studio-tidewater-slope-candidate-134/build-proof.json)
reste correctement `BUILD_ONLY`, distinct des preuves de runtime :

- exécutable : `700415ad6caaec69ee076e5ccacfc8c1490f15dc2be943f788267541567e6702` ;
- WASM : `9bd799d25243673a531c25f9052fa9889450c577da5c861d14b398e07ea84112`.

## Forêt Studio 135

Le [parcours complet](../../../../.tidewater-integration/studio-tidewater-slope-forest-135/fish-stand-studio-smoke-ledger.json)
passe : 1920 × 1080, douze segments de cinq secondes, sauvegarde/reouverture et
retour à l'édition, sources intactes. Aucun compilateur ne tourne pendant les
mesures. La [vérification continue](../../../../.tidewater-integration/studio-tidewater-slope-forest-135/slope-continuous-verification.json)
passe : 295 relevés à 250 ms, dont 144 contacts porteurs Rapier non glissants,
zéro contact perdu et vitesse verticale nulle sur ces 144 contacts. Le début du
parcours montre un dégagement de 0,180 m avec contact vrai et vitesse Y nulle ;
la première fin de segment, qui révélait le défaut du 132, donne désormais
0,145 m avec contact vrai et vitesse Y nulle.

La vitesse Y minimale du parcours vaut encore −26,160 m/s dans une phase de
glissement déclarée par Rapier. Ce cas est distinct du contact porteur corrigé ;
la preuve ne qualifie ni toutes les pentes ni chaque tick fixe de l'île. Les
[requêtes GPU aux douze fins de segment](../../../../.tidewater-integration/studio-tidewater-slope-forest-135/forest-water-query-verification.json)
passent : deux points, aucun rejet, étapes croissantes, écart point joueur/ancre
maximal de 0,158 m. Il s'agit d'une borne aux fins de segment ; pendant les
glissements rapides, le retard de lecture peut produire un écart supérieur.

La [synthèse de rendu](../../../../.tidewater-integration/studio-tidewater-slope-forest-135/plant-cache-forest-summary.json)
donne 127,54 images/s rAF à l'arrêt puis **112,91** sur onze segments non
profilés. Les p95 par segment vont de 15,5 à 16 ms, les 1 % low de 36,14 à
61,10 images/s. Les trois cascades progressent sans récupération ni changement
de génération ; le compteur d'éclairage reste à 2. Les trajectoires ont changé
avec la correction physique : ces valeurs ne prouvent pas un gain de rendu par
rapport au 129, ni la présentation GPU. La [capture finale](../../../../.tidewater-integration/studio-tidewater-slope-forest-135/fullscreen-forest-end.png)
a été inspectée : végétation, terrain et ombres sont visibles.

## Nage et qualification restante

Le [parcours côtier 136](../../../../.tidewater-integration/studio-tidewater-slope-swimming-136/fish-stand-studio-smoke-ledger.json)
et sa [vérification stricte](../../../../.tidewater-integration/studio-tidewater-slope-swimming-136/swimming-verification.json)
passent, avec le même candidat 134 :

| Étape | Mode | Contact physique | Ancre Y | Surface Y |
| --- | --- | --- | ---: | ---: |
| Plage | marche | oui | 0,396 | 0,000 |
| Flottaison | nage | non | −0,488 | −0,392 |
| Plongée C | nage | non | −0,841 | 0,364 |
| Maintien sans entrée verticale | nage | non | −1,493 | −0,540 |
| Remontée Espace | nage | non | −0,106 | −0,075 |
| Plongée Ctrl | nage | non | −1,187 | −0,412 |
| Seconde remontée | nage | non | −0,242 | −0,237 |
| Retour plage | marche | oui | 0,503 | 0,000 |

Le point GPU joueur suit un déplacement de 52,211 m jusqu'à la flottaison.
Les huit étapes gardent deux points GPU, aucun rejet, des étapes croissantes et
une heure exactement promue de f32 en f64. Aux instantanés contrôlés, l'écart
point/ancre maximal vaut 0,000031 m. Une entrée en nage et un retour en marche
sont observés. Au maintien, la vitesse Y vaut −0,000023 m/s et le dégagement au
fond 0,920 m. Les profondeurs instantanées dépendent aussi de la vague ; elles
ne représentent pas seulement la distance verticale parcourue par le joueur.
La sauvegarde/reouverture et le retour à l'édition passent ; le hash source
reste `50bfd655a7fb4f5a61e4593240ea86dc8d13136d82d558d34737a41b4c6c8720`.

La [capture sous la surface](../../../../.tidewater-integration/studio-tidewater-slope-swimming-136/swim-neutral.png)
a été inspectée : le fond demeure trop clair et le flare reste visible sous
l'eau. La correction physique n'ajoute pas encore la passe d'immersion.

Priorité suivante : raccorder la passe d'immersion, son masque et l'état
sous-marin des effets atmosphériques, puis rejouer un parcours avec captures
appairées au-dessus/sous la surface. Les pentes raides, les échelles raccordées
à Studio, les autres comportements du plan et la parité complète restent
ouverts. L'objectif Tidewater reste actif.
