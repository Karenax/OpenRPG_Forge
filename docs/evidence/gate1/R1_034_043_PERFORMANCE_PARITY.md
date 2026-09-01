# R1-034 à R1-043 — instrumentation performance et parité native/Studio

- Statut : **SMOKES NATIFS ET BROWSER CLEAR/RAIN ACQUIS SUR LEUR COMMIT EXACT ; PARITÉ
  NATIVE/STUDIO, GPU CIBLE ET QUALIFICATION VISUELLE FINALE OUVERTES**
- État natif exécuté (`HEAD` au moment des smokes) :
  `92286910a9bab622d5d54905551c0f03172e98e7`,
  arbre `6d7cd4ab8612686e7f73f01abeb5b8ad0174ebbe`
- Contrat natif candidat : `forge.runtime-metrics` version 8
- Contrat de scène Studio candidat : `forge.studio-scene-telemetry` version 7
- Roadmap : non modifiée par cette preuve ; les sous-preuves acquises sont détaillées ci-dessous

Ce rapport cartographie ce que le candidat sait réellement observer. Les résultats natifs ci-dessous
proviennent exclusivement du commit exact indiqué et ne réutilisent pas les anciens smokes RX 7800 XT.

## Validation automatisée de l'état intégré courant

| Validation intégrée | Résultat exact consigné |
|---|---|
| tests `forge_engine` | **119/119 PASS** |
| tests `forge_runtime` | **32/32 PASS** (31 bin + 1 intégration) |
| tests `forge_studio` | **31/31 PASS** (28 lib + 3 bin) |
| check de compilation WASM exact courant | **PASS — 1.06 s** |
| `cargo fmt --all -- --check` | **PASS** |
| `git diff --check` | **PASS** |
| Clippy strict exact de l'état combiné | **PASS — 12.04 s** |
| smoke GPU natif High fixe, `clear` puis `rain` | **ARTEFACTS PRODUITS ET AUDITÉS** — code de sortie non conservé |
| smoke Browser clear/rain High | **PASS AUTOMATISÉ BORNÉ** au commit `a34596bd…` — 24/24 contrôles chacun ; voir [`R1_BROWSER_WEBGPU_SMOKE.md`](R1_BROWSER_WEBGPU_SMOKE.md) |

La campagne automatisée valide les contrats et la compilation WASM. Les deux exécutions natives
séparées ajoutent une observation Vulkan et des captures. Le smoke Browser charge 9/9 modèles et
26/26 assets avec HTTP 200, sans requête `.meta`, échec réseau ni entrée console ; sa parité
`source_manifest_matched` est interne au Studio. Il conserve `gpu_presentation=not_observed` et ne
constitue ni une paire native/Studio exact-commit ni une acceptation visuelle finale.

## Exécutions natives exactes et artefacts

Les deux processus ont lancé directement
`E:\OpenRPG_Forge\.cargo-target-gate1-combined\release\forge_runtime.exe`, depuis
`E:\OpenRPG_Forge\.gate1-candidate`, avec
`BEVY_ASSET_ROOT=E:\OpenRPG_Forge\.gate1-candidate`. Le transcript shell original n'est pas conservé
dans le dossier de preuve. Les lignes suivantes sont donc une **reconstruction fidèle du protocole
effectivement exécuté**, et non un transcript cryptographiquement lié aux fichiers :

```powershell
& 'E:\OpenRPG_Forge\.cargo-target-gate1-combined\release\forge_runtime.exe' `
  --visual-valley --player-camera --weather clear --quality high `
  --capture 'E:\OpenRPG_Forge\.gate1-native-smoke-artifacts\92286910a9bab622d5d54905551c0f03172e98e7\visual-valley-clear-high.png' `
  --capture-frame 240 `
  --metrics 'E:\OpenRPG_Forge\.gate1-native-smoke-artifacts\92286910a9bab622d5d54905551c0f03172e98e7\visual-valley-clear-high.metrics.json' `
  --metrics-warmup-frames 300 --exit-after-frames 420 --no-vsync 2>&1 |
  Tee-Object 'E:\OpenRPG_Forge\.gate1-native-smoke-artifacts\92286910a9bab622d5d54905551c0f03172e98e7\visual-valley-clear-high.log'

