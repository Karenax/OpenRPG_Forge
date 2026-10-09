//! Opt-in comparison clock; carries measured dt/night, never captured exposure.
use bevy::{prelude::*, render::RenderApp};
use serde::Deserialize;
use std::{fs::File, io::Read, path::Path};

#[derive(Deserialize)]
#[serde(deny_unknown_fields)]
struct Document {
    schema: String,
    version: u32,
    source_trace_sha256: String,
    steps: Vec<Step>,
}

#[derive(Deserialize)]
#[serde(deny_unknown_fields)]
struct Step {
    ordinal: usize,
    dt: f32,
    night: f32,
}

#[derive(Resource)]
pub(super) struct ReferenceClock {
    steps: Vec<[f32; 2]>,
}

impl ReferenceClock {
    fn load(path: &Path, expected: &str) -> Result<Self, String> {
        let mut bytes = Vec::new();
        File::open(path)
            .map_err(|error| error.to_string())?
            .take(256 * 1024 + 1)
            .read_to_end(&mut bytes)
            .map_err(|error| error.to_string())?;
        if bytes.len() > 256 * 1024 || forge_core::bytes_sha256(&bytes).to_string() != expected {
            return Err("Reference final clock size or SHA-256 mismatch".into());
        }
        let document: Document =
            serde_json::from_slice(&bytes).map_err(|error| error.to_string())?;
        if document.schema != "forge.tidewater.final-comparison-clock"
            || document.version != 1
            || document.source_trace_sha256.len() != 64
            || !document
                .source_trace_sha256
                .bytes()
                .all(|byte| byte.is_ascii_hexdigit())
            || document.steps.is_empty()
            || document.steps.len() > 2048
        {
            return Err("Unsupported reference final clock document".into());
        }
        for (index, step) in document.steps.iter().enumerate() {
            if step.ordinal != index + 1
                || !step.dt.is_finite()
                || step.dt <= 0.0
                || step.dt > 0.25
                || !step.night.is_finite()
                || !(0.0..=1.0).contains(&step.night)
            {
                return Err("Invalid measured final clock step".into());
            }
        }
        info!(steps=document.steps.len(), source_trace=%document.source_trace_sha256,
            "Source final comparison clock loaded; only dt/night replayed, tail repeated after acquired steps");
        Ok(Self {
            steps: document
                .steps
                .into_iter()
                .map(|step| [step.dt, step.night])
                .collect(),
        })
    }

    pub(super) fn step(&self, submitted: u32) -> [f32; 2] {
        let index = usize::try_from(submitted.saturating_sub(1)).unwrap_or(usize::MAX);
        self.steps[index.min(self.steps.len() - 1)]
    }
}

pub(super) fn install(app: &mut App) -> Result<(), String> {
    let path = std::env::var_os("FORGE_TIDEWATER_SOURCE_FINAL_CLOCK");
    let hash = std::env::var("FORGE_TIDEWATER_SOURCE_FINAL_CLOCK_SHA256").ok();
    match (path, hash) {
        (None, None) => Ok(()),
        (Some(path), Some(hash)) => {
            let clock = ReferenceClock::load(Path::new(&path), &hash)?;
            app.get_sub_app_mut(RenderApp)
                .ok_or("Reference clock requires RenderApp")?
                .insert_resource(clock);
            Ok(())
        }
        _ => Err("Reference final clock requires both path and SHA-256".into()),
    }
}
