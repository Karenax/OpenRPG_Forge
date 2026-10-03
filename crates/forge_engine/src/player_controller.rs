use crate::player_camera::{
    PlayerCameraMode, PlayerCameraRig, first_person_pose, resolve_boom_distance, third_person_pose,
};
use bevy::{
    asset::AssetId,
    input::{InputSystems, mouse::AccumulatedMouseMotion},
    prelude::*,
};
use bevy_rapier3d::{
    control::{
        CharacterAutostep, CharacterLength, KinematicCharacterController,
        KinematicCharacterControllerOutput,
    },
    prelude::{
        Collider, PhysicsSet, QueryFilter, QueryFilterFlags, RapierColliderHandle,
        RapierContextEntityLink, ReadRapierContext, ShapeCastOptions,
    },
};
use forge_core::ContentHash;
use forge_project::PlayerControllerProfileDocument;
use std::{error::Error, fmt};

const CAMERA_COLLISION_MARGIN_M: f32 = 0.05;

#[path = "tidewater_player.rs"]
mod tidewater;
pub use crate::tidewater_locomotion::TidewaterLadder;
pub use tidewater::{TidewaterPlayerInput, TidewaterPlayerPlugin, TidewaterPlayerState};

/// Public fixed-schedule boundaries used by deterministic probes and later Studio playtests.
#[derive(Clone, Debug, Hash, PartialEq, Eq, SystemSet)]
pub enum PlayerControllerSet {
    /// Converts the latched command into one requested KCC translation.
    Drive,
    /// Reads the KCC result only after Rapier writeback has completed.
    Observe,
    /// Projects the verified player pose into the single stable camera entity.
    Camera,
}

/// Absolute world-space centre of the player capsule at startup.
#[derive(Clone, Copy, Debug, PartialEq, Resource)]
pub struct PlayerSpawnPoint(pub Vec3);

#[derive(Clone, Copy, Resource)]
struct PlayerInitialCameraTarget(Option<Vec3>);

#[derive(Clone, Copy, Resource)]
struct PlayerInitialCameraMode(PlayerCameraMode);

#[derive(Clone, Copy, Resource)]
struct PlayerInitialCameraPitch(Option<f32>);

impl PlayerSpawnPoint {
    #[must_use]
    pub const fn position(self) -> Vec3 {
        self.0
    }

    /// Relocates a configured initial pose while preserving its camera direction.
    /// Only valid before startup has bound the live player and camera.
    pub fn rebase_before_start(world: &mut World, position: Vec3) -> Result<(), String> {
        if !position.is_finite() {
            return Err("Player resume position must be finite".into());
        }
        let bindings = world
            .get_resource::<PlayerRuntimeBindings>()
            .ok_or_else(|| "Player controller is not configured".to_owned())?;
        if bindings.snapshot().is_some() {
            return Err("Initial player pose cannot be rebased after startup".into());
        }
        let previous = world
            .get_resource::<Self>()
            .ok_or_else(|| "Initial player spawn is missing".to_owned())?
            .0;
        let target = world
            .get_resource::<PlayerInitialCameraTarget>()
            .ok_or_else(|| "Initial player camera target is missing".to_owned())?
            .0
            .map(|target| position + (target - previous));
        if target.is_some_and(|target| !target.is_finite()) {
            return Err("Rebased player camera target must be finite".into());
        }
        world.insert_resource(Self(position));
        world.insert_resource(PlayerInitialCameraTarget(target));
        Ok(())
    }
}

/// One replayable input sample. Positive Y moves forward in the current camera yaw.
#[derive(Clone, Copy, Debug, Default, PartialEq)]
pub struct PlayerControlCommand {
    pub move_axis: Vec2,
    pub sprint: bool,
    pub jump: bool,
    /// Raw look units; the neutral profile supplies degrees per unit.
    pub look_delta: Vec2,
    pub toggle_camera: bool,
}

/// Runtime input ownership, controlled by the host; never persisted in the world document.
#[derive(Clone, Copy, Debug, Resource)]
pub struct PlayerInputFocus(pub bool);

impl Default for PlayerInputFocus {
    fn default() -> Self {
        Self(true)
    }
}

/// Host-owned projection barrier, separate from keyboard focus. Close before startup
/// when playtest projections are pending. This freezes only the player controller,
/// not the world simulation, and never becomes part of an authored document.
#[derive(Clone, Copy, Debug, Resource)]
pub struct PlayerSimulationReady(pub bool);

impl Default for PlayerSimulationReady {
    fn default() -> Self {
        Self(true)
    }
}

/// Coalescing batch consumed by the next fixed simulation tick.
///
/// Axis and sprint use the last submission, look deltas are summed, jump uses logical OR, and
/// camera toggles use parity. The applied sequence is the batch's inclusive high-water mark.
#[derive(Debug, Default, Resource)]
pub struct PendingPlayerControlCommand {
    next_sequence: u64,
    pending_sequence: Option<u64>,
    held_move_axis: Vec2,
    held_raw_move_axis: Vec2,
    held_sprint: bool,
    queued_jump: bool,
    queued_look_delta: Vec2,
    queued_toggle_camera: bool,
}

impl PendingPlayerControlCommand {
    /// Cancels held/queued input while retaining the monotonic command sequence.
    pub fn cancel_input(&mut self) {
        self.pending_sequence = None;
        self.held_move_axis = Vec2::ZERO;
        self.held_raw_move_axis = Vec2::ZERO;
        self.held_sprint = false;
        self.queued_jump = false;
        self.queued_look_delta = Vec2::ZERO;
        self.queued_toggle_camera = false;
    }

    /// Replaces held axes while accumulating one-shot actions for the next fixed tick.
    pub fn submit(
        &mut self,
        command: PlayerControlCommand,
    ) -> Result<u64, PlayerControlCommandError> {
        if !command.move_axis.is_finite() {
            return Err(PlayerControlCommandError::NonFiniteMoveAxis);
        }
        if !command.look_delta.is_finite() {
            return Err(PlayerControlCommandError::NonFiniteLookDelta);
        }
        let accumulated_look = self.queued_look_delta + command.look_delta;
        if !accumulated_look.is_finite() {
            return Err(PlayerControlCommandError::LookDeltaOverflow);
        }
        let sequence = self
            .next_sequence
            .checked_add(1)
            .ok_or(PlayerControlCommandError::SequenceOverflow)?;

        self.next_sequence = sequence;
        self.pending_sequence = Some(sequence);
        self.held_move_axis = normalize_move_axis(command.move_axis);
        self.held_raw_move_axis = command.move_axis;
        self.held_sprint = command.sprint;
        self.queued_jump |= command.jump;
        self.queued_look_delta = accumulated_look;
        self.queued_toggle_camera ^= command.toggle_camera;
        Ok(sequence)
    }

    fn drain_tick(&mut self) -> (Option<u64>, PlayerControlCommand) {
        let command = PlayerControlCommand {
            move_axis: self.held_move_axis,
            sprint: self.held_sprint,
            jump: self.queued_jump,
            look_delta: self.queued_look_delta,
            toggle_camera: self.queued_toggle_camera,
        };
        self.queued_jump = false;
        self.queued_look_delta = Vec2::ZERO;
        self.queued_toggle_camera = false;
        (self.pending_sequence.take(), command)
    }
}

#[derive(Clone, Copy, Debug, Eq, PartialEq)]
pub enum PlayerControlCommandError {
    NonFiniteMoveAxis,
    NonFiniteLookDelta,
    LookDeltaOverflow,
    SequenceOverflow,
}

impl fmt::Display for PlayerControlCommandError {
    fn fmt(&self, formatter: &mut fmt::Formatter<'_>) -> fmt::Result {
        match self {
            Self::NonFiniteMoveAxis => write!(formatter, "player move axis must be finite"),
            Self::NonFiniteLookDelta => write!(formatter, "player look delta must be finite"),
            Self::LookDeltaOverflow => {
                write!(formatter, "accumulated player look delta overflowed")
            }
            Self::SequenceOverflow => write!(formatter, "player command sequence overflowed"),
        }
    }
}

impl Error for PlayerControlCommandError {}

/// Stable ECS/asset/backend identities for one player projection.
#[derive(Clone, Copy, Debug, Eq, PartialEq)]
pub struct PlayerRuntimeBindingSnapshot {
    pub player_entity: Entity,
    pub camera_entity: Entity,
    pub mesh_asset_id: Option<AssetId<Mesh>>,
    pub rapier_collider_handle: Option<(u32, u32)>,
    pub rapier_context_entity: Option<Entity>,
    pub backend_identity_stable: bool,
}

/// Runtime-only bindings. No Bevy or Rapier identity crosses into persistent documents.
#[derive(Debug, Resource)]
pub struct PlayerRuntimeBindings {
    player_entity: Option<Entity>,
    camera_entity: Option<Entity>,
    mesh_handle: Option<Handle<Mesh>>,
    rapier_collider_handle: Option<(u32, u32)>,
    rapier_context_entity: Option<Entity>,
    backend_identity_stable: bool,
}

