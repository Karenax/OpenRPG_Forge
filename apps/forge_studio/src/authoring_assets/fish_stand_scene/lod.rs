//! Per-copy visual LOD uniforms; the common parent remains the selectable object.
use bevy::{
    camera::primitives::{Frustum, Sphere},
    ecs as bevy_ecs,
    prelude::*,
};
use forge_engine::{
    ManualPlacementPart,
    tidewater_fish_props::{lod, material::FishMaterial},
};

#[derive(Clone)]
pub(super) struct Asset {
    pub meshes: Vec<Handle<Mesh>>,
    pub material: Handle<FishMaterial>,
    pub pose: Transform,
}

#[derive(Component)]
struct Part {
    lod: usize,
    material: Handle<FishMaterial>,
    shadow_mesh: Handle<Mesh>,
}

pub(super) fn spawn(
    world: &mut World,
    id: forge_core::EntityId,
    root: Entity,
    assets: &[Asset],
) -> Result<Vec<Handle<FishMaterial>>, String> {
    let prepared = assets
        .iter()
        .map(|asset| {
            if asset.meshes.len() != 3 {
                return Err("Trois LOD source requis pour l'étal".into());
            }
            let material = world
                .resource::<Assets<FishMaterial>>()
                .get(&asset.material)
                .cloned()
                .ok_or("Matériau source d'étal absent")?;
            Ok((asset.clone(), material))
        })
        .collect::<Result<Vec<_>, String>>()?;
    let mut handles = Vec::new();
    for (asset, material) in prepared {
        for index in 0..2 {
            let handle = world
                .resource_mut::<Assets<FishMaterial>>()
                .add(material.clone());
            world.spawn((
                Mesh3d(asset.meshes[index].clone()),
                MeshMaterial3d(handle.clone()),
                asset.pose,
                Visibility::Hidden,
                ChildOf(root),
                ManualPlacementPart(id),
                Part {
                    lod: index,
                    material: handle.clone(),
                    shadow_mesh: asset.meshes[2].clone(),
                },
            ));
            handles.push(handle);
        }
    }
    Ok(handles)
}

pub(super) fn retire(world: &mut World, handles: Vec<Handle<FishMaterial>>) {
    let mut materials = world.resource_mut::<Assets<FishMaterial>>();
    for handle in handles {
        materials.remove(handle.id());
    }
}

pub(super) fn update(world: &mut World) -> Result<(), String> {
    let cameras: Vec<_> = world.query_filtered::<(&Camera, &GlobalTransform, &Frustum),
        forge_engine::tidewater_sky::preview::SceneCameraFilter>().iter(world)
        .filter(|(camera,_,_)| camera.is_active).map(|(_,global,frustum)|(global.translation(),*frustum)).collect();
    if cameras.len() != 1 {
        return Ok(());
    }
    let (camera, frustum) = cameras[0];
    let editing = world.contains_resource::<crate::authoring_render::FastEditing>();
    let time = if editing {
        0.0
    } else {
        world.resource::<Time>().elapsed_secs()
    };
    let frame = if editing {
        0
    } else {
        world.resource::<bevy::diagnostic::FrameCount>().0
    };
    let plans = world
        .query::<(Entity, &GlobalTransform, &Part)>()
        .iter(world)
        .map(|(entity, global, part)| {
            let (scale, _, center) = global.to_scale_rotation_translation();
            let length = scale.abs().max_element();
            let selection = lod::select(
                (center.as_dvec3() - camera.as_dvec3()).length(),
                f64::from(length),
                frustum.intersects_sphere(
                    &Sphere {
                        center: center.into(),
                        radius: length * 0.8,
                    },
                    true,
                ),
            )?;
            Ok((
                entity,
                part.material.clone(),
                selection
                    .draws
                    .into_iter()
                    .find(|draw| draw.lod == part.lod),
            ))
        })
        .collect::<Result<Vec<_>, String>>()?;
    if plans.is_empty() {
        return Ok(());
    }
    for (_, handle, _) in &plans {
        if !world
            .resource::<Assets<FishMaterial>>()
            .contains(handle.id())
        {
            return Err("Matériau LOD d'étal absent".into());
        }
    }
    let lighting = super::super::imported_scene::resident_lighting_material(world)
        .ok_or("Éclairage résident des poissons d'étal absent")?;
    forge_engine::tidewater_material::SceneLighting::new(
        lighting.scene_lighting.sun_direction.truncate().to_array(),
        lighting.scene_lighting.sun_color.truncate().to_array(),
        lighting
            .scene_lighting
            .environment_sh
            .map(|value| value.truncate().to_array()),
    )?;
    for (entity, handle, draw) in plans {
        world.entity_mut(entity).insert(if draw.is_some() {
            Visibility::Inherited
        } else {
            Visibility::Hidden
        });
        let mut materials = world.resource_mut::<Assets<FishMaterial>>();
        let previous = materials
            .get(&handle)
            .ok_or("Matériau LOD d'étal disparu")?;
        let mut value = previous.clone();
        value.update_scene_receivers(&lighting)?;
        value.settings.info.w = time;
        value.settings.frame_index = frame;
        if let Some(draw) = draw {
            value.settings.lod = Vec4::new(draw.fade, f32::from(u8::from(draw.outgoing)), 0.0, 0.0);
        }
        if value != *previous {
            *materials
                .get_mut(&handle)
                .ok_or("Matériau LOD d'étal disparu")? = value;
        }
    }
    Ok(())
}

