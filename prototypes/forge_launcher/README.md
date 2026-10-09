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

Le candidat courant charge également les débris depuis
`content/debris/procedural/`, `content/debris/scanned/` et
`content/debris/scanned.wgsl` à côté du lanceur. Ces chemins sont calculés
depuis le dossier extrait et remplacent les éventuelles variables de
développement héritées. Le shader scanné est un compagnon versionné distinct
du shader prototype présent dans son paquet. Aucun chemin du poste de
développement n'est nécessaire. Les anciens ZIP sans ce contenu doivent être
reconstruits pour ce candidat.

Le raccord courant des cailloux exige aussi `content/debris/pebbles/program.json`
et `content/debris/pebbles/pebbles.wgsl`. Le lanceur transmet ce dossier extrait
au runtime, en remplaçant toute variable de développement héritée. Le marqueur
`--verify-distributable --pebble-runtime` distingue ce raccord du binaire
précédent. Cette modification source nécessite de reconstruire et de retester
le lanceur et le runtime ; elle ne met pas à jour les ZIP déjà produits.

## Construire

Les cinq poissons source de l'étal et les 78 poissons décoratifs du village
utilisent aussi `content/fish/game/` et
`content/fish/surface/`. Le lanceur exige leurs manifestes et transmet leurs
chemins calculés depuis le dossier extrait, sans conserver une racine de
développement héritée. Le marqueur `--verify-distributable --source-fish-runtime`
répond `FORGE_LAUNCHER_SOURCE_FISH_RUNTIME_V1`. Ce raccord nécessite un nouveau
build et un essai du paquet extrait ; les ZIP existants restent inchangés.
L'assembleur exige désormais `-GameFishRoot` et `-FishSurfaceRoot`, ainsi qu'une
nouvelle preuve `Verify-NativeDebrisWalk.ps1 -WithPebbles -WithSourceFish` sur
le runtime exact. Le reçu de préparation des poissons est vérifié séparément
de la comparaison visuelle ; ses compteurs ne prouvent pas un dessin GPU.
Il doit conserver les 83 identités source, 166 slots visuels et 83 géométries
d'ombre masquées. Une ancienne preuve limitée aux cinq poissons est refusée.

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
Il exige aussi `--verify-distributable --debris-runtime`, qui doit afficher
`FORGE_LAUNCHER_DEBRIS_RUNTIME_V1`, et les paramètres
`-ProceduralDebrisRoot`, `-ScannedDebrisRoot`, `-ScannedDebrisProgram`.
Le raccord des cailloux exige en plus `--verify-distributable --pebble-runtime`
avec réponse `FORGE_LAUNCHER_PEBBLE_RUNTIME_V1`, `-PebbleProgramRoot` et
`-NativePebbleProof`. Ce dernier désigne `execution.json` produit par
`Verify-NativeDebrisWalk.ps1 -WithPebbles` sur le runtime exact à embarquer.
Une ancienne promenade sans cailloux, un hash de binaire différent ou une preuve
incomplète est refusée avant création du paquet. Cette promenade ne remplace
pas l'inspection visuelle et les essais du launcher et du jeu extraits.
Si Python n'est pas dans le PATH de construction, `-PythonExecutable` accepte
le chemin de son exécutable ; ce runtime ne fait pas partie des dépendances
du destinataire.
Il vérifie leurs pins, conserve tous les payloads et notices et inclut ce
contenu dans les empreintes du manifeste final. La validation d'une nouvelle
distribution exige toujours un build de production puis un parcours depuis
une extraction neuve ; les tests de préparation de commande seuls ne
qualifient ni la fenêtre Tauri ni le jeu extrait.

`npm run dev` sert une copie de `dist/` sur le port 1430 pour le développement.
Le port 8848 sert directement le dossier source `launcher-aube` ; il ne fournit
aucune interface de lancement natif.

Pour vérifier le chargement réel de l'interface embarquée, exécuter le binaire
de production avec `--verify-webview <chemin-de-recu-neuf.json>`, dans un dossier
existant. Ce diagnostic utilise un profil WebView isolé à côté du reçu et une
fenêtre masquée. Il charge Aventures, les styles et le vrai Snapshot IPC, puis
vérifie la disponibilité affichée du runtime. Il faut contrôler `pass` dans le
JSON produit ; le code de sortie seul ne suffit pas. Ce diagnostic ne lance
pas le jeu et ne remplace ni la revue visuelle ni l'essai du paquet extrait.

La revue des licences du code Forge et des illustrations reste distincte de
la qualification technique de cet exécutable.
