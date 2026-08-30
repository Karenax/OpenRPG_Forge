#![deny(unsafe_code)]

//! Bevy rendering adapter for shared Forge documents.

use bevy::{
    diagnostic::{DiagnosticsStore, EntityCountDiagnosticsPlugin, FrameTimeDiagnosticsPlugin},
    prelude::*,
    render::{
        diagnostic::RenderDiagnosticsPlugin,
        renderer::{RenderAdapterInfo, RenderDevice},
        view::screenshot::{Screenshot, save_to_disk},
    },
};
use forge_core::EntityId;
use forge_project::{SceneProofDocument, SceneProofKind};
use serde::Serialize;
use std::{
    fs,
    path::{Path, PathBuf},
};

/// Persistent identity carried by a transient Bevy entity.
#[derive(Clone, Copy, Component, Debug, Eq, PartialEq)]
pub struct DomainEntityId(pub EntityId);

/// Current selection produced by the renderer picking adapter.
#[derive(Clone, Copy, Debug, Default, Eq, PartialEq, Resource)]
pub struct ProofSelection(pub Option<EntityId>);

/// Shared source document. Bevy entities are always derived from this resource.
#[derive(Clone, Debug, Resource)]
pub struct ProofScene(pub SceneProofDocument);

#[derive(Clone, Debug, Default, Resource)]
pub struct RuntimeAutomation {
    pub capture_path: Option<PathBuf>,
    pub capture_frame: u32,
    pub metrics_path: Option<PathBuf>,
    pub metrics_warmup_frames: u32,
    pub exit_after_frames: Option<u32>,
}

#[derive(Clone, Debug, Default, Resource, Serialize)]
pub struct ProofSceneStats {
    pub source_entities: usize,
    pub mesh_draws_logical: usize,
    pub source_hash: String,
}

#[derive(Clone, Debug, Resource, Serialize)]
pub struct GpuDiagnostic {
    pub name: String,
    pub vendor: u32,
    pub device: u32,
    pub device_type: String,
    pub driver: String,
    pub driver_info: String,
    pub backend: String,
    pub max_texture_dimension_2d: u32,
    pub max_bind_groups: u32,
    pub max_uniform_buffer_binding_size: u64,
    pub max_storage_buffer_binding_size: u64,
}

#[derive(Component)]
struct DiagnosticsOverlay;

#[derive(Resource, Default)]
struct AdapterLogged(bool);

#[derive(Resource, Default)]
struct FrameLedger(Vec<f64>);

#[derive(Debug, Serialize)]
struct RuntimeMetricsReport {
    schema: &'static str,
    schema_version: u32,
    source_hash: String,
    gpu: Option<GpuDiagnostic>,
    frame: FrameMetrics,
    render_diagnostics: Vec<NamedDiagnostic>,
    render_work: ProofSceneStats,
}

#[derive(Debug, Serialize)]
struct FrameMetrics {
    samples: usize,
    duration_ms: f64,
    mean_ms: f64,
    p50_ms: f64,
    p95_ms: f64,
    p99_ms: f64,
    max_ms: f64,
    mean_fps: f64,
    one_percent_low_fps: f64,
    hitches_over_16_7_ms: usize,
    hitches_over_33_3_ms: usize,
}

#[derive(Debug, Serialize)]
struct NamedDiagnostic {
    path: String,
    value: f64,
    suffix: String,
}

/// Installs the document adapter, diagnostics, deterministic capture and bounded-exit harness.
pub struct ProofScenePlugin {
    document: SceneProofDocument,
    automation: RuntimeAutomation,
}

impl ProofScenePlugin {
    #[must_use]
    pub fn new(document: SceneProofDocument) -> Self {
        Self {
            document,
            automation: RuntimeAutomation {
                capture_frame: 120,
                metrics_warmup_frames: 60,
                ..default()
            },
        }
    }

    #[must_use]
    pub fn with_automation(mut self, automation: RuntimeAutomation) -> Self {
        self.automation = automation;
        self
    }
}

