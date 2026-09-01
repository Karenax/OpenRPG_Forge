# R1-038 / R1-039 — Observation mémoire GPU et préchauffage des pipelines

- Statut : **OBSERVATION NATIVE VULKAN ET SMOKE BROWSER CLEAR/RAIN ACQUIS ; R1-039 ET VARIANTES RESTENT OUVERTES**
- Base de départ exacte du lot isolé : `f786a38aca2023f08d708687f99a47d14f34e6d4`
- Portée du lot isolé : `forge_engine` et consommation par `forge_runtime` uniquement
- Candidat natif observé : `92286910a9bab622d5d54905551c0f03172e98e7`, arbre
  `6d7cd4ab8612686e7f73f01abeb5b8ad0174ebbe`
- Roadmap : non modifiée ; les sous-preuves natives sont acquises, les routes complètes restent ouvertes

## Objet du lot

Ce lot ajoute une observation honnête et sérialisée de deux propriétés distinctes :

1. les sous-allocations mémoire du périphérique wgpu courant lorsqu'elles sont exposées par le
   backend, complétées par la capacité des slabs de meshes Bevy ;
2. l'état des pipelines render et compute déjà exercés dans le processus courant, avec une barrière
   de trente frames rendues consécutives sans pipeline en attente ou en erreur.

Il ne fournit ni la VRAM physique totale, ni le budget mémoire du système d'exploitation, ni un cache
pipeline persistant. Aucun nombre absent n'est remplacé par zéro.

## Versions verrouillées inspectées

| Composant | Version résolue |
|---|---:|
| Bevy / `bevy_render` | `0.19.1` |
| wgpu / wgpu-core / wgpu-hal / wgpu-types | `29.0.4` |

Le lockfile du dépôt est l'autorité. Aucune dépendance Cargo n'a été ajoutée par ce lot.

## Sémantique mémoire

L'observation native appelle une seule fois, lors de l'écriture du rapport final :

```rust
RenderDevice::wgpu_device().generate_allocator_report()
```

Le rapport sérialise séparément :

- `wgpu_allocator_allocated_bytes` : somme des sous-allocations vivantes du périphérique wgpu ;
- `wgpu_allocator_reserved_bytes` : somme des blocs réservés par cet allocateur ;
- `mesh_allocator_slabs_bytes` : capacité totale des slabs de meshes gérés par Bevy, issue de
  `MeshAllocatorDiagnosticPlugin`.

Les invariants suivants sont imposés :

- un rapport wgpu absent reste `null` avec le statut `unsupported_or_unavailable` ;
- l'absence de `RenderDevice` reste distincte avec `render_device_unavailable` ;
- `allocated > reserved` invalide le rapport au lieu de produire une métrique incohérente ;
- les slabs mesh sont une mesure partielle et ne couvrent pas textures, render targets, staging,
  pipelines, allocations du pilote ou autres ressources GPU.

Les implémentations DX12 et Vulkan de wgpu peuvent produire le rapport allocateur, mais l'API reste
optionnelle. Le backend Browser WebGPU renvoie `None`. Les métriques de heap JavaScript ou de mémoire
linéaire WASM ne sont pas de la mémoire GPU et ne sont pas utilisées comme substitut.

Les noms et champs évitent volontairement `total_vram` : ces valeurs ne donnent ni capacité physique,
ni budget OS, ni résidence réelle globale.

## Sémantique du préchauffage

Le système RenderApp observe `PipelineCache` après `RenderSystems::Render` et compte :

- `total` ;
- `queued` ;
- `creating` ;
- `ready` (`CachedPipelineState::Ok`) ;
- `errors` ;
- `waiting`.

L'état devient `ready` uniquement après **30 frames Render consécutives** satisfaisant toutes les
conditions suivantes :

```text
total > 0
queued == 0
creating == 0
errors == 0
waiting == 0
ready == total
nombre total inchangé
```

Une variation du nombre total, un nouveau pipeline pending ou une erreur remet immédiatement
`stable_frames` à zéro. Une fenêtre de performance est rejetée si le cache n'était pas prêt à son
premier échantillon ou s'il quitte l'état prêt pendant la mesure.

Cette preuve couvre uniquement les variantes effectivement matérialisées par la scène et le profil
exercés. Un cache stable ne prouve pas qu'une variante latente de météo, matériau ou qualité a été
compilée. Bevy transmet `cache: None` aux descripteurs wgpu : ce préchauffage est à rejouer à chaque
processus et ne constitue pas un cache disque.

## Contrat runtime-metrics isolé et intégration candidate

Sur la base `f786a38a`, le lot fait évoluer `forge.runtime-metrics` de la version 6 à la version 7 avec
deux objets additifs :

- `gpu_memory` ;
- `pipeline_warmup`.

Une autre tranche d'intégration utilisait déjà la version 7 pour le profil qualité. Le candidat
combiné résout cette collision dans un contrat **`forge.runtime-metrics` version 8** unique. Il
conserve simultanément `quality`, `scenery`, `gpu_memory` et `pipeline_warmup`, en plus des autres
blocs Visual Valley. Pour une mesure gouvernée, la sérialisation refuse notamment une scenery en
attente/repli, un tier qualité non appliqué, une plage LOD scenery différente du preset appliqué ou
un pipeline non prêt pendant toute la fenêtre d'échantillonnage.