& 'E:\OpenRPG_Forge\.cargo-target-gate1-combined\release\forge_runtime.exe' `
  --visual-valley --player-camera --weather rain --quality high `
  --capture 'E:\OpenRPG_Forge\.gate1-native-smoke-artifacts\92286910a9bab622d5d54905551c0f03172e98e7\visual-valley-rain-high.png' `
  --capture-frame 240 `
  --metrics 'E:\OpenRPG_Forge\.gate1-native-smoke-artifacts\92286910a9bab622d5d54905551c0f03172e98e7\visual-valley-rain-high.metrics.json' `
  --metrics-warmup-frames 300 --exit-after-frames 420 --no-vsync 2>&1 |
  Tee-Object 'E:\OpenRPG_Forge\.gate1-native-smoke-artifacts\92286910a9bab622d5d54905551c0f03172e98e7\visual-valley-rain-high.log'
```

La résolution 1920 x 1080 vient de la fenêtre runtime et est attestée par les deux JSON et les deux
PNG ; aucun argument de taille n'a été passé. Manifest local recalculé en SHA-256 :

| Artefact | Octets | SHA-256 |
|---|---:|---|
| `visual-valley-clear-high.log` | 913 | `EA59E3FB006F5066E2751FE7CBACDC1CC3D52F25C3B61BF69BEE54C196B4DF32` |
| `visual-valley-clear-high.metrics.json` | 28 728 | `09A6324A5668B5824816620A095CBE67B2BB172E3D7D757815D8306D7EABD6F3` |
| `visual-valley-clear-high.png` | 2 222 085 | `5B458CAFC64A498C4BE552C888D74DFA795FD2FE1883E2ADC4EE6D17F285F122` |
| `visual-valley-rain-high.log` | 913 | `648E30CA689EEA098E158BC583EB9FC1160F9617CF589809198A90A04E4F9033` |
| `visual-valley-rain-high.metrics.json` | 29 465 | `F231A9ED74C7C3E0EDA97371B5374E7AE9107CFF2E896D14372887B562754385` |
| `visual-valley-rain-high.png` | 2 223 457 | `6BF73A6146C46266CE0B2427B3D4F50C3C6AF9FFBC3D80965EF107B966FAA72D` |

Le nom du dossier porte le SHA Git exécuté et ses fichiers sont cohérents avec cette exécution, mais ni les
JSON ni un manifeste signé n'embarquent le commit, la commande, le hash du binaire et le code de
sortie. La relation entre protocole, commit et fichiers est donc documentaire, pas cryptographique.
Une révision Git ultérieure n'est pas couverte par ces artefacts sans preuve distincte.

## Métriques natives observées

Les deux ledgers sont `forge.runtime-metrics` v8 et portent le même hash source/terrain
`9f16780a1de1e9dd4b65931624a8b45281713829434292bada320a578420b2ef`. Ils identifient une
`AMD Radeon RX 7800 XT`, le backend Vulkan et le pilote AMD propriétaire `26.8.1 (LLPC)`.

| Mesure 1080p High, VSync off | Clear | Rain |
|---|---:|---:|
| échantillons / durée | 120 / 935,183 ms | 120 / 935,592 ms |
| moyenne | 7,793 ms / 128,317 FPS | 7,797 ms / 128,261 FPS |
| p50 / p95 / p99 | 7,742 / 8,367 / 8,597 ms | 7,796 / 8,161 / 8,346 ms |
| maximum | 9,163 ms | 8,412 ms |
| 1 % low | 112,611 FPS | 119,348 FPS |
| hitches `>16,7 ms` / `>33,3 ms` | 0 / 0 | 0 / 0 |
| histogramme `<=16,7` / `16,7..33,3` / `>33,3` | 120 / 0 / 0 | 120 / 0 / 0 |

