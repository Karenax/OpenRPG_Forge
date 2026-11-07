//! Bounded replacement and the existing typed GPU inventory, shared with editors.
use super::{Prepared, PreparedHost, State, Status, bindings};
use crate::source_terrain::{
    lod::{LodMaterial, ViewGeometry},
    scene,
};
use bevy::{
    asset::{UntypedAssetId, VisitAssetDependencies},
    prelude::*,
    tasks::{AsyncComputeTaskPool, Task, futures::check_ready},
};
use forge_core::{ContentHash, EntityId};

pub(super) struct Inventory {
    owner: EntityId,
    identity: ContentHash,
    sources: Vec<UntypedAssetId>,
}

pub(super) enum PreparedUpdate {
    Full(Box<(scene::PreparedTerrain, ViewGeometry)>),
    Paint(Box<scene::PreparedRepaint>, ContentHash),
}

impl PreparedHost {
    /// Queue the latest document after checking the last requested identity.
    /// One CPU task runs at a time; only the latest queued input is retained.
    /// Success is not GPU/physics acknowledgement or publication to the editor.
    pub fn replace(self, world: &mut World, expected: ContentHash) -> Result<(), String> {
        if self.requires_water_context() {
            return Err("Terrain requires an explicit water context before replacement".into());
        }
        let status = world
            .get_resource::<Status>()
            .ok_or("Terrain host absent")?;
        if status.document != Some(expected) || status.error.is_some() {
            return Err("Terrain replacement is stale or its host has failed".into());
        }
        let document = self.plan.document_hash();
        let identity = self.identity;
        let relief = self.relief;
        let domain = self.domain;
        let water_level_m = self.water_context.level()?;
        let mut state = world
            .get_resource_mut::<State>()
            .ok_or("Terrain host has not started")?;
        let serial = state
            .serial
            .checked_add(1)
            .ok_or("Terrain replacement sequence exhausted")?;
        state.serial = serial;
        state.document = document;
        state.relief = relief;
        state.domain = domain;
        state.water_level_m = water_level_m;
        state.queued = Some(self);
        let mut status = world.resource_mut::<Status>();
        status.document = Some(document);
        status.package = Some(identity);
        status.pending = true;
        status.normals_ready = false;
        status.staged_frame = None;
        Ok(())
    }
}

pub(super) fn spawn(
    input: PreparedHost,
    current: Option<scene::ResidentTerrain>,
) -> Task<Result<Prepared, String>> {
    AsyncComputeTaskPool::get().spawn(async move {
        let water_level = input.water_context.level()?;
        if let Some(current) = current
            && let Some(paint) =
                scene::PreparedRepaint::new(&input.plan, &input.package, &current, water_level)?
        {
            return Ok(PreparedUpdate::Paint(
                Box::new(paint),
                input.plan.document_hash(),
            ));
        }
        let geometry = ViewGeometry::new(&input.plan)?;
        let prepared = scene::PreparedTerrain::for_host(
            &input.plan,
            &input.heights,
            &input.package,
            &geometry,
            water_level,
        )?;
        Ok(PreparedUpdate::Full(Box::new((prepared, geometry))))
    })
}

pub(super) fn prepare(world: &mut World, state: &mut State) -> Result<bool, String> {
    if let Some(task) = state.task.as_mut()
        && let Some(prepared) = check_ready(task)
    {
        state.task = None;
        if state.running_serial == state.serial {
            let resident = match prepared? {
                PreparedUpdate::Full(full) => {
                    let (terrain, geometry) = *full;
                    let resident = terrain.stage(
                        world,
                        bindings(world, state.document, &state.shore, state.domain)?,
                    )?;
                    state.tiles.adopt_root(
                        world,
                        resident.entity.ok_or("Prepared terrain root absent")?,
                        &geometry.bootstrap_node(),
                    )?;
                    state.geometry = Some(geometry);
                    resident
                }
                PreparedUpdate::Paint(paint, document) => {
                    let current = state
                        .resident
                        .as_ref()
                        .ok_or("Terrain paint source absent")?;
                    if !current
                        .entity
                        .is_some_and(|root| state.tiles.owns(world, root))
                    {
                        return Err("Terrain paint cache was replaced".into());
                    }
                    let geometry = state
                        .geometry
                        .as_mut()
                        .ok_or("Terrain paint bounds absent")?;
                    if geometry.document_hash != current.document_hash {
                        return Err("Terrain paint bounds are stale".into());
                    }
                    let resident = paint.stage(world)?;
                    geometry.document_hash = document;
                    resident
                }
            };
            state.resident = Some(resident);
            state.view_cadence.invalidate();
            state.selected_nodes.clear();
            world.resource_mut::<Status>().staged_frame = None;
        }
        // A stale result, including its failure, cannot replace the newest request.
    }
    if state.task.is_none()
        && let Some(input) = state.queued.take()
    {
        state.task = Some(spawn(input, state.resident.clone()));
        state.running_serial = state.serial;
    }
    Ok(state.resident.is_some())
}

