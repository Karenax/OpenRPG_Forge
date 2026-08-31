use bevy::{app::AppExit, prelude::*};
use bevy_rapier3d::prelude::Collider;
use forge_core::ContentHash;
use forge_engine::{
    ForgePlayerCamera, ForgePlayerCharacter, PendingPlayerControlCommand, PlayerCameraMode,
    PlayerControlCommand, PlayerControllerSet, PlayerRuntimeBindingSnapshot, PlayerRuntimeBindings,
    PlayerRuntimeSnapshot, TerrainRuntimeBindingSnapshot, TerrainRuntimeBindings,
    TerrainRuntimeState, VegetationInteractionField,
};
use forge_project::{PlayerControllerProfileDocument, TerrainDocument};
use forge_world::TerrainGenerationPlan;
use std::time::Instant;
use tracing::{error, info};

const EXPECTED_PLAYER_PROFILE_HASH: &str =
    "941eceefb29835c0949a9801f83f421839bcb5b0cd79583899c22d6832a74f90";
const PROBE_START_X_M: f32 = 16.0;
const PROBE_START_Z_M: f32 = 8.0;
const PROBE_SPAWN_CLEARANCE_M: f32 = 0.02;
const PROBE_WARMUP_MIN_FIXED_TICKS: u64 = 8;
const PROBE_WARMUP_MAX_FIXED_TICKS: u64 = 120;
const PROBE_WALK_FIXED_TICKS: u32 = 150;
const PROBE_MAX_WALK_UNGROUNDED_RUN: u32 = 4;
const PROBE_STOP_MAX_FIXED_TICKS: u32 = 45;
const PROBE_JUMP_MAX_FIXED_TICKS: u32 = 120;
const PROBE_CLEAR_CAMERA_MAX_FIXED_TICKS: u32 = 90;
const PROBE_OCCLUSION_MAX_FIXED_TICKS: u32 = 30;
const PROBE_RECOVERY_MAX_FIXED_TICKS: u32 = 90;
const PROBE_MODE_MAX_FIXED_TICKS: u32 = 12;
const PROBE_TIMEOUT_SECONDS: u64 = 30;
const CAMERA_WALL_DISTANCE_M: f32 = 2.2;
const CAMERA_WALL_HALF_WIDTH_M: f32 = 3.0;
const CAMERA_WALL_HALF_HEIGHT_M: f32 = 2.0;
const CAMERA_WALL_HALF_THICKNESS_M: f32 = 0.12;
const POSITION_EPSILON_M: f32 = 0.03;
const ROTATION_EPSILON: f32 = 1.0e-4;
const FOV_EPSILON_RADIANS: f32 = 1.0e-5;
const VEGETATION_INTERACTION_EPSILON_M: f32 = 1.0e-4;

#[derive(Clone, Resource)]
struct PlayerCameraProbeConfiguration {
    profile: PlayerControllerProfileDocument,
    profile_content_hash: ContentHash,
    terrain_document_hash: ContentHash,
    spawn_position: Vec3,
}

pub(crate) struct PlayerCameraProbePlugin {
    configuration: PlayerCameraProbeConfiguration,
}

impl PlayerCameraProbePlugin {
    pub(crate) fn new(
        terrain: &TerrainDocument,
        profile: &PlayerControllerProfileDocument,
    ) -> Result<Self, String> {
        profile
            .validate()
            .map_err(|error| format!("invalid player-camera probe profile: {error}"))?;
        let profile_content_hash = profile
            .content_hash()
            .map_err(|error| format!("cannot hash player-camera probe profile: {error}"))?;
        if profile_content_hash.to_string() != EXPECTED_PLAYER_PROFILE_HASH {
            return Err(format!(
                "embedded player-camera profile hash drifted: expected {EXPECTED_PLAYER_PROFILE_HASH}, got {profile_content_hash}"
            ));
        }
        let terrain_document_hash = terrain
            .content_hash()
            .map_err(|error| format!("cannot hash player-camera probe terrain: {error}"))?;
        let surface_height = TerrainGenerationPlan::from_document(terrain)
            .map_err(|error| format!("cannot plan player-camera probe terrain: {error}"))?
            .height_at(PROBE_START_X_M, PROBE_START_Z_M)
            .map_err(|error| format!("cannot sample player-camera probe spawn: {error}"))?;
        let capsule_half_height =
            profile.controller.capsule_radius_m + profile.controller.capsule_half_segment_m;
        let spawn_position = Vec3::new(
            PROBE_START_X_M,
            surface_height + capsule_half_height + PROBE_SPAWN_CLEARANCE_M,
            PROBE_START_Z_M,
        );

        Ok(Self {
            configuration: PlayerCameraProbeConfiguration {
                profile: profile.clone(),
                profile_content_hash,
                terrain_document_hash,
                spawn_position,
            },
        })
    }

    #[must_use]
    pub(crate) fn spawn_position(&self) -> Vec3 {
        self.configuration.spawn_position
    }
}

impl Plugin for PlayerCameraProbePlugin {
    fn build(&self, app: &mut App) {
        app.insert_resource(self.configuration.clone())
            .insert_resource(PlayerCameraProbeState::new())
            .add_systems(
                FixedUpdate,
                drive_player_camera_probe.before(PlayerControllerSet::Drive),
            )
            .add_systems(
                FixedUpdate,
                observe_player_camera_probe.after(PlayerControllerSet::Camera),
            )
            .add_systems(Update, watchdog_player_camera_probe);
    }
}

#[derive(Clone, Copy, Debug, Eq, PartialEq)]
enum PlayerCameraProbePhase {
    WarmingUp,
    Walking,
    Stopping,
    Jumping,
    ClearingCamera,
    Occluding,
    Recovering,
    FirstPerson,
    ThirdPerson,
    Completed,
    Failed,
}

