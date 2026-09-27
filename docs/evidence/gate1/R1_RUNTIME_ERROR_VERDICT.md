# Gate 1 — Verdict natif sur erreurs runtime

2026-09-05, branche Rust, travail local non publié ; Gate 1 reste ouverte.

## Défaut et correction

L'essai invalide décrit dans `R1_VEGETATION_INVARIANT_REJECTED.md` avait produit une erreur
de compilation shader, mais un code processus 0. Le code retour seul était donc insuffisant.

Le runtime observe maintenant les événements tracing ERROR indépendamment du filtre console.
Pour une exécution automatisée, un succès Bevy devient code 1 si une erreur a été observée.
Un code d'échec déjà présent est conservé. Les WARN seuls et la fermeture interactive ne changent
pas de comportement. La décision se prend après `app.run()` ; aucun arrêt brutal ou nouveau
thread de surveillance. Le subscriber d'observation est filtré au niveau ERROR, sans activer
les logs détaillés des autres niveaux. Aucune dépendance ajoutée.

La politique est volontairement conservatrice : une erreur même récupérée invalide ce run
automatisé. Elle ne détecte ni un défaut silencieux ni une erreur non émise via tracing.
Elle ne remplace pas l'inspection visuelle, les métriques ou les assertions de preuve.

## Vérifié

- `cargo test --workspace --locked -q` : **305 tests PASS**, dont trois nouveaux tests runtime.
- Tests synthétiques : succès sans erreur, WARN seul, erreur shader avec filtre console `off`,
  conservation d'un code 7, fermeture interactive inchangée, erreur asset depuis un worker
  partageant le dispatch. Ce ne sont pas des compilations GPU invalides réelles.
- `cargo clippy --workspace --all-targets --all-features --locked -- -D warnings` : PASS.
- `cargo build --workspace --locked -q` : PASS.
- `cargo fmt --all -- --check` et `git diff --check` : PASS après correction de formatage.
- Run natif RX 7800 XT/Vulkan de la vallée embarquée :
  `forge_runtime.exe --visual-valley --player-camera --exit-after-frames 240`.
  Fin à 240 frames, code 0 ; JSONL parsé, aucun ERROR. Les warnings GPU restent présents.
  Événement final à `2026-09-05T13:56:05.921355Z` : `success=true`,
  `runtime_errors_observed=false`, `automated_run=true`, `exit_code=0`.

Le premier smoke utilisait le filtre ambiant `RUST_LOG=warn`, donc ne conservait pas les
événements INFO. Le second utilise explicitement `info,wgpu_core=warn,wgpu_hal=warn,naga=warn`
pour archiver le verdict structuré ; les deux terminent avec code 0.

Build natif dev, target `C:\Users\karen\AppData\Local\Temp\openrpg-forge-r1-064-target`,
`CARGO_INCREMENTAL=0`, `RUSTFLAGS=-C debuginfo=0`.

| Artefact | SHA-256 |
| --- | --- |
| `debug/forge_runtime.exe` dans ce target | `0f2920c5109163505a99212e08605a705d147bfb21199fcae85e61f009318e0b` |
| `valid-info-run.jsonl` | `12554282a3878751f5bd8cc4e2ce4bb5b87fa9167958273730cb387f5c056b17` |

Logs conservés dans `E:\OpenRPG_Forge\.gate1-evidence-runtime\r1-runtime-error-verdict-20260905`.
Pas de changement du moteur partagé, du Studio ou des shaders : aucun nouveau lancement WebGPU
ni capture artistique pour ce lot de verdict natif. Les anciennes captures invalides restent invalides.
CI distante NOT RUN ; validations humaines restantes inchangées. Aucun commit/push.
