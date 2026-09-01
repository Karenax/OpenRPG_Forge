use bevy::{
    app::AppExit,
    diagnostic::FrameCount,
    ecs::system::SystemParam,
    prelude::*,
    render::view::screenshot::Screenshot,
    window::{PresentMode, PrimaryWindow},
};
use forge_engine::{
    EnvironmentProjectionStats, ForgePlayerCamera, ForgePlayerCharacter, GpuDiagnostic,
    PendingPlayerControlCommand, PipelineWarmupObservation, PlayerControlCommand,
    PlayerControllerSet, PlayerRuntimeBindingSnapshot, PlayerRuntimeBindings,
    PlayerRuntimeSnapshot, SceneryProjectionStats, SceneryProjectionStatus, TERRAIN_PHYSICS_HZ,
    VegetationProjectionStats, VisualValleyQualitySelection, VisualValleyQualityStats,
    VisualValleyShadowStats, VisualValleyStats, WaterProjectionStats, WeatherProjectionStats,
    validate_governed_gate1_render_state,
};
use forge_project::{PlayerControllerProfileDocument, TerrainDocument, VisualQualityTier};
use forge_world::TerrainGenerationPlan;
use serde::{Deserialize, Serialize};
use std::{
    fs,
    io::Read,
    path::{Path, PathBuf},
    time::{Duration, Instant},
};

const READY_SCHEMA: &str = "forge.gate1-gameplay-stutter-ready";
const SIGNAL_SCHEMA: &str = "forge.gate1-gameplay-stutter-start-signal";
const LEDGER_SCHEMA: &str = "forge.gate1-gameplay-stutter";
const SCHEMA_VERSION: u32 = 1;
const READY_FILE: &str = "gate1-gameplay-stutter.ready.json";
const START_SIGNAL_FILE: &str = "gate1-gameplay-stutter.start.signal.json";
const LEDGER_FILE: &str = "gate1-gameplay-stutter.json";
const FAILURE_FILE: &str = "gate1-gameplay-stutter.failure.txt";
const WATCHDOG: Duration = Duration::from_secs(75);
const READY_MAX_FIXED_TICKS: u32 = 1_200;
const FORWARD_TICKS: u32 = 900;
const REVERSE_TICKS: u32 = 900;
const TOTAL_MOVEMENT_TICKS: u32 = FORWARD_TICKS + REVERSE_TICKS;
const STOP_MAX_FIXED_TICKS: u32 = 45;
const STOP_STABLE_TICKS: u32 = 2;
const MIN_LEG_DISTANCE_M: f32 = 60.0;
const MAX_RETURN_DISTANCE_M: f32 = 5.0;
const MAX_SPAWN_POSITION_ERROR_M: f32 = 1.0;
const MAX_LATERAL_DRIFT_M: f32 = 1.5;
const HORIZONTAL_STEP_TOLERANCE_M: f32 = 0.005;
const MAX_CONTROLLER_TRANSLATION_ERROR_M: f32 = 0.001;
const MAX_UNGROUNDED_RUN: u32 = 4;
const START_X_M: f32 = 16.0;
const START_Z_M: f32 = 8.0;
const PRESENTMON_VERSION: &str = "2.4.1";
const MAX_START_SIGNAL_BYTES: u64 = 16 * 1024;
const PRESENTMON_SHA256: &str = "d74183e7ae630f72cd3690be0373ecbfdc6cbb86578148aab8fa2a7166068f34";

#[derive(Clone, Resource)]
struct Configuration {
    output_dir: PathBuf,
    terrain_hash: String,
    player_profile_hash: String,
    player_spawn_position: Vec3,
    maximum_horizontal_step_m: f32,
}

pub(crate) struct Gate1GameplayStutterPlugin {
    configuration: Configuration,
}

impl Gate1GameplayStutterPlugin {
    pub(crate) fn new(
        output_dir: PathBuf,
        terrain: &TerrainDocument,
        player: &PlayerControllerProfileDocument,
    ) -> Result<Self, String> {
        let terrain_plan = TerrainGenerationPlan::from_document(terrain)
            .map_err(|error| format!("cannot prepare stutter-proof terrain: {error}"))?;
        let capsule_half_height =
            player.controller.capsule_radius_m + player.controller.capsule_half_segment_m;
        let player_spawn_position = Vec3::new(
            START_X_M,
            terrain_plan
                .height_at(START_X_M, START_Z_M)
                .map_err(|error| format!("cannot sample stutter-proof spawn: {error}"))?
                + capsule_half_height
                + 0.02,
            START_Z_M,
        );
        Ok(Self {
            configuration: Configuration {
                output_dir,
                terrain_hash: terrain
                    .content_hash()
                    .map_err(|error| format!("cannot hash stutter-proof terrain: {error}"))?
                    .to_string(),
                player_profile_hash: player
                    .content_hash()
                    .map_err(|error| format!("cannot hash stutter-proof player profile: {error}"))?
                    .to_string(),
                player_spawn_position,
                maximum_horizontal_step_m: player.controller.walk_speed_mps / 60.0
                    + HORIZONTAL_STEP_TOLERANCE_M,
            },
        })
    }

    #[must_use]
    pub(crate) const fn player_spawn_position(&self) -> Vec3 {
        self.configuration.player_spawn_position
    }
}

impl Plugin for Gate1GameplayStutterPlugin {
    fn build(&self, app: &mut App) {
        app.insert_resource(Runtime::new(self.configuration.clone()))
            .add_systems(
                FixedUpdate,
                drive_controller.before(PlayerControllerSet::Drive),
            )
            .add_systems(
                FixedUpdate,
                observe_controller.after(PlayerControllerSet::Camera),
            )
            .add_systems(Last, observe_render_window)
            .add_systems(Update, watchdog);
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

impl GovernedScene<'_> {
    fn validate(&self) -> Result<(), String> {
        validate_governed_gate1_render_state(
            &self.visual_valley,
            &self.shadow,
            &self.vegetation,
            &self.scenery,
            &self.environment,
            &self.water,
            &self.weather,
            &self.quality,
        )
    }

    fn snapshot(&self) -> GovernedSceneEvidence {
        GovernedSceneEvidence {
            visual_valley: self.visual_valley.clone(),
            shadow: self.shadow.clone(),
            vegetation: self.vegetation.clone(),
            scenery: self.scenery.clone(),
            environment: self.environment.clone(),
            water: self.water.clone(),
            weather: self.weather.clone(),
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

#[derive(Clone, Copy, Debug, Eq, PartialEq)]
enum Phase {
    WaitingForReady,
    WaitingForSignal,
    PreparingForward,
    MovingForward,
    PreparingReverse,
    MovingReverse,
    PreparingStop,
    Stopping,
    Completed,
    Failed,
}

impl Phase {
    const fn label(self) -> &'static str {
        match self {
            Self::WaitingForReady => "waiting_for_ready",
            Self::WaitingForSignal => "waiting_for_signal",
            Self::PreparingForward => "preparing_forward",
            Self::MovingForward => "moving_forward",
            Self::PreparingReverse => "preparing_reverse",
            Self::MovingReverse => "moving_reverse",
            Self::PreparingStop => "preparing_stop",
            Self::Stopping => "stopping",
            Self::Completed => "completed",
            Self::Failed => "failed",
        }
    }

    const fn is_terminal(self) -> bool {
        matches!(self, Self::Completed | Self::Failed)
    }
}

#[derive(Resource)]
#[allow(
    clippy::struct_excessive_bools,
    reason = "the runtime state records independent fail-closed proof observations"
)]
struct Runtime {
    configuration: Configuration,
    started_at: Instant,
    phase: Phase,
    phase_ticks: u32,
    ready_wait_ticks: u32,
    ready_written: bool,
    signal: Option<StartSignal>,
    signal_observed_after_startup_ms: Option<f64>,
    initial_binding: Option<PlayerRuntimeBindingSnapshot>,
    initial_scene: Option<GovernedSceneEvidence>,
    initial_pipeline: Option<PipelineWarmupObservation>,
    initial_quality: Option<VisualValleyQualityStats>,
    initial_scenery: Option<SceneryProjectionStats>,
    initial_gpu: Option<GpuDiagnostic>,
    forward_sequence: Option<u64>,
    reverse_sequence: Option<u64>,
    stop_sequence: Option<u64>,
    forward_ticks: u32,
    reverse_ticks: u32,
    stop_wait_ticks: u32,
    stop_stable_ticks: u32,
    start_position: Option<Vec3>,
    forward_endpoint: Option<Vec3>,
    end_position: Option<Vec3>,
    stopped_position: Option<Vec3>,
    measurement_started_at: Option<Instant>,
    measurement_elapsed_seconds: Option<f64>,
    first_fixed_tick: Option<u64>,
    midpoint_fixed_tick: Option<u64>,
    last_fixed_tick: Option<u64>,
    previous_sample_fixed_tick: Option<u64>,
    previous_sample_position: Option<Vec3>,
    first_render_frame: Option<u32>,
    last_render_frame: Option<u32>,
    last_sample_render_frame: Option<u32>,
    render_frames_spanned: u32,
    render_invariant_checks: u32,
    render_measurement_active: bool,
    final_render_validation_pending: bool,
    final_render_validation_completed: bool,
    last_water_animation_updates: Option<u64>,
    water_animation_advancement_frames: u32,
    catch_up_fixed_ticks: u32,
    grounded_ticks: u32,
    collision_ticks: u32,
    current_ungrounded_run: u32,
    max_ungrounded_run: u32,
    max_horizontal_step_m: f32,
    max_controller_translation_error_m: f32,
}

impl Runtime {
    fn new(configuration: Configuration) -> Self {
        Self {
            configuration,
            started_at: Instant::now(),
            phase: Phase::WaitingForReady,
            phase_ticks: 0,
            ready_wait_ticks: 0,
            ready_written: false,
            signal: None,
            signal_observed_after_startup_ms: None,
            initial_binding: None,
            initial_scene: None,
            initial_pipeline: None,
            initial_quality: None,
            initial_scenery: None,
            initial_gpu: None,
            forward_sequence: None,
            reverse_sequence: None,
            stop_sequence: None,
            forward_ticks: 0,
            reverse_ticks: 0,
            stop_wait_ticks: 0,
            stop_stable_ticks: 0,
            start_position: None,
            forward_endpoint: None,
            end_position: None,
            stopped_position: None,
            measurement_started_at: None,
            measurement_elapsed_seconds: None,
            first_fixed_tick: None,
            midpoint_fixed_tick: None,
            last_fixed_tick: None,
            previous_sample_fixed_tick: None,
            previous_sample_position: None,
            first_render_frame: None,
            last_render_frame: None,
            last_sample_render_frame: None,
            render_frames_spanned: 0,
            render_invariant_checks: 0,
            render_measurement_active: false,
            final_render_validation_pending: false,
            final_render_validation_completed: false,
            last_water_animation_updates: None,
            water_animation_advancement_frames: 0,
            catch_up_fixed_ticks: 0,
            grounded_ticks: 0,
            collision_ticks: 0,
            current_ungrounded_run: 0,
            max_ungrounded_run: 0,
            max_horizontal_step_m: 0.0,
            max_controller_translation_error_m: 0.0,
        }
    }
}

#[derive(Clone, Debug, Deserialize, Serialize)]
#[serde(deny_unknown_fields)]
struct StartSignal {
    schema: String,
    schema_version: u32,
    process_id: u32,
    presentmon: PresentMonSignal,
}

#[derive(Clone, Debug, Deserialize, Serialize)]
#[serde(deny_unknown_fields)]
struct PresentMonSignal {
    capture_started: bool,
    target_process_id: u32,
    session_id: String,
    tool_version: String,
    tool_sha256: String,
    last_pre_signal_qpc: u64,
    measurement_not_before_qpc: u64,
    qpc_frequency_hz: u64,
    pre_signal_observed_rows: u32,
}

#[derive(Clone, Copy, Serialize)]
struct FileContract {
    ready: &'static str,
    start_signal: &'static str,
    ledger: &'static str,
    failure: &'static str,
}

#[derive(Clone, Copy, Serialize)]
#[allow(
    clippy::struct_excessive_bools,
    reason = "serialized protocol evidence keeps independent yes/no observations explicit"
)]
struct ProtocolEvidence {
    physical_resolution_px: [u32; 2],
    present_mode: &'static str,
    vsync_requested: bool,
    fixed_hz: u32,
    forward_ticks: u32,
    reverse_ticks: u32,
    nominal_measurement_seconds: u32,
    no_pause: bool,
    screenshots_requested: bool,
    presentmon_required: bool,
}

