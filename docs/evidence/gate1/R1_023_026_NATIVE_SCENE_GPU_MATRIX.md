# Gate 1 — matrice native de passes GPU de scène R1-023 / R1-026

Date : **2026-09-03**

## Verdict borné

**ACQUISITION AUTOMATISÉE NATIVE LOW/HIGH PASS — AUCUNE ROUTE FERMÉE.** Deux exécutions Release
propres de la même source ont acquis la séquence `clear → rain → clear`, 29 captures validées par
tier et des diagnostics GPU Bevy synchronisés pendant les fenêtres stabilisées des trois vues eau.
La matrice prouve la présence ou l'absence attendue de **passes de scène** selon le tier ; elle ne
prouve ni un draw ou matériau eau, ni la consommation GPU de l'uniform de wetness, ni l'identité de
la frame de rendu exacte.

`R1-023`, `R1-025` et `R1-026` restent **OUVERTES**. La qualification Browser WebGPU, la revue
humaine et le GPU cible restent dues. Gate 1 demeure **OUVERTE** et Gate 2 reste interdite.

## Identité exacte

| Élément | Valeur |
|---|---|
| commit d'acquisition | `41b49f66d4573de505be44dfd38da75704d697f7` |
| arbre source | `8da220873059e91daed6accc2c3ab0cfabe54e11` |
| binaire | Release, 82 668 032 octets |
| SHA-256 du binaire | `0d2e7c32b18ba18dbdfac503765cfed5a2dfd29366d5ae13d12b044866b42c48` |
| GPU / backend | AMD Radeon RX 7800 XT / Vulkan |
| pilote | AMD proprietary driver `26.8.1 (LLPC)` |
| résolution / présentation | 1920 × 1080 / `auto_no_vsync` / VSync faux |
| assets Forge | 9/9 lus depuis le worktree exact, aucune copie dans le dossier Release |
| assets Cadle | aucun utilisé, copié ou versionné |

Les deux validations attestent `clean_worktree_before_and_after=true`, l'identité exacte embarquée,
la racine d'assets sur le dépôt propre et des journaux stdout/stderr vides. Les diagnostics utilisent
`DiagnosticMeasurement.time`, qui est l'instant de synchronisation dans le main world et non le
timestamp d'exécution d'une frame GPU précise.

## Matrice Low / High

| Tier | Captures validées | Mesures GPU natives | Opaque | Transmission | Bloom | Tonemapping | Résultat borné |
|---|---:|---:|---|---|---|---|---|
| Low | 29 | 6 | présente sur 3/3 vues eau | absente comme attendu | absent comme attendu | présente sur 3/3 vues eau | passes de scène Low synchronisées |
| High | 29 | 12 | présente sur 3/3 vues eau | présente sur 3/3 vues eau | présente sur 3/3 vues eau | présente sur 3/3 vues eau | passes de scène High synchronisées |

Les trois checkpoints de chaque processus sont échantillonnés après huit frames stabilisées. Les
valeurs ci-dessous sont les derniers `elapsed_gpu` Bevy synchronisés pendant ces fenêtres ; elles ne
sont ni des temps de frame complète, ni un benchmark comparatif Low/High.

| Tier / checkpoint | Wetness CPU/ECS | opaque (ms) | transmission (ms) | bloom (ms) | tonemapping (ms) |
|---|---:|---:|---:|---:|---:|
| Low `water-clear` | 0 | 0,93636 | N/A | N/A | 0,09452 |
| Low `water-rain` | 0,78 | 1,02644 | N/A | N/A | 0,05908 |
| Low `water-restored` | 0 | 0,81880 | N/A | N/A | 0,05144 |
| High `water-clear` | 0 | 0,99776 | 0,05624 | 0,16568 | 0,05176 |
| High `water-rain` | 0,78 | 0,88936 | 0,05172 | 0,15660 | 0,04848 |
| High `water-restored` | 0 | 0,89192 | 0,05356 | 0,15532 | 0,04856 |

## Ledgers, validations et artefacts

| Tier | Ledger | Validation | Journaux |
|---|---|---|---|
| Low | `8ae2815ced427c7575831adc9ea40c95e79ef97e8eb8dc84f54e8562faa68737` | `51efdeecd7c2361156c78f2db8111e0c38161f556bf2b2d96e57cd5a04e554a1` | stdout/stderr vides |
| High | `2285929a42e9339312845f92a8bbb02afbd38b53674add342307fcb1100a238d` | `6186dd8bf799a01d172c73ff38daedf3a0bff8ead150dc800b2547001d3b0750` | stdout/stderr vides |

Les sous-dossiers
[`scene-gpu-low`](native/41b49f66d4573de505be44dfd38da75704d697f7/scene-gpu-low) et
[`scene-gpu-high`](native/41b49f66d4573de505be44dfd38da75704d697f7/scene-gpu-high) conservent chacun
trois PNG eau, un ledger, une validation et deux journaux : **14 fichiers, 12 879 028 octets** au
total. La synthèse machine est
[`scene-gpu-matrix.v1.json`](native/41b49f66d4573de505be44dfd38da75704d697f7/scene-gpu-matrix.v1.json).
La règle `.gitattributes` bornée `scene-gpu-*/*.json binary -eol` préserve les octets acquis ; les
14 tailles et SHA-256 ont également été revérifiés directement depuis l'index Git avant commit.

## Inspection visuelle d'agent

Les six PNG eau ont été inspectés à leur résolution native. Les vues sont lisibles et non vides, la
pluie est visible, le retour clair est cohérent, et Low et High sont visuellement distincts. Aucun
retour évident du défaut bleu-noir n'a été observé. Cette inspection est un contrôle technique par
agent ; elle ne constitue pas la revue humaine comparative exigée par Gate 1.

## Conséquence par route

| Route | Avancée exacte | Frontière restante | État |
|---|---|---|---|
| `R1-023` | passes opaque/tonemapping Low et opaque/transmission/bloom/tonemapping High échantillonnées aux vues eau | diagnostics à l'échelle de la scène, sans isolation d'un draw ou matériau eau, sans frame exacte ; Browser et humain dus | **OUVERTE** |
| `R1-025` | cycle CPU/ECS exact `0 → 0,78 → 0` et pixels acquis dans les deux tiers | aucune preuve que le fragment shader a consommé l'uniform wetness ; Browser et humain dus | **OUVERTE** |
| `R1-026` | matrice native Low/High : Bloom absent/présent selon le tier, tonemapping présent, endpoints de grade consignés | aucune passe distincte n'attribue le color grading aux pixels capturés ; Browser, humain et GPU cible dus | **OUVERTE** |

Les champs `weather_gpu_execution_observed`, `human_review_performed` et
`gate1_routes_qualified` restent donc tous à `false`. Cette matrice améliore la preuve native de
`R1-023` et `R1-026`, mais n'autorise ni leur fermeture, ni le démarrage de Gate 2.
