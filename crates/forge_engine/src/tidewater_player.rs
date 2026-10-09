//! Opt-in Tidewater locomotion on the existing Forge player/Rapier projection.
#![allow(clippy::needless_pass_by_value)] // Bevy system parameters are owned wrappers.
use super::{
    ForgePlayerCamera, ForgePlayerCharacter, PendingPlayerControlCommand, PlayerCameraMode,
    PlayerCameraRig, PlayerControllerSet, PlayerInputFocus, PlayerMotionState,
    PlayerRuntimeBindings, PlayerRuntimeProfile, PlayerRuntimeSnapshot, PlayerSimulationReady,
    freeze_player_controller, observe_player_controller, spawn_player_controller,
};
use crate::{
    tidewater_locomotion::{
        Input, LadderStep, Mode, RADIUS, SWIM_HEIGHT, State, TidewaterLadder, WALK_HEIGHT,
    },
    tidewater_water_query::Queries,
};
use bevy::{input::InputSystems, prelude::*};
use bevy_rapier3d::prelude::{
    CharacterAutostep, CharacterLength, Collider, KinematicCharacterController,
    KinematicCharacterControllerOutput, QueryFilter, QueryFilterFlags, RapierContext,
    ReadRapierContext,
};
use forge_world::sampled_terrain::SampledTerrainPlan;
use std::sync::Arc;

/// Held vertical input, separate from the generic controller's one-shot jump.
#[derive(Clone, Copy, Debug, Default, Resource)]
pub struct TidewaterPlayerInput {
    pub ascend: bool,
    pub descend: bool,
    pub control_descend: bool,
}

/// Runtime locomotion evidence; positions use the source foot/float anchor.
#[derive(Resource)]
pub struct TidewaterPlayerState {
    pub motion: State,
    pub water_query_step: u32,
    pub water_query_time: f64,
    pub water_query_position: [f32; 2],
    pub ground_height: f32,
    pub swim_entries: u64,
    pub walk_returns: u64,
    pub ladder_nearby: bool,
    pub ladder_climbing: bool,
    pub ladder_climbs: u64,
    water_query_slot: u32,
    collider_mode: Mode,
    backend_sync: BackendSync,
    climb_held: bool,
    rapier_contact: RapierContact,
    wall_candidates: Vec<Entity>,
    kcc_feedback_tick: u64,
    kcc_feedback_translation: [Vec3; 2],
    kcc_contacts: Vec<(Entity, Option<Vec3>)>,
}

#[derive(Default)]
struct RapierContact {
    grounded: bool,
    sliding_down_slope: bool,
}

#[derive(Clone, Copy, Debug, Default, PartialEq, Eq)]
enum BackendSync {
    #[default]
    Ready,
    Pending,
}

impl TidewaterPlayerState {
    /// Runtime-only witnesses from the last actual Rapier KCC output.
    #[must_use]
    pub fn kcc_contacts(&self) -> &[(Entity, Option<Vec3>)] {
        &self.kcc_contacts
    }

    /// Tick and requested/resolved translations belonging to these witnesses.
    #[must_use]
    pub const fn kcc_feedback(&self) -> (u64, [Vec3; 2]) {
        (self.kcc_feedback_tick, self.kcc_feedback_translation)
    }

    /// Last KCC ground contact before adaptation to source locomotion.
    #[must_use]
    pub const fn rapier_grounded(&self) -> bool {
        self.rapier_contact.grounded
    }

    /// Last KCC slope slide observation before source adaptation.
    #[must_use]
    pub const fn rapier_sliding_down_slope(&self) -> bool {
        self.rapier_contact.sliding_down_slope
    }

    /// Stable slot allocated by this player in the shared surface registry.
    #[must_use]
    pub const fn water_query_slot(&self) -> u32 {
        self.water_query_slot
    }

    pub(crate) const fn backend_sync_pending(&self) -> bool {
        matches!(self.backend_sync, BackendSync::Pending)
    }
}

#[derive(Resource)]
struct Surface {
    queries: Queries,
    slot: u32,
    terrain: Arc<SampledTerrainPlan>,
}

/// Installs source walk/swim rules without changing other Forge player sessions.
pub struct TidewaterPlayerPlugin {
    queries: Queries,
    slot: u32,
    terrain: Arc<SampledTerrainPlan>,
    interactive_input: bool,
    first_person: bool,
    ladders: Vec<TidewaterLadder>,
}

impl TidewaterPlayerPlugin {
    pub fn new(queries: Queries, terrain: Arc<SampledTerrainPlan>) -> Result<Self, String> {
        let slot = queries.allocate("player", 1)?;
        Ok(Self {
            queries,
            slot,
            terrain,
            interactive_input: true,
            first_person: true,
            ladders: Vec::new(),
        })
    }

