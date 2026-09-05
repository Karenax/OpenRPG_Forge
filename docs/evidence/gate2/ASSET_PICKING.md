# Gate 2 — picking corrélé des placements

Date : 5 septembre 2026

Branche : `Rust`

Candidat source : `79ea370e87da7bbc3ec19d7abd07889d781264c4`

Arbre source : `16dd740f967fdf4500bb5b9a980d40b05a82c424`

Base source : `83616dbdbe1ae6b6a215364a28c54b4955ec4904`

**R2-028 est `PASS` sur qualification fonctionnelle automatisée du picking en lecture seule des
placements déjà projetés. Gate 2 atteint 27/43 et reste ouverte.** Ce verdict ne qualifie ni l’UI
native ou visuelle, ni Release, le GPU, la performance, PresentMon ou la CI distante.

Le manifeste des reçus est
[`ASSET_PICKING_ARTIFACTS.json`](ASSET_PICKING_ARTIFACTS.json). Aucun journal brut n’est revendiqué.
`Test-Gate2AssetPickingEvidence.ps1` relie en lecture seule ce rapport au commit, à l’arbre, à la
base, à l’inventaire exact des cinq fichiers source et aux contrats/tests ciblés, sans relancer de
test, build, Studio ou inspection visuelle.

## Picking Bevy borné et corrélé

`EditorPlacementProjectionBridge` accepte un pointeur normalisé seulement lorsque la projection
désirée est exactement celle présentée. Chaque requête porte une génération et un serial strictement
croissant. Le résultat renderer ajoute projet, zone, révision du `PlacementDocument` et éventuel
`EntityId` persistant ; une nouvelle soumission ou `clear(generation)` invalide requête et résultat.
Une réponse supplantée ne peut donc ni sélectionner ni désélectionner.

Le système PostUpdate attend la propagation des transforms et le calcul de visibilité, construit le
rayon depuis l’unique caméra éditeur et interroge les meshes visibles. Le premier mesh visible fait
foi : terrain ou autre géométrie au premier plan occulte le placement. Un hit n’est accepté que si
son ascendance bornée à 64 arêtes rejoint une racine privée de la projection active, avec scope exact
et même `Entity`. Le staging caché est ignoré. Un miss exact est publié explicitement ; il ne peut pas
être confondu avec l’absence de résultat.

Le commit d’une nouvelle projection rafraîchit aussi le scope privé des racines conservées. Les tests
couvrent descendant glTF, limite d’ascendance 64/65, occlusion par terrain ou mesh générique, staging
caché, miss, latest-wins, rejet de génération/serial, invalidation submit/clear, scope d’une racine
conservée et absence d’upload mesh ou de reconstruction géométrique pendant le picking.

## Sélection Studio transitoire

Le canvas distingue le clic du déplacement caméra avec un seuil de 4 px. Les petits deltas restent
bufferisés à zéro ; au premier dépassement, le delta complet depuis l’origine est appliqué, puis les
deltas deviennent incrémentaux. La position finale de `pointerup` est intégrée avant de décider si le
geste est un clic. Un autre identifiant de pointeur est ignoré sans consommer le geste. Shift choisit
le pan ; sinon le déplacement pilote l’orbite.

Un clic valide demande un pick avec coordonnées normalisées. Le Studio n’accepte ensuite que le
dernier serial dont génération, projet, zone et **révision placements** correspondent encore au scope
actif et au document courant. Une réponse périmée ou un ID inconnu ne remplace pas la sélection ; un
miss exact la vide. Le panneau ne lit que le ledger canonique pour afficher clé stable, ID, position
et échelle. Échap et le bouton de désélection effacent uniquement l’état local.

La sélection n’ajoute aucune commande workspace et ne modifie ni JSON, hash ni révision. Elle reste
si le même ID existe après une actualisation, disparaît à la suppression/undo ou à la fermeture, et
ne réapparaît pas magiquement après redo ou réouverture : un nouveau clic est requis.

## Corrections acquises pendant la revue

Deux défauts découverts par la revue adversariale ont été corrigés **dans le candidat qualifié** ; ils
ne constituent pas des limites restantes :

- la première version corrélait le scope Studio à la révision globale du workspace alors que le
  renderer publiait la révision placements. Une édition terrain/chemin pouvait donc rendre tout pick
  faussement périmé. Le scope et `apply_sample` utilisent maintenant la même révision
  `area.placements.revision` ; un test prouve qu’une révision workspace seule reste valide et qu’une
  révision placements différente reste stale ;
- la première version appliquait à la caméra les deltas inférieurs ou égaux à 4 px tout en les
  classant comme clic, et ne tenait pas compte du déplacement final de `pointerup`. Les deltas sont
  maintenant bufferisés/catch-up, `pointerup` passe d’abord par `move_to`, et un mauvais pointeur ne
  consomme plus le geste. Les tests couvrent le seuil, le rattrapage exact et le mauvais pointeur.

## Preuves automatisées acquises

La campagne verrouillée Engine + Studio totalise **375 tests de bibliothèque PASS** :

`cargo test -p forge_engine -p forge_studio --lib --locked -j1`

- `forge_engine` : **220/220 PASS** ;
- `forge_studio` : **155/155 PASS**.

Les filtres ciblés, déjà inclus dans ce total, passent également :

- `cargo test -p forge_engine editor_placement --locked` : **18/18 PASS** ;
- `cargo test -p forge_studio world_selection::tests --lib --locked -j1` : **9/9 PASS**.

Les autres contrôles acquis sont :

- Clippy Engine natif `--all-targets --locked -D warnings`, exécuté par l’agent de
  qualification : **PASS** ;
- Clippy Studio natif `--all-targets --all-features --locked -D warnings` : **PASS** ;
- check Studio WASM web `--locked` : **PASS** ;
- Clippy Studio WASM web `--locked` strict : **PASS** ;
- `trunk build --locked --offline` en profil debug : **PASS**. Une invocation initiale a été rejetée
  avant build parce que `NO_COLOR=1` n’était pas accepté ; la variable a été retirée, puis deux
  builds réels ont réussi, le second après les corrections adversariales sur le candidat final ;
- `cargo fmt --all -- --check` et `git diff --check` : **PASS**.

Le lot modifie exactement cinq fichiers source : trois Studio et deux Engine. Aucun `Cargo.toml`,
`Cargo.lock`, package/version, asset, chemin ou contenu Cadle, schéma ou donnée persistante n’est
modifié.

## Frontières et résultats non exécutés

- UI native et revue visuelle : **NOT RUN** ;
- build natif autonome et Release, ainsi que Trunk Release : **NOT RUN** ;
- GPU, performance et PresentMon : **NOT RUN** ;
- CI distante : **NOT RUN — infrastructure billing**, selon ADR-0006 ;
- risque non bloquant : si l’unique caméra éditeur ou son viewport logique n’est pas disponible, la
  requête reste pending au lieu de publier un miss. Elle ne mute rien et sera résolue, supplantée ou
  invalidée lors d’un état renderer ultérieur ; aucun timeout/diagnostic spécifique n’est livré ;
- gizmo, duplication/suppression, snap, bâtiments et intérieurs : non livrés ;
- aucun asset ni contenu Cadle copié, créé ou modifié.

La prochaine tranche fonctionnelle est `R2-029`, gizmo translation/rotation/échelle.
