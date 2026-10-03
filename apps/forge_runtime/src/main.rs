#![deny(unsafe_code)]

mod environment_cycle_proof;
mod fixed_clock_steps;
mod fixed_view_input;
mod governed_asset_kit;
mod player_camera_probe;
mod runtime_errors;
mod sky_ibl_proof;
mod terrain_partial_update_probe;
mod walk_proof;
mod water_medium_input;
mod water_optics_isolation;
mod weather_input;

use bevy::{
    app::AppExit,
    asset::AssetPlugin,
    log::{DEFAULT_FILTER, LogPlugin},
    prelude::*,
    render::{
        RenderPlugin,
        settings::{RenderCreation, WgpuSettings},
    },
    window::{CursorGrabMode, CursorOptions, PresentMode, WindowResolution},
};
use forge_engine::BridgeCrossingProbePlugin;
use forge_engine::{
    ProofScenePlugin, RuntimeAutomation, VisualValleyEnvironmentPlugin,
    VisualValleyGovernedAssetKitPlugin, VisualValleyPlugin, VisualValleyShadowQualityPlugin,
    VisualValleyVegetationPlugin, VisualValleyWaterPlugin, VisualValleyWeatherPlugin,
    WindowResizeRequest, WorldSketchBridgePlugin,
};
use forge_project::{
    EnvironmentProfileDocument, SceneProofDocument, TerrainDocument, VegetationDocument,
    VisualQualityTier, VisualValleyQualityProfileDocument, WaterDocument, WeatherProfileDocument,
    WeatherQualityTier, WeatherState,
};
use forge_world::{
    TerrainGenerationPlan, VegetationGenerationPlan, WorldSketchBridgePlacementDocument,
    WorldSketchDocument, compile_world_sketch, embedded_gate0_proof_document,
    embedded_visual_valley_environment_profile, embedded_visual_valley_player_controller_profile,
    embedded_visual_valley_terrain_document, embedded_visual_valley_vegetation_document,
    embedded_visual_valley_water_document, embedded_visual_valley_world_sketch,
    water_from_carved_spline,
};
use governed_asset_kit::LoadedGovernedAssetKit;
use player_camera_probe::PlayerCameraProbePlugin;
use runtime_errors::RuntimeErrors;
use std::{env, fs, path::PathBuf};
use terrain_partial_update_probe::TerrainPartialUpdateProbePlugin;
use tracing_subscriber::{EnvFilter, prelude::*};

#[allow(
    clippy::too_many_lines,
    reason = "runtime startup keeps one auditable sequence from CLI validation through Bevy shutdown"
)]
fn main() -> AppExit {
    let runtime_errors = match initialize_structured_tracing() {
        Ok(errors) => errors,
        Err(message) => {
            eprintln!("forge_runtime: cannot initialize structured tracing: {message}");
            return AppExit::from_code(2);
        }
    };

    let options = match Options::parse(env::args().skip(1)) {
        Ok(options) => options,
        Err(message) => return startup_error("arguments", &message),
    };

    if options.log_probe {
        return run_structured_log_probe();
    }

    if let Err(message) = options.prepare_output_paths() {
        return startup_error("output_preflight", &message);
    }
    let mut document = match options.load_document() {
        Ok(document) => document,
        Err(message) => return startup_error("scene_load", &message),
    };
    let governed_asset_kit = match options.load_governed_asset_kit() {
        Ok(kit) => kit,
        Err(message) => return startup_error("asset_kit_load", &message),
    };
    let weather_cycle_domain = match environment_cycle_proof::weather_domain(
        options.weather_cycle_proof.is_some(),
        &document,
    ) {
        Ok(domain) => domain,
        Err(message) => return startup_error("weather_cycle_proof", &message),
    };
    let prototype_preparation = match options.prepare_vegetation_catalog_path.as_ref() {
        None => None,
        Some(path) => {
            let Some(kit) = governed_asset_kit.as_ref() else {
                return startup_error(
                    "prototype_catalog",
                    "prototype preparation requires a verified asset kit",
                );
            };
            let RuntimeDocument::VisualValley {
                vegetation: Some(vegetation),
                ..
            } = &document
            else {
                return startup_error(
                    "prototype_catalog",
                    "prototype preparation requires vegetation",
                );
            };
            let catalog = fs::read_to_string(path)
                .map_err(|error| error.to_string())
                .and_then(|json| {
                    forge_assets::VegetationPrototypeCatalog::from_json(&json)
                        .map_err(|error| error.to_string())
                });
            match catalog.and_then(|catalog| {
                forge_engine::VegetationPrototypeLoadPlugin::new(catalog, &kit.report, vegetation)
            }) {
                Ok(plugin) => Some(plugin),
                Err(message) => return startup_error("prototype_catalog", &message),
            }
        }
    };
    let bridge_document = match options.load_bridge_for_scene(&mut document) {
        Ok(document) => document,
        Err(message) => return startup_error("bridge_load", &message),
    };
    if let Some(bridge) = &bridge_document {
        let RuntimeDocument::VisualValley { terrain, .. } = &document else {
            return startup_error(
                "bridge_load",
                "bridge placements require a Visual Valley terrain document",
            );
        };
        if let Err(error) = WorldSketchBridgePlugin::new(terrain, bridge.clone()) {
            return startup_error("bridge_load", &error.to_string());
        }
    }

    let fixed_proof_plugin = if let Some(camera_id) = &options.fixed_proof_view {
        let RuntimeDocument::VisualValley {
            world_sketch: Some(sketch),
            ..
        } = &document
        else {
            return startup_error("fixed_proof_view", "an authored WorldSketch is required");
        };
        let view = if let Some(path) = &options.fixed_proof_view_input {
            match fixed_view_input::load(path, sketch) {
                Ok(candidate) => candidate,
                Err(error) => return startup_error("fixed_proof_view", &error),
            }
        } else {
            sketch.as_ref().clone()
        };
        match forge_engine::FixedProofViewPlugin::new(&view, camera_id) {
            Ok(plugin) => Some(plugin),
            Err(error) => return startup_error("fixed_proof_view", &error.to_string()),
        }
    } else {
        None
    };
    let present_mode = present_mode_for_vsync(options.vsync);
    let runtime_mode = document.mode_label();
    let window_title = document.window_title();
    let render_plugin = RenderPlugin {
        render_creation: RenderCreation::Automatic(Box::new(WgpuSettings {
            power_preference: bevy::render::settings::PowerPreference::HighPerformance,
            disabled_features: options
                .no_pipeline_statistics
                .then_some(bevy::render::settings::WgpuFeatures::PIPELINE_STATISTICS_QUERY),
            ..default()
        })),
        ..default()
    };
    let automation = options.runtime_automation();
    let automated_run = automated_runtime_requested(&automation)
        || options.bridge_frames.is_some()
        || options.environment_cycle_proof.is_some()
        || options.water_cycle_proof.is_some()
        || options.vegetation_cycle_proof.is_some()
        || options.global_cycle_proof.is_some()
        || options.weather_cycle_proof.is_some();
    let player_input_enabled =
        options.player_camera.is_interactive() && !automated_runtime_requested(&automation);
    let primary_cursor_options = cursor_options_for_player_camera(player_input_enabled);
    let weather_override = options
        .weather_state
        .map_or("document_default", weather_state_label);
    tracing::info!(
        target: "forge::runtime",
        schema = "forge.runtime-event",
        schema_version = 1_u64,
        event_kind = "runtime_started",
        runtime_mode,
        water_optics_isolation = options.water_optics_isolation.map_or("none", water_optics_isolation::Isolation::label),
        vsync = options.vsync,
        present_mode = present_mode_label(present_mode),
        resizable = true,
        bounded_exit_configured = automation.exit_after_frames.is_some(),
        bounded_exit_frame = u64::from(automation.exit_after_frames.unwrap_or_default()),
        automated_close_configured = automation.close_at_frame.is_some(),
        automated_close_frame = u64::from(automation.close_at_frame.unwrap_or_default()),
        terrain_partial_update_probe = options.terrain_partial_update_probe.is_enabled(),
        player_camera = options.player_camera.is_interactive(),
        player_camera_probe = options.player_camera.is_probe(),
        bridge_placement = bridge_document.is_some(),
        bridge_preview = options.bridge_preview,
        bridge_crossing_probe = options.bridge_crossing_probe,
        world_sketch_visual_valley = options.world_sketch_visual_valley,
        governed_asset_kit = governed_asset_kit.is_some(),
        weather_override,
        player_input_enabled,
        player_cursor_locked = player_input_enabled,
        "native runtime starting"
    );

    let mut app = App::new();
    let asset_plugin = governed_asset_kit
        .as_ref()
        .map_or_else(AssetPlugin::default, |kit| AssetPlugin {
            file_path: kit.asset_root.to_string_lossy().into_owned(),
            ..default()
        });
    app.insert_resource(ClearColor(Color::srgb(0.015, 0.025, 0.055)))
        .add_plugins(
            DefaultPlugins
                .set(WindowPlugin {
                    primary_window: Some(Window {
                        title: window_title.to_owned(),
                        resolution: WindowResolution::new(1920, 1080)
                            .with_scale_factor_override(1.0),
                        present_mode,
                        resizable: true,
                        ..default()
                    }),
                    primary_cursor_options: Some(primary_cursor_options),
                    ..default()
                })
                .set(asset_plugin)
                .set(render_plugin)
                .disable::<LogPlugin>(),
        );

    app.add_plugins(forge_engine::source_fragment::SourceFragmentPlugin);
    if let Some(plugin) = prototype_preparation {
        app.add_plugins(plugin);
    }
    if options.prototype_forest_preview {
        app.insert_resource(forge_engine::VegetationPrototypeForestPreview);
    }
    if options.prototype_far_batches {
        app.insert_resource(forge_engine::GovernedFarBatchPreview);
    }
    if options.water_depth_preview {
        app.insert_resource(forge_engine::WaterDepthOpticsPreview);
    }
    if options.water_reflection_preview {
        app.insert_resource(forge_engine::WaterReflectionOpticsPreview);
    }
    if let Some(path) = &options.cloud_layer_preview {
        let result = fs::read(path)
            .map_err(|error| error.to_string())
            .and_then(|bytes| {
                serde_json::from_slice::<forge_project::CloudLayerSettings>(&bytes)
                    .map_err(|error| error.to_string())
            })
            .and_then(|settings| {
                forge_engine::CloudLayerPreviewPlugin::new(settings)
                    .map_err(|error| error.to_string())
            });
        match result {
            Ok(plugin) => {
                app.add_plugins(plugin);
            }
            Err(message) => return startup_error("cloud_preview", &message),
        }
    }
    if let Err(message) = add_runtime_plugin(
        &mut app,
        document,
        RuntimePluginConfiguration {
            automation,
            terrain_debug: options.terrain_debug,
            terrain_partial_update_probe: options.terrain_partial_update_probe,
            player_camera: options.player_camera,
            bridge_document,
            bridge_preview: options.bridge_preview,
            bridge_crossing_probe: options.bridge_crossing_probe,
            bridge_frames: options.bridge_frames.clone(),
            temporal_lod_probe: options.temporal_lod_probe,
            governed_asset_kit,
            weather_state: options.weather_state,
            rain_quality: options.rain_quality,
            shadow_quality: options.shadow_quality,
            walk_shadow_profile: options.walk_shadow_profile.clone(),
            terrain_quality: options.terrain_quality,
            post_process_quality: options.post_process_quality,
            water_quality: options.water_quality,
            environment_quality: options.environment_quality,
            vegetation_quality: options.vegetation_quality,
            player_input_enabled,
        },
    ) {
        return startup_error("runtime_plugin", &message);
    }

    if options.volumetric_preview {
        app.insert_resource(forge_engine::VolumetricPreviewRequest { enabled: true });
        tracing::warn!(target: "forge::runtime", event_kind = "volumetric_preview_unqualified",
            "Experimental volumetrics enabled; distance fog retained; GPU qualification pending");
    }
    if let Some(tier) = options.global_quality {
        app.add_plugins(forge_engine::GlobalQualityPlugin(tier));
        if options.prewarm_quality {
            app.world_mut()
                .resource_mut::<forge_engine::ProfileWarmup>()
                .request();
        }
        if options.adaptive_quality {
            app.world_mut()
                .resource_mut::<forge_engine::GlobalQualityRequest>()
                .enable_adaptation();
        }
        tracing::warn!(target: "forge::runtime", schema = "forge.runtime-event", schema_version = 1_u64,
            event_kind = "global_quality_requested_partial", requested_tier = ?tier,
            requested_domains = 7_u64, full_preset_applied = false,
            adaptation_requested = options.adaptive_quality,
            "Initial domain budgets requested; effective plan acknowledgement remains required");
    }
    if let Some(plugin) = fixed_proof_plugin {
        app.add_plugins(plugin);
    }
    if let Some(steps) = options.fixed_proof_steps {
        fixed_clock_steps::install(&mut app, steps);
    }
    if let Some(proof) = options.walk_proof {
        app.add_plugins(proof);
    }
    if options.walk_proof_measure_moving {
        walk_proof::install_moving_measurement(&mut app);
    } else if options.walk_proof_measure {
        walk_proof::install_measurement(&mut app, options.walk_proof_measure_frozen);
    }
    if let Some(path) = options.sky_ibl_proof_output.clone() {
        sky_ibl_proof::install(&mut app, path);
    }
    if let Some(directory) = options.environment_cycle_proof
        && let Err(error) = environment_cycle_proof::install(&mut app, directory)
    {
        return startup_error("environment_cycle_proof", &error);
    }
    if let (Some(directory), Some(domain)) = (options.weather_cycle_proof, weather_cycle_domain)
        && let Err(error) = environment_cycle_proof::install_domain(&mut app, directory, domain)
    {
        return startup_error("weather_cycle_proof", &error);
    }
    if let Some(directory) = options.vegetation_cycle_proof
        && let Err(error) = environment_cycle_proof::install_domain(
            &mut app,
            directory,
            environment_cycle_proof::Domain::Vegetation,
        )
    {
        return startup_error("vegetation_cycle_proof", &error);
    }
    if let Some(directory) = options.global_cycle_proof
        && let Err(error) = environment_cycle_proof::install_domain(
            &mut app,
            directory,
            environment_cycle_proof::Domain::Global,
        )
    {
        return startup_error("global_cycle_proof", &error);
    }
    if let Some(directory) = options.water_cycle_proof {
        let Some(tier) = options.water_quality else {
            return startup_error("water_cycle_proof", "water quality is required");
        };
        if let Err(error) = environment_cycle_proof::install_domain(
            &mut app,
            directory,
            environment_cycle_proof::Domain::Water(tier),
        ) {
            return startup_error("water_cycle_proof", &error);
        }
    }
    let exit = app.run();
    let exit = environment_cycle_proof::resolve_exit(&app, exit);
    let exit = runtime_errors.resolve_exit(exit, automated_run);
    tracing::info!(
        target: "forge::runtime",
        schema = "forge.runtime-event",
        schema_version = 1_u64,
        event_kind = "runtime_stopped",
        success = exit.is_success(),
        exit_code = u64::from(app_exit_code(&exit)),
        runtime_errors_observed = runtime_errors.observed(),
        automated_run,
        shutdown = "bevy_app_exit",
        "runtime stopped"
    );
    exit
}

fn run_structured_log_probe() -> AppExit {
    tracing::info!(
        target: "forge::runtime",
        schema = "forge.runtime-event",
        schema_version = 1_u64,
        event_kind = "structured_log_probe",
        probe_sequence = 1_u64,
        probe_ok = true,
        "structured logging probe emitted"
    );
    tracing::info!(
        target: "forge::runtime",
        schema = "forge.runtime-event",
        schema_version = 1_u64,
        event_kind = "runtime_stopped",
        success = true,
        exit_code = 0_u64,
        shutdown = "logging_probe",
        "runtime stopped cleanly"
    );
    AppExit::Success
}

#[allow(
    clippy::struct_excessive_bools,
    reason = "Independent opt-in renderer qualification switches"
)]
struct RuntimePluginConfiguration {
    walk_shadow_profile: Option<PathBuf>,
    automation: RuntimeAutomation,
    terrain_debug: TerrainDebugMode,
    terrain_partial_update_probe: TerrainPartialUpdateProbeMode,
    player_camera: PlayerCameraRuntimeMode,
    bridge_document: Option<WorldSketchBridgePlacementDocument>,
    bridge_preview: bool,
    bridge_crossing_probe: bool,
    bridge_frames: Option<PathBuf>,
    temporal_lod_probe: bool,
    governed_asset_kit: Option<LoadedGovernedAssetKit>,
    weather_state: Option<WeatherState>,
    rain_quality: Option<WeatherQualityTier>,
    shadow_quality: Option<VisualQualityTier>,
    terrain_quality: Option<VisualQualityTier>,
    player_input_enabled: bool,
    post_process_quality: Option<VisualQualityTier>,
    water_quality: Option<VisualQualityTier>,
    environment_quality: Option<VisualQualityTier>,
    vegetation_quality: Option<VisualQualityTier>,
}

fn area_quality_policy(
    terrain: &TerrainDocument,
) -> Result<VisualValleyQualityProfileDocument, String> {
    let mut policy = VisualValleyQualityProfileDocument::from_json(include_str!(
        "../../../examples/visual_valley/quality-profile.v1.json"
    ))
    .map_err(|error| format!("invalid embedded quality profile: {error}"))?;
    // Bind only the revision; plugins still validate the authored project/area identity.
    policy.revision = terrain.revision;
    Ok(policy)
}