impl Plugin for ProofScenePlugin {
    fn build(&self, app: &mut App) {
        self.document
            .validate()
            .expect("ProofScenePlugin requires a validated document");
        app.insert_resource(ProofScene(self.document.clone()))
            .insert_resource(self.automation.clone())
            .init_resource::<ProofSceneStats>()
            .init_resource::<ProofSelection>()
            .init_resource::<AdapterLogged>()
            .init_resource::<FrameLedger>()
            .add_plugins((
                FrameTimeDiagnosticsPlugin::default(),
                EntityCountDiagnosticsPlugin::default(),
                RenderDiagnosticsPlugin,
                MeshPickingPlugin,
            ))
            .add_systems(Startup, spawn_proof_scene)
            .add_systems(
                Update,
                (
                    update_overlay,
                    record_gpu_diagnostic,
                    capture_at_requested_frame,
                ),
            )
            .add_systems(Last, (collect_frame_sample, write_metrics_and_exit).chain());
    }
}

/// Lossless coordinate conversion: metres, Y-up and right-handed on both sides.
#[must_use]
pub fn domain_transform_to_bevy(value: forge_core::Transform) -> Transform {
    Transform {
        translation: Vec3::new(
            value.translation_m.x,
            value.translation_m.y,
            value.translation_m.z,
        ),
        rotation: Quat::from_xyzw(
            value.rotation.x,
            value.rotation.y,
            value.rotation.z,
            value.rotation.w,
        ),
        scale: Vec3::new(value.scale.x, value.scale.y, value.scale.z),
    }
}

#[allow(
    clippy::needless_pass_by_value,
    reason = "Bevy systems require owned system parameter wrappers"
)]
fn spawn_proof_scene(
    mut commands: Commands,
    document: Res<ProofScene>,
    mut meshes: ResMut<Assets<Mesh>>,
    mut materials: ResMut<Assets<StandardMaterial>>,
    mut stats: ResMut<ProofSceneStats>,
) {
    stats.source_entities = document.0.entities.len();
    stats.source_hash = document
        .0
        .content_hash()
        .expect("validated scene hash")
        .to_string();

    for source in &document.0.entities {
        let persistent = DomainEntityId(source.id);
        let transform = domain_transform_to_bevy(source.transform);
        match source.kind {
            SceneProofKind::Ground { size_x_m, size_z_m } => {
                let mesh = meshes.add(Plane3d::default().mesh().size(size_x_m, size_z_m));
                let material = materials.add(StandardMaterial {
                    base_color: Color::srgb(0.08, 0.28, 0.12),
                    perceptual_roughness: 0.86,
                    ..default()
                });
                spawn_pickable_mesh(
                    &mut commands,
                    source.name.clone(),
                    persistent,
                    mesh,
                    material,
                    transform,
                );
                stats.mesh_draws_logical += 1;
            }
            SceneProofKind::Cube { edge_m } => {
                let mesh = meshes.add(Cuboid::new(edge_m, edge_m, edge_m));
                let material = materials.add(StandardMaterial {
                    base_color: Color::srgb(0.08, 0.36, 0.92),
                    metallic: 0.15,
                    perceptual_roughness: 0.32,
                    ..default()
                });
                spawn_pickable_mesh(
                    &mut commands,
                    source.name.clone(),
                    persistent,
                    mesh,
                    material,
                    transform,
                );
                stats.mesh_draws_logical += 1;
            }
            SceneProofKind::DirectionalLight { illuminance_lux } => {
                commands.spawn((
                    Name::new(source.name.clone()),
                    persistent,
                    DirectionalLight {
                        illuminance: illuminance_lux,
                        shadow_maps_enabled: true,
                        ..default()
                    },
                    transform,
                ));
            }
            SceneProofKind::Camera {
                vertical_fov_degrees,
                near_m,
                far_m,
            } => {
                commands.spawn((
                    Name::new(source.name.clone()),
                    persistent,
                    Camera3d::default(),
                    Projection::Perspective(PerspectiveProjection {
                        fov: vertical_fov_degrees.to_radians(),
                        near: near_m,
                        far: far_m,
                        ..default()
                    }),
                    transform,
                ));
            }
        }
    }

    spawn_diagnostics_overlay(&mut commands);

    info!(
        target: "forge::scene",
        scene = %document.0.name,
        source_hash = %stats.source_hash,
        entities = stats.source_entities,
        mesh_draws_logical = stats.mesh_draws_logical,
        "scene document materialized"
    );
}

