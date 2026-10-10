//! Scene-driven shadow inputs, shared by editor and native validation.
use super::{OpaqueDraw, graph::Input, terrain};
use crate::{
    sun_cascades::{CascadeConfig, CascadeView, SunCascades},
    tidewater_material::ObjectShadowSettings,
};
use bevy::{math::DVec3, prelude::*};

/// Explicit coverage contract for source-solid meshes outside village families.
/// Scene adapters own eligibility; authoring identity remains on common placements.
#[derive(Component)]
pub struct OpaqueCaster;

pub fn prepare(
    view: CascadeView,
    sun: DVec3,
    draws: Vec<OpaqueDraw>,
    terrain: Option<terrain::Input>,
) -> Result<Input, String> {
    let mut cascades = SunCascades::new(CascadeConfig::default())?;
    cascades.update(view, sun, true)?;
    let mut settings = ObjectShadowSettings {
        enabled: if cascades.sampling_enabled { 1.0 } else { 0.0 },
        camera: view.world_from_view.w_axis.as_vec4(),
        forward: (-view.world_from_view.z_axis).as_vec4(),
        ..default()
    };
    settings.camera.w = 0.0;
    if settings.enabled == 0.0 {
        return Ok(Input {
            settings,
            draws: Vec::new(),
            matrices: Vec::new(),
            terrain: None,
            plants: None,
            eyes: Vec::new(),
        });
    }
    let mut matrices = Vec::with_capacity(3);
    let mut eyes = Vec::with_capacity(3);
    for (i, cascade) in cascades.cascades().iter().enumerate() {
        let c = cascade.ok_or("Sun cascade missing")?;
        settings.matrices[i] = c.clip_from_world.as_mat4();
        settings.cascades[i] = bevy::math::DVec4::from_array(c.info).as_vec4();
        settings.blend[i] = bevy::math::DVec4::from_array(c.blend).as_vec4();
        matrices.push(settings.matrices[i]);
        eyes.push(c.world_from_view.w_axis.as_vec4());
    }
    Ok(Input {
        settings,
        draws,
        matrices,
        terrain,
        plants: None,
        eyes,
    })
}

/// Collect all solid village parts, including independently staged source props.
/// Follow local poses through pivots so an editor preview need not wait for
/// `GlobalTransform` propagation. Net/fish materials 5/6 remain excluded.
pub fn opaque_parts(world: &World) -> Result<Vec<(Entity, Mat4)>, String> {
    use crate::{tidewater_assets::scene::ResidentScene, tidewater_material::VillageMaterial};
    let mut parts = Vec::new();
    for instance in world
        .get_resource::<ResidentScene>()
        .into_iter()
        .flat_map(|scene| &scene.instances)
    {
        if world.get::<ChildOf>(instance.root).is_some() {
            return Err("Shadow root must be unparented".into());
        }
        for &entity in &instance.parts {
            if world
                .get::<ChildOf>(entity)
                .is_none_or(|parent| parent.parent() != instance.root)
            {
                return Err("Shadow caster hierarchy changed".into());
            }
            parts.push(entity);
        }
    }
    if let Some(boardwalk) = world.get_resource::<crate::tidewater_boardwalk::ResidentBoardwalk>() {
        parts.extend_from_slice(boardwalk.render_parts());
    }
    if let Some(pier) = world.get_resource::<crate::tidewater_pier::ResidentPier>() {
        parts.extend_from_slice(pier.render_parts());
    }
    if let Some(plaza) = world.get_resource::<crate::tidewater_plaza::ResidentPlaza>() {
        parts.extend_from_slice(plaza.render_parts());
    }
    if let Some(props) =
        world.get_resource::<crate::tidewater_village_props::ResidentVillageProps>()
    {
        parts.extend_from_slice(props.render_parts());
    }
    if let Some(props) = world.get_resource::<crate::tidewater_beach_props::ResidentBeachProps>() {
        parts.extend_from_slice(props.render_parts());
    }
    let mut result = Vec::with_capacity(parts.len());
    for entity in parts {
        let material = world
            .get::<MeshMaterial3d<VillageMaterial>>(entity)
            .and_then(|handle| {
                world
                    .get_resource::<Assets<VillageMaterial>>()?
                    .get(&handle.0)
            })
            .ok_or("Missing shadow caster material")?;
        if material.kind <= 4 {
            result.push((entity, world_pose(world, entity)?));
        }
    }
    if let Some(mut marked) = world.try_query_filtered::<Entity, With<OpaqueCaster>>() {
        let mut entities: Vec<_> = marked.iter(world).collect();
        entities.sort_unstable();
        for entity in entities {
            if !result.iter().any(|(existing, _)| *existing == entity) {
                if world.get::<Mesh3d>(entity).is_none() {
                    return Err("Explicit opaque caster mesh absent".into());
                }
                result.push((entity, world_pose(world, entity)?));
            }
        }
    }
    Ok(result)
}

