# R1-019 à R1-021 — environnement natif et approximation mesurée

## Verdict borné

**PASS AUTOMATISÉ LOCAL NATIF / PARTIEL MULTI-CIBLE** au commit
`7e9681dd643c2b5c4959d704cf43691f39c3d636`, arbre
`db516e937142a486e7c72655f717cb576df519d5`.

- `R1-019` : ciel physique Bevy, HDR, exposition bornée, tonemapping et soleil profilé observés en
  natif.
- `R1-020` : brouillard atmosphérique de distance observé en natif avec visibilité de 640 m.
- `R1-021` : l'approximation mesurée est le chemin retenu. Le profil demande une volumétrie, mais
  Forge n'installe volontairement aucun `VolumetricFog`, `FogVolume` ou `VolumetricLight` avant sa
  qualification native/WebGPU. Le statut est `not_qualified`, jamais une fausse incapacité GPU.

Ces trois items restent non cochés dans la roadmap tant que le même profil n'a pas été lancé dans le
Studio WebGPU. Cette preuve ne ferme ni Gate 1, ni `R1-014`, ni `R1-043`.

## Sources exactes

| Lot | Commit |
|---|---|
| contrat et fixture déterministes | `668dd68c5ed135549a29a8dbefa81de4c3e0ad13` |
| projection ECS Bevy | `b167c6a5fe90eb410a4bc0ca4590ea4796021e0e` |
| chargement runtime et métriques v5 | `7e9681dd643c2b5c4959d704cf43691f39c3d636` |

Le delta depuis `0b9ea137` porte sur neuf fichiers texte : 1 773 insertions et 14 suppressions. Il
n'ajoute ni dépendance, ni manifeste, ni binaire, ni image, ni modèle, ni texture. Le test de
provenance contient les identités interdites sous forme de liste négative ; aucune de ces identités
n'est une donnée runtime ou un contenu de scène.

Le document `forge.environment-profile` v1 est strict, borné à 64 Kio en entrée JSON et lié aux mêmes
identités projet/zone/révision que le terrain et la végétation. Son hash canonique est
`4b252f036887ec6dbe6ae0942862d15bdba589e7deab92e62a9091e21b6cdd6f`.

## Validation automatisée

Les validations ont été exécutées dans des worktrees détachés propres afin de ne pas modifier le
travail utilisateur non lié présent dans le checkout principal.

| Commande / preuve | Résultat |
|---|---|
| `cargo test -j1 -p forge_project -p forge_world -- --test-threads=1` | **69/69 PASS** |
| `cargo clippy -j1 -p forge_project -p forge_world --all-targets -- -D warnings` | **PASS** |
| `cargo test -j1 -p forge_engine -- --test-threads=1` | **70/70 PASS** |
| `cargo clippy -j1 -p forge_engine --all-targets -- -D warnings` | **PASS** |
| runtime : tests `visual_valley` | **3/3 PASS** |
| métriques : test environnement Gate 1 | **1/1 PASS** |
| `cargo clippy -j1 -p forge_engine -p forge_runtime --all-targets -- -D warnings` | **PASS** |
| `cargo fmt --all -- --check` et `git diff --check` dans les worktrees propres | **PASS** |

Les tests couvrent notamment : JSON inconnu/non fini/surdimensionné, bornes numériques et spatiales,
identités, hash déterministe, convention solaire cardinale, choix de fallback, topologies 0/1/2
caméras et soleils sans panic, présence des composants caméra/soleil/fog, et absence de section
`environment` dans les métriques Gate 0.

## Preuve GPU native

Commande overview exacte :

```powershell
$EvidenceRoot = 'E:\OpenRPG_Forge\.gate1-evidence-runtime\r1_019_021_environment'
cargo run -j1 -p forge_runtime -- --visual-valley `
  --capture "$EvidenceRoot\environment-overview.png" --capture-frame 240 `
  --metrics "$EvidenceRoot\environment-overview-metrics.json" `
  --metrics-warmup-frames 180 --exit-after-frames 300 --no-vsync
