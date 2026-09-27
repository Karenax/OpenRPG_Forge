# ADR-0010 — contrat de prototypes, 2026-09-06

Direction approuvée explicitement par l'utilisateur (« oui vas y »). Cette décision ne vaut
pas acceptation artistique des arbres ou passage de Gate 1.

## Implémenté

`forge_assets::VegetationPrototypeCatalog` est un document strict v1, sans moteur ni disque :
hash de végétation, kit/révision, liaison exacte de chaque couche arborée vers un GLB épinglé,
pivot Y-up et hauteurs source/cible explicites. Aucun placement n'est dupliqué dans le catalogue.
Les dimensions et le facteur de normalisation uniforme sont bornés ; les valeurs non finies
sont refusées. Le rapport de kit est validé, dont la provenance, avant résolution des références.

## Vérifié

- 19 tests `forge_assets` passent, dont trois nouveaux scénarios regroupant round-trip,
  champ placements interdit, normalisation, hashes périmés, révision, asset absent/non arbre,
  couverture manquante/dupliquée, dimensions invalides et provenance Cadle refusée.
- **418 tests workspace PASS**, un probe de timing volontairement ignoré demeure ignoré.
- Clippy workspace/all-targets/all-features strict : PASS après correction de deux points
  dans les tests (comparaison flottante remplacée par comparaison des bits, point-virgule).
  Le log de l'échec initial est conservé. Les 19 tests assets ont été rejoués après correction.
- `cargo check -p forge_assets --target wasm32-unknown-unknown --locked` : PASS.
- Formatage et `git diff --check` : PASS.

Preuves externes : `E:/OpenRPG_Forge/.gate1-evidence-runtime/r1-prototype-contract-20260906/`.
`tests.log` SHA-256 `6ffeef0343d98b28e968a1cc517d5dd638cc18aae28d3691a8b3e7a31e79bab9` ;
`clippy-replay.log` SHA-256 `9fc848c62aa20fd65cab241841cfecc8a28529bbd7d33cce5aa7dce9a43f72eb`.
Les logs `assets-replay.log` et `wasm.log` portent les contrôles complémentaires.
Target externe usuel, `CARGO_INCREMENTAL=0`, `RUSTFLAGS=-C debuginfo=0`, commandes verrouillées.

## À réaliser

Ce contrat n'est pas encore consommé par le renderer. L'adapter doit confronter les dimensions
déclarées aux meshes, vérifier leurs LOD et partager les handles sans dupliquer les colliders.
Le vent, les transitions HLOD et la mesure avant/après restent à réaliser. Aucune nouvelle capture
n'est nécessaire pour ce lot de données sans modification du rendu ; les captures du candidat
précédent ne prouvent pas ce raccord. Pas de dépendance ajoutée, de promotion, de push ou de gate fermée.
## Complément — résolution partagée des meshes

`forge_engine::resolve_vegetation_prototype` prépare trois listes de primitives depuis les
assets glTF chargés. Il réutilise les handles de mesh/matériau, ignore les nœuds de collision
et retourne une transformation de normalisation avant la pose du scatter. Aucune entité,
copie de mesh ou collision n'est créée par cette fonction.

Le contrôle des positions et transformations confronte la hauteur et le pivot déclarés aux
bounds LOD0 réels (tolérance 1 %, minimum 2 cm). Les trois LOD doivent être non vides et les UV
présents. Hiérarchie, skin, racine animée, metadata invalide, mesh invalide et dimension divergente
sont refusés. Une dépendance absente est signalée séparément ; le futur appelant devra observer
les échecs définitifs de chargement, pas attendre indéfiniment.

Trois tests synthétiques passent : partage des handles et sources conservées, normalisation,
collision exclue, dimensions/LOD/dépendances distingués et hiérarchie/UV manquants refusés.
**421 tests workspace PASS**, un probe timing ignoré ; Clippy workspace strict et check moteur
WASM `--no-default-features --features web` PASS. Un avertissement documentaire Clippy a été
corrigé avant le rejeu global. Formatage et diff contrôlés.