#[derive(Serialize)]
struct StartSignalContract {
    schema: &'static str,
    schema_version: u32,
    publish_atomically: bool,
    required_process_id: u32,
    required_presentmon_target_process_id: u32,
    required_presentmon_tool_version: &'static str,
    required_presentmon_tool_sha256: &'static str,
    qpc_boundary_required: bool,
}

#[derive(Clone, Serialize)]
struct SourceEvidence {
    terrain_hash: String,
    player_profile_hash: String,
    cadle_assets_used: bool,
}

#[derive(Clone, Serialize)]
struct BindingEvidence {
    player_entity: String,
    camera_entity: String,
    mesh_asset: String,
    rapier_collider_handle: [u32; 2],
    rapier_context_entity: String,
    backend_identity_stable: bool,
}

#[derive(Serialize)]
struct ReadyControllerEvidence {
    profile_hash: String,
    fixed_tick: u64,
    player_position_m: [f32; 3],
    binding: BindingEvidence,
    camera_marker_verified: bool,
}

#[derive(Clone, Copy, Serialize)]
struct QualificationBoundary {
    display_stutter_qualified: bool,
    human_review_performed: bool,
    gate1_route_qualified: bool,
}

#[derive(Serialize)]
struct ReadyLedger<'a> {
    schema: &'static str,
    schema_version: u32,
    status: &'static str,
    process_id: u32,
    build_source_commit: &'static str,
    build_profile: &'static str,
    watchdog_seconds: u64,
    files: FileContract,
    protocol: ProtocolEvidence,
    start_signal_contract: StartSignalContract,
    sources: SourceEvidence,
    governed_scene: GovernedSceneEvidence,
    pipeline_warmup: &'a PipelineWarmupObservation,
    quality: &'a VisualValleyQualityStats,
    scenery: &'a SceneryProjectionStats,
    gpu: &'a GpuDiagnostic,
    controller: ReadyControllerEvidence,
    qualification: QualificationBoundary,
}

#[derive(Serialize)]
struct ReadyEvidence {
    ready_file_written: bool,
    start_signal_file: &'static str,
    signal_schema: String,
    signal_schema_version: u32,
    signal_process_id: u32,
    observed_after_startup_ms: f64,
}

#[derive(Serialize)]
#[allow(
    clippy::struct_excessive_bools,
    reason = "serialized PresentMon evidence keeps independent host/runtime claims explicit"
)]
struct PresentMonEvidence {
    required: bool,
    host_declared_capture_started: bool,
    target_process_id: u32,
    session_id: String,
    tool_version: String,
    tool_sha256: String,
    last_pre_signal_qpc: u64,
    measurement_not_before_qpc: u64,
    qpc_frequency_hz: u64,
    pre_signal_observed_rows: u32,
    runtime_verified_capture: bool,
    display_metrics_ingested: bool,
}

#[derive(Serialize)]
struct PipelineStability {
    ready_before_measurement: bool,
    ready_for_entire_measurement: bool,
    initial_observation: PipelineWarmupObservation,
    final_observation: PipelineWarmupObservation,
}

#[derive(Serialize)]
struct QualityStability {
    fixed_high_before_measurement: bool,
    fixed_high_for_entire_measurement: bool,
    initial_observation: VisualValleyQualityStats,
    final_observation: VisualValleyQualityStats,
}

#[derive(Serialize)]
#[allow(
    clippy::struct_excessive_bools,
    reason = "serialized scenery evidence keeps independent readiness and fallback checks explicit"
)]
struct SceneryStability {
    nine_of_nine_before_measurement: bool,
    nine_of_nine_for_entire_measurement: bool,
    no_fallback_before_measurement: bool,
    no_fallback_for_entire_measurement: bool,
    initial_observation: SceneryProjectionStats,
    final_observation: SceneryProjectionStats,
}

#[derive(Serialize)]
struct GpuStability {
    present_before_measurement: bool,
    identity_stable: bool,
    initial_observation: GpuDiagnostic,
    final_observation: GpuDiagnostic,
}

#[derive(Serialize)]
#[allow(
    clippy::struct_excessive_bools,
    reason = "serialized stability evidence keeps independent observations explicit"
)]
struct GovernedSceneStability {
    qualified_before_measurement: bool,
    qualified_for_entire_measurement: bool,
    structural_identity_stable: bool,
    final_render_boundary_validated: bool,
    water_animation_advanced_each_render_frame: bool,
    water_animation_advancement_frames: u32,
    initial_observation: GovernedSceneEvidence,
    final_observation: GovernedSceneEvidence,
}

#[derive(Serialize)]
#[allow(
    clippy::struct_excessive_bools,
    reason = "serialized movement evidence keeps each safety and provenance observation explicit"
)]
struct MovementEvidence {
    kind: &'static str,
    forward_ticks: u32,
    reverse_ticks: u32,
    total_fixed_ticks: u32,
    nominal_duration_seconds: u32,
    observed_wall_duration_seconds: f64,
    first_fixed_tick: u64,
    midpoint_fixed_tick: u64,
    last_fixed_tick: u64,
    first_render_frame: u32,
    last_render_frame: u32,
    render_frames_spanned: u32,
    render_invariant_checks: u32,
    catch_up_fixed_ticks: u32,
    start_position_m: [f32; 3],
    forward_endpoint_m: [f32; 3],
    end_position_m: [f32; 3],
    stopped_position_m: [f32; 3],
    forward_distance_m: f32,
    reverse_distance_m: f32,
    return_distance_m: f32,
    forward_command_sequence: u64,
    reverse_command_sequence: u64,
    stop_command_sequence: u64,
    stop_wait_ticks: u32,
    stop_stable_ticks: u32,
    grounded_ticks: u32,
    collision_ticks: u32,
    maximum_ungrounded_run: u32,
    profile_horizontal_step_limit_m: f32,
    maximum_horizontal_step_m: f32,
    maximum_controller_translation_error_m: f32,
    binding: BindingEvidence,
    camera_marker_verified: bool,
    rapier_bindings_stable: bool,
    virtual_time_unpaused: bool,
    virtual_time_scale: f32,
    screenshots_observed: bool,
}

#[derive(Serialize)]
#[allow(
    clippy::struct_excessive_bools,
    reason = "serialized qualification evidence must keep every non-equivalent gate boundary explicit"
)]
struct LedgerQualification {
    real_gameplay_controller_exercised: bool,
    display_stutter_qualified: bool,
    presentmon_analysis_completed: bool,
    human_review_performed: bool,
    gate1_route_qualified: bool,
    gate1_qualified: bool,
    limitation: &'static str,
}

#[derive(Serialize)]
struct GameplayStutterLedger {
    schema: &'static str,
    schema_version: u32,
    status: &'static str,
    process_id: u32,
    build_source_commit: &'static str,
    build_profile: &'static str,
    watchdog_seconds: u64,
    protocol: ProtocolEvidence,
    sources: SourceEvidence,
    ready: ReadyEvidence,
    presentmon: PresentMonEvidence,
    governed_scene: GovernedSceneStability,
    pipeline_warmup: PipelineStability,
    quality: QualityStability,
    scenery: SceneryStability,
    gpu: GpuStability,
    movement: MovementEvidence,
    qualification: LedgerQualification,
}

