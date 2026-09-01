# Gate 1 — cartographie de preuve R1-022 à R1-026

## Verdict borné

**NON QUALIFIÉ.** Le candidat contient des contrats Forge, des adapters Bevy, des shaders WGSL et
des tests automatisés ciblant l'eau, la pluie, la wetness et le post-traitement. Les tests intégrés
moteur/runtime/Studio et le check WASM passent sur l'état combiné courant, mais aucun smoke GPU,
aucune capture et aucun lancement Browser WebGPU final n'ont encore été exécutés. Aucun résultat
ci-dessous ne doit donc être présenté comme une observation de présentation native ou WebGPU.

Les cinq critères restent ouverts :

| Critère | État de cette preuve |
|---|---|
| `R1-022` eau de rivière | projection CPU/ECS et shader présents ; présentation GPU non observée |
| `R1-023` réflexion/réfraction selon profil | SSR préparé ; réfraction écran absente et repli `surface_tint` actif |
| `R1-024` pluie | lot déterministe et animation WGSL présents ; pluie GPU non observée |
| `R1-025` wetness réversible | transition et uniforms testables ; résultat visuel multi-cible non observé |
| `R1-026` bloom et color grading bornés | composants Bevy bornés présents ; résultat GPU et revue visuelle dus |

La roadmap et `PROJECT_CONTEXT.md` ne sont pas modifiés par ce document. La qualification exige au
minimum les smokes et captures `clear`/`rain` du runtime natif **et** du Studio WebGPU, sur le commit
final d'intégration.

## Validation automatisée de l'état intégré courant

| Validation intégrée | Résultat exact consigné |
|---|---|
| tests `forge_engine` | **118/118 PASS** |
| tests `forge_runtime` | **32/32 PASS** (31 bin + 1 intégration) |
| tests `forge_studio` | **31/31 PASS** (28 lib + 3 bin) |
| check de compilation WASM exact courant | **PASS — 7.45 s** |
| `cargo fmt --all -- --check` | **PASS** |
| `git diff --check` | **PASS** |
| Clippy strict exact de l'état combiné | **PASS — 6.29 s** |
| smoke GPU natif, captures `clear`/`rain`, smoke/capture Browser WebGPU finaux | **NOT RUN** |

Ces résultats valident la compilation et les contrats automatisés intégrés. Ils ne prouvent ni la
compilation réelle des pipelines sur un adaptateur donné, ni leur exécution, ni les pixels présentés.

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

Ces garde-fous prouvent des cohérences CPU/ECS et empêchent un ledger partiel ou mélangé. Les champs
`gpu_pass_observed`, `gpu_execution_observed` et `gpu_presentation` restent volontairement négatifs
ou non observés tant qu'un vrai smoke avec capture n'a pas été produit.

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
- `VisualValleyWaterMaterial` est un matériau étendu Bevy opaque/deferred. Le shader Forge
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
  `material_extension_maps_profile_and_keeps_deferred_surface_opaque`,
  `camera_preparation_is_observed_without_claiming_gpu_execution`,
  `shader_contract_contains_profile_waves_depth_normals_and_deferred_output` et
  `runtime_phase_is_seeded_repeatable_wrapped_and_profile_driven`.

**Limites et preuve encore dues**

- la berge est une bande opaque de substitution : carving, blend authored, érosion et wet edge sont
  explicitement différés dans `WaterProjectionStats` ;
- l'opacité authored est enregistrée mais la surface reste opaque pour le chemin deferred/SSR ;
- exécuter et inspecter une capture 1920 × 1080 native `clear` puis `rain`, après warm-up, avec eau,
  berges, mouvement, intersections terrain et stabilité temporelle visibles ;
- reproduire les deux scènes dans le viewport Studio WebGPU et confirmer le même hash eau et le
  statut ECS `observed_main_world_components` ; ce statut seul ne constitue pas une preuve GPU.

### R1-023 — réflexion et réfraction selon profil

**API et projection réelles**

- `WaterOpticsProfile` borne qualité, réflexion, réfraction et fallbacks. La fixture High demande
  SSR 48 pas, distance 96 m et épaisseur 0,9 m ; elle demande une réfraction écran 24 pas jusqu'à
  3,2 m, avec repli `surface_tint`.