#[allow(
    clippy::too_many_lines,
    reason = "Visual Valley plugin composition is intentionally centralized for runtime-mode auditability"
)]
fn add_runtime_plugin(
    app: &mut App,
    document: RuntimeDocument,
    configuration: RuntimePluginConfiguration,
) -> Result<(), String> {
    let RuntimePluginConfiguration {
        walk_shadow_profile,
        automation,
        terrain_debug,
        terrain_partial_update_probe,
        player_camera,
        bridge_document,
        bridge_preview,
        bridge_crossing_probe,
        bridge_frames,
        temporal_lod_probe,
        governed_asset_kit,
        weather_state,
        rain_quality,
        shadow_quality,
        terrain_quality,
        post_process_quality,
        water_quality,
        environment_quality,
        vegetation_quality,
        player_input_enabled,
    } = configuration;
    match document {
        RuntimeDocument::Gate0(document) => {
            app.add_plugins(ProofScenePlugin::new(*document).with_automation(automation));
        }
        RuntimeDocument::VisualValley {
            terrain: document,
            vegetation,
            environment,
            water,
            weather,
            world_sketch,
            derived_bridge: _,
            road_surface,
        } => {
            let document = *document;
            if let Some(tier) = environment_quality {
                if environment.is_none() {
                    return Err("environment quality requires an environment profile".to_owned());
                }
                app.insert_resource(
                    forge_engine::EnvironmentQualityRequest::new(
                        &document,
                        area_quality_policy(&document)?,
                        tier,
                    )
                    .map_err(|error| {
                        format!("incompatible environment quality profile: {error}")
                    })?,
                );
            }
            if let Some(tier) = vegetation_quality {
                let source = vegetation
                    .as_deref()
                    .ok_or("vegetation quality requires a vegetation document")?;
                let request = forge_engine::VegetationQualityRequest::new(
                    &document,
                    area_quality_policy(&document)?,
                    tier,
                )
                .map_err(|error| format!("incompatible vegetation quality: {error}"))?;
                request
                    .validate_vegetation(source)
                    .map_err(|error| format!("unsupported vegetation presentation: {error}"))?;
                app.insert_resource(request);
            }
            if let Some(tier) = water_quality {
                if water.is_none() {
                    return Err("water quality requires the water adapter".to_owned());
                }
                app.insert_resource(
                    forge_engine::WaterQualityRequest::new(
                        &document,
                        area_quality_policy(&document)?,
                        tier,
                    )
                    .map_err(|error| format!("incompatible water quality profile: {error}"))?,
                );
            }
            if let Some(tier) = post_process_quality {
                if weather.is_none() {
                    return Err("post-processing quality requires the weather adapter".to_owned());
                }
                app.insert_resource(
                    forge_engine::PostProcessQualityRequest::new(
                        &document,
                        area_quality_policy(&document)?,
                        tier,
                    )
                    .map_err(|error| {
                        format!("incompatible post-processing quality profile: {error}")
                    })?,
                );
            }
            let terrain_plugin = terrain_quality
                .map(|tier| {
                    forge_engine::VisualValleyTerrainQualityPlugin::new(
                        &document,
                        area_quality_policy(&document)?,
                        tier,
                    )
                    .map_err(|error| format!("incompatible terrain quality profile: {error}"))
                })
                .transpose()?;
            let shadow_plugin = shadow_quality
                .map(|tier| {
                    let policy =
                        walk_proof::shadow_policy(&document, walk_shadow_profile.as_deref())?;
                    VisualValleyShadowQualityPlugin::new(&document, policy, tier)
                        .map_err(|error| format!("incompatible shadow quality profile: {error}"))
                })
                .transpose()?;
            let governed_asset_kit_plugin = match (governed_asset_kit, world_sketch.as_deref()) {
                (Some(kit), Some(world_sketch)) => Some(
                    VisualValleyGovernedAssetKitPlugin::new(
                        &document,
                        world_sketch,
                        kit.report,
                        kit.preview,
                    )
                    .map_err(|error| format!("cannot prepare governed asset kit: {error}"))?
                    .with_validation_camera(
                        !player_camera.is_enabled() && !bridge_preview && !bridge_crossing_probe,
                    ),
                ),
                (None, _) => None,
                (Some(_), None) => {
                    return Err(
                        "governed asset kit requires the compiled Visual Valley WorldSketch"
                            .to_owned(),
                    );
                }
            };
            let player_profile = (player_camera.is_enabled() || bridge_crossing_probe).then(|| {
                embedded_visual_valley_player_controller_profile().unwrap_or_else(|error| {
                    panic!("embedded Visual Valley player-controller profile is invalid: {error}")
                })
            });
            let player_probe = match (player_camera, player_profile.as_ref()) {
                (PlayerCameraRuntimeMode::Probe, Some(profile)) => Some(
                    PlayerCameraProbePlugin::new(&document, profile).unwrap_or_else(|error| {
                        panic!("cannot prepare bounded player-camera probe: {error}")
                    }),
                ),
                _ => None,
            };
            let bridge_probe = match (
                bridge_crossing_probe,
                bridge_document.as_ref(),
                player_profile.as_ref(),
            ) {
                (true, Some(bridge_document), Some(profile)) => Some(
                    BridgeCrossingProbePlugin::new(&document, bridge_document, profile)?
                        .with_temporal_lod_probe(temporal_lod_probe)
                        .with_recording_directory(bridge_frames)?,
                ),
                (false, _, _) => None,
                _ => panic!("bridge-crossing probe requires bridge placements and player profile"),
            };
            let bridge_plugin = bridge_document.map(|bridge_document| {
                WorldSketchBridgePlugin::new(&document, bridge_document)
                    .unwrap_or_else(|error| panic!("cannot project bridge placements: {error}"))
                    .with_preview_camera(bridge_preview)
            });

            let vegetation_plugin = vegetation
                .map(|vegetation| VisualValleyVegetationPlugin::new(document.clone(), *vegetation));
            let mut water_plugin =
                water.map(|water| VisualValleyWaterPlugin::new(document.clone(), *water));
            let weather_plugin = weather.map(|weather| {
                let mut plugin = VisualValleyWeatherPlugin::new(*weather);
                if let Some(state) = weather_state {
                    plugin = plugin.with_initial_state(state);
                }
                if let Some(quality) = rain_quality {
                    plugin = plugin.with_initial_rain_quality(quality);
                }
                plugin
            });
            let authored_player_spawn = match (world_sketch.as_deref(), player_profile.as_ref()) {
                (Some(sketch), Some(profile)) => Some(
                    forge_world::world_sketch_player_spawn(sketch, &document, profile)
                        .map_err(|error| format!("invalid WorldSketch player spawn: {error}"))?,
                ),
                _ => None,
            };
            let mut visual_valley = VisualValleyPlugin::new(document)
                .with_automation(automation)
                .with_chunk_lod_gizmos(terrain_debug.is_enabled());
            if let Some(surface) = road_surface {
                if let Some(plugin) = water_plugin.take() {
                    water_plugin = Some(
                        plugin
                            .with_road_surface((*surface).clone())
                            .map_err(|error| format!("invalid bank road surface: {error}"))?,
                    );
                }
                visual_valley = visual_valley
                    .with_road_surface(*surface)
                    .map_err(|error| format!("invalid road surface: {error}"))?;
            }
            if bridge_preview || governed_asset_kit_plugin.is_some() {
                visual_valley = visual_valley.with_overview_camera(false);
            }
            if let Some(profile) = player_profile {
                if let Some(position) = authored_player_spawn {
                    visual_valley =
                        visual_valley.with_player_spawn_position(Vec3::from_array(position));
                    // WorldSketch zones are centred at the origin. Probes own their view.
                    if player_probe.is_none() && bridge_probe.is_none() {
                        visual_valley = visual_valley.with_player_camera_target(Vec3::ZERO);
                    }
                }
                visual_valley = visual_valley
                    .with_player_controller(profile)
                    .with_player_interactive_input(player_input_enabled)
                    .with_overview_camera(false);
            }
            if let Some(probe) = &player_probe {
                visual_valley = visual_valley.with_player_spawn_position(probe.spawn_position());
            }
            if let Some(probe) = &bridge_probe {
                visual_valley = visual_valley.with_player_spawn_position(probe.spawn_position());
                if let Some(target) = probe.recording_camera_target() {
                    visual_valley = visual_valley.with_player_camera_target(target);
                }
            }

            app.add_plugins(visual_valley);
            if let Some(plugin) = terrain_plugin {
                app.add_plugins(plugin);
            }
            if let Some(plugin) = shadow_plugin {
                app.add_plugins(plugin);
            }
            if let Some(environment) = environment {
                app.add_plugins(VisualValleyEnvironmentPlugin::new(*environment));
            }
            if let Some(vegetation_plugin) = vegetation_plugin {
                app.add_plugins(vegetation_plugin);
            }
            if let Some(water_plugin) = water_plugin {
                app.add_plugins(water_plugin);
            }
            if let Some(weather_plugin) = weather_plugin {
                app.add_plugins(weather_plugin);
            }
            if let Some(bridge_plugin) = bridge_plugin {
                app.add_plugins(bridge_plugin);
            }
            if let Some(governed_asset_kit_plugin) = governed_asset_kit_plugin {
                app.add_plugins(governed_asset_kit_plugin);
                if player_camera.is_interactive() {
                    app.insert_resource(forge_engine::PlayerInputFocus(false))
                        .add_systems(
                            PreUpdate,
                            guard_governed_kit_player_input.before(bevy::input::InputSystems),
                        );
                }
            }
            if terrain_partial_update_probe.is_enabled() {
                app.add_plugins(TerrainPartialUpdateProbePlugin::new());
            }
            if let Some(probe) = player_probe {
                app.add_plugins(probe);
            }
            if let Some(probe) = bridge_probe {
                app.add_plugins(probe);
            }
        }
    }
    Ok(())
}

const GATE0_WINDOW_TITLE: &str = "OpenRPG Forge · Rust Gate 0";
const VISUAL_VALLEY_WINDOW_TITLE: &str = "OpenRPG Forge · Rust Gate 1 · Visual Valley";

#[derive(Debug)]
enum RuntimeDocument {
    Gate0(Box<SceneProofDocument>),
    VisualValley {
        terrain: Box<TerrainDocument>,
        vegetation: Option<Box<VegetationDocument>>,
        environment: Option<Box<EnvironmentProfileDocument>>,
        water: Option<Box<WaterDocument>>,
        weather: Option<Box<WeatherProfileDocument>>,
        world_sketch: Option<Box<WorldSketchDocument>>,
        derived_bridge: Option<Box<WorldSketchBridgePlacementDocument>>,
        road_surface: Option<Box<forge_world::RoadSurfaceProjection>>,
    },
}

impl RuntimeDocument {
    const fn mode_label(&self) -> &'static str {
        match self {
            Self::Gate0(_) => "gate0_scene_proof",
            Self::VisualValley {
                world_sketch: Some(_),
                ..
            } => "gate1_world_sketch_visual_valley",
            Self::VisualValley { .. } => "gate1_visual_valley",
        }
    }

    const fn window_title(&self) -> &'static str {
        match self {
            Self::Gate0(_) => GATE0_WINDOW_TITLE,
            Self::VisualValley { .. } => VISUAL_VALLEY_WINDOW_TITLE,
        }
    }
}

fn validated_visual_valley_document(
    document: TerrainDocument,
    vegetation: Option<VegetationDocument>,
    environment: Option<EnvironmentProfileDocument>,
    water: Option<WaterDocument>,
    weather: Option<WeatherProfileDocument>,
    world_sketch: Option<WorldSketchDocument>,
    source: &str,
) -> Result<RuntimeDocument, String> {
    TerrainGenerationPlan::from_document(&document)
        .map_err(|error| format!("unsupported Visual Valley terrain {source}: {error}"))?;
    if let Some(vegetation) = &vegetation {
        VegetationGenerationPlan::from_documents(&document, vegetation).map_err(|error| {
            format!("unsupported Visual Valley vegetation for {source}: {error}")
        })?;
    }
    if let Some(environment) = &environment {
        environment
            .validate_identity(document.project_id, document.area_id, document.revision)
            .map_err(|error| {
                format!("unsupported Visual Valley environment for {source}: {error}")
            })?;
    }
    if let Some(water) = &water {
        water
            .validate_identity(document.project_id, document.area_id, document.revision)
            .map_err(|error| format!("unsupported Visual Valley water for {source}: {error}"))?;
    }
    if let Some(weather) = &weather {
        weather
            .validate_identity(document.project_id, document.area_id, document.revision)
            .map_err(|error| format!("unsupported Visual Valley weather for {source}: {error}"))?;
    }
    Ok(RuntimeDocument::VisualValley {
        terrain: Box::new(document),
        vegetation: vegetation.map(Box::new),
        environment: environment.map(Box::new),
        water: water.map(Box::new),
        weather: weather.map(Box::new),
        world_sketch: world_sketch.map(Box::new),
        derived_bridge: None,
        road_surface: None,
    })
}

#[derive(Clone, Copy, Debug, Default, Eq, PartialEq)]
enum TerrainDebugMode {
    #[default]
    Hidden,
    ChunkLodGizmos,
}

impl TerrainDebugMode {
    const fn is_enabled(self) -> bool {
        matches!(self, Self::ChunkLodGizmos)
    }
}

#[derive(Clone, Copy, Debug, Default, Eq, PartialEq)]
enum TerrainPartialUpdateProbeMode {
    #[default]
    Disabled,
    Enabled,
}

impl TerrainPartialUpdateProbeMode {
    const fn is_enabled(self) -> bool {
        matches!(self, Self::Enabled)
    }
}

#[derive(Clone, Copy, Debug, Default, Eq, PartialEq)]
enum PlayerCameraRuntimeMode {
    #[default]
    Disabled,
    Interactive,
    Probe,
}

impl PlayerCameraRuntimeMode {
    const fn is_enabled(self) -> bool {
        !matches!(self, Self::Disabled)
    }

    const fn is_interactive(self) -> bool {
        matches!(self, Self::Interactive)
    }

    const fn is_probe(self) -> bool {
        matches!(self, Self::Probe)
    }

    fn select_interactive(&mut self) -> Result<(), String> {
        if self.is_probe() {
            return Err(
                "--player-camera and --player-camera-probe are mutually exclusive".to_owned(),
            );
        }
        *self = Self::Interactive;
        Ok(())
    }

    fn select_probe(&mut self) -> Result<(), String> {
        if self.is_interactive() {
            return Err(
                "--player-camera and --player-camera-probe are mutually exclusive".to_owned(),
            );
        }
        *self = Self::Probe;
        Ok(())
    }
}

#[allow(clippy::needless_pass_by_value, reason = "Bevy system parameters")]
fn guard_governed_kit_player_input(
    readiness: Res<forge_engine::GovernedAssetKitReadiness>,
    mut focus: ResMut<forge_engine::PlayerInputFocus>,
) {
    focus.0 = readiness.ready;
}

#[derive(Debug)]
#[allow(
    clippy::struct_excessive_bools,
    reason = "these booleans directly represent independent command-line switches"
)]
struct Options {
    sky_ibl_proof_output: Option<PathBuf>,
    walk_shadow_profile: Option<PathBuf>,
    volumetric_preview: bool,
    environment_cycle_proof: Option<PathBuf>,
    weather_cycle_proof: Option<PathBuf>,
    water_cycle_proof: Option<PathBuf>,
    global_quality: Option<VisualQualityTier>,
    adaptive_quality: bool,
    global_cycle_proof: Option<PathBuf>,
    vegetation_cycle_proof: Option<PathBuf>,
    scene_path: Option<PathBuf>,
    terrain_path: Option<PathBuf>,
    asset_kit_build_path: Option<PathBuf>,
    asset_kit_preview_path: Option<PathBuf>,
    prepare_vegetation_catalog_path: Option<PathBuf>,
    prototype_forest_preview: bool,
    prototype_far_batches: bool,
    water_depth_preview: bool,
    water_reflection_preview: bool,
    water_optics_isolation: Option<water_optics_isolation::Isolation>,
    water_medium_input: Option<PathBuf>,
    cloud_layer_preview: Option<PathBuf>,
    bridge_path: Option<PathBuf>,
    bridge_preview: bool,
    bridge_crossing_probe: bool,
    bridge_frames: Option<PathBuf>,
    temporal_lod_probe: bool,
    visual_valley: bool,
    world_sketch_visual_valley: bool,
    world_sketch_input: Option<PathBuf>,
    fixed_proof_view: Option<String>,
    fixed_proof_steps: Option<u32>,
    fixed_proof_view_input: Option<PathBuf>,
    walk_proof: Option<walk_proof::WalkProof>,
    walk_proof_measure: bool,
    walk_proof_measure_frozen: bool,
    walk_proof_measure_moving: bool,
    no_pipeline_statistics: bool,
    weather_input: Option<PathBuf>,
    terrain_debug: TerrainDebugMode,
    terrain_partial_update_probe: TerrainPartialUpdateProbeMode,
    player_camera: PlayerCameraRuntimeMode,
    weather_state: Option<WeatherState>,
    rain_quality: Option<WeatherQualityTier>,
    shadow_quality: Option<VisualQualityTier>,
    terrain_quality: Option<VisualQualityTier>,
    capture_path: Option<PathBuf>,
    post_process_quality: Option<VisualQualityTier>,
    water_quality: Option<VisualQualityTier>,
    environment_quality: Option<VisualQualityTier>,
    vegetation_quality: Option<VisualQualityTier>,
    capture_frame: u32,
    metrics_path: Option<PathBuf>,
    metrics_warmup_frames: u32,
    prewarm_quality: bool,
    exit_after_frames: Option<u32>,
    resize_at_frame: Option<u32>,
    resize_width: Option<u32>,
    resize_height: Option<u32>,
    close_at_frame: Option<u32>,
    vsync: bool,
    log_probe: bool,
}

impl Default for Options {
    fn default() -> Self {
        Self {
            sky_ibl_proof_output: None,
            walk_shadow_profile: None,
            volumetric_preview: false,
            environment_cycle_proof: None,
            weather_cycle_proof: None,
            water_cycle_proof: None,
            global_quality: None,
            adaptive_quality: false,
            global_cycle_proof: None,
            vegetation_cycle_proof: None,
            scene_path: None,
            terrain_path: None,
            asset_kit_build_path: None,
            asset_kit_preview_path: None,
            prepare_vegetation_catalog_path: None,
            prototype_forest_preview: false,
            prototype_far_batches: false,
            water_depth_preview: false,
            water_reflection_preview: false,
            water_optics_isolation: None,
            water_medium_input: None,
            cloud_layer_preview: None,
            bridge_path: None,
            bridge_preview: false,
            bridge_crossing_probe: false,
            bridge_frames: None,
            temporal_lod_probe: false,
            visual_valley: false,
            world_sketch_visual_valley: false,
            world_sketch_input: None,
            fixed_proof_view: None,
            fixed_proof_steps: None,
            fixed_proof_view_input: None,
            walk_proof: None,
            walk_proof_measure: false,
            walk_proof_measure_frozen: false,
            walk_proof_measure_moving: false,
            no_pipeline_statistics: false,
            weather_input: None,
            terrain_debug: TerrainDebugMode::Hidden,
            terrain_partial_update_probe: TerrainPartialUpdateProbeMode::Disabled,
            player_camera: PlayerCameraRuntimeMode::Disabled,
            weather_state: None,
            rain_quality: None,
            shadow_quality: None,
            terrain_quality: None,
            post_process_quality: None,
            water_quality: None,
            environment_quality: None,
            vegetation_quality: None,
            capture_path: None,
            capture_frame: 120,
            metrics_path: None,
            metrics_warmup_frames: 60,
            prewarm_quality: false,
            exit_after_frames: None,
            resize_at_frame: None,
            resize_width: None,
            resize_height: None,
            close_at_frame: None,
            vsync: true,
            log_probe: false,
        }
    }
}