fn drive_controller(
    mut pending: ResMut<PendingPlayerControlCommand>,
    mut runtime: ResMut<Runtime>,
    mut exit: MessageWriter<AppExit>,
) {
    if runtime.phase.is_terminal() {
        return;
    }
    let result = match runtime.phase {
        Phase::PreparingForward if runtime.forward_sequence.is_none() => pending
            .submit(PlayerControlCommand {
                move_axis: Vec2::Y,
                ..default()
            })
            .map(|sequence| runtime.forward_sequence = Some(sequence)),
        Phase::PreparingReverse if runtime.reverse_sequence.is_none() => pending
            .submit(PlayerControlCommand {
                move_axis: Vec2::NEG_Y,
                ..default()
            })
            .map(|sequence| runtime.reverse_sequence = Some(sequence)),
        Phase::PreparingStop if runtime.stop_sequence.is_none() => pending
            .submit(PlayerControlCommand::default())
            .map(|sequence| runtime.stop_sequence = Some(sequence)),
        _ => Ok(()),
    }
    .map_err(|error| format!("cannot submit gameplay stutter command: {error}"));
    if let Err(error) = result {
        fail(&mut runtime, &mut exit, &error);
    }
}

#[allow(
    clippy::needless_pass_by_value,
    clippy::too_many_arguments,
    clippy::too_many_lines,
    reason = "Bevy system parameters are owned handles and the proof validates all identities together"
)]
fn observe_controller(
    frame: Res<FrameCount>,
    fixed_time: Res<Time<Fixed>>,
    virtual_time: Res<Time<Virtual>>,
    pipeline: Res<PipelineWarmupObservation>,
    scene: GovernedScene,
    windows: Query<&Window, With<PrimaryWindow>>,
    bindings: Res<PlayerRuntimeBindings>,
    snapshot: Res<PlayerRuntimeSnapshot>,
    cameras: Query<(Entity, &Camera), With<ForgePlayerCamera>>,
    players: Query<Entity, With<ForgePlayerCharacter>>,
    screenshots: Query<Entity, With<Screenshot>>,
    mut runtime: ResMut<Runtime>,
    mut exit: MessageWriter<AppExit>,
) {
    if runtime.phase.is_terminal() {
        return;
    }
    runtime.phase_ticks = runtime.phase_ticks.saturating_add(1);
    let result = (|| -> Result<(), String> {
        validate_fixed_timestep(&fixed_time)?;
        match runtime.phase {
            Phase::WaitingForReady => {
                runtime.ready_wait_ticks = runtime.ready_wait_ticks.saturating_add(1);
                let Some(binding) = ready_binding(
                    &runtime.configuration,
                    &pipeline,
                    &scene,
                    &windows,
                    &bindings,
                    &snapshot,
                    &cameras,
                    &players,
                    &screenshots,
                    &virtual_time,
                )?
                else {
                    if runtime.ready_wait_ticks >= READY_MAX_FIXED_TICKS {
                        return Err(format!(
                            "gameplay stutter proof remained unready after {READY_MAX_FIXED_TICKS} fixed ticks"
                        ));
                    }
                    return Ok(());
                };
                write_ready(
                    &runtime,
                    binding,
                    &snapshot,
                    &pipeline,
                    &scene,
                    scene
                        .gpu
                        .as_deref()
                        .ok_or("GPU identity disappeared at ready boundary")?,
                )?;
                runtime.initial_binding = Some(binding);
                runtime.initial_scene = Some(scene.snapshot());
                runtime.initial_pipeline = Some(pipeline.clone());
                runtime.initial_quality = Some(scene.quality.clone());
                runtime.initial_scenery = Some(scene.scenery.clone());
                runtime.initial_gpu = scene.gpu.as_deref().cloned();
                runtime.ready_written = true;
                enter_phase(&mut runtime, Phase::WaitingForSignal);
                Ok(())
            }
            Phase::WaitingForSignal => {
                validate_common(
                    &runtime,
                    &pipeline,
                    &scene,
                    &windows,
                    &bindings,
                    &snapshot,
                    &cameras,
                    &players,
                    &screenshots,
                    &virtual_time,
                )?;
                let path = runtime.configuration.output_dir.join(START_SIGNAL_FILE);
                let Some(signal) = read_start_signal(&path, std::process::id())? else {
                    return Ok(());
                };
                runtime.signal_observed_after_startup_ms =
                    Some(runtime.started_at.elapsed().as_secs_f64() * 1_000.0);
                runtime.signal = Some(signal);
                runtime.start_position = Some(snapshot.player_position);
                runtime.previous_sample_position = Some(snapshot.player_position);
                runtime.measurement_started_at = Some(Instant::now());
                runtime.last_water_animation_updates = Some(scene.water.animation_updates);
                runtime.render_measurement_active = true;
                enter_phase(&mut runtime, Phase::PreparingForward);
                Ok(())
            }
            Phase::PreparingForward => {
                validate_common_for_measurement(
                    &runtime,
                    &pipeline,
                    &scene,
                    &windows,
                    &bindings,
                    &snapshot,
                    &cameras,
                    &players,
                    &screenshots,
                    &virtual_time,
                )?;
                let sequence = runtime
                    .forward_sequence
                    .ok_or("forward command was not submitted")?;
                if !require_sequence(&snapshot, sequence, runtime.phase_ticks, "forward")? {
                    return Ok(());
                }
                sample_movement(&mut runtime, &snapshot, frame.0, true)?;
                enter_phase(&mut runtime, Phase::MovingForward);
                Ok(())
            }
            Phase::MovingForward => {
                validate_common_for_measurement(
                    &runtime,
                    &pipeline,
                    &scene,
                    &windows,
                    &bindings,
                    &snapshot,
                    &cameras,
                    &players,
                    &screenshots,
                    &virtual_time,
                )?;
                let sequence = runtime.forward_sequence.ok_or("forward sequence missing")?;
                require_exact_sequence(&snapshot, sequence, "forward")?;
                sample_movement(&mut runtime, &snapshot, frame.0, true)?;
                if runtime.forward_ticks >= FORWARD_TICKS {
                    runtime.forward_endpoint = Some(snapshot.player_position);
                    runtime.midpoint_fixed_tick = Some(snapshot.fixed_tick);
                    enter_phase(&mut runtime, Phase::PreparingReverse);
                }
                Ok(())
            }
            Phase::PreparingReverse => {
                validate_common_for_measurement(
                    &runtime,
                    &pipeline,
                    &scene,
                    &windows,
                    &bindings,
                    &snapshot,
                    &cameras,
                    &players,
                    &screenshots,
                    &virtual_time,
                )?;
                let sequence = runtime
                    .reverse_sequence
                    .ok_or("reverse command was not submitted")?;
                if !require_sequence(&snapshot, sequence, runtime.phase_ticks, "reverse")? {
                    return Err(
                        "reverse command was not applied at the exact 900-tick boundary".to_owned(),
                    );
                }
                sample_movement(&mut runtime, &snapshot, frame.0, false)?;
                enter_phase(&mut runtime, Phase::MovingReverse);
                Ok(())
            }
            Phase::MovingReverse => {
                validate_common_for_measurement(
                    &runtime,
                    &pipeline,
                    &scene,
                    &windows,
                    &bindings,
                    &snapshot,
                    &cameras,
                    &players,
                    &screenshots,
                    &virtual_time,
                )?;
                let sequence = runtime.reverse_sequence.ok_or("reverse sequence missing")?;
                require_exact_sequence(&snapshot, sequence, "reverse")?;
                sample_movement(&mut runtime, &snapshot, frame.0, false)?;
                if runtime.reverse_ticks >= REVERSE_TICKS {
                    runtime.end_position = Some(snapshot.player_position);
                    runtime.last_fixed_tick = Some(snapshot.fixed_tick);
                    runtime.measurement_elapsed_seconds = Some(
                        runtime
                            .measurement_started_at
                            .ok_or("measurement start instant missing")?
                            .elapsed()
                            .as_secs_f64(),
                    );
                    runtime.final_render_validation_pending = true;
                    enter_phase(&mut runtime, Phase::PreparingStop);
                }
                Ok(())
            }
            Phase::PreparingStop => {
                validate_common(
                    &runtime,
                    &pipeline,
                    &scene,
                    &windows,
                    &bindings,
                    &snapshot,
                    &cameras,
                    &players,
                    &screenshots,
                    &virtual_time,
                )?;
                let sequence = runtime
                    .stop_sequence
                    .ok_or("stop command was not submitted")?;
                if !require_sequence(&snapshot, sequence, runtime.phase_ticks, "stop")? {
                    return Err(
                        "stop command was not applied immediately after the movement window"
                            .to_owned(),
                    );
                }
                enter_phase(&mut runtime, Phase::Stopping);
                Ok(())
            }
            Phase::Stopping => {
                validate_common(
                    &runtime,
                    &pipeline,
                    &scene,
                    &windows,
                    &bindings,
                    &snapshot,
                    &cameras,
                    &players,
                    &screenshots,
                    &virtual_time,
                )?;
                runtime.stop_wait_ticks = runtime.stop_wait_ticks.saturating_add(1);
                if runtime.stop_wait_ticks > STOP_MAX_FIXED_TICKS {
                    return Err(format!(
                        "gameplay controller did not stop within {STOP_MAX_FIXED_TICKS} fixed ticks"
                    ));
                }
                let sequence = runtime.stop_sequence.ok_or("stop sequence missing")?;
                require_exact_sequence(&snapshot, sequence, "stop")?;
                if snapshot.grounded && snapshot.horizontal_velocity.length() <= 0.05 {
                    runtime.stop_stable_ticks = runtime.stop_stable_ticks.saturating_add(1);
                } else {
                    runtime.stop_stable_ticks = 0;
                }
                if runtime.stop_stable_ticks >= STOP_STABLE_TICKS {
                    if !runtime.final_render_validation_completed {
                        return Ok(());
                    }
                    runtime.stopped_position = Some(snapshot.player_position);
                    write_ledger(
                        &runtime,
                        &pipeline,
                        &scene,
                        scene
                            .gpu
                            .as_deref()
                            .ok_or("GPU identity disappeared at final boundary")?,
                    )?;
                    runtime.phase = Phase::Completed;
                    tracing::info!(
                        target: "forge::proof",
                        schema = "forge.runtime-event",
                        schema_version = 1_u64,
                        event_kind = "gate1_gameplay_stutter_controller_window_completed",
                        ledger = %runtime.configuration.output_dir.join(LEDGER_FILE).display(),
                        movement_ticks = u64::from(TOTAL_MOVEMENT_TICKS),
                        display_stutter_qualified = false,
                        gate1_route_qualified = false,
                        "uninterrupted gameplay controller window completed; PresentMon analysis remains external"
                    );
                    exit.write(AppExit::Success);
                }
                Ok(())
            }
            Phase::Completed | Phase::Failed => Ok(()),
        }
    })();
    if let Err(error) = result {
        fail(&mut runtime, &mut exit, &error);
    }
}