impl Default for PlayerRuntimeBindings {
    fn default() -> Self {
        Self {
            player_entity: None,
            camera_entity: None,
            mesh_handle: None,
            rapier_collider_handle: None,
            rapier_context_entity: None,
            backend_identity_stable: true,
        }
    }
}

impl PlayerRuntimeBindings {
    #[must_use]
    pub fn snapshot(&self) -> Option<PlayerRuntimeBindingSnapshot> {
        Some(PlayerRuntimeBindingSnapshot {
            player_entity: self.player_entity?,
            camera_entity: self.camera_entity?,
            mesh_asset_id: self.mesh_handle.as_ref().map(Handle::id),
            rapier_collider_handle: self.rapier_collider_handle,
            rapier_context_entity: self.rapier_context_entity,
            backend_identity_stable: self.backend_identity_stable,
        })
    }

    fn register(
        &mut self,
        player_entity: Entity,
        camera_entity: Entity,
        mesh_handle: Option<Handle<Mesh>>,
    ) {
        assert!(self.player_entity.replace(player_entity).is_none());
        assert!(self.camera_entity.replace(camera_entity).is_none());
        assert!(self.mesh_handle.is_none());
        self.mesh_handle = mesh_handle;
    }

    fn observe_backend(
        &mut self,
        collider_handle: Option<(u32, u32)>,
        context_entity: Option<Entity>,
    ) {
        observe_stable_identity(
            &mut self.rapier_collider_handle,
            collider_handle,
            &mut self.backend_identity_stable,
        );
        observe_stable_identity(
            &mut self.rapier_context_entity,
            context_entity,
            &mut self.backend_identity_stable,
        );
    }
}

fn observe_stable_identity<T: Copy + Eq>(
    stored: &mut Option<T>,
    observed: Option<T>,
    stable: &mut bool,
) {
    match (*stored, observed) {
        (None, Some(value)) => *stored = Some(value),
        (Some(expected), Some(actual)) if expected != actual => *stable = false,
        (Some(_), None) => *stable = false,
        _ => {}
    }
}

/// Sequenced state exposed to bounded runtime probes after each fixed projection.
#[allow(
    clippy::struct_excessive_bools,
    reason = "probe snapshots intentionally expose independent simulation/query proof bits"
)]
#[derive(Clone, Debug, Resource)]
pub struct PlayerRuntimeSnapshot {
    /// Canonical hash of the validated engine-neutral profile projected by this runtime.
    pub profile_content_hash: Option<ContentHash>,
    pub fixed_tick: u64,
    /// Inclusive high-water mark of the last coalesced command batch applied by `Drive`.
    pub last_applied_sequence: Option<u64>,
    pub player_position: Vec3,
    pub desired_translation: Vec3,
    pub effective_translation: Vec3,
    pub horizontal_velocity: Vec3,
    pub vertical_velocity_mps: f32,
    pub grounded: bool,
    pub sliding_down_slope: bool,
    pub collisions: usize,
    pub camera_mode: PlayerCameraMode,
    pub camera_yaw_radians: f32,
    pub camera_pitch_radians: f32,
    pub camera_boom_distance_m: f32,
    pub camera_occluder: Option<Entity>,
    pub camera_hit_time_of_impact_m: Option<f32>,
    pub camera_query_succeeded: bool,
    pub camera_vertical_fov_radians: f32,
    pub backend_identity_stable: bool,
}

impl Default for PlayerRuntimeSnapshot {
    fn default() -> Self {
        Self {
            profile_content_hash: None,
            fixed_tick: 0,
            last_applied_sequence: None,
            player_position: Vec3::ZERO,
            desired_translation: Vec3::ZERO,
            effective_translation: Vec3::ZERO,
            horizontal_velocity: Vec3::ZERO,
            vertical_velocity_mps: 0.0,
            grounded: false,
            sliding_down_slope: false,
            collisions: 0,
            camera_mode: PlayerCameraMode::ThirdPerson,
            camera_yaw_radians: 0.0,
            camera_pitch_radians: 0.0,
            camera_boom_distance_m: 0.0,
            camera_occluder: None,
            camera_hit_time_of_impact_m: None,
            camera_query_succeeded: false,
            camera_vertical_fov_radians: 0.0,
            backend_identity_stable: true,
        }
    }
}

/// Marker for the single derived player capsule.
#[derive(Clone, Copy, Component, Debug, Eq, PartialEq)]
pub struct ForgePlayerCharacter;

/// Marker for the single camera entity retained across first/third-person toggles.
#[derive(Clone, Copy, Component, Debug, Eq, PartialEq)]
pub struct ForgePlayerCamera;

#[derive(Clone, Resource)]
struct PlayerRuntimeProfile(PlayerControllerProfileDocument);

#[derive(Component, Debug)]
struct PlayerMotionState {
    horizontal_velocity: Vec3,
    vertical_velocity_mps: f32,
    grounded: bool,
    fixed_tick: u64,
    last_desired_translation: Vec3,
}

impl Default for PlayerMotionState {
    fn default() -> Self {
        Self {
            horizontal_velocity: Vec3::ZERO,
            vertical_velocity_mps: 0.0,
            grounded: false,
            fixed_tick: 0,
            last_desired_translation: Vec3::ZERO,
        }
    }
}

/// Asset-free Bevy/Rapier adapter for one human-scale Forge player profile.
pub struct PlayerControllerPlugin {
    profile: PlayerControllerProfileDocument,
    spawn_point: PlayerSpawnPoint,
    interactive_input: bool,
    initial_camera_target: Option<Vec3>,
    initial_camera_mode: PlayerCameraMode,
    initial_camera_pitch_degrees: Option<f32>,
}

impl PlayerControllerPlugin {
    #[must_use]
    pub const fn new(profile: PlayerControllerProfileDocument, spawn_position: Vec3) -> Self {
        Self {
            profile,
            spawn_point: PlayerSpawnPoint(spawn_position),
            interactive_input: true,
            initial_camera_target: None,
            initial_camera_mode: PlayerCameraMode::ThirdPerson,
            initial_camera_pitch_degrees: None,
        }
    }

    /// Enables or suppresses OS keyboard/mouse sampling while preserving the public command latch.
    #[must_use]
    pub const fn with_interactive_input(mut self, enabled: bool) -> Self {
        self.interactive_input = enabled;
        self
    }

    /// Sets the initial horizontal heading only; pitch and subsequent player input remain free.
    /// A target directly above/below the spawn retains the default heading.
    #[must_use]
    pub const fn with_initial_camera_target(mut self, target: Vec3) -> Self {
        self.initial_camera_target = Some(target);
        self
    }

    /// Selects the startup view without changing the player's collision body or the V toggle.
    #[must_use]
    pub const fn with_initial_camera_mode(mut self, mode: PlayerCameraMode) -> Self {
        self.initial_camera_mode = mode;
        self
    }

    /// Sets a camera pitch within the validated profile's authored limits.
    #[must_use]
    pub const fn with_initial_camera_pitch_degrees(mut self, pitch: f32) -> Self {
        self.initial_camera_pitch_degrees = Some(pitch);
        self
    }
}

impl Plugin for PlayerControllerPlugin {
    fn build(&self, app: &mut App) {
        self.profile.validate().unwrap_or_else(|error| {
            panic!("PlayerControllerPlugin requires a valid profile: {error}")
        });
        let profile_content_hash = self.profile.content_hash().unwrap_or_else(|error| {
            panic!("PlayerControllerPlugin could not hash its validated profile: {error}")
        });
        assert!(
            self.spawn_point.0.is_finite(),
            "PlayerControllerPlugin requires a finite spawn position"
        );
        if let Some(target) = self.initial_camera_target {
            assert!(
                target.is_finite() && (target - self.spawn_point.0).is_finite(),
                "PlayerControllerPlugin requires a finite initial camera direction"
            );
        }
        if let Some(pitch) = self.initial_camera_pitch_degrees {
            assert!(
                pitch.is_finite()
                    && (self.profile.camera.pitch_min_degrees
                        ..=self.profile.camera.pitch_max_degrees)
                        .contains(&pitch),
                "PlayerControllerPlugin requires an initial pitch inside the camera profile"
            );
        }

        let camera_system = update_player_camera.in_set(PlayerControllerSet::Camera);
        // Diagnostic ownership also applies when its paused clock is stepped.
        // Keep ordinary player simulation, but never overwrite the authored proof camera.
        #[cfg(feature = "qualification")]
        let camera_system = camera_system.run_if(bevy::ecs::schedule::common_conditions::not(
            bevy::ecs::schedule::common_conditions::resource_exists::<crate::FixedProofViewStatus>,
        ));
        app.insert_resource(PlayerRuntimeProfile(self.profile.clone()))
            .insert_resource(self.spawn_point)
            .insert_resource(PlayerInitialCameraTarget(self.initial_camera_target))
            .insert_resource(PlayerInitialCameraMode(self.initial_camera_mode))
            .insert_resource(PlayerInitialCameraPitch(self.initial_camera_pitch_degrees))
            .init_resource::<PendingPlayerControlCommand>()
            .init_resource::<PlayerInputFocus>()
            .init_resource::<PlayerSimulationReady>()
            .init_resource::<PlayerRuntimeBindings>()
            .insert_resource(PlayerRuntimeSnapshot {
                profile_content_hash: Some(profile_content_hash),
                ..default()
            })
            .configure_sets(
                FixedUpdate,
                PlayerControllerSet::Drive.before(PhysicsSet::SyncBackend),
            )
            .configure_sets(
                FixedUpdate,
                PlayerControllerSet::Observe.after(PhysicsSet::Writeback),
            )
            .configure_sets(
                FixedUpdate,
                PlayerControllerSet::Camera.after(PlayerControllerSet::Observe),
            )
            .add_systems(Startup, spawn_player_controller)
            .add_systems(
                FixedUpdate,
                drive_player_controller
                    .in_set(PlayerControllerSet::Drive)
                    .run_if(bevy::ecs::schedule::common_conditions::not(
                        bevy::ecs::schedule::common_conditions::resource_exists::<
                            TidewaterPlayerState,
                        >,
                    )),
            )
            .add_systems(
                FixedUpdate,
                observe_player_controller.in_set(PlayerControllerSet::Observe),
            )
            .add_systems(FixedUpdate, camera_system);
        if self.interactive_input {
            app.add_systems(PreUpdate, sample_player_input.after(InputSystems));
        }
    }
}

