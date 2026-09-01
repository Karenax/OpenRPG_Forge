use bevy::{
    app::AppExit,
    diagnostic::FrameCount,
    prelude::*,
    render::view::screenshot::{Screenshot, save_to_disk},
    window::{PresentMode, PrimaryWindow},
};
use forge_engine::{
    GpuDiagnostic, PipelineWarmupObservation, VisualValleyQualitySelection,
    VisualValleyQualityStats, WeatherProjectionStats, WeatherProjectionStatus, WeatherRuntimeState,
};
use forge_project::{
    PlayerControllerProfileDocument, TerrainDocument, WaterDocument, WeatherProfileDocument,
    WeatherState,
};
use forge_world::{TerrainGenerationPlan, sample_river_centerline};
use serde::Serialize;
use std::{
    fs,
    path::{Path, PathBuf},
    time::{Duration, Instant},
};

const SCHEMA: &str = "forge.gate1-visual-proof";
const SCHEMA_VERSION: u32 = 1;
const LEDGER_FILE: &str = "gate1-visual-proof.json";
const FAILURE_FILE: &str = "gate1-visual-proof.failure.txt";
const POSE_SETTLE_FRAMES: u32 = 8;
const CAPTURE_SETTLE_FRAMES: u32 = 6;
const MOVEMENT_FRAME_COUNT: u8 = 24;
const MOVEMENT_VIDEO_FPS: u8 = 8;
const PROOF_TIMEOUT: Duration = Duration::from_secs(90);
const WATER_VIEW_ANCHOR_X_M: f32 = 16.0;
const WATER_VIEW_ANCHOR_Z_M: f32 = 160.0;
const WATER_VIEW_DISTANCE_M: f32 = 14.0;
const MOVEMENT_START_X_M: f32 = 16.0;
const MOVEMENT_START_Z_M: f32 = 8.0;
const MOVEMENT_END_X_M: f32 = -24.0;
const MOVEMENT_END_Z_M: f32 = -56.0;
const NORMALIZED_GRADE_TO_CIE_OFFSET: f32 = 0.1;

/// Native, single-process acquisition harness for the still-open Gate 1 visual routes.
///
/// It records rendered artifacts and CPU/ECS endpoint evidence. It deliberately does not qualify
/// GPU pipeline execution, visual quality, or human acceptance.
pub struct Gate1VisualProofPlugin {
    configuration: ProofConfiguration,
}

impl Gate1VisualProofPlugin {
    pub fn new(
        output_dir: PathBuf,
        terrain: &TerrainDocument,
        water: &WaterDocument,
        weather: &WeatherProfileDocument,
        player: &PlayerControllerProfileDocument,
    ) -> Result<Self, String> {
        let terrain_plan = TerrainGenerationPlan::from_document(terrain)
            .map_err(|error| format!("cannot prepare proof terrain: {error}"))?;
        let eye_height_m = player.camera.eye_height_m;
        let vertical_fov_degrees = player.camera.first_person_vertical_fov_degrees;
        let human_pose = human_camera_pose(&terrain_plan, eye_height_m, vertical_fov_degrees)?;
        let water_pose =
            water_camera_pose(&terrain_plan, water, eye_height_m, vertical_fov_degrees)?;

        Ok(Self {
            configuration: ProofConfiguration {
                output_dir,
                terrain_plan,
                weather_profile: weather.clone(),
                eye_height_m,
                vertical_fov_degrees,
                human_pose,
                water_pose,
                terrain_hash: terrain
                    .content_hash()
                    .map_err(|error| format!("cannot hash proof terrain: {error}"))?
                    .to_string(),
                water_hash: water
                    .content_hash()
                    .map_err(|error| format!("cannot hash proof water: {error}"))?
                    .to_string(),
                weather_hash: weather
                    .content_hash()
                    .map_err(|error| format!("cannot hash proof weather: {error}"))?
                    .to_string(),
                player_profile_hash: player
                    .content_hash()
                    .map_err(|error| format!("cannot hash proof player profile: {error}"))?
                    .to_string(),
            },
        })
    }
}

impl Plugin for Gate1VisualProofPlugin {
    fn build(&self, app: &mut App) {
        app.insert_resource(Gate1VisualProofRuntime::new(self.configuration.clone()))
            .add_systems(Last, advance_gate1_visual_proof);
    }
}

#[derive(Clone)]
struct ProofConfiguration {
    output_dir: PathBuf,
    terrain_plan: TerrainGenerationPlan,
    weather_profile: WeatherProfileDocument,
    eye_height_m: f32,
    vertical_fov_degrees: f32,
    human_pose: CameraPose,
    water_pose: CameraPose,
    terrain_hash: String,
    water_hash: String,
    weather_hash: String,
    player_profile_hash: String,
}

#[derive(Resource)]
struct Gate1VisualProofRuntime {
    configuration: ProofConfiguration,
    started_at: Instant,
    phase: ProofPhase,
    pending_capture: Option<PendingCapture>,
    captures: Vec<CaptureEvidence>,
    weather_checkpoints: Vec<WeatherCheckpoint>,
}

impl Gate1VisualProofRuntime {
    fn new(configuration: ProofConfiguration) -> Self {
        Self {
            configuration,
            started_at: Instant::now(),
            phase: ProofPhase::WaitingForWarmup,
            pending_capture: None,
            captures: Vec::with_capacity(usize::from(MOVEMENT_FRAME_COUNT) + 5),
            weather_checkpoints: Vec::with_capacity(5),
        }
    }
}

#[derive(Clone, Copy, Debug, Eq, PartialEq)]
enum ProofPhase {
    WaitingForWarmup,
    SettlingFixed { shot: FixedShot, ready_frame: u32 },
    TransitioningWeather(WeatherState),
    SettlingMovement { index: u8, ready_frame: u32 },
    AwaitingCapture,
    Finalizing,
    Completed,
    Failed,
}

#[derive(Clone, Copy, Debug, Eq, PartialEq)]
enum FixedShot {
    DistantClear,
    HumanClear,
    WaterClear,
    WaterRain,
    WaterRestored,
}

impl FixedShot {
    const fn id(self) -> &'static str {
        match self {
            Self::DistantClear => "distant-clear",
            Self::HumanClear => "human-clear",
            Self::WaterClear => "water-clear",
            Self::WaterRain => "water-rain",
            Self::WaterRestored => "water-restored",
        }
    }

    const fn relative_path(self) -> &'static str {
        match self {
            Self::DistantClear => "captures/distant-clear.png",
            Self::HumanClear => "captures/human-clear.png",
            Self::WaterClear => "captures/water-clear.png",
            Self::WaterRain => "captures/water-rain.png",
            Self::WaterRestored => "captures/water-restored.png",
        }
    }

    const fn expected_weather(self) -> WeatherState {
        match self {
            Self::WaterRain => WeatherState::Rain,
            Self::DistantClear | Self::HumanClear | Self::WaterClear | Self::WaterRestored => {
                WeatherState::Clear
            }
        }
    }
}

