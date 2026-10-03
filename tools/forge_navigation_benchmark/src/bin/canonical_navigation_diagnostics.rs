//! Bounded diagnostics over the canonical terrain probe.
//!
//! This binary deliberately keeps obstacle insertion and band rebuilds outside
//! the product runtime. The selected provider versions expose full navmesh
//! regeneration and manual Landmass animation links, but no runtime obstacle
//! carving or native tile-cache seam API. The report records those limits
//! instead of treating synthetic diagnostics as a Gate 3 qualification.
#![deny(unsafe_code)]

use bevy_math::{UVec3, Vec3, Vec3A};
use bevy_rerecast::rerecast::{AreaType, TriMesh};
use forge_core::canonical_json_sha256;
use serde::Deserialize;
use serde_json::{Value, json};
use std::{error::Error, fs, io::Write, path::Path, time::Instant};

const AGENT_RADIUS_M: f32 = 0.35;
const AGENT_HEIGHT_M: f32 = 1.8;
const REPRODUCIBILITY_RUNS: usize = 3;
const DETOUR_EPSILON_M: f32 = 0.5;

#[path = "../navigation.rs"]
#[allow(
    dead_code,
    reason = "the diagnostics exercise only the path and off-mesh-link portions of the shared laboratory probes"
)]
mod navigation;
#[path = "../provider.rs"]
mod provider;

#[derive(Debug, Deserialize)]
struct CanonicalReport {
    schema: String,
    version: u32,
    area_id: String,
    source_hash: String,
    terrain_hash: String,
    raster_hash: String,
    geometry_hash: String,
    crop_xz_m: [f64; 4],
    start_m: [f32; 3],
    end_m: [f32; 3],
    canonical_geometry: CanonicalGeometry,
    tiles: Vec<CanonicalTile>,
}

#[derive(Debug, Deserialize)]
struct CanonicalGeometry {
    positions_m: Vec<[f32; 3]>,
    triangles: Vec<[u32; 3]>,
    geometry_hash: String,
}

#[derive(Debug, Deserialize)]
struct CanonicalTile {
    index: usize,
    source_cell_bounds: [u16; 4],
    crop_xz_m: [f64; 4],
    start_m: [f32; 3],
    end_m: [f32; 3],
    geometry_hash: String,
    vertices: usize,
    triangles: usize,
    deterministic: bool,
    landmass_path_found: bool,
    runs: Vec<Value>,
}

#[derive(Clone, Debug)]
struct MeshData {
    positions: Vec<[f32; 3]>,
    triangles: Vec<[u32; 3]>,
    mesh: TriMesh,
}

#[allow(
    clippy::struct_field_names,
    reason = "the obstacle fixture names preserve explicit world-unit semantics in the report"
)]
#[derive(Clone, Debug)]
struct ObstacleBox {
    center_xz_m: [f32; 2],
    half_width_m: f32,
    half_depth_m: f32,
    bottom_y_m: f32,
    top_y_m: f32,
}

#[derive(Clone, Debug)]
struct StateRun {
    navmesh_hash: String,
    generation_ms: f64,
    path: navigation::PathProbe,
}

#[derive(Clone, Debug)]
struct StateProbe {
    runs: Vec<StateRun>,
}

type DetourSelection = (ObstacleBox, MeshData, StateRun, Vec<Value>);

impl StateProbe {
    fn deterministic(&self) -> bool {
        let Some(first) = self.runs.first() else {
            return false;
        };
        let fingerprint = path_fingerprint(&first.path);
        self.runs.iter().all(|run| {
            run.navmesh_hash == first.navmesh_hash
                && path_fingerprint(&run.path) == fingerprint
                && run.path.found == first.path.found
        })
    }

    fn first_path(&self) -> Option<&navigation::PathProbe> {
        self.runs.first().map(|run| &run.path)
    }

    fn hashes(&self) -> Vec<&str> {
        self.runs
            .iter()
            .map(|run| run.navmesh_hash.as_str())
            .collect()
    }
}

