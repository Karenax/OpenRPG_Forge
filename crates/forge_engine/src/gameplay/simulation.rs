use super::{GameplayBody, GameplayRuntime, engine, neutral};
use crate::{
    DomainEntityId, ForgePlayerCharacter, PlayerRuntimeBindings, PlayerRuntimeSnapshot,
    PlayerSimulationReady, TidewaterPlayerState,
};
use bevy::prelude::*;
use bevy_rapier3d::prelude::{
    Collider, CollisionEvent, KinematicCharacterControllerOutput, QueryFilter, RapierContext,
    ReadRapierContext, ShapeCastOptions, Sleeping, Velocity,
};
use forge_core::CommandId;
use forge_gameplay::{
    ActionObservation, ActorObservation, ContactObservation, DangerObservation, GameAction,
    GameRequest, HeatExposure, ObjectObservation, PhysicsFrame,
};
use std::collections::BTreeSet;

type Bodies<'w, 's> = Query<
    'w,
    's,
    (
        Entity,
        &'static DomainEntityId,
        &'static GameplayBody,
        &'static Transform,
        Option<&'static Velocity>,
        Option<&'static Sleeping>,
    ),
>;
type Player<'w, 's> = Query<
    'w,
    's,
    (
        &'static Transform,
        &'static Collider,
        Option<Ref<'static, KinematicCharacterControllerOutput>>,
    ),
    With<ForgePlayerCharacter>,
>;

#[allow(
    clippy::needless_pass_by_value,
    reason = "Bevy owns the system parameter wrappers"
)]
pub(super) fn remember_velocity(
    runtime: Res<GameplayRuntime>,
    rapier: ReadRapierContext,
    mut bodies: Query<(Entity, &mut GameplayBody)>,
) {
    let context = rapier.single().ok();
    for (entity, mut body) in &mut bodies {
        body.previous_velocity = if runtime.enabled {
            context
                .as_ref()
                .and_then(|context| {
                    context
                        .entity2body()
                        .get(&entity)
                        .and_then(|handle| context.rigidbody_set.bodies.get(*handle))
                })
                .map_or(Vec3::ZERO, |body| {
                    let v = body.linvel();
                    Vec3::new(v.x, v.y, v.z)
                })
        } else {
            Vec3::ZERO
        };
    }
}

#[allow(
    clippy::needless_pass_by_value,
    clippy::too_many_arguments,
    reason = "one fixed-step adapter reads the canonical post-writeback physics and player projection"
)]
pub(super) fn observe(
    mut runtime: ResMut<GameplayRuntime>,
    ready: Res<PlayerSimulationReady>,
    bindings: Res<PlayerRuntimeBindings>,
    player_snapshot: Res<PlayerRuntimeSnapshot>,
    fixed_time: Res<Time<Fixed>>,
    rapier: ReadRapierContext,
    bodies: Bodies,
    player: Player,
    mut collisions: MessageReader<CollisionEvent>,
    water: Option<Res<TidewaterPlayerState>>,
    mut timing: Option<ResMut<crate::playtest_timing::PlaytestTiming>>,
) {
    let mut timing_scope = crate::playtest_timing::TimingScope::new(
        timing.as_deref_mut(),
        crate::playtest_timing::TimingPhase::GameplayObserve,
    );
    runtime.player_settled = false;
    let Some(binding) = bindings.snapshot() else {
        collisions.clear();
        return;
    };
    let player_id = runtime.session.document().player;
    runtime.entities.insert(player_id, binding.player_entity);
    if !runtime.enabled || !ready.0 {
        collisions.clear();
        return;
    }
    let Ok(context) = rapier.single() else {
        runtime.error = Some("Gameplay physics context unavailable".into());
        return;
    };
    let Ok((transform, collider, output)) = player.single() else {
        runtime.error = Some("Gameplay player collider unavailable".into());
        return;
    };
    let backend = current_player_collider(
        &context,
        binding.player_entity,
        transform,
        collider,
        water.as_deref(),
    );
    let current_output = output
        .as_ref()
        .filter(|output| {
            backend.is_some()
                && output.is_changed()
                && output
                    .desired_translation
                    .abs_diff_eq(player_snapshot.desired_translation, 1.0e-6)
        })
        .map(|output| &**output);
    // A save carries the player's pose, not KCC velocity. Observe support and
    // settled motion after writeback rather than discarding a jump or a walk.
    runtime.player_settled = backend.is_some() && player_is_settled(&player_snapshot);
    let Some(mut frame) = make_frame(&runtime, &bodies, player_snapshot.player_position) else {
        runtime.error = Some("Gameplay physics tick overflow".into());
        runtime.enabled = false;
        return;
    };
    // The motion integrator keeps requested velocity while a KCC is blocked.
    // Only its resolved displacement carries the player's collision energy.
    let player_velocity = current_output.map_or(Vec3::ZERO, |_| {
        player_snapshot.effective_translation / fixed_time.timestep().as_secs_f32()
    });
    frame.contacts = collect_contacts(
        &runtime,
        &mut collisions,
        &bodies,
        player_velocity,
        &context,
        backend.is_some(),
    );
    perceive(&runtime, &context, binding.player_entity, &mut frame);
    let receipt = match timing_scope
        .measure(crate::playtest_timing::TimingPhase::GameplayAdvance, || {
            runtime.session.advance_frame(&frame)
        }) {
        Ok(receipt) => receipt,
        Err(error) => {
            runtime.error = Some(error.to_string());
            runtime.enabled = false;
            return;
        }
    };
    let mut events = receipt.events;
    let wet = player_is_wet(water.as_deref(), transform.translation);
    if let Some(backend) = backend {
        events.extend(execute_pending_actions(
            &mut runtime,
            &context,
            binding.player_entity,
            backend,
            current_output,
            wet,
        ));
    }
    if !events.is_empty() {
        tracing::info!(target:"forge::gameplay", source_hash=%runtime.session.source_hash(), tick=runtime.session.state().tick,
            sequence=runtime.session.state().sequence, ?events, "authored gameplay consequences from canonical physics");
        runtime.last_events.clone_from(&events);
    }
    runtime.effects.extend(events);
}

