# Gate 1 — Preuve R1-066 terrain sculpté, collision et audit de praticabilité

Date de validation locale : **2026-09-01**  
Branche : **`Rust`**  
Statut Git : **arbre de travail local non publié**

## Verdict borné

**PASS LOCAL POUR R1-066.** Le relief issu du `WorldSketch` est maintenant matérialisé dans un vrai
`forge.terrain` v2 à deltas clairsemés. Le générateur Forge reconstruit ses 64 chunks et l'adapter
physique existant produit les 64 heightfields Rapier. Un rayon Rapier lancé sur un point déformé de
plus d'un mètre retrouve la même hauteur que l'échantillonnage de surface Forge.

La compilation produit aussi `forge.world-traversal-audit` v1. Cet audit mesure les pentes des faces
réelles du terrain, applique la limite de 45° du contrôleur joueur, bloque l'eau, étiquette les
composantes terrestres, vérifie la route et détecte le croisement `road_main` / `river_main` associé à
`bridge_south`.

Ce verdict ne qualifie **ni un navmesh, ni un pathfinder, ni le franchissement du pont**. Le pont
Blender reste une prévisualisation sans contrat de placement/collider runtime et le fournisseur de
navigation est volontairement `not_selected` jusqu'à son benchmark.

## Contrats matérialisés

- source éditable : `forge.world-sketch` v1, hash canonique
  `46f1d766a9dc35af75d172a7d1700af95dda397ba956ceba0e23318df49f3839` ;
- terrain source inchangé : `forge.terrain` v1, hash canonique
  `9f16780a1de1e9dd4b65931624a8b45281713829434292bada320a578420b2ef` ;
- terrain sculpté : `forge.terrain` v2, hash canonique
  `f47294def69f86f2d3c7e1fd52c522cab0b1a595e6e292bb7af724742e4ddb16` ;
- audit : `forge.world-traversal-audit` v1, hash canonique
  `527636b9c62dd66fac6241da1687413c679be9b1b2d9dd23534aeb8c7e332aef` ;
- entrée Blender : `forge.world-sketch-blender-input` v3, hash canonique
  `798ae9c542a970b7e35b92215de825350a288c007a01d5336b93ca6236ee54fe`.

Le compilateur conserve le terrain procédural v1 au lieu de le modifier silencieusement. Il dérive
`terrain-carved.v2.json`, puis régénère la grille utilisée par le carving et Blender depuis ce nouveau
document. Les 8 722 deltas sont triés `(sample_z, sample_x)`, bornés à la grille `257 × 257` et restent
sous la limite contractuelle de 65 536 entrées.

## Mesures de praticabilité

| Mesure | Valeur observée |
|---|---:|
| grille | 257 × 257, pas 2 m |
| deltas de hauteur | 8 722 |
| pente joueur maximale | 45° |
| pente maximale observée sur les triangles | 66,38218° |
| échantillons pente franchissable | 65 100 |
| échantillons bloqués par pente | 949 |
| échantillons eau bloquée | 2 103 |
| échantillons terre franchissable | 62 997 |
| composantes terrestres | 1 |
| taille de la composante principale | 62 997 |
| échantillons de route analysés | 61 |
| échantillons de route bloqués par l'eau | 1 |
| route continue sans structure | non |
| cellules de croisement route/rivière | 32 |
| centre du croisement | (−59,375 m ; −169,5625 m) |
| cellules de l'emprise de pont candidate | 109 |

Les extrémités de la route appartiennent à la même grande composante terrestre parce qu'un détour est
possible à l'échelle de la carte. Cela ne transforme pas la route elle-même en franchissement : son
profil central est discontinu au croisement et l'état émis est `bridge_collider_required`.

## Preuve physique Rapier

Le test
`physics::terrain::tests::world_sketch_carved_candidate_reaches_all_64_rapier_heightfields_and_raycast`
effectue la chaîne suivante :

1. compile le `WorldSketch` embarqué ;
2. vérifie que le nombre de deltas v2 égale le nombre d'échantillons sculptés ;
3. reconstruit les 64 chunks et les 64 payloads heightfield Rapier ;
4. vérifie que toutes les hauteurs sont finies ;
5. sélectionne la plus forte déformation située hors d'une couture de chunk ;
6. confirme une différence de plus d'un mètre avec le terrain source ;
7. lance un rayon vertical sur le heightfield Rapier correspondant ;
8. exige un écart d'au plus `0,001 m` entre l'impact et la surface Forge.

