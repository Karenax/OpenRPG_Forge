//! Read-only first provider probe over authenticated canonical raster geometry.
//! It deliberately reports the missing full-world contract instead of selecting a supplier.
#![deny(unsafe_code)]
use bevy_math::{UVec3, Vec3, Vec3A};
use bevy_rerecast::rerecast::{AreaType, TriMesh};
use forge_core::{AreaId, canonical_json_sha256};
use forge_project::authoring::{
    AuthoringRequest, host::AuthoringHost, terrain_source::TerrainSourceAssembly,
    workspace::WorkspaceHostRequest,
};
use forge_world::sampled_terrain::SampledTerrainPlan;
use std::{error::Error, fs::OpenOptions, io::Write, path::Path, time::Instant};

const AGENT_RADIUS_M: f32 = 0.35;
const AGENT_HEIGHT_M: f32 = 1.8;
const REPRODUCIBILITY_RUNS: usize = 3;

type GeometryOutput = (TriMesh, Vec<[f32; 3]>, Vec<[u32; 3]>);
type CellBounds = ((u16, u16), (u16, u16));

#[path = "../navigation.rs"]
#[allow(
    dead_code,
    reason = "The shared laboratory exposes other independent probes"
)]
mod navigation;
#[path = "../provider.rs"]
mod provider;

fn main() -> Result<(), Box<dyn Error>> {
    let args: Vec<_> = std::env::args().skip(1).collect();
    if args.len() != 11 {
        return Err("usage: canonical_terrain_probe <authoring-root> <area-id> <min-x> <max-x> <min-z> <max-z> <start-x> <start-z> <end-x> <end-z> <new-report.json>".into());
    }
    let values: Vec<f64> = args[2..10]
        .iter()
        .map(|v| v.parse())
        .collect::<Result<_, _>>()?;
    if values.iter().any(|v| !v.is_finite()) || values[0] >= values[1] || values[2] >= values[3] {
        return Err("invalid finite crop bounds".into());
    }
    if [values[4], values[6]]
        .iter()
        .any(|x| *x < values[0] || *x > values[1])
        || [values[5], values[7]]
            .iter()
            .any(|z| *z < values[2] || *z > values[3])
    {
        return Err("path endpoints must be inside the requested crop".into());
    }
    if (values[4] - values[6]).abs() < f64::EPSILON && (values[5] - values[7]).abs() < f64::EPSILON
    {
        return Err("path endpoints must be distinct".into());
    }
    let (source_hash, document, plan) = {
        let mut host = AuthoringHost::new(Path::new(&args[0]))?;
        host.execute_workspace(WorkspaceHostRequest::Open)?;
        let state = host.execute(AuthoringRequest::OpenManaged {
            area_id: args[1].parse::<AreaId>()?,
        })?;
        let head = state.zone_head.ok_or("managed zone has no head")?;
        let document = state.document.ok_or("zone has no terrain")?;
        let mut assembly = TerrainSourceAssembly::new(&document)?;
        let terrain_hash = document.content_hash()?;
        while let Some(offset) = assembly.next_offset() {
            assembly.accept(host.read_terrain_source(terrain_hash, offset)?)?;
        }
        let plan = SampledTerrainPlan::from_document(&document, &assembly.finish()?)?;
        (head.content_hash()?, document, plan)
    };
    let terrain_hash = document.content_hash()?;
    let raster_hash = document
        .sampled_source
        .as_ref()
        .ok_or("managed zone has no sampled source")?
        .samples_sha256;
    let (mesh, positions, triangles) = geometry(&plan, &values[..4])?;
    let mesh_hash = canonical_json_sha256(&(&positions, &triangles))?;
    let geometry_min: [f32; 3] = std::array::from_fn(|axis| {
        positions
            .iter()
            .map(|p| p[axis])
            .fold(f32::INFINITY, f32::min)
    });
    let geometry_max: [f32; 3] = std::array::from_fn(|axis| {
        positions
            .iter()
            .map(|p| p[axis])
            .fold(f32::NEG_INFINITY, f32::max)
    });
    let start = point(&plan, values[4], values[5])?;
    let end = point(&plan, values[6], values[7])?;
    let runs = measure_runs(&mesh, start, end)?;
    let deterministic = runs_are_deterministic(&runs);
    let tiles = measure_tiles(&plan, &values[..4])?;
    let report = serde_json::json!({
        "schema": "forge.navigation.canonical-terrain-probe", "version": 2,
        "area_id": document.area_id, "source_hash": source_hash,
        "terrain_hash": terrain_hash, "raster_hash": raster_hash,
        "geometry_hash": mesh_hash, "crop_xz_m": values[..4], "sampling_stride": 1,
        "geometry_envelope_m": {"min": geometry_min, "max": geometry_max},
        "crop_policy": "conservative triangle overlap; path may use the reported envelope",
        "source_hash_kind": "ZoneAuthoringHead", "deterministic": deterministic,
        "provider": {"rerecast":"0.4.0", "bevy_rerecast":"0.5.0", "landmass":"0.9.2", "agent_radius_m":AGENT_RADIUS_M,"agent_height_m":AGENT_HEIGHT_M,"max_simplification_error":provider::CONSERVATIVE_MAX_SIMPLIFICATION_ERROR,"parameter_profile":"uniform_conservative_v1"},
        "vertices": positions.len(), "triangles": triangles.len(),
        "canonical_geometry": {
            "positions_m": positions, "triangles": triangles,
            "geometry_hash": mesh_hash, "source_hash": source_hash,
            "raster_hash": raster_hash, "terrain_hash": terrain_hash,
            "crop_xz_m": values[..4]
        },
        "start_m": start.to_array(), "end_m": end.to_array(), "runs": runs,
        "tile_policy": "up to three contiguous x strips on the canonical sample lattice; each strip is regenerated independently",
        "tile_count": tiles.len(), "tiles": tiles,
        "selection": {"status": "blocked_pending_full_world_contract"},
        "missing_proofs": ["cross-tile Landmass links", "native tile cache and partial reconstruction", "static colliders and water exclusions", "multi-island seams", "dynamic obstacles", "real browser execution", "dependency governance and ADR acceptance"]
    });
    let mut output = OpenOptions::new()
        .write(true)
        .create_new(true)
        .open(&args[10])?;
    output.write_all(&serde_json::to_vec_pretty(&report)?)?;
    output.write_all(b"\n")?;
    output.sync_all()?;
    Ok(())
}

