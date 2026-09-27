# Gate 1 — Preuve R1-068 benchmark du fournisseur de navigation

Date de validation locale : **2026-09-01**  
Branche : **`Rust`**  
Statut Git : **arbre de travail local non publié**

## Verdict borné

**PASS DU BENCHMARK, NO-GO POUR L'INTÉGRATION.** L'outil reproductible couvre le terrain sculpté,
le franchissement réel, l'intérieur, les portes, deux profils d'agent, les coûts, un lien hors
maillage et l'évitement local. La pile Rerecast + Landmass réussit tous les probes locaux et compile
pour Windows comme pour `wasm32-unknown-unknown`.

Elle n'est pas sélectionnée : la sortie de vallée complète est rejetée brute par Landmass, Rerecast
ne fournit ni tuiles, ni reconstruction partielle, ni carving d'obstacle dynamique ; le backend
physique maintenu cible Avian au lieu de Rapier et `cargo-deny` bloque `bincode 2.0.1` via
`RUSTSEC-2025-0141`.

## Artefact de référence hors Git

```text
E:\OpenRPG_Forge\.gate1-evidence-runtime\navigation-benchmark-r1-068-isolated-final-20260901-190000\navigation-provider-benchmark.v1.json
SHA-256 : 208ab622a9bab445135c528a9f6356afd1a8b0b29605f4e10cb0ab8c8dd3bf19
```

Sources canoniques consommées :

| Document | SHA-256 canonique |
|---|---|
| `world-sketch.v1.json` | `46f1d766a9dc35af75d172a7d1700af95dda397ba956ceba0e23318df49f3839` |
| terrain sculpté | `f47294def69f86f2d3c7e1fd52c522cab0b1a595e6e292bb7af724742e4ddb16` |

## Extérieur réel

| Mesure | Crop 96 m | Vallée 512 m |
|---|---:|---:|
| triangles source | 3 910 | 74 337 |
| polygones produits | 708 | 2 834 |
| triangles de détail | 2 474 | 17 203 |
| temps des 3 générations | 133,087 / 126,419 / 134,348 ms | 5 216,383 / 5 527,974 / 4 947,123 ms |
| hashes uniques sur 3 runs | 1 | 1 |
| chemin rive à rive avec pont | oui | sortie brute invalide |
| chemin rive à rive sans pont | non | non applicable |

Hash fournisseur global commun aux trois runs :
`242a476eed7f14483bf2c8626a9008a5cf3e161cca77b76250ee804c16d3078d`.

La conversion globale brute échoue de deux manières cohérentes :

- avec le height mesh : `The triangle at index 2819 in the height mesh is clockwise instead of
  counter-clockwise` ;
- sans le height mesh : `The polygon at index 2819 is concave or has edges in clockwise order`.

Le diagnostic trouve un seul polygone à aire XZ négative, d'aire signée `-12,647827 m²`. Une copie
normalisée doit inverser ce polygone et 18 triangles de hauteur ; le chemin est alors trouvé en deux
waypoints. Cette copie n'est pas acceptée comme sortie de production et reste visible dans le rapport
avec `winding_normalization_accepted=false`.

## Probes fonctionnels

| Contrat | Résultat |
|---|---:|
| porte ouverte | chemin trouvé |
| porte fermée | aucun chemin |
| porte réouverte | chemin trouvé, hash identique à l'ouverture initiale |
| reconstruction partielle de porte | absente ; régénération complète mesurée à 0,848 ms |
| agent rayon 0,25 m dans ouverture 1,2 m | chemin trouvé |
| agent rayon 0,65 m dans ouverture 1,2 m | aucun chemin |
| lien hors maillage | aucun chemin avant, chemin avec 1 lien après |
| coût de zone | corridor peu coûteux choisi |
| évitement local | deux cibles atteintes en 207 ticks |
| séparation minimale des agents | 0,7000608 m pour deux rayons de 0,35 m |
| vitesse latérale maximale | 0,34995383 m/s |

`all_non_global_feature_probes_passed=true` mais
`all_required_contract_probes_passed=false`.

## Gates de code et plateforme

| Contrôle | Résultat |
|---|---:|
| format workspace produit + manifeste benchmark | **PASS** |
| Clippy benchmark via `--manifest-path`, toutes cibles, `-D warnings` | **PASS** |
| tests benchmark via `--manifest-path` | **5/5 PASS** |
| `cargo test -p forge_world --locked` | **57/57 PASS** |
| check benchmark natif via `--manifest-path --locked` | **PASS** |
| check benchmark `wasm32-unknown-unknown` via `--manifest-path --locked` | **PASS** |
| `cargo check --workspace --all-targets --locked` | **PASS** |
| `cargo deny check` sur le workspace produit | **PASS, code 0** |
| licences `cargo-deny` | **PASS** |
| bans et sources `cargo-deny` | **PASS** |
| advisories `cargo-deny` | **FAIL : RUSTSEC-2025-0141** |

Le premier check WASM a correctement refusé l'absence de backend de hasard. La dépendance directe
ciblée `getrandom = 0.3.4` avec feature `wasm_js` a ensuite permis deux checks WASM successifs, sans
configuration globale du compilateur.

Le laboratoire est exclu du workspace produit et possède son propre `Cargo.lock` :
`cargo metadata` confirme que `forge_navigation_benchmark` n'est pas un membre produit, le lockfile
racine ne contient pas `bincode`, et seul le lockfile du laboratoire le contient. Le gate produit
reste donc vert sans masquer l'advisory du candidat.

## Maintenance amont

Les recherches GitHub effectuées le 1er septembre 2026 sur `rerecast clockwise`, `rerecast winding`
et `rerecast concave` ne trouvent aucun ticket correspondant dans les dépôts Rerecast ou Landmass.
Aucun ticket, message ou pull request externe n'a été créé : cela demanderait une autorisation de
publication distincte.

## Limites

- compilation WASM prouvée, mais aucune exécution navigateur de la pile candidate ;
- génération synchrone dans le benchmark pour mesurer précisément, alors que l'adapter Bevy amont
  utilise une tâche de fond ;
- aucun obstacle dynamique fournisseur faute de reconstruction partielle ;
- aucun test de streaming par tuiles puisque la fonction est absente ;
- aucun code candidat introduit dans le runtime ;
- aucune qualification de carte jouable complète ni de qualité artistique AAA.

## Suite explicite

Conserver la pile en laboratoire. La prochaine décision doit choisir entre contribuer une correction
amont bornée à la conversion globale, benchmarker un adapter de tuilage strictement provider-backed,
ou qualifier une autre pile. L'intégration runtime reste interdite tant que l'ADR-0009 n'est pas
acceptée et que les blocages ne sont pas levés.
