# Gate 1 — Preuve R1-001 TerrainDocument v1

Date : 2026-08-31
Branche : `Rust`
Commit de code qualifié : `9bcdc02cc99cb918f94a8c952ecf461c7da8aca0`
Arbre Git : `f54417010da74eb556bd0ae0174bf804b78ae4f6`
Verdict : **PASS LOCAL — R1-001 uniquement**

## Portée acquise

Le lot introduit un document terrain persistant et moteur-neutre dans `forge_project`, puis une
recette Visual Valley originale dans `forge_world`. La fixture fixe :

- le schéma `forge.terrain` version 1 ;
- une surface de `512 × 512 m` en mètres, Y-up, droitier ;
- la seed `94838452929881` ;
- un pas d'échantillonnage de `2 m` et des paramètres logiques `FractalValueNoiseV1` ;
- exactement trois couches logiques propres à Visual Valley : herbe, terre et roche.

Le contrat JSON refuse les champs inconnus, les schémas et versions incompatibles, les valeurs non
finies ou invalides, les grilles non alignées, les identifiants dupliqués et les collections hors
bornes. Le document générique accepte une à trente-deux couches ; l'exigence de trois couches reste
localisée dans la recette Visual Valley et ne préjuge pas de `R1-004`.

## Empreintes

| Objet | Empreinte SHA-256 |
|---|---|
| contenu JSON canonique `TerrainDocument` | `9f16780a1de1e9dd4b65931624a8b45281713829434292bada320a578420b2ef` |
| fichier brut `examples/visual_valley/terrain.v1.json` | `38a59c26bfc2aee5d099efb2965ec8c538bfc9a850618a2e9e61d3f713f4994f` |

Le test golden lie explicitement la fixture embarquée, la recette Rust et l'empreinte canonique. Une
seed différente change l'empreinte.

## Validations locales exécutées

| Commande | Résultat |
|---|---|
| `cargo fmt --check` | PASS |
| `cargo test -p forge_project --locked` | PASS, 8 tests |
| `cargo test -p forge_world --locked` | PASS, 7 tests |
| `cargo clippy -p forge_project -p forge_world --all-targets --locked -- -D warnings` | PASS |
| `cargo check -p forge_project -p forge_world --target wasm32-unknown-unknown --locked` | PASS |
| `CARGO_BUILD_JOBS=1 cargo test --workspace --locked` | PASS, 49 tests, 0 échec |
| `git diff --check` sur le lot | PASS |

Une première tentative workspace parallèle a épuisé la mémoire pendant la recompilation de Bevy et
n'est pas comptée comme preuve. Le replay strictement séquentiel a terminé avec le code de sortie 0
après `20m 39s` de reconstruction du cache.

La validation a eu lieu dans la copie de travail principale, qui conservait des modifications
documentaires et scripts non liés. Les sept fichiers du lot correspondaient exactement au commit
qualifié ; aucun manifest Cargo ni `Cargo.lock` n'a changé.

## Revue indépendante

Une seconde revue automatisée a confirmé l'orientation des dépendances
`forge_world → forge_project → forge_core`, l'absence de type Bevy/GPU/Tauri/Rapier dans le
document, la fixture stricte, les hashes et l'absence de référence Cadle ou d'asset. Le minimum
générique de couches a été corrigé de trois à une avant le verdict final.

## Exception CI distante ADR-0006

Le run GitHub Actions [`33341579542`](https://github.com/Karenax/OpenRPG_Forge/actions/runs/33341579542)
cible exactement `9bcdc02cc99cb918f94a8c952ecf461c7da8aca0`. Ses quatre jobs affichent `failure`,
mais ont `steps = []` et la même annotation de refus de démarrage pour facturation ou plafond :

| Job | ID | Étapes | Qualification Forge |
|---|---:|---:|---|
| `wasm` | `99338060598` | 0 | `NOT RUN — infrastructure billing` |
| `native (ubuntu-24.04)` | `99338060653` | 0 | `NOT RUN — infrastructure billing` |
| `native (windows-2025)` | `99338060726` | 0 | `NOT RUN — infrastructure billing` |
| `supply-chain` | `99338060736` | 0 | `NOT RUN — infrastructure billing` |

Ce statut n'est ni un échec produit ni une CI verte. La substitution locale est autorisée par
ADR-0006 jusqu'à Gate 4.

## Limites conservées

`R1-001` prouve seulement le document source. Il ne produit encore aucune hauteur, chunk, mesh,
normale, matière PBR, collision ou image. Les paramètres physiques et arithmétiques dérivés devront
être bornés et recevoir un nouveau golden avant `R1-002/R1-003`. Aucun item `R1-002` à `R1-055`
n'est qualifié par cette preuve, et aucune revue visuelle humaine Gate 1 n'est encore engagée.
