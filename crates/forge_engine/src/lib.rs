#![deny(unsafe_code)]

//! Bevy rendering adapter for shared Forge documents.

mod environment;
mod gpu_performance;
#[cfg(feature = "physics-rapier")]
mod physics;
#[cfg(feature = "physics-rapier")]
mod player_camera;
#[cfg(feature = "physics-rapier")]
mod player_controller;
mod quality;
mod scenery;
#[cfg(feature = "physics-rapier")]
mod terrain_update;
mod vegetation;
mod visual_valley;
mod water;
mod weather;

pub use environment::{
    EnvironmentDistanceFogStats, EnvironmentProjectionStats, EnvironmentProjectionStatus,
    EnvironmentSkyStats, EnvironmentSunStats, EnvironmentVolumetricStats,
    VisualValleyEnvironmentPlugin,
};
pub use gpu_performance::{
    GpuMemoryObservation, PIPELINE_WARMUP_STABLE_FRAMES, PipelineWarmupObservation,
    PipelineWarmupReport, PipelineWarmupStatus, WgpuAllocatorReportStatus,
};
#[cfg(feature = "physics-rapier")]
pub(crate) use physics::build_terrain_heightfield_collider;
#[cfg(feature = "physics-rapier")]
pub use physics::{
    TERRAIN_PHYSICS_HZ, TerrainHeightfieldCollider, TerrainPhysicsAdapterError,
    TerrainPhysicsPlugin,
};
#[cfg(feature = "physics-rapier")]
pub use player_camera::PlayerCameraMode;
#[cfg(feature = "physics-rapier")]
pub use player_controller::{
    ForgePlayerCamera, ForgePlayerCharacter, PendingPlayerControlCommand, PlayerControlCommand,
    PlayerControlCommandError, PlayerControllerPlugin, PlayerControllerSet,
    PlayerRuntimeBindingSnapshot, PlayerRuntimeBindings, PlayerRuntimeSnapshot, PlayerSpawnPoint,
};
use quality::QUALITY_FIELD_PROJECTION_CONTRACT;
pub use quality::{
    QualityFieldProjection, QualityFieldProjectionStatus, QualityTierTransition,
    VisualValleyQualityPlugin, VisualValleyQualitySelection, VisualValleyQualityStats,
};
pub use scenery::{
    SceneryAdapterError, SceneryInstanceCounts, SceneryProjectionStats, SceneryProjectionStatus,
    VisualValleySceneryPlugin, inspect_scenery_projection,
};
#[cfg(feature = "physics-rapier")]
pub use terrain_update::{
    PendingTerrainPartialUpdate, TerrainRuntimeBindingSnapshot, TerrainRuntimeBindings,
    TerrainRuntimeState, TerrainRuntimeUpdateError, TerrainRuntimeUpdateOutcome,
    TerrainRuntimeUpdateReport, TerrainRuntimeUpdateSet, TerrainUpdateSubmissionError,
};
pub use vegetation::{
    MAX_VEGETATION_CANDIDATE_ATTEMPTS, MAX_VEGETATION_GRASS_BLADES,
    MAX_VEGETATION_PROJECTION_ENTITIES, VegetationAdapterError, VegetationChunkBatchMarker,
    VegetationInteractionField, VegetationLodRanges, VegetationMeshError, VegetationMeshKey,
    VegetationPlacementMarker, VegetationPlacementStats, VegetationProjectionCounts,
    VegetationProjectionLod, VegetationProjectionStats, VegetationRenderPathStats,
    VegetationTriangleStats, VegetationVisibilityBucket, VegetationVisibilityObservationStatus,
    VegetationVisibilityStats, VisualValleyVegetationExtension, VisualValleyVegetationMaterial,
    VisualValleyVegetationPlugin, inspect_vegetation_projection,
};
pub use visual_valley::{
    VisualValleyAdapterError, VisualValleyDebug, VisualValleyPlugin,
    VisualValleyShadowObservationStatus, VisualValleyShadowStats, VisualValleyStats,
    VisualValleyTerrainChunk, VisualValleyTerrainExtension, VisualValleyTerrainMaterial,
};
pub(crate) use visual_valley::{VisualValleyChunkDebugBounds, build_bevy_terrain_mesh};
pub use water::{
    VISUAL_VALLEY_WATER_CLEARANCE_M, VisualValleyRiverBankSurface, VisualValleyWaterExtension,
    VisualValleyWaterMaterial, VisualValleyWaterPlugin, VisualValleyWaterSurface,
    WaterAdapterError, WaterProjectionStats, WaterRuntimeObservationStatus, WaterRuntimeState,
};
pub use weather::{
    MAX_WEATHER_BATCH_DROPS, VisualValleyRainBatch, VisualValleyRainExtension,
    VisualValleyRainMaterial, VisualValleyWeatherPlugin, WeatherAdapterError,
    WeatherPostProcessingStats, WeatherProjectionStats, WeatherProjectionStatus, WeatherRainStats,
    WeatherRuntimeState, WeatherWetnessStats,
};

use bevy::{
    diagnostic::{DiagnosticsStore, EntityCountDiagnosticsPlugin, FrameTimeDiagnosticsPlugin},
    ecs::system::SystemParam,
    prelude::*,
    render::{
        diagnostic::RenderDiagnosticsPlugin,
        renderer::{RenderAdapterInfo, RenderDevice},
        view::screenshot::{Screenshot, save_to_disk},
    },
    window::{PresentMode, PrimaryWindow, WindowCloseRequested, WindowResized},
};
use forge_core::{EntityId, FrameDistribution as CoreFrameDistribution, FrameTimeHistogram};
use forge_project::{SceneProofDocument, SceneProofKind};
use gpu_performance::{
    GpuPerformancePlugin, collect_gpu_memory_observation, qualify_pipeline_warmup,
};
use serde::Serialize;
use std::{
    fmt::Write as _,
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
    pub resize_request: Option<WindowResizeRequest>,
    pub close_at_frame: Option<u32>,
}

/// Deterministic native-window resize used only by the proof harness.
#[derive(Clone, Copy, Debug, Eq, PartialEq, Serialize)]
pub struct WindowResizeRequest {
    pub frame: u32,
    pub width: u32,
    pub height: u32,
}

#[derive(Clone, Debug, Default, Resource, Serialize)]
pub struct ProofSceneStats {
    /// Canonical input records represented by this runtime scene. Visual Valley has one terrain
    /// document; its 64 chunks are derived meshes, not 64 persistent source entities.
    pub source_entities: usize,
    /// Mesh instances materialized from the source document; never a GPU draw-call count.
    pub source_mesh_instances: usize,
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

#[derive(Default, Resource)]
pub(crate) struct GovernedVisualValleyMetricsRequired;

#[derive(Clone, Copy, Debug, Eq, PartialEq)]
struct Gate1RenderState {
    quality_current_tier: Option<forge_project::VisualQualityTier>,
    quality_applied_tier: Option<forge_project::VisualQualityTier>,
    scenery_status: Option<SceneryProjectionStatus>,
    scenery_loaded_models: Option<usize>,
    scenery_failed_models: Option<usize>,
    scenery_hidden_procedural_entities: Option<usize>,
    scenery_replacement_instances: Option<usize>,
    scenery_lod_start_bits: Option<u32>,
    scenery_lod_end_bits: Option<u32>,
}

impl Gate1RenderState {
    fn from_evidence(
        quality: Option<&VisualValleyQualityStats>,
        scenery: Option<&SceneryProjectionStats>,
    ) -> Option<Self> {
        (quality.is_some() || scenery.is_some()).then_some(Self {
            quality_current_tier: quality.map(|stats| stats.current_tier),
            quality_applied_tier: quality.and_then(|stats| stats.dynamically_applied_tier),
            scenery_status: scenery.map(|stats| stats.status),
            scenery_loaded_models: scenery.map(|stats| stats.loaded_models),
            scenery_failed_models: scenery.map(|stats| stats.failed_models),
            scenery_hidden_procedural_entities: scenery
                .map(|stats| stats.hidden_procedural_entities),
            scenery_replacement_instances: scenery.map(|stats| stats.replacement_instances),
            scenery_lod_start_bits: scenery.map(|stats| stats.replacement_lod_start_m.to_bits()),
            scenery_lod_end_bits: scenery.map(|stats| stats.replacement_lod_end_m.to_bits()),
        })
    }
}

#[derive(Resource)]
struct FrameLedger {
    samples_ms: Vec<f64>,
    first_frame: Option<u32>,
    last_frame: Option<u32>,
    last_recorded_frame: Option<u32>,
    sample_limit: usize,
    pipeline_ready_at_measurement_start: Option<bool>,
    pipeline_ready_for_entire_measurement: bool,
    gate1_render_state_stable_at_measurement_start: Option<bool>,
    gate1_render_state_stable_for_entire_measurement: bool,
    last_gate1_render_state: Option<Gate1RenderState>,
}

impl FrameLedger {
    fn for_automation(automation: &RuntimeAutomation) -> Self {
        let sample_window = automation
            .metrics_path
            .as_ref()
            .and(automation.exit_after_frames)
            .filter(|last_frame| *last_frame > automation.metrics_warmup_frames)
            .map(|last_frame| {
                let first_frame = automation.metrics_warmup_frames + 1;
                let sample_count = last_frame - automation.metrics_warmup_frames;
                let sample_limit = usize::try_from(sample_count)
                    .unwrap_or(MAX_FRAME_LEDGER_SAMPLES)
                    .min(MAX_FRAME_LEDGER_SAMPLES);
                (first_frame, last_frame, sample_limit)
            });
        let (first_frame, last_frame, sample_limit) = sample_window.map_or(
            (None, None, 0),
            |(first_frame, last_frame, sample_limit)| {
                (Some(first_frame), Some(last_frame), sample_limit)
            },
        );
        Self {
            samples_ms: Vec::with_capacity(sample_limit),
            first_frame,
            last_frame,
            last_recorded_frame: None,
            sample_limit,
            pipeline_ready_at_measurement_start: None,
            pipeline_ready_for_entire_measurement: true,
            gate1_render_state_stable_at_measurement_start: None,
            gate1_render_state_stable_for_entire_measurement: true,
            last_gate1_render_state: None,
        }
    }