    #[must_use]
    pub const fn with_interactive_input(mut self, enabled: bool) -> Self {
        self.interactive_input = enabled;
        self
    }

    #[must_use]
    pub const fn with_first_person(mut self, enabled: bool) -> Self {
        self.first_person = enabled;
        self
    }

    /// Supplies authored logical ladders independently from their visible meshes.
    #[must_use]
    pub fn with_ladders(mut self, ladders: impl IntoIterator<Item = TidewaterLadder>) -> Self {
        self.ladders = ladders.into_iter().collect();
        assert!(
            self.ladders.iter().all(|ladder| ladder.is_finite()),
            "TidewaterPlayerPlugin requires finite ladder coordinates"
        );
        self
    }
}

impl Plugin for TidewaterPlayerPlugin {
    fn build(&self, app: &mut App) {
        for ladder in self.ladders.iter().copied() {
            app.world_mut()
                .spawn((Name::new("Tidewater climbable ladder"), ladder));
        }
        app.insert_resource(Surface {
            queries: self.queries.clone(),
            slot: self.slot,
            terrain: self.terrain.clone(),
        })
        .insert_resource(TidewaterPlayerState {
            motion: State::new(Vec3::ZERO),
            water_query_step: 0,
            water_query_time: 0.0,
            water_query_position: [0.0; 2],
            ground_height: 0.0,
            rapier_contact: RapierContact::default(),
            wall_candidates: Vec::new(),
            kcc_feedback_tick: 0,
            kcc_feedback_translation: [Vec3::ZERO; 2],
            kcc_contacts: Vec::new(),
            swim_entries: 0,
            walk_returns: 0,
            ladder_nearby: false,
            ladder_climbing: false,
            ladder_climbs: 0,
            water_query_slot: self.slot,
            collider_mode: Mode::Walk,
            backend_sync: BackendSync::Ready,
            climb_held: false,
        })
        .init_resource::<TidewaterPlayerInput>()
        .insert_resource(InitialView(self.first_person))
        .add_systems(Startup, initialize.after(spawn_player_controller))
        .add_systems(FixedUpdate, drive.in_set(PlayerControllerSet::Drive))
        .add_systems(
            FixedUpdate,
            resolve
                .after(observe_player_controller)
                .in_set(PlayerControllerSet::Observe),
        );
        if self.interactive_input {
            app.add_systems(PreUpdate, sample_input.after(InputSystems));
        }
    }
}

#[derive(Resource)]
struct InitialView(bool);

fn initialize(
    player: Query<&Transform, With<ForgePlayerCharacter>>,
    mut camera: Query<&mut PlayerCameraRig, With<ForgePlayerCamera>>,
    initial_view: Res<InitialView>,
    surface: Res<Surface>,
    mut state: ResMut<TidewaterPlayerState>,
) {
    if let Ok(transform) = player.single() {
        state.motion = State::new(transform.translation - Vec3::Y * (WALK_HEIGHT * 0.5));
        surface
            .queries
            .set_point(
                surface.slot,
                transform.translation.x,
                transform.translation.z,
            )
            .expect("finite Tidewater player spawn query");
    }
    if let Ok(mut rig) = camera.single_mut() {
        rig.mode = if initial_view.0 {
            PlayerCameraMode::FirstPerson
        } else {
            PlayerCameraMode::ThirdPerson
        };
        rig.pitch_radians = 0.0;
    }
}

fn sample_input(
    keyboard: Option<Res<ButtonInput<KeyCode>>>,
    focus: Res<PlayerInputFocus>,
    ready: Res<PlayerSimulationReady>,
    mut input: ResMut<TidewaterPlayerInput>,
) {
    *input = TidewaterPlayerInput::default();
    if let Some(keyboard) = keyboard.filter(|_| focus.0 && ready.0) {
        input.ascend = keyboard.pressed(KeyCode::Space);
        input.descend = keyboard.pressed(KeyCode::KeyC);
        input.control_descend = keyboard.pressed(KeyCode::ControlLeft);
    }
}