#[allow(
    clippy::needless_pass_by_value,
    clippy::too_many_arguments,
    reason = "Bevy system parameters are owned handles and every rendered frame keeps invariants explicit"
)]
fn observe_render_window(
    virtual_time: Res<Time<Virtual>>,
    pipeline: Res<PipelineWarmupObservation>,
    scene: GovernedScene,
    windows: Query<&Window, With<PrimaryWindow>>,
    screenshots: Query<Entity, With<Screenshot>>,
    mut runtime: ResMut<Runtime>,
    mut exit: MessageWriter<AppExit>,
) {
    if !runtime.render_measurement_active {
        return;
    }
    if let Err(error) = observe_water_animation(&mut runtime, scene.water.animation_updates) {
        fail(&mut runtime, &mut exit, &error);
        return;
    }
    let result = validate_render_invariants(
        &runtime,
        &pipeline,
        &scene,
        &windows,
        &screenshots,
        &virtual_time,
    );
    if let Err(error) = result {
        fail(&mut runtime, &mut exit, &error);
        return;
    }
    runtime.render_invariant_checks = runtime.render_invariant_checks.saturating_add(1);
    if runtime.final_render_validation_pending {
        runtime.final_render_validation_pending = false;
        runtime.final_render_validation_completed = true;
        runtime.render_measurement_active = false;
    }
}

fn observe_water_animation(runtime: &mut Runtime, current_updates: u64) -> Result<(), String> {
    let previous_updates = runtime
        .last_water_animation_updates
        .ok_or("water animation baseline is absent")?;
    if current_updates <= previous_updates {
        return Err(format!(
            "water animation did not advance on a rendered measurement frame: previous={previous_updates}, current={current_updates}"
        ));
    }
    runtime.last_water_animation_updates = Some(current_updates);
    runtime.water_animation_advancement_frames =
        runtime.water_animation_advancement_frames.saturating_add(1);
    Ok(())
}

fn watchdog(mut runtime: ResMut<Runtime>, mut exit: MessageWriter<AppExit>) {
    if runtime.phase.is_terminal() || runtime.started_at.elapsed() < WATCHDOG {
        return;
    }
    let phase = runtime.phase;
    fail(
        &mut runtime,
        &mut exit,
        &format!(
            "gameplay stutter proof exceeded its {} second watchdog in phase {}",
            WATCHDOG.as_secs(),
            phase.label()
        ),
    );
}

#[allow(
    clippy::too_many_arguments,
    reason = "the readiness boundary deliberately names every independent runtime identity"
)]
fn ready_binding(
    configuration: &Configuration,
    pipeline: &PipelineWarmupObservation,
    scene: &GovernedScene,
    windows: &Query<&Window, With<PrimaryWindow>>,
    bindings: &PlayerRuntimeBindings,
    snapshot: &PlayerRuntimeSnapshot,
    cameras: &Query<(Entity, &Camera), With<ForgePlayerCamera>>,
    players: &Query<Entity, With<ForgePlayerCharacter>>,
    screenshots: &Query<Entity, With<Screenshot>>,
    virtual_time: &Time<Virtual>,
) -> Result<Option<PlayerRuntimeBindingSnapshot>, String> {
    if !pipeline.is_ready()
        || scene.validate().is_err()
        || !quality_is_fixed_high(&scene.quality)
        || !scenery_is_activated(&scene.scenery)
        || scene.gpu.is_none()
        || snapshot.profile_content_hash.is_none()
        || !snapshot.grounded
        || !snapshot.camera_query_succeeded
        || bindings.snapshot().is_none()
    {
        return Ok(None);
    }
    validate_window(windows)?;
    validate_virtual_time(virtual_time)?;
    if !screenshots.is_empty() {
        return Err("a screenshot entity exists before stutter measurement".to_owned());
    }
    let binding = bindings
        .snapshot()
        .ok_or("player runtime binding disappeared at ready boundary")?;
    validate_player_identity(configuration, binding, snapshot, cameras, players)?;
    if snapshot.player_position.z <= 0.0
        || (snapshot.player_position.x - START_X_M).abs() > MAX_SPAWN_POSITION_ERROR_M
        || (snapshot.player_position.z - START_Z_M).abs() > MAX_SPAWN_POSITION_ERROR_M
    {
        return Err(format!(
            "stutter-proof player did not start at the governed spawn: {:?}",
            snapshot.player_position
        ));
    }
    Ok(Some(binding))
}

#[allow(clippy::too_many_arguments)]
fn validate_common(
    runtime: &Runtime,
    pipeline: &PipelineWarmupObservation,
    scene: &GovernedScene,
    windows: &Query<&Window, With<PrimaryWindow>>,
    bindings: &PlayerRuntimeBindings,
    snapshot: &PlayerRuntimeSnapshot,
    cameras: &Query<(Entity, &Camera), With<ForgePlayerCamera>>,
    players: &Query<Entity, With<ForgePlayerCharacter>>,
    screenshots: &Query<Entity, With<Screenshot>>,
    virtual_time: &Time<Virtual>,
) -> Result<(), String> {
    validate_render_invariants(runtime, pipeline, scene, windows, screenshots, virtual_time)?;
    let expected = runtime
        .initial_binding
        .ok_or("initial player binding is absent")?;
    let actual = bindings
        .snapshot()
        .ok_or("player runtime binding disappeared")?;
    if actual != expected || !actual.backend_identity_stable || !snapshot.backend_identity_stable {
        return Err(format!(
            "player or Rapier binding changed: expected {expected:?}, observed {actual:?}"
        ));
    }
    validate_player_identity(&runtime.configuration, actual, snapshot, cameras, players)
}

#[allow(clippy::too_many_arguments)]
fn validate_common_for_measurement(
    runtime: &Runtime,
    pipeline: &PipelineWarmupObservation,
    scene: &GovernedScene,
    windows: &Query<&Window, With<PrimaryWindow>>,
    bindings: &PlayerRuntimeBindings,
    snapshot: &PlayerRuntimeSnapshot,
    cameras: &Query<(Entity, &Camera), With<ForgePlayerCamera>>,
    players: &Query<Entity, With<ForgePlayerCharacter>>,
    screenshots: &Query<Entity, With<Screenshot>>,
    virtual_time: &Time<Virtual>,
) -> Result<(), String> {
    if runtime.signal.is_none() {
        return Err("measurement began without an accepted PresentMon start signal".to_owned());
    }
    validate_common(
        runtime,
        pipeline,
        scene,
        windows,
        bindings,
        snapshot,
        cameras,
        players,
        screenshots,
        virtual_time,
    )
}

fn validate_render_invariants(
    runtime: &Runtime,
    pipeline: &PipelineWarmupObservation,
    scene: &GovernedScene,
    windows: &Query<&Window, With<PrimaryWindow>>,
    screenshots: &Query<Entity, With<Screenshot>>,
    virtual_time: &Time<Virtual>,
) -> Result<(), String> {
    validate_window(windows)?;
    validate_virtual_time(virtual_time)?;
    scene.validate()?;
    let initial_scene = runtime
        .initial_scene
        .as_ref()
        .ok_or("initial governed scene observation is absent")?;
    if !governed_scene_identity_matches(initial_scene, scene) {
        return Err("governed Visual Valley structural identity changed".to_owned());
    }
    if !screenshots.is_empty() {
        return Err("a screenshot entity appeared during stutter measurement".to_owned());
    }
    let initial_pipeline = runtime
        .initial_pipeline
        .as_ref()
        .ok_or("initial pipeline observation is absent")?;
    if !pipeline.is_ready()
        || pipeline.total != initial_pipeline.total
        || pipeline.ready != pipeline.total
        || pipeline.queued != 0
        || pipeline.creating != 0
        || pipeline.waiting != 0
        || pipeline.errors != 0
    {
        return Err(format!(
            "pipeline warmup changed during stutter measurement: {pipeline:?}"
        ));
    }
    let initial_quality = runtime
        .initial_quality
        .as_ref()
        .ok_or("initial quality observation is absent")?;
    if !quality_is_fixed_high(&scene.quality)
        || scene.quality.profile_hash != initial_quality.profile_hash
        || scene.quality.transition_count != initial_quality.transition_count
    {
        return Err("fixed High quality changed during stutter measurement".to_owned());
    }
    let initial_scenery = runtime
        .initial_scenery
        .as_ref()
        .ok_or("initial scenery observation is absent")?;
    if !scenery_is_activated(&scene.scenery)
        || scene.scenery.manifest_hash != initial_scenery.manifest_hash
        || scene.scenery.loaded_models != initial_scenery.loaded_models
        || scene.scenery.replacement_instances != initial_scenery.replacement_instances
    {
        return Err("governed scenery changed during stutter measurement".to_owned());
    }
    let initial_gpu = runtime
        .initial_gpu
        .as_ref()
        .ok_or("initial GPU observation is absent")?;
    let gpu = scene
        .gpu
        .as_deref()
        .ok_or("GPU identity disappeared during stutter measurement")?;
    if !gpu_identity_matches(initial_gpu, gpu) {
        return Err("GPU identity changed during stutter measurement".to_owned());
    }
    Ok(())
}

