# R1-040 — Raccord terrain LOD natif, qualification partielle

2026-09-06 local (journaux UTC du 2026-09-05, 22:25–22:26). Branche Rust, arbre modifié,
non publié. **Gate 1 et R1-040 restent ouverts.** Aucun début de Gate 2.

Le défaut de culling Low décrit dans le premier lot ci-dessous a été corrigé ensuite : voir
le complément « Horizon conservé » en fin de rapport. Les premières preuves restent historiques.

## Implémenté

`VisualValleyTerrainQualityPlugin` et `TerrainQualityRequest::request` consomment uniquement
la section terrain de la politique validée. Le natif expose `--terrain-quality low|medium|high|ultra`.
Sans cette option, le rendu précédent est conservé ; ni preset global ni contrôleur adaptatif
ni sélecteur Studio ne sont implicitement activés.

Les 64 meshes complets et bindings Rapier restent disponibles pour la vérification des transactions.
Leurs entités de rendu deviennent cachées. Trois copies de présentation par chunk, LOD 0/1/2,
sont recentrées au centre des bornes du chunk source et partagent origine, matériau et bornes.
Elles sont des entités sœurs, pas des enfants du rendu canonique caché. Les données persistantes,
positions authorées, colliders et handles canoniques ne sont pas réécrits par le choix de qualité.

Les bandes complémentaires sont LOD0-distance → LOD1-distance, puis LOD1-distance →
LOD2-distance ; le dernier niveau disparaît entre LOD2-distance et cull-distance. Le shader
utilise le dither standard Bevy, y compris les variantes de prépasse appropriées.
Les distances CPU et GPU utilisent maintenant la même origine (`use_aabb=false`).

La projection observe le cache terrain en Update, après les transactions des schedules fixes.
Hash global/tier inchangés : aucun recalcul de topologie. Sinon, fingerprints par chunk et
préparation de tous les payloads affectés avant mutation ; bindings/meshes dérivés prévalidés.
Les handles et entités dérivés sont réutilisés. Une erreur refuse la projection et ne publie
pas un nouveau tier projeté. Ce contrôle intervient lors d'une actualisation, pas comme
surveillance permanente de toute suppression ECS externe.

`terrain_lod` dans les métriques natives et `terrain_lod_projected` dans les logs comptent
les **allocations CPU**, pas les LOD réellement actifs, draw calls ou uploads GPU terminés.
High : 192 meshes dérivés, 109248 sommets, 193536 triangles alloués, en plus des caches
canoniques. Low : 192, 87744 et 150528. La mémoire de présentation augmente volontairement
pour préserver les copies canoniques vérifiables ; aucun gain mémoire n'est revendiqué.
L'overlay historique « LOD 0 : 64 chunks » décrit ces caches canoniques, pas les niveaux actifs.

## Échec visuel conservé et correction

Première tentative dans `E:/OpenRPG_Forge/.gate1-evidence-runtime/r1-terrain-lod-20260906`,
exécutable `c823defdddd49deb46a9ec819d432080466806664516e13ab0289a64f0c4e3ae`.
Les captures High/Low ont révélé du terrain proche absent malgré zéro erreur GPU.
**Ces images sont rejetées**, pas des preuves de qualité ou de performance acceptée.

Cause identifiée dans Bevy 0.19.1 : la sélection CPU `use_aabb` utilisait le centre du chunk,
mais `mesh.wgsl` et `prepass.wgsl` appellent le calcul de dither avec l'origine du modèle.
Les meshes terrain contenaient des positions absolues et avaient tous Transform identité.
La correction est le recentrage de copies de présentation, y compris LOD0 ; elle ne modifie
ni Bevy ni la projection canonique utilisée par les transactions.

## Vérifié

Quatre nouveaux tests moteur : cycle High/Low/Ultra/Medium/High, données sources/colliders
inchangés, handles stables, 256 assets mesh au total, rendu canonique caché et copies recentrées
reconstituant les positions à 0,1 mm près ; invalidation synthétique d'un seul chunk ; refus
atomique si un binding dérivé manque ; bandes complémentaires et politique invalide refusée.
Un nouveau test CLI couvre quatre valeurs, deux modes vallée, omissions, doublons et invalides.

