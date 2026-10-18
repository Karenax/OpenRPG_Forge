# Reprendre OpenRPG Forge hors Codex

Validation du 18 octobre : [rapport de séparation moteur/carte](evidence/architecture/STRICT_MAP_SEPARATION_20261018.md) ;
14 contrôles réussis et 2 232 tests du workspace réussis. Les preuves de runtime
générique et des extensions optiques gardent des périmètres distincts.
Le dernier parcours natif instrumenté de la carte conserve une réserve de cadence
(44,81 FPS moyens pour un seuil de 60) ; ses contrôles fonctionnels passent.

Dernière revue : 18 octobre 2026. Ce guide est un point d'entrée opératoire,
pas un second contexte narratif. Aucun compte Codex, plugin mémoire ou historique
de conversation n'est requis pour lire le projet et développer ses sources.

## Lire et choisir le périmètre

Depuis la séparation du 18 octobre, développer le moteur dans `crates/forge_engine`
et les contrats génériques d'assets dans `crates/forge_assets`. La carte, ses
ports et ses données résident dans `crates/forge_tidewater` et
`crates/forge_tidewater_assets`. Aucun alias des anciennes API de carte ne reste
dans le moteur. Les exemples de qualification se lancent avec `-p forge_tidewater`
et les vérificateurs de données avec `-p forge_tidewater_assets` ; leurs noms et
arguments restent les mêmes. Lire [ADR-0026](ADR/ADR-0026-strict-engine-map-separation.md)
et les README des composants avant de reprendre un ancien script.

`scripts/Test-GenericEngine.ps1 -PrepareBrowser` contrôle aussi le moteur seul,
sans fonctionnalités par défaut et en WASM, puis le workspace et les ports.
Les scripts historiques de `scripts/hivemind` ne constituent pas des outils
produit et ne sont pas réécrits par cette séparation.

1. Lire [PROJECT_CONTEXT.md](../PROJECT_CONTEXT.md), qui porte l'état canonique.
2. Lire [AGENTS.md](../AGENTS.md) : architecture, gouvernance et règles de travail,
   applicables aussi à un développeur humain ou à un autre assistant.
3. Consulter les [ADR](ADR), en particulier ADR-0001 à ADR-0006, puis les décisions
   propres au sous-système et [ADR-0023](ADR/ADR-0023-gate3-owner-removes-github-ci-criterion.md).
4. Lire l'état courant de la [roadmap](planning/MIGRATION_ROADMAP.md), puis le
   README du sous-système concerné.

La branche active est `Rust`, dans `E:\OpenRPG_Forge\OpenRPG_Forge-Rust` sur le
poste actuel. `main` est la référence historique C#/Godot, sans dépendance
d'exécution du produit Rust. Sur un autre poste, utiliser un clone de `Rust` et
adapter les chemins ; sur ce poste, conserver le checkout commun.

Gate 3 est clôturée le 15 octobre ; Gate 4 est **clôturée localement** le
16 octobre, lot `0.1.0-preview.20261016.019`. Les
[rapports Gate 3](evidence/gate3/GATE3_CLOSURE_20261015.md) et
[Gate 4](evidence/gate4/GATE4_CLOSURE_20261016.md) portent les critères et limites.
Les anciennes sections « reste ouvert » ou « à acquérir » sont des constats datés,
pas des instructions de recommencer les gates. Distribution, Windows vierge,
Linux, réserve GLib et CI distante gardent des verdicts séparés.

**Prochaine étape :** choisir un périmètre explicite avec le propriétaire à partir
de cet état. Aucune gate suivante n'est ouverte par la passation. Pour reprendre
un chantier déjà autorisé, établir son objectif, ses fichiers et ses preuves
actuelles avant toute modification ; une ancienne conversation n'est pas requise
pour comprendre les règles du dépôt.

## Préserver le travail local avant la reprise

Le commit de référence de l'inventaire initial du 17 octobre est
`2b0d60fa` (`feat: close Gate 4 governed assets and standalone module delivery`).
Cela identifie un état Git, pas tous les fichiers locaux ni tous les octets
qualifiés : le rapport Gate 4 référence aussi un gel de sources et ses preuves.

La séparation du 18 octobre travaille depuis
`3c3a5efeba2473574597871f223d74aa800cf99a`, toujours sur le checkout partagé
modifié. Les nouveaux composants de carte et les contrats neutres doivent être
conservés avec les suppressions de leurs anciens emplacements : un clone du
commit seul ne reproduit pas ce lot local avant son commit et sa publication.

L'[inventaire local daté](handoff/LOCAL_WORKTREE_20261017.json) liste les états
Git et les chemins visibles pendant cette passation, hors fichiers de cette édition,
avec taille et SHA-256 des fichiers texte de travail lisibles. Les binaires
locaux sont seulement recensés par chemin et taille. Les suppressions restent
des suppressions ; un inventaire n'est ni une sauvegarde ni un commit.
Les fichiers ignorés et les données hors checkout ne sont pas inventoriés par Git.
Le travail concurrent peut évoluer après cette capture.

