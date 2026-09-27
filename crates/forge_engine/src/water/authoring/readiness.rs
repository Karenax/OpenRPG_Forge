//! CPU/ECS barrier; deliberately does not claim a displayed frame or water physics.
use super::{AuthoringWaterState, VisualValleyWaterMaterial};
use bevy::prelude::*;
use forge_core::{ContentHash, EntityId};

/// Verify exact source, accepted terrain and live owned resources before simulation.
#[must_use]
pub fn ready(world: &World, head: ContentHash, terrain: ContentHash, rivers: &[EntityId]) -> bool {
    let Some(state) = world.get_resource::<AuthoringWaterState>() else {
        return false;
    };
    if state.source_hash != Some(head)
        || state.terrain_hash != Some(terrain)
        || state.owned.len() != rivers.len()
        || world
            .get_resource::<crate::TerrainRuntimeState>()
            .is_none_or(|r| r.cache().source_document_hash != terrain)
    {
        return false;
    }
    let (Some(meshes), Some(materials), Some(banks), Some(images)) = (
        world.get_resource::<Assets<Mesh>>(),
        world.get_resource::<Assets<VisualValleyWaterMaterial>>(),
        world.get_resource::<Assets<crate::VisualValleyTerrainMaterial>>(),
        world.get_resource::<Assets<Image>>(),
    ) else {
        return false;
    };
    state.owned.iter().zip(rivers).all(|(body, id)| {
        body.id == *id
            && body
                .entities
                .iter()
                .zip(&body.meshes)
                .all(|(entity, mesh)| {
                    meshes.get(mesh).is_some()
                        && world.get::<Mesh3d>(*entity).is_some_and(|m| m.0 == *mesh)
                        && world
                            .get::<Transform>(*entity)
                            .is_some_and(|t| *t == Transform::IDENTITY)
                })
            && materials.get(&body.material).is_some()
            && banks
                .get(&body.bank)
                .is_some_and(|b| b.extension.detail_texture == body.detail)
            && images.get(&body.detail).is_some()
            && world
                .get::<MeshMaterial3d<VisualValleyWaterMaterial>>(body.entities[0])
                .is_some_and(|m| m.0 == body.material)
            && world
                .get::<MeshMaterial3d<crate::VisualValleyTerrainMaterial>>(body.entities[1])
                .is_some_and(|m| m.0 == body.bank)
    })
}
