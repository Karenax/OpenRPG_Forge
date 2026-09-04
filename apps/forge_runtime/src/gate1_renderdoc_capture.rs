use bevy::{
    app::AppExit,
    diagnostic::FrameCount,
    ecs::system::SystemParam,
    prelude::*,
    render::diagnostic::RenderDiagnosticsPlugin,
    window::{PresentMode, PrimaryWindow},
};
use forge_core::{canonical_json_bytes, canonical_json_sha256};
use forge_engine::{
    EnvironmentProjectionStats, GpuDiagnostic, PipelineWarmupObservation, RenderDiagnosticsPolicy,
    RuntimeAutomation, SceneryProjectionStats, SceneryProjectionStatus, VegetationProjectionStats,
    VisualValleyQualitySelection, VisualValleyQualityStats, VisualValleyShadowStats,
    VisualValleyStats, WaterProjectionStats, WeatherProjectionStats,
    validate_governed_gate1_render_state,
};
use forge_project::{
    EnvironmentProfileDocument, TerrainDocument, VegetationDocument, VisualQualityTier,
    VisualValleyQualityProfileDocument, WaterDocument, WeatherProfileDocument, WeatherState,
};
use serde::{Deserialize, Serialize};
use std::{
    fs,
    io::{Read, Write},
    path::{Path, PathBuf},
    time::{Duration, Instant},
};

const READY_SCHEMA: &str = "forge.gate1-renderdoc-ready";
const SIGNAL_SCHEMA: &str = "forge.gate1-renderdoc-capture-signal";
const LEDGER_SCHEMA: &str = "forge.gate1-renderdoc-runtime";
const READY_FILE: &str = "gate1-renderdoc.ready.json";
const SIGNAL_FILE: &str = "gate1-renderdoc.capture.signal.json";
const LEDGER_FILE: &str = "gate1-renderdoc.runtime.json";
const FAILURE_FILE: &str = "gate1-renderdoc.failure.txt";
const READY_SCHEMA_VERSION: u32 = 1;
const SIGNAL_SCHEMA_VERSION: u32 = 1;
const LEDGER_SCHEMA_VERSION: u32 = 1;
const REQUIRED_RENDERDOC_VERSION: &str = "1.46";
const FREEZE_SETTLE_FRAMES: u32 = 30;
const CAPTURE_WINDOW_FRAMES: u32 = 120;
const WATCHDOG: Duration = Duration::from_secs(90);
const MAX_SIGNAL_BYTES: u64 = 32 * 1024;
const MAX_READY_BYTES: u64 = 4 * 1024 * 1024;
const REQUIRED_WIDTH: u32 = 1_920;
const REQUIRED_HEIGHT: u32 = 1_080;
const FROZEN_CAPTURE_TICK: u64 = 0;

#[derive(Clone, Resource)]
struct Configuration {
    output_dir: PathBuf,
    nonce: String,
    weather: WeatherState,
    sources: SourceHashes,
    seeds: SourceSeeds,
}

#[derive(Clone, Debug, Eq, PartialEq, Serialize)]
struct SourceHashes {
    terrain: String,
    vegetation: String,
    environment: String,
    water: String,
    weather: String,
    quality: String,
    cadle_assets_used: bool,
}

#[derive(Clone, Debug, Eq, PartialEq, Serialize)]
struct SourceSeeds {
    terrain: u64,
    vegetation: u64,
    water: u64,
    weather: u64,
}

pub(crate) struct Gate1RenderDocCapturePlugin {
    configuration: Configuration,
}

impl Gate1RenderDocCapturePlugin {
    #[allow(
        clippy::too_many_arguments,
        reason = "the capture contract hashes every independently governed Visual Valley input"
    )]
    pub(crate) fn new(
        output_dir: PathBuf,
        nonce: String,
        expected_weather: WeatherState,
        terrain: &TerrainDocument,
        vegetation: &VegetationDocument,
        environment: &EnvironmentProfileDocument,
        water: &WaterDocument,
        weather: &WeatherProfileDocument,
        quality: &VisualValleyQualityProfileDocument,
    ) -> Result<Self, String> {
        validate_nonce(&nonce)?;
        Ok(Self {
            configuration: Configuration {
                output_dir,
                nonce,
                weather: expected_weather,
                sources: SourceHashes {
                    terrain: content_hash(terrain, "terrain")?,
                    vegetation: content_hash(vegetation, "vegetation")?,
                    environment: content_hash(environment, "environment")?,
                    water: content_hash(water, "water")?,
                    weather: content_hash(weather, "weather")?,
                    quality: content_hash(quality, "quality")?,
                    cadle_assets_used: false,
                },
                seeds: SourceSeeds {
                    terrain: terrain.seed,
                    vegetation: vegetation.seed,
                    water: water.seed,
                    weather: weather.seed,
                },
            },
        })
    }
}

impl Plugin for Gate1RenderDocCapturePlugin {
    fn build(&self, app: &mut App) {
        app.insert_resource(CaptureRuntime::new(self.configuration.clone()))
            .add_systems(Last, drive_capture_protocol);
    }

    fn finish(&self, app: &mut App) {
        let automation = app
            .world()
            .get_resource::<RuntimeAutomation>()
            .unwrap_or_else(|| panic!("Gate 1 RenderDoc capture requires RuntimeAutomation"));
        assert_eq!(
            automation.render_diagnostics_policy,
            RenderDiagnosticsPolicy::DisabledForExternalPresentationQualification,
            "Gate 1 RenderDoc capture must disable Bevy render diagnostics"
        );
        assert!(
            !app.is_plugin_added::<RenderDiagnosticsPlugin>(),
            "Gate 1 RenderDoc capture must not add RenderDiagnosticsPlugin"
        );
    }
}

fn content_hash<T: Serialize>(value: &T, label: &str) -> Result<String, String> {
    canonical_json_sha256(value)
        .map(|hash| hash.to_string())
        .map_err(|error| format!("cannot hash Gate 1 RenderDoc {label}: {error}"))
}

#[derive(Clone, Copy, Debug, Eq, PartialEq)]
enum Phase {
    WaitingForReady,
    FreezeSettling,
    WaitingForSignal,
    CaptureWindow,
    Completed,
    Failed,
}

