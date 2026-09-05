# Gate 2 — placement gouverné d’assets

Date : 5 septembre 2026

Branche : `Rust`

Candidat source : `c5ccc8742be18513cd3f117215fa57e5c6b46553`

Arbre source : `479c5484de5c903a41f17f3350dd96fccba8e036`

Base source : `cc03751adfbbbfd79ffab58b2b38a5b2a621b2c3`

**R2-027 est `PASS` sur qualification fonctionnelle automatisée du glisser-déposer d’un Asset
gouverné vers une zone, de sa persistance et de sa projection Bevy atomique. Gate 2 atteint 26/43
et reste ouverte.** Ce verdict ne qualifie ni l’UI native ou visuelle, ni Release, le GPU, la
performance, PresentMon ou la CI distante.

Le manifeste des reçus est
[`ASSET_PLACEMENT_ARTIFACTS.json`](ASSET_PLACEMENT_ARTIFACTS.json). Aucun journal brut n’est
revendiqué. `Test-Gate2AssetPlacementEvidence.ps1` relie en lecture seule ce rapport au commit, à
l’arbre, à la base, à l’inventaire exact des 29 fichiers source et aux contrats/test ciblés, sans
relancer de test, build, scan d’installation ou inspection visuelle.

## Source persistante et commande partagée

Le workspace v8 ajoute à chaque zone un `PlacementDocument` `forge.placement` v1, moteur-neutre.
Il conserve des `AssetPin` immuables et complets — identité stable, sous-ensemble, pack, version,
auteur, chemins gouvernés, hashes scène/manifeste/licence, licence SPDX et échelle de base — puis des
`AuthoredPlacement` triés par `EntityId`, contenant seulement `asset_id` et `forge_core::Transform`.
Il ne persiste aucun `Entity`, handle ou transform Bevy.

Le document est borné à 256 pins, 1 024 instances et 512 Kio JSON. Identités nil, ordre non canonique,
doublons, pin absent ou rebinding d’un `asset_id`, chemins non portables, hashes nuls et transforms
non finis/non normalisés/hors bornes échouent avant mutation. Les migrations v1 à v7 créent un
document vide dans la zone en mémoire et ne réécrivent pas l’ancien snapshot.

`forge_world::PlaceAssetCommand` est une commande pure liée à un `command_id`, une révision de base,
un projet/module/zone et aux hashes exacts des sources terrain, chemins, routes, rivières et
placements. Elle recalcule Y sur le terrain effectif après carving rivière, applique une rotation
identité et l’échelle de base une seule fois, puis dérive l’ID persistant de l’instance du
`command_id`. L’édition partagée ajoute pin et instance atomiquement, en une révision et une entrée
d’historique ; son inverse permet undo, et redo restaure les mêmes identités et sources.

Les tests couvrent le round-trip canonique, le hash stable, les budgets, le placement déterministe,
le terrain effectif, les sources périmées ou forgées, les limites de collection, une seule révision,
undo/redo, sauvegarde immuable et réouverture exacte.

## Interaction directe Assets vers Monde

La fenêtre Assets rend chaque modèle vérifié draggable avec un payload interne contenant seulement
`asset_id` et le hash exact du snapshot. Le canvas Monde normalise le pointeur dans son rectangle,
demande au renderer un échantillon de terrain et n’accepte le drop que si génération, serial,
projet, zone et révision correspondent exactement au contexte encore actif. Hit absent/périmé,
coordonnée non finie, catalogue absent, refresh en vol, zone fermée ou session occupée ne produisent
aucune mutation.

Le frontend ne transmet que X/Z et la corrélation de surface. Le worker natif résout de nouveau
l’Asset dans le snapshot validé courant, reconstitue son pin depuis la provenance gouvernée puis
recalcule Y depuis les sources canoniques. L’UI ne peut donc ni fournir un chemin de scène, ni
fabriquer un pin, ni imposer une hauteur. Le placement passe par la même commande de workspace que
les autres clients ; il n’existe pas de document canonique parallèle dans la WebView.

