//! Runtime-only kinematic motion for actors reacting to an observed danger.
//!
//! The domain owns the `fleeing` state.  This adapter only turns that state
//! into a governed destination after rechecking source/range/line of sight.
//! Landmass plans the route; only the existing Rapier KCC resolves movement.
//! Authored patrol destinations use the same generic route/physical adapter.

use super::{GameplayRuntime, engine};
use crate::navigation::NavigationRuntime;
use crate::{DomainEntityId, PlayerSimulationReady};
use bevy::prelude::*;
use bevy_rapier3d::{
    control::{CharacterAutostep, CharacterLength, KinematicCharacterController},
    prelude::{
        KinematicCharacterControllerOutput, QueryFilter, QueryFilterFlags, RapierContext,
        ReadRapierContext,
    },
};

/// The authored NPC mesh and collider share a 0.35 m radius and 0.60 m
/// half-segment: the resulting capsule is 1.90 m high and is centred on the
/// authored actor position (the Studio actor form supplies its ground offset).
pub(crate) const NPC_CAPSULE_RADIUS_M: f32 = 0.35;
pub(crate) const NPC_CAPSULE_HALF_SEGMENT_M: f32 = 0.60;
pub(crate) const NPC_CAPSULE_HEIGHT_M: f32 =
    NPC_CAPSULE_HALF_SEGMENT_M * 2.0 + NPC_CAPSULE_RADIUS_M * 2.0;

const NPC_WALK_SPEED_MPS: f32 = 2.0;
const NPC_GRAVITY_MPS2: f32 = -9.81;
const NPC_MAX_STEP_HEIGHT_M: f32 = 0.4;
const NPC_MIN_STEP_WIDTH_M: f32 = 0.35;
const NPC_GROUND_SNAP_M: f32 = 0.1;
const NPC_MAX_SLOPE_CLIMB_DEGREES: f32 = 45.0;
const NPC_MIN_SLOPE_SLIDE_DEGREES: f32 = 50.0;

/// Runtime-only marker for an authored non-player actor projected with the
/// generic danger-motion adapter.
#[derive(Component, Clone, Copy, Debug, Default)]
pub struct DangerMotionActor;

/// Kinematic state kept outside the authored/game-save model.
#[derive(Component, Clone, Copy, Debug, Default)]
pub struct DangerMotionState {
    pub vertical_velocity_mps: f32,
    pub grounded: bool,
    pub effective_translation: Vec3,
    pub collision_count: usize,
    /// Request captured before Rapier consumes controller.translation.
    pub requested_translation: Option<Vec3>,
    /// Set only after a `KCCOutput` changed in the completed physical step.
    pub feedback_current: bool,
    /// A provider-verified escape destination, used as next tick's intent.
    pub escape_target: Option<Vec3>,
}

type DangerActors<'w, 's> = Query<
    'w,
    's,
    (
        Entity,
        &'static DomainEntityId,
        &'static Transform,
        &'static mut KinematicCharacterController,
        &'static mut DangerMotionState,
    ),
    With<DangerMotionActor>,
>;

struct EscapeRequest {
    actor: forge_core::EntityId,
    entity: Entity,
    center: Vec3,
    direction: Vec3,
    distance: f32,
}

/// A neutral, human-scale KCC profile.  Gravity is supplied by `drive`, so
/// the Rapier body remains kinematic and obstacle collisions remain visible in
/// `KinematicCharacterControllerOutput`.
pub(crate) fn npc_controller() -> KinematicCharacterController {
    KinematicCharacterController {
        up: Vec3::Y,
        offset: CharacterLength::Absolute(0.01),
        slide: true,
        autostep: Some(CharacterAutostep {
            max_height: CharacterLength::Absolute(NPC_MAX_STEP_HEIGHT_M),
            min_width: CharacterLength::Absolute(NPC_MIN_STEP_WIDTH_M),
            include_dynamic_bodies: false,
        }),
        max_slope_climb_angle: NPC_MAX_SLOPE_CLIMB_DEGREES.to_radians(),
        min_slope_slide_angle: NPC_MIN_SLOPE_SLIDE_DEGREES.to_radians(),
        snap_to_ground: Some(CharacterLength::Absolute(NPC_GROUND_SNAP_M)),
        filter_flags: QueryFilterFlags::EXCLUDE_SENSORS,
        ..default()
    }
}

