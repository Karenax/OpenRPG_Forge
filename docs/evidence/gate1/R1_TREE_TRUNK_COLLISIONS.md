# Gate 1 — raccord physique des arbres procéduraux

2026-09-06, branche Rust locale, sans publication. Gate 1 reste ouverte.

## Correction implémentée

Le scatter protégeait déjà les couches déclarant une collision lors des changements de densité,
mais l'adapter végétation ne matérialisait aucun obstacle Rapier. Préserver leurs placements
logiques n'était donc pas une preuve de collisions actives.

`crates/forge_engine/src/vegetation/collision.rs` projette maintenant les arbres du cache canonique
dont la couche déclare une collision. Forme (capsule/cylindre), rayon, hauteur totale et distance
d'activation proviennent du document existant, pas d'une nouvelle recette. Rayon et hauteur suivent
l'échelle du placement ; la base reste sur sa position terrain. La capsule soustrait le rayon
à la demi-hauteur totale avant conversion Rapier. Une couche sans collision ne crée rien.

La vallée rev3 produit 636 entités physiques statiques distinctes des 1272 entités visuelles
near/medium, sans mesh ni VisibilityRange. Leurs identités et formes ne sont pas recréées par les
transitions de qualité. L'activation suit la distance horizontale au joueur (96 m dans la recette),
avant SyncBackend Rapier ; hors rayon, ColliderDisabled conserve l'entité. Sans joueur unique,
les obstacles restent activés. Aucune source, exclusion, placement manuel ou apparence n'est modifié.

La première implémentation expérimentale utilisait un cylindre aux dimensions du mesh ; elle a
été remplacée avant livraison par le profil canonique. Ses résultats intermédiaires ne qualifient
pas la version finale. Le contrat canonique actuel est plus large que le tronc visuel simplifié :
cette enveloppe et les sensations de déplacement restent à apprécier lors de la revue humaine.

## Vérification

`cargo test --workspace --locked -q` : **399 PASS**, zéro échec/ignoré
(Assets 16, CLI 3+4, Core 11, Engine 155, Project 45, Runtime 61+1, Studio 22+3, World 78).
`cargo clippy --workspace --all-targets --all-features --locked -- -D warnings` : PASS.
`cargo check -p forge_engine --no-default-features --features web --target wasm32-unknown-unknown --locked` : PASS.
Target local habituel `C:/Users/karen/AppData/Local/Temp/openrpg-forge-r1-064-target`,
incremental=0, RUSTFLAGS=`-C debuginfo=0`. Les tests couvrent :

- contrôleur cinématique Rapier réellement arrêté par une capsule issue d'un arbre généré,
  collision identifiée ; passage libre à côté ; collider enregistré dans Rapier ;
- variante cylindre, collision absente, seuil d'activation inclusif, sortie/retour et absence de joueur ;
- 636 projections dès chaque qualité initiale ; identités, positions et dimensions physiques
  préservées pendant High → Low → Medium → Ultra → High et refus d'un profil invalide ;
- hashes de terrain/végétation et bindings visuels toujours conservés par le test existant.

## Exécution native finale

Build `cargo build -p forge_runtime --locked` : PASS. RX 7800 XT/Vulkan, cwd workspace Rust :

```text
RUST_LOG=info
forge_runtime.exe --world-sketch-visual-valley
  --asset-kit-build E:/OpenRPG_Forge/.gate1-evidence-runtime/r1-063-authored-preservation-20260905-b/rebuild-1/asset-kit-build.v1.json
  --asset-kit-preview examples/visual_valley/asset-kits/r1-063/runtime-preview-authored.v1.json
  --bridge-crossing-probe
```

Fin 2026-09-06T03:54:46Z, sortie 0, aucun ERROR, `runtime_errors_observed=false`.
636 colliders d'arbres matérialisés (pas 636 actifs simultanément), kit prêt avec 20 instances
et 36 colliders enregistrés. Traversée : 518/518 ticks au sol, 435 contacts tablier,
42,5282059 m, bindings terrain/joueur/pont conservés, arrêt au sol. Les tests Rapier ciblés
prouvent l'obstacle arbre ; le probe pont prouve ici la non-régression du parcours, pas une
traversée exhaustive de tous les troncs. Avertissements amont `@invariant` conservés.

Racine ignorée de preuve : `E:/OpenRPG_Forge/.gate1-evidence-runtime/r1-tree-collisions-20260906`.
Le premier smoke sortait aussi avec code 0, mais `RUST_LOG=warn` hérité masquait les événements
de résultat (`runtime.log` conservé). Le rejeu informatif ci-dessus fait autorité.

| Artefact conservé | SHA-256 |
|---|---|
| `tests.log` (399 PASS) | `9db878bdb984ed817f234bc27bafe79c58262141307dc9b8caaf51f574a321a6` |
| `runtime-info.log` | `eccce9b63a6cb50e58324dcc0ac819a6e810b33fa4613c402d8efc08812c9122` |
| `forge_runtime.exe` | `56f92b1c434b46148186954bf69ca02adc5c33b93d8caaf31e4d3221dea5de55` |

Formatage et contrôle du diff ciblé : PASS. Aucun benchmark de cadence déduit de ces smokes.

## Limites

Pas de gain visuel ou de performance revendiqué ; aucune capture répétée d'un rendu inchangé.
Pas de remplacement des arbres par le kit GLB, ni de collision sur feuillage/herbe/arbustes.
Pas de qualification manuelle, de nouveau package Tauri ou de preuve interactive Web de ce raccord.
Le check WASM n'est pas une exécution navigateur. Les futurs changements de terrain/scatter
devront réconcilier cette projection physique avec la nouvelle source, dans le travail d'édition.
CI distante NOT RUN sous ADR-0006 ; aucun asset Cadle ajouté.
