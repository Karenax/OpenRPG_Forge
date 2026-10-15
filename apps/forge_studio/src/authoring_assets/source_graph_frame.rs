//! Apply an explicit admitted frame to camera and shadow materials together.
//! The host supplies world/object mappings, source clocks and mip settings.
//! This does not infer them from pack names, camera positions or Bevy defaults.
use super::{source_graph_resources::validate_uniforms, source_graph_scene::SceneBindings};
use bevy::prelude::*;
use forge_engine::source_graph::{
    SourceGraphMaterial, SourceGraphShadowRoute, SourceGraphUniforms,
};
use std::collections::{BTreeMap, BTreeSet};

/// Call after composing the current transforms and before render extraction.
/// Shadow routing performs its entity maintenance before transform propagation;
/// its derived materials also need this current frame, rather than last frame.
/// All identities, ownership and frame values are checked before any mutation.
pub(super) fn update(
    world: &mut World,
    bindings: &SceneBindings,
    contexts: &BTreeMap<(usize, usize), SourceGraphUniforms>,
) -> Result<usize, String> {
    if contexts.keys().ne(bindings.source_materials.keys()) {
        return Err("Current source frame differs from admitted material instances".into());
    }
    for context in contexts.values() {
        validate_uniforms(context)?;
    }
    let mut updates = BTreeMap::new();
    let mut main_ids = BTreeSet::new();
    for (key, handle) in &bindings.source_materials {
        if !main_ids.insert(handle.id()) {
            return Err("Source material handle is shared across independent node frames".into());
        }
        updates.insert(handle.id(), (handle.clone(), contexts[key].clone(), false));
    }
    let routes: Vec<_> = world
        .query::<(
            Entity,
            &MeshMaterial3d<SourceGraphMaterial>,
            &SourceGraphShadowRoute,
        )>()
        .iter(world)
        .filter(|(_, material, _)| main_ids.contains(&material.0.id()))
        .map(|(entity, material, route)| (entity, material.0.id(), route.clone()))
        .collect();
    for (owner, main, route) in routes {
        if world
            .get::<ChildOf>(route.proxy)
            .is_none_or(|parent| parent.parent() != owner)
            || world
                .get::<MeshMaterial3d<SourceGraphMaterial>>(route.proxy)
                .is_none_or(|material| material.0 != route.shadow_material)
        {
            return Err("Source shadow ownership changed before frame update".into());
        }
        if updates.contains_key(&route.shadow_material.id()) {
            return Err("Derived shadow handle aliases another frame destination".into());
        }
        updates.insert(
            route.shadow_material.id(),
            (route.shadow_material, updates[&main].1.clone(), true),
        );
    }
    let assets = world
        .get_resource::<Assets<SourceGraphMaterial>>()
        .ok_or("Source material assets unavailable")?;
    for (handle, _, shadow_only) in updates.values() {
        let material = assets
            .get(handle)
            .ok_or("Current frame material is not resident")?;
        if material.extension.program_key.shadow_only != *shadow_only {
            return Err("Source camera/shadow program role changed".into());
        }
        if material.extension.program_key.requires_preintegrated_brdf
            || material.extension.program_key.requires_sky_cube
        {
            let images = world
                .get_resource::<Assets<Image>>()
                .ok_or("Source scene image assets unavailable")?;
            material
                .extension
                .validate_scene_dependencies(images)
                .map_err(str::to_owned)?;
        }
    }
    // Check every actual draw instance after transform propagation, including
    // managed shadow instances. A later bad instance must not advance any clock.
    let instances: Vec<_> = world
        .query::<(
            &MeshMaterial3d<SourceGraphMaterial>,
            Option<&GlobalTransform>,
        )>()
        .iter(world)
        .filter(|(material, _)| updates.contains_key(&material.0.id()))
        .map(|(material, transform)| (material.0.id(), transform.map(GlobalTransform::to_matrix)))
        .collect();
    let mut resident_instances = BTreeSet::new();
    for (id, matrix) in instances {
        let matrix = matrix.ok_or("Source draw instance lacks a composed global transform")?;
        let source = &updates[&id].1;
        forge_engine::source_graph::source_graph_position_frame(
            source.source_object_from_target_object,
            matrix,
            source.source_world_from_target_world,
            source.source_absolute_world_from_target_world,
        )
        .map_err(str::to_owned)?;
        resident_instances.insert(id);
    }
    if updates.keys().any(|id| !resident_instances.contains(id)) {
        return Err("Source frame destination has no resident draw instance".into());
    }
    let mut assets = world.resource_mut::<Assets<SourceGraphMaterial>>();
    let count = updates.len();
    for (handle, mut context, _) in updates.into_values() {
        let mut material = assets
            .get_mut(&handle)
            .expect("Frame destinations preflighted");
        // Decoder modes belong to captured image admission, never the host clock.
        context.normal_decode_modes = material.extension.source.normal_decode_modes;
        material.extension.source = context;
    }
    Ok(count)
}

