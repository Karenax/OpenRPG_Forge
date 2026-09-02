use bevy::{
    app::AppExit,
    diagnostic::FrameCount,
    prelude::*,
    render::view::screenshot::{Screenshot, save_to_disk},
    window::{PresentMode, PrimaryWindow},
};
use forge_engine::{
    ForgePlayerCamera, ForgePlayerCharacter, GpuDiagnostic, PendingPlayerControlCommand,
    PipelineWarmupObservation, PlayerControlCommand, PlayerControllerSet,
    PlayerRuntimeBindingSnapshot, PlayerRuntimeBindings, PlayerRuntimeSnapshot, TERRAIN_PHYSICS_HZ,
    VisualValleyQualitySelection, VisualValleyQualityStats, WeatherProjectionStats,
    WeatherProjectionStatus, WeatherRuntimeState,
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
const SCHEMA_VERSION: u32 = 2;
const LEDGER_FILE: &str = "gate1-visual-proof.json";
const FAILURE_FILE: &str = "gate1-visual-proof.failure.txt";
const POSE_SETTLE_FRAMES: u32 = 8;
const CAPTURE_SETTLE_FRAMES: u32 = 6;
const MOVEMENT_FRAME_COUNT: u8 = 24;
const MOVEMENT_VIDEO_FPS: u8 = 8;
const MOVEMENT_FIXED_HZ: u32 = 60;
const MOVEMENT_MIN_FORWARD_DISTANCE_M: f32 = 8.0;
const MOVEMENT_MAX_LATERAL_DRIFT_M: f32 = 0.75;
const MOVEMENT_MAX_UNGROUNDED_RUN: u32 = 4;
const MOVEMENT_STOP_STABLE_TICKS: u8 = 2;
const MOVEMENT_STOP_MAX_FIXED_TICKS: u32 = 30;
const MOVEMENT_STOP_POSITION_EPSILON_M: f32 = 1.0e-4;
const PLAYER_READY_MAX_FIXED_TICKS: u32 = 300;
const PROOF_TIMEOUT: Duration = Duration::from_secs(90);
const WATER_VIEW_ANCHOR_X_M: f32 = 16.0;
const WATER_VIEW_ANCHOR_Z_M: f32 = 160.0;
const WATER_VIEW_DISTANCE_M: f32 = 10.0;
const MOVEMENT_START_X_M: f32 = 16.0;
const MOVEMENT_START_Z_M: f32 = 8.0;
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
        let distant_pose = distant_camera_pose(&terrain_plan)?;
        let human_pose = human_camera_pose(&terrain_plan, eye_height_m, vertical_fov_degrees)?;
        let water_pose =
            water_camera_pose(&terrain_plan, water, eye_height_m, vertical_fov_degrees)?;
        let capsule_half_height_m =
            player.controller.capsule_radius_m + player.controller.capsule_half_segment_m;
        let player_spawn_position = Vec3::new(
            MOVEMENT_START_X_M,
            terrain_plan
                .height_at(MOVEMENT_START_X_M, MOVEMENT_START_Z_M)
                .map_err(|error| format!("cannot sample proof player spawn: {error}"))?
                + capsule_half_height_m
                + 0.02,
            MOVEMENT_START_Z_M,
        );

        Ok(Self {
            configuration: ProofConfiguration {
                output_dir,
                weather_profile: weather.clone(),
                distant_pose,
                human_pose,
                water_pose,
                player_spawn_position,
                capsule_half_height_m,
                camera_pivot_height_m: player.camera.pivot_height_m,
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

    #[must_use]
    pub const fn player_spawn_position(&self) -> Vec3 {
        self.configuration.player_spawn_position
    }
}

impl Plugin for Gate1VisualProofPlugin {
    fn build(&self, app: &mut App) {
        app.insert_resource(Gate1VisualProofRuntime::new(self.configuration.clone()))
            .insert_resource(Gate1GameplayCameraEnabled(false))
            .configure_sets(
                FixedUpdate,
                PlayerControllerSet::Camera.run_if(gate1_gameplay_camera_enabled),
            )
            .add_systems(
                FixedUpdate,
                drive_gate1_gameplay_movement.before(PlayerControllerSet::Drive),
            )
            .add_systems(
                FixedUpdate,
                observe_gate1_gameplay_movement.after(PlayerControllerSet::Camera),
            )
            .add_systems(Last, advance_gate1_visual_proof);
    }
}

#[derive(Resource)]
struct Gate1GameplayCameraEnabled(bool);

#[allow(
    clippy::needless_pass_by_value,
    reason = "Bevy run conditions own their system parameter wrappers"
)]
fn gate1_gameplay_camera_enabled(enabled: Res<Gate1GameplayCameraEnabled>) -> bool {
    enabled.0
}

#[derive(Clone)]
struct ProofConfiguration {
    output_dir: PathBuf,
    weather_profile: WeatherProfileDocument,
    distant_pose: CameraPose,
    human_pose: CameraPose,
    water_pose: CameraPose,
    player_spawn_position: Vec3,
    capsule_half_height_m: f32,
    camera_pivot_height_m: f32,
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
    movement: GameplayMovementRuntime,
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
            movement: GameplayMovementRuntime::default(),
        }
    }
}

#[derive(Clone, Copy, Debug, Eq, PartialEq)]
enum ProofPhase {
    WaitingForWarmup,
    SettlingFixed { shot: FixedShot, ready_frame: u32 },
    TransitioningWeather(WeatherState),
    WaitingForPlayerController,
    PreparingMovement,
    Moving { index: u8 },
    SettlingMovement { index: u8, ready_frame: u32 },
    StoppingMovement,
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
    movement_fixed_tick: Option<u64>,
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
    movement_fixed_tick: Option<u64>,
}

#[derive(Clone, Debug, Serialize)]
struct CameraEvidence {
    translation_m: [f32; 3],
    forward: [f32; 3],
    vertical_fov_degrees: f32,
    forge_player_camera: bool,
}