fn collect_contacts(
    runtime: &GameplayRuntime,
    collisions: &mut MessageReader<CollisionEvent>,
    bodies: &Bodies,
    player_velocity: Vec3,
    context: &RapierContext,
    player_contact_current: bool,
) -> Vec<ContactObservation> {
    let mut contacts = Vec::new();
    let mut observed_pairs = BTreeSet::new();
    for event in collisions.read() {
        if let CollisionEvent::Started(a, b, _) = *event
            && let Some(contact) = contact(
                runtime,
                a,
                b,
                bodies,
                player_velocity,
                context,
                player_contact_current,
            )
        {
            let pair = (
                contact.source.min(contact.target),
                contact.source.max(contact.target),
            );
            if observed_pairs.insert(pair) {
                contacts.push(contact);
            }
        }
    }
    // Impulses on already-touching objects do not always produce another Started event.
    for entity in runtime.entities.values() {
        for pair in context.contact_pairs_with(*entity) {
            if let (Some(a), Some(b)) = (pair.collider1(), pair.collider2())
                && let Some(contact) = contact(
                    runtime,
                    a,
                    b,
                    bodies,
                    player_velocity,
                    context,
                    player_contact_current,
                )
                && contact.normal_speed_m_s > 0.1
            {
                let pair = (
                    contact.source.min(contact.target),
                    contact.source.max(contact.target),
                );
                if observed_pairs.insert(pair) {
                    contacts.push(contact);
                }
            }
        }
    }
    contacts
}

fn execute_pending_actions(
    runtime: &mut GameplayRuntime,
    context: &RapierContext,
    player_entity: Entity,
    backend: &bevy_rapier3d::rapier::prelude::Collider,
    output: Option<&KinematicCharacterControllerOutput>,
    wet: bool,
) -> Vec<forge_gameplay::GameplayEvent> {
    let mut events = Vec::new();
    while let Some(request) = runtime.pending.pop_front() {
        let observation = action_observation(
            runtime,
            &request,
            context,
            player_entity,
            backend,
            output,
            wet,
        );
        match runtime.session.execute(&request, observation) {
            Ok(receipt) => {
                runtime.feedback = format!("Action acceptée · étape {}", receipt.sequence);
                events.extend(receipt.events);
            }
            Err(error) => runtime.feedback = error.to_string(),
        }
    }
    events
}

