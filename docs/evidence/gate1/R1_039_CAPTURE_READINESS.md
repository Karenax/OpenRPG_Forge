# R1-039 — Capture bornée après disponibilité observée

2026-09-05, Rust local non publié. **R1-039 et Gate 1 restent ouverts.**
Déclencheur : la capture High noire conservée dans `R1_040_SHADOW_BUDGETS.md` montre qu'un
nombre fixe d'images et une sortie code 0 ne prouvent pas un rendu prêt.

## Implémenté

Le harness partagé de capture simple installe un observateur seulement lorsqu'un PNG est
demandé. Il lit `PipelineCache` dans le RenderApp et transfère les compteurs vers le MainWorld
via ExtractSchedule, sans thread ou canal externe. La capture exige :

- huit observations nouvelles consécutives d'un cache non vide, de taille stable,
  sans pipeline Queued/Creating/Err ; un échantillon répété n'avance pas le compteur ;
- une fenêtre primaire de dimensions non nulles ;
- le kit gouverné entièrement chargé, LOD et collisions compris, lorsqu'il est présent ;
- des primitives opaques non nulles si le backend expose ce diagnostic. Une absence de
  diagnostic n'est pas transformée en observation GPU positive.

`--capture-frame` devient une borne minimale. Le PNG n'est demandé qu'une fois et les logs
conservent frame minimale, frame de demande réelle et frame du callback de readback.
Le snapshot des pipelines est gelé à la demande. À la sortie, le harness exige un callback
de cette capture puis un fichier régulier non vide ; un ancien fichier ne suffit plus.
Le callback n'est pas nommé « écriture réussie » : le contrôle fichier et le garde ERROR natif
restent distincts. Le délai `--exit-after-frames` n'est jamais étendu ; un manque de disponibilité
ou de readback provoque une erreur. Un watchdog de 120 secondes borne aussi les captures sans
sortie en nombre d'images. Les captures vidéo du pont utilisent leur harness séparé, inchangé.

La télémétrie additive `capture_readiness` documente ces observations. Le collecteur de timings
reste inchangé : aucune image lente n'est filtrée, aucune fenêtre de mesure n'est décalée
silencieusement. Les runs interactifs sans capture n'installent pas cet observateur.

## Vérifications

Replay final `cargo test --workspace --locked -q` : **348 tests PASS**.

Tests ciblés : cache vide, observation répétée, taille modifiée, compilation/erreur,
scène incomplète, demande unique, readback manquant, ancien fichier refusé ; watchdog testé
avec `TimeUpdateStrategy::ManualDuration(121 s)`, sans attendre réellement ni produire de PNG.
Clippy workspace/all-targets/all-features avec `-D warnings`, build Runtime natif et check
moteur `--no-default-features --features web --target wasm32-unknown-unknown` : PASS.
Pas de lancement Chrome nouveau ; compilation WASM n'est pas qualification navigateur.

Artefacts : `E:/OpenRPG_Forge/.gate1-evidence-runtime/r1-capture-readiness-20260905`.
Runtime utilisé : `40af47fc92c7f0af136e9860d22edfb1395f80f5824a3cd3b2381c0c2b466105`.
Build avec target `C:/Users/karen/AppData/Local/Temp/openrpg-forge-r1-064-target`,
`CARGO_INCREMENTAL=0`, `RUSTFLAGS=-C debuginfo=0`, Rust épinglé.

### Vallée : demande précoce réellement différée

Commande : `--world-sketch-visual-valley --weather rain --shadow-quality high
--capture <early.png> --capture-frame 1 --metrics <early.json>
--metrics-warmup-frames 600 --exit-after-frames 1020`, avec le kit
`E:/OpenRPG_Forge/.gate1-evidence-runtime/r1-063-authored-preservation-20260905-b/rebuild-1/asset-kit-build.v1.json`
et l'aperçu
`E:/OpenRPG_Forge/.gate1-evidence-runtime/r1-vegetation-normals-after-20260905/human_bridge-clear/preview.json`.

RX 7800 XT / Vulkan, Full HD, AutoVsync : demande différée de frame 1 à **131**, 91 pipelines
prêts, zéro pending/failed, huit observations stables ; callback **134**, sortie **1020**, code 0,
zéro ERROR. Kit 20 instances/36 colliders ; terrain
`18bd933d37f06a6a08a34b19938a78cb8f6fe8d9667b34bd2dcd6355956fd208`.
PNG inspecté : vallée, pont, végétation et pluie visibles, pas d'écran noir. Pas de verdict artistique.
420 échantillons après warmup : 109,97 FPS moyens, p95 9,836 ms, 1 % low 84,35 FPS,
zéro image >16,7 ms. Run court de ~3,82 s mesurés, pas qualification de performance.

### Régression du harness Gate 0

Sans mode vallée : `--capture <gate0.png> --capture-frame 1 --metrics <gate0.json>
--metrics-warmup-frames 180 --exit-after-frames 420`.
Demande à frame **37**, 43 pipelines prêts, huit observations stables, callback **40**,
code 0 et zéro ERROR. PNG inspecté : sol vert et cube bleu présents. Ce smoke du harness ne
remplace ni ne réécrit les preuves historiques de Gate 0.

### Échec attendu, sans faux PNG

`--visual-valley --capture <deadline7.png> --capture-frame 1 --exit-after-frames 7` :
code **1**, `capture_failed` indique que la disponibilité n'a pas été atteinte, aucun PNG créé.
Une première commande avec sortie frame 2 a été refusée plus tôt par le CLI (code 2, règle
existante des six images minimum après la demande) ; son log `too-short.log` est conservé mais
n'est pas utilisé comme preuve du garde de rendu. `deadline7.log` est la preuve pertinente.

| Artefact | SHA-256 |
| --- | --- |
| early.png | `62ca2a44e76636f41736aec713014f952f41cd8fcbf6b61f46559739a3ece081` |
| early.json | `c1299314d1a869961d105e546c4e4c8f1043f7518d97b29bfbed58a0ef92b191` |
| gate0.png | `50ad9ac419ef61a808099353a10b180663327633b49c61d51de19be263939256` |
| gate0.json | `ae13353ec6e009d2429b4ff361200fc812707cc39f0527dc4b544fdde484cad4` |
| deadline7.log | `cc04db9b5a48ef50e5629debf1a3d69d4f715e8fd97ca361230aa576d06404c6` |

WorldSketch fichier inchangé avant/après :
`5b2088952cad01732a53b371462247cf386bb25141c39ff97a60fb14bd2e5fc1`.

## Toujours ouvert

Ce garde observe les pipelines connus, sans provoquer la compilation de toutes les variantes
futures. Il ne prouve ni correction de chaque pixel, ni disponibilité de chaque ressource GPU,
ni absence de trou lors d'une transition de qualité à chaud. Le diagnostic opaque peut être
retardé ou indisponible ; l'inspection reste requise. R1-039 exige encore le préchauffage des
parcours/profils et leur qualification, au-delà de ce correctif de capture. Profils globaux,
revue humaine, comparaison artistique et worktree propre ADR-0006 restent dus.
CI distante **NOT RUN**. Aucune dépendance, aucun asset Cadle, aucune publication ou nouvelle gate.