#[allow(
    clippy::needless_pass_by_value,
    reason = "Bevy systems require owned system parameter wrappers"
)]
#[allow(
    clippy::too_many_lines,
    reason = "Creates the player, its camera, and their runtime bindings in one startup transaction."
)]
fn spawn_player_controller(
    mut commands: Commands,
    profile: Res<PlayerRuntimeProfile>,
    (spawn_point, camera_target, camera_mode, camera_pitch): (
        Res<PlayerSpawnPoint>,
        Res<PlayerInitialCameraTarget>,
        Res<PlayerInitialCameraMode>,
        Res<PlayerInitialCameraPitch>,
    ),
    mut bindings: ResMut<PlayerRuntimeBindings>,
    mut snapshot: ResMut<PlayerRuntimeSnapshot>,
    meshes: Option<ResMut<Assets<Mesh>>>,
    materials: Option<ResMut<Assets<StandardMaterial>>>,
) {
    let controller_profile = &profile.0.controller;
    let camera_profile = &profile.0.camera;
    let mesh_handle = meshes.map(|mut assets| {
        assets.add(Capsule3d::new(
            controller_profile.capsule_radius_m,
            controller_profile.capsule_half_segment_m * 2.0,
        ))
    });
    let material_handle = materials.map(|mut assets| {
        assets.add(StandardMaterial {
            base_color: Color::srgb(0.16, 0.42, 0.78),
            metallic: 0.05,
            perceptual_roughness: 0.72,
            ..default()
        })
    });

    let mut player = commands.spawn((
        Name::new("Forge player capsule"),
        ForgePlayerCharacter,
        Collider::capsule_y(
            controller_profile.capsule_half_segment_m,
            controller_profile.capsule_radius_m,
        ),
        KinematicCharacterController {
            up: Vec3::Y,
            offset: CharacterLength::Absolute(0.01),
            slide: true,
            autostep: Some(CharacterAutostep {
                max_height: CharacterLength::Absolute(controller_profile.max_step_height_m),
                min_width: CharacterLength::Absolute(controller_profile.min_step_width_m),
                include_dynamic_bodies: false,
            }),
            max_slope_climb_angle: controller_profile.max_slope_climb_degrees.to_radians(),
            min_slope_slide_angle: controller_profile.min_slope_slide_degrees.to_radians(),
            snap_to_ground: Some(CharacterLength::Absolute(controller_profile.ground_snap_m)),
            filter_flags: QueryFilterFlags::EXCLUDE_SENSORS,
            ..default()
        },
        PlayerMotionState::default(),
        Transform::from_translation(spawn_point.0),
        if camera_mode.0 == PlayerCameraMode::FirstPerson {
            Visibility::Hidden
        } else {
            Visibility::Visible
        },
    ));
    if let (Some(mesh), Some(material)) = (&mesh_handle, &material_handle) {
        player.insert((Mesh3d(mesh.clone()), MeshMaterial3d(material.clone())));
    }
    let player_entity = player.id();

    let mut rig = PlayerCameraRig::new(camera_profile);
    rig.mode = camera_mode.0;
    if let Some(pitch) = camera_pitch.0 {
        rig.pitch_radians = pitch.to_radians();
    }
    if rig.mode == PlayerCameraMode::FirstPerson {
        rig.boom_distance_m = 0.0;
    }
    if let Some(target) = camera_target.0 {
        let direction = target - spawn_point.0;
        if direction.x.abs().max(direction.z.abs()) > f32::EPSILON {
            rig.yaw_radians = (-direction.x).atan2(-direction.z);
        }
    }
    let capsule_half_height =
        controller_profile.capsule_radius_m + controller_profile.capsule_half_segment_m;
    let pose = match rig.mode {
        PlayerCameraMode::FirstPerson => {
            first_person_pose(spawn_point.0, capsule_half_height, rig, camera_profile)
        }
        PlayerCameraMode::ThirdPerson => {
            third_person_pose(spawn_point.0, capsule_half_height, rig, camera_profile)
        }
    };
    let vertical_fov_degrees = match rig.mode {
        PlayerCameraMode::FirstPerson => camera_profile.first_person_vertical_fov_degrees,
        PlayerCameraMode::ThirdPerson => camera_profile.third_person_vertical_fov_degrees,
    };
    let camera_entity = commands
        .spawn((
            Name::new("Forge player camera"),
            ForgePlayerCamera,
            rig,
            Camera3d::default(),
            AmbientLight {
                color: Color::srgb(0.48, 0.58, 0.72),
                brightness: 95.0,
                ..default()
            },
            Projection::Perspective(PerspectiveProjection {
                fov: vertical_fov_degrees.to_radians(),
                near: 0.05,
                far: 1_600.0,
                ..default()
            }),
            Transform::from_translation(pose.translation).looking_to(pose.forward, Vec3::Y),
        ))
        .id();

    bindings.register(player_entity, camera_entity, mesh_handle);
    snapshot.player_position = spawn_point.0;
    snapshot.camera_mode = rig.mode;
    snapshot.camera_yaw_radians = rig.yaw_radians;
    snapshot.camera_pitch_radians = rig.pitch_radians;
    snapshot.camera_boom_distance_m = rig.boom_distance_m;
    snapshot.camera_vertical_fov_radians = vertical_fov_degrees.to_radians();
}

#[allow(
    clippy::needless_pass_by_value,
    reason = "Bevy systems require owned system parameter wrappers"
)]
fn sample_player_input(
    keyboard: Option<Res<ButtonInput<KeyCode>>>,
    mouse_motion: Option<Res<AccumulatedMouseMotion>>,
    focus: Res<PlayerInputFocus>,
    warmup: Option<Res<crate::ProfileWarmup>>,
    mut pending: ResMut<PendingPlayerControlCommand>,
) {
    if !focus.0
        || warmup
            .as_deref()
            .is_some_and(crate::ProfileWarmup::is_warming)
    {
        pending.cancel_input();
        return;
    }
    let Some(keyboard) = keyboard else {
        return;
    };
    let axis = Vec2::new(
        axis_value(&keyboard, KeyCode::KeyD) - axis_value(&keyboard, KeyCode::KeyA),
        axis_value(&keyboard, KeyCode::KeyW) - axis_value(&keyboard, KeyCode::KeyS),
    );
    let command = PlayerControlCommand {
        move_axis: axis,
        sprint: keyboard.pressed(KeyCode::ShiftLeft) || keyboard.pressed(KeyCode::ShiftRight),
        jump: keyboard.just_pressed(KeyCode::Space),
        look_delta: mouse_motion.map_or(Vec2::ZERO, |motion| motion.delta),
        toggle_camera: keyboard.just_pressed(KeyCode::KeyV),
    };
    pending
        .submit(command)
        .expect("keyboard and accumulated mouse input are finite");
}

fn axis_value(keyboard: &ButtonInput<KeyCode>, code: KeyCode) -> f32 {
    // A browser automation tap can deliver keydown and keyup before the next
    // Bevy frame. Keep that one physical press observable for this sample;
    // sustained movement still uses the held state on following frames.
    f32::from(keyboard.pressed(code) || keyboard.just_pressed(code))
}

