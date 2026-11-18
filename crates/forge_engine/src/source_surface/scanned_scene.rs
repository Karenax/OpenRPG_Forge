//! Scanned draw adoption under existing common-placement roots.
//! Collision ownership and custom shadow submission remain with the scene adapter.
use super::{
    scanned_draws::{Frame, MAX_PLACEMENTS},
    scanned_material::ScannedMaterial,
};
use crate::{ManualPlacementPart, ManualPlacementProjection};
use bevy::{
    asset::RenderAssetUsages, light::NotShadowCaster, mesh::MeshTag, prelude::*,
    render::storage::ShaderBuffer,
};
use forge_core::EntityId;
use std::collections::BTreeMap;

#[cfg(test)]
#[path = "scanned_scene_tests.rs"]
mod tests;

pub struct Prototype {
    pub asset: u8,
    pub lod: u8,
    pub mesh: Mesh,
}

pub struct ResidentDraws {
    meshes: BTreeMap<(u8, u8), Handle<Mesh>>,
    entities: BTreeMap<(EntityId, u8), Entity>,
    material: Handle<ScannedMaterial>,
    buffer: Handle<ShaderBuffer>,
    records_capacity: usize,
}

#[derive(Component)]
struct DrawIdentity(EntityId, u8);

impl ResidentDraws {
    pub(super) fn owns(&self, world: &World, entity: Entity) -> bool {
        world
            .get::<DrawIdentity>(entity)
            .is_some_and(|id| self.entities.get(&(id.0, id.1)) == Some(&entity))
    }

    pub(super) fn prototype_count(&self, world: &World, asset: u8) -> usize {
        let mesh_assets = world.resource::<Assets<Mesh>>();
        (0..3)
            .filter(|lod| {
                self.meshes
                    .get(&(asset, *lod))
                    .is_some_and(|mesh| mesh_assets.get(mesh).is_some())
            })
            .count()
    }

    /// Install the twelve shared prototypes once, using an already resident material.
    ///
    /// # Errors
    /// Refuses missing stores/material/storage, or duplicate/incomplete prototypes.
    pub fn stage(
        world: &mut World,
        prototypes: Vec<Prototype>,
        placement_count: usize,
        material: Handle<ScannedMaterial>,
    ) -> Result<Self, String> {
        let mut keys = std::collections::BTreeSet::new();
        if placement_count > MAX_PLACEMENTS
            || prototypes.len() != 12
            || prototypes
                .iter()
                .any(|p| p.asset >= 4 || p.lod >= 3 || !keys.insert((p.asset, p.lod)))
        {
            return Err("Scanned prototype population differs".into());
        }
        let buffer = world
            .get_resource::<Assets<ScannedMaterial>>()
            .and_then(|assets| assets.get(&material))
            .ok_or("Scanned material not resident")?
            .extension
            .instances
            .clone();
        if world
            .get_resource::<Assets<ShaderBuffer>>()
            .and_then(|a| a.get(&buffer))
            .is_none()
            || world.get_resource::<Assets<Mesh>>().is_none()
        {
            return Err("Scanned mesh or instance-buffer store unavailable".into());
        }
        // Two simultaneous LODs per accepted placement. Frustum/LOD changes must
        // update contents without resizing: Bevy reuses a GPU buffer only while
        // its byte length is stable, and material bindings retain that buffer.
        let records_capacity = placement_count.max(1) * 2;
        *world
            .resource_mut::<Assets<ShaderBuffer>>()
            .get_mut(&buffer)
            .ok_or("Scanned buffer retired")? = ShaderBuffer::new(
            &vec![0; records_capacity * 16],
            RenderAssetUsages::default(),
        );
        world
            .resource_mut::<Assets<ScannedMaterial>>()
            .get_mut(&material)
            .ok_or("Scanned material retired")?
            .extension
            .instances = buffer.clone();
        let meshes = prototypes
            .into_iter()
            .map(|p| {
                (
                    (p.asset, p.lod),
                    world.resource_mut::<Assets<Mesh>>().add(p.mesh),
                )
            })
            .collect();
        Ok(Self {
            meshes,
            entities: BTreeMap::new(),
            material,
            buffer,
            records_capacity,
        })
    }

