//! Camera-following cell draws on the existing Bevy renderer.
//! These transient draw cells are not persisted as fabricated placeable objects.
use super::{pebble_cells::Selector, pebble_material::PebbleMaterial};
use bevy::{
    asset::RenderAssetUsages, camera::visibility::NoFrustumCulling, light::NotShadowCaster,
    mesh::MeshTag, prelude::*, render::storage::ShaderBuffer,
};

#[derive(Resource)]
pub struct ResidentField {
    mesh: Handle<Mesh>,
    material: Handle<PebbleMaterial>,
    buffer: Handle<ShaderBuffer>,
    entities: Vec<Entity>,
}

impl ResidentField {
    /// Adopt a validated shared patch after the actual terrain/material is resident.
    /// No draw is created until the caller publishes a source-selected camera frame.
    ///
    /// # Errors
    /// Refuses missing resources or a different patch population before publication.
    pub fn stage(
        world: &mut World,
        mesh: Mesh,
        material: Handle<PebbleMaterial>,
    ) -> Result<Self, String> {
        if mesh.count_vertices() != 2864
            || mesh
                .indices()
                .is_none_or(|indices| indices.len() != 4704 * 3)
        {
            return Err("Pebble patch population differs".into());
        }
        let extension = &world
            .get_resource::<Assets<PebbleMaterial>>()
            .and_then(|assets| assets.get(&material))
            .ok_or("Pebble material not resident")?
            .extension;
        let buffer = extension.cells.clone();
        let images = world
            .get_resource::<Assets<Image>>()
            .ok_or("Pebble image store unavailable")?;
        if [&extension.heights, &extension.detail, &extension.density]
            .iter()
            .any(|handle| images.get(*handle).is_none())
            || world
                .get_resource::<Assets<ShaderBuffer>>()
                .and_then(|assets| assets.get(&buffer))
                .is_none()
            || world.get_resource::<Assets<Mesh>>().is_none()
        {
            return Err("Pebble terrain, mask, mesh or cell buffer unavailable".into());
        }
        let mesh = world.resource_mut::<Assets<Mesh>>().add(mesh);
        Ok(Self {
            mesh,
            material,
            buffer,
            entities: Vec::new(),
        })
    }

    /// Publish cells from the validated source selector, preserving Float32 records.
    /// Identity transforms and disabled automatic culling match the original field.
    ///
    /// # Errors
    /// Refuses invalid frames or retired resources before changing draws or storage.
    #[allow(
        clippy::float_cmp,
        reason = "Cell padding is an exact zero ABI field, not a measured coordinate."
    )]
    pub fn synchronize(&mut self, world: &mut World, selector: &Selector) -> Result<(), String> {
        let cells = selector.slots();
        if cells.len() > 240
            || cells.iter().any(|cell| {
                cell.iter().any(|value| !value.is_finite()) || cell[2] != 0.0 || cell[3] != 0.0
            })
        {
            return Err("Pebble visible cell records differ".into());
        }
        let material = world
            .get_resource::<Assets<PebbleMaterial>>()
            .and_then(|assets| assets.get(&self.material))
            .ok_or("Pebble material retired")?;
        if material.extension.cells != self.buffer
            || world
                .get_resource::<Assets<Mesh>>()
                .and_then(|assets| assets.get(&self.mesh))
                .is_none()
            || world
                .get_resource::<Assets<ShaderBuffer>>()
                .and_then(|assets| assets.get(&self.buffer))
                .is_none()
        {
            return Err("Pebble resident resources retired or replaced".into());
        }
        let mut bytes = Vec::with_capacity(cells.len().max(1) * 16);
        for cell in cells {
            for value in cell {
                bytes.extend_from_slice(&value.to_le_bytes());
            }
        }
        // A legal storage binding remains when the source selects no visible cells.
        // No entity addresses this record and no missing source pixels are replaced.
        if bytes.is_empty() {
            bytes.resize(16, 0);
        }
        *world
            .resource_mut::<Assets<ShaderBuffer>>()
            .get_mut(&self.buffer)
            .ok_or("Pebble cell buffer retired")? =
            ShaderBuffer::new(&bytes, RenderAssetUsages::default());
        while self.entities.len() > cells.len() {
            if let Some(entity) = self.entities.pop() {
                world.despawn(entity);
            }
        }
        for index in 0..cells.len() {
            let tag = u32::try_from(index).map_err(|error| error.to_string())?;
            let components = (
                Mesh3d(self.mesh.clone()),
                MeshMaterial3d(self.material.clone()),
                MeshTag(tag),
                Transform::IDENTITY,
                Visibility::Inherited,
                NoFrustumCulling,
                NotShadowCaster,
            );
            if let Some(entity) = self.entities.get(index).copied()
                && world.get_entity(entity).is_ok()
            {
                world.entity_mut(entity).insert(components);
            } else {
                let entity = world.spawn(components).id();
                if index < self.entities.len() {
                    self.entities[index] = entity;
                } else {
                    self.entities.push(entity);
                }
            }
        }
        Ok(())
    }

    /// Retire only this field's draws and patch; caller-owned materials/maps survive.
    pub fn clear(self, world: &mut World) {
        for entity in self.entities {
            world.despawn(entity);
        }
        if let Some(mut meshes) = world.get_resource_mut::<Assets<Mesh>>() {
            meshes.remove(self.mesh.id());
        }
    }
}
