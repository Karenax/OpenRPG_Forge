# R1-034 à R1-043 — instrumentation performance et parité native/Studio

- Statut : **R1-034, R1-039, R1-040 ET R1-043 ACQUISES ; GPU CIBLE, R1-037 ET QUALIFICATION
  VISUELLE FINALE OUVERTS**
- État natif exécuté (`HEAD` au moment des smokes) :
  `d9e2670111624aa836ab138afc35cddf441865ef`,
  arbre `d6e5a40f73b47934444bda4514d5cdcb1dab093d`
- Contrat natif candidat : `forge.runtime-metrics` version 8
- Contrat de scène Studio candidat : `forge.studio-scene-telemetry` version 7
- Overlay CPU/GPU courant : `afb270c328b7095a84d452b93b9fdd38e2737f33`, arbre
  `5eeb9098b8300dead8679de7d478f5096a572fd1`
- Roadmap : `R1-034`, `R1-039`, `R1-040` et `R1-043` fermées ; les routes encore ouvertes sont
  détaillées ci-dessous

Ce rapport cartographie ce que le candidat sait réellement observer. La paire native/Browser vient
du commit d9 exact, la matrice qualité de `5eadec7f` et l'overlay CPU/GPU de `afb270c3`. La campagne
`a34596bd…` reste historique et byte-for-byte intacte.

## Validation automatisée de l'état intégré courant

| Validation intégrée | Résultat exact consigné |
|---|---|
| tests `forge_engine` | **122/122 PASS** |
| tests workspace/all-targets | **298/298 PASS** |
| check de compilation WASM exact courant | **PASS — 8,24 s** |
| `cargo fmt --all -- --check` | **PASS** |
| `git diff --check` | **PASS** |
| Clippy strict workspace/all-targets | **PASS**, zéro warning |
| smoke GPU natif High fixe, `clear` puis `rain` | **PASS** — deux codes de sortie 0, 46/46 contrôles croisés |
| smoke Browser clear/rain High | **PASS AUTOMATISÉ BORNÉ** au commit `d9e2670…` — 24/24 contrôles chacun ; voir [`R1_BROWSER_WEBGPU_SMOKE.md`](R1_BROWSER_WEBGPU_SMOKE.md) |

La campagne automatisée valide les contrats et la compilation WASM. Les deux exécutions natives
séparées ajoutent une observation Vulkan et des captures. Le smoke Browser charge 9/9 modèles et
26/26 assets avec HTTP 200, sans requête `.meta`, échec réseau ni entrée console. La paire native et
Studio vise désormais le même commit et les sept hashes communs concordent. Le Browser conserve
`gpu_presentation=not_observed` : la preuve ferme la parité de données, pas la présentation GPU ni
l'acceptation visuelle finale.

## Exécutions natives exactes et artefacts

Le runtime Release a été reconstruit dans un worktree détaché propre sur `d9e2670…`, puis lancé
directement deux fois avec `BEVY_ASSET_ROOT` pointant vers ce même worktree. Clear et rain terminent
avec le code 0. Les six artefacts versionnés, les commandes, le hash du binaire et les 46 assertions
croisées sont détaillés dans
[`R1_043_NATIVE_STUDIO_EXACT_COMMIT.md`](R1_043_NATIVE_STUDIO_EXACT_COMMIT.md).

| Mesure 1080p High, VSync off | Clear | Rain |
|---|---:|---:|
| échantillons | 120 | 120 |
| moyenne | 8,3478 ms / 119,792 FPS | 8,2920 ms / 120,598 FPS |
| p95 | 8,7132 ms | 9,0950 ms |
| 1 % low | 113,775 FPS | 96,354 FPS |
| hitches `>16,7 ms` / `>33,3 ms` | 0 / 0 | 0 / 0 |
| histogramme `<=16,7` / `16,7..33,3` / `>33,3` | 120 / 0 / 0 | 120 / 0 / 0 |

Les deux logs d9 sont vides : aucun warning ni `ERROR` n'y est enregistré. La correction visuelle est
nettement observable, mais les contours sombres/stipple et l'absence d'une vue eau dédiée maintiennent
la qualification artistique ouverte. Voir
[`R1_GRAPHICS_FORWARD_IBL_CORRECTION.md`](R1_GRAPHICS_FORWARD_IBL_CORRECTION.md).

## Overlay CPU/GPU exact `afb270c3`