impl PlayerCameraProbePhase {
    const fn label(self) -> &'static str {
        match self {
            Self::WarmingUp => "warming_up",
            Self::Walking => "walking",
            Self::Stopping => "stopping",
            Self::Jumping => "jumping",
            Self::ClearingCamera => "clearing_camera",
            Self::Occluding => "occluding",
            Self::Recovering => "recovering",
            Self::FirstPerson => "first_person",
            Self::ThirdPerson => "third_person",
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
    reason = "the bounded probe records independent assertions; the phase enum remains its state machine"
)]
struct PlayerCameraProbeState {
    started_at: Instant,
    phase: PlayerCameraProbePhase,
    fixed_ticks: u64,
    phase_ticks: u32,
    initial_player_bindings: Option<PlayerRuntimeBindingSnapshot>,
    initial_terrain_bindings: Option<Vec<TerrainRuntimeBindingSnapshot>>,
    walk_sequence: Option<u64>,
    walk_sequence_applied: bool,
    walk_start_position: Option<Vec3>,
    walk_end_position: Option<Vec3>,
    walk_observed_ticks: u32,
    walk_grounded_ticks: u32,
    walk_current_ungrounded_run: u32,
    walk_max_ungrounded_run: u32,
    stop_sequence: Option<u64>,
    stop_sequence_applied: bool,
    stop_stable_ticks: u32,
    jump_sequence: Option<u64>,
    jump_sequence_applied: bool,
    jump_start_y_m: f32,
    jump_apex_y_m: f32,
    jump_airborne_ticks: u32,
    jump_saw_upward_velocity: bool,
    wall_entity: Option<Entity>,
    wall_removed: bool,
    unoccluded_boom_distance_m: f32,
    occluded_boom_distance_m: Option<f32>,
    recovery_previous_distance_m: Option<f32>,
    recovery_samples: u32,
    first_person_sequence: Option<u64>,
    first_person_sequence_applied: bool,
    first_person_proven: bool,
    third_person_sequence: Option<u64>,
    third_person_sequence_applied: bool,
    third_person_proven: bool,
    vegetation_interaction_available: bool,
    vegetation_interaction_walk_samples: u32,
    vegetation_interaction_velocity_aligned_samples: u32,
    vegetation_interaction_max_center_error_m: f32,
}

impl PlayerCameraProbeState {
    fn new() -> Self {
        Self {
            started_at: Instant::now(),
            phase: PlayerCameraProbePhase::WarmingUp,
            fixed_ticks: 0,
            phase_ticks: 0,
            initial_player_bindings: None,
            initial_terrain_bindings: None,
            walk_sequence: None,
            walk_sequence_applied: false,
            walk_start_position: None,
            walk_end_position: None,
            walk_observed_ticks: 0,
            walk_grounded_ticks: 0,
            walk_current_ungrounded_run: 0,
            walk_max_ungrounded_run: 0,
            stop_sequence: None,
            stop_sequence_applied: false,
            stop_stable_ticks: 0,
            jump_sequence: None,
            jump_sequence_applied: false,
            jump_start_y_m: 0.0,
            jump_apex_y_m: f32::NEG_INFINITY,
            jump_airborne_ticks: 0,
            jump_saw_upward_velocity: false,
            wall_entity: None,
            wall_removed: false,
            unoccluded_boom_distance_m: 0.0,
            occluded_boom_distance_m: None,
            recovery_previous_distance_m: None,
            recovery_samples: 0,
            first_person_sequence: None,
            first_person_sequence_applied: false,
            first_person_proven: false,
            third_person_sequence: None,
            third_person_sequence_applied: false,
            third_person_proven: false,
            vegetation_interaction_available: false,
            vegetation_interaction_walk_samples: 0,
            vegetation_interaction_velocity_aligned_samples: 0,
            vegetation_interaction_max_center_error_m: 0.0,
        }
    }
}

#[derive(Component)]
struct PlayerCameraProbeWall;

#[allow(
    clippy::needless_pass_by_value,
    reason = "Bevy systems require owned system parameter wrappers"
)]
fn drive_player_camera_probe(
    mut commands: Commands,
    configuration: Res<PlayerCameraProbeConfiguration>,
    snapshot: Res<PlayerRuntimeSnapshot>,
    mut pending: ResMut<PendingPlayerControlCommand>,
    mut probe: ResMut<PlayerCameraProbeState>,
    mut exit: MessageWriter<AppExit>,
) {
    if probe.phase.is_terminal() {
        return;
    }

    let result = match probe.phase {
        PlayerCameraProbePhase::Walking if probe.walk_sequence.is_none() => pending
            .submit(PlayerControlCommand {
                move_axis: Vec2::Y,
                ..default()
            })
            .map(|sequence| probe.walk_sequence = Some(sequence))
            .map_err(|error| format!("cannot submit player walk command: {error}")),
        PlayerCameraProbePhase::Stopping if probe.stop_sequence.is_none() => pending
            .submit(PlayerControlCommand::default())
            .map(|sequence| probe.stop_sequence = Some(sequence))
            .map_err(|error| format!("cannot submit player stop command: {error}")),
        PlayerCameraProbePhase::Jumping if probe.jump_sequence.is_none() => pending
            .submit(PlayerControlCommand {
                jump: true,
                ..default()
            })
            .map(|sequence| probe.jump_sequence = Some(sequence))
            .map_err(|error| format!("cannot submit player jump command: {error}")),
        PlayerCameraProbePhase::Occluding if probe.wall_entity.is_none() => {
            let wall = spawn_camera_probe_wall(&mut commands, &configuration, &snapshot);
            probe.wall_entity = Some(wall);
            Ok(())
        }
        PlayerCameraProbePhase::Recovering if !probe.wall_removed => {
            let result = probe.wall_entity.map_or_else(
                || Err("camera recovery started without a probe wall".to_owned()),
                |wall| {
                    commands.entity(wall).despawn();
                    Ok(())
                },
            );
            if result.is_ok() {
                probe.wall_removed = true;
            }
            result
        }
        PlayerCameraProbePhase::FirstPerson if probe.first_person_sequence.is_none() => pending
            .submit(PlayerControlCommand {
                toggle_camera: true,
                ..default()
            })
            .map(|sequence| probe.first_person_sequence = Some(sequence))
            .map_err(|error| format!("cannot submit first-person toggle: {error}")),
        PlayerCameraProbePhase::ThirdPerson if probe.third_person_sequence.is_none() => pending
            .submit(PlayerControlCommand {
                toggle_camera: true,
                ..default()
            })
            .map(|sequence| probe.third_person_sequence = Some(sequence))
            .map_err(|error| format!("cannot submit third-person toggle: {error}")),
        _ => Ok(()),
    };

    if let Err(reason) = result {
        let phase = probe.phase;
        fail_probe(probe.fixed_ticks, phase, &reason, &mut exit);
        probe.phase = PlayerCameraProbePhase::Failed;
    }
}

fn spawn_camera_probe_wall(
    commands: &mut Commands,
    configuration: &PlayerCameraProbeConfiguration,
    snapshot: &PlayerRuntimeSnapshot,
) -> Entity {
    let camera_profile = &configuration.profile.camera;
    let controller_profile = &configuration.profile.controller;
    let capsule_half_height =
        controller_profile.capsule_radius_m + controller_profile.capsule_half_segment_m;
    let pivot = snapshot.player_position - Vec3::Y * capsule_half_height
        + Vec3::Y * camera_profile.pivot_height_m;
    let view_rotation = Quat::from_rotation_y(snapshot.camera_yaw_radians)
        * Quat::from_rotation_x(snapshot.camera_pitch_radians);
    let backward = -(view_rotation * Vec3::NEG_Z);
    let wall_center = pivot + backward * CAMERA_WALL_DISTANCE_M;

    commands
        .spawn((
            Name::new("Player-camera probe occlusion wall"),
            PlayerCameraProbeWall,
            Collider::cuboid(
                CAMERA_WALL_HALF_WIDTH_M,
                CAMERA_WALL_HALF_HEIGHT_M,
                CAMERA_WALL_HALF_THICKNESS_M,
            ),
            Transform::from_translation(wall_center)
                .with_rotation(Quat::from_rotation_y(snapshot.camera_yaw_radians)),
        ))
        .id()
}