#[allow(
    clippy::needless_pass_by_value,
    reason = "Bevy systems require owned system parameter wrappers"
)]
fn drive_player_controller(
    fixed_time: Res<Time<Fixed>>,
    ready: Res<PlayerSimulationReady>,
    profile: Res<PlayerRuntimeProfile>,
    mut pending: ResMut<PendingPlayerControlCommand>,
    mut snapshot: ResMut<PlayerRuntimeSnapshot>,
    mut player: Query<
        (&mut KinematicCharacterController, &mut PlayerMotionState),
        With<ForgePlayerCharacter>,
    >,
    mut camera: Query<&mut PlayerCameraRig, With<ForgePlayerCamera>>,
) {
    let Ok((mut controller, mut motion)) = player.single_mut() else {
        return;
    };
    if !ready.0 {
        pending.cancel_input();
        freeze_player_controller(&mut controller, &mut motion, &mut snapshot);
        return;
    }
    let Ok(mut camera_rig) = camera.single_mut() else {
        return;
    };
    let (sequence, command) = pending.drain_tick();
    if command.toggle_camera {
        camera_rig.toggle_mode(&profile.0.camera);
    }
    camera_rig.apply_look(command.look_delta, &profile.0.camera);

    let delta_seconds = fixed_time.timestep().as_secs_f32();
    let horizontal_rotation = camera_rig.horizontal_rotation();
    let right = horizontal_rotation * Vec3::X;
    let forward = horizontal_rotation * Vec3::NEG_Z;
    let desired_direction =
        (right * command.move_axis.x + forward * command.move_axis.y).normalize_or_zero();
    let speed = profile.0.controller.walk_speed_mps
        * if command.sprint {
            profile.0.controller.sprint_multiplier
        } else {
            1.0
        };
    let target_horizontal_velocity = desired_direction * speed;
    let acceleration = if desired_direction == Vec3::ZERO {
        profile.0.controller.braking_mps2
    } else {
        profile.0.controller.acceleration_mps2
    };
    motion.horizontal_velocity = move_towards(
        motion.horizontal_velocity,
        target_horizontal_velocity,
        acceleration * delta_seconds,
    );

    if motion.grounded && motion.vertical_velocity_mps < 0.0 {
        motion.vertical_velocity_mps = 0.0;
    }
    if command.jump && motion.grounded {
        motion.vertical_velocity_mps = profile.0.controller.jump_speed_mps;
        motion.grounded = false;
    }
    motion.vertical_velocity_mps += profile.0.controller.gravity_mps2 * delta_seconds;

    let desired_translation = motion.horizontal_velocity * delta_seconds
        + Vec3::Y * motion.vertical_velocity_mps * delta_seconds;
    // Keep the downward collision probe, but do not accumulate Rapier's normal
    // nudge as lateral motion while resting on walkable ground. Restore sliding
    // for movement, jumping, falling, and a previously observed steep-slope slide.
    controller.slide = !motion.grounded
        || motion.horizontal_velocity != Vec3::ZERO
        || motion.vertical_velocity_mps > 0.0
        || snapshot.sliding_down_slope;
    controller.translation = Some(desired_translation);
    motion.fixed_tick = motion.fixed_tick.saturating_add(1);
    motion.last_desired_translation = desired_translation;

    snapshot.fixed_tick = motion.fixed_tick;
    if sequence.is_some() {
        snapshot.last_applied_sequence = sequence;
    }
    snapshot.desired_translation = desired_translation;
    snapshot.horizontal_velocity = motion.horizontal_velocity;
    snapshot.vertical_velocity_mps = motion.vertical_velocity_mps;
    snapshot.camera_mode = camera_rig.mode;
    snapshot.camera_yaw_radians = camera_rig.yaw_radians;
    snapshot.camera_pitch_radians = camera_rig.pitch_radians;
}

fn freeze_player_controller(
    controller: &mut KinematicCharacterController,
    motion: &mut PlayerMotionState,
    snapshot: &mut PlayerRuntimeSnapshot,
) {
    controller.translation = None;
    motion.horizontal_velocity = Vec3::ZERO;
    motion.vertical_velocity_mps = 0.0;
    motion.last_desired_translation = Vec3::ZERO;
    snapshot.desired_translation = Vec3::ZERO;
    snapshot.effective_translation = Vec3::ZERO;
    snapshot.horizontal_velocity = Vec3::ZERO;
    snapshot.vertical_velocity_mps = 0.0;
    snapshot.collisions = 0;
    snapshot.sliding_down_slope = false;
}

#[allow(
    clippy::needless_pass_by_value,
    clippy::type_complexity,
    reason = "the query keeps all backend identity observations in one post-writeback snapshot"
)]
fn observe_player_controller(
    ready: Res<PlayerSimulationReady>,
    mut bindings: ResMut<PlayerRuntimeBindings>,
    mut snapshot: ResMut<PlayerRuntimeSnapshot>,
    mut player: Query<
        (
            &Transform,
            &mut PlayerMotionState,
            Option<&KinematicCharacterControllerOutput>,
            Option<&RapierColliderHandle>,
            Option<&RapierContextEntityLink>,
        ),
        With<ForgePlayerCharacter>,
    >,
) {
    let Ok((transform, mut motion, output, collider_handle, context_link)) = player.single_mut()
    else {
        return;
    };
    let collider_handle = collider_handle.map(|handle| handle.0.into_raw_parts());
    let context_entity = context_link.map(|link| link.0);
    bindings.observe_backend(collider_handle, context_entity);

    if let Some(output) = output.filter(|_| ready.0) {
        motion.grounded = output.grounded;
        if output.grounded && motion.vertical_velocity_mps < 0.0 {
            motion.vertical_velocity_mps = 0.0;
        }
        snapshot.effective_translation = output.effective_translation;
        snapshot.grounded = output.grounded;
        snapshot.sliding_down_slope = output.is_sliding_down_slope;
        snapshot.collisions = output.collisions.len();
    }
    snapshot.player_position = transform.translation;
    snapshot.vertical_velocity_mps = motion.vertical_velocity_mps;
    snapshot.backend_identity_stable = bindings.backend_identity_stable;
}

#[allow(
    clippy::needless_pass_by_value,
    clippy::too_many_lines,
    clippy::too_many_arguments,
    clippy::type_complexity,
    reason = "the Bevy system atomically projects one player pose, Rapier query, and stable camera"
)]
fn update_player_camera(
    fixed_time: Res<Time<Fixed>>,
    profile: Res<PlayerRuntimeProfile>,
    bindings: Res<PlayerRuntimeBindings>,
    rapier: ReadRapierContext,
    mut snapshot: ResMut<PlayerRuntimeSnapshot>,
    tidewater: Option<ResMut<TidewaterPlayerState>>,
    mut player: Query<(&Transform, &mut Visibility), With<ForgePlayerCharacter>>,
    mut camera: Query<
        (&mut Transform, &mut Projection, &mut PlayerCameraRig),
        (With<ForgePlayerCamera>, Without<ForgePlayerCharacter>),
    >,
) {
    let Ok((player_transform, mut player_visibility)) = player.single_mut() else {
        return;
    };
    let Ok((mut camera_transform, mut projection, mut rig)) = camera.single_mut() else {
        return;
    };
    let controller_profile = &profile.0.controller;
    let mut effective_camera_profile = profile.0.camera;
    let mut player_center = player_transform.translation;
    let mut capsule_half_height =
        controller_profile.capsule_radius_m + controller_profile.capsule_half_segment_m;
    if let Some(mut state) = tidewater {
        player_center = state.motion.eye(fixed_time.timestep().as_secs_f32());
        capsule_half_height = 0.0;
        effective_camera_profile.eye_height_m = 0.0;
        effective_camera_profile.pivot_height_m = 0.0;
    }
    let camera_profile = &effective_camera_profile;

    match rig.mode {
        PlayerCameraMode::FirstPerson => {
            *player_visibility = Visibility::Hidden;
            rig.boom_distance_m = 0.0;
            let pose = first_person_pose(player_center, capsule_half_height, *rig, camera_profile);
            *camera_transform =
                Transform::from_translation(pose.translation).looking_to(pose.forward, Vec3::Y);
            set_perspective_fov(
                &mut projection,
                camera_profile
                    .first_person_vertical_fov_degrees
                    .to_radians(),
            );
            snapshot.camera_occluder = None;
            snapshot.camera_hit_time_of_impact_m = None;
            snapshot.camera_query_succeeded = false;
            snapshot.camera_vertical_fov_radians = camera_profile
                .first_person_vertical_fov_degrees
                .to_radians();
        }
        PlayerCameraMode::ThirdPerson => {
            *player_visibility = Visibility::Visible;
            let mut cast_rig = *rig;
            cast_rig.boom_distance_m = camera_profile.third_person_distance_m;
            let desired_pose =
                third_person_pose(player_center, capsule_half_height, cast_rig, camera_profile);
            let cast_vector = desired_pose.translation - desired_pose.pivot;
            let cast_distance = cast_vector.length();
            let mut collision_limit = None;
            let mut occluder = None;
            let mut hit_time_of_impact = None;
            let player_entity = bindings.player_entity;
            let camera_shape = Collider::ball(camera_profile.collision_radius_m);
            if let (Ok(context), Some(player_entity), Some(ball)) =
                (rapier.single(), player_entity, camera_shape.as_ball())
            {
                snapshot.camera_query_succeeded = true;
                let filter = QueryFilter::default()
                    .exclude_sensors()
                    .exclude_collider(player_entity);
                if let Some((entity, hit)) = context.cast_shape(
                    desired_pose.pivot,
                    Quat::IDENTITY,
                    cast_vector.normalize_or_zero(),
                    ball.raw,
                    ShapeCastOptions::with_max_time_of_impact(cast_distance),
                    filter,
                ) {
                    collision_limit =
                        Some((hit.time_of_impact - CAMERA_COLLISION_MARGIN_M).max(0.0));
                    occluder = Some(entity);
                    hit_time_of_impact = Some(hit.time_of_impact);
                }
            } else {
                snapshot.camera_query_succeeded = false;
            }
            rig.boom_distance_m = resolve_boom_distance(
                rig.boom_distance_m,
                camera_profile.third_person_distance_m,
                collision_limit,
                camera_profile.recovery_speed_mps,
                fixed_time.timestep().as_secs_f32(),
            );
            let pose = third_person_pose(player_center, capsule_half_height, *rig, camera_profile);
            *camera_transform =
                Transform::from_translation(pose.translation).looking_to(pose.forward, Vec3::Y);
            set_perspective_fov(
                &mut projection,
                camera_profile
                    .third_person_vertical_fov_degrees
                    .to_radians(),
            );
            snapshot.camera_occluder = occluder;
            snapshot.camera_hit_time_of_impact_m = hit_time_of_impact;
            snapshot.camera_vertical_fov_radians = camera_profile
                .third_person_vertical_fov_degrees
                .to_radians();
        }
    }

    snapshot.camera_mode = rig.mode;
    snapshot.camera_yaw_radians = rig.yaw_radians;
    snapshot.camera_pitch_radians = rig.pitch_radians;
    snapshot.camera_boom_distance_m = rig.boom_distance_m;
}