#[derive(Clone, Copy, Debug, Eq, PartialEq)]
enum CaptureKind {
    Fixed(FixedShot),
    Movement(u8),
}

impl CaptureKind {
    fn id(self) -> String {
        match self {
            Self::Fixed(shot) => shot.id().to_owned(),
            Self::Movement(index) => format!("movement-{index:03}"),
        }
    }

    fn relative_path(self) -> String {
        match self {
            Self::Fixed(shot) => shot.relative_path().to_owned(),
            Self::Movement(index) => format!("movement/frame-{index:03}.png"),
        }
    }

    const fn expected_weather(self) -> WeatherState {
        match self {
            Self::Fixed(shot) => shot.expected_weather(),
            Self::Movement(_) => WeatherState::Clear,
        }
    }
}

struct PendingCapture {
    kind: CaptureKind,
    relative_path: String,
    requested_frame: u32,
    camera: CameraEvidence,
    weather: WeatherCheckpoint,
    physical_width: u32,
    physical_height: u32,
}

#[derive(Clone, Copy, Debug)]
struct CameraPose {
    translation: Vec3,
    target: Vec3,
    vertical_fov_degrees: f32,
}

#[derive(Clone, Debug, Serialize)]
struct CaptureEvidence {
    id: String,
    relative_path: String,
    requested_frame: u32,
    verified_frame: u32,
    bytes: u64,
    physical_width: u32,
    physical_height: u32,
    camera: CameraEvidence,
}

#[derive(Clone, Debug, Serialize)]
struct CameraEvidence {
    translation_m: [f32; 3],
    forward: [f32; 3],
    vertical_fov_degrees: f32,
}

#[derive(Clone, Debug, Serialize)]
#[allow(
    clippy::struct_excessive_bools,
    reason = "the proof ledger keeps each independently validated projection explicit"
)]
struct WeatherCheckpoint {
    capture_id: String,
    state: WeatherState,
    requested_frame: u32,
    simulation_seconds: f64,
    current_wetness: f32,
    target_wetness: f32,
    terrain_materials: usize,
    vegetation_materials: usize,
    terrain_uniforms_match: bool,
    vegetation_uniforms_match: bool,
    clear_target_restored_exactly: Option<bool>,
    rain_visible: bool,
    rain_rendered_opacity: f32,
    rain_shader_uniform_updated: bool,
    rain_camera_followed: bool,
    bloom_requested: bool,
    bloom_component_present: bool,
    color_grading_component_present: bool,
    mapped_exposure_ev: f32,
    mapped_temperature: f32,
    mapped_tint: f32,
    mapped_post_saturation: f32,
    mapped_contrast: f32,
    mapped_gamma: f32,
    weather_gpu_execution_observed: bool,
    evidence_source: &'static str,
}

#[derive(Serialize)]
struct Gate1VisualProofLedger {
    schema: &'static str,
    schema_version: u32,
    status: &'static str,
    process_id: u32,
    build_source_commit: &'static str,
    protocol: ProtocolEvidence,
    routes: [RouteEvidence; 5],
    sources: SourceEvidence,
    pipeline_warmup: PipelineWarmupObservation,
    quality: VisualValleyQualityStats,
    timeline: TimelineEvidence,
    captures: Vec<CaptureEvidence>,
    movement: MovementEvidence,
    qualification: QualificationBoundary,
}

#[derive(Serialize)]
struct ProtocolEvidence {
    physical_resolution_px: [u32; 2],
    requested_present_mode: &'static str,
    vsync_requested: bool,
    gpu: GpuDiagnostic,
}

#[derive(Serialize)]
struct RouteEvidence {
    route: &'static str,
    automated_status: &'static str,
}

#[derive(Serialize)]
struct SourceEvidence {
    terrain_hash: String,
    water_hash: String,
    weather_hash: String,
    player_profile_hash: String,
    quality_profile_hash: String,
    cadle_assets_used: bool,
}

#[derive(Serialize)]
struct TimelineEvidence {
    same_process: bool,
    requested_states: [WeatherState; 3],
    checkpoints: Vec<WeatherCheckpoint>,
}

#[derive(Serialize)]
struct MovementEvidence {
    kind: &'static str,
    gameplay_controller_exercised: bool,
    frame_count: u8,
    intended_video_fps: u8,
    start_xz_m: [f32; 2],
    end_xz_m: [f32; 2],
    frame_pattern: &'static str,
}

#[derive(Serialize)]
#[allow(
    clippy::struct_excessive_bools,
    reason = "qualification boundaries must remain individually machine-readable"
)]
struct QualificationBoundary {
    rendered_png_artifacts_acquired: bool,
    weather_cpu_ecs_projection_observed: bool,
    weather_gpu_execution_observed: bool,
    human_review_performed: bool,
    visual_quality_accepted: bool,
    gate1_routes_qualified: bool,
    limitation: &'static str,
}