#[derive(Default)]
struct GameplayMovementRuntime {
    player_ready_wait_ticks: u32,
    baseline_fixed_tick: Option<u64>,
    command_sequence: Option<u64>,
    stop_sequence: Option<u64>,
    start_position_m: Option<Vec3>,
    stopped_position_m: Option<Vec3>,
    initial_bindings: Option<PlayerRuntimeBindingSnapshot>,
    checkpoints: Vec<MovementCheckpointEvidence>,
    observed_ticks: u32,
    grounded_ticks: u32,
    collision_ticks: u32,
    current_ungrounded_run: u32,
    max_ungrounded_run: u32,
    stop_stable_ticks: u8,
    stop_wait_ticks: u32,
    previous_stop_position_m: Option<Vec3>,
    observed_fixed_hz: Option<f64>,
}

#[derive(Clone, Debug, Serialize)]
struct MovementCheckpointEvidence {
    index: u8,
    target_tick_offset: u64,
    observed_tick_offset: u64,
    fixed_tick: u64,
    player_position_m: [f32; 3],
    grounded: bool,
    collisions: usize,
    camera_entity: String,
    camera: CameraEvidence,
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
#[allow(
    clippy::struct_excessive_bools,
    reason = "the movement ledger preserves independently validated qualification bits"
)]
struct MovementEvidence {
    kind: &'static str,
    gameplay_controller_exercised: bool,
    interactive_input_enabled: bool,
    fixed_hz: f64,
    fixed_timestep_seconds: f64,
    frame_count: u8,
    intended_video_fps: u8,
    start_xz_m: [f32; 2],
    end_xz_m: [f32; 2],
    stopped_xz_m: [f32; 2],
    planar_distance_m: f32,
    forward_distance_m: f32,
    lateral_drift_m: f32,
    command_sequence: u64,
    stop_sequence: u64,
    command_applied_exactly: bool,
    stopped_command_applied_exactly: bool,
    stop_wait_ticks: u32,
    observed_fixed_ticks: u32,
    grounded_ticks: u32,
    maximum_ungrounded_run: u32,
    collision_ticks: u32,
    camera_entity: String,
    player_entity: String,
    rapier_collider_handle: [u32; 2],
    rapier_context_entity: String,
    backend_identity_stable: bool,
    camera_marker_verified: bool,
    checkpoints: Vec<MovementCheckpointEvidence>,
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
    real_time_stutter_qualified: bool,
    human_review_performed: bool,
    visual_quality_accepted: bool,
    gate1_routes_qualified: bool,
    limitation: &'static str,
}

fn drive_gate1_gameplay_movement(
    mut pending: ResMut<PendingPlayerControlCommand>,
    mut runtime: ResMut<Gate1VisualProofRuntime>,
    mut exit: MessageWriter<AppExit>,
) {
    let result = match runtime.phase {
        ProofPhase::PreparingMovement if runtime.movement.command_sequence.is_none() => pending
            .submit(PlayerControlCommand {
                move_axis: Vec2::Y,
                ..default()
            })
            .map(|sequence| runtime.movement.command_sequence = Some(sequence))
            .map_err(|error| format!("cannot submit gameplay movement command: {error}")),
        ProofPhase::StoppingMovement if runtime.movement.stop_sequence.is_none() => pending
            .submit(PlayerControlCommand::default())
            .map(|sequence| runtime.movement.stop_sequence = Some(sequence))
            .map_err(|error| format!("cannot submit gameplay stop command: {error}")),
        _ => Ok(()),
    };
    if let Err(error) = result {
        fail_proof(&mut runtime, &mut exit, &error);
    }
}