fn set_perspective_fov(projection: &mut Projection, fov_radians: f32) {
    if let Projection::Perspective(perspective) = projection {
        perspective.fov = fov_radians;
    }
}

fn normalize_move_axis(axis: Vec2) -> Vec2 {
    if axis.length_squared() > 1.0 {
        axis.normalize()
    } else {
        axis
    }
}

fn move_towards(current: Vec3, target: Vec3, maximum_delta: f32) -> Vec3 {
    let delta = target - current;
    let distance = delta.length();
    if distance <= maximum_delta || distance == 0.0 {
        target
    } else {
        current + delta / distance * maximum_delta
    }
}

#[cfg(test)]
mod tests {
    use super::*;
    use bevy::{time::TimeUpdateStrategy, transform::TransformPlugin};
    use bevy_rapier3d::prelude::{NoUserData, RapierPhysicsPlugin, TimestepMode};
    use forge_world::VisualValleyPlayerControllerRecipe;
    use std::time::Duration;

    #[test]
    fn input_normalizes_diagonals_and_keeps_one_shots_until_one_fixed_tick() {
        let mut pending = PendingPlayerControlCommand::default();
        pending
            .submit(PlayerControlCommand {
                move_axis: Vec2::ONE,
                jump: true,
                look_delta: Vec2::new(2.0, -3.0),
                toggle_camera: true,
                ..default()
            })
            .unwrap();

        let (first_sequence, first) = pending.drain_tick();
        let (second_sequence, second) = pending.drain_tick();
        assert_eq!(first_sequence, Some(1));
        assert_eq!(second_sequence, None);
        assert!((first.move_axis.length() - 1.0).abs() <= 1.0e-6);
        assert!(first.jump && first.toggle_camera);
        assert_eq!(first.look_delta, Vec2::new(2.0, -3.0));
        assert_eq!(second.move_axis, first.move_axis);
        assert!(!second.jump && !second.toggle_camera);
        assert_eq!(second.look_delta, Vec2::ZERO);
    }

    #[test]
    fn coalesced_batch_uses_high_sequence_last_axis_summed_look_and_toggle_parity() {
        let mut pending = PendingPlayerControlCommand::default();
        let first = pending
            .submit(PlayerControlCommand {
                move_axis: Vec2::X,
                jump: true,
                look_delta: Vec2::new(1.0, 2.0),
                toggle_camera: true,
                ..default()
            })
            .unwrap();
        let high = pending
            .submit(PlayerControlCommand {
                move_axis: Vec2::Y,
                sprint: true,
                look_delta: Vec2::new(3.0, 4.0),
                toggle_camera: true,
                ..default()
            })
            .unwrap();
        let (sequence, batch) = pending.drain_tick();

        assert_eq!(first, 1);
        assert_eq!(high, 2);
        assert_eq!(sequence, Some(high));
        assert_eq!(batch.move_axis, Vec2::Y);
        assert!(batch.sprint && batch.jump);
        assert_eq!(batch.look_delta, Vec2::new(4.0, 6.0));
        assert!(!batch.toggle_camera);
    }

    #[test]
    fn non_finite_probe_commands_are_rejected_without_mutating_held_state() {
        let mut pending = PendingPlayerControlCommand::default();
        pending
            .submit(PlayerControlCommand {
                move_axis: Vec2::X,
                ..default()
            })
            .unwrap();
        assert_eq!(
            pending.submit(PlayerControlCommand {
                move_axis: Vec2::new(f32::NAN, 0.0),
                ..default()
            }),
            Err(PlayerControlCommandError::NonFiniteMoveAxis)
        );
        assert_eq!(pending.drain_tick().1.move_axis, Vec2::X);
    }

    #[test]
    fn interactive_sampling_is_default_but_can_be_disabled_without_overwriting_probe_input() {
        let profile = test_profile();
        let mut disabled = test_app_with_interactive_input(profile.clone(), Vec3::ZERO, false);
        disabled.insert_resource(ButtonInput::<KeyCode>::default());
        let probe_sequence = submit(
            &mut disabled,
            PlayerControlCommand {
                move_axis: Vec2::Y,
                ..default()
            },
        );
        disabled
            .world_mut()
            .resource_mut::<ButtonInput<KeyCode>>()
            .press(KeyCode::KeyA);
        disabled.world_mut().run_schedule(PreUpdate);
        let (disabled_sequence, retained) = disabled
            .world_mut()
            .resource_mut::<PendingPlayerControlCommand>()
            .drain_tick();
        assert_eq!(disabled_sequence, Some(probe_sequence));
        assert_eq!(retained.move_axis, Vec2::Y);

        let mut interactive = test_app_with_interactive_input(profile, Vec3::ZERO, true);
        interactive.insert_resource(ButtonInput::<KeyCode>::default());
        let initial_sequence = submit(
            &mut interactive,
            PlayerControlCommand {
                move_axis: Vec2::Y,
                ..default()
            },
        );
        interactive
            .world_mut()
            .resource_mut::<ButtonInput<KeyCode>>()
            .press(KeyCode::KeyA);
        interactive.world_mut().run_schedule(PreUpdate);
        let (interactive_sequence, sampled) = interactive
            .world_mut()
            .resource_mut::<PendingPlayerControlCommand>()
            .drain_tick();
        assert_eq!(interactive_sequence, Some(initial_sequence + 1));
        assert_eq!(sampled.move_axis, Vec2::NEG_X);
    }

    #[test]
    fn interactive_sampling_keeps_a_same_frame_keyboard_tap_for_one_command() {
        let mut app = test_app_with_interactive_input(test_profile(), Vec3::ZERO, true);
        app.insert_resource(ButtonInput::<KeyCode>::default());
        let mut keyboard = app.world_mut().resource_mut::<ButtonInput<KeyCode>>();
        keyboard.press(KeyCode::KeyW);
        keyboard.release(KeyCode::KeyW);

        app.world_mut().run_schedule(PreUpdate);

        let (_, sampled) = app
            .world_mut()
            .resource_mut::<PendingPlayerControlCommand>()
            .drain_tick();
        assert_eq!(sampled.move_axis, Vec2::Y);
        assert!(!sampled.sprint && !sampled.jump && !sampled.toggle_camera);
    }

    #[test]
    fn losing_input_focus_cancels_held_and_queued_actions_without_resetting_sequence() {
        let mut app = test_app_with_interactive_input(test_profile(), Vec3::ZERO, true);
        app.insert_resource(ButtonInput::<KeyCode>::default());
        let initial = submit(
            &mut app,
            PlayerControlCommand {
                move_axis: Vec2::Y,
                sprint: true,
                jump: true,
                look_delta: Vec2::new(5.0, 3.0),
                toggle_camera: true,
            },
        );
        app.world_mut().resource_mut::<PlayerInputFocus>().0 = false;
        app.world_mut()
            .resource_mut::<ButtonInput<KeyCode>>()
            .press(KeyCode::KeyW);
        app.world_mut().run_schedule(PreUpdate);
        assert_eq!(
            app.world_mut()
                .resource_mut::<PendingPlayerControlCommand>()
                .drain_tick(),
            (None, PlayerControlCommand::default())
        );
        app.world_mut().resource_mut::<PlayerInputFocus>().0 = true;
        app.world_mut().run_schedule(PreUpdate);
        let (sequence, resumed) = app
            .world_mut()
            .resource_mut::<PendingPlayerControlCommand>()
            .drain_tick();
        assert_eq!(sequence, Some(initial + 1));
        assert_eq!(resumed.move_axis, Vec2::Y);
        assert!(!resumed.jump && !resumed.sprint && !resumed.toggle_camera);
        assert_eq!(resumed.look_delta, Vec2::ZERO);
    }

