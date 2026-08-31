# Gate 1 — Preuve R1-009 scatter de végétation déterministe

Date de validation locale : **2026-08-31**

Branche : **`Rust`**

Commit code qualifié : **`0a1c5655428938b8d47afa3bc024d20669e41657`**

Tree Git : **`b4045375162519e66424af2331de59b5cc04203b`**

Verdict du lot : **PASS AUTOMATISÉ LOCAL — R1-009 uniquement**

Gate 1 reste **OUVERTE**. Cette preuve qualifie le document source et le cache logique de scatter ;
elle ne qualifie ni les instances GPU `R1-010`, ni les LOD/HLOD, ni l'herbe rendue, le vent,
l'interaction, le culling, les ombres ou les métriques `R1-011` à `R1-018`.

## Résultat livré

- `forge_project` introduit le schéma strict `forge.vegetation` version 1, moteur-neutre et sans type
  Bevy, GPU ou physique.
- La recette Visual Valley fixe une surface de **512 × 512 m**, **64 chunks** de 64 m et une grille
  globale de **64 × 64 cellules**, parcourue dans un ordre stable.
- Le scatter est reproductible à seed et sources identiques. Les identifiants d'instances dépendent
  du projet, de la zone, des seeds terrain/végétation, de la couche, de la cellule et de la tentative,
  sans hasard implicite.
- L'échantillonnage de surface reprend exactement les deux triangles du mesh terrain et produit
  hauteur, normale, gradient de pente et poids des trois matières.
- Les filtres associent un rôle typé `grass`/`earth`/`rock` à l'identifiant de matière attendu ; un
  ordre ou un identifiant divergent est refusé au lieu de placer silencieusement sur la mauvaise
  couche.
- Les contraintes d'altitude, pente, matière, exclusions circulaire/corridor et espacement minimum
  sont appliquées avant insertion. L'espacement inter-chunks utilise des buckets déterministes.
- Le cache final est ordonné et hashé ; un changement de seed modifie les IDs et l'empreinte, tandis
  qu'une régénération identique reproduit les mêmes résultats.

Le profil de collision des arbres est seulement une intention persistante et bornée ; aucun collider
physique d'arbre n'est matérialisé par ce lot.

## Empreintes et golden Visual Valley

| Objet | Valeur qualifiée |
|---|---:|
| SHA-256 du contenu canonique `VegetationDocument` | `5c7619a6ee04533accbf998e3c12077363e4e3b06c86d28e9a62b76bae80a5b8` |
| SHA-256 du cache de scatter généré | `82995caa4f17c315cdc42542b255e50945dc70b2e3c959a97b818ebbd60ceb98` |
| Arbres | 704 |
| Arbustes | 1 228 |
| Fougères | 1 282 |
| Patches d'herbe logiques | 5 068 |
| Total des placements logiques | **8 282** |

Le golden lie la fixture `examples/visual_valley/vegetation.v1.json`, les sources terrain et le cache
généré. Ces nombres ne sont ni un compteur d'instances GPU visibles, ni un compteur de draw calls.

## Bornes de travail et d'entrée

| Borne | Limite | Valeur Visual Valley |
|---|---:|---:|
| JSON source | 1 048 576 octets | sous la limite |
| couches logiques | 16 | 4 |
| exclusions | 32 | 2 |
| tentatives par cellule et couche | 8 | 2 / 2 / 2 / 4 |
| tentatives de placement cumulées | 131 072 | 40 960 |
| brins d'herbe dérivables | 1 048 576 | 524 288 au pire d'après la recette |
| brins par patch | 512 | 32 |

Les 5 068 patches acceptés représenteraient **162 176 brins** si la présentation les déployait
tous. Cette expansion n'est pas réalisée ni qualifiée par `R1-009`.

## Validation automatisée locale

Les validations ont été exécutées séquentiellement avec un seul job Cargo afin de borner la pression
mémoire.