Preuves : `E:/OpenRPG_Forge/.gate1-evidence-runtime/r1-prototype-resolution-20260906/`,
`tests.log` SHA-256 `25986b2e946c247d58e62bff06d7323510bc1358470fe9caeac1be2c7b568b11`,
`clippy.log` et `wasm.log`. Même target et variables de compilation que ci-dessus.

**Limite** : tests sur assets synthétiques chargés, pas de replay GLB réel de cette fonction,
pas encore de système de chargement/cache ni de remplacement du scatter. Le raccord du vent,
des matériaux de rendu et des HLOD reste à faire. Aucun changement visuel ou gain de performance
n'est revendiqué et aucune gate n'est fermée.

## Complément — chargement asynchrone natif, 2026-09-06

`VegetationPrototypeLoadPlugin` valide le catalogue contre la végétation compilée et le kit
déjà vérifié par l'hôte. Il demande les GLB par asset unique, observe les erreurs de dépendances
et une échéance de 120 secondes, puis publie le cache seulement après résolution de toutes
les liaisons. Les états Loading/Ready/Failed sont explicites. Aucun mesh, matériau, placement
ou collider n'est copié par cet adapter. Le préfixe de source mémoire Web est disponible,
mais le démarrage Studio ne l'utilise pas encore.

La commande native optionnelle `--prepare-vegetation-catalog` exige le kit vérifié et une
végétation Visual Valley. Son contrôle d'options rejette aussi les doublons. La préparation
ne remplace pas le scatter : le journal le dit explicitement.

### Vérifié réellement

- GLB candidat chêne `f71fb7bb6b8e0b6ab9e818f23172e6a55b6763e3ef21e1435de3a2e9dc49ac50`,
  catalogue externe dans `r1-foliage-runtime-20260906/prototype-catalog.v1.json`.
  Bounds LOD0 réimportés : base -0,06517145 m, sommet 17,64097977 m ; hauteur déclarée
  17,706152 m et hauteur cible 5,65 m. Ce ne sont pas les dimensions nominales de recette.
- Smoke natif de 420 frames : événement `vegetation_prototypes_prepared`, un GLB, une couche,
  sortie 0 sans erreur runtime observée ; toujours 20 scènes du kit prêtes et 36 colliders.
- Contre-épreuve avec hauteur déclarée de 10 m et mêmes sources : événement d'erreur
  `vegetation_prototype_load_failed`, `DimensionMismatch`, sortie 1 et `success=false`.
  Aucun événement de cache prêt n'est attendu pour cette entrée rejetée.
- 422 tests workspace PASS, un probe timing volontairement ignoré. Clippy initial refuse
  la longueur du parseur CLI ; extraction du traitement des chemins uniques sans suppression
  du contrôle. Rejeu des tests runtime, Clippy workspace/all-targets/all-features strict et
  check moteur WASM `--no-default-features --features web --target wasm32-unknown-unknown`
  PASS. Le smoke utilise le binaire construit avant ce découpage équivalent du parseur.

Preuves externes : `E:/OpenRPG_Forge/.gate1-evidence-runtime/r1-prototype-loading-20260906/`.

- `tests.log` SHA-256 `760480d8c3f4473289aa1ded652e6c834f3ade9bf75f83aae40ccf7a2af07f92`.
- `runtime.log` SHA-256 `2de7b42bfce55aaaa2bc502d9ea25e557c50e8fac66419bc74a899ca1f9c56a3`.
- `invalid-height-runtime.log` SHA-256 `dce273fa1eef96c4c769e5453c64a146cbfb0c2f5dbf5709d4b379d9e29ee807`.
- `clippy.log` conserve l'échec initial ; `clippy-replay.log`, `runtime-tests-replay.log`
  et `wasm.log` portent les contrôles après correction.

### Limites restantes