#[allow(
    clippy::needless_pass_by_value,
    clippy::too_many_arguments,
    reason = "Bevy systems require owned wrappers for the independent public resources verified after the camera set"
)]
fn observe_player_camera_probe(
    configuration: Res<PlayerCameraProbeConfiguration>,
    terrain_runtime: Res<TerrainRuntimeState>,
    terrain_bindings: Res<TerrainRuntimeBindings>,
    player_bindings: Res<PlayerRuntimeBindings>,
    snapshot: Res<PlayerRuntimeSnapshot>,
    vegetation_interaction: Option<Res<VegetationInteractionField>>,
    player_visibility: Query<&Visibility, With<ForgePlayerCharacter>>,
    camera: Query<(&Transform, &Projection), With<ForgePlayerCamera>>,
    mut probe: ResMut<PlayerCameraProbeState>,
    mut exit: MessageWriter<AppExit>,
) {
    if probe.phase.is_terminal() {
        return;
    }

    probe.fixed_ticks = probe.fixed_ticks.saturating_add(1);
    probe.phase_ticks = probe.phase_ticks.saturating_add(1);
    let result = advance_probe(
        &configuration,
        &terrain_runtime,
        &terrain_bindings,
        &player_bindings,
        &snapshot,
        vegetation_interaction.as_deref(),
        &player_visibility,
        &camera,
        &mut probe,
    );
    match result {
        Ok(true) => {
            probe.phase = PlayerCameraProbePhase::Completed;
            exit.write(AppExit::Success);
        }
        Ok(false) => {}
        Err(reason) => {
            let phase = probe.phase;
            fail_probe(probe.fixed_ticks, phase, &reason, &mut exit);
            probe.phase = PlayerCameraProbePhase::Failed;
        }
    }
}

fn watchdog_player_camera_probe(
    mut probe: ResMut<PlayerCameraProbeState>,
    mut exit: MessageWriter<AppExit>,
) {
    if probe.phase.is_terminal() || probe.started_at.elapsed().as_secs() < PROBE_TIMEOUT_SECONDS {
        return;
    }

    let phase = probe.phase;
    fail_probe(
        probe.fixed_ticks,
        phase,
        "player-camera probe exceeded its 30 second real-time watchdog",
        &mut exit,
    );
    probe.phase = PlayerCameraProbePhase::Failed;
}

#[allow(
    clippy::too_many_arguments,
    reason = "phase verification compares all public projections without mutating the player"
)]
fn advance_probe(
    configuration: &PlayerCameraProbeConfiguration,
    terrain_runtime: &TerrainRuntimeState,
    terrain_bindings: &TerrainRuntimeBindings,
    player_bindings: &PlayerRuntimeBindings,
    snapshot: &PlayerRuntimeSnapshot,
    vegetation_interaction: Option<&VegetationInteractionField>,
    player_visibility: &Query<&Visibility, With<ForgePlayerCharacter>>,
    camera: &Query<(&Transform, &Projection), With<ForgePlayerCamera>>,
    probe: &mut PlayerCameraProbeState,
) -> Result<bool, String> {
    observe_vegetation_interaction(vegetation_interaction, snapshot, probe)?;
    match probe.phase {
        PlayerCameraProbePhase::WarmingUp => {
            advance_warmup(
                configuration,
                terrain_runtime,
                terrain_bindings,
                player_bindings,
                snapshot,
                player_visibility,
                camera,
                probe,
            )?;
        }
        PlayerCameraProbePhase::Walking => advance_walk(snapshot, probe)?,
        PlayerCameraProbePhase::Stopping => advance_stop(snapshot, probe)?,
        PlayerCameraProbePhase::Jumping => advance_jump(snapshot, probe)?,
        PlayerCameraProbePhase::ClearingCamera => {
            advance_clear_camera(configuration, snapshot, probe)?;
        }
        PlayerCameraProbePhase::Occluding => {
            advance_occlusion(configuration, snapshot, probe)?;
        }
        PlayerCameraProbePhase::Recovering => {
            advance_recovery(configuration, snapshot, probe)?;
        }
        PlayerCameraProbePhase::FirstPerson => {
            advance_first_person(
                configuration,
                player_bindings,
                snapshot,
                player_visibility,
                camera,
                probe,
            )?;
        }
        PlayerCameraProbePhase::ThirdPerson => {
            if advance_third_person(
                configuration,
                terrain_runtime,
                terrain_bindings,
                player_bindings,
                snapshot,
                player_visibility,
                camera,
                probe,
            )? {
                return Ok(true);
            }
        }
        PlayerCameraProbePhase::Completed => return Ok(true),
        PlayerCameraProbePhase::Failed => {}
    }
    Ok(false)
}

fn observe_vegetation_interaction(
    interaction: Option<&VegetationInteractionField>,
    snapshot: &PlayerRuntimeSnapshot,
    probe: &mut PlayerCameraProbeState,
) -> Result<(), String> {
    let Some(interaction) = interaction else {
        return Ok(());
    };
    probe.vegetation_interaction_available = true;
    if probe.phase == PlayerCameraProbePhase::WarmingUp
        && interaction.radius_m == 0.0
        && interaction.strength == 0.0
    {
        return Ok(());
    }
    if !interaction.center_m.is_finite()
        || !interaction.push_direction_xz.is_finite()
        || !interaction.radius_m.is_finite()
        || !interaction.strength.is_finite()
        || interaction.radius_m <= 0.0
        || interaction.strength <= 0.0
    {
        return Err(format!(
            "vegetation interaction is not finite and active: {interaction:?}"
        ));
    }
    let center_error_m = interaction.center_m.distance(snapshot.player_position);
    probe.vegetation_interaction_max_center_error_m = probe
        .vegetation_interaction_max_center_error_m
        .max(center_error_m);
    if center_error_m > VEGETATION_INTERACTION_EPSILON_M {
        return Err(format!(
            "vegetation interaction center trails the post-physics player pose by {center_error_m} m"
        ));
    }

    if probe.phase == PlayerCameraProbePhase::Walking {
        let velocity = Vec2::new(
            snapshot.horizontal_velocity.x,
            snapshot.horizontal_velocity.z,
        );
        if let Some(direction) = velocity.try_normalize() {
            probe.vegetation_interaction_walk_samples =
                probe.vegetation_interaction_walk_samples.saturating_add(1);
            let interaction_direction =
                interaction
                    .push_direction_xz
                    .try_normalize()
                    .ok_or_else(|| {
                        "vegetation interaction direction is zero while walking".to_owned()
                    })?;
            if interaction_direction.dot(direction) >= 0.999 {
                probe.vegetation_interaction_velocity_aligned_samples = probe
                    .vegetation_interaction_velocity_aligned_samples
                    .saturating_add(1);
            } else {
                return Err(format!(
                    "vegetation interaction direction is not aligned with player velocity: interaction={interaction_direction:?}, velocity={direction:?}"
                ));
            }
        }
    }
    Ok(())
}

