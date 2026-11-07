//! Terrain-only hierarchy admitted atomically with the fine cells. General static
//! scene/vegetation HLOD remains a separate contract; these meshes never collide.
use super::{CellMaterial, CellSource, LiveCell, PreparedBatch};
use bevy::{
    asset::RenderAssetUsages, mesh::Indices, prelude::*, render::render_resource::PrimitiveTopology,
};
use forge_core::{ContentHash, EntityId};
use forge_world::{
    cell_streaming::{CellDetail, CellStreamPlan},
    sampled_terrain::SampledTerrainPlan,
    terrain_hlod::{self, TerrainHlodKey, TerrainHlodLayout, TerrainHlodTile},
};
use std::collections::BTreeMap;

#[cfg(test)]
mod tests;

// Includes stride-two interiors and refined edges; checked again on real payloads.
const TILE_BYTES: u64 = 32_768;
// Shared topology permits at most 541 vertices and 2,472 indices. The temporary
// RGB and RGBA buffers coexist, and procedural tiles also carry canonical UVs.
const PROCEDURAL_TILE_BYTES: u64 = 541 * 60 + 2472 * 4;

#[derive(Resource, Clone, Debug, Default)]
pub struct TerrainHlodStats {
    pub tiles: usize,
    pub aggregated_tiles: usize,
    pub covered_base_cells: usize,
    pub maximum_level: u8,
    pub cpu_mesh_bytes: u64,
    pub resident_triangles: usize,
    /// Combined fine + distant owned CPU buffers, old meshes included during staging.
    pub peak_staging_mesh_bytes: u64,
}

#[derive(Component, Clone, Copy, Debug)]
pub struct StreamedTerrainHlod {
    pub logical_id: EntityId,
    pub tile: TerrainHlodTile,
    pub source: ContentHash,
}

#[derive(Default)]
pub(super) struct State {
    layout: Option<TerrainHlodLayout>,
    live: BTreeMap<TerrainHlodKey, DistantCell>,
}

impl State {
    pub(super) fn layout(&self) -> Option<&TerrainHlodLayout> {
        self.layout.as_ref()
    }
}

struct DistantCell {
    tile: TerrainHlodTile,
    source: ContentHash,
    stride: u8,
    cell: LiveCell,
}

#[derive(Clone, Debug, PartialEq, Eq)]
pub(super) struct Request {
    layout: TerrainHlodLayout,
    source: ContentHash,
    stride: u8,
    tile_bytes: u64,
    acquire: BTreeMap<TerrainHlodKey, TerrainHlodTile>,
}

pub(super) struct Prepared {
    request: Request,
    meshes: BTreeMap<TerrainHlodKey, PreparedMesh>,
}

struct PreparedMesh {
    mesh: Mesh,
    bytes: u64,
    staging_bytes: u64,
    triangles: usize,
}

pub(super) fn request(
    state: &super::State,
    plan: &CellStreamPlan,
) -> Result<Option<Request>, String> {
    request_with_stride(state, plan, state.source.mesh_quality().distant_stride())
}

pub(super) fn request_with_stride(
    state: &super::State,
    plan: &CellStreamPlan,
    stride: u8,
) -> Result<Option<Request>, String> {
    let Some(distant) = &state.hlod else {
        return Ok(None);
    };
    let layout = TerrainHlodLayout::new(&state.policy, plan.desired())?;
    let acquire: BTreeMap<_, _> = layout
        .tiles()
        .iter()
        .filter(|(key, tile)| {
            !distant.live.get(key).is_some_and(|live| {
                live.tile == **tile && live.source == plan.source() && live.stride == stride
            })
        })
        .map(|(&key, &tile)| (key, tile))
        .collect();
    let next = Request {
        layout,
        source: plan.source(),
        stride,
        tile_bytes: match &state.source {
            CellSource::Sampled(_) => TILE_BYTES,
            CellSource::Procedural { .. } => PROCEDURAL_TILE_BYTES,
        },
        acquire,
    };
    let fine_new = plan
        .acquisitions()
        .values()
        .try_fold(0_u64, |sum, detail| {
            sum.checked_add(if *detail == CellDetail::Full {
                state.policy.full_cell_bytes
            } else {
                state.policy.hlod_cell_bytes
            })
            .ok_or("Terrain HLOD fine byte estimate overflow")
        })?;
    let distant_new = (next.acquire.len() as u64)
        .checked_mul(next.tile_bytes)
        .ok_or("Terrain HLOD tile byte estimate overflow")?;
    let distant_resident = retained_bytes(distant, &next) + distant_new;
    check_bytes(
        state,
        plan.estimated_resident_bytes,
        fine_new,
        distant_resident,
        distant_new,
    )?;
    Ok(Some(next))
}

