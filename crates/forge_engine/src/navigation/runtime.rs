use super::{
    NavigationPolicy, NavigationRuntime, NavigationStatus,
    cache::{MAX_PENDING, MAX_PENDING_BYTES, MAX_TILES, TileRecord},
    geometry,
};
use bevy::prelude::*;
use bevy_rapier3d::prelude::DefaultRapierContext;
use forge_navigation::TileInput;
use forge_navigation::landmass;

fn context(world: &mut World) -> Result<Entity, String> {
    world
        .query_filtered::<Entity, With<DefaultRapierContext>>()
        .single(world)
        .map_err(|_| "navigation requires exactly one current Rapier context".into())
}

/// Runs before the KCC request: ECS edits pending backend synchronization or
/// changed source receipts withdraw old islands before any horizontal movement.
pub(crate) fn guard(world: &mut World) {
    let context = context(world);
    let sources = context
        .as_ref()
        .map_err(Clone::clone)
        .and_then(|context| geometry::receipts(world, *context));
    let policy = world.resource::<NavigationPolicy>().clone();
    world.resource_scope(|world, mut navigation: Mut<NavigationRuntime>| {
        let present: std::collections::BTreeSet<_> = world
            .query_filtered::<&crate::DomainEntityId, With<crate::gameplay::DangerMotionActor>>()
            .iter(world)
            .filter(|domain| {
                world
                    .resource::<crate::gameplay::GameplayRuntime>()
                    .session()
                    .document()
                    .actor(domain.0)
                    .is_some()
            })
            .map(|domain| domain.0)
            .collect();
        let obsolete: Vec<_> = navigation
            .routes
            .keys()
            .filter(|actor| !present.contains(actor))
            .copied()
            .collect();
        for actor in obsolete {
            if let Some(route) = navigation.routes.remove(&actor) {
                navigation.archipelago.remove_agent(route.agent);
            }
        }
        navigation.actuation_ready = context.is_ok() && navigation.required().len() <= MAX_TILES;
        if let Ok(context) = context.as_ref() {
            update_characters(world, *context, &mut navigation);
        }
        for key in navigation.required() {
            let current = sources
                .as_ref()
                .map_err(Clone::clone)
                .and_then(|sources| geometry::stamp(sources, key, navigation.profile, &policy));
            if navigation
                .tiles
                .get(&key)
                .is_some_and(|tile| tile.stamp != current.as_ref().ok().copied())
            {
                navigation.invalidate(key, current.err());
            }
        }
    });
}

fn update_characters(world: &World, context: Entity, navigation: &mut NavigationRuntime) {
    use crate::{ForgePlayerCharacter, gameplay::DangerMotionActor};
    use bevy_rapier3d::prelude::{RapierContextColliders, RapierRigidBodySet};
    let Some(colliders) = world.get::<RapierContextColliders>(context) else {
        return;
    };
    let Some(bodies) = world.get::<RapierRigidBodySet>(context) else {
        return;
    };
    let mut present = std::collections::BTreeSet::new();
    for (&entity, &handle) in colliders.entity2collider() {
        if world.get::<DangerMotionActor>(entity).is_some() {
            continue;
        }
        let Some(collider) = colliders.colliders.get(handle) else {
            continue;
        };
        if !collider.is_enabled() || collider.is_sensor() {
            continue;
        }
        let body = collider
            .parent()
            .and_then(|handle| bodies.bodies.get(handle));
        if !body.is_some_and(bevy_rapier3d::rapier::dynamics::RigidBody::is_dynamic)
            && world.get::<ForgePlayerCharacter>(entity).is_none()
        {
            continue;
        }
        let bounds = collider.compute_aabb();
        let center = (bounds.mins + bounds.maxs) * 0.5;
        let half = (bounds.maxs - bounds.mins) * 0.5;
        let radius = (half.x * half.x + half.z * half.z).sqrt();
        let feet = Vec3::new(center.x, bounds.mins.y, center.z);
        // A conservative cylinder is used only by local avoidance. Actual
        // collision remains the original installed shape in the same KCC world.
        let nearby = navigation
            .routes
            .values()
            .any(|route| Vec3::from_array(route.observation.feet).distance(feet) < 10.0 + radius);
        if !nearby {
            continue;
        }
        present.insert(entity);
        let velocity = body.map_or(
            Vec3::ZERO,
            bevy_rapier3d::rapier::dynamics::RigidBody::linvel,
        );
        let id = *navigation.characters.entry(entity).or_insert_with(|| {
            navigation.archipelago.add_character(landmass::Character::<
                forge_navigation::ForgeCoordinates,
            > {
                position: forge_navigation::point(feet.to_array()),
                velocity: forge_navigation::point(velocity.to_array()),
                radius,
            })
        });
        if let Some(character) = navigation.archipelago.get_character_mut(id) {
            character.position = forge_navigation::point(feet.to_array());
            character.velocity = forge_navigation::point(velocity.to_array());
            character.radius = radius;
        }
    }
    let removed: Vec<_> = navigation
        .characters
        .keys()
        .filter(|entity| !present.contains(entity))
        .copied()
        .collect();
    for entity in removed {
        if let Some(id) = navigation.characters.remove(&entity) {
            navigation.archipelago.remove_character(id);
        }
    }
}

