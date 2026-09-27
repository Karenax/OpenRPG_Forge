# R1-040 — Budgets de post-traitement consommés par la météo

2026-09-06 local, branche Rust, non publié. **Gate 1 et R1-040 restent ouverts.**

## Implémenté

`PostProcessQualityRequest` valide l'identité de zone et la politique, conserve son hash et
permet une demande de tier sans changer les sources. Le CLI expose
`--post-process-quality low|medium|high|ultra` dans les deux modes vallée. Omission : comportement
authoré inchangé. Valeur invalide, doublon, argument absent ou mode sans vallée refusés.

Le consommateur est le système météo existant `project_post_processing`, pas un second writer
concurrent. Les interrupteurs du preset contrôlent :

- bloom : peut être supprimé par qualité ; réactivation avec les paramètres authorés, sans
  forcer un bloom désactivé dans la source météo ;
- étalonnage : grade météo courant ou `ColorGrading::default()` neutre, sans réécrire les grades ;
- tonemapping : TonyMcMapface ou `Tonemapping::None` explicite, sans enlever HDR, SSR ou TAA.

Les profils livrés désactivent seulement le bloom en Low ; les modes grading/tonemapping désactivés
sont exercés avec une politique synthétique valide. Ils ne sont pas prétendus vérifiés visuellement.

`weather.post_processing.quality` distingue tier demandé et tier observé en CPU. L'observateur
exige une caméra active unique, présence effective du bloom attendue, tonemapper attendu et tous
les champs du grading correspondant au grade courant ou neutre. Il ne confirme pas le contrôleur
global et ne prétend pas prouver un upload GPU. `bloom_requested` reste l'intention authorée :
il vaut true en Low même si `bloom_component_present` vaut false, ce qui est désormais explicité.

## Vérifications automatisées

Trois tests moteur ajoutés :

1. cycle Low/Medium/Ultra/High avec Low synthétique tout désactivé ; neutralité et retour exact
   du grade pluie, bloom/intensité, tonemapping, hash/source météo, horloge à 123 s, wetness et binding
   du mesh pluie conservés ;
2. deux caméras actives : aucun tier déclaré projeté, puis récupération après retrait de la seconde ;
3. politique de révision étrangère refusée, divergence de grading global ou highlights détectée.

Un test CLI couvre les quatre niveaux dans les deux modes, absence d'override et entrées invalides.
Les tests météo existants de transition clair/pluie restent exécutés ; le cycle qualité ci-dessus
fige le temps en pluie et ne constitue pas à lui seul une preuve de transition météo complète.

`cargo test --workspace --locked -q` : **362 PASS**.
`cargo clippy --workspace --all-targets --all-features --locked -- -D warnings` : PASS.
`cargo check -p forge_engine --no-default-features --features web --target wasm32-unknown-unknown --locked` : PASS.
Formatage et `git diff --check` : PASS.
Target `C:/Users/karen/AppData/Local/Temp/openrpg-forge-r1-064-target`, incremental=0,
`RUSTFLAGS=-C debuginfo=0`. Aucun nouveau lancement Chrome pour ce raccord.

## Exécution native

Artifacts dans `E:/OpenRPG_Forge/.gate1-evidence-runtime/r1-post-process-quality-20260906`.
Exécutable utilisé : `50071a558a1eb5c9cc4963ff0fab30f76d50321004d37a3872dd4d27cf52f2c2`.
RX 7800 XT/Vulkan, Full HD, WorldSketch rev3, caméra `human_bridge`, kit corrigé gouverné,
pluie. Deux lancements successifs identiques sauf `--post-process-quality low` puis `high`.
Options communes : `--capture-frame 1 --metrics-warmup-frames 600 --exit-after-frames 1020`.
Kit et preview identiques au protocole `R1_040_TERRAIN_LOD_RENDERER.md`, sans override terrain.

Les deux PNG ont été inspectés : vallée, pont, relief et pluie rendus, différences de bloom
modestes sur cette vue ; végétation tramée et silhouettes stylisées toujours visibles.
Codes 0, zéro ERROR. Avertissements préexistants Vulkan et invariant WGSL non masqués.
Sources météo identiques ; grade pluie conservé (-0,35 EV, température -0,006, tint 0,001,
saturation 0,86, contraste 1,08, gamma 1). Low observe bloom absent, High bloom présent.
High expose `render/bloom/elapsed_gpu` (dernier échantillon 0,19532 ms) ; Low n'expose aucun
diagnostic bloom. Ce constat est une preuve native de la passe, pas une moyenne de son coût.

