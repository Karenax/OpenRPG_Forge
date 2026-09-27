# Gate 1 — Raccordement des contrôles dans Studio

## Diagnostic discriminant du 8 septembre — touche DOM sans code physique

Ajout local dans `input_capture.rs` et le handler `keydown` du canvas : dernier
type de code physique reçu, sans stocker ou afficher `event.key`, du texte libre,
des événements hors canvas ou un historique. Labels bornés dans
`input_key_observation.rs`, nettoyés avec le composant. Ce diagnostic ne soumet
aucune commande et ne contourne ni focus ni pointer lock. Le contrôleur reste
fondé sur les codes physiques Bevy, afin de conserver les positions WASD/ZQSD.

Vérifications : 42 tests Studio PASS (`cargo test -p forge_studio --lib --offline
--locked -q`), format/diff PASS, Clippy Studio WASM strict PASS, Trunk release
offline/locked PASS puis build Tauri PASS. Warning de compatibilité future
`proc-macro-error2 2.0.1` conservé. Aucun contrat moteur, document partagé,
shader ou dépendance modifié ; pas de nouvelle qualification workspace globale.

Candidat séparé `E:/OpenRPG_Forge/.gate1-evidence-runtime/r1-tauri-keyboard-observation-20260908` :
EXE SHA-256 `FE8FE787ABFAA14B8E17DE862CB02C96EFA039AEB5C80CBC2ADA80A1E851243E`,
WASM `EFBAB9372768284795D39427CFCB990172FBE206718EFC139265269334B776D5`.
Kit forestier et vues copiés depuis le candidat précédent ; les pins du bundle
sont vérifiés par `Build-StudioCandidate.ps1` et consignés dans `build-proof.json`.
L'ancien candidat reste intact. Aucun push.

Exécution Tauri via Computer Use : une fenêtre, monde initialisé une fois,
canvas chargé ; « aucune touche observée » avant entrée. Après clic/capture puis
`press_key('v')`, observation différée : **« code physique absent »**. Ce label
correspond strictement à `KeyboardEvent.code` vide ou `Unidentified`. Le canvas
reçoit donc un événement, mais pas le `KeyV` que lit le contrôleur : l'hypothèse
d'un événement totalement absent du canvas est écartée pour cet essai. Cela
explique l'absence de bascule de cette injection, sans prouver que tous les
claviers physiques fonctionnent ni qualifier tout le chemin Bevy.

Témoin Espace : label resté identique, saut non établi ; sans compteur d'événements,
ne pas en déduire une nouvelle réception distincte. Échap libère le pointeur,
sans prétendre que cet événement navigateur a atteint le contrôleur. Aucun
parcours à la lisière ni preuve d'échelle à distance comparable obtenu.
Ne plus répéter V avec ce mode d'injection ou reconstruire le contrôleur sur
la seule base de cette absence de mouvement. Un parcours de preuve via les
commandes normales serait distinct d'une qualification du clavier physique.

Captures brutes conservées dans `input-proof/` :

| Capture | SHA-256 |
| --- | --- |
| v-code-absent.jpg | `9540280A1B13800D98ED88C547CA9F8B80AA93242B3553C88D0B05591126681F` |
| released.jpg | `A75D52F54FFAB3571D72DBBD9791BE9B30755F2880BB89FA7EF8B89AB47BA7AE` |

Studio fermé par Alt+F4 ; aucun processus du chemin exact ni WebView associé
restant au contrôle, session Node réinitialisée. Skill Computer Use utilisé
pour le lancement, les actions et les captures ; aucune entrée Windows alternative.
Gate 1 reste ouverte ; le diagnostic n'est pas une validation des contrôles.

### Nouvel essai sur le candidat forestier du 8 septembre

`r1-tauri-forest-scale-density-20260908/forge-studio-candidate.exe` ouvert via
Computer Use : une fenêtre, monde initialisé une fois, clic dans le canvas puis
statut « Exploration active ». Touches W puis V envoyées séparément ; aucune
marche ou bascule identifiable dans les observations suivantes. La lisière
n'a pas été rejointe, aucune preuve de rapport arbre/personnage obtenue.
Il s'agit de la même limite déjà observée ci-dessous, pas d'un progrès clavier.