    fn record(
        &mut self,
        frame: u32,
        frame_ms: f64,
        pipeline_ready: bool,
        gate1_render_state: Option<Gate1RenderState>,
    ) {
        let gate1_state_stable = match (self.last_gate1_render_state, gate1_render_state) {
            (None, None) => true,
            (Some(previous), Some(current)) => previous == current,
            (None, Some(_)) | (Some(_), None) => false,
        };
        self.last_gate1_render_state = gate1_render_state;
        let inside_window = self
            .first_frame
            .zip(self.last_frame)
            .is_some_and(|(first_frame, last_frame)| (first_frame..=last_frame).contains(&frame));
        if inside_window {
            self.pipeline_ready_at_measurement_start
                .get_or_insert(pipeline_ready);
            self.pipeline_ready_for_entire_measurement &= pipeline_ready;
            self.gate1_render_state_stable_at_measurement_start
                .get_or_insert(gate1_state_stable);
            self.gate1_render_state_stable_for_entire_measurement &= gate1_state_stable;
        }
        if inside_window
            && self.samples_ms.len() < self.sample_limit
            && self
                .last_recorded_frame
                .is_none_or(|last_recorded_frame| frame > last_recorded_frame)
            && frame_ms.is_finite()
            && frame_ms > 0.0
        {
            self.samples_ms.push(frame_ms);
            self.last_recorded_frame = Some(frame);
        }
    }
}

#[derive(Clone, Debug, PartialEq, Serialize)]
struct WindowStateReport {
    logical_width: f32,
    logical_height: f32,
    physical_width: u32,
    physical_height: u32,
    scale_factor: f32,
    resizable: bool,
    present_mode_request: String,
    vsync_requested: bool,
}

#[derive(Clone, Debug, Resource)]
struct WindowLifecycle {
    initial: Option<WindowStateReport>,
    latest: Option<WindowStateReport>,
    resize_request: Option<WindowResizeRequest>,
    resize_request_issued: bool,
    resize_request_issued_frame: Option<u32>,
    resize_request_observed: bool,
    resize_request_observed_frame: Option<u32>,
    resize_events_observed: u32,
    automated_close_frame: Option<u32>,
    automated_close_issued: bool,
    close_requests_observed: u32,
}

#[derive(Clone, Debug, PartialEq, Serialize)]
struct WindowLifecycleReport {
    initial: Option<WindowStateReport>,
    final_state: Option<WindowStateReport>,
    resize_request: Option<WindowResizeRequest>,
    resize_request_issued: bool,
    resize_request_issued_frame: Option<u32>,
    resize_request_observed: bool,
    resize_request_observed_frame: Option<u32>,
    resize_events_observed: u32,
    automated_close_frame: Option<u32>,
    automated_close_issued: bool,
    close_requests_observed: u32,
}

#[derive(Debug, Serialize)]
struct RuntimeMetricsReport {
    schema: &'static str,
    schema_version: u32,
    source_hash: String,
    gpu: Option<GpuDiagnostic>,
    gpu_memory: GpuMemoryObservation,
    pipeline_warmup: PipelineWarmupReport,
    window: WindowLifecycleReport,
    frame: FrameMetrics,
    render_diagnostics: Vec<NamedDiagnostic>,
    render_work: RenderWorkReport,
    #[serde(skip_serializing_if = "Option::is_none")]
    visual_valley: Option<VisualValleyStats>,
    #[serde(skip_serializing_if = "Option::is_none")]
    visual_valley_shadow: Option<VisualValleyShadowStats>,
    #[serde(skip_serializing_if = "Option::is_none")]
    vegetation: Option<VegetationProjectionStats>,
    #[serde(skip_serializing_if = "Option::is_none")]
    scenery: Option<SceneryProjectionStats>,
    #[serde(skip_serializing_if = "Option::is_none")]
    environment: Option<EnvironmentProjectionStats>,
    #[serde(skip_serializing_if = "Option::is_none")]
    water: Option<WaterProjectionStats>,
    #[serde(skip_serializing_if = "Option::is_none")]
    weather: Option<WeatherProjectionStats>,
    #[serde(skip_serializing_if = "Option::is_none")]
    quality: Option<VisualValleyQualityStats>,
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
    histogram: FrameTimeHistogram,
}

#[derive(Clone, Debug, PartialEq, Serialize)]
struct NamedDiagnostic {
    path: String,
    value: f64,
    suffix: String,
}

#[derive(Clone, Debug, PartialEq, Serialize)]
struct RenderWorkReport {
    source_entities: usize,
    source_mesh_instances: usize,
    /// Deliberately null: Bevy 0.19 exposes no backend-neutral global draw-call counter.
    gpu_draw_calls: Option<u64>,
    gpu_draw_call_status: &'static str,
    gpu_main_opaque_primitives: Option<f64>,
    gpu_main_opaque_vertex_shader_invocations: Option<f64>,
    gpu_main_opaque_fragment_shader_invocations: Option<f64>,
    timed_render_spans_observed: usize,
}

const GPU_DRAW_CALL_STATUS: &str = "unavailable_bevy_0_19_no_global_counter";
const MAX_FRAME_LEDGER_SAMPLES: usize = 1_000_000;
const GATE1_MINIMUM_FRAME_SAMPLES: usize = 120;

/// Installs the document adapter, diagnostics, deterministic capture and bounded-exit harness.
pub struct ProofScenePlugin {
    document: SceneProofDocument,
    automation: RuntimeAutomation,
}

/// Shared diagnostics, capture, frame ledger and lifecycle plumbing for one rendered scene.
///
/// This remains crate-private: applications select a concrete scene plugin, never the evidence
/// harness by itself.
struct RuntimeEvidencePlugin {
    automation: RuntimeAutomation,
}

impl RuntimeEvidencePlugin {
    fn new(automation: RuntimeAutomation) -> Self {
        Self { automation }
    }
}

impl Plugin for RuntimeEvidencePlugin {
    fn build(&self, app: &mut App) {
        app.insert_resource(self.automation.clone())
            .insert_resource(FrameLedger::for_automation(&self.automation))
            .insert_resource(WindowLifecycle::new(
                self.automation.resize_request,
                self.automation.close_at_frame,
            ))
            .init_resource::<ProofSceneStats>()
            .init_resource::<AdapterLogged>()
            .add_plugins((
                FrameTimeDiagnosticsPlugin::default(),
                EntityCountDiagnosticsPlugin::default(),
                RenderDiagnosticsPlugin,
                GpuPerformancePlugin,
            ))
            .add_systems(
                Startup,
                (initialize_diagnostics_overlay, initialize_window_lifecycle).chain(),
            )
            .add_systems(
                Update,
                (
                    record_gpu_diagnostic,
                    request_automated_resize,
                    request_automated_close,
                    observe_window_events,
                    update_overlay,
                    capture_at_requested_frame,
                )
                    .chain(),
            )
            .add_systems(
                Last,
                (collect_frame_sample, write_metrics_and_exit)
                    .chain()
                    .after(quality::VisualValleyQualitySet::Apply)
                    .after(scenery::VisualValleyScenerySet::ApplyQuality),
            );
    }
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
            .init_resource::<ProofSelection>()
            .add_plugins((
                RuntimeEvidencePlugin::new(self.automation.clone()),
                MeshPickingPlugin,
            ))
            .add_systems(Startup, spawn_proof_scene);
    }
}

impl WindowLifecycle {
    const fn new(
        resize_request: Option<WindowResizeRequest>,
        automated_close_frame: Option<u32>,
    ) -> Self {
        Self {
            initial: None,
            latest: None,
            resize_request,
            resize_request_issued: false,
            resize_request_issued_frame: None,
            resize_request_observed: false,
            resize_request_observed_frame: None,
            resize_events_observed: 0,
            automated_close_frame,
            automated_close_issued: false,
            close_requests_observed: 0,
        }
    }

    #[allow(
        clippy::cast_precision_loss,
        reason = "window dimensions are validated human-scale pixel counts"
    )]
    fn observe_resize(&mut self, frame: u32, width: f32, height: f32) {
        self.resize_events_observed = self.resize_events_observed.saturating_add(1);
        if let Some(request) = self.resize_request {
            let width_matches = (width - request.width as f32).abs() <= 0.5;
            let height_matches = (height - request.height as f32).abs() <= 0.5;
            let follows_request = self
                .resize_request_issued_frame
                .is_some_and(|issued_frame| frame > issued_frame);
            if follows_request && width_matches && height_matches {
                self.resize_request_observed = true;
                self.resize_request_observed_frame.get_or_insert(frame);
            }
        }
    }

    fn report(&self, final_window: Option<WindowStateReport>) -> WindowLifecycleReport {
        WindowLifecycleReport {
            initial: self.initial.clone(),
            final_state: final_window.or_else(|| self.latest.clone()),
            resize_request: self.resize_request,
            resize_request_issued: self.resize_request_issued,
            resize_request_issued_frame: self.resize_request_issued_frame,
            resize_request_observed: self.resize_request_observed,
            resize_request_observed_frame: self.resize_request_observed_frame,
            resize_events_observed: self.resize_events_observed,
            automated_close_frame: self.automated_close_frame,
            automated_close_issued: self.automated_close_issued,
            close_requests_observed: self.close_requests_observed,
        }
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

fn present_mode_request_label(mode: PresentMode) -> &'static str {
    match mode {
        PresentMode::AutoVsync => "auto_vsync",
        PresentMode::AutoNoVsync => "auto_no_vsync",
        PresentMode::Fifo => "fifo",
        PresentMode::FifoRelaxed => "fifo_relaxed",
        PresentMode::Immediate => "immediate",
        PresentMode::Mailbox => "mailbox",
    }
}

const fn present_mode_requests_vsync(mode: PresentMode) -> bool {
    matches!(
        mode,
        PresentMode::AutoVsync
            | PresentMode::Fifo
            | PresentMode::FifoRelaxed
            | PresentMode::Mailbox
    )
}

fn window_state_report(window: &Window) -> WindowStateReport {
    WindowStateReport {
        logical_width: window.resolution.width(),
        logical_height: window.resolution.height(),
        physical_width: window.resolution.physical_width(),
        physical_height: window.resolution.physical_height(),
        scale_factor: window.resolution.scale_factor(),
        resizable: window.resizable,
        present_mode_request: present_mode_request_label(window.present_mode).to_owned(),
        vsync_requested: present_mode_requests_vsync(window.present_mode),
    }
}

fn initialize_window_lifecycle(
    windows: Query<&Window, With<PrimaryWindow>>,
    mut lifecycle: ResMut<WindowLifecycle>,
) {
    let Ok(window) = windows.single() else {
        warn!(
            target: "forge::window",
            schema = "forge.runtime-event",
            schema_version = 1_u64,
            event_kind = "window_initialization_unavailable",
            "primary window state was not available"
        );
        return;
    };
    let state = window_state_report(window);
    lifecycle.initial = Some(state.clone());
    lifecycle.latest = Some(state.clone());
    info!(
        target: "forge::window",
        schema = "forge.runtime-event",
        schema_version = 1_u64,
        event_kind = "window_initialized",
        logical_width = state.logical_width,
        logical_height = state.logical_height,
        physical_width = state.physical_width,
        physical_height = state.physical_height,
        scale_factor = state.scale_factor,
        resizable = state.resizable,
        present_mode_request = %state.present_mode_request,
        vsync_requested = state.vsync_requested,
        "primary window initialized"
    );
}

#[allow(
    clippy::cast_precision_loss,
    clippy::needless_pass_by_value,
    reason = "Bevy systems own wrappers; validated pixel counts fit exactly at runtime sizes"
)]
fn request_automated_resize(
    frame: Res<bevy::diagnostic::FrameCount>,
    mut windows: Query<&mut Window, With<PrimaryWindow>>,
    mut lifecycle: ResMut<WindowLifecycle>,
) {
    let Some(request) = lifecycle.resize_request else {
        return;
    };
    if lifecycle.resize_request_issued || frame.0 < request.frame {
        return;
    }
    let Ok(mut window) = windows.single_mut() else {
        return;
    };
    window
        .resolution
        .set(request.width as f32, request.height as f32);
    lifecycle.resize_request_issued = true;
    lifecycle.resize_request_issued_frame = Some(frame.0);
    lifecycle.latest = Some(window_state_report(&window));
    info!(
        target: "forge::window",
        schema = "forge.runtime-event",
        schema_version = 1_u64,
        event_kind = "window_resize_requested",
        frame = frame.0,
        logical_width = request.width,
        logical_height = request.height,
        "window resize submitted to the backend"
    );
}

