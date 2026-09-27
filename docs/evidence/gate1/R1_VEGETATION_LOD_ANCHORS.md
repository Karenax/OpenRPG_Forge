# Gate 1 — Repères et fondus des LOD végétaux

Date : 2026-09-06. Workspace Rust existant, modifications locales préservées, sans publication.
Suite du constat visuel de `R1_052_CADLE_VISUAL_GAP.md`, après la densité d'herbe par zones.

## Défaut et correction

Les plages High précédentes faisaient fondre les arbres de 24 à 72 m puis de 72 à 180 m.
Les silhouettes pouvaient donc rester pointillées sur une grande partie de la vue. De plus,
les instances individuelles mesuraient leur distance depuis leur propre origine, alors que
le HLOD utilisait l'origine du lot pour le shader et le centre AABB pour le culling CPU.
Des marges numériquement identiques ne prouvaient pas une transition spatiale complémentaire.

Le correctif conserve la représentation de l'ADR-0007 et les chemins publics Bevy 0.19.1 :

- fondus bornés aux huit derniers mètres de chaque intervalle, donc **64–72 m** et
  **172–180 m** en High ; les fins de portée du profil ne sont pas augmentées ;
- les UV0 XZ et UV1 Y des seuls meshes HLOD procéduraux transportent la racine locale
  de chaque placement, identique pour tous les sommets de sa silhouette ; aucun asset texturé
  n'utilise ce matériau interne et les meshes individuels ne possèdent pas UV1 ;
- le shader reconstruit la racine monde, élévation comprise, et émet le facteur de fondu
  signé attendu par Bevy, complémentaire du LOD individuel. Le vertex shader partagé alimente
  le même rejet dans les passes de normales/profondeur et de couleur ;
- la portée CPU du lot est élargie conservativement du rayon maximal des racines par rapport
  à son origine. Le shader garde la **coupure réelle à 384 m par placement** en High.
  Cette plage CPU élargie n'est pas une augmentation de la portée visuelle déclarée ;
- les uniforms de distance sont mis à jour explicitement après `apply_quality`, dans la même
  image que le nouveau profil. Les autres paramètres de temps, interaction et wetness restent gérés
  par le writer existant.

Le contrat signé est celui exposé par les sources locales Bevy `mesh_functions.wgsl` et
`pbr_functions.wgsl` : apparition -16→0, disparition 0→16. Le masque de rejet Bevy est importé,
pas remplacé par un nouveau motif. Aucun fork, phase GPU personnalisée, raw wgpu, dépendance,
texture ou ressource Cadle n'est ajouté.

## Invariants et coût structurel

WorldSketch, terrain, document végétation, IDs, poses, géométrie et colliders sont inchangés
par ce lot. Le jeu de données courant reste celui de la correction précédente :
12 508 placements dont 9 606 touffes et 636 arbres ; 3 666 entités visuelles,
64 lots d'herbe et 64 HLOD, 614 784 brins en High, 1 860 176 triangles de meshes végétaux uniques.
UV1 ajoute huit octets par sommet HLOD (34 664 sommets, 277 312 octets de payload en High),
plus un vec4 d'uniforms. Ce décompte n'est pas une mesure exhaustive de RAM/VRAM.

Les compteurs de visibilité CPU HLOD portent désormais explicitement sur les lots conservateurs,
pas sur le nombre de silhouettes effectivement dessinées après rejet shader. Ils ne deviennent
ni un compteur de draw calls ni une preuve de visibilité GPU exacte.

## Vérifications

Trois nouveaux tests couvrent les racines UV exactes de tous les sommets arbre/arbuste/fougère,
la borne conservative CPU pour plusieurs directions et élévations, et les transitions/uniforms
High bornés. Le test de cycle Low/Medium/Ultra/High vérifie en plus les uniforms après une seule
mise à jour, avec conservation des sources, handles et colliders. Les anciens tests de géométrie,
budgets, marges complémentaires, culling, sources et qualité sont conservés.

Les premières validations sont suivies d'un jeu `*-final.log` après le correctif d'ordre des
systèmes. Les sorties intermédiaires restent identifiées comme telles, sans écrasement.
Artefacts locaux sous `E:/OpenRPG_Forge/.gate1-evidence-runtime/r1-tree-fades-20260906`.

