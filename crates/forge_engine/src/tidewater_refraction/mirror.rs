//! Disposable render mirrors, synchronized after the authored transforms.
use super::{
    CaptureSource, Control, LAYER, Observation, TERRAIN_SHADER, Targets, TerrainCapture,
    TerrainExtension, VILLAGE_SHADER, VillageCapture, VillageExtension, shaders, spawn_camera,
    target_image,
};
use crate::{tidewater_material::VillageMaterial, tidewater_terrain::lod::LodMaterial};
use bevy::{camera::visibility::RenderLayers, prelude::*, render::render_resource::TextureFormat};
use bevy::{
    camera::{primitives::Aabb, visibility::NoFrustumCulling},
    light::NotShadowCaster,
    render::camera::TemporalJitter,
};
use std::collections::{HashMap, HashSet};

#[derive(Resource, Default)]
pub(super) struct State {
    pub(super) source: Option<Entity>,
    camera: Option<Entity>,
    terrain_shader: String,
    village_shader: String,
    terrain: HashMap<Entity, Entity>,
    village: HashMap<Entity, Entity>,
    terrain_materials: HashMap<AssetId<LodMaterial>, Handle<TerrainCapture>>,
    village_materials: HashMap<AssetId<VillageMaterial>, Handle<VillageCapture>>,
}

pub(super) fn synchronize(world: &mut World) {
    let Some(mut state) = world.remove_resource::<State>() else {
        return;
    };
    let result = synchronize_inner(world, &mut state);
    if let Err(error) = result {
        // Keep a visible diagnostic and the opaque fallback. Never render a
        // stale author's scene after unload or a rejected shader replacement.
        if let Some(camera) = state
            .camera
            .and_then(|entity| world.get_mut::<Camera>(entity))
        {
            camera.into_inner().is_active = false;
        }
        let mut observation = world.resource_mut::<Observation>();
        observation.ready = false;
        observation.error = Some(error);
    }
    let ready = world.resource::<Observation>().ready;
    if let Some(resident) = world.get_resource::<crate::tidewater_ocean_optics::ResidentOptics>() {
        let handle = resident.material.clone();
        if let Some(mut material) = world
            .resource_mut::<Assets<crate::tidewater_ocean_optics::OpticalMaterial>>()
            .get_mut(&handle)
        {
            material.refraction_ready.x = f32::from(ready);
        }
    }
    world.insert_resource(state);
}

fn synchronize_inner(world: &mut World, state: &mut State) -> Result<(), String> {
    let source = state.source.ok_or("Refraction awaits its scene camera")?;
    let camera = world
        .get::<Camera>(source)
        .ok_or("Refraction source camera removed")?;
    let full_size = camera
        .physical_viewport_size()
        .ok_or("Refraction source viewport not ready")?;
    let clip = camera.clip_from_view();
    let active = camera.is_active && world.resource::<Control>().enabled;
    let projection = world
        .get::<Projection>(source)
        .cloned()
        .ok_or("Refraction source projection absent")?;
    let pose = *world
        .get::<GlobalTransform>(source)
        .ok_or("Refraction source camera pose absent")?;
    let jitter = world.get::<TemporalJitter>(source).cloned();
    let terrain_handle = world
        .get_resource::<crate::tidewater_terrain::scene::ResidentTerrain>()
        .and_then(|resident| resident.material.clone())
        .ok_or("Refraction awaits its terrain")?;
    let sea = world
        .resource::<Assets<LodMaterial>>()
        .get(&terrain_handle)
        .ok_or("Refraction terrain material missing")?
        .base
        .caustic_params
        .tiles_m
        .z;
    if !sea.is_finite() {
        return Err("Refraction sea level nonfinite".into());
    }
    stage_shaders(world, state)?;
    let extent = super::size(full_size);
    let targets = world.resource::<Targets>().clone();
    resize(world, &targets, extent)?;
    let capture = match state.camera {
        Some(entity) if world.get::<Camera>(entity).is_some() => entity,
        _ => {
            let entity = spawn_camera(world, targets.color.clone());
            state.camera = Some(entity);
            entity
        }
    };
    let capture_size = world
        .get::<Camera>(capture)
        .and_then(Camera::physical_target_size);
    mirror_camera_pose(world, capture, source, projection, pose);
    let mut camera = world
        .get_mut::<Camera>(capture)
        .ok_or("Refraction capture camera missing")?;
    camera.is_active = active;
    // A half-resolution target must retain the exact main projection, even at
    // odd sizes; auto-aspect at 960/540 would differ from a 1919/1079 view.
    camera.computed.clip_from_view = clip;
    if let Some(mut jitter) = jitter {
        jitter.offset *= extent.as_vec2() / full_size.as_vec2();
        world.entity_mut(capture).insert(jitter);
    } else {
        world.entity_mut(capture).remove::<TemporalJitter>();
    }
    synchronize_village(world, state, sea)?;
    synchronize_terrain(world, state, sea)?;
    world.insert_resource(Observation {
        size: extent.to_array(),
        village_parts: state.village.len(),
        terrain_parts: state.terrain.len(),
        source_camera: pose.to_matrix().to_cols_array(),
        capture_camera: pose.to_matrix().to_cols_array(),
        ready: active && capture_size == Some(extent),
        error: None,
    });
    Ok(())
}

