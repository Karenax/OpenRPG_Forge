//! Schedule the common source-placement owner alongside Studio projections.
use bevy::prelude::*;
use std::sync::Arc;

pub(super) use forge_engine::source_graph_placement::Status;

pub(super) fn submit(
    commands: &mut Commands,
    root: Entity,
    carrier: Arc<[u8]>,
    physics: Option<forge_engine::source_graph_mount::SourcePhysicsPayload>,
    particles: Option<forge_engine::source_graph_mount::SourceParticlePayload>,
) {
    forge_engine::source_graph_placement::submit(commands, root, carrier, physics, particles, 31);
}

pub(super) fn remove(commands: &mut Commands, root: Entity) {
    forge_engine::source_graph_placement::remove(commands, root);
}

pub(super) fn register(app: &mut App) {
    app.configure_sets(
        PostUpdate,
        forge_engine::source_graph_runtime::SourceSceneSystems::Placements
            .after(super::preview::PreviewSystems::Requests)
            .after(super::placements::PlacementSystems::Projection),
    );
}