Lecture de Winit 0.30.13 : ses gestionnaires de pointeur appellent déjà
`canvas.focus()` ; ajouter aveuglément un second appel côté Studio n'est donc
pas un diagnostic établi. L'API Computer Use documentée expose `press_key`,
mais pas de maintien de touche avec durée : ne pas annoncer un parcours tenu
par cet outil ni inventer un argument de maintien. Une prochaine investigation
doit distinguer réception DOM, réception Winit/Bevy et commande consommée,
ou utiliser un mécanisme de parcours existant approprié, pas répéter ces touches.
Studio fermé par Alt+F4, absence du processus vérifiée puis session Node réinitialisée.

## Complément Tauri du 8 septembre — clavier non démontré

Essai sur le candidat `r1-tauri-lighting-continuity-20260908`, EXE SHA-256
`7355C486E5F50791CDBA691991B4774C4CE14485AD16E8A137BAEF1872F71788`.
Le clic physique dans le canvas affiche « Exploration active ». L'envoi de V
par Computer Use ne produit pas de bascule observable dans la capture suivante.
Ce constat ne prouve ni une panne du contrôleur ni la fiabilité de l'injection
clavier : le focus remonté dans les DevTools reste également sur leur titre
après clic dans leur console. Aucun script n'y a été saisi ou exécuté.

Inspection locale de Bevy 0.19.1 : `ButtonInput::release` conserve
`just_pressed`, et le sampler Forge lit cette transition après `InputSystems`.
L'hypothèse d'une pression perdue simplement parce que press/release arrivent
dans la même frame n'est donc pas étayée. Aucun correctif spéculatif, aucune
reconstruction et aucun nouveau PASS clavier. La dernière vue est orientée
vers le haut, capsule au premier plan ; elle n'est pas une preuve de première
personne. Les succès Chrome du 5 septembre ci-dessous restent historiques.

Artefacts externes sous le dossier du candidat, `keyboard-focus-proof/` :

- `last-viewport.jpg` : `E1178F910432A067C647EC43A0DCFA89CFE1745A6673A86575E6F3CCACB39EB7` ;
- `last-viewport.txt` : `1AFA1571A187356F81B436455E833A9E28629891B33B74605D26DE6F5DD4AA57` ;
- `devtools-focus.txt` : `19DD30826D8615D3178FE7568B35407C30D4700455A82EB95AF55DD4CA6518B4`.

DevTools et candidat fermés ; contrôle du chemin EXE exact : aucun processus
restant, session Node réinitialisée. Ne pas répéter les mêmes touches/captures
sans moyen nouveau de distinguer l'événement reçu du résultat moteur.

## Preuves historiques du 5 septembre

2026-09-05, branche Rust, local non publié. Gate 1 reste ouverte.

## Implémenté

Le Studio n'installe plus un contrôleur systématiquement privé d'entrée interactive.
Il réutilise le sampler et les commandes joueur du moteur, sans second contrôleur :
clic sur le canvas au premier plan pour capturer la souris, commandes physiques WASD/ZQSD,
Maj, Espace, souris et V ; Échap libère explicitement la capture. L'aide est superposée au
viewport sans modifier la taille de sa grille. Espace ne fait pas défiler la page pendant
la capture. Gate 0 ne demande pas cette capture.

La ressource runtime `PlayerInputFocus` vaut vrai par défaut dans le moteur et faux à
l'installation Studio. Avant l'échantillonnage, le Studio vérifie le focus du document et
que `pointerLockElement` désigne son canvas. Sans possession, le moteur efface les axes
maintenus, le sprint, le saut, le delta caméra et la bascule en attente, mais ne réinitialise
pas le compteur de séquence. Les probes à sampler désactivé gardent leur chemin existant.
Aucun document authoré, hash source, shader ou règle de physique modifié par ce lot.

## Vérifié automatiquement