Pas de test d'expiration réelle de 120 secondes ni de dépendance fichier défaillante injectée
dans le chargeur ; publication multi-couche atomique et déduplication multi-couche non exercées
par ce smoke à une couche. Pas de rechargement à chaud qualifié. Cache CPU prêt ne signifie
ni matériau de rendu converti, ni vent raccordé, ni scatter/HLOD remplacé. Pas de capture ou
gain de performance nouveau revendiqué. Gate 1 reste ouverte, validations humaines et CI
distante non réalisées, aucun push ni promotion d'assets.

## Complément — matériaux PBR partagés, 2026-09-06

Le cache de préparation conserve maintenant les handles `StandardMaterial` que le handler
glTF PBR de Bevy 0.19.1 a déjà produits. Le code de référence inspecté est
`bevy_pbr-0.19.1/src/gltf.rs` : sous-assets `MaterialN/std` et `DefaultMaterial/std`.
Forge ne réimplémente pas cette conversion et n'alloue ni matériau ni texture supplémentaire.
La table est indexée par identité du matériau glTF pour toutes les couches et tous les LOD.
La publication du cache attend aussi la présence des matériaux PBR ; un sous-asset absent
est une erreur explicite. Aucun matériau source n'est modifié.

Vérifications : chemins natifs, source mémoire nommée, matériau par défaut et labels invalides
couverts par le nouveau test. **423 tests workspace PASS**, un probe timing ignoré ; Clippy
strict workspace/all-targets/all-features, build runtime et check moteur WASM PASS.
Le smoke natif reconstruit de 420 frames résout **4 matériaux PBR partagés**, un GLB et une
couche, sort avec code 0 et sans erreur runtime observée. Le kit reste à 20 scènes prêtes,
0 échec et 36 colliders. Ce smoke vérifie des handles résidents, pas une nouvelle forêt rendue.

Preuves : `E:/OpenRPG_Forge/.gate1-evidence-runtime/r1-prototype-pbr-20260906/` :

- `tests.log` SHA-256 `85508c046b5c6567892df1101f57d8a767d472e997158898cefacd4ec6157849` ;
- `runtime.log` SHA-256 `db458170cf5257699d35e679b0c1cb0a19226b18fa6e75eb370b3e79abbb3659` ;
- `clippy.log`, `build.log`, `wasm.log`, `metrics.json` pour les contrôles associés.

Le shader procédural actuel remplace les couleurs par une palette et utilise la couleur de
sommet comme poids de vent : il ne doit pas être appliqué tel quel aux matériaux importés.
Le vent préservant PBR/UV/alpha, les racines et les passes de profondeur/mouvement reste à
raccorder, ainsi que le scatter/HLOD et le démarrage Studio. Aucune parité pixel, qualification
artistique ou performance supplémentaire n'est inférée. Aucune gate fermée et aucun push.

## Complément — meshes normalisés partagés, 2026-09-06

Le chargeur prépare désormais `render_layers` avec des meshes dérivés dont le pivot et l'échelle
du catalogue sont appliqués une seule fois. La future projection appliquera uniquement la pose
canonique du placement ; elle ne devra pas réappliquer la normalisation. Bevy transforme les
positions, normales et tangentes ; les UV0/UV1, couleurs et données source restent intactes.
Le shader procédural n'est toujours pas appliqué à ces matériaux PBR.

Un dérivé est créé par couple identité du mesh source / transformation (bits exacts), partagé
entre toutes les références et LOD correspondants. Cela ajoute un cache géométrique dérivé,
pas une copie par arbre du scatter. Les sources GLB sont conservées, les handles des matériaux
PBR réutilisés. Aucun collider ni placement n'est ajouté. Tous les meshes sont validés avant
publication dans `Assets<Mesh>` ; cisaillement, transformation dégénérée, positions non finies
et normales manquantes/invalides sont refusés.

Trois nouveaux tests couvrent racine à zéro et hauteur cible, UV/couleurs/tangentes conservées,
source inchangée, partage entre LOD, échec sans allocation partielle dans le store Bevy,
rotation/échelle non uniforme des normales et tangentes, cisaillement et normales absentes.
**426 tests workspace PASS**, un probe timing ignoré ; Clippy strict workspace PASS après
correction des comparaisons flottantes dans le nouveau test (log initial conservé).