#[allow(clippy::too_many_arguments, clippy::type_complexity)]
fn drive(
    time: Res<Time<Fixed>>,
    ready: Res<PlayerSimulationReady>,
    profile: Res<PlayerRuntimeProfile>,
    surface: Res<Surface>,
    bindings: Res<PlayerRuntimeBindings>,
    rapier: ReadRapierContext,
    vertical: Res<TidewaterPlayerInput>,
    mut state: ResMut<TidewaterPlayerState>,
    mut pending: ResMut<PendingPlayerControlCommand>,
    mut snapshot: ResMut<PlayerRuntimeSnapshot>,
    mut player: Query<
        (
            &Transform,
            &mut KinematicCharacterController,
            &mut PlayerMotionState,
        ),
        With<ForgePlayerCharacter>,
    >,
    mut camera: Query<&mut PlayerCameraRig, With<ForgePlayerCamera>>,
) {
    let Ok((transform, mut controller, mut motion)) = player.single_mut() else {
        return;
    };
    if !ready.0 {
        pending.cancel_input();
        state.motion.velocity = Vec3::ZERO;
        state.wall_candidates.clear();
        freeze_player_controller(&mut controller, &mut motion, &mut snapshot);
        return;
    }
    let Ok(mut camera) = camera.single_mut() else {
        return;
    };
    let raw_axis = pending.held_raw_move_axis;
    let (sequence, command) = pending.drain_tick();
    if command.toggle_camera {
        camera.toggle_mode(&profile.0.camera);
    }
    camera.apply_look(command.look_delta, &profile.0.camera);
    if let Some(batch) = surface.queries.latest()
        && let Some(sample) = batch.values.get(surface.slot as usize)
    {
        state.motion.water_h = sample.height;
        state.water_query_step = batch.step;
        state.water_query_time = batch.time;
        state.water_query_position = batch.points[surface.slot as usize];
    }
    if state.backend_sync_pending() {
        state.wall_candidates.clear();
        freeze_player_controller(&mut controller, &mut motion, &mut snapshot);
        motion.fixed_tick = motion.fixed_tick.saturating_add(1);
        snapshot.fixed_tick = motion.fixed_tick;
        if sequence.is_some() {
            snapshot.last_applied_sequence = sequence;
        }
        snapshot.camera_mode = camera.mode;
        snapshot.camera_yaw_radians = camera.yaw_radians;
        snapshot.camera_pitch_radians = camera.pitch_radians;
        return;
    }
    let height = state.motion.water_h;
    state.climb_held = vertical.ascend || raw_axis.y > 0.0;
    let desired_anchor = if state.ladder_climbing && state.climb_held {
        state.motion.position
    } else {
        state.motion.advance(
            time.timestep().as_secs_f32(),
            Input {
                move_axis: raw_axis,
                yaw: camera.yaw_radians,
                pitch: camera.pitch_radians,
                sprint: command.sprint,
                jump: command.jump,
                ascend: vertical.ascend,
                descend: vertical.descend,
                control_descend: vertical.control_descend,
            },
            height,
        )
    };
    let mut desired =
        desired_anchor + Vec3::Y * half_height(state.collider_mode) - transform.translation;
    motion.horizontal_velocity = state.motion.velocity.with_y(0.0);
    motion.vertical_velocity_mps = state.motion.velocity.y;
    motion.grounded = state.motion.mode == Mode::Walk && state.motion.grounded;
    controller.slide = !motion.grounded
        || motion.horizontal_velocity != Vec3::ZERO
        || motion.vertical_velocity_mps > 0.0
        || snapshot.sliding_down_slope;
    desired = supported_walk_drive(
        &mut state,
        &mut controller,
        &rapier,
        bindings.player_entity,
        desired,
    );
    controller.translation = Some(desired);
    motion.fixed_tick = motion.fixed_tick.saturating_add(1);
    motion.last_desired_translation = desired;
    snapshot.fixed_tick = motion.fixed_tick;
    if sequence.is_some() {
        snapshot.last_applied_sequence = sequence;
    }
    snapshot.desired_translation = desired;
    snapshot.horizontal_velocity = motion.horizontal_velocity;
    snapshot.vertical_velocity_mps = motion.vertical_velocity_mps;
    snapshot.camera_mode = camera.mode;
    snapshot.camera_yaw_radians = camera.yaw_radians;
    snapshot.camera_pitch_radians = camera.pitch_radians;
}