fn spawn_diagnostics_overlay(commands: &mut Commands) {
    commands.spawn((
        DiagnosticsOverlay,
        Text::new("Forge diagnostics: warming up"),
        TextColor(Color::WHITE),
        TextFont {
            font_size: bevy::text::FontSize::Px(16.0),
            ..default()
        },
        Node {
            position_type: PositionType::Absolute,
            top: px(12),
            left: px(12),
            padding: UiRect::all(px(8)),
            ..default()
        },
        BackgroundColor(Color::srgba(0.01, 0.02, 0.04, 0.78)),
        ZIndex(100),
    ));
}

fn spawn_pickable_mesh(
    commands: &mut Commands,
    name: String,
    persistent: DomainEntityId,
    mesh: Handle<Mesh>,
    material: Handle<StandardMaterial>,
    transform: Transform,
) {
    commands
        .spawn((
            Name::new(name),
            persistent,
            Mesh3d(mesh),
            MeshMaterial3d(material),
            transform,
        ))
        .observe(select_proof_entity);
}

#[allow(
    clippy::needless_pass_by_value,
    reason = "Bevy observers require owned event wrapper parameters"
)]
fn select_proof_entity(
    click: On<Pointer<Click>>,
    entities: Query<&DomainEntityId>,
    mut selection: ResMut<ProofSelection>,
) {
    if let Ok(entity_id) = entities.get(click.entity)
        && selection.0 != Some(entity_id.0)
    {
        selection.0 = Some(entity_id.0);
        info!(target: "forge::selection", entity_id = %entity_id.0, "proof entity selected from viewport");
    }
}

#[allow(
    clippy::needless_pass_by_value,
    reason = "Bevy systems require owned system parameter wrappers"
)]
fn update_overlay(
    diagnostics: Res<DiagnosticsStore>,
    stats: Res<ProofSceneStats>,
    gpu: Option<Res<GpuDiagnostic>>,
    mut overlay: Single<&mut Text, With<DiagnosticsOverlay>>,
) {
    let fps = diagnostics
        .get(&FrameTimeDiagnosticsPlugin::FPS)
        .and_then(bevy::diagnostic::Diagnostic::smoothed)
        .unwrap_or_default();
    let frame_ms = diagnostics
        .get(&FrameTimeDiagnosticsPlugin::FRAME_TIME)
        .and_then(bevy::diagnostic::Diagnostic::smoothed)
        .unwrap_or_default();
    let adapter = gpu
        .as_ref()
        .map_or("initialisation GPU", |gpu| gpu.name.as_str());
    **overlay = Text::new(format!(
        "OpenRPG Forge · Gate 0\n{adapter}\n{fps:6.1} FPS · {frame_ms:5.2} ms\n{} entités source · {} draws logiques\n{}",
        stats.source_entities, stats.mesh_draws_logical, stats.source_hash
    ));
}

fn record_gpu_diagnostic(
    adapter: Option<Res<RenderAdapterInfo>>,
    device: Option<Res<RenderDevice>>,
    mut logged: ResMut<AdapterLogged>,
    mut commands: Commands,
) {
    if logged.0 {
        return;
    }
    let (Some(adapter), Some(device)) = (adapter, device) else {
        return;
    };
    let limits = device.limits();
    let diagnostic = GpuDiagnostic {
        name: adapter.name.clone(),
        vendor: adapter.vendor,
        device: adapter.device,
        device_type: format!("{:?}", adapter.device_type),
        driver: adapter.driver.clone(),
        driver_info: adapter.driver_info.clone(),
        backend: format!("{:?}", adapter.backend),
        max_texture_dimension_2d: limits.max_texture_dimension_2d,
        max_bind_groups: limits.max_bind_groups,
        max_uniform_buffer_binding_size: limits.max_uniform_buffer_binding_size,
        max_storage_buffer_binding_size: limits.max_storage_buffer_binding_size,
    };
    info!(
        target: "forge::gpu",
        adapter = %diagnostic.name,
        backend = %diagnostic.backend,
        device_type = %diagnostic.device_type,
        driver = %diagnostic.driver,
        "high-performance adapter initialized"
    );
    commands.insert_resource(diagnostic);
    logged.0 = true;
}

#[allow(
    clippy::needless_pass_by_value,
    reason = "Bevy systems require owned system parameter wrappers"
)]
fn capture_at_requested_frame(
    mut commands: Commands,
    frame: Res<bevy::diagnostic::FrameCount>,
    automation: Res<RuntimeAutomation>,
) {
    if frame.0 == automation.capture_frame
        && let Some(path) = &automation.capture_path
    {
        commands
            .spawn(Screenshot::primary_window())
            .observe(save_to_disk(path.clone()));
        info!(target: "forge::proof", path = %path.display(), frame = frame.0, "capture requested");
    }
}