#[cfg(test)]
mod tests {
    use super::*;
    use bevy::{asset::RenderAssetUsages, mesh::PrimitiveTopology};
    use forge_engine::source_graph::{
        SourceGraphExtension, SourceGraphForwardState, SourceGraphKey, SourceGraphSurfacePhase,
        install_source_graph_shadow_route, source_graph_material,
    };

    fn context(time: f32) -> SourceGraphUniforms {
        let reflection = Mat4::from_scale(Vec3::new(-1.0, 1.0, 1.0));
        SourceGraphUniforms {
            source_object_from_target_object: reflection,
            target_object_from_source_object: reflection,
            source_world_from_target_world: reflection,
            source_absolute_world_from_target_world: reflection,
            source_time_parameters: Vec4::splat(time),
            source_previous_time_parameters: Vec4::splat(time - 1.0),
            source_scale_mip_min: Vec4::new(0.5, 0.25, -0.75, f32::MIN_POSITIVE),
            normal_decode_modes: [Vec4::ZERO; 2],
        }
    }

    fn fixture() -> (World, SceneBindings, Entity) {
        let mut world = World::new();
        world.init_resource::<Assets<Mesh>>();
        world.init_resource::<Assets<SourceGraphMaterial>>();
        let mesh = world.resource_mut::<Assets<Mesh>>().add(Mesh::new(
            PrimitiveTopology::TriangleList,
            RenderAssetUsages::MAIN_WORLD,
        ));
        let key = SourceGraphKey {
            requires_source_state: false,
            source_state_ready: false,
            requires_native_srgb_encoder: false,
            native_srgb_encoder_ready: false,
            requires_deferred_srgb: false,
            deferred_srgb_ready: false,
            requires_preintegrated_brdf: false,
            preintegrated_brdf_ready: false,
            requires_sky_cube: false,
            sky_cube_ready: false,
            color_write_mask: 15, // Existing legacy ALL-writes fixture.
            prepass_cull: 2,
            shadow_only: false,
            orthographic_shadow_raster: None,
            vertex: 123,
            fragment: 123,
            prepass_vertex: 456,
            prepass_fragment: 456,
            original_alpha_to_mask: false,
            requires_original_vertex_color: false,
            original_uv_requirements: Default::default(),
            forward_state: SourceGraphForwardState {
                color_src: 1,
                color_dst: 0,
                alpha_src: 1,
                alpha_dst: 0,
                cull: 2,
                compare: 4,
                write_depth: true,
            },
        };
        let mut original = context(0.0);
        original.normal_decode_modes[0] = Vec4::new(2.0, 1.0, 0.0, 0.0);
        let material = source_graph_material(
            SourceGraphExtension::from_source_bindings(key, original, 0, vec![]).unwrap(),
            SourceGraphSurfacePhase::Opaque,
        );
        let handle = world
            .resource_mut::<Assets<SourceGraphMaterial>>()
            .add(material);
        let owner = world
            .spawn((
                Mesh3d(mesh),
                MeshMaterial3d(handle.clone()),
                Transform::IDENTITY,
                GlobalTransform::IDENTITY,
                Visibility::Inherited,
            ))
            .id();
        let bindings = SceneBindings {
            source_materials: BTreeMap::from([((3, 2), handle)]),
            ..Default::default()
        };
        (world, bindings, owner)
    }