#[allow(
    clippy::too_many_lines,
    reason = "the diagnostic report keeps source, deterministic runs, obstacle, bands, seams, and selection evidence contiguous"
)]
fn main() -> Result<(), Box<dyn Error>> {
    let args: Vec<_> = std::env::args().skip(1).collect();
    if args.len() != 2 {
        return Err(
            "usage: canonical_navigation_diagnostics <canonical-terrain-probe.v2.json> <new-report.v3.json>"
                .into(),
        );
    }
    let input_path = Path::new(&args[0]);
    let output_path = Path::new(&args[1]);
    let input: CanonicalReport = serde_json::from_str(&fs::read_to_string(input_path)?)?;
    validate_input(&input)?;

    let base = mesh_from_geometry(&input.canonical_geometry);
    let start = Vec3::from_array(input.start_m);
    let end = Vec3::from_array(input.end_m);
    let baseline = measure_state(&base.mesh, start, end)?;
    let baseline_path = baseline
        .first_path()
        .ok_or("baseline probe produced no run")?;
    if !baseline_path.found {
        return Err("baseline probe did not find a Landmass path".into());
    }

    let obstacle = choose_obstacle(&base, baseline_path)?;
    let inserted_mesh = append_obstacle(&base, &obstacle)?;
    let candidate = measure_one(&inserted_mesh.mesh, start, end);
    let (selected_obstacle, selected_mesh, _selected_candidate, candidate_attempts) =
        select_detour_candidate(&base, &obstacle, start, end, &inserted_mesh, &candidate)?;
    let inserted = measure_state(&selected_mesh.mesh, start, end)?;
    let removed = measure_state(&base.mesh, start, end)?;
    let seam_probe = navigation::off_mesh_link_probe()?;
    let band_diagnostics = measure_bands(&input, &base)?;
    let base_geometry_hash = mesh_hash(&base.positions, &base.triangles)?;
    let inserted_geometry_hash = mesh_hash(&selected_mesh.positions, &selected_mesh.triangles)?;
    let baseline_length = baseline_path_length(&baseline)?;
    let inserted_length = inserted.first_path().map(path_length).unwrap_or_default();
    let detour_proven = inserted
        .first_path()
        .is_some_and(|path| path.found && inserted_length > baseline_length + DETOUR_EPSILON_M);
    let removed_matches_baseline = same_state_result(&baseline, &removed);

    let report = json!({
        "schema": "forge.navigation.canonical-terrain-diagnostics",
        "version": 3,
        "input_report": input_path.display().to_string(),
        "area_id": input.area_id,
        "source_hash": input.source_hash,
        "terrain_hash": input.terrain_hash,
        "raster_hash": input.raster_hash,
        "canonical_geometry_hash": input.geometry_hash,
        "canonical_geometry_counts": {
            "vertices": input.canonical_geometry.positions_m.len(),
            "triangles": input.canonical_geometry.triangles.len(),
            "crop_xz_m": input.crop_xz_m,
        },
        "provider": {
            "rerecast": "0.4.0",
            "bevy_rerecast": "0.5.0",
            "landmass": "0.9.2",
            "landmass_rerecast": "0.3.0",
            "agent_radius_m": AGENT_RADIUS_M,
            "agent_height_m": AGENT_HEIGHT_M,
        },
        "deterministic_hashes": {
            "baseline": state_json(&baseline),
            "inserted_obstacle": state_json(&inserted),
            "removed_obstacle": state_json(&removed),
            "baseline_deterministic": baseline.deterministic(),
            "inserted_deterministic": inserted.deterministic(),
            "removed_deterministic": removed.deterministic(),
            "removed_matches_baseline": removed_matches_baseline,
            "base_geometry_hash": base_geometry_hash,
            "inserted_geometry_hash": inserted_geometry_hash,
        },
        "dynamic_obstacle_diagnostic": {
            "status": if detour_proven { "detour_observed" } else { "detour_not_proven" },
            "provider_runtime_carving_supported": false,
            "method": "append_exact_box_to_the_same_canonical_triangle_mesh_then_full_rerecast_regeneration",
            "source_geometry_preserved": true,
            "candidate_attempts": candidate_attempts,
            "selected_obstacle": obstacle_json(&selected_obstacle),
            "selected_obstacle_geometry_hash": inserted_geometry_hash,
            "baseline_path_length_m": baseline_length,
            "inserted_path_length_m": inserted_length,
            "detour_epsilon_m": DETOUR_EPSILON_M,
            "insertion": {
                "path_found": inserted.first_path().is_some_and(|path| path.found),
                "detour_proven": detour_proven,
            },
            "removal": {
                "path_found": removed.first_path().is_some_and(|path| path.found),
                "restored_baseline_hash_and_path": removed_matches_baseline,
            },
            "qualification": "diagnostic_only_full_rebuild; no native runtime obstacle API",
        },
        "bounded_band_diagnostics": {
            "status": "independent_band_rebuilds_only",
            "native_partial_rebuild_supported": false,
            "three_band_input_count": input.tiles.len(),
            "bands": band_diagnostics,
            "qualification": "bands_are_not_native_tiles_and_do_not_prove_tile_cache_or_seam_reconstruction",
        },
        "landmass_seam_link_diagnostic": {
            "status": "manual_animation_link_api_only",
            "without_link_path_found": seam_probe.0.found,
            "with_link_path_found": seam_probe.1.found,
            "with_link_animation_link_count": seam_probe.1.animation_link_count,
            "native_tile_seam_import_supported": false,
            "qualification": "synthetic_disconnected_platforms; not a canonical terrain seam",
        },
        "provider_api_limits": {
            "rerecast_tiling": "configuration field exists; selected crate exposes no tile asset or tile update API",
            "bevy_regeneration": "NavmeshGenerator::regenerate queues a full navmesh regeneration",
            "partial_rebuild": "not exposed by bevy_rerecast 0.5.0",
            "dynamic_obstacles": "no runtime carving or obstacle mutation API in selected provider stack",
            "landmass_seams": "manual AnimationLink is public; canonical tile seam governance is absent",
        },
        "selection": { "status": "blocked_pending_full_world_contract" },
        "no_go": true,
        "missing_proofs": [
            "native tile cache and bounded partial reconstruction",
            "canonical cross-tile Landmass seam links",
            "runtime dynamic obstacle insertion and removal API",
            "static colliders and water exclusions",
            "multi-island seams and NPC traversal",
            "real browser execution and human acceptance",
            "dependency governance and explicit ADR acceptance",
        ],
    });

    let mut output = fs::OpenOptions::new()
        .write(true)
        .create_new(true)
        .open(output_path)?;
    serde_json::to_writer_pretty(&mut output, &report)?;
    output.write_all(b"\n")?;
    output.sync_all()?;
    Ok(())
}