- `cargo test --workspace --locked -q` : **357 PASS**, aucun échec.
- `cargo clippy --workspace --all-targets --all-features --locked -- -D warnings` : PASS.
- `cargo check -p forge_engine --no-default-features --features web --target wasm32-unknown-unknown --locked` : PASS.
- `cargo fmt --all -- --check` et `git diff --check` : PASS.

Target local `C:/Users/karen/AppData/Local/Temp/openrpg-forge-r1-064-target`,
`CARGO_INCREMENTAL=0`, `RUSTFLAGS=-C debuginfo=0`. Aucun lancement Chrome pour ce raccord.

### Exécutions corrigées

Artifacts : `E:/OpenRPG_Forge/.gate1-evidence-runtime/r1-terrain-lod-centered-20260906`.
Exécutable utilisé : `1a1b6ccfae9460fecbb6b3cf46de9c16a0e60c8db2c6991c726c091d4d5294d6`.
RX 7800 XT, Vulkan, pilote 26.8.1, 1920×1080 ; même exécutable, caméra `human_bridge`,
WorldSketch rev3, clair et kit gouverné corrigé (20 instances, 36 colliders).
Runs successifs, aucune compilation en parallèle. Warmup600, exit1020, 420 samples,
environ 3,6 secondes mesurées : **smoke court, pas benchmark de gate**.

| Terrain | FPS moyens | p95 ms | 1% low FPS | >16,7 ms | Primitives opaques GPU, scène entière |
| --- | ---: | ---: | ---: | ---: | ---: |
| Sans override | 115,82 | 9,2703 | 96,95 | 0 | 285374 |
| High | 114,30 | 9,5225 | 88,66 | 0 | 253798 |
| Low | 115,75 | 9,3894 | 91,79 | 0 | 206526 |

Le travail géométrique observé diminue ; **aucun gain de cadence n'est démontré**.
Codes0, zéro log ERROR. Avertissements préexistants Vulkan et invariant WGSL conservés.
Capture demandée au plus tôt frame1, émise High64/Low82/baseline60 après 8 observations
stables ; readback High67/Low85/baseline63, 90/90/89 pipelines prêts.
High et Low corrigés inspectés : terrain proche rétabli, détails et pont visibles.
High conserve le relief lointain. **Low n'est pas accepté visuellement** : son cull384m
retire du terrain alors que végétation et certains objets lointains restent visibles.
Cette incohérence inter-domaines doit être résolue avant qualification des profils globaux.

Reproduction depuis le workspace Rust, avec le binaire ci-dessus :

```powershell
$kit='E:/OpenRPG_Forge/.gate1-evidence-runtime/r1-063-authored-preservation-20260905-b/rebuild-1/asset-kit-build.v1.json'
$preview='E:/OpenRPG_Forge/.gate1-evidence-runtime/r1-vegetation-normals-after-20260905/human_bridge-clear/preview.json'
# Choisir un nouveau dossier de preuve ; High/Low remplacent seulement l'option terrain.
forge_runtime --world-sketch-visual-valley --asset-kit-build $kit --asset-kit-preview $preview --weather clear --terrain-quality high --capture fresh/high.png --capture-frame 1 --metrics fresh/high.json --metrics-warmup-frames 600 --exit-after-frames 1020
forge_runtime --visual-valley --terrain-quality high --terrain-partial-update-probe
```

Le second parcours n'est pas une substitution synthétique de ressource : vraie transaction
native, delta +5m au sample31/31, ticks soumission3/application4/vérification6 puis deux ticks.
Trois meshes et un collider/backend Rapier vérifiés ; les 64 entités de rendu, assets mesh et
entités collider sont préservés. Log LOD suivant : hash cible
`1fe904debcc863cbdb0bf677902fe99081cfc0641405d005f30d5aed3a9b0631`, **3 chunks reconstruits**.
Code0. Le cache LOD suit le hash cible ; ce run ne prouve pas une traversée joueur après retouche.

