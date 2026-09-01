# R1-034 à R1-043 — instrumentation performance et parité native/Studio

- Statut : **GARDE-FOUS INTÉGRÉS ET VALIDÉS PAR TESTS/COMPILATION ; QUALIFICATION FINALE NON EXÉCUTÉE**
- État inspecté lors de la préparation : `HEAD` détaché
  `12e087b4a597097749a53cd6d635382cf2c36065`, complété par le diff d'intégration courant
- Contrat natif candidat : `forge.runtime-metrics` version 8
- Contrat de scène Studio candidat : `forge.studio-scene-telemetry` version 7
- Roadmap : `R1-034` à `R1-043` restent ouverts

Ce rapport cartographie ce que le candidat sait réellement observer. Il ne réutilise pas les anciens
smokes RX 7800 XT comme preuve de l'état intégré actuel et n'attribue aucun résultat de benchmark à
un code non encore identifié par un commit final.

## Validation automatisée de l'état intégré courant

| Validation intégrée | Résultat exact consigné |
|---|---|
| tests `forge_engine` | **117/117 PASS** |
| tests `forge_runtime` | **30/30 PASS** |
| tests `forge_studio` | **29/29 PASS** |
| check de compilation WASM exact courant | **PASS — 6.83 s** |
| `cargo fmt --all -- --check` | **PASS** |
| `git diff --check` | **PASS** |
| Clippy strict exact de l'état combiné | **PASS — 11.27 s** |
| smoke GPU natif, capture finale et lancement Browser WebGPU final | **NOT RUN** |

La campagne intégrée valide les contrats automatisés et la compilation WASM. Elle n'est ni un
benchmark, ni un smoke du renderer, ni une capture, ni une observation de présentation GPU/WebGPU.

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

Le collecteur Studio partage les mêmes formules moteur-neutres. Il attend la readiness de la scène,
ignore ensuite 60 frames et en mesure exactement 120. Cette proximité de contrat rend une
comparaison possible ; elle n'en constitue pas encore l'exécution.

## Cartographie R1-034 à R1-043