fn validate_input(input: &CanonicalReport) -> Result<(), Box<dyn Error>> {
    if input.schema != "forge.navigation.canonical-terrain-probe" || input.version != 2 {
        return Err("diagnostics require a canonical terrain probe v2 report".into());
    }
    let hash = mesh_hash(
        &input.canonical_geometry.positions_m,
        &input.canonical_geometry.triangles,
    )?;
    if hash != input.geometry_hash || hash != input.canonical_geometry.geometry_hash {
        return Err("canonical geometry hash does not match the v2 report".into());
    }
    if input.canonical_geometry.positions_m.is_empty()
        || input.canonical_geometry.triangles.is_empty()
    {
        return Err("canonical geometry is empty".into());
    }
    if input
        .canonical_geometry
        .positions_m
        .iter()
        .flatten()
        .any(|value| !value.is_finite())
        || input
            .canonical_geometry
            .triangles
            .iter()
            .flatten()
            .any(|index| {
                usize::try_from(*index).map_or(true, |index| {
                    index >= input.canonical_geometry.positions_m.len()
                })
            })
    {
        return Err("canonical geometry contains invalid finite values or indices".into());
    }
    if input.tiles.len() != 3 {
        return Err("v3 diagnostics require the three v2 x bands".into());
    }
    Ok(())
}

fn mesh_from_geometry(geometry: &CanonicalGeometry) -> MeshData {
    let mesh = TriMesh {
        vertices: geometry
            .positions_m
            .iter()
            .copied()
            .map(Vec3A::from_array)
            .collect(),
        indices: geometry
            .triangles
            .iter()
            .copied()
            .map(UVec3::from_array)
            .collect(),
        area_types: vec![AreaType::NOT_WALKABLE; geometry.triangles.len()],
    };
    MeshData {
        positions: geometry.positions_m.clone(),
        triangles: geometry.triangles.clone(),
        mesh,
    }
}