#[allow(clippy::cast_possible_truncation, clippy::cast_sign_loss)]
fn geometry(plan: &SampledTerrainPlan, bounds: &[f64]) -> Result<GeometryOutput, Box<dyn Error>> {
    let ((min_x, max_x), (min_z, max_z)) = crop_cells(plan, bounds)?;
    if u32::from(max_x - min_x) * u32::from(max_z - min_z) > 65_536 {
        return Err(
            "crop exceeds the 65536 unit-cell memory budget; partition it explicitly".into(),
        );
    }
    let mut positions = Vec::new();
    let mut triangles = Vec::new();
    for z in (min_z..max_z).step_by(128) {
        for x in (min_x..max_x).step_by(128) {
            let patch =
                plan.build_patch([x, z], [(max_x - x).min(128), (max_z - z).min(128)], 1)?;
            let (indices, remainder) = patch.indices.as_chunks::<3>();
            if !remainder.is_empty() {
                return Err("patch indices are not a multiple of three".into());
            }
            for indices in indices {
                let mut vertices = [[0.0_f32; 3]; 3];
                for (slot, index) in indices.iter().enumerate() {
                    let index = usize::try_from(*index)?;
                    let vertex = vertices
                        .get_mut(slot)
                        .ok_or("patch triangle has too many vertices")?;
                    *vertex = *patch
                        .positions_m
                        .get(index)
                        .ok_or("patch triangle index out of range")?;
                }
                // Keep crossing triangles too: centroid-only clipping loses corridor edges.
                if [0, 2].iter().enumerate().any(|(axis, component)| {
                    let min = vertices
                        .iter()
                        .map(|p| f64::from(p[*component]))
                        .fold(f64::INFINITY, f64::min);
                    let max = vertices
                        .iter()
                        .map(|p| f64::from(p[*component]))
                        .fold(f64::NEG_INFINITY, f64::max);
                    max < bounds[axis * 2] || min > bounds[axis * 2 + 1]
                }) {
                    continue;
                }
                let base = u32::try_from(positions.len())?;
                positions.extend(vertices);
                triangles.push([base, base + 1, base + 2]);
            }
        }
    }
    if triangles.is_empty() {
        return Err("crop contains no terrain triangles".into());
    }
    if positions.iter().flatten().any(|value| !value.is_finite()) {
        return Err("canonical patch exceeds finite float32 geometry".into());
    }
    let mesh = TriMesh {
        vertices: positions.iter().copied().map(Vec3A::from_array).collect(),
        indices: triangles.iter().copied().map(UVec3::from_array).collect(),
        area_types: vec![AreaType::NOT_WALKABLE; triangles.len()],
    };
    Ok((mesh, positions, triangles))
}