fn validate_player_identity(
    configuration: &Configuration,
    binding: PlayerRuntimeBindingSnapshot,
    snapshot: &PlayerRuntimeSnapshot,
    cameras: &Query<(Entity, &Camera), With<ForgePlayerCamera>>,
    players: &Query<Entity, With<ForgePlayerCharacter>>,
) -> Result<(), String> {
    if snapshot.profile_content_hash.map(|hash| hash.to_string())
        != Some(configuration.player_profile_hash.clone())
    {
        return Err("player profile hash changed during stutter proof".to_owned());
    }
    if !binding.backend_identity_stable
        || binding.rapier_collider_handle.is_none()
        || binding.rapier_context_entity.is_none()
        || !snapshot.camera_query_succeeded
    {
        return Err("player camera or Rapier backend is not stable".to_owned());
    }
    let (camera_entity, camera) = cameras
        .single()
        .map_err(|_| "stutter proof requires exactly one Forge player camera".to_owned())?;
    if camera_entity != binding.camera_entity || !camera.is_active {
        return Err("active Forge player camera does not match its runtime binding".to_owned());
    }
    let player_entity = players
        .single()
        .map_err(|_| "stutter proof requires exactly one Forge player character".to_owned())?;
    if player_entity != binding.player_entity {
        return Err("Forge player character does not match its runtime binding".to_owned());
    }
    Ok(())
}

fn validate_fixed_timestep(fixed_time: &Time<Fixed>) -> Result<(), String> {
    let expected = Duration::from_secs_f64(1.0 / TERRAIN_PHYSICS_HZ);
    if fixed_time.timestep() != expected {
        return Err(format!(
            "stutter proof observed fixed timestep {:?}, expected {expected:?}",
            fixed_time.timestep()
        ));
    }
    Ok(())
}

fn validate_virtual_time(time: &Time<Virtual>) -> Result<(), String> {
    if time.is_paused() || time.relative_speed().to_bits() != 1.0_f32.to_bits() {
        return Err(format!(
            "stutter proof requires unpaused 1.0x virtual time, observed paused={} speed={}",
            time.is_paused(),
            time.relative_speed()
        ));
    }
    Ok(())
}

fn validate_window(windows: &Query<&Window, With<PrimaryWindow>>) -> Result<(), String> {
    let window = windows
        .single()
        .map_err(|_| "stutter proof requires exactly one primary window".to_owned())?;
    let resolution = [
        window.resolution.physical_width(),
        window.resolution.physical_height(),
    ];
    if resolution != [1920, 1080] || window.present_mode != PresentMode::AutoNoVsync {
        return Err(format!(
            "stutter proof requires 1920x1080 AutoNoVsync, observed {}x{} {:?}",
            resolution[0], resolution[1], window.present_mode
        ));
    }
    Ok(())
}

fn quality_is_fixed_high(quality: &VisualValleyQualityStats) -> bool {
    quality.selection == VisualValleyQualitySelection::Fixed(VisualQualityTier::High)
        && quality.startup_tier == VisualQualityTier::High
        && quality.current_tier == VisualQualityTier::High
        && quality.dynamically_applied_tier == Some(VisualQualityTier::High)
        && !quality.adaptation_enabled
        && quality.transition_count == 0
}

fn scenery_is_activated(scenery: &SceneryProjectionStats) -> bool {
    scenery.status == SceneryProjectionStatus::Activated
        && scenery.unique_models == 9
        && scenery.loaded_models == scenery.unique_models
        && scenery.failed_models == 0
        && scenery.requested_instances > 0
        && scenery.replacement_instances > 0
        && scenery.tracked_files == 27
        && scenery.tracked_bytes > 0
}

fn governed_scene_identity_matches(
    initial: &GovernedSceneEvidence,
    current: &GovernedScene,
) -> bool {
    initial.visual_valley.eq(&current.visual_valley)
        && initial.shadow.eq(&current.shadow)
        && initial.vegetation.terrain_document_hash == current.vegetation.terrain_document_hash
        && initial.vegetation.vegetation_document_hash
            == current.vegetation.vegetation_document_hash
        && initial
            .vegetation
            .placements
            .eq(&current.vegetation.placements)
        && initial
            .vegetation
            .projection
            .eq(&current.vegetation.projection)
        && initial
            .vegetation
            .triangles
            .eq(&current.vegetation.triangles)
        && initial
            .vegetation
            .render_path
            .eq(&current.vegetation.render_path)
        && initial.scenery.eq(&current.scenery)
        && initial.environment.eq(&current.environment)
        && water_structural_identity_matches(&initial.water, &current.water)
        && initial.weather.eq(&current.weather)
}

fn water_structural_identity_matches(
    initial: &WaterProjectionStats,
    current: &WaterProjectionStats,
) -> bool {
    if current.animation_updates < initial.animation_updates {
        return false;
    }
    let mut normalized_initial = initial.clone();
    let mut normalized_current = current.clone();
    normalized_initial.animation_updates = 0;
    normalized_current.animation_updates = 0;
    normalized_initial == normalized_current
}

fn gpu_identity_matches(left: &GpuDiagnostic, right: &GpuDiagnostic) -> bool {
    left.name == right.name
        && left.vendor == right.vendor
        && left.device == right.device
        && left.device_type == right.device_type
        && left.driver == right.driver
        && left.driver_info == right.driver_info
        && left.backend == right.backend
}

fn require_sequence(
    snapshot: &PlayerRuntimeSnapshot,
    expected: u64,
    phase_ticks: u32,
    label: &str,
) -> Result<bool, String> {
    match snapshot.last_applied_sequence {
        Some(actual) if actual == expected => Ok(true),
        Some(actual) if actual > expected => Err(format!(
            "{label} sequence {expected} was skipped by applied sequence {actual}"
        )),
        _ if phase_ticks <= 4 => Ok(false),
        _ => Err(format!(
            "{label} sequence {expected} was not applied within four fixed ticks"
        )),
    }
}

fn require_exact_sequence(
    snapshot: &PlayerRuntimeSnapshot,
    expected: u64,
    label: &str,
) -> Result<(), String> {
    if snapshot.last_applied_sequence == Some(expected) {
        Ok(())
    } else {
        Err(format!(
            "{label} sequence changed: expected {expected}, observed {:?}",
            snapshot.last_applied_sequence
        ))
    }
}

fn sample_movement(
    runtime: &mut Runtime,
    snapshot: &PlayerRuntimeSnapshot,
    render_frame: u32,
    forward: bool,
) -> Result<(), String> {
    if !snapshot.player_position.is_finite()
        || !snapshot.horizontal_velocity.is_finite()
        || !snapshot.effective_translation.is_finite()
    {
        return Err("gameplay stutter movement produced non-finite state".to_owned());
    }
    if let Some(previous) = runtime.previous_sample_fixed_tick
        && snapshot.fixed_tick != previous.saturating_add(1)
    {
        return Err(format!(
            "movement fixed ticks are not consecutive: previous {previous}, current {}",
            snapshot.fixed_tick
        ));
    }
    runtime.previous_sample_fixed_tick = Some(snapshot.fixed_tick);
    if let Some(previous_position) = runtime.previous_sample_position {
        let observed_translation = snapshot.player_position - previous_position;
        let step_distance_m = observed_translation.xz().length();
        let controller_translation_error_m = observed_translation
            .xz()
            .distance(snapshot.effective_translation.xz());
        if controller_translation_error_m > MAX_CONTROLLER_TRANSLATION_ERROR_M {
            return Err(format!(
                "observed player translation differs from the controller/KCC effective translation by {controller_translation_error_m} m; limit is {MAX_CONTROLLER_TRANSLATION_ERROR_M} m"
            ));
        }
        if step_distance_m > runtime.configuration.maximum_horizontal_step_m {
            return Err(format!(
                "gameplay movement advanced {step_distance_m} m in one fixed tick; profile-derived limit is {} m",
                runtime.configuration.maximum_horizontal_step_m
            ));
        }
        runtime.max_horizontal_step_m = runtime.max_horizontal_step_m.max(step_distance_m);
        runtime.max_controller_translation_error_m = runtime
            .max_controller_translation_error_m
            .max(controller_translation_error_m);
    }
    runtime.previous_sample_position = Some(snapshot.player_position);
    runtime.first_fixed_tick.get_or_insert(snapshot.fixed_tick);
    runtime.last_fixed_tick = Some(snapshot.fixed_tick);
    let first_render_frame = *runtime.first_render_frame.get_or_insert(render_frame);
    runtime.last_render_frame = Some(render_frame);
    runtime.render_frames_spanned = render_frame
        .saturating_sub(first_render_frame)
        .saturating_add(1);
    if runtime.last_sample_render_frame == Some(render_frame) {
        runtime.catch_up_fixed_ticks = runtime.catch_up_fixed_ticks.saturating_add(1);
    }
    runtime.last_sample_render_frame = Some(render_frame);
    if forward {
        runtime.forward_ticks = runtime.forward_ticks.saturating_add(1);
        if runtime.forward_ticks > FORWARD_TICKS {
            return Err("forward movement exceeded its exact fixed-tick budget".to_owned());
        }
    } else {
        runtime.reverse_ticks = runtime.reverse_ticks.saturating_add(1);
        if runtime.reverse_ticks > REVERSE_TICKS {
            return Err("reverse movement exceeded its exact fixed-tick budget".to_owned());
        }
    }
    if snapshot.grounded {
        runtime.grounded_ticks = runtime.grounded_ticks.saturating_add(1);
        runtime.current_ungrounded_run = 0;
    } else {
        runtime.current_ungrounded_run = runtime.current_ungrounded_run.saturating_add(1);
        runtime.max_ungrounded_run = runtime
            .max_ungrounded_run
            .max(runtime.current_ungrounded_run);
    }
    if snapshot.collisions > 0 {
        runtime.collision_ticks = runtime.collision_ticks.saturating_add(1);
    }
    Ok(())
}

