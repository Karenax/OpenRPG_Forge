# Correction du relancement Tidewater — 10 octobre 2026

Les sessions de l'installation R2 quittaient avec `Error: "ocean output already exists"`.
La recette réutilisait `{user}/captures/game.png` et ses sorties `.ocean`, `.shore`,
etc. L'essai précédent vérifiait le Snapshot du launcher et la recette, sans
ouvrir réellement le jeu ; il ne couvrait donc pas cette panne de relancement.

La version `0.1.0-preview.20261010.3` utilise
`{user}/captures/game-{session}.png`. Le jeton de session associe PID et temps en
nanosecondes ; toutes les sorties d'un lancement partagent cette valeur. Les
captures existantes restent conservées. Le launcher vérifie également les arrêts
pendant les trois premières secondes avant de renvoyer `LaunchStarted`.

EXE hors ligne : `E:/OpenRPG_Forge/OpenRPG-Forge-Setup-20261010-r3.exe`.
Taille : 1 188 383 030 octets. SHA-256 :
`5b07587d2d98c1ee1f3e95ec3e6e2a8f94b89fbb8f47e7a48ab69efaf129ddf1`.

## Construction et périmètre

`E:/OpenRPG_Forge/.delivery-evidence/20261010/classic-build-005-game-sessions`
contient les sources figées, les tests et les preuves de construction de
production. Tests PASS : livraison 9 + désinstallation 4 ; launcher 3 bibliothèque
+ 7 desktop ; gestionnaire de contenu 3. Deux régressions couvrent les sorties
distinctes et préservées, et la détection d'un processus quittant immédiatement.

`aube-presentation-parity-r3.json` confirme les 86 fichiers du frontend Aube
identiques à R2. Le launcher, le gestionnaire de contenu et l'assistant classique
gardent leurs rôles séparés. Le moteur du jeu et Studio restent les binaires du
9 octobre, vérifiés par leurs hashes avant réemballage ; aucune modification
concurrente du moteur n'est intégrée à cette correction.

## Vérification de l'installation réelle

Les preuves de mise à jour sur `F:/instal test/Installation` sont conservées dans
`repair-game-launch-r3` : installation et vérification intégrale PASS,
63 fichiers personnels byte-identiques, cinq raccourcis et deux anciens
raccourcis étrangers conservés, précédent core conservé, vrai WebView/IPC Aube PASS.
Les essais de deux sessions interactives réelles sont conservés dans
`game-sessions-r3-002`. Le script de réparation et ses imports sont conservés sous
`classic-build-005-game-sessions/validation-scripts`, et le harnais de jeu exécuté
avec ses imports est figé sous `classic-build-005-game-sessions/validation-scripts-v2`.

Le premier harnais, conservé dans `game-sessions-r3`, attendait un EOF sur les
pipes stdout/stderr du diagnostic : le jeu Windows survivant au launcher
héritait d'un handle de pipe. Cet essai est incomplet et n'est pas compté PASS.
Le second harnais écrit ces flux dans des fichiers, attend uniquement le
processus launcher, et active `RUST_LOG=info` pour conserver la preuve du GPU
et de la fenêtre dans le journal. Le seul jeu lancé par le premier harnais a
été arrêté après vérification de son PID, parent, exécutable et argument de capture.

La commande native de diagnostic appelle exactement `RequestLaunch`, le backend
utilisé par le bouton du renderer. Les contrôles portent sur le processus réel,
la création de fenêtre rapportée par Bevy, le GPU et l'absence d'erreur pendant
une minute. Ils ne constituent pas un clic physique automatisé ni une acceptation
visuelle humaine.

**Deux sessions PASS** : PID 7740 puis 43540, chacune active pendant 60 secondes,
fenêtre `OpenRPG Forge — Tidewater`, GPU Vulkan RX 7800 XT, initialisations
océan et spray terminées et aucune erreur dans les journaux. Les préfixes
`game-8504-1791627483080580800` et `game-45792-1791627546686992500` sont distincts.
Les 64 fichiers de captures préexistants à cet essai restent byte-identiques,
y compris ceux conservés du premier harnais incomplet. Les deux processus
possédés par le test ont été arrêtés à sa fin ; aucun autre jeu n'est arrêté.
`verification.json` et `render-readiness.json` conservent les résultats.
`classic-build-005-game-sessions/installer-validation.json` associe le hash de
l'EXE exact aux preuves de réparation, des deux sessions et des scripts exécutés.

L'EXE R2, dont le hash est vérifié avant déplacement, est conservé seulement sous
`classic-build-004-aube/installer-superseded-game-collision.exe` pour éviter
de réinstaller la recette fautive.

Gate 3 reste ouverte ; ces essais ne qualifient pas une installation Windows
vierge ni une release complète.