impl Options {
    fn enable_adaptive_quality(&mut self) -> Result<(), String> {
        if self.adaptive_quality {
            return Err("duplicate --adaptive-quality".into());
        }
        self.adaptive_quality = true;
        Ok(())
    }

    fn validate_adaptive_quality(&self) -> Result<(), String> {
        if self.adaptive_quality
            && (self.global_quality.is_none()
                || self.metrics_path.is_some()
                || self.capture_path.is_some()
                || self.global_cycle_proof.is_some()
                || self.environment_cycle_proof.is_some()
                || self.water_cycle_proof.is_some()
                || self.vegetation_cycle_proof.is_some()
                || self.bridge_crossing_probe
                || self.bridge_frames.is_some()
                || self.player_camera.is_probe()
                || self.terrain_partial_update_probe.is_enabled()
                || self.volumetric_preview
                || (self.prototype_forest_preview && !self.prototype_far_batches))
        {
            return Err("--adaptive-quality requires --quality and excludes fixed metrics/captures, cycle/probe automation and unqualified optical/forest previews".into());
        }
        Ok(())
    }

    fn set_cycle_proof(&mut self, option: &str, path: PathBuf) -> Result<(), String> {
        let target = match option {
            "--vegetation-cycle-proof" => &mut self.vegetation_cycle_proof,
            "--quality-cycle-proof" => &mut self.global_cycle_proof,
            "--water-cycle-proof" => &mut self.water_cycle_proof,
            "--weather-cycle-proof" => &mut self.weather_cycle_proof,
            _ => &mut self.environment_cycle_proof,
        };
        if target.replace(path).is_some() {
            return Err(format!("{option} may only be specified once"));
        }
        Ok(())
    }

    fn validate_environment_cycle_proof(&self) -> Result<(), String> {
        self.validate_weather_cycle_proof()?;
        if self.water_cycle_proof.is_some()
            && (self.water_quality.is_none()
                || self.global_quality.is_some()
                || self.global_cycle_proof.is_some()
                || self.environment_cycle_proof.is_some()
                || self.vegetation_cycle_proof.is_some()
                || automated_runtime_requested(&self.runtime_automation())
                || self.bridge_crossing_probe
                || self.bridge_frames.is_some()
                || !matches!(self.player_camera, PlayerCameraRuntimeMode::Disabled)
                || self.terrain_partial_update_probe != TerrainPartialUpdateProbeMode::Disabled)
        {
            return Err("--water-cycle-proof requires --water-quality and no other automation/player probe or global quality".to_owned());
        }
        if self.global_cycle_proof.is_some()
            && (self.global_quality.is_none()
                || self.environment_cycle_proof.is_some()
                || self.vegetation_cycle_proof.is_some()
                || automated_runtime_requested(&self.runtime_automation())
                || self.bridge_crossing_probe
                || self.bridge_frames.is_some()
                || (self.player_camera != PlayerCameraRuntimeMode::Disabled
                    && self.walk_proof.is_none())
                || self.walk_proof_measure
                || self.terrain_partial_update_probe != TerrainPartialUpdateProbeMode::Disabled)
        {
            return Err(
                "--quality-cycle-proof requires --quality and no other automation/player probe"
                    .to_owned(),
            );
        }
        if self.global_quality.is_some()
            && (self.environment_cycle_proof.is_some() || self.vegetation_cycle_proof.is_some())
        {
            return Err("--quality cannot be combined with a single-domain cycle proof".to_owned());
        }
        if self.vegetation_cycle_proof.is_some()
            && (self.vegetation_quality.is_none()
                || self.environment_cycle_proof.is_some()
                || automated_runtime_requested(&self.runtime_automation())
                || self.bridge_crossing_probe
                || self.bridge_frames.is_some()
                || self.player_camera != PlayerCameraRuntimeMode::Disabled
                || self.terrain_partial_update_probe != TerrainPartialUpdateProbeMode::Disabled)
        {
            return Err("--vegetation-cycle-proof requires --vegetation-quality and no other automation/player probe".to_owned());
        }
        if self.environment_cycle_proof.is_some()
            && (self.environment_quality.is_none()
                || automated_runtime_requested(&self.runtime_automation())
                || self.bridge_crossing_probe
                || self.bridge_frames.is_some()
                || self.player_camera != PlayerCameraRuntimeMode::Disabled
                || self.terrain_partial_update_probe != TerrainPartialUpdateProbeMode::Disabled)
        {
            return Err("--environment-cycle-proof requires --environment-quality and no other automation/player probe".to_owned());
        }
        Ok(())
    }
    fn set_visual_quality(&mut self, option: &str, value: Option<&str>) -> Result<(), String> {
        if option == "--quality" {
            return self.set_global_quality(value);
        }
        if self.global_quality.is_some() {
            return Err(
                "--quality cannot be combined with per-domain quality overrides".to_owned(),
            );
        }
        let target = match option {
            "--water-quality" => &mut self.water_quality,
            "--environment-quality" => &mut self.environment_quality,
            "--vegetation-quality" => &mut self.vegetation_quality,
            "--post-process-quality" => &mut self.post_process_quality,
            "--terrain-quality" => &mut self.terrain_quality,
            "--shadow-quality" => &mut self.shadow_quality,
            _ => return Err(format!("unknown visual quality option: {option}")),
        };
        if target.is_some() {
            return Err(format!("{option} may only be specified once"));
        }
        *target = Some(match value {
            Some("low") => VisualQualityTier::Low,
            Some("medium") => VisualQualityTier::Medium,
            Some("high") => VisualQualityTier::High,
            Some("ultra") => VisualQualityTier::Ultra,
            _ => return Err(format!("{option} expects low|medium|high|ultra")),
        });
        Ok(())
    }
    fn set_rain_quality(&mut self, value: Option<&str>) -> Result<(), String> {
        if self.global_quality.is_some() {
            return Err("--quality cannot be combined with --rain-quality".to_owned());
        }
        if self.rain_quality.is_some() {
            return Err("--rain-quality may only be specified once".to_owned());
        }
        self.rain_quality = Some(match value {
            Some("low") => WeatherQualityTier::Low,
            Some("medium") => WeatherQualityTier::Medium,
            Some("high") => WeatherQualityTier::High,
            Some("ultra") => WeatherQualityTier::Ultra,
            _ => return Err("--rain-quality expects low|medium|high|ultra".to_owned()),
        });
        Ok(())
    }

    fn set_global_quality(&mut self, value: Option<&str>) -> Result<(), String> {
        if self.global_quality.is_some()
            || self.rain_quality.is_some()
            || [
                self.shadow_quality,
                self.terrain_quality,
                self.post_process_quality,
                self.water_quality,
                self.environment_quality,
                self.vegetation_quality,
            ]
            .iter()
            .any(Option::is_some)
        {
            return Err(
                "--quality must appear once, without per-domain quality overrides".to_owned(),
            );
        }
        for option in [
            "--shadow-quality",
            "--terrain-quality",
            "--post-process-quality",
            "--water-quality",
            "--environment-quality",
            "--vegetation-quality",
        ] {
            self.set_visual_quality(option, value)?;
        }
        self.set_rain_quality(value)?;
        self.global_quality = self.shadow_quality;
        Ok(())
    }

    fn set_cloud_preview_path(
        &mut self,
        arguments: &mut impl Iterator<Item = String>,
    ) -> Result<(), String> {
        set_unique_path(
            &mut self.cloud_layer_preview,
            arguments,
            "--cloud-layer-preview",
        )
    }

    fn enable_volumetric_preview(&mut self) -> Result<(), String> {
        if self.volumetric_preview {
            return Err("--volumetric-preview may only be specified once".to_owned());
        }
        self.volumetric_preview = true;
        Ok(())
    }

    fn set_catalog_path(
        &mut self,
        arguments: &mut impl Iterator<Item = String>,
    ) -> Result<(), String> {
        set_unique_path(
            &mut self.prepare_vegetation_catalog_path,
            arguments,
            "--prepare-vegetation-catalog",
        )
    }

    fn select_render_preview(&mut self, option: &str) -> bool {
        if option == "--water-reflection-preview" {
            self.water_reflection_preview = true;
            return true;
        }
        if option == "--water-depth-preview" {
            self.water_depth_preview = true;
            return true;
        }
        if !matches!(
            option,
            "--prototype-forest-preview" | "--prototype-forest-batches"
        ) {
            return false;
        }
        self.prototype_forest_preview = true;
        self.prototype_far_batches |= option == "--prototype-forest-batches";
        true
    }

    fn set_fixed_proof_view(
        &mut self,
        arguments: &mut impl Iterator<Item = String>,
    ) -> Result<(), String> {
        if self.fixed_proof_view.is_some() {
            return Err("--fixed-proof-view may only be specified once".into());
        }
        self.fixed_proof_view = Some(
            arguments
                .next()
                .ok_or("--fixed-proof-view requires an authored camera ID")?,
        );
        Ok(())
    }

    fn parse(mut arguments: impl Iterator<Item = String>) -> Result<Self, String> {
        let mut options = Self::default();
        while let Some(argument) = arguments.next() {
            match argument.as_str() {
                "--adaptive-quality" => options.enable_adaptive_quality()?,
                "--cloud-layer-preview" => options.set_cloud_preview_path(&mut arguments)?,
                "--volumetric-preview" => options.enable_volumetric_preview()?,
                option if options.select_render_preview(option) => {}
                "--scene" => options.scene_path = Some(next_path(&mut arguments, "--scene")?),
                "--terrain" => options.terrain_path = Some(next_path(&mut arguments, "--terrain")?),
                "--asset-kit-build" => {
                    options.asset_kit_build_path =
                        Some(next_path(&mut arguments, "--asset-kit-build")?);
                }
                "--asset-kit-preview" => {
                    options.asset_kit_preview_path =
                        Some(next_path(&mut arguments, "--asset-kit-preview")?);
                }
                "--prepare-vegetation-catalog" => {
                    options.set_catalog_path(&mut arguments)?;
                }
                "--bridge-placement" => {
                    options.bridge_path = Some(next_path(&mut arguments, "--bridge-placement")?);
                }
                "--bridge-preview" => options.bridge_preview = true,
                "--environment-cycle-proof"
                | "--weather-cycle-proof"
                | "--water-cycle-proof"
                | "--vegetation-cycle-proof"
                | "--quality-cycle-proof" => {
                    options.set_cycle_proof(&argument, next_path(&mut arguments, &argument)?)?;
                }
                "--bridge-crossing-probe" => options.bridge_crossing_probe = true,
                "--temporal-lod-probe" => options.temporal_lod_probe = true,
                "--bridge-frames" => {
                    set_unique_path(&mut options.bridge_frames, &mut arguments, &argument)?;
                }
                "--visual-valley" => options.visual_valley = true,
                "--world-sketch-visual-valley" => options.world_sketch_visual_valley = true,
                "--fixed-proof-view" | "--fixed-proof-view-input" | "--fixed-proof-steps" => {
                    options.set_fixed_proof_option(&argument, &mut arguments)?;
                }
                flag if Self::is_candidate_input(flag) => {
                    options.set_candidate_input(&mut arguments, &argument)?;
                }
                "--terrain-debug" => options.terrain_debug = TerrainDebugMode::ChunkLodGizmos,
                "--terrain-partial-update-probe" => {
                    options.terrain_partial_update_probe = TerrainPartialUpdateProbeMode::Enabled;
                }
                "--walk-proof" => options.set_walk_proof(&mut arguments)?,
                flag if walk_proof::is_measure_flag(flag) => options.set_walk_measure(flag)?,
                "--player-camera" => options.player_camera.select_interactive()?,
                "--player-camera-probe" => options.player_camera.select_probe()?,
                "--weather" => options.set_weather(&mut arguments)?,
                option if is_visual_quality_option(option) => {
                    options.set_visual_quality(option, arguments.next().as_deref())?;
                }
                "--rain-quality" => {
                    options.set_rain_quality(arguments.next().as_deref())?;
                }
                "--capture" => {
                    options.capture_path = Some(next_path(&mut arguments, "--capture")?);
                }
                "--capture-frame" => {
                    options.capture_frame = next_u32(&mut arguments, "--capture-frame")?;
                }
                "--metrics" => {
                    options.metrics_path = Some(next_path(&mut arguments, "--metrics")?);
                }
                "--metrics-warmup-frames" => {
                    options.metrics_warmup_frames =
                        next_u32(&mut arguments, "--metrics-warmup-frames")?;
                }
                "--prewarm-quality" => options.prewarm_quality = true,
                "--exit-after-frames" => {
                    options.exit_after_frames =
                        Some(next_u32(&mut arguments, "--exit-after-frames")?);
                }
                "--resize-at-frame" => {
                    options.resize_at_frame = Some(next_u32(&mut arguments, "--resize-at-frame")?);
                }
                "--resize-width" => {
                    options.resize_width = Some(next_u32(&mut arguments, "--resize-width")?);
                }
                "--resize-height" => {
                    options.resize_height = Some(next_u32(&mut arguments, "--resize-height")?);
                }
                "--close-at-frame" => {
                    options.close_at_frame = Some(next_u32(&mut arguments, "--close-at-frame")?);
                }
                "--no-vsync" => options.vsync = false,
                "--log-probe" => options.log_probe = true,
                "--help" | "-h" => {
                    return Err("usage: forge_runtime [--scene file.json | --visual-valley | --world-sketch-visual-valley | --terrain terrain.json] [--world-sketch-input candidate.json] [--weather-input weather.json] [--water-optics-isolation normals-off|refraction-off] [--weather-cycle-proof fresh-directory] [--asset-kit-build build.json --asset-kit-preview preview.json] [--bridge-placement bridge.json] [--bridge-preview | --bridge-crossing-probe [--bridge-frames fresh-directory]] [--terrain-debug] [--terrain-partial-update-probe] [--player-camera | --player-camera-probe] [--weather clear|rain] [--quality low|medium|high|ultra] [--adaptive-quality] [--rain-quality low|medium|high|ultra] [--shadow-quality low|medium|high|ultra] [--terrain-quality low|medium|high|ultra] [--post-process-quality low|medium|high|ultra] [--water-quality low|medium|high|ultra] [--environment-quality low|medium|high|ultra] [--vegetation-quality low|medium|high|ultra] [--environment-cycle-proof fresh-directory | --vegetation-cycle-proof fresh-directory] [--capture file.png] [--capture-frame n] [--metrics file.json] [--metrics-warmup-frames n] [--exit-after-frames n] [--resize-at-frame n --resize-width px --resize-height px] [--close-at-frame n] [--no-vsync] [--log-probe]".to_owned());
                }
                _ => return Err(format!("unknown option `{argument}`")),
            }
        }
        options.validate()?;
        Ok(options)
    }

    fn set_weather(&mut self, arguments: &mut impl Iterator<Item = String>) -> Result<(), String> {
        if self.weather_state.is_some() {
            return Err("--weather may only be specified once".to_owned());
        }
        self.weather_state = Some(next_weather_state(arguments)?);
        Ok(())
    }

    fn set_fixed_proof_option(
        &mut self,
        flag: &str,
        arguments: &mut impl Iterator<Item = String>,
    ) -> Result<(), String> {
        match flag {
            "--fixed-proof-view" => self.set_fixed_proof_view(arguments),
            "--fixed-proof-view-input" => {
                set_unique_path(&mut self.fixed_proof_view_input, arguments, flag)
            }
            "--fixed-proof-steps" => {
                if self.fixed_proof_steps.is_some() {
                    return Err("--fixed-proof-steps may only be specified once".into());
                }
                self.fixed_proof_steps = Some(next_u32(arguments, flag)?);
                Ok(())
            }
            _ => Err(format!("unknown fixed proof option `{flag}`")),
        }
    }

    fn set_walk_measure(&mut self, flag: &str) -> Result<(), String> {
        if flag == "--no-pipeline-statistics" {
            self.no_pipeline_statistics = true;
            return Ok(());
        }
        if self.walk_proof_measure {
            return Err("walk measurement modes are unique and mutually exclusive".into());
        }
        self.walk_proof_measure = true;
        self.walk_proof_measure_frozen = flag.ends_with("-frozen");
        self.walk_proof_measure_moving = flag.ends_with("-moving");
        Ok(())
    }

    fn set_walk_proof(
        &mut self,
        arguments: &mut impl Iterator<Item = String>,
    ) -> Result<(), String> {
        if self.walk_proof.is_some() {
            return Err("duplicate --walk-proof".into());
        }
        self.walk_proof = Some(walk_proof::WalkProof::parse(
            &arguments
                .next()
                .ok_or("--walk-proof requires ticks:axis_x:axis_y")?,
        )?);
        Ok(())
    }

