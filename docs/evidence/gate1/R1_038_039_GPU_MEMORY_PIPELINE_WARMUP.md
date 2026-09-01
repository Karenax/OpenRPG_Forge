# R1-038 / R1-039 — Observation mémoire GPU et préchauffage des pipelines

- Statut : **IMPLÉMENTATION CANDIDATE TESTÉE, NON QUALIFIÉE SUR GPU RÉEL**
- Base exacte : `f786a38aca2023f08d708687f99a47d14f34e6d4`
- Portée : `forge_engine` et consommation par `forge_runtime` uniquement
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

## Contrat runtime-metrics isolé

Sur la base `f786a38a`, le lot fait évoluer `forge.runtime-metrics` de la version 6 à la version 7 avec
deux objets additifs :

- `gpu_memory` ;
- `pipeline_warmup`.

Une autre tranche d'intégration utilise déjà la version 7 pour le profil qualité. L'intégrateur doit
combiner les deux extensions dans une **version 8** unique ; il ne doit pas conserver deux contrats
différents portant le même numéro 7.

## Validations locales

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

## Limites et qualification restante

- Smoke runtime avec adaptateur DX12/Vulkan réel : **NOT RUN**.
- Rapport allocateur réellement observé et vérification `allocated <= reserved` : **NOT RUN**.
- Validation que `mesh_allocator_slabs_bytes > 0` sur Visual Valley rendue : **NOT RUN**.
- Replay séparé des variantes `clear` et `rain`, puis Low/Medium/High/Ultra : **NOT RUN**.
- Browser WebGPU réel avec rapport allocateur `null` et convergence pipelines : **NOT RUN**.
- CI distante : **NOT RUN — infrastructure billing**, conformément à ADR-0006.

En conséquence, ce lot livre le contrat, l'instrumentation et les tests ciblés, mais ne ferme pas
`R1-038` ou `R1-039`. Leur qualification exige encore les smokes GPU réels et les variantes de profil
sur le futur candidat intégré.

## Frontières préservées

- aucun fichier Studio modifié ;
- aucune roadmap ni `PROJECT_CONTEXT.md` modifiés ;
- aucun asset ajouté ;
- aucune copie ou dépendance Cadle ;
- aucun fork Bevy, aucune passe raw wgpu et aucun faux compteur de draw calls.