    #[test]
    fn acceleration_and_braking_have_bounded_exact_steps() {
        let accelerated = move_towards(Vec3::ZERO, Vec3::new(0.0, 0.0, -5.0), 0.4);
        assert!((accelerated.length() - 0.4).abs() <= 1.0e-6);
        let braked = move_towards(accelerated, Vec3::ZERO, 0.6);
        assert_eq!(braked, Vec3::ZERO);
    }

    #[test]
    fn runtime_snapshot_binds_the_exact_canonical_profile_hash() {
        let profile = test_profile();
        let expected = profile.content_hash().unwrap();
        assert_eq!(
            expected.to_string(),
            "941eceefb29835c0949a9801f83f421839bcb5b0cd79583899c22d6832a74f90"
        );
        let app = test_app(profile, Vec3::ZERO);

        assert_eq!(
            app.world()
                .resource::<PlayerRuntimeSnapshot>()
                .profile_content_hash,
            Some(expected)
        );
    }

    #[test]
    fn flat_kcc_moves_jumps_lands_and_keeps_backend_identities() {
        let profile = test_profile();
        let half_height = capsule_half_height(&profile);
        let mut app = test_app(profile, Vec3::new(0.0, half_height + 0.02, 0.0));
        spawn_box(
            &mut app,
            Vec3::new(0.0, -0.1, 0.0),
            Vec3::new(12.0, 0.1, 12.0),
            Quat::IDENTITY,
        );
        start_and_settle(&mut app);

        let initial_bindings = bindings(&app);
        let move_sequence = submit(
            &mut app,
            PlayerControlCommand {
                move_axis: Vec2::Y,
                ..default()
            },
        );
        run_fixed(&mut app, 30);
        let moved = snapshot(&app);
        assert_eq!(moved.last_applied_sequence, Some(move_sequence));
        assert!(moved.player_position.z < -1.0);
        assert!(moved.grounded);

        let jump_sequence = submit(
            &mut app,
            PlayerControlCommand {
                jump: true,
                ..default()
            },
        );
        run_fixed(&mut app, 1);
        let airborne = snapshot(&app);
        assert_eq!(airborne.last_applied_sequence, Some(jump_sequence));
        assert!(!airborne.grounded);
        assert!(airborne.vertical_velocity_mps > 0.0);
        run_fixed(&mut app, 90);
        let landed = snapshot(&app);
        assert!(landed.grounded);
        assert!(landed.player_position.y >= half_height - 0.02);

        let final_bindings = bindings(&app);
        assert_eq!(initial_bindings, final_bindings);
        assert!(final_bindings.rapier_collider_handle.is_some());
        assert!(final_bindings.rapier_context_entity.is_some());
        assert!(final_bindings.backend_identity_stable);
    }

    #[test]
    fn ecs_profile_wires_walk_sprint_and_bounded_braking() {
        let profile = test_profile();
        let walk_speed = profile.controller.walk_speed_mps;
        let sprint_speed = walk_speed * profile.controller.sprint_multiplier;
        let maximum_braking_step = profile.controller.braking_mps2 / 60.0;
        let half_height = capsule_half_height(&profile);
        let mut app = test_app(profile, Vec3::new(0.0, half_height + 0.02, 0.0));
        spawn_box(
            &mut app,
            Vec3::new(0.0, -0.1, 0.0),
            Vec3::new(12.0, 0.1, 12.0),
            Quat::IDENTITY,
        );
        start_and_settle(&mut app);

        submit(
            &mut app,
            PlayerControlCommand {
                move_axis: Vec2::Y,
                ..default()
            },
        );
        run_fixed(&mut app, 30);
        let walking = snapshot(&app).horizontal_velocity.length();
        assert!(walking <= walk_speed + 1.0e-5);
        assert!((walking - walk_speed).abs() <= 1.0e-4);

        submit(
            &mut app,
            PlayerControlCommand {
                move_axis: Vec2::Y,
                sprint: true,
                ..default()
            },
        );
        run_fixed(&mut app, 20);
        let sprinting = snapshot(&app).horizontal_velocity.length();
        assert!((sprinting - sprint_speed).abs() <= 1.0e-4);

        submit(&mut app, PlayerControlCommand::default());
        let mut previous = sprinting;
        for _ in 0..20 {
            run_fixed(&mut app, 1);
            let current = snapshot(&app).horizontal_velocity.length();
            assert!(current <= previous + 1.0e-6);
            assert!(previous - current <= maximum_braking_step + 1.0e-5);
            previous = current;
        }
        assert!(previous <= 1.0e-6);
    }

    #[test]
    fn low_step_passes_and_high_step_blocks_with_five_centimetre_margins() {
        let profile = test_profile();
        let threshold = profile.controller.max_step_height_m;
        let low = run_step_scenario(profile.clone(), threshold - 0.05);
        let high = run_step_scenario(profile, threshold + 0.05);

        assert!(
            low.player_position.z < -1.5,
            "low step did not pass: {low:?}"
        );
        assert!(
            low.player_position.y > 1.05,
            "low step was not climbed: {low:?}"
        );
        assert!(
            high.player_position.z > -1.0,
            "high step was crossed: {high:?}"
        );
        assert!(
            high.player_position.y < 1.05,
            "high step was climbed: {high:?}"
        );
    }

    #[test]
    fn low_slope_climbs_and_high_slope_blocks_with_six_degree_margins() {
        let profile = test_profile();
        let threshold = profile.controller.max_slope_climb_degrees;
        let low = run_slope_scenario(profile.clone(), threshold - 6.0);
        let high = run_slope_scenario(profile, threshold + 6.0);

        assert!(
            low.player_position.z < -1.5,
            "low slope did not pass: {low:?}"
        );
        assert!(
            low.player_position.y > 1.4,
            "low slope was not climbed: {low:?}"
        );
        assert!(
            high.player_position.z > -0.9,
            "high slope was climbed: {high:?}"
        );
        assert!(
            high.player_position.y < 1.2,
            "high slope gained height: {high:?}"
        );
    }

    #[test]
    fn slope_above_slide_threshold_reports_sliding_and_moves_downhill() {
        let profile = test_profile();
        let slope_degrees = profile.controller.min_slope_slide_degrees + 6.0;
        assert!(
            slope_degrees >= profile.controller.min_slope_slide_degrees + 5.0,
            "slide-threshold proof requires at least a five-degree margin"
        );
        let radians = slope_degrees.to_radians();
        let half_length = 5.0;
        let half_thickness = 0.1;
        let upslope_distance_m = 6.0;
        let surface_position = Vec3::new(
            0.0,
            upslope_distance_m * radians.sin(),
            -upslope_distance_m * radians.cos() + half_thickness * radians.sin(),
        );
        let spawn_position = surface_position + Vec3::Y * (capsule_half_height(&profile) + 0.02);
        let mut app = test_app(profile, spawn_position);
        let ramp_center = Vec3::new(
            0.0,
            half_length * radians.sin() - half_thickness * radians.cos(),
            -half_length * radians.cos(),
        );
        spawn_box(
            &mut app,
            ramp_center,
            Vec3::new(3.0, half_thickness, half_length),
            Quat::from_rotation_x(radians),
        );
        app.update();

        let mut saw_sliding_output = false;
        let mut lowest_y = spawn_position.y;
        let mut furthest_downhill_z = spawn_position.z;
        for _ in 0..45 {
            run_fixed(&mut app, 1);
            let observed = snapshot(&app);
            saw_sliding_output |= observed.sliding_down_slope;
            lowest_y = lowest_y.min(observed.player_position.y);
            furthest_downhill_z = furthest_downhill_z.max(observed.player_position.z);
        }

        assert!(
            saw_sliding_output,
            "Rapier never reported sliding above the configured threshold"
        );
        assert!(
            lowest_y < spawn_position.y - 0.10,
            "the player did not measurably lose altitude on the steep slope"
        );
        assert!(
            furthest_downhill_z > spawn_position.z + 0.10,
            "the player did not measurably move down the ramp"
        );
    }