## Pins SHA-256

- `terrain_quality.rs` : `da65cceaee64242bf27465755ed75e26589cbb58cc13ada64cbdcb49722bd0e9`.
- `terrain_quality/tests.rs` : `fbffad47db09bfeb2fd763f8664845519acbe3d22f87aff1ac5fa04321dbf70a`.
- shader terrain : `ef9724f666b74f7d7e6ae4210ecce4a035337ee0cfd969186cce328f640b800f`.
- WorldSketch fichier inchangé : `5b2088952cad01732a53b371462247cf386bb25141c39ff97a60fb14bd2e5fc1`.
- politique fichier inchangée : `ec4c0db5b3a0287a309a541cb889120143cecd515dec2cea748f527c0c0fd9e3`.
- terrain sculpté canonique : `18bd933d37f06a6a08a34b19938a78cb8f6fe8d9667b34bd2dcd6355956fd208`.
- politique liée en mémoire à rev3 : `57c8099e9be1b6b150b5d678a0920fadd5fbbfaa50d378d7e9c43dbbbdfd2a35`.
- High PNG : `999c5167b8c35c167172e35ec88f9e49ebbf0990cf3c66ed7cd1876785b7a452`.
- High JSON : `f9894972c9e8b438d032ce31b0b23bbcd93963ada7ae96d8acdce51071223a5c`.
- Low PNG : `86980d70076a7ada90c6e3e90cea2cc9ac50ecf8c63cef35e15f504969c20d2f`.
- Low JSON : `c481ee5f51931bc2d6a84d2202ca1b228b9a0b73f152660169e5673cf459adf9`.
- baseline JSON : `a0a28f16a0b04fb1cbb952327981ee69de3bfc9f74086cdf7fea995650ab31af`.
- probe log : `617278b87219b048d5de4a004b86f9a0386985c0b75db7ee478f523ae6bb2b77`.

## Restant / verdict

Raccord technique et retouche native vérifiés, pas profil global terminé. Il reste le culling
cohérent entre domaines, les transitions en mouvement, les comptes actifs, le cycle GPU à chaud,
le raccord Studio et sa qualification réelle, la matrice performance longue/milieu de gamme
et l'acceptation artistique humaine. Aucun asset Cadle, aucune nouvelle dépendance.
Preuves locales sur arbre modifié, pas qualification finale ADR-0006. CI **NOT RUN**.

## Complément — Horizon conservé (2026-09-06)

La première politique faisait disparaître le terrain dès 288 m en Low, jusqu'à 384 m, au milieu
d'une vallée de 512 × 512 m. Des objets lointains restaient
visibles sans leur relief. Correction de présentation, sans modifier WorldSketch, terrain,
placements ou colliders : cull terrain **896 m pour les quatre tiers**, anciennement 384/512/640/896.
Les deux transitions de LOD et les pas 8/16, 4/8, 2/4, 2/4 restent inchangés.

Le fondu final a désormais pour largeur le minimum de `(lod1_distance-lod0_distance)` et
`(cull_distance-lod2_distance)`, au lieu de couvrir tout l'intervalle LOD2→cull.
Il commence à 800/784/768/704 m et finit à 896 m. Le terrain lointain simplifié reste opaque
entre la fin de sa transition d'entrée et ce fondu final. Le culling et la simplification
restent présents ; aucune validation n'est supprimée ou affaiblie.

Nouveau test `valley_horizon_stays_opaque_from_author_cameras_and_area_bounds` :
échec reproduit sur les anciens réglages (Low, fondu 288 m), puis passage après correction.
Les quatre caméras authorées et huit coins de la boîte englobante du terrain sculpté,
hauteur caméra +2 m, sont confrontés aux 64 centres de chunks pour chacun des quatre tiers :
3072 vérifications. Les coins bornent conservativement les positions au sol dans cette zone.
Ce n'est pas une qualification de mondes plus grands ou de caméras volant arbitrairement loin.
Le golden hash de la politique est mis à jour pour les trois valeurs de cull modifiées ;
le round-trip et les contraintes monotones restent testés. Sa révision reste celle de la
fixture de zone ; son identité de contenu change explicitement via SHA-256.