impl Phase {
    const fn label(self) -> &'static str {
        match self {
            Self::WaitingForReady => "waiting_for_ready",
            Self::FreezeSettling => "freeze_settling",
            Self::WaitingForSignal => "waiting_for_signal",
            Self::CaptureWindow => "capture_window",
            Self::Completed => "completed",
            Self::Failed => "failed",
        }
    }

    const fn is_terminal(self) -> bool {
        matches!(self, Self::Completed | Self::Failed)
    }
}

#[derive(Resource)]
struct CaptureRuntime {
    configuration: Configuration,
    started_at: Instant,
    phase: Phase,
    freeze_settle_frames: u32,
    capture_window_frames: u32,
    ready_frame: Option<u32>,
    signal_frame: Option<u32>,
    frozen_state: Option<FrozenStateEvidence>,
    frozen_state_sha256: Option<String>,
    signal: Option<CaptureSignal>,
}

impl CaptureRuntime {
    fn new(configuration: Configuration) -> Self {
        Self {
            configuration,
            started_at: Instant::now(),
            phase: Phase::WaitingForReady,
            freeze_settle_frames: 0,
            capture_window_frames: 0,
            ready_frame: None,
            signal_frame: None,
            frozen_state: None,
            frozen_state_sha256: None,
            signal: None,
        }
    }
}

#[derive(Clone, Serialize)]
struct GovernedSceneEvidence {
    visual_valley: VisualValleyStats,
    shadow: VisualValleyShadowStats,
    vegetation: VegetationProjectionStats,
    scenery: SceneryProjectionStats,
    environment: EnvironmentProjectionStats,
    water: WaterProjectionStats,
    weather: WeatherProjectionStats,
}

impl GovernedSceneEvidence {
    fn validate(&self, quality: &VisualValleyQualityStats) -> Result<(), String> {
        validate_governed_gate1_render_state(
            &self.visual_valley,
            &self.shadow,
            &self.vegetation,
            &self.scenery,
            &self.environment,
            &self.water,
            &self.weather,
            quality,
        )
    }
}

#[derive(SystemParam)]
struct GovernedScene<'w> {
    visual_valley: Res<'w, VisualValleyStats>,
    shadow: Res<'w, VisualValleyShadowStats>,
    vegetation: Res<'w, VegetationProjectionStats>,
    scenery: Res<'w, SceneryProjectionStats>,
    environment: Res<'w, EnvironmentProjectionStats>,
    water: Res<'w, WaterProjectionStats>,
    weather: Res<'w, WeatherProjectionStats>,
    quality: Res<'w, VisualValleyQualityStats>,
    gpu: Option<Res<'w, GpuDiagnostic>>,
}

impl<'w> GovernedScene<'w> {
    fn into_observation(
        self,
    ) -> (
        GovernedSceneEvidence,
        &'w VisualValleyQualityStats,
        Option<&'w GpuDiagnostic>,
    ) {
        let evidence = GovernedSceneEvidence {
            visual_valley: self.visual_valley.clone(),
            shadow: self.shadow.clone(),
            vegetation: self.vegetation.clone(),
            scenery: self.scenery.clone(),
            environment: self.environment.clone(),
            water: self.water.clone(),
            weather: self.weather.clone(),
        };
        (
            evidence,
            self.quality.into_inner(),
            self.gpu.map(Res::into_inner),
        )
    }
}

#[derive(Clone, Serialize)]
struct CameraEvidence {
    translation_m: [f32; 3],
    forward: [f32; 3],
    vertical_fov_degrees: f32,
    active: bool,
}

#[derive(Clone, Serialize)]
struct WindowEvidence {
    physical_width: u32,
    physical_height: u32,
    scale_factor: f32,
    visible: bool,
    resizable: bool,
}

#[derive(Clone, Serialize)]
struct FrozenStateEvidence {
    sources: SourceHashes,
    seeds: SourceSeeds,
    tick: ClockTickEvidence,
    vsync_requested: bool,
    expected_weather: &'static str,
    weather_target: String,
    weather_wetness_current_bits: u32,
    weather_wetness_target_bits: u32,
    quality_selection: String,
    quality_startup_tier: String,
    quality_current_tier: String,
    quality_transition_count: u64,
    pipeline_total: usize,
    pipeline_ready: usize,
    camera: CameraEvidence,
    window: WindowEvidence,
    gpu: GpuDiagnostic,
    governed_scene_hashes: GovernedSceneHashes,
}

#[derive(Clone, Serialize)]
struct ClockTickEvidence {
    virtual_elapsed_nanoseconds: u64,
    fixed_elapsed_nanoseconds: u64,
    fixed_timestep_nanoseconds: u64,
    fixed_tick: u64,
}

#[derive(Clone, Serialize)]
struct GovernedSceneHashes {
    visual_valley: String,
    shadow: String,
    vegetation: String,
    scenery: String,
    environment: String,
    water_static: String,
    weather_static: String,
}

#[derive(Serialize)]
struct WaterStaticEvidence<'a> {
    source_hash: &'a str,
    terrain_source_hash: &'a str,
    vertices: usize,
    triangles: usize,
    water_mesh_entities: usize,
    river_bank_mesh_entities: usize,
    runtime_observation: String,
    requested_quality: String,
    shader_asset_path: &'a str,
}

#[derive(Serialize)]
struct WeatherStaticEvidence<'a> {
    source_hash: &'a str,
    status: String,
    target_state: String,
    rain_batch_entities: usize,
    rain_vertices: usize,
    rain_triangles: usize,
    wetness_current_bits: u32,
    wetness_target_bits: u32,
    camera_entities: usize,
    active_camera_entities: usize,
}

#[derive(Clone, Debug, Deserialize, Serialize)]
#[serde(deny_unknown_fields)]
struct CaptureSignal {
    schema: String,
    schema_version: u32,
    nonce: String,
    process_id: u32,
    source_commit: String,
    ready_sha256: String,
    capture_id: String,
    renderdoc: RenderDocSignal,
}

#[derive(Clone, Debug, Deserialize, Serialize)]
#[serde(deny_unknown_fields)]
struct RenderDocSignal {
    capture_triggered: bool,
    target_control_ident: u32,
    target_api: String,
    tool_version: String,
    qrenderdoc_sha256: String,
    renderdoccmd_sha256: String,
    renderdoc_dll_sha256: String,
}