#[allow(
    clippy::too_many_arguments,
    clippy::type_complexity,
    clippy::cast_possible_truncation
)]
fn resolve(
    time: Res<Time<Fixed>>,
    ready: Res<PlayerSimulationReady>,
    surface: Res<Surface>,
    profile: Res<PlayerRuntimeProfile>,
    bindings: Res<PlayerRuntimeBindings>,
    rapier: ReadRapierContext,
    mut state: ResMut<TidewaterPlayerState>,
    mut snapshot: ResMut<PlayerRuntimeSnapshot>,
    mut meshes: Option<ResMut<Assets<Mesh>>>,
    ladders: Query<&TidewaterLadder>,
    mut player: Query<
        (
            &mut Transform,
            &mut Collider,
            &mut KinematicCharacterController,
            &mut PlayerMotionState,
            Option<&KinematicCharacterControllerOutput>,
        ),
        With<ForgePlayerCharacter>,
    >,
) {
    if !ready.0 {
        return;
    }
    let Ok((mut transform, mut collider, mut controller, mut motion, output)) = player.single_mut()
    else {
        return;
    };
    if finish_backend_sync(&mut state, &transform, &mut motion, &mut snapshot) {
        state.kcc_contacts.clear();
        return;
    }
    observe_kcc_contacts(&mut state, motion.fixed_tick, output);
    state.rapier_contact.grounded = motion.grounded;
    state.rapier_contact.sliding_down_slope = snapshot.sliding_down_slope;
    let anchor = transform.translation - Vec3::Y * half_height(state.collider_mode);
    let Ok(terrain_ground) = surface
        .terrain
        .height_at(f64::from(anchor.x), f64::from(anchor.z))
    else {
        error!("Tidewater player left the canonical terrain bounds");
        return;
    };
    let (ground, capsule_support) = resolve_ground_support(
        &rapier,
        bindings.player_entity,
        anchor,
        terrain_ground as f32,
        state.motion.mode,
        controller.min_slope_slide_angle,
    );
    // Preserve a collision-resolved ascent while the controller is grounded.
    // Lowering these intermediate poses to the terrain ray prevents successive
    // autostep sweeps from reaching the tread above the capsule's centre ray.
    let collision_rise = resolved_collision_rise(output, controller.normal_nudge_factor);
    let (anchor, support) = walking_support(
        &state.motion,
        &motion,
        anchor,
        ground,
        capsule_support,
        collision_rise,
    );
    let before = state.motion.mode;
    state.motion.resolve(anchor, support);
    remember_grounded_wall_candidates(&mut state, &controller, output);
    state.ground_height = ground;
    state.ladder_nearby = false;
    state.ladder_climbing = false;
    if before == Mode::Swim {
        let climb_held = state.climb_held;
        for ladder in &ladders {
            let step =
                state
                    .motion
                    .climb_ladder(time.timestep().as_secs_f32(), climb_held, *ladder);
            if step != LadderStep::OutOfRange {
                state.ladder_nearby = true;
                state.ladder_climbing = step == LadderStep::Climbing;
                if step == LadderStep::Completed {
                    state.ladder_climbs += 1;
                    state.ground_height = ladder.exit_anchor.y;
                }
                break;
            }
        }
    }
    let after = state.motion.mode;
    if before != after {
        match after {
            Mode::Swim => state.swim_entries += 1,
            Mode::Walk => state.walk_returns += 1,
        }
        *collider = Collider::capsule_y(half_height(after) - RADIUS, RADIUS);
        controller.autostep = (after == Mode::Walk).then_some(CharacterAutostep {
            max_height: CharacterLength::Absolute(0.4),
            min_width: CharacterLength::Absolute(profile.0.controller.min_step_width_m),
            include_dynamic_bodies: false,
        });
        controller.snap_to_ground = (after == Mode::Walk).then_some(CharacterLength::Absolute(
            profile.0.controller.ground_snap_m,
        ));
        if let Some(assets) = meshes.as_deref_mut()
            && let Some(handle) = &bindings.mesh_handle
            && let Some(mut mesh) = assets.get_mut(handle)
        {
            *mesh = Capsule3d::new(RADIUS, 2.0 * (half_height(after) - RADIUS)).into();
        }
        state.collider_mode = after;
        // Rapier runs its KCC before copying changed Bevy transforms and collider
        // shapes into the backend. Hold the next KCC tick so that copy completes
        // before another sweep starts from the teleported/resized capsule.
        state.backend_sync = BackendSync::Pending;
    }
    transform.translation = state.motion.position + Vec3::Y * half_height(after);
    motion.grounded = after == Mode::Walk && state.motion.grounded;
    motion.vertical_velocity_mps = state.motion.velocity.y;
    motion.horizontal_velocity = state.motion.velocity.with_y(0.0);
    snapshot.player_position = transform.translation;
    snapshot.grounded = motion.grounded;
    snapshot.vertical_velocity_mps = motion.vertical_velocity_mps;
    snapshot.horizontal_velocity = motion.horizontal_velocity;
}