Le lot local comprend notamment les scripts d'import `scripts/hivemind/`, leurs
deux documents de suivi, des suppressions de configuration Codex et d'anciens
artefacts d'installateur. Ne pas tout ajouter à Git automatiquement. Les suivis
locaux `docs/planning/HIVEMIND_SAVED_PHYSICS_REFERENCE.md` et
`docs/planning/HIVEMIND_SOURCE_LOD_HIERARCHY.md` font partie de ce travail non
publié ; leurs preuves privées peuvent être sur `F:`. Ils sont recensés dans
l'inventaire et doivent être récupérés avec la sauvegarde locale si nécessaires.

Depuis la racine du checkout, ces commandes de lecture permettent de réévaluer
la situation (exécutées pendant la revue) :

```powershell
git branch --show-current
git rev-parse HEAD
git status --short
git diff --name-status
git ls-files --others --exclude-standard
```

Avant de changer de machine, conserver une sauvegarde privée du checkout avec
ses fichiers locaux, des données ci-dessous et des cibles de jonctions utilisées.
Vérifier la relecture et les empreintes de cette sauvegarde. Un clone Git ne
contient pas les fichiers non suivis, les fichiers ignorés ou les données privées.
Ne pas exécuter `reset --hard`, `clean` ou un nettoyage global pour reprendre.
Ne pas restaurer les anciennes configurations d'agents supprimées. Aucun commit
ou push n'est autorisé par ce guide.

## Environnement de développement

La procédure actuellement documentée vise Windows x64. Préparer Git, PowerShell
7.2 ou plus récent (`pwsh`, utilisé par les hooks), Rustup avec MSVC/Windows SDK,
WebView2 pour le shell Studio, Python 3.11 ou plus récent pour les outils et Node.js pour le
frontend Aube et les tests navigateur. Ces outils de développement ne sont pas
des prérequis du client installé. Linux n'est pas qualifié par cette passation.

La [toolchain](../rust-toolchain.toml) épingle Rust **1.98.0**, rustfmt, Clippy,
rust-src et `wasm32-unknown-unknown`. Le
[bootstrap](../scripts/Bootstrap-Rust.ps1) épingle cargo-deny 0.20.2,
cargo-audit 0.22.2, Trunk 0.21.14, Tauri CLI 2.11.4 et wasm-bindgen-cli 0.2.127.
Le script télécharge des dépendances et refuse une branche autre que `Rust`.
Les dépendances locales sous `third_party/` sont nécessaires : garder le clone
complet et `Cargo.lock`. Cadle n'est pas requis pour compiler Forge.

Procédures déjà présentes dans le dépôt, à exécuter sur le poste de reprise après
installation des prérequis (non relancées lors de cette édition documentaire) :

```powershell
.\scripts\Bootstrap-Rust.ps1 -InstallTools
.\scripts\Test-Rust.ps1
```

Le second script lance formatage, Clippy, tests natifs, contrôles WASM, Trunk et
supply-chain. Sélectionner les validations selon le changement et le protocole,
sans assimiler compilation, tests, GPU, acceptation humaine et qualification.
La supply-chain conserve une réserve GLib/Linux ; lire le résultat effectif.

## Données et lancement du poste actuel

Le point d'entrée du propriétaire reste `E:\OpenRPG_Forge\Lancer-Editeur.cmd`.
Il lance le Studio commun et définit `OPENRPG_ASSET_VAULT`. Préserver ce lanceur
et son profil ; changer d'éditeur de code ne nécessite pas de les remplacer.
Les valeurs suivantes ont été lues dans le lanceur, le profil et les scripts le
17 octobre ; elles sont des chemins du poste actuel, pas des valeurs universelles.

| Élément à conserver ou remapper | Emplacement actuel / rôle |
|---|---|
| Studio et profil côte à côte | `E:\OpenRPG_Forge\.gate3-ntfs-cargo-20261002\release\forge-studio.exe` et `forge-studio.local.json` |
| Projets et cartes auteur | champ `authoring_root` du profil : `E:\OpenRPG_Forge\.gate3-evidence\20261011\p05-human-review-shared019-rpg007-001\authoring-data` |
| Vault privé | `F:\OpenRPG_AssetVault`, référencé par le lanceur et `asset_vault` |
| Assets du frontend | `E:\OpenRPG_Forge\.forge-local\studio-assets`, avec `frontend-static-inputs.json` |
| Données du jeu et des scènes | `E:\OpenRPG_Forge\.forge-local\runtime-data` ; `module_assets.root` vise `data\scene` et son catalogue épinglé |
| Jeu natif séparé | `game.executable` : `E:\OpenRPG_Forge\.gate3-ntfs-cargo-20261002\release\examples\tidewater_plant_probe.exe` |
| Rapports des parties | `game.session_root` : `E:\OpenRPG_Forge\.forge-local\game-sessions` |
| Preuves et livraisons locales | dossiers voisins `.gate3-evidence`, `.gate4-evidence`, installateur et contenus référencés par les rapports |
| Travaux privés d'import | `F:\OpenRPG_ImportWork` et les emplacements indiqués par les suivis d'import |

