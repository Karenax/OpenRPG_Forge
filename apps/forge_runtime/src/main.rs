#![deny(unsafe_code)]

mod gate1_gameplay_stutter;
mod gate1_renderdoc_capture;
mod gate1_visual_proof;
mod player_camera_probe;
#[cfg(all(feature = "presentation-profile", not(feature = "render-profile")))]
mod presentation_profile;
mod terrain_partial_update_probe;

#[cfg(not(any(feature = "render-profile", feature = "presentation-profile")))]
use bevy::log::DEFAULT_FILTER;
use bevy::{
    app::AppExit,
    log::LogPlugin,
    prelude::*,
    render::{
        RenderPlugin,
        settings::{RenderCreation, WgpuSettings},
    },
    window::{CursorGrabMode, CursorOptions, PresentMode, WindowResolution},
};
use forge_engine::{
    PIPELINE_WARMUP_STABLE_FRAMES, ProofScenePlugin, RenderDiagnosticsPolicy, RuntimeAutomation,
    VisualValleyEnvironmentPlugin, VisualValleyPlugin, VisualValleyQualityPlugin,
    VisualValleyQualitySelection, VisualValleySceneryPlugin, VisualValleyVegetationPlugin,
    VisualValleyWaterPlugin, VisualValleyWeatherPlugin, WindowResizeRequest,
};
use forge_project::{
    EnvironmentProfileDocument, SceneProofDocument, TerrainDocument, VegetationDocument,
    VisualQualityTier, VisualValleyQualityProfileDocument, WaterDocument, WeatherProfileDocument,
    WeatherState,
};
use forge_world::{
    TerrainGenerationPlan, VegetationGenerationPlan, embedded_gate0_proof_document,
    embedded_visual_valley_environment_profile, embedded_visual_valley_player_controller_profile,
    embedded_visual_valley_terrain_document, embedded_visual_valley_vegetation_document,
    embedded_visual_valley_water_document, embedded_visual_valley_weather_profile,
};
use gate1_gameplay_stutter::{Gate1GameplayStutterPlugin, Gate1PresentationRoute};
use gate1_renderdoc_capture::{
    Gate1RenderDocCapturePlugin, validate_nonce as validate_gate1_renderdoc_nonce,
};
use gate1_visual_proof::Gate1VisualProofPlugin;
use player_camera_probe::PlayerCameraProbePlugin;
#[cfg(all(feature = "presentation-profile", not(feature = "render-profile")))]
use presentation_profile::PresentationProfileController;
use std::{
    env, fs,
    path::{Path, PathBuf},
};
use terrain_partial_update_probe::TerrainPartialUpdateProbePlugin;
#[cfg(not(any(feature = "render-profile", feature = "presentation-profile")))]
use tracing_subscriber::EnvFilter;
#[cfg(feature = "render-profile")]
use tracing_subscriber::{Layer as _, filter::LevelFilter};

#[cfg(feature = "render-profile")]
const NONQUALIFYING_RENDER_PROFILE_MARKER: &str = "NONQUALIFYING_RENDER_PROFILE.json";
#[cfg(feature = "render-profile")]
const NONQUALIFYING_RENDER_PROFILE_SCHEMA: &str = "forge.nonqualifying-render-profile";
#[cfg(feature = "render-profile")]
const NONQUALIFYING_RENDER_PROFILE_REASON: &str = "render-profile instrumentation changes runtime logging and render scheduling; this diagnostic run cannot qualify Gate 1";
const NONQUALIFYING_GATE1_FIFO_DIAGNOSTIC_MARKER: &str = "NONQUALIFYING_GATE1_FIFO_DIAGNOSTIC.json";
const NONQUALIFYING_GATE1_FIFO_DIAGNOSTIC_SCHEMA: &str =
    "forge.nonqualifying-gate1-fifo-diagnostic";
const NONQUALIFYING_GATE1_FIFO_DIAGNOSTIC_REASON: &str = "the explicit FIFO presentation route changes the qualifying AutoNoVsync request and is diagnostic-only; this run cannot qualify Gate 1";
const GATE1_PRESENTATION_OBSERVATION_SCOPE: &str = "bevy_window_request_not_resolved_surface";

#[cfg(feature = "render-profile")]
#[derive(serde::Serialize)]
struct NonqualifyingRenderProfileMarker {
    schema: &'static str,
    schema_version: u32,
    qualifying: bool,
    source_commit: Option<&'static str>,
    feature: &'static str,
    reason: &'static str,
}

#[derive(serde::Serialize)]
struct NonqualifyingGate1FifoDiagnosticMarker {
    schema: &'static str,
    schema_version: u32,
    qualifying: bool,
    source_commit: Option<&'static str>,
    presentation_route: &'static str,
    requested_present_mode: &'static str,
    vsync_requested: bool,
    observation_scope: &'static str,
    reason: &'static str,
}

