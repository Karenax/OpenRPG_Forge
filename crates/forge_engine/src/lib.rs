#![deny(unsafe_code)]

//! Bevy rendering adapter for shared Forge documents.

#[cfg(feature = "physics-rapier")]
mod bridge;
#[cfg(feature = "qualification")]
mod bridge_crossing_probe;
#[cfg(feature = "qualification")]
pub use bridge_crossing_probe::{
    BridgeCrossingProbePlugin, BridgeCrossingProbeStatus, BridgeTemporalLodStatus,
};
mod capture_readiness;
pub use capture_readiness::issued_capture_frame;
pub mod finite_perspective;
mod manual_placement_identity;
pub mod scene_camera;
#[cfg(feature = "physics-rapier")]
pub mod source_collider_projection;
#[cfg(feature = "physics-rapier")]
pub mod source_collider_scene;
#[cfg(feature = "physics-rapier")]
pub mod source_colliders;
#[cfg(feature = "physics-rapier")]
pub mod source_mesh_collider_projection;
#[cfg(feature = "physics-rapier")]
pub mod source_mesh_colliders;
#[cfg(feature = "physics-rapier")]
pub mod source_physics_contacts;
#[cfg(feature = "physics-rapier")]
pub mod source_physics_transport;
pub use manual_placement_identity::ManualPlacementPart;
#[cfg(feature = "physics-rapier")]
mod terrain_brush_cursor;
#[cfg(feature = "physics-rapier")]
pub use terrain_brush_cursor::{TerrainBrushCursor, update_terrain_brush_cursor};
#[cfg(feature = "qualification")]
mod fixed_proof_view;
#[cfg(feature = "qualification")]
pub use fixed_proof_view::{
    FixedProofClock, FixedProofViewError, FixedProofViewPlugin, FixedProofViewStatus,
};
mod cloud_preview;
mod environment;
pub use cloud_preview::{CloudLayerPreviewPlugin, CloudPreviewObservation};
mod frame_overlay;
#[cfg(any(target_arch = "wasm32", test))]
mod preparation_budget;
mod quality_cpu_trace;
pub use quality_cpu_trace::{QualityCpuFrame, QualityCpuPhase, QualityCpuTracePlugin};
mod gpu_memory;
pub use gpu_memory::{GpuAllocatorTotals, GpuMemoryDiagnostic};
#[cfg(feature = "physics-rapier")]
mod global_quality;
#[cfg(feature = "physics-rapier")]
mod profile_warmup;
#[cfg(feature = "physics-rapier")]
pub use global_quality::{
    EffectiveQualityEvidence, GlobalQualityPlugin, GlobalQualityRequest, GlobalQualityStats,
    QualityAdaptationStats, QualityApplicationEvidence, QualityDomainProjection,
};
#[cfg(feature = "physics-rapier")]
pub use profile_warmup::{PreparedQualityVariant, ProfileWarmup, ProfileWarmupPhase};
mod post_process_quality;
pub use post_process_quality::{PostProcessQualityObservation, PostProcessQualityRequest};
pub mod atmospheric_haze;
#[cfg(feature = "physics-rapier")]
pub mod gameplay;
#[cfg(feature = "physics-rapier")]
mod governed_asset_kit;
pub mod lens_flare_sun;
#[cfg(all(feature = "physics-rapier", not(target_arch = "wasm32")))]
pub mod native_gameplay;
#[cfg(feature = "physics-rapier")]
pub mod navigation;
#[cfg(feature = "physics-rapier")]
pub mod physics;
#[cfg(feature = "physics-rapier")]
pub mod player_camera;
#[cfg(feature = "physics-rapier")]
pub mod player_controller;
#[cfg(feature = "physics-rapier")]
pub mod player_motion_extension;
#[cfg(feature = "physics-rapier")]
pub mod playtest_timing;
#[cfg(feature = "physics-rapier")]
pub mod sampled_terrain_physics;
mod shadow_quality;
#[cfg(feature = "physics-rapier")]
pub mod source_mesh_pipeline;
pub mod source_particle_compute;
pub mod source_particle_frame;
#[cfg(feature = "physics-rapier")]
pub mod source_particle_host;
#[cfg(feature = "physics-rapier")]
pub mod source_particle_host_input;
#[cfg(feature = "physics-rapier")]
pub mod source_particle_transport;
mod temporal_lod;
pub mod water_medium;
pub use temporal_lod::{TemporalLod, TemporalLodError, TemporalLodPolicy, TemporalLodSample};
#[cfg(feature = "physics-rapier")]
mod terrain_quality;
#[cfg(feature = "physics-rapier")]
pub use terrain_quality::{
    TerrainLodProjectionStats, TerrainQualityRequest, VisualValleyTerrainQualityPlugin,
};
#[cfg(feature = "physics-rapier")]
mod terrain_update;
mod vegetation;
pub mod vegetation_far_geometry;
#[cfg(feature = "physics-rapier")]
mod vegetation_prototypes;
#[cfg(feature = "physics-rapier")]
pub use vegetation::{
    GovernedFarBatchPreview, VegetationPrototypeForestPreview, governed_forest_ready,
    stop_governed_forest,
};
pub use vegetation::{
    GovernedTreePrimitive, VegetationQualityObservation, VegetationQualityRequest,
    VegetationScatterProjection, update_vegetation_sources,
    update_vegetation_sources_with_buildings, update_vegetation_sources_with_paths,
    update_vegetation_sources_with_promotions, update_vegetation_sources_with_waterways,
};
#[cfg(feature = "physics-rapier")]
pub use vegetation_prototypes::{
    PromotedTreeAsset, RenderVegetationPrimitive, RenderVegetationPrototype,
    ResolvedVegetationPrimitive, ResolvedVegetationPrototype, TreePromotionSelection,
    VegetationPrototypeCache, VegetationPrototypeLoadPlugin, VegetationPrototypeLoadStatus,
    VegetationPrototypeLoaderPlugin, VegetationPrototypeResolveError, VegetationPrototypeWindCache,
    WindVegetationPrimitive, prepare_tree_promotion, resolve_authored_species,
    resolve_promoted_tree_asset, resolve_vegetation_prototype,
};
mod visual_valley;
mod water;
#[cfg(feature = "physics-rapier")]
pub use water::authoring as authoring_water;
pub use water::{
    WaterDepthOpticsPreview, WaterQualityObservation, WaterQualityRequest,
    WaterReflectionOpticsPreview,
};
#[cfg(feature = "physics-rapier")]
pub mod source_fragment;
#[cfg(feature = "physics-rapier")]
mod source_fragment_loader;
#[cfg(feature = "physics-rapier")]
pub mod source_graph;
#[cfg(feature = "physics-rapier")]
pub mod source_graph_glb;
#[cfg(feature = "physics-rapier")]
pub mod source_graph_scene;
#[cfg(feature = "physics-rapier")]
pub mod source_graph_scene_geometry;
#[cfg(feature = "physics-rapier")]
pub mod source_graph_scene_textures;
mod weather;

