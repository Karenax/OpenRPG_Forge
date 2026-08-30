#![deny(unsafe_code)]

use bevy::{
    app::AppExit,
    log::{DEFAULT_FILTER, LogPlugin},
    prelude::*,
    render::{
        RenderPlugin,
        settings::{RenderCreation, WgpuSettings},
    },
    window::{PresentMode, WindowResolution},
};
use forge_engine::{ProofScenePlugin, RuntimeAutomation, WindowResizeRequest};
use forge_project::SceneProofDocument;
use forge_world::embedded_gate0_proof_document;
use std::{env, fs, path::PathBuf};
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
        return AppExit::Success;
    }

    if let Err(message) = options.prepare_output_paths() {
        return startup_error("output_preflight", &message);
    }
    let document = match options.load_document() {
        Ok(document) => document,
        Err(message) => return startup_error("scene_load", &message),
    };

    let present_mode = present_mode_for_vsync(options.vsync);
    let render_plugin = RenderPlugin {
        render_creation: RenderCreation::Automatic(Box::new(WgpuSettings {
            power_preference: bevy::render::settings::PowerPreference::HighPerformance,
            ..default()
        })),
        ..default()
    };
    let automation = options.runtime_automation();
    tracing::info!(
        target: "forge::runtime",
        schema = "forge.runtime-event",
        schema_version = 1_u64,
        event_kind = "runtime_started",
        vsync = options.vsync,
        present_mode = present_mode_label(present_mode),
        resizable = true,
        bounded_exit_configured = automation.exit_after_frames.is_some(),
        bounded_exit_frame = u64::from(automation.exit_after_frames.unwrap_or_default()),
        automated_close_configured = automation.close_at_frame.is_some(),
        automated_close_frame = u64::from(automation.close_at_frame.unwrap_or_default()),
        "native runtime starting"
    );

    let mut app = App::new();
    app.insert_resource(ClearColor(Color::srgb(0.015, 0.025, 0.055)))
        .add_plugins(
            DefaultPlugins
                .set(WindowPlugin {
                    primary_window: Some(Window {
                        title: "OpenRPG Forge · Rust Gate 0".to_owned(),
                        resolution: WindowResolution::new(1920, 1080)
                            .with_scale_factor_override(1.0),
                        present_mode,
                        resizable: true,
                        ..default()
                    }),
                    ..default()
                })
                .set(render_plugin)
                .disable::<LogPlugin>(),
        )
        .add_plugins(ProofScenePlugin::new(document).with_automation(automation));

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

#[derive(Debug)]
struct Options {
    scene_path: Option<PathBuf>,
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
                    return Err("usage: forge_runtime [--scene file.json] [--capture file.png] [--capture-frame n] [--metrics file.json] [--metrics-warmup-frames n] [--exit-after-frames n] [--resize-at-frame n --resize-width px --resize-height px] [--close-at-frame n] [--no-vsync] [--log-probe]".to_owned());
                }
                _ => return Err(format!("unknown option `{argument}`")),
            }
        }
        options.validate()?;
        Ok(options)
    }

    fn validate(&self) -> Result<(), String> {
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
                || self.resize_at_frame.is_some())
        {
            return Err(
                "--close-at-frame is a standalone lifecycle probe and cannot be combined with capture, metrics, bounded exit or resize"
                    .to_owned(),
            );
        }
        if self.log_probe
            && (self.scene_path.is_some()
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

    fn load_document(&self) -> Result<SceneProofDocument, String> {
        if let Some(path) = &self.scene_path {
            let json = fs::read_to_string(path)
                .map_err(|error| format!("cannot read {}: {error}", path.display()))?;
            return SceneProofDocument::from_json(&json)
                .map_err(|error| format!("invalid scene {}: {error}", path.display()));
        }
        embedded_gate0_proof_document().map_err(|error| error.to_string())
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
        assert_eq!(
            first.content_hash().unwrap(),
            second.content_hash().unwrap()
        );
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
    }
}
