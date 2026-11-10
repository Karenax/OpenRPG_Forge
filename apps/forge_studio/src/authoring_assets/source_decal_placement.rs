//! Project pinned mesh decals saved with the other manual placements.
use bevy::{
    ecs as bevy_ecs,
    prelude::*,
    render::renderer::{RenderDevice, RenderQueue},
};
use forge_engine::{
    source_decal_map::SourceDecalMap,
    source_decal_view::{SourceDecalRaster, SourceDecalViewStatus},
    source_graph::SourceGraphMaterial,
};
use std::{cell::RefCell, collections::BTreeMap, sync::Arc};

#[derive(Component)]
pub(super) struct Carrier(pub Arc<[u8]>);

#[derive(Component)]
pub(super) enum Status {
    Ready { center: Vec3, radius: f32 },
    Refused(String),
}

#[derive(Resource, Default)]
struct Map(SourceDecalMap);

type Pin = forge_core::ContentHash;
type Completed = (Pin, Result<SourceDecalRaster, String>);
thread_local! {
    static RESULT: RefCell<Option<Completed>> = const { RefCell::new(None) };
}

#[derive(Default)]
struct Prep {
    pending: Option<Pin>,
    loaded: BTreeMap<Pin, Result<SourceDecalRaster, String>>,
}

pub(super) fn register(app: &mut App) {
    app.init_resource::<Map>().add_systems(
        PostUpdate,
        update
            .after(bevy::transform::TransformSystems::Propagate)
            .before(super::placements::PlacementSystems::Projection),
    );
}

pub(super) fn release_camera(world: &mut World) {
    world.resource_scope(|world, mut map: Mut<Map>| map.0.clear(world));
}

fn update(world: &mut World, mut prep: Local<Prep>) {
    let mut query = world.query::<(
        Entity,
        &Carrier,
        &forge_engine::ManualPlacementProjection,
        &GlobalTransform,
        &Visibility,
        Option<&Status>,
    )>();
    let mut requests = query
        .iter(world)
        .map(|(entity, carrier, placement, pose, visibility, status)| {
            (
                placement.0.id,
                placement.0.asset_hash,
                entity,
                carrier.0.clone(),
                pose.to_matrix(),
                *visibility != Visibility::Hidden || status.is_none(),
            )
        })
        .collect::<Vec<_>>();
    requests.sort_by_key(|r| r.0);
    if let Some((key, result)) = RESULT.with(|slot| slot.borrow_mut().take()) {
        if prep.pending == Some(key) {
            prep.pending = None;
            prep.loaded.insert(key, result);
        }
    }
    prep.loaded
        .retain(|key, _| requests.iter().any(|request| &request.1 == key));
    if requests.is_empty() {
        release_camera(world);
        return;
    }
    if prep.pending.is_none()
        && let Some((_, pin, _, bytes, _, _)) =
            requests.iter().find(|r| !prep.loaded.contains_key(&r.1))
        && let (Some(device), Some(queue)) = (
            world.get_resource::<RenderDevice>().cloned(),
            world.get_resource::<RenderQueue>().cloned(),
        )
    {
        let pin = *pin;
        let bytes = bytes.clone();
        prep.pending = Some(pin);
        wasm_bindgen_futures::spawn_local(async move {
            let result = if forge_core::bytes_sha256(&bytes) == pin {
                SourceDecalRaster::prepare(&device, &queue, &bytes).await
            } else {
                Err("La toile ne correspond pas au fichier de la carte".into())
            };
            RESULT.with(|slot| slot.replace(Some((pin, result))));
        });
    }
    let mut draws = Vec::new();
    for &(_, pin, entity, _, pose, visible) in &requests {
        match prep.loaded.get(&pin) {
            Some(Ok(raster)) if visible => draws.push((raster.clone(), pose)),
            Some(Err(error)) => {
                world
                    .entity_mut(entity)
                    .insert(Status::Refused(error.clone()));
            }
            _ => {}
        }
    }
    let mut cameras = world.query_filtered::<(Entity, &Camera), (
        forge_engine::source_sky::preview::SceneCameraFilter,
        Without<forge_engine::ForgePlayerCamera>,
    )>();
    let cameras = cameras
        .iter(world)
        .filter(|(_, c)| c.is_active)
        .map(|(entity, _)| entity)
        .collect::<Vec<_>>();
    let [camera] = cameras.as_slice() else {
        release_camera(world);
        return;
    };
    let mut receivers = world.query_filtered::<&MeshMaterial3d<SourceGraphMaterial>,
        With<forge_engine::ManualPlacementPart>>();
    let receivers = receivers.iter(world).map(|handle| handle.0.id()).collect();
    let expected = draws.len();
    let result = world.resource_scope(|world, mut map: Mut<Map>| {
        map.0.reconcile(world, *camera, &receivers, draws)
    });
    let error = match result {
        Ok(SourceDecalViewStatus::Encoded(count)) if count == expected && count != 0 => {
            for &(_, pin, entity, _, _, _) in &requests {
                if world.get::<Status>(entity).is_some() {
                    continue;
                }
                if let Some(Ok(raster)) = prep.loaded.get(&pin) {
                    let status = match raster.preview_frame() {
                        Ok((center, _, radius)) => Status::Ready { center, radius },
                        Err(error) => Status::Refused(error),
                    };
                    world.entity_mut(entity).insert(status);
                }
            }
            None
        }
        Ok(SourceDecalViewStatus::Refused(reason)) | Err(reason) => Some(reason),
        _ => None,
    };
    if let Some(error) = error {
        for (_, _, entity, _, _, _) in requests {
            world
                .entity_mut(entity)
                .insert(Status::Refused(error.clone()));
        }
    }
}