#[allow(
    clippy::needless_pass_by_value,
    clippy::too_many_arguments,
    clippy::too_many_lines,
    reason = "the fixed checkpoint observer validates controller, Rapier, camera and timing identities atomically"
)]
fn observe_gate1_gameplay_movement(
    frame: Res<FrameCount>,
    mut virtual_time: ResMut<Time<Virtual>>,
    mut fixed_time: ResMut<Time<Fixed>>,
    bindings: Res<PlayerRuntimeBindings>,
    snapshot: Res<PlayerRuntimeSnapshot>,
    cameras: Query<(Entity, &Camera, &Transform, &Projection), With<ForgePlayerCamera>>,
    players: Query<Entity, With<ForgePlayerCharacter>>,
    mut runtime: ResMut<Gate1VisualProofRuntime>,
    mut exit: MessageWriter<AppExit>,
) {
    if matches!(runtime.phase, ProofPhase::Completed | ProofPhase::Failed) {
        return;
    }

    let result = (|| -> Result<(), String> {
        if matches!(
            runtime.phase,
            ProofPhase::WaitingForPlayerController
                | ProofPhase::PreparingMovement
                | ProofPhase::Moving { .. }
                | ProofPhase::StoppingMovement
        ) {
            observe_fixed_timestep(&mut runtime.movement, &fixed_time)?;
        }
        match runtime.phase {
            ProofPhase::WaitingForPlayerController => {
                runtime.movement.player_ready_wait_ticks =
                    runtime.movement.player_ready_wait_ticks.saturating_add(1);
                match ready_player_binding(
                    &runtime.configuration,
                    &bindings,
                    &snapshot,
                    &cameras,
                    &players,
                ) {
                    Ok(Some((binding, camera))) => {
                        runtime.movement.baseline_fixed_tick = Some(snapshot.fixed_tick);
                        runtime.movement.start_position_m = Some(snapshot.player_position);
                        runtime.movement.initial_bindings = Some(binding);
                        record_movement_checkpoint(&mut runtime, 0, &snapshot, camera)?;
                        pause_at_movement_checkpoint(
                            &mut runtime,
                            &mut virtual_time,
                            &mut fixed_time,
                            0,
                            frame.0,
                        );
                        Ok(())
                    }
                    Ok(None)
                        if runtime.movement.player_ready_wait_ticks
                            < PLAYER_READY_MAX_FIXED_TICKS =>
                    {
                        Ok(())
                    }
                    Ok(None) => Err(format!(
                        "gameplay controller remained unready after {PLAYER_READY_MAX_FIXED_TICKS} fixed ticks"
                    )),
                    Err(error) => Err(error),
                }
            }
            ProofPhase::PreparingMovement => {
                let camera = validate_active_player_binding(
                    &runtime, &bindings, &snapshot, &cameras, &players,
                )?;
                let sequence = runtime
                    .movement
                    .command_sequence
                    .ok_or_else(|| "gameplay movement command was not submitted".to_owned())?;
                if snapshot.last_applied_sequence != Some(sequence) {
                    return Err(format!(
                        "gameplay movement sequence {sequence} was not applied exactly; observed {:?}",
                        snapshot.last_applied_sequence
                    ));
                }
                observe_movement_tick(&mut runtime.movement, &snapshot)?;
                runtime.phase = ProofPhase::Moving { index: 1 };
                maybe_pause_for_movement_checkpoint(
                    &mut runtime,
                    &snapshot,
                    &mut virtual_time,
                    &mut fixed_time,
                    frame.0,
                    camera,
                )
            }
            ProofPhase::Moving { .. } => {
                let camera = validate_active_player_binding(
                    &runtime, &bindings, &snapshot, &cameras, &players,
                )?;
                let sequence = runtime.movement.command_sequence.ok_or_else(|| {
                    "active gameplay movement lost its command sequence".to_owned()
                })?;
                if snapshot.last_applied_sequence == Some(sequence) {
                    observe_movement_tick(&mut runtime.movement, &snapshot)?;
                    maybe_pause_for_movement_checkpoint(
                        &mut runtime,
                        &snapshot,
                        &mut virtual_time,
                        &mut fixed_time,
                        frame.0,
                        camera,
                    )
                } else {
                    Err(format!(
                        "gameplay movement sequence changed before stop: expected {sequence}, observed {:?}",
                        snapshot.last_applied_sequence
                    ))
                }
            }
            ProofPhase::StoppingMovement => {
                let _camera = validate_active_player_binding(
                    &runtime, &bindings, &snapshot, &cameras, &players,
                )?;
                runtime.movement.stop_wait_ticks =
                    runtime.movement.stop_wait_ticks.saturating_add(1);
                if runtime.movement.stop_wait_ticks > MOVEMENT_STOP_MAX_FIXED_TICKS {
                    return Err(format!(
                        "gameplay controller did not stop within {MOVEMENT_STOP_MAX_FIXED_TICKS} fixed ticks"
                    ));
                }
                let sequence = runtime
                    .movement
                    .stop_sequence
                    .ok_or_else(|| "gameplay stop command was not submitted".to_owned())?;
                if snapshot.last_applied_sequence == Some(sequence) {
                    if stop_sample_is_stable(runtime.movement.previous_stop_position_m, &snapshot) {
                        runtime.movement.stop_stable_ticks =
                            runtime.movement.stop_stable_ticks.saturating_add(1);
                    } else {
                        runtime.movement.stop_stable_ticks = 0;
                    }
                    runtime.movement.previous_stop_position_m = Some(snapshot.player_position);
                    if runtime.movement.stop_stable_ticks >= MOVEMENT_STOP_STABLE_TICKS {
                        runtime.movement.stopped_position_m = Some(snapshot.player_position);
                        runtime.phase = ProofPhase::Finalizing;
                    }
                    Ok(())
                } else {
                    Err(format!(
                        "gameplay stop sequence {sequence} was not applied exactly; observed {:?}",
                        snapshot.last_applied_sequence
                    ))
                }
            }
            _ => Ok(()),
        }
    })();

    if let Err(error) = result {
        fail_proof(&mut runtime, &mut exit, &error);
    }
}

fn ready_player_binding(
    configuration: &ProofConfiguration,
    bindings: &PlayerRuntimeBindings,
    snapshot: &PlayerRuntimeSnapshot,
    cameras: &Query<(Entity, &Camera, &Transform, &Projection), With<ForgePlayerCamera>>,
    players: &Query<Entity, With<ForgePlayerCharacter>>,
) -> Result<Option<(PlayerRuntimeBindingSnapshot, CameraEvidence)>, String> {
    let Some(profile_hash) = snapshot.profile_content_hash else {
        return Ok(None);
    };
    if profile_hash.to_string() != configuration.player_profile_hash {
        return Err(format!(
            "gameplay controller profile hash mismatch: expected {}, observed {profile_hash}",
            configuration.player_profile_hash
        ));
    }
    let Some(binding) = bindings.snapshot() else {
        return Ok(None);
    };
    if !binding.backend_identity_stable {
        return Err("gameplay controller backend identity is unstable".to_owned());
    }
    if binding.rapier_collider_handle.is_none() || binding.rapier_context_entity.is_none() {
        return Ok(None);
    }
    if !snapshot.grounded || !snapshot.camera_query_succeeded {
        return Ok(None);
    }
    let camera = validate_player_entities(configuration, binding, snapshot, cameras, players)?;
    if snapshot.player_position.z <= 0.0
        || (snapshot.player_position.x - MOVEMENT_START_X_M).abs() > MOVEMENT_MAX_LATERAL_DRIFT_M
        || (snapshot.player_position.z - MOVEMENT_START_Z_M).abs() > MOVEMENT_MAX_LATERAL_DRIFT_M
    {
        return Err(format!(
            "gameplay proof player did not start at the governed positive-Z spawn: {:?}",
            snapshot.player_position
        ));
    }
    Ok(Some((binding, camera)))
}

fn validate_active_player_binding(
    runtime: &Gate1VisualProofRuntime,
    bindings: &PlayerRuntimeBindings,
    snapshot: &PlayerRuntimeSnapshot,
    cameras: &Query<(Entity, &Camera, &Transform, &Projection), With<ForgePlayerCamera>>,
    players: &Query<Entity, With<ForgePlayerCharacter>>,
) -> Result<CameraEvidence, String> {
    let expected = runtime
        .movement
        .initial_bindings
        .ok_or_else(|| "gameplay movement has no initial binding snapshot".to_owned())?;
    let actual = bindings
        .snapshot()
        .ok_or_else(|| "gameplay movement bindings disappeared".to_owned())?;
    if actual != expected || !actual.backend_identity_stable || !snapshot.backend_identity_stable {
        return Err(format!(
            "gameplay controller bindings changed during movement: expected {expected:?}, observed {actual:?}"
        ));
    }
    if snapshot.profile_content_hash.map(|hash| hash.to_string())
        != Some(runtime.configuration.player_profile_hash.clone())
    {
        return Err("gameplay controller profile hash changed during movement".to_owned());
    }
    if !snapshot.camera_query_succeeded {
        return Err("gameplay camera Rapier query failed during movement".to_owned());
    }
    validate_player_entities(&runtime.configuration, actual, snapshot, cameras, players)
}

