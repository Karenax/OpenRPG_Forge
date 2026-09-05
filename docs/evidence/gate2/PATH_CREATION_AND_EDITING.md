# Gate 2 — création et édition persistantes de chemin

Date : 5 septembre 2026

Branche : `Rust`

Candidat source : `91be10b84898fab1623ef7f7d67ef4fe2dd4ea17`

Arbre source : `a44593d4affc3f6bea588f0d7570d40c10da9efd`

Parent source : `168e95da4a80d6ef7e9b2736980463fd1053d8cb`

**R2-021 est `PASS` sur qualification fonctionnelle automatisée de la création et du
redessin persistants d’un chemin générique. Gate 2 atteint 20/43 et reste ouverte.** Le verdict ne
couvre ni une inspection visuelle native, ni un build Release, ni les profils de route, rivières,
intersections ou placements génériques des routes suivantes.

Le manifeste lié aux reçus se trouve dans
[`PATH_CREATION_AND_EDITING_ARTIFACTS.json`](PATH_CREATION_AND_EDITING_ARTIFACTS.json). Aucun journal
brut n’a été capturé pour ce lot : `artifacts` reste volontairement vide. Le script
`Test-Gate2PathEvidence.ps1` contrôle les reçus déclarés, l’inventaire et les contrats du commit
source, ainsi que son arbre et son parent exacts.

## Contrat livré

Le workspace v4 contient, pour chaque zone, un document `forge.path` v1 indépendant du renderer.
Un chemin porte un `PathId` stable, un nom, un état ouvert/fermé, l’algorithme
`centripetal_catmull_rom_v1` et des points de contrôle ordonnés. La collection et ses formes sont
bornées : 64 chemins, 64 points par chemin, 2 048 points au total, 8 192 m par chemin, 32 768 m au
total et 512 Kio de JSON.

Le Studio propose deux actions directes et volontairement étroites : créer un chemin ouvert ou
redessiner les points d’un chemin existant. Le geste reste local pendant le glissement et une seule
commande est envoyée au relâchement. Le geste UI accepte de 2 à 64 points espacés d’au moins 4 m ;
la frontière monde refuse en plus les entrées non finies, hors de `[-256, 256[` ou dont deux points
consécutifs sont distants de moins de 0,5 m. Le Y persistant de chaque point est recalculé par
`TerrainGenerationPlan::surface_at` : le navigateur ne fait pas autorité sur la hauteur.

La projection dérivée est reproductible : deux points produisent une ligne, trois points ou plus
utilisent Catmull–Rom centripète v1, avec des subdivisions calculées sur les cordes de contrôle selon
une cible de 2 m et des extrémités exactes. Cette cible n’est pas une garantie de longueur d’arc :
sur une courbe forte, deux échantillons voisins peuvent être distants de plus de 2 m. `GeneratedPaths`
transporte les hashes terrain et document de chemins, les contrôles et les échantillons reliés à leur
segment source. Une courbe qui dépasse la zone par overshoot échoue au lieu d’être clampée. Avant
d’exposer un edit de création ou de redessin, le monde génère tout le document candidat ; une
révision que l’`AreaPreview` ne saurait projeter ne peut donc pas être commitée.

Une création acceptée ajoute un chemin ; un redessin remplace uniquement ses points de contrôle et
préserve identifiant, nom, état ouvert/fermé et algorithme. Chaque action acceptée produit exactement
une révision et une entrée undo. No-op, révision périmée, mauvais projet/zone, chemin manquant ou déjà
présent, trace invalide et projection impossible échouent avant mutation. Undo/redo, sauvegarde et
réouverture conservent la source exacte ainsi que l’identité et le nom déterministes attribués par le
Studio.

L’`AreaPreview` transporte toujours la projection complète des chemins avec la projection terrain.
Une édition limitée aux chemins reconstruit zéro chunk terrain, régénère zéro chunk végétation et
conserve les 64 chunks végétation. Le viewport refuse les hashes, identités, bornes, ordres de
segments ou budgets incohérents, puis remplace l’overlay canonique atomiquement sans upload de mesh
terrain. Le trait cyan local n’est jamais persistant et est purgé sur annulation, changement de zone
ou génération, et sur toute erreur d’installation ou de staging.

Aucun cache de chemin séparé n’est introduit : le document reste la source canonique et les
échantillons sont des données dérivées locales.

## Compatibilité des sources