Le commit `afb270c328b7095a84d452b93b9fdd38e2737f33` ajoute à l'overlay les diagnostics Bevy nommés
`render/main_opaque_pass_3d/elapsed_cpu` et `elapsed_gpu`. Le binaire Release mesure 81 745 408 octets
et porte le SHA-256 `9c7a85d9719101e53a25f79d8846462561c81dd9f7e5b1f72743d0964f2caffa`.

Deux runs High, clear puis rain, ont été rejoués sur RX 7800 XT/Vulkan, 1920 x 1080, caméra joueur et
VSync off :

| Preuve | Clear | Rain |
|---|---:|---:|
| temps CPU opaque du ledger | 0,0101 ms | 0,0153 ms |
| temps GPU opaque du ledger | 0,68072 ms | 0,7246 ms |
| valeur visible dans le PNG | CPU 0,010 / GPU 0,674 ms | CPU 0,012 / GPU 0,661 ms |
| samples | 120 | 120 |
| pipelines | 69/69 | 71/71 |
| p95 frame | 8,6078 ms | 8,8098 ms |
| stdout/stderr | vides | vides |

L'overlay et le ledger sont échantillonnés à des frames différentes ; leur faible variation est donc
attendue. Les deux PNG ont été inspectés à leur résolution originale et rendent les valeurs lisibles.
Le fallback `Opaque pass: CPU unavailable | GPU unavailable` est couvert par test. Le test vérifie
aussi que les projections destinées uniquement à l'overlay ne dupliquent aucun champ dans le schéma
v8 : les valeurs restent sérialisées dans `render_diagnostics`.

Les artefacts, hashes et validation sont sous
[`native/afb270c328b7095a84d452b93b9fdd38e2737f33/overlay`](native/afb270c328b7095a84d452b93b9fdd38e2737f33/overlay).
Le compteur global de draw calls reste explicitement indisponible ; cette preuve ferme `R1-034`, pas
`R1-037`.

## Contrat de mesure candidat

Une mesure Visual Valley gouvernée n'est sérialisée que si toutes les conditions suivantes sont
réunies :

- le runtime a chargé la composition complète terrain + végétation + environnement + eau + météo +
  qualité, puis expose aussi les projections ombres et décor CC0 attendues ; une composition terrain
  seule conserve un rapport partiel non gouverné et ne peut pas se faire passer pour la preuve Gate 1 ;
- le cache de pipelines a déjà convergé et reste prêt pendant toute la fenêtre ;
- le tier qualité courant est effectivement appliqué ;
- la projection scenery CC0 est activée, complète et sans repli procédural ;
- sa plage LOD correspond au preset qualité appliqué ;
- la fenêtre contient au minimum **120 échantillons de frame après le warm-up configuré**.

Le validateur gouverné refuse l'écriture si l'une des huit projections terrain, ombres, végétation,
décor CC0, environnement, eau, météo ou qualité est absente ou incohérente. Il contrôle notamment
les fingerprints terrain croisés avec la végétation et l'eau, les identités de documents présentes,
les 34 champs du ledger qualité, les LOD décor/qualité et les cascades d'ombres du tier appliqué.

Pendant la fenêtre native, une empreinte d'état suit le tier courant/appliqué et l'état complet du
décor (activation, modèles chargés/échoués, substitutions, instances et plage LOD). Elle doit être
stable au début puis durant toute la mesure ; sinon le ledger gouverné est refusé. Dans Studio, le
manifeste porte les huit hashes canoniques de source et doit correspondre à l'observation runtime. Une
empreinte séparée exige la convergence tier/LOD ; toute transition ou divergence remet à zéro les 60
frames de warm-up et les 120 échantillons au lieu de mélanger deux configurations.

Ces fingerprints sont des garde-fous d'identité et de stabilité CPU/ECS. Ils ne prouvent pas que la
composition a été présentée par le GPU, que les shaders ont été exécutés ou que les pixels sont
corrects.

Le ledger natif collecte un échantillon positif et fini au plus par frame. Son bloc `frame` contient
la durée, la moyenne, p50, p95, p99, le maximum, les FPS moyens, le 1 % low, les hitches et
l'histogramme. Le 1 % low est calculé à partir de la moyenne des
`ceil(nombre_d'échantillons / 100)` frames les plus lentes. Le p95 et le p99 utilisent le rang le
plus proche.

L'histogramme est exhaustif et non recouvrant :