fn validate_player_entities(
    configuration: &ProofConfiguration,
    binding: PlayerRuntimeBindingSnapshot,
    snapshot: &PlayerRuntimeSnapshot,
    cameras: &Query<(Entity, &Camera, &Transform, &Projection), With<ForgePlayerCamera>>,
    players: &Query<Entity, With<ForgePlayerCharacter>>,
) -> Result<CameraEvidence, String> {
    let (camera_entity, camera, transform, projection) = cameras
        .single()
        .map_err(|_| "gameplay proof requires exactly one Forge player camera".to_owned())?;
    if !camera.is_active || camera_entity != binding.camera_entity {
        return Err(
            "active Forge player camera does not match the stable runtime binding".to_owned(),
        );
    }
    let player_entity = players
        .single()
        .map_err(|_| "gameplay proof requires exactly one Forge player character".to_owned())?;
    if player_entity != binding.player_entity {
        return Err("Forge player character does not match the stable runtime binding".to_owned());
    }
    if snapshot.camera_mode != forge_engine::PlayerCameraMode::ThirdPerson {
        return Err("gameplay proof requires the third-person controller camera".to_owned());
    }
    expected_controller_camera(configuration, snapshot, transform, projection)
}

fn expected_controller_camera(
    configuration: &ProofConfiguration,
    snapshot: &PlayerRuntimeSnapshot,
    transform: &Transform,
    projection: &Projection,
) -> Result<CameraEvidence, String> {
    let view_rotation = Quat::from_rotation_y(snapshot.camera_yaw_radians)
        * Quat::from_rotation_x(snapshot.camera_pitch_radians);
    let expected_forward = view_rotation * Vec3::NEG_Z;
    let expected_pivot = snapshot.player_position - Vec3::Y * configuration.capsule_half_height_m
        + Vec3::Y * configuration.camera_pivot_height_m;
    let expected_translation = expected_pivot - expected_forward * snapshot.camera_boom_distance_m;
    let actual_forward = transform.forward().as_vec3();
    let Projection::Perspective(perspective) = projection else {
        return Err("gameplay controller camera is not perspective".to_owned());
    };
    if transform.translation.distance(expected_translation) > 1.0e-4
        || actual_forward.distance(expected_forward) > 1.0e-5
        || (perspective.fov - snapshot.camera_vertical_fov_radians).abs() > 1.0e-6
    {
        return Err(format!(
            "Forge player camera is not the controller projection: translation {:?}/{expected_translation:?}, forward {actual_forward:?}/{expected_forward:?}, fov {}/{}",
            transform.translation, perspective.fov, snapshot.camera_vertical_fov_radians
        ));
    }
    Ok(CameraEvidence {
        translation_m: transform.translation.to_array(),
        forward: actual_forward.to_array(),
        vertical_fov_degrees: perspective.fov.to_degrees(),
        forge_player_camera: true,
    })
}

fn observe_fixed_timestep(
    movement: &mut GameplayMovementRuntime,
    fixed_time: &Time<Fixed>,
) -> Result<(), String> {
    let expected = Duration::from_secs_f64(1.0 / TERRAIN_PHYSICS_HZ);
    let actual = fixed_time.timestep();
    if actual != expected {
        return Err(format!(
            "gameplay proof observed fixed timestep {actual:?}, expected {expected:?}"
        ));
    }
    let observed_hz = 1.0 / actual.as_secs_f64();
    if movement
        .observed_fixed_hz
        .is_some_and(|previous| previous.to_bits() != observed_hz.to_bits())
    {
        return Err("gameplay proof fixed timestep changed during the trajectory".to_owned());
    }
    movement.observed_fixed_hz = Some(observed_hz);
    Ok(())
}

fn observe_movement_tick(
    movement: &mut GameplayMovementRuntime,
    snapshot: &PlayerRuntimeSnapshot,
) -> Result<(), String> {
    if !snapshot.player_position.is_finite() || !snapshot.horizontal_velocity.is_finite() {
        return Err("gameplay movement produced a non-finite player snapshot".to_owned());
    }
    movement.observed_ticks = movement.observed_ticks.saturating_add(1);
    if snapshot.grounded {
        movement.grounded_ticks = movement.grounded_ticks.saturating_add(1);
        movement.current_ungrounded_run = 0;
    } else {
        movement.current_ungrounded_run = movement.current_ungrounded_run.saturating_add(1);
        movement.max_ungrounded_run = movement
            .max_ungrounded_run
            .max(movement.current_ungrounded_run);
    }
    if snapshot.collisions > 0 {
        movement.collision_ticks = movement.collision_ticks.saturating_add(1);
    }
    Ok(())
}

fn stop_sample_is_stable(
    previous_position: Option<Vec3>,
    snapshot: &PlayerRuntimeSnapshot,
) -> bool {
    snapshot.grounded
        && snapshot.horizontal_velocity.length() <= 0.05
        && snapshot.effective_translation.xz().length() <= MOVEMENT_STOP_POSITION_EPSILON_M
        && previous_position.is_some_and(|previous| {
            previous.xz().distance(snapshot.player_position.xz())
                <= MOVEMENT_STOP_POSITION_EPSILON_M
        })
}

