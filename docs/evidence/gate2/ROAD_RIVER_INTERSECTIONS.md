# Gate 2 — intersections route-rivière bornées et gués de terrain

Date : 5 septembre 2026

Branche : `Rust`

Candidat source : `5b05553e374ec59afd115f30335cb8039ef19452`

Arbre source : `6e733362bb3ed888b08f45409be7522f0a4744c7`

Parent source : `2717f0c1a5997e3662c9a6d5063e4bcbbdd342f0`

**R2-024 est `PASS` sur qualification fonctionnelle automatisée d’une intersection route-rivière
bornée, dérivée sous la forme unique `TerrainFordV1`. Gate 2 atteint 23/43 et reste ouverte.** Ce
verdict ne qualifie ni un système générique d’intersections, ni une inspection visuelle native, ni
les builds Release, le GPU, la performance, PresentMon ou la CI distante.

Le manifeste lié aux reçus se trouve dans
[`ROAD_RIVER_INTERSECTIONS_ARTIFACTS.json`](ROAD_RIVER_INTERSECTIONS_ARTIFACTS.json). Aucun journal
brut n’a été capturé pour ce lot : `artifacts` reste volontairement vide. Le script
`Test-Gate2IntersectionEvidence.ps1` lie ces preuves au commit, à son arbre, à son parent et à son
inventaire exact de 14 fichiers, puis contrôle les contrats et l’absence de verdicts interdits.

## Contrat livré

R2-024 ne crée ni document persistant, ni nouvelle commande d’édition, ni nouveau schéma de
workspace. Le workspace reste en version 6. Un gué est une projection dérivée des documents
`forge.path`, `forge.road` et `forge.river` existants, au fil des commandes de création ou redessin
de chemin et de profil route/rivière déjà qualifiées. Le terrain canonique reste intact ; le terrain
effectif sculpté par la rivière reste dérivé et n’est jamais persisté.

`GeneratedRivers` passe à la génération version 2 et transporte une liste
`road_river_intersections`. Le seul type livré est `TerrainFordV1`. Chaque entrée lie deux `PathId`
distincts et contient position XZ, angle aigu de croisement, hauteur de surface de route et hauteur
d’eau. La liste est strictement triée et unique par `(river_path_id, road_path_id)`, exhaustive par
rapport aux routes et rivières générées, et bornée à 64 entrées.

Une paire route-rivière est acceptée uniquement si ses lignes se croisent une fois, de manière
transversale et isolée. L’angle aigu est compris dans l’intervalle inclusif 30 à 90 degrés. Le
croisement doit rester suffisamment loin des extrémités des deux chemins et le chevauchement des
empreintes doit être local au point de traversée. Tangence, colinéarité, absence ou multiplicité de
croisements, angle trop faible, proximité d’une extrémité et overlap prolongé échouent avant
mutation. Les overlaps rivière-rivière et l’affectation route+rivière au même `PathId` restent
refusés.

Les hauteurs du gué sont échantillonnées sur les surfaces générées réellement présentées. La
hauteur d’eau doit dépasser celle de la route d’au moins 0,05 m. Position, angle et hauteurs sont
comparés bit à bit avec les valeurs redérivées ; une omission, insertion ou modification de
métadonnée est refusée. Les hashes terrain canonique, terrain effectif, chemins, routes et rivières,
ainsi que la version de génération, doivent rester corrélés.

La validation partagée `validate_generated_road_river_intersections` reconstruit l’ensemble complet
des gués à partir des lignes centrales générées. Avant cela, les lignes centrales de rivière et de
route sont liées bit à bit au rééchantillonnage déterministe des `GeneratedPaths`, sous leurs budgets
agrégés respectifs de 16 384 rangées. Le moteur lie en outre les chemins au `TerrainDocument` et au
`PathDocument` canoniques, et exige que les ensembles générés de routes et rivières correspondent
exactement aux `RoadDocument` et `RiverDocument` canoniques.

## Frontière de confiance du viewport

Le viewport est alimenté par le worker natif interne du Studio ; ce canal interne constitue la
frontière de confiance de R2-024 et n’est pas présenté comme une interface réseau hostile. Après une
réponse asynchrone normale, le Studio relit zone, révision et hashes terrain/chemins/routes/rivières
avant d’armer le candidat. L’aperçu de densité utilise la zone déjà validée et effectue les mêmes
corrélations. Les documents canoniques sont donc ceux de la zone courante, jamais des documents
déclarés par le payload dérivé.

Le viewport **ne rejoue pas intégralement `RiverGenerationPlan`**. Il valide séparément les recettes
canoniques, le rééchantillonnage exact des chemins, les ensembles complets de routes/rivières, leurs
meshes bornés, puis redérive exhaustivement les gués. Cette validation fail-closed couvre les
omissions, falsifications et sources périmées testées ; elle ne doit pas être décrite comme une
seconde exécution complète et indépendante de toute la génération de rivière. Une requête invalide
préserve le terrain, les chemins, routes, rivières, marqueur, handles, compte d’assets et hashes déjà
présentés.