fn append_obstacle(base: &MeshData, obstacle: &ObstacleBox) -> Result<MeshData, Box<dyn Error>> {
    if !obstacle.bottom_y_m.is_finite()
        || !obstacle.top_y_m.is_finite()
        || obstacle.bottom_y_m >= obstacle.top_y_m
        || obstacle.half_width_m <= 0.0
        || obstacle.half_depth_m <= 0.0
    {
        return Err("dynamic obstacle dimensions are invalid".into());
    }
    let [center_x, center_z] = obstacle.center_xz_m;
    let [half_width, half_depth] = [obstacle.half_width_m, obstacle.half_depth_m];
    let vertices = [
        [
            center_x - half_width,
            obstacle.bottom_y_m,
            center_z - half_depth,
        ],
        [
            center_x + half_width,
            obstacle.bottom_y_m,
            center_z - half_depth,
        ],
        [
            center_x + half_width,
            obstacle.bottom_y_m,
            center_z + half_depth,
        ],
        [
            center_x - half_width,
            obstacle.bottom_y_m,
            center_z + half_depth,
        ],
        [
            center_x - half_width,
            obstacle.top_y_m,
            center_z - half_depth,
        ],
        [
            center_x + half_width,
            obstacle.top_y_m,
            center_z - half_depth,
        ],
        [
            center_x + half_width,
            obstacle.top_y_m,
            center_z + half_depth,
        ],
        [
            center_x - half_width,
            obstacle.top_y_m,
            center_z + half_depth,
        ],
    ];
    let base_index = u32::try_from(base.positions.len())?;
    let triangles = [
        [0, 2, 1],
        [0, 3, 2],
        [4, 5, 6],
        [4, 6, 7],
        [0, 1, 5],
        [0, 5, 4],
        [1, 2, 6],
        [1, 6, 5],
        [2, 3, 7],
        [2, 7, 6],
        [3, 0, 4],
        [3, 4, 7],
    ]
    .map(|[a, b, c]| [base_index + a, base_index + b, base_index + c]);
    let mut positions = base.positions.clone();
    positions.extend(vertices);
    let mut indices = base.triangles.clone();
    indices.extend(triangles);
    let mesh = TriMesh {
        vertices: positions.iter().copied().map(Vec3A::from_array).collect(),
        indices: indices.iter().copied().map(UVec3::from_array).collect(),
        area_types: vec![AreaType::NOT_WALKABLE; indices.len()],
    };
    Ok(MeshData {
        positions,
        triangles: indices,
        mesh,
    })
}

fn choose_obstacle(
    base: &MeshData,
    path: &navigation::PathProbe,
) -> Result<ObstacleBox, Box<dyn Error>> {
    let segment = path
        .waypoints_m
        .windows(2)
        .max_by(|left, right| {
            segment_length(left)
                .partial_cmp(&segment_length(right))
                .unwrap_or(std::cmp::Ordering::Equal)
        })
        .ok_or("baseline path has no segment")?;
    let midpoint = [
        f32::midpoint(segment[0][0], segment[1][0]),
        f32::midpoint(segment[0][2], segment[1][2]),
    ];
    let (minimum_y, maximum_y) = base.positions.iter().map(|point| point[1]).fold(
        (f32::INFINITY, f32::NEG_INFINITY),
        |(minimum, maximum), point| (minimum.min(point), maximum.max(point)),
    );
    if !minimum_y.is_finite() || !maximum_y.is_finite() {
        return Err("canonical geometry has no finite height range".into());
    }
    Ok(ObstacleBox {
        center_xz_m: midpoint,
        half_width_m: 8.0,
        half_depth_m: 1.5,
        bottom_y_m: minimum_y - 2.0,
        top_y_m: maximum_y + 4.0,
    })
}