#[allow(
    clippy::needless_pass_by_value,
    clippy::too_many_arguments,
    clippy::too_many_lines,
    reason = "the bounded native state machine keeps every proof dependency and failure explicit"
)]
fn advance_gate1_visual_proof(
    mut commands: Commands,
    frame: Res<FrameCount>,
    pipeline: Res<PipelineWarmupObservation>,
    quality: Res<VisualValleyQualityStats>,
    gpu: Option<Res<GpuDiagnostic>>,
    weather_stats: Res<WeatherProjectionStats>,
    mut weather_runtime: ResMut<WeatherRuntimeState>,
    windows: Query<&Window, With<PrimaryWindow>>,
    mut cameras: Query<(&Camera, &mut Transform, &mut Projection), With<Camera3d>>,
    mut runtime: ResMut<Gate1VisualProofRuntime>,
    mut exit: MessageWriter<AppExit>,
) {
    if matches!(runtime.phase, ProofPhase::Completed | ProofPhase::Failed) {
        return;
    }
    if runtime.started_at.elapsed() > PROOF_TIMEOUT {
        fail_proof(
            &mut runtime,
            &mut exit,
            "the native visual proof exceeded its 90 second watchdog",
        );
        return;
    }

    let current_frame = frame.0;
    match runtime.phase {
        ProofPhase::WaitingForWarmup => {
            if !pipeline.is_ready()
                || validate_weather_endpoint(
                    &weather_stats,
                    &runtime.configuration.weather_profile,
                    WeatherState::Clear,
                )
                .is_err()
            {
                return;
            }
            if let Err(error) = validate_fixed_quality(&quality) {
                fail_proof(&mut runtime, &mut exit, &error);
                return;
            }
            enter_fixed_shot(
                &mut runtime,
                &mut cameras,
                FixedShot::DistantClear,
                current_frame,
            )
            .unwrap_or_else(|error| fail_proof(&mut runtime, &mut exit, &error));
        }
        ProofPhase::SettlingFixed { shot, ready_frame } if current_frame >= ready_frame => {
            if !pipeline.is_ready() {
                return;
            }
            let result = request_capture(
                &mut commands,
                &mut runtime,
                &windows,
                &mut cameras,
                &weather_stats,
                &weather_runtime,
                CaptureKind::Fixed(shot),
                current_frame,
            );
            if let Err(error) = result {
                fail_proof(&mut runtime, &mut exit, &error);
            }
        }
        ProofPhase::SettlingMovement { index, ready_frame } if current_frame >= ready_frame => {
            if !pipeline.is_ready() {
                return;
            }
            let result = request_capture(
                &mut commands,
                &mut runtime,
                &windows,
                &mut cameras,
                &weather_stats,
                &weather_runtime,
                CaptureKind::Movement(index),
                current_frame,
            );
            if let Err(error) = result {
                fail_proof(&mut runtime, &mut exit, &error);
            }
        }
        ProofPhase::AwaitingCapture => {
            let Some(pending) = runtime.pending_capture.as_ref() else {
                fail_proof(
                    &mut runtime,
                    &mut exit,
                    "capture phase has no pending artifact",
                );
                return;
            };
            if current_frame
                < pending
                    .requested_frame
                    .saturating_add(CAPTURE_SETTLE_FRAMES)
            {
                return;
            }
            let output_path = runtime
                .configuration
                .output_dir
                .join(&pending.relative_path);
            let bytes = match completed_capture_size(&output_path) {
                Ok(Some(bytes)) => bytes,
                Ok(None) => return,
                Err(error) => {
                    fail_proof(&mut runtime, &mut exit, &error);
                    return;
                }
            };
            let Some(pending) = runtime.pending_capture.take() else {
                fail_proof(
                    &mut runtime,
                    &mut exit,
                    "verified capture disappeared from the pending state",
                );
                return;
            };
            let kind = pending.kind;
            if matches!(kind, CaptureKind::Fixed(_)) {
                runtime.weather_checkpoints.push(pending.weather.clone());
            }
            runtime.captures.push(CaptureEvidence {
                id: kind.id(),
                relative_path: pending.relative_path,
                requested_frame: pending.requested_frame,
                verified_frame: current_frame,
                bytes,
                physical_width: pending.physical_width,
                physical_height: pending.physical_height,
                camera: pending.camera,
            });
            tracing::info!(
                target: "forge::proof",
                schema = "forge.runtime-event",
                schema_version = 1_u64,
                event_kind = "gate1_visual_capture_verified",
                capture_id = %kind.id(),
                frame = current_frame,
                bytes,
                "Gate 1 visual proof capture verified"
            );
            if let Err(error) = continue_after_capture(
                &mut runtime,
                &mut cameras,
                &mut weather_runtime,
                kind,
                current_frame,
            ) {
                fail_proof(&mut runtime, &mut exit, &error);
            }
        }
        ProofPhase::TransitioningWeather(target) => {
            if validate_weather_endpoint(
                &weather_stats,
                &runtime.configuration.weather_profile,
                target,
            )
            .is_err()
            {
                return;
            }
            let shot = match target {
                WeatherState::Clear => FixedShot::WaterRestored,
                WeatherState::Rain => FixedShot::WaterRain,
            };
            if let Err(error) = enter_fixed_shot(&mut runtime, &mut cameras, shot, current_frame) {
                fail_proof(&mut runtime, &mut exit, &error);
            }
        }
        ProofPhase::Finalizing => {
            if let Err(error) = validate_weather_endpoint(
                &weather_stats,
                &runtime.configuration.weather_profile,
                WeatherState::Clear,
            )
            .and_then(|()| validate_fixed_quality(&quality))
            .and_then(|()| write_ledger(&runtime, &pipeline, &quality, gpu.as_deref(), &windows))
            {
                fail_proof(&mut runtime, &mut exit, &error);
                return;
            }
            runtime.phase = ProofPhase::Completed;
            tracing::info!(
                target: "forge::proof",
                schema = "forge.runtime-event",
                schema_version = 1_u64,
                event_kind = "gate1_visual_proof_completed",
                ledger = %runtime.configuration.output_dir.join(LEDGER_FILE).display(),
                captures = runtime.captures.len(),
                human_review_performed = false,
                gate1_routes_qualified = false,
                "Gate 1 visual proof artifacts acquired; qualification remains pending"
            );
            exit.write(AppExit::Success);
        }
        ProofPhase::SettlingFixed { .. }
        | ProofPhase::SettlingMovement { .. }
        | ProofPhase::Completed
        | ProofPhase::Failed => {}
    }
}

fn enter_fixed_shot(
    runtime: &mut Gate1VisualProofRuntime,
    cameras: &mut Query<(&Camera, &mut Transform, &mut Projection), With<Camera3d>>,
    shot: FixedShot,
    current_frame: u32,
) -> Result<(), String> {
    match shot {
        FixedShot::DistantClear => validate_single_active_perspective_camera(cameras)?,
        FixedShot::HumanClear => {
            apply_camera_pose(cameras, runtime.configuration.human_pose)?;
        }
        FixedShot::WaterClear | FixedShot::WaterRain | FixedShot::WaterRestored => {
            apply_camera_pose(cameras, runtime.configuration.water_pose)?;
        }
    }
    runtime.phase = ProofPhase::SettlingFixed {
        shot,
        ready_frame: current_frame.saturating_add(POSE_SETTLE_FRAMES),
    };
    Ok(())
}

fn enter_movement_frame(
    runtime: &mut Gate1VisualProofRuntime,
    cameras: &mut Query<(&Camera, &mut Transform, &mut Projection), With<Camera3d>>,
    index: u8,
    current_frame: u32,
) -> Result<(), String> {
    let pose = movement_camera_pose(
        &runtime.configuration.terrain_plan,
        runtime.configuration.eye_height_m,
        runtime.configuration.vertical_fov_degrees,
        index,
    )?;
    apply_camera_pose(cameras, pose)?;
    runtime.phase = ProofPhase::SettlingMovement {
        index,
        ready_frame: current_frame.saturating_add(POSE_SETTLE_FRAMES),
    };
    Ok(())
}

