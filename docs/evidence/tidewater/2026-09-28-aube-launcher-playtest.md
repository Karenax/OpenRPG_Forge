# Distribution locale Tidewater avec L’Aube des royaumes — 28 septembre 2026

Le port `127.0.0.1:8848` était servi par `python -m http.server` depuis
`E:/OpenRPG_Forge/launcher-aube`. Le premier candidat Windows utilisait une
autre présentation Tauri/Babylon et ne répondait pas au choix du propriétaire.
Le candidat `003` embarque maintenant le site Aube comme frontend Tauri, sans
serveur local. Seule la rubrique « Jouer » reçoit un pont vers la commande
native existante `request-launch`.

Source du frontend : `launcher-aube/index.html`, SHA-256
`35a7a2d5b6ab088d4a539655c5112f0eabdfaec1586cf35a5c6f0be61c104c8b`.
La copie de production conserve les 68 images octet pour octet (68 hashes
comparés, 0 différence). `sources/manifest.json` et une mention de provenance
dans `content.js` ont été rendus relatifs pour ne pas transmettre les chemins
de la machine de travail. La source du port 8848 n’a pas été modifiée.

Le lanceur a été construit avec `cargo tauri build --no-bundle --features
production`, dans `E:/OpenRPG_Forge/.build/cargo-target`, et répond
`FORGE_LAUNCHER_AUBE_FRONTEND_READY` à `--verify-distributable`. Le script
`Build-WindowsPlaytest.ps1` refuse désormais les anciens exécutables qui
n’embarquent pas Aube.

Validation locale :

- `npm run build` : Aube et 68 images copiés ; syntaxe de `desktop.js` vérifiée ;
- `python -B test_content.py` : 21/21 ; `node --test test_codex_core.cjs` : 9/9 ;
- `cargo fmt --check` pour le lanceur et parse PowerShell du script : PASS ;
- ZIP extrait dans un dossier neuf : 2 504 fichiers, 2 504 hashes SHA-256
  concordants ; exécutable Aube vérifié ;
- depuis le ZIP extrait : test de promenade du moteur natif PASS, 8,994 m,
  311 ticks au sol ;
- parcours Windows réel : accueil Aube affiché à `http://tauri.localhost/`,
  menu « Jouer », bouton « Lancer l’île Tidewater », statut de lancement réussi
  et fenêtre native Tidewater apparue.

Archive locale :
`E:/OpenRPG_Forge/.tidewater-integration/OpenRPG-Forge-Aube-Tidewater-Win64-20260928-003.zip`
(409 620 450 octets ; SHA-256
`9616837657624db2a6671533d8df688475833275a2d07b66f05bd0b99f4e108c`).

Le contenu est techniquement qualifié sur cette machine. L’installation sur
une autre machine, la signature Windows et la revue des droits de distribution
du code et des illustrations Aube restent ouvertes avant un envoi public.

## Retrait de l’ancienne présentation

À la demande du propriétaire, le frontend Babylon/Vite `web/`, sa configuration
TypeScript/Vite, sa dépendance npm et l’ADR d’exception ont été retirés. Le
paquet npm du lanceur n’a plus aucune dépendance ; le shell Rust/Tauri et son
pont vers Tidewater sont conservés. L’icône Valedorn a été remplacée par une
icône Aube, et les icônes Tauri ont été régénérées.

Le candidat courant est désormais `004` :
`E:/OpenRPG_Forge/.tidewater-integration/OpenRPG-Forge-Aube-Tidewater-Win64-20260928-004.zip`
(409 619 292 octets ; SHA-256
`969b9bbc71eaf6c06df1b543fe0b9432a2de677f2aa118ad60b805dfd31fe9ef`).
Après extraction neuve : `FORGE_LAUNCHER_AUBE_FRONTEND_READY` et 2 504/2 504
empreintes de fichiers concordantes. Le parcours jeu de `003` reste applicable :
le moteur, les données et le pont de lancement ne changent pas dans `004`.

La suppression physique de l’ancien PNG `assets/launcher-valedorn-v1.png` a été
refusée par l’auto-review, y compris avec son chemin exact (`blocked by policy`).
La commande de retrait des anciens dossiers `public/`, `node_modules/` et des
icônes a aussi été refusée. Ces caches et les anciens paquets 001/002/003
restent locaux, hors du paquet 004. Aucun autre moyen de suppression n’a été
utilisé pour contourner ce refus.