Preuves externes : `E:/OpenRPG_Forge/.gate1-evidence-runtime/r1-prototype-normalized-20260906/`.
Le vent et son ancrage animé ne sont pas encore implémentés, et ces meshes ne remplacent pas
encore le scatter/HLOD. Cette étape ne constitue ni capture d'une nouvelle forêt ni qualification
de performance ; la mémoire supplémentaire des dérivés doit entrer dans la comparaison finale.

Replay natif reconstruit de 420 frames : **12 meshes normalisés**, 4 matériaux PBR partagés,
un GLB et une couche prêts. Sortie 0, aucune erreur runtime observée ; toujours 20 scènes du
kit chargées, 0 échec et 36 colliders. Check moteur WASM PASS. `tests.log` SHA-256
`7141ac77df688a7fae2514e8992117757844401edcf74d3351eda7856be3848e` ; `runtime.log` SHA-256
`c43f76e3046f087878d7905797a061e09effd5919900df22d2e71754e901e3cc`.
Logs complémentaires : `clippy.log`, `clippy-replay.log`, `build.log`, `wasm.log`, `metrics.json`.
Cette preuve porte sur la préparation des vrais meshes GLB, pas leur affectation au scatter.

## Complément — mode shader PBR/vent, 2026-09-06

L'extension du shader végétation possède un mode PBR gouverné explicite, désactivé par défaut.
`with_governed_prototype_height` refuse les hauteurs non finies ou hors 0,01–1000 m. Sur les
meshes préalablement normalisés, le poids de vent vaut le carré de la hauteur relative bornée :
zéro au sol, un au sommet de référence. Il ne consomme pas l'alpha des sommets. Les chemins
de position courante et précédente utilisent le même poids. UV1 n'est pas interprété comme
racine HLOD en mode importé ; le PBR standard, ses maps, teintes et alpha ne sont plus écrasés
par la palette procédurale. Les bornes de déplacement existantes restent communes.

**427 tests workspace PASS**, Clippy strict, build runtime et check moteur WASM PASS.
Le nouveau test vérifie les paramètres et le contrat source du shader, pas son exécution GPU.
Smoke natif 420 frames avec capture à la frame 300 : sortie 0, aucune erreur runtime observée,
capture 1920×1080 inspectée. Le mode procédural actuel s'affiche avec terrain, pont et arbres ;
aucune régression grossière visible, mais pas de comparaison pixel exacte ni de gain de performance
revendiqué. Kit toujours à 20 scènes et 36 colliders.

Preuves : `E:/OpenRPG_Forge/.gate1-evidence-runtime/r1-prototype-wind-shader-20260906/`.
`tests.log` SHA-256 `aaa8e411f09254eba692e72d932e51f58571b2a65d78a65b6b4cc3be38674b5e` ;
`hero.png` SHA-256 `15de498b3c3f1be447e8bfacec7b2253562d403b03ee90602c19684ebc721d99`.
Autres logs : `clippy.log`, `build.log`, `wasm.log`, `runtime.log`, `metrics.json`.

**Limite importante** : la capture compile et exécute le shader en mode procédural, pas un arbre
importé animé. Le mode PBR/vent n'est pas encore affecté aux matériaux du cache ; horloge,
mouillure, affectation au scatter et HLOD cohérents restent à raccorder puis qualifier.
L'ancrage animé des racines n'a pas encore de preuve GPU. Le mélange visuel d'arbres détaillés
et anguleux reste visible et non accepté. Gate 1 reste ouverte, aucune promotion ni publication.

## Complément — cache de matériaux de vent et horloge, 2026-09-06

`VegetationPrototypeWindCache` est maintenant préparé après le cache géométrique. Un matériau
étendu est partagé par matériau PBR source / hauteur cible, sans copie de texture ni mutation
du matériau source. La hauteur validée accompagne les prototypes résolus et normalisés.
Le rendu forward est sélectionné explicitement ; les matériaux unlit sont refusés, leur shader
n'étant pas couvert par cet adapter. La préparation valide toutes les entrées avant allocation.
Une erreur est journalisée et conservée dans le cache ; elle ne devient pas un fallback silencieux.

