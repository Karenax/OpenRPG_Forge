# Gate 3 — état CI et disponibilité des contrôles locaux au 13 octobre 2026

Constat produit dans le checkout commun `E:\OpenRPG_Forge\OpenRPG_Forge-Rust`,
entre 13:25 et 13:40 UTC environ, par lectures Git/GitHub et contrôles locaux
légers. Ce document ne clôture pas Gate 3 et ne transforme aucun refus de CI
en PASS. Le propriétaire a accepté la boucle RPG dans le jeu séparé et le
parcours créé depuis Studio ; cette acceptation humaine est distincte des
contrôles techniques décrits ici.

## Révision et état des entrées

- Branche : `Rust`.
- HEAD et SHA distant lu par `git ls-remote origin refs/heads/Rust` :
  `4799ebb3dfa98795d97862f0fed3cd4f6a876887`.
- Arbre Git : `0fe90bf7d17a034d0b84f5d41f67066da6410b2b`.
- Divergence `HEAD...origin/Rust` : `0 / 0`.
- Commit applicatif précédent : `fadd790dd7dfb82dd84198744ac25e58de9226b8` ;
  HEAD ajoute la documentation de publication et du refus de CI.
- Checkout modifié : travaux indépendants de bibliothèque d'assets, moteur,
  scripts `hivemind` et retraits de configuration Codex présents. Ils ont été
  conservés. Le SHA Git ci-dessus ne représente donc pas toutes les entrées
  du checkout commun utilisées par un futur build local.

L'instruction propriétaire du 11 octobre impose les compilations et validations
dans cette version commune avec un manifeste des entrées suivies, modifiées et
non suivies. Aucun clone, worktree ou copie de code n'a été créé. Un futur verdict
local devra citer son propre manifeste exact et les hashes de ses binaires ;
le précédent EXE Studio ne qualifie pas automatiquement les nouvelles entrées.

## Résultat distant courant

