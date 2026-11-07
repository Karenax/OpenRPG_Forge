//! Both viewport consumers use the same ID pass and their actual resident material.
use super::{State, picking};
use bevy::prelude::*;
use forge_core::{ContentHash, EntityId};
use forge_engine::source_vegetation::{
    picking::{render, world as world_picking},
    scene::streamed::host::picking as host,
};

pub(super) enum Pass {
    Captured(Box<render::Pass>),
    World(Box<render::Pass<world_picking::PickingMaterial>>),
}

impl Pass {
    pub(super) fn spawn(
        world: &mut World,
        head: ContentHash,
        generation: u64,
        module_revision: u64,
        view: render::View,
    ) -> Result<(Self, Option<host::Identity>), String> {
        if crate::authoring_module_viewport::installed() {
            if !crate::authoring_module_viewport::ready()? {
                return Err("La carte doit terminer sa préparation avant la sélection.".into());
            }
            let key = host::identity(world)?;
            if key.head != head {
                return Err("Projection végétale non synchronisée".into());
            }
            let snapshot = host::snapshot(world, &key)?;
            return Ok((
                Self::World(Box::new(render::Pass::spawn(world, snapshot, view)?)),
                Some(key),
            ));
        }
        let state = world
            .get_resource::<State>()
            .ok_or("Plantes non projetées")?;
        if state.source != Some((head, generation, module_revision)) || state.failed.is_some() {
            return Err("Projection végétale non synchronisée".into());
        }
        let mut snapshot = state
            .resident
            .as_ref()
            .ok_or("Plantes non projetées")?
            .picking_snapshot(world, picking::material)?;
        if let Some(species) = &state.species {
            if !state.species_committed {
                return Err("Modèles végétaux non synchronisés".into());
            }
            snapshot.include_models(head, species.picking_draws(world, head)?)?;
        }
        Ok((
            Self::Captured(Box::new(render::Pass::spawn(world, snapshot, view)?)),
            None,
        ))
    }

    pub(super) fn target(&self) -> &Handle<Image> {
        match self {
            Self::Captured(pass) => &pass.target,
            Self::World(pass) => &pass.target,
        }
    }

    pub(super) fn shader_id(&self) -> AssetId<Shader> {
        match self {
            Self::Captured(_) => picking::shader_id(),
            Self::World(_) => world_picking::shader_id(),
        }
    }

    pub(super) fn has_source_batches(&self) -> bool {
        match self {
            Self::Captured(pass) => !pass.snapshot.batches.is_empty(),
            Self::World(pass) => !pass.snapshot.batches.is_empty(),
        }
    }

    pub(super) fn has_models(&self) -> bool {
        match self {
            Self::Captured(pass) => !pass.snapshot.models.is_empty(),
            Self::World(pass) => !pass.snapshot.models.is_empty(),
        }
    }

    pub(super) fn resolve(&self, id: u32, head: ContentHash) -> Result<Option<EntityId>, String> {
        match self {
            Self::Captured(pass) => pass.snapshot.resolve(id, head),
            Self::World(pass) => pass.snapshot.resolve(id, head),
        }
    }

    pub(super) fn add_occluders(
        &mut self,
        world: &mut World,
        input: super::Prepared,
    ) -> Result<(), String> {
        match self {
            Self::Captured(pass) => pass.add_occluders(world, input),
            Self::World(pass) => pass.add_occluders(world, input),
        }
    }

    pub(super) fn retire(self, world: &mut World) {
        match self {
            Self::Captured(pass) => pass.retire(world),
            Self::World(pass) => pass.retire(world),
        }
    }
}
