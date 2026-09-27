# R1-040 — Qualité végétale initiale raccordée au rendu

2026-09-06 local, branche Rust, arbre hérité modifié, non publié.
**Gate 1 et R1-040 restent ouverts. CI distante NOT RUN.**

## Implémenté

`VegetationQualityRequest`, installé avant construction du plugin, valide la politique liée
au terrain puis dérive densité décorative, distances near/medium/HLOD/cull et brins par patch.
La préparation existante construit réellement meshes grass/HLOD et instances à partir de cette
sélection. Les documents et caches canoniques ne sont pas réécrits. Vent, interaction et portée
des ombres sont conservés ; une présentation incompatible est refusée, sans clamp silencieux.
Les couches avec collision restent protégées. Aucun nouvel asset ou dépendance.

CLI stricte `--vegetation-quality low|medium|high|ultra`, uniquement dans les deux modes vallée.
L'absence du flag conserve le chemin historique. `quality.initial_tier` et
`quality.canonical_placements` décrivent la préparation CPU ; les anciens compteurs de placements
décrivent alors la sélection de présentation, pas la population canonique ni des draw calls.
Pas de méthode de changement à chaud qui ignorerait une demande : cette capacité reste à faire.

## Vérifié automatiquement

Test WorldSketch rev3 avec terrain sculpté : quatre niveaux, hashes source inchangés,
636 arbres protégés, 7433 placements canoniques, distances des arbres et brins/patch cohérents,
vent et interaction conservés. Une politique aux portées incompatibles est rejetée.
La CLI teste niveaux, modes, arguments absents/invalides/dupliqués.

| Profil | Placements sélectionnés | Entités rendu | Brins | Triangles meshes alloués |
| --- | ---: | ---: | ---: | ---: |
| Low | 4043 | 2547 | 27120 | 65588 |
| Medium | 5689 | 3071 | 67640 | 148724 |
| High | 7433 | 3666 | 144992 | 305808 |
| Ultra | 9110 | 4212 | 271776 | 561560 |

Ce sont des compteurs CPU, pas la géométrie active ou les triangles GPU isolés de végétation.
`cargo test --workspace --locked -q` : **376 PASS**. Clippy workspace all-targets/all-features
avec `-D warnings` : PASS. Check `forge_engine --no-default-features --features web` vers
`wasm32-unknown-unknown --locked` : PASS. Target isolé habituel, incremental=0,
`RUSTFLAGS=-C debuginfo=0`.

## Exécution native

Deux exécutions séquentielles, sans compilation concurrente, même binaire :
`9ef372a633feece9ab05564588309a435a5891d2a561993cb6c59ad8bbd0a438`.
Mode `--world-sketch-visual-valley`, kit gouverné et aperçu `human_bridge-clear`, météo clear.
Kit : `r1-063-authored-preservation-20260905-b/rebuild-1/asset-kit-build.v1.json` ;
aperçu : `r1-vegetation-normals-after-20260905/human_bridge-clear/preview.json`.
Ces chemins sont relatifs au dossier local `E:/OpenRPG_Forge/.gate1-evidence-runtime`.
Ajouter `--vegetation-quality low` puis `ultra`, `--capture <profil>.png --capture-frame 1
--metrics <profil>.json --metrics-warmup-frames 600 --exit-after-frames 1020`.
Sorties dans son sous-dossier `r1-vegetation-quality-20260906`.

Les deux processus terminent à 0, sans ERROR dans les logs. Les deux PNG sont inspectés :
Low réduit nettement herbe et végétation distante, Ultra densifie les zones ; terrain, pont et
eau restent présents. Le rendu reste stylisé et tramé, pas une validation artistique humaine.
Captures émises/readback : Low 72/75, Ultra 57/60 ; 89 pipelines prêts et 8 observations stables.

| Profil | FPS moyen | p95 ms | 1 % low FPS | Samples |
| --- | ---: | ---: | ---: | ---: |
| Low | 118,96 | 9,0973 | 99,12 | 420 |
| Ultra | 112,38 | 9,6852 | 86,62 | 420 |

Smokes courts (~3,5–3,7 s), aucun hitch >16,7 ms ; pas un benchmark de qualification.
Vent/eau ne sont pas au même instant : aucune comparaison pixel à pixel ni gain isolé revendiqué.

## Pins SHA-256

- Terrain canonique sculpté : `18bd933d37f06a6a08a34b19938a78cb8f6fe8d9667b34bd2dcd6355956fd208`.
- Végétation source : `83b3c8c003677b6ed33f903465df54bb4dc82db1c1e3ba3dc60a64ee349c623e`.
- Politique liée rev3 : `6ae47b0ce3d513683394749b625ac2a3dcbdfcd94173a6f84f4e3070f6c94871`.
- Module quality : `f2fd892fb4d134582b8bae1688cf47fbf5e5a5cb55fb8fbef2ca1b92aa464aad`.
- Test quality : `3504135954a3f8695a91bf52a6027dd6ea24e7da8608ed661319ae247b2c7f78`.
- low.json : `9967144ff019cf63b32e4d8e2e756e1bf403379ea1cca797db405abdef4dedb2`.
- low.log : `d4bcf5b4152fda0205d9e97dc19f1c1bed633e3150c8ee7f04a61744b229dd57`.
- low.png : `2e31eb059998c84ad518a8dd7cab2a349de3022690be8e11b7ae0d9b3543823a`.
- ultra.json : `c2d1ee4a9c0461007ca6e7ffd329347cb5534cc1550e7156c75db553652a1774`.
- ultra.log : `be38c0ab0500fc223d6cf23be49769fa1a7f2a5ec3a0561e66cad49237cb2335`.
- ultra.png : `69c34322782dd0a001ccde979c85d562f75aedac064e72c3790dd6a368a88f4b`.

## Reste prévu, non qualifié

Changement végétal à chaud avec conservation des bindings, contrôleur global et sélection Studio.
Les positions individuelles des brins peuvent varier avec le budget ; seules les garanties des
placements de la sélection pure s'appliquent. Pas de qualification WebGPU visuelle dans ce lot,
ni benchmark long, acceptation artistique humaine ou passage de gate. Aucun asset Cadle ni Git publié.