/// After physics writeback and gameplay projection: snapshot canonical prepared
/// geometry, poll workers, recheck receipts, then publish validated live islands.
pub(crate) fn refresh(world: &mut World) {
    let context = context(world);
    let sources = context
        .as_ref()
        .map_err(Clone::clone)
        .and_then(|context| geometry::receipts(world, *context));
    let policy = world.resource::<NavigationPolicy>().clone();
    world.resource_scope(|world,mut navigation:Mut<NavigationRuntime>| {
        observe_feedback(world, &mut navigation);
        let required=navigation.required();
        if required.len()>MAX_TILES {
            let withdrawn:Vec<_>=navigation.tiles.keys().copied().collect();
            for key in withdrawn {
                navigation.invalidate(key,Some("navigation corridor tile budget exceeded".into()));
            }
            // Cancelled native jobs remain counted until their actual task
            // completes. Keep draining them while refusing new dispatch.
            poll(&mut navigation);
            for route in navigation.routes.values_mut() {
                route.observation.status=NavigationStatus::BudgetExceeded;
                route.observation.desired_velocity=[0.0;3];
            }
            return;
        }
        let obsolete:Vec<_>=navigation.tiles.keys().filter(|key|!required.contains(key)).copied().collect();
        for key in obsolete {
            navigation.invalidate(key,None);navigation.tiles.remove(&key);
        }
        let removed:Vec<_>=navigation.applied_costs.iter().filter(|index| !policy.costs.contains_key(index)).copied().collect();
        for index in removed {
            let _=navigation.archipelago.set_type_index_cost(usize::from(index),1.0);
            navigation.applied_costs.remove(&index);
        }
        for (&index,&cost) in &policy.costs {
            if let Err(error)=navigation.archipelago.set_type_index_cost(usize::from(index),cost) {
                tracing::error!(target:"forge::navigation",%error,"invalid canonical navigation cost");
            }
            navigation.applied_costs.insert(index);
        }
        for key in required {
            let receipt=sources.as_ref().map_err(Clone::clone)
                .and_then(|sources|geometry::stamp(sources,key,navigation.profile,&policy));
                navigation.tiles.entry(key).or_insert_with(||TileRecord { stamp:None,island:None,
                    observation:super::NavigationTileObservation { key,revision:0,token:0,source_hash:String::new(),
                        status:NavigationStatus::WaitingForSource,source_triangles:0,polygons:0,curved_obstacles:0,curved_footprint_excess_bound_m:0.0,
                        retained_bytes:0,build_ms:0.0,error:None } });
            let stamp=match receipt {
                Ok(stamp)=>stamp,
                Err(error)=>{
                    navigation.invalidate(key,Some(error.clone()));
                    if let Some(tile)=navigation.tiles.get_mut(&key) {
                        tile.observation.status=NavigationStatus::WaitingForSource;tile.observation.error=Some(error);
                    }
                    continue;
                }
            };
            if navigation.tiles.get(&key).is_some_and(|tile|tile.stamp==Some(stamp)) { continue; }
            navigation.invalidate(key,None);
            if navigation.pending.len()>=MAX_PENDING { continue; }
            let pending_bytes=navigation.pending.iter().map(input_bytes).sum::<usize>();
            if pending_bytes>=MAX_PENDING_BYTES { continue; }
            navigation.token=navigation.token.saturating_add(1);
            let token=navigation.token;
            let revision=navigation.tiles.get(&key).map_or(1,|tile|tile.observation.revision.saturating_add(1));
            let input=context.as_ref().map_err(Clone::clone)
                .and_then(|context|geometry::collect(world,*context,key,navigation.profile,revision,token));
            match input {
                Ok(input)=>{
                    if pending_bytes.saturating_add(input_bytes(&input))>MAX_PENDING_BYTES { break; }
                    if let Some(tile)=navigation.tiles.get_mut(&key) {
                        tile.stamp=Some(stamp);tile.observation.revision=revision;
                        tile.observation.token=token;tile.observation.source_hash.clone_from(&input.source_hash);
                        tile.observation.source_triangles=input.triangles.len();
                        tile.observation.curved_obstacles=input.curved_obstacles.len();
                        tile.observation.curved_footprint_excess_bound_m=input.curved_obstacles.iter().map(|obstacle|obstacle.footprint_excess_bound_m).fold(0.0,f32::max);
                        tile.observation.status=if input.triangles.is_empty() {NavigationStatus::EmptySource} else {NavigationStatus::Queued};
                    }
                    if !input.triangles.is_empty() { navigation.pending.push_back(input); }
                }
                Err(error)=>{
                    if let Some(tile)=navigation.tiles.get_mut(&key) {
                        tile.stamp=Some(stamp);tile.observation.status=NavigationStatus::InvalidGeometry;
                        tile.observation.error=Some(error);
                    }
                }
            }
        }
        poll(&mut navigation);
        dispatch(&mut navigation);
    });
}

