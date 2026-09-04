# Gate 2 — aperçu pré-validation de la densité de végétation

Date : 5 septembre 2026

Branche : `Rust`

Candidat source : `cb9a8b1e2d2b2e6b85792a508df54925b2026a59`

Arbre source : `ae61bc521e7a95f6b7e2ab23168da370c34cb40c`

Parent : `718eb9882319c71f5395877f785d1dcc110b3951`

**R2-019 est `PASS` sur qualification fonctionnelle automatisée de l’aperçu de densité de
végétation avant validation. Gate 2 atteint 18/43 et reste ouverte.** Ce verdict ne couvre pas
un aperçu pré-validation du relief, des matériaux ou des exclusions, ni une inspection visuelle
native, une mesure de latence/FPS/GPU ou la promotion d’une instance.

Le manifeste lié aux reçus se trouve dans
[`VEGETATION_DENSITY_REALTIME_PREVIEW_ARTIFACTS.json`](VEGETATION_DENSITY_REALTIME_PREVIEW_ARTIFACTS.json).

## Contrat livré

Le chemin de densité existant reste la seule source canonique. Pendant le glissement, le Studio
envoie une trajectoire candidate typée au service natif ; celui-ci clone le workspace accepté sous
verrou, puis calcule la densité et la projection hors du verrou. Cette opération ne modifie ni le
document de session, ni l’historique, ni l’état dirty, ni le disque.

La coordination asynchrone est bornée :

- intervalle logique de 125 ms, soit huit opportunités d’envoi par seconde ;
- une requête native active et une seule trajectoire en attente ;
- remplacement de l’attente par la trajectoire la plus récente ;
- priorité au point final, qu’un mouvement tardif ne peut plus remplacer ;
- réponse obsolète, annulée ou appartenant à une autre zone/révision jamais présentée ;
- erreur d’une réponse déjà supplantée non fatale pour le point final en attente.

Le candidat changé porte `base_revision + 1`; un no-op conserve `base_revision`. Le navigateur
valide la requête, la série, la révision, les hashes terrain/végétation/densité et la complétude des
64 chunks avant de publier la projection. Le trait final n’envoie l’unique commande persistante
qu’après observation, par le renderer, de la génération, du projet, de la zone, de la révision et du
hash de densité exacts, avec 64 chunks, aucun chunk en attente et aucune erreur. Annulation,
`pointercancel`, changement de zone/révision ou erreur restaurent la projection canonique.

## Preuves fonctionnelles

### Suites ciblées

Commande :

```powershell
cargo test -j 1 -p forge_project -p forge_world -p forge_studio --lib --bin forge-studio --locked --offline
```

Résultat : **285 PASS, 0 échec, 0 ignoré** :

- `forge_project` lib : 94 ;
- `forge_studio` lib : 100 ;
- binaire natif `forge-studio` : 6 ;
- `forge_world` lib : 85.

Les reçus couvrent notamment :

- enveloppes IPC strictes et liaison série/révision/hash/projection du service natif ;
- égalité du candidat avec l’application canonique et immutabilité exacte de la session ;
- rejets stale, mauvais projet/zone/révision/série et entrées invalides sans mutation ;
- succession de candidats depuis la même base et no-op explicite ;
- cache `canonique -> candidat A -> candidat B -> no-op -> canonique`, avec parité génération
  complète et conservation des handles attendus ;
- rafale latest-wins, cadence, priorité finale, annulation, réponse tardive, cible changée,
  ticket forgé et erreur d’un aperçu supplanté.

### Passe élargie

Commande :

```powershell
cargo test -j 1 --workspace --all-targets --exclude forge_runtime --locked --offline
```

Résultat : **476 PASS, 0 échec, 0 ignoré sur 10 cibles**. `forge_runtime`, inchangé par les dix
fichiers du candidat, est exclu pour ne pas relancer le linkage PDB qui avait saturé le volume E:
lors du lot précédent. Ce résultat n’est pas présenté comme une passe workspace complète.

### Compilation et lint

- `cargo clippy -j 1 -p forge_project -p forge_world -p forge_studio --lib --all-targets
  --locked --offline -- -D warnings` : **PASS** ;
- Clippy strict du Studio WASM : **PASS** ;
- `cargo check` du Studio WASM : **PASS** ;
- `trunk build --locked --offline` : **PASS** ;
- `cargo fmt --all -- --check` et `git diff --check` : **PASS**.

Le build Trunk Release a terminé sa compilation Rust, puis l’étape d’assets a refusé de télécharger
`wasm-opt`, absent localement, à cause du mode offline : **NOT COMPLETED**, jamais transformé en
PASS. L’avis de future incompatibilité tiers `proc-macro-error2 2.0.1` est conservé. Le build Release
natif n’a pas été relancé.

## Frontières et résultats non exécutés

- UI/inspection visuelle native : **NOT RUN**, à la demande du propriétaire de privilégier les
  fonctions et de regrouper les vérifications d’interface ;
- cadence 125 ms : borne logique uniquement, aucune latence, aucun FPS et aucune performance mesurés ;
- GPU, PresentMon et qualification performance : **NOT RUN** ;
- CI distante : **NOT RUN — infrastructure billing**, dérogation ADR-0006 ;
- aperçu pré-validation terrain, matériaux et exclusions : non revendiqué ;
- `R2-020` promotion/retouche persistante : reste ouverte ;
- aucune dépendance, aucun manifest Cargo, aucun asset, aucun contenu Cadle et aucun changement de
  schéma.

La prochaine tranche fonctionnelle est `R2-020`. Elle doit promouvoir puis retoucher une instance
générée de végétation de façon persistante, annulable et réouvrable, sans anticiper le picking ou le
gizmo génériques de `R2-028/029`.
