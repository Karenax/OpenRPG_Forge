# Gate 1 — cartographie de preuve R1-022 à R1-026

## Verdict borné

**NON QUALIFIÉ AU GLOBAL.** La paire `d9e2670…` conserve sa campagne native/Browser exacte,
24/24 contrôles par météo et 46/46 contrôles croisés. La campagne native la plus récente est liée au
commit exact `125ba0b8bb1fe04b2b34cae5abc41c26d1919b00`, arbre
`6d4639c824ccfa81c3d5ed911903cec2bb3c5074`. Son validateur schema 2 passe avec 29 PNG aux hashes
uniques, un MP4 de 24 frames/3 secondes, 75 pipelines prêts, zéro erreur de pipeline et zéro entrée
warning/erreur/panic dans les journaux runtime. Les vues `water-clear`, `water-rain` et
`water-restored` sont non vides, distinctes et cohérentes à l'inspection d'agent ; la pluie et le
retour clair y sont lisibles.

Cette acquisition ne transforme pas le fallback en réflexion/réfraction GPU qualifiée. Le ledger
conserve `weather_gpu_execution_observed=false`, le validateur conserve
`human_review_performed=false` et son statut exact reste
`automated_artifacts_valid_pending_stutter_human_and_gpu_qualification`.

État des cinq critères :

| Critère | État de cette preuve |
|---|---|
| `R1-022` eau de rivière | **acquis fonctionnellement** en natif et Browser : document/hash et composants observés ; trois vues natives dédiées `125ba0b` inspectables, vue Browser d9 encore masquée, `gpu_pass_observed=false` et `gpu_presentation=not_observed` |
| `R1-023` réflexion/réfraction selon profil | SSR indisponible résolu vers Environment IBL ; réfraction résolue vers Bevy `ScreenSpaceTransmission` High ; trois PNG eau exacts `125ba0b` inspectables, mais aucune observation instrumentée de l'exécution GPU ni acceptation humaine formelle |
| `R1-024` pluie | **acquise fonctionnellement** en natif et Browser High : cible rain, lot visible et stries dans les PNG ; animation et présentation GPU non prouvées |
| `R1-025` wetness réversible | séquence réelle même processus `clear → rain → clear`, endpoints CPU/ECS 0 → 0,78 → 0 et trois PNG exacts acquis ; exécution GPU instrumentée, qualification Browser et retour visuel humain accepté dus |
| `R1-026` bloom et color grading bornés | endpoints numériques clear/rain, présence du composant Bloom et trois PNG ciblés acquis ; exécution GPU instrumentée, matrice de profils et qualification visuelle humaine dues |

Les sous-preuves fonctionnelles de `R1-022` et `R1-024` sont acquises. La validation automatisée et
les pixels natifs ciblés de `R1-023`/`R1-025`/`R1-026` sont maintenant acquis, mais ces trois routes
restent ouvertes en raison de la présentation GPU non instrumentée, de la qualification Browser et
de l'acceptation visuelle humaine encore dues. Les sections historiques plus bas conservent les
campagnes précédentes. La correction d9 est détaillée dans
[`R1_GRAPHICS_FORWARD_IBL_CORRECTION.md`](R1_GRAPHICS_FORWARD_IBL_CORRECTION.md), la campagne
historique `a7c9b1e1` dans
[`R1_025_026_046_048_NATIVE_VISUAL_PROOF.md`](R1_025_026_046_048_NATIVE_VISUAL_PROOF.md) et
l'identité `125ba0b` dans le présent document ; ses artefacts bruts restent externes.

## Validation automatisée de l'état intégré courant

| Validation intégrée | Résultat exact consigné |
|---|---|
| tests `forge_engine` historiques d9 | **120/120 PASS** |
| tests workspace/all-targets historiques d9 | **296/296 PASS** |
| check de compilation WASM historique d9 | **PASS — 8,71 s** |
| `cargo fmt --all -- --check` historique d9 | **PASS** |
| `git diff --check` historique d9 | **PASS** |
| Clippy strict workspace/all-targets historique d9 | **PASS**, zéro warning |
| smoke GPU natif et captures `clear`/`rain` High | **PASS exact d9** — deux codes de sortie 0, logs sans warning/erreur |
| smoke/captures Browser clear/rain High | **PASS exact d9** — 24/24 contrôles chacun ; voir [`R1_BROWSER_WEBGPU_SMOKE.md`](R1_BROWSER_WEBGPU_SMOKE.md) |
| tests eau ciblés `forge_engine` | **PASS — 14/14** |
| tests `forge_runtime` après intégration RenderDoc v2 | **PASS — 67/67 unitaires + 1/1 intégration** |
| preuve visuelle native exacte `125ba0b` | **PASS automatisé** — 29 PNG uniques, MP4 24 frames/3 s, 75 pipelines prêts, logs runtime vides |

