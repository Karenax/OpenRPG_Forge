# Lanceur Windows Tidewater — L’Aube des royaumes

La distribution Windows reprend l’interface actuellement servie depuis
`E:/OpenRPG_Forge/launcher-aube` sur le port 8848. `npm run build` copie son
HTML/CSS, son guide, son bestiaire et ses 68 images dans `dist/` sans changer
les sources. Le seul ajout au site empaqueté est `desktop.js` : dans la rubrique
« Jouer », il appelle la commande Tauri existante pour ouvrir l’île Tidewater.
Dans le navigateur, la maquette reste autonome et ne lance aucun processus.

La fenêtre Tauri contient les fichiers web. Elle ne dépend pas du serveur local
8848 chez les destinataires. Le moteur Rust reste dans `runtime/tidewater.exe`
et les données de l’île dans `data/` à côté du lanceur. Les captures et journaux
vont dans `%LOCALAPPDATA%/OpenRPG Forge/Tidewater/captures`.

## Construire

Depuis ce dossier, avec Node.js et Rust installés :

```powershell
npm run build
cargo fmt --check
cargo test --all-features
cargo clippy --all-targets --all-features -- -D warnings
cargo tauri build --no-bundle --features production
```

Le binaire se trouve dans `target/release/forge-launcher-prototype.exe` ou dans
le `CARGO_TARGET_DIR` configuré. La commande
`forge-launcher-prototype.exe --verify-distributable` doit afficher
`FORGE_LAUNCHER_AUBE_FRONTEND_READY`. L’assembleur
`OpenRPG_Forge-Rust/scripts/tidewater/Build-WindowsPlaytest.ps1` refuse
explicitement les anciens lanceurs, même s’ils étaient construits pour Tauri.

`npm run dev` sert une copie de `dist/` sur le port 1430 pour le développement.
Le port 8848 sert directement le dossier source `launcher-aube` ; il ne fournit
aucune interface de lancement natif.

La revue des licences du code Forge et des illustrations reste distincte de
la qualification technique de cet exécutable.