    #[allow(
        clippy::too_many_lines,
        reason = "CLI validation keeps the complete cross-option contract in one auditable sequence"
    )]
    fn validate(&self) -> Result<(), String> {
        water_optics_isolation::validate(self)?;
        water_medium_input::validate(self)?;
        if self.fixed_proof_view_input.is_some() && self.fixed_proof_view.is_none() {
            return Err("--fixed-proof-view-input requires a bounded fixed proof view".into());
        }
        if self
            .fixed_proof_steps
            .is_some_and(|steps| !(4..=32).contains(&steps))
            || (self.fixed_proof_steps.is_some() && self.fixed_proof_view.is_none())
        {
            return Err(
                "--fixed-proof-steps requires a fixed proof view and 4..32 quarter-second steps"
                    .into(),
            );
        }
        if self.sky_ibl_proof_output.is_some()
            && (self.capture_path.is_none()
                || self.exit_after_frames.is_none()
                || self.walk_proof_measure
                || (!self.visual_valley && !self.world_sketch_visual_valley))
        {
            return Err(
                "--sky-ibl-proof-output requires bounded valley capture without walk measurement"
                    .into(),
            );
        }
        if self.walk_shadow_profile.is_some()
            && (self.walk_proof.is_none()
                || (!self.walk_proof_measure && self.capture_path.is_none())
                || self.shadow_quality.is_none()
                || self.global_quality.is_some())
        {
            return Err("--walk-shadow-profile requires bounded walk measurement or capture and per-domain --shadow-quality, no global quality".into());
        }
        if self.no_pipeline_statistics && !self.walk_proof_measure {
            return Err("--no-pipeline-statistics requires a bounded walk measurement".into());
        }
        if self.walk_proof_measure
            && (self.walk_proof.is_none()
                || self.capture_path.is_some()
                || self.metrics_path.is_some()
                || self.resize_at_frame.is_some()
                || self.close_at_frame.is_some()
                || self.adaptive_quality)
        {
            return Err(
                "--walk-proof-measure requires a walk, no capture/metrics/resize/close/adaptation"
                    .into(),
            );
        }
        if self.walk_proof.is_some()
            && (!self.world_sketch_visual_valley
                || !self.player_camera.is_interactive()
                || (self.exit_after_frames.is_none() && self.global_cycle_proof.is_none())
                || self.fixed_proof_view.is_some()
                || self.bridge_crossing_probe
                || self.terrain_partial_update_probe.is_enabled()
                || self.environment_cycle_proof.is_some()
                || self.weather_cycle_proof.is_some()
                || self.water_cycle_proof.is_some()
                || self.vegetation_cycle_proof.is_some())
        {
            return Err(
                "--walk-proof requires bounded WorldSketch --player-camera without other probes"
                    .into(),
            );
        }
        if self.cloud_layer_preview.is_some()
            && !(self.visual_valley || self.world_sketch_visual_valley)
        {
            return Err("--cloud-layer-preview requires a Visual Valley scene".to_owned());
        }
        self.validate_adaptive_quality()?;
        if self.temporal_lod_probe && (!self.bridge_crossing_probe || self.bridge_frames.is_none())
        {
            return Err(
                "--temporal-lod-probe requires --bridge-crossing-probe and bounded --bridge-frames"
                    .into(),
            );
        }
        if self.prewarm_quality
            && (self.global_quality.is_none()
                || self.fixed_proof_view.is_some()
                || self.environment_cycle_proof.is_some()
                || self.weather_cycle_proof.is_some()
                || self.water_cycle_proof.is_some()
                || self.vegetation_cycle_proof.is_some()
                || self.bridge_crossing_probe)
        {
            return Err("--prewarm-quality requires --quality, without fixed views or competing domain/bridge probes".into());
        }
        if self.volumetric_preview && self.environment_quality.is_none() {
            return Err(
                "--volumetric-preview requires --environment-quality or --quality".to_owned(),
            );
        }
        if self.world_sketch_input.is_some() && !self.world_sketch_visual_valley {
            return Err("--world-sketch-input requires --world-sketch-visual-valley".to_owned());
        }
        if self.fixed_proof_view.is_some()
            && (!self.world_sketch_visual_valley
                || self.capture_path.is_none()
                || self.exit_after_frames.is_none()
                || self.metrics_path.is_some()
                || self.player_camera != PlayerCameraRuntimeMode::Disabled
                || self.bridge_crossing_probe
                || self.bridge_frames.is_some()
                || self.adaptive_quality
                || self.resize_at_frame.is_some()
                || self.close_at_frame.is_some()
                || self.terrain_partial_update_probe.is_enabled()
                || self.environment_cycle_proof.is_some()
                || self.weather_cycle_proof.is_some()
                || self.water_cycle_proof.is_some()
                || self.vegetation_cycle_proof.is_some()
                || self.global_cycle_proof.is_some())
        {
            return Err("--fixed-proof-view requires a bounded WorldSketch capture; no metrics, movement, resize or cycle probes".into());
        }
        if self.weather_input.is_some() && !self.visual_valley && !self.world_sketch_visual_valley {
            return Err(
                "--weather-input requires --visual-valley or --world-sketch-visual-valley"
                    .to_owned(),
            );
        }
        let input_count = usize::from(self.scene_path.is_some())
            + usize::from(self.terrain_path.is_some())
            + usize::from(self.visual_valley)
            + usize::from(self.world_sketch_visual_valley);
        if input_count > 1 {
            return Err(
                "--scene, --terrain, --visual-valley and --world-sketch-visual-valley are mutually exclusive"
                    .to_owned(),
            );
        }
        if self.terrain_debug.is_enabled()
            && self.terrain_path.is_none()
            && !self.visual_valley
            && !self.world_sketch_visual_valley
        {
            return Err(
                "--terrain-debug requires --visual-valley, --world-sketch-visual-valley or --terrain"
                    .to_owned(),
            );
        }
        if self.weather_state.is_some() && !self.visual_valley && !self.world_sketch_visual_valley {
            return Err(
                "--weather requires --visual-valley or --world-sketch-visual-valley".to_owned(),
            );
        }
        self.validate_governed_asset_kit()?;
        if self.rain_quality.is_some() && !self.visual_valley && !self.world_sketch_visual_valley {
            return Err(
                "--rain-quality requires --visual-valley or --world-sketch-visual-valley"
                    .to_owned(),
            );
        }
        if self.shadow_quality.is_some() && !self.visual_valley && !self.world_sketch_visual_valley
        {
            return Err(
                "--shadow-quality requires --visual-valley or --world-sketch-visual-valley"
                    .to_owned(),
            );
        }
        if self.terrain_quality.is_some() && !self.visual_valley && !self.world_sketch_visual_valley
        {
            return Err(
                "--terrain-quality requires --visual-valley or --world-sketch-visual-valley"
                    .to_owned(),
            );
        }
        if self.post_process_quality.is_some()
            && !self.visual_valley
            && !self.world_sketch_visual_valley
        {
            return Err(
                "--post-process-quality requires --visual-valley or --world-sketch-visual-valley"
                    .to_owned(),
            );
        }
        if self.water_quality.is_some() && !self.visual_valley && !self.world_sketch_visual_valley {
            return Err(
                "--water-quality requires --visual-valley or --world-sketch-visual-valley"
                    .to_owned(),
            );
        }
        if self.environment_quality.is_some()
            && !self.visual_valley
            && !self.world_sketch_visual_valley
        {
            return Err(
                "--environment-quality requires --visual-valley or --world-sketch-visual-valley"
                    .to_owned(),
            );
        }
        self.validate_bridge()?;
        if self.vegetation_quality.is_some()
            && !self.visual_valley
            && !self.world_sketch_visual_valley
        {
            return Err(
                "--vegetation-quality requires --visual-valley or --world-sketch-visual-valley"
                    .to_owned(),
            );
        }
        self.validate_environment_cycle_proof()?;
        self.validate_terrain_partial_update_probe()?;
        self.validate_player_camera()?;
        if self.capture_path.is_some()
            && self
                .exit_after_frames
                .is_some_and(|frame| frame <= self.capture_frame.saturating_add(5))
        {
            return Err(
                "--exit-after-frames must be at least six frames after --capture-frame".to_owned(),
            );
        }
        match (self.resize_at_frame, self.resize_width, self.resize_height) {
            (None, None, None) => {}
            (Some(_), Some(width), Some(height)) if width > 0 && height > 0 => {}
            (Some(_), Some(_), Some(_)) => {
                return Err("automated resize dimensions must be greater than zero".to_owned());
            }
            _ => {
                return Err("--resize-at-frame, --resize-width and --resize-height must be provided together".to_owned());
            }
        }
        if self.metrics_path.is_some()
            && self
                .exit_after_frames
                .is_none_or(|exit_frame| exit_frame <= self.metrics_warmup_frames)
        {
            return Err(
                "--metrics requires --exit-after-frames after --metrics-warmup-frames".to_owned(),
            );
        }
        if let Some(resize_frame) = self.resize_at_frame {
            if self
                .exit_after_frames
                .is_none_or(|exit_frame| exit_frame <= resize_frame.saturating_add(5))
            {
                return Err(
                    "automated resize requires --exit-after-frames at least six frames later"
                        .to_owned(),
                );
            }
            if self.capture_path.is_some() && self.capture_frame <= resize_frame.saturating_add(5) {
                return Err(
                    "--capture-frame must be at least six frames after --resize-at-frame"
                        .to_owned(),
                );
            }
        }
        if self.close_at_frame.is_some()
            && (self.capture_path.is_some()
                || self.metrics_path.is_some()
                || self.exit_after_frames.is_some()
                || self.resize_at_frame.is_some()
                || self.terrain_partial_update_probe.is_enabled()
                || self.bridge_crossing_probe)
        {
            return Err(
                "--close-at-frame is a standalone lifecycle probe and cannot be combined with capture, metrics, bounded exit or resize"
                    .to_owned(),
            );
        }
        if self.log_probe
            && (self.scene_path.is_some()
                || self.terrain_path.is_some()
                || self.visual_valley
                || self.world_sketch_visual_valley
                || self.asset_kit_build_path.is_some()
                || self.asset_kit_preview_path.is_some()
                || self.bridge_path.is_some()
                || self.bridge_preview
                || self.bridge_crossing_probe
                || self.terrain_debug.is_enabled()
                || self.terrain_partial_update_probe.is_enabled()
                || self.player_camera.is_enabled()
                || self.weather_state.is_some()
                || self.capture_path.is_some()
                || self.metrics_path.is_some()
                || self.exit_after_frames.is_some()
                || self.resize_at_frame.is_some()
                || self.close_at_frame.is_some()
                || !self.vsync)
        {
            return Err("--log-probe cannot be combined with runtime options".to_owned());
        }
        Ok(())
    }

    fn validate_player_camera(&self) -> Result<(), String> {
        if self.player_camera.is_probe() && self.asset_kit_build_path.is_some() {
            return Err(
                "the governed asset-kit cannot be combined with the specialized --player-camera-probe"
                    .to_owned(),
            );
        }
        if self.player_camera.is_enabled() && self.bridge_crossing_probe {
            return Err(
                "--bridge-crossing-probe cannot be combined with --player-camera or --player-camera-probe"
                    .to_owned(),
            );
        }
        if self.player_camera.is_enabled() && self.bridge_preview {
            return Err(
                "--bridge-preview cannot be combined with --player-camera or --player-camera-probe"
                    .to_owned(),
            );
        }
        if self.player_camera.is_enabled() && self.terrain_partial_update_probe.is_enabled() {
            return Err("--terrain-partial-update-probe cannot be combined with --player-camera or --player-camera-probe".to_owned());
        }
        if self.player_camera.is_enabled()
            && self.terrain_path.is_none()
            && !self.visual_valley
            && !self.world_sketch_visual_valley
        {
            let option = if self.player_camera.is_probe() {
                "--player-camera-probe"
            } else {
                "--player-camera"
            };
            return Err(format!(
                "{option} requires --visual-valley, --world-sketch-visual-valley or --terrain"
            ));
        }
        if self.player_camera.is_probe()
            && (self.capture_path.is_some()
                || self.metrics_path.is_some()
                || self.exit_after_frames.is_some()
                || self.resize_at_frame.is_some()
                || self.close_at_frame.is_some()
                || self.log_probe)
        {
            return Err("--player-camera-probe is self-bounded and cannot be combined with capture, metrics, bounded exit, resize, close or logging probes".to_owned());
        }
        Ok(())
    }

    fn validate_terrain_partial_update_probe(&self) -> Result<(), String> {
        if self.terrain_partial_update_probe.is_enabled() && self.bridge_crossing_probe {
            return Err(
                "--terrain-partial-update-probe cannot be combined with --bridge-crossing-probe"
                    .to_owned(),
            );
        }
        if self.terrain_partial_update_probe.is_enabled() && self.player_camera.is_enabled() {
            return Err("--terrain-partial-update-probe cannot be combined with --player-camera or --player-camera-probe".to_owned());
        }
        if self.terrain_partial_update_probe.is_enabled()
            && self.terrain_path.is_none()
            && !self.visual_valley
            && !self.world_sketch_visual_valley
        {
            return Err(
                "--terrain-partial-update-probe requires --visual-valley, --world-sketch-visual-valley or --terrain"
                    .to_owned(),
            );
        }
        if self.terrain_partial_update_probe.is_enabled()
            && (self.capture_path.is_some()
                || self.metrics_path.is_some()
                || self.exit_after_frames.is_some()
                || self.resize_at_frame.is_some()
                || self.close_at_frame.is_some()
                || self.log_probe)
        {
            return Err("--terrain-partial-update-probe is self-bounded and cannot be combined with capture, metrics, bounded exit, resize, close or logging probes".to_owned());
        }
        Ok(())
    }

    fn validate_bridge(&self) -> Result<(), String> {
        if self.bridge_path.is_some()
            && self.terrain_path.is_none()
            && !self.world_sketch_visual_valley
        {
            return Err(
                "--bridge-placement requires --terrain or --world-sketch-visual-valley".to_owned(),
            );
        }
        if self.bridge_preview && self.bridge_path.is_none() {
            return Err("--bridge-preview requires --bridge-placement".to_owned());
        }
        if self.bridge_frames.is_some() && !self.bridge_crossing_probe {
            return Err("--bridge-frames requires --bridge-crossing-probe".to_owned());
        }
        if self.bridge_crossing_probe
            && self.bridge_path.is_none()
            && !self.world_sketch_visual_valley
        {
            return Err("--bridge-crossing-probe requires --bridge-placement".to_owned());
        }
        if self.bridge_preview && self.bridge_crossing_probe {
            return Err(
                "--bridge-preview and --bridge-crossing-probe are mutually exclusive".to_owned(),
            );
        }
        if self.bridge_crossing_probe
            && (self.capture_path.is_some()
                || self.metrics_path.is_some()
                || self.exit_after_frames.is_some()
                || self.resize_at_frame.is_some()
                || self.close_at_frame.is_some()
                || self.log_probe)
        {
            return Err("--bridge-crossing-probe is self-bounded and cannot be combined with capture, metrics, bounded exit, resize, close or logging probes".to_owned());
        }
        Ok(())
    }

    fn validate_governed_asset_kit(&self) -> Result<(), String> {
        // Only the chunk-batched governed path has a current projection receipt.
        // The cycle still requires the engine's full effective GPU acknowledgment.
        if self.prototype_forest_preview
            && !self.prototype_far_batches
            && self.global_cycle_proof.is_some()
        {
            return Err("--quality-cycle-proof cannot qualify --prototype-forest-preview or --prototype-forest-batches: prototype vegetation is Unqualified; ordinary preview captures remain available, without global qualification".into());
        }
        if self.prototype_forest_preview && self.prepare_vegetation_catalog_path.is_none() {
            return Err("--prototype-forest-preview requires --prepare-vegetation-catalog".into());
        }
        if self.prepare_vegetation_catalog_path.is_some() && self.asset_kit_build_path.is_none() {
            return Err(
                "--prepare-vegetation-catalog requires --asset-kit-build and --asset-kit-preview"
                    .into(),
            );
        }
        match (
            self.asset_kit_build_path.as_ref(),
            self.asset_kit_preview_path.as_ref(),
        ) {
            (None, None) => return Ok(()),
            (Some(_), None) => return Err("--asset-kit-build requires --asset-kit-preview".into()),
            (None, Some(_)) => return Err("--asset-kit-preview requires --asset-kit-build".into()),
            (Some(_), Some(_)) => {}
        }
        if !self.world_sketch_visual_valley {
            return Err(
                "--asset-kit-build and --asset-kit-preview require --world-sketch-visual-valley"
                    .into(),
            );
        }
        Ok(())
    }

    fn runtime_automation(&self) -> RuntimeAutomation {
        RuntimeAutomation {
            capture_path: self.capture_path.clone(),
            capture_frame: self.capture_frame,
            metrics_path: self.metrics_path.clone(),
            metrics_warmup_frames: self.metrics_warmup_frames,
            exit_after_frames: self.exit_after_frames,
            resize_request: match (self.resize_at_frame, self.resize_width, self.resize_height) {
                (Some(frame), Some(width), Some(height)) => Some(WindowResizeRequest {
                    frame,
                    width,
                    height,
                }),
                _ => None,
            },
            close_at_frame: self.close_at_frame,
        }
    }

    fn validate_weather_cycle_proof(&self) -> Result<(), String> {
        if self.weather_cycle_proof.is_some()
            && (!(self.visual_valley || self.world_sketch_visual_valley)
                || self.weather_input.is_none()
                || self.weather_state.is_some()
                || self.adaptive_quality
                || self.environment_cycle_proof.is_some()
                || self.water_cycle_proof.is_some()
                || self.vegetation_cycle_proof.is_some()
                || self.global_cycle_proof.is_some()
                || automated_runtime_requested(&self.runtime_automation())
                || self.bridge_crossing_probe
                || self.bridge_frames.is_some()
                || self.player_camera != PlayerCameraRuntimeMode::Disabled
                || self.terrain_partial_update_probe != TerrainPartialUpdateProbeMode::Disabled)
        {
            return Err("--weather-cycle-proof requires valley --weather-input and excludes weather override, adaptive quality and other automation/player probes".into());
        }
        Ok(())
    }

    fn is_candidate_input(flag: &str) -> bool {
        matches!(
            flag,
            "--world-sketch-input"
                | "--water-optics-isolation"
                | "--weather-input"
                | "--water-medium-input"
                | "--walk-shadow-profile"
                | "--sky-ibl-proof-output"
        )
    }

    fn set_candidate_input(
        &mut self,
        arguments: &mut impl Iterator<Item = String>,
        argument: &str,
    ) -> Result<(), String> {
        if argument == "--water-optics-isolation" {
            return water_optics_isolation::select(&mut self.water_optics_isolation, arguments);
        }
        let destination = match argument {
            "--weather-input" => &mut self.weather_input,
            "--water-medium-input" => &mut self.water_medium_input,
            "--walk-shadow-profile" => &mut self.walk_shadow_profile,
            "--sky-ibl-proof-output" => &mut self.sky_ibl_proof_output,
            _ => &mut self.world_sketch_input,
        };
        set_unique_path(destination, arguments, argument)
    }

    fn load_document(&self) -> Result<RuntimeDocument, String> {
        let mut document = self.load_document_source()?;
        if let Some(path) = &self.water_medium_input {
            water_medium_input::apply(path, &mut document)?;
        }
        if let Some(isolation) = self.water_optics_isolation {
            water_optics_isolation::apply(isolation, &mut document)?;
        }
        Ok(document)
    }

    fn load_document_source(&self) -> Result<RuntimeDocument, String> {
        if let Some(path) = &self.scene_path {
            let json = fs::read_to_string(path)
                .map_err(|error| format!("cannot read {}: {error}", path.display()))?;
            let document = SceneProofDocument::from_json(&json)
                .map_err(|error| format!("invalid scene {}: {error}", path.display()))?;
            return Ok(RuntimeDocument::Gate0(Box::new(document)));
        }
        if let Some(path) = &self.terrain_path {
            let json = fs::read_to_string(path)
                .map_err(|error| format!("cannot read {}: {error}", path.display()))?;
            let document = TerrainDocument::from_json(&json)
                .map_err(|error| format!("invalid terrain {}: {error}", path.display()))?;
            let source = path.display().to_string();
            return validated_visual_valley_document(
                document, None, None, None, None, None, &source,
            );
        }
        if self.world_sketch_visual_valley {
            let world_sketch = if let Some(path) = &self.world_sketch_input {
                let json = fs::read_to_string(path)
                    .map_err(|error| format!("cannot read {}: {error}", path.display()))?;
                forge_world::WorldSketchDocument::from_json(&json)
                    .map_err(|error| format!("invalid WorldSketch {}: {error}", path.display()))?
            } else {
                embedded_visual_valley_world_sketch().map_err(|error| {
                    format!("invalid embedded Visual Valley WorldSketch: {error}")
                })?
            };
            let compiled = compile_world_sketch(&world_sketch)
                .map_err(|error| format!("cannot compile Visual Valley WorldSketch: {error}"))?;
            let rivers: Vec<_> = compiled
                .spline_carving
                .splines
                .iter()
                .filter(|spline| spline.kind == forge_world::WorldSketchSplineKind::River)
                .collect();
            if rivers.len() != 1 {
                return Err("WorldSketch runtime requires exactly one primary river".to_owned());
            }
            let mut environment = embedded_visual_valley_environment_profile()
                .map_err(|error| format!("invalid embedded Visual Valley environment: {error}"))?;
            let mut water = embedded_visual_valley_water_document()
                .map_err(|error| format!("invalid embedded Visual Valley water: {error}"))?;
            let weather =
                weather_input::load(self.weather_input.as_deref(), Some(world_sketch.revision))?;
            // These profiles are derived copies for this WorldSketch revision, not edits
            // to the embedded templates. Project/area compatibility is still validated.
            environment.revision = world_sketch.revision;
            water.revision = world_sketch.revision;
            let surface = forge_world::RoadSurfaceProjection::from_compiled(&compiled)
                .map_err(|error| format!("invalid compiled road surface: {error}"))?;
            let mut document = validated_visual_valley_document(
                compiled.carved_terrain,
                Some(compiled.vegetation),
                Some(environment),
                Some(
                    water_from_carved_spline(water, rivers[0])
                        .map_err(|error| format!("invalid carved river water: {error}"))?,
                ),
                Some(weather),
                Some(world_sketch),
                "compiled Visual Valley WorldSketch",
            )?;
            if let RuntimeDocument::VisualValley { road_surface, .. } = &mut document {
                *road_surface = Some(Box::new(surface));
            }
            if let RuntimeDocument::VisualValley { derived_bridge, .. } = &mut document
                && !compiled.bridge_placements.placements.is_empty()
            {
                *derived_bridge = Some(Box::new(compiled.bridge_placements));
            }
            return Ok(document);
        }
        if self.visual_valley {
            let document = embedded_visual_valley_terrain_document()
                .map_err(|error| format!("invalid embedded Visual Valley terrain: {error}"))?;
            let vegetation = embedded_visual_valley_vegetation_document()
                .map_err(|error| format!("invalid embedded Visual Valley vegetation: {error}"))?;
            let environment = embedded_visual_valley_environment_profile()
                .map_err(|error| format!("invalid embedded Visual Valley environment: {error}"))?;
            let water = embedded_visual_valley_water_document()
                .map_err(|error| format!("invalid embedded Visual Valley water: {error}"))?;
            let weather = weather_input::load(self.weather_input.as_deref(), None)?;
            return validated_visual_valley_document(
                document,
                Some(vegetation),
                Some(environment),
                Some(water),
                Some(weather),
                None,
                "embedded Visual Valley terrain",
            );
        }
        embedded_gate0_proof_document()
            .map(|document| RuntimeDocument::Gate0(Box::new(document)))
            .map_err(|error| error.to_string())
    }

    fn load_governed_asset_kit(&self) -> Result<Option<LoadedGovernedAssetKit>, String> {
        match (
            self.asset_kit_build_path.as_ref(),
            self.asset_kit_preview_path.as_ref(),
        ) {
            (Some(build_path), Some(preview_path)) => {
                LoadedGovernedAssetKit::load(build_path, preview_path)
                    .map(Some)
                    .map_err(|error| error.to_string())
            }
            (None, None) => Ok(None),
            _ => Err("governed asset-kit paths were not validated as a pair".to_owned()),
        }
    }

    fn load_bridge_document(&self) -> Result<Option<WorldSketchBridgePlacementDocument>, String> {
        let Some(path) = &self.bridge_path else {
            return Ok(None);
        };
        let json = fs::read_to_string(path)
            .map_err(|error| format!("cannot read {}: {error}", path.display()))?;
        WorldSketchBridgePlacementDocument::from_json(&json)
            .map(Some)
            .map_err(|error| format!("invalid bridge placements {}: {error}", path.display()))
    }

    fn load_bridge_for_scene(
        &self,
        scene: &mut RuntimeDocument,
    ) -> Result<Option<WorldSketchBridgePlacementDocument>, String> {
        // An explicit input wins, including its failures: never hide a bad file with a fallback.
        if self.bridge_path.is_some() {
            return self.load_bridge_document();
        }
        Ok(match scene {
            RuntimeDocument::VisualValley { derived_bridge, .. } => {
                derived_bridge.take().map(|bridge| *bridge)
            }
            RuntimeDocument::Gate0(_) => None,
        })
    }

    fn prepare_output_paths(&self) -> Result<(), String> {
        for (label, path) in [
            ("capture", self.capture_path.as_deref()),
            ("metrics", self.metrics_path.as_deref()),
        ] {
            let Some(path) = path else {
                continue;
            };
            if let Some(parent) = path
                .parent()
                .filter(|parent| !parent.as_os_str().is_empty())
            {
                fs::create_dir_all(parent).map_err(|error| {
                    format!(
                        "cannot create {label} directory {}: {error}",
                        parent.display()
                    )
                })?;
            }
            match fs::remove_file(path) {
                Ok(()) => {}
                Err(error) if error.kind() == std::io::ErrorKind::NotFound => {}
                Err(error) => {
                    return Err(format!(
                        "cannot clear previous {label} output {}: {error}",
                        path.display()
                    ));
                }
            }
        }
        Ok(())
    }
}