Ces résultats valident la compilation et les contrats automatisés intégrés. Les exécutions natives
ci-dessous ajoutent une observation Vulkan et des pixels capturés. Le Browser charge 9/9 modèles et
26/26 assets avec HTTP 200, sans requête `.meta`, échec réseau ni entrée console. Sa parité
`source_manifest_matched` est interne à l'exécution Studio ; `gpu_presentation` reste
`not_observed`, sans preuve exhaustive des shaders ou passes GPU.

## Campagne historique 92286910 — smoke natif clear/rain

La preuve vise le commit exact exécuté, `HEAD` au moment des smokes,
`92286910a9bab622d5d54905551c0f03172e98e7`, arbre
`6d7cd4ab8612686e7f73f01abeb5b8ad0174ebbe`. Le binaire
`E:\OpenRPG_Forge\.cargo-target-gate1-combined\release\forge_runtime.exe` a été lancé directement
depuis `E:\OpenRPG_Forge\.gate1-candidate`, avec `BEVY_ASSET_ROOT` sur ce worktree,
`--visual-valley --player-camera --weather clear|rain --quality high`, capture frame 240, warm-up
métriques 300, sortie bornée frame 420 et VSync désactivée. La résolution 1920 x 1080 est attestée
par les JSON et PNG, pas imposée par un argument.

Le transcript shell exact n'est pas conservé. La reconstruction fidèle des deux commandes figure
dans `docs/evidence/gate1/R1_034_043_PERFORMANCE_PARITY.md`. Le dossier local ne contient pas de
manifeste liant cryptographiquement commit, binaire, commande, code de sortie et artefacts.

| Artefact | SHA-256 |
|---|---|
| `visual-valley-clear-high.log` | `EA59E3FB006F5066E2751FE7CBACDC1CC3D52F25C3B61BF69BEE54C196B4DF32` |
| `visual-valley-clear-high.metrics.json` | `09A6324A5668B5824816620A095CBE67B2BB172E3D7D757815D8306D7EABD6F3` |
| `visual-valley-clear-high.png` | `5B458CAFC64A498C4BE552C888D74DFA795FD2FE1883E2ADC4EE6D17F285F122` |
| `visual-valley-rain-high.log` | `648E30CA689EEA098E158BC583EB9FC1160F9617CF589809198A90A04E4F9033` |
| `visual-valley-rain-high.metrics.json` | `F231A9ED74C7C3E0EDA97371B5374E7AE9107CFF2E896D14372887B562754385` |
| `visual-valley-rain-high.png` | `6BF73A6146C46266CE0B2427B3D4F50C3C6AF9FFBC3D80965EF107B966FAA72D` |

Les deux ledgers `forge.runtime-metrics` v8 portent le hash eau
`c581e9ef65ca1bc34d5f94908d5f43df77794da4ad5b97e1b3c4f8c0d025ebce`, le hash météo
`b0d8e12bd49d3510361d587463a3002e98741b68052b283fa3f8ca432c87dd9e` et le hash terrain
`9f16780a1de1e9dd4b65931624a8b45281713829434292bada320a578420b2ef`.

