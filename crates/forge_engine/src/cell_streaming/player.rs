//! Opt-in ownership of a single default Rapier pipeline and player terrain readiness.
//! Keep all active body envelopes resident; never freeze only the visible player
//! while distant dynamics continue over evicted ground. No authored state is changed.
use super::{CellStreamingObserver, CellStreamingProtection, CellStreamingStats, State, requested};
use crate::{ForgePlayerCharacter, PlayerControllerSet, PlayerSimulationReady};
use bevy::{ecs::change_detection::Tick, prelude::*};
use bevy_rapier3d::prelude::{
    Collider, DefaultRapierContext, KinematicCharacterController, PhysicsSet, RapierConfiguration,
    RapierContextEntityLink, RigidBody,
};
use forge_world::cell_streaming::{CellDetail, CellKey, CellStreamingPolicy};
use serde::Serialize;
use std::collections::{BTreeMap, BTreeSet};

mod boundary;

#[cfg(test)]
mod tests;

#[derive(Clone, Copy, Debug, Hash, PartialEq, Eq, SystemSet)]
pub enum PlayerCellStreamingSet {
    /// Publish body coverage before player/gameplay actuators.
    Request,
    /// Hosts must publish any additional character motion before this final gate.
    Enforce,
}

#[derive(Resource, Clone, Debug, Default, Serialize)]
pub struct PlayerCellStreamingStatus {
    pub ready: bool,
    pub protected_cells: usize,
    pub blocked_fixed_ticks: u64,
    pub error: Option<String>,
}

/// Install only for a host whose single default physics pipeline and player
/// readiness are owned by this streamed terrain. Multiple contexts fail closed.
/// Hosts must order every additional motion actuator before `Enforce`.
pub struct PlayerCellStreamingPlugin;

impl Plugin for PlayerCellStreamingPlugin {
    fn build(&self, app: &mut App) {
        assert!(
            app.world().contains_resource::<State>(),
            "Install sampled cell streaming first"
        );
        let policy = app.world().resource::<State>().policy.clone();
        boundary::install(app.world_mut(), &policy);
        let request = guard
            .in_set(PlayerCellStreamingSet::Request)
            .before(PlayerControllerSet::Drive);
        #[cfg(not(target_arch = "wasm32"))]
        let request = request.before(crate::native_gameplay::NativeGameplayReadinessSet);
        app.insert_resource(PlayerSimulationReady(false))
            .init_resource::<PlayerCellStreamingStatus>()
            .add_systems(FixedUpdate, request)
            .add_systems(
                FixedUpdate,
                enforce
                    .in_set(PlayerCellStreamingSet::Enforce)
                    .after(PlayerControllerSet::Drive)
                    .after(crate::gameplay::GameplaySet::NpcDrive)
                    .before(PhysicsSet::SyncBackend),
            );
    }
}

fn guard(world: &mut World) {
    let result = refresh(world);
    let ready = result.as_ref().is_ok_and(|&value| value);
    // SyncBackend still admits the new static colliders while simulation is held.
    // Nothing advances until the next guard observes their actual backend shapes.
    for mut config in world.query::<&mut RapierConfiguration>().iter_mut(world) {
        config.physics_pipeline_active = ready;
    }
    world.resource_mut::<PlayerSimulationReady>().0 = ready;
    let count = world.resource::<CellStreamingProtection>().cells.len();
    let mut status = world.resource_mut::<PlayerCellStreamingStatus>();
    let error = result.err();
    if status.error != error
        && let Some(message) = &error
    {
        error!(target: "forge::streaming", "Streamed physics held: {message}");
    }
    status.error = error;
    status.ready = ready;
    status.protected_cells = count;
    if !status.ready {
        status.blocked_fixed_ticks = status.blocked_fixed_ticks.saturating_add(1);
    }
}

