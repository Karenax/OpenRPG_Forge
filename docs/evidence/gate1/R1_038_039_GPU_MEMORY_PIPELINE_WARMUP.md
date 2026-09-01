# R1-038 / R1-039 — Observation mémoire GPU et préchauffage des pipelines

- Statut : **CANDIDAT COMBINÉ — FORMAT/TESTS/WASM/CLIPPY PASS ; SMOKES GPU NOT RUN**
- Base de départ exacte du lot isolé : `f786a38aca2023f08d708687f99a47d14f34e6d4`
- Portée du lot isolé : `forge_engine` et consommation par `forge_runtime` uniquement
- Roadmap : `R1-038` et `R1-039` restent ouverts

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
| tests bibliothèque `forge_engine` | **PASS — 117/117** |
| tests `forge_runtime` | **PASS — 30/30** |
| tests bibliothèque `forge_studio` | **PASS — 29/29** |
| `cargo check -p forge_studio --lib --target wasm32-unknown-unknown --no-default-features --features web` exact courant | **PASS — 6.83 s** |
| `$env:CARGO_TARGET_DIR='E:\OpenRPG_Forge\.cargo-target-gate1-combined'; cargo clippy -p forge_engine -p forge_runtime -p forge_studio --all-targets --locked -j 1 -- -D warnings` | **PASS — 11.27 s** |
| Smoke natif produisant un ledger `forge.runtime-metrics` v8 | **NOT RUN** |
| Smoke GPU réel et observation allocateur/pipelines | **NOT RUN** |
| Smoke Browser WebGPU et ledger Studio v7 | **NOT RUN** |

Ces validations intégrées prouvent les tests CPU, le format, la compilation Studio WASM et le Clippy
strict. Elles ne remplacent pas les smokes de présentation GPU.

## Limites et qualification restante

- Smoke runtime avec adaptateur DX12/Vulkan réel : **PAS ENCORE EXÉCUTÉ**.
- Rapport allocateur réellement observé et vérification `allocated <= reserved` : **PAS ENCORE
  EXÉCUTÉ**.
- Validation que `mesh_allocator_slabs_bytes > 0` sur Visual Valley rendue : **PAS ENCORE
  EXÉCUTÉ**.
- Replay séparé des variantes `clear` et `rain`, puis Low/Medium/High/Ultra : **PAS ENCORE
  EXÉCUTÉ**.
- Browser WebGPU réel avec rapport allocateur `null` et convergence pipelines : **PAS ENCORE
  EXÉCUTÉ**.
- CI distante du lot isolé : **NOT RUN dans cette preuve** ; aucun résultat d'une autre exécution
  n'est attribué à ce lot.

En conséquence, ce lot livre le contrat, l'instrumentation et les tests ciblés, mais ne ferme pas
`R1-038` ou `R1-039`. Leur qualification exige encore les smokes GPU réels et les variantes de profil
sur le candidat intégré actuel.

## Frontières préservées par le lot isolé

- aucun fichier Studio modifié par le lot GPU/pipeline isolé ; le candidat combiné contient par
  ailleurs l'observation Studio v7 de qualité/scenery ;
- aucune roadmap ni `PROJECT_CONTEXT.md` modifiés ;
- aucun asset ajouté ;
- aucune copie ou dépendance Cadle ;
- aucun fork Bevy, aucune passe raw wgpu et aucun faux compteur de draw calls.