Résultats finaux : **407 tests workspace PASS**, Clippy workspace tous targets/features strict
PASS, check moteur `--no-default-features --features web --target wasm32-unknown-unknown` PASS,
build runtime natif PASS, `cargo fmt --all -- --check` et `git diff --check` PASS.
Tous les appels Cargo utilisent `--locked`, Rust 1.98 épinglé,
`CARGO_TARGET_DIR=C:/Users/karen/AppData/Local/Temp/openrpg-forge-r1-064-target`,
`CARGO_INCREMENTAL=0`, `RUSTFLAGS=-C debuginfo=0`.

Deux captures finales ouvertes et inspectées par l'agent : `human-final.png` et `hero-final.png`,
1920 × 1080, RX 7800 XT/Vulkan, clair, High, `--no-vsync`, capture frame 600 après readiness,
warmup 240 et arrêt 1200. Invocation `forge_runtime-final.exe --world-sketch-visual-valley`,
`RUST_LOG=info`, build kit
`E:/OpenRPG_Forge/.gate1-evidence-runtime/r1-063-authored-preservation-20260905-b/rebuild-1/asset-kit-build.v1.json`.
Preview humain `E:/OpenRPG_Forge/.gate1-evidence-runtime/r1-ab-20260906/preview-human.json` ;
preview héros `examples/visual_valley/asset-kits/r1-063/runtime-preview-authored.v1.json`.
Options `--capture <vue>.png --metrics <vue>.json --capture-frame 600
--metrics-warmup-frames 240 --exit-after-frames 1200` ; logs `<vue>.log`.

Les deux processus se terminent avec code 0 et zéro ERROR. Les avertissements wgpu `@invariant`
et de chargement des layers Vulkan restent présents ; ce lot ne les corrige pas. Les deux vues
confirment 20 instances et 36 colliders du kit prêts, ainsi que les hashes terrain
`18bd933d37f06a6a08a34b19938a78cb8f6fe8d9667b34bd2dcd6355956fd208` et végétation
`49819e19e9baa077d040ee1d1f64474d084d86e200b3eef940e4e7ab8c084f30` inchangés.
Les fichiers WorldSketch et preview authoré restent byte-identiques (`5b208895…`, `bde50c49…`).

À hauteur humaine, les arbres proches/moyens auparavant perforés sont nettement plus pleins.
La vue héros confirme la conservation de la scène et du pont, mais montre encore des zones
pointillées lointaines et des silhouettes simplifiées : **pas de verdict global « tous les
pointillés sont résolus »**. La prairie reste trop découverte. Ces deux captures ne remplacent
ni les douze vues normalisées ni la revue artistique de Gate 1.

| Fenêtre finale avec capture | Échantillons | Moyenne ms | p95 ms | 1 % low FPS | Frames >33,3 ms |
|---|---:|---:|---:|---:|---:|
| humain | 959 | 10,007 | 11,019 | 54,13 | 1 |
| héros | 959 | 10,067 | 10,801 | 66,41 | 1 |

Ces fenêtres incluent la capture et ne sont pas des benchmarks comparatifs. La visibilité CPU
observée est de 280 entités/36 lots HLOD pour l'humain et 237/37 pour le héros, sans incohérence
hors plage ; elle ne mesure pas les silhouettes conservées par le shader.

| Artefact final | SHA-256 |
|---|---|
| `forge_runtime-final.exe` | `e477b72326ba9b5344a9bcc2e9fdfc62e047f1e5c9638db3e3a55e52de02057b` |
| `human-final.png` | `dbf04f808428b87d49f721067600baa42636c645865902ffdd5159d580b59a26` |
| `human-final.json` | `81969d8506554fb21f981da903c34b51c5c81b2906eb3580aae0aa6e20de8ee6` |
| `hero-final.png` | `d380421908e304e94d2a4d407417ef3cac2c2271af0001ebc0031888b98ca2bc` |
| `hero-final.json` | `1c1917609a5e9984c1a57dd41fa05bdb74db7225ab396137fe1d6d6e2de126a1` |
| `tests-final.log` | `edfc95819f517e9ba61b2e98cad35d91606efa2f7de633ffadc7f94817b42a0d` |

## Limites

Le fondu est intentionnellement encore visible dans ses bandes courtes. Ce correctif ne transforme
pas les silhouettes simplifiées en arbres réalistes, ne remplit pas les zones d'herbe nues et ne
qualifie pas à lui seul une traversée continue des transitions. Les formes des LOD étant distinctes,
leur raccord artistique reste à apprécier en mouvement. Le check WASM ne constitue pas un lancement
Chrome/WebGPU du shader. La comparaison Cadle complète, les performances finales et la revue humaine
restent dues ; CI distante NOT RUN selon ADR-0006, Gate 1 ouverte.