fn initialize_structured_tracing() -> Result<RuntimeErrors, String> {
    let fallback = format!("info,{DEFAULT_FILTER},forge=info");
    let filter = EnvFilter::try_from_default_env().unwrap_or_else(|_| EnvFilter::new(fallback));
    let errors = RuntimeErrors::default();
    let json = tracing_subscriber::fmt::layer()
        .json()
        .flatten_event(true)
        .with_current_span(true)
        .with_span_list(true)
        .with_target(true)
        .with_thread_ids(true)
        .with_file(true)
        .with_line_number(true)
        .with_writer(std::io::stderr)
        .with_filter(filter);
    tracing_subscriber::registry()
        .with(
            errors
                .clone()
                .with_filter(tracing_subscriber::filter::LevelFilter::ERROR),
        )
        .with(json)
        .try_init()
        .map_err(|error| error.to_string())?;
    Ok(errors)
}

fn startup_error(phase: &str, message: &str) -> AppExit {
    tracing::error!(
        target: "forge::runtime",
        schema = "forge.runtime-event",
        schema_version = 1_u64,
        event_kind = "startup_failed",
        phase,
        error = message,
        exit_code = 2_u64,
        "runtime startup failed"
    );
    AppExit::from_code(2)
}

const fn present_mode_for_vsync(vsync: bool) -> PresentMode {
    if vsync {
        PresentMode::AutoVsync
    } else {
        PresentMode::AutoNoVsync
    }
}

const fn present_mode_label(mode: PresentMode) -> &'static str {
    match mode {
        PresentMode::AutoVsync => "auto_vsync",
        PresentMode::AutoNoVsync => "auto_no_vsync",
        PresentMode::Fifo => "fifo",
        PresentMode::FifoRelaxed => "fifo_relaxed",
        PresentMode::Immediate => "immediate",
        PresentMode::Mailbox => "mailbox",
    }
}

fn automated_runtime_requested(automation: &RuntimeAutomation) -> bool {
    automation.capture_path.is_some()
        || automation.metrics_path.is_some()
        || automation.exit_after_frames.is_some()
        || automation.resize_request.is_some()
        || automation.close_at_frame.is_some()
}

fn cursor_options_for_player_camera(player_input_enabled: bool) -> CursorOptions {
    if player_input_enabled {
        CursorOptions {
            visible: false,
            grab_mode: CursorGrabMode::Locked,
            ..default()
        }
    } else {
        CursorOptions::default()
    }
}

fn app_exit_code(exit: &AppExit) -> u8 {
    match exit {
        AppExit::Success => 0,
        AppExit::Error(code) => code.get(),
    }
}

fn is_visual_quality_option(option: &str) -> bool {
    matches!(
        option,
        "--shadow-quality"
            | "--quality"
            | "--terrain-quality"
            | "--post-process-quality"
            | "--water-quality"
            | "--environment-quality"
            | "--vegetation-quality"
    )
}

fn set_unique_path(
    destination: &mut Option<PathBuf>,
    arguments: &mut impl Iterator<Item = String>,
    option: &str,
) -> Result<(), String> {
    if destination.is_some() {
        return Err(format!("{option} may only be specified once"));
    }
    *destination = Some(next_path(arguments, option)?);
    Ok(())
}

fn next_path(
    arguments: &mut impl Iterator<Item = String>,
    option: &str,
) -> Result<PathBuf, String> {
    arguments
        .next()
        .map(PathBuf::from)
        .ok_or_else(|| format!("{option} requires a path"))
}

fn next_u32(arguments: &mut impl Iterator<Item = String>, option: &str) -> Result<u32, String> {
    arguments
        .next()
        .ok_or_else(|| format!("{option} requires an integer"))?
        .parse()
        .map_err(|_| format!("{option} requires an unsigned integer"))
}

fn next_weather_state(
    arguments: &mut impl Iterator<Item = String>,
) -> Result<WeatherState, String> {
    match arguments.next().as_deref() {
        Some("clear") => Ok(WeatherState::Clear),
        Some("rain") => Ok(WeatherState::Rain),
        Some(value) => Err(format!(
            "--weather requires `clear` or `rain`, received `{value}`"
        )),
        None => Err("--weather requires `clear` or `rain`".to_owned()),
    }
}

const fn weather_state_label(state: WeatherState) -> &'static str {
    match state {
        WeatherState::Clear => "clear",
        WeatherState::Rain => "rain",
    }
}

#[cfg(test)]
mod tests {
    #[test]
    fn cloud_preview_requires_explicit_scene_and_unique_input() {
        use super::Options;
        let parse = |args: &[&str]| Options::parse(args.iter().map(|arg| (*arg).to_owned()));
        assert!(parse(&["--cloud-layer-preview", "clouds.json"]).is_err());
        assert!(parse(&["--visual-valley", "--cloud-layer-preview", "clouds.json"]).is_ok());
        assert!(
            parse(&[
                "--visual-valley",
                "--cloud-layer-preview",
                "a.json",
                "--cloud-layer-preview",
                "b.json"
            ])
            .is_err()
        );
    }
    #[test]
    fn walk_proof_requires_bounded_interactive_player_and_no_competing_probe() {
        let parse = |args: &[&str]| Options::parse(args.iter().map(|arg| (*arg).to_owned()));
        let base = [
            "--world-sketch-visual-valley",
            "--player-camera",
            "--exit-after-frames",
            "2600",
            "--walk-proof",
            "1400:-0.5:1",
        ];
        assert!(parse(&base).is_ok());
        assert!(!parse(&base).unwrap().no_pipeline_statistics);
        let moving = [base.as_slice(), &["--walk-proof-measure-moving"]].concat();
        assert!(parse(&moving).unwrap().walk_proof_measure_moving);
        for conflicting in [
            "--walk-proof-measure",
            "--walk-proof-measure-frozen",
            "--walk-proof-measure-moving",
        ] {
            assert!(parse(&[moving.as_slice(), &[conflicting]].concat()).is_err());
        }
        assert!(parse(&[moving.as_slice(), &["--capture", "moving.png"]].concat()).is_err());
        let profile = ["--walk-shadow-profile", "probe.json"];
        assert!(parse(&[base.as_slice(), &profile].concat()).is_err());
        let captured = [
            base.as_slice(),
            &profile,
            &["--shadow-quality", "high", "--capture", "proof.png"],
        ]
        .concat();
        assert!(parse(&captured).is_ok());
        assert!(parse(&[captured.as_slice(), &["--walk-proof-measure-frozen"]].concat()).is_err());
        let measured = [base.as_slice(), &["--walk-proof-measure-frozen"], &profile].concat();
        assert!(parse(&[measured.as_slice(), &["--quality", "high"]].concat()).is_err());
        assert!(parse(&[measured.as_slice(), &["--shadow-quality", "high"]].concat()).is_ok());
        assert!(
            parse(&[measured.as_slice(), &["--shadow-quality", "high"], &profile].concat())
                .is_err()
        );
        assert!(parse(&[base.as_slice(), &["--no-pipeline-statistics"]].concat()).is_err());
        assert!(
            parse(
                &[
                    base.as_slice(),
                    &["--walk-proof-measure-frozen", "--no-pipeline-statistics"],
                ]
                .concat()
            )
            .unwrap()
            .no_pipeline_statistics
        );
        assert!(parse(&["--walk-proof", "10:0:1"]).is_err());
        for extra in [
            vec!["--bridge-crossing-probe"],
            vec!["--walk-proof", "10:0:1"],
            vec!["--fixed-proof-view", "human_bridge"],
        ] {
            assert!(parse(&[base.as_slice(), &extra].concat()).is_err());
        }
    }

    #[test]
    fn sky_readback_requires_bounded_capture_and_rejects_duplicate_output() {
        let base = [
            "--visual-valley",
            "--capture",
            "proof.png",
            "--exit-after-frames",
            "1200",
            "--sky-ibl-proof-output",
            "sky.bin",
        ];
        assert!(Options::parse(base.into_iter().map(str::to_owned)).is_ok());
        assert!(
            Options::parse(
                ["--sky-ibl-proof-output", "sky.bin"]
                    .into_iter()
                    .map(str::to_owned)
            )
            .is_err()
        );
        assert!(
            Options::parse(
                base.into_iter()
                    .chain(["--sky-ibl-proof-output", "other.bin"])
                    .map(str::to_owned)
            )
            .is_err()
        );
    }
    use forge_world::embedded_visual_valley_weather_profile;

    #[test]
    fn moving_global_cycle_is_bounded_and_does_not_allow_measurement() {
        let base = [
            "--world-sketch-visual-valley",
            "--player-camera",
            "--walk-proof",
            "3600:1:0.22",
            "--quality",
            "high",
            "--quality-cycle-proof",
            "fresh",
        ];
        assert!(Options::parse(base.into_iter().map(str::to_owned)).is_ok());
        for extra in [
            vec!["--walk-proof-measure"],
            vec!["--exit-after-frames", "500"],
            vec!["--fixed-proof-view", "human_bridge"],
        ] {
            let args = [base.as_slice(), extra.as_slice()].concat();
            assert!(Options::parse(args.into_iter().map(str::to_owned)).is_err());
        }
    }

    #[test]
    fn fixed_proof_view_requires_bounded_capture_and_excludes_live_workloads() {
        let base = [
            "--world-sketch-visual-valley",
            "--fixed-proof-view",
            "human_bridge",
            "--capture",
            "proof.png",
            "--exit-after-frames",
            "420",
        ];
        let options = Options::parse(base.into_iter().map(str::to_owned)).unwrap();
        assert_eq!(options.fixed_proof_view.as_deref(), Some("human_bridge"));
        for steps in ["4", "12", "32"] {
            let args = [base.as_slice(), &["--fixed-proof-steps", steps]].concat();
            assert!(Options::parse(args.into_iter().map(str::to_owned)).is_ok());
        }
        for steps in ["0", "3", "33"] {
            let args = [base.as_slice(), &["--fixed-proof-steps", steps]].concat();
            assert!(Options::parse(args.into_iter().map(str::to_owned)).is_err());
        }
        assert!(
            Options::parse(["--fixed-proof-steps", "4"].into_iter().map(str::to_owned)).is_err()
        );
        for extra in [
            vec!["--metrics", "metrics.json"],
            vec!["--player-camera"],
            vec!["--adaptive-quality"],
            vec!["--fixed-proof-view", "human_bridge"],
            vec!["--player-camera-probe"],
            vec!["--quality-cycle-proof", "proof"],
        ] {
            let args = [base.as_slice(), extra.as_slice()].concat();
            assert!(Options::parse(args.into_iter().map(str::to_owned)).is_err());
        }
        assert!(
            Options::parse(
                ["--fixed-proof-view", "human_bridge"]
                    .into_iter()
                    .map(str::to_owned)
            )
            .is_err()
        );
        assert!(
            Options::parse(
                ["--world-sketch-visual-valley", "--fixed-proof-view"]
                    .into_iter()
                    .map(str::to_owned)
            )
            .is_err()
        );
    }

    #[test]
    fn adaptive_quality_is_opt_in_and_excludes_fixed_proofs() {
        let base = ["--world-sketch-visual-valley", "--quality", "high"];
        assert!(
            !super::Options::parse(base.into_iter().map(str::to_owned))
                .unwrap()
                .adaptive_quality
        );
        let adaptive = [
            base.as_slice(),
            &["--adaptive-quality", "--exit-after-frames", "6000"],
        ]
        .concat();
        assert!(
            super::Options::parse(adaptive.into_iter().map(str::to_owned))
                .unwrap()
                .adaptive_quality
        );
        for extra in [
            vec!["--metrics", "metrics.json"],
            vec!["--capture", "capture.png"],
            vec!["--quality-cycle-proof", "fresh"],
            vec!["--adaptive-quality"],
            vec!["--volumetric-preview"],
        ] {
            let args = [base.as_slice(), &["--adaptive-quality"], &extra].concat();
            assert!(super::Options::parse(args.into_iter().map(str::to_owned)).is_err());
        }
        assert!(
            super::Options::parse(
                ["--visual-valley", "--adaptive-quality"]
                    .into_iter()
                    .map(str::to_owned)
            )
            .is_err()
        );
    }
    use super::*;