#[allow(
    clippy::needless_pass_by_value,
    reason = "Bevy systems require owned system parameter wrappers"
)]
fn collect_frame_sample(
    frame: Res<bevy::diagnostic::FrameCount>,
    automation: Res<RuntimeAutomation>,
    time: Res<Time<Real>>,
    mut ledger: ResMut<FrameLedger>,
) {
    if frame.0 > automation.metrics_warmup_frames {
        let frame_ms = time.delta_secs_f64() * 1_000.0;
        if frame_ms.is_finite() && frame_ms > 0.0 {
            ledger.0.push(frame_ms);
        }
    }
}

#[allow(
    clippy::needless_pass_by_value,
    reason = "Bevy systems require owned system parameter wrappers"
)]
fn write_metrics_and_exit(
    frame: Res<bevy::diagnostic::FrameCount>,
    automation: Res<RuntimeAutomation>,
    ledger: Res<FrameLedger>,
    stats: Res<ProofSceneStats>,
    gpu: Option<Res<GpuDiagnostic>>,
    diagnostics: Res<DiagnosticsStore>,
    mut exit: MessageWriter<AppExit>,
) {
    if automation
        .exit_after_frames
        .is_none_or(|last_frame| frame.0 < last_frame)
    {
        return;
    }

    if let Some(path) = &automation.capture_path {
        match capture_artifact_size(path) {
            Ok(bytes) => {
                info!(target: "forge::proof", path = %path.display(), bytes, "runtime capture verified");
            }
            Err(error) => {
                error!(target: "forge::proof", path = %path.display(), %error, "runtime capture failed");
                exit.write(AppExit::error());
                return;
            }
        }
    }

    if let Some(path) = &automation.metrics_path {
        match build_runtime_metrics(&ledger.0, &stats, gpu.as_deref(), &diagnostics)
            .and_then(|report| write_metrics(path, &report))
        {
            Ok(()) => {
                info!(target: "forge::proof", path = %path.display(), samples = ledger.0.len(), "runtime metrics written");
            }
            Err(error) => {
                error!(target: "forge::proof", path = %path.display(), %error, "runtime metrics failed");
                exit.write(AppExit::error());
                return;
            }
        }
    }

    info!(target: "forge::proof", frame = frame.0, "bounded runtime completed");
    exit.write(AppExit::Success);
}

fn capture_artifact_size(path: &Path) -> Result<u64, String> {
    let metadata = fs::metadata(path)
        .map_err(|error| format!("cannot inspect {}: {error}", path.display()))?;
    if !metadata.is_file() {
        return Err(format!("{} is not a regular file", path.display()));
    }
    if metadata.len() == 0 {
        return Err(format!("{} is empty", path.display()));
    }
    Ok(metadata.len())
}

fn build_runtime_metrics(
    samples: &[f64],
    stats: &ProofSceneStats,
    gpu: Option<&GpuDiagnostic>,
    diagnostics: &DiagnosticsStore,
) -> Result<RuntimeMetricsReport, String> {
    if samples.is_empty() {
        return Err("no post-warmup frame samples were collected".to_owned());
    }
    let mut sorted = samples.to_vec();
    sorted.sort_by(f64::total_cmp);
    let duration_ms = samples.iter().sum::<f64>();
    let sample_count = u32::try_from(samples.len())
        .map_err(|_| "frame sample count exceeds the supported u32 ledger".to_owned())?;
    let mean_ms = duration_ms / f64::from(sample_count);
    let slowest_count = samples.len().div_ceil(100).max(1);
    let slowest_count_u32 = u32::try_from(slowest_count)
        .map_err(|_| "slow-frame sample count exceeds the supported u32 ledger".to_owned())?;
    let slowest_mean_ms =
        sorted.iter().rev().take(slowest_count).sum::<f64>() / f64::from(slowest_count_u32);
    let frame = FrameMetrics {
        samples: samples.len(),
        duration_ms,
        mean_ms,
        p50_ms: percentile(&sorted, 50, 100),
        p95_ms: percentile(&sorted, 95, 100),
        p99_ms: percentile(&sorted, 99, 100),
        max_ms: *sorted.last().ok_or("missing sorted frame sample")?,
        mean_fps: 1_000.0 / mean_ms,
        one_percent_low_fps: 1_000.0 / slowest_mean_ms,
        hitches_over_16_7_ms: samples.iter().filter(|value| **value > 16.7).count(),
        hitches_over_33_3_ms: samples.iter().filter(|value| **value > 33.3).count(),
    };
    let mut render_diagnostics = diagnostics
        .iter()
        .filter(|diagnostic| diagnostic.path().as_str().starts_with("render/"))
        .filter_map(|diagnostic| {
            diagnostic.value().map(|value| NamedDiagnostic {
                path: diagnostic.path().to_string(),
                value,
                suffix: diagnostic.suffix.to_string(),
            })
        })
        .collect::<Vec<_>>();
    render_diagnostics.sort_by(|left, right| left.path.cmp(&right.path));
    Ok(RuntimeMetricsReport {
        schema: "forge.runtime-metrics",
        schema_version: 1,
        source_hash: stats.source_hash.clone(),
        gpu: gpu.cloned(),
        frame,
        render_diagnostics,
        render_work: stats.clone(),
    })
}