#[cfg(feature = "physics-rapier")]
pub use bridge::{
    WorldSketchBridgeAdapterError, WorldSketchBridgeDeck, WorldSketchBridgeParapet,
    WorldSketchBridgePlugin, WorldSketchBridgeStats,
};
pub use environment::{
    EnvironmentDistanceFogStats, EnvironmentProjectionStats, EnvironmentProjectionStatus,
    EnvironmentQualityObservation, EnvironmentQualityRequest, EnvironmentSkyStats,
    EnvironmentSunStats, EnvironmentVolumetricStats, VisualValleyEnvironmentPlugin,
    VolumetricPreviewRequest,
};
#[cfg(feature = "physics-rapier")]
pub use governed_asset_kit::{
    GovernedAssetKitAdapterError, GovernedAssetKitPlacementMarker, GovernedAssetKitReadiness,
    VisualValleyGovernedAssetKitPlugin,
};
#[cfg(feature = "physics-rapier")]
pub use physics::ManualPlacementCollisionGeometry;
#[cfg(feature = "physics-rapier")]
pub(crate) use physics::build_terrain_heightfield_collider;
#[cfg(feature = "physics-rapier")]
pub use physics::{
    ManualPlacementCollision, ManualPlacementCollisionExpected, ManualPlacementCollisionFailed,
    ManualPlacementCollisionNotRequired, ManualPlacementCollisionPlugin,
    ManualPlacementCollisionStatus, ManualPlacementProjection, PathSurfaceColliderIdentity,
    TERRAIN_PHYSICS_HZ, TerrainHeightfieldCollider, TerrainPhysicsAdapterError,
    TerrainPhysicsPlugin, fish_stand_colliders, manual_placements_ready, placement_transform,
    profiled_path_collider, profiled_path_collider_present,
};
#[cfg(feature = "physics-rapier")]
pub use player_camera::PlayerCameraMode;
#[cfg(feature = "physics-rapier")]
pub use player_controller::{
    ForgePlayerCamera, ForgePlayerCharacter, PendingPlayerControlCommand, PlayerControlCommand,
    PlayerControlCommandError, PlayerControllerPlugin, PlayerControllerSet, PlayerInputFocus,
    PlayerRuntimeBindingSnapshot, PlayerRuntimeBindings, PlayerRuntimeSnapshot,
    PlayerSimulationReady, PlayerSpawnPoint,
};
pub use shadow_quality::{
    ShadowQualityObservation, ShadowQualityRequest, VisualValleyShadowQualityPlugin,
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
    VegetationInteractionField, VegetationLiveMeshStats, VegetationLodRanges, VegetationMeshError,
    VegetationMeshKey, VegetationPlacementMarker, VegetationPlacementStats,
    VegetationProjectionCounts, VegetationProjectionLod, VegetationProjectionStats,
    VegetationRenderPathStats, VegetationTriangleStats, VegetationVisibilityBucket,
    VegetationVisibilityObservationStatus, VegetationVisibilityStats,
    VisualValleyVegetationExtension, VisualValleyVegetationMaterial, VisualValleyVegetationPlugin,
    inspect_vegetation_projection,
};
pub use visual_valley::{
    VisualValleyAdapterError, VisualValleyDebug, VisualValleyPlugin,
    VisualValleyShadowObservationStatus, VisualValleyShadowStats, VisualValleyStats,
    VisualValleyTerrainChunk, VisualValleyTerrainExtension, VisualValleyTerrainMaterial,
};
#[cfg(feature = "physics-rapier")]
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
    },
    window::{PresentMode, PrimaryWindow, WindowCloseRequested, WindowResized},
};
use capture_readiness::{CaptureProgress, CaptureReadinessPlugin, capture_at_requested_frame};
use forge_core::{EntityId, FrameDistribution as CoreFrameDistribution, FrameTimeHistogram};
use forge_project::{SceneProofDocument, SceneProofKind};
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