    #[test]
    fn camera_and_managed_shadow_receive_same_current_and_previous_frame() {
        let (mut world, bindings, owner) = fixture();
        let proxy = install_source_graph_shadow_route(&mut world, owner, 31).unwrap();
        let next = context(7.0);
        assert_eq!(
            update(&mut world, &bindings, &BTreeMap::from([((3, 2), next)])).unwrap(),
            2
        );
        let assets = world.resource::<Assets<SourceGraphMaterial>>();
        for entity in [owner, proxy] {
            let handle = &world
                .get::<MeshMaterial3d<SourceGraphMaterial>>(entity)
                .unwrap()
                .0;
            let source = &assets.get(handle).unwrap().extension.source;
            assert_eq!(source.source_time_parameters, Vec4::splat(7.0));
            assert_eq!(source.source_previous_time_parameters, Vec4::splat(6.0));
            assert_eq!(source.source_scale_mip_min.z, -0.75);
            assert_eq!(source.normal_decode_modes[0], Vec4::new(2.0, 1.0, 0.0, 0.0));
        }
    }

    #[test]
    fn invalid_later_frame_refuses_before_updating_any_material() {
        let (mut world, mut bindings, _) = fixture();
        let first = bindings.source_materials[&(3, 2)].clone();
        let clone = world
            .resource::<Assets<SourceGraphMaterial>>()
            .get(&first)
            .unwrap()
            .clone();
        let second = world
            .resource_mut::<Assets<SourceGraphMaterial>>()
            .add(clone);
        bindings.source_materials.insert((7, 2), second.clone());
        let mut invalid = context(8.0);
        invalid.source_world_from_target_world = Mat4::ZERO;
        assert!(
            update(
                &mut world,
                &bindings,
                &BTreeMap::from([((3, 2), context(7.0)), ((7, 2), invalid)])
            )
            .is_err()
        );
        let assets = world.resource::<Assets<SourceGraphMaterial>>();
        for handle in [first, second] {
            assert_eq!(
                assets
                    .get(&handle)
                    .unwrap()
                    .extension
                    .source
                    .source_time_parameters,
                Vec4::ZERO
            );
        }
    }

    #[test]
    fn changed_shadow_ownership_refuses_without_mutating_the_camera_material() {
        let (mut world, bindings, owner) = fixture();
        let proxy = install_source_graph_shadow_route(&mut world, owner, 31).unwrap();
        world.entity_mut(proxy).remove::<ChildOf>();
        assert!(
            update(
                &mut world,
                &bindings,
                &BTreeMap::from([((3, 2), context(7.0))])
            )
            .is_err()
        );
        let assets = world.resource::<Assets<SourceGraphMaterial>>();
        assert_eq!(
            assets
                .get(&bindings.source_materials[&(3, 2)])
                .unwrap()
                .extension
                .source
                .source_time_parameters,
            Vec4::ZERO
        );
    }

    #[test]
    fn projective_and_mismatched_world_frames_refuse_before_material_update() {
        for projective in [true, false] {
            let (mut world, bindings, _) = fixture();
            let mut next = context(7.0);
            if projective {
                next.source_world_from_target_world.x_axis.w = 0.01;
            } else {
                next.source_absolute_world_from_target_world.y_axis.y *= 2.0;
            }
            assert!(update(&mut world, &bindings, &BTreeMap::from([((3, 2), next)])).is_err());
            assert_eq!(
                world
                    .resource::<Assets<SourceGraphMaterial>>()
                    .get(&bindings.source_materials[&(3, 2)])
                    .unwrap()
                    .extension
                    .source
                    .source_time_parameters,
                Vec4::ZERO
            );
        }
    }

    #[test]
    fn singular_draw_instance_refuses_before_material_update() {
        let (mut world, bindings, owner) = fixture();
        world
            .entity_mut(owner)
            .insert(GlobalTransform::from(Mat4::from_scale(Vec3::new(
                0.0, 2.0, 3.0,
            ))));
        assert!(
            update(
                &mut world,
                &bindings,
                &BTreeMap::from([((3, 2), context(7.0))])
            )
            .is_err()
        );
        assert_eq!(
            world
                .resource::<Assets<SourceGraphMaterial>>()
                .get(&bindings.source_materials[&(3, 2)])
                .unwrap()
                .extension
                .source
                .source_time_parameters,
            Vec4::ZERO
        );
    }