#[derive(Serialize)]
struct ReadyReceipt<'a> {
    schema: &'static str,
    schema_version: u32,
    status: &'static str,
    nonce: &'a str,
    process_id: u32,
    source_commit: &'static str,
    build_profile: &'static str,
    required_backend: &'static str,
    required_renderdoc_version: &'static str,
    ready_frame: u32,
    freeze_settle_frames: u32,
    virtual_time_paused: bool,
    frozen_state_sha256: &'a str,
    frozen_state: &'a FrozenStateEvidence,
    files: FileContract,
    qualification: QualificationBoundary,
}

#[derive(Serialize)]
struct RuntimeLedger<'a> {
    schema: &'static str,
    schema_version: u32,
    status: &'static str,
    nonce: &'a str,
    process_id: u32,
    source_commit: &'static str,
    build_profile: &'static str,
    ready_frame: u32,
    signal_frame: u32,
    capture_window_frames: u32,
    ready_sha256: &'a str,
    capture_id: &'a str,
    renderdoc: &'a RenderDocSignal,
    frozen_state_sha256: &'a str,
    frozen_state_initial: &'a FrozenStateEvidence,
    frozen_state_final: &'a FrozenStateEvidence,
    frozen_state_stable: bool,
    files: FileContract,
    qualification: QualificationBoundary,
}

#[derive(Clone, Copy, Serialize)]
struct FileContract {
    ready: &'static str,
    signal: &'static str,
    ledger: &'static str,
    failure: &'static str,
}

#[derive(Clone, Copy)]
struct PendingExternalQualification;

impl Serialize for PendingExternalQualification {
    fn serialize<S>(&self, serializer: S) -> Result<S::Ok, S::Error>
    where
        S: serde::Serializer,
    {
        serializer.serialize_bool(false)
    }
}

#[derive(Clone, Copy, Serialize)]
struct QualificationBoundary {
    runtime_state_qualified: bool,
    external_capture_observed_by_runtime: PendingExternalQualification,
    renderdoc_analysis_completed_by_runtime: PendingExternalQualification,
    gate1_route_qualified: PendingExternalQualification,
    limitation: &'static str,
}

fn file_contract() -> FileContract {
    FileContract {
        ready: READY_FILE,
        signal: SIGNAL_FILE,
        ledger: LEDGER_FILE,
        failure: FAILURE_FILE,
    }
}

fn qualification_boundary(runtime_state_qualified: bool) -> QualificationBoundary {
    QualificationBoundary {
        runtime_state_qualified,
        external_capture_observed_by_runtime: PendingExternalQualification,
        renderdoc_analysis_completed_by_runtime: PendingExternalQualification,
        gate1_route_qualified: PendingExternalQualification,
        limitation: "the runtime validates only its frozen capture window; the host and qrenderdoc adapter must validate the external capture and action counters",
    }
}

#[allow(
    clippy::too_many_arguments,
    reason = "one observer validates every state that can invalidate the external frame capture"
)]
fn drive_capture_protocol(
    mut runtime: ResMut<CaptureRuntime>,
    frame_count: Res<FrameCount>,
    pipeline: Res<PipelineWarmupObservation>,
    scene: GovernedScene,
    windows: Query<&Window, With<PrimaryWindow>>,
    cameras: Query<(&Camera, &Transform, &Projection), With<Camera3d>>,
    mut virtual_time: ResMut<Time<Virtual>>,
    mut fixed_time: ResMut<Time<Fixed>>,
    mut exit: MessageWriter<AppExit>,
) {
    if runtime.phase.is_terminal() {
        return;
    }
    if capture_watchdog_expired(&mut runtime, &mut exit) {
        return;
    }

    let frame_count = frame_count.into_inner();
    let pipeline = pipeline.into_inner();
    let (scene_evidence, quality, gpu) = scene.into_observation();
    let Some(gpu) = gpu else {
        if runtime.phase != Phase::WaitingForReady {
            fail(
                &mut runtime,
                &mut exit,
                "GPU identity disappeared after readiness",
            );
        }
        return;
    };

    if runtime.phase == Phase::WaitingForReady {
        if !pipeline.is_ready()
            || scene_evidence.scenery.status != SceneryProjectionStatus::Activated
            || scene_evidence.validate(quality).is_err()
        {
            return;
        }
        let fixed_timestep = fixed_time.timestep();
        *virtual_time = Time::<Virtual>::default();
        virtual_time.pause();
        *fixed_time = Time::<Fixed>::from_duration(fixed_timestep);
        let observation = observe_frozen_state(
            &runtime.configuration,
            pipeline,
            &scene_evidence,
            quality,
            gpu,
            &windows,
            &cameras,
            &virtual_time,
            &fixed_time,
        );
        let Some((observation, observation_hash)) =
            capture_result_or_fail(&mut runtime, &mut exit, observation)
        else {
            return;
        };
        runtime.frozen_state_sha256 = Some(observation_hash);
        runtime.frozen_state = Some(observation);
        runtime.phase = Phase::FreezeSettling;
        return;
    }

    if !virtual_time.is_paused() {
        fail(
            &mut runtime,
            &mut exit,
            "virtual time resumed during the frozen capture protocol",
        );
        return;
    }
    let overstep = fixed_time.overstep();
    fixed_time.discard_overstep(overstep);
    let observation = observe_frozen_state(
        &runtime.configuration,
        pipeline,
        &scene_evidence,
        quality,
        gpu,
        &windows,
        &cameras,
        &virtual_time,
        &fixed_time,
    );
    let Some((observation, observation_hash)) =
        capture_result_or_fail(&mut runtime, &mut exit, observation)
    else {
        return;
    };
    if runtime.frozen_state_sha256.as_deref() != Some(observation_hash.as_str()) {
        let reason = format!(
            "governed scene, camera, window, quality or GPU identity changed after freeze: initial={}, observed={}",
            serde_json::to_string(&runtime.frozen_state).unwrap_or_default(),
            serde_json::to_string(&observation).unwrap_or_default(),
        );
        fail(&mut runtime, &mut exit, &reason);
        return;
    }

    advance_capture_phase(
        &mut runtime,
        frame_count.0,
        &observation,
        &observation_hash,
        &mut exit,
    );
}

fn capture_watchdog_expired(
    runtime: &mut CaptureRuntime,
    exit: &mut MessageWriter<AppExit>,
) -> bool {
    if runtime.started_at.elapsed() <= WATCHDOG {
        return false;
    }
    fail(
        runtime,
        exit,
        &format!("capture protocol exceeded {} seconds", WATCHDOG.as_secs()),
    );
    true
}