    // Called only after synchronize validates the complete frame and resident assets.
    fn upload_records(&mut self, world: &mut World, records: &[[f32; 4]]) -> Result<(), String> {
        let previous_capacity = self.records_capacity;
        if records.len() > self.records_capacity {
            self.records_capacity = records.len().next_power_of_two();
        }
        let mut bytes = Vec::with_capacity(self.records_capacity * 16);
        for record in records {
            for value in record {
                bytes.extend_from_slice(&value.to_le_bytes());
            }
        }
        // Empty and partially visible frames keep the same allocation. Padding
        // is unaddressed by this validated frame, not replacement source data.
        bytes.resize(self.records_capacity * 16, 0);
        {
            let mut buffers = world.resource_mut::<Assets<ShaderBuffer>>();
            let mut resident = buffers
                .get_mut(&self.buffer)
                .ok_or("Scanned buffer retired")?;
            *resident = ShaderBuffer::new(&bytes, RenderAssetUsages::default());
        }
        if self.records_capacity != previous_capacity {
            // Authoring may add placements beyond the initial reserve. Explicitly
            // reprepare the material only when growth replaces the GPU allocation.
            world
                .resource_mut::<Assets<ScannedMaterial>>()
                .get_mut(&self.material)
                .ok_or("Scanned material retired")?
                .extension
                .instances = self.buffer.clone();
        }
        Ok(())
    }

    /// Publish one validated frame, reusing unchanged draw entities and mesh handles.
    /// Common roots own edited transforms, identity, collisions and deletion policy.
    ///
    /// # Errors
    /// Refuses missing/stale common roots, duplicate draws, invalid tags/records and
    /// missing renderer assets before mutating the buffer or any entity.
    #[allow(
        clippy::float_cmp,
        reason = "The record asset ordinal is an exact integer f32 ABI field, not a measured scalar."
    )]
    pub fn synchronize(
        &mut self,
        world: &mut World,
        frame: &Frame,
        roots: &BTreeMap<EntityId, Entity>,
    ) -> Result<(), String> {
        if frame.draws.len() != frame.records.len() || frame.draws.len() > MAX_PLACEMENTS * 2 {
            return Err("Scanned frame population differs".into());
        }
        let mut wanted = BTreeMap::new();
        let mesh_assets = world
            .get_resource::<Assets<Mesh>>()
            .ok_or("Scanned meshes unavailable")?;
        for (index, draw) in frame.draws.iter().enumerate() {
            let record = frame.records[index];
            let root = *roots
                .get(&draw.placement_id)
                .ok_or("Scanned common root absent")?;
            let projection = world
                .get::<ManualPlacementProjection>(root)
                .ok_or("Scanned root has no common placement")?;
            if usize::try_from(draw.tag).ok() != Some(index)
                || projection.0.id != draw.placement_id
                || projection.0.transform != draw.transform
                || record.iter().any(|v| !v.is_finite())
                || record[0] != f32::from(draw.asset)
                || !(0.0..=1.0).contains(&record[2])
                || ![0.0, 1.0].contains(&record[3])
                || wanted
                    .insert((draw.placement_id, draw.lod), (root, draw.tag))
                    .is_some()
                || self
                    .meshes
                    .get(&(draw.asset, draw.lod))
                    .and_then(|h| mesh_assets.get(h))
                    .is_none()
            {
                return Err("Scanned frame or common root differs".into());
            }
        }
        if world
            .get_resource::<Assets<ScannedMaterial>>()
            .and_then(|a| a.get(&self.material))
            .is_none()
            || world
                .get_resource::<Assets<ShaderBuffer>>()
                .and_then(|a| a.get(&self.buffer))
                .is_none()
        {
            return Err("Scanned material or buffer retired".into());
        }
        self.upload_records(world, &frame.records)?;
        for key in self.entities.keys().copied().collect::<Vec<_>>() {
            if !wanted.contains_key(&key)
                && let Some(entity) = self.entities.remove(&key)
            {
                world.despawn(entity);
            }
        }
        for draw in &frame.draws {
            let key = (draw.placement_id, draw.lod);
            let (root, tag) = wanted[&key];
            let entity = self
                .entities
                .get(&key)
                .copied()
                .filter(|e| world.get_entity(*e).is_ok());
            let components = (
                Mesh3d(self.meshes[&(draw.asset, draw.lod)].clone()),
                MeshMaterial3d(self.material.clone()),
                MeshTag(tag),
                Transform::IDENTITY,
                Visibility::Inherited,
                ChildOf(root),
                ManualPlacementPart(draw.placement_id),
                DrawIdentity(draw.placement_id, draw.lod),
                NotShadowCaster,
            );
            let entity = if let Some(entity) = entity {
                world.entity_mut(entity).insert(components);
                entity
            } else {
                world.spawn(components).id()
            };
            self.entities.insert(key, entity);
        }
        Ok(())
    }
}