Sur une autre machine, rétablir les données nécessaires puis préparer un profil
pour cette machine avec ses chemins. Les chemins relatifs du profil se résolvent
par rapport à l'EXE. Ne pas retirer les vérifications de catalogue/hash pour
compenser une donnée absente. Les projets, sauvegardes, sources privées et leurs
licences doivent être transférés de manière autorisée ; ils ne sont pas inclus
automatiquement dans Git. Les liens des rapports vers les preuves voisines ne
seront utilisables qu'après restauration de ces dossiers.

## Compiler le Studio et préparer une livraison

Le hook [Trunk](../apps/forge_studio/Trunk.toml) compile le worker de navigation
et prépare les assets. Il exige `CARGO_TARGET_DIR` explicitement ; le staging
utilise `FORGE_STUDIO_LOCAL_ASSETS` ou, par défaut, le dossier voisin
`.forge-local/studio-assets`. Il contrôle le manifeste et refuse certaines
jonctions. Un répertoire vide ne remplace pas les données authentifiées.
La présence du manifeste frontend a été constatée pendant la revue ; son contenu
n'a pas été requalifié. L'accès au Vault `F:` a été refusé par l'environnement
de cette session : sa disponibilité et son contenu restent à vérifier sur le
poste de reprise. Ne pas confondre refus d'accès et absence du dossier.

Sur le poste actuel, les valeurs utilisées par la procédure commune sont :

```powershell
$env:CARGO_TARGET_DIR = 'E:\OpenRPG_Forge\.gate3-ntfs-cargo-20261002'
$env:FORGE_STUDIO_LOCAL_ASSETS = 'E:\OpenRPG_Forge\.forge-local\studio-assets'
```

Le wrapper [Invoke-StudioTrunk.ps1](../scripts/Invoke-StudioTrunk.ps1) gère le
build/serve et normalise `NO_COLOR`. Pour une reconstruction livrable, suivre
[scripts/delivery/README.md](../scripts/delivery/README.md), le validateur commun
et ses reçus, plutôt que d'assembler d'anciens binaires. Le target générique
`forge_runtime` et le jeu livré `tidewater_plant_probe` ont des rôles distincts.
Sérialiser les builds lourds et préserver les sessions du propriétaire.

Le launcher Aube, le gestionnaire de contenus, Studio, jeu natif et packs de
contenus sont des composants distincts. Le
[rapport Gate 4](evidence/gate4/GATE4_CLOSURE_20261016.md) identifie l'installateur
unique et les contenus séparés du lot 019, avec empreintes et limites.

## Fin d'une session de reprise

Consigner le périmètre modifié, les commandes réellement exécutées et leurs
résultats, les preuves et limites, puis la prochaine action concrète. Actualiser
le contexte ou la roadmap seulement si leur état change. Ne pas stocker une
décision indispensable uniquement dans la mémoire d'un assistant. Une mémoire
ancienne ne remplace jamais les fichiers et preuves actuels du projet.

Cette passation a été vérifiée par lecture des scripts, contrôle des liens
documentaires et `git diff --check`. Aucun bootstrap, build, test produit,
lancement de jeu ou transfert de données n'a été effectué pour cette édition.
Le propriétaire demande ensuite la publication du lot documentaire sur `Rust` ;
elle conserve les autres travaux locaux hors de son périmètre.
La reprise depuis un clone neuf sur un autre PC reste à éprouver.

## Contrôles des entrées moteur communes — 18 octobre 2026

Le [bilan d’architecture](planning/ENGINE_RESERVES_20261018.md) distingue les
corrections réalisées de la généricité globale encore ouverte. Le runner local
reproduit les garde-fous, Clippy natif/WASM, tests workspace et le probe optique :

```powershell
pwsh -NoProfile -File scripts/Test-GenericEngine.ps1 -PrepareBrowser
```

Il écrit dans un nouveau dossier `output/generic-engine-<date>`, déjà ignoré par
Git. Il exige une toolchain et les dépendances Rust disponibles localement
(`--locked --offline`) et le CLI `wasm-bindgen` pour préparer le navigateur.
`-TargetDirectory` permet de choisir le cache Cargo. L’étape navigateur doit être
exécutée sur localhost avec WebGPU : la préparation ne vaut pas preuve de rendu.
Le [guide des API](../crates/forge_tidewater/COMMON_SCENE_INPUTS.md) précise les
conventions et limites. Ce runner n’installe ni ne publie le produit et ne clôture
aucune gate.

Pour le probe navigateur, servir le dossier de résultat affiché par le runner :

```powershell
python -m http.server 8769 --bind 127.0.0.1 --directory "output/generic-engine-<date>"
```

Ouvrir `http://127.0.0.1:8769/index.html` dans un navigateur WebGPU. La page doit
afficher PASS après les dix readbacks, puis arrêter le serveur avec Ctrl+C.
Les outils vérifiés ici sont Rust 1.98.0 et `wasm-bindgen` 0.2.127, ce dernier
devant correspondre au lockfile. Les dépendances et le CLI doivent être disponibles
localement avant les commandes `--offline`.