Le workspace passe du schéma 3 au schéma 4 uniquement pour intégrer le document de chemins. Les
workspaces v1, v2 et v3 restent lisibles ; leur chargement crée en mémoire un document de chemins
vide sans réécrire le snapshot d’origine. Les hashes des documents terrain, végétation et densité
déjà embarqués restent identiques. Le hash du workspace migré change intentionnellement, puisque le
schéma v4 et le nouveau document vide font partie de sa source canonique.

Aucun manifest Cargo, dépendance, asset ou contenu Cadle n’est ajouté ou modifié.

## Preuves automatisées acquises

Les quatre suites de bibliothèque totalisent **504 tests PASS** sans additionner les sous-suites
ciblées, qui recoupent ces mêmes tests :

- `cargo test -p forge_project` : **115/115 PASS** ;
- `cargo test -p forge_world` : **103/103 PASS**, après le rejet atomique de l’overshoot candidat ;
- `cargo test -p forge_engine --lib` : **176/176 PASS**, après le même correctif ;
- `cargo test -p forge_studio --lib` : **110/110 PASS**.

Le binaire Studio ajoute `cargo test -p forge_studio --bin forge-studio` : **6/6 PASS** ; ce reçu
reste séparé du total des bibliothèques ci-dessus.

Les reçus ciblés supplémentaires sont :

- `cargo test -p forge_engine editor_viewport --lib` : **18/18 PASS**, sous-suite du résultat
  Engine complet ;
- service Studio création/redessin : **2/2 PASS** ;
- `PathTrace` Studio : **2/2 PASS** ;
- cache `AreaPreview` path-only : **1/1 PASS**.

Les contrôles de compilation et lint acquis sont :

- `forge_project` : Clippy natif strict, check WASM et Clippy WASM strict **PASS** ;
- `forge_world` : Clippy natif et WASM strict **PASS** ;
- `forge_engine` : Clippy natif strict **PASS** ; Clippy WASM de la bibliothèque produit **PASS** ;
- `forge_studio` : check natif des tests, check WASM et Clippy natif/WASM strict **PASS** ;
- `trunk build --locked --offline` avec `NO_COLOR=true` : **PASS** en profil debug ;
- `cargo fmt --all -- --check` et `git diff --check` : **PASS** sur le worktree qualifié.

Une première invocation Trunk a refusé immédiatement la valeur héritée `NO_COLOR=1`, que cette
version de Trunk n’accepte pas. Le rejeu explicite avec `NO_COLOR=true` est le reçu qualifiant ;
l’avis tiers inchangé `proc-macro-error2 2.0.1` reste visible.

La tentative Engine WASM avec `--all-targets` est honnêtement classée **FAIL préexistant hors
R2-021** : le test historique `src/lib.rs:3435` mélange `std::time::Instant` et l’`Instant`
Bevy/`web_time`. Le chemin produit réellement compilé par
`cargo clippy -p forge_engine --lib --target wasm32-unknown-unknown --no-default-features --features web -- -D warnings`
est **PASS**. Le paquet ne transforme donc pas l’échec du harnais de test WASM en PASS global.

Les tests et inspections statiques couvrent notamment :

- JSON/hash canoniques, limites, tri, identités et migrations v1/v2/v3 vers workspace v4 ;
- création, redessin, une révision, inverse/rejeu, no-op, stale, mauvaise zone et chemin absent ;
- ligne, Catmull–Rom, endpoints, projection Y terrain, rejeu/hash et overshoot sans edit exposé ;
- aperçu complet, conservation terrain/végétation path-only et budget de sérialisation ;
- validation et remplacement atomiques de l’overlay, plus invalidation du trait local ;
- commandes Studio typées, undo/redo, sauvegarde, réouverture et identité déterministe.

## Frontières et résultats non exécutés

- UI/inspection visuelle native : **NOT RUN**, conformément au regroupement ultérieur des contrôles
  d’interface ;
- build natif Release et build Trunk Release : **NOT RUN** ;
- CI distante : **NOT RUN — infrastructure billing**, dérogation ADR-0006 ;
- PresentMon, GPU, latence et qualification performance : **NOT RUN** ;
- renommage et suppression de chemin : non livrés ;
- picking et gizmo génériques : non livrés ;
- aperçu natif de la spline pendant le glissement : non livré ; seul le trait local transitoire est
  affiché avant la commande ;
- la cible d’échantillonnage de 2 m subdivise les cordes de contrôle et ne borne pas encore
  strictement la longueur d’arc locale d’une courbe forte ;
- `R2-022` profil de route, `R2-023` carving de rivière, `R2-024` intersections et toutes les routes
  suivantes : explicitement exclus de R2-021.

La prochaine tranche fonctionnelle est `R2-022`, route avec profil.
