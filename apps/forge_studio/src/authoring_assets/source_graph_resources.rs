//! Registration for the common source scene host, using the canonical intake.
//! Explicit frame/clock inputs remain the caller's qualification responsibility.
//! Resource admission is not evidence of rendered equivalence or Studio readiness.
use super::source_graph_scene::{OrdinaryPrimitive, SceneBindings};
use bevy::{
    prelude::*,
    render::{renderer::RenderDevice, settings::WgpuFeatures},
};
use forge_assets::{
    source_graph_bundle::SourceGraphImportedStorageFormat, source_graph_glb::SourceGraphGlb,
};
use forge_engine::{
    source_graph::{SourceGraphMaterial, SourceGraphShaderRegistry, SourceGraphUniforms},
    source_graph_glb::{source_graph_mesh_from_glb, source_graph_program_from_glb},
    source_graph_scene_geometry::{
        ScenePrimitiveRoute, SourceGraphSceneGeometry, SourcePrimitiveKey,
    },
};
use std::collections::{BTreeMap, BTreeSet};

/// Optional explicit policy on the hidden host root. An absent component keeps
/// authored sampling. Mounted hosts own distinct images, even for identical
/// shader hashes; changing this component requires a new mount.
#[derive(Component, Clone, Copy)]
pub(super) struct SceneSampling(pub forge_engine::source_graph::SourceGraphSceneSampling);

pub(super) struct OriginalPasses {
    pub render_queue: i32,
    pub disabled: Vec<String>,
}

pub(super) struct RegisteredScene {
    pub owned_image_ids: BTreeSet<AssetId<Image>>,
    pub sampling: forge_engine::source_graph::SourceGraphSceneSampling,
    pub bindings: SceneBindings,
    /// The caller must retain ordering and route the actual passes before reveal.
    pub original_passes: BTreeMap<usize, OriginalPasses>,
}

pub(super) fn validate_uniforms(source: &SourceGraphUniforms) -> Result<(), String> {
    let matrices = [
        source.source_object_from_target_object,
        source.target_object_from_source_object,
        source.source_world_from_target_world,
        source.source_absolute_world_from_target_world,
    ];
    if matrices.iter().any(|m| {
        !m.is_finite()
            || !m.determinant().is_finite()
            || m.determinant() == 0.0
            || m.x_axis.w != 0.0
            || m.y_axis.w != 0.0
            || m.z_axis.w != 0.0
            || m.w_axis.w != 1.0
    }) || !source.source_time_parameters.is_finite()
        || !source.source_previous_time_parameters.is_finite()
        || !source.source_scale_mip_min.is_finite()
        || source.source_scale_mip_min.x <= 0.0
        || source.source_scale_mip_min.y <= 0.0
        || source.source_scale_mip_min.w <= 0.0
    {
        return Err(
            "Explicit finite/invertible source frames and clock/mip inputs required".into(),
        );
    }
    let product = source.source_object_from_target_object * source.target_object_from_source_object;
    if !product.is_finite()
        || product
            .to_cols_array()
            .iter()
            .zip(Mat4::IDENTITY.to_cols_array())
            .any(|(actual, expected)| (*actual - expected).abs() > 1e-4)
    {
        return Err("Declared object basis mappings are not inverse".into());
    }
    // The GPU computes the inverse per instance. Admit the shared mappings
    // here, including the exact affine rows and common relative/absolute basis.
    forge_engine::source_graph::source_graph_position_frame(
        source.source_object_from_target_object,
        Mat4::IDENTITY,
        source.source_world_from_target_world,
        source.source_absolute_world_from_target_world,
    )
    .map_err(str::to_owned)?;
    Ok(())
}

/// Register each shared geometry and material image batch once. Material
/// instances keep per-node uniforms, while images/shaders are shared. Missing
/// declared images are never padded with generated textures or native defaults.
/// On failure no scene is attached; newly created images are removed. Verified
/// shader cache entries remain owned and reusable by the canonical registry.
pub(super) fn register(
    world: &mut World,
    glb: &SourceGraphGlb<'_>,
    scene: &SourceGraphSceneGeometry,
    uniforms: &BTreeMap<(usize, usize), SourceGraphUniforms>,
    ordinary: BTreeMap<SourcePrimitiveKey, OrdinaryPrimitive>,
) -> Result<RegisteredScene, String> {
    register_with_sampling(
        world,
        glb,
        scene,
        uniforms,
        ordinary,
        forge_engine::source_graph::SourceGraphSceneSampling::Authored,
    )
}

