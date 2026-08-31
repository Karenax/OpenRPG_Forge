#![deny(unsafe_code)]

mod player_camera_probe;
mod terrain_partial_update_probe;

use bevy::{
    app::AppExit,
    log::{DEFAULT_FILTER, LogPlugin},
    prelude::*,
    render::{
        RenderPlugin,
        settings::{RenderCreation, WgpuSettings},
    },
    window::{CursorGrabMode, CursorOptions, PresentMode, WindowResolution},
};
use forge_engine::{
    ProofScenePlugin, RuntimeAutomation, VisualValleyEnvironmentPlugin, VisualValleyPlugin,
    VisualValleyVegetationPlugin, WindowResizeRequest,
};
use forge_project::{
    EnvironmentProfileDocument, SceneProofDocument, TerrainDocument, VegetationDocument,
};
use forge_world::{
    TerrainGenerationPlan, VegetationGenerationPlan, embedded_gate0_proof_document,
    embedded_visual_valley_environment_profile, embedded_visual_valley_player_controller_profile,
    embedded_visual_valley_terrain_document, embedded_visual_valley_vegetation_document,
};
use player_camera_probe::PlayerCameraProbePlugin;
use std::{env, fs, path::PathBuf};
use terrain_partial_update_probe::TerrainPartialUpdateProbePlugin;
use tracing_subscriber::EnvFilter;

fn main() -> AppExit {
    if let Err(message) = initialize_structured_tracing() {
        eprintln!("forge_runtime: cannot initialize structured tracing: {message}");
        return AppExit::from_code(2);
    }

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
    let document = match options.load_document() {
        Ok(document) => document,
        Err(message) => return startup_error("scene_load", &message),
    };

    let present_mode = present_mode_for_vsync(options.vsync);
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
        player_input_enabled,
        player_cursor_locked = player_input_enabled,
        "native runtime starting"
    );

    let mut app = App::new();
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
                .set(render_plugin)
                .disable::<LogPlugin>(),
        );

    add_runtime_plugin(
        &mut app,
        document,
        automation,
        options.terrain_debug,
        options.terrain_partial_update_probe,
        options.player_camera,
        player_input_enabled,
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

fn add_runtime_plugin(
    app: &mut App,
    document: RuntimeDocument,
    automation: RuntimeAutomation,
    terrain_debug: TerrainDebugMode,
    terrain_partial_update_probe: TerrainPartialUpdateProbeMode,
    player_camera: PlayerCameraRuntimeMode,
    player_input_enabled: bool,
) {
    match document {
        RuntimeDocument::Gate0(document) => {
            app.add_plugins(ProofScenePlugin::new(document).with_automation(automation));
        }
        RuntimeDocument::VisualValley {
            terrain: document,
            vegetation,
            environment,
        } => {
            let player_profile = player_camera.is_enabled().then(|| {
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

            let vegetation_plugin = vegetation
                .map(|vegetation| VisualValleyVegetationPlugin::new(document.clone(), *vegetation));
            let mut visual_valley = VisualValleyPlugin::new(document)
                .with_automation(automation)
                .with_chunk_lod_gizmos(terrain_debug.is_enabled());
            if let Some(profile) = player_profile {
                visual_valley = visual_valley
                    .with_player_controller(profile)
                    .with_player_interactive_input(player_input_enabled)
                    .with_overview_camera(false);
            }
            if let Some(probe) = &player_probe {
                visual_valley = visual_valley.with_player_spawn_position(probe.spawn_position());
            }

            app.add_plugins(visual_valley);
            if let Some(environment) = environment {
                app.add_plugins(VisualValleyEnvironmentPlugin::new(*environment));
            }
            if let Some(vegetation_plugin) = vegetation_plugin {
                app.add_plugins(vegetation_plugin);
            }
            if terrain_partial_update_probe.is_enabled() {
                app.add_plugins(TerrainPartialUpdateProbePlugin::new());
            }
            if let Some(probe) = player_probe {
                app.add_plugins(probe);
            }
        }
    }
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
    Ok(RuntimeDocument::VisualValley {
        terrain: document,
        vegetation: vegetation.map(Box::new),
        environment: environment.map(Box::new),
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

#[derive(Debug)]
struct Options {
    scene_path: Option<PathBuf>,
    terrain_path: Option<PathBuf>,
    visual_valley: bool,
    terrain_debug: TerrainDebugMode,
    terrain_partial_update_probe: TerrainPartialUpdateProbeMode,
    player_camera: PlayerCameraRuntimeMode,
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
                    return Err("usage: forge_runtime [--scene file.json | --visual-valley | --terrain terrain.json] [--terrain-debug] [--terrain-partial-update-probe] [--player-camera | --player-camera-probe] [--capture file.png] [--capture-frame n] [--metrics file.json] [--metrics-warmup-frames n] [--exit-after-frames n] [--resize-at-frame n --resize-width px --resize-height px] [--close-at-frame n] [--no-vsync] [--log-probe]".to_owned());
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
        }
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
            return validated_visual_valley_document(document, None, None, &source);
        }
        if self.visual_valley {
            let document = embedded_visual_valley_terrain_document()
                .map_err(|error| format!("invalid embedded Visual Valley terrain: {error}"))?;
            let vegetation = embedded_visual_valley_vegetation_document()
                .map_err(|error| format!("invalid embedded Visual Valley vegetation: {error}"))?;
            let environment = embedded_visual_valley_environment_profile()
                .map_err(|error| format!("invalid embedded Visual Valley environment: {error}"))?;
            return validated_visual_valley_document(
                document,
                Some(vegetation),
                Some(environment),
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
        Ok(())
    }
}

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

fn next_u32(arguments: &mut impl Iterator<Item = String>, option: &str) -> Result<u32, String> {
    arguments
        .next()
        .ok_or_else(|| format!("{option} requires an integer"))?
        .parse()
        .map_err(|_| format!("{option} requires an unsigned integer"))
}

#[cfg(test)]
mod tests {
    use super::*;

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
        } = loaded
        else {
            panic!("--visual-valley must select the embedded terrain/vegetation/environment set");
        };
        let embedded = embedded_visual_valley_terrain_document().unwrap();
        let embedded_vegetation = embedded_visual_valley_vegetation_document().unwrap();
        let embedded_environment = embedded_visual_valley_environment_profile().unwrap();
        assert_eq!(loaded, embedded);
        assert_eq!(*loaded_vegetation, embedded_vegetation);
        assert_eq!(*loaded_environment, embedded_environment);
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
            "mismatched environment",
        )
        .unwrap_err();
        assert!(error.contains("environment"));
        assert!(error.contains("area identity mismatch"), "{error}");
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