fn current_player_collider<'a>(
    context: &'a RapierContext<'_>,
    entity: Entity,
    transform: &Transform,
    collider: &Collider,
    water: Option<&TidewaterPlayerState>,
) -> Option<&'a bevy_rapier3d::rapier::prelude::Collider> {
    let handle = context.colliders.entity2collider().get(&entity)?;
    let backend = context.colliders.colliders.get(*handle)?;
    // Tidewater normalizes supported feet vertically after writeback. Capsule
    // transitions explicitly wait for backend synchronization; queries always
    // consume the installed capsule and pose, including the current height.
    let same_capsule = backend
        .shape()
        .as_capsule()
        .zip(collider.raw.as_capsule())
        .is_some_and(|(installed, projected)| {
            installed.radius.to_bits() == projected.radius.to_bits()
                && installed.segment.a == projected.segment.a
                && installed.segment.b == projected.segment.b
        });
    let pose = backend.position();
    let pose_current = pose
        .translation
        .xz()
        .abs_diff_eq(transform.translation.xz(), 1.0e-6)
        && pose.rotation.abs_diff_eq(transform.rotation, 1.0e-6)
        && (water.is_some() || (pose.translation.y - transform.translation.y).abs() <= 1.0e-6);
    (same_capsule && pose_current && !water.is_some_and(TidewaterPlayerState::backend_sync_pending))
        .then_some(backend)
}

fn player_is_settled(snapshot: &PlayerRuntimeSnapshot) -> bool {
    snapshot.backend_identity_stable
        && snapshot.grounded
        && snapshot.horizontal_velocity.is_finite()
        && snapshot.vertical_velocity_mps.is_finite()
        && snapshot.desired_translation.is_finite()
        && snapshot.effective_translation.is_finite()
        && snapshot.horizontal_velocity.length_squared() <= 0.05_f32.powi(2)
        && snapshot.vertical_velocity_mps.abs() <= 0.05
        && snapshot.desired_translation.xz().length_squared() <= 0.001_f32.powi(2)
        && snapshot.effective_translation.length_squared() <= 0.001_f32.powi(2)
}

fn player_is_wet(water: Option<&TidewaterPlayerState>, position: Vec3) -> bool {
    water.is_some_and(|state| {
        let query = Vec2::from_array(state.water_query_position);
        state.water_query_step > 0
            && query.distance(position.xz()) < 1.0
            && state.motion.water_mean.is_some()
            && (state.motion.wade > 0.0
                || matches!(state.motion.mode, crate::tidewater_locomotion::Mode::Swim))
    })
}

fn make_frame(
    runtime: &GameplayRuntime,
    bodies: &Bodies,
    player_position: Vec3,
) -> Option<PhysicsFrame> {
    let mut frame = PhysicsFrame {
        source_hash: runtime.session.source_hash(),
        tick: runtime.session.state().tick.checked_add(1)?,
        actors: vec![ActorObservation {
            actor: runtime.session.document().player,
            position: neutral(player_position),
        }],
        objects: Vec::new(),
        contacts: Vec::new(),
        heat: Vec::new(),
        danger: Vec::new(),
    };
    for (_, id, _, transform, velocity, sleeping) in bodies {
        if runtime.session.state().actors.contains_key(&id.0) {
            frame.actors.push(ActorObservation {
                actor: id.0,
                position: neutral(transform.translation),
            });
        } else if runtime
            .session
            .state()
            .objects
            .get(&id.0)
            .is_some_and(|o| !o.consumed && !o.destroyed)
        {
            let moving = velocity.is_some_and(|v| {
                v.linear.length_squared() > 0.0025 || v.angular.length_squared() > 0.0025
            }) && sleeping.is_none_or(|s| !s.sleeping);
            frame.objects.push(ObjectObservation {
                object: id.0,
                position: neutral(transform.translation),
                rotation_xyzw: transform.rotation.to_array(),
                in_motion: moving,
            });
        }
    }
    Some(frame)
}

fn contact(
    runtime: &GameplayRuntime,
    a: Entity,
    b: Entity,
    bodies: &Bodies,
    player_velocity: Vec3,
    context: &RapierContext,
    player_contact_current: bool,
) -> Option<ContactObservation> {
    let find = |entity| {
        runtime
            .entities
            .iter()
            .find_map(|(id, bound)| (*bound == entity).then_some(*id))
    };
    let first = find(a)?;
    let second = find(b)?;
    let player = runtime.session.document().player;
    if !player_contact_current && (first == player || second == player) {
        return None;
    }
    let velocity = |entity, id| {
        bodies.get(entity).map_or_else(
            |_| {
                if id == runtime.session.document().player {
                    player_velocity
                } else {
                    Vec3::ZERO
                }
            },
            |(_, _, body, _, _, _)| body.previous_velocity,
        )
    };
    let va = velocity(a, first);
    let vb = velocity(b, second);
    let pair = context.contact_pair(a, b)?;
    if !pair.has_any_active_contact() {
        return None;
    }
    let normal = pair.manifolds().next()?.normal();
    let relative = if pair.collider1() == Some(a) {
        va - vb
    } else {
        vb - va
    };
    let speed = relative.dot(normal).max(0.0);
    let energy = |id| {
        runtime
            .session
            .document()
            .object(id)
            .map_or(-1.0, |o| o.physics.mass_kg * speed * speed)
    };
    let (source, target) = if energy(first) >= energy(second) {
        (first, second)
    } else {
        (second, first)
    };
    let definition = runtime.session.document().object(source)?;
    let state = runtime.session.state().objects.get(&source)?;
    if definition.physics.mass_kg <= 0.0 || state.consumed || state.destroyed {
        return None;
    }
    let key = format!(
        "physical-contact/{}/{}/{source}/{target}",
        runtime.session.source_hash(),
        runtime.session.state().tick + 1
    );
    Some(ContactObservation {
        command_id: CommandId::deterministic(key),
        source,
        target,
        initiator: state.initiator,
        normal_speed_m_s: speed.min(200.0),
    })
}

