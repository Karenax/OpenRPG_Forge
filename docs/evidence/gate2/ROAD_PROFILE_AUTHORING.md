# Gate 2 — profils de route persistants et projection terrain

Date : 5 septembre 2026

Branche : `Rust`

Candidat source : `e117d834b6ecdb6a283238e931631181b88f8ee6`

Arbre source : `1df4af8c05881cb59993385b9f275b0fcb71a2b3`

Parent source : `7995ab1a9d82d1462d98aecc0abbf2a2aebb60ff`

**R2-022 est `PASS` sur qualification fonctionnelle automatisée de l’ajout et de la retouche d’un
profil de route persistant sur un chemin R2-021. Gate 2 atteint 21/43 et reste ouverte.** Le verdict
ne couvre ni inspection visuelle native, ni build Release, ni qualification GPU/performance, ni
carving de rivière ou intersections.

Le manifeste lié aux reçus se trouve dans
[`ROAD_PROFILE_AUTHORING_ARTIFACTS.json`](ROAD_PROFILE_AUTHORING_ARTIFACTS.json). Aucun journal brut
n’a été capturé pour ce lot : `artifacts` reste volontairement vide. Le script
`Test-Gate2RoadEvidence.ps1` contrôle les reçus déclarés, l’inventaire exact des 26 fichiers du
commit source, ses contrats, son arbre et son parent, ainsi que l’absence de verdicts interdits.

## Contrat livré

Le workspace v5 contient pour chaque zone un document `forge.road` v1, indépendant du renderer. Une
route associe au plus un profil à un `PathId` existant et ouvert. Les entrées sont strictement triées
et uniques. Le document est borné à 64 routes et 64 Kio de JSON. La largeur doit rester entre 1 et
16 m, le bombé entre 0 et 0,30 m, et le bombé ne peut dépasser 5 % de la largeur.

Le budget de 8 192 m porte sur la longueur cumulée des **polylignes de contrôle** des chemins
auxquels un profil de route est affecté. Ce n’est pas une borne de longueur d’arc des splines. Un
chemin absent ou fermé, un profil non fini ou hors bornes, une collection trop grande ou un budget
dépassé sont refusés avant mutation.

La projection dérivée `TerrainDrapedCrownV1` est reproductible. Elle produit pour
`CompactedEarthV1` un ruban de trois sommets par rangée — bord gauche, centre bombé et bord droit —
et quatre triangles par intervalle longitudinal. Les rangées suivent le terrain avec un dégagement
de 0,04 m. Une subdivision adaptative garantit une **corde horizontale** au plus égale à 2 m entre
deux centres consécutifs ; ce plafond ne constitue pas une borne de longueur d’arc. Les plafonds
agrégés sont 16 384 rangées, 49 152 sommets et 196 608 indices.

`GeneratedRoads` transporte les hashes des documents terrain, chemins et routes. La génération
refuse les identités croisées, les chemins manquants ou fermés, les sorties non finies, les budgets
dépassés, les triangles dégénérés et les empreintes sortant de `[-256, 256[` au lieu de les clamper.
Le document candidat complet est généré avant exposition d’un edit. Un redessin de chemin revalide
également les routes existantes, afin qu’une empreinte devenue invalide ne puisse pas être commitée.

Le Studio expose un formulaire étroit par chemin : créer ou mettre à jour largeur et bombé, avec
surface terre compactée fixe. Le client refuse déjà un no-op, mais le service et le workspace
restent l’autorité. Une commande acceptée produit exactement une révision et une entrée undo ;
stale, mauvais projet/zone, hashes source périmés, chemin absent/fermé, profil invalide et no-op
échouent atomiquement. Undo/redo, sauvegarde, réouverture et redessin ultérieur du chemin conservent
le profil canonique.