// Rapier's KCC actuator runs even with physics_pipeline_active=false. Inspect
// commands after the ordinary actuators, then remove every translation request
// when held. A zero translation still runs Rapier's contact corrections. The
// envelope margin bounds a whole next fixed step of KCC motion.
fn enforce(world: &mut World) {
    let host_ready = world.resource::<PlayerSimulationReady>().0;
    let dt = world.resource::<Time<Fixed>>().timestep().as_secs_f32();
    let profile = &world
        .resource::<crate::player_controller::PlayerRuntimeProfile>()
        .0
        .controller;
    let margin = profile.walk_speed_mps * profile.sprint_multiplier * dt * 2.0;
    let invalid = world
        .query::<&KinematicCharacterController>()
        .iter(world)
        .any(|controller| {
            controller
                .translation
                .is_some_and(|v| !v.is_finite() || v.x.hypot(v.z) > margin)
        });
    let projected_ready = refresh(world).is_ok_and(|value| value);
    if host_ready && projected_ready && !invalid {
        return;
    }
    hold_player_simulation(world);
    let mut status = world.resource_mut::<PlayerCellStreamingStatus>();
    if status.ready {
        status.blocked_fixed_ticks = status.blocked_fixed_ticks.saturating_add(1);
    }
    status.ready = false;
    if invalid {
        status.error = Some("Character motion exceeds the protected fixed-step envelope".into());
    }
}

/// Hold the player, all Rapier pipelines, and gameplay until the host's next
/// readiness decision. Call after motion actuators and before backend sync:
/// disabling Rapier alone does not prevent kinematic controller movement.
/// This operation never releases a hold or changes the reason for it.
pub fn hold_player_simulation(world: &mut World) {
    for mut controller in world
        .query::<&mut KinematicCharacterController>()
        .iter_mut(world)
    {
        controller.translation = None;
    }
    for mut configuration in world.query::<&mut RapierConfiguration>().iter_mut(world) {
        configuration.physics_pipeline_active = false;
    }
    world.resource_mut::<PlayerSimulationReady>().0 = false;
    if let Some(mut runtime) = world.get_resource_mut::<crate::gameplay::GameplayRuntime>() {
        runtime.set_enabled(false);
    }
}

#[derive(Clone, Copy)]
struct HeldValue {
    previous: bool,
    written: Tick,
}

/// A temporary host-owned hold. Release it at the beginning of the next fixed
/// step, before the other admission systems recompute their own decisions.
/// Values changed by another system after the hold are never restored over it.
#[derive(Default)]
pub struct TemporarySimulationHold {
    player: Option<HeldValue>,
    contexts: BTreeMap<Entity, HeldValue>,
}

impl TemporarySimulationHold {
    pub fn hold(&mut self, world: &mut World) {
        let player = world.resource_ref::<PlayerSimulationReady>();
        if self
            .player
            .is_none_or(|held| held.written != player.last_changed())
        {
            self.player = Some(HeldValue {
                previous: player.0,
                written: player.last_changed(),
            });
        }
        let contexts: Vec<_> = world
            .query::<(Entity, Ref<RapierConfiguration>)>()
            .iter(world)
            .map(|(entity, config)| {
                (
                    entity,
                    config.physics_pipeline_active,
                    config.last_changed(),
                )
            })
            .collect();
        self.contexts
            .retain(|entity, _| contexts.iter().any(|item| item.0 == *entity));
        for &(entity, active, tick) in &contexts {
            if self
                .contexts
                .get(&entity)
                .is_none_or(|held| held.written != tick)
            {
                self.contexts.insert(
                    entity,
                    HeldValue {
                        previous: active,
                        written: tick,
                    },
                );
            }
        }

        hold_player_simulation(world);
        if let Some(held) = &mut self.player {
            held.written = world.resource_ref::<PlayerSimulationReady>().last_changed();
        }
        for (entity, held) in &mut self.contexts {
            if let Some(config) = world.entity(*entity).get_ref::<RapierConfiguration>() {
                held.written = config.last_changed();
            }
        }
    }

    pub fn release(&mut self, world: &mut World) {
        if let Some(held) = self.player.take()
            && let Some(current) = world.get_resource_ref::<PlayerSimulationReady>()
            && current.last_changed() == held.written
            && current.0 != held.previous
        {
            world.resource_mut::<PlayerSimulationReady>().0 = held.previous;
        }
        for (entity, held) in std::mem::take(&mut self.contexts) {
            let unchanged = world
                .get_entity(entity)
                .ok()
                .and_then(|entity| entity.get_ref::<RapierConfiguration>())
                .is_some_and(|config| {
                    config.last_changed() == held.written
                        && config.physics_pipeline_active != held.previous
                });
            if unchanged && let Some(mut config) = world.get_mut::<RapierConfiguration>(entity) {
                config.physics_pipeline_active = held.previous;
            }
        }
    }
}