fn visible(
    context: &RapierContext,
    origin: Vec3,
    target: Vec3,
    exclude: Entity,
    expected: Option<Entity>,
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
        .is_none_or(|(hit, _)| Some(hit) == expected)
}

fn perceive(
    runtime: &GameplayRuntime,
    context: &RapierContext,
    player: Entity,
    frame: &mut PhysicsFrame,
) {
    let range = runtime.session.document().profile.interaction_range_m;
    for (object, current) in &runtime.session.state().objects {
        if current.consumed
            || current.destroyed
            || !(current.burning || (current.released && current.in_motion))
        {
            continue;
        }
        for actor in &frame.actors {
            let current_position = frame
                .objects
                .iter()
                .find(|o| o.object == *object)
                .map_or(current.position, |o| o.position);
            let delta = engine(actor.position) - engine(current_position);
            if delta.length_squared() > range * range {
                continue;
            }
            let Some(actor_entity) = runtime.entities.get(&actor.actor) else {
                continue;
            };
            let los = visible(
                context,
                engine(actor.position),
                engine(current_position),
                *actor_entity,
                runtime.entities.get(object).copied(),
            );
            if current.burning {
                frame.heat.push(HeatExposure {
                    object: *object,
                    actor: actor.actor,
                    line_of_sight: los,
                });
            }
            if *actor_entity != player {
                frame.danger.push(DangerObservation {
                    object: *object,
                    actor: actor.actor,
                    line_of_sight: los,
                });
            }
        }
    }
}

#[allow(
    clippy::too_many_arguments,
    reason = "the contact proof binds one request to its canonical player collider, target and water observation"
)]
fn action_observation(
    runtime: &GameplayRuntime,
    request: &GameRequest,
    context: &RapierContext,
    player_entity: Entity,
    player_collider: &bevy_rapier3d::rapier::prelude::Collider,
    output: Option<&KinematicCharacterControllerOutput>,
    wet: bool,
) -> ActionObservation {
    let target = match request.action {
        GameAction::ChooseDialogue { speaker, .. } => speaker,
        GameAction::Attack { target } => target,
        GameAction::Pickup { object }
        | GameAction::SetDoor { object, .. }
        | GameAction::Push { object, .. }
        | GameAction::Throw { object, .. }
        | GameAction::Ignite { object }
        | GameAction::Extinguish { object } => object,
    };
    let Some(entity) = runtime.entities.get(&target).copied() else {
        return ActionObservation::default();
    };
    let position = runtime
        .session
        .state()
        .objects
        .get(&target)
        .map(|o| o.position)
        .or_else(|| {
            runtime
                .session
                .state()
                .actors
                .get(&target)
                .map(|a| a.position)
        });
    let Some(position) = position else {
        return ActionObservation::default();
    };
    let target_position = engine(position);
    let pose = player_collider.position();
    let direction = (target_position - pose.translation).normalize_or_zero();
    let touching = context
        .contact_pair(player_entity, entity)
        .is_some_and(|p| p.has_any_active_contact())
        || output.is_some_and(|o| o.collisions.iter().any(|c| c.entity == entity))
        || context
            .cast_shape(
                pose.translation,
                pose.rotation,
                direction,
                player_collider.shape(),
                ShapeCastOptions::with_max_time_of_impact(0.05),
                QueryFilter::default().predicate(&|candidate| candidate == entity),
            )
            .is_some();
    ActionObservation {
        line_of_sight: visible(
            context,
            pose.translation,
            target_position,
            player_entity,
            Some(entity),
        ),
        confirmed_contact: touching,
        water_contact: wet,
    }
}