fn capture_result_or_fail<T>(
    runtime: &mut CaptureRuntime,
    exit: &mut MessageWriter<AppExit>,
    result: Result<T, String>,
) -> Option<T> {
    match result {
        Ok(value) => Some(value),
        Err(error) => {
            fail(runtime, exit, &error);
            None
        }
    }
}

fn advance_capture_phase(
    runtime: &mut CaptureRuntime,
    frame_count: u32,
    observation: &FrozenStateEvidence,
    observation_hash: &str,
    exit: &mut MessageWriter<AppExit>,
) {
    match runtime.phase {
        Phase::FreezeSettling => {
            runtime.freeze_settle_frames = runtime.freeze_settle_frames.saturating_add(1);
            if runtime.freeze_settle_frames < FREEZE_SETTLE_FRAMES {
                return;
            }
            runtime.ready_frame = Some(frame_count);
            if let Err(error) = write_ready(runtime) {
                fail(runtime, exit, &error);
                return;
            }
            tracing::info!(
                target: "forge::proof",
                schema = "forge.runtime-event",
                schema_version = 1_u64,
                event_kind = "gate1_renderdoc_ready",
                nonce = %runtime.configuration.nonce,
                process_id = u64::from(std::process::id()),
                frozen_state_sha256 = %observation_hash,
                "Gate 1 RenderDoc runtime is frozen and ready"
            );
            runtime.phase = Phase::WaitingForSignal;
        }
        Phase::WaitingForSignal => {
            let signal_path = runtime.configuration.output_dir.join(SIGNAL_FILE);
            let signal = match read_signal(&signal_path) {
                Ok(Some(signal)) => signal,
                Ok(None) => return,
                Err(error) => {
                    fail(runtime, exit, &error);
                    return;
                }
            };
            if let Err(error) = validate_signal(&signal, runtime) {
                fail(runtime, exit, &error);
                return;
            }
            runtime.signal = Some(signal);
            runtime.signal_frame = Some(frame_count);
            runtime.phase = Phase::CaptureWindow;
        }
        Phase::CaptureWindow => {
            runtime.capture_window_frames = runtime.capture_window_frames.saturating_add(1);
            if runtime.capture_window_frames < CAPTURE_WINDOW_FRAMES {
                return;
            }
            if let Err(error) = write_ledger(runtime, observation) {
                fail(runtime, exit, &error);
                return;
            }
            runtime.phase = Phase::Completed;
            tracing::info!(
                target: "forge::proof",
                schema = "forge.runtime-event",
                schema_version = 1_u64,
                event_kind = "gate1_renderdoc_capture_window_completed",
                nonce = %runtime.configuration.nonce,
                capture_window_frames = u64::from(runtime.capture_window_frames),
                external_capture_observed = false,
                "Gate 1 RenderDoc runtime window completed pending external analysis"
            );
            exit.write(AppExit::Success);
        }
        Phase::WaitingForReady | Phase::Completed | Phase::Failed => {}
    }
}

#[allow(
    clippy::too_many_arguments,
    reason = "the fingerprint deliberately binds every independently governed runtime source"
)]
fn observe_frozen_state(
    configuration: &Configuration,
    pipeline: &PipelineWarmupObservation,
    scene: &GovernedSceneEvidence,
    quality: &VisualValleyQualityStats,
    gpu: &GpuDiagnostic,
    windows: &Query<&Window, With<PrimaryWindow>>,
    cameras: &Query<(&Camera, &Transform, &Projection), With<Camera3d>>,
    virtual_time: &Time<Virtual>,
    fixed_time: &Time<Fixed>,
) -> Result<(FrozenStateEvidence, String), String> {
    validate_frozen_preconditions(configuration, pipeline, scene, quality, gpu)?;
    let tick = observe_clock_tick(virtual_time, fixed_time)?;

    let primary_window = windows
        .single()
        .map_err(|_| "Gate 1 RenderDoc requires exactly one primary window".to_owned())?;
    if primary_window.present_mode != PresentMode::AutoNoVsync {
        return Err(format!(
            "Gate 1 RenderDoc requires the AutoNoVsync presentation request, observed {:?}",
            primary_window.present_mode
        ));
    }
    let window = WindowEvidence {
        physical_width: primary_window.resolution.physical_width(),
        physical_height: primary_window.resolution.physical_height(),
        scale_factor: primary_window.resolution.scale_factor(),
        visible: primary_window.visible,
        resizable: primary_window.resizable,
    };
    if window.physical_width != REQUIRED_WIDTH
        || window.physical_height != REQUIRED_HEIGHT
        || !window.visible
    {
        return Err(format!(
            "Gate 1 RenderDoc requires a visible {REQUIRED_WIDTH}x{REQUIRED_HEIGHT} surface, observed {}x{} visible={}",
            window.physical_width, window.physical_height, window.visible
        ));
    }

    let (camera, transform, projection) = cameras
        .single()
        .map_err(|_| "Gate 1 RenderDoc requires exactly one 3D camera".to_owned())?;
    let Projection::Perspective(perspective) = projection else {
        return Err("Gate 1 RenderDoc requires a perspective camera".to_owned());
    };
    if !camera.is_active || !transform.translation.is_finite() {
        return Err("Gate 1 RenderDoc camera is inactive or non-finite".to_owned());
    }
    let camera = CameraEvidence {
        translation_m: transform.translation.to_array(),
        forward: transform.forward().as_vec3().to_array(),
        vertical_fov_degrees: perspective.fov.to_degrees(),
        active: camera.is_active,
    };

    let water_static = WaterStaticEvidence {
        source_hash: &scene.water.source_hash,
        terrain_source_hash: &scene.water.terrain_source_hash,
        vertices: scene.water.vertices,
        triangles: scene.water.triangles,
        water_mesh_entities: scene.water.water_mesh_entities,
        river_bank_mesh_entities: scene.water.river_bank_mesh_entities,
        runtime_observation: format!("{:?}", scene.water.runtime_observation),
        requested_quality: format!("{:?}", scene.water.resolved_optics.requested_quality),
        shader_asset_path: scene.water.shader_asset_path,
    };
    let weather_static = WeatherStaticEvidence {
        source_hash: &scene.weather.source_hash,
        status: format!("{:?}", scene.weather.status),
        target_state: format!("{:?}", scene.weather.target_state),
        rain_batch_entities: scene.weather.rain.batch_entities,
        rain_vertices: scene.weather.rain.vertices,
        rain_triangles: scene.weather.rain.triangles,
        wetness_current_bits: scene.weather.wetness.current.to_bits(),
        wetness_target_bits: scene.weather.wetness.target.to_bits(),
        camera_entities: scene.weather.post_processing.camera_3d_entities,
        active_camera_entities: scene.weather.post_processing.active_camera_3d_entities,
    };

    let evidence = FrozenStateEvidence {
        sources: configuration.sources.clone(),
        seeds: configuration.seeds.clone(),
        tick,
        vsync_requested: false,
        expected_weather: weather_label(configuration.weather),
        weather_target: format!("{:?}", scene.weather.target_state),
        weather_wetness_current_bits: scene.weather.wetness.current.to_bits(),
        weather_wetness_target_bits: scene.weather.wetness.target.to_bits(),
        quality_selection: format!("{:?}", quality.selection),
        quality_startup_tier: format!("{:?}", quality.startup_tier),
        quality_current_tier: format!("{:?}", quality.current_tier),
        quality_transition_count: quality.transition_count,
        pipeline_total: pipeline.total,
        pipeline_ready: pipeline.ready,
        camera,
        window,
        gpu: gpu.clone(),
        governed_scene_hashes: GovernedSceneHashes {
            visual_valley: content_hash(&scene.visual_valley, "Visual Valley observation")?,
            shadow: content_hash(&scene.shadow, "shadow observation")?,
            vegetation: vegetation_static_hash(&scene.vegetation)?,
            scenery: content_hash(&scene.scenery, "scenery observation")?,
            environment: content_hash(&scene.environment, "environment observation")?,
            water_static: content_hash(&water_static, "static water observation")?,
            weather_static: content_hash(&weather_static, "static weather observation")?,
        },
    };
    let hash = content_hash(&evidence, "frozen state")?;
    Ok((evidence, hash))
}