fn continue_after_capture(
    runtime: &mut Gate1VisualProofRuntime,
    cameras: &mut Query<(&Camera, &mut Transform, &mut Projection), With<Camera3d>>,
    weather_runtime: &mut WeatherRuntimeState,
    kind: CaptureKind,
    current_frame: u32,
) -> Result<(), String> {
    match kind {
        CaptureKind::Fixed(FixedShot::DistantClear) => {
            enter_fixed_shot(runtime, cameras, FixedShot::HumanClear, current_frame)
        }
        CaptureKind::Fixed(FixedShot::HumanClear) => {
            enter_fixed_shot(runtime, cameras, FixedShot::WaterClear, current_frame)
        }
        CaptureKind::Fixed(FixedShot::WaterClear) => {
            weather_runtime.request_state(WeatherState::Rain);
            runtime.phase = ProofPhase::TransitioningWeather(WeatherState::Rain);
            Ok(())
        }
        CaptureKind::Fixed(FixedShot::WaterRain) => {
            weather_runtime.request_state(WeatherState::Clear);
            runtime.phase = ProofPhase::TransitioningWeather(WeatherState::Clear);
            Ok(())
        }
        CaptureKind::Fixed(FixedShot::WaterRestored) => {
            enter_movement_frame(runtime, cameras, 0, current_frame)
        }
        CaptureKind::Movement(index) if index + 1 < MOVEMENT_FRAME_COUNT => {
            enter_movement_frame(runtime, cameras, index + 1, current_frame)
        }
        CaptureKind::Movement(_) => {
            runtime.phase = ProofPhase::Finalizing;
            Ok(())
        }
    }
}

#[allow(
    clippy::too_many_arguments,
    reason = "a capture atomically records its camera, weather, window, and filesystem evidence"
)]
fn request_capture(
    commands: &mut Commands,
    runtime: &mut Gate1VisualProofRuntime,
    windows: &Query<&Window, With<PrimaryWindow>>,
    cameras: &mut Query<(&Camera, &mut Transform, &mut Projection), With<Camera3d>>,
    weather_stats: &WeatherProjectionStats,
    weather_runtime: &WeatherRuntimeState,
    kind: CaptureKind,
    current_frame: u32,
) -> Result<(), String> {
    if runtime.pending_capture.is_some() {
        return Err("a second screenshot was requested while one is pending".to_owned());
    }
    validate_weather_endpoint(
        weather_stats,
        &runtime.configuration.weather_profile,
        kind.expected_weather(),
    )?;
    let camera = current_camera_evidence(cameras)?;
    let window = windows
        .single()
        .map_err(|_| "visual proof requires exactly one primary window".to_owned())?;
    let physical_width = window.resolution.physical_width();
    let physical_height = window.resolution.physical_height();
    if physical_width != 1920 || physical_height != 1080 {
        return Err(format!(
            "visual proof requires a 1920x1080 primary window, observed {physical_width}x{physical_height}"
        ));
    }
    let relative_path = kind.relative_path();
    let output_path = runtime.configuration.output_dir.join(&relative_path);
    commands
        .spawn(Screenshot::primary_window())
        .observe(save_to_disk(output_path.clone()));
    runtime.pending_capture = Some(PendingCapture {
        kind,
        relative_path,
        requested_frame: current_frame,
        camera,
        weather: weather_checkpoint(kind.id(), current_frame, weather_stats, weather_runtime)?,
        physical_width,
        physical_height,
    });
    runtime.phase = ProofPhase::AwaitingCapture;
    tracing::info!(
        target: "forge::proof",
        schema = "forge.runtime-event",
        schema_version = 1_u64,
        event_kind = "gate1_visual_capture_requested",
        capture_id = %kind.id(),
        path = %output_path.display(),
        frame = current_frame,
        "Gate 1 visual proof capture requested"
    );
    Ok(())
}

fn validate_single_active_perspective_camera(
    cameras: &mut Query<(&Camera, &mut Transform, &mut Projection), With<Camera3d>>,
) -> Result<(), String> {
    let (camera, _, projection) = cameras
        .single_mut()
        .map_err(|_| "visual proof requires exactly one 3D camera".to_owned())?;
    if !camera.is_active {
        return Err("the single visual proof camera is inactive".to_owned());
    }
    if !matches!(&*projection, Projection::Perspective(_)) {
        return Err("visual proof requires a perspective camera".to_owned());
    }
    Ok(())
}

fn apply_camera_pose(
    cameras: &mut Query<(&Camera, &mut Transform, &mut Projection), With<Camera3d>>,
    pose: CameraPose,
) -> Result<(), String> {
    let (camera, mut transform, mut projection) = cameras
        .single_mut()
        .map_err(|_| "visual proof requires exactly one 3D camera".to_owned())?;
    if !camera.is_active {
        return Err("the single visual proof camera is inactive".to_owned());
    }
    let Projection::Perspective(perspective) = &mut *projection else {
        return Err("visual proof requires a perspective camera".to_owned());
    };
    perspective.fov = pose.vertical_fov_degrees.to_radians();
    perspective.near = 0.1;
    perspective.far = 1_600.0;
    *transform = Transform::from_translation(pose.translation).looking_at(pose.target, Vec3::Y);
    Ok(())
}

fn current_camera_evidence(
    cameras: &mut Query<(&Camera, &mut Transform, &mut Projection), With<Camera3d>>,
) -> Result<CameraEvidence, String> {
    let (camera, transform, projection) = cameras
        .single_mut()
        .map_err(|_| "visual proof requires exactly one 3D camera".to_owned())?;
    if !camera.is_active {
        return Err("the single visual proof camera is inactive".to_owned());
    }
    let Projection::Perspective(perspective) = &*projection else {
        return Err("visual proof requires a perspective camera".to_owned());
    };
    Ok(CameraEvidence {
        translation_m: transform.translation.to_array(),
        forward: transform.forward().as_vec3().to_array(),
        vertical_fov_degrees: perspective.fov.to_degrees(),
    })
}

fn human_camera_pose(
    terrain: &TerrainGenerationPlan,
    eye_height_m: f32,
    vertical_fov_degrees: f32,
) -> Result<CameraPose, String> {
    terrain_relative_pose(
        terrain,
        [MOVEMENT_START_X_M, MOVEMENT_START_Z_M],
        [MOVEMENT_START_X_M, MOVEMENT_START_Z_M - 24.0],
        eye_height_m,
        vertical_fov_degrees,
    )
}