#[allow(
    clippy::too_many_arguments,
    reason = "warmup captures all immutable proof baselines at one fixed boundary"
)]
fn advance_warmup(
    configuration: &PlayerCameraProbeConfiguration,
    terrain_runtime: &TerrainRuntimeState,
    terrain_bindings: &TerrainRuntimeBindings,
    player_bindings: &PlayerRuntimeBindings,
    snapshot: &PlayerRuntimeSnapshot,
    player_visibility: &Query<&Visibility, With<ForgePlayerCharacter>>,
    camera: &Query<(&Transform, &Projection), With<ForgePlayerCamera>>,
    probe: &mut PlayerCameraProbeState,
) -> Result<(), String> {
    let terrain_hash = terrain_runtime
        .document()
        .content_hash()
        .map_err(|error| format!("cannot hash terrain during player warmup: {error}"))?;
    if terrain_hash != configuration.terrain_document_hash {
        return Err(format!(
            "player probe terrain hash mismatch: expected {}, got {terrain_hash}",
            configuration.terrain_document_hash
        ));
    }
    if let Some(actual) = snapshot.profile_content_hash
        && actual != configuration.profile_content_hash
    {
        return Err(format!(
            "player runtime profile hash mismatch: expected {}, got {actual}",
            configuration.profile_content_hash
        ));
    }

    let player_binding = player_bindings.snapshot();
    let terrain_binding_snapshot = terrain_bindings.snapshots().ok();
    let ready = probe.fixed_ticks >= PROBE_WARMUP_MIN_FIXED_TICKS
        && snapshot.profile_content_hash == Some(configuration.profile_content_hash)
        && snapshot.grounded
        && snapshot.camera_query_succeeded
        && player_binding.is_some_and(|binding| {
            binding.rapier_collider_handle.is_some()
                && binding.rapier_context_entity.is_some()
                && binding.backend_identity_stable
        })
        && terrain_binding_snapshot.is_some();
    if !ready {
        if probe.fixed_ticks >= PROBE_WARMUP_MAX_FIXED_TICKS {
            return Err(format!(
                "player probe warmup remained incomplete after {} fixed ticks (profile_hash={}, grounded={}, camera_query={}, player_bindings={}, terrain_bindings={})",
                probe.fixed_ticks,
                snapshot.profile_content_hash.is_some(),
                snapshot.grounded,
                snapshot.camera_query_succeeded,
                player_binding.is_some(),
                terrain_binding_snapshot.is_some()
            ));
        }
        return Ok(());
    }

    let player_binding = player_binding
        .ok_or_else(|| "player bindings disappeared at warmup boundary".to_owned())?;
    let terrain_binding_snapshot = terrain_binding_snapshot
        .ok_or_else(|| "terrain bindings disappeared at warmup boundary".to_owned())?;
    if !player_binding.backend_identity_stable {
        return Err("player backend identity changed during warmup".to_owned());
    }
    if snapshot.camera_mode != PlayerCameraMode::ThirdPerson {
        return Err("player probe did not start in third-person mode".to_owned());
    }
    if snapshot.player_position.z <= 0.0
        || (snapshot.player_position.x - PROBE_START_X_M).abs() > POSITION_EPSILON_M
        || (snapshot.player_position.z - PROBE_START_Z_M).abs() > POSITION_EPSILON_M
    {
        return Err(format!(
            "player probe must start inside the central positive-Z chunk at ({PROBE_START_X_M}, {PROBE_START_Z_M}); observed {:?}",
            snapshot.player_position
        ));
    }
    verify_third_person_pose(
        configuration,
        snapshot,
        player_binding,
        player_visibility,
        camera,
    )?;

    probe.initial_player_bindings = Some(player_binding);
    probe.initial_terrain_bindings = Some(terrain_binding_snapshot);
    probe.walk_start_position = Some(snapshot.player_position);
    enter_phase(probe, PlayerCameraProbePhase::Walking);
    Ok(())
}

fn advance_walk(
    snapshot: &PlayerRuntimeSnapshot,
    probe: &mut PlayerCameraProbeState,
) -> Result<(), String> {
    let sequence = probe
        .walk_sequence
        .ok_or_else(|| "walking phase has no submitted command".to_owned())?;
    if !probe.walk_sequence_applied {
        if !sequence_was_applied(snapshot, sequence, "walk")? {
            return phase_timeout(
                probe.phase_ticks,
                4,
                "walk command was not applied within four fixed ticks",
            );
        }
        probe.walk_sequence_applied = true;
    }

    probe.walk_observed_ticks = probe.walk_observed_ticks.saturating_add(1);
    if snapshot.grounded {
        probe.walk_grounded_ticks = probe.walk_grounded_ticks.saturating_add(1);
        probe.walk_current_ungrounded_run = 0;
    } else {
        probe.walk_current_ungrounded_run = probe.walk_current_ungrounded_run.saturating_add(1);
        probe.walk_max_ungrounded_run = probe
            .walk_max_ungrounded_run
            .max(probe.walk_current_ungrounded_run);
    }

    if probe.walk_observed_ticks < PROBE_WALK_FIXED_TICKS {
        return Ok(());
    }

    let start = probe
        .walk_start_position
        .ok_or_else(|| "walking phase lost its start position".to_owned())?;
    let end = snapshot.player_position;
    if start.z <= 0.0 || end.z >= 0.0 {
        return Err(format!(
            "player did not cross the central z=0 terrain seam: start_z={}, end_z={}",
            start.z, end.z
        ));
    }
    if end.z >= start.z - 8.0 {
        return Err(format!(
            "player walk covered too little forward distance: start_z={}, end_z={}",
            start.z, end.z
        ));
    }
    if (end.x - start.x).abs() > 0.75 {
        return Err(format!(
            "player drifted laterally while crossing the seam: start_x={}, end_x={}",
            start.x, end.x
        ));
    }
    if probe.walk_max_ungrounded_run > PROBE_MAX_WALK_UNGROUNDED_RUN || !snapshot.grounded {
        return Err(format!(
            "terrain seam crossing lost grounded continuity: grounded_ticks={}, observed_ticks={}, longest_ungrounded_run={}, final_grounded={}",
            probe.walk_grounded_ticks,
            probe.walk_observed_ticks,
            probe.walk_max_ungrounded_run,
            snapshot.grounded
        ));
    }

    probe.walk_end_position = Some(end);
    enter_phase(probe, PlayerCameraProbePhase::Stopping);
    Ok(())
}

