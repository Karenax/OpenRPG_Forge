//! Renderer-owned asset inventory; never serialized as a project document.
#[cfg(any(test, feature = "physics-rapier"))]
use crate::{
    VisualValleyRainMaterial, VisualValleyTerrainMaterial, VisualValleyVegetationMaterial,
    VisualValleyWaterMaterial, cloud_preview::CloudMaterial,
};
#[cfg(any(test, feature = "physics-rapier"))]
use bevy::{
    asset::VisitAssetDependencies,
    light::{EnvironmentMapLight, GeneratedEnvironmentMapLight},
};
use bevy::{
    asset::{AssetId, UntypedAssetId},
    ecs::system::SystemParam,
    pbr::{Material, MaterialBindGroupAllocators, PreparedMaterial},
    prelude::*,
    render::{
        erased_render_asset::ErasedRenderAssets,
        mesh::{RenderMesh, allocator::MeshAllocator, morph::RenderMorphTargetAllocator},
        render_asset::RenderAssets,
        storage::{GpuShaderBuffer, ShaderBuffer},
        texture::GpuImage,
    },
};
use serde::Serialize;
use std::collections::{HashMap, HashSet};

pub(crate) const MAX_CUSTOM_MESHES: usize = 512;

#[derive(Clone, Debug, Default, PartialEq, Eq)]
pub(crate) struct Requirements {
    meshes: HashSet<AssetId<Mesh>>,
    materials: HashSet<UntypedAssetId>,
    images: HashSet<AssetId<Image>>,
    buffers: HashMap<AssetId<ShaderBuffer>, u64>,
    unknown_material_entities: usize,
    missing_source_materials: usize,
    unsupported_dependencies: usize,
}

impl Requirements {
    /// A retained inventory may cover fewer current sources only when every
    /// typed asset and every buffer size is already part of that inventory.
    pub(crate) fn contains(&self, other: &Self) -> bool {
        other.meshes.is_subset(&self.meshes)
            && other.materials.is_subset(&self.materials)
            && other.images.is_subset(&self.images)
            && other
                .buffers
                .iter()
                .all(|(id, bytes)| self.buffers.get(id) == Some(bytes))
            && self.unknown_material_entities == 0
            && other.unknown_material_entities == 0
            && self.missing_source_materials == 0
            && other.missing_source_materials == 0
            && self.unsupported_dependencies == 0
            && other.unsupported_dependencies == 0
    }

    pub(crate) fn custom<M: Material>(
        world: &World,
        meshes: &[Handle<Mesh>],
        materials: &[Handle<M>],
    ) -> Result<Self, String> {
        if meshes.is_empty()
            || materials.is_empty()
            || meshes.len() > MAX_CUSTOM_MESHES
            || materials.len() > 256
        {
            return Err("Render inventory needs bounded nonempty mesh and material sources".into());
        }
        let mut required = Self::default();
        for mesh in meshes {
            required.meshes.insert(mesh.id());
        }
        for handle in materials {
            material(world, handle, &mut required, |source, required| {
                dependencies(world, source, required);
            });
        }
        if required.images.len() > 4096
            || required.buffers.len() > 2048
            || required.missing_source_materials != 0
            || required.unsupported_dependencies != 0
            || required
                .buffers
                .values()
                .try_fold(0_u64, |sum, size| sum.checked_add(*size))
                .is_none_or(|sum| sum > 256 * 1024 * 1024)
            || !required.sources_exist::<M>(world)
        {
            return Err("Render inventory has absent or unsupported source dependencies".into());
        }
        Ok(required)
    }

    pub(crate) fn sources_exist<M: Material>(&self, world: &World) -> bool {
        let mut images = HashSet::new();
        let mut buffers = HashMap::new();
        let mut valid = true;
        for id in &self.materials {
            let source = id
                .try_typed::<M>()
                .ok()
                .and_then(|id| world.get_resource::<Assets<M>>()?.get(id));
            let Some(source) = source else {
                return false;
            };
            crate::render_inventory::visit_material_dependencies(source, &mut |id| {
                if let Ok(id) = id.try_typed::<Image>() {
                    images.insert(id);
                } else if let Ok(id) = id.try_typed::<ShaderBuffer>() {
                    if let Some(size) = buffer_size(world, id) {
                        buffers.insert(id, size);
                    } else {
                        valid = false;
                    }
                } else {
                    valid = false;
                }
            });
        }
        valid
            && images == self.images
            && buffers == self.buffers
            && self.meshes.iter().all(|id| {
                world
                    .get_resource::<Assets<Mesh>>()
                    .is_some_and(|assets| assets.contains(*id))
            })
            && self.materials.iter().all(|id| {
                id.try_typed::<M>().is_ok_and(|id| {
                    world
                        .get_resource::<Assets<M>>()
                        .is_some_and(|assets| assets.contains(id))
                })
            })
            && self.images.iter().all(|id| {
                world
                    .get_resource::<Assets<Image>>()
                    .is_some_and(|assets| assets.contains(*id))
            })
            && self
                .buffers
                .iter()
                .all(|(id, size)| buffer_size(world, *id) == Some(*size))
    }