Les ledgers ne contiennent pas les 120 échantillons bruts : les percentiles ne sont donc pas
recalculables indépendamment à partir des seuls artefacts. La fenêtre mesurée dure moins d'une
seconde, à caméra fixe ; elle ne prouve pas un parcours long, le streaming ou l'absence de stutter en
déplacement.

Chaque log contient exactement un événement `WARN` et aucun `ERROR`. Le warning
`wgpu_core::validation` indique qu'un vertex shader écrit `@builtin(position)` sans attribut
`@invariant` alors que le pipeline emploie une comparaison de profondeur `Equal`, avec risque
d'artefacts sur certaines machines. Les logs ne sont donc pas « clean ».

L'inspection des PNG confirme une vue claire et une vue pluie, mais montre des motifs noirs
stippled/jagged sur le terrain, la végétation et les silhouettes. La pluie est visible, tandis que
l'eau, la SSR et la réfraction ne sont pas clairement inspectables. Une comparaison RGB pixel à
pixel trouve au moins un canal différent pour 96,12 % des pixels entre clear et rain, ce qui signale
un changement colorimétrique global important à revoir. Le warning et ces observations ne prouvent
pas une cause unique, mais interdisent une acceptation visuelle finale sur ces seules captures.

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

Le collecteur Studio partage les mêmes formules moteur-neutres. Le smoke Browser `a34596bd…` attend
la readiness de la scène, ignore 60 frames et en mesure exactement 120 pour clear puis rain. Cette
exécution acquiert les distributions de la boucle Bevy WASM ; elle ne mesure pas un timestamp GPU et
ne forme pas encore une comparaison exact-commit avec le natif `92286910…`.

## Cartographie R1-034 à R1-043