fn movement_camera_pose(
    terrain: &TerrainGenerationPlan,
    eye_height_m: f32,
    vertical_fov_degrees: f32,
    index: u8,
) -> Result<CameraPose, String> {
    if index >= MOVEMENT_FRAME_COUNT {
        return Err(format!(
            "movement frame {index} exceeds the {MOVEMENT_FRAME_COUNT} frame proof path"
        ));
    }
    let denominator = f32::from(MOVEMENT_FRAME_COUNT - 1);
    let amount = f32::from(index) / denominator;
    let next_amount = (f32::from(index) + 1.0).min(denominator) / denominator;
    let position = [
        MOVEMENT_START_X_M + (MOVEMENT_END_X_M - MOVEMENT_START_X_M) * amount,
        MOVEMENT_START_Z_M + (MOVEMENT_END_Z_M - MOVEMENT_START_Z_M) * amount,
    ];
    let mut target = [
        MOVEMENT_START_X_M + (MOVEMENT_END_X_M - MOVEMENT_START_X_M) * next_amount,
        MOVEMENT_START_Z_M + (MOVEMENT_END_Z_M - MOVEMENT_START_Z_M) * next_amount,
    ];
    if index + 1 == MOVEMENT_FRAME_COUNT {
        target = [
            MOVEMENT_END_X_M + (MOVEMENT_END_X_M - MOVEMENT_START_X_M) / denominator,
            MOVEMENT_END_Z_M + (MOVEMENT_END_Z_M - MOVEMENT_START_Z_M) / denominator,
        ];
    }
    terrain_relative_pose(
        terrain,
        position,
        target,
        eye_height_m,
        vertical_fov_degrees,
    )
}

fn terrain_relative_pose(
    terrain: &TerrainGenerationPlan,
    position_xz: [f32; 2],
    target_xz: [f32; 2],
    eye_height_m: f32,
    vertical_fov_degrees: f32,
) -> Result<CameraPose, String> {
    let height = terrain
        .height_at(position_xz[0], position_xz[1])
        .map_err(|error| format!("cannot sample proof camera position: {error}"))?;
    let target_height = terrain
        .height_at(target_xz[0], target_xz[1])
        .map_err(|error| format!("cannot sample proof camera target: {error}"))?;
    Ok(CameraPose {
        translation: Vec3::new(position_xz[0], height + eye_height_m, position_xz[1]),
        target: Vec3::new(
            target_xz[0],
            target_height + eye_height_m * 0.82,
            target_xz[1],
        ),
        vertical_fov_degrees,
    })
}

fn water_camera_pose(
    terrain: &TerrainGenerationPlan,
    water: &WaterDocument,
    eye_height_m: f32,
    vertical_fov_degrees: f32,
) -> Result<CameraPose, String> {
    let samples = sample_river_centerline(water)
        .map_err(|error| format!("cannot sample proof river: {error}"))?;
    let (index, focus) = samples
        .iter()
        .enumerate()
        .min_by(|(_, left), (_, right)| {
            water_anchor_distance_squared(left.center_m.x, left.center_m.z).total_cmp(
                &water_anchor_distance_squared(right.center_m.x, right.center_m.z),
            )
        })
        .ok_or_else(|| "proof river has no centreline sample".to_owned())?;
    let previous = samples[index.saturating_sub(1)].center_m;
    let next = samples[(index + 1).min(samples.len() - 1)].center_m;
    let tangent = Vec2::new(next.x - previous.x, next.z - previous.z).normalize_or_zero();
    if tangent.length_squared() <= f32::EPSILON {
        return Err("proof river anchor has no plan-view tangent".to_owned());
    }
    let normal = Vec2::new(-tangent.y, tangent.x);
    let candidates = [
        Vec2::new(focus.center_m.x, focus.center_m.z) + normal * WATER_VIEW_DISTANCE_M,
        Vec2::new(focus.center_m.x, focus.center_m.z) - normal * WATER_VIEW_DISTANCE_M,
    ];
    let (camera_xz, height) = candidates
        .into_iter()
        .filter_map(|candidate| {
            terrain
                .height_at(candidate.x, candidate.y)
                .ok()
                .map(|height| (candidate, height))
        })
        .max_by(|(_, left_height), (_, right_height)| left_height.total_cmp(right_height))
        .ok_or_else(|| "proof water camera cannot be placed inside the terrain".to_owned())?;
    Ok(CameraPose {
        translation: Vec3::new(camera_xz.x, height + eye_height_m, camera_xz.y),
        target: Vec3::new(focus.center_m.x, focus.center_m.y + 0.25, focus.center_m.z),
        vertical_fov_degrees,
    })
}

fn water_anchor_distance_squared(x: f32, z: f32) -> f32 {
    (x - WATER_VIEW_ANCHOR_X_M).powi(2) + (z - WATER_VIEW_ANCHOR_Z_M).powi(2)
}

fn validate_weather_endpoint(
    stats: &WeatherProjectionStats,
    profile: &WeatherProfileDocument,
    expected_state: WeatherState,
) -> Result<(), String> {
    if stats.source_hash
        != profile
            .content_hash()
            .map_err(|error| error.to_string())?
            .to_string()
    {
        return Err("weather projection hash differs from the proof profile".to_owned());
    }
    if stats.status != WeatherProjectionStatus::ObservedCpuProjection {
        return Err(format!(
            "weather CPU/ECS projection is not complete: {:?}",
            stats.status
        ));
    }
    if stats.target_state != expected_state {
        return Err(format!(
            "weather target is {:?}, expected {:?}",
            stats.target_state, expected_state
        ));
    }
    let expected_wetness = profile.wetness.target_for(expected_state);
    if stats.wetness.current.to_bits() != expected_wetness.to_bits()
        || stats.wetness.target.to_bits() != expected_wetness.to_bits()
    {
        return Err(format!(
            "weather wetness has not reached the exact {expected_state:?} endpoint"
        ));
    }
    if stats.wetness.terrain_materials == 0
        || stats.wetness.vegetation_materials == 0
        || !stats.wetness.terrain_uniforms_match
        || !stats.wetness.vegetation_uniforms_match
    {
        return Err(
            "wetness uniforms are not projected to every terrain and vegetation material"
                .to_owned(),
        );
    }
    if expected_state == WeatherState::Clear
        && stats.wetness.clear_target_restored_exactly != Some(true)
    {
        return Err("clear wetness was not restored exactly".to_owned());
    }
    if stats.post_processing.active_camera_3d_entities != 1
        || stats.post_processing.camera_3d_entities != 1
    {
        return Err("weather proof requires exactly one active 3D camera".to_owned());
    }
    if stats.post_processing.bloom_requested != stats.post_processing.bloom_component_present {
        return Err("bloom component does not match the active quality request".to_owned());
    }
    if !stats.post_processing.color_grading_component_present {
        return Err("color grading is not projected to the active camera".to_owned());
    }
    if !stats.rain.shader_animation_uniform_updated || !stats.rain.camera_followed {
        return Err("rain batch animation or camera binding is incomplete".to_owned());
    }
    let grade = profile.post_processing.grade_for(expected_state);
    validate_mapped_grade(
        stats.post_processing.mapped_exposure_ev,
        grade.exposure_ev,
        "exposure",
    )?;
    validate_mapped_grade(
        stats.post_processing.mapped_temperature,
        grade.temperature * NORMALIZED_GRADE_TO_CIE_OFFSET,
        "temperature",
    )?;
    validate_mapped_grade(
        stats.post_processing.mapped_tint,
        grade.tint * NORMALIZED_GRADE_TO_CIE_OFFSET,
        "tint",
    )?;
    validate_mapped_grade(
        stats.post_processing.mapped_post_saturation,
        grade.saturation,
        "saturation",
    )?;
    validate_mapped_grade(
        stats.post_processing.mapped_contrast,
        grade.contrast,
        "contrast",
    )?;
    validate_mapped_grade(stats.post_processing.mapped_gamma, grade.gamma, "gamma")
}