fn world_pose(world: &World, entity: Entity) -> Result<Mat4, String> {
    let mut model = Mat4::IDENTITY;
    let mut current = entity;
    let mut visited = Vec::new();
    loop {
        if visited.len() >= 64 || visited.contains(&current) {
            return Err("Invalid shadow caster ancestry".into());
        }
        visited.push(current);
        model = world
            .get::<Transform>(current)
            .ok_or("Missing shadow caster pose")?
            .to_matrix()
            * model;
        match world.get::<ChildOf>(current) {
            Some(parent) => current = parent.parent(),
            None => break,
        }
    }
    if !model.is_finite() {
        return Err("Non-finite shadow caster pose".into());
    }
    Ok(model)
}

/// The same render entities and transforms supply each opaque depth draw.
pub fn buildings(world: &World) -> Result<Vec<OpaqueDraw>, String> {
    let parts = opaque_parts(world)?;
    if parts.is_empty() {
        return Ok(Vec::new());
    }
    let meshes = world.resource::<Assets<Mesh>>();
    let mut draws = Vec::with_capacity(parts.len());
    for (entity, model) in parts {
        let mesh = world
            .get::<Mesh3d>(entity)
            .and_then(|h| meshes.get(&h.0))
            .ok_or("Missing shadow caster mesh")?;
        draws.push(OpaqueDraw::from_mesh(mesh, model)?);
    }
    Ok(draws)
}

#[cfg(test)]
mod tests {
    use super::*;

    #[test]
    #[allow(
        clippy::float_cmp,
        reason = "Exact copied fixture values and GPU wire fields must remain identical; an epsilon would weaken this regression."
    )]
    fn explicit_caster_follows_common_parent_edits_and_removal() {
        use bevy::{
            asset::RenderAssetUsages, mesh::Indices, render::render_resource::PrimitiveTopology,
        };
        let mut world = World::new();
        world.init_resource::<Assets<Mesh>>();
        let mesh = Mesh::new(
            PrimitiveTopology::TriangleList,
            RenderAssetUsages::default(),
        )
        .with_inserted_attribute(
            Mesh::ATTRIBUTE_POSITION,
            vec![[0.0_f32, 0.0, 0.0], [1.0, 0.0, 0.0], [0.0, 1.0, 0.0]],
        )
        .with_inserted_indices(Indices::U32(vec![0, 1, 2]));
        let handle = world.resource_mut::<Assets<Mesh>>().add(mesh);
        let root = world.spawn(Transform::from_xyz(49.9, 0.25, -74.6)).id();
        let local = Transform::from_xyz(0.0, 1.0, 0.0);
        let part = world
            .spawn((
                OpaqueCaster,
                Mesh3d(handle),
                local,
                GlobalTransform::IDENTITY,
                ChildOf(root),
            ))
            .id();
        let first = opaque_parts(&world).unwrap();
        assert_eq!(first.len(), 1);
        assert_eq!(first[0].0, part);
        assert_eq!(first[0].1.w_axis.y, 1.25);
        assert_eq!(buildings(&world).unwrap().len(), 1);
        world.get_mut::<Transform>(root).unwrap().translation.x += 10.0;
        let moved = opaque_parts(&world).unwrap();
        assert!((moved[0].1.w_axis.x - first[0].1.w_axis.x - 10.0).abs() < 0.0001);
        world.entity_mut(part).remove::<OpaqueCaster>();
        assert!(opaque_parts(&world).unwrap().is_empty());
        world.entity_mut(part).insert(OpaqueCaster);
        assert_eq!(opaque_parts(&world).unwrap().len(), 1);
        world.despawn(root);
        assert!(opaque_parts(&world).unwrap().is_empty());
    }

    #[test]
    fn nested_caster_uses_current_editor_pose_instead_of_stale_global_pose() {
        let mut world = World::new();
        let root_pose = Transform::from_xyz(55.0, 0.0, -64.0)
            .with_rotation(Quat::from_rotation_y(0.4))
            .with_scale(Vec3::splat(2.0));
        let root = world.spawn(root_pose).id();
        let pivot_pose = Transform::from_xyz(0.0, 2.0, 3.0);
        let pivot = world.spawn((pivot_pose, ChildOf(root))).id();
        let local = Transform::from_xyz(1.0, 0.0, 0.0);
        let part = world
            .spawn((local, GlobalTransform::IDENTITY, ChildOf(pivot)))
            .id();
        let expected = root_pose.to_matrix() * pivot_pose.to_matrix() * local.to_matrix();
        assert!(
            world_pose(&world, part)
                .unwrap()
                .abs_diff_eq(expected, 0.0001)
        );
        world.get_mut::<Transform>(root).unwrap().translation.x += 20.0;
        let updated = world_pose(&world, part).unwrap();
        assert!(
            (updated.w_axis.x
                - (root_pose.to_matrix() * pivot_pose.to_matrix() * local.to_matrix())
                    .w_axis
                    .x
                - 20.0)
                .abs()
                < 0.0001
        );
        world.entity_mut(pivot).remove::<Transform>();
        assert!(world_pose(&world, part).is_err());
    }
}