| Route | Instrumentation réellement présente dans le candidat | Limite ou preuve encore requise | Statut |
|---|---|---|---|
| `R1-034` overlay CPU/GPU | overlay natif inspecté dans les deux PNG avec adaptateur, FPS, frame time et statistiques opaques | aucun timestamp de frame GPU affiché ; primitives et invocations shader ne sont pas un temps GPU | **OBSERVÉ, PARTIEL** |
| `R1-035` frame histogram | natif clear/rain `120/0/0` ; Browser clear `118/2/0`, rain `120/0/0`, sommes exactement égales à 120 | distributions sur commits différents, sans parcours | **SOUS-PREUVES NATIVE ET BROWSER ACQUISES** |
| `R1-036` moyenne, p95, 1 % low | natif clear : 128,317 FPS / 8,367 ms / 112,611 FPS ; natif rain : 128,261 / 8,161 / 119,348 ; loop WASM clear : 10,7283 ms / 13,4 ms / 54,945 FPS ; rain : 9,1683 / 10,1 / 84,034 | fenêtres courtes et caméra fixe ; la loop WASM n'est pas un temps GPU | **SOUS-PREUVES NATIVE ET BROWSER ACQUISES** |
| `R1-037` draw calls, triangles, instances | triangles logiques du terrain, comptes de meshes source et projections ECS végétation/scenery ; primitives/invocations opaques quand le backend les expose | `gpu_draw_calls = null` avec statut `unavailable_bevy_0_19_no_global_counter` ; aucun total exhaustif whole-scene de triangles ou instances réellement soumis au GPU | **NON QUALIFIÉ** |
| `R1-038` VRAM estimée | rapports allocateur Vulkan observés : 561 396 832/876 609 552 octets clear et 565 656 524/876 609 552 rain ; slabs 54 042 544 octets | sous-allocations seulement, pas la VRAM totale, le budget OS ou la résidence ; Browser doit rester `null` sans substitut | **SOUS-PREUVE NATIVE ACQUISE, ROUTE OUVERTE** |
| `R1-039` préchauffage pipelines | natif clear 68/68 et rain 69/69 prêts, zéro queued/creating/error/waiting et 177 frames stables | le Browser atteint la readiness mais n'expose pas la convergence du cache pipeline ; aucun cache persistant ni autres tiers | **SOUS-PREUVE HIGH NATIVE ACQUISE, ROUTE OUVERTE** |
| `R1-040` profil dynamique | High fixe sélectionné/courant/appliqué en natif et Browser ; LOD scenery 72–180 m concordant | adaptation désactivée, zéro transition, aucun Low/Medium/Ultra/adaptatif et limites visuelles présentes | **SOUS-PREUVE HIGH FIXE ACQUISE, ROUTE OUVERTE** |
| `R1-041` benchmark RTX 3060 ou RX 6600 | le ledger v8 peut recevoir la mesure 1080p High et l'identité de l'adaptateur | aucun lancement sur RTX 3060 12 Go ou RX 6600 8 Go ; objectifs 60 FPS moyen, p95 `<= 16,7 ms`, 1 % low `>= 45 FPS` non mesurés | **NON QUALIFIÉ** |
| `R1-042` benchmark RX 7800 XT | deux mesures v8 du candidat exact sur RX 7800 XT/Vulkan, 1080p High fixe, clear/rain et VSync off | benchmark court et fixe ; ne vaut ni cible milieu de gamme ni parcours représentatif | **SOUS-PREUVE DE DÉVELOPPEMENT ACQUISE** |
| `R1-043` comparaison native vs Studio WebGPU | Browser `a34596bd…` : clear/rain 24/24, huit hashes alignés en interne, 9/9 modèles, 60 + 120 frames | natif Release/perf à `92286910…`, Browser à `a34596bd…`; le correctif a changé le hash décor `6b142441…` vers `6b41ec73…`; rejeu natif exact `a34596bd…` dû ; loop WASM non GPU et `gpu_presentation=not_observed` | **OUVERT — PAIRE EXACT-COMMIT ABSENTE** |

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
| Runtime natif, Low/Medium/High/Ultra puis adaptation bornée | **NOT RUN** |
| Benchmark de développement RX 7800 XT, mêmes scène/profil/résolution | **ACQUIS — protocole court à caméra fixe** |
| Benchmark cible RTX 3060 12 Go ou RX 6600 8 Go, mêmes scène/profil/résolution | **NOT RUN** |
| Studio Browser, Visual Valley High, clear puis rain, 60 + 120 frames | **ACQUIS À `a34596bd…`** — 24/24 contrôles chacun, captures et JSON versionnés ; présentation GPU non observée |
| Comparaison native/Studio des distributions et de la parité de données sur conditions équivalentes | **DUE — PAIRE EXACT-COMMIT ABSENTE** : natif `92286910…`, Browser `a34596bd…`; rejeu natif `a34596bd…` requis |

Les deux profils matériels répondent à des questions différentes : la RX 7800 XT détecte les
régressions sur la machine de développement (`R1-042`), tandis que seule une RTX 3060 ou RX 6600
satisfait la cible milieu de gamme de `R1-041`. Un succès RX 7800 XT ne peut pas être extrapolé à la
machine cible.

## Verdict de ce document

Le natif `92286910…` possède deux ledgers v8 et deux captures High clear/rain sur RX 7800 XT/Vulkan.
Le Browser `a34596bd…` ajoute deux captures, deux ledgers de scène et deux distributions de loop WASM
avec 24/24 contrôles chacune. `R1-034` reste partielle faute de temps GPU ; `R1-037`, `R1-041` et
`R1-043` restent non qualifiées ; `R1-039` et `R1-040` restent ouvertes faute de preuve pipeline
Browser, transitions et matrice de tiers. Le GPU milieu de gamme, le rejeu natif exact `a34596bd…`
et l'acceptation visuelle restent dus. Le warning natif wgpu, `gpu_presentation=not_observed`, les
commits différents et les fenêtres courtes interdisent un verdict global Gate 1.

Références détaillées :

- `docs/evidence/gate1/R1_038_039_GPU_MEMORY_PIPELINE_WARMUP.md` ;
- `docs/evidence/gate1/R1_040_QUALITY_RUNTIME_ADAPTER.md`.
