//! Acquired pier animation follows the same canonical placements as geometry.
use super::super::{imported_buildings, placements};
use bevy::{ecs as bevy_ecs, prelude::*};
use forge_core::EntityId;
use forge_project::zone_authoring::placements::ManualPlacement;
use forge_tidewater::{
    scene_wind::WindFrame, tidewater_pier::ResidentPier,
    tidewater_pier_animation::ResidentPierMotion,
};
use std::{
    cell::RefCell,
    collections::{BTreeMap, BTreeSet},
};

thread_local! {
    static READY:RefCell<Result<bool,String>>=const{RefCell::new(Ok(false))};
}
struct Instance {
    placement: ManualPlacement,
    motion: ResidentPierMotion,
}
#[derive(Resource, Default)]
struct State {
    instances: BTreeMap<EntityId, Instance>,
}

pub(super) fn install(app: &mut App) {
    READY.with(|slot| *slot.borrow_mut() = Ok(false));
    app.init_resource::<State>()
        .add_systems(Update, animate.after(super::wind::advance_clock));
}
pub(super) fn ready() -> Result<bool, String> {
    READY.with(|slot| slot.borrow().clone())
}

fn animate(world: &mut World) {
    if !crate::authoring_viewport::can_project_sources() || super::wind::ready() != Ok(true) {
        READY.with(|slot| *slot.borrow_mut() = Ok(false));
        return;
    }
    let Some(package) = imported_buildings::pier_motion_package() else {
        world.resource_mut::<State>().instances.clear();
        READY.with(|slot| *slot.borrow_mut() = Ok(true));
        return;
    };
    let desired = placements::desired_piers();
    let ids: BTreeSet<_> = desired.iter().map(|p| p.id).collect();
    if ids.len() != desired.len() {
        READY.with(|slot| {
            *slot.borrow_mut() = Err("Duplicate canonical pier motion instance".into());
        });
        return;
    }
    let Some(time) = world.get_resource::<Time>() else {
        return;
    };
    // Bind/rebind and removal still follow canonical edits; a zero delta retains
    // the acquired pendulum pose without advancing decorative animation.
    let delta = if world.contains_resource::<crate::authoring_render::FastEditing>() {
        0.0
    } else {
        time.delta_secs_f64()
    };
    let wind = world.resource::<WindFrame>().wind();
    let geometry_ready = world
        .get_resource::<ResidentPier>()
        .is_some_and(|geometry| {
            geometry.is_synchronized(world)
                && desired.iter().all(|p| {
                    geometry
                        .authored_roots()
                        .iter()
                        .any(|(_, id, t)| *id == p.id && *t == p.transform)
                })
        });
    if !desired.is_empty() && !geometry_ready {
        // The PostUpdate geometry projection may be rebuilding this frame.
        // Keep pendulum state until the next valid hierarchy is available.
        READY.with(|slot| *slot.borrow_mut() = Ok(false));
        return;
    }
    world.resource_scope(|world, mut state: Mut<State>| {
        // Geometry and emitter collectors retire their own derived entities.
        // Removal here drops only motion state; it does not edit the document.
        state.instances.retain(|id, _| ids.contains(id));
        let result = (|| -> Result<bool, String> {
            for placement in &desired {
                if let Some(instance) = state.instances.get_mut(&placement.id) {
                    if instance.placement != *placement {
                        instance.motion.rebind(world, &package, placement)?;
                        instance.placement = placement.clone();
                    }
                    if instance.motion.advance(world, delta, wind).is_err() {
                        // Library reacquisition can recreate entities without a
                        // document change. Rebind preserves acquired phases/time.
                        instance.motion.rebind(world, &package, placement)?;
                        instance.motion.advance(world, delta, wind)?;
                    }
                } else {
                    let mut motion = ResidentPierMotion::bind(world, &package, placement)?;
                    motion.advance(world, delta, wind)?;
                    state.instances.insert(
                        placement.id,
                        Instance {
                            placement: placement.clone(),
                            motion,
                        },
                    );
                }
            }
            Ok(true)
        })();
        READY.with(|slot| *slot.borrow_mut() = result);
    });
}