| Contrôle | Résultat |
|---|---:|
| `cargo test -j1 -p forge_project -p forge_world -- --nocapture --test-threads=1` | **58/58 PASS** : `forge_project` 21/21, `forge_world` 37/37 |
| `cargo clippy -j1 -p forge_project -p forge_world --all-targets -- -D warnings` | PASS |
| `cargo check -j1 -p forge_project -p forge_world --target wasm32-unknown-unknown` | PASS compilation WASM |
| `cargo fmt --all -- --check` | PASS |
| `git diff --check` sur le lot | PASS |
| `cargo deny check` | PASS : advisories, bans, licences et sources acceptés ; avertissements de doublons hérités |
| `cargo audit --no-fetch` | PASS, code de sortie 0 ; 20 avertissements autorisés hérités, aucun blocage |

Les tests couvrent le round-trip strict, les entrées invalides, les budgets, les coutures, les deux
triangles du mesh, les cas numériques extrêmes, les filtres de matière, les exclusions, l'espacement
inter-chunks, la stabilité des IDs et les deux empreintes golden. Le check WASM prouve uniquement la
compilation de `forge_project` et `forge_world` pour cette cible, pas une parité de rendu native/WebGPU.

Le lot n'ajoute ni dépendance, ni changement de manifest Cargo, ni changement de `Cargo.lock`. Les
avertissements de doublons `cargo deny` et les 20 avertissements autorisés `cargo audit` appartiennent
au graphe hérité ; ils ne sont pas présentés comme absents.

## Provenance et indépendance Cadle

Les neuf fichiers du commit ont été inspectés : aucun asset, nom, lore, texte, code, dépendance ou
référence d'exécution Cadle n'a été introduit. La fixture est une recette JSON Forge originale et
les placements sont entièrement dérivés des documents terrain/végétation. Aucun asset Cadle n'est
copié dans Forge.

Le code ne dépend pas non plus de Three.js, Godot, Unity ou Unreal. Le clone Cadle reste une référence
locale externe et n'intervient dans aucun test ou golden `R1-009`.

## CI distante — exception ADR-0006

Le push du commit code a déclenché le run GitHub Actions
[`33399841860`](https://github.com/Karenax/OpenRPG_Forge/actions/runs/33399841860), qui cible exactement
`0a1c5655428938b8d47afa3bc024d20669e41657`. Ses quatre jobs ont échoué avant toute étape :

| Job | ID | Étapes exécutées | Classification |
|---|---:|---:|---|
| `supply-chain` | `99513170808` | 0 | `NOT RUN — infrastructure billing` |
| `native (windows-2025)` | `99513171081` | 0 | `NOT RUN — infrastructure billing` |
| `native (ubuntu-24.04)` | `99513171104` | 0 | `NOT RUN — infrastructure billing` |
| `wasm` | `99513171250` | 0 | `NOT RUN — infrastructure billing` |

Conformément à ADR-0006 et à la dérogation explicite jusqu'à Gate 4, l'absence de CI distante ne
bloque pas le verdict local de `R1-009`. Elle n'est ni une CI verte ni une exécution de tests distante.

## Limites et suite obligatoire

- Aucune scène de végétation n'a été rendue, capturée ou inspectée : il n'existe donc aucune preuve
  visuelle pour ce lot.
- Les collisions physiques des arbres sont différées ; seul leur contrat persistant est présent.
- `generate_chunk()` initialise aujourd'hui le cache global puis clone le chunk demandé. Cette limite
  P2 est bornée par les budgets et préserve les coutures, mais devra être revisitée pour une vraie
  reconstruction partielle.
- La compilation WASM ne prouve ni le lancement WebGPU, ni la parité visuelle ou fonctionnelle.
- La revue humaine comparative Gate 1 et le benchmark 1080p High sur RTX 3060/RX 6600 n'ont pas été
  réalisés et ne sont pas couverts par la dérogation CI.
- `R1-010` à `R1-018` restent ouverts jusqu'à leurs preuves de rendu, capture, inspection, culling,
  ombres, interaction et métriques.
