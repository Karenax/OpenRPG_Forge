//! Disposable render mirrors, synchronized after the authored transforms.
use super::{
    CaptureSource, Control, LAYER, Observation, TERRAIN_SHADER, Targets, TerrainCapture,
    TerrainExtension, VILLAGE_SHADER, VillageCapture, VillageExtension, shaders, spawn_camera,
    target_image,
};
use crate::{tidewater_material::VillageMaterial, tidewater_terrain::lod::LodMaterial};
use bevy::camera::primitives::Frustum;
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
    warmup: Option<u64>,
    activity: super::Activity,
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
    state.activity.material_updates = 0;
    state.activity.renderable_updates = 0;
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
        let value = f32::from(ready);
        if world
            .resource::<Assets<crate::tidewater_ocean_optics::OpticalMaterial>>()
            .get(&handle)
            .is_some_and(|material| material.refraction_ready.x.to_bits() != value.to_bits())
            && let Some(mut material) = world
                .resource_mut::<Assets<crate::tidewater_ocean_optics::OpticalMaterial>>()
                .get_mut(&handle)
        {
            material.refraction_ready.x = value;
        }
    }
    world.insert_resource(state.activity.clone());
    world.insert_resource(state);
}

#[allow(
    clippy::too_many_lines,
    reason = "Publish camera, mirrors and capture readiness as one coherent transaction"
)]
fn synchronize_inner(world: &mut World, state: &mut State) -> Result<(), String> {
    let source = state.source.ok_or("Refraction awaits its scene camera")?;
    let camera = world
        .get::<Camera>(source)
        .ok_or("Refraction source camera removed")?;
    let full_size = camera
        .physical_viewport_size()
        .ok_or("Refraction source viewport not ready")?;
    let clip = camera.clip_from_view();
    let enabled = camera.is_active && world.resource::<Control>().enabled;
    let projection = world
        .get::<Projection>(source)
        .cloned()
        .ok_or("Refraction source projection absent")?;
    let pose = *world
        .get::<GlobalTransform>(source)
        .ok_or("Refraction source camera pose absent")?;
    let jitter = world.get::<TemporalJitter>(source).cloned();
    let demanded = super::demand::needed(world, source);
    state.activity.demanded = demanded;
    let active = enabled && demanded;
    if !active {
        suspend(world, state);
        return Ok(());
    }

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
    if world
        .get::<Camera>(capture)
        .is_none_or(|camera| !camera.is_active)
    {
        state.warmup = Some(targets.encoded_frames.load(super::Ordering::Acquire));
    }
    mirror_camera_pose(world, capture, source, projection, pose);
    let mut camera = world
        .get_mut::<Camera>(capture)
        .ok_or("Refraction capture camera missing")?;
    if camera.is_active != active {
        camera.is_active = active;
    }
    // A half-resolution target must retain the exact main projection, even at
    // odd sizes; auto-aspect at 960/540 would differ from a 1919/1079 view.
    camera.computed.clip_from_view = clip;
    if let Some(mut jitter) = jitter {
        jitter.offset *= extent.as_vec2() / full_size.as_vec2();
        if world
            .get::<TemporalJitter>(capture)
            .is_none_or(|current| current.offset != jitter.offset)
        {
            world.entity_mut(capture).insert(jitter);
        }
    } else {
        world.entity_mut(capture).remove::<TemporalJitter>();
    }
    synchronize_village(world, state, sea)?;
    synchronize_terrain(world, state, sea)?;
    let fresh = state.warmup.is_none_or(|baseline| {
        targets
            .encoded_frames
            .load(super::Ordering::Acquire)
            .saturating_sub(baseline)
            >= 2
    });
    if fresh {
        state.warmup = None;
    }
    world.insert_resource(Observation {
        size: extent.to_array(),
        village_parts: state.village.len(),
        terrain_parts: state.terrain.len(),
        source_camera: pose.to_matrix().to_cols_array(),
        capture_camera: pose.to_matrix().to_cols_array(),
        ready: active && fresh && capture_size == Some(extent),
        error: None,
    });
    Ok(())
}

fn suspend(world: &mut World, state: &mut State) {
    if let Some(entity) = state.camera
        && world
            .get::<Camera>(entity)
            .is_some_and(|camera| camera.is_active)
        && let Some(mut camera) = world.get_mut::<Camera>(entity)
    {
        camera.is_active = false;
    }
    state.warmup = None;
    state.activity.suspended_frames = state.activity.suspended_frames.saturating_add(1);
    let mut observation = world.resource_mut::<Observation>();
    observation.ready = false;
    observation.error = None;
}

