//! Scene-driven shadow inputs, shared by editor and native validation.
use super::{OpaqueDraw, graph::Input, terrain};
use crate::{
    sun_cascades::{CascadeConfig, CascadeView, SunCascades},
    tidewater_material::ObjectShadowSettings,
};
use bevy::{math::DVec3, prelude::*};

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
    use bevy::mesh::VertexAttributeValues;
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
        let Some(VertexAttributeValues::Float32x3(positions)) =
            mesh.attribute(Mesh::ATTRIBUTE_POSITION)
        else {
            return Err("Invalid shadow caster positions".into());
        };
        let vertices = positions
            .iter()
            .flatten()
            .flat_map(|v| v.to_le_bytes())
            .collect();
        let indices = mesh
            .indices()
            .ok_or("Shadow caster indices missing")?
            .iter()
            .map(|i| {
                u32::try_from(i)
                    .map(u32::to_le_bytes)
                    .map_err(|e| e.to_string())
            })
            .collect::<Result<Vec<_>, _>>()?
            .into_iter()
            .flatten()
            .collect();
        draws.push(OpaqueDraw::new(vertices, indices, model)?);
    }
    Ok(draws)
}

#[cfg(test)]
mod tests {
    use super::*;

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