fn vegetation_static_hash(observation: &VegetationProjectionStats) -> Result<String, String> {
    let mut static_observation = observation.clone();
    // Audit age advances with main-world frames even when virtual time is paused.
    // Keep every actual visibility count and projection field in the fingerprint.
    static_observation.visibility.observation_frames = 0;
    content_hash(&static_observation, "static vegetation observation")
}

fn validate_frozen_preconditions(
    configuration: &Configuration,
    pipeline: &PipelineWarmupObservation,
    scene: &GovernedSceneEvidence,
    quality: &VisualValleyQualityStats,
    gpu: &GpuDiagnostic,
) -> Result<(), String> {
    scene.validate(quality)?;
    validate_sources(configuration, scene, quality)?;
    validate_quality(quality)?;
    if scene.weather.target_state != configuration.weather
        || scene.weather.wetness.current.to_bits() != scene.weather.wetness.target.to_bits()
    {
        return Err(format!(
            "weather is not fixed at {:?}: target={:?}, wetness={}/{}",
            configuration.weather,
            scene.weather.target_state,
            scene.weather.wetness.current,
            scene.weather.wetness.target
        ));
    }
    if !pipeline.is_ready()
        || pipeline.errors != 0
        || pipeline.total == 0
        || pipeline.ready != pipeline.total
        || pipeline.queued != 0
        || pipeline.creating != 0
        || pipeline.waiting != 0
    {
        return Err(format!(
            "pipelines are not stably ready: status={:?}, total={}, ready={}, queued={}, creating={}, waiting={}, errors={}",
            pipeline.status,
            pipeline.total,
            pipeline.ready,
            pipeline.queued,
            pipeline.creating,
            pipeline.waiting,
            pipeline.errors
        ));
    }
    if gpu.backend != "Vulkan" || gpu.name.trim().is_empty() {
        return Err(format!(
            "Gate 1 RenderDoc currently requires an identified Vulkan adapter, observed backend={:?}, name={:?}",
            gpu.backend, gpu.name
        ));
    }
    Ok(())
}

fn observe_clock_tick(
    virtual_time: &Time<Virtual>,
    fixed_time: &Time<Fixed>,
) -> Result<ClockTickEvidence, String> {
    if !virtual_time.is_paused()
        || !virtual_time.elapsed().is_zero()
        || !virtual_time.delta().is_zero()
        || !fixed_time.elapsed().is_zero()
        || !fixed_time.delta().is_zero()
        || !fixed_time.overstep().is_zero()
    {
        return Err(format!(
            "Gate 1 RenderDoc clock is not frozen at tick {FROZEN_CAPTURE_TICK}: virtual_paused={}, virtual_elapsed={:?}, virtual_delta={:?}, fixed_elapsed={:?}, fixed_delta={:?}, fixed_overstep={:?}",
            virtual_time.is_paused(),
            virtual_time.elapsed(),
            virtual_time.delta(),
            fixed_time.elapsed(),
            fixed_time.delta(),
            fixed_time.overstep()
        ));
    }
    let fixed_timestep_nanoseconds = u64::try_from(fixed_time.timestep().as_nanos())
        .map_err(|error| format!("Gate 1 RenderDoc fixed timestep exceeds uint64: {error}"))?;
    if fixed_timestep_nanoseconds == 0 {
        return Err("Gate 1 RenderDoc fixed timestep cannot be zero".to_owned());
    }
    Ok(ClockTickEvidence {
        virtual_elapsed_nanoseconds: 0,
        fixed_elapsed_nanoseconds: 0,
        fixed_timestep_nanoseconds,
        fixed_tick: FROZEN_CAPTURE_TICK,
    })
}

fn validate_sources(
    configuration: &Configuration,
    scene: &GovernedSceneEvidence,
    quality: &VisualValleyQualityStats,
) -> Result<(), String> {
    let expected = &configuration.sources;
    let observed = [
        (
            "terrain",
            expected.terrain.as_str(),
            scene.visual_valley.source_hash.as_str(),
        ),
        (
            "vegetation",
            expected.vegetation.as_str(),
            scene.vegetation.vegetation_document_hash.as_str(),
        ),
        (
            "environment",
            expected.environment.as_str(),
            scene.environment.source_hash.as_str(),
        ),
        (
            "water",
            expected.water.as_str(),
            scene.water.source_hash.as_str(),
        ),
        (
            "weather",
            expected.weather.as_str(),
            scene.weather.source_hash.as_str(),
        ),
        (
            "quality",
            expected.quality.as_str(),
            quality.profile_hash.as_str(),
        ),
    ];
    if let Some((label, expected, actual)) = observed
        .into_iter()
        .find(|(_, expected, actual)| expected != actual)
    {
        return Err(format!(
            "Gate 1 RenderDoc {label} source hash changed: expected {expected}, observed {actual}"
        ));
    }
    if expected.cadle_assets_used {
        return Err("Gate 1 RenderDoc source contract cannot use Cadle assets".to_owned());
    }
    Ok(())
}