#[allow(
    clippy::needless_pass_by_value,
    clippy::type_complexity,
    reason = "the actuator reads the runtime state and writes one KCC request per projected actor"
)]
pub(super) fn drive(
    time: Res<Time<Fixed>>,
    ready: Option<Res<PlayerSimulationReady>>,
    runtime: Res<GameplayRuntime>,
    mut navigation: ResMut<NavigationRuntime>,
    rapier: ReadRapierContext,
    mut actors: DangerActors,
    mut timing: Option<ResMut<crate::playtest_timing::PlaytestTiming>>,
) {
    let _timing_scope = crate::playtest_timing::TimingScope::new(
        timing.as_deref_mut(),
        crate::playtest_timing::TimingPhase::NpcDrive,
    );
    let valid_simulation = runtime.is_ready() && ready.is_none_or(|ready| ready.0);
    let context = valid_simulation.then(|| rapier.single().ok()).flatten();
    let delta_seconds = time.timestep().as_secs_f32();
    let mut blocked_escape_candidates = Vec::new();

    for (entity, domain, transform, mut controller, mut motion) in &mut actors {
        let Some(actor) = runtime.session.state().actors.get(&domain.0) else {
            stop(&mut controller, &mut motion);
            continue;
        };
        if !valid_simulation || actor.vitality == 0 {
            navigation.disable_actor(domain.0, transform.translation);
            stop(&mut controller, &mut motion);
            continue;
        }

        let direction = context
            .as_ref()
            .and_then(|context| {
                flee_direction(&runtime, context, entity, domain.0, transform.translation)
            })
            .unwrap_or(Vec3::ZERO);
        let patrol = runtime
            .session
            .document()
            .actor(domain.0)
            .map(|actor| &actor.patrol);
        let target = if direction == Vec3::ZERO {
            motion.escape_target = None;
            patrol
                .filter(|points| !points.is_empty())
                .map(|points| engine(points[navigation.patrol_index(domain.0) % points.len()]))
        } else {
            context.as_ref().and_then(|context| {
                let distance = runtime.session.document().profile.interaction_range_m * 1.5;
                blocked_escape_candidates.push(EscapeRequest {
                    actor: domain.0,
                    entity,
                    center: transform.translation,
                    direction,
                    distance,
                });
                flee_goal(
                    context,
                    entity,
                    &mut motion,
                    transform.translation,
                    direction,
                    distance,
                )
            })
        };
        if let Some(target) = target
            && transform.translation.distance(target) < 0.25
        {
            navigation.advance_patrol(domain.0, patrol.map_or(0, Vec::len));
        }
        let actual = if delta_seconds > 0.0 {
            motion.effective_translation / delta_seconds
        } else {
            Vec3::ZERO
        };
        navigation.intent(
            domain.0,
            transform.translation,
            actual,
            target,
            (motion.grounded, motion.collision_count),
            true,
        );
        // This loop supplies physical feedback/intent for all agents before one
        // shared Landmass update performs local avoidance.
        controller.translation = None;
        motion.requested_translation = None;
        motion.feedback_current = false;
    }
    navigation.tick(delta_seconds);
    select_escape_targets(
        &mut navigation,
        &mut actors,
        context.as_ref(),
        blocked_escape_candidates,
    );
    apply_navigation_motion(
        &runtime,
        &navigation,
        &mut actors,
        valid_simulation,
        delta_seconds,
    );
}

fn apply_navigation_motion(
    runtime: &GameplayRuntime,
    navigation: &NavigationRuntime,
    actors: &mut DangerActors,
    valid_simulation: bool,
    delta_seconds: f32,
) {
    for (_, domain, _, mut controller, mut motion) in actors {
        let available = valid_simulation
            && runtime
                .session
                .state()
                .actors
                .get(&domain.0)
                .is_some_and(|actor| actor.vitality > 0);
        if !available {
            stop(&mut controller, &mut motion);
            continue;
        }
        apply_motion(
            &mut controller,
            &mut motion,
            navigation.desired(domain.0),
            delta_seconds,
        );
    }
}

fn select_escape_targets(
    navigation: &mut NavigationRuntime,
    actors: &mut DangerActors,
    context: Option<&RapierContext>,
    requests: Vec<EscapeRequest>,
) {
    let Some(context) = context else {
        return;
    };
    for request in requests {
        if navigation.needs_escape_search(request.actor)
            && let Some(target) = navigation.reachable_escape_goal(
                request.actor,
                &escape_candidates(
                    context,
                    request.entity,
                    request.center,
                    request.direction,
                    request.distance,
                ),
            )
            && let Some((_, _, _, _, mut motion)) = actors
                .iter_mut()
                .find(|(_, domain, _, _, _)| domain.0 == request.actor)
        {
            motion.escape_target = Some(target);
        }
    }
}

fn flee_goal(
    context: &RapierContext,
    actor: Entity,
    motion: &mut DangerMotionState,
    center: Vec3,
    direction: Vec3,
    distance: f32,
) -> Option<Vec3> {
    let away = motion
        .escape_target
        .map(|target| (target - center).with_y(0.0));
    if away
        .is_some_and(|away| away.length() < 0.25 || away.normalize_or_zero().dot(direction) < 0.2)
    {
        motion.escape_target = None;
    }
    motion
        .escape_target
        .or_else(|| flee_target(context, actor, center + direction * distance))
}

fn apply_motion(
    controller: &mut KinematicCharacterController,
    motion: &mut DangerMotionState,
    desired: Vec3,
    delta_seconds: f32,
) {
    let horizontal = desired.clamp_length_max(NPC_WALK_SPEED_MPS);
    if motion.grounded && motion.vertical_velocity_mps < 0.0 {
        motion.vertical_velocity_mps = 0.0;
    }
    motion.vertical_velocity_mps += NPC_GRAVITY_MPS2 * delta_seconds;
    if !motion.vertical_velocity_mps.is_finite() {
        stop(controller, motion);
        return;
    }
    controller.slide =
        horizontal != Vec3::ZERO || !motion.grounded || motion.vertical_velocity_mps > 0.0;
    controller.translation =
        Some(horizontal * delta_seconds + Vec3::Y * motion.vertical_velocity_mps * delta_seconds);
    motion.requested_translation = controller.translation;
}