fn validate_mapped_grade(
    actual: Option<f32>,
    expected: f32,
    field: &'static str,
) -> Result<(), String> {
    let actual = actual.ok_or_else(|| format!("mapped color grade `{field}` is absent"))?;
    if !actual.is_finite() || (actual - expected).abs() > 1.0e-6 {
        return Err(format!(
            "mapped color grade `{field}` is {actual}, expected {expected}"
        ));
    }
    Ok(())
}

fn validate_fixed_quality(quality: &VisualValleyQualityStats) -> Result<(), String> {
    if !matches!(quality.selection, VisualValleyQualitySelection::Fixed(_))
        || quality.adaptation_enabled
        || quality.transition_count != 0
        || quality.current_tier != quality.startup_tier
        || quality.dynamically_applied_tier != Some(quality.current_tier)
    {
        return Err("visual proof requires one fully applied fixed quality tier".to_owned());
    }
    Ok(())
}

fn weather_checkpoint(
    capture_id: String,
    requested_frame: u32,
    stats: &WeatherProjectionStats,
    runtime: &WeatherRuntimeState,
) -> Result<WeatherCheckpoint, String> {
    let mapped = &stats.post_processing;
    Ok(WeatherCheckpoint {
        capture_id,
        state: stats.target_state,
        requested_frame,
        simulation_seconds: runtime.simulation_seconds(),
        current_wetness: stats.wetness.current,
        target_wetness: stats.wetness.target,
        terrain_materials: stats.wetness.terrain_materials,
        vegetation_materials: stats.wetness.vegetation_materials,
        terrain_uniforms_match: stats.wetness.terrain_uniforms_match,
        vegetation_uniforms_match: stats.wetness.vegetation_uniforms_match,
        clear_target_restored_exactly: stats.wetness.clear_target_restored_exactly,
        rain_visible: stats.rain.visible,
        rain_rendered_opacity: stats.rain.rendered_opacity,
        rain_shader_uniform_updated: stats.rain.shader_animation_uniform_updated,
        rain_camera_followed: stats.rain.camera_followed,
        bloom_requested: mapped.bloom_requested,
        bloom_component_present: mapped.bloom_component_present,
        color_grading_component_present: mapped.color_grading_component_present,
        mapped_exposure_ev: required_grade(mapped.mapped_exposure_ev, "exposure")?,
        mapped_temperature: required_grade(mapped.mapped_temperature, "temperature")?,
        mapped_tint: required_grade(mapped.mapped_tint, "tint")?,
        mapped_post_saturation: required_grade(mapped.mapped_post_saturation, "post_saturation")?,
        mapped_contrast: required_grade(mapped.mapped_contrast, "contrast")?,
        mapped_gamma: required_grade(mapped.mapped_gamma, "gamma")?,
        weather_gpu_execution_observed: stats.gpu_execution_observed,
        evidence_source: stats.evidence_source,
    })
}

fn required_grade(value: Option<f32>, field: &'static str) -> Result<f32, String> {
    value.ok_or_else(|| format!("mapped color grade `{field}` is absent"))
}

fn completed_capture_size(path: &Path) -> Result<Option<u64>, String> {
    let metadata = match fs::metadata(path) {
        Ok(metadata) => metadata,
        Err(error) if error.kind() == std::io::ErrorKind::NotFound => return Ok(None),
        Err(error) => return Err(format!("cannot inspect {}: {error}", path.display())),
    };
    if !metadata.is_file() {
        return Err(format!("{} is not a regular file", path.display()));
    }
    if metadata.len() == 0 {
        return Ok(None);
    }
    Ok(Some(metadata.len()))
}

#[allow(
    clippy::too_many_lines,
    reason = "the versioned ledger construction keeps every route and qualification limit visible"
)]
fn write_ledger(
    runtime: &Gate1VisualProofRuntime,
    pipeline: &PipelineWarmupObservation,
    quality: &VisualValleyQualityStats,
    gpu: Option<&GpuDiagnostic>,
    windows: &Query<&Window, With<PrimaryWindow>>,
) -> Result<(), String> {
    let expected_capture_count = usize::from(MOVEMENT_FRAME_COUNT) + 5;
    if runtime.captures.len() != expected_capture_count {
        return Err(format!(
            "visual proof produced {} captures, expected {expected_capture_count}",
            runtime.captures.len()
        ));
    }
    let gpu =
        gpu.ok_or_else(|| "GPU adapter identity is absent from the visual proof".to_owned())?;
    let window = windows
        .single()
        .map_err(|_| "visual proof requires exactly one primary window".to_owned())?;
    let physical_resolution_px = [
        window.resolution.physical_width(),
        window.resolution.physical_height(),
    ];
    if physical_resolution_px != [1920, 1080] {
        return Err(format!(
            "visual proof final resolution is {}x{}, expected 1920x1080",
            physical_resolution_px[0], physical_resolution_px[1]
        ));
    }
    if window.present_mode != PresentMode::AutoNoVsync {
        return Err(format!(
            "visual proof requested present mode is {:?}, expected AutoNoVsync",
            window.present_mode
        ));
    }
    let build_source_commit = embedded_source_commit()?;
    let weather_gpu_execution_observed = runtime
        .weather_checkpoints
        .iter()
        .any(|checkpoint| checkpoint.weather_gpu_execution_observed);
    let ledger = Gate1VisualProofLedger {
        schema: SCHEMA,
        schema_version: SCHEMA_VERSION,
        status: "automated_capture_complete_pending_qualification",
        process_id: std::process::id(),
        build_source_commit,
        protocol: ProtocolEvidence {
            physical_resolution_px,
            requested_present_mode: present_mode_label(window.present_mode),
            vsync_requested: false,
            gpu: gpu.clone(),
        },
        routes: [
            RouteEvidence {
                route: "R1-025",
                automated_status: "clear_rain_clear_cpu_ecs_endpoints_observed",
            },
            RouteEvidence {
                route: "R1-026",
                automated_status: "color_grade_endpoints_and_bloom_component_presence_observed",
            },
            RouteEvidence {
                route: "R1-046",
                automated_status: "human_height_png_acquired_pending_human_review",
            },
            RouteEvidence {
                route: "R1-047",
                automated_status: "distant_png_acquired_pending_human_review",
            },
            RouteEvidence {
                route: "R1-048",
                automated_status: "movement_frames_acquired_pending_mp4_and_human_review",
            },
        ],
        sources: SourceEvidence {
            terrain_hash: runtime.configuration.terrain_hash.clone(),
            water_hash: runtime.configuration.water_hash.clone(),
            weather_hash: runtime.configuration.weather_hash.clone(),
            player_profile_hash: runtime.configuration.player_profile_hash.clone(),
            quality_profile_hash: quality.profile_hash.clone(),
            cadle_assets_used: false,
        },
        pipeline_warmup: pipeline.clone(),
        quality: quality.clone(),
        timeline: TimelineEvidence {
            same_process: true,
            requested_states: [WeatherState::Clear, WeatherState::Rain, WeatherState::Clear],
            checkpoints: runtime.weather_checkpoints.clone(),
        },
        captures: runtime.captures.clone(),
        movement: MovementEvidence {
            kind: "deterministic_terrain_relative_proof_camera",
            gameplay_controller_exercised: false,
            frame_count: MOVEMENT_FRAME_COUNT,
            intended_video_fps: MOVEMENT_VIDEO_FPS,
            start_xz_m: [MOVEMENT_START_X_M, MOVEMENT_START_Z_M],
            end_xz_m: [MOVEMENT_END_X_M, MOVEMENT_END_Z_M],
            frame_pattern: "movement/frame-%03d.png",
        },
        qualification: QualificationBoundary {
            rendered_png_artifacts_acquired: true,
            weather_cpu_ecs_projection_observed: true,
            weather_gpu_execution_observed,
            human_review_performed: false,
            visual_quality_accepted: false,
            gate1_routes_qualified: false,
            limitation: "machine acquisition only: screenshot presence and CPU/ECS projections do not qualify GPU execution, gameplay movement, comparative visual quality, or human acceptance",
        },
    };
    let path = runtime.configuration.output_dir.join(LEDGER_FILE);
    let temporary_path = runtime
        .configuration
        .output_dir
        .join("gate1-visual-proof.json.tmp");
    let json = serde_json::to_vec_pretty(&ledger)
        .map_err(|error| format!("cannot serialize visual proof ledger: {error}"))?;
    fs::write(&temporary_path, json).map_err(|error| {
        format!(
            "cannot write temporary visual proof ledger {}: {error}",
            temporary_path.display()
        )
    })?;
    fs::rename(&temporary_path, &path).map_err(|error| {
        format!(
            "cannot publish visual proof ledger {}: {error}",
            path.display()
        )
    })
}