    #[cfg(feature = "physics-rapier")]
    pub(crate) fn vegetation(
        world: &World,
        entities: impl IntoIterator<Item = Entity>,
    ) -> Option<Self> {
        let mut required = Self::default();
        for entity in entities {
            let mesh = world.get::<Mesh3d>(entity)?;
            if !world.get_resource::<Assets<Mesh>>()?.contains(mesh.id()) {
                return None;
            }
            let handle = world.get::<MeshMaterial3d<VisualValleyVegetationMaterial>>(entity)?;
            required.meshes.insert(mesh.id());
            material(world, &handle.0, &mut required, |source, required| {
                dependencies(world, source, required);
            });
        }
        Some(required)
    }

    /// Explicit subset for another renderer adapter, using the same actual GPU
    /// allocation checks as global quality. CPU handles never acknowledge it.
    #[cfg(feature = "physics-rapier")]
    pub(crate) fn subset(
        meshes: impl IntoIterator<Item = AssetId<Mesh>>,
        materials: impl IntoIterator<Item = UntypedAssetId>,
        images: impl IntoIterator<Item = AssetId<Image>>,
    ) -> Self {
        Self {
            meshes: meshes.into_iter().collect(),
            materials: materials.into_iter().collect(),
            images: images.into_iter().collect(),
            ..Self::default()
        }
    }
}
fn buffer_size(world: &World, id: AssetId<ShaderBuffer>) -> Option<u64> {
    let source = world.get_resource::<Assets<ShaderBuffer>>()?.get(id)?;
    let size = source
        .data
        .as_ref()
        .map_or(source.buffer_description.size, |data| data.len() as u64);
    (size > 0).then_some(size)
}

fn dependencies<M: Material>(world: &World, source: &M, required: &mut Requirements) {
    crate::render_inventory::visit_material_dependencies(source, &mut |id| {
        if let Ok(id) = id.try_typed::<Image>() {
            required.images.insert(id);
        } else if let Ok(id) = id.try_typed::<ShaderBuffer>() {
            if let Some(size) = buffer_size(world, id) {
                required.buffers.insert(id, size);
            } else {
                required.unsupported_dependencies += 1;
            }
        } else {
            required.unsupported_dependencies += 1;
        }
    });
}

#[derive(Clone, Debug, Default, PartialEq, Eq, Serialize)]
pub struct GpuAssetEvidence {
    pub meshes_expected: usize,
    pub meshes_prepared: usize,
    pub materials_expected: usize,
    pub materials_bound: usize,
    pub images_expected: usize,
    pub images_prepared: usize,
    #[serde(skip_serializing_if = "is_default")]
    pub buffers_expected: usize,
    #[serde(skip_serializing_if = "is_default")]
    pub buffers_prepared: usize,
    pub unknown_material_entities: usize,
    pub missing_source_materials: usize,
    pub unsupported_dependencies: usize,
    /// Only this explicit inventory, not all view-dependent effects or correct pixels.
    pub inventory_ready: bool,
}
fn is_default<T: Default + PartialEq>(value: &T) -> bool {
    *value == T::default()
}

#[cfg(any(test, feature = "physics-rapier"))]
fn images(material: &StandardMaterial, requirements: &mut Requirements) {
    material.visit_dependencies(&mut |id| match id.try_typed::<Image>() {
        Ok(id) => {
            requirements.images.insert(id);
        }
        Err(_) => requirements.unsupported_dependencies += 1,
    });
}

fn material<M: Material>(
    world: &World,
    handle: &Handle<M>,
    requirements: &mut Requirements,
    dependencies: impl FnOnce(&M, &mut Requirements),
) {
    if !requirements.materials.insert(handle.id().untyped()) {
        return;
    }
    if let Some(source) = world
        .get_resource::<Assets<M>>()
        .and_then(|assets| assets.get(handle))
    {
        dependencies(source, requirements);
    } else {
        requirements.missing_source_materials += 1;
    }
}