    #[test]
    fn stationary_player_does_not_creep_on_walkable_slopes() {
        for degrees in [0.0_f32, 15.0, 30.0, -15.0] {
            let profile = test_profile();
            let half_height = capsule_half_height(&profile);
            let mut app = test_app(profile, Vec3::new(0.0, half_height + 0.1, 0.0));
            spawn_box(
                &mut app,
                Vec3::new(0.0, -0.1, 0.0),
                Vec3::new(10.0, 0.1, 10.0),
                Quat::from_rotation_x(degrees.to_radians()),
            );
            app.update();
            run_fixed(&mut app, 120);
            let origin = snapshot(&app).player_position;
            assert!(snapshot(&app).grounded);
            let mut maximum_drift = 0.0_f32;
            for _ in 0..1800 {
                run_fixed(&mut app, 1);
                let observed = snapshot(&app);
                assert_eq!(observed.horizontal_velocity, Vec3::ZERO);
                maximum_drift = maximum_drift.max(origin.distance(observed.player_position));
            }
            assert!(
                maximum_drift <= 0.02,
                "idle slope {degrees} degrees drifted {maximum_drift} m in 30 seconds"
            );
        }
    }

    #[test]
    fn camera_sphere_cast_closes_immediately_then_recovers_and_toggle_keeps_entity() {
        let profile = test_profile();
        let camera = profile.camera;
        let half_height = capsule_half_height(&profile);
        let mut app = test_app(profile, Vec3::new(0.0, half_height + 0.02, 0.0));
        spawn_box(
            &mut app,
            Vec3::new(0.0, -0.1, 0.0),
            Vec3::new(12.0, 0.1, 12.0),
            Quat::IDENTITY,
        );
        let wall = spawn_box(
            &mut app,
            Vec3::new(0.0, 1.2, 2.2),
            Vec3::new(3.0, 2.0, 0.1),
            Quat::IDENTITY,
        );
        start_and_settle(&mut app);

        let closed = snapshot(&app);
        let identity = bindings(&app);
        assert_eq!(closed.camera_occluder, Some(wall));
        assert!(closed.camera_query_succeeded);
        assert!(closed.camera_boom_distance_m < camera.third_person_distance_m);
        app.world_mut().despawn(wall);
        run_fixed(&mut app, 1);
        let recovering = snapshot(&app);
        assert!(recovering.camera_boom_distance_m > closed.camera_boom_distance_m);
        assert!(
            recovering.camera_boom_distance_m - closed.camera_boom_distance_m
                <= camera.recovery_speed_mps / 60.0 + 1.0e-5
        );

        submit(
            &mut app,
            PlayerControlCommand {
                toggle_camera: true,
                ..default()
            },
        );
        run_fixed(&mut app, 1);
        let first_person = snapshot(&app);
        assert_eq!(first_person.camera_mode, PlayerCameraMode::FirstPerson);
        assert_eq!(
            first_person.camera_boom_distance_m.to_bits(),
            0.0_f32.to_bits()
        );
        assert!(!first_person.camera_query_succeeded);
        assert_eq!(
            first_person.camera_vertical_fov_radians.to_bits(),
            camera
                .first_person_vertical_fov_degrees
                .to_radians()
                .to_bits()
        );
        assert_eq!(marker_counts(&mut app), (1, 1));
        assert!(app.world().get_entity(identity.player_entity).is_ok());
        assert!(app.world().get_entity(identity.camera_entity).is_ok());
        assert_eq!(
            app.world()
                .get::<Visibility>(identity.player_entity)
                .copied(),
            Some(Visibility::Hidden)
        );
        let player_y = app
            .world()
            .get::<Transform>(identity.player_entity)
            .unwrap()
            .translation
            .y;
        let camera_y = app
            .world()
            .get::<Transform>(identity.camera_entity)
            .unwrap()
            .translation
            .y;
        let expected_eye_y = player_y - half_height + camera.eye_height_m;
        assert!((camera_y - expected_eye_y).abs() <= 1.0e-5);

        submit(
            &mut app,
            PlayerControlCommand {
                toggle_camera: true,
                ..default()
            },
        );
        run_fixed(&mut app, 1);
        let third_person = snapshot(&app);
        assert_eq!(third_person.camera_mode, PlayerCameraMode::ThirdPerson);
        assert!(third_person.camera_boom_distance_m >= camera.third_person_min_distance_m);
        assert!(third_person.camera_boom_distance_m <= camera.third_person_distance_m);
        assert_eq!(identity.camera_entity, bindings(&app).camera_entity);
        assert_eq!(identity.player_entity, bindings(&app).player_entity);
        assert_eq!(marker_counts(&mut app), (1, 1));
        assert_eq!(
            app.world()
                .get::<Visibility>(identity.player_entity)
                .copied(),
            Some(Visibility::Visible)
        );
    }

    #[test]
    fn near_occluder_can_close_below_preferred_minimum_without_crossing_hit() {
        let profile = test_profile();
        let camera = profile.camera;
        let half_height = capsule_half_height(&profile);
        let mut app = test_app(profile, Vec3::new(0.0, half_height + 0.02, 0.0));
        spawn_box(
            &mut app,
            Vec3::new(0.0, -0.1, 0.0),
            Vec3::new(12.0, 0.1, 12.0),
            Quat::IDENTITY,
        );
        let wall = spawn_box(
            &mut app,
            Vec3::new(0.0, 1.65, 0.20),
            Vec3::new(3.0, 2.0, 0.05),
            Quat::IDENTITY,
        );
        app.update();
        let before = bindings(&app);
        run_fixed(&mut app, 4);
        let closed = snapshot(&app);
        let hit = closed
            .camera_hit_time_of_impact_m
            .expect("the close wall must be hit");

        assert_eq!(closed.camera_occluder, Some(wall));
        assert!(hit < camera.third_person_min_distance_m);
        assert!(closed.camera_boom_distance_m < camera.third_person_min_distance_m);
        assert!(
            closed.camera_boom_distance_m <= (hit - CAMERA_COLLISION_MARGIN_M).max(0.0) + 1.0e-5
        );
        assert!(closed.camera_boom_distance_m <= 1.0e-5);
        let camera_transform = app
            .world()
            .get::<Transform>(before.camera_entity)
            .expect("the bound camera entity must keep its transform");
        let actual_forward = camera_transform.rotation * Vec3::NEG_Z;
        let expected_forward = Quat::from_rotation_y(closed.camera_yaw_radians)
            * Quat::from_rotation_x(closed.camera_pitch_radians)
            * Vec3::NEG_Z;
        assert!(actual_forward.abs_diff_eq(expected_forward, 1.0e-5));
        assert_eq!(before.camera_entity, bindings(&app).camera_entity);
        assert_eq!(before.player_entity, bindings(&app).player_entity);
    }

    #[test]
    fn simulation_barrier_freezes_fall_and_discards_waiting_commands() {
        let spawn = Vec3::new(0.0, 10.0, 0.0);
        let mut app = test_app_with_interactive_input(test_profile(), spawn, false);
        app.insert_resource(PlayerSimulationReady(false));
        app.update();
        let initial = snapshot(&app);
        for _ in 0..8 {
            submit(
                &mut app,
                PlayerControlCommand {
                    move_axis: Vec2::Y,
                    jump: true,
                    look_delta: Vec2::splat(12.0),
                    toggle_camera: true,
                    ..default()
                },
            );
            app.update();
        }
        let waiting = snapshot(&app);
        assert_eq!(waiting.player_position, spawn);
        assert_eq!(waiting.fixed_tick, initial.fixed_tick);
        assert_eq!(waiting.last_applied_sequence, initial.last_applied_sequence);
        assert_eq!(waiting.camera_mode, initial.camera_mode);
        assert_eq!(
            waiting.camera_yaw_radians.to_bits(),
            initial.camera_yaw_radians.to_bits()
        );
        assert_eq!(waiting.vertical_velocity_mps.to_bits(), 0.0_f32.to_bits());
        app.world_mut().resource_mut::<PlayerSimulationReady>().0 = true;
        run_fixed(&mut app, 2);
        let resumed = snapshot(&app);
        assert!(resumed.player_position.y < spawn.y);
        assert_eq!(resumed.horizontal_velocity, Vec3::ZERO);
        assert_eq!(resumed.last_applied_sequence, initial.last_applied_sequence);
        assert_eq!(resumed.camera_mode, initial.camera_mode);
        app.world_mut().resource_mut::<PlayerSimulationReady>().0 = false;
        for _ in 0..4 {
            app.update();
        }
        let paused = snapshot(&app);
        assert_eq!(paused.player_position, resumed.player_position);
        assert_eq!(paused.fixed_tick, resumed.fixed_tick);
        assert_eq!(paused.effective_translation, Vec3::ZERO);
        assert_eq!(paused.vertical_velocity_mps.to_bits(), 0.0_f32.to_bits());
    }

    fn test_profile() -> PlayerControllerProfileDocument {
        VisualValleyPlayerControllerRecipe.build().unwrap()
    }

    fn capsule_half_height(profile: &PlayerControllerProfileDocument) -> f32 {
        profile.controller.capsule_radius_m + profile.controller.capsule_half_segment_m
    }