#[allow(
    clippy::cast_possible_truncation,
    clippy::cast_sign_loss,
    reason = "finite cell bounds are checked against the validated u16 raster domain"
)]
fn axis_cells(
    minimum: f64,
    maximum: f64,
    origin: f64,
    spacing: f64,
    limit: u32,
) -> Result<(u16, u16), Box<dyn Error>> {
    if !minimum.is_finite()
        || !maximum.is_finite()
        || !origin.is_finite()
        || !spacing.is_finite()
        || spacing <= 0.0
        || minimum >= maximum
    {
        return Err("invalid finite crop axis".into());
    }
    let min = ((minimum - origin) / spacing).floor();
    let max = ((maximum - origin) / spacing).ceil();
    if !min.is_finite()
        || !max.is_finite()
        || min < 1.0
        || max > f64::from(limit.saturating_sub(1))
        || max > f64::from(u16::MAX)
        || min >= max
    {
        return Err("crop leaves canonical raster domain".into());
    }
    Ok((u16::try_from(min as u32)?, u16::try_from(max as u32)?))
}

fn crop_cells(plan: &SampledTerrainPlan, bounds: &[f64]) -> Result<CellBounds, Box<dyn Error>> {
    if bounds.len() != 4 {
        return Err("crop requires min-x, max-x, min-z, max-z".into());
    }
    let source = plan
        .document()
        .sampled_source
        .as_ref()
        .ok_or("missing raster")?;
    Ok((
        axis_cells(
            bounds[0],
            bounds[1],
            source.origin_xz_m[0],
            source.sample_spacing_m,
            source.width,
        )?,
        axis_cells(
            bounds[2],
            bounds[3],
            source.origin_xz_m[1],
            source.sample_spacing_m,
            source.height,
        )?,
    ))
}

fn measure_runs(
    mesh: &TriMesh,
    start: Vec3,
    end: Vec3,
) -> Result<Vec<serde_json::Value>, Box<dyn Error>> {
    let mut runs = Vec::with_capacity(REPRODUCIBILITY_RUNS);
    for _ in 0..REPRODUCIBILITY_RUNS {
        let timer = Instant::now();
        let navmesh = provider::generate_navmesh(
            mesh.clone(),
            provider::settings_for_agent(AGENT_RADIUS_M, AGENT_HEIGHT_M),
        )?;
        let generation_ms = timer.elapsed().as_secs_f64() * 1000.0;
        let navmesh_hash = canonical_json_sha256(&navmesh)?;
        let path = navigation::query_rerecast_path(&navmesh, start, end, AGENT_RADIUS_M)
            .map_err(|e| e.to_string());
        let landmass_path_found = path.as_ref().is_ok_and(|probe| probe.found);
        runs.push(serde_json::json!({
            "navmesh_hash": navmesh_hash,
            "generation_ms": generation_ms,
            "landmass_path_found": landmass_path_found,
            "path": path,
        }));
    }
    Ok(runs)
}