fn read_start_signal(path: &Path, expected_process_id: u32) -> Result<Option<StartSignal>, String> {
    let metadata = match fs::symlink_metadata(path) {
        Ok(metadata) => metadata,
        Err(error) if error.kind() == std::io::ErrorKind::NotFound => return Ok(None),
        Err(error) => {
            return Err(format!(
                "cannot inspect PresentMon start signal {}: {error}",
                path.display()
            ));
        }
    };
    if metadata.file_type().is_symlink() || !metadata.is_file() || metadata.len() == 0 {
        return Err(format!(
            "PresentMon start signal must be a non-empty regular file: {}",
            path.display()
        ));
    }
    let file = fs::File::open(path)
        .map_err(|error| format!("cannot open PresentMon start signal: {error}"))?;
    let opened_metadata = file
        .metadata()
        .map_err(|error| format!("cannot inspect opened PresentMon start signal: {error}"))?;
    if !opened_metadata.is_file()
        || opened_metadata.len() == 0
        || opened_metadata.len() > MAX_START_SIGNAL_BYTES
    {
        return Err(format!(
            "opened PresentMon start signal must be a regular file of 1..={MAX_START_SIGNAL_BYTES} bytes"
        ));
    }
    let mut json = String::new();
    file.take(MAX_START_SIGNAL_BYTES + 1)
        .read_to_string(&mut json)
        .map_err(|error| format!("cannot read PresentMon start signal: {error}"))?;
    if json.len() as u64 > MAX_START_SIGNAL_BYTES {
        return Err("PresentMon start signal exceeds its bounded size".to_owned());
    }
    let signal: StartSignal = serde_json::from_str(&json)
        .map_err(|error| format!("invalid PresentMon start signal JSON: {error}"))?;
    validate_start_signal(&signal, expected_process_id)?;
    Ok(Some(signal))
}

fn validate_start_signal(signal: &StartSignal, expected_process_id: u32) -> Result<(), String> {
    if signal.schema != SIGNAL_SCHEMA || signal.schema_version != SCHEMA_VERSION {
        return Err("PresentMon start signal schema identity is invalid".to_owned());
    }
    if signal.process_id != expected_process_id
        || signal.presentmon.target_process_id != expected_process_id
    {
        return Err(format!(
            "PresentMon start signal targets PID {}/{}, expected {expected_process_id}",
            signal.process_id, signal.presentmon.target_process_id
        ));
    }
    if !signal.presentmon.capture_started {
        return Err("PresentMon start signal does not declare an active capture".to_owned());
    }
    if signal.presentmon.tool_version != PRESENTMON_VERSION
        || signal.presentmon.tool_sha256 != PRESENTMON_SHA256
    {
        return Err("PresentMon start signal tool identity is not the pinned binary".to_owned());
    }
    if signal.presentmon.last_pre_signal_qpc == 0
        || signal.presentmon.measurement_not_before_qpc == 0
        || signal.presentmon.qpc_frequency_hz == 0
        || signal.presentmon.pre_signal_observed_rows == 0
        || signal.presentmon.last_pre_signal_qpc >= signal.presentmon.measurement_not_before_qpc
    {
        return Err(
            "PresentMon start signal has no observed pre-measurement QPC boundary".to_owned(),
        );
    }
    if signal.presentmon.session_id.is_empty()
        || signal.presentmon.session_id.len() > 128
        || !signal
            .presentmon
            .session_id
            .chars()
            .all(|character| character.is_ascii_alphanumeric() || matches!(character, '-' | '_'))
    {
        return Err("PresentMon session id is empty or unsafe".to_owned());
    }
    Ok(())
}

fn write_ready(
    runtime: &Runtime,
    binding: PlayerRuntimeBindingSnapshot,
    snapshot: &PlayerRuntimeSnapshot,
    pipeline: &PipelineWarmupObservation,
    scene: &GovernedScene,
    gpu: &GpuDiagnostic,
) -> Result<(), String> {
    for forbidden in [START_SIGNAL_FILE, LEDGER_FILE, FAILURE_FILE] {
        if runtime.configuration.output_dir.join(forbidden).exists() {
            return Err(format!(
                "stale gameplay stutter artifact exists before ready: {forbidden}"
            ));
        }
    }
    let process_id = std::process::id();
    let ledger = ReadyLedger {
        schema: READY_SCHEMA,
        schema_version: SCHEMA_VERSION,
        status: "ready_waiting_for_presentmon_start_signal",
        process_id,
        build_source_commit: embedded_source_commit()?,
        build_profile: embedded_build_profile()?,
        watchdog_seconds: WATCHDOG.as_secs(),
        files: file_contract(),
        protocol: protocol(),
        start_signal_contract: StartSignalContract {
            schema: SIGNAL_SCHEMA,
            schema_version: SCHEMA_VERSION,
            publish_atomically: true,
            required_process_id: process_id,
            required_presentmon_target_process_id: process_id,
            required_presentmon_tool_version: PRESENTMON_VERSION,
            required_presentmon_tool_sha256: PRESENTMON_SHA256,
            qpc_boundary_required: true,
        },
        sources: sources(&runtime.configuration),
        governed_scene: scene.snapshot(),
        pipeline_warmup: pipeline,
        quality: &scene.quality,
        scenery: &scene.scenery,
        gpu,
        controller: ReadyControllerEvidence {
            profile_hash: runtime.configuration.player_profile_hash.clone(),
            fixed_tick: snapshot.fixed_tick,
            player_position_m: snapshot.player_position.to_array(),
            binding: binding_evidence(binding)?,
            camera_marker_verified: true,
        },
        qualification: QualificationBoundary {
            display_stutter_qualified: false,
            human_review_performed: false,
            gate1_route_qualified: false,
        },
    };
    write_json_atomic(&runtime.configuration.output_dir.join(READY_FILE), &ledger)?;
    tracing::info!(
        target: "forge::proof",
        schema = "forge.runtime-event",
        schema_version = 1_u64,
        event_kind = "gate1_gameplay_stutter_ready",
        process_id = u64::from(process_id),
        ready_file = %runtime.configuration.output_dir.join(READY_FILE).display(),
        "gameplay stutter proof ready for PresentMon attachment"
    );
    Ok(())
}