pub(super) fn register_with_sampling(
    world: &mut World,
    glb: &SourceGraphGlb<'_>,
    scene: &SourceGraphSceneGeometry,
    uniforms: &BTreeMap<(usize, usize), SourceGraphUniforms>,
    ordinary: BTreeMap<SourcePrimitiveKey, OrdinaryPrimitive>,
    sampling: forge_engine::source_graph::SourceGraphSceneSampling,
) -> Result<RegisteredScene, String> {
    let mut expected = BTreeSet::new();
    let mut expected_ordinary = BTreeSet::new();
    for primitive in &scene.instances {
        match primitive.route {
            ScenePrimitiveRoute::SourceGraph { material } => {
                expected.insert((primitive.source_node, material));
            }
            ScenePrimitiveRoute::HostGltf { material } => {
                let original = ordinary
                    .get(&primitive.geometry)
                    .ok_or("Original ordinary primitive binding absent")?;
                if original.material_index != material {
                    return Err("Ordinary material index changed".into());
                }
                expected_ordinary.insert(primitive.geometry);
            }
        }
    }
    if expected != uniforms.keys().copied().collect()
        || expected_ordinary != ordinary.keys().copied().collect()
    {
        return Err("Explicit frame bindings/ordinary routes differ from scene instances".into());
    }
    for source in uniforms.values() {
        validate_uniforms(source)?;
    }
    if scene.absent_original_tangents().next().is_some() {
        return Err("Missing original tangent needs its qualified native-default adapter".into());
    }
    let device = world
        .get_resource::<RenderDevice>()
        .cloned()
        .ok_or("Actual render device unavailable")?;
    if !world.contains_resource::<Assets<Mesh>>()
        || !world.contains_resource::<Assets<Image>>()
        || !world.contains_resource::<Assets<Shader>>()
        || !world.contains_resource::<Assets<SourceGraphMaterial>>()
        || !world.contains_resource::<SourceGraphShaderRegistry>()
    {
        return Err("Source renderer resources/plugins unavailable".into());
    }
    if !ordinary.is_empty() {
        let meshes = world.resource::<Assets<Mesh>>();
        let materials = world
            .get_resource::<Assets<StandardMaterial>>()
            .ok_or("Ordinary glTF material assets unavailable")?;
        if ordinary
            .values()
            .any(|p| meshes.get(&p.mesh).is_none() || materials.get(&p.material).is_none())
        {
            return Err("Original ordinary primitive resources are not resident".into());
        }
    }
    let used_materials: BTreeSet<_> = expected.iter().map(|(_, material)| *material).collect();
    let mut programs = BTreeMap::new();
    let mut original_passes = BTreeMap::new();
    for index in used_materials {
        let contract = &glb
            .materials
            .iter()
            .find(|(i, _)| *i == index)
            .ok_or("Source material declaration absent")?
            .1;
        let program = source_graph_program_from_glb(contract, &glb.views)?.with_scene_sampling(
            sampling,
            world.get_resource::<bevy::render::renderer::RenderAdapter>(),
        )?;
        if !program.requires_captured_images() && !contract.textures.is_empty() {
            return Err(
                "Source texture storage not captured; external PNG fallback forbidden".into(),
            );
        }
        if contract.textures.iter().any(|t| {
            t.imported_storage
                .as_ref()
                .is_some_and(|s| s.format != SourceGraphImportedStorageFormat::Rgba8Unorm)
        }) && !device
            .features()
            .contains(WgpuFeatures::TEXTURE_COMPRESSION_BC)
        {
            return Err("Actual device lacks the captured BC texture feature".into());
        }
        original_passes.insert(
            index,
            OriginalPasses {
                render_queue: program.original_render_queue,
                disabled: program.original_disabled_passes.clone(),
            },
        );
        programs.insert(index, program);
    }
    // Decode all meshes before registration, preserving the same selected routes.
    let mut prepared_meshes = BTreeMap::new();
    for key in scene.source_geometries.keys() {
        let prepared = source_graph_mesh_from_glb(glb, key.mesh, key.primitive)?;
        prepared_meshes.insert(*key, prepared.mesh);
    }
    let images_before: BTreeSet<_> = world.resource::<Assets<Image>>().ids().collect();
    let registered: Result<BTreeMap<usize, SourceGraphMaterial>, String> =
        world.resource_scope(|world, mut shaders: Mut<Assets<Shader>>| {
            world.resource_scope(|world, mut registry: Mut<SourceGraphShaderRegistry>| {
                let mut images = world.resource_mut::<Assets<Image>>();
                programs
                    .into_iter()
                    .map(|(index, program)| {
                        let context = uniforms
                            .iter()
                            .find(|((_, i), _)| *i == index)
                            .expect("Material instance exists")
                            .1
                            .clone();
                        let material = if program.requires_captured_images() {
                            program.register_captured_images(
                                &mut shaders,
                                &mut registry,
                                &mut images,
                                &device,
                                context,
                            )
                        } else {
                            program.register(&mut shaders, &mut registry, context, Vec::new())
                        }
                        .map_err(str::to_owned)?;
                        Ok((index, material))
                    })
                    .collect()
            })
        });
    let registered = match registered {
        Ok(materials) => materials,
        Err(error) => {
            let mut images = world.resource_mut::<Assets<Image>>();
            let newly_created: Vec<_> = images
                .ids()
                .filter(|id| !images_before.contains(id))
                .collect();
            for id in newly_created {
                images.remove(id);
            }
            return Err(error);
        }
    };
    let mut bindings = SceneBindings {
        ordinary,
        ..Default::default()
    };
    for (key, mesh) in prepared_meshes {
        bindings
            .source_meshes
            .insert(key, world.resource_mut::<Assets<Mesh>>().add(mesh));
    }
    for (key, context) in uniforms {
        let mut material = registered[&key.1].clone();
        let modes = material.extension.source.normal_decode_modes;
        material.extension.source = context.clone();
        // These modes come from the admitted embedded contract, not a host guess.
        material.extension.source.normal_decode_modes = modes;
        bindings.source_materials.insert(
            *key,
            world
                .resource_mut::<Assets<SourceGraphMaterial>>()
                .add(material),
        );
    }
    Ok(RegisteredScene {
        owned_image_ids: world
            .resource::<Assets<Image>>()
            .ids()
            .filter(|id| !images_before.contains(id))
            .collect(),
        sampling,
        bindings,
        original_passes,
    })
}

