# Gate 1 — Silhouettes végétales distantes

État ultérieur : les arbres proches/moyens ont ensuite reçu un candidat ramifié, avec nouveaux
décomptes et captures. Les descriptions de cônes ci-dessous restent historiques ; le HLOD est
inchangé. Voir `R1_BRANCHED_PROCEDURAL_TREES.md`.

## Complément ultérieur — normales des volumes procéduraux

2026-09-05 : le test `volumetric_vegetation_faces_point_outward` reproduit une orientation
entrante des faces du cylindre et du cône. L'ordre des sommets est inversé dans ces deux
primitives : normales radiales sortantes, normales de couronne également ascendantes.
Cela concerne les troncs, couronnes proches/moyennes et cônes d'arbustes ; aucun changement
des silhouettes HLOD, placements, seeds, sources, shaders ou assets gouvernés.
Les décomptes exacts existants restent verts : arbre proche 43 triangles, moyen 22,
arbuste 21. Aucun sommet, triangle, matériau ou entité supplémentaire.

Le test échoue avant correction puis passe. **330 tests workspace passent** après correction
(Engine 108, Runtime 53), Clippy engine/runtime toutes cibles avec warnings refusés passe.
Les captures normalisées `human_bridge` clair/pluie réussissent avant et après, sources
rehashées inchangées, fermeture code 0 et aucune ERROR. Dossiers externes :

- `E:/OpenRPG_Forge/.gate1-evidence-runtime/r1-vegetation-normals-before-20260905`
- `E:/OpenRPG_Forge/.gate1-evidence-runtime/r1-vegetation-normals-after-20260905`

Runtime après : `79abab2ad51d0bcfd134c8e997f96eb1064af39e7fb768dab3e06f10725b4cd9`.
Manifest avant : `7531ca34b26b6373d618ea5699269456e1367138d577c944402c630b85097cf2`.
Manifest après : `aed52972026a5ccbf85480990da191bb3a5dd184c225f7709ac7f7db84618dcf`.
Ils contiennent les hashes des PNG, métriques, entrées et exécutables.

Les vues claires avant/après et la vue pluie après ont été inspectées. **Aucun gain artistique
net démontré** : le matériau double face pouvait déjà compenser les normales entrantes visibles.
Le style conique reste inchangé ; la correction garantit la géométrie et ne résout pas ce défaut
artistique. Les animations ne sont pas figées et des compilations/tests ont chevauché les captures :
aucune conclusion de performance à partir de leurs FPS. Pas de nouveau lancement WebGPU pour ce
correctif partagé. Gate 1 et la revue artistique restent ouvertes.

## Preuve historique de silhouette

Date : 2026-09-05. Branche `Rust`, travail local non publié, sources et retouches préservées.

## Implémenté

Les anciens HLOD utilisaient deux rectangles opaques croisés par plante ; la couleur interpolait
entre écorce et feuillage sur toute la hauteur des arbres, produisant des poteaux lointains.
Le HLOD arbre utilise maintenant deux plans portant chacun un tronc étroit et trois couronnes
triangulaires, aux hauteurs/rayons du modèle procédural proche. Arbustes et fougères utilisent
des silhouettes effilées au lieu de rectangles. Aucun asset externe ni shader nouveau.

Arbre : 10 triangles contre 4, 26 sommets ; arbuste/fougère : 4 triangles inchangés. Le regroupement
par chunk, les plages de visibilité, le culling, les IDs, transformations et matériaux restent
inchangés. Ce sont toujours des silhouettes procédurales de preuve, pas des arbres artistiques finaux.

## Vérifié

- `cargo test --workspace --locked -q` : **298 PASS**, code 0 (Engine 98 ; autres décomptes
  identiques au rapport `R1_WORLDSKETCH_SCATTER_CORRIDORS.md`).
- `cargo clippy --workspace --all-targets --all-features --locked -- -D warnings` : PASS.
- Test de mesh complet et décompte exact ; nouveau test : tronc limité à 0,22 m de rayon,
  couronnes commençant à 1,75 m, hauteur maximale identique au LOD proche, 8 sommets d'écorce,
  18 de feuillage et poids de vent bornés. Les tests AABB/culling/batching restent verts.
- `cargo check -p forge_engine --no-default-features --features web --target wasm32-unknown-unknown --locked` :
  **PASS**, code 0, 3 min 08 s (première préparation de ce graphe de dépendances dans le target).
  Ce contrôle compile les types du renderer et de Rapier pour WASM ; il ne constitue ni un
  bundle WebGPU exécutable ni un lancement Studio/navigateur.
- Formatage et `git diff --check` : PASS.

Commandes avec `CARGO_TARGET_DIR=C:\Users\karen\AppData\Local\Temp\openrpg-forge-r1-064-target`,
`CARGO_INCREMENTAL=0`, `RUSTFLAGS=-C debuginfo=0`.

## Capture native

Dossier `E:\OpenRPG_Forge\.gate1-evidence-runtime\r1-hlod-silhouettes-20260905` :
`hero.png` et `metrics.json`, run du 12:41:00 UTC, code 0. Même candidat WorldSketch externe,
kit corrigé, aperçu et pont que le rapport des corridors ; mêmes frames 300/180/420,
RX 7800 XT/Vulkan/1920 × 1080. La source verrouillée par défaut n'a pas été modifiée.

Capture inspectée : les poteaux sont remplacés par des silhouettes lisibles de conifères.
7 275 placements, 3 568 entités et 64 lots HLOD inchangés. Sur ce candidat, le HLOD passe de
11 252 à 15 014 triangles ; le total des meshes végétaux uniques passe de 296 920 à 300 682
(+3 762, environ +1,27 %). Ce sont des comptes de géométrie, pas des draw calls GPU.

La compilation WASM tournait pendant cette capture : timings conservés dans le JSON mais exclus
d'une conclusion de performance. Les warnings Vulkan AMD et shader `@invariant` restent présents.
La palette jaune/rose, les berges sombres, la faible qualité des formes proches et la densité artistique
restent non qualifiées ; une amélioration de silhouette ne ferme pas Gate 1.

SHA-256 :

- binaire : `d4c0392ceda48028d9f36b6d551e372bf7ede6738865579d38f11c5611de62e4` ;
- capture : `935eeb206add91d95e5a8cf08804ebf78d028d7a52fe0bfe0285b367044e7ea9` ;
- métriques : `11621356148f88177a03678bfbb3d254b82aeb41ec0bd504ba2a73e65f6d61b7`.

Gate 1 ouverte, validation humaine due, candidat de creusement non promu. CI distante `NOT RUN`.