fn runs_are_deterministic(runs: &[serde_json::Value]) -> bool {
    runs.windows(2).all(|pair| {
        pair[0]["navmesh_hash"] == pair[1]["navmesh_hash"]
            && pair[0]["path"] == pair[1]["path"]
            && pair[0]["landmass_path_found"] == pair[1]["landmass_path_found"]
    })
}

fn measure_tiles(
    plan: &SampledTerrainPlan,
    bounds: &[f64],
) -> Result<Vec<serde_json::Value>, Box<dyn Error>> {
    let ((min_x, max_x), (min_z, max_z)) = crop_cells(plan, bounds)?;
    let source = plan
        .document()
        .sampled_source
        .as_ref()
        .ok_or("missing raster")?;
    let x_span = u32::from(max_x - min_x);
    let tile_count = if x_span >= 3 {
        3_usize
    } else if x_span >= 2 {
        2
    } else {
        1
    };
    let mut tiles = Vec::with_capacity(tile_count);
    for index in 0..tile_count {
        let tile_count_u32 = u32::try_from(tile_count)?;
        let start_offset = (x_span * u32::try_from(index)?).div_euclid(tile_count_u32);
        let end_offset = (x_span * u32::try_from(index + 1)?).div_euclid(tile_count_u32);
        let tile_min_x = min_x
            .checked_add(u16::try_from(start_offset)?)
            .ok_or("tile minimum exceeds u16 range")?;
        let tile_max_x = min_x
            .checked_add(u16::try_from(end_offset)?)
            .ok_or("tile maximum exceeds u16 range")?;
        if tile_min_x >= tile_max_x {
            return Err("tile partition produced an empty strip".into());
        }
        let tile_bounds = [
            source.origin_xz_m[0] + f64::from(tile_min_x) * source.sample_spacing_m,
            source.origin_xz_m[0] + f64::from(tile_max_x) * source.sample_spacing_m,
            source.origin_xz_m[1] + f64::from(min_z) * source.sample_spacing_m,
            source.origin_xz_m[1] + f64::from(max_z) * source.sample_spacing_m,
        ];
        let (mesh, positions, triangles) = geometry(plan, &tile_bounds)?;
        let geometry_hash = canonical_json_sha256(&(&positions, &triangles))?;
        let center_z = f64::midpoint(tile_bounds[2], tile_bounds[3]);
        let start = point(
            plan,
            tile_bounds[0] + (tile_bounds[1] - tile_bounds[0]) * 0.25,
            center_z,
        )?;
        let end = point(
            plan,
            tile_bounds[0] + (tile_bounds[1] - tile_bounds[0]) * 0.75,
            center_z,
        )?;
        let runs = measure_runs(&mesh, start, end)?;
        let deterministic = runs_are_deterministic(&runs);
        let landmass_path_found = runs.iter().all(|run| {
            run["landmass_path_found"]
                .as_bool()
                .is_some_and(|found| found)
        });
        tiles.push(serde_json::json!({
            "index": index,
            "source_cell_bounds": [tile_min_x, tile_max_x, min_z, max_z],
            "crop_xz_m": tile_bounds,
            "geometry_hash": geometry_hash,
            "vertices": positions.len(),
            "triangles": triangles.len(),
            "start_m": start.to_array(),
            "end_m": end.to_array(),
            "deterministic": deterministic,
            "landmass_path_found": landmass_path_found,
            "runs": runs,
        }));
    }
    Ok(tiles)
}

#[allow(clippy::cast_possible_truncation)]
fn point(plan: &SampledTerrainPlan, x: f64, z: f64) -> Result<Vec3, Box<dyn Error>> {
    let point = Vec3::new(x as f32, plan.height_at(x, z)? as f32, z as f32);
    if !point.is_finite() {
        return Err("endpoint exceeds finite float32 geometry".into());
    }
    Ok(point)
}