| Observation native High | Clear | Rain |
|---|---:|---:|
| eau : contrôles / samples / sommets / triangles | 62 / 137 / 959 / 1 088 | identique |
| winding positif / corrections / fallbacks tangente | 1 088 / 8 / 0 | identique |
| clearance min/max pour 0,14 m demandé | 0,13999939 / 0,6260648 m | identique |
| composants eau / berge / caméra SSR | 1 / 1 / 1 | 1 / 1 / 1 |
| `gpu_pass_observed` | `false` | `false` |
| gouttes demandées/générées / batches / entités par goutte | 8 192 / 8 192 / 1 / 0 | identique |
| pluie visible / opacité | `false` / 0 | `true` / 0,42 |
| wetness courante/cible | 0 / 0, restauration claire exacte | 0,78 / 0,78 |
| uniforms terrain/végétation concordants | oui / oui | oui / oui |
| exposition / température / tint | 0 / 0,08 / 0,01 | -0,35 / -0,12 / 0,02 |
| saturation / contraste / gamma | 1,04 / 1,06 / 1 | 0,86 / 1,08 / 1 |
| Bloom et color grading présents | oui / oui | oui / oui |

Le chemin eau résout High en SSR 48 pas sans fallback réflexion, mais l'intensité et la distance SSR
restent non mappées. La réfraction écran est indisponible et le fallback `surface_tint` de force 0,18
est actif. Les métriques eau gardent honnêtement `gpu_pass_observed=false`; les métriques météo
gardent `gpu_execution_observed=false` et `status=observed_cpu_projection`.

Chaque log contient exactement un warning `wgpu_core::validation` et aucun `ERROR` : un vertex shader
écrit `@builtin(position)` sans `@invariant` alors que le pipeline utilise `Equal`, ce qui peut créer
des artefacts selon la machine. Les captures montrent effectivement de nombreux motifs noirs
stippled/jagged, sans permettre d'en attribuer la cause. La pluie cyan est visible dans le PNG rain,
mais une image fixe ne prouve ni animation, ni suivi caméra temporel, ni impacts. L'eau, la SSR et la
réfraction ne sont pas clairement inspectables. Une comparaison RGB pixel à pixel trouve au moins
un canal différent pour 96,12 % des pixels entre clear et rain ; le changement ressemble donc à un
étalonnage global très fort plutôt qu'à une seule mouillure localisée.

## Sources et identités inspectées

- `forge.water` version 1 : `examples/visual_valley/water.v1.json`, hash canonique attendu
  `c581e9ef65ca1bc34d5f94908d5f43df77794da4ad5b97e1b3c4f8c0d025ebce` ;
- `forge.weather-profile` version 1 : `examples/visual_valley/weather.v1.json`, hash canonique
  attendu `b0d8e12bd49d3510361d587463a3002e98741b68052b283fa3f8ca432c87dd9e` ;
- profil qualité Visual Valley : `examples/visual_valley/quality-profile.v1.json`, qui pilote les
  budgets d'eau, de pluie et de post-traitement ;
- adapters partagés : `crates/forge_engine/src/water.rs`, `weather.rs` et `quality.rs` ;
- intégrations réelles : `apps/forge_runtime/src/main.rs`, `apps/forge_studio/src/lib.rs` et
  `apps/forge_studio/src/telemetry.rs`.

Les recettes `VisualValleyWaterRecipe` et `VisualValleyWeatherRecipe` sont originales, procédurales
et sans asset. Leurs tests négatifs interdisent notamment toute référence d'identité upstream,
texture ou asset. Aucun asset Cadle n'est requis ou copié par ce lot.

## Garde-fous intégrés de composition et d'identité

- le runtime n'active le mode de métriques Gate 1 gouvernées que lorsque la composition Visual
  Valley comprend simultanément végétation, environnement, eau, météo et qualité autour du terrain ;
- une fois ce mode actif, le ledger est refusé si l'une des projections terrain, ombres, végétation,
  décor CC0, environnement, eau, météo ou qualité manque ou demeure incohérente. Les hashes terrain
  portés par la végétation et l'eau doivent notamment correspondre au terrain observé ;
- le ledger natif exige au moins **120 échantillons de frame post-warm-up** et une empreinte d'état
  stable avant et pendant toute la fenêtre. Cette empreinte couvre le tier qualité appliqué ainsi que
  l'activation, les comptes, l'absence d'échec/repli et la plage LOD du décor ; tout changement
  invalide la mesure au lieu d'agréger deux compositions différentes ;
- le manifeste Studio expose les huit identités canoniques terrain, végétation, environnement,
  contrôleur joueur, eau, météo, décor et qualité, puis refuse une divergence entre sélection et
  observation runtime ;