#[allow(
    clippy::too_many_lines,
    reason = "the final serialized proof ledger keeps all independent observations together"
)]
fn write_ledger(
    runtime: &Runtime,
    pipeline: &PipelineWarmupObservation,
    scene: &GovernedScene,
    gpu: &GpuDiagnostic,
) -> Result<(), String> {
    let signal = runtime
        .signal
        .as_ref()
        .ok_or("accepted PresentMon start signal is absent")?;
    let binding = runtime
        .initial_binding
        .ok_or("initial player binding is absent")?;
    let start = runtime.start_position.ok_or("movement start is absent")?;
    let forward = runtime
        .forward_endpoint
        .ok_or("forward endpoint is absent")?;
    let end = runtime.end_position.ok_or("movement end is absent")?;
    let stopped = runtime
        .stopped_position
        .ok_or("stopped position is absent")?;
    let forward_distance_m = start.xz().distance(forward.xz());
    let reverse_distance_m = forward.xz().distance(end.xz());
    let return_distance_m = start.xz().distance(end.xz());
    let observed_wall_duration_seconds = runtime
        .measurement_elapsed_seconds
        .ok_or("measurement wall duration is absent")?;
    validate_final_movement(
        runtime,
        start,
        forward,
        end,
        forward_distance_m,
        reverse_distance_m,
        return_distance_m,
        observed_wall_duration_seconds,
    )?;
    let initial_pipeline = runtime
        .initial_pipeline
        .clone()
        .ok_or("initial pipeline observation is absent")?;
    let initial_scene = runtime
        .initial_scene
        .clone()
        .ok_or("initial governed scene observation is absent")?;
    let initial_quality = runtime
        .initial_quality
        .clone()
        .ok_or("initial quality observation is absent")?;
    let initial_scenery = runtime
        .initial_scenery
        .clone()
        .ok_or("initial scenery observation is absent")?;
    let initial_gpu = runtime
        .initial_gpu
        .clone()
        .ok_or("initial GPU observation is absent")?;
    let ledger = GameplayStutterLedger {
        schema: LEDGER_SCHEMA,
        schema_version: SCHEMA_VERSION,
        status: "controller_window_complete_pending_presentmon_analysis",
        process_id: std::process::id(),
        build_source_commit: embedded_source_commit()?,
        build_profile: embedded_build_profile()?,
        watchdog_seconds: WATCHDOG.as_secs(),
        protocol: protocol(),
        sources: sources(&runtime.configuration),
        ready: ReadyEvidence {
            ready_file_written: runtime.ready_written,
            start_signal_file: START_SIGNAL_FILE,
            signal_schema: signal.schema.clone(),
            signal_schema_version: signal.schema_version,
            signal_process_id: signal.process_id,
            observed_after_startup_ms: runtime
                .signal_observed_after_startup_ms
                .ok_or("start signal observation time is absent")?,
        },
        presentmon: PresentMonEvidence {
            required: true,
            host_declared_capture_started: signal.presentmon.capture_started,
            target_process_id: signal.presentmon.target_process_id,
            session_id: signal.presentmon.session_id.clone(),
            tool_version: signal.presentmon.tool_version.clone(),
            tool_sha256: signal.presentmon.tool_sha256.clone(),
            last_pre_signal_qpc: signal.presentmon.last_pre_signal_qpc,
            measurement_not_before_qpc: signal.presentmon.measurement_not_before_qpc,
            qpc_frequency_hz: signal.presentmon.qpc_frequency_hz,
            pre_signal_observed_rows: signal.presentmon.pre_signal_observed_rows,
            runtime_verified_capture: false,
            display_metrics_ingested: false,
        },
        governed_scene: GovernedSceneStability {
            qualified_before_measurement: true,
            qualified_for_entire_measurement: true,
            structural_identity_stable: true,
            final_render_boundary_validated: runtime.final_render_validation_completed,
            water_animation_advanced_each_render_frame: runtime.water_animation_advancement_frames
                == runtime.render_invariant_checks,
            water_animation_advancement_frames: runtime.water_animation_advancement_frames,
            initial_observation: initial_scene,
            final_observation: scene.snapshot(),
        },
        pipeline_warmup: PipelineStability {
            ready_before_measurement: true,
            ready_for_entire_measurement: true,
            initial_observation: initial_pipeline,
            final_observation: pipeline.clone(),
        },
        quality: QualityStability {
            fixed_high_before_measurement: true,
            fixed_high_for_entire_measurement: true,
            initial_observation: initial_quality,
            final_observation: scene.quality.clone(),
        },
        scenery: SceneryStability {
            nine_of_nine_before_measurement: true,
            nine_of_nine_for_entire_measurement: true,
            no_fallback_before_measurement: true,
            no_fallback_for_entire_measurement: true,
            initial_observation: initial_scenery,
            final_observation: scene.scenery.clone(),
        },
        gpu: GpuStability {
            present_before_measurement: true,
            identity_stable: true,
            initial_observation: initial_gpu,
            final_observation: gpu.clone(),
        },
        movement: MovementEvidence {
            kind: "uninterrupted_fixed_tick_gameplay_controller",
            forward_ticks: runtime.forward_ticks,
            reverse_ticks: runtime.reverse_ticks,
            total_fixed_ticks: runtime.forward_ticks.saturating_add(runtime.reverse_ticks),
            nominal_duration_seconds: 30,
            observed_wall_duration_seconds,
            first_fixed_tick: runtime
                .first_fixed_tick
                .ok_or("first fixed tick is absent")?,
            midpoint_fixed_tick: runtime
                .midpoint_fixed_tick
                .ok_or("midpoint fixed tick is absent")?,
            last_fixed_tick: runtime.last_fixed_tick.ok_or("last fixed tick is absent")?,
            first_render_frame: runtime
                .first_render_frame
                .ok_or("first render frame is absent")?,
            last_render_frame: runtime
                .last_render_frame
                .ok_or("last render frame is absent")?,
            render_frames_spanned: runtime.render_frames_spanned,
            render_invariant_checks: runtime.render_invariant_checks,
            catch_up_fixed_ticks: runtime.catch_up_fixed_ticks,
            start_position_m: start.to_array(),
            forward_endpoint_m: forward.to_array(),
            end_position_m: end.to_array(),
            stopped_position_m: stopped.to_array(),
            forward_distance_m,
            reverse_distance_m,
            return_distance_m,
            forward_command_sequence: runtime
                .forward_sequence
                .ok_or("forward command sequence is absent")?,
            reverse_command_sequence: runtime
                .reverse_sequence
                .ok_or("reverse command sequence is absent")?,
            stop_command_sequence: runtime
                .stop_sequence
                .ok_or("stop command sequence is absent")?,
            stop_wait_ticks: runtime.stop_wait_ticks,
            stop_stable_ticks: runtime.stop_stable_ticks,
            grounded_ticks: runtime.grounded_ticks,
            collision_ticks: runtime.collision_ticks,
            maximum_ungrounded_run: runtime.max_ungrounded_run,
            profile_horizontal_step_limit_m: runtime.configuration.maximum_horizontal_step_m,
            maximum_horizontal_step_m: runtime.max_horizontal_step_m,
            maximum_controller_translation_error_m: runtime.max_controller_translation_error_m,
            binding: binding_evidence(binding)?,
            camera_marker_verified: true,
            rapier_bindings_stable: true,
            virtual_time_unpaused: true,
            virtual_time_scale: 1.0,
            screenshots_observed: false,
        },
        qualification: LedgerQualification {
            real_gameplay_controller_exercised: true,
            display_stutter_qualified: false,
            presentmon_analysis_completed: false,
            human_review_performed: false,
            gate1_route_qualified: false,
            gate1_qualified: false,
            limitation: "runtime proves an uninterrupted controller/Rapier/camera window but cannot verify or ingest the external PresentMon display trace",
        },
    };
    write_json_atomic(&runtime.configuration.output_dir.join(LEDGER_FILE), &ledger)
}

#[allow(clippy::too_many_arguments)]
fn validate_final_movement(
    runtime: &Runtime,
    start: Vec3,
    forward: Vec3,
    end: Vec3,
    forward_distance_m: f32,
    reverse_distance_m: f32,
    return_distance_m: f32,
    observed_wall_duration_seconds: f64,
) -> Result<(), String> {
    if runtime.forward_ticks != FORWARD_TICKS
        || runtime.reverse_ticks != REVERSE_TICKS
        || runtime.forward_ticks.saturating_add(runtime.reverse_ticks) != TOTAL_MOVEMENT_TICKS
    {
        return Err("movement did not preserve the exact 900+900 fixed-tick contract".to_owned());
    }
    if start.z <= 0.0 || forward.z >= 0.0 || end.z <= 0.0 {
        return Err(format!(
            "movement did not cross and recross the central seam: start={start:?}, forward={forward:?}, end={end:?}"
        ));
    }
    if forward_distance_m < MIN_LEG_DISTANCE_M
        || reverse_distance_m < MIN_LEG_DISTANCE_M
        || return_distance_m > MAX_RETURN_DISTANCE_M
        || (forward.x - start.x).abs() > MAX_LATERAL_DRIFT_M
        || (end.x - start.x).abs() > MAX_LATERAL_DRIFT_M
    {
        let forward_lateral_drift_m = (forward.x - start.x).abs();
        let end_lateral_drift_m = (end.x - start.x).abs();
        return Err(format!(
            "movement path is outside its governed bounds: forward={forward_distance_m}, reverse={reverse_distance_m}, return={return_distance_m}, forward_lateral={forward_lateral_drift_m}, end_lateral={end_lateral_drift_m}, start={start:?}, forward_endpoint={forward:?}, end={end:?}"
        ));
    }
    if runtime.max_ungrounded_run > MAX_UNGROUNDED_RUN
        || runtime.grounded_ticks != TOTAL_MOVEMENT_TICKS
        || runtime.collision_ticks == 0
    {
        return Err(format!(
            "movement lost terrain continuity: grounded={}/{TOTAL_MOVEMENT_TICKS}, nonempty_collision_observation_ticks={}, max_ungrounded={}",
            runtime.grounded_ticks, runtime.collision_ticks, runtime.max_ungrounded_run
        ));
    }
    if runtime.max_horizontal_step_m > runtime.configuration.maximum_horizontal_step_m
        || runtime.max_controller_translation_error_m > MAX_CONTROLLER_TRANSLATION_ERROR_M
    {
        return Err(format!(
            "movement escaped the controller/KCC bounds: observed_step={}, profile_step_limit={}, translation_error={}",
            runtime.max_horizontal_step_m,
            runtime.configuration.maximum_horizontal_step_m,
            runtime.max_controller_translation_error_m
        ));
    }
    if !(29.5..=30.5).contains(&observed_wall_duration_seconds) {
        return Err(format!(
            "movement wall duration {observed_wall_duration_seconds} s is outside the governed 30-second window"
        ));
    }
    if runtime.render_frames_spanned == 0 || runtime.render_invariant_checks == 0 {
        return Err("movement window observed no validated rendered frame".to_owned());
    }
    if runtime.water_animation_advancement_frames != runtime.render_invariant_checks {
        return Err(format!(
            "water animation advanced on {}/{} validated rendered frames",
            runtime.water_animation_advancement_frames, runtime.render_invariant_checks
        ));
    }
    if runtime.render_measurement_active
        || runtime.final_render_validation_pending
        || !runtime.final_render_validation_completed
    {
        return Err("the final rendered measurement boundary was not validated".to_owned());
    }
    Ok(())
}

fn protocol() -> ProtocolEvidence {
    ProtocolEvidence {
        physical_resolution_px: [1920, 1080],
        present_mode: "auto_no_vsync",
        vsync_requested: false,
        fixed_hz: 60,
        forward_ticks: FORWARD_TICKS,
        reverse_ticks: REVERSE_TICKS,
        nominal_measurement_seconds: 30,
        no_pause: true,
        screenshots_requested: false,
        presentmon_required: true,
    }
}

fn file_contract() -> FileContract {
    FileContract {
        ready: READY_FILE,
        start_signal: START_SIGNAL_FILE,
        ledger: LEDGER_FILE,
        failure: FAILURE_FILE,
    }
}

fn sources(configuration: &Configuration) -> SourceEvidence {
    SourceEvidence {
        terrain_hash: configuration.terrain_hash.clone(),
        player_profile_hash: configuration.player_profile_hash.clone(),
        cadle_assets_used: false,
    }
}

fn binding_evidence(binding: PlayerRuntimeBindingSnapshot) -> Result<BindingEvidence, String> {
    let (collider_index, collider_generation) = binding
        .rapier_collider_handle
        .ok_or("Rapier collider handle is absent")?;
    let context = binding
        .rapier_context_entity
        .ok_or("Rapier context entity is absent")?;
    Ok(BindingEvidence {
        player_entity: format!("{:?}", binding.player_entity),
        camera_entity: format!("{:?}", binding.camera_entity),
        mesh_asset: format!("{:?}", binding.mesh_asset_id),
        rapier_collider_handle: [collider_index, collider_generation],
        rapier_context_entity: format!("{context:?}"),
        backend_identity_stable: binding.backend_identity_stable,
    })
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
            "Gate 1 gameplay stutter acquisition requires Cargo profile `release`, observed `{value}`"
        ));
    }
    Ok(value)
}