Validation finale : **358 tests workspace PASS**, Clippy workspace/all-targets/all-features PASS,
compilation `forge_engine` WebGPU/WASM PASS, formatage et `git diff --check` PASS. Commandes et
environnement identiques à ceux consignés dans le premier lot. Aucun lancement Chrome supplémentaire.

### Exécution native corrigée

Cache `E:/OpenRPG_Forge/.gate1-evidence-runtime/r1-terrain-horizon-20260906` ;
exécutable `e780539c57707b0425c27d35adbb8c6a2f631fbf858d98885818eabfe312893d`.
Même commande que le premier lot, avec `--terrain-quality low`, puis sans override pour
la référence ; mêmes kit/caméra/clair, Full HD, RX 7800 XT/Vulkan, warmup 600/exit 1020.
Deux runs successifs du même binaire, sans compilation concurrente ; codes 0 et zéro ERROR.
La capture Low inspectée retrouve les collines derrière les arbres et le donjon. Relief
simplifié encore anguleux, végétation tramée et acceptation artistique toujours ouverte.
Kit 20/20 et colliders 36/36 ; hash terrain et WorldSketch inchangés.

| Terrain | FPS moyens | p95 ms | 1% low FPS | >16,7ms | Primitives opaques scène entière |
| --- | ---: | ---: | ---: | ---: | ---: |
| Sans override | 116,52 | 9,2934 | 91,79 | 0 | 285374 |
| Low, horizon conservé | 114,96 | 9,5575 | 81,54 | 0 | 208430 |

420 échantillons, environ 3,6 s : pas de qualification performance longue. Aucun gain de cadence
revendiqué. Les allocations Low restent 192 meshes/87744 sommets/150528 triangles ; seul le
nombre de chunks affichés augmente. La précédente mesure Low incomplète observait 206526
primitives : le relief rétabli ajoute 1904 primitives dans cette vue (compteur GPU scène entière,
pas une mesure isolée ni un nouveau benchmark strict entre ces deux exécutables).
Capture Low émise frame 58/readback 61 après 90 pipelines prêts et 8 observations stables.

Pins nouveaux, les anciens hashes ci-dessus restent associés aux runs historiques :

- politique fichier : `24dee2d732e49b5d6c14525608622f6ac1c1db6fb31b25d9bdc01bbb65df88c3` ;
- politique canonique fixture : `3b442b45d834ccd4fa8864710adf54acc6f00b2c1ae90914fab503bd11534906` ;
- politique canonique liée à rev3 : `6ae47b0ce3d513683394749b625ac2a3dcbdfcd94173a6f84f4e3070f6c94871` ;
- `terrain_quality.rs` : `61821bcb65237b897d0cef169b8581baaac58eb195fb4307e282572a01b2b428` ;
- tests : `3187de33baa9e4f20786b8bfc7b25f5feabb67cf2ea1349b26cb87c613b1d3f6` ;
- Low PNG : `c9ef5638d2b4a2a44371f01cd281c4f5627bbca4da0f0e4ba5585e940c88a71f` ;
- Low JSON : `d17908f4c2273f5c845bd34872e4fc83f8164a67ecbfe95b6b35dd3b4432957f` ;
- baseline JSON : `cd11124ce89363d8988e38a5272d28450884f1f6d418742af3cdd061a9491651`.

Les tests de préservation des sources, handles et actualisation partielle restent en place.
La couverture de l'horizon dans cette vallée ne valide pas le contrôleur global, les autres
domaines de qualité, le mouvement ou Studio. R1-040 et Gate 1 restent ouverts ; CI **NOT RUN**.
