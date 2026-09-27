//! Retain geometry/buffers and surviving entities across camera selections.
use super::{ImportedPlant, Vegetation, View, far_ids, near_ids};
use crate::tidewater_plants::PlantMaterial;
use bevy::{
    camera::visibility::NoFrustumCulling, light::NotShadowCaster, mesh::MeshTag, prelude::*,
    render::storage::ShaderBuffer,
};
use std::collections::{BTreeMap, BTreeSet};
pub mod picking;

struct LevelAssets {
    mesh: Handle<Mesh>,
    material: Handle<PlantMaterial>,
    entities: BTreeMap<usize, Entity>,
    order: Vec<Entity>,
}
pub struct Resident {
    scene: Vegetation,
    levels: Vec<Vec<LevelAssets>>,
}
#[derive(Debug, Default, PartialEq, Eq)]
pub struct SelectionReport {
    pub retained: usize,
    pub spawned: usize,
    pub removed: usize,
    pub total: usize,
}

impl Resident {
    /// All validation occurs before this call; require the three asset stores
    /// before inserting anything. The source scene owns no persistent documents.
    pub fn stage(
        world: &mut World,
        scene: Vegetation,
        base: &PlantMaterial,
    ) -> Result<Self, String> {
        if !world.contains_resource::<Assets<Mesh>>()
            || !world.contains_resource::<Assets<PlantMaterial>>()
            || !world.contains_resource::<Assets<ShaderBuffer>>()
        {
            return Err("Plant asset stores unavailable".into());
        }
        let mut groups = Vec::new();
        for group in &scene.groups {
            let mut levels = Vec::new();
            if !group.records.is_empty() {
                let records = world
                    .resource_mut::<Assets<ShaderBuffer>>()
                    .add(ShaderBuffer::from(group.records.clone()));
                let models = world
                    .resource_mut::<Assets<ShaderBuffer>>()
                    .add(ShaderBuffer::from(group.models.clone()));
                for level in &group.levels {
                    let data = &level.description;
                    let mut material = base.clone();
                    material.kind = data.kind;
                    material.instances = records.clone();
                    material.models = models.clone();
                    material.params.gust_lod.z = data.range[0];
                    material.params.gust_lod.w = data.range[1];
                    material.params.lod_end_frame.x = data.range[2];
                    levels.push(LevelAssets {
                        mesh: world.resource_mut::<Assets<Mesh>>().add(level.mesh.clone()),
                        material: world.resource_mut::<Assets<PlantMaterial>>().add(material),
                        entities: BTreeMap::new(),
                        order: Vec::new(),
                    });
                }
            }
            groups.push(levels);
        }
        Ok(Self {
            scene,
            levels: groups,
        })
    }

    #[must_use]
    pub fn materials(&self) -> Vec<Handle<PlantMaterial>> {
        self.levels
            .iter()
            .flatten()
            .map(|l| l.material.clone())
            .collect()
    }
    /// Source selection order, independent of the age/order of ECS allocations.
    #[must_use]
    pub fn entities(&self) -> Vec<Entity> {
        self.levels
            .iter()
            .flatten()
            .flat_map(|l| l.order.iter().copied())
            .collect()
    }

    pub fn select(&mut self, world: &mut World, view: View) -> Result<SelectionReport, String> {
        if !view.camera.is_finite() {
            return Err("Invalid plant view".into());
        }
        let mut report = SelectionReport::default();
        for (group, levels) in self.scene.groups.iter().zip(&mut self.levels) {
            let near = near_ids(group, view.camera);
            for (level, assets) in group.levels.iter().zip(levels) {
                let mut ids = if level.description.near {
                    near.clone()
                } else {
                    far_ids(group, view.camera)
                };
                ids.truncate(level.description.capacity);
                let desired: BTreeSet<_> = ids.iter().copied().collect();
                assets.entities.retain(|index, entity| {
                    if desired.contains(index) && world.get_entity(*entity).is_ok() {
                        return true;
                    }
                    world.despawn(*entity);
                    report.removed += 1;
                    false
                });
                assets.order.clear();
                for index in ids {
                    let entity = if let Some(&entity) = assets.entities.get(&index) {
                        report.retained += 1;
                        if world.get::<Visibility>(entity) != Some(&view.visibility) {
                            world.entity_mut(entity).insert(view.visibility);
                        }
                        entity
                    } else {
                        let mut entity = world.spawn((
                            Mesh3d(assets.mesh.clone()),
                            MeshMaterial3d(assets.material.clone()),
                            MeshTag(u32::try_from(index).expect("bounded source count")),
                            Transform::IDENTITY,
                            NoFrustumCulling,
                            NotShadowCaster,
                            view.visibility,
                        ));
                        if let Some(ids) = &group.identities {
                            entity.insert(ImportedPlant {
                                id: ids[index],
                                head_hash: self.scene.head_hash,
                            });
                        }
                        let entity = entity.id();
                        assets.entities.insert(index, entity);
                        report.spawned += 1;
                        entity
                    };
                    assets.order.push(entity);
                    report.total += 1;
                }
            }
        }
        Ok(report)
    }

    /// Retire only this projection's entities. Shared textures remain library-owned.
    pub fn retire(self, world: &mut World) {
        for level in self.levels.into_iter().flatten() {
            for entity in level.entities.into_values() {
                world.despawn(entity);
            }
        }
    }
}
