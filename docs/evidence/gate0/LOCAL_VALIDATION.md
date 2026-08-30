# Gate 0 — Validation locale et clone propre

Date : 30 août 2026
Branche : `Rust`
Commit de code testé : `528a909738bde92a8831e5b41ba185ab35ffef7f`
Verdict local : **PASS TECHNIQUE — NE VAUT PAS PASSAGE DE GATE**

## Portée

Cette preuve couvre le workspace Rust, le bootstrap, les tests, Clippy, WASM, Trunk, Tauri, la scène
partagée, la chaîne d'approvisionnement, le runtime natif et l'isolation de Cadle. Le replay final a
été exécuté dans un clone propre distinct, sans `upstream/cadle/` et sans correction manuelle.

Elle ne couvre pas la CI distante, une revue visuelle humaine, un compteur réel de draw calls GPU,
les statistiques navigateur/Studio ni une distribution.

## Environnement

| Élément | Valeur vérifiée |
|---|---|
| OS | Microsoft Windows 11 Professionnel `10.0.26200`, 64 bits |
| CPU | AMD Ryzen 5 7600 6-Core Processor, 12 CPU logiques |
| RAM visible | 33 463 984 128 octets |
| GPU | AMD Radeon RX 7800 XT |
| backend/pilote | Vulkan, `26.8.1 (LLPC)` |
| `rustc` | `1.98.0 (88d9e12ae 2026-08-18)` |
| `cargo` | `1.98.0 (797e8a9bc 2026-08-05)` |
| cibles | `x86_64-pc-windows-msvc`, `wasm32-unknown-unknown` |
| `cargo-deny` | `0.20.2` |
| `cargo-audit` | `0.22.2` |
| Trunk | `0.21.14` |
| Tauri CLI | `2.11.4` |
| `wasm-bindgen-cli` | `0.2.127` |

## Clone et isolation

Le clone avait été créé avec les options `--no-local --single-branch --branch Rust`, puis mis à jour
par `fetch` et fast-forward vers le commit qualifié. Les contrôles finaux ont donné :

```text
branch = Rust
HEAD = 528a909738bde92a8831e5b41ba185ab35ffef7f
git status --porcelain = vide
upstream/cadle = absent
fichiers suivis sous upstream/cadle/** = 0
```

Le test dynamique prouve que build, tests, WASM, Tauri et runtime n'ont pas besoin du snapshot Cadle.
Aucun asset ou code Cadle n'a été copié dans Forge.

## Workspace et scène partagée

`cargo metadata --locked --no-deps --format-version 1` retourne exactement huit packages :

```text
forge_assets
forge_cli
forge_core
forge_engine
forge_project
forge_runtime
forge_studio
forge_world
```

La CLI valide le document partagé :

```text
VALID schema_version=1 revision=1 entities=4 sha256=0ea53d539067f399e7e76678532144955f35e5a15e95d11becf3a174843c67df
```

Le runtime natif, le viewport WASM et le backend Studio utilisent ce même document JSON. Le cas
positif passe `SCENE_PROOF.schema.json` ; un document avec `schema_version = 2` est rejeté. Les tests
Rust vérifient aussi les IDs uniques, le quaternion, la caméra, la lumière et le round-trip sans perte.

## Validation agrégée

Commande finale dans le clone propre :

```powershell
$env:CARGO_BUILD_JOBS = '1'
./scripts/Test-Rust.ps1
```

Résultat : code 0.

```text
FORGE_RUSTSEC_POLICY_PASS vulnerabilities=0 warnings=20 distribution_blockers=1
FORGE_RUST_VALIDATION_PASS wasm=True supply_chain=True
```

Le script a exécuté avec succès :

- `cargo fmt --all -- --check` ;
- `cargo clippy --workspace --all-targets --locked -- -D warnings` ;
- `cargo test --workspace --all-targets --locked` ;
- check et Clippy strict de `forge_engine`/Studio pour `wasm32-unknown-unknown` ;
- `trunk build --release --config Trunk.toml` ;
- `cargo deny --locked check licenses bans sources advisories` ;
- la politique RustSec épinglée.

Répartition exacte des 27 tests :

| Package/cible | Tests passés |
|---|---:|
| `forge_assets` | 3 |
| `forge_cli` intégration | 3 |
| `forge_core` | 5 |
| `forge_engine` | 3 |
| `forge_project` | 3 |
| `forge_runtime` | 3 |
| `forge_studio` frontend | 2 |
| `forge_studio` backend | 1 |
| `forge_world` | 4 |
| **Total** | **27** |

Résultat : 27 passés, 0 échec, 0 ignoré.

Une tentative ciblée parallèle antérieure avait fait échouer `rustc` par épuisement LLVM de mémoire.
Le replay mono-job ci-dessus distingue ce problème de ressources d'un défaut produit et passe
intégralement.

## Correctifs de reproductibilité qualifiés

Le premier replay en clone propre avait exposé deux défauts :