/// Called at the beginning of `navigation::refresh`, after physics writeback and
/// `danger_motion::observe`. Planning still consumes the previous output in Drive;
/// only the public observation is updated to the completed physical step.
fn observe_feedback(world: &mut World, navigation: &mut NavigationRuntime) {
    use crate::{
        DomainEntityId,
        gameplay::{DangerMotionActor, DangerMotionState, GameplayRuntime},
    };
    let dt = world.resource::<Time<Fixed>>().timestep().as_secs_f32();
    let tick = world.resource::<GameplayRuntime>().session().state().tick;
    for (domain, transform, motion) in world
        .query_filtered::<(
            &DomainEntityId,
            &Transform,
            &DangerMotionState,
        ), With<DangerMotionActor>>()
        .iter(world)
    {
        if let Some(route) = navigation.routes.get_mut(&domain.0) {
            route.observation.feet =
                (transform.translation - Vec3::Y * (navigation.profile.height_m * 0.5)).to_array();
            route.observation.actual_velocity = if dt > 0.0 {
                (motion.effective_translation / dt).to_array()
            } else {
                [0.0; 3]
            };
            route.observation.grounded = motion.grounded;
            route.observation.collision_count = motion.collision_count;
            route.observation.feedback_tick = motion.feedback_current.then_some(tick);
            route.observation.requested_translation = motion.feedback_current
                .then_some(motion.requested_translation)
                .flatten().map(|request| request.to_array());
        }
    }
}