    #[test]
    fn world_sketch_bridge_is_automatic_and_consumed_once() {
        let options = Options::parse(
            [
                "--world-sketch-visual-valley".to_owned(),
                "--bridge-crossing-probe".to_owned(),
            ]
            .into_iter(),
        )
        .unwrap();
        let mut scene = options.load_document().unwrap();
        let bridge = options.load_bridge_for_scene(&mut scene).unwrap().unwrap();
        let compiled =
            compile_world_sketch(&embedded_visual_valley_world_sketch().unwrap()).unwrap();
        assert_eq!(bridge, compiled.bridge_placements);
        assert!(options.load_bridge_for_scene(&mut scene).unwrap().is_none());
        let legacy = Options::parse(["--visual-valley".to_owned()].into_iter()).unwrap();
        assert!(
            legacy
                .load_bridge_for_scene(&mut legacy.load_document().unwrap())
                .unwrap()
                .is_none()
        );
    }

    #[test]
    fn invalid_explicit_bridge_does_not_fall_back_to_compiled_bridge() {
        let mut options =
            Options::parse(["--world-sketch-visual-valley".to_owned()].into_iter()).unwrap();
        let mut scene = options.load_document().unwrap();
        options.bridge_path = Some(
            std::env::temp_dir().join(format!("forge-missing-bridge-{}.json", std::process::id())),
        );
        assert!(options.load_bridge_for_scene(&mut scene).is_err());
        options.bridge_path = None;
        assert!(options.load_bridge_for_scene(&mut scene).unwrap().is_some());
    }

    #[test]
    fn options_reject_exit_before_capture_has_time_to_finish() {
        let result = Options::parse(
            [
                "--capture".to_owned(),
                "proof.png".to_owned(),
                "--capture-frame".to_owned(),
                "10".to_owned(),
                "--exit-after-frames".to_owned(),
                "12".to_owned(),
            ]
            .into_iter(),
        );
        assert!(result.is_err());

        let maximal_capture_frame = Options::parse(
            [
                "--capture".to_owned(),
                "proof.png".to_owned(),
                "--capture-frame".to_owned(),
                u32::MAX.to_string(),
                "--exit-after-frames".to_owned(),
                u32::MAX.to_string(),
            ]
            .into_iter(),
        );
        assert!(maximal_capture_frame.is_err());
    }

    #[test]
    fn default_scene_is_valid_and_deterministic() {
        let first = Options::default().load_document().unwrap();
        let second = Options::default().load_document().unwrap();
        assert_eq!(first.mode_label(), "gate0_scene_proof");
        assert_eq!(first.window_title(), GATE0_WINDOW_TITLE);
        let RuntimeDocument::Gate0(first) = first else {
            panic!("the default runtime input must remain the Gate 0 proof scene");
        };
        let RuntimeDocument::Gate0(second) = second else {
            panic!("the default runtime input must remain the Gate 0 proof scene");
        };
        assert_eq!(
            first.content_hash().unwrap(),
            second.content_hash().unwrap()
        );
    }

    #[test]
    fn visual_valley_is_an_explicit_strict_gate1_mode() {
        let options = Options::parse(["--visual-valley".to_owned()].into_iter()).unwrap();
        let loaded = options.load_document().unwrap();

        assert_eq!(loaded.mode_label(), "gate1_visual_valley");
        assert_eq!(loaded.window_title(), VISUAL_VALLEY_WINDOW_TITLE);
        let RuntimeDocument::VisualValley {
            terrain: loaded,
            vegetation: Some(loaded_vegetation),
            environment: Some(loaded_environment),
            water: Some(loaded_water),
            weather: Some(loaded_weather),
            world_sketch: None,
            derived_bridge: None,
            road_surface: None,
        } = loaded
        else {
            panic!("--visual-valley must select the complete embedded Gate 1 document set");
        };
        let embedded = embedded_visual_valley_terrain_document().unwrap();
        let embedded_vegetation = embedded_visual_valley_vegetation_document().unwrap();
        let embedded_environment = embedded_visual_valley_environment_profile().unwrap();
        let embedded_water = embedded_visual_valley_water_document().unwrap();
        let embedded_weather = embedded_visual_valley_weather_profile().unwrap();
        assert_eq!(*loaded, embedded);
        assert_eq!(*loaded_vegetation, embedded_vegetation);
        assert_eq!(*loaded_environment, embedded_environment);
        assert_eq!(*loaded_water, embedded_water);
        assert_eq!(*loaded_weather, embedded_weather);
        assert_eq!(
            loaded.content_hash().unwrap(),
            embedded.content_hash().unwrap()
        );
    }

    #[test]
    fn world_sketch_visual_valley_uses_the_compiled_carved_terrain_and_complete_scene() {
        let options =
            Options::parse(["--world-sketch-visual-valley".to_owned()].into_iter()).unwrap();
        let loaded = options.load_document().unwrap();

        assert_eq!(loaded.mode_label(), "gate1_world_sketch_visual_valley");
        let RuntimeDocument::VisualValley {
            terrain,
            vegetation: Some(vegetation),
            environment: Some(_),
            water: Some(water),
            weather: Some(_),
            world_sketch: Some(world_sketch),
            derived_bridge: Some(_),
            road_surface: Some(_),
        } = loaded
        else {
            panic!("WorldSketch mode must load the complete compiled Visual Valley scene");
        };
        let embedded = embedded_visual_valley_world_sketch().unwrap();
        let compiled = compile_world_sketch(&embedded).unwrap();
        let river = compiled
            .spline_carving
            .splines
            .iter()
            .find(|spline| spline.kind == forge_world::WorldSketchSplineKind::River)
            .unwrap();
        assert_eq!(
            water.river.control_points.len(),
            river.centerline_samples.len()
        );
        assert_eq!(
            water.river.control_points[0].center_m.x.to_bits(),
            river.centerline_samples[0].x_m.to_bits()
        );
        assert_eq!(
            world_sketch.content_hash().unwrap(),
            embedded.content_hash().unwrap()
        );
        assert_eq!(
            terrain.content_hash().unwrap(),
            compiled.carved_terrain.content_hash().unwrap()
        );
        assert_eq!(
            vegetation.content_hash().unwrap(),
            compiled.vegetation.content_hash().unwrap()
        );
    }

    #[test]
    fn visual_valley_rejects_an_incompatible_vegetation_pair_before_bevy() {
        let terrain = embedded_visual_valley_terrain_document().unwrap();
        let mut vegetation = embedded_visual_valley_vegetation_document().unwrap();
        vegetation.area_id = forge_core::AreaId::deterministic("runtime-mismatched-area");

        let error = validated_visual_valley_document(
            terrain,
            Some(vegetation),
            None,
            None,
            None,
            None,
            "mismatched test pair",
        )
        .unwrap_err();
        assert!(error.contains("vegetation"));
        assert!(error.contains("area identities do not match"), "{error}");
    }

    #[test]
    fn visual_valley_rejects_an_incompatible_environment_before_bevy() {
        let terrain = embedded_visual_valley_terrain_document().unwrap();
        let mut environment = embedded_visual_valley_environment_profile().unwrap();
        environment.area_id = forge_core::AreaId::deterministic("runtime-mismatched-area");

        let error = validated_visual_valley_document(
            terrain,
            None,
            Some(environment),
            None,
            None,
            None,
            "mismatched environment",
        )
        .unwrap_err();
        assert!(error.contains("environment"));
        assert!(error.contains("area identity mismatch"), "{error}");
    }

    #[test]
    fn visual_valley_rejects_incompatible_water_and_weather_before_bevy() {
        let terrain = embedded_visual_valley_terrain_document().unwrap();
        let mut water = embedded_visual_valley_water_document().unwrap();
        water.area_id = forge_core::AreaId::deterministic("runtime-mismatched-water-area");
        let error = validated_visual_valley_document(
            terrain.clone(),
            None,
            None,
            Some(water),
            None,
            None,
            "mismatched water",
        )
        .unwrap_err();
        assert!(error.contains("water"));
        assert!(error.contains("area identity mismatch"), "{error}");

        let mut weather = embedded_visual_valley_weather_profile().unwrap();
        weather.area_id = forge_core::AreaId::deterministic("runtime-mismatched-weather-area");
        let error = validated_visual_valley_document(
            terrain,
            None,
            None,
            None,
            Some(weather),
            None,
            "mismatched weather",
        )
        .unwrap_err();
        assert!(error.contains("weather"));
        assert!(error.contains("area identity mismatch"), "{error}");
    }

    #[test]
    fn volumetric_preview_requires_quality_and_rejects_duplicates() {
        for args in [
            vec!["--volumetric-preview"],
            vec!["--visual-valley", "--volumetric-preview"],
            vec![
                "--visual-valley",
                "--quality",
                "high",
                "--volumetric-preview",
                "--volumetric-preview",
            ],
        ] {
            assert!(Options::parse(args.into_iter().map(str::to_owned)).is_err());
        }
        for flag in ["--quality", "--environment-quality"] {
            let options = Options::parse(
                ["--visual-valley", flag, "high", "--volumetric-preview"]
                    .into_iter()
                    .map(str::to_owned),
            )
            .unwrap();
            assert!(options.volumetric_preview);
        }
        assert!(!Options::default().volumetric_preview);
    }

    #[test]
    fn global_cycle_requires_global_quality_and_exclusive_automation() {
        let base = [
            "--world-sketch-visual-valley",
            "--quality",
            "high",
            "--quality-cycle-proof",
            "fresh",
        ];
        assert!(Options::parse(base.into_iter().map(str::to_owned)).is_ok());
        for extra in [
            vec!["--capture", "x.png"],
            vec!["--quality-cycle-proof", "duplicate"],
            vec!["--environment-cycle-proof", "other"],
            vec!["--player-camera"],
            vec!["--exit-after-frames", "60"],
        ] {
            assert!(Options::parse(base.into_iter().chain(extra).map(str::to_owned)).is_err());
        }
        assert!(
            Options::parse(
                ["--visual-valley", "--quality-cycle-proof", "fresh"]
                    .into_iter()
                    .map(str::to_owned)
            )
            .is_err()
        );
    }

    #[test]
    fn prewarm_requires_global_quality_and_allows_sequential_global_cycle() {
        let parse = |args: &[&str]| Options::parse(args.iter().map(|s| (*s).to_owned()));
        assert!(parse(&["--visual-valley", "--prewarm-quality"]).is_err());
        assert!(
            parse(&["--visual-valley", "--quality", "high", "--prewarm-quality"])
                .unwrap()
                .prewarm_quality
        );
        assert!(
            parse(&[
                "--world-sketch-visual-valley",
                "--quality",
                "high",
                "--prewarm-quality",
                "--quality-cycle-proof",
                "fresh"
            ])
            .is_ok()
        );
        assert!(
            parse(&[
                "--world-sketch-visual-valley",
                "--quality",
                "high",
                "--prewarm-quality",
                "--fixed-proof-view",
                "human_bridge",
                "--capture",
                "fresh.png",
                "--exit-after-frames",
                "600"
            ])
            .is_err()
        );
    }

    #[test]
    fn global_quality_sets_all_domains_and_rejects_ambiguous_overrides() {
        for (label, tier, rain) in [
            ("low", VisualQualityTier::Low, WeatherQualityTier::Low),
            (
                "medium",
                VisualQualityTier::Medium,
                WeatherQualityTier::Medium,
            ),
            ("high", VisualQualityTier::High, WeatherQualityTier::High),
            ("ultra", VisualQualityTier::Ultra, WeatherQualityTier::Ultra),
        ] {
            for mode in ["--visual-valley", "--world-sketch-visual-valley"] {
                let options =
                    Options::parse([mode, "--quality", label].into_iter().map(str::to_owned))
                        .unwrap();
                assert_eq!(options.global_quality, Some(tier));
                assert_eq!(options.rain_quality, Some(rain));
                assert!(
                    [
                        options.shadow_quality,
                        options.terrain_quality,
                        options.post_process_quality,
                        options.water_quality,
                        options.environment_quality,
                        options.vegetation_quality
                    ]
                    .iter()
                    .all(|value| *value == Some(tier))
                );
            }
        }
        for flag in [
            "--quality",
            "--rain-quality",
            "--shadow-quality",
            "--terrain-quality",
            "--post-process-quality",
            "--water-quality",
            "--environment-quality",
            "--vegetation-quality",
        ] {
            for args in [
                ["--visual-valley", "--quality", "high", flag, "low"],
                ["--visual-valley", flag, "low", "--quality", "high"],
            ] {
                assert!(Options::parse(args.into_iter().map(str::to_owned)).is_err());
            }
        }
        for args in [
            vec!["--quality", "high"],
            vec!["--visual-valley", "--quality"],
            vec!["--visual-valley", "--quality", "bad"],
            vec![
                "--visual-valley",
                "--quality",
                "high",
                "--vegetation-cycle-proof",
                "fresh",
            ],
        ] {
            assert!(Options::parse(args.into_iter().map(str::to_owned)).is_err());
        }
    }

    #[test]
    fn vegetation_cycle_rejects_conflicting_automation() {
        let base = [
            "--visual-valley",
            "--vegetation-quality",
            "high",
            "--vegetation-cycle-proof",
            "fresh",
        ];
        assert!(Options::parse(base.into_iter().map(str::to_owned)).is_ok());
        for extra in [
            vec!["--capture", "x.png"],
            vec!["--vegetation-cycle-proof", "duplicate"],
            vec![
                "--environment-quality",
                "high",
                "--environment-cycle-proof",
                "other",
            ],
            vec!["--player-camera"],
        ] {
            assert!(Options::parse(base.into_iter().chain(extra).map(str::to_owned)).is_err());
        }
        assert!(
            Options::parse(
                ["--visual-valley", "--vegetation-cycle-proof", "fresh"]
                    .into_iter()
                    .map(str::to_owned)
            )
            .is_err()
        );
    }

    #[test]
    fn water_cycle_proof_rejects_missing_quality_and_conflicting_runs() {
        let base = [
            "--world-sketch-visual-valley",
            "--water-quality",
            "high",
            "--water-cycle-proof",
            "fresh",
        ];
        assert!(Options::parse(base.into_iter().map(str::to_owned)).is_ok());
        for extra in [
            vec!["--exit-after-frames", "60"],
            vec!["--capture", "image.png"],
            vec!["--water-cycle-proof", "duplicate"],
            vec!["--player-camera"],
            vec!["--bridge-crossing-probe"],
            vec!["--environment-cycle-proof", "other"],
            vec!["--vegetation-cycle-proof", "other"],
            vec!["--quality-cycle-proof", "other"],
        ] {
            assert!(Options::parse(base.into_iter().chain(extra).map(str::to_owned)).is_err());
        }
        for args in [
            vec!["--visual-valley", "--water-cycle-proof", "fresh"],
            vec!["--water-cycle-proof"],
        ] {
            assert!(Options::parse(args.into_iter().map(str::to_owned)).is_err());
        }
    }

    #[test]
    fn environment_cycle_proof_rejects_conflicting_automation() {
        let base = [
            "--visual-valley",
            "--environment-quality",
            "high",
            "--environment-cycle-proof",
            "fresh-proof",
        ];
        assert!(Options::parse(base.into_iter().map(str::to_owned)).is_ok());
        for extra in [
            vec!["--exit-after-frames", "60"],
            vec!["--capture", "image.png"],
            vec!["--environment-cycle-proof", "duplicate"],
            vec!["--player-camera"],
        ] {
            assert!(Options::parse(base.into_iter().chain(extra).map(str::to_owned)).is_err());
        }
        assert!(
            Options::parse(
                ["--visual-valley", "--environment-cycle-proof", "fresh"]
                    .into_iter()
                    .map(str::to_owned)
            )
            .is_err()
        );
    }

    #[test]
    fn vegetation_quality_cli_is_strict() {
        for mode in ["--visual-valley", "--world-sketch-visual-valley"] {
            for label in ["low", "medium", "high", "ultra"] {
                assert!(
                    Options::parse(
                        [mode, "--vegetation-quality", label]
                            .into_iter()
                            .map(str::to_owned)
                    )
                    .unwrap()
                    .vegetation_quality
                    .is_some()
                );
            }
        }
        for args in [
            vec!["--vegetation-quality", "low"],
            vec!["--visual-valley", "--vegetation-quality"],
            vec!["--visual-valley", "--vegetation-quality", "bad"],
            vec![
                "--visual-valley",
                "--vegetation-quality",
                "low",
                "--vegetation-quality",
                "high",
            ],
        ] {
            assert!(Options::parse(args.into_iter().map(str::to_owned)).is_err());
        }
    }

    #[test]
    fn environment_quality_override_is_strict_and_requires_a_valley() {
        for mode in ["--visual-valley", "--world-sketch-visual-valley"] {
            for (label, tier) in [
                ("low", VisualQualityTier::Low),
                ("medium", VisualQualityTier::Medium),
                ("high", VisualQualityTier::High),
                ("ultra", VisualQualityTier::Ultra),
            ] {
                let options = Options::parse(
                    [mode, "--environment-quality", label]
                        .into_iter()
                        .map(str::to_owned),
                )
                .unwrap();
                assert_eq!(options.environment_quality, Some(tier));
            }
        }
        for args in [
            vec!["--environment-quality", "low"],
            vec!["--visual-valley", "--environment-quality"],
            vec!["--visual-valley", "--environment-quality", "unknown"],
            vec![
                "--visual-valley",
                "--environment-quality",
                "low",
                "--environment-quality",
                "high",
            ],
        ] {
            assert!(Options::parse(args.into_iter().map(str::to_owned)).is_err());
        }
        assert!(
            Options::parse(["--visual-valley".to_owned()].into_iter())
                .unwrap()
                .environment_quality
                .is_none()
        );
    }

    #[test]
    fn water_quality_override_is_strict_and_requires_a_valley() {
        for mode in ["--visual-valley", "--world-sketch-visual-valley"] {
            for (label, tier) in [
                ("low", VisualQualityTier::Low),
                ("medium", VisualQualityTier::Medium),
                ("high", VisualQualityTier::High),
                ("ultra", VisualQualityTier::Ultra),
            ] {
                let options = Options::parse(
                    [mode, "--water-quality", label]
                        .into_iter()
                        .map(str::to_owned),
                )
                .unwrap();
                assert_eq!(options.water_quality, Some(tier));
            }
        }
        for args in [
            vec!["--water-quality", "low"],
            vec!["--visual-valley", "--water-quality"],
            vec!["--visual-valley", "--water-quality", "extreme"],
            vec![
                "--visual-valley",
                "--water-quality",
                "low",
                "--water-quality",
                "high",
            ],
        ] {
            assert!(Options::parse(args.into_iter().map(str::to_owned)).is_err());
        }
        assert_eq!(
            Options::parse(["--visual-valley".to_owned()].into_iter())
                .unwrap()
                .water_quality,
            None
        );
    }

