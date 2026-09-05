# Gate 2 — rivière persistante, carving non destructif et terrain effectif

Date : 5 septembre 2026

Branche : `Rust`

Candidat source : `7316e552d25db173af515f572a3a8ccc24c2f90f`

Arbre source : `805da88d55d4327adf4d64ffd733a4f3f0d54765`

Parent source : `07d03acd42d4d8519b01c541d7a2df7fdc7b2441`

**R2-023 est `PASS` sur qualification fonctionnelle automatisée d’une rivière persistante, de son
carving non destructif et de sa projection atomique dans le viewport. Gate 2 atteint 22/43 et reste
ouverte.** Le verdict ne couvre ni inspection visuelle native, ni build Release, ni qualification
GPU/performance, ni résolution d’intersections R2-024.

Le manifeste lié aux reçus se trouve dans
[`RIVER_CARVING_ARTIFACTS.json`](RIVER_CARVING_ARTIFACTS.json). Aucun journal brut n’a été capturé
pour ce lot : `artifacts` reste volontairement vide. Le script `Test-Gate2RiverEvidence.ps1`
contrôle les reçus déclarés, l’inventaire exact des 27 fichiers du commit source, ses contrats, son
arbre et son parent, ainsi que l’absence de verdicts interdits.

## Contrat livré

Le workspace v6 contient pour chaque zone un document `forge.river` v1 indépendant du renderer.
Une rivière associe au plus un profil à un `PathId` existant et ouvert. Les entrées sont strictement
triées et uniques. Le document est borné à 16 rivières et 64 Kio de JSON. La largeur de surface doit
rester entre 2 et 32 m, la profondeur de lit entre 0,15 et 8 m, et la largeur de raccord de berge
entre 1 et 16 m.

Le budget de 8 192 m porte sur la longueur cumulée des **polylignes de contrôle** des chemins
auxquels une rivière est affectée. Ce n’est pas une borne de longueur d’arc des splines. Un chemin
absent ou fermé, un profil non fini ou hors bornes, une collection trop grande ou un budget dépassé
sont refusés avant mutation. Un même chemin ne peut pas porter simultanément route et rivière.
Jusqu’à R2-024, un chevauchement géométrique rivière/rivière ou rivière/route est refusé dans les
deux ordres de création, avec un budget maximal de 4 000 000 comparaisons. Il ne s’agit pas encore
d’un système d’intersections.

La projection dérivée `TerrainCarvedChannelV1` est reproductible. Elle subdivise la ligne centrale
avec une **corde horizontale** au plus égale à 2 m ; ce plafond ne constitue pas une borne de
longueur d’arc. La surface `ForgeWaterV1` produit un ruban de trois sommets par rangée et quatre
triangles par intervalle longitudinal. Sa hauteur reste identique sur une section et monotone non
croissante dans la direction du chemin, avec un dégagement de 0,04 m. Les plafonds agrégés sont
16 384 rangées, 49 152 sommets et 196 608 indices.

Le carving fabrique un `TerrainDocument` **effectif et dérivé**, jamais persisté à la place du
terrain canonique. Le lit et les berges utilisent un raccord `smoothstep`; la hauteur cible est
toujours plafonnée par la hauteur canonique, donc aucun échantillon n’est relevé. Le centre du lit
atteint la profondeur demandée et bascule vers le poids procédural terre, tandis que les berges
mélangent progressivement relief et matériau. Une projection vide restitue exactement le terrain
canonique. Le nombre d’échantillons de terrain influencés est borné à 65 536.

`GeneratedRivers` transporte les hashes des documents canonique terrain, chemins, routes et
rivières, ainsi que le hash du terrain effectif. La génération refuse les identités croisées, les
chemins manquants ou fermés, les sorties non finies, les budgets dépassés, les triangles dégénérés,
les overlaps interdits et les empreintes sortant de `[-256, 256[` au lieu de les clamper. Le document
candidat complet, son terrain dérivé et son mesh d’eau sont générés avant exposition d’un edit. Un
redessin de chemin revalide également routes et rivières avant qu’une révision existe.

Le Studio expose un formulaire étroit par chemin pour créer ou retoucher largeur, profondeur et
raccord de berge ; algorithme et surface restent les variantes Forge v1 fixes. Le client refuse déjà
un no-op, mais le service et le workspace restent l’autorité. Une commande acceptée produit
exactement une révision et une entrée undo. Stale, mauvais projet/zone, hashes source périmés,
chemin absent/fermé, conflit route, overlap, profil invalide et no-op échouent atomiquement.
Undo/redo, sauvegarde, réouverture et redessin ultérieur du chemin conservent le profil canonique.