/// Update only the disposable capture; the source remains an authored camera.
fn mirror_camera_pose(
    world: &mut World,
    capture: Entity,
    source: Entity,
    projection: Projection,
    pose: GlobalTransform,
) {
    world.entity_mut(capture).insert((
        CaptureSource { view: source },
        projection,
        pose.compute_transform(),
        pose,
    ));
}

fn stage_shaders(world: &mut World, state: &mut State) -> Result<(), String> {
    for (base, target, previous, transform) in [
        (
            &crate::tidewater_terrain::TERRAIN_SHADER,
            &TERRAIN_SHADER,
            &mut state.terrain_shader,
            shaders::terrain as fn(&str) -> Result<String, String>,
        ),
        (
            &crate::tidewater_material::VILLAGE_SHADER,
            &VILLAGE_SHADER,
            &mut state.village_shader,
            shaders::village,
        ),
    ] {
        let shaders = world.resource::<Assets<Shader>>();
        let source = shaders
            .get(base)
            .ok_or("Refraction source shader absent")?
            .source
            .as_str();
        if source == previous {
            continue;
        }
        let patched = transform(source)?;
        source.clone_into(previous);
        world
            .resource_mut::<Assets<Shader>>()
            .insert(
                target.id(),
                Shader::from_wgsl(patched, "tidewater-refraction-capture.wgsl"),
            )
            .map_err(|error| error.to_string())?;
    }
    Ok(())
}

fn resize(world: &mut World, targets: &Targets, extent: UVec2) -> Result<(), String> {
    let mut images = world.resource_mut::<Assets<Image>>();
    for (handle, format) in [
        (&targets.color, TextureFormat::Rgba16Float),
        (&targets.depth, TextureFormat::Depth32Float),
    ] {
        if images
            .get(handle)
            .is_none_or(|image| image.size() != extent)
        {
            images
                .insert(handle.id(), target_image(extent, format))
                .map_err(|error| error.to_string())?;
        }
    }
    Ok(())
}