/// Update only the disposable capture; the source remains an authored camera.
fn mirror_camera_pose(
    world: &mut World,
    capture: Entity,
    source: Entity,
    projection: Projection,
    pose: GlobalTransform,
) {
    if world
        .get::<CaptureSource>(capture)
        .is_none_or(|current| current.view != source)
    {
        world
            .entity_mut(capture)
            .insert(CaptureSource { view: source });
    }
    if world
        .get::<Projection>(capture)
        .is_none_or(|current| current.get_clip_from_view() != projection.get_clip_from_view())
    {
        world.entity_mut(capture).insert(projection);
    }
    if world.get::<GlobalTransform>(capture) != Some(&pose) {
        world
            .entity_mut(capture)
            .insert((pose.compute_transform(), pose));
    }
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
            if world
                .resource::<Assets<VillageCapture>>()
                .get(handle)
                .is_none_or(|old| {
                    old.base != material.base || old.extension.sea != material.extension.sea
                })
            {
                world
                    .resource_mut::<Assets<VillageCapture>>()
                    .insert(handle.id(), material)
                    .map_err(|error| error.to_string())?;
                state.activity.material_updates += 1;
            }
            handle.clone()
        } else {
            state.activity.material_updates += 1;
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
        state.activity.renderable_updates += u32::from(synchronize_renderable(
            world,
            target,
            mesh,
            handles[&material.id()].clone(),
            pose,
        ));
        let visible = world.get::<Aabb>(source).is_none_or(|bounds| {
            state
                .source
                .and_then(|camera| world.get::<Frustum>(camera))
                .is_none_or(|frustum| frustum.intersects_obb(bounds, &pose.affine(), true, false))
        });
        let visibility = if visible {
            Visibility::Visible
        } else {
            Visibility::Hidden
        };
        if world.get::<Visibility>(target) != Some(&visibility) {
            world.entity_mut(target).insert(visibility);
        }
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
            if world
                .resource::<Assets<TerrainCapture>>()
                .get(handle)
                .is_none_or(|old| {
                    old.base.base != material.base.base
                        || old.base.extension != material.base.extension
                        || old.extension.sea != material.extension.sea
                })
            {
                world
                    .resource_mut::<Assets<TerrainCapture>>()
                    .insert(handle.id(), material)
                    .map_err(|error| error.to_string())?;
                state.activity.material_updates += 1;
            }
            handle.clone()
        } else {
            state.activity.material_updates += 1;
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
        state.activity.renderable_updates += u32::from(synchronize_renderable(
            world,
            target,
            mesh,
            handles[&material.id()].clone(),
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

fn synchronize_renderable<M: Material>(
    world: &mut World,
    target: Entity,
    mesh: Mesh3d,
    material: Handle<M>,
    pose: GlobalTransform,
) -> bool {
    let mut changed = false;
    if world
        .get::<Mesh3d>(target)
        .is_none_or(|old| old.0 != mesh.0)
    {
        world.entity_mut(target).insert(mesh);
        changed = true;
    }
    if world
        .get::<MeshMaterial3d<M>>(target)
        .is_none_or(|old| old.0 != material)
    {
        world.entity_mut(target).insert(MeshMaterial3d(material));
        changed = true;
    }
    if world.get::<GlobalTransform>(target) != Some(&pose) {
        world
            .entity_mut(target)
            .insert((pose.compute_transform(), pose));
        changed = true;
    }
    changed
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
    fn unchanged_mirrors_stay_clean_and_pose_or_material_changes_are_published() {
        let mut world = World::new();
        let target = spawn_mirror(&mut world);
        let mesh = Mesh3d(Handle::default());
        let material: Handle<StandardMaterial> =
            bevy::asset::uuid_handle!("bdff88b6-e4d4-4571-9466-742c386bd528");
        let pose = GlobalTransform::from_translation(Vec3::new(1.0, 2.0, 3.0));
        assert!(synchronize_renderable(
            &mut world,
            target,
            mesh.clone(),
            material.clone(),
            pose
        ));
        world.clear_trackers();
        assert!(!synchronize_renderable(
            &mut world,
            target,
            mesh.clone(),
            material.clone(),
            pose
        ));
        assert!(
            !world
                .entity(target)
                .get_ref::<Mesh3d>()
                .unwrap()
                .is_changed()
        );
        assert!(
            !world
                .entity(target)
                .get_ref::<GlobalTransform>()
                .unwrap()
                .is_changed()
        );
        let moved = GlobalTransform::from_translation(Vec3::new(4.0, 2.0, 3.0));
        let replacement: Handle<StandardMaterial> =
            bevy::asset::uuid_handle!("663cf484-148b-4a87-bf40-e6566bd39e8a");
        assert!(synchronize_renderable(
            &mut world,
            target,
            mesh,
            replacement.clone(),
            moved
        ));
        assert!(
            !world
                .entity(target)
                .get_ref::<Mesh3d>()
                .unwrap()
                .is_changed()
        );
        assert_eq!(world.get::<GlobalTransform>(target), Some(&moved));
        assert_eq!(
            world
                .get::<MeshMaterial3d<StandardMaterial>>(target)
                .unwrap()
                .0,
            replacement
        );
    }

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
