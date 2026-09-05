# Gate 2 — murs procéduraux persistants et projection terrain

Date : 5 septembre 2026

Branche : `Rust`

Candidat source : `e31f822faa5fbe612017ff1efcf206a76a86038b`

Arbre source : `2416fec8684dd08fb4bc86824cf75a219b08ff8b`

Parent source : `c6024e5045d4faa846834454aa8c08eee8a20d29`

**R2-025 est `PASS` sur qualification fonctionnelle automatisée de murs procéduraux persistants,
bornés et projetés sur le terrain effectif. Gate 2 atteint 24/43 et reste ouverte.** Ce verdict ne
qualifie ni l’inspection visuelle native, ni les builds Release, le GPU, la performance, PresentMon
ou la CI distante.

Le manifeste lié aux reçus se trouve dans
[`PROCEDURAL_WALLS_ARTIFACTS.json`](PROCEDURAL_WALLS_ARTIFACTS.json). Aucun journal brut n’a été
capturé : `artifacts` reste volontairement vide. `Test-Gate2WallEvidence.ps1` lie la preuve au commit,
à son arbre, à son parent et à l’inventaire exact de 25 fichiers source, puis contrôle les bornes,
tests et résultats différés sans relancer les builds lourds.

## Source persistante et commande

Le workspace passe de la version 6 à la version 7 et ajoute, pour chaque zone, un document
`forge.wall` v1 indépendant du renderer. Il contient au plus 64 affectations strictement triées et
uniques par `PathId`, sous un budget JSON de 64 Kio. Chaque mur utilise l’algorithme
`TerrainDrapedSolidV1`, la surface logique Forge `ForgeStoneV1`, une hauteur comprise entre 0,25 et
8 m et une épaisseur comprise entre 0,10 et 2 m.

Un mur doit référencer un chemin existant et ouvert. Le budget de 8 192 m porte sur la longueur
cumulée des polylignes de contrôle des chemins murés, pas sur la longueur d’arc des splines. Le même
`PathId` ne peut pas porter simultanément un mur et une route ou une rivière. En revanche, des objets
portés par des chemins distincts peuvent se croiser, se toucher à une extrémité, être tangents ou
colinéaires : leurs meshes peuvent se superposer et aucune ouverture, jonction ou intersection
dérivée n’est créée par R2-025.

`WallUpsertCommand` crée ou remplace un profil au moyen d’une édition liée à la révision et aux cinq
hashes terrain, chemins, routes, rivières et murs. Validation numérique, chemin absent/fermé,
conflits, no-op, mauvais projet, stale ou hash périmé échouent avant mutation. Une commande acceptée
ajoute exactement une révision et une entrée d’historique. Undo/redo, redessin du chemin,
sauvegarde et réouverture conservent le document canonique et redérivent le même résultat.

Les workspaces v1 à v6 restent lisibles. Leur ouverture ajoute en mémoire un document de murs vide
sans réécrire le snapshot historique ; les documents déjà connus et leurs hashes embarqués sont
préservés. Le hash du workspace migré change intentionnellement parce que le schéma v7 et le nouveau
document vide deviennent une partie de sa source canonique.

## Génération déterministe

`WallGenerationPlan::from_documents` reçoit le terrain **effectif** dérivé des rivières, les chemins
et les murs. La sortie v1 est strictement triée et liée aux hashes de ces trois documents. Chaque
échantillon du `GeneratedPath` produit une rangée de huit sommets dupliqués par face. Les bases gauche
et droite sont échantillonnées séparément sur le terrain effectif puis enfoncées de 0,02 m ; les
sommets sont exactement les bases augmentées de la hauteur du profil.

Le solide possède quatre bandes longitudinales et deux embouts, soit 24 indices par intervalle et
12 indices d’embouts, ainsi que huit sommets d’embouts par mur. Les normales sont dérivées
déterministement des triangles incidents. Les plafonds agrégés sont 16 384 rangées, 131 584 sommets
et 393 204 indices. Empreinte hors `[-256, 256[`, tangente invalide, triangle dégénéré, dépassement
ou arithmétique non représentable échouent sans clamp ni résultat partiel.

`validate_generated_walls` régénère le lot complet depuis les sources canoniques et compare version,
hashes, ordre, exhaustivité, identités, profils, surface, positions, normales, topologie et indices.
Positions et normales doivent être bit-identiques au résultat attendu. Les commandes chemin et
rivière revalident les murs avant d’exposer une révision. Le résultat rivière et son terrain effectif
déjà calculés sont réutilisés ; aucun second calcul rivière n’est lancé dans ces flux, et une zone
sans mur sort avant toute projection rivière propre aux murs.

## Studio, cache et viewport

Le Studio expose un formulaire par chemin ouvert pour saisir hauteur et épaisseur. L’algorithme et
la surface logique restent les variantes Forge v1 fixes ; les profils route, rivière et mur sont
mutuellement exclusifs sur un même chemin. La génération potentiellement coûteuse est exécutée sur
le worker bloquant natif, hors de la boucle asynchrone principale.

