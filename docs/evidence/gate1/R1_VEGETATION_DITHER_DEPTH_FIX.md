# Gate 1 — Cohérence profondeur/couleur des transitions végétales

2026-09-05, branche `Rust`, local non publié. Complète l'essai invariant rejeté ; ce correctif
ne modifie pas le shader WGSL, les meshes, les placements ou les distances LOD.

## Cause isolée

Le run diagnostic `dither-zero` force temporairement le niveau de dither à zéro dans les
deux passes du shader végétal. Les points noirs disparaissent, avec un rendu natif valide
et aucun log ERROR. Cette désactivation n'est PAS conservée : elle supprime les transitions.

Inspection Bevy 0.19.1, `bevy_pbr/src/prepass/mod.rs` : pour un matériau opaque, sans cible
normales/vecteurs/deferred, `fragment_required` est faux. La profondeur est alors écrite
sans rejet du motif LOD ; la passe couleur rejette ensuite des pixels. Ces pixels gardent
une profondeur sans couleur correspondante. Les statistiques GPU du run initial confirment
zéro invocation fragment dans l'early prepass.

La variante standard avec `NormalPrepass` active la branche `PREPASS_FRAGMENT` de
`bevy_pbr/src/render/pbr_prepass.wgsl`, qui applique `visibility_range_dither` avant les sorties.
Le choix n'est pas un contournement du test de profondeur ni une modification de Bevy.

## Implémenté

Le plugin de végétation ajoute `NormalPrepass` aux caméras 3D qui ne le possèdent pas.
La requête filtrée couvre aussi les caméras créées ultérieurement et ne réinsère pas le
composant chaque frame. Le shader est restauré exactement ; dither, culling, bandes LOD,
vent et matériaux inchangés. Aucun fork, dépendance ou asset ajouté.

Coût assumé : attachement de normales `Rgb10a2Unorm` (source
`bevy_core_pipeline/src/prepass/mod.rs`), soit 8 294 400 octets bruts en 1920 × 1080
sans MSAA, hors alignements/allocations du backend, plus le travail fragment. Ce n'est
pas une mesure globale de VRAM et ce n'est pas une optimisation de performance.

## Preuve native

Dossier `E:\OpenRPG_Forge\.gate1-evidence-runtime\r1-vegetation-invariant-20260905`.
Scène embarquée, caméra joueur, RX 7800 XT/Vulkan, 1920 × 1080 ; options identiques :
`--visual-valley --player-camera --capture-frame 300 --metrics-warmup-frames 180
--exit-after-frames 420`. Comparaison `restored` (ancienne projection) / `normal-prepass`.
Le candidat WorldSketch et le kit Blender ne sont pas chargés dans ces runs.

Capture corrigée inspectée : les trous noirs des arbres sont remplacés par le fond visible
à travers le motif LOD. Le motif reste discernable ; aucune acceptation artistique du
crossfade, de la densité ou de la forme des plantes n'est revendiquée.
Run corrigé 13:39:55 UTC, code 0, parsing du JSONL : aucun ERROR.
Warnings Vulkan AMD et invariant deferred persistants, non masqués.

| Mesure | Avant | Correctif |
| --- | ---: | ---: |
| Frames | 240 | 240 |
| Moyenne CPU frame (ms) | 7,9101 | 8,0215 |
| p95 (ms) | 8,5722 | 8,5923 |
| 1 % low (FPS) | 48,05 | 47,78 |
| Maximum (ms) | 40,4565 | 43,7828 |
| Early prepass GPU, dernier relevé (ms) | 0,02844 | 0,04904 |
| Early prepass invocations fragment, dernier relevé | 0 | 2 283 489 |
| Early prepass primitives, dernier relevé | 71 273 | 71 273 |

Échantillons courts (~2 secondes), chacun avec un pic : suffisant pour observer un coût,
pas pour qualifier une régression chiffrée robuste ou l'absence de stutter. Géométrie et
placements inchangés ; ce sont des primitives, pas des draw calls.

SHA-256 :

- `normal-prepass.png` : `f8561c6988f4d00d1cfd72b71cd55cd38675d70f95a82665e08b22c47c52d185` ;
- `normal-prepass-metrics.json` : `902e52cff85a48024a592bad0d72802331ae92d48dce46247506fc24c16fdf72`.

Logs durables `normal-prepass-run.jsonl`, diagnostic non promu `dither-zero-run.jsonl`.
La capture ancienne et ses hashes sont décrits dans `R1_VEGETATION_INVARIANT_REJECTED.md`.

## Contrôles automatisés

- `cargo clippy --workspace --all-targets --all-features --locked -- -D warnings` : PASS ;
- `cargo test --workspace --locked -q` : **302 PASS**, Engine 100, Studio 23 ;
- nouveau test : ajout initial du prepass, caméra créée ultérieurement, entités non-caméra
  intactes, absence de multiplication des composants ;
- `cargo check -p forge_engine --no-default-features --features web --target wasm32-unknown-unknown --locked` : PASS ;
- `cargo fmt --all -- --check`, `git diff --check` : PASS ; shader original sans diff.

Même target et variables Cargo que les preuves précédentes. Aucune nouvelle dépendance.

## Complément Chrome/WebGPU

Bundle reconstruit avec succès via Trunk dans le dossier externe
`E:\OpenRPG_Forge\.gate1-evidence-runtime\r1-studio-dither-depth-20260905\dist`.
SHA-256 du WASM : `ccea8f690a74abe54b8d76fc76511d21db0ca24f10f31c168dbba446bc517ba1`.
Lancement Chrome local sur `http://127.0.0.1:1420/?forge_scene=visual-valley&forge_weather=clear`.
La capture inspectée dans la conversation montre le fond à travers les transitions végétales,
sans les trous noirs observés avant correction. Le motif LOD reste visible et la végétation
reste artistiquement non qualifiée. Cette capture n'est pas archivée comme fichier durable.

Relevé réel `2026-09-05T13:44:48.801Z`, conservé sans modification des statuts dans
`R1_VEGETATION_DITHER_WEBGPU_LEDGER.json` :

- canvas CSS et buffer : 2048 × 1157, un seul démarrage ;
- 60 échantillons de préchauffage, puis 120 frames sur 4,041 secondes ;
- moyenne 33,675 ms (29,70 FPS), p95 38,4 ms, 1 % low 9,59 FPS, maximum 111,4 ms ;
- six hashes des manifestes sélectionné/runtime alignés, une caméra active, météo claire ;
- consultation des dix derniers logs erreur/avertissement : liste vide, sans garantie exhaustive.

Ce relevé CPU de la boucle WASM n'est pas un timing GPU, ni un benchmark comparatif qualifié
1080p sur GPU cible. Les seuils de performance ne sont pas validés par ce run. Le ledger
conserve volontairement `gpu_presentation: not_observed` dans l'observation de scène :
l'inspection visuelle externe ne remplace pas une sonde de présentation automatisée.
Les tests workspace précédents (302 PASS) ne sont pas rejoués pour cet ajout documentaire ;
le JSON est vérifié contre le schéma Studio et le diff documentaire est contrôlé.

## Qualification restante

Le correctif dispose maintenant d'observations natives et Chrome/WebGPU locales.
Validation humaine et archivage durable de la capture Chrome toujours requis. Gate 1 ouverte, CI distante NOT RUN,
aucun commit/push, aucun changement artistique verrouillé.