fn retained_bytes(state: &State, request: &Request) -> u64 {
    state
        .live
        .iter()
        .filter(|(key, _)| {
            request.layout.tiles().contains_key(key) && !request.acquire.contains_key(key)
        })
        .map(|(_, live)| live.cell.bytes)
        .sum()
}

fn check_bytes(
    state: &super::State,
    fine_resident: u64,
    fine_new: u64,
    distant_resident: u64,
    distant_new: u64,
) -> Result<u64, String> {
    let old_fine: u64 = state.live.values().map(|cell| cell.bytes).sum();
    let old_distant: u64 = state.hlod.as_ref().map_or(0, |distant| {
        distant.live.values().map(|live| live.cell.bytes).sum()
    });
    let resident = fine_resident
        .checked_add(distant_resident)
        .ok_or("Terrain HLOD resident byte count overflow")?;
    let peak = [old_fine, old_distant, fine_new, distant_new]
        .into_iter()
        .try_fold(0_u64, u64::checked_add)
        .ok_or("Terrain HLOD staging byte count overflow")?;
    if resident > state.policy.max_resident_bytes || peak > state.policy.max_staging_bytes {
        return Err(
            "Combined fine terrain and HLOD exceeds the resident/staging byte budget".into(),
        );
    }
    Ok(peak)
}

pub(super) fn prepare(
    terrain: &SampledTerrainPlan,
    request: Request,
    authored: bool,
) -> Result<Prepared, String> {
    if terrain.document_hash() != request.source {
        return Err("Terrain HLOD preparation source changed".into());
    }
    let mut meshes = BTreeMap::new();
    for (&key, &tile) in &request.acquire {
        let payload = terrain_hlod::build_mesh_with_stride(terrain, tile, request.stride)?;
        let mut bytes = payload.bytes();
        let staging_bytes = bytes;
        let triangles = payload.indices.len() / 3;
        if bytes > TILE_BYTES {
            return Err("Terrain HLOD payload exceeds its preparation bound".into());
        }
        let mut mesh = Mesh::new(
            PrimitiveTopology::TriangleList,
            RenderAssetUsages::default(),
        );
        mesh.insert_attribute(Mesh::ATTRIBUTE_POSITION, payload.positions_m);
        mesh.insert_attribute(Mesh::ATTRIBUTE_NORMAL, payload.normals);
        if authored {
            mesh.insert_attribute(Mesh::ATTRIBUTE_COLOR, payload.material_weights);
        } else {
            bytes -= (payload.material_weights.capacity() * size_of::<[f32; 4]>()) as u64;
        }
        mesh.insert_indices(Indices::U32(payload.indices));
        meshes.insert(
            key,
            PreparedMesh {
                mesh,
                bytes,
                staging_bytes,
                triangles,
            },
        );
    }
    Ok(Prepared { request, meshes })
}

pub(super) fn prepare_source(
    source: &CellSource,
    request: Request,
    authored: bool,
) -> Result<Prepared, String> {
    match source {
        CellSource::Sampled(source) => prepare(source.terrain(), request, authored),
        CellSource::Procedural { source, .. } => prepare_procedural(source, request, authored),
    }
}