#[allow(
    clippy::too_many_lines,
    reason = "the executable entry point keeps CLI, logging and Bevy startup order explicit"
)]
fn main() -> AppExit {
    #[cfg(feature = "render-profile")]
    eprintln!(
        "forge_runtime: NONQUALIFYING render-profile build; Bevy trace_chrome logging is active"
    );

    #[cfg(not(any(feature = "render-profile", feature = "presentation-profile")))]
    if let Err(message) = initialize_structured_tracing() {
        eprintln!("forge_runtime: cannot initialize structured tracing: {message}");
        return AppExit::from_code(2);
    }

    let options = match Options::parse(env::args().skip(1)) {
        Ok(options) => options,
        Err(message) => return startup_error("arguments", &message),
    };

    #[cfg(feature = "render-profile")]
    if let Some(message) = options.render_profile_route_error() {
        eprintln!("forge_runtime: {message}");
        return AppExit::from_code(2);
    }

    #[cfg(all(feature = "presentation-profile", not(feature = "render-profile")))]
    if let Some(message) = options.presentation_profile_route_error() {
        eprintln!("forge_runtime: {message}");
        return AppExit::from_code(2);
    }

    #[cfg(all(feature = "presentation-profile", not(feature = "render-profile")))]
    let presentation_profile = options.presentation_profile_controller();
    #[cfg(all(feature = "presentation-profile", not(feature = "render-profile")))]
    {
        if presentation_profile.is_enabled() {
            eprintln!(
                "forge_runtime: NONQUALIFYING presentation-profile diagnostic; exact Bevy/wgpu application spans only"
            );
        }
        if let Err(message) = presentation_profile::initialize_tracing(&presentation_profile) {
            eprintln!("forge_runtime: cannot initialize presentation-profile tracing: {message}");
            return AppExit::from_code(2);
        }
    }

    if options.log_probe {
        return run_structured_log_probe();
    }

    if let Err(message) = options.prepare_output_paths() {
        return startup_error("output_preflight", &message);
    }
    #[cfg(feature = "render-profile")]
    if let Err(message) = options.write_nonqualifying_render_profile_marker() {
        return startup_error("render_profile_marker", &message);
    }
    if let Err(message) = options.write_nonqualifying_gate1_fifo_diagnostic_marker() {
        return startup_error("gate1_fifo_diagnostic_marker", &message);
    }
    let document = match options.load_document() {
        Ok(document) => document,
        Err(message) => return startup_error("scene_load", &message),
    };

    let present_mode = options.requested_present_mode();
    let runtime_mode = document.mode_label();
    let window_title = document.window_title();
    let render_plugin = RenderPlugin {
        render_creation: RenderCreation::Automatic(Box::new(WgpuSettings {
            power_preference: bevy::render::settings::PowerPreference::HighPerformance,
            ..default()
        })),
        ..default()
    };
    let automation = options.runtime_automation();
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
        gate1_gameplay_stutter = options.gate1_gameplay_stutter_output.is_some(),
        gate1_renderdoc_capture = options.gate1_renderdoc_output.is_some(),
        gate1_presentation_route = options.gate1_presentation_route.presentation_route(),
        render_diagnostics_enabled = automation.render_diagnostics_policy
            == RenderDiagnosticsPolicy::Enabled,
        gate1_visual_proof = options.gate1_visual_proof_output.is_some(),
        weather_override,
        player_input_enabled,
        player_cursor_locked = player_input_enabled,
        "native runtime starting"
    );

    let default_plugins = DefaultPlugins
        .set(WindowPlugin {
            primary_window: Some(Window {
                title: window_title.to_owned(),
                resolution: WindowResolution::new(1920, 1080).with_scale_factor_override(1.0),
                present_mode,
                resizable: true,
                ..default()
            }),
            primary_cursor_options: Some(primary_cursor_options),
            ..default()
        })
        .set(render_plugin);
    #[cfg(feature = "render-profile")]
    let default_plugins = default_plugins.set(LogPlugin {
        fmt_layer: |_| {
            Some(Box::new(
                tracing_subscriber::fmt::Layer::default()
                    .with_writer(std::io::stderr)
                    .with_filter(LevelFilter::INFO),
            ))
        },
        ..default()
    });
    #[cfg(not(feature = "render-profile"))]
    let default_plugins = default_plugins.disable::<LogPlugin>();

    let mut app = App::new();
    #[cfg(all(feature = "presentation-profile", not(feature = "render-profile")))]
    if presentation_profile.is_enabled() {
        app.insert_resource(presentation_profile.clone());
    }
    app.insert_resource(ClearColor(Color::srgb(0.015, 0.025, 0.055)))
        .add_plugins(default_plugins);

    add_runtime_plugin(
        &mut app,
        document,
        RuntimePluginConfiguration {
            automation,
            terrain_debug: options.terrain_debug,
            terrain_partial_update_probe: options.terrain_partial_update_probe,
            player_camera: options.player_camera,
            gate1_gameplay_stutter_output: options.gate1_gameplay_stutter_output.clone(),
            gate1_presentation_route: options.gate1_presentation_route,
            gate1_renderdoc_output: options.gate1_renderdoc_output.clone(),
            gate1_renderdoc_nonce: options.gate1_renderdoc_nonce.clone(),
            gate1_visual_proof_output: options.gate1_visual_proof_output.clone(),
            weather_state: options.weather_state,
            quality_selection: options.quality_selection,
            player_input_enabled,
        },
    );

    let exit = app.run();
    tracing::info!(
        target: "forge::runtime",
        schema = "forge.runtime-event",
        schema_version = 1_u64,
        event_kind = "runtime_stopped",
        success = exit.is_success(),
        exit_code = u64::from(app_exit_code(&exit)),
        shutdown = "bevy_app_exit",
        "runtime stopped cleanly"
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

struct RuntimePluginConfiguration {
    automation: RuntimeAutomation,
    terrain_debug: TerrainDebugMode,
    terrain_partial_update_probe: TerrainPartialUpdateProbeMode,
    player_camera: PlayerCameraRuntimeMode,
    gate1_gameplay_stutter_output: Option<PathBuf>,
    gate1_presentation_route: Gate1PresentationRoute,
    gate1_renderdoc_output: Option<PathBuf>,
    gate1_renderdoc_nonce: Option<String>,
    gate1_visual_proof_output: Option<PathBuf>,
    weather_state: Option<WeatherState>,
    quality_selection: Option<VisualValleyQualitySelection>,
    player_input_enabled: bool,
}

#[allow(
    clippy::too_many_lines,
    reason = "one composition point wires the correlated optional Visual Valley plugins"
)]
fn add_runtime_plugin(
    app: &mut App,
    document: RuntimeDocument,
    configuration: RuntimePluginConfiguration,
) {
    let RuntimePluginConfiguration {
        automation,
        terrain_debug,
        terrain_partial_update_probe,
        player_camera,
        gate1_gameplay_stutter_output,
        gate1_presentation_route,
        gate1_renderdoc_output,
        gate1_renderdoc_nonce,
        gate1_visual_proof_output,
        weather_state,
        quality_selection,
        player_input_enabled,
    } = configuration;
    match document {
        RuntimeDocument::Gate0(document) => {
            app.add_plugins(ProofScenePlugin::new(document).with_automation(automation));
        }
        RuntimeDocument::VisualValley {
            terrain: document,
            vegetation,
            environment,
            water,
            weather,
            quality,
        } => {
            let gameplay_stutter_enabled = gate1_gameplay_stutter_output.is_some();
            let visual_proof_enabled = gate1_visual_proof_output.is_some();
            let governed_metrics_required = vegetation.is_some()
                && environment.is_some()
                && water.is_some()
                && weather.is_some()
                && quality.is_some();
            let player_profile = player_controller_required(
                player_camera,
                gameplay_stutter_enabled,
                visual_proof_enabled,
            )
            .then(|| {
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
            let gameplay_stutter_plugin = gate1_gameplay_stutter_output.map(|output_dir| {
                let player = player_profile.as_ref().unwrap_or_else(|| {
                    panic!("Gate 1 gameplay stutter proof requires the embedded player-controller profile")
                });
                Gate1GameplayStutterPlugin::new(
                    output_dir,
                    gate1_presentation_route,
                    &document,
                    player,
                )
                .unwrap_or_else(|error| {
                    panic!("cannot prepare Gate 1 gameplay stutter harness: {error}")
                })
            });
            let visual_proof_plugin = gate1_visual_proof_output.map(|output_dir| {
                let water = water.as_deref().unwrap_or_else(|| {
                    panic!("Gate 1 visual proof requires the embedded water document")
                });
                let weather = weather.as_deref().unwrap_or_else(|| {
                    panic!("Gate 1 visual proof requires the embedded weather profile")
                });
                let player = player_profile.as_ref().unwrap_or_else(|| {
                    panic!("Gate 1 visual proof requires the embedded player-controller profile")
                });
                Gate1VisualProofPlugin::new(output_dir, &document, water, weather, player)
                    .unwrap_or_else(|error| {
                        panic!("cannot prepare Gate 1 visual proof harness: {error}")
                    })
            });
            let renderdoc_capture_plugin =
                gate1_renderdoc_output
                    .zip(gate1_renderdoc_nonce)
                    .map(|(output_dir, nonce)| {
                        let vegetation = vegetation.as_deref().unwrap_or_else(|| {
                            panic!(
                                "Gate 1 RenderDoc capture requires the embedded vegetation document"
                            )
                        });
                        let environment = environment.as_deref().unwrap_or_else(|| {
                            panic!(
                                "Gate 1 RenderDoc capture requires the embedded environment profile"
                            )
                        });
                        let water = water.as_deref().unwrap_or_else(|| {
                            panic!("Gate 1 RenderDoc capture requires the embedded water document")
                        });
                        let weather = weather.as_deref().unwrap_or_else(|| {
                            panic!("Gate 1 RenderDoc capture requires the embedded weather profile")
                        });
                        let quality = quality.as_deref().unwrap_or_else(|| {
                            panic!("Gate 1 RenderDoc capture requires the embedded quality profile")
                        });
                        let expected_weather = weather_state.unwrap_or_else(|| {
                            panic!("Gate 1 RenderDoc capture requires an explicit weather state")
                        });
                        Gate1RenderDocCapturePlugin::new(
                            output_dir,
                            nonce,
                            expected_weather,
                            &document,
                            vegetation,
                            environment,
                            water,
                            weather,
                            quality,
                        )
                        .unwrap_or_else(|error| {
                            panic!("cannot prepare Gate 1 RenderDoc capture harness: {error}")
                        })
                    });

            let scenery_plugin = vegetation.as_deref().map(|vegetation| {
                VisualValleySceneryPlugin::new(document.clone(), vegetation.clone())
            });
            let resolved_quality = quality.map(|quality| {
                let selection = quality_selection
                    .unwrap_or(VisualValleyQualitySelection::Fixed(quality.default_tier));
                let plugin = VisualValleyQualityPlugin::new(*quality, selection);
                let tier = plugin.startup_tier();
                let preset = plugin.startup_preset();
                (plugin, preset, tier)
            });
            let vegetation_plugin = vegetation.map(|vegetation| {
                let plugin = VisualValleyVegetationPlugin::new(document.clone(), *vegetation);
                match &resolved_quality {
                    Some((_, preset, _)) => plugin.with_quality_preset(preset.clone()),
                    None => plugin,
                }
            });
            let water_plugin = water.map(|water| {
                let plugin = VisualValleyWaterPlugin::new(document.clone(), *water);
                match &resolved_quality {
                    Some((_, preset, tier)) => plugin.with_quality_preset(preset.clone(), *tier),
                    None => plugin,
                }
            });
            let weather_plugin = weather.map(|weather| {
                let mut plugin = VisualValleyWeatherPlugin::new(*weather);
                if let Some(state) = weather_state {
                    plugin = plugin.with_initial_state(state);
                }
                if let Some((_, preset, tier)) = &resolved_quality {
                    plugin = plugin.with_quality_preset(preset.clone(), *tier);
                }
                plugin
            });
            let mut visual_valley = VisualValleyPlugin::new(document)
                .with_automation(automation)
                .with_chunk_lod_gizmos(terrain_debug.is_enabled());
            if governed_metrics_required {
                visual_valley = visual_valley.with_governed_metrics_required();
            }
            if let Some(profile) = player_profile {
                visual_valley = visual_valley
                    .with_player_controller(profile)
                    .with_player_interactive_input(player_input_enabled)
                    .with_overview_camera(false);
            }
            if let Some(probe) = &player_probe {
                visual_valley = visual_valley.with_player_spawn_position(probe.spawn_position());
            }
            if let Some(proof) = &gameplay_stutter_plugin {
                visual_valley =
                    visual_valley.with_player_spawn_position(proof.player_spawn_position());
            }
            if let Some(proof) = &visual_proof_plugin {
                visual_valley =
                    visual_valley.with_player_spawn_position(proof.player_spawn_position());
            }

            app.add_plugins(visual_valley);
            if let Some(environment) = environment {
                let plugin = VisualValleyEnvironmentPlugin::new(*environment);
                app.add_plugins(match &resolved_quality {
                    Some((_, preset, _)) => plugin.with_quality_preset(preset.clone()),
                    None => plugin,
                });
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
            if let Some(scenery_plugin) = scenery_plugin {
                app.add_plugins(scenery_plugin);
            }
            if let Some((quality_plugin, _, _)) = resolved_quality {
                app.add_plugins(quality_plugin);
            }
            if terrain_partial_update_probe.is_enabled() {
                app.add_plugins(TerrainPartialUpdateProbePlugin::new());
            }
            if let Some(probe) = player_probe {
                app.add_plugins(probe);
            }
            if let Some(gameplay_stutter_plugin) = gameplay_stutter_plugin {
                app.add_plugins(gameplay_stutter_plugin);
            }
            if let Some(visual_proof_plugin) = visual_proof_plugin {
                app.add_plugins(visual_proof_plugin);
            }
            if let Some(renderdoc_capture_plugin) = renderdoc_capture_plugin {
                app.add_plugins(renderdoc_capture_plugin);
            }
        }
    }
}

const fn player_controller_required(
    player_camera: PlayerCameraRuntimeMode,
    gate1_gameplay_stutter: bool,
    gate1_visual_proof: bool,
) -> bool {
    player_camera.is_enabled() || gate1_gameplay_stutter || gate1_visual_proof
}

const GATE0_WINDOW_TITLE: &str = "OpenRPG Forge · Rust Gate 0";
const VISUAL_VALLEY_WINDOW_TITLE: &str = "OpenRPG Forge · Rust Gate 1 · Visual Valley";

#[derive(Debug)]
enum RuntimeDocument {
    Gate0(SceneProofDocument),
    VisualValley {
        terrain: TerrainDocument,
        vegetation: Option<Box<VegetationDocument>>,
        environment: Option<Box<EnvironmentProfileDocument>>,
        water: Option<Box<WaterDocument>>,
        weather: Option<Box<WeatherProfileDocument>>,
        quality: Option<Box<VisualValleyQualityProfileDocument>>,
    },
}

impl RuntimeDocument {
    const fn mode_label(&self) -> &'static str {
        match self {
            Self::Gate0(_) => "gate0_scene_proof",
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
    quality: Option<VisualValleyQualityProfileDocument>,
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
    if let Some(quality) = &quality {
        quality
            .validate_identity(document.project_id, document.area_id, document.revision)
            .map_err(|error| {
                format!("unsupported Visual Valley quality profile for {source}: {error}")
            })?;
    }
    Ok(RuntimeDocument::VisualValley {
        terrain: document,
        vegetation: vegetation.map(Box::new),
        environment: environment.map(Box::new),
        water: water.map(Box::new),
        weather: weather.map(Box::new),
        quality: quality.map(Box::new),
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

#[cfg(feature = "render-profile")]
#[derive(Clone, Copy, Debug, Default, Eq, PartialEq)]
enum RenderProfileGate1Diagnostic {
    #[default]
    Disabled,
    Enabled,
}

#[cfg(feature = "render-profile")]
impl RenderProfileGate1Diagnostic {
    const fn is_enabled(self) -> bool {
        matches!(self, Self::Enabled)
    }
}

#[cfg(feature = "presentation-profile")]
#[derive(Clone, Copy, Debug, Default, Eq, PartialEq)]
enum PresentationProfileGate1Diagnostic {
    #[default]
    Disabled,
    Enabled,
}

#[cfg(feature = "presentation-profile")]
impl PresentationProfileGate1Diagnostic {
    const fn is_enabled(self) -> bool {
        matches!(self, Self::Enabled)
    }
}

#[derive(Debug)]
struct Options {
    scene_path: Option<PathBuf>,
    terrain_path: Option<PathBuf>,
    visual_valley: bool,
    terrain_debug: TerrainDebugMode,
    terrain_partial_update_probe: TerrainPartialUpdateProbeMode,
    player_camera: PlayerCameraRuntimeMode,
    gate1_gameplay_stutter_output: Option<PathBuf>,
    gate1_presentation_route: Gate1PresentationRoute,
    gate1_renderdoc_output: Option<PathBuf>,
    gate1_renderdoc_nonce: Option<String>,
    gate1_visual_proof_output: Option<PathBuf>,
    #[cfg(feature = "render-profile")]
    render_profile_gate1_diagnostic: RenderProfileGate1Diagnostic,
    #[cfg(feature = "presentation-profile")]
    presentation_profile_gate1_diagnostic: PresentationProfileGate1Diagnostic,
    weather_state: Option<WeatherState>,
    quality_selection: Option<VisualValleyQualitySelection>,
    capture_path: Option<PathBuf>,
    capture_frame: u32,
    metrics_path: Option<PathBuf>,
    metrics_warmup_frames: u32,
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
            scene_path: None,
            terrain_path: None,
            visual_valley: false,
            terrain_debug: TerrainDebugMode::Hidden,
            terrain_partial_update_probe: TerrainPartialUpdateProbeMode::Disabled,
            player_camera: PlayerCameraRuntimeMode::Disabled,
            gate1_gameplay_stutter_output: None,
            gate1_presentation_route: Gate1PresentationRoute::AutoNoVsync,
            gate1_renderdoc_output: None,
            gate1_renderdoc_nonce: None,
            gate1_visual_proof_output: None,
            #[cfg(feature = "render-profile")]
            render_profile_gate1_diagnostic: RenderProfileGate1Diagnostic::Disabled,
            #[cfg(feature = "presentation-profile")]
            presentation_profile_gate1_diagnostic: PresentationProfileGate1Diagnostic::Disabled,
            weather_state: None,
            quality_selection: None,
            capture_path: None,
            capture_frame: 120,
            metrics_path: None,
            metrics_warmup_frames: 60,
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
    #[allow(
        clippy::too_many_lines,
        reason = "the strict CLI parser keeps every mutually exclusive route in one ordered match"
    )]
    fn parse(mut arguments: impl Iterator<Item = String>) -> Result<Self, String> {
        let mut options = Self::default();
        while let Some(argument) = arguments.next() {
            match argument.as_str() {
                "--scene" => options.scene_path = Some(next_path(&mut arguments, "--scene")?),
                "--terrain" => {
                    options.terrain_path = Some(next_path(&mut arguments, "--terrain")?);
                }
                "--visual-valley" => options.visual_valley = true,
                "--terrain-debug" => options.terrain_debug = TerrainDebugMode::ChunkLodGizmos,
                "--terrain-partial-update-probe" => {
                    options.terrain_partial_update_probe = TerrainPartialUpdateProbeMode::Enabled;
                }
                "--player-camera" => options.player_camera.select_interactive()?,
                "--player-camera-probe" => options.player_camera.select_probe()?,
                "--gate1-gameplay-stutter" => {
                    if options.gate1_gameplay_stutter_output.is_some() {
                        return Err(
                            "--gate1-gameplay-stutter may only be specified once".to_owned()
                        );
                    }
                    options.gate1_gameplay_stutter_output =
                        Some(next_path(&mut arguments, "--gate1-gameplay-stutter")?);
                }
                "--gate1-fifo-diagnostic" => {
                    if matches!(
                        options.gate1_presentation_route,
                        Gate1PresentationRoute::FifoDiagnostic
                    ) {
                        return Err("--gate1-fifo-diagnostic may only be specified once".to_owned());
                    }
                    options.gate1_presentation_route = Gate1PresentationRoute::FifoDiagnostic;
                }
                "--gate1-renderdoc-capture" => {
                    if options.gate1_renderdoc_output.is_some() {
                        return Err(
                            "--gate1-renderdoc-capture may only be specified once".to_owned()
                        );
                    }
                    options.gate1_renderdoc_output =
                        Some(next_path(&mut arguments, "--gate1-renderdoc-capture")?);
                }
                "--gate1-renderdoc-nonce" => {
                    if options.gate1_renderdoc_nonce.is_some() {
                        return Err("--gate1-renderdoc-nonce may only be specified once".to_owned());
                    }
                    options.gate1_renderdoc_nonce =
                        Some(next_string(&mut arguments, "--gate1-renderdoc-nonce")?);
                }
                "--gate1-visual-proof" => {
                    if options.gate1_visual_proof_output.is_some() {
                        return Err("--gate1-visual-proof may only be specified once".to_owned());
                    }
                    options.gate1_visual_proof_output =
                        Some(next_path(&mut arguments, "--gate1-visual-proof")?);
                }
                #[cfg(feature = "render-profile")]
                "--render-profile-gate1-diagnostic" => {
                    options.render_profile_gate1_diagnostic = RenderProfileGate1Diagnostic::Enabled;
                }
                #[cfg(feature = "presentation-profile")]
                "--presentation-profile-gate1-diagnostic" => {
                    if options.presentation_profile_gate1_diagnostic.is_enabled() {
                        return Err(
                            "--presentation-profile-gate1-diagnostic may only be specified once"
                                .to_owned(),
                        );
                    }
                    options.presentation_profile_gate1_diagnostic =
                        PresentationProfileGate1Diagnostic::Enabled;
                }
                "--weather" => {
                    if options.weather_state.is_some() {
                        return Err("--weather may only be specified once".to_owned());
                    }
                    options.weather_state = Some(next_weather_state(&mut arguments)?);
                }
                "--quality" => {
                    let tier = next_quality_tier(&mut arguments)?;
                    options.select_quality(VisualValleyQualitySelection::Fixed(tier))?;
                }
                "--adaptive-quality" => {
                    options.select_quality(VisualValleyQualitySelection::Adaptive)?;
                }
                "--benchmark-quality" => {
                    options.select_quality(VisualValleyQualitySelection::Benchmark)?;
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
                    return Err("usage: forge_runtime [--scene file.json | --visual-valley | --terrain terrain.json] [--quality low|medium|high|ultra | --adaptive-quality | --benchmark-quality] [--gate1-gameplay-stutter output-dir [--gate1-fifo-diagnostic] | --gate1-renderdoc-capture output-dir --gate1-renderdoc-nonce nonce | --gate1-visual-proof output-dir] [--terrain-debug] [--terrain-partial-update-probe] [--player-camera | --player-camera-probe] [--weather clear|rain] [--capture file.png] [--capture-frame n] [--metrics file.json] [--metrics-warmup-frames n] [--exit-after-frames n] [--resize-at-frame n --resize-width px --resize-height px] [--close-at-frame n] [--no-vsync] [--log-probe]".to_owned());
                }
                _ => return Err(format!("unknown option `{argument}`")),
            }
        }
        options.validate()?;
        Ok(options)
    }

    fn validate(&self) -> Result<(), String> {
        let input_count = usize::from(self.scene_path.is_some())
            + usize::from(self.terrain_path.is_some())
            + usize::from(self.visual_valley);
        if input_count > 1 {
            return Err("--scene, --terrain and --visual-valley are mutually exclusive".to_owned());
        }
        if self.terrain_debug.is_enabled() && self.terrain_path.is_none() && !self.visual_valley {
            return Err("--terrain-debug requires --visual-valley or --terrain".to_owned());
        }
        if self.weather_state.is_some() && !self.visual_valley {
            return Err("--weather requires --visual-valley".to_owned());
        }
        if self.quality_selection.is_some() && !self.visual_valley {
            return Err("quality selection requires --visual-valley".to_owned());
        }
        #[cfg(feature = "render-profile")]
        self.validate_render_profile_gate1_diagnostic()?;
        #[cfg(feature = "presentation-profile")]
        self.validate_presentation_profile_gate1_diagnostic()?;
        self.validate_gate1_fifo_diagnostic()?;
        self.validate_gate1_gameplay_stutter()?;
        self.validate_gate1_renderdoc_capture()?;
        self.validate_gate1_visual_proof()?;
        self.validate_terrain_partial_update_probe()?;
        self.validate_player_camera()?;
        self.validate_capture_and_metrics()?;
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
                || self.terrain_partial_update_probe.is_enabled())
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
                || self.terrain_debug.is_enabled()
                || self.terrain_partial_update_probe.is_enabled()
                || self.player_camera.is_enabled()
                || self.gate1_gameplay_stutter_output.is_some()
                || self.gate1_renderdoc_output.is_some()
                || self.gate1_renderdoc_nonce.is_some()
                || self.gate1_visual_proof_output.is_some()
                || self.weather_state.is_some()
                || self.quality_selection.is_some()
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

    #[cfg(feature = "render-profile")]
    fn render_profile_route_error(&self) -> Option<&'static str> {
        if self.log_probe {
            return Some(
                "NONQUALIFYING render-profile builds refuse --log-probe because Forge structured JSON tracing is disabled",
            );
        }
        if self.gate1_renderdoc_output.is_some()
            || self.gate1_visual_proof_output.is_some()
            || (self.gate1_gameplay_stutter_output.is_some()
                && !self.render_profile_gate1_diagnostic.is_enabled())
        {
            return Some(
                "NONQUALIFYING render-profile builds refuse Gate 1 qualification routes; use a diagnostic --visual-valley route instead",
            );
        }
        None
    }

    #[cfg(feature = "render-profile")]
    fn validate_render_profile_gate1_diagnostic(&self) -> Result<(), String> {
        if !self.render_profile_gate1_diagnostic.is_enabled() {
            return Ok(());
        }
        if self.gate1_renderdoc_output.is_some() || self.gate1_visual_proof_output.is_some() {
            return Err(
                "--render-profile-gate1-diagnostic cannot be combined with Gate 1 qualification capture routes"
                    .to_owned(),
            );
        }
        if self.log_probe {
            return Err(
                "--render-profile-gate1-diagnostic cannot be combined with --log-probe".to_owned(),
            );
        }
        if self.gate1_gameplay_stutter_output.is_none() {
            return Err(
                "--render-profile-gate1-diagnostic requires --gate1-gameplay-stutter".to_owned(),
            );
        }
        Ok(())
    }

    #[cfg(all(feature = "presentation-profile", not(feature = "render-profile")))]
    fn presentation_profile_route_error(&self) -> Option<&'static str> {
        if self.gate1_renderdoc_output.is_some()
            || self.gate1_visual_proof_output.is_some()
            || (self.gate1_gameplay_stutter_output.is_some()
                && !self.presentation_profile_gate1_diagnostic.is_enabled())
        {
            return Some(
                "NONQUALIFYING presentation-profile builds refuse Gate 1 qualification routes; pass --presentation-profile-gate1-diagnostic with --gate1-gameplay-stutter for the bounded diagnostic",
            );
        }
        None
    }

    #[cfg(feature = "presentation-profile")]
    fn validate_presentation_profile_gate1_diagnostic(&self) -> Result<(), String> {
        if !self.presentation_profile_gate1_diagnostic.is_enabled() {
            return Ok(());
        }
        #[cfg(feature = "render-profile")]
        return Err(
            "--presentation-profile-gate1-diagnostic is incompatible with the trace_chrome render-profile feature"
                .to_owned(),
        );
        #[cfg(not(feature = "render-profile"))]
        {
            if self.gate1_renderdoc_output.is_some() || self.gate1_visual_proof_output.is_some() {
                return Err(
                    "--presentation-profile-gate1-diagnostic cannot be combined with Gate 1 qualification capture routes"
                        .to_owned(),
                );
            }
            if self.log_probe {
                return Err(
                    "--presentation-profile-gate1-diagnostic cannot be combined with --log-probe"
                        .to_owned(),
                );
            }
            if self.gate1_gameplay_stutter_output.is_none() {
                return Err(
                    "--presentation-profile-gate1-diagnostic requires --gate1-gameplay-stutter"
                        .to_owned(),
                );
            }
            Ok(())
        }
    }

    #[cfg(all(feature = "presentation-profile", not(feature = "render-profile")))]
    fn presentation_profile_controller(&self) -> PresentationProfileController {
        self.gate1_gameplay_stutter_output
            .as_deref()
            .filter(|_| self.presentation_profile_gate1_diagnostic.is_enabled())
            .map_or_else(
                PresentationProfileController::disabled,
                PresentationProfileController::enabled,
            )
    }

    fn validate_gate1_fifo_diagnostic(&self) -> Result<(), String> {
        if !matches!(
            self.gate1_presentation_route,
            Gate1PresentationRoute::FifoDiagnostic
        ) {
            return Ok(());
        }
        if self.gate1_gameplay_stutter_output.is_none() {
            return Err("--gate1-fifo-diagnostic requires --gate1-gameplay-stutter".to_owned());
        }
        if !self.vsync {
            return Err("--gate1-fifo-diagnostic cannot be combined with --no-vsync".to_owned());
        }
        #[cfg(feature = "render-profile")]
        return Err(
            "--gate1-fifo-diagnostic is unavailable in NONQUALIFYING render-profile builds"
                .to_owned(),
        );
        #[cfg(not(feature = "render-profile"))]
        Ok(())
    }

    fn validate_gate1_gameplay_stutter(&self) -> Result<(), String> {
        let Some(output_dir) = self.gate1_gameplay_stutter_output.as_deref() else {
            return Ok(());
        };
        if output_dir.as_os_str().is_empty() {
            return Err(
                "--gate1-gameplay-stutter requires a non-empty output directory".to_owned(),
            );
        }
        if !self.visual_valley {
            return Err("--gate1-gameplay-stutter requires --visual-valley".to_owned());
        }
        if self.quality_selection
            != Some(VisualValleyQualitySelection::Fixed(VisualQualityTier::High))
        {
            return Err("--gate1-gameplay-stutter requires explicit --quality high".to_owned());
        }
        if self.terrain_debug.is_enabled()
            || self.terrain_partial_update_probe.is_enabled()
            || self.player_camera.is_enabled()
            || self.gate1_renderdoc_output.is_some()
            || self.gate1_visual_proof_output.is_some()
            || self.weather_state.is_some()
            || self.capture_path.is_some()
            || self.metrics_path.is_some()
            || self.exit_after_frames.is_some()
            || self.resize_at_frame.is_some()
            || self.close_at_frame.is_some()
            || self.log_probe
        {
            return Err(
                "--gate1-gameplay-stutter owns controller, camera, capture synchronization and exit; it cannot be combined with debug, other probes, weather, capture, metrics, resize, close or logging modes"
                    .to_owned(),
            );
        }
        match self.gate1_presentation_route {
            Gate1PresentationRoute::AutoNoVsync if self.vsync => {
                return Err(
                    "--gate1-gameplay-stutter requires --no-vsync for the governed acquisition protocol"
                        .to_owned(),
                );
            }
            Gate1PresentationRoute::FifoDiagnostic if !self.vsync => {
                return Err("--gate1-fifo-diagnostic cannot be combined with --no-vsync".to_owned());
            }
            _ => {}
        }
        Ok(())
    }

    fn validate_gate1_renderdoc_capture(&self) -> Result<(), String> {
        let (output_dir, nonce) = match (
            self.gate1_renderdoc_output.as_deref(),
            self.gate1_renderdoc_nonce.as_deref(),
        ) {
            (None, None) => return Ok(()),
            (Some(_), None) => {
                return Err("--gate1-renderdoc-capture requires --gate1-renderdoc-nonce".to_owned());
            }
            (None, Some(_)) => {
                return Err("--gate1-renderdoc-nonce requires --gate1-renderdoc-capture".to_owned());
            }
            (Some(output_dir), Some(nonce)) => (output_dir, nonce),
        };
        if !cfg!(target_os = "windows") {
            return Err(
                "--gate1-renderdoc-capture is currently qualified only on Windows Vulkan"
                    .to_owned(),
            );
        }
        if output_dir.as_os_str().is_empty() {
            return Err(
                "--gate1-renderdoc-capture requires a non-empty output directory".to_owned(),
            );
        }
        validate_gate1_renderdoc_nonce(nonce)?;
        if !self.visual_valley {
            return Err("--gate1-renderdoc-capture requires --visual-valley".to_owned());
        }
        if self.quality_selection
            != Some(VisualValleyQualitySelection::Fixed(VisualQualityTier::High))
        {
            return Err("--gate1-renderdoc-capture requires explicit --quality high".to_owned());
        }
        if self.weather_state.is_none() {
            return Err(
                "--gate1-renderdoc-capture requires explicit --weather clear or --weather rain"
                    .to_owned(),
            );
        }
        if self.vsync {
            return Err(
                "--gate1-renderdoc-capture requires --no-vsync for the governed acquisition protocol"
                    .to_owned(),
            );
        }
        if self.terrain_debug.is_enabled()
            || self.terrain_partial_update_probe.is_enabled()
            || self.player_camera.is_enabled()
            || self.gate1_gameplay_stutter_output.is_some()
            || self.gate1_visual_proof_output.is_some()
            || self.capture_path.is_some()
            || self.metrics_path.is_some()
            || self.exit_after_frames.is_some()
            || self.resize_at_frame.is_some()
            || self.close_at_frame.is_some()
            || self.log_probe
            || !matches!(
                self.gate1_presentation_route,
                Gate1PresentationRoute::AutoNoVsync
            )
        {
            return Err(
                "--gate1-renderdoc-capture owns camera, weather, capture synchronization and exit; it cannot be combined with debug, probes, player-camera, other Gate 1 routes, built-in capture, metrics, resize, close or logging modes"
                    .to_owned(),
            );
        }
        Ok(())
    }

    fn validate_capture_and_metrics(&self) -> Result<(), String> {
        if self.capture_path.is_some()
            && self
                .exit_after_frames
                .is_some_and(|frame| frame <= self.capture_frame.saturating_add(5))
        {
            return Err(
                "--exit-after-frames must be at least six frames after --capture-frame".to_owned(),
            );
        }
        if self.capture_path.is_some() && self.metrics_path.is_some() {
            let earliest_metrics_warmup = self
                .capture_frame
                .checked_add(PIPELINE_WARMUP_STABLE_FRAMES)
                .ok_or_else(|| {
                    format!(
                        "--capture-frame is too large to leave {PIPELINE_WARMUP_STABLE_FRAMES} stable pipeline frames before metrics warmup"
                    )
                })?;
            if self.metrics_warmup_frames < earliest_metrics_warmup {
                return Err(format!(
                    "--metrics-warmup-frames must be at least {PIPELINE_WARMUP_STABLE_FRAMES} frames after --capture-frame (minimum {earliest_metrics_warmup})"
                ));
            }
        }
        Ok(())
    }

    fn validate_gate1_visual_proof(&self) -> Result<(), String> {
        let Some(output_dir) = self.gate1_visual_proof_output.as_deref() else {
            return Ok(());
        };
        if output_dir.as_os_str().is_empty() {
            return Err("--gate1-visual-proof requires a non-empty output directory".to_owned());
        }
        if !self.visual_valley {
            return Err("--gate1-visual-proof requires --visual-valley".to_owned());
        }
        if matches!(
            self.quality_selection,
            Some(VisualValleyQualitySelection::Adaptive | VisualValleyQualitySelection::Benchmark)
        ) {
            return Err(
                "--gate1-visual-proof requires a fixed quality tier; adaptive and benchmark modes are not accepted"
                    .to_owned(),
            );
        }
        if self.terrain_debug.is_enabled()
            || self.terrain_partial_update_probe.is_enabled()
            || self.player_camera.is_enabled()
            || self.gate1_renderdoc_output.is_some()
            || self.weather_state.is_some()
            || self.capture_path.is_some()
            || self.metrics_path.is_some()
            || self.exit_after_frames.is_some()
            || self.resize_at_frame.is_some()
            || self.close_at_frame.is_some()
            || self.log_probe
        {
            return Err(
                "--gate1-visual-proof owns camera, weather, capture and exit; it cannot be combined with debug, probe, player-camera, weather, capture, metrics, resize, close or logging modes"
                    .to_owned(),
            );
        }
        if self.vsync {
            return Err(
                "--gate1-visual-proof requires --no-vsync for the governed acquisition protocol"
                    .to_owned(),
            );
        }
        Ok(())
    }

    fn validate_player_camera(&self) -> Result<(), String> {
        if self.player_camera.is_enabled() && self.terrain_partial_update_probe.is_enabled() {
            return Err("--terrain-partial-update-probe cannot be combined with --player-camera or --player-camera-probe".to_owned());
        }
        if self.player_camera.is_enabled() && self.terrain_path.is_none() && !self.visual_valley {
            let option = if self.player_camera.is_probe() {
                "--player-camera-probe"
            } else {
                "--player-camera"
            };
            return Err(format!("{option} requires --visual-valley or --terrain"));
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
        if self.terrain_partial_update_probe.is_enabled() && self.player_camera.is_enabled() {
            return Err("--terrain-partial-update-probe cannot be combined with --player-camera or --player-camera-probe".to_owned());
        }
        if self.terrain_partial_update_probe.is_enabled()
            && self.terrain_path.is_none()
            && !self.visual_valley
        {
            return Err(
                "--terrain-partial-update-probe requires --visual-valley or --terrain".to_owned(),
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
            render_diagnostics_policy: if cfg!(feature = "render-profile")
                || self.gate1_gameplay_stutter_output.is_some()
                || self.gate1_renderdoc_output.is_some()
            {
                RenderDiagnosticsPolicy::DisabledForExternalPresentationQualification
            } else {
                RenderDiagnosticsPolicy::Enabled
            },
        }
    }

    const fn requested_present_mode(&self) -> PresentMode {
        if self.gate1_gameplay_stutter_output.is_some() {
            self.gate1_presentation_route.present_mode()
        } else {
            present_mode_for_vsync(self.vsync)
        }
    }

    fn select_quality(&mut self, selection: VisualValleyQualitySelection) -> Result<(), String> {
        if self.quality_selection.is_some() {
            return Err(
                "--quality, --adaptive-quality and --benchmark-quality are mutually exclusive and may only be specified once"
                    .to_owned(),
            );
        }
        self.quality_selection = Some(selection);
        Ok(())
    }

    fn load_document(&self) -> Result<RuntimeDocument, String> {
        if let Some(path) = &self.scene_path {
            let json = fs::read_to_string(path)
                .map_err(|error| format!("cannot read {}: {error}", path.display()))?;
            let document = SceneProofDocument::from_json(&json)
                .map_err(|error| format!("invalid scene {}: {error}", path.display()))?;
            return Ok(RuntimeDocument::Gate0(document));
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
        if self.visual_valley {
            let document = embedded_visual_valley_terrain_document()
                .map_err(|error| format!("invalid embedded Visual Valley terrain: {error}"))?;
            let vegetation = embedded_visual_valley_vegetation_document()
                .map_err(|error| format!("invalid embedded Visual Valley vegetation: {error}"))?;
            let environment = embedded_visual_valley_environment_profile()
                .map_err(|error| format!("invalid embedded Visual Valley environment: {error}"))?;
            let water = embedded_visual_valley_water_document()
                .map_err(|error| format!("invalid embedded Visual Valley water: {error}"))?;
            let weather = embedded_visual_valley_weather_profile()
                .map_err(|error| format!("invalid embedded Visual Valley weather: {error}"))?;
            let quality = VisualValleyQualityProfileDocument::from_json(include_str!(
                "../../../examples/visual_valley/quality-profile.v1.json"
            ))
            .map_err(|error| format!("invalid embedded Visual Valley quality profile: {error}"))?;
            return validated_visual_valley_document(
                document,
                Some(vegetation),
                Some(environment),
                Some(water),
                Some(weather),
                Some(quality),
                "embedded Visual Valley terrain",
            );
        }
        embedded_gate0_proof_document()
            .map(RuntimeDocument::Gate0)
            .map_err(|error| error.to_string())
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
        if let Some(output_dir) = self.gate1_visual_proof_output.as_deref() {
            prepare_gate1_visual_proof_directory(output_dir)?;
        }
        if let Some(output_dir) = self.gate1_gameplay_stutter_output.as_deref() {
            prepare_gate1_gameplay_stutter_directory(output_dir)?;
        }
        if let Some(output_dir) = self.gate1_renderdoc_output.as_deref() {
            prepare_gate1_renderdoc_directory(output_dir)?;
        }
        Ok(())
    }

    #[cfg(feature = "render-profile")]
    fn write_nonqualifying_render_profile_marker(&self) -> Result<(), String> {
        if !self.render_profile_gate1_diagnostic.is_enabled() {
            return Ok(());
        }
        self.validate_render_profile_gate1_diagnostic()?;
        let output_dir = self
            .gate1_gameplay_stutter_output
            .as_deref()
            .ok_or_else(|| {
                "--render-profile-gate1-diagnostic requires --gate1-gameplay-stutter".to_owned()
            })?;
        let marker = NonqualifyingRenderProfileMarker {
            schema: NONQUALIFYING_RENDER_PROFILE_SCHEMA,
            schema_version: 1,
            qualifying: false,
            source_commit: option_env!("FORGE_SOURCE_COMMIT"),
            feature: "render-profile",
            reason: NONQUALIFYING_RENDER_PROFILE_REASON,
        };
        write_nonqualifying_render_profile_marker_atomic(
            &output_dir.join(NONQUALIFYING_RENDER_PROFILE_MARKER),
            &marker,
        )
    }

    fn write_nonqualifying_gate1_fifo_diagnostic_marker(&self) -> Result<(), String> {
        if !matches!(
            self.gate1_presentation_route,
            Gate1PresentationRoute::FifoDiagnostic
        ) {
            return Ok(());
        }
        self.validate_gate1_fifo_diagnostic()?;
        let output_dir = self
            .gate1_gameplay_stutter_output
            .as_deref()
            .ok_or_else(|| {
                "--gate1-fifo-diagnostic requires --gate1-gameplay-stutter".to_owned()
            })?;
        let marker = NonqualifyingGate1FifoDiagnosticMarker {
            schema: NONQUALIFYING_GATE1_FIFO_DIAGNOSTIC_SCHEMA,
            schema_version: 1,
            qualifying: false,
            source_commit: option_env!("FORGE_SOURCE_COMMIT"),
            presentation_route: self.gate1_presentation_route.presentation_route(),
            requested_present_mode: self.gate1_presentation_route.requested_present_mode(),
            vsync_requested: self.gate1_presentation_route.vsync_requested(),
            observation_scope: GATE1_PRESENTATION_OBSERVATION_SCOPE,
            reason: NONQUALIFYING_GATE1_FIFO_DIAGNOSTIC_REASON,
        };
        write_nonqualifying_gate1_fifo_diagnostic_marker_atomic(
            &output_dir.join(NONQUALIFYING_GATE1_FIFO_DIAGNOSTIC_MARKER),
            &marker,
        )
    }
}

#[cfg(feature = "render-profile")]
fn write_nonqualifying_render_profile_marker_atomic(
    path: &Path,
    marker: &NonqualifyingRenderProfileMarker,
) -> Result<(), String> {
    let file_name = path
        .file_name()
        .and_then(|name| name.to_str())
        .ok_or_else(|| {
            format!(
                "render-profile marker has no UTF-8 filename: {}",
                path.display()
            )
        })?;
    let temporary = path.with_file_name(format!(".{file_name}.tmp"));
    let json = serde_json::to_vec_pretty(marker)
        .map_err(|error| format!("cannot serialize render-profile marker: {error}"))?;
    let mut file = fs::OpenOptions::new()
        .write(true)
        .create_new(true)
        .open(&temporary)
        .map_err(|error| {
            format!(
                "cannot create temporary render-profile marker {}: {error}",
                temporary.display()
            )
        })?;
    if let Err(error) = std::io::Write::write_all(&mut file, &json).and_then(|()| file.sync_all()) {
        drop(file);
        let _ = fs::remove_file(&temporary);
        return Err(format!(
            "cannot write temporary render-profile marker {}: {error}",
            temporary.display()
        ));
    }
    drop(file);

    // `fs::rename` replaces an existing destination on Windows. Publishing through a hard link
    // instead gives us one atomic, no-clobber directory entry because both paths share a parent.
    if let Err(error) = fs::hard_link(&temporary, path) {
        let _ = fs::remove_file(&temporary);
        if error.kind() == std::io::ErrorKind::AlreadyExists {
            return Err(format!(
                "refusing to replace existing render-profile marker {}",
                path.display()
            ));
        }
        return Err(format!(
            "cannot atomically publish render-profile marker {} as {}: {error}",
            temporary.display(),
            path.display()
        ));
    }
    fs::remove_file(&temporary).map_err(|error| {
        format!(
            "render-profile marker {} was published but temporary link {} could not be removed: {error}",
            path.display(),
            temporary.display()
        )
    })
}

fn write_nonqualifying_gate1_fifo_diagnostic_marker_atomic(
    path: &Path,
    marker: &NonqualifyingGate1FifoDiagnosticMarker,
) -> Result<(), String> {
    let file_name = path
        .file_name()
        .and_then(|name| name.to_str())
        .ok_or_else(|| {
            format!(
                "Gate 1 FIFO diagnostic marker has no UTF-8 filename: {}",
                path.display()
            )
        })?;
    let temporary = path.with_file_name(format!(".{file_name}.tmp"));
    let json = serde_json::to_vec_pretty(marker)
        .map_err(|error| format!("cannot serialize Gate 1 FIFO diagnostic marker: {error}"))?;
    let mut file = fs::OpenOptions::new()
        .write(true)
        .create_new(true)
        .open(&temporary)
        .map_err(|error| {
            format!(
                "cannot create temporary Gate 1 FIFO diagnostic marker {}: {error}",
                temporary.display()
            )
        })?;
    if let Err(error) = std::io::Write::write_all(&mut file, &json).and_then(|()| file.sync_all()) {
        drop(file);
        let _ = fs::remove_file(&temporary);
        return Err(format!(
            "cannot write temporary Gate 1 FIFO diagnostic marker {}: {error}",
            temporary.display()
        ));
    }
    drop(file);

    // The temporary and final entries share a directory. A hard link publishes atomically while
    // preserving the proof contract's no-clobber behavior on Windows.
    if let Err(error) = fs::hard_link(&temporary, path) {
        let _ = fs::remove_file(&temporary);
        if error.kind() == std::io::ErrorKind::AlreadyExists {
            return Err(format!(
                "refusing to replace existing Gate 1 FIFO diagnostic marker {}",
                path.display()
            ));
        }
        return Err(format!(
            "cannot atomically publish Gate 1 FIFO diagnostic marker {} as {}: {error}",
            temporary.display(),
            path.display()
        ));
    }
    fs::remove_file(&temporary).map_err(|error| {
        format!(
            "Gate 1 FIFO diagnostic marker {} was published but temporary link {} could not be removed: {error}",
            path.display(),
            temporary.display()
        )
    })
}

fn prepare_gate1_gameplay_stutter_directory(output_dir: &Path) -> Result<(), String> {
    match fs::symlink_metadata(output_dir) {
        Ok(metadata) => {
            if metadata.file_type().is_symlink() {
                return Err(format!(
                    "Gate 1 gameplay stutter output cannot be a symlink: {}",
                    output_dir.display()
                ));
            }
            if !metadata.is_dir() {
                return Err(format!(
                    "Gate 1 gameplay stutter output is not a directory: {}",
                    output_dir.display()
                ));
            }
            let mut entries = fs::read_dir(output_dir).map_err(|error| {
                format!(
                    "cannot inspect Gate 1 gameplay stutter output {}: {error}",
                    output_dir.display()
                )
            })?;
            if entries
                .next()
                .transpose()
                .map_err(|error| {
                    format!(
                        "cannot inspect Gate 1 gameplay stutter output {}: {error}",
                        output_dir.display()
                    )
                })?
                .is_some()
            {
                return Err(format!(
                    "Gate 1 gameplay stutter output must be absent or empty: {}",
                    output_dir.display()
                ));
            }
        }
        Err(error) if error.kind() == std::io::ErrorKind::NotFound => {
            fs::create_dir_all(output_dir).map_err(|error| {
                format!(
                    "cannot create Gate 1 gameplay stutter output {}: {error}",
                    output_dir.display()
                )
            })?;
        }
        Err(error) => {
            return Err(format!(
                "cannot inspect Gate 1 gameplay stutter output {}: {error}",
                output_dir.display()
            ));
        }
    }
    Ok(())
}

fn prepare_gate1_renderdoc_directory(output_dir: &Path) -> Result<(), String> {
    match fs::symlink_metadata(output_dir) {
        Ok(metadata) => {
            if metadata.file_type().is_symlink() {
                return Err(format!(
                    "Gate 1 RenderDoc output cannot be a symlink: {}",
                    output_dir.display()
                ));
            }
            if !metadata.is_dir() {
                return Err(format!(
                    "Gate 1 RenderDoc output is not a directory: {}",
                    output_dir.display()
                ));
            }
            let mut entries = fs::read_dir(output_dir).map_err(|error| {
                format!(
                    "cannot inspect Gate 1 RenderDoc output {}: {error}",
                    output_dir.display()
                )
            })?;
            if entries
                .next()
                .transpose()
                .map_err(|error| {
                    format!(
                        "cannot inspect Gate 1 RenderDoc output {}: {error}",
                        output_dir.display()
                    )
                })?
                .is_some()
            {
                return Err(format!(
                    "Gate 1 RenderDoc output must be absent or empty: {}",
                    output_dir.display()
                ));
            }
        }
        Err(error) if error.kind() == std::io::ErrorKind::NotFound => {
            fs::create_dir_all(output_dir).map_err(|error| {
                format!(
                    "cannot create Gate 1 RenderDoc output {}: {error}",
                    output_dir.display()
                )
            })?;
        }
        Err(error) => {
            return Err(format!(
                "cannot inspect Gate 1 RenderDoc output {}: {error}",
                output_dir.display()
            ));
        }
    }
    Ok(())
}

fn prepare_gate1_visual_proof_directory(output_dir: &Path) -> Result<(), String> {
    match fs::symlink_metadata(output_dir) {
        Ok(metadata) => {
            if metadata.file_type().is_symlink() {
                return Err(format!(
                    "Gate 1 visual proof output cannot be a symlink: {}",
                    output_dir.display()
                ));
            }
            if !metadata.is_dir() {
                return Err(format!(
                    "Gate 1 visual proof output is not a directory: {}",
                    output_dir.display()
                ));
            }
            let mut entries = fs::read_dir(output_dir).map_err(|error| {
                format!(
                    "cannot inspect Gate 1 visual proof output {}: {error}",
                    output_dir.display()
                )
            })?;
            if entries
                .next()
                .transpose()
                .map_err(|error| {
                    format!(
                        "cannot inspect Gate 1 visual proof output {}: {error}",
                        output_dir.display()
                    )
                })?
                .is_some()
            {
                return Err(format!(
                    "Gate 1 visual proof output must be absent or empty: {}",
                    output_dir.display()
                ));
            }
        }
        Err(error) if error.kind() == std::io::ErrorKind::NotFound => {
            fs::create_dir_all(output_dir).map_err(|error| {
                format!(
                    "cannot create Gate 1 visual proof output {}: {error}",
                    output_dir.display()
                )
            })?;
        }
        Err(error) => {
            return Err(format!(
                "cannot inspect Gate 1 visual proof output {}: {error}",
                output_dir.display()
            ));
        }
    }
    for child in ["captures", "movement"] {
        let path = output_dir.join(child);
        fs::create_dir(&path).map_err(|error| {
            format!(
                "cannot create Gate 1 visual proof directory {}: {error}",
                path.display()
            )
        })?;
    }
    Ok(())
}

#[cfg(not(any(feature = "render-profile", feature = "presentation-profile")))]
fn initialize_structured_tracing() -> Result<(), String> {
    let fallback = format!("info,{DEFAULT_FILTER},forge=info");
    let filter = EnvFilter::try_from_default_env().unwrap_or_else(|_| EnvFilter::new(fallback));
    tracing_subscriber::fmt()
        .with_env_filter(filter)
        .json()
        .flatten_event(true)
        .with_current_span(true)
        .with_span_list(true)
        .with_target(true)
        .with_thread_ids(true)
        .with_file(true)
        .with_line_number(true)
        .with_writer(std::io::stderr)
        .try_init()
        .map_err(|error| error.to_string())
}

fn startup_error(phase: &str, message: &str) -> AppExit {
    #[cfg(any(feature = "render-profile", feature = "presentation-profile"))]
    eprintln!("forge_runtime: startup failed during {phase}: {message}");
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

fn next_path(
    arguments: &mut impl Iterator<Item = String>,
    option: &str,
) -> Result<PathBuf, String> {
    arguments
        .next()
        .map(PathBuf::from)
        .ok_or_else(|| format!("{option} requires a path"))
}

fn next_string(
    arguments: &mut impl Iterator<Item = String>,
    option: &str,
) -> Result<String, String> {
    arguments
        .next()
        .ok_or_else(|| format!("{option} requires a value"))
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

fn next_quality_tier(
    arguments: &mut impl Iterator<Item = String>,
) -> Result<VisualQualityTier, String> {
    match arguments.next().as_deref() {
        Some("low") => Ok(VisualQualityTier::Low),
        Some("medium") => Ok(VisualQualityTier::Medium),
        Some("high") => Ok(VisualQualityTier::High),
        Some("ultra") => Ok(VisualQualityTier::Ultra),
        Some(value) => Err(format!(
            "--quality requires `low`, `medium`, `high` or `ultra`, received `{value}`"
        )),
        None => Err("--quality requires `low`, `medium`, `high` or `ultra`".to_owned()),
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
    use super::*;

    #[cfg(feature = "render-profile")]
    #[test]
    fn render_profile_refuses_qualification_and_structured_log_routes() {
        let mut options = Options::default();
        assert_eq!(options.render_profile_route_error(), None);

        options.log_probe = true;
        assert!(
            options
                .render_profile_route_error()
                .is_some_and(|message| message.contains("--log-probe"))
        );

        options.log_probe = false;
        options.gate1_gameplay_stutter_output = Some(PathBuf::from("gameplay-stutter"));
        assert!(
            options
                .render_profile_route_error()
                .is_some_and(|message| message.contains("Gate 1 qualification routes"))
        );
        options.render_profile_gate1_diagnostic = RenderProfileGate1Diagnostic::Enabled;
        assert_eq!(options.render_profile_route_error(), None);

        options.gate1_gameplay_stutter_output = None;
        options.gate1_visual_proof_output = Some(PathBuf::from("visual-proof"));
        assert!(
            options
                .render_profile_route_error()
                .is_some_and(|message| message.contains("Gate 1 qualification routes"))
        );
    }

    #[cfg(feature = "render-profile")]
    #[test]
    fn render_profile_gate1_diagnostic_is_an_explicit_gameplay_only_opt_in() {
        let accepted = Options::parse(
            [
                "--visual-valley",
                "--quality",
                "high",
                "--gate1-gameplay-stutter",
                "stutter-output",
                "--no-vsync",
                "--render-profile-gate1-diagnostic",
            ]
            .into_iter()
            .map(str::to_owned),
        )
        .unwrap();
        assert!(accepted.render_profile_gate1_diagnostic.is_enabled());
        assert_eq!(accepted.render_profile_route_error(), None);

        assert_eq!(
            Options::parse(
                ["--render-profile-gate1-diagnostic"]
                    .into_iter()
                    .map(str::to_owned)
            )
            .unwrap_err(),
            "--render-profile-gate1-diagnostic requires --gate1-gameplay-stutter"
        );
        assert_eq!(
            Options::parse(
                [
                    "--visual-valley",
                    "--quality",
                    "high",
                    "--gate1-visual-proof",
                    "visual-output",
                    "--no-vsync",
                    "--render-profile-gate1-diagnostic",
                ]
                .into_iter()
                .map(str::to_owned)
            )
            .unwrap_err(),
            "--render-profile-gate1-diagnostic cannot be combined with Gate 1 qualification capture routes"
        );
        assert_eq!(
            Options::parse(
                [
                    "--gate1-renderdoc-capture",
                    "renderdoc-output",
                    "--render-profile-gate1-diagnostic",
                ]
                .into_iter()
                .map(str::to_owned)
            )
            .unwrap_err(),
            "--render-profile-gate1-diagnostic cannot be combined with Gate 1 qualification capture routes"
        );
        assert_eq!(
            Options::parse(
                ["--log-probe", "--render-profile-gate1-diagnostic"]
                    .into_iter()
                    .map(str::to_owned)
            )
            .unwrap_err(),
            "--render-profile-gate1-diagnostic cannot be combined with --log-probe"
        );
    }

    #[cfg(not(feature = "render-profile"))]
    #[test]
    fn normal_build_does_not_recognize_render_profile_gate1_diagnostic() {
        assert_eq!(
            Options::parse(
                ["--render-profile-gate1-diagnostic"]
                    .into_iter()
                    .map(str::to_owned)
            )
            .unwrap_err(),
            "unknown option `--render-profile-gate1-diagnostic`"
        );
    }

    #[cfg(all(feature = "presentation-profile", not(feature = "render-profile")))]
    #[test]
    fn presentation_profile_is_an_explicit_gameplay_only_opt_in() {
        let accepted = Options::parse(
            [
                "--visual-valley",
                "--quality",
                "high",
                "--gate1-gameplay-stutter",
                "stutter-output",
                "--no-vsync",
                "--presentation-profile-gate1-diagnostic",
            ]
            .into_iter()
            .map(str::to_owned),
        )
        .unwrap();
        assert!(accepted.presentation_profile_gate1_diagnostic.is_enabled());
        assert_eq!(accepted.presentation_profile_route_error(), None);
        assert!(accepted.presentation_profile_controller().is_enabled());

        assert_eq!(
            Options::parse(
                ["--presentation-profile-gate1-diagnostic"]
                    .into_iter()
                    .map(str::to_owned)
            )
            .unwrap_err(),
            "--presentation-profile-gate1-diagnostic requires --gate1-gameplay-stutter"
        );

        let unmarked = Options::parse(
            [
                "--visual-valley",
                "--quality",
                "high",
                "--gate1-gameplay-stutter",
                "stutter-output",
                "--no-vsync",
            ]
            .into_iter()
            .map(str::to_owned),
        )
        .unwrap();
        assert!(
            unmarked
                .presentation_profile_route_error()
                .is_some_and(|message| message.contains("NONQUALIFYING"))
        );
        assert!(!unmarked.presentation_profile_controller().is_enabled());
    }

    #[cfg(all(feature = "presentation-profile", feature = "render-profile"))]
    #[test]
    fn presentation_profile_refuses_trace_chrome_coactivation() {
        assert_eq!(
            Options::parse(
                ["--presentation-profile-gate1-diagnostic"]
                    .into_iter()
                    .map(str::to_owned)
            )
            .unwrap_err(),
            "--presentation-profile-gate1-diagnostic is incompatible with the trace_chrome render-profile feature"
        );
    }

    #[cfg(not(feature = "presentation-profile"))]
    #[test]
    fn normal_build_does_not_recognize_presentation_profile_diagnostic() {
        assert_eq!(
            Options::parse(
                ["--presentation-profile-gate1-diagnostic"]
                    .into_iter()
                    .map(str::to_owned)
            )
            .unwrap_err(),
            "unknown option `--presentation-profile-gate1-diagnostic`"
        );
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
            quality: Some(loaded_quality),
        } = loaded
        else {
            panic!("--visual-valley must select the complete embedded Gate 1 document set");
        };
        let embedded = embedded_visual_valley_terrain_document().unwrap();
        let embedded_vegetation = embedded_visual_valley_vegetation_document().unwrap();
        let embedded_environment = embedded_visual_valley_environment_profile().unwrap();
        let embedded_water = embedded_visual_valley_water_document().unwrap();
        let embedded_weather = embedded_visual_valley_weather_profile().unwrap();
        let embedded_quality = VisualValleyQualityProfileDocument::from_json(include_str!(
            "../../../examples/visual_valley/quality-profile.v1.json"
        ))
        .unwrap();
        assert_eq!(loaded, embedded);
        assert_eq!(*loaded_vegetation, embedded_vegetation);
        assert_eq!(*loaded_environment, embedded_environment);
        assert_eq!(*loaded_water, embedded_water);
        assert_eq!(*loaded_weather, embedded_weather);
        assert_eq!(*loaded_quality, embedded_quality);
        assert_eq!(
            loaded.content_hash().unwrap(),
            embedded.content_hash().unwrap()
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

        assert_eq!(
            Options::parse(["--weather".to_owned(), "rain".to_owned()].into_iter()).unwrap_err(),
            "--weather requires --visual-valley"
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
    fn gate1_visual_proof_is_a_strict_fixed_quality_owned_sequence() {
        let accepted = Options::parse(
            [
                "--visual-valley".to_owned(),
                "--quality".to_owned(),
                "high".to_owned(),
                "--gate1-visual-proof".to_owned(),
                "proof-output".to_owned(),
                "--no-vsync".to_owned(),
            ]
            .into_iter(),
        )
        .unwrap();
        assert_eq!(
            accepted.gate1_visual_proof_output,
            Some(PathBuf::from("proof-output"))
        );
        assert_eq!(
            accepted.quality_selection,
            Some(VisualValleyQualitySelection::Fixed(VisualQualityTier::High))
        );
        assert!(player_controller_required(
            accepted.player_camera,
            false,
            accepted.gate1_visual_proof_output.is_some()
        ));
        assert!(!accepted.player_camera.is_enabled());

        assert_eq!(
            Options::parse(
                ["--gate1-visual-proof".to_owned(), "proof-output".to_owned(),].into_iter(),
            )
            .unwrap_err(),
            "--gate1-visual-proof requires --visual-valley"
        );
        assert!(
            Options::parse(
                [
                    "--visual-valley".to_owned(),
                    "--adaptive-quality".to_owned(),
                    "--gate1-visual-proof".to_owned(),
                    "proof-output".to_owned(),
                ]
                .into_iter(),
            )
            .unwrap_err()
            .contains("requires a fixed quality tier")
        );
        for conflicting in [
            vec!["--weather", "rain"],
            vec!["--player-camera"],
            vec!["--capture", "other.png"],
            vec!["--metrics", "metrics.json"],
            vec!["--terrain-debug"],
        ] {
            let mut arguments = vec![
                "--visual-valley".to_owned(),
                "--gate1-visual-proof".to_owned(),
                "proof-output".to_owned(),
            ];
            arguments.extend(conflicting.into_iter().map(str::to_owned));
            assert!(
                Options::parse(arguments.into_iter())
                    .unwrap_err()
                    .contains("owns camera, weather, capture and exit")
            );
        }
        assert_eq!(
            Options::parse(
                [
                    "--visual-valley".to_owned(),
                    "--gate1-visual-proof".to_owned(),
                    "first".to_owned(),
                    "--gate1-visual-proof".to_owned(),
                    "second".to_owned(),
                ]
                .into_iter(),
            )
            .unwrap_err(),
            "--gate1-visual-proof may only be specified once"
        );
        assert_eq!(
            Options::parse(
                [
                    "--visual-valley".to_owned(),
                    "--quality".to_owned(),
                    "high".to_owned(),
                    "--gate1-visual-proof".to_owned(),
                    "proof-output".to_owned(),
                ]
                .into_iter(),
            )
            .unwrap_err(),
            "--gate1-visual-proof requires --no-vsync for the governed acquisition protocol"
        );
    }

    #[test]
    #[allow(
        clippy::too_many_lines,
        reason = "one parser contract test enumerates every conflicting automation mode"
    )]
    fn gate1_gameplay_stutter_is_a_strict_high_no_vsync_owned_sequence() {
        let accepted = Options::parse(
            [
                "--visual-valley".to_owned(),
                "--quality".to_owned(),
                "high".to_owned(),
                "--gate1-gameplay-stutter".to_owned(),
                "stutter-output".to_owned(),
                "--no-vsync".to_owned(),
            ]
            .into_iter(),
        )
        .unwrap();
        assert_eq!(
            accepted.gate1_gameplay_stutter_output,
            Some(PathBuf::from("stutter-output"))
        );
        assert!(player_controller_required(
            accepted.player_camera,
            accepted.gate1_gameplay_stutter_output.is_some(),
            false,
        ));
        assert!(!accepted.player_camera.is_enabled());
        assert_eq!(
            accepted.runtime_automation().render_diagnostics_policy,
            RenderDiagnosticsPolicy::DisabledForExternalPresentationQualification
        );
        assert_eq!(
            accepted.gate1_presentation_route,
            Gate1PresentationRoute::AutoNoVsync
        );
        assert_eq!(accepted.requested_present_mode(), PresentMode::AutoNoVsync);

        let ordinary_high = Options::parse(
            [
                "--visual-valley".to_owned(),
                "--quality".to_owned(),
                "high".to_owned(),
                "--no-vsync".to_owned(),
            ]
            .into_iter(),
        )
        .unwrap();
        #[cfg(not(feature = "render-profile"))]
        assert_eq!(
            ordinary_high.runtime_automation().render_diagnostics_policy,
            RenderDiagnosticsPolicy::Enabled
        );
        #[cfg(feature = "render-profile")]
        assert_eq!(
            ordinary_high.runtime_automation().render_diagnostics_policy,
            RenderDiagnosticsPolicy::DisabledForExternalPresentationQualification
        );

        assert_eq!(
            Options::parse(
                [
                    "--gate1-gameplay-stutter".to_owned(),
                    "stutter-output".to_owned(),
                ]
                .into_iter(),
            )
            .unwrap_err(),
            "--gate1-gameplay-stutter requires --visual-valley"
        );
        for arguments in [
            vec![
                "--visual-valley",
                "--gate1-gameplay-stutter",
                "stutter-output",
                "--no-vsync",
            ],
            vec![
                "--visual-valley",
                "--quality",
                "low",
                "--gate1-gameplay-stutter",
                "stutter-output",
                "--no-vsync",
            ],
        ] {
            assert_eq!(
                Options::parse(arguments.into_iter().map(str::to_owned)).unwrap_err(),
                "--gate1-gameplay-stutter requires explicit --quality high"
            );
        }
        assert_eq!(
            Options::parse(
                [
                    "--visual-valley".to_owned(),
                    "--quality".to_owned(),
                    "high".to_owned(),
                    "--gate1-gameplay-stutter".to_owned(),
                    "stutter-output".to_owned(),
                ]
                .into_iter(),
            )
            .unwrap_err(),
            "--gate1-gameplay-stutter requires --no-vsync for the governed acquisition protocol"
        );
        let conflict = Options::parse(
            [
                "--visual-valley".to_owned(),
                "--quality".to_owned(),
                "high".to_owned(),
                "--gate1-gameplay-stutter".to_owned(),
                "stutter-output".to_owned(),
                "--gate1-visual-proof".to_owned(),
                "visual-output".to_owned(),
                "--no-vsync".to_owned(),
            ]
            .into_iter(),
        )
        .unwrap_err();
        assert!(conflict.contains("owns controller, camera"));
        let base = vec![
            "--visual-valley",
            "--quality",
            "high",
            "--gate1-gameplay-stutter",
            "stutter-output",
            "--no-vsync",
        ];
        for extra in [
            vec!["--terrain-debug"],
            vec!["--terrain-partial-update-probe"],
            vec!["--player-camera"],
            vec!["--player-camera-probe"],
            vec!["--weather", "clear"],
            vec!["--capture", "capture.png"],
            vec!["--metrics", "metrics.json"],
            vec!["--exit-after-frames", "120"],
            vec!["--resize-at-frame", "60"],
            vec!["--close-at-frame", "120"],
            vec!["--log-probe"],
        ] {
            let mut arguments = base.clone();
            arguments.extend(extra);
            let error = Options::parse(arguments.into_iter().map(str::to_owned)).unwrap_err();
            assert!(
                error.contains("owns controller, camera"),
                "unexpected conflict error: {error}"
            );
        }
        assert_eq!(
            Options::parse(
                [
                    "--visual-valley".to_owned(),
                    "--quality".to_owned(),
                    "high".to_owned(),
                    "--gate1-gameplay-stutter".to_owned(),
                    "first".to_owned(),
                    "--gate1-gameplay-stutter".to_owned(),
                    "second".to_owned(),
                    "--no-vsync".to_owned(),
                ]
                .into_iter(),
            )
            .unwrap_err(),
            "--gate1-gameplay-stutter may only be specified once"
        );
    }

    #[cfg(not(feature = "render-profile"))]
    #[test]
    fn gate1_fifo_diagnostic_is_explicit_owned_and_never_no_vsync() {
        let accepted = Options::parse(
            [
                "--visual-valley",
                "--quality",
                "high",
                "--gate1-gameplay-stutter",
                "stutter-output",
                "--gate1-fifo-diagnostic",
            ]
            .into_iter()
            .map(str::to_owned),
        )
        .unwrap();
        assert_eq!(
            accepted.gate1_presentation_route,
            Gate1PresentationRoute::FifoDiagnostic
        );
        assert!(accepted.vsync);
        assert_eq!(accepted.requested_present_mode(), PresentMode::Fifo);

        assert_eq!(
            Options::parse(["--gate1-fifo-diagnostic"].into_iter().map(str::to_owned)).unwrap_err(),
            "--gate1-fifo-diagnostic requires --gate1-gameplay-stutter"
        );
        assert_eq!(
            Options::parse(
                [
                    "--visual-valley",
                    "--quality",
                    "high",
                    "--gate1-gameplay-stutter",
                    "stutter-output",
                    "--gate1-fifo-diagnostic",
                    "--no-vsync",
                ]
                .into_iter()
                .map(str::to_owned)
            )
            .unwrap_err(),
            "--gate1-fifo-diagnostic cannot be combined with --no-vsync"
        );
        assert_eq!(
            Options::parse(
                [
                    "--visual-valley",
                    "--quality",
                    "high",
                    "--gate1-gameplay-stutter",
                    "stutter-output",
                    "--gate1-fifo-diagnostic",
                    "--gate1-fifo-diagnostic",
                ]
                .into_iter()
                .map(str::to_owned)
            )
            .unwrap_err(),
            "--gate1-fifo-diagnostic may only be specified once"
        );
    }

    #[cfg(feature = "render-profile")]
    #[test]
    fn gate1_fifo_diagnostic_is_unavailable_in_render_profile_builds() {
        assert_eq!(
            Options::parse(
                [
                    "--visual-valley",
                    "--quality",
                    "high",
                    "--gate1-gameplay-stutter",
                    "stutter-output",
                    "--gate1-fifo-diagnostic",
                ]
                .into_iter()
                .map(str::to_owned)
            )
            .unwrap_err(),
            "--gate1-fifo-diagnostic is unavailable in NONQUALIFYING render-profile builds"
        );
    }

    #[test]
    fn quality_selection_is_explicit_strict_and_visual_valley_only() {
        for (value, tier) in [
            ("low", VisualQualityTier::Low),
            ("medium", VisualQualityTier::Medium),
            ("high", VisualQualityTier::High),
            ("ultra", VisualQualityTier::Ultra),
        ] {
            let options = Options::parse(
                [
                    "--visual-valley".to_owned(),
                    "--quality".to_owned(),
                    value.to_owned(),
                ]
                .into_iter(),
            )
            .unwrap();
            assert_eq!(
                options.quality_selection,
                Some(VisualValleyQualitySelection::Fixed(tier))
            );
        }
        assert_eq!(
            Options::parse(["--adaptive-quality".to_owned()].into_iter()).unwrap_err(),
            "quality selection requires --visual-valley"
        );
        assert!(
            Options::parse(
                [
                    "--visual-valley".to_owned(),
                    "--quality".to_owned(),
                    "cinematic".to_owned(),
                ]
                .into_iter(),
            )
            .unwrap_err()
            .contains("received `cinematic`")
        );
        assert!(
            Options::parse(
                [
                    "--visual-valley".to_owned(),
                    "--adaptive-quality".to_owned(),
                    "--benchmark-quality".to_owned(),
                ]
                .into_iter(),
            )
            .unwrap_err()
            .contains("mutually exclusive")
        );
        let benchmark = Options::parse(
            [
                "--visual-valley".to_owned(),
                "--benchmark-quality".to_owned(),
            ]
            .into_iter(),
        )
        .unwrap();
        assert_eq!(
            benchmark.quality_selection,
            Some(VisualValleyQualitySelection::Benchmark)
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
            "--scene, --terrain and --visual-valley are mutually exclusive"
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
            "--scene, --terrain and --visual-valley are mutually exclusive"
        );
    }

    #[test]
    fn terrain_debug_is_explicit_and_rejected_outside_gate1_modes() {
        let error = Options::parse(["--terrain-debug".to_owned()].into_iter()).unwrap_err();
        assert_eq!(
            error,
            "--terrain-debug requires --visual-valley or --terrain"
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
            "--player-camera requires --visual-valley or --terrain"
        );

        let probe_error =
            Options::parse(["--player-camera-probe".to_owned()].into_iter()).unwrap_err();
        assert_eq!(
            probe_error,
            "--player-camera-probe requires --visual-valley or --terrain"
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
            "--player-camera requires --visual-valley or --terrain"
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
            "--player-camera-probe requires --visual-valley or --terrain"
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
                "180".to_owned(),
                "--exit-after-frames".to_owned(),
                "240".to_owned(),
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
        assert_eq!(options.exit_after_frames, Some(240));
    }

    #[test]
    fn capture_and_metrics_reject_sampling_before_pipeline_stability() {
        let error = Options::parse(
            [
                "--capture".to_owned(),
                "proof.png".to_owned(),
                "--capture-frame".to_owned(),
                "120".to_owned(),
                "--metrics".to_owned(),
                "metrics.json".to_owned(),
                "--metrics-warmup-frames".to_owned(),
                "149".to_owned(),
                "--exit-after-frames".to_owned(),
                "180".to_owned(),
            ]
            .into_iter(),
        )
        .unwrap_err();

        assert_eq!(
            error,
            "--metrics-warmup-frames must be at least 30 frames after --capture-frame (minimum 150)"
        );

        let overflow = Options::parse(
            [
                "--capture".to_owned(),
                "proof.png".to_owned(),
                "--capture-frame".to_owned(),
                u32::MAX.to_string(),
                "--metrics".to_owned(),
                "metrics.json".to_owned(),
                "--metrics-warmup-frames".to_owned(),
                u32::MAX.to_string(),
            ]
            .into_iter(),
        )
        .unwrap_err();

        assert_eq!(
            overflow,
            "--capture-frame is too large to leave 30 stable pipeline frames before metrics warmup"
        );
    }

    #[test]
    fn capture_and_metrics_accept_exact_pipeline_stability_boundary() {
        let options = Options::parse(
            [
                "--capture".to_owned(),
                "proof.png".to_owned(),
                "--capture-frame".to_owned(),
                "120".to_owned(),
                "--metrics".to_owned(),
                "metrics.json".to_owned(),
                "--metrics-warmup-frames".to_owned(),
                "150".to_owned(),
                "--exit-after-frames".to_owned(),
                "180".to_owned(),
            ]
            .into_iter(),
        )
        .unwrap();

        assert_eq!(options.capture_frame, 120);
        assert_eq!(options.metrics_warmup_frames, 150);
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
            "--terrain-partial-update-probe requires --visual-valley or --terrain"
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
            "--terrain-partial-update-probe requires --visual-valley or --terrain"
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
    fn gate1_renderdoc_requires_a_complete_paired_route() {
        assert_eq!(
            Options::parse(
                [
                    "--gate1-renderdoc-capture".to_owned(),
                    "renderdoc-output".to_owned(),
                ]
                .into_iter(),
            )
            .unwrap_err(),
            "--gate1-renderdoc-capture requires --gate1-renderdoc-nonce"
        );
        assert_eq!(
            Options::parse(
                [
                    "--gate1-renderdoc-nonce".to_owned(),
                    "0123456789abcdef0123456789abcdef".to_owned(),
                ]
                .into_iter(),
            )
            .unwrap_err(),
            "--gate1-renderdoc-nonce requires --gate1-renderdoc-capture"
        );
    }

    #[cfg(target_os = "windows")]
    #[test]
    fn gate1_renderdoc_is_a_strict_high_weather_no_vsync_route() {
        let accepted = Options::parse(
            [
                "--visual-valley",
                "--quality",
                "high",
                "--weather",
                "clear",
                "--gate1-renderdoc-capture",
                "renderdoc-output",
                "--gate1-renderdoc-nonce",
                "0123456789abcdef0123456789abcdef",
                "--no-vsync",
            ]
            .into_iter()
            .map(str::to_owned),
        )
        .unwrap();
        assert_eq!(
            accepted.gate1_renderdoc_output,
            Some(PathBuf::from("renderdoc-output"))
        );
        assert_eq!(accepted.weather_state, Some(WeatherState::Clear));
        assert_eq!(accepted.requested_present_mode(), PresentMode::AutoNoVsync);
        assert_eq!(
            accepted.runtime_automation().render_diagnostics_policy,
            RenderDiagnosticsPolicy::DisabledForExternalPresentationQualification
        );

        for arguments in [
            vec![
                "--visual-valley",
                "--quality",
                "high",
                "--gate1-renderdoc-capture",
                "renderdoc-output",
                "--gate1-renderdoc-nonce",
                "0123456789abcdef0123456789abcdef",
                "--no-vsync",
            ],
            vec![
                "--visual-valley",
                "--quality",
                "high",
                "--weather",
                "rain",
                "--gate1-renderdoc-capture",
                "renderdoc-output",
                "--gate1-renderdoc-nonce",
                "0123456789abcdef0123456789abcdef",
            ],
        ] {
            assert!(Options::parse(arguments.into_iter().map(str::to_owned)).is_err());
        }
    }

    #[test]
    fn gate1_renderdoc_preflight_refuses_stale_outputs_without_deleting_them() {
        let unique = std::time::SystemTime::now()
            .duration_since(std::time::UNIX_EPOCH)
            .unwrap()
            .as_nanos();
        let root = env::temp_dir().join(format!(
            "openrpg-forge-gate1-renderdoc-{}-{unique}",
            std::process::id()
        ));
        let options = Options {
            gate1_renderdoc_output: Some(root.clone()),
            ..Options::default()
        };
        options.prepare_output_paths().unwrap();
        assert!(root.is_dir());

        fs::write(root.join("stale.json"), b"stale proof").unwrap();
        let error = options.prepare_output_paths().unwrap_err();
        assert!(error.contains("must be absent or empty"));
        assert!(root.join("stale.json").is_file());
        fs::remove_dir_all(root).unwrap();
    }

    #[test]
    fn gate1_visual_proof_preflight_refuses_stale_outputs_without_deleting_them() {
        let unique = std::time::SystemTime::now()
            .duration_since(std::time::UNIX_EPOCH)
            .unwrap()
            .as_nanos();
        let root = env::temp_dir().join(format!(
            "openrpg-forge-gate1-visual-proof-{}-{unique}",
            std::process::id()
        ));
        let options = Options {
            visual_valley: true,
            gate1_visual_proof_output: Some(root.clone()),
            ..Options::default()
        };
        options.prepare_output_paths().unwrap();
        assert!(root.join("captures").is_dir());
        assert!(root.join("movement").is_dir());

        let error = options.prepare_output_paths().unwrap_err();
        assert!(error.contains("must be absent or empty"));
        assert!(root.join("captures").is_dir());
        assert!(root.join("movement").is_dir());
        fs::remove_dir_all(root).unwrap();
    }

    #[test]
    fn gate1_gameplay_stutter_preflight_refuses_stale_outputs_without_deleting_them() {
        let unique = std::time::SystemTime::now()
            .duration_since(std::time::UNIX_EPOCH)
            .unwrap()
            .as_nanos();
        let root = env::temp_dir().join(format!(
            "openrpg-forge-gate1-gameplay-stutter-{}-{unique}",
            std::process::id()
        ));
        let options = Options {
            visual_valley: true,
            gate1_gameplay_stutter_output: Some(root.clone()),
            ..Options::default()
        };
        options.prepare_output_paths().unwrap();
        assert!(root.is_dir());

        fs::write(root.join("stale.json"), b"stale proof").unwrap();
        let error = options.prepare_output_paths().unwrap_err();
        assert!(error.contains("must be absent or empty"));
        assert!(root.join("stale.json").is_file());
        fs::remove_dir_all(root).unwrap();
    }

    #[cfg(not(feature = "render-profile"))]
    #[test]
    fn gate1_fifo_diagnostic_writes_only_its_atomic_nonqualifying_marker() {
        let unique = std::time::SystemTime::now()
            .duration_since(std::time::UNIX_EPOCH)
            .unwrap()
            .as_nanos();
        let root = env::temp_dir().join(format!(
            "openrpg-forge-gate1-fifo-marker-{}-{unique}",
            std::process::id()
        ));
        let options = Options::parse(
            [
                "--visual-valley".to_owned(),
                "--quality".to_owned(),
                "high".to_owned(),
                "--gate1-gameplay-stutter".to_owned(),
                root.display().to_string(),
                "--gate1-fifo-diagnostic".to_owned(),
            ]
            .into_iter(),
        )
        .unwrap();

        options.prepare_output_paths().unwrap();
        options
            .write_nonqualifying_gate1_fifo_diagnostic_marker()
            .unwrap();

        let marker_path = root.join(NONQUALIFYING_GATE1_FIFO_DIAGNOSTIC_MARKER);
        let marker: serde_json::Value =
            serde_json::from_slice(&fs::read(&marker_path).unwrap()).unwrap();
        assert_eq!(
            marker,
            serde_json::json!({
                "schema": "forge.nonqualifying-gate1-fifo-diagnostic",
                "schema_version": 1,
                "qualifying": false,
                "source_commit": option_env!("FORGE_SOURCE_COMMIT"),
                "presentation_route": "fifo_diagnostic",
                "requested_present_mode": "fifo",
                "vsync_requested": true,
                "observation_scope": "bevy_window_request_not_resolved_surface",
                "reason": "the explicit FIFO presentation route changes the qualifying AutoNoVsync request and is diagnostic-only; this run cannot qualify Gate 1",
            })
        );
        assert_eq!(fs::read_dir(&root).unwrap().count(), 1);
        assert!(
            !root
                .join(format!(".{NONQUALIFYING_GATE1_FIFO_DIAGNOSTIC_MARKER}.tmp"))
                .exists()
        );

        let original = fs::read(&marker_path).unwrap();
        let error = options
            .write_nonqualifying_gate1_fifo_diagnostic_marker()
            .unwrap_err();
        assert!(error.contains("refusing to replace existing Gate 1 FIFO diagnostic marker"));
        assert_eq!(fs::read(&marker_path).unwrap(), original);
        fs::remove_dir_all(root).unwrap();
    }

    #[cfg(feature = "render-profile")]
    #[test]
    fn render_profile_gate1_diagnostic_writes_only_the_exact_nonqualifying_marker() {
        let unique = std::time::SystemTime::now()
            .duration_since(std::time::UNIX_EPOCH)
            .unwrap()
            .as_nanos();
        let root = env::temp_dir().join(format!(
            "openrpg-forge-render-profile-marker-{}-{unique}",
            std::process::id()
        ));
        let options = Options::parse(
            [
                "--visual-valley".to_owned(),
                "--quality".to_owned(),
                "high".to_owned(),
                "--gate1-gameplay-stutter".to_owned(),
                root.display().to_string(),
                "--no-vsync".to_owned(),
                "--render-profile-gate1-diagnostic".to_owned(),
            ]
            .into_iter(),
        )
        .unwrap();

        options.prepare_output_paths().unwrap();
        options.write_nonqualifying_render_profile_marker().unwrap();

        let marker_path = root.join(NONQUALIFYING_RENDER_PROFILE_MARKER);
        let marker: serde_json::Value =
            serde_json::from_slice(&fs::read(&marker_path).unwrap()).unwrap();
        assert_eq!(
            marker,
            serde_json::json!({
                "schema": "forge.nonqualifying-render-profile",
                "schema_version": 1,
                "qualifying": false,
                "source_commit": option_env!("FORGE_SOURCE_COMMIT"),
                "feature": "render-profile",
                "reason": "render-profile instrumentation changes runtime logging and render scheduling; this diagnostic run cannot qualify Gate 1",
            })
        );
        assert_eq!(fs::read_dir(&root).unwrap().count(), 1);
        assert!(
            !root
                .join(format!(".{NONQUALIFYING_RENDER_PROFILE_MARKER}.tmp"))
                .exists()
        );

        let original = fs::read(&marker_path).unwrap();
        let error = options
            .write_nonqualifying_render_profile_marker()
            .unwrap_err();
        assert!(error.contains("refusing to replace existing render-profile marker"));
        assert_eq!(fs::read(&marker_path).unwrap(), original);
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
}
