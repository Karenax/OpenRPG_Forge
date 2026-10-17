//! Native observations of the shared gameplay/asset/physics adapters.
use super::{
    evidence_contract::{
        ExpectedManual, ExpectedProjection, ProjectionObservation, RendererObservation, verify,
    },
    input::ModuleGameOptions,
    manual::PreparedManual,
};
use bevy::{
    app::AppExit,
    prelude::*,
    render::{ExtractSchedule, RenderApp},
};
use forge_delivery::module_runtime::LoadedModuleZone;
use forge_project::zone_authoring::placements::ManualPlacement;
use std::{
    fs::{self, File, OpenOptions},
    io::Write,
    path::PathBuf,
    sync::{Arc, Mutex},
};

mod collect;
mod renderer;

#[derive(Clone, Default)]
struct Shared {
    renderer: RendererObservation,
    final_snapshot: Option<ProjectionObservation>,
    capture_snapshot: Option<ProjectionObservation>,
    functional_identity: Option<(forge_core::ContentHash, forge_core::ContentHash)>,
}

#[derive(Resource, Clone, Default)]
struct Bridge(Arc<Mutex<Shared>>);

#[derive(Resource, Clone)]
struct Context {
    expected: Arc<ExpectedProjection>,
    manual: Arc<Vec<ManualPlacement>>,
    bridge: Bridge,
    capture_frame: Option<u32>,
    last_frame: Option<u32>,
}

pub struct RuntimeEvidence {
    context: Context,
    file: File,
    path: PathBuf,
    automation: bool,
}

pub fn prepare(
    options: &ModuleGameOptions,
    loaded: &LoadedModuleZone,
    manual: &PreparedManual,
    capture_frame: u32,
) -> Result<Option<RuntimeEvidence>, String> {
    let Some(document) = loaded.zone.head().corrections.gameplay.as_deref() else {
        return Ok(None);
    };
    let manual = manual.accepted();
    let expected = ExpectedProjection {
        module_id: document.module_id,
        area_id: document.area_id,
        source_hash: loaded.zone.head().content_hash()?,
        functional_hash: document.functional_hash().map_err(|e| e.to_string())?,
        visuals: loaded
            .zone
            .head()
            .corrections
            .gameplay_visuals
            .clone()
            .unwrap_or_default(),
        manual: manual
            .iter()
            .map(|placement| ExpectedManual {
                entity_id: placement.id,
                asset: forge_project::module_gameplay_visuals::VisualAssetPin {
                    asset_id: placement.asset_id,
                    sha256: placement.asset_hash,
                },
                transform: placement.transform,
            })
            .collect(),
    };
    let directory = if let Some(capture) = &options.capture {
        capture
            .parent()
            .ok_or("Module capture parent missing")?
            .to_path_buf()
    } else if let Some(report) = std::env::var_os("FORGE_GAME_REPORT_PATH") {
        let report = PathBuf::from(report);
        if !report.is_absolute() {
            return Err("Native game session report path must be absolute".into());
        }
        report
            .parent()
            .ok_or("Native game report parent missing")?
            .to_path_buf()
    } else {
        options
            .save_root
            .join("module-sessions")
            .join(forge_delivery::attempt()?)
    };
    let path = directory.join("module-runtime-report.json");
    super::output::prepare(&options.save_root, Some(&path), &loaded.package_root)?;
    fs::create_dir_all(&directory).map_err(|e| e.to_string())?;
    forge_delivery::manifest::safe_chain(&path)?;
    let file = OpenOptions::new()
        .write(true)
        .create_new(true)
        .open(&path)
        .map_err(|e| e.to_string())?;
    Ok(Some(RuntimeEvidence {
        context: Context {
            expected: Arc::new(expected),
            manual: Arc::new(manual),
            bridge: Bridge::default(),
            capture_frame: options.capture.as_ref().map(|_| capture_frame),
            last_frame: options.exit_after_frames,
        },
        file,
        path,
        automation: options.exit_after_frames.is_some(),
    }))
}

impl RuntimeEvidence {
    pub fn install(&self, app: &mut App) {
        app.insert_resource(self.context.clone()).add_systems(
            Last,
            collect::observe.before(bevy::diagnostic::update_frame_count),
        );
        if let Some(render) = app.get_sub_app_mut(RenderApp) {
            render
                .insert_resource(self.context.bridge.clone())
                .add_systems(ExtractSchedule, renderer::observe);
        }
    }

    pub fn finish(mut self, exit: &AppExit) -> Result<(), String> {
        let shared = self
            .context
            .bridge
            .0
            .lock()
            .map_err(|_| "Module evidence synchronization failed")?
            .clone();
        let final_result = shared
            .final_snapshot
            .as_ref()
            .ok_or_else(|| "Native module produced no projection observation".to_owned())
            .and_then(|observed| verify(&self.context.expected, observed));
        let capture_result = if self.context.capture_frame.is_some() {
            shared
                .capture_snapshot
                .as_ref()
                .ok_or_else(|| {
                    "The requested capture frame has no native projection observation".to_owned()
                })
                .and_then(|observed| verify(&self.context.expected, observed))
        } else {
            Ok(())
        };
        let fatal = shared.final_snapshot.as_ref().is_some_and(|observed| {
            observed.simulation_error.is_some()
                || observed.visual_error.is_some()
                || observed.renderer.failed > 0
        });
        let ready = final_result.is_ok() && capture_result.is_ok();
        let runtime_exit_success = exit.is_success() && !fatal && (!self.automation || ready);
        let report = serde_json::json!({"schema":"forge.module-rpg-runtime-report","version":1,"pid":std::process::id(),
            "scope":"native canonical RPG/model/static-collision projection and renderer pipeline observations",
            "bevy_exit_success":exit.is_success(),"runtime_exit_success":runtime_exit_success,
            "projection_ready":ready,"expected":&*self.context.expected,"final_observation":shared.final_snapshot,
            "capture_earliest_frame":self.context.capture_frame,
            "capture_issued_frame":shared.capture_snapshot.as_ref().map(|snapshot| snapshot.frame),
            "capture_observation":shared.capture_snapshot,
            "final_error":final_result.err(),"capture_error":capture_result.err(),
            "human_visual_acceptance":false,"pixel_fidelity_qualified":false,"gate_qualified":false});
        let bytes = serde_json::to_vec_pretty(&report).map_err(|e| e.to_string())?;
        if bytes.len() > 8 * 1024 * 1024 {
            return Err("Native module evidence exceeds 8 MiB".into());
        }
        self.file
            .write_all(&bytes)
            .and_then(|()| self.file.sync_all())
            .map_err(|e| e.to_string())?;
        tracing::info!(report=%self.path.display(),projection_ready=ready,runtime_exit_success,"Native module projection report written");
        if !runtime_exit_success {
            return Err(format!(
                "Native module exit/projection validation failed; report {}",
                self.path.display()
            ));
        }
        Ok(())
    }
}
