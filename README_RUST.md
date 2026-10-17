# OpenRPG Forge — procédures historiques de fondation Gate 0

Pour une reprise actuelle, utiliser le [guide de reprise](docs/HANDOFF.md), le
[contexte canonique](PROJECT_CONTEXT.md) et la [roadmap](docs/planning/MIGRATION_ROADMAP.md).
Gate 3 est clôturée et Gate 4 clôturée localement au 16 octobre 2026.

Les procédures ci-dessous décrivent l'environnement et les preuves Gate 0 du
30 août. Elles ne constituent pas le parcours actuel d'édition Tidewater ou de
livraison. Les anciennes conditions de CI ne rouvrent pas les gates clôturées ;
consulter les actualisations d'ADR-0006 et ADR-0023. Les commandes historiques
ne sont pas une qualification du checkout actuel.

## Environnement qualifié le 30 août 2026

- branche : `Rust` ;
- `rustc 1.98.0 (88d9e12ae 2026-08-18)` ;
- `cargo 1.98.0 (797e8a9bc 2026-08-05)` ;
- cibles : `x86_64-pc-windows-msvc`, `wasm32-unknown-unknown` ;
- `cargo-deny 0.20.2`, `cargo-audit 0.22.2`, `trunk 0.21.14`, Tauri CLI `2.11.4` ;
- GPU de preuve : AMD Radeon RX 7800 XT, backend Vulkan, pilote `26.8.1 (LLPC)` ;
- document de scène partagé : SHA-256
  `0ea53d539067f399e7e76678532144955f35e5a15e95d11becf3a174843c67df`.

## Bootstrap Windows

Depuis la racine d'un clone de la branche `Rust` :

```powershell
.\scripts\Bootstrap-Rust.ps1 -InstallTools
```

Le script vérifie la branche, installe la toolchain épinglée et la cible WASM, puis contrôle les outils
de Gate 0. La preuve de clone propre est enregistrée séparément dans le rapport de gate.

## Validation agrégée

```powershell
.\scripts\Test-Rust.ps1
```

Le script exécute formatage, Clippy strict, tests, compilation WASM, bundle Trunk, `cargo-deny` et la
politique RustSec exacte. Pour un diagnostic borné :

```powershell
.\scripts\Test-Rust.ps1 -SkipWasm
.\scripts\Test-Rust.ps1 -SkipSupplyChain
.\scripts\Test-RustSec.ps1 -NoFetch
```

La chaîne d'approvisionnement est documentée dans
`docs/evidence/gate0/SUPPLY_CHAIN.md`. Elle contient zéro vulnérabilité RustSec connue pour le
lockfile audité, mais conserve explicitement le blocage de distribution Linux
`RUSTSEC-2024-0429` (`glib 0.18.5`).

## Runtime natif et capture reproductible

```powershell
cargo run --release --locked -p forge_runtime -- `
  --capture artifacts/gate-0/native-runtime.png `
  --capture-frame 180 `
  --metrics artifacts/gate-0/native-runtime-metrics.json `
  --metrics-warmup-frames 120 `
  --exit-after-frames 240 `
  --no-vsync
```

Les artefacts sous `artifacts/gate-0/` restent locaux et ignorés. Le rapport versionné enregistre leurs
hashes, dimensions, métriques et résultat d'inspection sans incorporer les médias.

## Studio WASM/WebGPU

```powershell
Push-Location apps/forge_studio
try {
    trunk build --release --config Trunk.toml
    trunk serve --release --config Trunk.toml
} finally {
    Pop-Location
}
```

Le frontend demande réellement un adapter et un device WebGPU. Il charge le même JSON embarqué que
le runtime natif, affiche son hash, et maintient une seule initialisation Bevy pendant les sélections.
En navigateur autonome, l'absence du backend Tauri est affichée comme telle.

## Shell Tauri

Build vérifié sans installeur :

```powershell
Push-Location apps/forge_studio
try {
    cargo tauri build --no-bundle
} finally {
    Pop-Location
}
```

Le binaire produit est `target/release/forge-studio.exe`. La sélection par hiérarchie et par picking
3D traverse un canal Rust typé ; le backend rejette les `EntityId` absents du document partagé. La
latence aller-retour est affichée dans l'inspecteur.

## CI et statut de Gate 0 — exigences initiales historiques

`.github/workflows/rust-gate.yml` définit les jobs format, Clippy, tests, Windows, Linux, WASM et
chaîne d'approvisionnement. La configuration locale ne vaut pas exécution distante : sans push
autorisé et sans run GitHub Actions vert, le protocole initial refusait le passage
de Gate 0. Les décisions et clôtures ultérieures remplacent cette condition
historique dans leur portée explicite ; voir l'état courant en tête de document.

## Frontières

- aucune commande Gate 0 ne nécessite `main` ;
- `upstream/cadle/` peut être absent ;
- aucun code, asset, capture, nom ou texte Cadle n'entre dans le runtime ou le Studio Forge ;
- le JavaScript généré par la toolchain WASM n'est pas une source de règles métier ;
- les manifests candidats déclarent `MIT OR Apache-2.0`, alors que `PROJECT_CONTEXT.md` exprime une
  intention PolyForm Noncommercial : aucune distribution ne doit intervenir avant décision du
  propriétaire ;
- aucune publication ni distribution n'est autorisée par ce document.