Résultat observé : **1/1 PASS**. Les tests historiques de coutures, winding et ordre des triangles
restent exécutés dans la suite complète de `forge_engine`.

## Reproductibilité des documents

Trois compilations dans des dossiers neufs ont produit les mêmes octets :

| Fichier | SHA-256 brut | hashes uniques sur 3 runs |
|---|---|---:|
| `terrain.v1.json` | `38a59c26bfc2aee5d099efb2965ec8c538bfc9a850618a2e9e61d3f713f4994f` | 1 |
| `terrain-carved.v2.json` | `49f2565670afa4c5fdb782bc88cce3ce161ee1d96a3f1f2cac4a37652400c3cc` | 1 |
| `vegetation.v1.json` | `3e75bd5218fe1c14e9fdd287774cfd2f585c0b0cb2ca844691840b9281ff0d5b` | 1 |
| `semantic-masks.v1.json` | `481c520eb8e0a93d6bd0f896650f7223bb3e01c928cda004a8c78164709ccbb1` | 1 |
| `spline-carving.v1.json` | `db0dc0eec4b605dd1da4ce8dcd61b70081d0cbcc9caaf1dca8bbbfd1dfe9ffd9` | 1 |
| `traversal-audit.v1.json` | `52333bc2d5b6e4517d99205e401ac75111cf446160d86686b5583df9f4dd90db` | 1 |
| `blender-input.v3.json` | `3ce459c40e6e869d3203c3c576bab421d3211307d088b998b1833d0838876430` | 1 |
| `world-sketch.compilation.v3.json` | `84e249e0d30f44c24bee39221d59842a3c39b164dc6e175e0d1f609c716d4e4a` | 1 |

## Preuve Blender et inspection visuelle

Le worker Blender 4.5.10 LTS version 6 a été reconstruit deux fois en `1920 × 1080`, pas terrain
2 m. Il ajoute un troisième atlas composé de : pente 0–90°, pente franchissable, eau bloquée, terre
franchissable et emprise du collider de pont candidat.

- GLB identique octet par octet :
  `b723830d1cb76d38cd059ea6e34651422f6e9d97efb247f80b89fbbdd2bbc05a` ;
- atlas de praticabilité identique octet par octet :
  `684efab8739ea2e075f61ed3e581e13affc59005d443730d5617ef2b0f77120c` ;
- image héroïque, quatre caméras et trois atlas : pixels RGBA strictement identiques entre les deux
  runs.

L'atlas et les captures `human_bridge` / `valley_overview` ont été inspectés à leur résolution
originale. La rivière bloquée, les bandes non franchissables, la route et la petite emprise jaune du
pont sont distinctes. Cette inspection qualifie la lisibilité de l'audit, pas la qualité artistique
AAA du greybox.

## Gates de code

| Contrôle | Résultat |
|---|---:|
| `cargo fmt --all -- --check` | **PASS** |
| Clippy paquets modifiés, toutes cibles, `-D warnings` | **PASS** |
| tests `forge_world` | **57/57 PASS** |
| tests unitaires + commandes `forge_cli` | **7/7 PASS** |
| tests `forge_engine` | **89/89 PASS** |
| syntaxe du worker Python | **PASS** |

## Artefacts locaux hors Git

```text
E:\OpenRPG_Forge\.gate1-evidence-runtime\world-sketch-v3-collision-traversal-20260901-103033\
E:\OpenRPG_Forge\.gate1-evidence-runtime\world-sketch-v3-collision-traversal-20260901-103033-run2\
E:\OpenRPG_Forge\.gate1-evidence-runtime\world-sketch-v3-collision-traversal-20260901-103033-run3\
E:\OpenRPG_Forge\.gate1-evidence-runtime\world-sketch-v3-collision-traversal-20260901-103033-blender-run1\
E:\OpenRPG_Forge\.gate1-evidence-runtime\world-sketch-v3-collision-traversal-20260901-103033-blender-run2\
```

## Suite bloquée explicitement

Le prochain incrément gameplay doit matérialiser un pont gouverné dans Forge, créer son collider,
lancer le contrôleur Rapier sur les deux rives et prouver le franchissement. Le choix du fournisseur
de navigation vient ensuite, après benchmark sur cette carte et sans construire un moteur de
navigation maison par défaut. `R1-063` reste parallèlement le prochain lot artistique : premier kit
gouverné rochers/arbres/architecture avec retopologie, UV/PBR, LOD, collisions et turntables.