- la fenêtre Studio attend 60 frames de warm-up puis 120 échantillons. Son empreinte exige un tier
  courant réellement appliqué et les mêmes bornes LOD côté qualité et décor ; une transition ou une
  empreinte non convergée réinitialise la fenêtre entière.

Ces garde-fous prouvent des cohérences CPU/ECS et empêchent un ledger partiel ou mélangé. Même après
le smoke natif avec capture, les champs `gpu_pass_observed`, `gpu_execution_observed` et
`gpu_presentation` restent volontairement négatifs ou non observés : ils ne doivent pas être inversés
par inférence depuis la seule existence d'un PNG.

## Matrice critères → API réelle → tests existants → preuve due

### R1-022 — eau de rivière

**API et projection réelles**

- `forge_project::WaterDocument` porte une source versionnée, moteur-neutre et bornée : route,
  surface, berges et intention optique.
- `forge_world::VisualValleyWaterRecipe::build` authorise 62 points de contrôle ;
  `sample_river_centerline` applique le contrat Catmull-Rom centripète déterministe et produit 137
  échantillons sur la fixture courante.
- `forge_engine::VisualValleyWaterPlugin` projette deux meshes dérivés : une surface à trois lanes
  et deux bandes de berge. Les tests attendent 411 sommets/544 triangles pour l'eau et 548
  sommets/544 triangles pour les berges, avec winding positif et dégagement terrain minimal de
  0,14 m.
- `VisualValleyWaterMaterial` est un matériau étendu Bevy opaque/forward. Le shader Forge
  `visual_valley_water.wgsl` consomme amplitude, longueur d'onde, vitesse, direction, profondeur et
  force des normales ; `WaterRuntimeState` projette une phase animée déterministe.
- `WaterProjectionStats` enregistre les hashes source/terrain, géométrie, clearance, uniforms,
  topologie caméra et composants ECS. Son champ `gpu_pass_observed` reste volontairement `false`.

**Tests concernés inventoriés dans le candidat**

- contrat : `versioned_json_round_trip_and_hash_are_deterministic`,
  `identity_and_geometry_bounds_are_strict`,
  `profile_relations_reject_unbounded_or_impossible_requests` ;
- recette : `embedded_fixture_matches_recipe_and_serializes_exactly`,
  `canonical_sampler_is_deterministic_bounded_and_endpoint_exact`,
  `authored_route_tracks_the_rendered_valley_and_requires_bank_projection` ;
- adapter : `real_sixty_two_control_route_projects_three_lanes_independently`,
  `projected_lanes_are_monotone_and_every_triangle_has_positive_winding`,
  `material_extension_maps_profile_and_keeps_forward_surface_opaque`,
  `camera_preparation_is_observed_without_claiming_gpu_execution`,
  `shader_contract_contains_profile_waves_depth_normals_and_deferred_output` et
  `runtime_phase_is_seeded_repeatable_wrapped_and_profile_driven`.

**Limites et preuve encore dues**

- la berge est une bande opaque de substitution : carving, blend authored, érosion et wet edge sont
  explicitement différés dans `WaterProjectionStats` ;
- l'opacité authored est enregistrée mais la surface reste opaque pour le chemin forward ;
- les captures natives 1920 × 1080 clear/rain historiques sont complétées par trois vues eau
  dédiées exactes `125ba0b`. Elles rendent le changement météo et le retour clair inspectables, mais
  ne prouvent ni le chemin GPU optique réellement exécuté, ni la stabilité temporelle, ni une
  acceptation humaine comparative ;
- le Browser clear/rain High confirme le même hash eau et le statut ECS
  `observed_main_world_components` dans les deux scene ledgers ; l'eau reste toutefois masquée par le
  cadrage et `gpu_presentation=not_observed`, donc une capture dédiée et une preuve GPU restent dues.

### R1-023 — réflexion et réfraction selon profil

**API et projection réelles**

- `WaterOpticsProfile` borne qualité, réflexion, réfraction et fallbacks. La fixture High demande
  SSR 48 pas, distance 96 m et épaisseur 0,9 m ; elle demande une réfraction écran 24 pas jusqu'à
  3,2 m, avec repli `surface_tint`.
- `WaterDocument::resolve_optics_path` distingue capacité `available`, `unavailable` et
  `not_qualified`, sans confondre sélection logique et exécution GPU.