#[allow(
    clippy::needless_pass_by_value,
    reason = "Bevy systems require owned system parameter wrappers"
)]
fn request_automated_close(
    frame: Res<bevy::diagnostic::FrameCount>,
    windows: Query<Entity, With<PrimaryWindow>>,
    mut lifecycle: ResMut<WindowLifecycle>,
    mut close_requests: MessageWriter<WindowCloseRequested>,
) {
    let Some(close_at_frame) = lifecycle.automated_close_frame else {
        return;
    };
    if lifecycle.automated_close_issued || frame.0 < close_at_frame {
        return;
    }
    let Ok(window) = windows.single() else {
        return;
    };
    close_requests.write(WindowCloseRequested { window });
    lifecycle.automated_close_issued = true;
    info!(
        target: "forge::window",
        schema = "forge.runtime-event",
        schema_version = 1_u64,
        event_kind = "window_close_requested",
        frame = frame.0,
        origin = "automation",
        "window close requested through the normal Bevy lifecycle"
    );
}

#[allow(
    clippy::needless_pass_by_value,
    reason = "Bevy systems require owned system parameter wrappers"
)]
fn observe_window_events(
    frame: Res<bevy::diagnostic::FrameCount>,
    mut resize_events: MessageReader<WindowResized>,
    mut close_events: MessageReader<WindowCloseRequested>,
    windows: Query<(Entity, &Window), With<PrimaryWindow>>,
    mut lifecycle: ResMut<WindowLifecycle>,
) {
    let Ok((primary_entity, primary_window)) = windows.single() else {
        return;
    };

    for event in resize_events.read() {
        if event.window != primary_entity {
            continue;
        }
        lifecycle.observe_resize(frame.0, event.width, event.height);
        lifecycle.latest = Some(window_state_report(primary_window));
        info!(
            target: "forge::window",
            schema = "forge.runtime-event",
            schema_version = 1_u64,
            event_kind = "window_resize_observed",
            frame = frame.0,
            logical_width = event.width,
            logical_height = event.height,
            resize_events_observed = lifecycle.resize_events_observed,
            requested_resize_observed = lifecycle.resize_request_observed,
            "window backend confirmed a logical resize"
        );
    }

    for event in close_events.read() {
        if event.window != primary_entity {
            continue;
        }
        lifecycle.close_requests_observed = lifecycle.close_requests_observed.saturating_add(1);
        info!(
            target: "forge::window",
            schema = "forge.runtime-event",
            schema_version = 1_u64,
            event_kind = "window_close_observed",
            close_requests_observed = lifecycle.close_requests_observed,
            "window close request observed before teardown"
        );
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
                stats.source_mesh_instances += 1;
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
                stats.source_mesh_instances += 1;
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

    info!(
        target: "forge::scene",
        schema = "forge.runtime-event",
        schema_version = 1_u64,
        event_kind = "scene_materialized",
        scene = %document.0.name,
        source_hash = %stats.source_hash,
        entities = stats.source_entities,
        source_mesh_instances = stats.source_mesh_instances,
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

fn initialize_diagnostics_overlay(mut commands: Commands) {
    spawn_diagnostics_overlay(&mut commands);
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
        info!(
            target: "forge::selection",
            schema = "forge.runtime-event",
            schema_version = 1_u64,
            event_kind = "viewport_entity_selected",
            entity_id = %entity_id.0,
            "proof entity selected from viewport"
        );
    }
}

#[derive(SystemParam)]
struct Gate1OverlayStats<'w> {
    governed_metrics_required: Option<Res<'w, GovernedVisualValleyMetricsRequired>>,
    visual_valley: Option<Res<'w, VisualValleyStats>>,
    visual_valley_shadow: Option<Res<'w, VisualValleyShadowStats>>,
    vegetation: Option<Res<'w, VegetationProjectionStats>>,
    scenery: Option<Res<'w, SceneryProjectionStats>>,
    environment: Option<Res<'w, EnvironmentProjectionStats>>,
    water: Option<Res<'w, WaterProjectionStats>>,
    weather: Option<Res<'w, WeatherProjectionStats>>,
    quality: Option<Res<'w, VisualValleyQualityStats>>,
}

#[derive(SystemParam)]
struct RuntimePerformanceEvidence<'w> {
    render_device: Option<Res<'w, RenderDevice>>,
    pipeline_warmup: Res<'w, PipelineWarmupObservation>,
    diagnostics: Res<'w, DiagnosticsStore>,
}

#[allow(
    clippy::needless_pass_by_value,
    reason = "Bevy systems require owned system parameter wrappers"
)]
fn update_overlay(
    diagnostics: Res<DiagnosticsStore>,
    stats: Res<ProofSceneStats>,
    gate1: Gate1OverlayStats,
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
        .map_or("GPU initialising", |gpu| gpu.name.as_str());
    let render_work = build_render_work_report(&stats, &diagnostics);
    **overlay = Text::new(if let Some(visual_valley) = gate1.visual_valley {
        let mut logical_details = visual_valley.overlay_details();
        if let Some(vegetation) = gate1.vegetation {
            logical_details.push('\n');
            logical_details.push_str(&vegetation.overlay_details());
        }
        if let Some(water) = gate1.water {
            let _ = write!(
                logical_details,
                "\nWater: {} samples | {} triangles | {:?}",
                water.centerline_samples, water.triangles, water.runtime_observation
            );
        }
        if let Some(weather) = gate1.weather {
            let _ = write!(
                logical_details,
                "\nWeather: {:?} | {} drops | wetness {:.2} | {:?}",
                weather.target_state,
                weather.rain.generated_drops,
                weather.wetness.current,
                weather.status
            );
        }
        if let Some(scenery) = gate1.scenery {
            let _ = write!(
                logical_details,
                "\nCC0 scenery: {:?} | {}/{} models | LOD {:.0}-{:.0} m",
                scenery.status,
                scenery.loaded_models,
                scenery.unique_models,
                scenery.replacement_lod_start_m,
                scenery.replacement_lod_end_m
            );
        }
        if let Some(quality) = gate1.quality {
            let _ = write!(
                logical_details,
                "\nQuality: {:?} | {:?} | applied {:?}",
                quality.selection, quality.current_tier, quality.dynamically_applied_tier
            );
        }
        format_runtime_diagnostics_overlay(
            "OpenRPG Forge | Gate 1 / Visual Valley",
            adapter,
            fps,
            frame_ms,
            &stats,
            &render_work,
            Some(&logical_details),
        )
    } else {
        format_diagnostics_overlay(adapter, fps, frame_ms, &stats, &render_work)
    });
}

fn format_diagnostics_overlay(
    adapter: &str,
    fps: f64,
    frame_ms: f64,
    stats: &ProofSceneStats,
    render_work: &RenderWorkReport,
) -> String {
    format_runtime_diagnostics_overlay(
        "OpenRPG Forge | Gate 0",
        adapter,
        fps,
        frame_ms,
        stats,
        render_work,
        None,
    )
}

fn format_runtime_diagnostics_overlay(
    title: &str,
    adapter: &str,
    fps: f64,
    frame_ms: f64,
    stats: &ProofSceneStats,
    render_work: &RenderWorkReport,
    logical_details: Option<&str>,
) -> String {
    let gpu_work = match (
        render_work.gpu_main_opaque_primitives,
        render_work.gpu_main_opaque_vertex_shader_invocations,
    ) {
        (Some(primitives), Some(vertices)) => {
            format!("GPU opaque: {primitives:.0} primitives | {vertices:.0} vertex invocations")
        }
        (Some(primitives), None) => format!("GPU opaque: {primitives:.0} primitives"),
        _ => "GPU pipeline statistics: unavailable".to_owned(),
    };
    let mut output = format!(
        "{title}\n{adapter}\n{fps:6.1} FPS | {frame_ms:5.2} ms\n{} source entities | {} source meshes\nGPU draw calls: unavailable\n{gpu_work}\n{}",
        stats.source_entities, stats.source_mesh_instances, stats.source_hash
    );
    if let Some(logical_details) = logical_details {
        output.push('\n');
        output.push_str(logical_details);
    }
    output
        .chars()
        .map(|character| if character.is_ascii() { character } else { '?' })
        .collect()
}

fn build_render_work_report(
    stats: &ProofSceneStats,
    diagnostics: &DiagnosticsStore,
) -> RenderWorkReport {
    RenderWorkReport {
        source_entities: stats.source_entities,
        source_mesh_instances: stats.source_mesh_instances,
        gpu_draw_calls: None,
        gpu_draw_call_status: GPU_DRAW_CALL_STATUS,
        gpu_main_opaque_primitives: diagnostic_value(
            diagnostics,
            "render/main_opaque_pass_3d/clipper_primitives_out",
        ),
        gpu_main_opaque_vertex_shader_invocations: diagnostic_value(
            diagnostics,
            "render/main_opaque_pass_3d/vertex_shader_invocations",
        ),
        gpu_main_opaque_fragment_shader_invocations: diagnostic_value(
            diagnostics,
            "render/main_opaque_pass_3d/fragment_shader_invocations",
        ),
        timed_render_spans_observed: diagnostics
            .iter()
            .filter(|diagnostic| {
                let path = diagnostic.path().as_str();
                path.starts_with("render/") && path.ends_with("/elapsed_cpu")
            })
            .count(),
    }
}

fn diagnostic_value(diagnostics: &DiagnosticsStore, path: &str) -> Option<f64> {
    diagnostics
        .iter()
        .find(|diagnostic| diagnostic.path().as_str() == path)
        .and_then(bevy::diagnostic::Diagnostic::value)
        .filter(|value| value.is_finite() && *value >= 0.0)
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
        schema = "forge.runtime-event",
        schema_version = 1_u64,
        event_kind = "gpu_adapter_initialized",
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
        info!(
            target: "forge::proof",
            schema = "forge.runtime-event",
            schema_version = 1_u64,
            event_kind = "capture_requested",
            path = %path.display(),
            frame = frame.0,
            "capture requested"
        );
    }
}

#[allow(
    clippy::needless_pass_by_value,
    reason = "Bevy systems require owned system parameter wrappers"
)]
fn collect_frame_sample(
    frame: Res<bevy::diagnostic::FrameCount>,
    time: Res<Time<Real>>,
    pipeline_warmup: Res<PipelineWarmupObservation>,
    quality: Option<Res<VisualValleyQualityStats>>,
    scenery: Option<Res<SceneryProjectionStats>>,
    mut ledger: ResMut<FrameLedger>,
) {
    ledger.record(
        frame.0,
        time.delta_secs_f64() * 1_000.0,
        pipeline_warmup.is_ready(),
        Gate1RenderState::from_evidence(quality.as_deref(), scenery.as_deref()),
    );
}