fn advance_stop(
    snapshot: &PlayerRuntimeSnapshot,
    probe: &mut PlayerCameraProbeState,
) -> Result<(), String> {
    let sequence = probe
        .stop_sequence
        .ok_or_else(|| "stopping phase has no submitted command".to_owned())?;
    if !probe.stop_sequence_applied {
        if !sequence_was_applied(snapshot, sequence, "stop")? {
            return phase_timeout(
                probe.phase_ticks,
                4,
                "stop command was not applied within four fixed ticks",
            );
        }
        probe.stop_sequence_applied = true;
    }

    if snapshot.horizontal_velocity.length() <= 0.05 && snapshot.grounded {
        probe.stop_stable_ticks = probe.stop_stable_ticks.saturating_add(1);
    } else {
        probe.stop_stable_ticks = 0;
    }
    if probe.stop_stable_ticks >= 2 {
        probe.jump_start_y_m = snapshot.player_position.y;
        probe.jump_apex_y_m = snapshot.player_position.y;
        enter_phase(probe, PlayerCameraProbePhase::Jumping);
        return Ok(());
    }
    phase_timeout(
        probe.phase_ticks,
        PROBE_STOP_MAX_FIXED_TICKS,
        "player did not stop while grounded within the bounded braking window",
    )
}

fn advance_jump(
    snapshot: &PlayerRuntimeSnapshot,
    probe: &mut PlayerCameraProbeState,
) -> Result<(), String> {
    let sequence = probe
        .jump_sequence
        .ok_or_else(|| "jump phase has no submitted command".to_owned())?;
    if !probe.jump_sequence_applied {
        if !sequence_was_applied(snapshot, sequence, "jump")? {
            return phase_timeout(
                probe.phase_ticks,
                4,
                "jump command was not applied within four fixed ticks",
            );
        }
        probe.jump_sequence_applied = true;
    }

    probe.jump_apex_y_m = probe.jump_apex_y_m.max(snapshot.player_position.y);
    probe.jump_saw_upward_velocity |= snapshot.vertical_velocity_mps > 0.1;
    if !snapshot.grounded {
        probe.jump_airborne_ticks = probe.jump_airborne_ticks.saturating_add(1);
    }

    if probe.jump_airborne_ticks >= 3 && snapshot.grounded {
        let apex_delta_m = probe.jump_apex_y_m - probe.jump_start_y_m;
        if !probe.jump_saw_upward_velocity || apex_delta_m <= 0.5 {
            return Err(format!(
                "jump did not produce a verified upward arc: upward_velocity={}, apex_delta_m={apex_delta_m}",
                probe.jump_saw_upward_velocity
            ));
        }
        enter_phase(probe, PlayerCameraProbePhase::ClearingCamera);
        return Ok(());
    }
    phase_timeout(
        probe.phase_ticks,
        PROBE_JUMP_MAX_FIXED_TICKS,
        "player did not complete an airborne jump and grounded landing",
    )
}

fn advance_clear_camera(
    configuration: &PlayerCameraProbeConfiguration,
    snapshot: &PlayerRuntimeSnapshot,
    probe: &mut PlayerCameraProbeState,
) -> Result<(), String> {
    let nominal = configuration.profile.camera.third_person_distance_m;
    if snapshot.camera_mode == PlayerCameraMode::ThirdPerson
        && snapshot.camera_query_succeeded
        && snapshot.camera_occluder.is_none()
        && snapshot.camera_boom_distance_m >= nominal - 0.02
    {
        probe.unoccluded_boom_distance_m = snapshot.camera_boom_distance_m;
        enter_phase(probe, PlayerCameraProbePhase::Occluding);
        return Ok(());
    }
    phase_timeout(
        probe.phase_ticks,
        PROBE_CLEAR_CAMERA_MAX_FIXED_TICKS,
        "third-person camera did not reach an unobstructed nominal boom before occlusion proof",
    )
}

fn advance_occlusion(
    configuration: &PlayerCameraProbeConfiguration,
    snapshot: &PlayerRuntimeSnapshot,
    probe: &mut PlayerCameraProbeState,
) -> Result<(), String> {
    let wall = probe
        .wall_entity
        .ok_or_else(|| "occlusion phase has no procedural wall".to_owned())?;
    if snapshot.camera_occluder == Some(wall) {
        let closed = snapshot.camera_boom_distance_m;
        if !snapshot.camera_query_succeeded
            || snapshot.camera_hit_time_of_impact_m.is_none()
            || closed >= probe.unoccluded_boom_distance_m - 0.25
            || closed >= configuration.profile.camera.third_person_distance_m
        {
            return Err(format!(
                "procedural wall hit did not close the camera boom: before={}, after={}, query={}, hit={:?}",
                probe.unoccluded_boom_distance_m,
                closed,
                snapshot.camera_query_succeeded,
                snapshot.camera_hit_time_of_impact_m
            ));
        }
        probe.occluded_boom_distance_m = Some(closed);
        enter_phase(probe, PlayerCameraProbePhase::Recovering);
        return Ok(());
    }
    phase_timeout(
        probe.phase_ticks,
        PROBE_OCCLUSION_MAX_FIXED_TICKS,
        "camera sphere cast did not report the procedural occlusion wall",
    )
}

