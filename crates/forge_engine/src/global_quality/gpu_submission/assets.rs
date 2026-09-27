//! Renderer-owned asset inventory; never serialized as a project document.
use crate::{
    VisualValleyRainMaterial, VisualValleyTerrainMaterial, VisualValleyVegetationMaterial,
    VisualValleyWaterMaterial, cloud_preview::CloudMaterial,
};
use bevy::{
    asset::{AssetId, UntypedAssetId, VisitAssetDependencies},
    ecs::system::SystemParam,
    light::{EnvironmentMapLight, GeneratedEnvironmentMapLight},
    pbr::{Material, MaterialBindGroupAllocators, PreparedMaterial},
    prelude::*,
    render::{
        erased_render_asset::ErasedRenderAssets,
        mesh::{RenderMesh, allocator::MeshAllocator, morph::RenderMorphTargetAllocator},
        render_asset::RenderAssets,
        texture::GpuImage,
    },
};
use serde::Serialize;
use std::collections::HashSet;

#[derive(Default, PartialEq, Eq)]
pub(super) struct Requirements {
    meshes: HashSet<AssetId<Mesh>>,
    materials: HashSet<UntypedAssetId>,
    images: HashSet<AssetId<Image>>,
    unknown_material_entities: usize,
    missing_source_materials: usize,
    unsupported_dependencies: usize,
}

#[derive(Clone, Debug, Default, PartialEq, Eq, Serialize)]
pub struct GpuAssetEvidence {
    pub meshes_expected: usize,
    pub meshes_prepared: usize,
    pub materials_expected: usize,
    pub materials_bound: usize,
    pub images_expected: usize,
    pub images_prepared: usize,
    pub unknown_material_entities: usize,
    pub missing_source_materials: usize,
    pub unsupported_dependencies: usize,
    /// Only this explicit inventory, not all view-dependent effects or correct pixels.
    pub inventory_ready: bool,
}

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

pub(super) fn collect(world: &mut World) -> Requirements {
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
                images(&m.base, r);
                r.images.insert(m.extension.detail_texture.id());
            });
        }
        if let Some(m) = vegetation {
            material(world, &m.0, &mut requirements, |m, r| images(&m.base, r));
        }
        if let Some(m) = water {
            material(world, &m.0, &mut requirements, |m, r| images(&m.base, r));
        }
        if let Some(m) = rain {
            material(world, &m.0, &mut requirements, |m, r| images(&m.base, r));
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
pub(super) struct GpuAssets<'w> {
    meshes: Option<Res<'w, RenderAssets<RenderMesh>>>,
    allocator: Option<Res<'w, MeshAllocator>>,
    morph_targets: Option<Res<'w, RenderMorphTargetAllocator>>,
    materials: Option<Res<'w, ErasedRenderAssets<PreparedMaterial>>>,
    bindings: Option<Res<'w, MaterialBindGroupAllocators>>,
    images: Option<Res<'w, RenderAssets<GpuImage>>>,
}

impl GpuAssets<'_> {
    pub(super) fn observe(&self, required: &Requirements) -> GpuAssetEvidence {
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
        let mut evidence = GpuAssetEvidence {
            meshes_expected: required.meshes.len(),
            meshes_prepared,
            materials_expected: required.materials.len(),
            materials_bound,
            images_expected: required.images.len(),
            images_prepared,
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
        assert!(first != second); // Changed requirements reset queue completion in extract.
        assert_eq!(second.images, HashSet::from([replacement.id()]));
        world
            .resource_mut::<Assets<CloudMaterial>>()
            .remove(handle.id());
        assert_eq!(collect(&mut world).missing_source_materials, 1);
    }
}