fn prepare_procedural(
    source: &forge_world::cell_streaming::ProceduralCellSource,
    request: Request,
    authored: bool,
) -> Result<Prepared, String> {
    if source.residency_source() != request.source || request.tile_bytes != PROCEDURAL_TILE_BYTES {
        return Err("Procedural terrain HLOD preparation source changed".into());
    }
    let mut meshes = BTreeMap::new();
    for (&key, &tile) in &request.acquire {
        let payload =
            terrain_hlod::build_procedural_mesh_with_stride(source, tile, request.stride)?;
        let original = payload.bytes();
        let triangles = payload.indices.len() / 3;
        let rgb_bytes = (payload.material_weights.capacity()
            * size_of::<forge_world::TerrainMaterialWeights>()) as u64;
        let mut staging_bytes = original;
        let mut bytes = original
            .checked_sub(rgb_bytes)
            .ok_or("Terrain HLOD material byte count mismatch")?;
        let mut mesh = Mesh::new(
            PrimitiveTopology::TriangleList,
            RenderAssetUsages::default(),
        );
        if authored {
            let colors: Vec<_> = payload
                .material_weights
                .iter()
                .map(|weights| [weights.grass, weights.earth, weights.rock, 1.0])
                .collect();
            let color_bytes = (colors.capacity() * size_of::<[f32; 4]>()) as u64;
            staging_bytes = original
                .checked_add(color_bytes)
                .ok_or("Terrain HLOD staging byte count overflow")?;
            bytes = bytes
                .checked_add(color_bytes)
                .ok_or("Terrain HLOD buffer byte count overflow")?;
            mesh.insert_attribute(Mesh::ATTRIBUTE_COLOR, colors);
        }
        if staging_bytes > request.tile_bytes {
            return Err("Procedural terrain HLOD exceeds its preparation bound".into());
        }
        mesh.insert_attribute(Mesh::ATTRIBUTE_POSITION, payload.positions_m);
        mesh.insert_attribute(Mesh::ATTRIBUTE_NORMAL, payload.normals);
        mesh.insert_attribute(Mesh::ATTRIBUTE_UV_0, payload.uvs);
        mesh.insert_indices(Indices::U32(payload.indices));
        meshes.insert(
            key,
            PreparedMesh {
                mesh,
                bytes,
                staging_bytes,
                triangles,
            },
        );
    }
    Ok(Prepared { request, meshes })
}

fn live_is_intact(world: &World, live: &DistantCell) -> bool {
    live.cell.is_intact(world)
        && world
            .get::<StreamedTerrainHlod>(live.cell.entity)
            .is_some_and(|marker| {
                marker.tile == live.tile
                    && marker.source == live.source
                    && marker.logical_id == live.cell.logical_id
            })
}

pub(super) fn is_synchronized(world: &World, state: &super::State) -> bool {
    let Some(distant) = &state.hlod else {
        return true;
    };
    let Some(layout) = &distant.layout else {
        return false;
    };
    let source = state.source.residency_source();
    let stride = state.source.mesh_quality().distant_stride();
    let total = usize::from(state.policy.dimensions[0]) * usize::from(state.policy.dimensions[1]);
    layout.covered_cells() + state.live.len() == total
        && layout.tiles().len() == distant.live.len()
        && distant.live.values().all(|live| {
            live.source == source && live.stride == stride && live_is_intact(world, live)
        })
}