fn percentile(sorted: &[f64], numerator: usize, denominator: usize) -> f64 {
    let rank = sorted.len().saturating_mul(numerator).div_ceil(denominator);
    sorted[rank.saturating_sub(1).min(sorted.len() - 1)]
}

fn write_metrics(path: &PathBuf, report: &RuntimeMetricsReport) -> Result<(), String> {
    if let Some(parent) = path
        .parent()
        .filter(|parent| !parent.as_os_str().is_empty())
    {
        fs::create_dir_all(parent)
            .map_err(|error| format!("cannot create {}: {error}", parent.display()))?;
    }
    let json = serde_json::to_vec_pretty(report)
        .map_err(|error| format!("cannot serialize metrics: {error}"))?;
    fs::write(path, json).map_err(|error| format!("cannot write {}: {error}", path.display()))
}

#[cfg(test)]
mod tests {
    use super::*;

    #[test]
    fn domain_transform_conversion_keeps_all_components() {
        let source = forge_core::Transform {
            translation_m: forge_core::Vec3::new(1.0, 2.0, 3.0),
            rotation: forge_core::UnitQuaternion::IDENTITY,
            scale: forge_core::Vec3::new(2.0, 3.0, 4.0),
        };
        let converted = domain_transform_to_bevy(source);
        assert_eq!(converted.translation, Vec3::new(1.0, 2.0, 3.0));
        assert_eq!(converted.rotation, Quat::IDENTITY);
        assert_eq!(converted.scale, Vec3::new(2.0, 3.0, 4.0));
    }

    #[test]
    fn frame_metrics_use_nearest_rank_and_slowest_one_percent() {
        let samples = (1..=200).map(f64::from).collect::<Vec<_>>();
        let stats = ProofSceneStats {
            source_entities: 4,
            mesh_draws_logical: 2,
            source_hash: "abc".to_owned(),
        };
        let diagnostics = DiagnosticsStore::default();
        let report = build_runtime_metrics(&samples, &stats, None, &diagnostics).unwrap();
        assert_eq!(report.frame.samples, 200);
        assert!((report.frame.p50_ms - 100.0).abs() < f64::EPSILON);
        assert!((report.frame.p95_ms - 190.0).abs() < f64::EPSILON);
        assert!((report.frame.p99_ms - 198.0).abs() < f64::EPSILON);
        assert!((report.frame.max_ms - 200.0).abs() < f64::EPSILON);
        assert!((report.frame.one_percent_low_fps - (1_000.0 / 199.5)).abs() < f64::EPSILON);
    }

    #[test]
    fn capture_artifact_must_exist_and_be_non_empty() {
        let path = std::env::temp_dir().join(format!(
            "openrpg-forge-capture-artifact-{}.png",
            std::process::id()
        ));
        let _ = fs::remove_file(&path);
        assert!(capture_artifact_size(&path).is_err());

        fs::write(&path, []).unwrap();
        assert!(capture_artifact_size(&path).is_err());

        fs::write(&path, [1_u8]).unwrap();
        assert_eq!(capture_artifact_size(&path).unwrap(), 1);
        fs::remove_file(path).unwrap();
    }
}
