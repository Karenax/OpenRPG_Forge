# R1-040 — Densité végétale de présentation, fondation pure

2026-09-06 local, Rust, non publié. **Gate 1 et R1-040 restent ouverts.**

## Implémentation

`VegetationGenerationPlan::generate_render_density` dérive une sélection de présentation
sans modifier `generate()`, son cache canonique, les sources de terrain/végétation ni leurs hashes.
Le résultat distinct `VegetationRenderDensityProjection` rapporte densité demandée, placements
canoniques et placements protégés. Ce n'est pas une nouvelle recette ou une mutation de gameplay.
Valeurs non finies ou hors 0,1..2 refusées par erreur typée.

- Densité 1 : copie exacte du cache canonique, mêmes identités, ordre, compteurs et hash dérivé.
- Densité inférieure : sélection déterministe par rang de l'identité, sans déplacer les survivants.
- Densité supérieure : ajout depuis les tentatives bornées de la recette, avec seuil d'acceptation
  multiplié. Tous les placements canoniques sont conservés. Les contraintes de matériau, hauteur,
  pente et exclusions utilisent le même chemin de génération. L'espacement est initialisé avec
  les placements existants ; les candidats supplémentaires sont traités par rang stable croissant.
  Augmenter la densité ne remplace donc pas les éléments déjà retenus par des candidats plus tôt
  dans l'ordre spatial. Aucun nouvel ensemble illimité de tentatives ou clone de recette modifiée.
- Les couches déclarant une collision sont protégées à toutes les densités : ni suppression
  graphique d'obstacles existants ni ajout d'obstacles visuels sans collision correspondante.

Le multiplicateur porte sur la décoration et ses probabilités, pas sur un nombre exact d'objets :
espacement, exclusions, saturation des probabilités et protection des collisions affectent le total.
Le contrat physique doit consommer uniquement le cache canonique. Ce lot de sélection pure ne
matérialisait pas de colliders Rapier pour les arbres ; ce raccord est réalisé ultérieurement dans
`R1_TREE_TRUNK_COLLISIONS.md`. Le tri et la sélection
sont destinés à un changement de qualité, pas à une exécution par frame.

## Preuve automatisée sur WorldSketch rev3

Le test compile la vallée réelle et utilise son terrain sculpté. Il parcourt six densités,
vérifie l'inclusion exacte de chaque sélection dans la suivante (tous champs de chaque instance),
la conservation des placements avec collision, les hashes source inchangés, l'identité exacte
à densité 1, le rejeu déterministe de chaque densité et le cache canonique intact après l'ensemble.
NaN, infini, valeurs négatives, zéro et 2,1 sont refusés.

| Densité | Placements de présentation | Placements protégés |
| --- | ---: | ---: |
| 0,1 | 1296 | 636 |
| 0,5 | 4043 | 636 |
| 0,75 | 5689 | 636 |
| 1 | 7433 | 636 |
| 1,25 | 9110 | 636 |
| 2 | 14032 | 636 |

Ces nombres sont logiques : pas des entités Bevy, triangles, colliders actifs ou draw calls GPU.
Le test ciblé passe ; SHA-256 du module et test :
`75c468e90d89c665a8d70019fa577f8cae7c0378749b8f5eb9488572b444285a`.
`cargo test --workspace --locked -q` : **374 PASS** ; Clippy workspace tous targets/features
avec warnings en erreurs : PASS. Target isolé habituel, incremental=0, `RUSTFLAGS=-C debuginfo=0`.
Check `forge_world` vers wasm32, formatage et diff : PASS.

## Non réalisé par ce lot

Le renderer ne consomme pas encore cette API : la scène affichée reste inchangée. Restent le
raccord aux meshes grass/HLOD et aux instances, les distances LOD, la conservation des bindings
lors des changements de qualité, les budgets de brins, la CLI/Studio et la preuve native.
Aucun gain de performance ou résultat visuel n'est revendiqué. Pas de capture inutile d'un rendu
inchangé. CI NOT RUN, aucune dépendance ajoutée, aucun asset Cadle ou publication Git.

Suite : le raccord initial renderer/LOD/brins et la CLI sont maintenant réalisés et vérifiés
séparément dans [R1_040_VEGETATION_INITIAL_QUALITY.md](R1_040_VEGETATION_INITIAL_QUALITY.md).
Le paragraphe précédent décrit la portée historique de ce premier lot, pas l'état actuel.
