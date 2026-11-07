//! Studio input bridge for source scenes owned by the shared engine.
use bevy::prelude::*;
pub(super) use forge_engine::source_graph_runtime::{
    RuntimePolicy, SourceClock, SourceFrameStatus, release, retain, update,
};
use forge_engine::source_graph_runtime::{
    SourceSceneResidents, SourceSceneRuntimePlugin, SourceSceneSystems,
};
use leptos::prelude::Callable;

/// Runs before owner Update systems, so this explicit delta reaches the same frame.
fn forward_particle_inputs(world: &mut World) {
    let controls = super::source_graph_particles::take_controls();
    world.resource_scope(|world, residents: Mut<SourceSceneResidents>| {
        let mut residents = residents;
        for control in controls {
            let root = match control.target {
                super::source_graph_particles::ControlTarget::Preview => {
                    super::source_graph_preview::current_root(world)
                }
                super::source_graph_particles::ControlTarget::Placement(id) => {
                    let roots: Vec<_> = world
                        .query::<(Entity, &forge_engine::ManualPlacementProjection)>()
                        .iter(world)
                        .filter(|(entity, placement)| {
                            placement.0.id == id && residents.contains(*entity)
                        })
                        .map(|(entity, _)| entity)
                        .collect();
                    if roots.len() == 1 {
                        Some(roots[0])
                    } else {
                        None
                    }
                }
            };
            let result = root
                .ok_or("Effet chargé introuvable".to_owned())
                .and_then(|root| residents.push_particle_event(root, control.event));
            control.reply.run(result);
        }
    });
}
pub(super) fn register(app: &mut App) {
    app.add_plugins(SourceSceneRuntimePlugin::default());
    app.add_systems(
        PreUpdate,
        forward_particle_inputs.in_set(SourceSceneSystems::HostInput),
    );
}