    #[test]
    fn rotated_scaled_draw_and_camera_origin_keep_explicit_frame() {
        let (mut world, bindings, owner) = fixture();
        let matrix = Mat4::from_scale_rotation_translation(
            Vec3::new(2.0, 3.0, 4.0),
            Quat::from_rotation_y(0.7),
            Vec3::new(16.0, 0.0, 8.0),
        );
        world
            .entity_mut(owner)
            .insert(GlobalTransform::from(matrix));
        let mut next = context(7.0);
        next.source_absolute_world_from_target_world.w_axis.x = 1000.0;
        assert_eq!(
            update(&mut world, &bindings, &BTreeMap::from([((3, 2), next)])).unwrap(),
            1
        );
        let source = &world
            .resource::<Assets<SourceGraphMaterial>>()
            .get(&bindings.source_materials[&(3, 2)])
            .unwrap()
            .extension
            .source;
        assert_eq!(source.source_time_parameters, Vec4::splat(7.0));
        assert_eq!(
            source.source_absolute_world_from_target_world.w_axis.x,
            1000.0
        );
    }

    #[test]
    fn required_sky_missing_cleared_or_evicted_refuses_camera_and_shadow_atomically() {
        use forge_engine::source_graph::SourceGraphSkyCube;
        let (mut world, bindings, owner) = fixture();
        let main = bindings.source_materials[&(3, 2)].clone();
        world
            .resource_mut::<Assets<SourceGraphMaterial>>()
            .get_mut(&main)
            .unwrap()
            .extension
            .program_key
            .requires_sky_cube = true;
        let proxy = install_source_graph_shadow_route(&mut world, owner, 31).unwrap();
        world.entity_mut(proxy).insert(GlobalTransform::IDENTITY);
        let shadow = world
            .get::<MeshMaterial3d<SourceGraphMaterial>>(proxy)
            .unwrap()
            .0
            .clone();
        let assert_clock = |world: &World, time: f32| {
            for handle in [&main, &shadow] {
                assert_eq!(
                    world
                        .resource::<Assets<SourceGraphMaterial>>()
                        .get(handle)
                        .unwrap()
                        .extension
                        .source
                        .source_time_parameters,
                    Vec4::splat(time)
                );
            }
        };
        let next = BTreeMap::from([((3, 2), context(7.0))]);
        assert!(update(&mut world, &bindings, &next).is_err());
        assert_clock(&world, 0.0);
        world.init_resource::<Assets<Image>>();
        assert!(update(&mut world, &bindings, &next).is_err());
        assert_clock(&world, 0.0);
        let bytes = vec![0_u8; (4 + 1) * 6 * 8];
        let cube = SourceGraphSkyCube::from_mip_major_rgba16f_le(
            &bytes,
            forge_core::bytes_sha256(&bytes),
            2,
            1,
            2,
        )
        .unwrap();
        let binding = cube.install_unqualified(&mut world.resource_mut::<Assets<Image>>());
        for handle in [&main, &shadow] {
            let mut assets = world.resource_mut::<Assets<SourceGraphMaterial>>();
            let mut material = assets.get_mut(handle).unwrap();
            material.extension.set_sky_cube(&binding);
            assert!(SourceGraphKey::from(&material.extension).sky_cube_ready);
        }
        assert_eq!(world.resource::<Assets<Image>>().len(), 1);
        assert_eq!(update(&mut world, &bindings, &next).unwrap(), 2);
        assert_clock(&world, 7.0);
        world
            .resource_mut::<Assets<SourceGraphMaterial>>()
            .get_mut(&shadow)
            .unwrap()
            .extension
            .clear_sky_cube();
        let later = BTreeMap::from([((3, 2), context(9.0))]);
        assert!(update(&mut world, &bindings, &later).is_err());
        assert_clock(&world, 7.0);
        world
            .resource_mut::<Assets<SourceGraphMaterial>>()
            .get_mut(&shadow)
            .unwrap()
            .extension
            .set_sky_cube(&binding);
        let image_id = world.resource::<Assets<Image>>().iter().next().unwrap().0;
        world
            .resource_mut::<Assets<Image>>()
            .get_mut(image_id)
            .unwrap()
            .texture_descriptor
            .mip_level_count = 1;
        assert!(update(&mut world, &bindings, &later).is_err());
        assert_clock(&world, 7.0);
        world.resource_mut::<Assets<Image>>().remove(image_id);
        assert!(update(&mut world, &bindings, &later).is_err());
        assert_clock(&world, 7.0);
    }
}