fn resolve_ground_support(
    rapier: &ReadRapierContext,
    player_entity: Option<Entity>,
    anchor: Vec3,
    terrain_ground: f32,
    mode: Mode,
    min_slope_slide_angle: f32,
) -> (f32, bool) {
    let mut ground = terrain_ground;
    let mut capsule_support = false;
    if let (Ok(context), Some(entity)) = (rapier.single(), player_entity) {
        let margin = if mode == Mode::Walk { 0.45 } else { 0.3 };
        let origin = anchor + Vec3::Y * margin;
        if let Some((_, hit)) = context.cast_ray_and_get_normal(
            origin,
            Vec3::NEG_Y,
            4096.0,
            true,
            QueryFilter::default()
                .exclude_sensors()
                .exclude_collider(entity),
        ) {
            ground = ground.max(hit.point.y);
            capsule_support = hit.time_of_impact > 0.0
                && nearby_walkable_support(anchor, hit.point, hit.normal, min_slope_slide_angle);
        }
    }
    (ground, capsule_support)
}

fn resolved_collision_rise(
    output: Option<&KinematicCharacterControllerOutput>,
    normal_nudge_factor: f32,
) -> bool {
    output.is_some_and(|output| {
        let contacts = u16::try_from(output.collisions.len()).unwrap_or(u16::MAX);
        let noise = normal_nudge_factor * (f32::from(contacts) + 1.0);
        output.effective_translation.y > output.desired_translation.y.max(0.0) + noise
    })
}

fn remember_grounded_wall_candidates(
    state: &mut TidewaterPlayerState,
    controller: &KinematicCharacterController,
    output: Option<&KinematicCharacterControllerOutput>,
) {
    if state.motion.mode == Mode::Walk && state.motion.grounded && state.motion.velocity.y <= 0.0 {
        if let Some(output) = output {
            remember_wall_candidates(&mut state.wall_candidates, output, controller);
        }
    } else {
        state.wall_candidates.clear();
    }
}

fn walking_support(
    source: &State,
    motion: &PlayerMotionState,
    mut anchor: Vec3,
    ground: f32,
    capsule_support: bool,
    collision_rise: bool,
) -> (Vec3, f32) {
    // Normalize the flat contact skin to source feet. On slopes a capsule needs
    // more vertical clearance, so keep its collision-resolved anchor instead.
    let grounded_walk = source.mode == Mode::Walk && motion.grounded && source.velocity.y <= 0.0;
    if grounded_walk && !collision_rise && (anchor.y - ground).abs() < 0.06 {
        anchor.y = ground;
    }
    // Rapier's slide flag also includes intentional movement on walkable slopes.
    // Keep grounded contact only when its nearby backend surface is nonslip,
    // without lowering the capsule to point feet. Preserve ray/terrain height
    // separately for water depth and diagnostics.
    let support = if grounded_walk && capsule_support {
        anchor.y
    } else {
        ground
    };
    (anchor, support)
}

fn nearby_walkable_support(
    anchor: Vec3,
    point: Vec3,
    normal: Vec3,
    min_slope_slide_angle: f32,
) -> bool {
    let normal_up = normal.dot(Vec3::Y);
    // The lower capsule sphere needs clearance on a slope. Measure the actual
    // plane separation along its backend normal, using the existing 6 cm foot
    // tolerance; a distant ray floor or quasi-wall must not fabricate support.
    let normal_clearance = (anchor.y - point.y + RADIUS) * normal_up - RADIUS;
    normal_up >= min_slope_slide_angle.cos() && normal_clearance.abs() < 0.06
}

fn observe_kcc_contacts(
    state: &mut TidewaterPlayerState,
    tick: u64,
    output: Option<&KinematicCharacterControllerOutput>,
) {
    state.kcc_contacts.clear();
    state.kcc_feedback_tick = tick;
    state.kcc_feedback_translation = output.map_or([Vec3::ZERO; 2], |output| {
        [output.desired_translation, output.effective_translation]
    });
    if let Some(output) = output {
        state
            .kcc_contacts
            .extend(output.collisions.iter().map(|collision| {
                (
                    collision.entity,
                    collision.hit.details.map(|hit| hit.normal1),
                )
            }));
    }
}

fn remember_wall_candidates(
    candidates: &mut Vec<Entity>,
    output: &KinematicCharacterControllerOutput,
    controller: &KinematicCharacterController,
) {
    // Rapier limits a character sweep to twenty collisions. Keep identifiers
    // only; a fresh backend cast must validate every witness before it is used.
    for collision in &output.collisions {
        if let Some(hit) = collision.hit.details
            && hit.normal1.dot(controller.up).abs() <= controller.max_slope_climb_angle.cos()
            && !candidates.contains(&collision.entity)
            && candidates.len() < 20
        {
            candidates.push(collision.entity);
        }
    }
}