Le cache Assets refuse de résoudre un placement durant un refresh. Une réouverture ne dépend pas de
l’affichage préalable de la fenêtre Assets : le service effectue au besoin le même scan offline
épinglé, compare chaque pin persistant au catalogue gouverné courant et refuse un repin. Cette
revalidation conserve la limite R2-026 : la racine checkout est supposée fiable et stable pendant
le scan ; une frontière hostile contre mutation concurrente et la racine packagée appartiennent à
Gate 4.

## Projection Bevy atomique

`EditorPlacementProjectionBridge::submit_document` valide le document complet, les chemins relatifs
de scènes, hashes, transforms et budgets natifs avant chargement. Les plafonds défensifs renderer
sont 4 096 instances, 512 scènes uniques et 1 Gio suivi. Les octets sont comptés une fois par paire
unique `(scene_path, scene_hash)`, afin que plusieurs instances partageant une scène ne multiplient
pas artificiellement le budget.

Chaque source charge `GltfAssetLabel::Scene(0)` depuis son chemin gouverné. Le staging reste caché
jusqu’à ce que la scène, ses dépendances directes et toutes ses dépendances récursives soient
`Loaded`. Une nouvelle génération annule son staging précédent. Dans une même zone, un échec détruit
seulement le staging et conserve la projection précédente ; lors d’un changement de scope, l’ancien
scope est retiré avant attente afin de ne jamais afficher ses Assets sur le nouveau terrain.

Le commit remplace la projection en une fois, conserve handle et `Entity` des instances dont source,
scène, hash, budget et transform sont inchangés, et retire/restaure correctement les instances lors
d’undo/redo. `clear(generation)` est latest-wins et efface actif, staging et identité de scope quand
aucune zone n’est ouverte. Une actualisation placement-only effectue zéro upload de mesh terrain et
zéro reconstruction des caches géométriques ; le cache Studio conserve tous les chunks terrain et
végétation.

## Preuves automatisées acquises

Une campagne verrouillée unique totalise **678 tests de bibliothèque PASS** :

`cargo test -p forge_assets -p forge_project -p forge_world -p forge_engine -p forge_studio --lib
--locked`

- `forge_assets` : **21/21 PASS** ;
- `forge_project` : **161/161 PASS** ;
- `forge_world` : **139/139 PASS** ;
- `forge_engine` : **211/211 PASS** ;
- `forge_studio` : **146/146 PASS**.

Le binaire Studio ajoute `cargo test -p forge_studio --bin forge-studio --locked` : **6/6 PASS**, hors
du total des bibliothèques.

Les contrôles acquis sont :

- `cargo clippy -p forge_assets -p forge_project -p forge_world --all-targets --all-features
  --locked -- -D warnings` : **PASS** ;
- `cargo clippy -p forge_engine --lib --locked -- -D warnings` : **PASS** ;
- `cargo clippy -p forge_studio --all-targets --all-features --locked -- -D warnings` : **PASS** ;
- check Studio WASM web `--locked` : **PASS** ;
- Clippy Studio WASM web `--locked` strict : **PASS** ;
- `trunk build --locked --offline` en profil debug : **PASS** ;
- `cargo fmt --all -- --check` et `git diff --check` : **PASS**.

Le lot modifie exactement 29 fichiers source. `Cargo.lock` est intact et aucun package ou version
n’est ajouté. Le seul `Cargo.toml` modifié est celui du Studio, pour activer la feature déjà épinglée
`web-sys/DragEvent`. Aucun asset, fichier Cadle, contenu de pack ou dépendance runtime nouvelle n’est
ajouté.

## Frontières et résultats non exécutés

- UI native et revue visuelle : **NOT RUN** ;
- build natif autonome et Release : **NOT RUN** ;
- GPU, performance et PresentMon : **NOT RUN** ;
- CI distante : **NOT RUN — infrastructure billing**, selon ADR-0006 ;
- sécurité contre mutation concurrente de la racine d’assets et racine packagée : Gate 4 ;
- picking, gizmo, duplication/suppression, snap, bâtiments et intérieurs : non livrés ;
- aucun asset ni contenu Cadle copié, créé ou modifié.

La prochaine tranche fonctionnelle est `R2-028`, picking.