1. un environnement hérité `NO_COLOR=1` faisait échouer Trunk, qui attend un booléen textuel ;
2. un parent de capture absent provoquait un échec d'écriture Bevy sans rendre le processus non nul.

Le commit `528a9097` ajoute `scripts/Invoke-StudioTrunk.ps1`, utilisé par les hooks Tauri, qui
normalise temporairement `NO_COLOR=true` puis restaure l'environnement. Le runtime crée les parents,
supprime les sorties périmées et vérifie l'existence et la taille de la capture avant de réussir.
Deux tests supplémentaires portent le total à 27.

## Tauri et bundle final

Le test de régression a volontairement conservé la valeur problématique :

```powershell
$env:NO_COLOR = '1'
Push-Location apps/forge_studio
try {
    cargo tauri build --no-bundle
} finally {
    Pop-Location
}
```

Le hook a appelé le wrapper Trunk automatiquement. La commande a terminé avec le code 0, sans
correction externe.

| Fichier final du clone propre | Taille | SHA-256 |
|---|---:|---|
| `target/release/forge-studio.exe` | 16 304 640 octets | `4bb12eef7841b9bc4247b6cc12ea4012aa241d75c31f75dea1d43edc2a77efb0` |
| `dist/forge_studio_bg.wasm` | 34 879 512 octets | `d307e835f6aede2fb8cdc5b0939d2219a54354b79def6fb09f65805779086a28` |
| `dist/forge_studio.js` | 181 150 octets | `4751935be971a12ee33840bb93f9e39a1e92bce10d964a0e6b5c23d864399ecb` |
| `dist/index.html` après packaging Tauri | 1 037 octets | `f5693da13400410133362e9525159824ddb978a91929e66c0660ae5acfa67904` |

L'avertissement de compatibilité future de `proc-macro-error2 2.0.1` reste visible ; il ne vient pas
du code Forge et figure dans la politique RustSec.

## Runtime natif final

Avant lancement, `artifacts/gate-0/final-528a909` n'existait pas. Il n'a pas été précréé.

```powershell
cargo run --release --locked -j 1 -p forge_runtime -- `
  --capture artifacts/gate-0/final-528a909/native-runtime.png `
  --capture-frame 180 `
  --metrics artifacts/gate-0/final-528a909/native-runtime-metrics.json `
  --metrics-warmup-frames 120 `
  --exit-after-frames 240 `
  --no-vsync
```

Résultat : code 0 ; le runtime a créé seul le dossier et les deux sorties.

| Artefact | Dimensions/taille | SHA-256 |
|---|---:|---|
| `forge_runtime.exe` | 73 513 984 octets | `b5654c52a27920ffec7516b36231b0ddab6ad04b1d6b47b4e452329634a5184e` |
| `native-runtime.png` | 1920 × 1080, 1 027 592 octets | `0e3a206d2f984c0b42927801df3b2d54664a6ef13da76852a6008dcb5a332061` |
| `native-runtime-metrics.json` | 3 581 octets | `225bb327f1311376d84d62c97723ebe0857b6371e6d67bc9c4b1c453563af950` |

Ledger post-warm-up :

| Mesure | Valeur |
|---|---:|
| échantillons | 120 |
| durée | 454,8433 ms |
| moyenne | 3,7904 ms |
| moyenne FPS | 263,8271 |
| p50 | 2,3067 ms |
| p95 | 8,0712 ms |
| p99 | 8,7186 ms |
| maximum | 10,5704 ms |
| 1 % low | 103,6860 FPS |
| hitches > 16,7 ms | 0 |
| hitches > 33,3 ms | 0 |

Le ledger expose quatre entités source et deux `mesh_draws_logical`. Cette dernière valeur n'est pas
un compteur des appels de rendu GPU. Les diagnostics Bevy donnent des invocations et durées de stages,
mais aucun nombre fiable de draw calls.

La capture a été inspectée automatiquement : sol vert, cube bleu centré, overlay net, aucune
corruption. L'enveloppe `NATIVE_RUNTIME.visual-proof.json` valide contre
`VISUAL_PROOF.schema.json`, porte `source_tree_clean: true` et conserve la revue humaine en
`uninspected`.

## Chaîne d'approvisionnement

```text
advisories ok, bans ok, licenses ok, sources ok
FORGE_RUSTSEC_POLICY_PASS vulnerabilities=0 warnings=20 distribution_blockers=1
```

`RUSTSEC-2024-0429` sur `glib 0.18.5` reste bloquant avant une distribution Linux. L'intention de
licence PolyForm du contexte doit également être réconciliée par le propriétaire avec les manifests
`MIT OR Apache-2.0` avant toute distribution.

## Verdict

Le socle Gate 0 est reproductible localement depuis un clone propre Windows, sans Cadle et sans étape
manuelle cachée. La gate globale reste non passée parce que la CI distante, la revue visuelle humaine
et la décision Studio sont absentes. Gates 1–4 restent non commencées.
