# R1-040 — Topologies LOD terrain à bords conservés

2026-09-06, Rust local non publié. **R1-040 et Gate 1 restent ouverts.**
Ce lot fournit le calcul commun des meshes simplifiés ; il ne change pas encore le renderer.

## Implémenté

`forge_world::TerrainLodTopology::new` construit une topologie déterministe pour les pas
1, 2, 4, 8, 16 et 32. Les cellules intérieures utilisent deux triangles. Les cellules touchant
le bord utilisent un éventail autour d'un échantillon central existant ; les 128 segments de
périmètre de deux mètres sont conservés intégralement. Il n'y a ni jupe masquant un trou,
ni hauteur inventée, ni besoin de connaître le LOD du voisin pour partager le même bord.

`project` lit le chunk complet, valide sa grille et ses attributs, puis copie les positions,
normales, UV et poids de matériaux retenus. Au pas 1, tous les tableaux et indices sont identiques
au rendu source. `TerrainLodMesh` est une sortie de présentation sans collider ni document
persistant. Les sources de terrain et les collisions ne sont jamais réécrites.

Les pas non pris en charge, attributs manquants/non finis, poids invalides, grille déplacée
ou ordre de triangles source endommagé sont refusés avec une erreur typée.

| Pas | Sommets/chunk | Triangles/chunk | Triangles pour 64 chunks au même pas |
| --- | ---: | ---: | ---: |
| 1 | 1089 | 2048 | 131072 |
| 2 | 413 | 696 | 44544 |
| 4 | 205 | 280 | 17920 |
| 8 | 149 | 168 | 10752 |
| 16 | 133 | 136 | 8704 |
| 32 | 129 | 128 | 8192 |

Ce sont des comptes géométriques CPU, **pas des triangles effectivement soumis au GPU**, des
draw calls ou une mesure de FPS. Le coût des bords détaillés est volontaire et reste visible.
Les profils courants utilisent les pas 8/16 (Low), 4/8 (Medium), 2/4 (High et Ultra).

## Réellement vérifié

Quatre nouveaux tests :

- six topologies répétées exactement, comptes strictement décroissants, triangles non dégénérés
  orientés vers le haut, aire XZ totale exacte, deux incidences opposées par arête intérieure et
  exactement 128 segments unitaires sur le périmètre ;
- projection des 64 chunks à chacun des six pas, attributs retenus exacts, identité complète du
  pas 1 et hash du cache source inchangé, données collider incluses ;
- vallée WorldSketch réellement sculptée (`carved_terrain`, distincte de `terrain`) : les 112
  voisinages de chunks sont comparés avec les 36 couples de pas, soit 4032 raccords. Chaque
  bord partage les mêmes 33 positions, normales, UV et poids de matériaux ;
- sept pas invalides et cinq corruptions de payload refusés.

`cargo test --workspace --locked -q` : **352 PASS**.
`cargo clippy --workspace --all-targets --all-features --locked -- -D warnings` : PASS.
`cargo check -p forge_world --target wasm32-unknown-unknown --locked` : PASS.
`cargo fmt --all -- --check` et `git diff --check` : PASS.
Environnement : Rust épinglé, target
`C:/Users/karen/AppData/Local/Temp/openrpg-forge-r1-064-target`, `CARGO_INCREMENTAL=0`,
`RUSTFLAGS=-C debuginfo=0`. Pas de lancement GPU : aucun consommateur renderer n'a changé.

Pins de cette preuve :

- implémentation `lod.rs` : `d4d215faedb99e5cd4f32fc8f52f2bef728a7e8feb5bd000cc1d3a6f32760900` ;
- tests `lod/tests.rs` : `f94ad8ee794e5ed874f00aaa7073a4d35955529028fb6633176ad00059fcbb6d` ;
- WorldSketch source inchangé : `5b2088952cad01732a53b371462247cf386bb25141c39ff97a60fb14bd2e5fc1` ;
- politique inchangée : `ec4c0db5b3a0287a309a541cb889120143cecd515dec2cea748f527c0c0fd9e3`.

## Raccord runtime restant

État historique à la clôture de ce lot pur. Le raccord réalisé ensuite est décrit dans
`R1_040_TERRAIN_LOD_RENDERER.md`, avec limites distinctes et sans passage de gate.

Le renderer garde ses 64 meshes complets en LOD 0. Il reste à sélectionner les niveaux par
distance/preset, publier les comptes effectivement actifs, gérer les transitions et le culling,
puis inspecter et mesurer le résultat natif/WebGPU.

Point d'intégration important établi par inspection : `TerrainRuntimeBindings` relie les handles
de rendu complets aux colliders ; `terrain_update` vérifie les tableaux exacts après transaction.
Remplacer simplement ces meshes par un LOD casserait cette vérification. Le raccord doit garder
la projection canonique vérifiable et traiter les LOD comme caches dérivés, avec invalidation
des seuls chunks affectés, sans toucher aux retouches ou aux bindings physiques. Cette garantie
n'est pas encore prouvée en ECS par ce lot. Aucun gain runtime n'est revendiqué avant ce raccord.

Preuves locales sur arbre modifié, pas qualification finale ADR-0006. CI **NOT RUN** ;
revue artistique humaine et passage explicite de gate toujours dus. Aucune nouvelle dépendance,
aucun asset Cadle, aucune publication, aucune fonction de Gate 2 commencée.