    #[test]
    fn post_process_quality_override_is_strict_and_requires_a_valley() {
        for mode in ["--visual-valley", "--world-sketch-visual-valley"] {
            for (label, tier) in [
                ("low", VisualQualityTier::Low),
                ("medium", VisualQualityTier::Medium),
                ("high", VisualQualityTier::High),
                ("ultra", VisualQualityTier::Ultra),
            ] {
                let options = Options::parse(
                    [mode, "--post-process-quality", label]
                        .into_iter()
                        .map(str::to_owned),
                )
                .unwrap();
                assert_eq!(options.post_process_quality, Some(tier));
            }
        }
        for args in [
            vec!["--post-process-quality", "low"],
            vec!["--visual-valley", "--post-process-quality"],
            vec!["--visual-valley", "--post-process-quality", "extreme"],
            vec![
                "--visual-valley",
                "--post-process-quality",
                "low",
                "--post-process-quality",
                "high",
            ],
        ] {
            assert!(Options::parse(args.into_iter().map(str::to_owned)).is_err());
        }
        assert_eq!(
            Options::parse(["--visual-valley".to_owned()].into_iter())
                .unwrap()
                .post_process_quality,
            None
        );
    }

    #[test]
    fn terrain_quality_override_is_strict_and_requires_a_valley() {
        for mode in ["--visual-valley", "--world-sketch-visual-valley"] {
            for (label, tier) in [
                ("low", VisualQualityTier::Low),
                ("medium", VisualQualityTier::Medium),
                ("high", VisualQualityTier::High),
                ("ultra", VisualQualityTier::Ultra),
            ] {
                let options = Options::parse(
                    [mode, "--terrain-quality", label]
                        .into_iter()
                        .map(str::to_owned),
                )
                .unwrap();
                assert_eq!(options.terrain_quality, Some(tier));
            }
        }
        for args in [
            vec!["--terrain-quality", "low"],
            vec!["--visual-valley", "--terrain-quality"],
            vec!["--visual-valley", "--terrain-quality", "extreme"],
            vec![
                "--visual-valley",
                "--terrain-quality",
                "low",
                "--terrain-quality",
                "high",
            ],
        ] {
            assert!(Options::parse(args.into_iter().map(str::to_owned)).is_err());
        }
        assert_eq!(
            Options::parse(["--visual-valley".to_owned()].into_iter())
                .unwrap()
                .terrain_quality,
            None
        );
    }

    #[test]
    fn shadow_quality_override_is_strict_and_requires_a_valley() {
        for mode in ["--visual-valley", "--world-sketch-visual-valley"] {
            for (label, tier) in [
                ("low", VisualQualityTier::Low),
                ("medium", VisualQualityTier::Medium),
                ("high", VisualQualityTier::High),
                ("ultra", VisualQualityTier::Ultra),
            ] {
                let options = Options::parse(
                    [mode, "--shadow-quality", label]
                        .into_iter()
                        .map(str::to_owned),
                )
                .unwrap();
                assert_eq!(options.shadow_quality, Some(tier));
            }
        }
        for args in [
            vec!["--shadow-quality", "low"],
            vec!["--visual-valley", "--shadow-quality"],
            vec!["--visual-valley", "--shadow-quality", "extreme"],
            vec![
                "--visual-valley",
                "--shadow-quality",
                "low",
                "--shadow-quality",
                "high",
            ],
        ] {
            assert!(Options::parse(args.into_iter().map(str::to_owned)).is_err());
        }
        assert_eq!(
            Options::parse(["--visual-valley".to_owned()].into_iter())
                .unwrap()
                .shadow_quality,
            None
        );
    }

    #[test]
    fn rain_quality_override_is_strict_and_requires_a_valley() {
        for mode in ["--visual-valley", "--world-sketch-visual-valley"] {
            for (label, tier) in [
                ("low", WeatherQualityTier::Low),
                ("medium", WeatherQualityTier::Medium),
                ("high", WeatherQualityTier::High),
                ("ultra", WeatherQualityTier::Ultra),
            ] {
                let options = Options::parse(
                    [mode, "--rain-quality", label]
                        .into_iter()
                        .map(str::to_owned),
                )
                .unwrap();
                assert_eq!(options.rain_quality, Some(tier));
            }
        }
        for args in [
            vec!["--rain-quality", "low"],
            vec!["--visual-valley", "--rain-quality"],
            vec!["--visual-valley", "--rain-quality", "extreme"],
            vec![
                "--visual-valley",
                "--rain-quality",
                "low",
                "--rain-quality",
                "high",
            ],
        ] {
            assert!(Options::parse(args.into_iter().map(str::to_owned)).is_err());
        }
        assert_eq!(
            Options::parse(["--visual-valley".to_owned()].into_iter())
                .unwrap()
                .rain_quality,
            None
        );
    }

    #[test]
    fn weather_override_is_strict_and_requires_visual_valley() {
        let clear = Options::parse(
            [
                "--visual-valley".to_owned(),
                "--weather".to_owned(),
                "clear".to_owned(),
            ]
            .into_iter(),
        )
        .unwrap();
        assert_eq!(clear.weather_state, Some(WeatherState::Clear));

        let rain = Options::parse(
            [
                "--visual-valley".to_owned(),
                "--weather".to_owned(),
                "rain".to_owned(),
            ]
            .into_iter(),
        )
        .unwrap();
        assert_eq!(rain.weather_state, Some(WeatherState::Rain));
        let world_sketch_rain = Options::parse(
            [
                "--world-sketch-visual-valley".to_owned(),
                "--weather".to_owned(),
                "rain".to_owned(),
            ]
            .into_iter(),
        )
        .unwrap();
        assert_eq!(world_sketch_rain.weather_state, Some(WeatherState::Rain));

        assert_eq!(
            Options::parse(["--weather".to_owned(), "rain".to_owned()].into_iter()).unwrap_err(),
            "--weather requires --visual-valley or --world-sketch-visual-valley"
        );
        assert!(
            Options::parse(
                [
                    "--visual-valley".to_owned(),
                    "--weather".to_owned(),
                    "storm".to_owned(),
                ]
                .into_iter(),
            )
            .unwrap_err()
            .contains("received `storm`")
        );
        assert_eq!(
            Options::parse(["--visual-valley".to_owned(), "--weather".to_owned()].into_iter())
                .unwrap_err(),
            "--weather requires `clear` or `rain`"
        );
        assert_eq!(
            Options::parse(
                [
                    "--visual-valley".to_owned(),
                    "--weather".to_owned(),
                    "clear".to_owned(),
                    "--weather".to_owned(),
                    "rain".to_owned(),
                ]
                .into_iter(),
            )
            .unwrap_err(),
            "--weather may only be specified once"
        );
    }

    #[test]
    fn runtime_document_sources_are_mutually_exclusive() {
        let error = Options::parse(
            [
                "--scene".to_owned(),
                "proof.json".to_owned(),
                "--visual-valley".to_owned(),
            ]
            .into_iter(),
        )
        .unwrap_err();
        assert_eq!(
            error,
            "--scene, --terrain, --visual-valley and --world-sketch-visual-valley are mutually exclusive"
        );

        let error = Options::parse(
            [
                "--terrain".to_owned(),
                "terrain.json".to_owned(),
                "--visual-valley".to_owned(),
            ]
            .into_iter(),
        )
        .unwrap_err();
        assert_eq!(
            error,
            "--scene, --terrain, --visual-valley and --world-sketch-visual-valley are mutually exclusive"
        );

        let error = Options::parse(
            [
                "--visual-valley".to_owned(),
                "--world-sketch-visual-valley".to_owned(),
            ]
            .into_iter(),
        )
        .unwrap_err();
        assert_eq!(
            error,
            "--scene, --terrain, --visual-valley and --world-sketch-visual-valley are mutually exclusive"
        );
    }

    #[test]
    fn governed_asset_kit_paths_are_paired_and_require_world_sketch_mode() {
        assert_eq!(
            Options::parse(["--asset-kit-build".to_owned(), "build.json".to_owned()].into_iter(),)
                .unwrap_err(),
            "--asset-kit-build requires --asset-kit-preview"
        );
        assert_eq!(
            Options::parse(
                ["--asset-kit-preview".to_owned(), "preview.json".to_owned(),].into_iter(),
            )
            .unwrap_err(),
            "--asset-kit-preview requires --asset-kit-build"
        );
        assert_eq!(
            Options::parse(
                [
                    "--visual-valley".to_owned(),
                    "--asset-kit-build".to_owned(),
                    "build.json".to_owned(),
                    "--asset-kit-preview".to_owned(),
                    "preview.json".to_owned(),
                ]
                .into_iter(),
            )
            .unwrap_err(),
            "--asset-kit-build and --asset-kit-preview require --world-sketch-visual-valley"
        );

        let valid = Options::parse(
            [
                "--world-sketch-visual-valley".to_owned(),
                "--asset-kit-build".to_owned(),
                "build.json".to_owned(),
                "--asset-kit-preview".to_owned(),
                "preview.json".to_owned(),
            ]
            .into_iter(),
        )
        .unwrap();
        assert_eq!(
            valid.asset_kit_build_path,
            Some(PathBuf::from("build.json"))
        );
        assert_eq!(
            valid.asset_kit_preview_path,
            Some(PathBuf::from("preview.json"))
        );
    }

    #[test]
    fn prototype_preparation_requires_verified_kit_and_rejects_duplicate_option() {
        assert!(Options::parse(["--prototype-forest-preview".to_owned()].into_iter()).is_err());
        assert!(Options::parse(["--prototype-forest-batches".to_owned()].into_iter()).is_err());
        assert!(
            Options::parse(
                ["--prepare-vegetation-catalog", "catalog.json"]
                    .into_iter()
                    .map(str::to_owned)
            )
            .is_err()
        );
        let base = [
            "--world-sketch-visual-valley",
            "--asset-kit-build",
            "build.json",
            "--asset-kit-preview",
            "preview.json",
            "--prepare-vegetation-catalog",
            "catalog.json",
        ];
        let options = Options::parse(base.into_iter().map(str::to_owned)).unwrap();
        assert!(!options.prototype_forest_preview);
        assert!(!options.prototype_far_batches);
        let batched = Options::parse(
            base.into_iter()
                .chain(["--prototype-forest-batches"])
                .map(str::to_owned),
        )
        .unwrap();
        assert!(batched.prototype_forest_preview && batched.prototype_far_batches);
        assert!(
            Options::parse(
                base.into_iter()
                    .chain(["--prototype-forest-preview"])
                    .map(str::to_owned)
            )
            .unwrap()
            .prototype_forest_preview
        );
        assert_eq!(
            options.prepare_vegetation_catalog_path,
            Some(PathBuf::from("catalog.json"))
        );
        assert!(
            Options::parse(
                base.into_iter()
                    .chain(["--prepare-vegetation-catalog", "again.json"])
                    .map(str::to_owned)
            )
            .is_err()
        );
    }

    #[test]
    fn global_cycle_rejects_unqualified_forest_without_rejecting_preview_captures() {
        let base = [
            "--world-sketch-visual-valley",
            "--asset-kit-build",
            "build.json",
            "--asset-kit-preview",
            "preview.json",
            "--prepare-vegetation-catalog",
            "catalog.json",
            "--quality",
            "high",
        ];
        // Preparing a catalog does not itself activate the unqualified renderer.
        assert!(
            Options::parse(
                base.into_iter()
                    .chain(["--quality-cycle-proof", "fresh"])
                    .map(str::to_owned)
            )
            .is_ok()
        );
        for preview in ["--prototype-forest-preview", "--prototype-forest-batches"] {
            for reversed in [false, true] {
                let extras = if reversed {
                    vec!["--quality-cycle-proof", "fresh", preview]
                } else {
                    vec![preview, "--quality-cycle-proof", "fresh"]
                };
                let result = Options::parse(base.into_iter().chain(extras).map(str::to_owned));
                if preview == "--prototype-forest-batches" {
                    assert!(result.is_ok());
                } else {
                    assert!(
                        result
                            .unwrap_err()
                            .contains("prototype vegetation is Unqualified")
                    );
                }
            }
            assert!(
                Options::parse(
                    base.into_iter()
                        .chain([
                            preview,
                            "--capture",
                            "preview.png",
                            "--capture-frame",
                            "60",
                            "--exit-after-frames",
                            "120"
                        ])
                        .map(str::to_owned)
                )
                .is_ok()
            );
        }
    }

    #[test]
    fn governed_asset_kit_accepts_playtest_but_rejects_the_specialized_camera_probe() {
        for camera in ["--player-camera", "--player-camera-probe"] {
            let parsed = Options::parse(
                [
                    "--world-sketch-visual-valley".to_owned(),
                    "--asset-kit-build".to_owned(),
                    "build.json".to_owned(),
                    "--asset-kit-preview".to_owned(),
                    "preview.json".to_owned(),
                    camera.to_owned(),
                ]
                .into_iter(),
            );
            if camera == "--player-camera" {
                let options = parsed.unwrap();
                assert!(options.player_camera.is_interactive());
                assert!(options.asset_kit_build_path.is_some());
                assert!(options.asset_kit_preview_path.is_some());
            } else {
                assert_eq!(
                    parsed.unwrap_err(),
                    "the governed asset-kit cannot be combined with the specialized --player-camera-probe"
                );
            }
        }
    }

    #[test]
    fn governed_kit_input_guard_waits_and_reblocks_on_lost_readiness() {
        let mut app = App::new();
        app.insert_resource(forge_engine::GovernedAssetKitReadiness::default())
            .insert_resource(forge_engine::PlayerInputFocus(true))
            .add_systems(Update, guard_governed_kit_player_input);
        for ready in [false, true, false] {
            app.world_mut()
                .resource_mut::<forge_engine::GovernedAssetKitReadiness>()
                .ready = ready;
            app.update();
            assert_eq!(
                app.world().resource::<forge_engine::PlayerInputFocus>().0,
                ready
            );
        }
    }

    #[test]
    fn terrain_debug_is_explicit_and_rejected_outside_gate1_modes() {
        let error = Options::parse(["--terrain-debug".to_owned()].into_iter()).unwrap_err();
        assert_eq!(
            error,
            "--terrain-debug requires --visual-valley, --world-sketch-visual-valley or --terrain"
        );

        let options = Options::parse(
            ["--visual-valley".to_owned(), "--terrain-debug".to_owned()].into_iter(),
        )
        .unwrap();
        assert_eq!(options.terrain_debug, TerrainDebugMode::ChunkLodGizmos);
    }

    #[test]
    fn player_camera_modes_are_explicit_and_restricted_to_gate1_terrain() {
        assert_eq!(
            Options::default().player_camera,
            PlayerCameraRuntimeMode::Disabled
        );

        let interactive_error =
            Options::parse(["--player-camera".to_owned()].into_iter()).unwrap_err();
        assert_eq!(
            interactive_error,
            "--player-camera requires --visual-valley, --world-sketch-visual-valley or --terrain"
        );

        let probe_error =
            Options::parse(["--player-camera-probe".to_owned()].into_iter()).unwrap_err();
        assert_eq!(
            probe_error,
            "--player-camera-probe requires --visual-valley, --world-sketch-visual-valley or --terrain"
        );

        let interactive_scene_error = Options::parse(
            [
                "--scene".to_owned(),
                "proof.json".to_owned(),
                "--player-camera".to_owned(),
            ]
            .into_iter(),
        )
        .unwrap_err();
        assert_eq!(
            interactive_scene_error,
            "--player-camera requires --visual-valley, --world-sketch-visual-valley or --terrain"
        );

        let probe_scene_error = Options::parse(
            [
                "--scene".to_owned(),
                "proof.json".to_owned(),
                "--player-camera-probe".to_owned(),
            ]
            .into_iter(),
        )
        .unwrap_err();
        assert_eq!(
            probe_scene_error,
            "--player-camera-probe requires --visual-valley, --world-sketch-visual-valley or --terrain"
        );

        let interactive = Options::parse(
            ["--visual-valley".to_owned(), "--player-camera".to_owned()].into_iter(),
        )
        .unwrap();
        assert_eq!(
            interactive.player_camera,
            PlayerCameraRuntimeMode::Interactive
        );

        let visual_valley_probe = Options::parse(
            [
                "--visual-valley".to_owned(),
                "--player-camera-probe".to_owned(),
            ]
            .into_iter(),
        )
        .unwrap();
        assert_eq!(
            visual_valley_probe.player_camera,
            PlayerCameraRuntimeMode::Probe
        );

        let terrain_interactive = Options::parse(
            [
                "--terrain".to_owned(),
                "terrain.json".to_owned(),
                "--player-camera".to_owned(),
            ]
            .into_iter(),
        )
        .unwrap();
        assert_eq!(
            terrain_interactive.player_camera,
            PlayerCameraRuntimeMode::Interactive
        );

        let probe = Options::parse(
            [
                "--terrain".to_owned(),
                "terrain.json".to_owned(),
                "--player-camera-probe".to_owned(),
            ]
            .into_iter(),
        )
        .unwrap();
        assert_eq!(probe.player_camera, PlayerCameraRuntimeMode::Probe);
    }

    #[test]
    fn player_camera_interactive_and_probe_modes_are_mutually_exclusive() {
        for arguments in [
            [
                "--visual-valley".to_owned(),
                "--player-camera".to_owned(),
                "--player-camera-probe".to_owned(),
            ],
            [
                "--visual-valley".to_owned(),
                "--player-camera-probe".to_owned(),
                "--player-camera".to_owned(),
            ],
        ] {
            let error = Options::parse(arguments.into_iter()).unwrap_err();
            assert_eq!(
                error,
                "--player-camera and --player-camera-probe are mutually exclusive"
            );
        }
    }

    #[test]
    fn terrain_partial_update_probe_rejects_every_player_camera_mode_in_any_order() {
        for camera_option in ["--player-camera", "--player-camera-probe"] {
            for options in [
                vec![
                    "--visual-valley".to_owned(),
                    "--terrain-partial-update-probe".to_owned(),
                    camera_option.to_owned(),
                ],
                vec![
                    "--visual-valley".to_owned(),
                    camera_option.to_owned(),
                    "--terrain-partial-update-probe".to_owned(),
                ],
            ] {
                let error = Options::parse(options.into_iter()).unwrap_err();
                assert_eq!(
                    error,
                    "--terrain-partial-update-probe cannot be combined with --player-camera or --player-camera-probe"
                );
            }
        }
    }