Le ledger de scène Studio du candidat combiné est en version 7. Il observe le tier courant, le tier
effectivement appliqué et les bornes LOD attendues, puis exige leur concordance avec l'observation
scenery avant de déclarer Visual Valley prête. Ce ledger Studio n'est pas une observation de mémoire
GPU ni une preuve de présentation GPU.

La sérialisation native v8 exige la composition Gate 1 complète : terrain, ombres, végétation,
scenery, environnement, eau, météo et qualité. Le ledger qualité doit contenir ses 34 champs uniques
et documentés, répartis en 5 projections au démarrage, 15 projections lors d'une transition de tier
et 14 champs explicitement `not_applicable`. Une mesure gouvernée contient au minimum **120
échantillons de frame finis et positifs après le warm-up**.

Le ledger natif suit en outre une empreinte de l'état de rendu : tier courant/appliqué, statut et
comptes de chargement/remplacement scenery, ainsi que sa plage LOD. Cette empreinte doit être stable
avant la première frame mesurée et pendant toute la fenêtre. Studio v7 emploie une empreinte propre
sur la scène, les tiers courant/appliqué et les plages LOD qualité/scenery. Une empreinte incomplète
ou modifiée réinitialise ses 60 frames de warm-up et ses 120 frames de mesure. L'empreinte SHA-256
canonique du profil qualité est identique dans `quality.profile_hash` côté natif et
`source_hashes.quality` côté Studio.

## Validations locales du lot isolé

Les résultats ci-dessous ont été obtenus avant l'intégration avec les tranches qualité, scenery et
Studio. Ils valident le lot GPU/pipeline isolé ; ils ne sont pas attribués au candidat combiné.

| Commande | Résultat |
|---|---|
| `cargo fmt --all -- --check` | PASS |
| `cargo test -p forge_engine --lib` | PASS — 97 réussis, 0 échec |
| `cargo test -p forge_runtime --bin forge_runtime` | PASS — 28 réussis, 0 échec |
| `cargo clippy -p forge_engine -p forge_runtime --all-targets --all-features -- -D warnings` | PASS, zéro avertissement |
| `cargo check -j1 -p forge_engine --target wasm32-unknown-unknown --no-default-features --features web --locked` | PASS — compilation froide 13 min 47 s, replay final incrémental 2,62 s |

Les tests nouveaux couvrent :

- distinction entre périphérique absent, rapport indisponible et totaux observés, sans substituer
  `None` par zéro ;
- rejet de `allocated > reserved` ;
- attente exacte de trente frames stables ;
- reset sur nouveau pipeline ou état `Creating` ;
- refus d'un cache vide ou en erreur ;
- refus d'une mesure commencée ou poursuivie pendant le warm-up ;
- conversion bornée de la mesure de slabs en octets ;
- présence additive des deux objets dans la sérialisation de métriques v7.

## Validation du candidat combiné

| Validation finale sur l'état combiné exact | Résultat |
|---|---|
| `cargo fmt --all -- --check` | **PASS** |
| `git diff --check` | **PASS** |
| tests bibliothèque `forge_engine` | **PASS — 119/119** |
| tests `forge_runtime` | **PASS — 32/32** (31 bin + 1 intégration) |
| tests `forge_studio` | **PASS — 31/31** (28 lib + 3 bin) |
| `cargo check -p forge_studio --lib --target wasm32-unknown-unknown --no-default-features --features web` exact courant | **PASS — 1.06 s** |
| `$env:CARGO_TARGET_DIR='E:\OpenRPG_Forge\.cargo-target-gate1-combined'; cargo clippy -p forge_engine -p forge_runtime -p forge_studio --all-targets --locked -j 1 -- -D warnings` | **PASS — 12.04 s** |
| Smoke natif produisant un ledger `forge.runtime-metrics` v8 | **ACQUIS — High clear/rain, 120 samples chacun** |
| Smoke GPU réel et observation allocateur/pipelines | **ACQUIS — RX 7800 XT/Vulkan** |
| Smoke Browser clear/rain High et ledger Studio v7 | **PASS AUTOMATISÉ BORNÉ à `a34596bd…`** — 24/24 contrôles chacun ; voir [`R1_BROWSER_WEBGPU_SMOKE.md`](R1_BROWSER_WEBGPU_SMOKE.md) |

Ces validations intégrées prouvent les tests CPU, le format, la compilation Studio WASM et le Clippy
strict. Les deux exécutions natives suivantes ajoutent l'observation allocateur/pipeline réelle sur
Vulkan. Le Browser charge 9/9 modèles et 26/26 assets HTTP 200, sans `.meta`, échec réseau ou entrée
console, mais n'expose ni rapport allocateur GPU ni convergence du cache pipeline.

## Preuve native exacte du candidat combiné

