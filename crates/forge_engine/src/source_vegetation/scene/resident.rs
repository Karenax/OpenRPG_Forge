//! Retain geometry/buffers and surviving entities across camera selections.
use super::super::world::SourceMaterial;
use super::{ImportedPlant, Vegetation, View};
use bevy::{
    camera::visibility::NoFrustumCulling, light::NotShadowCaster, mesh::MeshTag, prelude::*,
    render::storage::ShaderBuffer,
};
use std::collections::{BTreeMap, BTreeSet};
pub mod picking;

struct LevelAssets<M: SourceMaterial> {
    mesh: Handle<Mesh>,
    material: Handle<M>,
    entities: BTreeMap<usize, Entity>,
    order: Vec<Entity>,
}
pub struct Resident<M: SourceMaterial> {
    scene: Vegetation,
    levels: Vec<Vec<LevelAssets<M>>>,
    identities: BTreeSet<forge_core::EntityId>,
}
#[derive(Debug, Default, PartialEq, Eq)]
pub struct SelectionReport {
    pub retained: usize,
    pub spawned: usize,
    pub removed: usize,
    pub total: usize,
}

impl<M: SourceMaterial> Resident<M> {
    /// All validation occurs before this call; require the three asset stores
    /// before inserting anything. The source scene owns no persistent documents.
    pub fn stage(world: &mut World, scene: Vegetation, base: &M) -> Result<Self, String> {
        scene.validate()?;
        if !world.contains_resource::<Assets<Mesh>>()
            || !world.contains_resource::<Assets<M>>()
            || !world.contains_resource::<Assets<ShaderBuffer>>()
        {
            return Err("Plant asset stores unavailable".into());
        }
        if scene
            .groups
            .iter()
            .flat_map(|group| &group.levels)
            .any(|level| level.description.kind > M::MAX_KIND)
        {
            return Err("Plant source kind is unsupported by this material".into());
        }
        for group in &scene.groups {
            for level in &group.levels {
                M::validate_records(level.description.kind, &group.records)?;
            }
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
                    material.set_source(data.kind, records.clone(), models.clone());
                    material.params_mut().gust_lod.z = data.range[0];
                    material.params_mut().gust_lod.w = data.range[1];
                    material.params_mut().lod_end_frame.x = data.range[2];
                    levels.push(LevelAssets {
                        mesh: world.resource_mut::<Assets<Mesh>>().add(level.mesh.clone()),
                        material: world.resource_mut::<Assets<M>>().add(material),
                        entities: BTreeMap::new(),
                        order: Vec::new(),
                    });
                }
            }
            groups.push(levels);
        }
        let identities = scene
            .groups
            .iter()
            .filter_map(|group| group.identities.as_ref())
            .flatten()
            .copied()
            .collect();
        Ok(Self {
            scene,
            levels: groups,
            identities,
        })
    }

    #[must_use]
    pub fn materials(&self) -> Vec<Handle<M>> {
        self.levels
            .iter()
            .flatten()
            .map(|l| l.material.clone())
            .collect()
    }

    #[must_use]
    pub fn head_hash(&self) -> Option<forge_core::ContentHash> {
        self.scene.head_hash
    }
    /// Source selection order, independent of the age/order of ECS allocations.
    #[must_use]
    pub fn entities(&self) -> Vec<Entity> {
        self.iter_entities().collect()
    }

    pub fn iter_entities(&self) -> impl Iterator<Item = Entity> + '_ {
        self.levels
            .iter()
            .flatten()
            .flat_map(|l| l.order.iter().copied())
    }

    #[must_use]
    pub fn entity_count(&self) -> usize {
        self.levels
            .iter()
            .flatten()
            .map(|level| level.order.len())
            .sum()
    }

    pub fn select(&mut self, world: &mut World, view: View) -> Result<SelectionReport, String> {
        let selected = self.selection_indices(view.camera)?;
        self.select_indices(world, &selected, view.visibility)
    }

    pub fn selection_indices(&self, camera: Vec3) -> Result<Vec<Vec<Vec<usize>>>, String> {
        self.scene.selection_indices(camera)
    }

    /// Plan the ordinary source selection while removing models replaced by an
    /// owning host. Canonical records, ordering, buffers and collisions survive.
    /// Unknown replacement identities fail before any entity changes.
    pub fn selection_indices_excluding(
        &self,
        camera: Vec3,
        replaced: &BTreeSet<forge_core::EntityId>,
    ) -> Result<Vec<Vec<Vec<usize>>>, String> {
        if !replaced.is_subset(&self.identities) {
            return Err("Replacement selection contains an unknown source plant".into());
        }
        let mut selected = self.selection_indices(camera)?;
        if !replaced.is_empty() {
            for (group, levels) in self.scene.groups.iter().zip(&mut selected) {
                if let Some(identities) = &group.identities {
                    for indices in levels {
                        indices.retain(|&index| !replaced.contains(&identities[index]));
                    }
                }
            }
        }
        Ok(selected)
    }

    /// The host must admit the replacement's renderer resources before calling
    /// this and fence the combined publication before allowing gameplay.
    pub fn select_excluding(
        &mut self,
        world: &mut World,
        view: View,
        replaced: &BTreeSet<forge_core::EntityId>,
    ) -> Result<SelectionReport, String> {
        let selected = self.selection_indices_excluding(view.camera, replaced)?;
        self.select_indices(world, &selected, view.visibility)
    }

    pub fn entities_with_lod(&self) -> impl Iterator<Item = (Entity, usize)> + '_ {
        self.levels.iter().flat_map(|levels| {
            levels.iter().enumerate().flat_map(|(lod, level)| {
                level.order.iter().copied().map(move |entity| (entity, lod))
            })
        })
    }

    #[must_use]
    pub fn handles(&self) -> (Vec<Handle<Mesh>>, Vec<Handle<M>>) {
        (
            self.levels
                .iter()
                .flatten()
                .map(|level| level.mesh.clone())
                .collect(),
            self.materials(),
        )
    }

    pub fn is_intact(&self, world: &World) -> bool {
        self.sources_intact(world) && self.entities_intact(world)
    }

    pub fn sources_intact(&self, world: &World) -> bool {
        self.scene
            .groups
            .iter()
            .zip(&self.levels)
            .all(|(group, levels)| {
                levels.iter().zip(&group.levels).all(|(level, source)| {
                    world
                        .get_resource::<Assets<Mesh>>()
                        .is_some_and(|assets| assets.contains(level.mesh.id()))
                        && world
                            .get_resource::<Assets<M>>()
                            .and_then(|assets| assets.get(level.material.id()))
                            .is_some_and(|material| {
                                let range = source.description.range;
                                material.kind() == source.description.kind
                                    && material.params().lod_end_frame.z.to_bits()
                                        == 0.0_f32.to_bits()
                                    && material.params().lod_end_frame.w.to_bits()
                                        == 0.0_f32.to_bits()
                                    && [
                                        material.params().gust_lod.z,
                                        material.params().gust_lod.w,
                                        material.params().lod_end_frame.x,
                                    ]
                                    .map(f32::to_bits)
                                        == range.map(f32::to_bits)
                            })
                })
            })
    }

    fn entities_intact(&self, world: &World) -> bool {
        self.scene
            .groups
            .iter()
            .zip(&self.levels)
            .all(|(group, levels)| {
                levels.iter().all(|level| {
                    level.entities.iter().all(|(&index, &entity)| {
                        world
                            .get::<Mesh3d>(entity)
                            .is_some_and(|mesh| mesh.0 == level.mesh)
                            && world
                                .get::<MeshMaterial3d<M>>(entity)
                                .is_some_and(|material| material.0 == level.material)
                            && world
                                .get::<MeshTag>(entity)
                                .is_some_and(|tag| usize::try_from(tag.0).ok() == Some(index))
                            && world.get::<Transform>(entity) == Some(&Transform::IDENTITY)
                            && world.get::<ChildOf>(entity).is_none()
                            && world.get::<Visibility>(entity) == Some(&Visibility::Visible)
                            && world.get::<NoFrustumCulling>(entity).is_some()
                            && world.get::<NotShadowCaster>(entity).is_none() == M::enable_shadows()
                            && group.identities.as_ref().is_none_or(|ids| {
                                world.get::<ImportedPlant>(entity).is_some_and(|plant| {
                                    plant.id == ids[index]
                                        && plant.head_hash == self.scene.head_hash
                                })
                            })
                    })
                })
            })
    }

    /// The caller validates the complete partition plan and its real GPU fence first.
    pub fn select_indices(
        &mut self,
        world: &mut World,
        selected: &[Vec<Vec<usize>>],
        visibility: Visibility,
    ) -> Result<SelectionReport, String> {
        if selected.len() != self.scene.groups.len() {
            return Err("Plant selection omits a source group".into());
        }
        for ((group, assets), selection) in self.scene.groups.iter().zip(&self.levels).zip(selected)
        {
            if selection.len() != assets.len()
                || selection.iter().any(|ids| {
                    ids.iter().any(|&id| id >= group.records.len())
                        || ids.iter().copied().collect::<BTreeSet<_>>().len() != ids.len()
                })
            {
                return Err("Invalid plant level/instance selection".into());
            }
        }
        let mut report = SelectionReport::default();
        for ((group, levels), selection) in
            self.scene.groups.iter().zip(&mut self.levels).zip(selected)
        {
            for (assets, ids) in levels.iter_mut().zip(selection) {
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
                for &index in ids {
                    let entity = if let Some(&entity) = assets.entities.get(&index) {
                        report.retained += 1;
                        if world.get::<Visibility>(entity) != Some(&visibility) {
                            world.entity_mut(entity).insert(visibility);
                        }
                        entity
                    } else {
                        let mut entity = world.spawn((
                            Mesh3d(assets.mesh.clone()),
                            MeshMaterial3d(assets.material.clone()),
                            MeshTag(u32::try_from(index).expect("bounded source count")),
                            Transform::IDENTITY,
                            NoFrustumCulling,
                            visibility,
                        ));
                        if !M::enable_shadows() {
                            entity.insert(NotShadowCaster);
                        }
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