#[derive(Resource, Default)]
struct ShadowCache {
    key: Vec<(Entity, bevy::asset::AssetId<Mesh>)>,
    geometry: Option<std::sync::Arc<Vec<forge_engine::tidewater_fish_props::shadow::MaskedMesh>>>,
}

pub(super) fn clear_shadow(world: &mut World) {
    world.remove_resource::<ShadowCache>();
    let _ = super::super::fish_shadow_scene::publish::<super::super::fish_shadow_scene::Stand>(
        world, None,
    );
}

pub(super) fn publish_shadow(
    world: &mut World,
    shader: Option<std::sync::Arc<String>>,
) -> Result<(), String> {
    use forge_engine::tidewater_fish_props::shadow;
    let cameras: Vec<_> = world.query_filtered::<(&Camera, &GlobalTransform),
        forge_engine::tidewater_sky::preview::SceneCameraFilter>().iter(world)
        .filter(|(camera,_)| camera.is_active).map(|(_,global)|global.translation()).collect();
    let target = world
        .get_resource::<crate::authoring_imported_shadows::Receiver>()
        .filter(|receiver| receiver.0.settings.enabled >= 0.5)
        .map(|receiver| receiver.0.image.clone());
    let (Some(target), Some(shader)) = (target, shader) else {
        clear_shadow(world);
        return Ok(());
    };
    if cameras.len() != 1 {
        clear_shadow(world);
        return Ok(());
    }
    let camera = cameras[0];
    let inventory = world
        .query::<(Entity, &GlobalTransform, &Part)>()
        .iter(world)
        .filter(|(_, _, part)| part.lod == 0)
        .map(|(entity, global, part)| {
            let (scale, _, center) = global.to_scale_rotation_translation();
            let selection = lod::select(
                (center.as_dvec3() - camera.as_dvec3()).length(),
                f64::from(scale.abs().max_element()),
                true,
            )?;
            Ok((
                entity,
                part.shadow_mesh.clone(),
                shadow::gpu::Pose {
                    model: global.to_matrix(),
                    enabled: selection.shadow,
                },
            ))
        })
        .collect::<Result<Vec<_>, String>>()?;
    if inventory.is_empty() {
        clear_shadow(world);
        return Ok(());
    }
    let key: Vec<_> = inventory
        .iter()
        .map(|(entity, mesh, _)| (*entity, mesh.id()))
        .collect();
    world.init_resource::<ShadowCache>();
    let geometry =
        world.resource_scope(|world, mut cache: Mut<ShadowCache>| -> Result<_, String> {
            if cache.geometry.is_none() || cache.key != key {
                let meshes = world.resource::<Assets<Mesh>>();
                let geometry = inventory
                    .iter()
                    .map(|(_, mesh, _)| {
                        shadow::MaskedMesh::from_mesh(
                            meshes.get(mesh).ok_or("Mesh ombre d'étal absent")?,
                        )
                    })
                    .collect::<Result<Vec<_>, String>>()?;
                cache.geometry = Some(std::sync::Arc::new(geometry));
                cache.key = key;
            }
            Ok(cache
                .geometry
                .as_ref()
                .ok_or("Géométrie masquée d'étal absente")?
                .clone())
        })?;
    let frame = shadow::Frame {
        target,
        shader,
        meshes: geometry,
        poses: inventory.into_iter().map(|(_, _, pose)| pose).collect(),
        time: world.resource::<Time>().elapsed_secs(),
    };
    super::super::fish_shadow_scene::publish::<super::super::fish_shadow_scene::Stand>(
        world,
        Some(frame),
    )
}