| Post-traitement | FPS moyens | p95 ms | 1% low FPS | >16,7ms |
| --- | ---: | ---: | ---: | ---: |
| Low | 122,52 | 8,9276 | 103,91 | 0 |
| High | 115,80 | 9,3774 | 96,92 | 0 |

420 échantillons, 3,43/3,63 s : smoke court, pas benchmark de gate ou gain général revendiqué.
Capture Low émise 93/readback 96, 86 pipelines prêts ; High 92/95, 91 pipelines prêts ; huit
observations stables dans les deux cas. Le changement à chaud est testé en ECS, pas sur GPU.

## SHA-256

- ressource/adaptation : `a9d1625e99c25621214bc31533f756468efd410157cf6d47c216608e0ab0ae1c` ;
- tests post-traitement : `2dc35e52521754b95627f9b9ae0373f39fb15ca227b82ec4fd7e80c751797e99` ;
- `weather.rs` : `c05df7c73d2435465d6cdedfe00304d9bd29750ddd274141264eb6d4f2dd2bff` ;
- runtime `main.rs` : `90fcb0341f9ef7c2512e606f407939feff2b4da5b4637abb477299d8099fa1af` ;
- politique fichier inchangée : `24dee2d732e49b5d6c14525608622f6ac1c1db6fb31b25d9bdc01bbb65df88c3` ;
- politique liée à rev3 : `6ae47b0ce3d513683394749b625ac2a3dcbdfcd94173a6f84f4e3070f6c94871` ;
- source météo canonique runtime : `322ee487ea89a4709bbf101d78cffecc05084d7cfc1e8778c608c2bc7d2a2115` ;
- Low PNG : `bc50c3bcfe5c9914cf2d35644ce6592e1c577ca3e39b2afdaf64e71811c01ebd` ;
- Low JSON : `4a2427aca16ffa5600ce15bebd67f8f7b516da33ff0269244b4003ba0d4c6126` ;
- High PNG : `11f47df69a187e8d727a10a763269f9af09e3aa07a34f28c560ddb8222ef5fd1` ;
- High JSON : `6a78c25234fd2e268d558c96964229c66908efdb6bf770207b0040d2e4864587`.

## Prévu, non réalisé par ce lot

Budgets d'environnement, d'eau et de végétation complets, contrôleur qualité global, sélection
Studio, cycle GPU à chaud et qualification longue/humaine. Le brouillard volumétrique conserve
son fallback documenté ; aucun budget demandé n'est présenté comme une activation qualifiée.
Le présent raccord de post-traitement n'implémente pas ces domaines et ne ferme aucune gate.
Preuves locales sur arbre modifié, pas qualification finale ADR-0006 ; CI **NOT RUN**.
Aucune dépendance ajoutée, aucun asset Cadle, aucun commit ou push.

## Correction du relevé environnement après changements de qualité — 2026-09-06

Le relevé environnement était figé en PostStartup : une modification ultérieure des ombres,
de la résolution IBL ou du tonemapping pouvait laisser des métriques périmées. La projection
authorée reste unique au démarrage ; seul l'observateur est déplacé en PostUpdate, après les
writers météo/qualité en Update. Il reconnaît explicitement `none`, `tony_mc_mapface` et `other`.
La complétude attend le tonemapper demandé par la politique post-traitement validée, ou
TonyMcMapface sans override. Il n'écrit aucun composant de rendu ou document source.

Un nouveau test vérifie après démarrage : changement IBL 256→64, ombres désactivées, tonemapping
non demandé détecté, restauration et caméra inactive détectées ; hash source inchangé.
Le test de cycle post-traitement existant installe désormais également le plugin environnement :
Low synthétique sans tonemapping puis Medium/Ultra/High donnent des observations cohérentes,
sans perte des invariants météo déjà testés. **130 tests moteur PASS** sur cette révision.
Clippy moteur tous targets/features, check moteur WebAssembly, formatage et diff : PASS.
La suite workspace complète n'a pas été relancée pour ce correctif local d'observation.
Pas de nouvelle capture : changement d'observation CPU uniquement, pas de rendu modifié.
Ce contrôle de présence n'atteste ni l'exécution GPU ni l'application des budgets environnement.
Les valeurs de brouillard issues de la source restent des paramètres demandés, pas des mesures GPU.

SHA-256 `environment.rs` : `e0a8f42fa37a612dabc869de660b247497bebe256cedbcd518ba8b8f5ef3048b`.
SHA-256 `weather/post_process_tests.rs` : `b2d1197c1d3eaa5cd88d9d3901ab01ac5b07b98fa30a8c6cb9a7a5f8b3ad4a9a`.