fn synchronize_village(world: &mut World, state: &mut State, sea: f32) -> Result<(), String> {
    let sources = world
        .query::<(
            Entity,
            &Mesh3d,
            &MeshMaterial3d<VillageMaterial>,
            &GlobalTransform,
            Option<&InheritedVisibility>,
            Option<&Aabb>,
        )>()
        .iter(world)
        .filter(|(_, _, _, _, visible, _)| visible.is_none_or(|v| v.get()))
        .filter(|(_, _, material, pose, _, bounds)| {
            world
                .resource::<Assets<VillageMaterial>>()
                .get(&material.0)
                .is_some_and(|m| m.kind < 6)
                && bounds.is_none_or(|bounds| below_clip(bounds, pose, sea + 0.4))
        })
        .map(|(entity, mesh, material, pose, _, _)| {
            (entity, mesh.clone(), material.0.clone(), *pose)
        })
        .collect::<Vec<_>>();
    let mut handles = HashMap::new();
    for (_, _, source, _) in &sources {
        if handles.contains_key(&source.id()) {
            continue;
        }
        let base = world
            .resource::<Assets<VillageMaterial>>()
            .get(source)
            .cloned()
            .ok_or("Refraction village material removed")?;
        let material = VillageCapture {
            base,
            extension: VillageExtension {
                sea: Vec4::new(sea, 0.0, 0.0, 0.0),
            },
        };
        let target = if let Some(handle) = state.village_materials.get(&source.id()) {
            world
                .resource_mut::<Assets<VillageCapture>>()
                .insert(handle.id(), material)
                .map_err(|error| error.to_string())?;
            handle.clone()
        } else {
            world.resource_mut::<Assets<VillageCapture>>().add(material)
        };
        handles.insert(source.id(), target);
    }
    let mut retained = HashSet::new();
    for (source, mesh, material, pose) in sources {
        let target = *state
            .village
            .entry(source)
            .or_insert_with(|| spawn_mirror(world));
        world.entity_mut(target).insert((
            mesh,
            MeshMaterial3d(handles[&material.id()].clone()),
            pose.compute_transform(),
            pose,
        ));
        retained.insert(source);
    }
    retire(world, &mut state.village, &retained);
    for (source, handle) in &state.village_materials {
        if !handles.contains_key(source) {
            world
                .resource_mut::<Assets<VillageCapture>>()
                .remove(handle.id());
        }
    }
    state.village_materials = handles;
    Ok(())
}

fn synchronize_terrain(world: &mut World, state: &mut State, sea: f32) -> Result<(), String> {
    let sources = world
        .query::<(
            Entity,
            &Mesh3d,
            &MeshMaterial3d<LodMaterial>,
            &GlobalTransform,
            Option<&InheritedVisibility>,
        )>()
        .iter(world)
        .filter(|(_, _, _, _, v)| v.is_none_or(|v| v.get()))
        .map(|(entity, mesh, material, pose, _)| (entity, mesh.clone(), material.0.clone(), *pose))
        .collect::<Vec<_>>();
    let mut handles = HashMap::new();
    for (_, _, source, _) in &sources {
        if handles.contains_key(&source.id()) {
            continue;
        }
        let base = world
            .resource::<Assets<LodMaterial>>()
            .get(source)
            .cloned()
            .ok_or("Refraction terrain material removed")?;
        let material = TerrainCapture {
            base,
            extension: TerrainExtension {
                sea: Vec4::new(sea, 0.0, 0.0, 0.0),
            },
        };
        let target = if let Some(handle) = state.terrain_materials.get(&source.id()) {
            world
                .resource_mut::<Assets<TerrainCapture>>()
                .insert(handle.id(), material)
                .map_err(|error| error.to_string())?;
            handle.clone()
        } else {
            world.resource_mut::<Assets<TerrainCapture>>().add(material)
        };
        handles.insert(source.id(), target);
    }
    let mut retained = HashSet::new();
    for (source, mesh, material, pose) in sources {
        let target = *state
            .terrain
            .entry(source)
            .or_insert_with(|| spawn_mirror(world));
        world.entity_mut(target).insert((
            mesh,
            MeshMaterial3d(handles[&material.id()].clone()),
            pose.compute_transform(),
            pose,
        ));
        retained.insert(source);
    }
    retire(world, &mut state.terrain, &retained);
    for (source, handle) in &state.terrain_materials {
        if !handles.contains_key(source) {
            world
                .resource_mut::<Assets<TerrainCapture>>()
                .remove(handle.id());
        }
    }
    state.terrain_materials = handles;
    Ok(())
}

fn spawn_mirror(world: &mut World) -> Entity {
    world
        .spawn((
            Name::new("Derived Tidewater refraction mesh"),
            RenderLayers::layer(LAYER),
            Visibility::Visible,
            NoFrustumCulling,
            NotShadowCaster,
        ))
        .id()
}