Le binaire Release
`E:\OpenRPG_Forge\.cargo-target-gate1-combined\release\forge_runtime.exe` a été lancé depuis le
worktree candidat avec `BEVY_ASSET_ROOT` pointant sur ce même worktree, en Visual Valley,
`--player-camera`, High fixe, `--weather clear` puis `rain`, capture frame 240, warm-up métriques 300,
sortie bornée frame 420 et `--no-vsync`. La reconstruction complète des deux lignes de commande et le
manifest SHA-256 des six fichiers sont consignés dans
`docs/evidence/gate1/R1_034_043_PERFORMANCE_PARITY.md`.

Le transcript shell, le hash du binaire et le code de sortie ne sont pas embarqués avec les fichiers.
Les deux ledgers locaux ont néanmoins été relus et hashés directement :

| Ledger `forge.runtime-metrics` v8 | Clear | Rain |
|---|---:|---:|
| SHA-256 JSON | `09A6324A5668B5824816620A095CBE67B2BB172E3D7D757815D8306D7EABD6F3` | `F231A9ED74C7C3E0EDA97371B5374E7AE9107CFF2E896D14372887B562754385` |
| `wgpu_allocator_report_status` | `observed` | `observed` |
| alloué | 561 396 832 octets (535,390 MiB) | 565 656 524 octets (539,452 MiB) |
| réservé | 876 609 552 octets (836,000 MiB) | 876 609 552 octets (836,000 MiB) |
| slabs meshes | 54 042 544 octets (51,539 MiB) | 54 042 544 octets (51,539 MiB) |
| état final pipelines | 68/68 `ready` | 69/69 `ready` |
| queued / creating / errors / waiting | 0 / 0 / 0 / 0 | 0 / 0 / 0 / 0 |
| frames stables / minimum | 177 / 30 | 177 / 30 |
| frames Render observées / stabilisation | 418 / frame 271 | 418 / frame 271 |
| prêt avant / pendant toute la mesure | oui / oui | oui / oui |

Dans les deux cas `allocated <= reserved` et les slabs sont strictement positifs. La différence rain
est de 4 259 692 octets, soit 4,062 MiB alloués. Les valeurs restent explicitement bornées aux
sous-allocations du périphérique wgpu courant et à la capacité des slabs Bevy : elles ne mesurent ni
VRAM physique totale, ni budget OS, ni résidence globale.

Rain rapporte un pipeline prêt de plus que clear et observe séparément le pass transparent de pluie.
Les artefacts ne lient toutefois pas le delta de compteur à un pipeline nommé. Cela ne prouve pas les
variantes Low, Medium, Ultra ou adaptatives, ni un cache persistant. Le smoke Browser High est acquis,
mais sa readiness CPU/ECS ne nomme ni ne compte les pipelines.

Chaque log natif contient exactement un `WARN` `wgpu_core::validation` et aucun `ERROR` : un vertex
shader produit `@builtin(position)` sans `@invariant` dans un pipeline à comparaison `Equal`. wgpu
signale un risque d'artefact selon la machine. Le warning ne remet pas en cause les nombres
allocateur/pipeline, mais interdit de qualifier les logs de propres ou la présentation visuelle de
validée.

## Limites et qualification restante

- Smoke runtime avec adaptateur Vulkan réel : **ACQUIS sur RX 7800 XT**.
- Rapport allocateur réellement observé et vérification `allocated <= reserved` : **ACQUIS pour
  clear et rain**.
- Validation que `mesh_allocator_slabs_bytes > 0` sur Visual Valley rendue : **ACQUISE pour clear
  et rain**.
- Replay séparé High `clear` et `rain` : **ACQUIS** ; Low/Medium/Ultra et adaptation : **NOT RUN**.
- Browser clear/rain High : **ACQUIS à `a34596bd…`** avec 60 + 120 frames ; clear moyenne/p95/1 % low
  10,7283 ms/13,4 ms/54,945 FPS et rain 9,1683 ms/10,1 ms/84,034 FPS. Ces temps mesurent la loop WASM,
  pas le GPU ; `gpu_presentation=not_observed` et aucune convergence pipeline Browser n'est exposée.
- CI distante du lot isolé : **NOT RUN dans cette preuve** ; aucun résultat d'une autre exécution
  n'est attribué à ce lot.

En conséquence, les sous-preuves natives d'allocations estimées (`R1-038`) et de convergence des
pipelines High clear/rain (`R1-039`) sont acquises sur le candidat natif exact. Le smoke Browser est
acquis séparément à `a34596bd…`, sans observation mémoire GPU/pipeline. Les deux routes restent
ouvertes au niveau global : autres tiers/variantes, cache par nouveau processus, présentation GPU et
acceptation visuelle ne sont pas couverts.

## Frontières préservées par le lot isolé

- aucun fichier Studio modifié par le lot GPU/pipeline isolé ; le candidat combiné contient par
  ailleurs l'observation Studio v7 de qualité/scenery ;
- aucune roadmap ni `PROJECT_CONTEXT.md` modifiés ;
- aucun asset ajouté ;
- aucune copie ou dépendance Cadle ;
- aucun fork Bevy, aucune passe raw wgpu et aucun faux compteur de draw calls.
