# Contrôle transversal de développement — 14 septembre 2026

Workspace existant `Rust`, HEAD f786a38aca2023f08d708687f99a47d14f34e6d4,
arbre modifié préservé (1 539 entrées porcelain au départ). Ce contrôle n'est pas
une qualification sur arbre propre, ni une clôture de Gate 2 sous ADR-0006.
Aucune publication ou copie d'assets Cadle.

## Passage regroupé

`cargo test --workspace --offline --locked -j 2` : exit 0, compilation 4 min 46 s,
943 tests réussis, zéro échec, trois ignorés, 55 suites avec résultats (dont
doc-tests sans cas). Détail machine : `WORKSPACE_TESTS_20260914.json`.
Les ignorés sont deux sondes CPU végétation opt-in et un replay d'archive Studio
opt-in ; ils ne sont pas comptés comme réussis. Pas d'acquisition GPU déduite.

Cache et TEMP/TMP sur E:, CARGO_INCREMENTAL=0, RUSTFLAGS=-C debuginfo=0.
Features par défaut du workspace, pas un test de toutes les combinaisons.
Le passage comprend le moteur, runtime, Studio, CLI, données, monde et assets.
Processus de compilation/tests terminés et absence vérifiée après ce passage.

## Défaut trouvé et correction

Clippy workspace all-targets/all-features a ensuite refusé StudioCommand :
variante Authoring de 272 octets contre 16 pour la suivante. Requête désormais
boxée dans l'enveloppe ; extraction explicite dans le host et constructeurs
frontend adaptés. Aucun changement JSON. Pas d'allow ajouté.

Après correction : Clippy workspace all-targets/all-features PASS (24,37 s).
Test de compatibilité JSON ajouté ; `cargo test -p forge_studio --offline
--locked -j 2` : 63 tests bibliothèque + 4 shell PASS, compilation 43,25 s.
Le contrôle Web a détecté ensuite 102 lignes dans AuthoringControls ; extraction
de l'appel typé au host, puis Clippy WASM/web strict PASS (6,27 s).

Le total 943 décrit le passage avant cette correction d'allocation. Le contrôle
après correction est celui des consommateurs Studio, pas un second passage
workspace de 944 tests. Fmt global et diff check finaux PASS ; absence de processus
Cargo/rustc/candidat confirmée. Aucun lancement visuel n'est acquis.

## Limites et suite

Ces preuves soldent un contrôle transversal de développement, pas toutes les
conditions de la gate : parcours graphique groupé encore dû, outil Windows en
échec lors du dernier préflight ; fonctionnalités et critères restant ouverts
dans la roadmap. Build/package final, supply-chain et qualification propre
restent à traiter selon leur périmètre. CI NOT RUN — infrastructure billing.
