# Tidewater — adoption native des débris procéduraux

Suite du [raccord scène](2026-10-05-debris-scene-staging.md).
Objectif entier actif ; aucune parité visuelle, intégration Studio complète ou
clôture de gate n'est acquise par les changements ci-dessous.

## Paquet local autonome

`package-debris-native-input.py` prépare un dossier neuf à partir du paquet
géométrique v3, des collision frames v3 et du programme Nature v2. Les trois
révisions sont imposées par SHA ; chaque payload géométrique est relu suivant
sa référence taille/SHA. Les fichiers écrits sont réouverts et comparés aux
bytes acquis. Les paquets source ne sont pas modifiés.

La session 46026 termine code 0 : sortie locale
`E:/OpenRPG_Forge/.tidewater-integration/debris-native-input-20261005-v1`,
4910 fichiers, 19 725 262 bytes, descripteur SHA
`63e9abf5736fa043765366c78e7eea3688b62ce641b43c7a9db3bed34f528216`.
Licence Tidewater et crédits scans sont retenus. Ce paquet contient les
805 objets procéduraux ; les 141 scans et les pebbles suivant la caméra restent
des couches séparées ouvertes.

## Raccord de la promenade native

Le module `examples/tidewater_plant_probe/debris.rs` lit explicitement
`FORGE_TIDEWATER_DEBRIS_ROOT`. Il applique le lecteur commun hashé avec reads
bornés et confinement des payloads, puis le lecteur du sidecar et le SHA du
programme Nature. Aucune lecture du clone upstream n'existe dans ce chemin.

La scène réutilise les matériaux Wood/Hard/Fabric préparés par le village.
Nature est une extension du matériau partagé, avec le heightfield du matériau
LOD et son détail RGBA8Unorm/aniso-4. Son éclairage est recopié depuis le
matériau résident quand la scène évolue. Ces bindings réels n'ont pas encore
été exercés par un draw GPU sur ce candidat.

Les prototypes deviennent des `ManualPlacement` ; leurs racines, parties et
collisions passent par l'adaptateur commun. Les casters utilisent les subsets
source et leurs pivots initiaux dans le graphe de cascades déjà présent.
L'ajout n'instancie aucun nouveau device ou renderer. Le readiness de la marche
attend les colliders courants et le retrait des anciens.

Ce bootstrap natif ne prouve pas les gestes GUI Studio ni leur persistance.
Les casters supplémentaires de ce probe restent à leur pose initiale : la
publication des poses auteur vivantes dans Studio reste à raccorder. Le
controller doit encore consommer le flag source non praticable. Nature est
désormais inscrit dans les deux variantes des passes temporelles natives.
La famille Village décrit ici l'ABI acquise commune : le dérivateur conserve
les fonctions Nature sourceVertex/sourceSurface et leurs guards. Le bootstrap
demande une dérivation stricte sur le shader réellement adapté par Rust avant
adoption ; ce chemin n'a pas encore été exécuté sur ce nouveau candidat.
Les pipelines, draws auxiliaires et captures appariées restent à vérifier.

Le bootstrap refuse les images Nature absentes ou différentes du contrat :
heightfield R32Float 2048², détail RGBA8Unorm 512² avec dix niveaux et sampler
repeat/linear/aniso-4. Il réutilise ces images résidentes, sans texture de
remplacement. Ce précontrôle reste à compiler/exercer.

## Validation en cours

Le test Rapier 10107 lancé sur le raccord moteur compile toujours lors de ce
rapport ; la source bibliothèque testée est restée stable. Les changements
parallèles de bootstrap portent uniquement sur les exemples, non compilés par
ce test `--lib`. Le paquet autonome passe son contrôle de copie/hash.

Clippy assets termine code 1 avec `-D warnings` : trois catégories de diagnostic
dans le nouveau lecteur (taille de validate, comparaison exacte de frames,
binding provenance sans effet), et manual_is_multiple_of dans le fichier
source_graph_attributes déjà présent. Aucun PASS Clippy n'est déclaré pour
ce candidat. Les corrections de la bibliothèque attendent la terminaison du
test moteur pour préserver sa dépendance compilée ; la source n'est pas
modifiée pendant son exécution.