fn validate_quality(quality: &VisualValleyQualityStats) -> Result<(), String> {
    if quality.selection != VisualValleyQualitySelection::Fixed(VisualQualityTier::High)
        || quality.startup_tier != VisualQualityTier::High
        || quality.current_tier != VisualQualityTier::High
        || quality.dynamically_applied_tier != Some(VisualQualityTier::High)
        || quality.adaptation_enabled
        || quality.transition_count != 0
    {
        return Err(format!(
            "Gate 1 RenderDoc quality is not fixed High: selection={:?}, startup={:?}, current={:?}, dynamic={:?}, adaptation={}, transitions={}",
            quality.selection,
            quality.startup_tier,
            quality.current_tier,
            quality.dynamically_applied_tier,
            quality.adaptation_enabled,
            quality.transition_count
        ));
    }
    Ok(())
}

fn write_ready(runtime: &CaptureRuntime) -> Result<(), String> {
    let frozen_state = runtime
        .frozen_state
        .as_ref()
        .ok_or("frozen state is absent at ready publication")?;
    let frozen_state_sha256 = runtime
        .frozen_state_sha256
        .as_deref()
        .ok_or("frozen state hash is absent at ready publication")?;
    let receipt = ReadyReceipt {
        schema: READY_SCHEMA,
        schema_version: READY_SCHEMA_VERSION,
        status: "ready_frozen_pending_external_capture_signal",
        nonce: &runtime.configuration.nonce,
        process_id: std::process::id(),
        source_commit: embedded_source_commit()?,
        build_profile: embedded_build_profile()?,
        required_backend: "Vulkan",
        required_renderdoc_version: REQUIRED_RENDERDOC_VERSION,
        ready_frame: runtime.ready_frame.ok_or("ready frame is absent")?,
        freeze_settle_frames: runtime.freeze_settle_frames,
        virtual_time_paused: true,
        frozen_state_sha256,
        frozen_state,
        files: file_contract(),
        qualification: qualification_boundary(false),
    };
    write_json_atomic(&runtime.configuration.output_dir.join(READY_FILE), &receipt)
}

fn write_ledger(runtime: &CaptureRuntime, final_state: &FrozenStateEvidence) -> Result<(), String> {
    let signal = runtime.signal.as_ref().ok_or("capture signal is absent")?;
    let initial = runtime
        .frozen_state
        .as_ref()
        .ok_or("initial frozen state is absent")?;
    let frozen_state_sha256 = runtime
        .frozen_state_sha256
        .as_deref()
        .ok_or("frozen state hash is absent")?;
    let final_hash = content_hash(final_state, "final frozen state")?;
    if final_hash != frozen_state_sha256 {
        return Err(format!(
            "final frozen state hash changed: initial={frozen_state_sha256}, final={final_hash}"
        ));
    }
    let ledger = RuntimeLedger {
        schema: LEDGER_SCHEMA,
        schema_version: LEDGER_SCHEMA_VERSION,
        status: "stable_capture_window_completed_pending_external_capture_analysis",
        nonce: &runtime.configuration.nonce,
        process_id: std::process::id(),
        source_commit: embedded_source_commit()?,
        build_profile: embedded_build_profile()?,
        ready_frame: runtime.ready_frame.ok_or("ready frame is absent")?,
        signal_frame: runtime.signal_frame.ok_or("signal frame is absent")?,
        capture_window_frames: runtime.capture_window_frames,
        ready_sha256: &signal.ready_sha256,
        capture_id: &signal.capture_id,
        renderdoc: &signal.renderdoc,
        frozen_state_sha256,
        frozen_state_initial: initial,
        frozen_state_final: final_state,
        frozen_state_stable: true,
        files: file_contract(),
        qualification: qualification_boundary(true),
    };
    write_json_atomic(&runtime.configuration.output_dir.join(LEDGER_FILE), &ledger)
}

fn read_signal(path: &Path) -> Result<Option<CaptureSignal>, String> {
    let metadata = match fs::symlink_metadata(path) {
        Ok(metadata) => metadata,
        Err(error) if error.kind() == std::io::ErrorKind::NotFound => return Ok(None),
        Err(error) => {
            return Err(format!(
                "cannot inspect capture signal {}: {error}",
                path.display()
            ));
        }
    };
    if metadata.file_type().is_symlink() || !metadata.is_file() {
        return Err(format!(
            "capture signal must be a regular non-symlink file: {}",
            path.display()
        ));
    }
    if metadata.len() == 0 || metadata.len() > MAX_SIGNAL_BYTES {
        return Err(format!(
            "capture signal size {} is outside 1..={MAX_SIGNAL_BYTES} bytes",
            metadata.len()
        ));
    }
    let file = fs::File::open(path)
        .map_err(|error| format!("cannot open capture signal {}: {error}", path.display()))?;
    let mut bytes = Vec::with_capacity(
        usize::try_from(metadata.len())
            .map_err(|error| format!("signal size overflow: {error}"))?,
    );
    file.take(MAX_SIGNAL_BYTES + 1)
        .read_to_end(&mut bytes)
        .map_err(|error| format!("cannot read capture signal {}: {error}", path.display()))?;
    if bytes.len() as u64 != metadata.len() {
        return Err("capture signal changed while being read".to_owned());
    }
    serde_json::from_slice(&bytes)
        .map(Some)
        .map_err(|error| format!("invalid capture signal {}: {error}", path.display()))
}