- `WaterDocument::resolve_optics_path` distingue capacité `available`, `unavailable` et
  `not_qualified`, sans confondre sélection logique et exécution GPU.
- l'adapter installe réellement `ScreenSpaceReflections`, `DeferredPrepass`, `Msaa::Off` et une
  transmission désactivée sur la caméra. Le chemin Bevy courant déclare la réfraction écran
  indisponible et applique un tint de surface de force 0,18.
- le profil qualité commande la réflexion : Low retire le SSR, Medium demande 24 pas, High 48 et
  Ultra 64. L'intensité demandée est observée dans `WaterProjectionStats`, mais n'est pas mappée par
  l'adapter eau courant. La distance maximale, la réfraction, ses pas et sa profondeur restent les
  quatre champs `not_applicable` du profil qualité.

**Tests concernés inventoriés dans le candidat**

- `optical_fallback_resolution_is_explicit_and_measurable` ;
- `requested_and_fallback_optics_are_observable` ;
- `metrics_resolve_optics_without_claiming_gpu_or_webgpu_execution` ;
- `tier_projection_is_reversible_and_stable_between_transitions` pour l'ajout/retrait du composant
  SSR et son budget de pas.

**Limites et preuve encore dues**

- la disponibilité structurelle de `ScreenSpaceReflections` ne prouve ni compilation ni exécution
  du pipeline sur D3D12/Vulkan/WebGPU ;
- l'intensité et la distance SSR demandées ne sont pas mappées par le composant Bevy actuel ;
- la réfraction écran demandée en High/Ultra n'est pas implémentée. Le repli tint est honnête mais ne
  permet pas de fermer le critère comme « réfraction écran » ;
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
- lancer le runtime natif en `clear` puis `rain`, capturer après warm-up et vérifier que le lot est
  caché/visible, animé et centré sur la caméra sans hitch récurrent ;
- refaire le smoke et les captures dans un navigateur/WebView avec WebGPU réellement actif, puis
  conserver console, backend/adaptateur GPU, hash météo et ledger Studio ;
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
- exécuter une séquence réelle `clear → rain → clear` en natif et WebGPU, capturer les états secs,
  mouillés puis restaurés, et confirmer simultanément les uniforms/ledgers et le résultat visuel ;
- vérifier par inspection que le terrain et la végétation s'assombrissent et deviennent moins
  rugueux sans dérive permanente de palette.

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

- ni la présence de composants ECS ni leur sérialisation ne prouvent le post-traitement final ;
- produire les captures native et Studio WebGPU `clear`/`rain`, Low et High au minimum, puis vérifier
  l'absence de clipping, pompage, halo excessif ou dérive colorimétrique ;
- consigner backend, résolution, profil, seed, commit, warm-up, métriques et inspection humaine avant
  de cocher `R1-026`.

## Protocole minimal de fermeture

| Cible | Variantes obligatoires | Preuves minimales |
|---|---|---|
| Runtime natif | `clear` et `rain`, High, 1920 × 1080, VSync off, même seed/caméra | deux captures inspectées, métriques après warm-up, backend/GPU/driver, hashes eau/météo, absence d'erreur shader/pipeline |
| Studio WebGPU | `forge_scene=visual-valley` avec `forge_weather=clear`, puis `rain`, High, même cadrage | WebGPU réellement actif, deux captures inspectées, ledger de parité avec hashes identiques, console et réseau sans échec d'asset/shader |
| Réversibilité | `clear → rain → clear` sur une exécution instrumentée par cible | wetness terminale exacte et retour visuel sec, sans mutation des palettes |
| Profils optiques/post-FX | Low/Medium/High/Ultra, au minimum captures Low/High | chemin SSR/fallback observé, paramètres appliqués, coût et artefacts comparés |

Les résultats automatisés intégrés disponibles sont consignés ci-dessus. Le commit et l'arbre du
candidat final, les smokes GPU natifs et Browser WebGPU, les hashes des nouveaux artefacts locaux et
la revue humaine des captures restent dus. Tant que ces éléments manquent, `R1-022` à `R1-026` et
Gate 1 restent **NON QUALIFIÉS**.