fn embedded_source_commit() -> Result<&'static str, String> {
    let value = option_env!("FORGE_SOURCE_COMMIT").ok_or_else(|| {
        "FORGE_SOURCE_COMMIT was not embedded; rebuild the runtime from a clean exact commit"
            .to_owned()
    })?;
    if value.len() != 40 || !value.bytes().all(|byte| byte.is_ascii_hexdigit()) {
        return Err(format!(
            "embedded FORGE_SOURCE_COMMIT `{value}` is not a 40-character Git object id"
        ));
    }
    Ok(value)
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

fn fail_proof(
    runtime: &mut Gate1VisualProofRuntime,
    exit: &mut MessageWriter<AppExit>,
    error: &str,
) {
    if matches!(runtime.phase, ProofPhase::Failed | ProofPhase::Completed) {
        return;
    }
    runtime.phase = ProofPhase::Failed;
    let failure_path = runtime.configuration.output_dir.join(FAILURE_FILE);
    if let Err(write_error) = fs::write(&failure_path, error.as_bytes()) {
        tracing::error!(
            target: "forge::proof",
            schema = "forge.runtime-event",
            schema_version = 1_u64,
            event_kind = "gate1_visual_proof_failure_write_failed",
            path = %failure_path.display(),
            error = %write_error,
            "cannot write Gate 1 visual proof failure artifact"
        );
    }
    tracing::error!(
        target: "forge::proof",
        schema = "forge.runtime-event",
        schema_version = 1_u64,
        event_kind = "gate1_visual_proof_failed",
        %error,
        human_review_performed = false,
        gate1_routes_qualified = false,
        "Gate 1 visual proof failed"
    );
    exit.write(AppExit::error());
}

#[cfg(test)]
mod tests {
    use super::*;
    use forge_engine::{WeatherPostProcessingStats, WeatherRainStats, WeatherWetnessStats};
    use forge_project::WeatherQualityTier;
    use forge_world::{
        embedded_visual_valley_player_controller_profile, embedded_visual_valley_terrain_document,
        embedded_visual_valley_water_document, embedded_visual_valley_weather_profile,
    };

    #[test]
    fn camera_plan_uses_authored_human_height_and_an_authored_water_anchor() {
        let terrain = embedded_visual_valley_terrain_document().unwrap();
        let water = embedded_visual_valley_water_document().unwrap();
        let weather = embedded_visual_valley_weather_profile().unwrap();
        let player = embedded_visual_valley_player_controller_profile().unwrap();
        let plugin = Gate1VisualProofPlugin::new(
            PathBuf::from("proof"),
            &terrain,
            &water,
            &weather,
            &player,
        )
        .unwrap();
        let plan = &plugin.configuration.terrain_plan;
        let expected_human_y = plan
            .height_at(MOVEMENT_START_X_M, MOVEMENT_START_Z_M)
            .unwrap()
            + player.camera.eye_height_m;

        assert_eq!(
            plugin.configuration.human_pose.translation.y.to_bits(),
            expected_human_y.to_bits()
        );
        assert_eq!(
            plugin
                .configuration
                .human_pose
                .vertical_fov_degrees
                .to_bits(),
            player.camera.first_person_vertical_fov_degrees.to_bits()
        );
        assert!(
            water_anchor_distance_squared(
                plugin.configuration.water_pose.target.x,
                plugin.configuration.water_pose.target.z
            ) <= 4.0
        );
        let water_pose = plugin.configuration.water_pose;
        let water_replay = water_camera_pose(
            plan,
            &water,
            player.camera.eye_height_m,
            player.camera.first_person_vertical_fov_degrees,
        )
        .unwrap();
        let samples = sample_river_centerline(&water).unwrap();
        let (focus_index, focus) = samples
            .iter()
            .enumerate()
            .min_by(|(_, left), (_, right)| {
                water_anchor_distance_squared(left.center_m.x, left.center_m.z).total_cmp(
                    &water_anchor_distance_squared(right.center_m.x, right.center_m.z),
                )
            })
            .unwrap();
        let previous = samples[focus_index.saturating_sub(1)].center_m;
        let next = samples[(focus_index + 1).min(samples.len() - 1)].center_m;
        let tangent = Vec2::new(next.x - previous.x, next.z - previous.z).normalize();
        let normal = Vec2::new(-tangent.y, tangent.x);
        let expected_camera = [
            Vec2::new(focus.center_m.x, focus.center_m.z) + normal * WATER_VIEW_DISTANCE_M,
            Vec2::new(focus.center_m.x, focus.center_m.z) - normal * WATER_VIEW_DISTANCE_M,
        ]
        .into_iter()
        .filter_map(|candidate| {
            plan.height_at(candidate.x, candidate.y)
                .ok()
                .map(|height| (candidate, height))
        })
        .max_by(|(_, left_height), (_, right_height)| left_height.total_cmp(right_height))
        .unwrap();
        assert_eq!(
            [water_pose.translation.x, water_pose.translation.z].map(f32::to_bits),
            expected_camera.0.to_array().map(f32::to_bits)
        );
        assert_eq!(
            water_pose.translation.y.to_bits(),
            (expected_camera.1 + player.camera.eye_height_m).to_bits()
        );
        assert!(
            ((water_pose.translation.xz() - water_pose.target.xz()).length()
                - WATER_VIEW_DISTANCE_M)
                .abs()
                < 1.0e-4
        );
        assert!((water_pose.target - water_pose.translation).normalize().y < 0.0);
        assert_eq!(
            water_pose.translation.to_array().map(f32::to_bits),
            water_replay.translation.to_array().map(f32::to_bits)
        );
        assert_eq!(
            water_pose.target.to_array().map(f32::to_bits),
            water_replay.target.to_array().map(f32::to_bits)
        );
        assert_eq!(plugin.configuration.terrain_hash.len(), 64);
        assert_eq!(plugin.configuration.water_hash.len(), 64);
        assert_eq!(plugin.configuration.weather_hash.len(), 64);
        assert_eq!(plugin.configuration.player_profile_hash.len(), 64);
    }