```

Commande hauteur humaine exacte :

```powershell
$EvidenceRoot = 'E:\OpenRPG_Forge\.gate1-evidence-runtime\r1_019_021_environment'
cargo run -j1 -p forge_runtime -- --visual-valley --player-camera `
  --capture "$EvidenceRoot\environment-player.png" --capture-frame 240 `
  --exit-after-frames 260 --no-vsync
```

Les deux exécutions se terminent avec le code 0 sur AMD Radeon RX 7800 XT, Vulkan, pilote AMD
propriétaire `26.8.1 (LLPC)`, en 1920 × 1080 sans VSync. Aucun des avertissements Bevy
d'auto-désactivation de `AtmospherePlugin` (compute shaders, `Rgba16Float` storage binding ou
storage textures) n'apparaît. Les seuls avertissements observés concernent le registre du loader
Vulkan et un manifeste AMD dupliqué.

| Artefact local non versionné | Octets | SHA-256 |
|---|---:|---|
| `environment-overview.png` | 1 998 691 | `5f8db26f8c4c64519d9a732edab4fc676020d9b7862c295682b042f369dd4bc5` |
| `environment-overview-metrics.json` | 9 120 | `4e88cead653d24131b03d24d4b43e5f8d0c5cf4069071796fbc2880c9d7d3621` |
| `environment-player.png` | 2 177 314 | `eb4450d5a21d32f785bdd1a2c0c1cebf1aab4fb3203df53d9b831daf0ea465a4` |

Les captures ont été inspectées : ciel clair stable, éclairage solaire et ombres orientés de façon
cohérente, et atténuation atmosphérique visible sur les reliefs lointains. L'overview est plus voilé
que la vue humaine, ce qui est cohérent avec sa distance, mais reste un point de calibration visuelle
pour la revue humaine finale.

## Métriques v5 observées

- `environment.status = observed_approximation` ; source d'évidence ECS explicitement
  `main_world_components_not_gpu_execution`.
- une caméra 3D active, un soleil Visual Valley, une atmosphère et zéro volume fog.
- HDR présent, exposition EV100 12, tonemapper `tony_mc_mapface`, distance aérienne 1 600 m.
- soleil 55 000 lux, ombres actives, disque solaire présent.
- brouillard atmosphérique présent, visibilité 640 m, opacité maximale 0,82.
- volumétrie demandée mais non appliquée ; chemin `distance_fog_fallback`, 64 pas demandés.
- 120 frames après préchauffage : moyenne 2,969145 ms, p95 2,9381 ms, p99 3,1355 ms,
  maximum 33,7815 ms, moyenne 336,80 FPS, 1 % low 54,18 FPS, un hitch > 33,3 ms.

Cette mesure est un smoke de développement RX 7800 XT, pas la qualification finale `R1-041` sur
RTX 3060/RX 6600 et pas une comparaison A/B isolant chaque effet.

## CI distante

Workflow `Rust gate`, run `33436489971`, commit exact `7e9681dd` : quatre jobs
(`supply-chain`, `native (windows-2025)`, `wasm`, `native (ubuntu-24.04)`), zéro étape exécutée. Les
quatre annotations indiquent que le job n'a pas démarré à cause de la facturation ou de la limite de
dépense du compte.

Verdict : **NOT RUN — infrastructure billing**, couvert uniquement par la dérogation CI distante
explicitement autorisée ; aucun job n'est présenté comme vert.

## Restes obligatoires

- lancement réel du même profil et des pipelines sous Studio WebGPU ;
- calibration/revue humaine comparative finale ;
- benchmark 1080p High sur RTX 3060 12 Go ou RX 6600 8 Go ;
- reprise des captures après eau, pluie, wetness et post-traitement final.