fn supported_walk_drive(
    state: &mut TidewaterPlayerState,
    controller: &mut KinematicCharacterController,
    rapier: &ReadRapierContext,
    entity: Option<Entity>,
    desired: Vec3,
) -> Vec3 {
    if state.motion.mode == Mode::Walk
        && state.motion.grounded
        && state.motion.velocity.y <= 0.0
        && !state.ladder_climbing
        && desired.with_y(0.0).length_squared() > 1.0e-10
        && controller.custom_shape.is_none()
        && let (Ok(context), Some(entity)) = (rapier.single(), entity)
    {
        let constrained = constrain_wall_drive(
            &context,
            entity,
            controller,
            desired,
            &mut state.wall_candidates,
        );
        if constrained.with_y(0.0).length_squared() <= 1.0e-10 {
            // Once every horizontal component is physically blocked, this is
            // an idle grounded sweep. Stop continuation after its floor hit,
            // as the ordinary idle controller does, retaining gravity/snap.
            controller.slide = false;
        }
        constrained
    } else {
        state.wall_candidates.clear();
        desired
    }
}

fn player_query_filter(
    context: &RapierContext<'_>,
    controller: &KinematicCharacterController,
    collider: &bevy_rapier3d::rapier::prelude::Collider,
    entity: Entity,
) -> QueryFilter<'static> {
    let mut filter = QueryFilter {
        flags: controller.filter_flags,
        groups: controller.filter_groups,
        ..default()
    }
    .exclude_collider(entity);
    if let Some(parent) = collider.parent()
        && let Some(parent_entity) = context.rigidbody_set.rigid_body_entity(parent)
    {
        filter = filter.exclude_rigid_body(parent_entity);
    }
    filter
}

fn wall_drive_collider<'a>(
    context: &'a RapierContext<'_>,
    entity: Entity,
    candidates: &mut Vec<Entity>,
) -> Option<&'a bevy_rapier3d::rapier::prelude::Collider> {
    let collider = context
        .colliders
        .entity2collider()
        .get(&entity)
        .and_then(|handle| context.colliders.colliders.get(*handle));
    if collider.is_none() {
        candidates.clear();
    }
    collider
}

fn constrain_wall_drive(
    context: &RapierContext<'_>,
    entity: Entity,
    controller: &KinematicCharacterController,
    desired: Vec3,
    candidates: &mut Vec<Entity>,
) -> Vec3 {
    if candidates.is_empty() {
        return desired;
    }
    let Some(collider) = wall_drive_collider(context, entity, candidates) else {
        return desired;
    };
    let horizontal = desired.with_y(0.0);
    let mut normals = Vec::new();
    let base_filter = player_query_filter(context, controller, collider, entity);
    #[cfg(test)]
    let mut timer = tests::WallGuardTimer::new();
    candidates.retain(|target| {
        #[cfg(test)]
        timer.cast();
        if let Some(normal) = cast_wall_normal(
            context,
            controller,
            base_filter,
            collider,
            *target,
            Vec3::ZERO,
            horizontal,
        ) {
            normals.push((*target, normal));
            true
        } else {
            false
        }
    });
    if normals.is_empty() {
        return desired;
    }
    #[cfg(test)]
    timer.preflight();
    let (translation, grounded, noise, blocking_contacts) =
        predict_wall_drive(context, controller, desired, base_filter, collider, true);
    if !grounded {
        return desired;
    }
    // A nearby future wall cannot veto an already accomplished demand.
    // If progress is partial, compare the actual raw KCC with stairs disabled:
    // a genuine autostep must contribute both upward and forward movement.
    let mut constrained = desired;
    let mut without_stairs = None;
    for (target, normal) in normals {
        // A fresh short cast may see the border of a neighbouring heightfield
        // while the actual sweep resolves only walkable support. Attribute
        // missing progress to a wall only when that sweep hit the same collider
        // with an opposing, non-walkable normal.
        if !blocking_contacts.contains(&target) {
            continue;
        }
        let demand = -desired.with_y(0.0).dot(normal);
        let progress = -translation.with_y(0.0).dot(normal);
        if progress >= demand - noise {
            continue;
        }
        #[cfg(test)]
        timer.cast();
        if cast_wall_normal(
            context,
            controller,
            base_filter,
            collider,
            target,
            translation,
            horizontal,
        )
        .is_none()
        {
            continue;
        }
        if controller.autostep.is_some() {
            let control = *without_stairs.get_or_insert_with(|| {
                #[cfg(test)]
                timer.preflight();
                predict_wall_drive(context, controller, desired, base_filter, collider, false).0
            });
            if translation.y > control.y + noise
                && (progress > -control.with_y(0.0).dot(normal) + noise
                    || predicts_bounded_step_recovery(
                        context,
                        controller,
                        base_filter,
                        collider,
                        desired,
                        normal,
                        noise,
                    ))
            {
                continue;
            }
        }
        let entering = constrained.with_y(0.0).dot(normal);
        if entering < 0.0 {
            constrained -= normal * entering;
        }
    }
    if constrained.with_y(0.0).length_squared() > 1.0e-10 && constrained != desired {
        #[cfg(test)]
        timer.cast();
        constrained =
            supported_tangent_drive(context, controller, base_filter, collider, constrained);
    }
    constrained
}