- l'adapter déclare le SSR Bevy 0.19.1 indisponible sur le chemin forward courant. High résout donc la
  réflexion vers le fallback Environment IBL ; l'intensité authored 0,72 est mappée à la réflectance
  du matériau. Les 48 pas et la distance 96 m restent enregistrés mais non appliqués au fallback ;
- la réfraction écran est déclarée disponible via le chemin forward transmissif Bevy. Le matériau
  reçoit `specular_transmission=0,18` et une épaisseur uniforme bornée à 3,2 m ; la caméra reçoit
  `ScreenSpaceTransmission` High, soit 16 taps de flou et une copie de couche. Les 24 pas authored
  sont conservés mais ne sont pas assimilés au nombre de copies de couche Bevy ;
- l'adapter retire `ScreenSpaceReflections` et `DeferredPrepass`, conserve MSAA 4 et exige la source
  `AtmosphereEnvironmentMapLight`. Les composants, paramètres CPU et matériaux sont observés dans le
  main world, sans prétendre que le GPU a exécuté la réflexion ou la transmission ;
- le profil qualité conserve l'intention Low/Medium/High/Ultra et ne peut plus réactiver le SSR
  indisponible après un changement de tier.

**Tests concernés inventoriés dans le candidat**

- `optical_fallback_resolution_is_explicit_and_measurable` ;
- `requested_and_fallback_optics_are_observable` ;
- `metrics_resolve_optics_without_claiming_gpu_or_webgpu_execution` ;
- `tier_projection_is_reversible_and_stable_between_transitions` pour l'ajout/retrait du composant
  SSR lorsque disponible, plus `high_quality_transition_cannot_reenable_unavailable_ssr` pour le
  fallback courant.

**Limites et preuve encore dues**

- la source Environment IBL et la réflectance du matériau sont observées côté CPU/ECS, mais le
  filtrage de la carte d'environnement et son échantillonnage GPU ne le sont pas ;
- l'intensité de réflexion est mappée à la réflectance, mais les 48 pas et la distance SSR demandés ne
  s'appliquent pas au fallback Environment IBL ;
- la réfraction High configure bien `ScreenSpaceTransmission`, la transmission matériau, l'épaisseur
  et le preset 16 taps. Cette observation de composants n'est pas une preuve d'exécution GPU native
  ou WebGPU ;
- les trois vues eau exactes `125ba0b` sont acquises et cohérentes à l'inspection d'agent. Le ledger
  conserve toutefois explicitement l'exécution GPU à faux et aucune revue humaine formelle n'est
  enregistrée ; elles ne ferment donc pas la qualification ;
- capturer et comparer Low/Medium/High/Ultra en natif et WebGPU, vérifier visuellement la réflexion,
  le fallback de réfraction, les artefacts de bord et le coût ; décider explicitement si le critère
  accepte le fallback authored ou exige un vrai chemin de réfraction avant qualification.

### R1-024 — pluie

**API et projection réelles**

- `WeatherProfileDocument` et `RainProfile` bornent seed, volume, vitesse, vent, opacité et budgets
  Low/Medium/High/Ultra de 2 048/4 096/8 192/16 384 gouttes.
- `VisualValleyWeatherPlugin` construit un seul mesh déterministe et une seule entité
  `VisualValleyRainBatch`, jamais une entité ECS par goutte. En High, 8 192 gouttes correspondent à
  65 536 sommets et 32 768 triangles.
- `visual_valley_rain.wgsl` anime le lot dans un volume périodique suivant vitesse de chute et vent,
  avec positions précédente/courante pour le motion-vector prepass. Le lot suit l'unique caméra 3D
  active.
- le runtime expose `--weather clear|rain` uniquement avec Visual Valley ; le Studio expose le
  paramètre strict `forge_weather=clear|rain`. Les deux installent le même plugin moteur.
- `WeatherProjectionStats` observe lot, visibilité, suivi caméra et uniform d'animation dans le
  main world, mais conserve `gpu_execution_observed = false`.

**Tests concernés inventoriés dans le candidat**

- `high_profile_builds_one_deterministic_batch_without_drop_entities` ;
- `changing_only_the_seed_changes_geometry_but_not_budget` ;
- `initial_state_builder_contract_can_start_fully_clear_or_raining` ;
- `headless_plugin_projects_one_rain_batch_materials_and_camera_components` ;
- runtime : `weather_override_is_strict_and_requires_visual_valley` ;
- Studio : tests de parsing strict, de manifeste/hash météo et de mismatch runtime.

