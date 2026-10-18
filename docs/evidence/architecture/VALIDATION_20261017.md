# Validation des corrections d'architecture — 17 octobre 2026

Périmètre : [analyse et limites](../../planning/ENGINE_ARCHITECTURE_DEBT_20261017.md).
Checkout commun `Rust`, base `3c3a5efeba2473574597871f223d74aa800cf99a`, modifié.
Les autres travaux locaux sont préservés et ne sont pas qualifiés par ce bilan.

## Contrôles exécutés

| Contrôle | Résultat |
|---|---|
| Garde-fous Python avant corrections | RED : deux tests échouent sur les copies et l'inclusion vers l'orchestration |
| Garde-fous Python après corrections | PASS : quatre tests, dont refus d'un include construit vers l'orchestration |
| Inventaires réels du checkout | PASS : 2 518 entrées de build ; zéro fichier d'orchestration compilé ; 2 521 entrées d'orchestration conservées dans le checkout |
| Contrat d'archives avant refactorisation | PASS : un test, versions historiques, réouverture, Undo, octets et refus |
| `cargo test -p forge_project --lib --tests --locked --offline` | PASS : 278 tests, 0 échec, 1 ignoré, 50 suites |
| Tests CPU moteur et scènes indépendantes corrigés | PASS : 944 unitaires + 2 intégration, 0 échec, 31 ignorés |
| `cargo clippy --workspace --all-targets --locked --offline -- -D warnings` | PASS après découpage du probe de layouts |
| `cargo test --workspace --all-targets --locked --offline -j 2` | PASS : 2 355 tests, 0 échec, 56 ignorés, 250 suites |
| Compilation moteur et Studio WebGPU (`cargo check`, cible WASM, feature `web`) | PASS pour les deux composants |
| Clippy strict Studio WebGPU | FAIL : 80 diagnostics, tous dans des fichiers inchangés depuis la base |

Les tests Rust utilisent le cache commun
`E:\OpenRPG_Forge\.gate3-ntfs-cargo-20261002`. La première acquisition de la suite
project dans le bac à sable a donné 91 PASS/11 échecs d'accès filesystem dans les
tests de persistance. Le rejeu avec accès filesystem normal a terminé sans échec ;
aucune assertion ni protection de persistance n'a été affaiblie pour cela.

Le premier passage moteur a donné 943 PASS/31 ignorés, puis 1 PASS/1 échec dans
les scènes indépendantes. Le diagnostic ciblé a confirmé `DiscontinuityFound`
pour le second profil d'exposition. La correction du générateur de segments et
le test supplémentaire des coefficients d'origine passent au rejeu : 944 + 2
tests, sans affaiblir l'assertion sur le second profil.

Le premier Clippy a exposé `too_many_lines` sur la fonction de 225 lignes du
probe de layouts, auparavant incluse depuis l'orchestration. Le code a été
découpé en responsabilités distinctes ; le Clippy workspace passe sans ajouter
de suppression de ce lint.

Les logs détaillés `project-tests-20261017.log` et `engine-tests-20261017.log`
sont des artefacts locaux dans ce dossier, non des preuves de CI distante.
Les acquisitions corrigées sont dans `engine-tests-corrected-20261017.log` et
`clippy-corrected-20261017.log` ; les échecs initiaux restent conservés séparément.
Le [contrôle des inventaires](BUILD_SCOPE_20261017.json) vérifie également que
le candidat historique reste informatif et que le harness produit est épinglé.

Le premier build des tests workspace a été interrompu volontairement après
identification de son processus et de ses enfants : le parallélisme automatique
multipliait les compilateurs/linkers et ralentissait la machine. Les messages de
link interrompu de ce log ne sont pas des échecs d'assertions de tests. Le même
périmètre a terminé avec `-j 2`, sans changer les sources, les tests ou les
profils. Le log de référence de ce rejeu est
`workspace-tests-bounded-20261017.log`.
Le [manifeste des exemples](EXAMPLE_CONSOLIDATION_20261017.json) conserve leurs
hashes d'origine ; l'[inventaire statique](ENGINE_INVENTORY_20261017.json) décrit
le périmètre courant. Les commentaires d'attribution sont inclus dans les comptes.

Le Clippy strict WebGPU de Studio reste en échec : les 80 diagnostics concernent
exclusivement des fichiers identiques à la base Git, vérifiés par hash. Le
[relevé des diagnostics](STUDIO_WASM_LINT_BASELINE_20261017.json) conserve leurs
emplacements et les hashes des fichiers. La compilation WASM normale passe,
mais elle ne remplace pas ce contrôle de lint. Aucun lint n'a été désactivé.
Le détail des commandes et les hashes des logs sont dans le
[reçu de validation](VALIDATION_RESULTS_20261017.json).

## Limites

La qualification de généricité globale reste fausse. Les tests des deux scènes
indépendantes portent sur les entrées CPU, sans exécution de shader ou readback
GPU. Aucun parcours Studio/jeu installé, aucune capture native/WebGPU, aucune
nouvelle mesure de performance, aucune release ni CI distante n'est acquis par
ces corrections. Les anciens rapports gardent leurs candidats et leurs limites.