Le [run 38394829662](https://github.com/Karenax/OpenRPG_Forge/actions/runs/38394829662),
créé à `2026-10-13T12:06:02Z`, vise le HEAD exact
`4799ebb3dfa98795d97862f0fed3cd4f6a876887`. Il est `completed / failure`.
Les quatre jobs ont une liste d'étapes vide (`steps=[]`).

| Job | ID du job/check-run | Conclusion GitHub | Étapes | Qualification Forge |
|---|---:|---|---:|---|
| [native (windows-2025)](https://github.com/Karenax/OpenRPG_Forge/actions/runs/38394829662/job/115287273553) | 115287273553 | failure | 0 | NOT RUN — infrastructure billing |
| [native (ubuntu-24.04)](https://github.com/Karenax/OpenRPG_Forge/actions/runs/38394829662/job/115287273463) | 115287273463 | failure | 0 | NOT RUN — infrastructure billing |
| [wasm](https://github.com/Karenax/OpenRPG_Forge/actions/runs/38394829662/job/115287273027) | 115287273027 | failure | 0 | NOT RUN — infrastructure billing |
| [supply-chain](https://github.com/Karenax/OpenRPG_Forge/actions/runs/38394829662/job/115287273652) | 115287273652 | failure | 0 | NOT RUN — infrastructure billing |

Les annotations des quatre check-runs ont été lues séparément avec
`gh api repos/Karenax/OpenRPG_Forge/check-runs/ID/annotations`. Elles portent
sur `.github`, ligne 1, niveau `failure`, et contiennent le même message exact :

> The job was not started because recent account payments have failed or your spending limit needs to be increased. Please check the 'Billing & plans' section in your settings

Le [run applicatif 38394515234](https://github.com/Karenax/OpenRPG_Forge/actions/runs/38394515234),
créé à `2026-10-13T12:03:21Z` sur `fadd790dd7dfb82dd84198744ac25e58de9226b8`,
présente également quatre jobs `failure` avec `steps=[]` : Windows
`115286211896`, Linux `115286212220`, WASM `115286212234`, supply-chain
`115286212341`. Son annotation Windows porte la même cause de facturation.

Commandes de constat : `gh run view ID --repo Karenax/OpenRPG_Forge --json
conclusion,status,headSha,createdAt,jobs,url`, `gh run list --repo
Karenax/OpenRPG_Forge --branch Rust --workflow rust-gate.yml --limit 6`, et
lecture des annotations ci-dessus. Aucun workflow n'a été relancé ou modifié.

## Dernière exécution distante réelle et corrections présentes

Le [run 38123749165 du 11 octobre](https://github.com/Karenax/OpenRPG_Forge/actions/runs/38123749165)
a réellement exécuté les runners sur
`48a894d550c5770a5ea50bc54707113e85ab5218` :

- WASM et supply-chain ont réussi leurs étapes ;
- Windows a échoué à Clippy sur `needless_pass_by_value` dans
  `apps/forge_studio/src/main.rs:86` et `redundant_closure` à la ligne 103 ;
- Linux a échoué sur `std::os::windows::fs::symlink_file` utilisé sans garde
  dans `apps/forge_studio/src/asset_library_native.rs:2162`, ainsi que sur
  l'argument transmis par valeur ;
- les tests workspace et les deux builds natifs qui suivent Clippy ont été
  `skipped`. Leur réussite ne peut pas être déduite du formatage ou des jobs
  WASM/supply-chain.

Les logs échoués ont été lus par `gh run view 38123749165 --repo
Karenax/OpenRPG_Forge --log-failed`. Une lecture directe des sources courantes
et de `git show HEAD:CHEMIN` confirme la présence des correctifs dans le HEAD
publié :

- `execute_module_model_request` reçoit `&ModuleModelRequest` et utilise
  `.map_err(ToString::to_string)` ;
- le test de refus d'échappement des miniatures déclare le lien avec
  `#[cfg(windows)] ...symlink_file` ou `#[cfg(unix)] ...symlink`.

Il s'agit d'une vérification de sources. Elle ne prouve ni un Clippy courant
PASS, ni une compilation Linux ; les runs récents n'ont testé aucun de ces
correctifs. Le fichier de bibliothèque conserve ses autres modifications
locales, sans qu'elles soient absorbées dans le commit publié par cette lecture.

## Applicabilité de l'exception ADR-0006

[ADR-0006](../../ADR/ADR-0006-remote-ci-exception-gates-0-4.md) réserve
l'exception aux refus avant runner/étape pour facturation, et interdit de
présenter ces `NOT RUN` comme une CI verte. Le refus courant correspond bien
à cette cause, mais l'exception a déjà expiré :

- le retour réel des runners a été observé depuis le run du 3 octobre, puis
  encore le 11 octobre ;
- la préparation d'un installateur ou d'une distribution est également un
  déclencheur explicite d'expiration, sans prolongation tacite ;
- l'actualisation du 10 octobre exige la correction des échecs source et une
  CI PASS sur le candidat final.

Le retour de la panne de facturation ne réactive donc pas automatiquement
l'ancienne exception. Sous les décisions actuelles, la CI réelle reste une
condition de clôture technique. Une nouvelle exception ne peut pas être
inventée par ce rapport ; elle exigerait une décision propriétaire explicite
et bornée. Une acceptation humaine de la boucle RPG ne vaut pas cette dérogation.

## Checks requis par le workflow actuel

[`.github/workflows/rust-gate.yml`](../../../.github/workflows/rust-gate.yml)
définit quatre jobs, déclenchés par push ou pull request sur `Rust` :

| Périmètre | Commandes de qualification après installation des outils épinglés |
|---|---|
| Windows et Linux | `cargo fmt --all -- --check` ; `cargo clippy --workspace --all-targets --locked -- -D warnings` ; `cargo test --workspace --all-targets --locked` ; `cargo build --locked --release -p forge_runtime` ; `cargo build --locked --release -p forge_engine --example tidewater_plant_probe` (ajout local, voir ci-dessous) ; `cargo tauri build --no-bundle` depuis `apps/forge_studio` |
| WASM | `cargo check --locked --target wasm32-unknown-unknown -p forge_engine --no-default-features --features web` ; `trunk build --release --config Trunk.toml` depuis `apps/forge_studio` |
| Supply-chain | `cargo deny check licenses bans sources advisories` ; `./scripts/Test-RustSec.ps1` après installation de cargo-audit |

La lecture REST de la protection de branche a retourné HTTP 403 (fonction
non disponible pour le dépôt sous son plan actuel). Ce rapport décrit les
checks prescrits par le workflow et la gouvernance ; il ne prétend pas avoir
lu une liste de checks de protection de branche.

Pendant ce chantier, une correction locale du workflow ajoute `Build separate
native game` après le build de `forge_runtime`, avec la commande release de
`tidewater_plant_probe` ci-dessus. Le jeu séparé courant utilise cet exécutable ;
le seul build release de `forge_runtime` ne le qualifiait pas. Le workflow du
HEAD distant `4799ebb3` ne contient pas encore cette nouvelle étape. Elle est
**locale et non publiée**, et aucun run distant ne l'a exécutée.

La campagne locale coordonnée fixe aussi `RUST_TEST_THREADS=2` pour limiter la
concurrence des fixtures volumineuses utilisant les pools globaux. Elle conserve
les mêmes tests, assertions et délais ; ce paramètre ne requalifie pas l'ancien
échec. Les campagnes et reçus doivent déclarer leur concurrence exacte. Il ne
constitue pas une modification tacite de configuration CI distante.

## Contrôles locaux légers effectués

Node utilisé :
`C:\Users\karen\.cache\codex-runtimes\codex-primary-runtime\dependencies\node\bin\node.exe`.

| Commande dans le checkout commun | Résultat observé |
|---|---|
| `node --test apps/forge_studio/tests/editor-gpu-pacing.test.cjs` | exit 0 ; 8 tests PASS, 0 FAIL, 0 skipped, 0 cancelled |
| `node --check apps/forge_studio/editor-gpu-pacing.js` | exit 0 |
| `node --check apps/forge_studio/gpu-memory-observation.js` | exit 0 |

Ces huit tests couvrent les callbacks, annulations, cadence actif/repos et
pause/reprise. Ils ne sont ni une session GPU, ni un benchmark, ni une preuve
du parcours RPG ou de plusieurs heures d'édition. `--check` valide uniquement
la syntaxe JavaScript.

SHA-256 observés pour ces entrées :

- `editor-gpu-pacing.js` :
  `e3bde09cacdadeb13e61cc6a08733086e382c59fcbaab368b39d910430679b7d`.
- `tests/editor-gpu-pacing.test.cjs` :
  `68aa387be44dd2296a3132daa6216d0509ef5d476f046e5783c4029de94e7b7a`.
- `gpu-memory-observation.js` :
  `f7a07f4f2863913bd57f2d2178d2ab02c9e23b91d57d903da7b702cfdbefb68c`.

## Disponibilité locale, sans installation ni téléchargement de code

Les commandes de version retournent cargo-deny **0.20.2**, cargo-audit
**0.22.2**, Trunk **0.21.14** et wasm-bindgen **0.2.127**. Les binaires sont
disponibles dans `C:\Users\karen\.cargo\bin`. `rustup target list --installed`
retourne seulement `wasm32-unknown-unknown` et `x86_64-pc-windows-msvc`.

`Get-Command` ne trouve pas `docker`, `podman` ni `cross` sur le PATH.
`wsl.exe` existe, mais `wsl --list --verbose` n'établit aucune distribution
utilisable : la requête ne donne d'abord aucune sortie, puis retourne
« Classe non enregistrée », `Wsl/CallMsi/Install/REGDB_E_CLASSNOTREG`, exit 1.
La clé `HKCU\Software\Microsoft\Windows\CurrentVersion\Lxss` est absente.
La requête WSL diagnostique devenue bloquée a été nettoyée uniquement sur ses
deux PID identifiés, après vérification de leurs arguments et parentage ; aucun
service WSL ou processus d'une autre conversation n'a été arrêté.

Une compilation Linux locale n'est donc pas disponible par les outils
observés. Aucune installation WSL, distribution, container, target Rust ou
dépendance n'a été demandée ou exécutée.

Les contrôles cargo-deny/RustSec et les compilations attendent la coordination
du cache Cargo commun. Ils ne sont pas encore exécutés par cet audit. Le script
RustSec supporte `-NoFetch` pour vérifier une base déjà présente sans rafraîchir
les advisories ; ce résultat conserverait la date de cette base et ne vaudrait
pas un audit de fraîcheur. La politique existante retient notamment
`glib 0.18.5 / RUSTSEC-2024-0429` avec `distribution_blocking=true` : un PASS
de la politique ne retire pas cette restriction de distribution.

## Suite de qualification

Après coordination : valider les entrées communes exactes, exécuter les checks
locaux appropriés, construire les binaires nécessaires et observer leurs
parcours réels, sans remplacer ces derniers par un simple build. Conserver les
preuves des interactions environnementales et de la réaction IA séparément
de l'acceptation humaine de la boucle RPG. Terminer le dossier local de clôture,
puis traiter le blocage externe de facturation et obtenir une CI réellement
PASS sur la révision finale publiée, sauf nouvelle décision propriétaire.

Aucun commit, push, rerun, paiement, affaiblissement de check ou changement
d'ADR n'a été effectué pour ce constat. Gate 3 reste ouverte à cette étape.

## Diagnostic du test terrain concurrent — 13 octobre, 13:48 UTC

Un contrôle workspace exécuté par un autre chantier dans le checkout commun
termine avec 896 tests moteur PASS, un FAIL et 31 ignorés, après 404,58 s.
Le log conservé est
`F:/OpenRPG_ImportWork/hivemind-native-bevy-curve-compute-v1617/cargo-workspace-test-selected.log`.
L'échec concerne
`terrain_quality::tests::background_bank_change_keeps_geometry_without_qualifying_stale_projection` :
panique sur le watchdog « terrain background deadline », à la ligne 496 du test.
Les assertions de géométrie, handles et hash ne sont pas la cause observée.
Plusieurs autres tests terrain de ce même processus dépassent soixante secondes.

Avant toute correction, le binaire exact de ce log est relancé avec
`--exact terrain_quality::tests::background_bank_change_keeps_geometry_without_qualifying_stale_projection --nocapture`.
Résultat isolé : exit 0, un test PASS, zéro FAIL, 927 filtrés, 19,98 s.
Le binaire est
`F:/OpenRPG_Forge/relocated-work/Rust-target/debug/deps/forge_engine-bf47a7d727cc1cb7.exe`,
SHA-256 `089e791bc3a383d6b22bcebbe56b8194b7bec36797df5d52aa284b4f5cf54781`.
Son hash et ceux des trois sources terrain concernées sont identiques avant et
après ce diagnostic. Le reçu indique explicitement `qualification=false` :
ce rejeu d'ancien binaire ne qualifie pas les sources communes actuelles.

La correction locale approuvée conserve la carte complète, le watchdog de
trente secondes et toutes les assertions. Elle synchronise le test sur
`Task::is_finished()` du worker réel, puis exécute un seul `app.update()` pour
publier et vérifier le résultat. La boucle précédente répétait les mises à jour
complètes de l'application pendant la préparation, donc les protections et
empreintes des mêmes chunks, en concurrence avec les autres tests et les pools
globaux. Le helper `Preparation::is_finished` est sous `cfg(test)` ; le chemin
de préparation de production, sa clé de fraîcheur et sa publication restent
inchangés. La contention est cohérente avec le contraste entre le FAIL global
et le PASS isolé ; le correctif reste à confirmer dans les nouveaux tests.

`rustfmt --edition 2024 --check` sur les deux fichiers et `git diff --check`
passent. Aucun build ou rejeu du nouveau code n'a encore été lancé par cet audit.
Les sources, le diff et les reçus sont épinglés dans
`E:/OpenRPG_Forge/.gate3-evidence/20261013/closure-terrain-diagnostic-001`.
La qualification requiert une compilation coordonnée du checkout commun puis
le test ciblé et une non-régression sur le nouveau binaire exact.