fn advance_recovery(
    configuration: &PlayerCameraProbeConfiguration,
    snapshot: &PlayerRuntimeSnapshot,
    probe: &mut PlayerCameraProbeState,
) -> Result<(), String> {
    let wall = probe
        .wall_entity
        .ok_or_else(|| "recovery phase lost its procedural wall identity".to_owned())?;
    if !probe.wall_removed {
        return phase_timeout(
            probe.phase_ticks,
            4,
            "procedural camera wall was not scheduled for removal",
        );
    }
    if snapshot.camera_occluder == Some(wall) {
        return phase_timeout(
            probe.phase_ticks,
            PROBE_RECOVERY_MAX_FIXED_TICKS,
            "removed procedural wall remained in the camera query pipeline",
        );
    }
    if let Some(other) = snapshot.camera_occluder {
        return Err(format!(
            "camera recovery encountered an unexpected occluder {other:?}"
        ));
    }
    if !snapshot.camera_query_succeeded {
        return Err("camera query stopped succeeding after wall removal".to_owned());
    }

    let closed = probe
        .occluded_boom_distance_m
        .ok_or_else(|| "recovery phase has no closed boom baseline".to_owned())?;
    let current = snapshot.camera_boom_distance_m;
    if current + 1.0e-5 < closed {
        return Err(format!(
            "camera boom moved inward after wall removal: closed={closed}, current={current}"
        ));
    }
    if let Some(previous) = probe.recovery_previous_distance_m {
        let maximum_step = configuration.profile.camera.recovery_speed_mps / 60.0 + 1.0e-4;
        if current + 1.0e-5 < previous || current - previous > maximum_step {
            return Err(format!(
                "camera recovery was not monotone and bounded: previous={previous}, current={current}, maximum_step={maximum_step}"
            ));
        }
    }
    probe.recovery_previous_distance_m = Some(current);
    probe.recovery_samples = probe.recovery_samples.saturating_add(1);

    let nominal = configuration.profile.camera.third_person_distance_m;
    if current >= nominal - 0.01 {
        if probe.recovery_samples < 2 || current <= closed {
            return Err("camera recovery completed without a measured outward sequence".to_owned());
        }
        enter_phase(probe, PlayerCameraProbePhase::FirstPerson);
        return Ok(());
    }
    phase_timeout(
        probe.phase_ticks,
        PROBE_RECOVERY_MAX_FIXED_TICKS,
        "camera did not recover to its nominal third-person distance",
    )
}

fn advance_first_person(
    configuration: &PlayerCameraProbeConfiguration,
    player_bindings: &PlayerRuntimeBindings,
    snapshot: &PlayerRuntimeSnapshot,
    player_visibility: &Query<&Visibility, With<ForgePlayerCharacter>>,
    camera: &Query<(&Transform, &Projection), With<ForgePlayerCamera>>,
    probe: &mut PlayerCameraProbeState,
) -> Result<(), String> {
    let sequence = probe
        .first_person_sequence
        .ok_or_else(|| "first-person phase has no submitted toggle".to_owned())?;
    if !probe.first_person_sequence_applied {
        if !sequence_was_applied(snapshot, sequence, "first-person toggle")? {
            return phase_timeout(
                probe.phase_ticks,
                PROBE_MODE_MAX_FIXED_TICKS,
                "first-person toggle was not applied",
            );
        }
        probe.first_person_sequence_applied = true;
    }

    let binding = player_bindings
        .snapshot()
        .ok_or_else(|| "player bindings disappeared in first-person mode".to_owned())?;
    let initial = probe
        .initial_player_bindings
        .ok_or_else(|| "first-person phase lost initial player bindings".to_owned())?;
    if binding != initial {
        return Err(format!(
            "player or camera identity changed entering first person: before={initial:?}, after={binding:?}"
        ));
    }
    verify_first_person_pose(configuration, snapshot, binding, player_visibility, camera)?;
    probe.first_person_proven = true;
    enter_phase(probe, PlayerCameraProbePhase::ThirdPerson);
    Ok(())
}

#[allow(
    clippy::too_many_arguments,
    reason = "final third-person proof closes over every captured identity and source hash"
)]
fn advance_third_person(
    configuration: &PlayerCameraProbeConfiguration,
    terrain_runtime: &TerrainRuntimeState,
    terrain_bindings: &TerrainRuntimeBindings,
    player_bindings: &PlayerRuntimeBindings,
    snapshot: &PlayerRuntimeSnapshot,
    player_visibility: &Query<&Visibility, With<ForgePlayerCharacter>>,
    camera: &Query<(&Transform, &Projection), With<ForgePlayerCamera>>,
    probe: &mut PlayerCameraProbeState,
) -> Result<bool, String> {
    let sequence = probe
        .third_person_sequence
        .ok_or_else(|| "third-person phase has no submitted toggle".to_owned())?;
    if !probe.third_person_sequence_applied {
        if !sequence_was_applied(snapshot, sequence, "third-person toggle")? {
            phase_timeout(
                probe.phase_ticks,
                PROBE_MODE_MAX_FIXED_TICKS,
                "third-person toggle was not applied",
            )?;
            return Ok(false);
        }
        probe.third_person_sequence_applied = true;
    }

    let player_binding = player_bindings
        .snapshot()
        .ok_or_else(|| "player bindings disappeared after third-person toggle".to_owned())?;
    verify_third_person_pose(
        configuration,
        snapshot,
        player_binding,
        player_visibility,
        camera,
    )?;
    probe.third_person_proven = true;
    verify_final_sources_and_bindings(
        configuration,
        terrain_runtime,
        terrain_bindings,
        player_binding,
        snapshot,
        probe,
    )?;
    emit_completed_event(configuration, player_binding, snapshot, probe);
    Ok(true)
}

fn verify_first_person_pose(
    configuration: &PlayerCameraProbeConfiguration,
    snapshot: &PlayerRuntimeSnapshot,
    binding: PlayerRuntimeBindingSnapshot,
    player_visibility: &Query<&Visibility, With<ForgePlayerCharacter>>,
    camera: &Query<(&Transform, &Projection), With<ForgePlayerCamera>>,
) -> Result<(), String> {
    if snapshot.camera_mode != PlayerCameraMode::FirstPerson
        || snapshot.camera_boom_distance_m.abs() > 1.0e-5
        || snapshot.camera_query_succeeded
        || snapshot.camera_occluder.is_some()
    {
        return Err(format!(
            "first-person snapshot is inconsistent: mode={:?}, boom={}, query={}, occluder={:?}",
            snapshot.camera_mode,
            snapshot.camera_boom_distance_m,
            snapshot.camera_query_succeeded,
            snapshot.camera_occluder
        ));
    }
    let visibility = player_visibility
        .get(binding.player_entity)
        .map_err(|error| format!("cannot read first-person player visibility: {error}"))?;
    if *visibility != Visibility::Hidden {
        return Err(format!(
            "first-person mode must hide the player mesh, observed {visibility:?}"
        ));
    }
    let (camera_transform, projection) = camera
        .get(binding.camera_entity)
        .map_err(|error| format!("cannot read first-person camera projection: {error}"))?;
    let expected_fov = configuration
        .profile
        .camera
        .first_person_vertical_fov_degrees
        .to_radians();
    verify_fov(
        projection,
        snapshot.camera_vertical_fov_radians,
        expected_fov,
    )?;

    let controller = &configuration.profile.controller;
    let camera_profile = &configuration.profile.camera;
    let capsule_half_height = controller.capsule_radius_m + controller.capsule_half_segment_m;
    let expected_position = snapshot.player_position - Vec3::Y * capsule_half_height
        + Vec3::Y * camera_profile.eye_height_m;
    verify_camera_transform(
        camera_transform,
        expected_position,
        snapshot,
        "first-person",
    )
}