fn write_json_atomic(path: &Path, value: &impl Serialize) -> Result<(), String> {
    if path.exists() {
        return Err(format!(
            "refusing to replace existing proof file {}",
            path.display()
        ));
    }
    let file_name = path
        .file_name()
        .and_then(|name| name.to_str())
        .ok_or_else(|| format!("proof path has no UTF-8 filename: {}", path.display()))?;
    let temporary = path.with_file_name(format!(".{file_name}.tmp"));
    if temporary.exists() {
        return Err(format!(
            "refusing to replace stale temporary proof file {}",
            temporary.display()
        ));
    }
    let json = serde_json::to_vec_pretty(value)
        .map_err(|error| format!("cannot serialize proof JSON: {error}"))?;
    fs::write(&temporary, json)
        .map_err(|error| format!("cannot write {}: {error}", temporary.display()))?;
    fs::rename(&temporary, path).map_err(|error| {
        format!(
            "cannot atomically publish {} as {}: {error}",
            temporary.display(),
            path.display()
        )
    })
}

fn enter_phase(runtime: &mut Runtime, phase: Phase) {
    runtime.phase = phase;
    runtime.phase_ticks = 0;
}

fn fail(runtime: &mut Runtime, exit: &mut MessageWriter<AppExit>, reason: &str) {
    if runtime.phase.is_terminal() {
        return;
    }
    let phase = runtime.phase;
    let failure = runtime.configuration.output_dir.join(FAILURE_FILE);
    let _ = fs::write(
        &failure,
        format!(
            "schema=forge.gate1-gameplay-stutter-failure\nschema_version=1\nphase={}\nreason={reason}\n",
            phase.label()
        ),
    );
    tracing::error!(
        target: "forge::proof",
        schema = "forge.runtime-event",
        schema_version = 1_u64,
        event_kind = "gate1_gameplay_stutter_failed",
        phase = phase.label(),
        error = reason,
        failure_file = %failure.display(),
        "gameplay stutter proof failed"
    );
    runtime.phase = Phase::Failed;
    exit.write(AppExit::error());
}

#[cfg(test)]
mod tests {
    use super::*;

    fn test_runtime() -> Runtime {
        Runtime::new(Configuration {
            output_dir: PathBuf::from("unused-test-output"),
            terrain_hash: "terrain".to_owned(),
            player_profile_hash: "player".to_owned(),
            player_spawn_position: Vec3::ZERO,
            maximum_horizontal_step_m: 0.1,
        })
    }

    fn valid_signal(process_id: u32) -> StartSignal {
        StartSignal {
            schema: SIGNAL_SCHEMA.to_owned(),
            schema_version: SCHEMA_VERSION,
            process_id,
            presentmon: PresentMonSignal {
                capture_started: true,
                target_process_id: process_id,
                session_id: "ForgeGate1_123".to_owned(),
                tool_version: PRESENTMON_VERSION.to_owned(),
                tool_sha256: PRESENTMON_SHA256.to_owned(),
                last_pre_signal_qpc: 123_000,
                measurement_not_before_qpc: 123_456,
                qpc_frequency_hz: 10_000_000,
                pre_signal_observed_rows: 3,
            },
        }
    }

    #[test]
    fn signal_contract_binds_pid_session_and_pinned_presentmon() {
        let signal = valid_signal(42);
        validate_start_signal(&signal, 42).unwrap();

        let mut wrong_pid = signal.clone();
        wrong_pid.presentmon.target_process_id = 43;
        assert!(validate_start_signal(&wrong_pid, 42).is_err());

        let mut wrong_hash = signal.clone();
        wrong_hash.presentmon.tool_sha256 = "0".repeat(64);
        assert!(validate_start_signal(&wrong_hash, 42).is_err());

        let mut missing_qpc_boundary = signal.clone();
        missing_qpc_boundary.presentmon.measurement_not_before_qpc = 0;
        assert!(validate_start_signal(&missing_qpc_boundary, 42).is_err());

        let mut unordered_qpc_boundary = signal.clone();
        unordered_qpc_boundary.presentmon.last_pre_signal_qpc = 123_457;
        assert!(validate_start_signal(&unordered_qpc_boundary, 42).is_err());

        let mut unsafe_session = signal;
        unsafe_session.presentmon.session_id = "bad session".to_owned();
        assert!(validate_start_signal(&unsafe_session, 42).is_err());
    }

    #[test]
    fn protocol_is_an_exact_unpaused_thirty_second_window() {
        let protocol = protocol();
        assert_eq!(protocol.physical_resolution_px, [1920, 1080]);
        assert_eq!(protocol.fixed_hz, 60);
        assert_eq!(protocol.forward_ticks + protocol.reverse_ticks, 1_800);
        assert_eq!(protocol.nominal_measurement_seconds, 30);
        assert!(protocol.no_pause);
        assert!(!protocol.screenshots_requested);
        assert!(protocol.presentmon_required);
    }

    #[test]
    fn sequence_wait_does_not_claim_application_before_the_snapshot_confirms_it() {
        let mut snapshot = PlayerRuntimeSnapshot {
            last_applied_sequence: Some(6),
            ..default()
        };

        assert!(!require_sequence(&snapshot, 7, 1, "forward").unwrap());
        assert!(!require_sequence(&snapshot, 7, 4, "forward").unwrap());
        assert!(require_sequence(&snapshot, 7, 5, "forward").is_err());

        snapshot.last_applied_sequence = Some(7);
        assert!(require_sequence(&snapshot, 7, 1, "forward").unwrap());

        snapshot.last_applied_sequence = Some(8);
        assert!(require_sequence(&snapshot, 7, 1, "forward").is_err());
    }

    #[test]
    fn movement_tracks_render_span_separately_from_fixed_tick_catch_up() {
        let mut runtime = test_runtime();
        let mut snapshot = PlayerRuntimeSnapshot {
            fixed_tick: 100,
            grounded: true,
            collisions: 1,
            ..default()
        };

        sample_movement(&mut runtime, &snapshot, 10, true).unwrap();
        snapshot.fixed_tick += 1;
        sample_movement(&mut runtime, &snapshot, 10, true).unwrap();
        snapshot.fixed_tick += 1;
        sample_movement(&mut runtime, &snapshot, 13, true).unwrap();

        assert_eq!(runtime.first_render_frame, Some(10));
        assert_eq!(runtime.last_render_frame, Some(13));
        assert_eq!(runtime.render_frames_spanned, 4);
        assert_eq!(runtime.catch_up_fixed_ticks, 1);
    }

    #[test]
    fn movement_rejects_a_single_tick_teleport() {
        let mut runtime = test_runtime();
        let mut snapshot = PlayerRuntimeSnapshot {
            fixed_tick: 100,
            grounded: true,
            collisions: 1,
            ..default()
        };
        sample_movement(&mut runtime, &snapshot, 10, true).unwrap();

        snapshot.fixed_tick += 1;
        snapshot.player_position.x = runtime.configuration.maximum_horizontal_step_m + 0.01;
        snapshot.effective_translation.x = snapshot.player_position.x;
        assert!(sample_movement(&mut runtime, &snapshot, 11, true).is_err());
    }

    #[test]
    fn movement_rejects_translation_not_produced_by_the_controller() {
        let mut runtime = test_runtime();
        let mut snapshot = PlayerRuntimeSnapshot {
            fixed_tick: 100,
            grounded: true,
            collisions: 1,
            ..default()
        };
        sample_movement(&mut runtime, &snapshot, 10, true).unwrap();

        snapshot.fixed_tick += 1;
        snapshot.player_position.x = 0.05;
        snapshot.effective_translation = Vec3::ZERO;
        assert!(sample_movement(&mut runtime, &snapshot, 11, true).is_err());
    }

    #[test]
    fn rendered_measurement_requires_water_animation_to_advance() {
        let mut runtime = test_runtime();
        runtime.last_water_animation_updates = Some(10);

        observe_water_animation(&mut runtime, 11).unwrap();
        assert_eq!(runtime.water_animation_advancement_frames, 1);
        assert!(observe_water_animation(&mut runtime, 11).is_err());
        assert!(observe_water_animation(&mut runtime, 9).is_err());
    }

    #[test]
    fn final_movement_accepts_measured_terrain_drift_but_keeps_lateral_bound() {
        let mut runtime = test_runtime();
        runtime.forward_ticks = FORWARD_TICKS;
        runtime.reverse_ticks = REVERSE_TICKS;
        runtime.grounded_ticks = TOTAL_MOVEMENT_TICKS;
        runtime.collision_ticks = 1;
        runtime.max_horizontal_step_m = 0.08;
        runtime.render_frames_spanned = 1;
        runtime.render_invariant_checks = 1;
        runtime.water_animation_advancement_frames = 1;
        runtime.final_render_validation_completed = true;

        let start = Vec3::new(16.0, -24.30, 8.0);
        let forward = Vec3::new(17.34, -10.18, -61.51);
        let end = Vec3::new(17.34, -25.10, 11.55);
        validate_final_movement(
            &runtime,
            start,
            forward,
            end,
            start.xz().distance(forward.xz()),
            forward.xz().distance(end.xz()),
            start.xz().distance(end.xz()),
            30.0,
        )
        .unwrap();

        let excessive_lateral_drift =
            Vec3::new(start.x + MAX_LATERAL_DRIFT_M + 0.01, forward.y, forward.z);
        assert!(
            validate_final_movement(
                &runtime,
                start,
                excessive_lateral_drift,
                end,
                start.xz().distance(excessive_lateral_drift.xz()),
                excessive_lateral_drift.xz().distance(end.xz()),
                start.xz().distance(end.xz()),
                30.0,
            )
            .is_err()
        );

        runtime.collision_ticks = 0;
        assert!(
            validate_final_movement(
                &runtime,
                start,
                forward,
                end,
                start.xz().distance(forward.xz()),
                forward.xz().distance(end.xz()),
                start.xz().distance(end.xz()),
                30.0,
            )
            .is_err()
        );
    }
}
