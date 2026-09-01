# R1-038 / R1-039 — Observation mémoire GPU et préchauffage des pipelines

- Statut courant : **R1-038 ACQUISE ; HIGH CLEAR/RAIN PRÉCHAUFFÉ ; R1-039 RESTE OUVERTE POUR LES
  AUTRES TIERS ET L'ADAPTATION**
- Base de départ exacte du lot isolé : `f786a38aca2023f08d708687f99a47d14f34e6d4`
- Portée du lot isolé : `forge_engine` et consommation par `forge_runtime` uniquement
- Candidat courant observé : `d9e2670111624aa836ab138afc35cddf441865ef`, arbre
  `d6e5a40f73b47934444bda4514d5cdcb1dab093d`
- Résultat d9 : clear 69/69 pipelines, rain 71/71, prêts pendant toute la mesure ; allocations wgpu
  530 273 124 / 534 407 108 octets et réservées 876 609 552 octets dans les deux runs
- Les sections historiques ci-dessous documentent l'origine du lot ; la roadmap courante fait foi

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
et documentés, répartis en 5 projections au démarrage, 14 projections lors d'une transition de tier
et 15 champs explicitement `not_applicable`. Une mesure gouvernée contient au minimum **120
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
| tests bibliothèque `forge_engine` | **PASS — 120/120** |
| tests workspace/all-targets | **PASS — 296/296** |
| check Studio WASM exact courant | **PASS — 8,71 s** |
| Clippy workspace/all-targets `-D warnings` | **PASS**, zéro warning |
| Smoke natif produisant un ledger `forge.runtime-metrics` v8 | **ACQUIS — High clear/rain, 120 samples chacun** |
| Smoke GPU réel et observation allocateur/pipelines | **ACQUIS — RX 7800 XT/Vulkan** |
| Smoke Browser clear/rain High et ledger Studio v7 | **PASS AUTOMATISÉ BORNÉ à `d9e2670…`** — 24/24 contrôles chacun ; voir [`R1_BROWSER_WEBGPU_SMOKE.md`](R1_BROWSER_WEBGPU_SMOKE.md) |

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
| SHA-256 JSON | `384089ac53c74e47d9f7fadc5f51c3fc51ff27b614db46d80e4bfc9ec0b919c8` | `fa5fc0aae0f4c0478666217866fa1794875131654b8ef2edbc4299e226c5085b` |
| `wgpu_allocator_report_status` | `observed` | `observed` |
| alloué | 530 273 124 octets | 534 407 108 octets |
| réservé | 876 609 552 octets (836,000 MiB) | 876 609 552 octets (836,000 MiB) |
| slabs meshes | 54 042 544 octets (51,539 MiB) | 54 042 544 octets (51,539 MiB) |
| état final pipelines | 69/69 `ready` | 71/71 `ready` |
| queued / creating / errors / waiting | 0 / 0 / 0 / 0 | 0 / 0 / 0 / 0 |
| frames stables / minimum | 177 / 30 | 177 / 30 |
| frames Render observées / stabilisation | 418 / frame 271 | 418 / frame 271 |
| prêt avant / pendant toute la mesure | oui / oui | oui / oui |

Dans les deux cas `allocated <= reserved` et les slabs sont strictement positifs. La différence rain
est de 4 259 692 octets, soit 4,062 MiB alloués. Les valeurs restent explicitement bornées aux
sous-allocations du périphérique wgpu courant et à la capacité des slabs Bevy : elles ne mesurent ni
VRAM physique totale, ni budget OS, ni résidence globale.

Rain rapporte deux pipelines prêts de plus que clear et observe séparément le pass transparent de pluie.
Les artefacts ne lient toutefois pas le delta de compteur à un pipeline nommé. Cela ne prouve pas les
variantes Low, Medium, Ultra ou adaptatives, ni un cache persistant. Le smoke Browser High est acquis,
mais sa readiness CPU/ECS ne nomme ni ne compte les pipelines.

Les deux logs d9 sont vides : aucun warning ni `ERROR` n'est enregistré dans ces exécutions. Cette
absence ne qualifie pas automatiquement les autres backends ni la présentation visuelle.

## Limites et qualification restante

- Smoke runtime avec adaptateur Vulkan réel : **ACQUIS sur RX 7800 XT**.
- Rapport allocateur réellement observé et vérification `allocated <= reserved` : **ACQUIS pour
  clear et rain**.
- Validation que `mesh_allocator_slabs_bytes > 0` sur Visual Valley rendue : **ACQUISE pour clear
  et rain**.
- Replay séparé High `clear` et `rain` : **ACQUIS** ; Low/Medium/Ultra et adaptation : **NOT RUN**.
- Browser clear/rain High : **ACQUIS à `d9e2670…`** avec 60 + 120 frames ; clear moyenne/p95/1 % low
  10,1175 ms/12,1 ms/77,519 FPS et rain 9,5633 ms/10,9 ms/85,106 FPS. Ces temps mesurent la loop WASM,
  pas le GPU ; `gpu_presentation=not_observed` et aucune convergence pipeline Browser n'est exposée.
- CI distante du lot isolé : **NOT RUN dans cette preuve** ; aucun résultat d'une autre exécution
  n'est attribué à ce lot.

En conséquence, `R1-038` est acquise sur le candidat natif exact. La sous-preuve High clear/rain de
`R1-039` est acquise, mais la route reste ouverte : autres tiers/variantes, cache par nouveau
processus, présentation GPU et acceptation visuelle ne sont pas couverts.

## Frontières préservées par le lot isolé

- aucun fichier Studio modifié par le lot GPU/pipeline isolé ; le candidat combiné contient par
  ailleurs l'observation Studio v7 de qualité/scenery ;
- aucune roadmap ni `PROJECT_CONTEXT.md` modifiés ;
- aucun asset ajouté ;
- aucune copie ou dépendance Cadle ;
- aucun fork Bevy, aucune passe raw wgpu et aucun faux compteur de draw calls.