/// Machine-readable evidence for the bounded R1-064 runtime projection.
#[derive(Clone, Debug, Default, Eq, PartialEq, Resource, Serialize)]
pub struct GovernedAssetKitRuntimeStats {
    pub asset_kit_id: String,
    pub asset_kit_revision: u32,
    pub input_manifest_sha256: String,
    pub source_world_sketch_sha256: String,
    pub source_terrain_sha256: String,
    pub camera_id: String,
    pub validation_camera_spawned: bool,
    pub declared_assets: usize,
    pub requested_scene_instances: usize,
    pub loaded_scene_instances: usize,
    pub failed_scene_instances: usize,
    pub configured_lod0_nodes: usize,
    pub configured_lod1_nodes: usize,
    pub configured_lod2_nodes: usize,
    pub configured_collision_nodes: usize,
    pub failed_collision_nodes: usize,
    pub ready_rapier_colliders: usize,
    pub all_scene_dependencies_loaded: bool,
}

#[derive(Clone, Debug, Resource, Serialize)]
pub struct GpuDiagnostic {
    pub memory: Option<GpuMemoryDiagnostic>,
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

/// Presentation only: hiding the overlay never stops diagnostic collection or export.
#[derive(Resource)]
pub struct DiagnosticsOverlaySettings {
    pub visible: bool,
}

/// Optional host label; leaves historical proof labels unchanged when absent.
#[derive(Resource)]
pub struct DiagnosticsOverlayTitle(pub String);

impl Default for DiagnosticsOverlaySettings {
    fn default() -> Self {
        Self { visible: true }
    }
}

#[derive(Resource, Default)]
struct AdapterLogged(bool);

#[derive(Resource)]
struct FrameLedger {
    samples_ms: Vec<f64>,
    first_frame: Option<u32>,
    last_frame: Option<u32>,
    last_recorded_frame: Option<u32>,
    sample_limit: usize,
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
        }
    }

    fn record(&mut self, frame: u32, frame_ms: f64) {
        let inside_window = self
            .first_frame
            .zip(self.last_frame)
            .is_some_and(|(first_frame, last_frame)| (first_frame..=last_frame).contains(&frame));
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

#[derive(bevy::ecs::system::SystemParam)]
struct ProjectionEvidence<'w> {
    capture: Option<Res<'w, CaptureProgress>>,
    clouds: Option<Res<'w, CloudPreviewObservation>>,
    #[cfg(feature = "physics-rapier")]
    terrain_lod: Option<Res<'w, TerrainLodProjectionStats>>,
    #[cfg(feature = "physics-rapier")]
    global_quality: Option<Res<'w, GlobalQualityStats>>,
    #[cfg(feature = "physics-rapier")]
    profile_warmup: Option<Res<'w, ProfileWarmup>>,
}