| Route | Instrumentation réellement présente dans le candidat | Limite ou preuve encore requise | Statut |
|---|---|---|---|
| `R1-034` overlay CPU/GPU | overlay natif avec adaptateur, FPS et frame time lissés ; statistiques opaques Bevy lorsqu'elles existent ; ledger séparé des diagnostics `render/*`, dont les spans CPU exposés | aucun timestamp de frame GPU n'est affiché ; primitives et invocations shader ne sont pas un temps GPU ; inspection d'un lancement réel encore absente | **PARTIEL, NON QUALIFIÉ** |
| `R1-035` frame histogram | histogramme partagé à trois buckets, somme exigée égale au nombre d'échantillons | aucun ledger v8 final du candidat intégré n'a été produit | **INSTRUMENTÉ, NOT RUN** |
| `R1-036` moyenne, p95, 1 % low | moyenne ms/FPS, p50, p95, p99, maximum, 1 % low et hitches ; minimum gouverné de 120 samples post-warm-up sur empreinte stable | aucun benchmark final à scène, résolution et profil verrouillés | **INSTRUMENTÉ, NOT RUN** |
| `R1-037` draw calls, triangles, instances | triangles logiques du terrain, comptes de meshes source et projections ECS végétation/scenery ; primitives/invocations opaques quand le backend les expose | `gpu_draw_calls = null` avec statut `unavailable_bevy_0_19_no_global_counter` ; aucun total exhaustif whole-scene de triangles ou instances réellement soumis au GPU | **NON QUALIFIÉ** |
| `R1-038` VRAM estimée | sous-allocations wgpu `allocated`/`reserved` optionnelles et capacité des slabs mesh Bevy, avec rejet de `allocated > reserved` | ni VRAM physique totale, ni budget OS, ni résidence globale ; observation native DX12/Vulkan réelle encore absente ; Browser WebGPU doit rester sans faux substitut | **INSTRUMENTÉ, NOT RUN** |
| `R1-039` préchauffage pipelines | compte `queued/creating/ready/errors/waiting`, convergence après 30 frames Render stables et rejet si le cache cesse d'être prêt pendant la mesure | seulement les variantes exercées dans le processus ; aucun cache persistant ; clear/rain et tiers qualité non rejoués sur l'intégration finale | **INSTRUMENTÉ, NOT RUN** |
| `R1-040` profil dynamique | sélection Low/Medium/High/Ultra, modes fixe/adaptatif/benchmark, tier réellement appliqué et concordance du LOD scenery ; le mode benchmark reste High fixe | plusieurs budgets sont explicitement appliqués seulement au démarrage ; transitions, rendu GPU et qualité visuelle du candidat combiné non exercés | **CANDIDAT INTÉGRÉ, NOT RUN** |
| `R1-041` benchmark RTX 3060 ou RX 6600 | le ledger v8 peut recevoir la mesure 1080p High et l'identité de l'adaptateur | aucun lancement sur RTX 3060 12 Go ou RX 6600 8 Go ; objectifs 60 FPS moyen, p95 `<= 16,7 ms`, 1 % low `>= 45 FPS` non mesurés | **NON QUALIFIÉ** |
| `R1-042` benchmark RX 7800 XT | la machine de développement documentée par les preuves précédentes est une RX 7800 XT/Vulkan | les smokes historiques portent sur des tranches antérieures ; aucun benchmark du candidat v8 intégré exact n'a été exécuté | **NOT RUN, NON QUALIFIÉ** |
| `R1-043` comparaison native vs Studio WebGPU | calcul de distribution partagé ; Studio collecte 60 frames de warm-up puis 120 frames après readiness, avec huit hashes de source et remise à zéro sur changement de l'empreinte tier/LOD | aucune paire native/Browser WebGPU à scène, météo, tier, résolution et fenêtre équivalents ; Studio mesure la boucle Bevy WASM, pas un timestamp GPU, et sa readiness CPU/ECS n'est pas une preuve de présentation GPU | **NON QUALIFIÉ** |

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
| Runtime natif, 1920 x 1080, High fixe, clear, VSync désactivée, ledger v8 avec au moins 120 samples qualifiés | **NOT RUN** |
| Runtime natif, même protocole sous rain | **NOT RUN** |
| Runtime natif, Low/Medium/High/Ultra puis adaptation bornée | **NOT RUN** |
| Benchmark de développement RX 7800 XT, mêmes scène/profil/résolution | **NOT RUN** |
| Benchmark cible RTX 3060 12 Go ou RX 6600 8 Go, mêmes scène/profil/résolution | **NOT RUN** |
| Studio Browser WebGPU, Visual Valley High, clear puis rain, 60 + 120 frames | **NOT RUN** |
| Comparaison native/Studio des distributions et de la parité de données sur conditions équivalentes | **NOT RUN** |

Les deux profils matériels répondent à des questions différentes : la RX 7800 XT détecte les
régressions sur la machine de développement (`R1-042`), tandis que seule une RTX 3060 ou RX 6600
satisfait la cible milieu de gamme de `R1-041`. Un succès RX 7800 XT ne peut pas être extrapolé à la
machine cible.

## Verdict de ce document

Le candidat fournit les contrats nécessaires pour produire une preuve performance plus fiable. Les
117 tests moteur, 30 tests runtime, 29 tests Studio, le check WASM, le format et le contrôle de diff
passent sur l'état intégré courant ; Clippy strict passe en 11.27 s et le check WASM exact courant en
6.83 s. Les smokes GPU natifs et Browser WebGPU restent **NOT RUN** ; aucune capture ni aucun
benchmark n'est attribué ici à cette intégration. `R1-037`, `R1-041` et `R1-043` sont explicitement
non qualifiés ; toutes les routes `R1-034` à `R1-043` restent ouvertes jusqu'aux exécutions réelles,
à l'inspection des artefacts et, pour l'overlay, à la couverture honnête du temps GPU manquant.

Références détaillées :

- `docs/evidence/gate1/R1_038_039_GPU_MEMORY_PIPELINE_WARMUP.md` ;
- `docs/evidence/gate1/R1_040_QUALITY_RUNTIME_ADAPTER.md`.
