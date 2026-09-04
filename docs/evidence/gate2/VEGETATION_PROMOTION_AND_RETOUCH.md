# Gate 2 — promotion et retouche persistantes de végétation

Date : 5 septembre 2026

Branche : `Rust`

Candidat source : `0e365796ca7782ee16b441bfd553c980a35fb060`

Arbre source : `d30e72ab9859c1c37fab0f3c2234f04f08636dc6`

Parent : `725057a2c081beed2b5c8150a2f467485475aeb9`

**R2-020 est `PASS` sur qualification fonctionnelle automatisée de la promotion et de la retouche
persistantes d’une instance de végétation. Gate 2 atteint 19/43 et reste ouverte.** Ce verdict ne
couvre ni le picking et le gizmo génériques de R2-028/029, ni une inspection visuelle native, ni une
mesure de latence, de FPS ou de GPU.

Le manifeste lié aux reçus se trouve dans
[`VEGETATION_PROMOTION_AND_RETOUCH_ARTIFACTS.json`](VEGETATION_PROMOTION_AND_RETOUCH_ARTIFACTS.json).

## Contrat livré

Le viewport propose une brosse dédiée à une seule opération : promouvoir l’instance procédurale la
plus proche, dans la couche choisie et dans une distance bornée. La résolution est déterministe par
distance, état déjà promu, puis identifiant stable. Une instance déjà promue visible au même point
est prioritaire et produit `AlreadyPromoted` au lieu de créer un doublon superposé.

La promotion ajoute une source éditable au document de végétation v2. Elle conserve une provenance
immuable : révision du workspace, algorithme `forge_scatter_v1`, hashes terrain/végétation/densité,
couche, type, transform généré, phase et réponse au vent, pente et poids matière. Le transform
éditable est initialement bit-identique au transform généré. La retouche explicite peut ensuite
modifier position, rotation Y et échelle sans modifier cette provenance.

Chaque promotion ou retouche acceptée produit exactement une révision d’historique. Undo/redo,
sauvegarde atomique et réouverture conservent les identités, la provenance et le transform. Une
instance promue reste une source même si sa tentative procédurale disparaît après modification de
densité, exclusion, terrain, seed ou catalogue ; elle est matérialisée exactement une fois. Les IDs
de couche encore référencés par une promotion orpheline restent réservés dans le Studio.

Le cache reste local :

- promotion initiale bit-identique : 64/64 handles conservés ;
- retouche dans le même chunk : 63/64 handles conservés ;
- déplacement vers un autre chunk : 62/64 handles conservés.

La sortie effective, promotions orphelines comprises, est soumise au plafond public d’expansion de
l’herbe. Un dépassement échoue avant publication et laisse clé, snapshot et handles du cache
intacts. La collection persistante est bornée à 1 024 promotions, triée et sans identifiant dupliqué.

## Compatibilité des sources

Le workspace reste en schéma 3. Le document de végétation passe de la version 1 à la version 2 pour
porter `promoted_instances`. La migration v1 vers v2 est explicite et synthétise une collection vide.
Les sources v1 restent lisibles et se re-sérialisent bit pour bit dans leur ancienne forme. Le fixture
Visual Valley v1 conserve son hash historique
`664df33ffe5ea42ae8e6dc9b30e496abe62f054583f483f8019f9abb6f8d00f7`.

## Preuves fonctionnelles

### Suites ciblées

Commande :

```powershell
cargo test -j 1 -p forge_project -p forge_world -p forge_studio --lib --bin forge-studio --locked --offline
```

Résultat : **305 PASS, 0 échec, 0 ignoré** :

- `forge_project` lib : 103 ;
- `forge_studio` lib : 103 ;
- binaire natif `forge-studio` : 6 ;
- `forge_world` lib : 93.

Les reçus couvrent notamment :

- migration v1, JSON/hash canoniques v2, bornes, tri, unicité et plafond de collection ;
- commande pure, révision, rejeu, inverse, no-op, stale, provenance forgée et suppression implicite ;
- résolution déterministe, promotion initiale bit-identique et rejet d’un chevauchement déjà promu ;
- persistance après disparition de la tentative procédurale et budget Grass réel ;
- cache 64/64, 63/64 et 62/64 ;
- IPC strict, une révision par action, undo/redo, sauvegarde et réouverture ;
- réservation d’un ID de couche appartenant à une promotion orpheline.

### Passe élargie

La commande suivante a été démarrée :

```powershell
cargo test -j 1 --workspace --exclude forge_runtime --locked --offline
```

Elle a été arrêtée avant l’exécution des tests lorsque l’espace libre du volume E: est descendu à
474,4 Mio pendant la reconstruction de `forge_assets`, inchangé par le candidat. Statut exact :
**NOT COMPLETED — protection du volume**, sans échec de test et sans PASS élargi revendiqué. Les
artefacts de compilation récupérables de `forge_engine`, également inchangé, ont ensuite été nettoyés
avec `cargo clean -p forge_engine`, libérant 17,3 Gio. Ce nettoyage ne touche aucune source.

### Compilation et lint du périmètre modifié

- Clippy strict natif de `forge_project`, `forge_world` et `forge_studio` : **PASS** ;
- Clippy strict du Studio WASM : **PASS** ;
- `cargo check` du Studio WASM : **PASS** ;
- `trunk build --locked --offline` : **PASS** ;
- `cargo fmt --all -- --check` et `git diff --check HEAD^ HEAD` : **PASS**.

L’avis de future incompatibilité tiers `proc-macro-error2 2.0.1` est conservé. Aucun build Release
natif ou Trunk Release n’a été relancé pour ce lot.

## Frontières et résultats non exécutés

- UI/inspection visuelle native : **NOT RUN**, selon la décision du propriétaire de privilégier les
  fonctions et de regrouper les contrôles d’interface ;
- picking, gizmo, duplication et suppression génériques : non revendiqués ;
- aperçu temps réel de la retouche : non revendiqué, l’application est explicite ;
- suppression/dépromotion d’une instance promue : non livrée par R2-020 ;
- GPU, PresentMon, latence et qualification performance : **NOT RUN** ;
- CI distante : **NOT RUN — infrastructure billing**, dérogation ADR-0006 ;
- aucune dépendance, aucun manifest Cargo, aucun asset et aucun contenu Cadle modifiés.

R2-C est maintenant complet. La prochaine tranche fonctionnelle est R2-D, à commencer par `R2-021`
création/édition persistante de chemin, sans anticiper les routes, rivières ou placements génériques.