    #[test]
    fn initial_camera_target_sets_heading_without_changing_spawn_or_pitch() {
        let profile = forge_world::VisualValleyPlayerControllerRecipe
            .build()
            .unwrap();
        let spawn = Vec3::new(15.0, 4.0, -225.0);
        for (target, expected_forward) in [
            (Vec3::ZERO, Vec3::new(-15.0, 0.0, 225.0).normalize()),
            (spawn + Vec3::X * 10.0, Vec3::X),
            (spawn + Vec3::Y * 10.0, Vec3::NEG_Z),
        ] {
            let mut app = App::new();
            app.add_plugins(MinimalPlugins).add_plugins(
                PlayerControllerPlugin::new(profile.clone(), spawn)
                    .with_initial_camera_target(target)
                    .with_interactive_input(false),
            );
            app.world_mut().run_schedule(Startup);
            let state = snapshot(&app);
            let forward = Quat::from_rotation_y(state.camera_yaw_radians) * Vec3::NEG_Z;
            assert!((forward - expected_forward).length() < 0.0001);
            assert!((state.player_position - spawn).length() < 0.0001);
            assert!((state.camera_pitch_radians - (-12.0_f32).to_radians()).abs() < 0.0001);
        }
    }

    #[test]
    fn first_person_start_hides_capsule_and_sets_eye_projection() {
        let profile = test_profile();
        let spawn = Vec3::new(4.0, 8.0, -16.0);
        let mut app = App::new();
        app.add_plugins(MinimalPlugins).add_plugins(
            PlayerControllerPlugin::new(profile.clone(), spawn)
                .with_initial_camera_mode(PlayerCameraMode::FirstPerson)
                .with_initial_camera_pitch_degrees(12.0)
                .with_interactive_input(false),
        );
        app.world_mut().run_schedule(Startup);
        let bindings = bindings(&app);
        assert_eq!(snapshot(&app).camera_mode, PlayerCameraMode::FirstPerson);
        assert!((snapshot(&app).camera_pitch_radians - 12.0_f32.to_radians()).abs() < 1.0e-6);
        assert_eq!(
            app.world()
                .get::<Visibility>(bindings.player_entity)
                .unwrap(),
            &Visibility::Hidden
        );
        let Projection::Perspective(camera) = app
            .world()
            .get::<Projection>(bindings.camera_entity)
            .unwrap()
        else {
            panic!("expected perspective camera")
        };
        assert!(
            (camera.fov
                - profile
                    .camera
                    .first_person_vertical_fov_degrees
                    .to_radians())
            .abs()
                < 1.0e-6
        );
    }

    #[cfg(feature = "qualification")]
    #[test]
    fn fixed_proof_clock_preserves_camera_during_player_ticks() {
        let document = forge_world::embedded_visual_valley_world_sketch().unwrap();
        let mut app = test_app(test_profile(), Vec3::ZERO);
        app.add_plugins(
            crate::FixedProofViewPlugin::new(&document, &document.validation_cameras[0].id)
                .unwrap(),
        );
        app.update();
        let camera = bindings(&app).camera_entity;
        let pose = *app.world().get::<Transform>(camera).unwrap();
        let Projection::Perspective(initial) = app.world().get::<Projection>(camera).unwrap()
        else {
            panic!()
        };
        let optics = (initial.fov, initial.near, initial.far);
        for step in 1..=2 {
            assert!(
                app.world_mut()
                    .resource_mut::<crate::FixedProofClock>()
                    .request_step()
            );
            app.update();
            assert_eq!(*app.world().get::<Transform>(camera).unwrap(), pose);
            let Projection::Perspective(current) = app.world().get::<Projection>(camera).unwrap()
            else {
                panic!()
            };
            assert_eq!((current.fov, current.near, current.far), optics);
            assert_eq!(app.world().resource::<crate::FixedProofClock>().steps, step);
        }
        assert!(snapshot(&app).fixed_tick > 0);
        assert_eq!(
            app.world().resource::<Time<Virtual>>().elapsed(),
            Duration::from_millis(500)
        );
        // Removing diagnostic ownership restores the ordinary camera follower.
        app.world_mut()
            .remove_resource::<crate::FixedProofViewStatus>();
        app.world_mut().run_schedule(FixedUpdate);
        assert_ne!(*app.world().get::<Transform>(camera).unwrap(), pose);
    }

    fn test_app(profile: PlayerControllerProfileDocument, spawn_position: Vec3) -> App {
        test_app_with_interactive_input(profile, spawn_position, true)
    }

    fn test_app_with_interactive_input(
        profile: PlayerControllerProfileDocument,
        spawn_position: Vec3,
        interactive_input: bool,
    ) -> App {
        let mut app = App::new();
        app.add_plugins(MinimalPlugins)
            .add_plugins(TransformPlugin)
            .insert_resource(Time::<Fixed>::from_hz(60.0))
            .insert_resource(TimeUpdateStrategy::ManualDuration(Duration::from_secs_f64(
                1.0 / 60.0,
            )))
            .insert_resource(TimestepMode::Fixed {
                dt: 1.0 / 60.0,
                substeps: 1,
            })
            .add_plugins(RapierPhysicsPlugin::<NoUserData>::default().in_fixed_schedule())
            .add_plugins(
                PlayerControllerPlugin::new(profile, spawn_position)
                    .with_interactive_input(interactive_input),
            );
        app
    }

    fn spawn_box(app: &mut App, center: Vec3, half_extents: Vec3, rotation: Quat) -> Entity {
        app.world_mut()
            .spawn((
                Collider::cuboid(half_extents.x, half_extents.y, half_extents.z),
                Transform::from_translation(center).with_rotation(rotation),
            ))
            .id()
    }

    fn start_and_settle(app: &mut App) {
        app.update();
        run_fixed(app, 4);
        assert!(snapshot(app).grounded);
    }

    fn run_fixed(app: &mut App, ticks: usize) {
        for _ in 0..ticks {
            let before = snapshot(app).fixed_tick;
            app.update();
            let after = snapshot(app).fixed_tick;
            assert_eq!(
                after,
                before + 1,
                "one manually advanced app update must execute exactly one fixed player tick"
            );
        }
    }

    fn submit(app: &mut App, command: PlayerControlCommand) -> u64 {
        app.world_mut()
            .resource_mut::<PendingPlayerControlCommand>()
            .submit(command)
            .unwrap()
    }

    fn snapshot(app: &App) -> PlayerRuntimeSnapshot {
        app.world().resource::<PlayerRuntimeSnapshot>().clone()
    }

    fn bindings(app: &App) -> PlayerRuntimeBindingSnapshot {
        app.world()
            .resource::<PlayerRuntimeBindings>()
            .snapshot()
            .unwrap()
    }

    fn marker_counts(app: &mut App) -> (usize, usize) {
        let world = app.world_mut();
        let player_count = world
            .query_filtered::<Entity, With<ForgePlayerCharacter>>()
            .iter(world)
            .count();
        let camera_count = world
            .query_filtered::<Entity, With<ForgePlayerCamera>>()
            .iter(world)
            .count();
        (player_count, camera_count)
    }

    fn run_step_scenario(
        profile: PlayerControllerProfileDocument,
        step_height: f32,
    ) -> PlayerRuntimeSnapshot {
        let half_height = capsule_half_height(&profile);
        let mut app = test_app(profile, Vec3::new(0.0, half_height + 0.02, 0.0));
        spawn_box(
            &mut app,
            Vec3::new(0.0, -0.1, 1.0),
            Vec3::new(3.0, 0.1, 2.0),
            Quat::IDENTITY,
        );
        spawn_box(
            &mut app,
            Vec3::new(0.0, step_height * 0.5, -2.5),
            Vec3::new(3.0, step_height * 0.5, 1.5),
            Quat::IDENTITY,
        );
        start_and_settle(&mut app);
        submit(
            &mut app,
            PlayerControlCommand {
                move_axis: Vec2::Y,
                ..default()
            },
        );
        run_fixed(&mut app, 45);
        snapshot(&app)
    }

    fn run_slope_scenario(
        profile: PlayerControllerProfileDocument,
        slope_degrees: f32,
    ) -> PlayerRuntimeSnapshot {
        let half_height = capsule_half_height(&profile);
        let mut app = test_app(profile, Vec3::new(0.0, half_height + 0.02, 1.0));
        spawn_box(
            &mut app,
            Vec3::new(0.0, -0.1, 1.0),
            Vec3::new(3.0, 0.1, 1.0),
            Quat::IDENTITY,
        );
        let radians = slope_degrees.to_radians();
        let half_length = 5.0;
        let half_thickness = 0.1;
        let center = Vec3::new(
            0.0,
            half_length * radians.sin() - half_thickness * radians.cos(),
            -half_length * radians.cos(),
        );
        spawn_box(
            &mut app,
            center,
            Vec3::new(3.0, half_thickness, half_length),
            Quat::from_rotation_x(radians),
        );
        start_and_settle(&mut app);
        submit(
            &mut app,
            PlayerControlCommand {
                move_axis: Vec2::Y,
                ..default()
            },
        );
        run_fixed(&mut app, 60);
        snapshot(&app)
    }
}