| Bucket | Sémantique |
|---|---|
| `frames_at_or_below_16_7_ms` | frame `<= 16,7 ms` |
| `frames_over_16_7_ms_at_or_below_33_3_ms` | `16,7 ms < frame <= 33,3 ms` |
| `frames_over_33_3_ms` | frame `> 33,3 ms` |

Le collecteur Studio partage les mêmes formules moteur-neutres. Le smoke Browser `d9e2670…` attend
la readiness de la scène, ignore 60 frames et en mesure exactement 120 pour clear puis rain. Cette
exécution acquiert les distributions de la boucle Bevy WASM ; elle ne mesure pas un timestamp GPU.
Le natif rejoué à `d9e2670…` fournit la paire exact-commit de données courante.

## Cartographie R1-034 à R1-043

| Route | Instrumentation réellement présente dans le candidat | Limite ou preuve encore requise | Statut |
|---|---|---|---|
| `R1-034` overlay CPU/GPU | commit `afb270c3` : deux PNG inspectés affichent frame, CPU opaque et GPU opaque issus des diagnostics Bevy réels ; fallback indisponible testé | le temps du pass opaque n'est pas un temps GPU frame entière et ne remplace pas `R1-037` | **ACQUIS** |
| `R1-035` frame histogram | natif et Browser clear/rain `120/0/0`, sommes exactement égales à 120 | paire exact-commit acquise, mais sans parcours | **SOUS-PREUVES NATIVE ET BROWSER ACQUISES** |
| `R1-036` moyenne, p95, 1 % low | natif clear : 119,792 FPS / 8,713 ms / 113,775 FPS ; natif rain : 120,598 / 9,095 / 96,354 ; loop WASM clear : 10,1175 ms / 12,1 ms / 77,519 FPS ; rain : 9,5633 / 10,9 / 85,106 | fenêtres courtes et caméra fixe ; la loop WASM n'est pas un temps GPU | **SOUS-PREUVES NATIVE ET BROWSER ACQUISES** |
| `R1-037` draw calls, triangles, instances | triangles logiques du terrain, comptes de meshes source et projections ECS végétation/scenery ; primitives/invocations opaques quand le backend les expose | `gpu_draw_calls = null` avec statut `unavailable_bevy_0_19_no_global_counter` ; aucun total exhaustif whole-scene de triangles ou instances réellement soumis au GPU | **NON QUALIFIÉ** |
| `R1-038` VRAM estimée | rapports allocateur Vulkan observés : 530 273 124/876 609 552 octets clear et 534 407 108/876 609 552 rain ; slabs 54 042 544 octets | sous-allocations seulement, pas la VRAM totale, le budget OS ou la résidence ; Browser doit rester `null` sans substitut | **SOUS-PREUVE NATIVE ACQUISE, ROUTE OUVERTE** |
| `R1-039` préchauffage pipelines | matrice native `5eadec7f` : 10/10 runs, de 59/59 à 74/74 prêts avant et pendant les 120 samples | le Browser n'expose toujours pas la convergence du cache et aucun cache persistant n'est revendiqué | **ACQUIS EN NATIF** |
| `R1-040` profil dynamique | matrice `5eadec7f` : quatre tiers fixes clear/rain, adaptation High vers Ultra, benchmark High, eau et LOD concordants | présentation Browser multi-tier et acceptation artistique non revendiquées | **ACQUIS FONCTIONNELLEMENT** |
| `R1-041` benchmark RTX 3060 ou RX 6600 | le ledger v8 peut recevoir la mesure 1080p High et l'identité de l'adaptateur | aucun lancement sur RTX 3060 12 Go ou RX 6600 8 Go ; objectifs 60 FPS moyen, p95 `<= 16,7 ms`, 1 % low `>= 45 FPS` non mesurés | **NON QUALIFIÉ** |
| `R1-042` benchmark RX 7800 XT | deux mesures v8 du candidat exact sur RX 7800 XT/Vulkan, 1080p High fixe, clear/rain et VSync off | benchmark court et fixe ; ne vaut ni cible milieu de gamme ni parcours représentatif | **SOUS-PREUVE DE DÉVELOPPEMENT ACQUISE** |
| `R1-043` comparaison native vs Studio WebGPU | natif et Browser `d9e2670…`, clear/rain, 46/46 assertions et sept hashes communs MATCH, dont scenery `6b41ec73…` | loop WASM non GPU, `gpu_presentation=not_observed` et hash contrôleur non répété dans le ledger natif | **PASS AUTOMATISÉ LOCAL BORNÉ** |

## Frontières des compteurs de rendu