L’`AreaPreview` transporte la projection complète des routes et vérifie les trois hashes. Une édition
road-only réutilise les projections terrain, végétation et chemins. Le moteur valide version,
identités, ordre, profils, bornes, topologie, winding, géométrie f64, normales et budgets avant le
staging. L’ancien affichage reste visible jusqu’au swap atomique ; les handles inchangés sont
réutilisés, une route retirée n’est libérée qu’au swap, et une réponse périmée ou invalide préserve
l’affichage courant. Une mise à jour limitée aux routes n’upload aucun mesh terrain.

Le matériau `CompactedEarthV1` réutilise le matériau terrain procédural Forge existant et ses poids
de sommet terre. Aucun asset, contenu Cadle ou dépendance runtime n’est ajouté. Le seul changement de
dépendance est `serde_json.workspace = true` sous `[dev-dependencies]` de `forge_world`, avec son
verrouillage dans `Cargo.lock` : c’est une dépendance de développement uniquement utilisée par les
tests JSON stricts.

## Compatibilité des sources

Le workspace passe du schéma 4 au schéma 5 uniquement pour intégrer le document de routes. Les
workspaces v1 à v4 restent lisibles ; leur chargement ajoute en mémoire un document route vide sans
réécrire le snapshot d’origine. Les hashes embarqués terrain, végétation, densité et chemins restent
identiques. Le hash du workspace migré change intentionnellement, puisque le schéma v5 et le nouveau
document vide font partie de sa source canonique.

## Preuves automatisées acquises

Les quatre suites de bibliothèque totalisent **533 tests PASS** :

- `cargo test -p forge_project` : **126/126 PASS** ;
- `cargo test -p forge_world` : **110/110 PASS** ;
- `cargo test -p forge_engine --lib` : **184/184 PASS** ;
- `cargo test -p forge_studio --lib` : **113/113 PASS**.

Le binaire Studio ajoute `cargo test -p forge_studio --bin forge-studio` : **6/6 PASS** ; ce reçu
reste séparé du total des bibliothèques.

Les contrôles de compilation et lint acquis sont :

- `forge_studio` : check WASM et Clippy natif/WASM strict **PASS** ;
- `forge_engine` : Clippy natif strict et Clippy WASM de la bibliothèque produit **PASS** ;
- `trunk build --locked --offline` avec `NO_COLOR=true` : **PASS** en profil debug ;
- `cargo fmt --all -- --check` et `git diff --check` : **PASS**.

Le reçu Engine WASM porte explicitement sur `--lib`, pas sur `--all-targets`. Le paquet ne revendique
donc aucun verdict sur le harnais de tests Engine WASM complet.

Les tests et inspections statiques couvrent notamment :

- JSON/hash canoniques, limites, tri, identité et migration workspace v4 vers v5 ;
- création/retouche, une révision, inverse/rejeu, no-op, stale, mauvaise zone et chemin absent/fermé ;
- budget des polylignes de contrôle, génération terrain, cordes horizontales, empreinte et budgets ;
- revalidation des routes après redessin de chemin ;
- validation géométrique, staging/swap atomique, réutilisation et retrait de handles ;
- cache road-only sans reconstruction terrain/végétation/chemins et sans upload terrain ;
- commande Studio typée, undo/redo, sauvegarde et réouverture.

## Frontières et résultats non exécutés

- UI/inspection visuelle native : **NOT RUN**, regroupée dans une passe ultérieure ;
- build natif Release et build Trunk Release : **NOT RUN** ;
- GPU et qualification performance : **NOT RUN** ;
- CI distante : **NOT RUN — infrastructure billing**, dérogation ADR-0006 ;
- PresentMon : **NOT RUN** ;
- suppression/renommage de route, peinture ou matériau éditable : non livrés ;
- carving de rivière `R2-023`, intersections `R2-024`, collisions et playtest : explicitement exclus ;
- aucun asset ou contenu Cadle n’est copié ou modifié ;
- la qualification visuelle des interactions Gate 2 reste regroupée pour plus tard.

La prochaine tranche fonctionnelle est `R2-023`, rivière et carving.