#[cfg(any(test, feature = "physics-rapier"))]
pub(crate) fn collect(world: &mut World) -> Requirements {
    let mut requirements = Requirements::default();
    let mut query = world.query::<(
        &Mesh3d,
        Option<&MeshMaterial3d<StandardMaterial>>,
        Option<&MeshMaterial3d<VisualValleyTerrainMaterial>>,
        Option<&MeshMaterial3d<VisualValleyVegetationMaterial>>,
        Option<&MeshMaterial3d<VisualValleyWaterMaterial>>,
        Option<&MeshMaterial3d<VisualValleyRainMaterial>>,
        Option<&MeshMaterial3d<CloudMaterial>>,
    )>();
    for (mesh, standard, terrain, vegetation, water, rain, cloud) in query.iter(world) {
        requirements.meshes.insert(mesh.id());
        let families = [
            standard.is_some(),
            terrain.is_some(),
            vegetation.is_some(),
            water.is_some(),
            rain.is_some(),
            cloud.is_some(),
        ];
        if families.iter().filter(|present| **present).count() != 1 {
            requirements.unknown_material_entities += 1;
        }
        if let Some(m) = standard {
            material(world, &m.0, &mut requirements, images);
        }
        if let Some(m) = terrain {
            material(world, &m.0, &mut requirements, |m, r| {
                dependencies(world, m, r);
            });
        }
        if let Some(m) = vegetation {
            material(world, &m.0, &mut requirements, |m, r| {
                dependencies(world, m, r);
            });
        }
        if let Some(m) = water {
            material(world, &m.0, &mut requirements, |m, r| {
                dependencies(world, m, r);
            });
        }
        if let Some(m) = rain {
            material(world, &m.0, &mut requirements, |m, r| {
                dependencies(world, m, r);
            });
        }
        if let Some(m) = cloud {
            material(world, &m.0, &mut requirements, |m, r| {
                r.images.insert(m.density.id());
            });
        }
    }
    let mut maps = world.query::<&EnvironmentMapLight>();
    for map in maps.iter(world) {
        requirements
            .images
            .extend([map.diffuse_map.id(), map.specular_map.id()]);
    }
    let mut generated = world.query::<&GeneratedEnvironmentMapLight>();
    for map in generated.iter(world) {
        requirements.images.insert(map.environment_map.id());
    }
    requirements
}

#[derive(SystemParam)]
pub(crate) struct GpuAssets<'w> {
    meshes: Option<Res<'w, RenderAssets<RenderMesh>>>,
    allocator: Option<Res<'w, MeshAllocator>>,
    morph_targets: Option<Res<'w, RenderMorphTargetAllocator>>,
    materials: Option<Res<'w, ErasedRenderAssets<PreparedMaterial>>>,
    bindings: Option<Res<'w, MaterialBindGroupAllocators>>,
    images: Option<Res<'w, RenderAssets<GpuImage>>>,
    buffers: Option<Res<'w, RenderAssets<GpuShaderBuffer>>>,
}

impl GpuAssets<'_> {
    pub(crate) fn observe(&self, required: &Requirements) -> GpuAssetEvidence {
        let meshes_prepared = required
            .meshes
            .iter()
            .filter(|id| {
                self.meshes
                    .as_ref()
                    .and_then(|assets| assets.get(**id))
                    .is_some_and(|mesh| {
                        mesh.vertex_count > 0
                            && self.allocator.as_ref().is_some_and(|allocator| {
                                allocator.mesh_vertex_slice(id).is_some()
                                    && (!mesh.indexed() || allocator.mesh_index_slice(id).is_some())
                                    && (!mesh.has_morph_targets()
                                        || allocator.mesh_morph_target_slice(id).is_some()
                                        || self.morph_targets.as_ref().is_some_and(|targets| {
                                            targets.get_image(**id).is_some()
                                        }))
                            })
                    })
            })
            .count();
        let materials_bound = required
            .materials
            .iter()
            .filter(|id| {
                self.materials
                    .as_ref()
                    .and_then(|assets| assets.get(**id))
                    .is_some_and(|material| {
                        self.bindings
                            .as_ref()
                            .and_then(|allocators| allocators.get(&id.type_id()))
                            .and_then(|allocator| allocator.get(material.binding.group))
                            .is_some_and(|slab| slab.bind_group().is_some())
                    })
            })
            .count();
        let images_prepared = required
            .images
            .iter()
            .filter(|id| {
                self.images
                    .as_ref()
                    .and_then(|assets| assets.get(**id))
                    .is_some()
            })
            .count();
        let buffers_prepared = required
            .buffers
            .iter()
            .filter(|(id, size)| {
                self.buffers
                    .as_ref()
                    .and_then(|assets| assets.get(**id))
                    .is_some_and(|source| source.buffer.size() >= **size)
            })
            .count();
        let mut evidence = GpuAssetEvidence {
            meshes_expected: required.meshes.len(),
            meshes_prepared,
            materials_expected: required.materials.len(),
            materials_bound,
            images_expected: required.images.len(),
            images_prepared,
            buffers_expected: required.buffers.len(),
            buffers_prepared,
            unknown_material_entities: required.unknown_material_entities,
            missing_source_materials: required.missing_source_materials,
            unsupported_dependencies: required.unsupported_dependencies,
            inventory_ready: false,
        };
        evidence.inventory_ready = evidence.meshes_expected > 0
            && evidence.materials_expected > 0
            && evidence.meshes_prepared == evidence.meshes_expected
            && evidence.materials_bound == evidence.materials_expected
            && evidence.images_prepared == evidence.images_expected
            && evidence.buffers_prepared == evidence.buffers_expected
            && evidence.unknown_material_entities == 0
            && evidence.missing_source_materials == 0
            && evidence.unsupported_dependencies == 0;
        evidence
    }
}