#[derive(Debug, Serialize)]
struct RuntimeMetricsReport {
    schema: &'static str,
    schema_version: u32,
    source_hash: String,
    gpu: Option<GpuDiagnostic>,
    window: WindowLifecycleReport,
    frame: FrameMetrics,
    render_diagnostics: Vec<NamedDiagnostic>,
    render_work: RenderWorkReport,
    #[serde(skip_serializing_if = "Option::is_none")]
    capture_readiness: Option<CaptureProgress>,
    #[serde(skip_serializing_if = "Option::is_none")]
    cloud_preview: Option<CloudPreviewObservation>,
    #[cfg(feature = "physics-rapier")]
    #[serde(skip_serializing_if = "Option::is_none")]
    terrain_lod: Option<TerrainLodProjectionStats>,
    #[cfg(feature = "physics-rapier")]
    #[serde(skip_serializing_if = "Option::is_none")]
    global_quality: Option<GlobalQualityStats>,
    #[cfg(feature = "physics-rapier")]
    #[serde(skip_serializing_if = "Option::is_none")]
    profile_warmup: Option<ProfileWarmup>,
    #[serde(skip_serializing_if = "Option::is_none")]
    visual_valley: Option<VisualValleyStats>,
    #[serde(skip_serializing_if = "Option::is_none")]
    visual_valley_shadow: Option<VisualValleyShadowStats>,
    #[serde(skip_serializing_if = "Option::is_none")]
    vegetation: Option<VegetationProjectionStats>,
    #[serde(skip_serializing_if = "Option::is_none")]
    environment: Option<EnvironmentProjectionStats>,
    #[serde(skip_serializing_if = "Option::is_none")]
    water: Option<WaterProjectionStats>,
    #[serde(skip_serializing_if = "Option::is_none")]
    weather: Option<WeatherProjectionStats>,
    #[serde(skip_serializing_if = "Option::is_none")]
    governed_asset_kit: Option<GovernedAssetKitRuntimeStats>,
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
        if self.automation.capture_path.is_some()
            && !app.is_plugin_added::<CaptureReadinessPlugin>()
        {
            app.add_plugins(CaptureReadinessPlugin);
        }
        app.insert_resource(self.automation.clone())
            .insert_resource(FrameLedger::for_automation(&self.automation))
            .insert_resource(WindowLifecycle::new(
                self.automation.resize_request,
                self.automation.close_at_frame,
            ))
            .init_resource::<ProofSceneStats>()
            .init_resource::<AdapterLogged>()
            .init_resource::<DiagnosticsOverlaySettings>()
            .add_plugins((
                FrameTimeDiagnosticsPlugin::default(),
                EntityCountDiagnosticsPlugin::default(),
                RenderDiagnosticsPlugin,
            ))
            .add_systems(
                Startup,
                (initialize_diagnostics_overlay, initialize_window_lifecycle).chain(),
            )
            .add_systems(
                Update,
                (
                    record_gpu_diagnostic,
                    gpu_memory::collect,
                    request_automated_resize,
                    request_automated_close,
                    observe_window_events,
                    update_overlay,
                    sync_diagnostics_overlay_visibility,
                    capture_at_requested_frame,
                )
                    .chain(),
            )
            .add_systems(Last, (collect_frame_sample, write_metrics_and_exit).chain());
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

#[allow(clippy::needless_pass_by_value, reason = "Bevy system parameter")]
fn sync_diagnostics_overlay_visibility(
    settings: Res<DiagnosticsOverlaySettings>,
    mut overlays: Query<&mut Node, With<DiagnosticsOverlay>>,
) {
    let display = if settings.visible {
        Display::Flex
    } else {
        Display::None
    };
    for mut node in &mut overlays {
        if node.display != display {
            node.display = display;
        }
    }
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
    title: Option<Res<'w, DiagnosticsOverlayTitle>>,
    visual_valley: Option<Res<'w, VisualValleyStats>>,
    vegetation: Option<Res<'w, VegetationProjectionStats>>,
    water: Option<Res<'w, WaterProjectionStats>>,
    weather: Option<Res<'w, WeatherProjectionStats>>,
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
    time: Res<Time<Real>>,
    mut cadence: Local<frame_overlay::FrameOverlay>,
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
    let render_work = build_render_work_report(&stats, &diagnostics);
    **overlay = Text::new(if let Some(visual_valley) = gate1.visual_valley {
        let mut logical_details = visual_valley.overlay_details();
        logical_details.push('\n');
        logical_details.push_str(cadence.record(time.delta_secs_f64() * 1_000.0));
        logical_details.push('\n');
        logical_details.push_str(&frame_overlay::render_timing_text(&diagnostics));
        if let Some(vegetation) = gate1.vegetation {
            logical_details.push('\n');
            logical_details.push_str(&vegetation.overlay_details());
        }
        if let Some(water) = gate1.water {
            let _ = write!(
                logical_details,
                "\nEau : {} échantillons · {} triangles · {:?}",
                water.centerline_samples, water.triangles, water.runtime_observation
            );
        }
        if let Some(weather) = gate1.weather {
            let _ = write!(
                logical_details,
                "\nMétéo : {:?} · {} gouttes · wetness {:.2} · {:?}",
                weather.target_state,
                weather.rain.generated_drops,
                weather.wetness.current,
                weather.status
            );
        }
        format_runtime_diagnostics_overlay(
            gate1
                .title
                .as_ref()
                .map_or("OpenRPG Forge · Gate 1 / Visual Valley", |title| {
                    title.0.as_str()
                }),
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
        "OpenRPG Forge · Gate 0",
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
            format!("GPU opaque : {primitives:.0} primitives · {vertices:.0} invocations vertex")
        }
        (Some(primitives), None) => format!("GPU opaque : {primitives:.0} primitives"),
        _ => "Statistiques pipeline GPU : indisponibles".to_owned(),
    };
    let mut output = format!(
        "{title}\n{adapter}\n{fps:6.1} FPS · {frame_ms:5.2} ms\n{} entités source · {} maillages source\nDraw calls GPU : indisponibles\n{gpu_work}\n{}",
        stats.source_entities, stats.source_mesh_instances, stats.source_hash
    );
    if let Some(logical_details) = logical_details {
        output.push('\n');
        output.push_str(logical_details);
    }
    frame_overlay::diagnostic_font_text(&output)
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
        memory: None,
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
fn collect_frame_sample(
    frame: Res<bevy::diagnostic::FrameCount>,
    time: Res<Time<Real>>,
    mut ledger: ResMut<FrameLedger>,
) {
    ledger.record(frame.0, time.delta_secs_f64() * 1_000.0);
}

#[allow(
    clippy::needless_pass_by_value,
    clippy::too_many_arguments,
    reason = "Bevy systems require explicit owned system parameters for independent ECS access"
)]
fn write_metrics_and_exit(
    frame: Res<bevy::diagnostic::FrameCount>,
    automation: Res<RuntimeAutomation>,
    ledger: Res<FrameLedger>,
    stats: Res<ProofSceneStats>,
    visual_valley: Option<Res<VisualValleyStats>>,
    visual_valley_shadow: Option<Res<VisualValleyShadowStats>>,
    vegetation: Option<Res<VegetationProjectionStats>>,
    environment: Option<Res<EnvironmentProjectionStats>>,
    water: Option<Res<WaterProjectionStats>>,
    weather: Option<Res<WeatherProjectionStats>>,
    governed_asset_kit: Option<Res<GovernedAssetKitRuntimeStats>>,
    gpu: Option<Res<GpuDiagnostic>>,
    diagnostics: Res<DiagnosticsStore>,
    windows: Query<&Window, With<PrimaryWindow>>,
    (lifecycle, evidence): (Res<WindowLifecycle>, ProjectionEvidence),
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
        match completed_capture_artifact_size(path, evidence.capture.as_deref()) {
            Ok(_) => {}
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
        match build_runtime_metrics(
            &ledger.samples_ms,
            &stats,
            visual_valley.as_deref(),
            visual_valley_shadow.as_deref(),
            vegetation.as_deref(),
            environment.as_deref(),
            water.as_deref(),
            weather.as_deref(),
            governed_asset_kit.as_deref(),
            gpu.as_deref(),
            &diagnostics,
            &lifecycle,
            final_window,
        )
        .and_then(|mut report| {
            report.capture_readiness = evidence.capture.as_deref().cloned();
            report.cloud_preview = evidence.clouds.as_deref().cloned();
            #[cfg(feature = "physics-rapier")]
            {
                report.terrain_lod = evidence.terrain_lod.as_deref().cloned();
                report.global_quality = evidence.global_quality.as_deref().cloned();
                report.profile_warmup = evidence.profile_warmup.as_deref().cloned();
            }
            write_metrics(path, &report)
        }) {
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

fn completed_capture_artifact_size(
    path: &Path,
    progress: Option<&CaptureProgress>,
) -> Result<u64, String> {
    progress
        .ok_or("capture readiness observer was not installed")?
        .ensure_completed()?;
    let bytes = capture_artifact_size(path)?;
    info!(target: "forge::proof", schema = "forge.runtime-event", schema_version = 1_u64,
        event_kind = "capture_verified", path = %path.display(), bytes,
        "runtime capture verified");
    Ok(bytes)
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
    reason = "the metrics serializer combines independent runtime evidence without hidden globals"
)]
fn build_runtime_metrics(
    samples: &[f64],
    stats: &ProofSceneStats,
    visual_valley: Option<&VisualValleyStats>,
    visual_valley_shadow: Option<&VisualValleyShadowStats>,
    vegetation: Option<&VegetationProjectionStats>,
    environment: Option<&EnvironmentProjectionStats>,
    water: Option<&WaterProjectionStats>,
    weather: Option<&WeatherProjectionStats>,
    governed_asset_kit: Option<&GovernedAssetKitRuntimeStats>,
    gpu: Option<&GpuDiagnostic>,
    diagnostics: &DiagnosticsStore,
    lifecycle: &WindowLifecycle,
    final_window: Option<WindowStateReport>,
) -> Result<RuntimeMetricsReport, String> {
    validate_governed_asset_kit_runtime_stats(governed_asset_kit)?;
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
        capture_readiness: None,
        cloud_preview: None,
        #[cfg(feature = "physics-rapier")]
        terrain_lod: None,
        #[cfg(feature = "physics-rapier")]
        global_quality: None,
        #[cfg(feature = "physics-rapier")]
        profile_warmup: None,
        schema: "forge.runtime-metrics",
        schema_version: 8,
        source_hash: stats.source_hash.clone(),
        gpu: gpu.cloned(),
        window: lifecycle.report(final_window),
        frame,
        render_diagnostics,
        render_work: build_render_work_report(stats, diagnostics),
        visual_valley: visual_valley.cloned(),
        visual_valley_shadow: visual_valley_shadow.cloned(),
        vegetation: vegetation.cloned(),
        environment: environment.cloned(),
        water: water.cloned(),
        weather: weather.cloned(),
        governed_asset_kit: governed_asset_kit.cloned(),
    })
}

fn validate_governed_asset_kit_runtime_stats(
    stats: Option<&GovernedAssetKitRuntimeStats>,
) -> Result<(), String> {
    let Some(stats) = stats else {
        return Ok(());
    };
    if stats.declared_assets == 0 || stats.requested_scene_instances == 0 {
        return Err(
            "governed asset-kit runtime proof contains no assets or scene instances".into(),
        );
    }
    if stats.failed_scene_instances > 0 {
        return Err(format!(
            "governed asset-kit runtime proof has {} failed scene instance(s)",
            stats.failed_scene_instances
        ));
    }
    if !stats.all_scene_dependencies_loaded
        || stats.loaded_scene_instances != stats.requested_scene_instances
    {
        return Err(format!(
            "governed asset-kit runtime proof loaded {} of {} scene instance(s) with dependencies",
            stats.loaded_scene_instances, stats.requested_scene_instances
        ));
    }
    if stats.configured_lod0_nodes == 0
        || stats.configured_lod1_nodes == 0
        || stats.configured_lod2_nodes == 0
    {
        return Err(format!(
            "governed asset-kit runtime proof is missing configured LOD nodes (LOD0 {}, LOD1 {}, LOD2 {})",
            stats.configured_lod0_nodes, stats.configured_lod1_nodes, stats.configured_lod2_nodes
        ));
    }
    if stats.configured_collision_nodes == 0
        || stats.failed_collision_nodes > 0
        || stats.ready_rapier_colliders != stats.configured_collision_nodes
    {
        return Err(format!(
            "governed asset-kit runtime proof materialized {} of {} configured Rapier collider(s)",
            stats.ready_rapier_colliders, stats.configured_collision_nodes
        ));
    }
    Ok(())
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
    fn hiding_overlay_preserves_entity_text_other_ui_and_frame_collection() {
        let mut app = App::new();
        app.init_resource::<DiagnosticsOverlaySettings>();
        assert!(app.world().resource::<DiagnosticsOverlaySettings>().visible);
        let automation = RuntimeAutomation {
            metrics_path: Some(PathBuf::from("not-written-by-this-test.json")),
            metrics_warmup_frames: 0,
            exit_after_frames: Some(6),
            ..default()
        };
        app.insert_resource(FrameLedger::for_automation(&automation))
            .init_resource::<Time<Real>>()
            .init_resource::<bevy::diagnostic::FrameCount>()
            .add_systems(Update, sync_diagnostics_overlay_visibility)
            .add_systems(Last, collect_frame_sample);
        let owned = app
            .world_mut()
            .spawn((DiagnosticsOverlay, Node::default(), Text::new("kept")))
            .id();
        let foreign = app
            .world_mut()
            .spawn(Node {
                display: Display::Grid,
                ..default()
            })
            .id();
        for (index, visible) in [false, true, false, false, true, false]
            .into_iter()
            .enumerate()
        {
            app.world_mut()
                .resource_mut::<DiagnosticsOverlaySettings>()
                .visible = visible;
            app.world_mut()
                .resource_mut::<bevy::diagnostic::FrameCount>()
                .0 = u32::try_from(index + 1).unwrap();
            app.world_mut()
                .resource_mut::<Time<Real>>()
                .advance_by(std::time::Duration::from_millis(16));
            app.update();
            assert_eq!(
                app.world().get::<Node>(owned).unwrap().display,
                if visible {
                    Display::Flex
                } else {
                    Display::None
                }
            );
            assert_eq!(app.world().get::<Text>(owned).unwrap().0, "kept");
            assert_eq!(
                app.world().get::<Node>(foreign).unwrap().display,
                Display::Grid
            );
            assert_eq!(
                app.world().resource::<FrameLedger>().samples_ms.len(),
                index + 1
            );
        }
    }

    #[test]
    fn an_existing_file_cannot_qualify_a_capture_without_this_runs_readback() {
        let existing = Path::new(concat!(env!("CARGO_MANIFEST_DIR"), "/Cargo.toml"));
        assert!(capture_artifact_size(existing).unwrap() > 0);
        assert!(completed_capture_artifact_size(existing, None).is_err());
        assert!(
            completed_capture_artifact_size(existing, Some(&CaptureProgress::default())).is_err()
        );
    }

    fn bounded_metrics_automation(warmup: u32, last_frame: Option<u32>) -> RuntimeAutomation {
        RuntimeAutomation {
            metrics_path: Some(PathBuf::from("frame-ledger-test.json")),
            metrics_warmup_frames: warmup,
            exit_after_frames: last_frame,
            ..default()
        }
    }

    fn sample_environment_stats() -> EnvironmentProjectionStats {
        EnvironmentProjectionStats {
            quality: None,
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
                atmosphere_ibl: true,
                atmosphere_ibl_resolution: Some([256, 256]),
                atmosphere_ibl_intensity: Some(1.0),
            },
            sun: EnvironmentSunStats {
                weather_response: environment::WeatherSunlightObservation::default(),
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
                effective_maximum_distance_m: None,
                requested: true,
                applied: false,
                resolved_path: "distance_fog_fallback",
                requested_step_count: 64,
                fallback: "distance_fog",
            },
        }
    }