Les champs disponibles doivent garder leur sens exact :

- `visual_valley.triangles` décrit les triangles dérivés du terrain Forge, pas tous les triangles
  réellement rendus dans la frame ;
- `source_mesh_instances` décrit les meshes matérialisés depuis les documents, pas des draw calls ;
- les comptes végétation et scenery décrivent une projection CPU/ECS et ses catégories de visibilité,
  pas des instances effectivement soumises après toutes les étapes du renderer ;
- `clipper_primitives_out`, les invocations vertex/fragment et les spans render sont des diagnostics
  de pipeline, jamais des substituts au nombre de draw calls ;
- sans instrumentation renderer exhaustive ou capture GPU reproductible, `R1-037` ne peut pas être
  fermé. L'exception Gate 0 d'ADR-0004 n'est pas étendue à cette route Gate 1.

## Mémoire GPU et pipelines

Le bloc `gpu_memory` distingue trois états : rapport allocateur observé, rapport indisponible malgré
un périphérique présent, ou périphérique absent. Une valeur indisponible reste `null`, jamais zéro.
La capacité des slabs mesh ne couvre notamment pas les textures, render targets, buffers de staging,
pipelines ou allocations du pilote. Elle ne doit donc pas être présentée comme « VRAM utilisée ».

Le bloc `pipeline_warmup` qualifie seulement les pipelines render/compute déjà rencontrés dans le
processus. Pour atteindre `ready`, le total doit être non nul, stable, entièrement prêt et sans
pipeline queued, creating, waiting ou en erreur pendant 30 frames Render consécutives. Une variante
météo, matériau ou qualité non exercée reste hors preuve. La mesure native finale doit démarrer après
cette convergence et être invalidée si la qualité, le LOD scenery ou le cache change pendant la
fenêtre.

## Matrice de qualification finale à exécuter

| Exécution sur le candidat intégré exact | Résultat actuel |
|---|---|
| Runtime natif, 1920 x 1080, High fixe, clear, VSync désactivée, ledger v8 avec au moins 120 samples qualifiés | **ACQUIS — 120 samples, artefacts hashés** |
| Runtime natif, même protocole sous rain | **ACQUIS — 120 samples, artefacts hashés** |
| Runtime natif, Low/Medium/High/Ultra puis adaptation bornée | **ACQUIS À `5eadec7f…`** — huit runs fixes clear/rain, adaptation High vers Ultra et benchmark High |
| Benchmark de développement RX 7800 XT, mêmes scène/profil/résolution | **ACQUIS — protocole court à caméra fixe** |
| Benchmark cible RTX 3060 12 Go ou RX 6600 8 Go, mêmes scène/profil/résolution | **NOT RUN** |
| Studio Browser, Visual Valley High, clear puis rain, 60 + 120 frames | **ACQUIS À `d9e2670…`** — 24/24 contrôles chacun, captures et JSON versionnés ; présentation GPU non observée |
| Comparaison native/Studio des distributions et de la parité de données sur conditions équivalentes | **ACQUISE À `d9e2670…`** — 46/46 assertions et sept hashes communs MATCH ; présentation GPU Browser non observée |

Les deux profils matériels répondent à des questions différentes : la RX 7800 XT détecte les
régressions sur la machine de développement (`R1-042`), tandis que seule une RTX 3060 ou RX 6600
satisfait la cible milieu de gamme de `R1-041`. Un succès RX 7800 XT ne peut pas être extrapolé à la
machine cible.

## Verdict de ce document

Le natif et le Browser `d9e2670…` possèdent chacun leurs preuves clear/rain. Les 46/46 assertions
croisées ferment `R1-043`. L'overlay exact `afb270c3` ferme `R1-034` ; `R1-037` et `R1-041`
restent non qualifiées. La matrice native `5eadec7f` ferme `R1-039` et `R1-040` sans
inventer de présentation Browser multi-tier. Le GPU milieu de gamme et l'acceptation visuelle restent
dus. Les résidus visuels, `gpu_presentation=not_observed` et les fenêtres courtes interdisent un verdict
global Gate 1.

Références détaillées :

- `docs/evidence/gate1/R1_038_039_GPU_MEMORY_PIPELINE_WARMUP.md` ;
- `docs/evidence/gate1/R1_040_QUALITY_RUNTIME_ADAPTER.md` ;
- `docs/evidence/gate1/R1_043_NATIVE_STUDIO_EXACT_COMMIT.md`.
