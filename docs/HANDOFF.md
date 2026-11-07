# Reprendre OpenRPG Forge hors Codex

<a id="gate-7-etat-courant"></a>

## Gate 7 — état courant au 7 novembre 2026

**Gate 7 reste ouverte.** R7-01 à R7-06 disposent de verdicts locaux Windows
datés. Le Hub a été retiré du checkout de qualification au lot 048. Le produit suit le parcours
Studio → export du jeu et de son launcher → jeu autonome.

- **R7-07, R7-09 et R7-10 sous Windows.** Le candidat privé 049 couvre le
  Studio installé et l'export de Tidewater et du Grand Plateau dans un
  Sandbox hors ligne sans SDK. Un invité joueur séparé a installé, lancé et
  repris les deux jeux sans Studio. Les cycles de mise à jour, restauration
  et désinstallation du Studio et de Tidewater préservent les données
  contrôlées. Le bilan Windows 049 rapproche 43 pièces.
- **R7-07, R7-09 et R7-10 sous Linux.** Le build privé 0837 et les essais en
  conteneur Debian 12 sans SDK couvrent l'ouverture de la fenêtre Studio
  et, avec le kit 0837, le lancement de diagnostic et la sauvegarde du
  Grand Plateau. Restent à qualifier un build sur les sources courantes,
  le parcours auteur/export, la reprise via le launcher, la mise à jour,
  la restauration et le parcours complet sur un OS propre.
- **R7-08.** Le service d'export natif a produit les crédits de la
  référence Tidewater. L'attribution des dépendances du Studio, dont
  `drain_filter_polyfill` identifié dans le WASM livré, et les droits de
  redistribution des assets et composants restent à établir.
- **Performance du moteur commun.** Dernier trajet Tidewater mesuré sur
  l'export 049 au profil Moyen : 18,85 ms en moyenne, 31,37 ms au p95.
  La cible moyenne de 16,67 ms et la comparaison appariée avec la
  référence restent à vérifier. Une exécution par variante ne permet
  pas d'attribuer l'écart mesuré à la dernière modification.