fn supported_tangent_drive(
    context: &RapierContext<'_>,
    controller: &KinematicCharacterController,
    filter: QueryFilter,
    collider: &bevy_rapier3d::rapier::prelude::Collider,
    desired: Vec3,
) -> Vec3 {
    let anchor = collider.position().translation - Vec3::Y * half_height(Mode::Walk);
    let Some((_, hit)) = context.cast_ray_and_get_normal(
        anchor + Vec3::Y * 0.45,
        Vec3::NEG_Y,
        4096.0,
        true,
        filter.exclude_sensors(),
    ) else {
        return desired;
    };
    if hit.time_of_impact <= 0.0
        || !nearby_walkable_support(
            anchor,
            hit.point,
            hit.normal,
            controller.min_slope_slide_angle,
        )
    {
        return desired;
    }
    // Supported gravity is balanced by the current nonslip contact. Request
    // the surviving tangent in that real plane; the KCC still owns every pose
    // and may lose support or snap to a lower floor during its actual sweep.
    desired.with_y(-hit.normal.dot(desired.with_y(0.0)) / hit.normal.y)
}

fn cast_wall_normal(
    context: &RapierContext<'_>,
    controller: &KinematicCharacterController,
    base_filter: QueryFilter,
    collider: &bevy_rapier3d::rapier::prelude::Collider,
    target: Entity,
    shift: Vec3,
    horizontal: Vec3,
) -> Option<Vec3> {
    let shape = collider.shape();
    let position =
        bevy_rapier3d::rapier::math::Pose::from_translation(shift) * *collider.position();
    let up_extent = shape
        .compute_local_aabb()
        .extents()
        .dot(controller.up.abs());
    let offset = match controller.offset {
        CharacterLength::Absolute(value) => value,
        CharacterLength::Relative(value) => value * up_extent,
    };
    let predicate = |candidate| candidate == target;
    let filter = QueryFilter {
        flags: base_filter.flags | QueryFilterFlags::EXCLUDE_DYNAMIC,
        ..base_filter
    }
    .exclude_sensors()
    .predicate(&predicate);
    let (_, hit) = context.with_query_pipeline(filter, |pipeline| {
        pipeline.query_pipeline.cast_shape(
            &position,
            horizontal.normalize(),
            shape,
            bevy_rapier3d::prelude::ShapeCastOptions {
                max_time_of_impact: 0.05,
                target_distance: offset,
                stop_at_penetration: false,
                compute_impact_geometry_on_penetration: true,
            },
        )
    })?;
    (hit.normal1.dot(controller.up).abs() <= controller.max_slope_climb_angle.cos()
        && hit.normal1.with_y(0.0).dot(horizontal) < 0.0)
        .then(|| hit.normal1.with_y(0.0).normalize())
}

fn predict_wall_drive(
    context: &RapierContext<'_>,
    controller: &KinematicCharacterController,
    desired: Vec3,
    filter: QueryFilter,
    collider: &bevy_rapier3d::rapier::prelude::Collider,
    stairs: bool,
) -> (Vec3, bool, f32, Vec<Entity>) {
    let raw = bevy_rapier3d::rapier::control::KinematicCharacterController {
        up: controller.up.normalize(),
        offset: controller.offset,
        slide: controller.slide,
        autostep: if stairs { controller.autostep } else { None },
        max_slope_climb_angle: controller.max_slope_climb_angle,
        min_slope_slide_angle: controller.min_slope_slide_angle,
        snap_to_ground: controller.snap_to_ground,
        normal_nudge_factor: controller.normal_nudge_factor,
    };
    let mut collisions = 0_u8;
    let mut blocking_contacts = Vec::new();
    #[cfg(test)]
    let mut observed = Vec::new();
    let predicted = context.with_query_pipeline(filter, |pipeline| {
        raw.move_shape(
            context.simulation.integration_parameters.dt,
            &pipeline.query_pipeline,
            collider.shape(),
            collider.position(),
            desired,
            |collision| {
                #[cfg(not(test))]
                let _ = collision;
                collisions = collisions.saturating_add(1);
                let normal = collision.hit.normal1;
                if normal.dot(controller.up).abs() <= controller.max_slope_climb_angle.cos()
                    && normal.with_y(0.0).dot(desired.with_y(0.0)) < 0.0
                    && let Some(entity) = context.colliders.collider_entity(collision.handle)
                    && !blocking_contacts.contains(&entity)
                {
                    blocking_contacts.push(entity);
                }
                #[cfg(test)]
                observed.push((
                    context.colliders.collider_entity(collision.handle),
                    collision.hit.normal1,
                    collision.translation_applied,
                    collision.translation_remaining,
                ));
            },
        )
    });
    #[cfg(test)]
    if std::env::var_os("FORGE_WALL_DIAGNOSTIC").is_some() {
        eprintln!(
            "wall preflight stairs={stairs}, desired={desired:?}, effective={:?}, grounded={}, collisions={observed:?}",
            predicted.translation, predicted.grounded
        );
    }
    let noise = controller.normal_nudge_factor * (f32::from(collisions) + 1.0);
    (
        predicted.translation,
        predicted.grounded,
        noise,
        blocking_contacts,
    )
}