#[allow(
    clippy::needless_pass_by_value,
    clippy::too_many_arguments,
    clippy::too_many_lines,
    reason = "Bevy lifecycle orchestration keeps independent proof failures explicit at the call site"
)]
fn write_metrics_and_exit(
    frame: Res<bevy::diagnostic::FrameCount>,
    automation: Res<RuntimeAutomation>,
    ledger: Res<FrameLedger>,
    stats: Res<ProofSceneStats>,
    gate1: Gate1OverlayStats,
    gpu: Option<Res<GpuDiagnostic>>,
    performance: RuntimePerformanceEvidence,
    windows: Query<&Window, With<PrimaryWindow>>,
    lifecycle: Res<WindowLifecycle>,
    mut exit: MessageWriter<AppExit>,
) {
    if automation
        .exit_after_frames
        .is_none_or(|last_frame| frame.0 < last_frame)
    {
        return;
    }

    if let Err(error) = ensure_resize_confirmed(&lifecycle) {
        error!(
            target: "forge::proof",
            schema = "forge.runtime-event",
            schema_version = 1_u64,
            event_kind = "window_resize_unconfirmed",
            %error,
            "bounded runtime ended before the backend confirmed the requested resize"
        );
        exit.write(AppExit::error());
        return;
    }

    if let Some(path) = &automation.capture_path {
        match capture_artifact_size(path) {
            Ok(bytes) => {
                info!(
                    target: "forge::proof",
                    schema = "forge.runtime-event",
                    schema_version = 1_u64,
                    event_kind = "capture_verified",
                    path = %path.display(),
                    bytes,
                    "runtime capture verified"
                );
            }
            Err(error) => {
                error!(
                    target: "forge::proof",
                    schema = "forge.runtime-event",
                    schema_version = 1_u64,
                    event_kind = "capture_failed",
                    path = %path.display(),
                    %error,
                    "runtime capture failed"
                );
                exit.write(AppExit::error());
                return;
            }
        }
    }

    if let Some(path) = &automation.metrics_path {
        let final_window = windows.single().ok().map(window_state_report);
        let result = collect_gpu_memory_observation(
            performance.render_device.as_deref(),
            &performance.diagnostics,
        )
        .and_then(|gpu_memory| {
            qualify_pipeline_warmup(
                ledger.pipeline_ready_at_measurement_start,
                ledger.pipeline_ready_for_entire_measurement,
                &performance.pipeline_warmup,
            )
            .map(|pipeline_warmup| (gpu_memory, pipeline_warmup))
        })
        .and_then(|(gpu_memory, pipeline_warmup)| {
            if gate1.governed_metrics_required.is_some() {
                qualify_gate1_render_state_stability(
                    ledger.gate1_render_state_stable_at_measurement_start,
                    ledger.gate1_render_state_stable_for_entire_measurement,
                )?;
            }
            build_runtime_metrics(
                &ledger.samples_ms,
                &stats,
                gate1.governed_metrics_required.is_some(),
                gate1.visual_valley.as_deref(),
                gate1.visual_valley_shadow.as_deref(),
                gate1.vegetation.as_deref(),
                gate1.scenery.as_deref(),
                gate1.environment.as_deref(),
                gate1.water.as_deref(),
                gate1.weather.as_deref(),
                gate1.quality.as_deref(),
                gpu.as_deref(),
                &gpu_memory,
                &pipeline_warmup,
                &performance.diagnostics,
                &lifecycle,
                final_window,
            )
            .and_then(|report| write_metrics(path, &report))
        });
        match result {
            Ok(()) => {
                info!(
                    target: "forge::proof",
                    schema = "forge.runtime-event",
                    schema_version = 1_u64,
                    event_kind = "runtime_metrics_written",
                    path = %path.display(),
                    samples = ledger.samples_ms.len(),
                    "runtime metrics written"
                );
            }
            Err(error) => {
                error!(
                    target: "forge::proof",
                    schema = "forge.runtime-event",
                    schema_version = 1_u64,
                    event_kind = "runtime_metrics_failed",
                    path = %path.display(),
                    %error,
                    "runtime metrics failed"
                );
                exit.write(AppExit::error());
                return;
            }
        }
    }

    info!(
        target: "forge::proof",
        schema = "forge.runtime-event",
        schema_version = 1_u64,
        event_kind = "bounded_runtime_completed",
        frame = frame.0,
        "bounded runtime completed"
    );
    exit.write(AppExit::Success);
}

fn ensure_resize_confirmed(lifecycle: &WindowLifecycle) -> Result<(), &'static str> {
    if lifecycle.resize_request.is_some()
        && !matches!(
            (
                lifecycle.resize_request_issued_frame,
                lifecycle.resize_request_observed_frame,
            ),
            (Some(issued), Some(observed)) if observed > issued
        )
    {
        return Err("requested resize was not confirmed by a later WindowResized backend event");
    }
    Ok(())
}

fn qualify_gate1_render_state_stability(
    stable_at_measurement_start: Option<bool>,
    stable_for_entire_measurement: bool,
) -> Result<(), String> {
    if stable_at_measurement_start != Some(true) {
        return Err(
            "governed Gate 1 render-state fingerprint was not stable before performance measurement"
                .to_owned(),
        );
    }
    if !stable_for_entire_measurement {
        return Err(
            "governed Gate 1 render-state fingerprint changed during performance measurement"
                .to_owned(),
        );
    }
    Ok(())
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

#[allow(
    clippy::too_many_arguments,
    reason = "governed Gate 1 qualification keeps each independent evidence resource explicit"
)]
fn validate_governed_gate1_evidence(
    samples: &[f64],
    visual_valley: Option<&VisualValleyStats>,
    visual_valley_shadow: Option<&VisualValleyShadowStats>,
    vegetation: Option<&VegetationProjectionStats>,
    scenery: Option<&SceneryProjectionStats>,
    environment: Option<&EnvironmentProjectionStats>,
    water: Option<&WaterProjectionStats>,
    weather: Option<&WeatherProjectionStats>,
    quality: Option<&VisualValleyQualityStats>,
) -> Result<(), String> {
    let visual_valley = visual_valley.ok_or("governed Gate 1 metrics require VisualValleyStats")?;
    let shadow =
        visual_valley_shadow.ok_or("governed Gate 1 metrics require VisualValleyShadowStats")?;
    let vegetation =
        vegetation.ok_or("governed Gate 1 metrics require VegetationProjectionStats")?;
    let scenery = scenery.ok_or("governed Gate 1 metrics require SceneryProjectionStats")?;
    let environment =
        environment.ok_or("governed Gate 1 metrics require EnvironmentProjectionStats")?;
    let water = water.ok_or("governed Gate 1 metrics require WaterProjectionStats")?;
    let weather = weather.ok_or("governed Gate 1 metrics require WeatherProjectionStats")?;
    let quality = quality.ok_or("governed Gate 1 metrics require VisualValleyQualityStats")?;

    validate_gate1_visual_valley_evidence(visual_valley)?;
    validate_gate1_shadow_evidence(shadow)?;
    validate_gate1_vegetation_evidence(vegetation, visual_valley)?;
    validate_gate1_scenery_evidence(scenery)?;
    validate_gate1_environment_evidence(environment)?;
    validate_gate1_water_evidence(water, visual_valley)?;
    validate_gate1_weather_evidence(weather)?;
    validate_gate1_quality_projection_ledger(quality)?;
    validate_gate1_applied_quality_tier(quality)?;
    validate_gate1_scenery_quality_projection(scenery, quality)?;
    validate_gate1_shadow_quality_projection(shadow, quality)?;
    validate_gate1_sample_count(samples)
}

fn validate_gate1_visual_valley_evidence(visual_valley: &VisualValleyStats) -> Result<(), String> {
    if visual_valley.source_hash.is_empty()
        || visual_valley.chunks == 0
        || visual_valley.vertices == 0
        || visual_valley.triangles == 0
        || visual_valley.material_layers < 3
        || visual_valley.lod_levels == 0
        || visual_valley.lod_zero_chunks == 0
        || visual_valley.lod_zero_chunks > visual_valley.chunks
    {
        return Err(format!(
            "governed Gate 1 Visual Valley evidence is not qualified: source_hash_present={}, chunks={}, vertices={}, triangles={}, material_layers={}, lod_levels={}, lod_zero_chunks={}",
            !visual_valley.source_hash.is_empty(),
            visual_valley.chunks,
            visual_valley.vertices,
            visual_valley.triangles,
            visual_valley.material_layers,
            visual_valley.lod_levels,
            visual_valley.lod_zero_chunks
        ));
    }
    Ok(())
}

fn validate_gate1_shadow_evidence(shadow: &VisualValleyShadowStats) -> Result<(), String> {
    let shadow_maximum_distance_m = shadow
        .configured_maximum_distance_m
        .filter(|distance| distance.is_finite() && *distance > 0.0);
    let shadow_bounds_are_finite_and_ordered = !shadow.cascade_bounds_m.is_empty()
        && shadow
            .cascade_bounds_m
            .iter()
            .all(|bound| bound.is_finite() && *bound > 0.0)
        && shadow
            .cascade_bounds_m
            .windows(2)
            .all(|bounds| bounds[0] < bounds[1])
        && shadow_maximum_distance_m.is_some_and(|maximum| {
            shadow
                .cascade_bounds_m
                .last()
                .is_some_and(|last| last.to_bits() == maximum.to_bits())
        });
    if shadow.status != VisualValleyShadowObservationStatus::ObservedSingleSun
        || shadow.sun_entities != 1
        || shadow.shadow_maps_enabled != Some(true)
        || !shadow_bounds_are_finite_and_ordered
    {
        return Err(format!(
            "governed Gate 1 shadow evidence is not qualified: status={:?}, suns={}, enabled={:?}, maximum={:?}, cascades={:?}",
            shadow.status,
            shadow.sun_entities,
            shadow.shadow_maps_enabled,
            shadow.configured_maximum_distance_m,
            shadow.cascade_bounds_m
        ));
    }
    Ok(())
}

fn validate_gate1_vegetation_evidence(
    vegetation: &VegetationProjectionStats,
    visual_valley: &VisualValleyStats,
) -> Result<(), String> {
    if vegetation.terrain_document_hash != visual_valley.source_hash
        || vegetation.vegetation_document_hash.is_empty()
        || vegetation.placements.accepted_placements == 0
        || vegetation.projection.total_entities == 0
        || vegetation.projection.placement_entities == 0
        || vegetation.projection.near_entities == 0
        || vegetation.projection.medium_entities == 0
        || vegetation.projection.grass_batches == 0
        || vegetation.projection.hlod_batches == 0
        || vegetation.visibility.status
            != VegetationVisibilityObservationStatus::ObservedSingleActiveCamera
        || vegetation.visibility.observation_frames == 0
        || vegetation.visibility.active_camera_views != 1
        || vegetation.visibility.visible_outside_range_inconsistencies != 0
    {
        return Err(format!(
            "governed Gate 1 vegetation evidence is not qualified: terrain_hash_match={}, vegetation_hash_present={}, placements={}, projection=total:{}, placements:{}, near:{}, medium:{}, grass:{}, hlod:{}, visibility={:?}, frames={}, cameras={}, inconsistencies={}",
            vegetation.terrain_document_hash == visual_valley.source_hash,
            !vegetation.vegetation_document_hash.is_empty(),
            vegetation.placements.accepted_placements,
            vegetation.projection.total_entities,
            vegetation.projection.placement_entities,
            vegetation.projection.near_entities,
            vegetation.projection.medium_entities,
            vegetation.projection.grass_batches,
            vegetation.projection.hlod_batches,
            vegetation.visibility.status,
            vegetation.visibility.observation_frames,
            vegetation.visibility.active_camera_views,
            vegetation.visibility.visible_outside_range_inconsistencies
        ));
    }
    Ok(())
}