L’`AreaPreview` génère les murs depuis le même terrain effectif que le terrain visible, la végétation,
les chemins et les routes. Une édition wall-only envoie zéro chunk terrain, conserve terrain
effectif, végétation, chemins, routes et rivières, et ne change que le lot de murs. Une retouche parmi
trois murs reconstruit un seul mesh et conserve les deux handles inchangés. Une édition rivière
recalcule au contraire les bases des murs contre le nouveau terrain effectif et maintient les hashes
corrélés.

Le viewport reçoit le `WallDocument` canonique avec `GeneratedWalls`. Il vérifie les sources,
budgets, chemins ouverts, profils, topologie, indices, géométrie, normales et rejoue la validation
exacte de `forge_world` avant le staging. Un payload translaté ou réorienté de manière cohérente, une
source falsifiée ou un conflit de rôle est refusé en conservant la scène, les handles et les compteurs
d’upload présentés. Le matériau pierre est un `StandardMaterial` Forge opaque et procédural, sans UV
ni texture externe.

## Compatibilité et provenance

Le lot modifie exactement 25 fichiers source. Aucun `Cargo.toml`, `Cargo.lock`, asset ou chemin/contenu
Cadle n’est ajouté ou modifié ; aucune dépendance runtime, dev ou version tierce ne change. Le nouveau
format persistant est limité au document `forge.wall` v1 et à l’évolution contrôlée du workspace v7.

## Preuves automatisées acquises

Les quatre suites de bibliothèque totalisent **608 tests PASS** :

- `cargo test -p forge_project --lib` : **149/149 PASS** ;
- `cargo test -p forge_world --lib` : **133/133 PASS** ;
- `cargo test -p forge_engine --lib` : **201/201 PASS** ;
- `cargo test -p forge_studio --lib` : **125/125 PASS**.

Le binaire Studio ajoute `cargo test -p forge_studio --bin forge-studio` : **6/6 PASS**, hors du total
des bibliothèques.

Les contrôles de compilation et lint acquis sont :

- Studio : check WASM et Clippy natif/WASM strict **PASS** ;
- Engine : Clippy natif `--all-targets --all-features` et Clippy WASM `--lib` **PASS** ;
- Project : Clippy natif strict acquis puis resté valable, aucun fichier Project n’ayant changé lors
  du correctif final limité à World ;
- World : Clippy natif `--all-targets` strict **PASS** ;
- Project + World : Clippy WASM des bibliothèques produit **PASS** ;
- `trunk build --locked --offline` avec `NO_COLOR=true` : **PASS** en profil debug après les
  récupérations environnementales détaillées ci-dessous ;
- `cargo fmt --all -- --check` et `git diff --check` : **PASS**.

Les reçus Engine et Project/World WASM portent explicitement sur `--lib`, pas sur `--all-targets` ;
aucun verdict sur leurs harnais de tests WASM complets n’est revendiqué.

Deux premières invocations Trunk ont été rejetées **avant tout build** parce que `NO_COLOR=1` n’était
pas accepté. Après correction en `NO_COLOR=true`, le premier build réel a échoué uniquement par
manque d’espace disque. La commande ciblée
`cargo clean -p forge_studio --target wasm32-unknown-unknown` a supprimé 1 259 fichiers, soit 3,4 Gio
d’artefacts régénérables ; le second build réel a ensuite réussi. Aucun journal brut n’est revendiqué.

Les tests couvrent notamment :

- document/hash JSON, bornes numériques, tri, unicité, budget de longueur et migration v1 à v6 ;
- création/retouche, une révision, inverse/rejeu, sauvegarde/réouverture et redessin ;
- déterminisme, terrain effectif, bases enfoncées, hauteurs, topologie, normales et budgets agrégés ;
- validation exacte et exhaustive, falsification géométrique et rejets atomiques ;
- contacts entre chemins distincts autorisés sans auto-jonction et conflits de rôles sur le même
  chemin refusés ;
- cache wall-only sans reconstruction terrain, conservation des projections et handles, puis
  régénération des bases après une modification rivière ;
- corrélation des sources Studio/viewport et préservation de la scène après rejet.

## Frontières et résultats non exécutés

- UI/inspection visuelle native : **NOT RUN**, regroupée dans une passe ultérieure à la demande du
  propriétaire ;
- build natif Release et build Trunk Release : **NOT RUN** ;
- GPU et qualification performance : **NOT RUN** ;
- CI distante : **NOT RUN — infrastructure billing**, dérogation ADR-0006 ;
- PresentMon : **NOT RUN** ;
- suppression/renommage de mur, ouvertures, portes, tours, collisions, navigation et jonctions
  automatiques : non livrés ;
- aucun asset ou contenu Cadle n’est copié ou modifié ;
- la qualification visuelle des interactions Gate 2 reste regroupée pour plus tard.

La prochaine tranche fonctionnelle est `R2-026`, navigateur d’assets.