    #[test]
    fn movement_frames_are_bounded_terrain_relative_and_replayable() {
        let terrain = embedded_visual_valley_terrain_document().unwrap();
        let plan = TerrainGenerationPlan::from_document(&terrain).unwrap();
        let player = embedded_visual_valley_player_controller_profile().unwrap();
        let first = (0..MOVEMENT_FRAME_COUNT)
            .map(|index| {
                movement_camera_pose(
                    &plan,
                    player.camera.eye_height_m,
                    player.camera.first_person_vertical_fov_degrees,
                    index,
                )
                .unwrap()
            })
            .collect::<Vec<_>>();
        let replay = (0..MOVEMENT_FRAME_COUNT)
            .map(|index| {
                movement_camera_pose(
                    &plan,
                    player.camera.eye_height_m,
                    player.camera.first_person_vertical_fov_degrees,
                    index,
                )
                .unwrap()
            })
            .collect::<Vec<_>>();

        assert_eq!(first.len(), usize::from(MOVEMENT_FRAME_COUNT));
        for (left, right) in first.iter().zip(replay) {
            assert_eq!(
                left.translation.to_array().map(f32::to_bits),
                right.translation.to_array().map(f32::to_bits)
            );
            assert_eq!(
                left.target.to_array().map(f32::to_bits),
                right.target.to_array().map(f32::to_bits)
            );
            let terrain_y = plan
                .height_at(left.translation.x, left.translation.z)
                .unwrap();
            assert_eq!(
                left.translation.y.to_bits(),
                (terrain_y + player.camera.eye_height_m).to_bits()
            );
        }
        assert_eq!(
            CaptureKind::Movement(0).relative_path(),
            "movement/frame-000.png"
        );
        assert_eq!(
            CaptureKind::Movement(MOVEMENT_FRAME_COUNT - 1).relative_path(),
            "movement/frame-023.png"
        );
        assert!(
            movement_camera_pose(
                &plan,
                player.camera.eye_height_m,
                player.camera.first_person_vertical_fov_degrees,
                MOVEMENT_FRAME_COUNT,
            )
            .is_err()
        );
    }

    #[test]
    fn endpoint_validation_rejects_non_reversible_wetness_and_unbounded_grade() {
        let profile = embedded_visual_valley_weather_profile().unwrap();
        let mut clear = sample_weather_stats(&profile, WeatherState::Clear);
        validate_weather_endpoint(&clear, &profile, WeatherState::Clear).unwrap();

        clear.wetness.clear_target_restored_exactly = Some(false);
        assert!(
            validate_weather_endpoint(&clear, &profile, WeatherState::Clear)
                .unwrap_err()
                .contains("restored exactly")
        );

        let mut rain = sample_weather_stats(&profile, WeatherState::Rain);
        validate_weather_endpoint(&rain, &profile, WeatherState::Rain).unwrap();
        rain.post_processing.mapped_temperature = Some(0.5);
        assert!(
            validate_weather_endpoint(&rain, &profile, WeatherState::Rain)
                .unwrap_err()
                .contains("temperature")
        );
    }

    fn sample_weather_stats(
        profile: &WeatherProfileDocument,
        state: WeatherState,
    ) -> WeatherProjectionStats {
        let wetness = profile.wetness.target_for(state);
        let grade = profile.post_processing.grade_for(state);
        WeatherProjectionStats {
            source_hash: profile.content_hash().unwrap().to_string(),
            status: WeatherProjectionStatus::ObservedCpuProjection,
            evidence_source: "test_cpu_projection",
            target_state: state,
            gpu_execution_observed: false,
            rain: WeatherRainStats {
                quality: Some(WeatherQualityTier::High),
                requested_drops: 1,
                generated_drops: 1,
                batch_entities: 1,
                per_drop_entities: 0,
                vertices: 8,
                triangles: 4,
                visible: state == WeatherState::Rain,
                camera_followed: true,
                shader_animation_uniform_updated: true,
                rendered_opacity: if state == WeatherState::Rain {
                    profile.rain.opacity
                } else {
                    0.0
                },
            },
            wetness: WeatherWetnessStats {
                current: wetness,
                target: wetness,
                terrain_materials: 3,
                vegetation_materials: 2,
                terrain_uniforms_match: true,
                vegetation_uniforms_match: true,
                clear_target_restored_exactly: (state == WeatherState::Clear).then_some(true),
            },
            post_processing: WeatherPostProcessingStats {
                camera_3d_entities: 1,
                active_camera_3d_entities: 1,
                bloom_requested: true,
                bloom_component_present: true,
                color_grading_component_present: true,
                mapped_exposure_ev: Some(grade.exposure_ev),
                mapped_temperature: Some(grade.temperature * NORMALIZED_GRADE_TO_CIE_OFFSET),
                mapped_tint: Some(grade.tint * NORMALIZED_GRADE_TO_CIE_OFFSET),
                mapped_post_saturation: Some(grade.saturation),
                mapped_contrast: Some(grade.contrast),
                mapped_gamma: Some(grade.gamma),
            },
            implemented_capabilities: &[],
            deferred_capabilities: &[],
        }
    }
}
