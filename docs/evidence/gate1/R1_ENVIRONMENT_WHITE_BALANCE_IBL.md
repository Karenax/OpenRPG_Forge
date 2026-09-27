# Gate 1 — Balance des blancs et éclairage indirect du ciel

Date : 2026-09-05. Branche `Rust`, local non publié. Gate 1 reste ouverte.

## Implémenté

Deux raccords de l'adapter, sans modifier les profils artistiques JSON :

- Les contrôles Forge température/teinte, bornés à [-1, 1], étaient transmis directement
  aux offsets CIE xy de Bevy. Ils sont maintenant multipliés par 0,05. Le profil clair
  (0,08 ; 0,01) devient (0,004 ; 0,0005). Zéro reste neutre ; exposition, saturation,
  contraste, gamma et interpolation météo ne changent pas. Cette échelle est un choix
  d'adapter pour les contrôles artistiques, pas une conversion en kelvins.
- Le ciel visible est maintenant aussi la source de l'éclairage indirect via
  `AtmosphereEnvironmentMapLight`, résolution issue du profil (256 × 256 par face ici),
  gain physique 1. L'ambiant de repli reste présent. Ciel désactivé : retrait du composant
  IBL, des réglages atmosphériques et du HDR ; ambiant conservé.

Références d'API inspectées dans le registre Cargo local : Bevy 0.19.1,
`bevy_render/src/view/mod.rs` (`ColorGradingGlobal`, calcul du point blanc),
`bevy_light/src/probe.rs` (`AtmosphereEnvironmentMapLight`) et
`bevy_pbr/src/atmosphere/environment.rs` (génération de la cubemap).
Pas de dépendance, shader ou asset externe ajouté. L'IBL ajoute une cubemap et sa génération
GPU : coût non nul, non isolé par ce lot ; aucune optimisation de performance revendiquée.

## Vérifié

- `cargo clippy --workspace --all-targets --all-features --locked -- -D warnings` : PASS.
- `cargo test --workspace --locked -q` : **299 PASS**, aucun échec ni test ignoré.
  Assets 14, CLI 7, Core 11, Engine 99, Project 45, Runtime 42, Studio 21, World 60.
- Test du mapping : valeurs claires exactes et bornes pour -1/0/1 ; tests des composants
  IBL, résolution, gain, topologie invalide et nouveau test ciel désactivé/repli ambiant.
- `cargo check -p forge_engine --no-default-features --features web --target wasm32-unknown-unknown --locked` :
  PASS sur le code de production final (avant le dernier ajout uniquement `#[test]`).
  Ce n'est ni un bundle navigateur ni un lancement Studio WebGPU.
- `cargo fmt --all -- --check` et `git diff --check` : PASS.

Environnement : Rust 1.98, target
`C:\Users\karen\AppData\Local\Temp\openrpg-forge-r1-064-target`,
`CARGO_INCREMENTAL=0`, `RUSTFLAGS=-C debuginfo=0`.
Échec initial conservé : fixture `EnvironmentSkyStats` incomplète et fonction d'observation
dépassant la limite Clippy. Fixture complétée, résolution du chemin de brouillard extraite
en fonction dédiée ; contrôles rejoués sans assouplir les règles.

## Capture et mesures natives

Dossier externe `E:\OpenRPG_Forge\.gate1-evidence-runtime\r1-lighting-bridge-20260905-a` :
`hero.png`, `metrics.json`. Run du 12:55:07 UTC, sortie 0, RX 7800 XT/Vulkan, 1920 × 1080.
Capture frame 300, préchauffage 180, sortie 420 ; aucune compilation Forge simultanée.
Même candidat externe, kit authoré corrigé, aperçu et pont que la preuve HLOD précédente :

```powershell
& 'C:\Users\karen\AppData\Local\Temp\openrpg-forge-r1-064-target\debug\forge_runtime.exe' `
  --world-sketch-visual-valley `
  --world-sketch-input E:\OpenRPG_Forge\.gate1-evidence-runtime\r1-river-profile-20260905\world-sketch-candidate.v1.json `
  --asset-kit-build E:\OpenRPG_Forge\.gate1-evidence-runtime\r1-063-authored-preservation-20260905-b\rebuild-1\asset-kit-build.v1.json `
  --asset-kit-preview E:\OpenRPG_Forge\.gate1-evidence-runtime\r1-river-profile-20260905\runtime-preview-candidate.v1.json `
  --bridge-placement E:\OpenRPG_Forge\.gate1-evidence-runtime\r1-scatter-corridors-20260905\compiled\bridge-placements.v1.json `
  --capture E:\OpenRPG_Forge\.gate1-evidence-runtime\r1-lighting-bridge-20260905-a\hero.png `
  --capture-frame 300 `
  --metrics E:\OpenRPG_Forge\.gate1-evidence-runtime\r1-lighting-bridge-20260905-a\metrics.json `
  --metrics-warmup-frames 180 --exit-after-frames 420
```

Pour un nouveau replay, choisir un nouveau dossier de sortie afin de conserver ces preuves.

Capture inspectée : ciel bleuté, terrain vert, berges éclairées plutôt que presque noires.
Le canyon reste géométrique, la végétation et les matériaux proches provisoires, l'horizon
artificiel. Ce constat n'est pas une acceptation artistique humaine.

Mesures : 240 frames, moyenne 8,394 ms / 119,13 FPS, p95 9,356 ms, 1 % low 52,54 FPS ;
un pic de 36,222 ms. Échantillon d'environ deux secondes : observation de smoke, pas benchmark
de gate ni preuve d'absence de stutter. Le coût IBL n'est pas instrumenté séparément dans ce ledger.
Les avertissements Vulkan AMD et shader `@invariant` persistent, aucun crash constaté.

Le ledger confirme IBL présent, résolution [256,256], gain 1, température 0,004, teinte 0,0005 ;
20/20 scènes chargées et 36/36 colliders Rapier. Les diagnostics IBL restent explicitement
`main_world_components_not_gpu_execution` : ils ne prouvent pas à eux seuls une passe GPU.

SHA-256 des octets :

- binaire : `af1c2021490cb2454b203e313c0c7a64a82d5b56f98f100dd4c4d86d4ab5b69c` ;
- capture : `aa3fddd587d969928a861dee24e756f0cbe666db3c9a41a300bec03ff1c39e02` ;
- métriques : `cf60ed62a5f3fec7d653a7403d011e8e61492737d3a1e1a6c9fafa44c44cd74c` ;
- WorldSketch source par défaut et copie avant candidat : même hash
  `87b901540a5f8f6f2faaa431721f120e1a48e2351458ca371caad914c1bae6ee`.

La source verrouillée n'a pas changé ; le candidat de creusement reste NON PROMU.
Les profils environnement/météo suivis n'ont aucun diff Git. Revues humaines, pluie et
Studio WebGPU restent dus ; CI distante `NOT RUN`. Aucun commit ni push.