fn maybe_pause_for_movement_checkpoint(
    runtime: &mut Gate1VisualProofRuntime,
    snapshot: &PlayerRuntimeSnapshot,
    virtual_time: &mut Time<Virtual>,
    fixed_time: &mut Time<Fixed>,
    current_frame: u32,
    camera: CameraEvidence,
) -> Result<(), String> {
    let ProofPhase::Moving { index } = runtime.phase else {
        return Ok(());
    };
    let baseline = runtime
        .movement
        .baseline_fixed_tick
        .ok_or_else(|| "gameplay movement has no baseline fixed tick".to_owned())?;
    let observed_offset = snapshot
        .fixed_tick
        .checked_sub(baseline)
        .ok_or_else(|| "gameplay fixed tick moved backwards".to_owned())?;
    let target_offset = movement_tick_offset(index)?;
    if observed_offset > target_offset {
        return Err(format!(
            "gameplay checkpoint {index} was skipped: target offset {target_offset}, observed {observed_offset}"
        ));
    }
    if observed_offset == target_offset {
        record_movement_checkpoint(runtime, index, snapshot, camera)?;
        pause_at_movement_checkpoint(runtime, virtual_time, fixed_time, index, current_frame);
    }
    Ok(())
}

fn record_movement_checkpoint(
    runtime: &mut Gate1VisualProofRuntime,
    index: u8,
    snapshot: &PlayerRuntimeSnapshot,
    camera: CameraEvidence,
) -> Result<(), String> {
    if usize::from(index) != runtime.movement.checkpoints.len() {
        return Err(format!(
            "gameplay checkpoint order mismatch: index {index}, existing {}",
            runtime.movement.checkpoints.len()
        ));
    }
    let baseline = runtime
        .movement
        .baseline_fixed_tick
        .ok_or_else(|| "gameplay checkpoint has no baseline fixed tick".to_owned())?;
    let observed_tick_offset = snapshot
        .fixed_tick
        .checked_sub(baseline)
        .ok_or_else(|| "gameplay checkpoint fixed tick moved backwards".to_owned())?;
    let target_tick_offset = movement_tick_offset(index)?;
    if observed_tick_offset != target_tick_offset {
        return Err(format!(
            "gameplay checkpoint {index} observed offset {observed_tick_offset}, expected {target_tick_offset}"
        ));
    }
    runtime
        .movement
        .checkpoints
        .push(MovementCheckpointEvidence {
            index,
            target_tick_offset,
            observed_tick_offset,
            fixed_tick: snapshot.fixed_tick,
            player_position_m: snapshot.player_position.to_array(),
            grounded: snapshot.grounded,
            collisions: snapshot.collisions,
            camera_entity: format!(
                "{:?}",
                runtime
                    .movement
                    .initial_bindings
                    .ok_or_else(|| "gameplay checkpoint has no camera binding".to_owned())?
                    .camera_entity
            ),
            camera,
        });
    Ok(())
}

fn pause_at_movement_checkpoint(
    runtime: &mut Gate1VisualProofRuntime,
    virtual_time: &mut Time<Virtual>,
    fixed_time: &mut Time<Fixed>,
    index: u8,
    current_frame: u32,
) {
    virtual_time.pause();
    let overstep = fixed_time.overstep();
    fixed_time.discard_overstep(overstep);
    runtime.phase = ProofPhase::SettlingMovement {
        index,
        ready_frame: current_frame.saturating_add(POSE_SETTLE_FRAMES),
    };
}

fn movement_tick_offset(index: u8) -> Result<u64, String> {
    if index >= MOVEMENT_FRAME_COUNT {
        return Err(format!(
            "movement frame {index} exceeds the {MOVEMENT_FRAME_COUNT} frame gameplay path"
        ));
    }
    Ok(
        (u64::from(index) * u64::from(MOVEMENT_FIXED_HZ) + u64::from(MOVEMENT_VIDEO_FPS) / 2)
            / u64::from(MOVEMENT_VIDEO_FPS),
    )
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
    mut virtual_time: ResMut<Time<Virtual>>,
    mut gameplay_camera_enabled: ResMut<Gate1GameplayCameraEnabled>,
    windows: Query<&Window, With<PrimaryWindow>>,
    mut cameras: Query<
        (
            &Camera,
            &mut Transform,
            &mut Projection,
            Option<&ForgePlayerCamera>,
        ),
        With<Camera3d>,
    >,
    mut players: Query<&mut Visibility, With<ForgePlayerCharacter>>,
    mut runtime: ResMut<Gate1VisualProofRuntime>,
    mut exit: MessageWriter<AppExit>,
) {
    if matches!(runtime.phase, ProofPhase::Completed | ProofPhase::Failed) {
        return;
    }

    if let Ok(mut visibility) = players.single_mut() {
        *visibility = if gameplay_camera_enabled.0 {
            Visibility::Visible
        } else {
            Visibility::Hidden
        };
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
                &virtual_time,
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
                &virtual_time,
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
                movement_fixed_tick: pending.movement_fixed_tick,
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
                &mut virtual_time,
                &mut gameplay_camera_enabled,
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
        | ProofPhase::WaitingForPlayerController
        | ProofPhase::PreparingMovement
        | ProofPhase::Moving { .. }
        | ProofPhase::StoppingMovement
        | ProofPhase::Completed
        | ProofPhase::Failed => {}
    }
}