fn retire(world: &mut World, mirrors: &mut HashMap<Entity, Entity>, retained: &HashSet<Entity>) {
    mirrors.retain(|source, target| {
        if retained.contains(source) {
            true
        } else {
            world.despawn(*target);
            false
        }
    });
}

fn below_clip(bounds: &Aabb, pose: &GlobalTransform, clip: f32) -> bool {
    let matrix = pose.to_matrix();
    let center = matrix.transform_point3(bounds.center.into());
    let y_radius = matrix
        .row(1)
        .truncate()
        .abs()
        .dot(bounds.half_extents.into());
    center.y - y_radius < clip
}

#[cfg(test)]
mod tests {
    use super::*;

    #[test]
    fn retargeted_capture_tracks_main_identity_without_mutating_authored_cameras() {
        let mut world = World::new();
        let first_pose = GlobalTransform::from_translation(Vec3::new(2.0, 3.0, 4.0));
        let second_pose = GlobalTransform::from(
            Transform::from_xyz(-5.0, 7.0, 9.0).with_rotation(Quat::from_rotation_y(0.2)),
        );
        let first_projection = Projection::Perspective(PerspectiveProjection {
            fov: 0.7,
            aspect_ratio: 1919.0 / 1079.0,
            ..default()
        });
        let second_projection = Projection::Perspective(PerspectiveProjection {
            fov: 0.9,
            aspect_ratio: 1279.0 / 719.0,
            ..default()
        });
        let first_clip = first_projection.get_clip_from_view();
        let second_clip = second_projection.get_clip_from_view();
        let first = world
            .spawn((
                Camera::default(),
                first_projection.clone(),
                first_pose.compute_transform(),
                first_pose,
                TemporalJitter {
                    offset: Vec2::new(0.25, -0.5),
                },
            ))
            .id();
        let second = world
            .spawn((
                Camera::default(),
                second_projection.clone(),
                second_pose.compute_transform(),
                second_pose,
            ))
            .id();
        let capture = world.spawn(super::super::CaptureCamera).id();
        mirror_camera_pose(&mut world, capture, first, first_projection, first_pose);
        assert_eq!(world.get::<CaptureSource>(capture).unwrap().view, first);
        mirror_camera_pose(&mut world, capture, second, second_projection, second_pose);
        assert_eq!(world.get::<CaptureSource>(capture).unwrap().view, second);
        assert_eq!(world.get::<GlobalTransform>(capture).unwrap(), &second_pose);
        assert_eq!(
            world
                .get::<Projection>(capture)
                .unwrap()
                .get_clip_from_view(),
            second_clip
        );
        for (source, pose, clip) in [
            (first, first_pose, first_clip),
            (second, second_pose, second_clip),
        ] {
            assert_eq!(world.get::<GlobalTransform>(source).unwrap(), &pose);
            assert_eq!(
                world.get::<Transform>(source).unwrap(),
                &pose.compute_transform()
            );
            assert_eq!(
                world
                    .get::<Projection>(source)
                    .unwrap()
                    .get_clip_from_view(),
                clip
            );
            assert!(world.get::<Camera>(source).unwrap().is_active);
            assert!(world.get::<CaptureSource>(source).is_none());
        }
        assert_eq!(
            world.get::<TemporalJitter>(first).unwrap().offset,
            Vec2::new(0.25, -0.5)
        );
        assert!(world.get::<TemporalJitter>(second).is_none());
    }

    #[test]
    fn transformed_bounds_keep_submerged_piles_and_reject_roofs() {
        let bounds = Aabb::from_min_max(Vec3::splat(-1.0), Vec3::splat(1.0));
        assert!(!below_clip(
            &bounds,
            &GlobalTransform::from_translation(Vec3::new(0.0, 3.0, 0.0)),
            0.4
        ));
        let tilted = GlobalTransform::from(
            Transform::from_xyz(55.0, 1.0, 29.0)
                .with_rotation(Quat::from_rotation_z(0.8))
                .with_scale(Vec3::new(1.0, 2.0, 1.0)),
        );
        assert!(below_clip(&bounds, &tilted, 0.4));
    }
}
