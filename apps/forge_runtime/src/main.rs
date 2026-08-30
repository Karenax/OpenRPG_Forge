#![deny(unsafe_code)]

use bevy::{
    prelude::*,
    render::{
        RenderPlugin,
        settings::{RenderCreation, WgpuSettings},
    },
    window::{PresentMode, WindowResolution},
};
use forge_engine::{ProofScenePlugin, RuntimeAutomation};
use forge_project::SceneProofDocument;
use forge_world::embedded_gate0_proof_document;
use std::{env, fs, path::PathBuf};

fn main() {
    let options = Options::parse(env::args().skip(1)).unwrap_or_else(|message| {
        eprintln!("forge_runtime: {message}");
        std::process::exit(2);
    });
    let document = options.load_document().unwrap_or_else(|message| {
        eprintln!("forge_runtime: {message}");
        std::process::exit(2);
    });

    let present_mode = if options.vsync {
        PresentMode::AutoVsync
    } else {
        PresentMode::AutoNoVsync
    };
    let render_plugin = RenderPlugin {
        render_creation: RenderCreation::Automatic(Box::new(WgpuSettings {
            power_preference: bevy::render::settings::PowerPreference::HighPerformance,
            ..default()
        })),
        ..default()
    };
    App::new()
        .insert_resource(ClearColor(Color::srgb(0.015, 0.025, 0.055)))
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
                .set(render_plugin),
        )
        .add_plugins(
            ProofScenePlugin::new(document).with_automation(RuntimeAutomation {
                capture_path: options.capture_path,
                capture_frame: options.capture_frame,
                metrics_path: options.metrics_path,
                metrics_warmup_frames: options.metrics_warmup_frames,
                exit_after_frames: options.exit_after_frames,
            }),
        )
        .run();
}

#[derive(Debug)]
struct Options {
    scene_path: Option<PathBuf>,
    capture_path: Option<PathBuf>,
    capture_frame: u32,
    metrics_path: Option<PathBuf>,
    metrics_warmup_frames: u32,
    exit_after_frames: Option<u32>,
    vsync: bool,
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
            vsync: true,
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
                "--no-vsync" => options.vsync = false,
                "--help" | "-h" => {
                    return Err("usage: forge_runtime [--scene file.json] [--capture file.png] [--capture-frame n] [--metrics file.json] [--metrics-warmup-frames n] [--exit-after-frames n] [--no-vsync]".to_owned());
                }
                _ => return Err(format!("unknown option `{argument}`")),
            }
        }
        if options.capture_path.is_some()
            && options
                .exit_after_frames
                .is_some_and(|frame| frame <= options.capture_frame + 5)
        {
            return Err(
                "--exit-after-frames must be at least six frames after --capture-frame".to_owned(),
            );
        }
        Ok(options)
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
}
