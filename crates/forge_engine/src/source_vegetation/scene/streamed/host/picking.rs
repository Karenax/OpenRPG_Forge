//! Read-only selection bridge for any consumer of the shared plant host.
use super::{HostStatus, State};
use crate::source_vegetation::{picking::world as picking, scene::resident::picking::Snapshot};
use bevy::prelude::World;
use forge_core::{ContentHash, EntityId};

#[derive(Clone, Debug, PartialEq, Eq)]
pub struct Identity {
    pub owner: EntityId,
    pub head: ContentHash,
    pub projection: ContentHash,
    pub generation: u64,
    pub sequence: u64,
}

impl Identity {
    #[must_use]
    pub fn matches_status(&self, status: &HostStatus) -> bool {
        status.ready
            && status.error.is_none()
            && status.head == Some(self.head)
            && status.identity == Some(self.projection)
            && status.generation == self.generation
            && status.sequence == self.sequence
    }
}

pub fn identity(world: &World) -> Result<Identity, String> {
    let state = world.get_resource::<State>().ok_or("Plant host absent")?;
    let status = world
        .get_resource::<HostStatus>()
        .ok_or("Plant status absent")?;
    if !status.ready
        || !state.published
        || !state.frame_ready
        || state.authoring_target.is_some()
        || status.head != Some(state.head)
        || status.identity != Some(state.render.identity())
        || status.error.is_some()
    {
        return Err("Plant selection is not ready for this head".into());
    }
    Ok(Identity {
        owner: state.owner,
        head: state.head,
        projection: state.render.identity(),
        generation: status.generation,
        sequence: state.render.selection_sequence(),
    })
}

pub fn snapshot(
    world: &World,
    expected: &Identity,
) -> Result<Snapshot<picking::PickingMaterial>, String> {
    if &identity(world)? != expected {
        return Err("Plant picking identity changed".into());
    }
    let state = world.get_resource::<State>().ok_or("Plant host absent")?;
    let mut snapshot = state.render.picking_snapshot(world, picking::material)?;
    if let Some(species) = &state.species {
        if !species.committed {
            return Err("Model selection is not published".into());
        }
        snapshot.include_models(
            expected.head,
            species
                .candidate
                .as_ref()
                .ok_or("Model presentation absent")?
                .picking_draws(world, expected.head)?,
        )?;
    }
    Ok(snapshot)
}