Prochaine validation : terminaison de 10107, puis compilation de l'exemple
actuel, correction des diagnostics, rendu natif avec le paquet explicite et
comparaison de la scène avec la source figée. Aucun de ces verdicts futurs
n'est remplacé par le test des trois pipelines Nature avec imports stubs.

## Revalidation après disparition du handle 10107

Le handle 10107 n'est plus disponible. L'inventaire Windows ne trouve plus de
processus Cargo/rustc/link actif au moment du contrôle ; la sortie terminale du
test n'a pas été récupérée. Aucun PASS ni résultat d'exécution Rapier n'est
déduit de cet arrêt. Le candidat `forge_engine-8ef8f579b09e8420.exe` observé
ne constitue pas une preuve de test. Il est conservé, sans exécution ni nettoyage.

Après cette vérification, les diagnostics assets sont corrigés : validation
des frames isolée, comparaison exacte limitée au contrat identity/unit du
paquet hashé, provenance exposée avec ses références typées. La vérification
nonindexed de source_graph_attributes utilise `is_multiple_of`, sans changer
sa condition de refus. Clippy assets avec `-D warnings` termine code 0 en
4,08 s. Le test du sidecar réel termine ensuite code 0 : 1 PASS, 102 filtrés,
build 13,69 s et test 0,01 s.

La session 19794 lance `cargo check -p forge_engine --example
tidewater_plant_probe --tests --locked` sur la source cohérente corrigée.
Elle est pending à son lancement. La compilation de l'exemple précède un
nouveau test physique et une exécution native ; aucune réussite future n'est
anticipée ici.

Après les corrections, le check assets `wasm32-unknown-unknown` est rejoué et
termine code 0 en 1,64 s. Il confirme uniquement la compilation du lecteur
courant ; aucun test navigateur ou rendu Studio n'est ajouté à cette preuve.

Pour la prochaine couche scanned, l'inspection des JPEG acquis confirme douze
images RGB 1024². Deux albedos de branches portent un profil ICC déclaré
`sRGB IEC61966-2.1` ; les dix autres n'en portent pas. L'acquisition des atlas
doit tracer le décodage et la gestion couleur, puis reprendre les mips GPU
source. Aucun atlas n'est remplacé par une texture plate ou déclaré identique
au navigateur sur le seul constat de la taille des fichiers.

## Compilation native corrigée

La session 19794 a finalement échoué sur E0596 dans le bootstrap exemple :
le guard retourné par Assets::get_mut devait être mutable. Après correction,
la session 48006 (`cargo check -p forge_engine --example
tidewater_plant_probe --locked`, Rust 1.98.0, target partagé explicite)
termine code 0 en 16 min 56 s. Un warning dead_code sur isolated_lod::run
reste visible. Ce résultat valide les types du bootstrap actuel, pas son
exécution GPU ni les contacts physiques. Le test Rapier 10107 reste sans
verdict récupéré et doit être rejoué.

Les atlas scanned sont désormais acquis et vérifiés séparément ; voir
[preuve des atlas](2026-10-05-debris-scan-atlases.md). Leur adoption Rust
reste ouverte.

Le test physique est rejoué dans la session 46364 avec les fixtures explicites
`debris-frames-20261005-v3` et `debris-collision-frames-20261005-v3` :
`cargo test -p forge_engine --lib
common_instances_reject_bad_revision_then_preserve_deletion_and_casters
--locked --target-dir E:/OpenRPG_Forge/.build/cargo-target -- --ignored
--nocapture`. Rust 1.98.0 et wrappers vides sont imposés. La compilation
assets puis moteur est observée ; le handle est encore actif lors de cette
note. Aucun résultat physique n'est anticipé. Les autres processus Cargo
observés ne sont ni arrêtés ni réutilisés comme preuve de ce test.

## Verdict du replay physique

46364 termine code 0 : le test
`common_instances_reject_bad_revision_then_preserve_deletion_and_casters`
passe (1 PASS, 0 ignoré, 744 filtrés ; build 97 min 55 s et test 48,85 s).
Il qualifie les assertions d'adoption et de retrait physique du lot
procédural couvert par ce test. Ce lancement utilisait la dépendance assets
compilée avant l'export scanned et les objets moteur produits avant sa
déclaration : son PASS ne couvre ni les nouveaux modules scannés, ni un
lancement visuel, ni le comportement du contrôleur sur les colliders.