**Limites et preuve encore dues**

- collisions au sol, éclaboussures et occlusion de la pluie sont explicitement différées ;
- les endpoints natifs clear/rain sont acquis : le ledger cache/affiche le lot, la pluie est visible
  dans le PNG rain et les 120 frames mesurées ne contiennent aucun hitch `>16,7 ms` ; une capture fixe
  ne prouve toutefois ni animation continue ni centrage temporel sur la caméra ;
- le smoke Browser clear/rain High est acquis : 24/24 contrôles chacun, cible météo correcte, 9/9
  modèles, 26 assets HTTP 200 et console vide. Le ledger rain et la capture montrent la pluie, mais
  `gpu_presentation=not_observed` et une image fixe ne prouvent pas son animation continue ;
- inspecter la lisibilité de la pluie à hauteur humaine et son coût avant de cocher `R1-024`.

### R1-025 — wetness réversible

**API et projection réelles**

- `WeatherProfileDocument::advance_wetness` avance un scalaire borné vers 0,78 sous pluie à 0,18/s,
  puis vers 0 sous ciel clair à 0,07/s, avec clamp exact sur la cible.
- `project_wetness_materials` projette ce scalaire et les multiplicateurs authored (roughness 0,32,
  albedo 0,72 à pleine wetness) vers les matériaux terrain et végétation.
- `visual_valley_terrain.wgsl` et `visual_valley_vegetation_deform.wgsl` mélangent réellement albedo
  et roughness à partir de l'uniform ; une wetness nulle est l'identité exacte du rendu authored.
- `WeatherWetnessStats` enregistre cible, valeur, nombre de matériaux, égalité des uniforms et
  restauration exacte de la cible claire lorsqu'elle est atteinte.

**Tests concernés inventoriés dans le candidat**

- `clear_rain_clear_transition_is_bounded_and_reversible` ;
- `recipe_replays_and_clear_rain_clear_restores_the_exact_target` ;
- `fixed_steps_replay_and_restore_the_exact_clear_target` ;
- `wetness_uniform_is_reversible_without_mutating_authored_palettes` ;
- le test headless complet vérifie les uniforms terrain et végétation.

**Limites et preuve encore dues**

- pas encore de flaques, réaction de normal map ni wet edge de berge ;
- l'adapter qualité documente `rain.wetness_enabled` comme appliqué au démarrage, pas comme une
  reconstruction dynamique de matériaux à chaque changement de tier ;
- le harness exact `125ba0b` prouve une transition native réelle dans le même processus, le retour
  CPU/ECS exact 0 → 0,78 → 0 et trois PNG liés au ledger ;
- les trois PNG ont des hashes distincts. Cela prouve leur acquisition, pas une identité visuelle
  parfaite après restauration ; `weather_gpu_execution_observed=false` empêche d'étendre la preuve
  CPU/ECS au GPU ou au Browser ;
- vérifier par inspection humaine que terrain et végétation s'assombrissent et deviennent moins
  rugueux sans dérive permanente, puis rejouer la séquence sous WebGPU.

### R1-026 — bloom et color grading bornés

**API et projection réelles**

- `BloomProfile` borne intensité et fréquences dans `[0,1]`. La fixture demande intensité 0,08,
  boost basse fréquence 0,35 et high-pass 0,82.
- `ColorGradeProfile` borne exposition, température, tint, saturation, contraste et gamma. Les
  profils clear/rain sont interpolés par le facteur de précipitation et retrouvent exactement les
  valeurs terminales.
- `project_post_processing` ajoute ou retire les vrais composants Bevy `Bloom` et `ColorGrading` sur
  l'unique caméra active. Le profil qualité désactive Bloom en Low et l'active en
  Medium/High/Ultra ; le color grading reste activé dans les quatre presets courants.
- `WeatherPostProcessingStats` observe présence des composants et valeurs mappées dans le main
  world, sans en déduire leur présentation GPU.

**Tests concernés inventoriés dans le candidat**