fn validate_signal(signal: &CaptureSignal, runtime: &CaptureRuntime) -> Result<(), String> {
    if signal.schema != SIGNAL_SCHEMA || signal.schema_version != SIGNAL_SCHEMA_VERSION {
        return Err(format!(
            "capture signal schema mismatch: {}/{}",
            signal.schema, signal.schema_version
        ));
    }
    if signal.nonce != runtime.configuration.nonce {
        return Err("capture signal nonce does not match runtime nonce".to_owned());
    }
    if signal.process_id != std::process::id() {
        return Err(format!(
            "capture signal PID {} does not match runtime PID {}",
            signal.process_id,
            std::process::id()
        ));
    }
    if signal.source_commit != embedded_source_commit()? {
        return Err("capture signal commit does not match embedded runtime commit".to_owned());
    }
    validate_sha256(&signal.ready_sha256, "ready_sha256")?;
    let ready_sha256 = canonical_json_file_sha256(
        &runtime.configuration.output_dir.join(READY_FILE),
        MAX_READY_BYTES,
        "ready receipt",
    )?;
    if signal.ready_sha256 != ready_sha256 {
        return Err(format!(
            "capture signal ready SHA-256 does not match the atomic ready receipt: expected {ready_sha256}, observed {}",
            signal.ready_sha256
        ));
    }
    validate_capture_id(&signal.capture_id)?;
    if !signal.renderdoc.capture_triggered
        || signal.renderdoc.target_control_ident == 0
        || signal.renderdoc.target_api != "Vulkan"
        || signal.renderdoc.tool_version != REQUIRED_RENDERDOC_VERSION
    {
        return Err(format!(
            "capture signal does not describe an armed RenderDoc {REQUIRED_RENDERDOC_VERSION} Vulkan capture"
        ));
    }
    validate_sha256(&signal.renderdoc.qrenderdoc_sha256, "qrenderdoc_sha256")?;
    validate_sha256(&signal.renderdoc.renderdoccmd_sha256, "renderdoccmd_sha256")?;
    validate_sha256(
        &signal.renderdoc.renderdoc_dll_sha256,
        "renderdoc_dll_sha256",
    )?;
    Ok(())
}

pub(crate) fn validate_nonce(value: &str) -> Result<(), String> {
    if value.len() != 32
        || !value.bytes().all(|byte| byte.is_ascii_hexdigit())
        || value.bytes().any(|byte| byte.is_ascii_uppercase())
    {
        return Err(
            "Gate 1 RenderDoc nonce must be exactly 32 lowercase hexadecimal characters".to_owned(),
        );
    }
    Ok(())
}

fn validate_sha256(value: &str, label: &str) -> Result<(), String> {
    if value.len() != 64
        || !value.bytes().all(|byte| byte.is_ascii_hexdigit())
        || value.bytes().any(|byte| byte.is_ascii_uppercase())
    {
        return Err(format!(
            "capture signal {label} must be a lowercase SHA-256"
        ));
    }
    Ok(())
}

fn validate_capture_id(value: &str) -> Result<(), String> {
    if value.is_empty()
        || value.len() > 96
        || !value
            .bytes()
            .all(|byte| byte.is_ascii_alphanumeric() || matches!(byte, b'-' | b'_' | b'.' | b':'))
    {
        return Err("capture_id is empty, too long or contains unsupported characters".to_owned());
    }
    Ok(())
}

fn embedded_source_commit() -> Result<&'static str, String> {
    let value = option_env!("FORGE_SOURCE_COMMIT").ok_or_else(|| {
        "FORGE_SOURCE_COMMIT was not embedded; rebuild from a clean exact commit".to_owned()
    })?;
    if value.len() != 40 || !value.bytes().all(|byte| byte.is_ascii_hexdigit()) {
        return Err(format!(
            "embedded FORGE_SOURCE_COMMIT `{value}` is not a 40-character Git object id"
        ));
    }
    Ok(value)
}

fn embedded_build_profile() -> Result<&'static str, String> {
    let value = env!("FORGE_BUILD_PROFILE");
    if value != "release" {
        return Err(format!(
            "Gate 1 RenderDoc acquisition requires Cargo profile `release`, observed `{value}`"
        ));
    }
    Ok(value)
}

fn canonical_json_file_sha256(
    path: &Path,
    maximum_bytes: u64,
    label: &str,
) -> Result<String, String> {
    let metadata = fs::symlink_metadata(path)
        .map_err(|error| format!("cannot inspect {label} {}: {error}", path.display()))?;
    if metadata.file_type().is_symlink() || !metadata.is_file() {
        return Err(format!(
            "{label} must be a regular non-symlink file: {}",
            path.display()
        ));
    }
    if metadata.len() == 0 || metadata.len() > maximum_bytes {
        return Err(format!(
            "{label} size {} is outside 1..={maximum_bytes} bytes",
            metadata.len()
        ));
    }
    let bytes = fs::read(path).map_err(|error| format!("cannot read {label}: {error}"))?;
    if bytes.len() as u64 != metadata.len() {
        return Err(format!("{label} changed while being read"));
    }
    let value: serde_json::Value =
        serde_json::from_slice(&bytes).map_err(|error| format!("cannot parse {label}: {error}"))?;
    let canonical = canonical_json_bytes(&value)
        .map_err(|error| format!("cannot canonicalize {label}: {error}"))?;
    if bytes != canonical {
        return Err(format!("{label} bytes are not canonical JSON"));
    }
    canonical_json_sha256(&value)
        .map(|hash| hash.to_string())
        .map_err(|error| format!("cannot hash {label}: {error}"))
}

fn write_json_atomic(path: &Path, value: &impl Serialize) -> Result<(), String> {
    if path.exists() {
        return Err(format!("refusing to replace proof file {}", path.display()));
    }
    let file_name = path
        .file_name()
        .and_then(|name| name.to_str())
        .ok_or_else(|| format!("proof path has no UTF-8 filename: {}", path.display()))?;
    let temporary = path.with_file_name(format!(".{file_name}.tmp"));
    let json = canonical_json_bytes(value)
        .map_err(|error| format!("cannot serialize canonical proof JSON: {error}"))?;
    let mut file = fs::OpenOptions::new()
        .write(true)
        .create_new(true)
        .open(&temporary)
        .map_err(|error| format!("cannot create {}: {error}", temporary.display()))?;
    if let Err(error) = file.write_all(&json).and_then(|()| file.sync_all()) {
        drop(file);
        let _ = fs::remove_file(&temporary);
        return Err(format!("cannot persist {}: {error}", temporary.display()));
    }
    drop(file);
    if let Err(error) = fs::hard_link(&temporary, path) {
        let _ = fs::remove_file(&temporary);
        return Err(format!(
            "cannot atomically publish {} as {}: {error}",
            temporary.display(),
            path.display()
        ));
    }
    fs::remove_file(&temporary).map_err(|error| {
        format!(
            "published {} but could not remove temporary link {}: {error}",
            path.display(),
            temporary.display()
        )
    })
}