- Tests workspace : **306 PASS**, dont 101 moteur. Nouveau test de perte/reprise de focus :
  annulation de toutes les actions, neutralité sans focus, reprise à séquence suivante.
- Clippy workspace strict : PASS après annotation du paramètre système Bevy.
- Clippy Studio WASM strict : PASS après simplification des conditions imbriquées.
- Check Studio WASM et builds Trunk : PASS. Warning de compatibilité future de la dépendance
  `proc-macro-error2 2.0.1` conservé ; ne pas annoncer une toolchain sans avertissement.
- Format et diff : PASS.

Variables Cargo : target `C:\Users\karen\AppData\Local\Temp\openrpg-forge-r1-064-target`,
`CARGO_INCREMENTAL=0`, `RUSTFLAGS=-C debuginfo=0`.

Non-régression native : `cargo build --workspace --locked -q` PASS, puis
`forge_runtime.exe --visual-valley --player-camera --exit-after-frames 240`, code 0,
240 frames et aucun ERROR dans le JSONL parsé. Verdict final
`2026-09-05T14:12:26.106403Z`, `runtime_errors_observed=false`.
SHA-256 exécutable : `f6e1d1c329bf99a0be0bdf82f477eca614db291ea6cdca5dc77fb680c05f837a`.
Log externe `r1-studio-explore-20260905/native-run.jsonl` :
`dcf577b525622ced21b02e87c2e59dc21513908d8cb9019c4811fa62bd515dce`.

## Observation Chrome réelle

Bundle observé :
`E:\OpenRPG_Forge\.gate1-evidence-runtime\r1-studio-explore-20260905\final-dist`.
WASM SHA-256 : `9bc6f0bad9f7c4574e3f7b4df43cf9bb78acb93f4a03a257679e4f1b9b1a97c4`.
URL : `http://127.0.0.1:1420/?forge_scene=visual-valley`.
Ce bundle inclut la libération Échap ; il précède seulement la simplification équivalente
des conditions imbriquées demandée par Clippy, ensuite compilée/vérifiée en WASM.

1. Les premiers clics dans l'onglet non ramené au premier plan sont refusés par Chrome
   (`WrongDocumentError`). Les contrôles restent inactifs : aucun succès revendiqué.
2. Après `Page.bringToFront` et clic utilisateur simulé, lecture DOM réelle via CDP :
   `pointerLockElement.id = forge-canvas`. Pas de modification de permission navigateur.
3. Touche V : première personne observée sur capture, capsule non visible, vue à hauteur
   joueur. La capture de la conversation n'est pas archivée comme fichier durable.
4. Échap : lecture DOM `pointerLockElement = null` sur la version corrigée.
5. Sélection du document contrôleur dans l'inspecteur : un rerender, canvas stable 1/1,
   monde toujours initialisé une seule fois. Aucun nouveau chargement de scène.

Dernier relevé d'interaction `2026-09-05T14:11:07.913Z`, CSS/buffer 2048 × 1101,
4,2 ms handler-to-post-paint pour une sélection seulement. Aucun log erreur/avertissement
retourné pour le second onglet. Le ledger de frames de ce parcours contient une période
avant remise au premier plan (p95 ~1002 ms) : il est impropre à qualifier les performances.
Les FPS instantanés visibles ensuite ne remplacent pas un benchmark contrôlé.

## Limites et suite

Le déplacement prolongé, le saut en navigateur et le ressenti souris restent à tester
humainement ; le contrôle navigateur disponible n'a pas exécuté un maintien de touche.
La caméra observée ne suffit pas à qualifier ces actions ni un parcours vidéo R1-048.
Alt-Tab réel, reprise de capture après Échap et WebView2 restent à qualifier. Les tests
headless couvrent l'annulation des commandes, pas toutes les politiques de focus des OS.

Cette capacité concerne la vallée embarquée, pas le candidat WorldSketch/kit Blender.
Elle ne constitue ni l'éditeur, ni une sauvegarde, ni le playtest authoré de Gate 2.
Validations humaines, performance, revue artistique et archivage durable restent ouverts.
CI distante NOT RUN ; aucune publication Git, aucun asset Cadle.