fn select_detour_candidate(
    base: &MeshData,
    initial: &ObstacleBox,
    start: Vec3,
    end: Vec3,
    initial_mesh: &MeshData,
    initial_candidate: &Result<StateRun, String>,
) -> Result<DetourSelection, Box<dyn Error>> {
    let mut attempts = Vec::new();
    let mut candidates = vec![initial.clone()];
    for half_width_m in [12.0_f32, 16.0, 20.0] {
        let mut candidate = initial.clone();
        candidate.half_width_m = half_width_m;
        candidates.push(candidate);
    }
    for (index, candidate) in candidates.into_iter().enumerate() {
        let mesh = if index == 0 {
            initial_mesh.clone()
        } else {
            append_obstacle(base, &candidate)?
        };
        let result = if index == 0 {
            initial_candidate.clone()
        } else {
            measure_one(&mesh.mesh, start, end)
        };
        match result {
            Ok(run) => {
                let path_length_m = path_length(&run.path);
                let path_found = run.path.found;
                attempts.push(json!({
                    "index": index,
                    "half_width_m": candidate.half_width_m,
                    "path_found": path_found,
                    "path_length_m": path_length_m,
                    "navmesh_hash": run.navmesh_hash,
                    "error": Value::Null,
                }));
                if path_found {
                    let baseline = measure_one(&base.mesh, start, end)?;
                    if path_length_m > path_length(&baseline.path) + DETOUR_EPSILON_M {
                        return Ok((candidate, mesh, run, attempts));
                    }
                }
            }
            Err(error) => attempts.push(json!({
                "index": index,
                "half_width_m": candidate.half_width_m,
                "path_found": false,
                "path_length_m": 0.0,
                "navmesh_hash": Value::Null,
                "error": error,
            })),
        }
    }
    let fallback = attempts
        .last()
        .and_then(|_| candidates_fallback(base, initial, start, end))
        .ok_or("no obstacle candidate produced a measurable regeneration")?;
    Ok((fallback.0, fallback.1, fallback.2, attempts))
}

fn candidates_fallback(
    base: &MeshData,
    initial: &ObstacleBox,
    start: Vec3,
    end: Vec3,
) -> Option<(ObstacleBox, MeshData, StateRun)> {
    let mut candidate = initial.clone();
    candidate.half_width_m = 20.0;
    let mesh = append_obstacle(base, &candidate).ok()?;
    let run = measure_one(&mesh.mesh, start, end).ok()?;
    Some((candidate, mesh, run))
}

fn measure_state(mesh: &TriMesh, start: Vec3, end: Vec3) -> Result<StateProbe, Box<dyn Error>> {
    let mut runs = Vec::with_capacity(REPRODUCIBILITY_RUNS);
    for _ in 0..REPRODUCIBILITY_RUNS {
        runs.push(measure_one(mesh, start, end).map_err(std::io::Error::other)?);
    }
    Ok(StateProbe { runs })
}

fn measure_one(mesh: &TriMesh, start: Vec3, end: Vec3) -> Result<StateRun, String> {
    let timer = Instant::now();
    let navmesh = provider::generate_navmesh(
        mesh.clone(),
        provider::settings_for_agent(AGENT_RADIUS_M, AGENT_HEIGHT_M),
    )
    .map_err(|error| error.to_string())?;
    let generation_ms = timer.elapsed().as_secs_f64() * 1000.0;
    let navmesh_hash = canonical_json_sha256(&navmesh)
        .map_err(|error| error.to_string())?
        .to_string();
    let path = navigation::query_rerecast_path(&navmesh, start, end, AGENT_RADIUS_M)
        .map_err(|error| error.to_string())?;
    Ok(StateRun {
        navmesh_hash,
        generation_ms,
        path,
    })
}

fn state_json(state: &StateProbe) -> Value {
    json!({
        "deterministic": state.deterministic(),
        "runs": state.runs.iter().map(|run| json!({
            "navmesh_hash": run.navmesh_hash,
            "generation_ms": run.generation_ms,
            "path": run.path,
        })).collect::<Vec<_>>(),
        "navmesh_hashes": state.hashes(),
    })
}

fn same_state_result(left: &StateProbe, right: &StateProbe) -> bool {
    left.runs.len() == right.runs.len()
        && left.runs.iter().zip(&right.runs).all(|(left, right)| {
            left.navmesh_hash == right.navmesh_hash
                && path_fingerprint(&left.path) == path_fingerprint(&right.path)
        })
}

fn path_fingerprint(path: &navigation::PathProbe) -> String {
    serde_json::to_string(path).unwrap_or_else(|_| "<path-serialization-error>".to_owned())
}

fn path_length(path: &navigation::PathProbe) -> f32 {
    path.waypoints_m.windows(2).map(segment_length).sum()
}

fn baseline_path_length(state: &StateProbe) -> Result<f32, Box<dyn Error>> {
    let path = state
        .first_path()
        .ok_or("baseline probe produced no path run")?;
    Ok(path_length(path))
}