## Persistance et cache Studio

La création route puis rivière et la création rivière puis route produisent le même gué déterministe.
Chaque commande acceptée ajoute exactement une révision et une entrée d’historique. Un redessin
séparé vers croisé crée une seule révision ; undo supprime le gué dérivé et redo restaure exactement
ses métadonnées. Sauvegarde puis réouverture redérivent le même hash sans persister le gué.

Lorsqu’une rivière existe déjà, l’ajout ou la retouche de la route traversante conserve le terrain
effectif, la végétation, les chemins et le mesh d’eau. Le payload terrain partiel contient zéro chunk,
donc le viewport n’upload ni ne reconstruit de mesh terrain. La route seule est reconstruite ; le gué
et ses hashes sont ajoutés ou actualisés. Le cache conserve 64 chunks de végétation et vérifie la
parité complète avec une génération froide.

## Compatibilité et provenance

Le schéma workspace reste v6 et les documents path/road/river gardent leurs versions existantes. Il
n’existe aucun nouveau format persistant et les anciens snapshots ne sont pas réécrits. Aucun asset,
contenu Cadle, manifeste Cargo, lockfile ou dépendance n’est ajouté ou modifié.

## Preuves automatisées acquises

Les quatre suites de bibliothèque totalisent **576 tests PASS** :

- `cargo test -p forge_project --lib` : **137/137 PASS** ;
- `cargo test -p forge_world --lib` : **126/126 PASS** ;
- `cargo test -p forge_engine --lib` : **193/193 PASS** ;
- `cargo test -p forge_studio --lib` : **120/120 PASS**.

Le binaire Studio ajoute `cargo test -p forge_studio --bin forge-studio` : **6/6 PASS** ; ce reçu
reste séparé du total des bibliothèques.

Les contrôles de compilation et lint acquis sont :

- `forge_studio` : check WASM et Clippy natif/WASM strict **PASS** ;
- `forge_engine` : Clippy natif `--all-targets --all-features` et Clippy WASM de la bibliothèque
  produit **PASS** ;
- `forge_project` + `forge_world` : Clippy natif `--all-targets` et Clippy WASM des bibliothèques
  produit **PASS** ;
- `trunk build --locked --offline` avec `NO_COLOR=true` : **PASS** en profil debug après récupération
  d’un manque d’espace disque ;
- `cargo fmt --all -- --check` et `git diff --check` : **PASS**.

Les reçus Engine et Project/World WASM portent explicitement sur `--lib`, pas sur `--all-targets`.
Le paquet ne revendique donc aucun verdict sur leurs harnais de tests WASM complets.

La première tentative Trunk a échoué uniquement avec une erreur d’espace disque. La commande ciblée
`cargo clean -p forge_studio --target wasm32-unknown-unknown` a supprimé 4,5 Gio d’artefacts WASM
régénérables, puis la seconde tentative Trunk a réussi. Aucun journal brut de ces tentatives n’est
revendiqué.

Les tests et inspections statiques couvrent notamment :

- déterminisme, ordre route→rivière et rivière→route, angle, profondeur et surfaces visibles ;
- tri, unicité, exhaustivité, budgets et rejets géométriques atomiques ;
- liaison exacte des lignes centrales générées aux sources terrain/chemins canoniques ;
- rejet de métadonnées de gué falsifiées, omises, dupliquées ou périmées ;
- préservation intégrale de la scène Engine après rejet ;
- une révision par commande, undo/redo, sauvegarde/réouverture et redessin séparé→croisé ;
- cache road-only sans reconstruction terrain, avec végétation, chemins et eau réutilisés ;
- corrélation des hashes et relecture de la zone courante avant présentation.

## Frontières et résultats non exécutés

- UI/inspection visuelle native : **NOT RUN**, regroupée dans une passe ultérieure à la demande du
  propriétaire ;
- build natif Release et build Trunk Release : **NOT RUN** ;
- GPU et qualification performance : **NOT RUN** ;
- CI distante : **NOT RUN — infrastructure billing**, dérogation ADR-0006 ;
- PresentMon : **NOT RUN** ;
- intersections génériques, ponts, tunnels, jonctions route-route/rivière-rivière et navigation :
  non livrés ;
- le viewport ne rejoue pas intégralement `RiverGenerationPlan` et reste dans sa frontière de
  confiance de worker natif interne ;
- aucun asset ou contenu Cadle n’est copié ou modifié ;
- la qualification visuelle des interactions Gate 2 reste regroupée pour plus tard.

La prochaine tranche fonctionnelle est `R2-025`, clôture ou mur de preuve.