fn escape_candidates(
    context: &RapierContext,
    actor: Entity,
    center: Vec3,
    away: Vec3,
    distance: f32,
) -> Vec<Vec3> {
    [0.0_f32, 30.0, -30.0, 60.0, -60.0, 75.0, -75.0]
        .into_iter()
        .map(|angle| center + Quat::from_rotation_y(angle.to_radians()) * away * distance)
        .chain(std::iter::once(center + away * distance * 0.5))
        .filter_map(|candidate| flee_target(context, actor, candidate))
        .collect()
}

fn flee_target(context: &RapierContext, actor: Entity, candidate: Vec3) -> Option<Vec3> {
    let origin = candidate + Vec3::Y * 4.0;
    let filter = QueryFilter {
        flags: QueryFilterFlags::EXCLUDE_SENSORS
            | QueryFilterFlags::EXCLUDE_DYNAMIC
            | QueryFilterFlags::EXCLUDE_KINEMATIC,
        ..default()
    }
    .exclude_collider(actor);
    let (_, distance) = context.cast_ray(origin, Vec3::NEG_Y, 8.0, true, filter)?;
    Some(origin - Vec3::Y * distance + Vec3::Y * (NPC_CAPSULE_HEIGHT_M * 0.5))
}

fn stop(controller: &mut KinematicCharacterController, motion: &mut DangerMotionState) {
    controller.translation = None;
    motion.vertical_velocity_mps = 0.0;
    motion.grounded = false;
    motion.effective_translation = Vec3::ZERO;
    motion.collision_count = 0;
    motion.requested_translation = None;
    motion.feedback_current = false;
    motion.escape_target = None;
}

/// Update grounded/collision evidence after Rapier writeback.  The next
/// drive step then applies gravity from the observed support state.
pub(super) fn observe(
    mut actors: Query<
        (
            &mut DangerMotionState,
            Option<Ref<KinematicCharacterControllerOutput>>,
        ),
        With<DangerMotionActor>,
    >,
) {
    for (mut motion, output) in &mut actors {
        motion.feedback_current = false;
        let Some(output) = output.filter(|output| {
            output.is_changed()
                && motion
                    .requested_translation
                    .is_some_and(|request| request.abs_diff_eq(output.desired_translation, 1.0e-6))
        }) else {
            motion.grounded = false;
            motion.effective_translation = Vec3::ZERO;
            motion.collision_count = 0;
            continue;
        };
        motion.feedback_current = true;
        motion.grounded = output.grounded;
        motion.effective_translation = output.effective_translation;
        motion.collision_count = output.collisions.len();
        if motion.grounded && motion.vertical_velocity_mps < 0.0 {
            motion.vertical_velocity_mps = 0.0;
        }
    }
}

fn flee_direction(
    runtime: &GameplayRuntime,
    context: &RapierContext,
    actor_entity: Entity,
    actor_id: forge_core::EntityId,
    actor_position: Vec3,
) -> Option<Vec3> {
    if !actor_position.is_finite() {
        return Some(Vec3::ZERO);
    }
    let actor_state = runtime.session.state().actors.get(&actor_id)?;
    if !actor_state.fleeing {
        return Some(Vec3::ZERO);
    }
    let actor_definition = runtime.session.document().actor(actor_id)?;
    if !actor_definition.flees_danger {
        return Some(Vec3::ZERO);
    }
    let range = runtime.session.document().profile.interaction_range_m;
    if !range.is_finite() || range < 0.0 {
        return Some(Vec3::ZERO);
    }
    let range_squared = range * range;
    let mut direction = Vec3::ZERO;
    for (object_id, object_state) in &runtime.session.state().objects {
        if object_state.destroyed
            || object_state.consumed
            || !(object_state.burning || (object_state.released && object_state.in_motion))
        {
            continue;
        }
        let source_position = engine(object_state.position);
        let delta = actor_position - source_position;
        if !delta.is_finite() || delta.length_squared() > range_squared {
            continue;
        }
        let Some(source_entity) = runtime.entities.get(object_id).copied() else {
            continue;
        };
        if !visible(
            context,
            actor_position,
            source_position,
            actor_entity,
            source_entity,
        ) {
            continue;
        }
        direction += delta.with_y(0.0).normalize_or_zero();
    }
    Some(direction.normalize_or_zero())
}

fn visible(
    context: &RapierContext,
    origin: Vec3,
    target: Vec3,
    exclude: Entity,
    expected: Entity,
) -> bool {
    let delta = target - origin;
    let distance = delta.length();
    if distance <= 0.001 {
        return true;
    }
    context
        .cast_ray(
            origin,
            delta / distance,
            distance,
            true,
            QueryFilter::default().exclude_collider(exclude),
        )
        .is_none_or(|(hit, _)| hit == expected)
}