Voir le [plan](planning/GATE7_IMPLEMENTATION_PLAN.md) et la
[matrice d'acceptation](evidence/gate7/GATE7_PROGRESS_20261023.md#matrice-dacceptation).
Les résultats sont rattachés aux révisions des candidats de qualification.
Le code publié sur la branche Rust peut correspondre à une révision antérieure.

### Reçus de référence

Les reçus ci-dessous appartiennent à la campagne interne
« gate7-monde-distribution ». Les paquets d'essai sont privés.

| Périmètre | Reçu | SHA-256 |
| --- | --- | --- |
| Volets Windows 049 | R7-07-09-10-windows-049-assessment.json | a8cc186a3cf6630ed9a63f2300d7f51e41fa366716f51c1cff16082346afb5d4 |
| Fenêtre Studio Linux 0837 | linux-studio-installed-sdk-free-container-0837.json | 2e2d45dd7256e8f7918e67ac5190b78fa19d8ff6b8432dae1f871e7d782e71d7 |
| Jeu et sauvegarde Linux 0837 | linux-plateau-gameplay-save-container-0837.json | 7d25a478358ae676f449eccd7f9f6b7f2d766e932093484fc0c971fd4680e217 |
| Crédits Tidewater | R7-08-reference-credits-export-049-data.json | cd4626880ef6478e75e1a264861fdac122ab27b99edaada67d89087e36279478 |
| Analyse du WASM Studio 049 | R7-08-studio-wasm-instruction-scope-049.json | 06c812528fae7e234b66b9c73098803aef117674c605b1780d24d150c3f136d8 |
| Mesure Tidewater Moyen | G7-D6-plant-partition-borrowed-comparison.json | 7395b30a91ede1a87d1363cba1072158ef54dc7648e7d5a2084498418694f049 |

## Historique de la passation

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
   propres au sous-système et [ADR-0034](ADR/ADR-0034-github-backup-local-gate-validation.md).
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
Linux, réserve GLib et résultats distants historiques gardent des verdicts séparés.

**État au 22 octobre : Gate 5 clôturée localement**, selon son
[rapport final](evidence/gate5/GATE5_CLOSURE_20261022.md) : treize domaines et
R5-P01/P02 validés. Il s'agit de fondations pour créer des quêtes ensuite, sans
livraison d'aventure. Gate 6 et la gate bonus restent non ouvertes. GitHub sert
de sauvegarde distante ; ses contrôles sont arrêtés et ne conditionnent plus
les gates, selon ADR-0034. Les validations locales restent exigées. La clôture
n'atteste pas un commit ou un push des modifications locales.

**Reprise du 19 octobre :** le propriétaire ouvre Gate 5 avec objectif de clôture,
selon le [plan de réalisation](planning/GATE5_IMPLEMENTATION_PLAN.md). Pour reprendre
un chantier déjà autorisé, établir son objectif, ses fichiers et ses preuves
actuelles avant toute modification ; une ancienne conversation n'est pas requise
pour comprendre les règles du dépôt.

La répartition propriétaire du 20 octobre remplace celle de l'ouverture : E:
porte les sources, outils, téléchargements, compilations, caches et temporaires ;
F: conserve les preuves, l'historique et la bibliothèque d'assets convertis.
Compiler le checkout commun avec `scripts/Invoke-ForgeCargo.ps1`, la cible
`E:/OpenRPG_Forge/.build/cargo-target`, `CARGO_HOME` sous `.build/cargo-home`,
`CARGO_INCREMENTAL=0` et `TEMP`/`TMP` sous `.build/tmp` sur E:.
Neutraliser les anciennes variables pointant vers F:, sans recopier les caches
historiques. Les preuves sont centralisées sous
`E:/OpenRPG_Forge/Preuves-et-analyses/actives/gate5/20261019/`, physiquement sur F:.
L'archive vérifiée est `F:/OpenRPG_Forge/Preuves-et-analyses/archives/gate5-20261022-e-001.zip` ;
son catalogue est `E:/OpenRPG_Forge/Preuves-et-analyses/rapports/gate5-closure-20261022-e-001.json`.
Les anciens chemins et reçus restent historiques. Vérifier les processus actifs
avant toute nouvelle compilation et consulter le rapport final avant de rejouer
une qualification déjà acquise.

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

Sur le poste actuel, la consigne propriétaire du 20 octobre remplace celle de
l'ouverture de Gate 5 : développer et compiler sur E:, conserver preuves,
historique et bibliothèque d'assets sur F:. Les chemins installés déjà constatés
plus haut restent des références de l'installation existante.

```powershell
$env:CARGO_TARGET_DIR = 'E:\OpenRPG_Forge\.build\cargo-target'
$env:CARGO_HOME = 'E:\OpenRPG_Forge\.build\cargo-home'
$env:CARGO_INCREMENTAL = '0'
$env:TEMP = 'E:\OpenRPG_Forge\.build\tmp'
$env:TMP = $env:TEMP
$env:FORGE_STUDIO_LOCAL_ASSETS = 'E:\OpenRPG_Forge\.forge-local\studio-assets'
```

Pour le frontend Gate 5, utiliser une destination de travail sur E: avant
d'appeler le wrapper Trunk, puis conserver les preuves utiles sur F:. Le hook
navigation utilise `TRUNK_STAGING_DIR` et hérite du cache Cargo et des temporaires E:.

```powershell
$env:TRUNK_BUILD_DIST = 'E:\OpenRPG_Forge\.build\frontend\gate5-candidat-unique'
$env:TRUNK_OFFLINE = 'true'
$env:TRUNK_SKIP_VERSION_CHECK = 'true'
$env:CARGO_NET_OFFLINE = 'true'
$env:PATH = 'E:\OpenRPG_Forge\.build\tools\binaryen-131-001\bin;' + $env:PATH
```

Trunk 0.21.14 et wasm-bindgen 0.2.127 sont disponibles sur le poste ; ce dernier
correspond au verrou courant. Les exécutables existants sur C: peuvent être lus,
mais aucun nouvel outil ou cache n'y est installé. Le mode hors ligne refuse un
outil absent avant d'ouvrir le cache automatique de Trunk : vérifier les outils
dans `PATH` et préparer les compléments éventuels sous `.build/tools/` sur E:. Voir le
[code de résolution des outils de Trunk 0.21.14](https://github.com/trunk-rs/trunk/blob/v0.21.14/src/tools.rs#L265).
Cette vérification ne constitue pas un build frontend. Le packager
`Build-StudioCandidate.ps1` accepte désormais une sortie neuve sous
`F:/OpenRPG_Forge/` ; lui fournir explicitement les dossiers de cache et de
temporaires F:. Ses anciens défauts ne sont pas les paramètres de Gate 5.
Le fichier d'entrée demande l'optimisation WASM `z`. Binaryen 131 a donc été
recopié depuis l'outil existant sur E: vers le dossier F: ajouté au `PATH`
ci-dessus, avec vérification d'empreinte, version exécutée et licence Apache 2.0.
Le reçu `wasm-opt-tool-001.json`, sous les preuves Gate 5 sur F:, indique qu'aucun
téléchargement ni build frontend n'a été exécuté par cette préparation.
Le hook Trunk peut déjà avoir copié `forge-kit`. Fournir tout de même
`-GovernedKit` au packager : celui-ci exige alors la même liste de fichiers et
les mêmes empreintes, sans remplacement ni duplication des pins. Les tests
d'entrée `candidate-kit-input-tests-002` du 20 octobre passent sur F:, y compris
le refus d'un fichier caché supplémentaire ; ils ne
constituent ni un build du Studio ni une qualification graphique.
Avant tout lancement graphique du nouveau binaire, fixer aussi :

```powershell
$env:FORGE_WEBVIEW_DATA_ROOT = 'E:\OpenRPG_Forge\.build\webview\studio'
```

Le raccordement natif `native_webview.rs` diffère alors la création automatique
des fenêtres et fournit ce chemin absolu au constructeur Tauri avant toute
création de WebView ; la fenêtre Atelier emploie le même dossier. Une valeur
vide ou relative est refusée au démarrage. Ce réglage est nécessaire car le
gestionnaire Tauri peut créer son dossier local par défaut avant l'appel à
WebView2. Les constats préparatoires du 20 octobre sont remplacés, pour l'état
courant, par les validations du candidat final et les parcours graphiques acquis
dans le [rapport Gate 5](evidence/gate5/GATE5_CLOSURE_20261022.md).
Vérifier le chemin effectif dans les processus WebView2 lors d'un nouveau
lancement et conserver les preuves nécessaires dans le catalogue central sur F:.

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
