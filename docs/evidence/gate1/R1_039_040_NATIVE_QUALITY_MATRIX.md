# R1-039 / R1-040 — matrice native de préchauffage et qualité

Date : **1er septembre 2026**

Verdict : **R1-039 ACQUISE EN NATIF ; R1-040 ACQUISE SUR LE RUNTIME PARTAGÉ**

Ce verdict qualifie le préchauffage des pipelines exercés par le runtime natif et la projection des
profils Low, Medium, High, Ultra, adaptatif et benchmark. Il ne vaut ni acceptation artistique, ni
benchmark sur le GPU milieu de gamme requis, ni observation de présentation GPU dans le Browser.

## Identité exacte

- commit source : `5eadec7f6f9f9b5b6734ce62ea3518f0297496f9` ;
- tree source : `a2fb4df0b7e2eff4d686dda575d4a2bf18cb12a1` ;
- binaire Release : 81 743 872 octets ;
- SHA-256 du binaire :
  `33b3ea91dff8d5d0aa4af054aacf6f575bc1dc449572b914452518acdeb1c3eb` ;
- machine : AMD Radeon RX 7800 XT, backend Vulkan, 1920 x 1080, VSync désactivée ;
- racine d'assets : `BEVY_ASSET_ROOT` pointant sur le worktree exact ;
- caméra : `--player-camera` pour les dix runs retenus ;
- profil qualité canonique :
  `8dc219ad88ba1472b0ebbb13d8f4beac13f3fae1eeff688c928abb044d96d0e2`.

Les quatre profils fixes utilisent capture frame 360, warm-up métriques 480 et sortie frame 600.
Le mode benchmark utilise les mêmes bornes. Le mode adaptatif utilise capture 840, warm-up 900 et
sortie 1020 afin que la transition puis la nouvelle empreinte de rendu aient le temps de converger.
Chaque ledger ne conserve que les 120 frames post-warm-up dont l'empreinte est stable.

## Validation source avant mesure

| Contrôle | Résultat |
|---|---|
| `cargo test -p forge_engine --lib --locked -j1` | **122/122 PASS** |
| `cargo test --workspace --all-targets --locked -j1` | **298/298 PASS** |
| `cargo clippy --workspace --all-targets -- -D warnings` | **PASS** |
| `cargo fmt --all -- --check` | **PASS** |
| `git diff --check` | **PASS** |
| Studio WASM `wasm32-unknown-unknown`, features `web` | **PASS** |

Le correctif source associe maintenant explicitement le tier qualité au profil eau. Le validateur
des métriques refuse un ledger dont `water.resolved_optics.requested_quality` diffère du tier
effectivement appliqué. Les tests couvrent les quatre tiers, les fallbacks disponibles et une
transition ECS Low vers Ultra sans réactivation de SSR/différé indisponible.

## Matrice native retenue

| Run | Tier appliqué / eau | Pipelines | Moyenne ms | p95 ms | 1 % low FPS | hitches > 16,7 ms |
|---|---|---:|---:|---:|---:|---:|
| Low clear | Low / Low | 59/59 | 5,6130 | 6,7886 | 113,104 | 0 |
| Low rain | Low / Low | 61/61 | 8,5513 | 13,9956 | 56,293 | 2 |
| Medium clear | Medium / Medium | 63/63 | 6,2959 | 6,8495 | 116,138 | 0 |
| Medium rain | Medium / Medium | 65/65 | 6,5001 | 7,0282 | 134,937 | 0 |
| High clear | High / High | 72/72 | 8,2254 | 8,6376 | 106,955 | 0 |
| High rain | High / High | 71/71 | 8,2229 | 8,7551 | 107,410 | 0 |
| Ultra clear | Ultra / Ultra | 72/72 | 9,6308 | 10,1900 | 87,310 | 0 |
| Ultra rain | Ultra / Ultra | 74/74 | 10,2077 | 11,5613 | 78,449 | 0 |
| Adaptive clear | High -> Ultra / Ultra | 72/72 | 8,7080 | 9,3854 | 95,155 | 0 |
| Benchmark clear | High / High | 69/69 | 8,3494 | 9,1656 | 93,158 | 0 |