fn finish_backend_sync(
    state: &mut TidewaterPlayerState,
    transform: &Transform,
    motion: &mut PlayerMotionState,
    snapshot: &mut PlayerRuntimeSnapshot,
) -> bool {
    if !state.backend_sync_pending() {
        return false;
    }
    state.backend_sync = BackendSync::Ready;
    motion.grounded = state.motion.mode == Mode::Walk && state.motion.grounded;
    motion.vertical_velocity_mps = state.motion.velocity.y;
    motion.horizontal_velocity = state.motion.velocity.with_y(0.0);
    snapshot.player_position = transform.translation;
    snapshot.desired_translation = Vec3::ZERO;
    snapshot.effective_translation = Vec3::ZERO;
    snapshot.grounded = motion.grounded;
    snapshot.vertical_velocity_mps = motion.vertical_velocity_mps;
    snapshot.horizontal_velocity = motion.horizontal_velocity;
    snapshot.collisions = 0;
    snapshot.sliding_down_slope = false;
    true
}

const fn half_height(mode: Mode) -> f32 {
    match mode {
        Mode::Walk => WALK_HEIGHT * 0.5,
        Mode::Swim => SWIM_HEIGHT * 0.5,
    }
}

#[cfg(test)]
#[path = "tidewater_player_tests.rs"]
mod tests;

// A convex step may start with a small backwards ascent before crossing its
// rounded capsule contact. Predict only a bounded, grounded, height-limited
// continuation; Rapier still owns every actual pose and contact.
fn predicts_bounded_step_recovery(
    context: &RapierContext<'_>,
    controller: &KinematicCharacterController,
    filter: QueryFilter,
    collider: &bevy_rapier3d::rapier::prelude::Collider,
    desired: Vec3,
    normal: Vec3,
    initial_noise: f32,
) -> bool {
    let Some(step) = controller.autostep else {
        return false;
    };
    let shape = collider.shape();
    let up = controller.up.normalize();
    let extent = shape.compute_local_aabb().extents().dot(up.abs());
    let max_height = match step.max_height {
        CharacterLength::Absolute(value) => value,
        CharacterLength::Relative(value) => value * extent,
    };
    let demand = -desired.with_y(0.0).dot(normal);
    let raw = bevy_rapier3d::rapier::control::KinematicCharacterController {
        up,
        offset: controller.offset,
        slide: controller.slide,
        autostep: controller.autostep,
        max_slope_climb_angle: controller.max_slope_climb_angle,
        min_slope_slide_angle: controller.min_slope_slide_angle,
        snap_to_ground: controller.snap_to_ground,
        normal_nudge_factor: controller.normal_nudge_factor,
    };
    context.with_query_pipeline(filter, |pipeline| {
        let initial = *collider.position();
        let mut pose = initial;
        let mut contacts = 0_u16;
        for _ in 0..8 {
            let movement = raw.move_shape(
                context.simulation.integration_parameters.dt,
                &pipeline.query_pipeline,
                shape,
                &pose,
                desired,
                |_| contacts = contacts.saturating_add(1),
            );
            if !movement.grounded {
                return false;
            }
            pose.translation += movement.translation;
            let delta = pose.translation - initial.translation;
            let noise = initial_noise + controller.normal_nudge_factor * f32::from(contacts);
            let rise = delta.dot(up);
            if !delta.is_finite() || rise > max_height + noise {
                return false;
            }
            if rise > noise && -delta.with_y(0.0).dot(normal) > demand + noise {
                return true;
            }
        }
        false
    })
}
