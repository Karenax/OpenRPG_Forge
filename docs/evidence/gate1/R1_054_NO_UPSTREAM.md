# R1-054 — build, tests et vallée depuis une copie sans upstream

2026-09-06. Preuve locale d'indépendance du dossier `upstream/cadle`, pas une qualification
de distribution hermétique ou un passage de Gate 1. Branche de travail Rust inchangée.

## Copie de validation

`E:/OpenRPG_Forge/.gate1-evidence-runtime/r1-no-upstream-20260906/source` contient 190 fichiers
du workspace courant : manifestes Rust, licences, configuration Cargo, crates, apps, exemples
et CLI. Inventaire issu de `git ls-files --cached --others --exclude-standard`, filtré sur les
fichiers réellement présents ; les suppressions locales ne sont pas ressuscitées. Liens symboliques
refusés. Chaque copie est vérifiée par SHA-256 avant compilation.

Ni `upstream` ni `.git` ne sont présents. Pas de branche ou worktree créé. Le clone canonique
Cadle reste intact dans le workspace original : cette expérience l'omet de la copie, elle ne le
supprime pas du disque. Le manifeste `source-snapshot.json` épingle les 190 chemins/tailles/hashes.
Après build/tests/exécution, les 190 fichiers originaux et les 190 copies correspondent encore.
Les sorties générées des outils ne sont pas présentées comme une copie Git propre qualifiante.

## Commandes et résultats

Variables : `CARGO_TARGET_DIR=C:/Users/karen/AppData/Local/Temp/openrpg-forge-r1-064-target`,
`CARGO_INCREMENTAL=0`, `RUSTFLAGS=-C debuginfo=0`.

```text
cargo build -p forge_runtime --locked --offline --manifest-path <source>/Cargo.toml
cargo test --workspace --locked --offline --manifest-path <source>/Cargo.toml -q
```

Build code 0 ; le journal de compilation identifie les crates moteur/runtime de la copie.
Tests code 0, **397 PASS**, zéro échec/ignoré : Assets 16, CLI 3+4, Core 11, Engine 153,
Project 45, Runtime 61+1, Studio 22+3, World 78. Somme extraite des lignes finales de `tests.log`.
Le cache Cargo et les dépendances déjà installées sont réutilisés : ni rebuild intégral des
dépendances ni test sur machine privée de tous les caches.

Le binaire construit est copié dans la racine de preuve et exécuté avec cwd `<source>` :

```text
../forge_runtime.exe --world-sketch-visual-valley
  --asset-kit-build ../kit/asset-kit-build.v1.json
  --asset-kit-preview ../kit/preview.json --bridge-crossing-probe
```

`kit/` contient seulement le rapport, l'aperçu et les six GLB autonomes du candidat gouverné,
copiés avec revalidation de leurs hashes. Aucun asset Cadle, ni accès requis à son dossier.
RX 7800 XT/Vulkan : 20 instances et 36 colliders prêts ; traversée 518/518 ticks au sol,
435 contacts pont, progression 42,5282059 m ; bindings terrain/joueur/pont conservés, arrêt au sol.
Sortie 0, aucun ERROR, `runtime_errors_observed=false`. Avertissements amont `@invariant` conservés.
Tests/compilation concomitants possibles pendant ce smoke : aucune mesure de performance déduite.

## Artefacts et limites

Racine commune : `E:/OpenRPG_Forge/.gate1-evidence-runtime/r1-no-upstream-20260906`.

| Artefact | SHA-256 |
|---|---|
| `source-snapshot.json` | `ab122ff77e007f181c1d11848d8f835a7a3c450248ad5f282c2afe73e07f2deb` |
| `tests.log` | `738cf77ce5a9164869a57d14315cc8da789da87ae7da4c73a98575980688d645` |
| `runtime.log` | `65a781fb4d48b0ce62d5d2589d34103893870b2d3776d2a60552dc080bf25c5e` |
| `forge_runtime.exe` | `36d8d47945f19ee331d5d8c574aef68f8cf46c65e746ff5cb9988b6aa26669c8` |

La preuve couvre le runtime natif et les tests du workspace sans dossier upstream dans leur
copie source. Elle ne couvre pas le packaging final, un lancement Studio/Tauri depuis cette copie,
un build Web complet ou une isolation OS interdisant tout accès au workspace original. Aucun
produit redistribuable n'est livré ici. CI distante NOT RUN ; revue humaine et Gate 1 ouvertes.

## Rectification de comptage

Les récents totaux narratifs 495/496 étaient des erreurs d'addition, pas le nombre de tests affiché
par Cargo. Les sorties disponibles du scénario partagé totalisent 395, celles du candidat HLOD
396 ; ses modifications ont ensuite été retirées. Les deux tests caméra ajoutés portent l'état
actuel à 397. Le présent journal conservé fait autorité pour ce run. Aucun test omis ou échec
masqué pour obtenir ce total ; ne pas extrapoler cette correction aux anciens runs sans leurs logs.