Les dix runs exposent :

- `forge.runtime-metrics` version 8 et exactement 120 échantillons ;
- pipelines `ready` avant la mesure et pendant toute la fenêtre, sans queued, creating, error ou
  waiting ;
- rapport allocateur wgpu réellement observé avec `allocated <= reserved` ;
- 9/9 modèles CC0 chargés, zéro échec et 99 remplacements + 26 détails activés ;
- exactement 5 champs `applied_at_startup`, 14 `applied_on_tier_transition` et 15
  `not_applicable` ;
- météo attendue et concordance stricte entre tier courant, tier appliqué et tier eau ;
- sorties standard et erreur vides.

Le run adaptatif démarre en High, transitionne une fois vers Ultra à la frame 604 pour
`sustained_frame_time_below_upgrade_threshold`, puis recommence le warm-up avant les 120 mesures.
Le benchmark reste High avec `benchmark_fixed_high=true` et zéro transition.

Low/rain contient deux hitches courts. Le p95 et le 1 % low de cette fenêtre restent dans les seuils
numériques, mais cette observation n'est pas transformée en preuve d'absence de stutter récurrent :
le parcours temporel représentatif et le GPU cible restent dus.

## Audit des artefacts

Le dossier
[`native/5eadec7f6f9f9b5b6734ce62ea3518f0297496f9/quality-matrix`](native/5eadec7f6f9f9b5b6734ce62ea3518f0297496f9/quality-matrix)
contient les dix ledgers et dix PNG retenus, ainsi que :

- [`validation.json`](native/5eadec7f6f9f9b5b6734ce62ea3518f0297496f9/quality-matrix/validation.json),
  audit 10/10 runs et 40/40 fichiers de la campagne locale avant sélection ;
- [`artifact-manifest.json`](native/5eadec7f6f9f9b5b6734ce62ea3518f0297496f9/quality-matrix/artifact-manifest.json),
  tailles et SHA-256 des vingt artefacts versionnés.

L'audit relit les dix JSON, vérifie les schémas, samples, pipelines, tiers, eau, météo, scenery,
allocations et logs. Il trouve **zéro référence Cadle**. Aucun asset Cadle n'a été copié dans Forge.

Deux séries locales ne sont pas retenues : la première omettait `BEVY_ASSET_ROOT`, ce qui a provoqué
un fallback scenery et le rejet correct des métriques ; la seconde utilisait la caméra d'ensemble,
donc une composition non comparable. Elles restent hors Git et ne contribuent à aucun verdict.

## Revue visuelle bornée

La comparaison des captures confirme les changements de densité, distance de visibilité, ombres,
pluie et post-traitement entre tiers. L'overlay affiche aussi le tier appliqué et la transition
adaptative vers Ultra.

Cette revue n'accepte pas le rendu final : les contours noirs et le stipple/aliasing sont visibles et
s'accentuent en High/Ultra. L'eau n'est pas isolée dans ce cadrage joueur. Les captures ferment la
preuve de fonctionnement des profils, pas `R1-023`, `R1-046` à `R1-052` ni les conditions humaines
de passage de Gate 1.

## Portée du verdict

- `R1-039` est acquise pour les pipelines natifs exercés dans Low/Medium/High/Ultra, clear/rain,
  adaptation et benchmark. Aucun cache persistant ni compteur Browser n'est revendiqué.
- `R1-040` est acquise : les quatre profils fixes fonctionnent, l'adaptation change réellement de
  tier, benchmark reste High, le scenery suit les LOD et l'eau suit le tier sans inventer de chemin
  GPU indisponible. Studio partage l'adapter et compile en WASM, mais aucune nouvelle présentation
  Browser multi-tier n'est revendiquée.
- La condition « profils qualité fonctionnels » est satisfaite sur preuves automatisées et captures
  natives, sans valoir revue artistique.
- Gate 1 reste ouverte pour le GPU cible, le parcours représentatif, les vues qualifiantes,
  l'instrumentation exhaustive et les revues humaines.
