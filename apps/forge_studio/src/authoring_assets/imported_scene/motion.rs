//! Acquired pier animation follows the same canonical placements as geometry.
use super::super::{imported_buildings, placements};
use bevy::{ecs as bevy_ecs, prelude::*};
use forge_engine::{
    pendulum_motion::host::collection::{Collection, Desired},
    source_wind::WindFrame,
};
use forge_tidewater::{tidewater_pier::ResidentPier, tidewater_pier_animation::ResidentPierMotion};
use std::cell::RefCell;

thread_local! {
    static READY:RefCell<Result<bool,String>>=const{RefCell::new(Ok(false))};
}
#[derive(Resource, Default)]
struct State {
    instances: Collection<ResidentPierMotion>,
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
        world.resource_scope(|world, mut state: Mut<State>| state.instances.clear(world));
        READY.with(|slot| *slot.borrow_mut() = Ok(true));
        return;
    };
    let desired = placements::desired_piers();
    let desired = match Desired::new(&desired) {
        Ok(desired) => desired,
        Err(error) => {
            READY.with(|slot| *slot.borrow_mut() = Err(error));
            return;
        }
    };
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
                && desired.placements().iter().all(|p| {
                    geometry
                        .authored_roots()
                        .iter()
                        .any(|(_, id, t)| *id == p.id && *t == p.transform)
                })
        });
    if !desired.placements().is_empty() && !geometry_ready {
        // The PostUpdate geometry projection may be rebuilding this frame.
        // Keep pendulum state until the next valid hierarchy is available.
        READY.with(|slot| *slot.borrow_mut() = Ok(false));
        return;
    }
    world.resource_scope(|world, mut state: Mut<State>| {
        // Geometry and emitter collectors retire their own derived entities.
        // The common collection owns only motion state, never the document.
        let result = state
            .instances
            .update(world, &package, &desired, delta, wind)
            .map(|()| true);
        READY.with(|slot| *slot.borrow_mut() = result);
    });
}
