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

/// Live root transforms include an editor gizmo preview. Materials 5/6 are the
/// source net/fish batches and do not belong to the opaque village shadow pass.
pub fn buildings(world: &World) -> Result<Vec<OpaqueDraw>, String> {
    use crate::{tidewater_assets::scene::ResidentScene, tidewater_material::VillageMaterial};
    use bevy::mesh::VertexAttributeValues;
    let Some(scene) = world.get_resource::<ResidentScene>() else {
        return Ok(Vec::new());
    };
    let meshes = world.resource::<Assets<Mesh>>();
    let materials = world.resource::<Assets<VillageMaterial>>();
    let mut draws = Vec::new();
    for instance in &scene.instances {
        if world.get::<ChildOf>(instance.root).is_some() {
            return Err("Shadow root must be unparented".into());
        }
        let root = world
            .get::<Transform>(instance.root)
            .ok_or("Missing building root pose")?
            .to_matrix();
        for &entity in &instance.parts {
            if world
                .get::<ChildOf>(entity)
                .is_none_or(|parent| parent.parent() != instance.root)
            {
                return Err("Shadow caster hierarchy changed".into());
            }
            let material = world
                .get::<MeshMaterial3d<VillageMaterial>>(entity)
                .and_then(|h| materials.get(&h.0))
                .ok_or("Missing shadow caster material")?;
            if material.kind > 4 {
                continue;
            }
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
            let local = world
                .get::<Transform>(entity)
                .ok_or("Missing shadow caster local pose")?
                .to_matrix();
            draws.push(OpaqueDraw::new(vertices, indices, root * local)?);
        }
    }
    Ok(draws)
}