type Resources = (Vec<Handle<Mesh>>, Handle<LodMaterial>, Vec<UntypedAssetId>);
fn resources(world: &World, state: &State) -> Result<Resources, String> {
    let resident = state.resident.as_ref().ok_or("Terrain projection absent")?;
    if resident.document_hash != state.document
        || resident.relief != state.relief
        || resident.water_level_m.map(f64::to_bits) != state.water_level_m.map(f64::to_bits)
        || world.resource::<Status>().pending
        || super::live::snapshot(world).is_none_or(|surface| surface.terrain_relief != state.relief)
    {
        return Err("Terrain replacement is not published".into());
    }
    let root = resident.entity.ok_or("Terrain root absent")?;
    let material = resident
        .material
        .as_ref()
        .ok_or("Terrain material absent")?
        .clone();
    let mut entities = state.tiles.entities();
    if !entities.contains(&root) {
        entities.push(root);
    }
    let mut meshes = Vec::new();
    for entity in entities {
        if !state.tiles.owns(world, entity)
            || world
                .get::<MeshMaterial3d<LodMaterial>>(entity)
                .is_none_or(|m| m.0 != material)
        {
            return Err("Terrain inventory contains a replaced projection".into());
        }
        let mesh = world
            .get::<Mesh3d>(entity)
            .ok_or("Terrain mesh absent")?
            .0
            .clone();
        if !meshes.contains(&mesh) {
            meshes.push(mesh);
        }
    }
    let mut sources: Vec<_> = meshes.iter().map(|mesh| mesh.id().untyped()).collect();
    sources.push(material.id().untyped());
    world
        .resource::<Assets<LodMaterial>>()
        .get(&material)
        .ok_or("Terrain material retired")?
        .visit_dependencies(&mut |id| sources.push(id));
    sources.sort_unstable();
    sources.dedup();
    Ok((meshes, material, sources))
}

/// Publish this owner's actual meshes/material/dependencies using the common barrier.
/// Stable resources keep their generation; head/view or resource changes revoke it.
pub fn request_inventory(
    world: &mut World,
    owner: EntityId,
    identity: ContentHash,
) -> Result<u64, String> {
    let (meshes, material, generation) =
        world.resource_scope(|world, mut state: Mut<State>| -> Result<_, String> {
            let (meshes, material, sources) = resources(world, &state)?;
            if state
                .inventory
                .as_ref()
                .is_some_and(|current| current.owner != owner)
            {
                return Err("Terrain inventory belongs to another consumer".into());
            }
            if state
                .inventory
                .as_ref()
                .is_none_or(|current| current.identity != identity || current.sources != sources)
            {
                state.inventory_generation = state
                    .inventory_generation
                    .checked_add(1)
                    .ok_or("Terrain inventory generation exhausted")?;
                state.inventory = Some(Inventory {
                    owner,
                    identity,
                    sources,
                });
            }
            Ok((meshes, material, state.inventory_generation))
        })?;
    crate::render_inventory::request_scoped(
        world,
        owner,
        identity,
        generation,
        &meshes,
        &[material],
    )?;
    Ok(generation)
}

/// Release only this consumer's inventory; keep the lifetime counter monotone.
pub fn release_inventory(world: &mut World, owner: EntityId) {
    let has_request = world.get_resource::<State>().is_some_and(|state| {
        state
            .inventory
            .as_ref()
            .is_some_and(|current| current.owner == owner)
    });
    if has_request {
        crate::render_inventory::release_scoped(world, owner);
        world.resource_mut::<State>().inventory = None;
    }
}
