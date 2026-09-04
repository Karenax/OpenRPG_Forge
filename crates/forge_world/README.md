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
Cadle. `apply_partial_update` prépare maintenant les remplacements de chunks depuis le diff exact
des deltas, et `apply_to_cache` les applique après revalidation du cache. Cette capacité sur les
données dérivées ne prouve pas à elle seule son raccordement à un viewport d'édition.

## Commandes de brosses de hauteur — Gate 2

`preview_terrain_brush(&WorkspaceDocument, TerrainBrushCommand)` est le point d'entrée pur et
moteur-neutre pour lever/abaisser, lisser, aplatir, ajouter du bruit et former une pente. Le contrat de
brosse v1 utilise une empreinte circulaire avec atténuation smoothstep, un rayon de 2 à 64 m et une
intensité dans ]0,1]. Le lissage lit seulement les neuf voisins de la source initiale ; la pente vise
un plan en hauteur absolue et gradients rise/run. Le bruit SplitMix64 est explicite et déterministe,
avec seed u32 transportable exactement par le Studio. Aucun hasard implicite ni donnée Cadle.

La demande porte CommandId, révision et identités projet/module/zone. Le résultat contient l'intention,
le nombre de hauteurs modifiées et un `WorkspaceEditResult` validé : source candidate, patch résolu,
hashes avant/après et snapshot inverse. Le document emprunté est intact ; les deltas restent triés,
sans doublons ni valeurs nulles. Une hauteur produite hors ±1024 m, une entrée non finie, une cible
étrangère, une révision périmée, un no-op ou un dépassement du budget source est refusé.

Les tests comparent chaque mode à une génération complète après remplacement partiel, sur une
empreinte traversant quatre chunks ; le chunk témoin non affecté reste identique. Le contrat partiel
actuel exige encore une révision terrain suivante exacte : l'intégration au viewport doit traiter
explicitement les sauts de révision workspace et l'undo/redo, sans falsifier les hashes.
L'IPC natif et la persistance sont testés, mais les outils visibles, le picking, les traits continus et
le recalcul partiel du renderer restent à raccorder. Aucun PASS R2-007..014 n'en découle.

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