L’`AreaPreview` calcule d’abord la rivière, puis utilise son terrain effectif pour les meshes terrain,
la végétation, les chemins et les routes. Une édition river-only prépare seulement les chunks de
terrain effectif modifiés, régénère la végétation avec conservation des chunks inchangés et maintient
tous les hashes dérivés corrélés. Le résolveur de promotion de végétation utilise lui aussi ce terrain
effectif : l’instance choisie correspond donc à la projection réellement visible, pas à l’ancien Y
canonique.

Les commandes susceptibles de générer une projection — promotion de végétation, création/redessin
de chemin, profil de route et profil de rivière — sont envoyées par l’entrée Tauri au worker bloquant
de l’exécuteur asynchrone. Elles ne bloquent plus la boucle asynchrone principale pendant leur calcul.

Le moteur valide version, hashes, ordre, profil, topologie, winding, géométrie f64, normales, surface
monotone et budgets avant le staging. Le terrain effectif et l’eau sont remplacés ensemble lors d’un
swap atomique. L’ancien affichage reste visible pendant le staging ; les handles inchangés sont
réutilisés, une rivière retirée n’est libérée qu’au swap, une relève annule proprement le candidat
obsolète et une réponse invalide préserve l’affichage courant sans fuite.

Le matériau d’eau est un `StandardMaterial` procédural Forge translucide sans texture externe.
Aucun asset, contenu Cadle, manifeste Cargo, lockfile ou dépendance n’est ajouté ou modifié.

## Compatibilité des sources

Le workspace passe du schéma 5 au schéma 6 uniquement pour intégrer le document de rivières. Les
workspaces v1 à v5 restent lisibles ; leur chargement ajoute en mémoire un document rivière vide sans
réécrire le snapshot d’origine. Les hashes embarqués terrain, végétation, densité, chemins et routes
restent identiques. Le hash du workspace migré change intentionnellement, puisque le schéma v6 et le
nouveau document vide font partie de sa source canonique.

## Preuves automatisées acquises

Les quatre suites de bibliothèque totalisent **564 tests PASS** :

- `cargo test -p forge_project --lib` : **137/137 PASS** ;
- `cargo test -p forge_world --lib` : **120/120 PASS** ;
- `cargo test -p forge_engine --lib` : **190/190 PASS** ;
- `cargo test -p forge_studio --lib` : **117/117 PASS**.

Le binaire Studio ajoute `cargo test -p forge_studio --bin forge-studio` : **6/6 PASS** ; ce reçu
reste séparé du total des bibliothèques.

Les contrôles de compilation et lint acquis sont :

- `forge_studio` : check WASM et Clippy natif/WASM strict **PASS** ;
- `forge_engine` : Clippy natif `--all-targets --all-features` et Clippy WASM de la bibliothèque
  produit **PASS** ;
- `forge_project` + `forge_world` : Clippy natif `--all-targets` et Clippy WASM des bibliothèques
  produit **PASS** ;
- `trunk build --locked --offline` avec `NO_COLOR=true` : **PASS** en profil debug ;
- `cargo fmt --all -- --check` et `git diff --check` : **PASS**.

Les reçus Engine et Project/World WASM portent explicitement sur `--lib`, pas sur `--all-targets`.
Le paquet ne revendique donc aucun verdict sur leurs harnais de tests WASM complets.

Les tests et inspections statiques couvrent notamment :

- JSON/hash canoniques, limites, tri, identité et migration workspace v5 vers v6 ;
- création/retouche, une révision, inverse/rejeu, no-op, stale, mauvaise zone et chemin absent/fermé ;
- budget des polylignes de contrôle, empreinte, cordes horizontales et plafonds de génération ;
- terrain canonique intact, terrain effectif borné, carving arrondi, terre et absence de relèvement ;
- surface d’eau monotone, UV, topologie, winding, normales et validation géométrique ;
- rejet atomique des conflits et overlaps avant R2-024, y compris après redessin de chemin ;
- staging/swap commun terrain-eau, réutilisation/retrait de handles et relève sans fuite ;
- cache river-only partiel et cohérence des projections dépendantes du terrain effectif ;
- résolution/promotion de végétation sur le Y effectivement visible ;
- commande Studio typée, exécution bloquante déportée, undo/redo, sauvegarde et réouverture.

## Frontières et résultats non exécutés

- UI/inspection visuelle native : **NOT RUN**, regroupée dans une passe ultérieure à la demande du
  propriétaire ;
- build natif Release et build Trunk Release : **NOT RUN** ;
- GPU et qualification performance : **NOT RUN** ;
- CI distante : **NOT RUN — infrastructure billing**, dérogation ADR-0006 ;
- PresentMon : **NOT RUN** ;
- résolution ou création d’intersections `R2-024` : non livrée, les overlaps sont refusés ;
- suppression/renommage de rivière, débit, animation, shader d’eau dédié et collisions : non livrés ;
- aucun asset ou contenu Cadle n’est copié ou modifié ;
- la qualification visuelle des interactions Gate 2 reste regroupée pour plus tard.

La prochaine tranche fonctionnelle est `R2-024`, intersections.
