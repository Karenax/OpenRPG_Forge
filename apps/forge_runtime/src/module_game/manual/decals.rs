//! Place the SHA-pinned mesh decals on the player camera with the Studio renderer.
use bevy::{
    prelude::*,
    render::renderer::{RenderDevice, RenderQueue},
};
use forge_engine::{
    ManualPlacementProjection,
    source_decal_map::SourceDecalMap,
    source_decal_view::{SourceDecalRaster, SourceDecalViewPlugin, SourceDecalViewStatus},
    source_graph::SourceGraphMaterial,
    source_graph_runtime::SourceSceneResidents,
};
use std::collections::{BTreeMap, BTreeSet};

#[derive(Component)]
struct MeshDecal(SourceDecalRaster);

pub(super) fn register(app: &mut App) {
    if !app.is_plugin_added::<SourceDecalViewPlugin>() {
        app.add_plugins(SourceDecalViewPlugin);
    }
    app.add_systems(Startup, spawn).add_systems(
        PostUpdate,
        draw.after(super::observe_source_graph)
            .after(bevy::transform::TransformSystems::Propagate),
    );
}

fn spawn(world: &mut World) {
    let result = (|| {
        let device = world
            .get_resource::<RenderDevice>()
            .ok_or("Mesh decal GPU is absent")?;
        let queue = world
            .get_resource::<RenderQueue>()
            .ok_or("Mesh decal GPU queue is absent")?;
        let mut cache = BTreeMap::<_, SourceDecalRaster>::new();
        let mut ready = Vec::new();
        for (placement, bytes) in &world.resource::<super::PreparedManual>().decals {
            let raster = if let Some(cached) = cache.get(&placement.asset_hash) {
                cached.clone()
            } else {
                if forge_core::bytes_sha256(bytes) != placement.asset_hash {
                    return Err("Mesh decal differs from its saved asset pin".into());
                }
                let raster =
                    bevy::tasks::block_on(SourceDecalRaster::prepare(device, queue, bytes))?;
                cache.insert(placement.asset_hash, raster.clone());
                raster
            };
            ready.push((placement.clone(), raster));
        }
        Ok::<_, String>(ready)
    })();
    match result {
        Ok(ready) => {
            for (placement, raster) in ready {
                world.spawn((
                    Name::new(format!("Module mesh decal · {}", placement.name)),
                    forge_engine::placement_transform(&placement),
                    ManualPlacementProjection(placement),
                    MeshDecal(raster),
                ));
            }
        }
        Err(error) => super::refuse(world, error),
    }
}

#[derive(Default)]
struct State {
    map: SourceDecalMap,
    since: Option<f64>,
}

fn draw(world: &mut World, mut state: Local<State>) {
    let now = world.resource::<Time<Real>>().elapsed_secs_f64();
    let started = *state.since.get_or_insert(now);
    match reconcile(world, &mut state.map) {
        Ok(true) => state.since = None,
        Ok(false) if now - started < 45.0 => {}
        Ok(false) => super::refuse(world, "No compatible mesh decal view became ready".into()),
        Err(error) => super::refuse(world, error),
    }
}

fn reconcile(world: &mut World, map: &mut SourceDecalMap) -> Result<bool, String> {
    let mut cameras = world.query_filtered::<(Entity, &Camera), (
        forge_engine::source_sky::preview::SceneCameraFilter,
        With<forge_engine::ForgePlayerCamera>,
    )>();
    let cameras = cameras
        .iter(world)
        .filter(|(_, camera)| camera.is_active)
        .map(|(e, _)| e)
        .collect::<Vec<_>>();
    let [camera] = cameras.as_slice() else {
        map.clear(world);
        return Ok(false);
    };
    let mut decals = world.query::<(&MeshDecal, &ManualPlacementProjection, &GlobalTransform)>();
    let mut draws = decals
        .iter(world)
        .map(|(r, p, t)| (p.0.id, r.0.clone(), t.to_matrix()))
        .collect::<Vec<_>>();
    draws.sort_by_key(|item| item.0);
    let draws = draws
        .into_iter()
        .map(|(_, r, t)| (r, t))
        .collect::<Vec<_>>();
    let count = draws.len();
    let mut roots = world
        .query_filtered::<(Entity, &ManualPlacementProjection), With<super::ModuleSourceGraph>>();
    let roots = roots
        .iter(world)
        .map(|(root, placement)| (root, placement.0.asset_hash))
        .collect::<Vec<_>>();
    let scenes = world.resource::<SourceSceneResidents>();
    let materials: BTreeSet<_> = roots
        .iter()
        .filter_map(|(root, pin)| scenes.static_primitives_for_carrier(*root, *pin))
        .flatten()
        .filter_map(|e| world.get::<MeshMaterial3d<SourceGraphMaterial>>(*e))
        .map(|handle| handle.0.id())
        .collect();
    match map.reconcile(world, *camera, &materials, draws)? {
        SourceDecalViewStatus::Encoded(n) if n == count && n != 0 => Ok(true),
        SourceDecalViewStatus::Refused(reason) => Err(reason),
        _ => Ok(false),
    }
}