fn refresh(world: &mut World) -> Result<bool, String> {
    let contexts: Vec<_> = world
        .query_filtered::<Entity, With<RapierConfiguration>>()
        .iter(world)
        .take(2)
        .collect();
    if contexts.len() != 1 || world.get::<DefaultRapierContext>(contexts[0]).is_none() {
        return Err("Streamed physics requires one exclusive default Rapier context".into());
    }
    if world
        .query_filtered::<&RapierContextEntityLink, Or<(With<RigidBody>, With<KinematicCharacterController>)>>()
        .iter(world)
        .any(|link| link.0 != contexts[0])
    {
        return Err("Moving body references a different Rapier context".into());
    }
    let policy = world.resource::<State>().policy.clone();
    if !boundary::ready(world, &policy)? {
        return Ok(false);
    }
    let players: Vec<_> = world
        .query_filtered::<Entity, With<ForgePlayerCharacter>>()
        .iter(world)
        .take(2)
        .collect();
    if players.len() != 1 {
        return Err("Streamed terrain requires exactly one live player".into());
    }
    let player = crate::physics::current_global(world, players[0])
        .ok_or("Invalid player transform hierarchy")?
        .translation();
    let position = [f64::from(player.x), f64::from(player.z)];
    if world
        .get::<KinematicCharacterController>(players[0])
        .is_none()
        || world.get::<Collider>(players[0]).is_none()
    {
        return Err("Streamed player has no physical body".into());
    }
    policy.cell_at(position).map_err(|e| e.to_string())?;
    let protected = body_coverage(world, &policy)?;
    world.resource_mut::<CellStreamingObserver>().position_xz_m = position;
    world.resource_mut::<CellStreamingProtection>().cells = protected;
    let state = world.resource::<State>();
    // Planning validates counts and both byte budgets before any pipeline can advance.
    let plan = state
        .stream
        .prepare_with_protected_cells(position, &world.resource::<CellStreamingProtection>().cells)
        .map_err(|error| error.to_string())?;
    let current = requested(world, state)?;
    // A failed asynchronous job may have recorded its request without admitting
    // its payload. Never infer readiness from that request identity alone.
    if let Some(error) = &world.resource::<CellStreamingStats>().last_error {
        return Err(format!("Streamed terrain request failed: {error}"));
    }
    if state.pending.is_some() || state.last_request.as_ref() != Some(&current) {
        return Ok(false);
    }
    if state.requested_source.is_some()
        || !plan.acquisitions().is_empty()
        || !plan.retirements().is_empty()
    {
        return Ok(false);
    }
    for (key, cell) in state.stream.resident() {
        let live = state
            .live
            .get(key)
            .ok_or("Protected terrain projection absent")?;
        if !live.is_intact(world) {
            return Err("Protected terrain projection was removed or replaced".into());
        }
        if cell.detail != CellDetail::Full {
            continue;
        }
        let collider_entity = live
            .collider_entity
            .ok_or("Full streamed terrain collider is absent")?;
        if !crate::physics::static_collider_ready(world, collider_entity) {
            return Ok(false);
        }
    }
    Ok(true)
}

fn body_coverage(world: &mut World, policy: &CellStreamingPolicy) -> Result<Vec<CellKey>, String> {
    let mut cells = BTreeSet::new();
    for body in crate::physics::body_envelopes(world)? {
        let centre = body.centre;
        let end = body.predicted;
        let radius = body.radius;
        // Physical limits may clip a conservative margin, never a body whose
        // current or predicted centre is already outside this admitted world.
        for centre in [centre, end] {
            policy
                .cell_at([f64::from(centre.x), f64::from(centre.z)])
                .map_err(|error| error.to_string())?;
        }
        let minimum = centre.min(end) - Vec3::splat(radius);
        let maximum = centre.max(end) + Vec3::splat(radius);
        let (minimum, maximum) = boundary::clip(policy, minimum, maximum);
        let low = policy.cell_at(minimum).map_err(|e| e.to_string())?;
        let high = policy.cell_at(maximum).map_err(|e| e.to_string())?;
        for z in low.z..=high.z {
            for x in low.x..=high.x {
                cells.insert(CellKey { x, z });
                if cells.len() > usize::from(policy.max_resident_cells) {
                    return Err("Active body coverage exceeds the cell budget".into());
                }
            }
        }
    }
    Ok(cells.into_iter().collect())
}