fn verify_third_person_pose(
    configuration: &PlayerCameraProbeConfiguration,
    snapshot: &PlayerRuntimeSnapshot,
    binding: PlayerRuntimeBindingSnapshot,
    player_visibility: &Query<&Visibility, With<ForgePlayerCharacter>>,
    camera: &Query<(&Transform, &Projection), With<ForgePlayerCamera>>,
) -> Result<(), String> {
    if snapshot.camera_mode != PlayerCameraMode::ThirdPerson
        || !snapshot.camera_query_succeeded
        || snapshot.camera_boom_distance_m < 0.0
        || snapshot.camera_boom_distance_m
            > configuration.profile.camera.third_person_distance_m + 1.0e-4
    {
        return Err(format!(
            "third-person snapshot is inconsistent: mode={:?}, boom={}, query={}",
            snapshot.camera_mode, snapshot.camera_boom_distance_m, snapshot.camera_query_succeeded
        ));
    }
    let visibility = player_visibility
        .get(binding.player_entity)
        .map_err(|error| format!("cannot read third-person player visibility: {error}"))?;
    if *visibility != Visibility::Visible {
        return Err(format!(
            "third-person mode must show the player mesh, observed {visibility:?}"
        ));
    }
    let (camera_transform, projection) = camera
        .get(binding.camera_entity)
        .map_err(|error| format!("cannot read third-person camera projection: {error}"))?;
    let expected_fov = configuration
        .profile
        .camera
        .third_person_vertical_fov_degrees
        .to_radians();
    verify_fov(
        projection,
        snapshot.camera_vertical_fov_radians,
        expected_fov,
    )?;

    let controller = &configuration.profile.controller;
    let camera_profile = &configuration.profile.camera;
    let capsule_half_height = controller.capsule_radius_m + controller.capsule_half_segment_m;
    let pivot = snapshot.player_position - Vec3::Y * capsule_half_height
        + Vec3::Y * camera_profile.pivot_height_m;
    let expected_forward = expected_camera_forward(snapshot);
    let expected_position = pivot - expected_forward * snapshot.camera_boom_distance_m;
    verify_camera_transform(
        camera_transform,
        expected_position,
        snapshot,
        "third-person",
    )
}

fn verify_camera_transform(
    transform: &Transform,
    expected_position: Vec3,
    snapshot: &PlayerRuntimeSnapshot,
    mode: &str,
) -> Result<(), String> {
    if !transform
        .translation
        .abs_diff_eq(expected_position, POSITION_EPSILON_M)
    {
        return Err(format!(
            "{mode} camera pose mismatch: expected {expected_position:?}, observed {:?}",
            transform.translation
        ));
    }
    let actual_forward = transform.rotation * Vec3::NEG_Z;
    let expected_forward = expected_camera_forward(snapshot);
    if !actual_forward.abs_diff_eq(expected_forward, ROTATION_EPSILON) {
        return Err(format!(
            "{mode} camera orientation mismatch: expected {expected_forward:?}, observed {actual_forward:?}"
        ));
    }
    Ok(())
}

fn expected_camera_forward(snapshot: &PlayerRuntimeSnapshot) -> Vec3 {
    Quat::from_rotation_y(snapshot.camera_yaw_radians)
        * Quat::from_rotation_x(snapshot.camera_pitch_radians)
        * Vec3::NEG_Z
}

fn verify_fov(projection: &Projection, snapshot_fov: f32, expected_fov: f32) -> Result<(), String> {
    let Projection::Perspective(perspective) = projection else {
        return Err("player camera does not use a perspective projection".to_owned());
    };
    if (perspective.fov - expected_fov).abs() > FOV_EPSILON_RADIANS
        || (snapshot_fov - expected_fov).abs() > FOV_EPSILON_RADIANS
    {
        return Err(format!(
            "player camera FOV mismatch: expected={expected_fov}, projection={}, snapshot={snapshot_fov}",
            perspective.fov
        ));
    }
    Ok(())
}

fn verify_final_sources_and_bindings(
    configuration: &PlayerCameraProbeConfiguration,
    terrain_runtime: &TerrainRuntimeState,
    terrain_bindings: &TerrainRuntimeBindings,
    player_binding: PlayerRuntimeBindingSnapshot,
    snapshot: &PlayerRuntimeSnapshot,
    probe: &PlayerCameraProbeState,
) -> Result<(), String> {
    let current_terrain_hash = terrain_runtime
        .document()
        .content_hash()
        .map_err(|error| format!("cannot hash final player-probe terrain: {error}"))?;
    if current_terrain_hash != configuration.terrain_document_hash {
        return Err(format!(
            "terrain document changed during player probe: expected {}, got {current_terrain_hash}",
            configuration.terrain_document_hash
        ));
    }
    if snapshot.profile_content_hash != Some(configuration.profile_content_hash) {
        return Err(format!(
            "player profile hash changed during probe: expected {}, got {:?}",
            configuration.profile_content_hash, snapshot.profile_content_hash
        ));
    }

    let initial_player = probe
        .initial_player_bindings
        .ok_or_else(|| "final proof has no initial player binding snapshot".to_owned())?;
    if player_binding != initial_player
        || !player_binding.backend_identity_stable
        || player_binding.rapier_collider_handle.is_none()
        || player_binding.rapier_context_entity.is_none()
        || !snapshot.backend_identity_stable
    {
        return Err(format!(
            "player bindings were not stable: initial={initial_player:?}, final={player_binding:?}, snapshot_backend_stable={}",
            snapshot.backend_identity_stable
        ));
    }
    let initial_terrain = probe
        .initial_terrain_bindings
        .as_ref()
        .ok_or_else(|| "final proof has no initial terrain binding snapshot".to_owned())?;
    let final_terrain = terrain_bindings
        .snapshots()
        .map_err(|error| format!("cannot snapshot final terrain bindings: {error}"))?;
    if &final_terrain != initial_terrain {
        return Err(
            "terrain render, mesh or collider identities changed during player probe".to_owned(),
        );
    }
    if !probe.walk_sequence_applied
        || !probe.stop_sequence_applied
        || !probe.jump_sequence_applied
        || !probe.first_person_sequence_applied
        || !probe.third_person_sequence_applied
        || !probe.first_person_proven
        || !probe.third_person_proven
    {
        return Err("not every public player command and camera mode proof completed".to_owned());
    }
    if !probe.wall_removed || snapshot.camera_occluder.is_some() {
        return Err("procedural camera wall was not removed cleanly".to_owned());
    }
    if probe.vegetation_interaction_available
        && (probe.vegetation_interaction_walk_samples == 0
            || probe.vegetation_interaction_velocity_aligned_samples
                != probe.vegetation_interaction_walk_samples
            || probe.vegetation_interaction_max_center_error_m > VEGETATION_INTERACTION_EPSILON_M)
    {
        return Err(format!(
            "vegetation interaction runtime proof is incomplete: samples={}, aligned={}, max_center_error_m={}",
            probe.vegetation_interaction_walk_samples,
            probe.vegetation_interaction_velocity_aligned_samples,
            probe.vegetation_interaction_max_center_error_m
        ));
    }
    Ok(())
}