#[cfg(test)]
mod tests {
    use super::*;

    fn explicit_fixture() -> SourceGraphUniforms {
        SourceGraphUniforms {
            source_object_from_target_object: Mat4::from_scale(Vec3::new(-1.0, 1.0, 1.0)),
            target_object_from_source_object: Mat4::from_scale(Vec3::new(-1.0, 1.0, 1.0)),
            source_world_from_target_world: Mat4::IDENTITY,
            source_absolute_world_from_target_world: Mat4::IDENTITY,
            source_time_parameters: Vec4::ZERO,
            source_previous_time_parameters: Vec4::ZERO,
            source_scale_mip_min: Vec4::new(1.0, 1.0, 0.0, f32::MIN_POSITIVE),
            normal_decode_modes: [Vec4::ZERO; 2],
        }
    }

    #[test]
    fn declared_reflection_and_source_clock_are_retained() {
        let source = explicit_fixture();
        validate_uniforms(&source).unwrap();
        assert_eq!(
            source
                .source_object_from_target_object
                .transform_point3(Vec3::X),
            Vec3::NEG_X
        );
        assert_eq!(source.source_scale_mip_min.z, 0.0);
    }

    #[test]
    fn non_inverse_and_singular_frames_are_refused() {
        let mut source = explicit_fixture();
        source.target_object_from_source_object = Mat4::IDENTITY;
        assert!(validate_uniforms(&source).is_err());
        source = explicit_fixture();
        source.source_world_from_target_world = Mat4::from_scale(Vec3::new(0.0, 1.0, 1.0));
        assert!(validate_uniforms(&source).is_err());
    }

    #[test]
    fn invalid_clock_and_source_scale_are_refused() {
        let mut source = explicit_fixture();
        source.source_previous_time_parameters.x = f32::NAN;
        assert!(validate_uniforms(&source).is_err());
        source = explicit_fixture();
        source.source_scale_mip_min.x = 0.0;
        assert!(validate_uniforms(&source).is_err());
    }
}