fn input_bytes(input: &TileInput) -> usize {
    input.vertices.len() * 12
        + input.triangles.len() * 12
        + input
            .areas
            .iter()
            .map(|area| area.vertices_xz.len() * 8 + 12)
            .sum::<usize>()
        + input
            .curved_obstacles
            .iter()
            .map(|obstacle| obstacle.parameters.len() * 4 + 192)
            .sum::<usize>()
}

#[cfg(not(target_arch = "wasm32"))]
fn poll(navigation: &mut NavigationRuntime) {
    use bevy::tasks::{block_on, futures_lite::future::poll_once};
    let mut finished = Vec::new();
    for (index, job) in navigation.jobs.iter_mut().enumerate() {
        if let Some(result) = block_on(poll_once(&mut job.task)) {
            finished.push((index, result));
        }
    }
    for (index, result) in finished.into_iter().rev() {
        let job = navigation.jobs.swap_remove(index);
        navigation.publish(
            &job.input,
            result,
            job.started.elapsed().as_secs_f64() * 1000.0,
        );
    }
}
#[cfg(not(target_arch = "wasm32"))]
fn dispatch(navigation: &mut NavigationRuntime) {
    use super::cache::{MAX_NATIVE_JOBS, NativeJob};
    use bevy::tasks::AsyncComputeTaskPool;
    while navigation.jobs.len() < MAX_NATIVE_JOBS {
        let Some(mut input) = navigation.pending.pop_front() else {
            break;
        };
        if let Some(tile) = navigation.tiles.get_mut(&input.key) {
            tile.observation.status = NavigationStatus::Generating;
        }
        let work = input.clone();
        input.vertices = Vec::new();
        input.triangles = Vec::new();
        input.areas = Vec::new();
        let cancelled = std::sync::Arc::new(std::sync::atomic::AtomicBool::new(false));
        let flag = cancelled.clone();
        let started = std::time::Instant::now();
        navigation.jobs.push(NativeJob {
            input,
            started,
            cancelled,
            task: AsyncComputeTaskPool::get().spawn(async move {
                forge_navigation::generate_tile_cancellable(work, || {
                    flag.load(std::sync::atomic::Ordering::Relaxed)
                        || started.elapsed() >= std::time::Duration::from_secs(30)
                })
            }),
        });
    }
}

#[cfg(target_arch = "wasm32")]
fn poll(navigation: &mut NavigationRuntime) {
    let now = super::worker::now_ms();
    let expired: Vec<_> = navigation
        .jobs
        .iter()
        .filter_map(|(token, (_, started))| ((now - started) > 30_000.0).then_some(*token))
        .collect();
    for token in expired {
        super::worker::cancel(token);
        if let Some((input, started)) = navigation.jobs.remove(&token) {
            navigation.publish(
                &input,
                Err("navigation worker generation timed out after 30 seconds".into()),
                now - started,
            );
        }
    }
    for (token, result) in super::worker::poll() {
        let Some((input, started)) = navigation.jobs.remove(&token) else {
            navigation.rejected_stale += 1;
            continue;
        };
        navigation.publish(&input, result, super::worker::now_ms() - started);
    }
}
#[cfg(target_arch = "wasm32")]
fn dispatch(navigation: &mut NavigationRuntime) {
    // One independent worker instance and one active generation, never a main-thread build.
    if !navigation.jobs.is_empty() {
        return;
    }
    let Some(mut input) = navigation.pending.pop_front() else {
        return;
    };
    match super::worker::dispatch(&input) {
        Ok(()) => {
            if let Some(tile) = navigation.tiles.get_mut(&input.key) {
                tile.observation.status = NavigationStatus::Generating;
            }
            let token = input.token;
            input.vertices = Vec::new();
            input.triangles = Vec::new();
            input.areas = Vec::new();
            navigation
                .jobs
                .insert(token, (input, super::worker::now_ms()));
        }
        Err(error) => navigation.publish(&input, Err(error), 0.0),
    }
}