fn enter_fixed_shot(
    runtime: &mut Gate1VisualProofRuntime,
    cameras: &mut Query<
        (
            &Camera,
            &mut Transform,
            &mut Projection,
            Option<&ForgePlayerCamera>,
        ),
        With<Camera3d>,
    >,
    shot: FixedShot,
    current_frame: u32,
) -> Result<(), String> {
    match shot {
        FixedShot::DistantClear => {
            apply_camera_pose(cameras, runtime.configuration.distant_pose)?;
        }
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

fn continue_after_capture(
    runtime: &mut Gate1VisualProofRuntime,
    cameras: &mut Query<
        (
            &Camera,
            &mut Transform,
            &mut Projection,
            Option<&ForgePlayerCamera>,
        ),
        With<Camera3d>,
    >,
    weather_runtime: &mut WeatherRuntimeState,
    virtual_time: &mut Time<Virtual>,
    gameplay_camera_enabled: &mut Gate1GameplayCameraEnabled,
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
            gameplay_camera_enabled.0 = true;
            runtime.phase = ProofPhase::WaitingForPlayerController;
            Ok(())
        }
        CaptureKind::Movement(index) if index + 1 < MOVEMENT_FRAME_COUNT => {
            virtual_time.unpause();
            runtime.phase = if index == 0 {
                ProofPhase::PreparingMovement
            } else {
                ProofPhase::Moving { index: index + 1 }
            };
            Ok(())
        }
        CaptureKind::Movement(_) => {
            virtual_time.unpause();
            runtime.phase = ProofPhase::StoppingMovement;
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
    cameras: &mut Query<
        (
            &Camera,
            &mut Transform,
            &mut Projection,
            Option<&ForgePlayerCamera>,
        ),
        With<Camera3d>,
    >,
    weather_stats: &WeatherProjectionStats,
    weather_runtime: &WeatherRuntimeState,
    virtual_time: &Time<Virtual>,
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
    let movement_fixed_tick = match kind {
        CaptureKind::Movement(index) => {
            if !virtual_time.is_paused() {
                return Err(format!(
                    "movement capture {index} was requested while virtual time was running"
                ));
            }
            let checkpoint = runtime
                .movement
                .checkpoints
                .get(usize::from(index))
                .ok_or_else(|| format!("movement capture {index} has no fixed checkpoint"))?;
            validate_capture_camera(index, &camera, &checkpoint.camera)?;
            Some(checkpoint.fixed_tick)
        }
        CaptureKind::Fixed(_) => None,
    };
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
        movement_fixed_tick,
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

fn apply_camera_pose(
    cameras: &mut Query<
        (
            &Camera,
            &mut Transform,
            &mut Projection,
            Option<&ForgePlayerCamera>,
        ),
        With<Camera3d>,
    >,
    pose: CameraPose,
) -> Result<(), String> {
    let (camera, mut transform, mut projection, _) = cameras
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
    cameras: &mut Query<
        (
            &Camera,
            &mut Transform,
            &mut Projection,
            Option<&ForgePlayerCamera>,
        ),
        With<Camera3d>,
    >,
) -> Result<CameraEvidence, String> {
    let (camera, transform, projection, forge_player_camera) = cameras
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
        forge_player_camera: forge_player_camera.is_some(),
    })
}

fn validate_capture_camera(
    index: u8,
    actual: &CameraEvidence,
    expected: &CameraEvidence,
) -> Result<(), String> {
    let translation_error =
        Vec3::from_array(actual.translation_m).distance(Vec3::from_array(expected.translation_m));
    let forward_error =
        Vec3::from_array(actual.forward).distance(Vec3::from_array(expected.forward));
    let fov_error = (actual.vertical_fov_degrees - expected.vertical_fov_degrees).abs();
    if !actual.forge_player_camera
        || !expected.forge_player_camera
        || translation_error > 1.0e-5
        || forward_error > 1.0e-6
        || fov_error > 1.0e-5
    {
        return Err(format!(
            "movement capture {index} camera diverged from its controller checkpoint: translation_error={translation_error}, forward_error={forward_error}, fov_error={fov_error}"
        ));
    }
    Ok(())
}

fn distant_camera_pose(terrain: &TerrainGenerationPlan) -> Result<CameraPose, String> {
    let generated = terrain
        .generate()
        .map_err(|error| format!("cannot generate proof terrain bounds: {error}"))?;
    let (minimum_height_m, maximum_height_m) = generated
        .chunks
        .iter()
        .flat_map(|chunk| chunk.positions_m.iter().map(|position| position[1]))
        .fold(
            (f32::INFINITY, f32::NEG_INFINITY),
            |(minimum, maximum), height| (minimum.min(height), maximum.max(height)),
        );
    if !minimum_height_m.is_finite() || !maximum_height_m.is_finite() {
        return Err("proof terrain has no finite height bounds".to_owned());
    }
    Ok(CameraPose {
        translation: Vec3::new(-210.0, maximum_height_m + 125.0, 210.0),
        target: Vec3::new(0.0, f32::midpoint(minimum_height_m, maximum_height_m), 0.0),
        vertical_fov_degrees: 52.0,
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
        target: Vec3::new(focus.center_m.x, focus.center_m.y + 0.05, focus.center_m.z),
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
    reason = "the movement claim is emitted only after every controller, physics, timing and camera invariant passes"
)]
fn validated_gameplay_movement(
    runtime: &Gate1VisualProofRuntime,
) -> Result<MovementEvidence, String> {
    if TERRAIN_PHYSICS_HZ.to_bits() != f64::from(MOVEMENT_FIXED_HZ).to_bits() {
        return Err(format!(
            "gameplay proof cadence is {TERRAIN_PHYSICS_HZ} Hz, expected {MOVEMENT_FIXED_HZ} Hz"
        ));
    }
    let movement = &runtime.movement;
    let observed_fixed_hz = movement
        .observed_fixed_hz
        .ok_or_else(|| "gameplay proof did not observe Time<Fixed>".to_owned())?;
    if (observed_fixed_hz - TERRAIN_PHYSICS_HZ).abs() > 1.0e-4 {
        return Err(format!(
            "gameplay proof observed {observed_fixed_hz} Hz, expected {TERRAIN_PHYSICS_HZ} Hz"
        ));
    }
    if movement.checkpoints.len() != usize::from(MOVEMENT_FRAME_COUNT) {
        return Err(format!(
            "gameplay proof recorded {} checkpoints, expected {MOVEMENT_FRAME_COUNT}",
            movement.checkpoints.len()
        ));
    }
    let binding = movement
        .initial_bindings
        .ok_or_else(|| "gameplay proof initial bindings are absent".to_owned())?;
    let expected_camera_entity = format!("{:?}", binding.camera_entity);
    for (expected_index, checkpoint) in (0..MOVEMENT_FRAME_COUNT).zip(&movement.checkpoints) {
        let expected_offset = movement_tick_offset(expected_index)?;
        if checkpoint.index != expected_index
            || checkpoint.target_tick_offset != expected_offset
            || checkpoint.observed_tick_offset != expected_offset
        {
            return Err(format!(
                "gameplay checkpoint {expected_index} is not bound to exact offset {expected_offset}: {checkpoint:?}"
            ));
        }
        if checkpoint.camera_entity != expected_camera_entity {
            return Err(format!(
                "gameplay checkpoint {expected_index} camera entity {} does not match stable binding {expected_camera_entity}",
                checkpoint.camera_entity
            ));
        }
        if !Vec3::from_array(checkpoint.player_position_m).is_finite()
            || !camera_evidence_is_finite(&checkpoint.camera)
        {
            return Err(format!(
                "gameplay checkpoint {expected_index} contains non-finite player or camera evidence"
            ));
        }
    }
    let command_sequence = movement
        .command_sequence
        .ok_or_else(|| "gameplay movement command sequence is absent".to_owned())?;
    let stop_sequence = movement
        .stop_sequence
        .ok_or_else(|| "gameplay stop command sequence is absent".to_owned())?;
    if stop_sequence != command_sequence.saturating_add(1) {
        return Err(format!(
            "gameplay stop sequence {stop_sequence} does not immediately follow movement sequence {command_sequence}"
        ));
    }
    if !binding.backend_identity_stable {
        return Err("gameplay proof backend identity is not stable".to_owned());
    }
    let rapier_collider_handle = binding
        .rapier_collider_handle
        .map(|(index, generation)| [index, generation])
        .ok_or_else(|| "gameplay proof Rapier collider handle is absent".to_owned())?;
    let rapier_context_entity = binding
        .rapier_context_entity
        .ok_or_else(|| "gameplay proof Rapier context entity is absent".to_owned())?;
    let start = movement
        .start_position_m
        .ok_or_else(|| "gameplay proof start position is absent".to_owned())?;
    let stopped = movement
        .stopped_position_m
        .ok_or_else(|| "gameplay proof stopped position is absent".to_owned())?;
    let first = movement
        .checkpoints
        .first()
        .ok_or_else(|| "gameplay proof first checkpoint is absent".to_owned())?;
    let last = movement
        .checkpoints
        .last()
        .ok_or_else(|| "gameplay proof final checkpoint is absent".to_owned())?;
    if first.player_position_m.map(f32::to_bits) != start.to_array().map(f32::to_bits) {
        return Err("gameplay frame 000 is not the pre-command player position".to_owned());
    }
    let end = Vec3::from_array(last.player_position_m);
    let planar_delta = end.xz() - start.xz();
    let planar_distance_m = planar_delta.length();
    let forward_distance_m = start.z - end.z;
    let lateral_drift_m = (end.x - start.x).abs();
    if start.z <= 0.0
        || end.z >= 0.0
        || forward_distance_m < MOVEMENT_MIN_FORWARD_DISTANCE_M
        || lateral_drift_m > MOVEMENT_MAX_LATERAL_DRIFT_M
    {
        return Err(format!(
            "gameplay path did not cross the central seam within bounds: start={start:?}, end={end:?}, forward={forward_distance_m}, lateral={lateral_drift_m}"
        ));
    }
    let expected_observed_ticks = u32::try_from(movement_tick_offset(MOVEMENT_FRAME_COUNT - 1)?)
        .map_err(|_| "gameplay checkpoint span does not fit in u32".to_owned())?;
    if movement.observed_ticks != expected_observed_ticks {
        return Err(format!(
            "gameplay proof observed {} movement ticks, expected {}",
            movement.observed_ticks, expected_observed_ticks
        ));
    }
    if movement.max_ungrounded_run > MOVEMENT_MAX_UNGROUNDED_RUN || !last.grounded {
        return Err(format!(
            "gameplay path lost grounded continuity: maximum run {}, final grounded {}",
            movement.max_ungrounded_run, last.grounded
        ));
    }
    if movement.collision_ticks == 0 {
        return Err("gameplay path never observed a terrain collision".to_owned());
    }
    if movement.stop_wait_ticks == 0
        || movement.stop_wait_ticks > MOVEMENT_STOP_MAX_FIXED_TICKS
        || movement.stop_stable_ticks < MOVEMENT_STOP_STABLE_TICKS
    {
        return Err(format!(
            "gameplay stop did not settle inside its bounded window: wait={}, stable={}",
            movement.stop_wait_ticks, movement.stop_stable_ticks
        ));
    }
    let movement_captures = runtime
        .captures
        .iter()
        .filter(|capture| capture.id.starts_with("movement-"))
        .collect::<Vec<_>>();
    if movement_captures.len() != usize::from(MOVEMENT_FRAME_COUNT) {
        return Err("every gameplay checkpoint must have exactly one movement capture".to_owned());
    }
    for (index, (capture, checkpoint)) in movement_captures
        .iter()
        .zip(&movement.checkpoints)
        .enumerate()
    {
        let index = u8::try_from(index)
            .map_err(|_| "movement capture index does not fit in u8".to_owned())?;
        if capture.id != format!("movement-{index:03}")
            || capture.movement_fixed_tick != Some(checkpoint.fixed_tick)
        {
            return Err(format!(
                "movement capture {index} is not linked to fixed checkpoint {}",
                checkpoint.fixed_tick
            ));
        }
        validate_capture_camera(index, &capture.camera, &checkpoint.camera)?;
    }

    Ok(MovementEvidence {
        kind: "deterministic_fixed_tick_gameplay_controller",
        gameplay_controller_exercised: true,
        interactive_input_enabled: false,
        fixed_hz: observed_fixed_hz,
        fixed_timestep_seconds: 1.0 / observed_fixed_hz,
        frame_count: MOVEMENT_FRAME_COUNT,
        intended_video_fps: MOVEMENT_VIDEO_FPS,
        start_xz_m: start.xz().to_array(),
        end_xz_m: end.xz().to_array(),
        stopped_xz_m: stopped.xz().to_array(),
        planar_distance_m,
        forward_distance_m,
        lateral_drift_m,
        command_sequence,
        stop_sequence,
        command_applied_exactly: true,
        stopped_command_applied_exactly: true,
        stop_wait_ticks: movement.stop_wait_ticks,
        observed_fixed_ticks: movement.observed_ticks,
        grounded_ticks: movement.grounded_ticks,
        maximum_ungrounded_run: movement.max_ungrounded_run,
        collision_ticks: movement.collision_ticks,
        camera_entity: format!("{:?}", binding.camera_entity),
        player_entity: format!("{:?}", binding.player_entity),
        rapier_collider_handle,
        rapier_context_entity: format!("{rapier_context_entity:?}"),
        backend_identity_stable: true,
        camera_marker_verified: true,
        checkpoints: movement.checkpoints.clone(),
        frame_pattern: "movement/frame-%03d.png",
    })
}

fn camera_evidence_is_finite(camera: &CameraEvidence) -> bool {
    Vec3::from_array(camera.translation_m).is_finite()
        && Vec3::from_array(camera.forward).is_finite()
        && camera.vertical_fov_degrees.is_finite()
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
    let movement = validated_gameplay_movement(runtime)?;
    let ledger = Gate1VisualProofLedger {
        schema: SCHEMA,
        schema_version: SCHEMA_VERSION,
        status: "automated_capture_complete_pending_stutter_and_qualification",
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
                automated_status: "gameplay_controller_frames_acquired_pending_mp4_stutter_and_human_review",
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
        movement,
        qualification: QualificationBoundary {
            rendered_png_artifacts_acquired: true,
            weather_cpu_ecs_projection_observed: true,
            weather_gpu_execution_observed,
            real_time_stutter_qualified: false,
            human_review_performed: false,
            visual_quality_accepted: false,
            gate1_routes_qualified: false,
            limitation: "machine acquisition only: screenshot presence, CPU/ECS projections and a paused fixed-tick gameplay trajectory do not qualify GPU execution, real-time stutter, comparative visual quality, or human acceptance",
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
    #[allow(
        clippy::too_many_lines,
        reason = "one deterministic camera-plan test compares all authored fixed-view and spawn inputs"
    )]
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
        let plan = TerrainGenerationPlan::from_document(&terrain).unwrap();
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
            &plan,
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
        assert_eq!(
            water_pose.target.y.to_bits(),
            (focus.center_m.y + 0.05).to_bits()
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
        let capsule_half_height_m =
            player.controller.capsule_radius_m + player.controller.capsule_half_segment_m;
        assert_eq!(
            plugin.player_spawn_position().to_array().map(f32::to_bits),
            Vec3::new(
                MOVEMENT_START_X_M,
                plan.height_at(MOVEMENT_START_X_M, MOVEMENT_START_Z_M)
                    .unwrap()
                    + capsule_half_height_m
                    + 0.02,
                MOVEMENT_START_Z_M,
            )
            .to_array()
            .map(f32::to_bits)
        );
        let distant_replay = distant_camera_pose(&plan).unwrap();
        assert_eq!(
            plugin
                .configuration
                .distant_pose
                .translation
                .to_array()
                .map(f32::to_bits),
            distant_replay.translation.to_array().map(f32::to_bits)
        );
        assert_eq!(
            plugin
                .configuration
                .distant_pose
                .target
                .to_array()
                .map(f32::to_bits),
            distant_replay.target.to_array().map(f32::to_bits)
        );
    }

    #[test]
    fn movement_frames_use_exact_replayable_sixty_hz_checkpoints() {
        let expected = [
            0, 8, 15, 23, 30, 38, 45, 53, 60, 68, 75, 83, 90, 98, 105, 113, 120, 128, 135, 143,
            150, 158, 165, 173,
        ];
        let first = (0..MOVEMENT_FRAME_COUNT)
            .map(|index| movement_tick_offset(index).unwrap())
            .collect::<Vec<_>>();
        let replay = (0..MOVEMENT_FRAME_COUNT)
            .map(|index| movement_tick_offset(index).unwrap())
            .collect::<Vec<_>>();

        assert_eq!(first, expected);
        assert_eq!(replay, expected);
        assert!(
            first
                .windows(2)
                .all(|window| matches!(window[1] - window[0], 7 | 8))
        );
        assert_eq!(TERRAIN_PHYSICS_HZ.to_bits(), 60.0_f64.to_bits());
        assert_eq!(
            CaptureKind::Movement(0).relative_path(),
            "movement/frame-000.png"
        );
        assert_eq!(
            CaptureKind::Movement(MOVEMENT_FRAME_COUNT - 1).relative_path(),
            "movement/frame-023.png"
        );
        assert!(movement_tick_offset(MOVEMENT_FRAME_COUNT).is_err());
    }

    #[test]
    fn movement_capture_rejects_a_camera_frozen_away_from_its_checkpoint() {
        let expected = CameraEvidence {
            translation_m: [1.0, 2.0, 3.0],
            forward: [0.0, 0.0, -1.0],
            vertical_fov_degrees: 60.0,
            forge_player_camera: true,
        };
        validate_capture_camera(0, &expected, &expected).unwrap();

        let mut frozen = expected.clone();
        frozen.translation_m[2] += 0.25;
        assert!(
            validate_capture_camera(1, &frozen, &expected)
                .unwrap_err()
                .contains("diverged")
        );
        frozen = expected.clone();
        frozen.vertical_fov_degrees += 1.0;
        assert!(validate_capture_camera(1, &frozen, &expected).is_err());
    }

    #[test]
    fn movement_cadence_reads_the_real_fixed_time_resource() {
        let mut movement = GameplayMovementRuntime::default();
        observe_fixed_timestep(&mut movement, &Time::<Fixed>::from_hz(60.0)).unwrap();
        assert!((movement.observed_fixed_hz.unwrap() - TERRAIN_PHYSICS_HZ).abs() <= 1.0e-4);
        assert!(
            observe_fixed_timestep(&mut movement, &Time::<Fixed>::from_hz(50.0))
                .unwrap_err()
                .contains("timestep")
        );
    }

    #[test]
    fn gameplay_stop_requires_physical_translation_and_position_to_settle() {
        let mut snapshot = PlayerRuntimeSnapshot {
            grounded: true,
            player_position: Vec3::new(4.0, 2.0, -8.0),
            horizontal_velocity: Vec3::ZERO,
            effective_translation: Vec3::ZERO,
            ..default()
        };
        assert!(stop_sample_is_stable(
            Some(snapshot.player_position),
            &snapshot
        ));

        snapshot.effective_translation.x = 0.01;
        assert!(!stop_sample_is_stable(
            Some(snapshot.player_position),
            &snapshot
        ));
        snapshot.effective_translation = Vec3::ZERO;
        assert!(!stop_sample_is_stable(
            Some(snapshot.player_position + Vec3::X * 0.01),
            &snapshot
        ));
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