fn validate_gate1_scenery_evidence(scenery: &SceneryProjectionStats) -> Result<(), String> {
    if scenery.status != SceneryProjectionStatus::Activated
        || scenery.failed_models != 0
        || scenery.unique_models == 0
        || scenery.loaded_models != scenery.unique_models
        || scenery.replacement_instances == 0
        || scenery.hidden_procedural_entities != scenery.replacement_instances
        || scenery.tracked_files == 0
        || scenery.tracked_bytes == 0
        || !scenery.replacement_lod_start_m.is_finite()
        || !scenery.replacement_lod_end_m.is_finite()
        || !scenery.detail_cull_distance_m.is_finite()
        || scenery.replacement_lod_start_m <= 0.0
        || scenery.replacement_lod_end_m < scenery.replacement_lod_start_m
        || scenery.detail_cull_distance_m <= 0.0
    {
        return Err(format!(
            "Visual Valley scenery is not qualified for metrics: status={:?}, models={}/{}, failures={}, hidden/replacements={}/{}, tracked={}/{}",
            scenery.status,
            scenery.loaded_models,
            scenery.unique_models,
            scenery.failed_models,
            scenery.hidden_procedural_entities,
            scenery.replacement_instances,
            scenery.tracked_files,
            scenery.tracked_bytes
        ));
    }
    Ok(())
}

fn validate_gate1_environment_evidence(
    environment: &EnvironmentProjectionStats,
) -> Result<(), String> {
    if environment.status != EnvironmentProjectionStatus::ObservedApproximation {
        return Err(format!(
            "governed Gate 1 environment evidence is not qualified: status={:?}",
            environment.status
        ));
    }
    Ok(())
}

fn validate_gate1_water_evidence(
    water: &WaterProjectionStats,
    visual_valley: &VisualValleyStats,
) -> Result<(), String> {
    if water.runtime_observation != WaterRuntimeObservationStatus::ObservedMainWorldComponents
        || water.terrain_source_hash != visual_valley.source_hash
    {
        return Err(format!(
            "governed Gate 1 water evidence is not qualified: status={:?}, terrain_hash_match={}",
            water.runtime_observation,
            water.terrain_source_hash == visual_valley.source_hash
        ));
    }
    Ok(())
}

fn validate_gate1_weather_evidence(weather: &WeatherProjectionStats) -> Result<(), String> {
    if weather.status != WeatherProjectionStatus::ObservedCpuProjection {
        return Err(format!(
            "governed Gate 1 weather evidence is not qualified: status={:?}",
            weather.status
        ));
    }
    Ok(())
}

fn validate_gate1_quality_projection_ledger(
    quality: &VisualValleyQualityStats,
) -> Result<(), String> {
    let quality_fields_match_contract = quality.field_projections.len()
        == QUALITY_FIELD_PROJECTION_CONTRACT.len()
        && quality
            .field_projections
            .iter()
            .zip(QUALITY_FIELD_PROJECTION_CONTRACT)
            .all(|(observed, (expected_field, expected_status))| {
                observed.field == expected_field && observed.status == expected_status
            });
    let quality_fields_are_unique_and_described =
        quality
            .field_projections
            .iter()
            .enumerate()
            .all(|(index, projection)| {
                !projection.field.is_empty()
                    && !projection.evidence.is_empty()
                    && !quality.field_projections[..index]
                        .iter()
                        .any(|previous| previous.field == projection.field)
            });
    let startup_projection_count = quality
        .field_projections
        .iter()
        .filter(|projection| projection.status == QualityFieldProjectionStatus::AppliedAtStartup)
        .count();
    let transition_projection_count = quality
        .field_projections
        .iter()
        .filter(|projection| {
            projection.status == QualityFieldProjectionStatus::AppliedOnTierTransition
        })
        .count();
    let not_applicable_projection_count = quality
        .field_projections
        .iter()
        .filter(|projection| projection.status == QualityFieldProjectionStatus::NotApplicable)
        .count();
    if quality.schema != "forge.visual-valley-quality-runtime"
        || quality.schema_version != 1
        || quality.profile_hash.is_empty()
        || !quality_fields_match_contract
        || !quality_fields_are_unique_and_described
        || startup_projection_count != 5
        || transition_projection_count != 15
        || not_applicable_projection_count != 14
    {
        return Err(format!(
            "governed Gate 1 quality projection ledger is not qualified: schema={} v{}, profile_hash_present={}, fields={}/{}, exact_contract={}, unique_and_described={}, statuses=startup:{}, transition:{}, not_applicable:{}",
            quality.schema,
            quality.schema_version,
            !quality.profile_hash.is_empty(),
            quality.field_projections.len(),
            QUALITY_FIELD_PROJECTION_CONTRACT.len(),
            quality_fields_match_contract,
            quality_fields_are_unique_and_described,
            startup_projection_count,
            transition_projection_count,
            not_applicable_projection_count
        ));
    }
    Ok(())
}

fn validate_gate1_applied_quality_tier(quality: &VisualValleyQualityStats) -> Result<(), String> {
    if quality.dynamically_applied_tier != Some(quality.current_tier) {
        return Err(format!(
            "Visual Valley quality tier {:?} is not applied before metrics",
            quality.current_tier
        ));
    }
    Ok(())
}

fn validate_gate1_scenery_quality_projection(
    scenery: &SceneryProjectionStats,
    quality: &VisualValleyQualityStats,
) -> Result<(), String> {
    let expected_lod_start_m = quality.requested_preset.vegetation.medium_lod_distance_m;
    let expected_lod_end_m = quality.requested_preset.vegetation.hlod_distance_m;
    if scenery.replacement_lod_start_m.to_bits() != expected_lod_start_m.to_bits()
        || scenery.replacement_lod_end_m.to_bits() != expected_lod_end_m.to_bits()
    {
        return Err(format!(
            "Visual Valley scenery LOD range {:.1}-{:.1} m does not match applied {:?} quality {:.1}-{:.1} m",
            scenery.replacement_lod_start_m,
            scenery.replacement_lod_end_m,
            quality.current_tier,
            expected_lod_start_m,
            expected_lod_end_m
        ));
    }
    Ok(())
}

fn validate_gate1_shadow_quality_projection(
    shadow: &VisualValleyShadowStats,
    quality: &VisualValleyQualityStats,
) -> Result<(), String> {
    let expected_shadow_maximum_distance_m = quality.requested_preset.shadows.maximum_distance_m;
    let expected_shadow_cascade_count = usize::from(quality.requested_preset.shadows.cascade_count);
    if shadow
        .configured_maximum_distance_m
        .is_none_or(|distance| distance.to_bits() != expected_shadow_maximum_distance_m.to_bits())
        || shadow.cascade_bounds_m.len() != expected_shadow_cascade_count
    {
        return Err(format!(
            "Visual Valley shadow projection does not match applied {:?} quality: maximum={:?}/{:.1}, cascades={}/{}",
            quality.current_tier,
            shadow.configured_maximum_distance_m,
            expected_shadow_maximum_distance_m,
            shadow.cascade_bounds_m.len(),
            expected_shadow_cascade_count
        ));
    }
    Ok(())
}

fn validate_gate1_sample_count(samples: &[f64]) -> Result<(), String> {
    if samples.len() < GATE1_MINIMUM_FRAME_SAMPLES {
        return Err(format!(
            "governed Gate 1 metrics require at least {GATE1_MINIMUM_FRAME_SAMPLES} post-warmup frame samples, observed {}",
            samples.len()
        ));
    }
    Ok(())
}