fn emit_completed_event(
    configuration: &PlayerCameraProbeConfiguration,
    player_binding: PlayerRuntimeBindingSnapshot,
    snapshot: &PlayerRuntimeSnapshot,
    probe: &PlayerCameraProbeState,
) {
    let walk_start = probe.walk_start_position.unwrap_or(Vec3::ZERO);
    let walk_end = probe.walk_end_position.unwrap_or(snapshot.player_position);
    let jump_apex_delta_m = probe.jump_apex_y_m - probe.jump_start_y_m;
    let occluded_boom_distance_m = probe.occluded_boom_distance_m.unwrap_or_default();
    let terrain_binding_count = probe.initial_terrain_bindings.as_ref().map_or(0, Vec::len);

    info!(
        target: "forge::player",
        schema = "forge.runtime-event",
        schema_version = 1_u64,
        event_kind = "player_camera_probe_completed",
        success = true,
        fixed_ticks = probe.fixed_ticks,
        player_profile_hash = %configuration.profile_content_hash,
        expected_player_profile_hash = EXPECTED_PLAYER_PROFILE_HASH,
        profile_hash_exact = configuration.profile_content_hash.to_string() == EXPECTED_PLAYER_PROFILE_HASH,
        terrain_document_hash = %configuration.terrain_document_hash,
        terrain_bindings = terrain_binding_count,
        terrain_bindings_preserved = true,
        player_bindings_preserved = true,
        backend_identity_stable = player_binding.backend_identity_stable,
        player_entity = ?player_binding.player_entity,
        camera_entity = ?player_binding.camera_entity,
        player_mesh_asset = ?player_binding.mesh_asset_id,
        player_rapier_collider = ?player_binding.rapier_collider_handle,
        player_rapier_context = ?player_binding.rapier_context_entity,
        walk_sequence = probe.walk_sequence.unwrap_or_default(),
        stop_sequence = probe.stop_sequence.unwrap_or_default(),
        jump_sequence = probe.jump_sequence.unwrap_or_default(),
        first_person_sequence = probe.first_person_sequence.unwrap_or_default(),
        third_person_sequence = probe.third_person_sequence.unwrap_or_default(),
        walk_fixed_ticks = probe.walk_observed_ticks,
        walk_grounded_ticks = probe.walk_grounded_ticks,
        walk_max_ungrounded_run = probe.walk_max_ungrounded_run,
        walk_start_x_m = f64::from(walk_start.x),
        walk_start_z_m = f64::from(walk_start.z),
        walk_end_x_m = f64::from(walk_end.x),
        walk_end_z_m = f64::from(walk_end.z),
        central_seam_crossed = walk_start.z > 0.0 && walk_end.z < 0.0,
        jump_airborne_ticks = probe.jump_airborne_ticks,
        jump_apex_delta_m = f64::from(jump_apex_delta_m),
        jump_landed = snapshot.grounded,
        unoccluded_boom_distance_m = f64::from(probe.unoccluded_boom_distance_m),
        occluded_boom_distance_m = f64::from(occluded_boom_distance_m),
        camera_recovery_samples = probe.recovery_samples,
        camera_recovery_monotone = true,
        first_person_visibility_fov_pose = probe.first_person_proven,
        third_person_visibility_fov_pose = probe.third_person_proven,
        vegetation_interaction_available = probe.vegetation_interaction_available,
        vegetation_interaction_walk_samples = probe.vegetation_interaction_walk_samples,
        vegetation_interaction_velocity_aligned_samples = probe.vegetation_interaction_velocity_aligned_samples,
        vegetation_interaction_max_center_error_m = f64::from(probe.vegetation_interaction_max_center_error_m),
        camera_mode_path = "third_person>first_person>third_person",
        final_camera_mode = ?snapshot.camera_mode,
        final_camera_boom_distance_m = f64::from(snapshot.camera_boom_distance_m),
        "player controller, terrain seam, camera and vegetation interaction probe passed on ordered fixed schedules"
    );
}

fn sequence_was_applied(
    snapshot: &PlayerRuntimeSnapshot,
    expected: u64,
    command: &str,
) -> Result<bool, String> {
    match snapshot.last_applied_sequence {
        Some(actual) if actual == expected => Ok(true),
        Some(actual) if actual > expected => Err(format!(
            "{command} command sequence {expected} was skipped by applied sequence {actual}"
        )),
        _ => Ok(false),
    }
}

fn phase_timeout(observed_ticks: u32, maximum_ticks: u32, message: &str) -> Result<(), String> {
    if observed_ticks >= maximum_ticks {
        Err(message.to_owned())
    } else {
        Ok(())
    }
}

fn enter_phase(probe: &mut PlayerCameraProbeState, phase: PlayerCameraProbePhase) {
    probe.phase = phase;
    probe.phase_ticks = 0;
}

fn fail_probe(
    fixed_tick: u64,
    phase: PlayerCameraProbePhase,
    reason: &str,
    exit: &mut MessageWriter<AppExit>,
) {
    error!(
        target: "forge::player",
        schema = "forge.runtime-event",
        schema_version = 1_u64,
        event_kind = "player_camera_probe_failed",
        success = false,
        fixed_tick,
        phase = phase.label(),
        error = reason,
        "player-camera probe failed"
    );
    exit.write(AppExit::error());
}

#[cfg(test)]
mod tests {
    use super::*;
    use forge_world::{
        embedded_visual_valley_player_controller_profile, embedded_visual_valley_terrain_document,
    };

    #[test]
    fn configuration_binds_the_exact_profile_hash_and_positive_central_seam_start() {
        let terrain = embedded_visual_valley_terrain_document().unwrap();
        let profile = embedded_visual_valley_player_controller_profile().unwrap();
        let plugin = PlayerCameraProbePlugin::new(&terrain, &profile).unwrap();

        assert_eq!(
            plugin.configuration.profile_content_hash.to_string(),
            EXPECTED_PLAYER_PROFILE_HASH
        );
        assert_eq!(
            plugin.configuration.terrain_document_hash,
            terrain.content_hash().unwrap()
        );
        assert_eq!(
            plugin.spawn_position().x.to_bits(),
            PROBE_START_X_M.to_bits()
        );
        assert_eq!(
            plugin.spawn_position().z.to_bits(),
            PROBE_START_Z_M.to_bits()
        );
        assert!(plugin.spawn_position().z > 0.0);
        assert!(plugin.spawn_position().is_finite());
    }
}
