# R1 — Touffes procédurales plus fines et resserrées

Date : 7 septembre 2026. Branche Rust, base `f786a38aca2023f08d708687f99a47d14f34e6d4`,
worktree modifié préservé. Gate 1 ouverte, aucune publication ni acquisition d'asset.

## Implémentation

Règle commune de `forge_engine::vegetation`, sans retouche de carte : rayon maximal des
racines 1,15 → 0,8 m et largeur des brins 4,5–10 → 2,5–5,5 cm, avant échelle de l'instance.
Hauteurs 22–65 cm, courbure, vent, couleurs, IDs, seeds stables, placements et budgets
inchangés. Toujours 7 vertices / 3 triangles par brin, regroupés par chunk. Racines
reprojetées sur les triangles du terrain à leurs nouvelles positions dérivées.

Le resserrement concentre les brins existants ; il n'augmente pas la couverture totale
de la carte. Les plages de sol nu entre touffes restent intentionnellement visibles.
Ce n'est ni un outil d'édition Studio, ni une validation du parcours auteur de Gate 2.

## Protocole

Référence : `R1_GRASS_STABLE_VARIATION.md`, image et métriques de son lancement natif.
Candidat : `E:/OpenRPG_Forge/.gate1-evidence-runtime/r1-grass-slender-20260907`.
`Replay.ps1` conserve WorldSketch embarqué, caméra joueur, High, 1920 × 1080,
capture frame 300, sortie 420, métriques après 180. Forêt procédurale sans kit externe.
Même GPU RX 7800 XT / Vulkan. Comparaison de silhouette, pas benchmark de performance.
`Validate.ps1` conserve les commandes et paramètres Cargo des contrôles locaux.

## État des vérifications

Les huit tests ciblés `cargo test -p forge_engine grass --lib` passent, dont ancrage
terrain sculpté, stabilité après permutation/suppression et budgets exacts. L'assertion
de largeur 2,5–5,5 cm est ajoutée ; les assertions de hauteur et de raccord sont conservées.
Build natif PASS. Replay terminé le 7 septembre à 02:30:08 UTC : exit 0,
`runtime_errors_observed=false`, 87 pipelines prêts, capture demandée frame 300,
readback 303, PNG vérifié puis ouvert et inspecté. Comparaison au même cadrage :
brins effectivement plus fins et bases plus compactes, mais touffes encore isolées,
ombres très lisibles et sol largement découvert. Le candidat est conservé pour la
finesse locale, sans prétendre résoudre la couverture ou le naturel de l'ensemble.

240 échantillons : moyenne 9,187 ms / 108,84 FPS ; p95 9,849 ms ; 1 % low 48,66 FPS ;
maximum 40,702 ms, un hitch >33,3 ms. Smoke court incluant la capture : aucun gain
de performance ou verdict GPU cible déduit. Terrain source inchangé :
`18bd933d37f06a6a08a34b19938a78cb8f6fe8d9667b34bd2dcd6355956fd208`.

| Artefact | SHA-256 |
|---|---|
| forge_runtime.exe | F66F75A2BC0A33551C0A058B0426B06E98031CC2F17A246FC4D9D50AE71CA311 |
| native.png | 4DB6B8759C3F3B83A838F3478DB0F4FA3DEA6E0AE780273B5D0F6E183D829810 |
| native-metrics.json | FAFACBB8DE85FFB83001FD7861ED113263A9F781057CC5343C0AA6453AB6E2CB |

Contrôles terminés, `Validate.ps1` exit 0 : **561 tests workspace PASS / 1 timing
préexistant ignoré**, Clippy strict all-targets/all-features PASS, check Studio
WASM/Web PASS, fmt et diff check PASS. Le warning futur `proc-macro-error2 v2.0.1`
reste visible. Aucun nouveau replay navigateur : la preuve WASM est une compilation.

Logs SHA-256 :

- workspace.log : `B33E6D1D5844A234038203CF1E6920DC1B1A84F50AD6FF25E886B436B773BE91`
- clippy.log : `BB6E6E7951A820F95742FCFF13ABC3D7B29B09EAEA23F656E2FB068C4A40100C`
- wasm.log : `6E163D8D95E1697F3C360693D71F969F6494AA6A991E592187232E6FA09E1577`

CI distante NOT RUN. Qualité artistique, rendu Web de ce candidat et acceptations
humaines non qualifiés ; aucun passage de gate.