#[cfg(test)]
mod tests {
    use super::*;

    #[test]
    fn expected_sources_are_deduplicated_but_never_count_as_gpu_prepared() {
        let mut world = World::new();
        world.init_resource::<Assets<StandardMaterial>>();
        let material = world
            .resource_mut::<Assets<StandardMaterial>>()
            .add(StandardMaterial {
                base_color_texture: Some(Handle::default()),
                ..default()
            });
        for _ in 0..2 {
            world.spawn((Mesh3d(Handle::default()), MeshMaterial3d(material.clone())));
        }
        let required = collect(&mut world);
        assert_eq!(required.meshes.len(), 1);
        assert_eq!(required.materials.len(), 1);
        assert_eq!(required.images.len(), 1);
        let mut state = bevy::ecs::system::SystemState::<GpuAssets>::new(&mut world);
        let evidence = state.get(&world).unwrap().observe(&required);
        assert!(!evidence.inventory_ready);
        assert_eq!(
            (
                evidence.meshes_prepared,
                evidence.materials_bound,
                evidence.images_prepared
            ),
            (0, 0, 0)
        );
    }

    #[test]
    fn unknown_or_missing_materials_and_empty_world_remain_unqualified() {
        let mut world = World::new();
        let required = collect(&mut world);
        let mut state = bevy::ecs::system::SystemState::<GpuAssets>::new(&mut world);
        assert!(
            !state
                .get(&world)
                .unwrap()
                .observe(&required)
                .inventory_ready
        );
        world.spawn(Mesh3d(Handle::default()));
        world.spawn((
            Mesh3d(Handle::default()),
            MeshMaterial3d::<StandardMaterial>(Handle::default()),
        ));
        let required = collect(&mut world);
        assert_eq!(required.unknown_material_entities, 1);
        assert_eq!(required.missing_source_materials, 1);
    }

    #[test]
    fn cloud_inventory_tracks_density_replacement_and_missing_material_without_gpu_claim() {
        let mut world = World::new();
        world.init_resource::<Assets<CloudMaterial>>();
        world.init_resource::<Assets<Image>>();
        let density = world.resource_mut::<Assets<Image>>().add(Image::default());
        let handle = world
            .resource_mut::<Assets<CloudMaterial>>()
            .add(CloudMaterial::default());
        world
            .resource_mut::<Assets<CloudMaterial>>()
            .get_mut(&handle)
            .unwrap()
            .density = density.clone();
        world.spawn((Mesh3d(Handle::default()), MeshMaterial3d(handle.clone())));
        let first = collect(&mut world);
        assert_eq!(first.unknown_material_entities, 0);
        assert_eq!(first.missing_source_materials, 0);
        assert_eq!(first.materials.len(), 1);
        assert_eq!(first.images, HashSet::from([density.id()]));
        let mut state = bevy::ecs::system::SystemState::<GpuAssets>::new(&mut world);
        let evidence = state.get(&world).unwrap().observe(&first);
        assert!(!evidence.inventory_ready);
        assert_eq!(evidence.images_prepared, 0);
        assert_eq!(evidence.materials_bound, 0);
        let replacement = world.resource_mut::<Assets<Image>>().add(Image::default());
        world
            .resource_mut::<Assets<CloudMaterial>>()
            .get_mut(&handle)
            .unwrap()
            .density = replacement.clone();
        let second = collect(&mut world);
        assert_ne!(first, second); // Changed requirements reset queue completion in extract.
        assert_eq!(second.images, HashSet::from([replacement.id()]));
        world
            .resource_mut::<Assets<CloudMaterial>>()
            .remove(handle.id());
        assert_eq!(collect(&mut world).missing_source_materials, 1);
    }
}