pub(super) fn validate(
    world: &World,
    state: &super::State,
    plan: &CellStreamPlan,
    prepared: &PreparedBatch,
) -> Result<u64, String> {
    let Some(distant) = &state.hlod else {
        return if prepared.hlod.is_none() {
            Ok(0)
        } else {
            Err("Unexpected terrain HLOD acknowledgement".into())
        };
    };
    let received = prepared
        .hlod
        .as_ref()
        .ok_or("Missing terrain HLOD acknowledgement")?;
    let expected = request(state, plan)?.ok_or("Terrain HLOD controller disappeared")?;
    if received.request != expected
        || received.meshes.keys().ne(expected.acquire.keys())
        || received
            .meshes
            .values()
            .any(|mesh| mesh.bytes > mesh.staging_bytes || mesh.staging_bytes > expected.tile_bytes)
    {
        return Err("Terrain HLOD acknowledgement differs from the admitted cell plan".into());
    }
    if distant
        .live
        .values()
        .any(|live| !live_is_intact(world, live))
    {
        return Err("An active terrain HLOD projection was removed or replaced".into());
    }
    let fine_new = prepared
        .cells
        .iter()
        .try_fold(0_u64, |sum, cell| sum.checked_add(cell.staging_bytes))
        .ok_or("Terrain HLOD fine byte count overflow")?;
    let fine_retained: u64 = state
        .stream
        .resident()
        .iter()
        .filter(|(key, _)| {
            plan.desired().contains_key(key) && !plan.acquisitions().contains_key(key)
        })
        .map(|(_, cell)| cell.bytes)
        .sum();
    let fine_resident_new = prepared
        .cells
        .iter()
        .try_fold(0_u64, |sum, cell| {
            sum.checked_add(cell.acknowledgement.bytes)
        })
        .ok_or("Terrain HLOD fine resident byte count overflow")?;
    let distant_new: u64 = received.meshes.values().map(|mesh| mesh.bytes).sum();
    let distant_staging: u64 = received
        .meshes
        .values()
        .map(|mesh| mesh.staging_bytes)
        .sum();
    check_bytes(
        state,
        fine_retained
            .checked_add(fine_resident_new)
            .ok_or("Terrain HLOD fine byte count overflow")?,
        fine_new,
        retained_bytes(distant, &expected) + distant_new,
        distant_staging,
    )
}

pub(super) fn apply(
    world: &mut World,
    state: &mut super::State,
    prepared: Prepared,
    staging_bytes: u64,
) {
    let Some(distant) = state.hlod.as_mut() else {
        return;
    };
    let retire: Vec<_> = distant
        .live
        .keys()
        .filter(|key| {
            !prepared.request.layout.tiles().contains_key(key)
                || prepared.request.acquire.contains_key(key)
        })
        .copied()
        .collect();
    for key in retire {
        if let Some(live) = distant.live.remove(&key) {
            world.despawn(live.cell.entity);
            world
                .resource_mut::<Assets<Mesh>>()
                .remove(live.cell.mesh.id());
        }
    }
    let area = state.source.area_id();
    for (key, payload) in prepared.meshes {
        let tile = prepared.request.acquire[&key];
        let logical_id = key.entity_id(area);
        let mesh = world.resource_mut::<Assets<Mesh>>().add(payload.mesh);
        let mut entity = world.spawn((
            Mesh3d(mesh.clone()),
            Transform::IDENTITY,
            StreamedTerrainHlod {
                logical_id,
                tile,
                source: prepared.request.source,
            },
        ));
        match &state.material {
            CellMaterial::Uniform(material) => {
                entity.insert(MeshMaterial3d(material.clone()));
            }
            CellMaterial::Authored(material) => {
                entity.insert(MeshMaterial3d(material.clone()));
            }
        }
        distant.live.insert(
            key,
            DistantCell {
                tile,
                source: prepared.request.source,
                stride: prepared.request.stride,
                cell: LiveCell {
                    entity: entity.id(),
                    logical_id,
                    mesh,
                    collider: None,
                    collider_entity: None,
                    collider_transform: None,
                    collider_marker: None,
                    triangles: payload.triangles,
                    bytes: payload.bytes,
                },
            },
        );
    }
    let mut metrics = world.resource_mut::<TerrainHlodStats>();
    metrics.tiles = distant.live.len();
    metrics.aggregated_tiles = distant.live.keys().filter(|key| key.level > 0).count();
    metrics.maximum_level = distant.live.keys().map(|key| key.level).max().unwrap_or(0);
    metrics.covered_base_cells = prepared.request.layout.covered_cells();
    metrics.cpu_mesh_bytes = distant.live.values().map(|live| live.cell.bytes).sum();
    metrics.resident_triangles = distant.live.values().map(|live| live.cell.triangles).sum();
    metrics.peak_staging_mesh_bytes = metrics.peak_staging_mesh_bytes.max(staging_bytes);
    distant.layout = Some(prepared.request.layout);
}