- `budgets_and_numeric_relations_are_strict` couvre notamment un grade non fini ;
- `color_grade_mapping_is_bounded_and_returns_exact_clear_values` vérifie le mapping Bevy ;
- `headless_plugin_projects_one_rain_batch_materials_and_camera_components` vérifie la présence des
  composants ;
- `tier_projection_is_reversible_and_stable_between_transitions` vérifie retrait/réinstallation de
  Bloom et conservation des paramètres authored.

**Limites et preuve encore dues**

- ni la présence de composants ECS ni leur sérialisation ne prouvent le post-traitement final. Le
  harness exact `125ba0b` borne honnêtement son statut à
  `color_grade_endpoints_and_bloom_component_presence_observed` ;
- les captures exactes `125ba0b` acquièrent les endpoints High clear
  `(0 ; 0,008 ; 0,001 ; 1,04 ; 1,06 ; 1)` et rain
  `(-0,35 ; -0,012 ; 0,002 ; 0,86 ; 1,08 ; 1)` pour exposition, température, tint, saturation,
  contraste et gamma. Produire encore Low et une inspection humaine ciblée du clipping, pompage et
  halo ;
- consigner backend, résolution, profil, seed, commit, warm-up, métriques et inspection humaine avant
  de cocher `R1-026`.

## Protocole minimal de fermeture

| Cible | Variantes obligatoires | Preuves minimales |
|---|---|---|
| Runtime natif | `clear` et `rain`, High, 1920 × 1080, VSync off, même seed/caméra | **AUTOMATISATION ACQUISE à `125ba0b`** : 29 PNG uniques, vidéo, identité GPU/driver, 75 pipelines prêts et logs propres ; exécution GPU optique instrumentée et revue humaine non acquises |
| Studio Browser | `forge_scene=visual-valley` avec `forge_weather=clear`, puis `rain`, High | **PARTIEL ACQUIS à `d9e2670…`** : 24/24 contrôles chacun, 9/9 modèles, 26 assets HTTP 200, hashes alignés, console/réseau propres et captures inspectées ; `gpu_presentation=not_observed` |
| Réversibilité | `clear → rain → clear` sur une exécution instrumentée par cible | **PARTIEL NATIF `125ba0b`** : wetness CPU/ECS terminale exacte et trois PNG liés ; exécution GPU instrumentée, retour visuel humain accepté et Browser dus |
| Profils optiques/post-FX | Low/Medium/High/Ultra, au minimum captures Low/High | chemin SSR/fallback observé, paramètres appliqués, coût et artefacts comparés |

Le commit/arbre, le smoke natif High clear/rain et le smoke Browser clear/rain restent consignés sur
la paire d9 exacte. Les temps Browser mesurent la boucle WASM, pas le GPU : clear
moyenne 10,1175 ms, p95 12,1 ms, 1 % low 77,519 FPS ; rain moyenne 9,5633 ms, p95 10,9 ms, 1 % low
85,106 FPS ; zéro hitch. Le lot exact `125ba0b` ajoute une preuve native automatisée réacquise :
SHA-256 runtime `0e7ba27a1a11b2d49181db4324bab5248de821c81ebe36d5cbd3513fc4a8400e`,
ledger `d6a50c665c2f8836e5dc28ae741fc5095f12b60e1cb8fa84e7cc89e2920188ec`, validation
`6F50AB274E98D6F850F3BD55D08D04878A643FDC87C3BD67470EE4454B3A6F11` et vidéo
`29a5db4cd1f86911d3b43662c70147585eafcbee83ed96b4b8b10b60a2f1b226`. Restent dus : séquence
Browser, matrice optique, sonde GPU et acceptation humaine.
Les trois PNG eau portent respectivement les SHA-256
`ED03BFEE3EAAECAF1AF27CBE03E9AF4CB4AD116157D99BFE37B8B6FC5F2B0CBC` (clear),
`5A97F46289469C0F33A7C011B73EB59E209C4400B7068B323279667D2B284506` (rain) et
`8F519DD1D335F5E382B2DFA58149DF82D7BAA8A2F4583517D85CBCE0C20EDF5C` (restored).
`R1-023`, `R1-025` et `R1-026` restent explicitement ouverts ; `R1-022` et `R1-024` sont acquis
fonctionnellement mais restent bornés visuellement/GPU. Gate 1 demeure **NON QUALIFIÉE**.