    fn sample_governed_asset_kit_stats() -> GovernedAssetKitRuntimeStats {
        GovernedAssetKitRuntimeStats {
            asset_kit_id: "visual-valley-r1-063".to_owned(),
            asset_kit_revision: 3,
            input_manifest_sha256: "manifest".to_owned(),
            source_world_sketch_sha256: "world-sketch".to_owned(),
            source_terrain_sha256: "terrain".to_owned(),
            camera_id: "hero_reference_match".to_owned(),
            validation_camera_spawned: true,
            declared_assets: 6,
            requested_scene_instances: 20,
            loaded_scene_instances: 20,
            failed_scene_instances: 0,
            configured_lod0_nodes: 24,
            configured_lod1_nodes: 24,
            configured_lod2_nodes: 24,
            configured_collision_nodes: 32,
            failed_collision_nodes: 0,
            ready_rapier_colliders: 32,
            all_scene_dependencies_loaded: true,
        }
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
            without_metrics.record(frame, 8.0);
            without_exit.record(frame, 8.0);
        }

        assert!(without_metrics.samples_ms.is_empty());
        assert!(without_exit.samples_ms.is_empty());
        assert_eq!(without_metrics.sample_limit, 0);
        assert_eq!(without_exit.sample_limit, 0);
    }

    #[test]
    fn frame_ledger_records_the_exact_post_warmup_bounded_window_once() {
        let mut ledger = FrameLedger::for_automation(&bounded_metrics_automation(2, Some(5)));

        ledger.record(2, 9.0);
        ledger.record(3, 10.0);
        ledger.record(3, 11.0);
        ledger.record(4, f64::NAN);
        ledger.record(4, 12.0);
        ledger.record(5, 14.0);
        ledger.record(6, 16.0);

        assert_eq!(ledger.first_frame, Some(3));
        assert_eq!(ledger.last_frame, Some(5));
        assert_eq!(ledger.sample_limit, 3);
        assert_eq!(ledger.samples_ms, vec![10.0, 12.0, 14.0]);
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
        let report = build_runtime_metrics(
            &samples,
            &stats,
            None,
            None,
            None,
            None,
            None,
            None,
            None,
            None,
            &diagnostics,
            &lifecycle,
            None,
        )
        .unwrap();
        assert_eq!(report.schema_version, 8);
        assert!(
            serde_json::to_value(&report)
                .unwrap()
                .get("global_quality")
                .is_none()
        );
        assert!(report.visual_valley.is_none());
        assert!(report.visual_valley_shadow.is_none());
        assert!(report.vegetation.is_none());
        assert!(report.environment.is_none());
        assert!(report.water.is_none());
        assert!(report.weather.is_none());
        assert!(report.governed_asset_kit.is_none());
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
    fn gate1_metrics_preserve_machine_readable_scene_projections() {
        let stats = ProofSceneStats {
            source_entities: 1,
            source_mesh_instances: 64,
            source_hash: "terrain".to_owned(),
        };
        let visual_valley = VisualValleyStats {
            source_hash: "terrain".to_owned(),
            chunks: 64,
            vertices: 69_696,
            triangles: 131_072,
            material_layers: 3,
            lod_levels: 1,
            lod_zero_chunks: 64,
        };
        let terrain = forge_world::embedded_visual_valley_terrain_document().unwrap();
        let vegetation_document =
            forge_world::embedded_visual_valley_vegetation_document().unwrap();
        let vegetation = inspect_vegetation_projection(&terrain, &vegetation_document).unwrap();
        let shadow = VisualValleyShadowStats {
            quality: None,
            status: VisualValleyShadowObservationStatus::ObservedSingleSun,
            sun_entities: 1,
            shadow_maps_enabled: Some(true),
            configured_maximum_distance_m: Some(128.0),
            cascade_bounds_m: vec![24.0, 48.0, 80.0, 128.0],
            evidence_source: "visual_valley_sun_cascade_shadow_component",
        };
        let environment = sample_environment_stats();
        let governed_asset_kit = sample_governed_asset_kit_stats();
        let report = build_runtime_metrics(
            &[8.0],
            &stats,
            Some(&visual_valley),
            Some(&shadow),
            Some(&vegetation),
            Some(&environment),
            None,
            None,
            Some(&governed_asset_kit),
            None,
            &DiagnosticsStore::default(),
            &WindowLifecycle::new(None, None),
            None,
        )
        .unwrap();

        assert_eq!(report.visual_valley, Some(visual_valley));
        assert_eq!(report.visual_valley_shadow, Some(shadow));
        assert_eq!(report.vegetation, Some(vegetation));
        assert_eq!(report.environment, Some(environment));
        assert!(report.water.is_none());
        assert!(report.weather.is_none());
        assert_eq!(report.governed_asset_kit, Some(governed_asset_kit));
        let serialized = serde_json::to_value(&report).unwrap();
        assert_eq!(serialized["visual_valley"]["chunks"], 64);
        assert_eq!(serialized["visual_valley"]["vertices"], 69_696);
        assert_eq!(serialized["visual_valley"]["triangles"], 131_072);
        assert_eq!(
            serialized["vegetation"]["placements"]["accepted_placements"],
            24_645
        );
        assert_eq!(serialized["vegetation"]["projection"]["grass_batches"], 64);
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
            serialized["governed_asset_kit"]["loaded_scene_instances"],
            20
        );
        assert_eq!(
            serialized["governed_asset_kit"]["ready_rapier_colliders"],
            32
        );
    }

    #[test]
    fn runtime_metrics_reject_incomplete_governed_asset_kit_evidence() {
        let stats = ProofSceneStats {
            source_entities: 1,
            source_mesh_instances: 64,
            source_hash: "terrain".to_owned(),
        };
        let mut governed_asset_kit = sample_governed_asset_kit_stats();
        governed_asset_kit.ready_rapier_colliders -= 1;

        let error = build_runtime_metrics(
            &[8.0],
            &stats,
            None,
            None,
            None,
            None,
            None,
            None,
            Some(&governed_asset_kit),
            None,
            &DiagnosticsStore::default(),
            &WindowLifecycle::new(None, None),
            None,
        )
        .unwrap_err();

        assert!(error.contains("materialized 31 of 32 configured Rapier collider"));
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
        assert!(text.contains("2 maillages source"));
        assert!(text.contains("Draw calls GPU : indisponibles"));
        assert!(text.contains("GPU opaque : 14 primitives"));
        assert!(!text.contains("draws logiques"));
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