fn fail(runtime: &mut CaptureRuntime, exit: &mut MessageWriter<AppExit>, reason: &str) {
    if runtime.phase.is_terminal() {
        return;
    }
    let phase = runtime.phase;
    let failure = runtime.configuration.output_dir.join(FAILURE_FILE);
    if !failure.exists() {
        let _ = fs::write(
            &failure,
            format!(
                "schema=forge.gate1-renderdoc-failure\nschema_version=1\nphase={}\nreason={reason}\n",
                phase.label()
            ),
        );
    }
    tracing::error!(
        target: "forge::proof",
        schema = "forge.runtime-event",
        schema_version = 1_u64,
        event_kind = "gate1_renderdoc_failed",
        phase = phase.label(),
        error = reason,
        failure_file = %failure.display(),
        "Gate 1 RenderDoc runtime protocol failed"
    );
    runtime.phase = Phase::Failed;
    exit.write(AppExit::error());
}

const fn weather_label(weather: WeatherState) -> &'static str {
    match weather {
        WeatherState::Clear => "clear",
        WeatherState::Rain => "rain",
    }
}

#[cfg(test)]
mod tests {
    use super::*;

    #[test]
    fn vegetation_fingerprint_ignores_audit_age_but_detects_graphical_changes() {
        let terrain = TerrainDocument::from_json(include_str!(
            "../../../examples/visual_valley/terrain.v1.json"
        ))
        .unwrap();
        let vegetation = VegetationDocument::from_json(include_str!(
            "../../../examples/visual_valley/vegetation.v1.json"
        ))
        .unwrap();
        let mut observation =
            forge_engine::inspect_vegetation_projection(&terrain, &vegetation).unwrap();
        observation.visibility.observation_frames = 17;
        let initial = vegetation_static_hash(&observation).unwrap();
        assert_eq!(observation.visibility.observation_frames, 17);
        observation.visibility.observation_frames = 18;
        assert_eq!(initial, vegetation_static_hash(&observation).unwrap());
        observation.visibility.total.cpu_visible_for_camera += 1;
        assert_ne!(initial, vegetation_static_hash(&observation).unwrap());
        observation.visibility.total.cpu_visible_for_camera -= 1;
        observation.projection.total_entities += 1;
        assert_ne!(initial, vegetation_static_hash(&observation).unwrap());
    }

    #[test]
    fn fixed_high_capture_requires_the_quality_tier_to_be_applied() {
        let profile = VisualValleyQualityProfileDocument::from_json(include_str!(
            "../../../examples/visual_valley/quality-profile.v1.json"
        ))
        .unwrap();
        let mut app = App::new();
        app.add_plugins(forge_engine::VisualValleyQualityPlugin::new(
            profile,
            VisualValleyQualitySelection::Fixed(VisualQualityTier::High),
        ));
        assert!(validate_quality(app.world().resource::<VisualValleyQualityStats>()).is_err());
        app.update();
        let applied = app.world().resource::<VisualValleyQualityStats>().clone();
        assert_eq!(
            applied.dynamically_applied_tier,
            Some(VisualQualityTier::High)
        );
        assert!(validate_quality(&applied).is_ok());

        let mut wrong_tier = applied.clone();
        wrong_tier.dynamically_applied_tier = Some(VisualQualityTier::Low);
        assert!(validate_quality(&wrong_tier).is_err());
        let mut adaptive = applied.clone();
        adaptive.adaptation_enabled = true;
        assert!(validate_quality(&adaptive).is_err());
        let mut transitioned = applied;
        transitioned.transition_count = 1;
        assert!(validate_quality(&transitioned).is_err());
    }

    fn signal() -> CaptureSignal {
        CaptureSignal {
            schema: SIGNAL_SCHEMA.to_owned(),
            schema_version: SIGNAL_SCHEMA_VERSION,
            nonce: "0123456789abcdef0123456789abcdef".to_owned(),
            process_id: std::process::id(),
            source_commit: option_env!("FORGE_SOURCE_COMMIT")
                .unwrap_or("0123456789abcdef0123456789abcdef01234567")
                .to_owned(),
            ready_sha256: "0".repeat(64),
            capture_id: "clear-vulkan-replay-1".to_owned(),
            renderdoc: RenderDocSignal {
                capture_triggered: true,
                target_control_ident: 1,
                target_api: "Vulkan".to_owned(),
                tool_version: REQUIRED_RENDERDOC_VERSION.to_owned(),
                qrenderdoc_sha256: "1".repeat(64),
                renderdoccmd_sha256: "2".repeat(64),
                renderdoc_dll_sha256: "3".repeat(64),
            },
        }
    }

    #[test]
    fn qualification_boundary_keeps_the_fail_closed_boolean_contract() {
        for runtime_state_qualified in [false, true] {
            let actual = serde_json::to_value(qualification_boundary(runtime_state_qualified))
                .expect("qualification boundary must serialize");
            assert_eq!(
                actual,
                serde_json::json!({
                    "runtime_state_qualified": runtime_state_qualified,
                    "external_capture_observed_by_runtime": false,
                    "renderdoc_analysis_completed_by_runtime": false,
                    "gate1_route_qualified": false,
                    "limitation": "the runtime validates only its frozen capture window; the host and qrenderdoc adapter must validate the external capture and action counters",
                })
            );
        }
    }

    #[test]
    fn nonce_contract_is_exact_and_lowercase() {
        assert!(validate_nonce("0123456789abcdef0123456789abcdef").is_ok());
        assert!(validate_nonce("0123456789ABCDEF0123456789ABCDEF").is_err());
        assert!(validate_nonce("0123").is_err());
        assert!(validate_nonce("g123456789abcdef0123456789abcdef").is_err());
    }

    #[test]
    fn signal_fields_are_strictly_deserialised() {
        let signal = signal();
        let json = serde_json::to_value(&signal).unwrap();
        assert_eq!(json["schema"], SIGNAL_SCHEMA);
        let mut unknown = json;
        unknown["unexpected"] = serde_json::Value::Bool(true);
        assert!(serde_json::from_value::<CaptureSignal>(unknown).is_err());
    }

    #[test]
    fn capture_identifiers_and_hashes_are_bounded() {
        assert!(validate_capture_id("rain-vulkan-replay-2").is_ok());
        assert!(validate_capture_id("../capture").is_err());
        assert!(validate_sha256(&"a".repeat(64), "test").is_ok());
        assert!(validate_sha256(&"A".repeat(64), "test").is_err());
    }
}