#[allow(
    clippy::too_many_arguments,
    reason = "the metrics serializer combines independent runtime evidence without hidden globals"
)]
fn build_runtime_metrics(
    samples: &[f64],
    stats: &ProofSceneStats,
    governed_gate1_required: bool,
    visual_valley: Option<&VisualValleyStats>,
    visual_valley_shadow: Option<&VisualValleyShadowStats>,
    vegetation: Option<&VegetationProjectionStats>,
    scenery: Option<&SceneryProjectionStats>,
    environment: Option<&EnvironmentProjectionStats>,
    water: Option<&WaterProjectionStats>,
    weather: Option<&WeatherProjectionStats>,
    quality: Option<&VisualValleyQualityStats>,
    gpu: Option<&GpuDiagnostic>,
    gpu_memory: &GpuMemoryObservation,
    pipeline_warmup: &PipelineWarmupReport,
    diagnostics: &DiagnosticsStore,
    lifecycle: &WindowLifecycle,
    final_window: Option<WindowStateReport>,
) -> Result<RuntimeMetricsReport, String> {
    if governed_gate1_required {
        validate_governed_gate1_evidence(
            samples,
            visual_valley,
            visual_valley_shadow,
            vegetation,
            scenery,
            environment,
            water,
            weather,
            quality,
        )?;
    }
    let distribution = CoreFrameDistribution::from_samples_ms(samples)
        .map_err(|error| format!("invalid post-warmup frame samples: {error}"))?;
    let mut sorted = samples.to_vec();
    sorted.sort_by(f64::total_cmp);
    let histogram = distribution.histogram;
    let hitches_over_16_7_ms = usize::try_from(
        u64::from(histogram.frames_over_16_7_ms_at_or_below_33_3_ms)
            + u64::from(histogram.frames_over_33_3_ms),
    )
    .map_err(|_| "60 FPS hitch count exceeds the platform ledger size".to_owned())?;
    let hitches_over_33_3_ms = usize::try_from(histogram.frames_over_33_3_ms)
        .map_err(|_| "30 FPS hitch count exceeds the platform ledger size".to_owned())?;
    let frame = FrameMetrics {
        samples: samples.len(),
        duration_ms: distribution.duration_ms,
        mean_ms: distribution.mean_ms,
        p50_ms: percentile(&sorted, 50, 100),
        p95_ms: distribution.p95_ms,
        p99_ms: percentile(&sorted, 99, 100),
        max_ms: *sorted.last().ok_or("missing sorted frame sample")?,
        mean_fps: distribution.mean_fps,
        one_percent_low_fps: distribution.one_percent_low_fps,
        hitches_over_16_7_ms,
        hitches_over_33_3_ms,
        histogram,
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
        schema_version: 8,
        source_hash: stats.source_hash.clone(),
        gpu: gpu.cloned(),
        gpu_memory: gpu_memory.clone(),
        pipeline_warmup: pipeline_warmup.clone(),
        window: lifecycle.report(final_window),
        frame,
        render_diagnostics,
        render_work: build_render_work_report(stats, diagnostics),
        visual_valley: visual_valley.cloned(),
        visual_valley_shadow: visual_valley_shadow.cloned(),
        vegetation: vegetation.cloned(),
        scenery: scenery.cloned(),
        environment: environment.cloned(),
        water: water.cloned(),
        weather: weather.cloned(),
        quality: quality.cloned(),
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

    fn bounded_metrics_automation(warmup: u32, last_frame: Option<u32>) -> RuntimeAutomation {
        RuntimeAutomation {
            metrics_path: Some(PathBuf::from("frame-ledger-test.json")),
            metrics_warmup_frames: warmup,
            exit_after_frames: last_frame,
            ..default()
        }
    }

    fn sample_gpu_memory() -> GpuMemoryObservation {
        GpuMemoryObservation {
            wgpu_allocator_report_status: WgpuAllocatorReportStatus::UnsupportedOrUnavailable,
            wgpu_allocator_allocated_bytes: None,
            wgpu_allocator_reserved_bytes: None,
            mesh_allocator_slabs_bytes: Some(4_096),
            wgpu_allocator_scope: "current_wgpu_device_suballocations_not_total_vram_or_os_budget",
            mesh_allocator_scope: "bevy_mesh_allocator_slab_capacity_only_not_total_gpu_memory",
        }
    }

    fn sample_pipeline_warmup() -> PipelineWarmupReport {
        PipelineWarmupReport {
            ready_before_measurement: true,
            ready_for_entire_measurement: true,
            final_observation: PipelineWarmupObservation {
                status: PipelineWarmupStatus::Ready,
                total: 12,
                queued: 0,
                creating: 0,
                ready: 12,
                errors: 0,
                waiting: 0,
                stable_frames: PIPELINE_WARMUP_STABLE_FRAMES,
                required_stable_frames: PIPELINE_WARMUP_STABLE_FRAMES,
                observed_render_frames: 42,
                stabilized_at_render_frame: Some(42),
                scope: "pipelines_exercised_by_this_process_only_no_persistent_pipeline_cache",
            },
        }
    }

    fn sample_gate1_render_state(
        tier: forge_project::VisualQualityTier,
        lod_start_m: f32,
        lod_end_m: f32,
    ) -> Gate1RenderState {
        Gate1RenderState {
            quality_current_tier: Some(tier),
            quality_applied_tier: Some(tier),
            scenery_status: Some(SceneryProjectionStatus::Activated),
            scenery_loaded_models: Some(9),
            scenery_failed_models: Some(0),
            scenery_hidden_procedural_entities: Some(128),
            scenery_replacement_instances: Some(128),
            scenery_lod_start_bits: Some(lod_start_m.to_bits()),
            scenery_lod_end_bits: Some(lod_end_m.to_bits()),
        }
    }

    fn sample_environment_stats() -> EnvironmentProjectionStats {
        EnvironmentProjectionStats {
            source_hash: "environment".to_owned(),
            status: EnvironmentProjectionStatus::ObservedApproximation,
            evidence_source: "main_world_components_not_gpu_execution",
            camera_3d_entities: 1,
            active_camera_3d_entities: 1,
            directional_light_entities: 1,
            visual_valley_sun_entities: 1,
            atmosphere_entities: 1,
            fog_volume_entities: 0,
            sky: EnvironmentSkyStats {
                requested: true,
                atmosphere_settings: true,
                hdr: true,
                exposure_ev100: Some(12.0),
                tonemapping: Some("tony_mc_mapface"),
                ambient_light: true,
                aerial_view_max_distance_m: 1_600.0,
            },
            sun: EnvironmentSunStats {
                illuminance_lux: Some(55_000.0),
                color_linear_rgba: Some([1.0, 0.85, 0.57, 1.0]),
                forward_world: Some([0.62, -0.62, -0.48]),
                shadows_enabled: Some(true),
                sun_disk: true,
            },
            distance_fog: EnvironmentDistanceFogStats {
                requested: true,
                component_present: true,
                falloff_kind: "atmospheric",
                measurement_distance_m: 640.0,
                maximum_opacity: 0.82,
            },
            volumetric: EnvironmentVolumetricStats {
                requested: true,
                applied: false,
                resolved_path: "distance_fog_fallback",
                requested_step_count: 64,
                fallback: "distance_fog",
            },
        }
    }

    fn sample_activated_scenery(
        terrain: &forge_project::TerrainDocument,
        vegetation: &forge_project::VegetationDocument,
    ) -> SceneryProjectionStats {
        let mut scenery = inspect_scenery_projection(terrain, vegetation).unwrap();
        scenery.loaded_models = scenery.unique_models;
        scenery.hidden_procedural_entities = scenery.replacement_instances;
        scenery.status = SceneryProjectionStatus::Activated;
        scenery
    }

    fn sample_applied_quality() -> VisualValleyQualityStats {
        let profile = forge_project::VisualValleyQualityProfileDocument::from_json(include_str!(
            "../../../examples/visual_valley/quality-profile.v1.json"
        ))
        .unwrap();
        let tier = forge_project::VisualQualityTier::High;
        let mut app = App::new();
        app.add_plugins(VisualValleyQualityPlugin::new(
            profile,
            VisualValleyQualitySelection::Fixed(tier),
        ));
        app.update();
        app.world().resource::<VisualValleyQualityStats>().clone()
    }

    fn sample_observed_water(terrain: forge_project::TerrainDocument) -> WaterProjectionStats {
        let water = forge_world::embedded_visual_valley_water_document().unwrap();
        let mut app = App::new();
        app.add_plugins((MinimalPlugins, bevy::asset::AssetPlugin::default()));
        app.init_asset::<Mesh>();
        app.init_asset::<StandardMaterial>();
        app.world_mut().spawn(Camera3d::default());
        app.add_plugins(VisualValleyWaterPlugin::new(terrain, water));
        app.update();
        app.world().resource::<WaterProjectionStats>().clone()
    }

    fn sample_observed_weather() -> WeatherProjectionStats {
        let mut app = App::new();
        app.add_plugins((MinimalPlugins, bevy::asset::AssetPlugin::default()));
        app.init_asset::<Mesh>();
        app.init_asset::<VisualValleyTerrainMaterial>();
        app.init_asset::<VisualValleyVegetationMaterial>();
        let _terrain_material = app
            .world_mut()
            .resource_mut::<Assets<VisualValleyTerrainMaterial>>()
            .add(VisualValleyTerrainMaterial {
                base: StandardMaterial::default(),
                extension: VisualValleyTerrainExtension::default(),
            });
        let _vegetation_material = app
            .world_mut()
            .resource_mut::<Assets<VisualValleyVegetationMaterial>>()
            .add(VisualValleyVegetationMaterial {
                base: StandardMaterial::default(),
                extension: VisualValleyVegetationExtension::default(),
            });
        app.world_mut().spawn((
            Camera3d::default(),
            Camera::default(),
            Transform::from_xyz(3.0, 4.0, 5.0),
            GlobalTransform::from_xyz(3.0, 4.0, 5.0),
        ));
        app.add_plugins(
            VisualValleyWeatherPlugin::new(
                forge_world::embedded_visual_valley_weather_profile().unwrap(),
            )
            .with_initial_state(forge_project::WeatherState::Rain),
        );
        app.update();
        app.world().resource::<WeatherProjectionStats>().clone()
    }

    struct SampleGate1Evidence {
        visual_valley: VisualValleyStats,
        shadow: VisualValleyShadowStats,
        vegetation: VegetationProjectionStats,
        scenery: SceneryProjectionStats,
        environment: EnvironmentProjectionStats,
        water: WaterProjectionStats,
        weather: WeatherProjectionStats,
        quality: VisualValleyQualityStats,
    }

    fn sample_gate1_evidence() -> SampleGate1Evidence {
        let terrain = forge_world::embedded_visual_valley_terrain_document().unwrap();
        let vegetation_document =
            forge_world::embedded_visual_valley_vegetation_document().unwrap();
        let mut vegetation = inspect_vegetation_projection(&terrain, &vegetation_document).unwrap();
        vegetation.visibility.status =
            VegetationVisibilityObservationStatus::ObservedSingleActiveCamera;
        vegetation.visibility.observation_frames = 1;
        vegetation.visibility.active_camera_views = 1;
        let scenery = sample_activated_scenery(&terrain, &vegetation_document);
        let quality = sample_applied_quality();
        let visual_valley = VisualValleyStats {
            source_hash: vegetation.terrain_document_hash.clone(),
            chunks: 64,
            vertices: 69_696,
            triangles: 131_072,
            material_layers: 3,
            lod_levels: 1,
            lod_zero_chunks: 64,
        };
        let shadow = VisualValleyShadowStats {
            status: VisualValleyShadowObservationStatus::ObservedSingleSun,
            sun_entities: 1,
            shadow_maps_enabled: Some(true),
            configured_maximum_distance_m: Some(
                quality.requested_preset.shadows.maximum_distance_m,
            ),
            cascade_bounds_m: vec![24.0, 48.0, 80.0, 128.0],
            evidence_source: "visual_valley_sun_cascade_shadow_component",
        };
        let water = sample_observed_water(terrain);
        let weather = sample_observed_weather();
        SampleGate1Evidence {
            visual_valley,
            shadow,
            vegetation,
            scenery,
            environment: sample_environment_stats(),
            water,
            weather,
            quality,
        }
    }

    fn build_sample_gate1_metrics(
        samples: &[f64],
        stats: &ProofSceneStats,
        evidence: &SampleGate1Evidence,
    ) -> Result<RuntimeMetricsReport, String> {
        let gpu_memory = sample_gpu_memory();
        let pipeline_warmup = sample_pipeline_warmup();
        build_runtime_metrics(
            samples,
            stats,
            true,
            Some(&evidence.visual_valley),
            Some(&evidence.shadow),
            Some(&evidence.vegetation),
            Some(&evidence.scenery),
            Some(&evidence.environment),
            Some(&evidence.water),
            Some(&evidence.weather),
            Some(&evidence.quality),
            None,
            &gpu_memory,
            &pipeline_warmup,
            &DiagnosticsStore::default(),
            &WindowLifecycle::new(None, None),
            None,
        )
    }

    fn validate_sample_gate1_evidence(evidence: &SampleGate1Evidence) -> Result<(), String> {
        validate_governed_gate1_evidence(
            &[8.0; GATE1_MINIMUM_FRAME_SAMPLES],
            Some(&evidence.visual_valley),
            Some(&evidence.shadow),
            Some(&evidence.vegetation),
            Some(&evidence.scenery),
            Some(&evidence.environment),
            Some(&evidence.water),
            Some(&evidence.weather),
            Some(&evidence.quality),
        )
    }

    #[test]
    fn frame_ledger_is_disabled_without_both_metrics_and_a_bounded_exit() {
        let mut without_metrics = FrameLedger::for_automation(&RuntimeAutomation {
            metrics_warmup_frames: 2,
            exit_after_frames: Some(8),
            ..default()
        });
        let mut without_exit = FrameLedger::for_automation(&bounded_metrics_automation(2, None));

        for frame in 0..=32 {
            without_metrics.record(frame, 8.0, true, None);
            without_exit.record(frame, 8.0, true, None);
        }

        assert!(without_metrics.samples_ms.is_empty());
        assert!(without_exit.samples_ms.is_empty());
        assert_eq!(without_metrics.sample_limit, 0);
        assert_eq!(without_exit.sample_limit, 0);
    }

    #[test]
    fn frame_ledger_records_the_exact_post_warmup_bounded_window_once() {
        let mut ledger = FrameLedger::for_automation(&bounded_metrics_automation(2, Some(5)));

        ledger.record(2, 9.0, false, None);
        ledger.record(3, 10.0, true, None);
        ledger.record(3, 11.0, true, None);
        ledger.record(4, f64::NAN, true, None);
        ledger.record(4, 12.0, true, None);
        ledger.record(5, 14.0, true, None);
        ledger.record(6, 16.0, false, None);

        assert_eq!(ledger.first_frame, Some(3));
        assert_eq!(ledger.last_frame, Some(5));
        assert_eq!(ledger.sample_limit, 3);
        assert_eq!(ledger.samples_ms, vec![10.0, 12.0, 14.0]);
        assert_eq!(ledger.pipeline_ready_at_measurement_start, Some(true));
        assert!(ledger.pipeline_ready_for_entire_measurement);
    }

    #[test]
    fn frame_ledger_detects_pipeline_warmup_during_measurement() {
        let mut ledger = FrameLedger::for_automation(&bounded_metrics_automation(2, Some(5)));

        ledger.record(3, 10.0, true, None);
        ledger.record(4, 11.0, false, None);
        ledger.record(5, 12.0, true, None);

        assert_eq!(ledger.pipeline_ready_at_measurement_start, Some(true));
        assert!(!ledger.pipeline_ready_for_entire_measurement);
        assert_eq!(
            ledger.gate1_render_state_stable_at_measurement_start,
            Some(true)
        );
        assert!(ledger.gate1_render_state_stable_for_entire_measurement);
    }

    #[test]
    fn frame_ledger_rejects_a_gate1_render_state_transition_before_the_next_render_observation() {
        let mut ledger = FrameLedger::for_automation(&bounded_metrics_automation(2, Some(5)));
        let high = sample_gate1_render_state(forge_project::VisualQualityTier::High, 72.0, 180.0);
        let low = sample_gate1_render_state(forge_project::VisualQualityTier::Low, 48.0, 128.0);

        ledger.record(2, 9.0, true, Some(high));
        ledger.record(3, 10.0, true, Some(high));
        ledger.record(4, 11.0, true, Some(low));
        ledger.record(5, 12.0, true, Some(low));

        assert_eq!(ledger.pipeline_ready_at_measurement_start, Some(true));
        assert!(ledger.pipeline_ready_for_entire_measurement);
        assert_eq!(
            ledger.gate1_render_state_stable_at_measurement_start,
            Some(true)
        );
        assert!(!ledger.gate1_render_state_stable_for_entire_measurement);
        let error = qualify_gate1_render_state_stability(
            ledger.gate1_render_state_stable_at_measurement_start,
            ledger.gate1_render_state_stable_for_entire_measurement,
        )
        .unwrap_err();
        assert_eq!(
            error,
            "governed Gate 1 render-state fingerprint changed during performance measurement"
        );
    }

    #[test]
    fn frame_ledger_reports_a_gate1_fingerprint_that_was_not_stable_before_measurement() {
        let mut ledger = FrameLedger::for_automation(&bounded_metrics_automation(2, Some(4)));
        let high = sample_gate1_render_state(forge_project::VisualQualityTier::High, 72.0, 180.0);

        ledger.record(3, 10.0, true, Some(high));
        ledger.record(4, 11.0, true, Some(high));

        assert_eq!(ledger.pipeline_ready_at_measurement_start, Some(true));
        assert!(ledger.pipeline_ready_for_entire_measurement);
        assert_eq!(
            ledger.gate1_render_state_stable_at_measurement_start,
            Some(false)
        );
        let error = qualify_gate1_render_state_stability(
            ledger.gate1_render_state_stable_at_measurement_start,
            ledger.gate1_render_state_stable_for_entire_measurement,
        )
        .unwrap_err();
        assert_eq!(
            error,
            "governed Gate 1 render-state fingerprint was not stable before performance measurement"
        );
    }

    #[test]
    fn frame_ledger_hard_cap_bounds_extreme_automation_requests() {
        let ledger = FrameLedger::for_automation(&bounded_metrics_automation(0, Some(u32::MAX)));

        assert_eq!(ledger.sample_limit, MAX_FRAME_LEDGER_SAMPLES);
        assert_eq!(ledger.first_frame, Some(1));
        assert_eq!(ledger.last_frame, Some(u32::MAX));
    }

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
            source_mesh_instances: 2,
            source_hash: "abc".to_owned(),
        };
        let diagnostics = DiagnosticsStore::default();
        let lifecycle = WindowLifecycle::new(None, None);
        let gpu_memory = sample_gpu_memory();
        let pipeline_warmup = sample_pipeline_warmup();
        let report = build_runtime_metrics(
            &samples,
            &stats,
            false,
            None,
            None,
            None,
            None,
            None,
            None,
            None,
            None,
            None,
            &gpu_memory,
            &pipeline_warmup,
            &diagnostics,
            &lifecycle,
            None,
        )
        .unwrap();
        assert_eq!(report.schema_version, 8);
        assert_eq!(report.gpu_memory, gpu_memory);
        assert_eq!(report.pipeline_warmup, pipeline_warmup);
        assert!(report.visual_valley.is_none());
        assert!(report.visual_valley_shadow.is_none());
        assert!(report.vegetation.is_none());
        assert!(report.environment.is_none());
        assert!(report.water.is_none());
        assert!(report.weather.is_none());
        assert!(report.quality.is_none());
        assert!(
            serde_json::to_value(&report)
                .unwrap()
                .get("visual_valley")
                .is_none()
        );
        assert_eq!(report.frame.samples, 200);
        assert!((report.frame.p50_ms - 100.0).abs() < f64::EPSILON);
        assert!((report.frame.p95_ms - 190.0).abs() < f64::EPSILON);
        assert!((report.frame.p99_ms - 198.0).abs() < f64::EPSILON);
        assert!((report.frame.max_ms - 200.0).abs() < f64::EPSILON);
        assert!((report.frame.one_percent_low_fps - (1_000.0 / 199.5)).abs() < f64::EPSILON);
        assert_eq!(report.frame.hitches_over_16_7_ms, 184);
        assert_eq!(report.frame.hitches_over_33_3_ms, 167);
        assert_eq!(
            report.frame.histogram,
            FrameTimeHistogram {
                frames_at_or_below_16_7_ms: 16,
                frames_over_16_7_ms_at_or_below_33_3_ms: 17,
                frames_over_33_3_ms: 167,
            }
        );
        assert_eq!(report.frame.histogram.total_samples(), 200);
        let serialized = serde_json::to_value(&report).unwrap();
        assert_eq!(
            serialized["frame"]["histogram"]["frames_at_or_below_16_7_ms"],
            16
        );
    }

    #[test]
    fn terrain_only_metrics_do_not_invent_the_full_governed_gate1_contract() {
        let stats = ProofSceneStats {
            source_entities: 1,
            source_mesh_instances: 1,
            source_hash: "terrain".to_owned(),
        };
        let visual_valley = VisualValleyStats {
            source_hash: "terrain".to_owned(),
            chunks: 1,
            vertices: 3,
            triangles: 1,
            material_layers: 3,
            lod_levels: 1,
            lod_zero_chunks: 1,
        };
        let gpu_memory = sample_gpu_memory();
        let pipeline_warmup = sample_pipeline_warmup();

        let report = build_runtime_metrics(
            &[8.0],
            &stats,
            false,
            Some(&visual_valley),
            None,
            None,
            None,
            None,
            None,
            None,
            None,
            None,
            &gpu_memory,
            &pipeline_warmup,
            &DiagnosticsStore::default(),
            &WindowLifecycle::new(None, None),
            None,
        )
        .unwrap();

        assert_eq!(report.visual_valley, Some(visual_valley));
        assert!(report.scenery.is_none());
        assert!(report.quality.is_none());
        assert_eq!(report.frame.samples, 1);
    }

    #[test]
    fn gate1_metrics_preserve_machine_readable_scene_projections() {
        let stats = ProofSceneStats {
            source_entities: 1,
            source_mesh_instances: 64,
            source_hash: "terrain".to_owned(),
        };
        let evidence = sample_gate1_evidence();
        let insufficient_window =
            build_sample_gate1_metrics(&[8.0; GATE1_MINIMUM_FRAME_SAMPLES - 1], &stats, &evidence)
                .unwrap_err();
        assert!(
            insufficient_window.contains("at least 120 post-warmup frame samples"),
            "unexpected governed Gate 1 qualification error: {insufficient_window}"
        );

        let report =
            build_sample_gate1_metrics(&[8.0; GATE1_MINIMUM_FRAME_SAMPLES], &stats, &evidence)
                .unwrap();

        assert_eq!(report.visual_valley.as_ref(), Some(&evidence.visual_valley));
        assert_eq!(report.visual_valley_shadow.as_ref(), Some(&evidence.shadow));
        assert_eq!(report.vegetation.as_ref(), Some(&evidence.vegetation));
        assert_eq!(
            serde_json::to_value(report.scenery.as_ref().unwrap()).unwrap(),
            serde_json::to_value(&evidence.scenery).unwrap()
        );
        assert_eq!(report.environment.as_ref(), Some(&evidence.environment));
        assert_eq!(report.water.as_ref(), Some(&evidence.water));
        assert_eq!(report.weather.as_ref(), Some(&evidence.weather));
        assert_eq!(
            serde_json::to_value(report.quality.as_ref().unwrap()).unwrap(),
            serde_json::to_value(&evidence.quality).unwrap()
        );
        let serialized = serde_json::to_value(&report).unwrap();
        assert_eq!(serialized["visual_valley"]["chunks"], 64);
        assert_eq!(serialized["visual_valley"]["vertices"], 69_696);
        assert_eq!(serialized["visual_valley"]["triangles"], 131_072);
        assert_eq!(
            serialized["vegetation"]["placements"]["accepted_placements"],
            8_282
        );
        assert_eq!(serialized["vegetation"]["projection"]["grass_batches"], 64);
        assert_eq!(serialized["scenery"]["status"], "activated");
        assert_eq!(serialized["quality"]["current_tier"], "high");
        assert_eq!(
            serialized["visual_valley_shadow"]["configured_maximum_distance_m"],
            128.0
        );
        assert!(serialized["vegetation"].get("draw_calls").is_none());
        assert_eq!(
            serialized["environment"]["status"],
            "observed_approximation"
        );
        assert_eq!(
            serialized["environment"]["volumetric"]["resolved_path"],
            "distance_fog_fallback"
        );
        assert_eq!(
            serialized["water"]["runtime_observation"],
            "observed_main_world_components"
        );
        assert_eq!(serialized["weather"]["status"], "observed_cpu_projection");
    }

    #[test]
    fn gate1_metrics_reject_scenery_fallback_and_unapplied_quality() {
        let stats = ProofSceneStats {
            source_entities: 1,
            source_mesh_instances: 1,
            source_hash: "terrain".to_owned(),
        };
        let mut evidence = sample_gate1_evidence();
        let qualified_scenery = evidence.scenery.clone();
        evidence.scenery.status = SceneryProjectionStatus::ProceduralFallbackLoadFailure;
        evidence.scenery.failed_models = 1;
        evidence.scenery.loaded_models = evidence.scenery.unique_models.saturating_sub(1);

        let scenery_error =
            build_sample_gate1_metrics(&[8.0; GATE1_MINIMUM_FRAME_SAMPLES], &stats, &evidence)
                .unwrap_err();
        assert!(
            scenery_error.contains("scenery is not qualified"),
            "unexpected governed Gate 1 qualification error: {scenery_error}"
        );

        evidence.scenery = qualified_scenery.clone();
        evidence.scenery.loaded_models = 0;
        evidence.scenery.unique_models = 0;
        evidence.scenery.hidden_procedural_entities = 0;
        evidence.scenery.replacement_instances = 0;
        let structural_error =
            build_sample_gate1_metrics(&[8.0; GATE1_MINIMUM_FRAME_SAMPLES], &stats, &evidence)
                .unwrap_err();
        assert!(
            structural_error.contains("scenery is not qualified"),
            "unexpected governed Gate 1 qualification error: {structural_error}"
        );

        evidence.scenery = qualified_scenery.clone();
        evidence.quality.dynamically_applied_tier = None;
        let quality_error =
            build_sample_gate1_metrics(&[8.0; GATE1_MINIMUM_FRAME_SAMPLES], &stats, &evidence)
                .unwrap_err();
        assert!(
            quality_error.contains("is not applied before metrics"),
            "unexpected governed Gate 1 qualification error: {quality_error}"
        );

        evidence.quality = sample_applied_quality();
        evidence.scenery = qualified_scenery;
        evidence.scenery.replacement_lod_start_m += 1.0;
        let lod_error =
            build_sample_gate1_metrics(&[8.0; GATE1_MINIMUM_FRAME_SAMPLES], &stats, &evidence)
                .unwrap_err();
        assert!(
            lod_error.contains("does not match applied"),
            "unexpected governed Gate 1 qualification error: {lod_error}"
        );
    }

    #[test]
    fn governed_gate1_metrics_require_every_full_composition_projection() {
        let evidence = sample_gate1_evidence();
        let samples = [8.0; GATE1_MINIMUM_FRAME_SAMPLES];
        let validate = |shadow: Option<&VisualValleyShadowStats>,
                        vegetation: Option<&VegetationProjectionStats>,
                        environment: Option<&EnvironmentProjectionStats>,
                        water: Option<&WaterProjectionStats>,
                        weather: Option<&WeatherProjectionStats>| {
            validate_governed_gate1_evidence(
                &samples,
                Some(&evidence.visual_valley),
                shadow,
                vegetation,
                Some(&evidence.scenery),
                environment,
                water,
                weather,
                Some(&evidence.quality),
            )
        };

        let missing = [
            (
                "VisualValleyShadowStats",
                validate(
                    None,
                    Some(&evidence.vegetation),
                    Some(&evidence.environment),
                    Some(&evidence.water),
                    Some(&evidence.weather),
                ),
            ),
            (
                "VegetationProjectionStats",
                validate(
                    Some(&evidence.shadow),
                    None,
                    Some(&evidence.environment),
                    Some(&evidence.water),
                    Some(&evidence.weather),
                ),
            ),
            (
                "EnvironmentProjectionStats",
                validate(
                    Some(&evidence.shadow),
                    Some(&evidence.vegetation),
                    None,
                    Some(&evidence.water),
                    Some(&evidence.weather),
                ),
            ),
            (
                "WaterProjectionStats",
                validate(
                    Some(&evidence.shadow),
                    Some(&evidence.vegetation),
                    Some(&evidence.environment),
                    None,
                    Some(&evidence.weather),
                ),
            ),
            (
                "WeatherProjectionStats",
                validate(
                    Some(&evidence.shadow),
                    Some(&evidence.vegetation),
                    Some(&evidence.environment),
                    Some(&evidence.water),
                    None,
                ),
            ),
        ];
        for (resource, result) in missing {
            assert_eq!(
                result.unwrap_err(),
                format!("governed Gate 1 metrics require {resource}")
            );
        }
    }

    #[test]
    fn governed_gate1_metrics_reject_unobserved_or_cross_document_evidence() {
        let mut evidence = sample_gate1_evidence();

        evidence.visual_valley.chunks = 0;
        let valley_error = validate_sample_gate1_evidence(&evidence).unwrap_err();
        assert!(valley_error.contains("Visual Valley evidence is not qualified"));
        evidence.visual_valley.chunks = 64;

        evidence.shadow.status = VisualValleyShadowObservationStatus::NotObserved;
        let shadow_error = validate_sample_gate1_evidence(&evidence).unwrap_err();
        assert!(shadow_error.contains("shadow evidence is not qualified"));
        evidence.shadow.status = VisualValleyShadowObservationStatus::ObservedSingleSun;

        evidence.vegetation.visibility.status = VegetationVisibilityObservationStatus::NotObserved;
        let vegetation_error = validate_sample_gate1_evidence(&evidence).unwrap_err();
        assert!(vegetation_error.contains("vegetation evidence is not qualified"));
        evidence.vegetation.visibility.status =
            VegetationVisibilityObservationStatus::ObservedSingleActiveCamera;
        evidence.vegetation.terrain_document_hash = "different-terrain".to_owned();
        let vegetation_hash_error = validate_sample_gate1_evidence(&evidence).unwrap_err();
        assert!(vegetation_hash_error.contains("terrain_hash_match=false"));
        evidence.vegetation.terrain_document_hash = evidence.visual_valley.source_hash.clone();

        evidence.environment.status = EnvironmentProjectionStatus::IncompleteComponents;
        let environment_error = validate_sample_gate1_evidence(&evidence).unwrap_err();
        assert!(environment_error.contains("environment evidence is not qualified"));
        evidence.environment.status = EnvironmentProjectionStatus::ObservedApproximation;

        evidence.water.runtime_observation = WaterRuntimeObservationStatus::NotObserved;
        let water_error = validate_sample_gate1_evidence(&evidence).unwrap_err();
        assert!(water_error.contains("water evidence is not qualified"));
        evidence.water.runtime_observation =
            WaterRuntimeObservationStatus::ObservedMainWorldComponents;
        evidence.water.terrain_source_hash = "different-terrain".to_owned();
        let water_hash_error = validate_sample_gate1_evidence(&evidence).unwrap_err();
        assert!(water_hash_error.contains("terrain_hash_match=false"));
        evidence.water.terrain_source_hash = evidence.visual_valley.source_hash.clone();

        evidence.weather.status = WeatherProjectionStatus::NotObserved;
        let weather_error = validate_sample_gate1_evidence(&evidence).unwrap_err();
        assert!(weather_error.contains("weather evidence is not qualified"));
        evidence.weather.status = WeatherProjectionStatus::ObservedCpuProjection;

        evidence.quality.field_projections.pop();
        let quality_count_error = validate_sample_gate1_evidence(&evidence).unwrap_err();
        assert!(quality_count_error.contains("quality projection ledger is not qualified"));
        evidence.quality = sample_applied_quality();
        let duplicate_field = evidence.quality.field_projections[0].field;
        evidence.quality.field_projections[1].field = duplicate_field;
        let quality_unique_error = validate_sample_gate1_evidence(&evidence).unwrap_err();
        assert!(quality_unique_error.contains("unique_and_described=false"));
        evidence.quality = sample_applied_quality();
        evidence.quality.field_projections[0].field = "vegetation.substituted_field";
        let quality_field_contract_error = validate_sample_gate1_evidence(&evidence).unwrap_err();
        assert!(quality_field_contract_error.contains("exact_contract=false"));
        evidence.quality = sample_applied_quality();
        let transition_status = evidence.quality.field_projections[0].status;
        evidence.quality.field_projections[0].status =
            evidence.quality.field_projections[15].status;
        evidence.quality.field_projections[15].status = transition_status;
        let quality_status_contract_error = validate_sample_gate1_evidence(&evidence).unwrap_err();
        assert!(quality_status_contract_error.contains("exact_contract=false"));
        evidence.quality = sample_applied_quality();
        evidence.quality.profile_hash.clear();
        let quality_hash_error = validate_sample_gate1_evidence(&evidence).unwrap_err();
        assert!(quality_hash_error.contains("profile_hash_present=false"));
    }

    #[test]
    fn render_work_never_invents_gpu_draw_calls() {
        let stats = ProofSceneStats {
            source_entities: 4,
            source_mesh_instances: 2,
            source_hash: "abc".to_owned(),
        };
        let mut diagnostics = DiagnosticsStore::default();
        add_test_diagnostic(
            &mut diagnostics,
            "render/main_opaque_pass_3d/clipper_primitives_out",
            14.0,
        );
        add_test_diagnostic(
            &mut diagnostics,
            "render/main_opaque_pass_3d/vertex_shader_invocations",
            28.0,
        );
        add_test_diagnostic(
            &mut diagnostics,
            "render/main_opaque_pass_3d/fragment_shader_invocations",
            1_086_894.0,
        );
        add_test_diagnostic(
            &mut diagnostics,
            "render/main_opaque_pass_3d/elapsed_cpu",
            0.01,
        );
        let render_work = build_render_work_report(&stats, &diagnostics);

        let text = format_diagnostics_overlay("test adapter", 120.0, 8.3, &stats, &render_work);
        assert!(text.contains("2 source meshes"));
        assert!(text.contains("GPU draw calls: unavailable"));
        assert!(text.contains("GPU opaque: 14 primitives"));
        assert!(!text.contains("draws logiques"));
        assert!(text.is_ascii());

        let visual_valley = VisualValleyStats {
            source_hash: "terrain".to_owned(),
            chunks: 64,
            vertices: 69_696,
            triangles: 131_072,
            material_layers: 3,
            lod_levels: 1,
            lod_zero_chunks: 64,
        };
        let logical_details = format!(
            "{}\n{}",
            visual_valley.overlay_details(),
            VegetationProjectionStats::default().overlay_details()
        );
        let gate1_text = format_runtime_diagnostics_overlay(
            "OpenRPG Forge | Gate 1 / Visual Valley",
            "GPU Etincelle é",
            120.0,
            8.3,
            &stats,
            &render_work,
            Some(&logical_details),
        );
        assert!(gate1_text.is_ascii());
        assert!(gate1_text.contains("3 PBR layers"));
        assert!(gate1_text.contains("grass batches"));
        assert_eq!(render_work.gpu_draw_calls, None);
        assert_eq!(
            render_work.gpu_draw_call_status,
            "unavailable_bevy_0_19_no_global_counter"
        );
        assert_eq!(render_work.gpu_main_opaque_primitives, Some(14.0));
        assert_eq!(render_work.timed_render_spans_observed, 1);
    }

    fn add_test_diagnostic(store: &mut DiagnosticsStore, path: &'static str, value: f64) {
        let mut diagnostic =
            bevy::diagnostic::Diagnostic::new(bevy::diagnostic::DiagnosticPath::const_new(path));
        diagnostic.add_measurement(bevy::diagnostic::DiagnosticMeasurement {
            time: std::time::Instant::now(),
            value,
        });
        store.add(diagnostic);
    }

    #[test]
    fn window_lifecycle_distinguishes_request_from_backend_observation() {
        let request = WindowResizeRequest {
            frame: 30,
            width: 1280,
            height: 720,
        };
        let mut lifecycle = WindowLifecycle::new(Some(request), Some(90));
        lifecycle.resize_request_issued = true;
        lifecycle.resize_request_issued_frame = Some(30);
        assert!(ensure_resize_confirmed(&lifecycle).is_err());
        lifecycle.observe_resize(30, 1280.0, 720.0);
        assert!(!lifecycle.resize_request_observed);
        lifecycle.observe_resize(31, 1024.0, 768.0);
        assert!(!lifecycle.resize_request_observed);
        assert!(ensure_resize_confirmed(&lifecycle).is_err());
        lifecycle.observe_resize(32, 1280.0, 720.0);
        assert!(lifecycle.resize_request_observed);
        assert!(ensure_resize_confirmed(&lifecycle).is_ok());
        assert_eq!(lifecycle.resize_request_observed_frame, Some(32));
        assert_eq!(lifecycle.resize_events_observed, 3);

        let mut window = Window {
            present_mode: PresentMode::AutoNoVsync,
            resizable: true,
            ..default()
        };
        window.resolution.set(1280.0, 720.0);
        let final_state = window_state_report(&window);
        let report = lifecycle.report(Some(final_state));
        assert_eq!(report.resize_request, Some(request));
        assert!(report.resize_request_issued);
        assert_eq!(report.resize_request_issued_frame, Some(30));
        assert!(report.resize_request_observed);
        assert_eq!(report.resize_request_observed_frame, Some(32));
        assert_eq!(
            report
                .final_state
                .as_ref()
                .map(|state| state.present_mode_request.as_str()),
            Some("auto_no_vsync")
        );
        assert_eq!(
            report
                .final_state
                .as_ref()
                .map(|state| state.vsync_requested),
            Some(false)
        );
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
