//! Publish complete shadow generations; never replace a pending GPU pass each frame.
use crate::authoring_assets::{imported_buildings, imported_scene};
use bevy::{
    ecs as bevy_ecs,
    math::{DMat4, DQuat},
    prelude::*,
};
use forge_engine::{
    sampled_terrain_physics::SampledTerrainPhysicsState,
    sun_cascades::CascadeView,
    tidewater_assets::scene::ResidentScene,
    tidewater_material::VillageMaterial,
    tidewater_object_shadow::{graph, scene, terrain},
    tidewater_scene_environment::ResidentEnvironment,
    tidewater_terrain::{TerrainMaterial, lod::LodMaterial},
};
use std::sync::atomic::Ordering;

#[derive(Resource)]
pub(crate) struct Receiver(pub graph::Target);
#[derive(Clone, PartialEq)]
struct Source {
    terrain: forge_core::ContentHash,
    hill: AssetId<Image>,
    library: u64,
    environment: AssetId<Image>,
    roots: Vec<Entity>,
}
#[derive(Clone, PartialEq)]
struct Key {
    source: Source,
    pose: Vec<u32>,
}
#[derive(Resource, Default)]
struct State {
    committed: Option<Key>,
    pending: Option<(Key, graph::Target)>,
    failed: Option<Key>,
    terrain: Option<(u64, terrain::authored::PreparedSource)>,
}
pub(crate) fn install(app: &mut App) {
    app.init_resource::<State>()
        .add_systems(PostUpdate, update.after(imported_scene::update));
}
fn snapshot(world: &World, camera: Entity) -> Result<(Key, CascadeView), String> {
    let transform = world.get::<Transform>(camera).ok_or("Caméra absente")?;
    let Some(Projection::Perspective(projection)) = world.get::<Projection>(camera) else {
        return Err("Perspective absente".into());
    };
    let environment = world.resource::<ResidentEnvironment>();
    let instances = world.get_resource::<ResidentScene>();
    let mut roots = Vec::new();
    let mut pose = transform
        .to_matrix()
        .to_cols_array()
        .map(f32::to_bits)
        .to_vec();
    pose.extend(
        [
            projection.fov,
            projection.aspect_ratio,
            projection.near,
            projection.far,
        ]
        .map(f32::to_bits),
    );
    pose.extend(
        environment
            .scene_lighting
            .sun_direction
            .to_array()
            .map(f32::to_bits),
    );
    if let Some(scene) = instances {
        for instance in &scene.instances {
            roots.push(instance.root);
            pose.extend(
                world
                    .get::<Transform>(instance.root)
                    .ok_or("Pose bâtiment absente")?
                    .to_matrix()
                    .to_cols_array()
                    .map(f32::to_bits),
            );
        }
    }
    let view = CascadeView {
        world_from_view: DMat4::from_rotation_translation(
            DQuat::from_array(transform.rotation.to_array().map(f64::from)).normalize(),
            transform.translation.as_dvec3(),
        ),
        near: f64::from(projection.near),
        fov_degrees: f64::from(projection.fov).to_degrees(),
        aspect: f64::from(projection.aspect_ratio),
    };
    Ok((
        Key {
            source: Source {
                terrain: world
                    .resource::<SampledTerrainPhysicsState>()
                    .plan
                    .document_hash(),
                hill: world
                    .resource::<forge_engine::tidewater_hill_shadow::Images>()
                    .output
                    .id(),
                library: imported_buildings::published_generation(),
                environment: environment.environment.id(),
                roots,
            },
            pose,
        },
        view,
    ))
}
fn update(world: &mut World) {
    if !crate::authoring_viewport::can_project_sources() || !world.contains_resource::<Receiver>() {
        return;
    }
    let Some((_, camera)) = crate::authoring_imported_terrain::shadow_source(world) else {
        return;
    };
    let (key, view) = match snapshot(world, camera) {
        Ok(value) => value,
        Err(error) => {
            crate::authoring_viewport::imported_failure(&error);
            return;
        }
    };
    world.resource_scope(|world, mut state: Mut<State>| {
        if let Some((pending, target)) = &state.pending {
            if pending.source != key.source {
                // Deleted/replaced objects and terrain must never publish an obsolete pass.
                state.pending = None;
            } else if !target.ready.load(Ordering::Acquire) {
                return;
            } else {
                // Camera/gizmo motion may continue while rendering: accept the completed
                // pose, then enqueue the latest one. Otherwise motion starves publication.
                publish(world, target);
                state.committed = Some(pending.clone());
                state.pending = None;
            }
        }
        if state.committed.as_ref() == Some(&key) || state.failed.as_ref() == Some(&key) {
            return;
        }
        match prepare(world, &mut state, &key.source, view)
            .and_then(|input| graph::stage(world, input))
        {
            Ok(target) => {
                state.pending = Some((key, target));
                state.failed = None;
            }
            Err(error) => {
                state.failed = Some(key);
                crate::authoring_viewport::report(format!(
                    "Ombres importées non actualisées : {error}"
                ));
            }
        }
    });
}
fn prepare(
    world: &World,
    state: &mut State,
    source: &Source,
    view: CascadeView,
) -> Result<graph::Input, String> {
    let (document, _) =
        crate::authoring_imported_terrain::shadow_source(world).ok_or("Terrain non prêt")?;
    if state.terrain.as_ref().is_none_or(|(generation, cached)| {
        *generation != source.library || cached.document_hash != source.terrain
    }) {
        let package = imported_buildings::terrain_package().ok_or("Paquet terrain absent")?;
        let shader = std::str::from_utf8(
            package
                .file("terrain-lod.wgsl")
                .ok_or("Shader relief absent")?,
        )
        .map_err(|e| e.to_string())?;
        let prepared = terrain::authored::PreparedSource::new(
            document,
            crate::authoring_raster::source_bytes(document)?.to_vec(),
            shader,
        )
        .map_err(|e| e.to_string())?;
        state.terrain = Some((source.library, prepared));
    }
    let terrain = state
        .terrain
        .as_ref()
        .ok_or("Cache terrain absent")?
        .1
        .at(view)
        .map_err(|e| e.to_string())?;
    let sun = world
        .resource::<ResidentEnvironment>()
        .scene_lighting
        .sun_direction
        .truncate()
        .as_dvec3()
        .normalize();
    scene::prepare(view, sun, scene::buildings(world)?, Some(terrain))
}
fn publish(world: &mut World, target: &graph::Target) {
    for (_, material) in world.resource_mut::<Assets<VillageMaterial>>().iter_mut() {
        material.object_shadow = target.image.clone();
        material.object_settings = target.settings.clone();
    }
    for (_, material) in world.resource_mut::<Assets<TerrainMaterial>>().iter_mut() {
        material.object_shadow = target.image.clone();
        material.object_settings = target.settings.clone();
    }
    for (_, material) in world.resource_mut::<Assets<LodMaterial>>().iter_mut() {
        material.base.object_shadow = target.image.clone();
        material.base.object_settings = target.settings.clone();
    }
    world.insert_resource(Receiver(target.clone()));
}