L'horloge végétation commune met à jour les matériaux gouvernés en conservant leurs paramètres
de hauteur et de mouillure, sans modifier les matériaux étrangers. Le shader PBR gouverné
applique désormais la mouillure à RGB/rugosité seulement, conservant l'alpha. Le système météo
existant fournit déjà les uniformes à ces matériaux étendus.

Deux tests supplémentaires passent : partage entre LOD, handles de textures et alpha conservés,
source PBR inchangée, hauteur invalide sans allocation partielle ; puis exécution du système
d'horloge avec temps contrôlé, paramètres conservés et matériau étranger inchangé.
**429 tests workspace PASS**, un probe timing ignoré ; Clippy workspace strict, build runtime
et check moteur WASM PASS. Une erreur initiale d'import de `OpaqueRendererMethod` a été corrigée
avant ces contrôles (type dans `bevy::material`, pas `bevy::pbr`).

Smoke natif 420 frames : événement `vegetation_prototype_wind_materials_prepared`, **4 matériaux**
pour une couche, sortie 0 sans erreur runtime observée. Capture `hero.png` inspectée : rendu
procédural actuel cohérent, mais **les arbres du scatter ne consomment toujours pas ce cache**.
Ce n'est pas une preuve GPU d'animation des prototypes ni de mouillure sur leurs matériaux.
L'affectation au scatter, les LOD/HLOD cohérents et la qualification visuelle/performance restent
à réaliser ; aucune gate fermée, aucun push.

Preuves : `E:/OpenRPG_Forge/.gate1-evidence-runtime/r1-prototype-wind-cache-20260906/`.
`tests.log` SHA-256 `4f969d2ca215e59ddb14c88c42aa10075b1247f20ebc84fcbd1f2795410d9911` ;
`runtime.log` SHA-256 `a10ae587a0bcadf627ad6aead7fc14928dc4ea8610c0fa5d21eebb5260c232d2`.
`clippy.log`, `build.log`, `wasm.log`, `metrics.json`, `hero.png` complètent ces preuves.

## Complément — liaison au scatter et cycles de qualité, 2026-09-06

L'inspection de `vegetation/quality/transition.rs` montre que chaque changement de qualité
réinstalle meshes et matériaux procéduraux. Les lots HLOD de `vegetation.rs` contiennent aussi
les arbres avec les arbustes/fougères : échanger les seuls arbres proches une fois serait incorrect.

`VegetationScatterProjection` expose maintenant en lecture les arbres du résultat de génération
déjà utilisé pour la projection active. Il conserve le même `Arc`, sans régénérer les poses ni
ajouter une source persistante. Le démarrage et la réconciliation de qualité réussie publient cette
vue avec la projection. Les marqueurs individuels conservent également `layer_id`, nécessaire
pour relier chaque placement au catalogue de prototypes sans identifier une couche par son nom
codé en dur.

Le test ECS de qualité existant a été renforcé : les 636 instances d'arbres sont conservées lors
du cycle Low/Medium/Ultra/High, et chaque marqueur proche/moyen garde la couche de son instance.
Ses contrôles existants de collisions, handles, sources et rejet de changement invalide restent
actifs. **429 tests workspace PASS**, un probe timing ignoré ; Clippy strict et check moteur WASM
PASS. Pas de nouveau test compté artificiellement : les assertions enrichissent le test existant.

Preuves : `E:/OpenRPG_Forge/.gate1-evidence-runtime/r1-prototype-scatter-bindings-20260906/`,
`tests.log` SHA-256 `5f7f4aebc4a81be2199209b7736bdfebf17d1ab567c9d4f0fc3e45ec7466624d`,
`clippy.log`, `wasm.log`. Aucun nouveau rendu ou capture pour ce lot sans changement visuel.
La projection des prototypes doit encore consommer cette vue, gérer la réconciliation et remplacer
les anciens arbres HLOD sans supprimer les autres végétaux. Le scatter rendu reste procédural ;
Gate 1 reste ouverte, aucun push ni promotion.