fn segment_length(segment: &[[f32; 3]]) -> f32 {
    let [left, right] = segment else {
        return 0.0;
    };
    Vec3::from_array(*left).distance(Vec3::from_array(*right))
}

fn mesh_hash(positions: &[[f32; 3]], triangles: &[[u32; 3]]) -> Result<String, Box<dyn Error>> {
    Ok(canonical_json_sha256(&(positions, triangles))?.to_string())
}

fn obstacle_json(obstacle: &ObstacleBox) -> Value {
    json!({
        "center_xz_m": obstacle.center_xz_m,
        "half_width_m": obstacle.half_width_m,
        "half_depth_m": obstacle.half_depth_m,
        "bottom_y_m": obstacle.bottom_y_m,
        "top_y_m": obstacle.top_y_m,
    })
}

fn measure_bands(input: &CanonicalReport, base: &MeshData) -> Result<Vec<Value>, Box<dyn Error>> {
    let mut bands = Vec::with_capacity(input.tiles.len());
    for tile in &input.tiles {
        let band = crop_mesh(base, tile.crop_xz_m)?;
        let geometry_hash = mesh_hash(&band.positions, &band.triangles)?;
        let measured = measure_state(
            &band.mesh,
            Vec3::from_array(tile.start_m),
            Vec3::from_array(tile.end_m),
        );
        let measurement = match measured {
            Ok(state) => json!({
                "status": "regenerated_full_band_mesh",
                "deterministic": state.deterministic(),
                "navmesh_hashes": state.hashes(),
                "landmass_path_found": state.first_path().is_some_and(|path| path.found),
                "runs": state_json(&state)["runs"].clone(),
            }),
            Err(error) => json!({
                "status": "band_regeneration_error",
                "error": error.to_string(),
            }),
        };
        bands.push(json!({
            "index": tile.index,
            "source_cell_bounds": tile.source_cell_bounds,
            "crop_xz_m": tile.crop_xz_m,
            "source_report_geometry_hash": tile.geometry_hash,
            "source_report_vertices": tile.vertices,
            "source_report_triangles": tile.triangles,
            "source_report_deterministic": tile.deterministic,
            "source_report_landmass_path_found": tile.landmass_path_found,
            "source_report_run_count": tile.runs.len(),
            "diagnostic_geometry_hash": geometry_hash,
            "diagnostic_vertices": band.positions.len(),
            "diagnostic_triangles": band.triangles.len(),
            "measurement": measurement,
        }));
    }
    Ok(bands)
}

fn crop_mesh(base: &MeshData, bounds: [f64; 4]) -> Result<MeshData, Box<dyn Error>> {
    let mut positions = Vec::new();
    let mut triangles = Vec::new();
    for triangle in &base.triangles {
        let source = triangle
            .iter()
            .map(|index| {
                let index = usize::try_from(*index).map_err(std::io::Error::other)?;
                base.positions
                    .get(index)
                    .copied()
                    .ok_or_else(|| std::io::Error::other("band triangle index out of range"))
            })
            .collect::<Result<Vec<_>, std::io::Error>>()?;
        let x_min = source
            .iter()
            .map(|point| f64::from(point[0]))
            .fold(f64::INFINITY, f64::min);
        let x_max = source
            .iter()
            .map(|point| f64::from(point[0]))
            .fold(f64::NEG_INFINITY, f64::max);
        let z_min = source
            .iter()
            .map(|point| f64::from(point[2]))
            .fold(f64::INFINITY, f64::min);
        let z_max = source
            .iter()
            .map(|point| f64::from(point[2]))
            .fold(f64::NEG_INFINITY, f64::max);
        if x_max < bounds[0] || x_min > bounds[1] || z_max < bounds[2] || z_min > bounds[3] {
            continue;
        }
        let base_index = u32::try_from(positions.len())?;
        positions.extend(source);
        triangles.push([base_index, base_index + 1, base_index + 2]);
    }
    if triangles.is_empty() {
        return Err("band crop contains no canonical triangles".into());
    }
    let mesh = TriMesh {
        vertices: positions.iter().copied().map(Vec3A::from_array).collect(),
        indices: triangles.iter().copied().map(UVec3::from_array).collect(),
        area_types: vec![AreaType::NOT_WALKABLE; triangles.len()],
    };
    Ok(MeshData {
        positions,
        triangles,
        mesh,
    })
}