    #[test]
    fn player_camera_probe_is_self_bounded_and_rejects_other_probes() {
        let conflicts: &[&[&str]] = &[
            &["--capture", "probe.png"],
            &["--metrics", "probe-metrics.json"],
            &["--exit-after-frames", "120"],
            &[
                "--resize-at-frame",
                "30",
                "--resize-width",
                "1280",
                "--resize-height",
                "720",
            ],
            &["--close-at-frame", "120"],
            &["--log-probe"],
        ];
        for conflict in conflicts {
            let mut arguments = vec![
                "--visual-valley".to_owned(),
                "--player-camera-probe".to_owned(),
            ];
            arguments.extend(conflict.iter().map(|argument| (*argument).to_owned()));

            let error = Options::parse(arguments.into_iter()).unwrap_err();
            assert_eq!(
                error,
                "--player-camera-probe is self-bounded and cannot be combined with capture, metrics, bounded exit, resize, close or logging probes"
            );
        }
    }

    #[test]
    fn interactive_player_camera_remains_compatible_with_capture_and_metrics() {
        let options = Options::parse(
            [
                "--visual-valley".to_owned(),
                "--player-camera".to_owned(),
                "--capture".to_owned(),
                "player-camera.png".to_owned(),
                "--capture-frame".to_owned(),
                "120".to_owned(),
                "--metrics".to_owned(),
                "player-camera-metrics.json".to_owned(),
                "--metrics-warmup-frames".to_owned(),
                "60".to_owned(),
                "--exit-after-frames".to_owned(),
                "180".to_owned(),
            ]
            .into_iter(),
        )
        .unwrap();

        assert_eq!(options.player_camera, PlayerCameraRuntimeMode::Interactive);
        assert_eq!(
            options.capture_path,
            Some(PathBuf::from("player-camera.png"))
        );
        assert_eq!(
            options.metrics_path,
            Some(PathBuf::from("player-camera-metrics.json"))
        );
        assert_eq!(options.exit_after_frames, Some(180));
    }

    #[test]
    fn help_lists_both_player_camera_modes() {
        let help = Options::parse(["--help".to_owned()].into_iter()).unwrap_err();
        assert!(help.contains("[--player-camera | --player-camera-probe]"));
    }

    #[test]
    fn only_interactive_player_camera_locks_and_hides_the_cursor() {
        let interactive = cursor_options_for_player_camera(true);
        assert!(!interactive.visible);
        assert_eq!(interactive.grab_mode, CursorGrabMode::Locked);

        let cursor = cursor_options_for_player_camera(false);
        assert!(cursor.visible);
        assert_eq!(cursor.grab_mode, CursorGrabMode::None);
    }

    #[test]
    fn bounded_automation_disables_live_player_input_for_reproducible_capture() {
        let mut automation = RuntimeAutomation::default();
        assert!(!automated_runtime_requested(&automation));
        automation.capture_path = Some(PathBuf::from("capture.png"));
        assert!(automated_runtime_requested(&automation));
    }

    #[test]
    fn terrain_partial_update_probe_is_explicit_and_restricted_to_gate1_modes() {
        let error =
            Options::parse(["--terrain-partial-update-probe".to_owned()].into_iter()).unwrap_err();
        assert_eq!(
            error,
            "--terrain-partial-update-probe requires --visual-valley, --world-sketch-visual-valley or --terrain"
        );

        let scene_error = Options::parse(
            [
                "--scene".to_owned(),
                "proof.json".to_owned(),
                "--terrain-partial-update-probe".to_owned(),
            ]
            .into_iter(),
        )
        .unwrap_err();
        assert_eq!(
            scene_error,
            "--terrain-partial-update-probe requires --visual-valley, --world-sketch-visual-valley or --terrain"
        );

        let visual_valley = Options::parse(
            [
                "--visual-valley".to_owned(),
                "--terrain-partial-update-probe".to_owned(),
            ]
            .into_iter(),
        )
        .unwrap();
        assert!(visual_valley.terrain_partial_update_probe.is_enabled());

        let terrain = Options::parse(
            [
                "--terrain".to_owned(),
                "terrain.json".to_owned(),
                "--terrain-partial-update-probe".to_owned(),
            ]
            .into_iter(),
        )
        .unwrap();
        assert!(terrain.terrain_partial_update_probe.is_enabled());
    }

    #[test]
    fn terrain_partial_update_probe_is_self_bounded_and_rejects_generic_automation() {
        let bounded_exit = Options::parse(
            [
                "--visual-valley".to_owned(),
                "--terrain-partial-update-probe".to_owned(),
                "--exit-after-frames".to_owned(),
                "120".to_owned(),
            ]
            .into_iter(),
        )
        .unwrap_err();
        assert_eq!(
            bounded_exit,
            "--terrain-partial-update-probe is self-bounded and cannot be combined with capture, metrics, bounded exit, resize, close or logging probes"
        );

        let capture = Options::parse(
            [
                "--visual-valley".to_owned(),
                "--terrain-partial-update-probe".to_owned(),
                "--capture".to_owned(),
                "probe.png".to_owned(),
            ]
            .into_iter(),
        )
        .unwrap_err();
        assert_eq!(
            capture,
            "--terrain-partial-update-probe is self-bounded and cannot be combined with capture, metrics, bounded exit, resize, close or logging probes"
        );
    }

    #[test]
    fn terrain_partial_update_probe_cannot_overlap_standalone_probes() {
        let close = Options::parse(
            [
                "--visual-valley".to_owned(),
                "--terrain-partial-update-probe".to_owned(),
                "--close-at-frame".to_owned(),
                "120".to_owned(),
            ]
            .into_iter(),
        )
        .unwrap_err();
        assert_eq!(
            close,
            "--terrain-partial-update-probe is self-bounded and cannot be combined with capture, metrics, bounded exit, resize, close or logging probes"
        );

        let logging = Options::parse(
            [
                "--visual-valley".to_owned(),
                "--terrain-partial-update-probe".to_owned(),
                "--log-probe".to_owned(),
            ]
            .into_iter(),
        )
        .unwrap_err();
        assert_eq!(
            logging,
            "--terrain-partial-update-probe is self-bounded and cannot be combined with capture, metrics, bounded exit, resize, close or logging probes"
        );
    }

    #[test]
    fn external_world_sketch_is_explicit_and_revalidated_before_rendering() {
        assert!(
            Options::parse(
                [
                    "--world-sketch-input".to_owned(),
                    "candidate.json".to_owned()
                ]
                .into_iter()
            )
            .is_err()
        );
        let root = env::temp_dir().join(format!("forge-world-sketch-input-{}", std::process::id()));
        fs::create_dir_all(&root).unwrap();
        let path = root.join("candidate.json");
        let mut sketch = embedded_visual_valley_world_sketch().unwrap();
        sketch
            .splines
            .iter_mut()
            .find(|s| s.kind == forge_world::WorldSketchSplineKind::River)
            .unwrap()
            .maximum_cut_m = Some(40.0);
        fs::write(&path, sketch.to_pretty_json().unwrap()).unwrap();
        let options = Options::parse(
            [
                "--world-sketch-visual-valley".to_owned(),
                "--world-sketch-input".to_owned(),
                path.display().to_string(),
            ]
            .into_iter(),
        )
        .unwrap();
        let RuntimeDocument::VisualValley { terrain, .. } = options.load_document().unwrap() else {
            panic!("expected valley");
        };
        assert_eq!(
            terrain.content_hash().unwrap(),
            compile_world_sketch(&sketch)
                .unwrap()
                .carved_terrain
                .content_hash()
                .unwrap()
        );
        fs::write(&path, "{\"unknown\":true}").unwrap();
        assert!(options.load_document().is_err());
        fs::remove_file(&path).unwrap();
        assert!(options.load_document().is_err());
        fs::remove_dir(&root).unwrap();
    }

    #[test]
    fn terrain_file_loading_rejects_unknown_fields_and_unsupported_generation_contracts() {
        let root = env::temp_dir().join(format!(
            "openrpg-forge-runtime-terrain-loading-{}",
            std::process::id()
        ));
        fs::create_dir_all(&root).unwrap();

        let valid_path = root.join("valid.json");
        fs::write(&valid_path, forge_world::VISUAL_VALLEY_TERRAIN_JSON).unwrap();
        let valid =
            Options::parse(["--terrain".to_owned(), valid_path.display().to_string()].into_iter())
                .unwrap()
                .load_document()
                .unwrap();
        assert!(matches!(
            valid,
            RuntimeDocument::VisualValley {
                vegetation: None,
                ..
            }
        ));

        let mut unknown_field: serde_json::Value =
            serde_json::from_str(forge_world::VISUAL_VALLEY_TERRAIN_JSON).unwrap();
        unknown_field
            .as_object_mut()
            .unwrap()
            .insert("unexpected".to_owned(), serde_json::Value::Bool(true));
        let unknown_path = root.join("unknown-field.json");
        fs::write(
            &unknown_path,
            serde_json::to_vec_pretty(&unknown_field).unwrap(),
        )
        .unwrap();
        let error = Options::parse(
            ["--terrain".to_owned(), unknown_path.display().to_string()].into_iter(),
        )
        .unwrap()
        .load_document()
        .unwrap_err();
        assert!(error.contains("invalid terrain"));
        assert!(error.contains("unknown field `unexpected`"));

        let mut unsupported: serde_json::Value =
            serde_json::from_str(forge_world::VISUAL_VALLEY_TERRAIN_JSON).unwrap();
        unsupported["extent_m"]["size_x_m"] = serde_json::Value::from(256.0);
        unsupported["extent_m"]["size_z_m"] = serde_json::Value::from(256.0);
        let unsupported_path = root.join("unsupported.json");
        fs::write(
            &unsupported_path,
            serde_json::to_vec_pretty(&unsupported).unwrap(),
        )
        .unwrap();
        let error = Options::parse(
            [
                "--terrain".to_owned(),
                unsupported_path.display().to_string(),
            ]
            .into_iter(),
        )
        .unwrap()
        .load_document()
        .unwrap_err();
        assert!(error.contains("unsupported Visual Valley terrain"));
        assert!(error.contains("512 by 512 metre extent"));

        fs::remove_dir_all(root).unwrap();
    }

    #[test]
    fn output_preflight_creates_parents_and_clears_stale_files() {
        let root = env::temp_dir().join(format!(
            "openrpg-forge-runtime-output-preflight-{}",
            std::process::id()
        ));
        let capture = root.join("nested/proof.png");
        let metrics = root.join("metrics.json");
        fs::create_dir_all(capture.parent().unwrap()).unwrap();
        fs::write(&capture, b"stale capture").unwrap();
        fs::write(&metrics, b"stale metrics").unwrap();

        let options = Options {
            capture_path: Some(capture.clone()),
            metrics_path: Some(metrics.clone()),
            ..Options::default()
        };
        options.prepare_output_paths().unwrap();

        assert!(capture.parent().unwrap().is_dir());
        assert!(!capture.exists());
        assert!(!metrics.exists());
        fs::remove_dir_all(root).unwrap();
    }

    #[test]
    fn options_require_a_complete_non_zero_resize_request() {
        let incomplete = Options::parse(
            [
                "--resize-at-frame".to_owned(),
                "30".to_owned(),
                "--resize-width".to_owned(),
                "1280".to_owned(),
            ]
            .into_iter(),
        );
        assert!(incomplete.is_err());

        let zero = Options::parse(
            [
                "--resize-at-frame".to_owned(),
                "30".to_owned(),
                "--resize-width".to_owned(),
                "0".to_owned(),
                "--resize-height".to_owned(),
                "720".to_owned(),
            ]
            .into_iter(),
        );
        assert!(zero.is_err());

        let valid = Options::parse(
            [
                "--resize-at-frame".to_owned(),
                "30".to_owned(),
                "--resize-width".to_owned(),
                "1280".to_owned(),
                "--resize-height".to_owned(),
                "720".to_owned(),
                "--exit-after-frames".to_owned(),
                "40".to_owned(),
            ]
            .into_iter(),
        )
        .unwrap();
        assert_eq!(
            valid.runtime_automation().resize_request,
            Some(WindowResizeRequest {
                frame: 30,
                width: 1280,
                height: 720,
            })
        );
    }

    #[test]
    fn vsync_mapping_and_exit_codes_are_explicit() {
        assert_eq!(present_mode_for_vsync(true), PresentMode::AutoVsync);
        assert_eq!(present_mode_for_vsync(false), PresentMode::AutoNoVsync);
        assert_eq!(present_mode_label(PresentMode::AutoVsync), "auto_vsync");
        assert_eq!(
            present_mode_label(PresentMode::AutoNoVsync),
            "auto_no_vsync"
        );
        assert_eq!(app_exit_code(&AppExit::Success), 0);
        assert_eq!(app_exit_code(&AppExit::from_code(7)), 7);
    }

    #[test]
    fn proof_modes_reject_early_or_conflicting_shutdown() {
        let close_before_capture = Options::parse(
            [
                "--capture".to_owned(),
                "proof.png".to_owned(),
                "--close-at-frame".to_owned(),
                "60".to_owned(),
                "--exit-after-frames".to_owned(),
                "180".to_owned(),
            ]
            .into_iter(),
        );
        assert!(close_before_capture.is_err());

        let resize_without_observation_window = Options::parse(
            [
                "--resize-at-frame".to_owned(),
                "60".to_owned(),
                "--resize-width".to_owned(),
                "1280".to_owned(),
                "--resize-height".to_owned(),
                "720".to_owned(),
                "--exit-after-frames".to_owned(),
                "64".to_owned(),
            ]
            .into_iter(),
        );
        assert!(resize_without_observation_window.is_err());

        let metrics_without_exit =
            Options::parse(["--metrics".to_owned(), "metrics.json".to_owned()].into_iter());
        assert!(metrics_without_exit.is_err());

        let probe_with_runtime_option =
            Options::parse(["--log-probe".to_owned(), "--no-vsync".to_owned()].into_iter());
        assert!(probe_with_runtime_option.is_err());

        let probe_with_visual_valley =
            Options::parse(["--log-probe".to_owned(), "--visual-valley".to_owned()].into_iter());
        assert!(probe_with_visual_valley.is_err());
    }

    #[test]
    fn bridge_modes_require_explicit_carved_terrain_and_are_bounded() {
        assert_eq!(
            Options::parse(
                ["--bridge-placement".to_owned(), "bridge.json".to_owned(),].into_iter(),
            )
            .unwrap_err(),
            "--bridge-placement requires --terrain or --world-sketch-visual-valley"
        );
        assert_eq!(
            Options::parse(["--bridge-preview".to_owned()].into_iter()).unwrap_err(),
            "--bridge-preview requires --bridge-placement"
        );

        let valid = Options::parse(
            [
                "--terrain".to_owned(),
                "terrain.json".to_owned(),
                "--bridge-placement".to_owned(),
                "bridge.json".to_owned(),
                "--bridge-crossing-probe".to_owned(),
            ]
            .into_iter(),
        )
        .unwrap();
        assert!(valid.bridge_crossing_probe);
        assert_eq!(valid.bridge_path, Some(PathBuf::from("bridge.json")));
        assert!(
            Options::parse(
                [
                    "--world-sketch-visual-valley".to_owned(),
                    "--bridge-placement".to_owned(),
                    "bridge.json".to_owned()
                ]
                .into_iter()
            )
            .is_ok()
        );

        for conflicting in [
            "--capture",
            "--player-camera",
            "--terrain-partial-update-probe",
        ] {
            let mut arguments = vec![
                "--terrain".to_owned(),
                "terrain.json".to_owned(),
                "--bridge-placement".to_owned(),
                "bridge.json".to_owned(),
                "--bridge-crossing-probe".to_owned(),
                conflicting.to_owned(),
            ];
            if conflicting == "--capture" {
                arguments.push("capture.png".to_owned());
            }
            assert!(Options::parse(arguments.into_iter()).is_err());
        }
    }

    #[test]
    fn bridge_recording_requires_its_probe_and_rejects_conflicting_automation() {
        let arguments = [
            "--world-sketch-visual-valley",
            "--bridge-crossing-probe",
            "--bridge-frames",
            "frames",
        ];
        let parsed = Options::parse(arguments.into_iter().map(str::to_owned)).unwrap();
        assert_eq!(parsed.bridge_frames, Some(PathBuf::from("frames")));
        assert!(
            Options::parse(["--bridge-frames", "frames"].into_iter().map(str::to_owned)).is_err()
        );
        for extra in [
            ["--bridge-frames", "other"],
            ["--metrics", "metrics.json"],
            ["--exit-after-frames", "100"],
        ] {
            assert!(Options::parse(arguments.into_iter().chain(extra).map(str::to_owned)).is_err());
        }
    }

    #[test]
    fn temporal_lod_probe_requires_bounded_bridge_recording() {
        assert!(Options::parse(["--temporal-lod-probe"].into_iter().map(str::to_owned)).is_err());
        let arguments = [
            "--world-sketch-visual-valley",
            "--bridge-crossing-probe",
            "--bridge-frames",
            "frames",
            "--temporal-lod-probe",
        ];
        assert!(
            Options::parse(arguments.into_iter().map(str::to_owned))
                .expect("bounded temporal recording")
                .temporal_lod_probe
        );
        assert!(
            Options::parse(
                [
                    "--world-sketch-visual-valley",
                    "--bridge-crossing-probe",
                    "--temporal-lod-probe"
                ]
                .into_iter()
                .map(str::to_owned)
            )
            .is_err()
        );
    }

    #[test]
    fn bridge_document_loader_rejects_unknown_fields_before_bevy() {
        let compiled = forge_world::compile_world_sketch(
            &forge_world::embedded_visual_valley_world_sketch().unwrap(),
        )
        .unwrap();
        let mut value = serde_json::to_value(compiled.bridge_placements).unwrap();
        value
            .as_object_mut()
            .unwrap()
            .insert("unexpected".to_owned(), serde_json::Value::Bool(true));
        let root = env::temp_dir().join(format!(
            "openrpg-forge-runtime-bridge-load-{}",
            std::process::id()
        ));
        fs::create_dir_all(&root).unwrap();
        let bridge_path = root.join("bridge.json");
        fs::write(&bridge_path, serde_json::to_vec_pretty(&value).unwrap()).unwrap();
        let options = Options {
            bridge_path: Some(bridge_path),
            terrain_path: Some(PathBuf::from("terrain.json")),
            ..Options::default()
        };

        let error = options.load_bridge_document().unwrap_err();
        assert!(error.contains("unknown field `unexpected`"));
        fs::remove_dir_all(root).unwrap();
    }
}
