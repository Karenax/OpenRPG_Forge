# forge_world

Produit de façon déterministe la scène de preuve Gate 0 (sol, cube, lumière et caméra) depuis une
recette versionnée. La sortie est un `SceneProofDocument` moteur-neutre ; aucun mesh ou asset externe
n'est stocké dans la recette.

Pour `R1-001`, la crate produit aussi la fixture originale
`examples/visual_valley/terrain.v1.json` depuis `VisualValleyTerrainRecipe`. Le document persistant
reste défini par `forge_project::TerrainDocument` ; `forge_world` ne possède pas une seconde source de
vérité.

`TerrainGenerationPlan` interprète ensuite ce document avec une sémantique pure et versionnée :

- origine centrée `[-256, +256]` sur X et Z, mètres, Y vertical, système droitier ;
- `FractalValueNoiseV1` = valeurs de lattice SplitMix64 signées, interpolation quintique, somme
  d'octaves normalisée, calculs `f64` puis une conversion `f32` ;
- grille unique de `257 × 257` échantillons, découpée en `8 × 8` chunks de `32 × 32` intervalles et
  `33 × 33` sommets ;
- positions globales, UV globaux, normales calculées depuis la source globale et coutures identiques ;
- indices et triangles collider en ordre explicite, avec winding `+Y` ;
- trois poids logiques herbe/terre/roche finis et normalisés ;
- plan de sélection partielle qui expose le rectangle de dépendance et retient exactement l'union en
  croix requise par les normales.

Ces données sont des caches dérivés sans Bevy, wgpu ou Rapier. Le rendu PBR et la création des
colliders moteur restent la responsabilité des adapters. Le module ne contient aucun asset ni code
Cadle. Le plan sale ne régénère et n'applique pas encore les chunks ; il ne qualifie donc pas à lui
seul `R1-007`.

La fixture de végétation est produite par `VisualValleyVegetationRecipe`. Ses quatre probabilités
de placement (arbres 0,20 ; arbustes 0,36 ; fougères 0,42 ; herbe 0,80) renforcent la couverture sans
changer seed, grille, tentatives, filtres, espacements ni exclusions. La nouvelle fixture initiale
reste en révision partagée 1 ; son hash et son commit distinguent ce contenu des preuves antérieures.
Les anciens placements d'herbe sont tous conservés. Pour les autres couches, de nouveaux candidats
peuvent remplacer des anciens dans l'ordre glouton d'espacement ; les instances survivantes restent
bit-identiques. Les tests épinglent la recette antérieure, les nouveaux comptes et cette distinction.

Validation ciblée :

```text
cargo fmt --package forge_world -- --check
cargo test -p forge_world --locked
cargo clippy -p forge_world --all-targets --locked -- -D warnings
```
