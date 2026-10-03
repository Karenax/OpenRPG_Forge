//! Campaign 003: an adapter-owned, bounded tile/landmass experiment.
//!
//! This binary intentionally remains in the navigation laboratory. It uses the
//! public Rerecast configuration fields (`tiling` and `tile_size`) while
//! keeping the tile cache, border ownership, obstacle invalidation, and seam
//! links in this executable. The pinned provider does not expose a native tile
//! cache or partial regeneration API, so the report never promotes these
//! adapter tiles to a runtime qualification.
#![deny(unsafe_code)]
#![allow(
    clippy::too_many_lines,
    reason = "the campaign report keeps each bounded state, rejection, route, and hash adjacent for auditability"
)]

use bevy_landmass::{
    ArchipelagoOptions, FromAgentRadius, PathStep, PointSampleDistance3d, coords::ThreeD,
};
use bevy_math::{UVec3, Vec3, Vec3A};
use bevy_rerecast::{
    Navmesh,
    rerecast::{AreaType, TriMesh},
};
use forge_core::canonical_json_sha256;
use landmass::{
    AnimationLink, Archipelago, FindPathError, Island, PermittedAnimationLinks, Transform,
};
use landmass_rerecast::convert_rerecast_navmesh_to_landmass_navmesh;
use serde::{Deserialize, Serialize};
use serde_json::{Value, json};
use std::{
    collections::HashMap,
    error::Error,
    fs::{self, OpenOptions},
    io::Write,
    path::Path,
    sync::Arc,
    time::Instant,
};

#[path = "../navigation.rs"]
#[allow(
    dead_code,
    reason = "the campaign reuses the laboratory's canonical path probe types and provider helpers"
)]
mod navigation;
#[path = "../provider.rs"]
mod provider;

const AGENT_RADIUS_M: f32 = 0.35;
const AGENT_HEIGHT_M: f32 = 1.8;
const BORDER_M: f32 = 2.0;
const TILE_SIZE_CELLS: u16 = 160;
const REPRODUCIBILITY_RUNS: usize = 3;
const OBSTACLE_TILE: usize = 1;
const BOUNDARY_LINK_KIND: usize = 3003;

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
    crop_xz_m: [f64; 4],
}

#[allow(
    clippy::struct_field_names,
    reason = "the v3 obstacle schema is preserved verbatim for source and report comparison"
)]
#[derive(Clone, Debug, Deserialize, Serialize)]
struct ObstacleBox {
    center_xz_m: [f32; 2],
    half_width_m: f32,
    half_depth_m: f32,
    bottom_y_m: f32,
    top_y_m: f32,
}

#[derive(Clone, Debug)]
struct MeshData {
    positions: Vec<[f32; 3]>,
    triangles: Vec<[u32; 3]>,
    mesh: TriMesh,
}

#[derive(Clone, Debug)]
struct TileGeometry {
    index: usize,
    core_bounds_m: [f32; 4],
    expanded_bounds_m: [f32; 4],
    geometry: MeshData,
}

struct BuiltTile {
    index: usize,
    core_bounds_m: [f32; 4],
    navmesh: Navmesh,
}

#[derive(Debug, Serialize)]
struct TileRunReport {
    run: usize,
    status: String,
    navmesh_hash: Option<String>,
    generation_ms: f64,
    error: Option<String>,
}

#[derive(Debug, Serialize)]
struct TileReport {
    index: usize,
    core_bounds_m: [f32; 4],
    expanded_bounds_m: [f32; 4],
    geometry_hash: String,
    vertices: usize,
    triangles: usize,
    status: String,
    deterministic: bool,
    runs: Vec<TileRunReport>,
}

#[derive(Debug, Serialize)]
struct PathReport {
    status: String,
    found: bool,
    waypoint_count: usize,
    animation_link_count: usize,
    path_length_m: f32,
    waypoints_m: Vec<[f32; 3]>,
    crosses_adapter_boundaries: bool,
    error: Option<String>,
}

#[derive(Debug, Serialize)]
struct SeamLinkReport {
    boundary_x_m: f32,
    start_edge: [[f32; 3]; 2],
    end_edge: [[f32; 3]; 2],
    kind: usize,
    bidirectional: bool,
    status: String,
    collision: String,
}

#[derive(Debug, Serialize)]
struct RouteReport {
    route_id: String,
    village_to_forest_without_links: PathReport,
    forest_to_village_without_links: PathReport,
    village_to_forest: PathReport,
    forest_to_village: PathReport,
    seam_links: Vec<SeamLinkReport>,
}

#[derive(Debug, Serialize)]
struct StateReport {
    label: String,
    obstacle: Option<ObstacleBox>,
    changed_tile: Option<usize>,
    tile_reports: Vec<TileReport>,
    tile_hashes_deterministic: bool,
    routes: Option<RouteReport>,
}

#[derive(Debug, Serialize)]
struct CampaignReport {
    schema: &'static str,
    version: u32,
    input_report: String,
    obstacle_report: String,
    area_id: String,
    source_hash: String,
    terrain_hash: String,
    raster_hash: String,
    canonical_geometry_hash: String,
    canonical_geometry_counts: Value,
    adapter: Value,
    states: Vec<StateReport>,
    comparisons: Value,
    provider_api_limits: Value,
    fixture_limits: Value,
    dependency_governance: Value,
    selection: Value,
    no_go: bool,
    missing_proofs: Vec<String>,
}

fn main() -> Result<(), Box<dyn Error>> {
    let args: Vec<_> = std::env::args().skip(1).collect();
    if args.len() != 3 {
        return Err("usage: canonical_navigation_campaign <canonical-terrain-probe.v2.json> <canonical-navigation-diagnostics.v3.json> <new-report.v1.json>".into());
    }

    let input_path = Path::new(&args[0]);
    let obstacle_path = Path::new(&args[1]);
    let output_path = Path::new(&args[2]);
    let input: CanonicalReport = serde_json::from_str(&fs::read_to_string(input_path)?)?;
    let obstacle_report: Value = serde_json::from_str(&fs::read_to_string(obstacle_path)?)?;
    validate_input(&input)?;
    let obstacle = obstacle_from_v3(&obstacle_report)?;
    validate_obstacle(&obstacle, &input)?;

    let base = mesh_from_geometry(&input.canonical_geometry);
    let tile_geometry = make_tile_geometry(&base, &input)?;
    let before = build_state("before", &tile_geometry, None, input.start_m, input.end_m)?;
    let after = build_state(
        "after_obstacle",
        &tile_geometry,
        Some(&obstacle),
        input.start_m,
        input.end_m,
    )?;
    let removed = build_state(
        "after_removal",
        &tile_geometry,
        None,
        input.start_m,
        input.end_m,
    )?;

    let comparisons = compare_states(&before, &after, &removed);
    let campaign = CampaignReport {
        schema: "forge.navigation.canonical-navigation-campaign",
        version: 1,
        input_report: input_path.display().to_string(),
        obstacle_report: obstacle_path.display().to_string(),
        area_id: input.area_id.clone(),
        source_hash: input.source_hash.clone(),
        terrain_hash: input.terrain_hash.clone(),
        raster_hash: input.raster_hash.clone(),
        canonical_geometry_hash: input.geometry_hash.clone(),
        canonical_geometry_counts: json!({
            "vertices": input.canonical_geometry.positions_m.len(),
            "triangles": input.canonical_geometry.triangles.len(),
            "crop_xz_m": input.crop_xz_m,
        }),
        adapter: json!({
            "strategy": "forge_adapter_owned_bounded_tiles_with_explicit_border",
            "core_tile_count": tile_geometry.len(),
            "border_m": BORDER_M,
            "provider_tiling_config_used": true,
            "max_simplification_error": provider::CONSERVATIVE_MAX_SIMPLIFICATION_ERROR,
            "parameter_profile": "uniform_conservative_v1",
            "provider_tile_size_cells": TILE_SIZE_CELLS,
            "provider_cell_size_m": 0.175,
            "provider_border_size_cells": 5,
            "border_formula": "rerecast ConfigBuilder border_size = walkable_radius + 3; ceil(0.35 / 0.175) + 3",
            "native_tile_cache_supported": false,
            "native_partial_rebuild_supported": false,
            "tile_ownership": "adapter crops canonical triangles into three overlapping x bands; state changes rebuild only tile 1",
            "obstacle_update_scope": { "tile_index": OBSTACLE_TILE, "geometry_append": "canonical v3 selected box" },
            "source_geometry_mutated": false,
        }),
        states: vec![before.report, after.report, removed.report],
        comparisons,
        provider_api_limits: json!({
            "rerecast": "public ConfigBuilder tiling/tile_size fields are used; crate exposes no tile asset, tile cache, or tile update handle",
            "bevy_rerecast": "public NavmeshGenerator regeneration is full-navmesh oriented; this lab calls the same public synchronous stages",
            "landmass": "public Archipelago and AnimationLink APIs are used; no SteeringLandmass symbol exists in pinned landmass 0.9.2",
            "seam_collision_reporting": "Landmass add_animation_link has no public collision/rejection result; report records registration and observed path use separately",
        }),
        fixture_limits: json!({
            "static_obstacle_fixture": "base009 authored fixture is present at E:\\OpenRPG_Forge\\.gate3-evidence\\20261002\\tidewater-gameplay-invalid-quest-runtime-001\\authoring-root",
            "static_obstacle_metadata_report": "base009-static-obstacles.json",
            "base009_source_zone_sha256": "50bfd655a7fb4f5a61e4593240ea86dc8d13136d82d558d34737a41b4c6c8720",
            "base009_managed_object_sha256": "72598abab38bf2460ab6bebead8a02f364d19894fa1804a50dd6e4c92247445e",
            "canonical_obstacle_source": obstacle_path.display().to_string(),
            "metadata_substitution": false,
            "water_and_static_collider_proof": "missing; authoring metadata is pinned separately because this lab has no Forge mesh-import/collider resolver",
        }),
        dependency_governance: json!({
            "advisory_audit": "see dependency-advisory-compare.json",
            "cargo_deny_ignore_added": false,
            "direct_rerecast_vs_full_bevy_wrapper": "recorded from the isolated lock/tree audit; RUSTSEC findings remain blocking",
        }),
        selection: json!({
            "status": "blocked_pending_native_tile_cache_and_full_world_contract",
            "adapter_observation_is_not_provider_qualification": true,
        }),
        no_go: true,
        missing_proofs: vec![
            "native provider tile cache and bounded partial reconstruction".to_owned(),
            "canonical Landmass seam acceptance and collision governance".to_owned(),
            "runtime dynamic obstacle insertion/removal API".to_owned(),
            "static mesh-import room/door and water exclusions".to_owned(),
            "NPC traversal and steering acceptance across authored world seams".to_owned(),
            "browser execution of this campaign and human acceptance".to_owned(),
            "explicit ADR acceptance and dependency advisory resolution".to_owned(),
        ],
    };

    let mut output = OpenOptions::new()
        .write(true)
        .create_new(true)
        .open(output_path)?;
    serde_json::to_writer_pretty(&mut output, &campaign)?;
    output.write_all(b"\n")?;
    output.sync_all()?;
    Ok(())
}

struct BuiltState {
    report: StateReport,
}

fn validate_input(input: &CanonicalReport) -> Result<(), Box<dyn Error>> {
    if input.schema != "forge.navigation.canonical-terrain-probe" || input.version != 2 {
        return Err("campaign requires canonical terrain probe v2".into());
    }
    if input.tiles.len() != 3 {
        return Err("campaign requires the three canonical x bands".into());
    }
    let hash = mesh_hash(
        &input.canonical_geometry.positions_m,
        &input.canonical_geometry.triangles,
    )?;
    if hash != input.geometry_hash || hash != input.canonical_geometry.geometry_hash {
        return Err("canonical geometry hash does not match v2 report".into());
    }
    if input.canonical_geometry.positions_m.is_empty()
        || input.canonical_geometry.triangles.is_empty()
    {
        return Err("canonical geometry is empty".into());
    }
    for triangle in &input.canonical_geometry.triangles {
        for index in triangle {
            if usize::try_from(*index).map_or(true, |index| {
                index >= input.canonical_geometry.positions_m.len()
            }) {
                return Err("canonical triangle index is out of range".into());
            }
        }
    }
    if input
        .canonical_geometry
        .positions_m
        .iter()
        .flatten()
        .any(|value| !value.is_finite())
    {
        return Err("canonical positions contain a non-finite value".into());
    }
    if input
        .tiles
        .iter()
        .enumerate()
        .any(|(index, tile)| tile.index != index)
    {
        return Err("canonical tile indices are not ordered 0,1,2".into());
    }
    Ok(())
}

fn obstacle_from_v3(report: &Value) -> Result<ObstacleBox, Box<dyn Error>> {
    let selected = report
        .get("dynamic_obstacle_diagnostic")
        .and_then(|value| value.get("selected_obstacle"))
        .ok_or("v3 report has no selected_obstacle")?;
    Ok(serde_json::from_value(selected.clone())?)
}

fn validate_obstacle(
    obstacle: &ObstacleBox,
    input: &CanonicalReport,
) -> Result<(), Box<dyn Error>> {
    if !obstacle.center_xz_m.iter().all(|value| value.is_finite())
        || !obstacle.bottom_y_m.is_finite()
        || !obstacle.top_y_m.is_finite()
        || obstacle.half_width_m <= 0.0
        || obstacle.half_depth_m <= 0.0
        || obstacle.bottom_y_m >= obstacle.top_y_m
    {
        return Err("v3 obstacle has invalid finite dimensions".into());
    }
    let bounds = input.tiles[OBSTACLE_TILE].crop_xz_m;
    let [x, z] = obstacle.center_xz_m;
    if f64::from(x + obstacle.half_width_m) < bounds[0]
        || f64::from(x - obstacle.half_width_m) > bounds[1]
        || f64::from(z + obstacle.half_depth_m) < bounds[2]
        || f64::from(z - obstacle.half_depth_m) > bounds[3]
    {
        return Err("v3 obstacle does not intersect canonical tile 1".into());
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

#[allow(
    clippy::cast_possible_truncation,
    reason = "the canonical report stores crop bounds as f64 while the pinned provider mesh uses f32 world units"
)]
fn make_tile_geometry(
    base: &MeshData,
    input: &CanonicalReport,
) -> Result<Vec<TileGeometry>, Box<dyn Error>> {
    let [world_min_x, world_max_x, world_min_z, world_max_z] = input.crop_xz_m;
    let mut tiles = Vec::with_capacity(input.tiles.len());
    for tile in &input.tiles {
        let [core_min_x, core_max_x, core_min_z, core_max_z] = tile.crop_xz_m;
        let bounds = [
            (core_min_x - f64::from(BORDER_M)).max(world_min_x) as f32,
            (core_max_x + f64::from(BORDER_M)).min(world_max_x) as f32,
            (core_min_z - f64::from(BORDER_M)).max(world_min_z) as f32,
            (core_max_z + f64::from(BORDER_M)).min(world_max_z) as f32,
        ];
        let geometry = crop_mesh(base, bounds)?;
        tiles.push(TileGeometry {
            index: tile.index,
            core_bounds_m: [
                core_min_x as f32,
                core_max_x as f32,
                core_min_z as f32,
                core_max_z as f32,
            ],
            expanded_bounds_m: bounds,
            geometry,
        });
    }
    Ok(tiles)
}

fn build_state(
    label: &str,
    base_tiles: &[TileGeometry],
    obstacle: Option<&ObstacleBox>,
    start_m: [f32; 3],
    end_m: [f32; 3],
) -> Result<BuiltState, Box<dyn Error>> {
    let mut tile_reports = Vec::with_capacity(base_tiles.len());
    let mut built_tiles = Vec::with_capacity(base_tiles.len());
    for tile in base_tiles {
        let geometry = match (tile.index == OBSTACLE_TILE, obstacle) {
            (true, Some(obstacle)) => append_obstacle(&tile.geometry, obstacle)?,
            _ => tile.geometry.clone(),
        };
        let geometry_hash = mesh_hash(&geometry.positions, &geometry.triangles)?;
        let mut runs = Vec::with_capacity(REPRODUCIBILITY_RUNS);
        let mut first_navmesh = None;
        for run in 0..REPRODUCIBILITY_RUNS {
            let timer = Instant::now();
            let mut settings = provider::settings_for_agent(AGENT_RADIUS_M, AGENT_HEIGHT_M);
            settings.tiling = true;
            settings.tile_size = TILE_SIZE_CELLS;
            match provider::generate_navmesh(geometry.mesh.clone(), settings) {
                Ok(navmesh) => {
                    let navmesh_hash = canonical_json_sha256(&navmesh)?.to_string();
                    let generation_ms = timer.elapsed().as_secs_f64() * 1000.0;
                    if first_navmesh.is_none() {
                        first_navmesh = Some(navmesh);
                    }
                    runs.push(TileRunReport {
                        run,
                        status: "generated".to_owned(),
                        navmesh_hash: Some(navmesh_hash),
                        generation_ms,
                        error: None,
                    });
                }
                Err(error) => runs.push(TileRunReport {
                    run,
                    status: "rejected".to_owned(),
                    navmesh_hash: None,
                    generation_ms: timer.elapsed().as_secs_f64() * 1000.0,
                    error: Some(error.to_string()),
                }),
            }
        }
        let deterministic = runs.len() == REPRODUCIBILITY_RUNS
            && runs.iter().all(|run| run.status == "generated")
            && runs
                .iter()
                .map(|run| run.navmesh_hash.as_deref())
                .all_equal();
        let status = if first_navmesh.is_some() {
            "generated"
        } else {
            "rejected"
        };
        tile_reports.push(TileReport {
            index: tile.index,
            core_bounds_m: tile.core_bounds_m,
            expanded_bounds_m: tile.expanded_bounds_m,
            geometry_hash: geometry_hash.clone(),
            vertices: geometry.positions.len(),
            triangles: geometry.triangles.len(),
            status: status.to_owned(),
            deterministic,
            runs,
        });
        if let Some(navmesh) = first_navmesh {
            built_tiles.push(BuiltTile {
                index: tile.index,
                core_bounds_m: tile.core_bounds_m,
                navmesh,
            });
        }
    }

    let tile_hashes_deterministic = tile_reports.iter().all(|tile| tile.deterministic);
    let routes = (built_tiles.len() == base_tiles.len()).then(|| {
        route_report(
            &built_tiles,
            Vec3::from_array(start_m),
            Vec3::from_array(end_m),
        )
    });
    Ok(BuiltState {
        report: StateReport {
            label: label.to_owned(),
            obstacle: obstacle.cloned(),
            changed_tile: obstacle.map(|_| OBSTACLE_TILE),
            tile_reports,
            tile_hashes_deterministic,
            routes,
        },
    })
}

fn append_obstacle(base: &MeshData, obstacle: &ObstacleBox) -> Result<MeshData, Box<dyn Error>> {
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

fn route_report(tiles: &[BuiltTile], start: Vec3, end: Vec3) -> RouteReport {
    let seam_links = seam_links(tiles, start, end);
    let village_to_forest_without_links = run_route(tiles, start, end, &[], false);
    let forest_to_village_without_links = run_route(tiles, end, start, &[], false);
    let village_to_forest = run_route(tiles, start, end, &seam_links, true);
    let forest_to_village = run_route(tiles, end, start, &seam_links, true);
    RouteReport {
        route_id: "canonical-village-forest".to_owned(),
        village_to_forest_without_links,
        forest_to_village_without_links,
        village_to_forest,
        forest_to_village,
        seam_links,
    }
}

fn run_route(
    tiles: &[BuiltTile],
    start: Vec3,
    end: Vec3,
    seam_links: &[SeamLinkReport],
    with_links: bool,
) -> PathReport {
    let links = seam_links
        .iter()
        .map(|link| AnimationLink::<ThreeD> {
            start_edge: (
                Vec3::from_array(link.start_edge[0]),
                Vec3::from_array(link.start_edge[1]),
            ),
            end_edge: (
                Vec3::from_array(link.end_edge[0]),
                Vec3::from_array(link.end_edge[1]),
            ),
            kind: link.kind,
            cost: 1.0,
            bidirectional: link.bidirectional,
        })
        .collect::<Vec<_>>();
    let result = query_tiles(tiles, start, end, with_links.then_some(links));
    match result {
        Ok(path) => PathReport {
            status: if path.found { "observed" } else { "no_path" }.to_owned(),
            found: path.found,
            waypoint_count: path.waypoint_count,
            animation_link_count: path.animation_link_count,
            path_length_m: path_length(&path.waypoints_m),
            crosses_adapter_boundaries: crosses_boundary(&path.waypoints_m, tiles),
            waypoints_m: path.waypoints_m,
            error: None,
        },
        Err(error) => PathReport {
            status: "rejected".to_owned(),
            found: false,
            waypoint_count: 0,
            animation_link_count: 0,
            path_length_m: 0.0,
            crosses_adapter_boundaries: false,
            waypoints_m: Vec::new(),
            error: Some(error),
        },
    }
}

fn query_tiles(
    tiles: &[BuiltTile],
    start_m: Vec3,
    end_m: Vec3,
    links: Option<Vec<AnimationLink<ThreeD>>>,
) -> Result<navigation::PathProbe, String> {
    let mut options = ArchipelagoOptions::<ThreeD>::from_agent_radius(AGENT_RADIUS_M);
    options.point_sample_distance = generous_sample_distance();
    let mut archipelago = Archipelago::new(options);
    for tile in tiles {
        let raw = convert_rerecast_navmesh_to_landmass_navmesh(&tile.navmesh);
        let valid = raw.validate().map_err(|error| {
            format!(
                "tile {} rejected by Landmass validation: {error}",
                tile.index
            )
        })?;
        archipelago.add_island(Island::new(Transform::default(), Arc::new(valid)));
    }
    if let Some(links) = links {
        for link in links {
            archipelago.add_animation_link(link);
        }
    }
    archipelago.update(1.0 / 30.0);
    let sample_distance = generous_sample_distance();
    let start = archipelago
        .sample_point(start_m, &sample_distance)
        .map_err(|error| format!("village start rejected by Landmass sample: {error}"))?;
    let end = archipelago
        .sample_point(end_m, &sample_distance)
        .map_err(|error| format!("forest end rejected by Landmass sample: {error}"))?;
    match archipelago.find_path(&start, &end, &HashMap::new(), PermittedAnimationLinks::All) {
        Ok(steps) => Ok(path_probe_from_steps(steps)),
        Err(FindPathError::NoPathFound) => Ok(navigation::PathProbe {
            found: false,
            waypoint_count: 0,
            animation_link_count: 0,
            waypoints_m: Vec::new(),
        }),
        Err(error) => Err(format!("Landmass path rejected: {error}")),
    }
}

fn path_probe_from_steps(steps: Vec<PathStep<ThreeD>>) -> navigation::PathProbe {
    let waypoint_count = steps
        .iter()
        .filter(|step| matches!(step, PathStep::Waypoint(_)))
        .count();
    let animation_link_count = steps.len() - waypoint_count;
    let waypoints_m = steps
        .into_iter()
        .flat_map(|step| match step {
            PathStep::Waypoint(point) => vec![point.to_array()],
            PathStep::AnimationLink {
                start_point,
                end_point,
                ..
            } => {
                vec![start_point.to_array(), end_point.to_array()]
            }
        })
        .collect();
    navigation::PathProbe {
        found: true,
        waypoint_count,
        animation_link_count,
        waypoints_m,
    }
}

fn seam_links(tiles: &[BuiltTile], start: Vec3, end: Vec3) -> Vec<SeamLinkReport> {
    [18.0_f32, 46.0]
        .into_iter()
        .enumerate()
        .map(|(boundary_index, boundary_x_m)| {
            let denominator = start.x - end.x;
            let t = if denominator.abs() > f32::EPSILON {
                (start.x - boundary_x_m) / denominator
            } else {
                0.5
            };
            let y = start.y + (end.y - start.y) * t;
            let z = start.z + (end.z - start.z) * t;
            let target = Vec3::new(boundary_x_m, y, z);
            let (left_index, right_index) = if boundary_index == 0 {
                (0, 1)
            } else {
                (1, 2)
            };
            let left_anchor = tiles
                .get(left_index)
                .and_then(|tile| nearest_surface_point(tile, target));
            let right_anchor = tiles
                .get(right_index)
                .and_then(|tile| nearest_surface_point(tile, target));
            let (start_edge, end_edge, status, collision) = match (left_anchor, right_anchor) {
                (Some(left), Some(right)) => (
                    [left.to_array(), left.to_array()],
                    [right.to_array(), right.to_array()],
                    "registered_with_canonical_navmesh_point_anchors".to_owned(),
                    "provider_exposes_no_animation_link_collision_or_rejection_event; point anchors were sampled from both generated navmeshes".to_owned(),
                ),
                _ => (
                    [
                        [boundary_x_m - 0.6, y, z],
                        [boundary_x_m - 0.6, y, z],
                    ],
                    [
                        [boundary_x_m + 0.6, y, z],
                        [boundary_x_m + 0.6, y, z],
                    ],
                    "rejected_missing_canonical_navmesh_anchor".to_owned(),
                    "one or both generated tile navmeshes had no surface point near the canonical crossing".to_owned(),
                ),
            };
            SeamLinkReport {
                boundary_x_m,
                start_edge,
                end_edge,
                kind: BOUNDARY_LINK_KIND,
                bidirectional: true,
                status,
                collision,
            }
        })
        .collect()
}

#[allow(
    clippy::cast_precision_loss,
    reason = "a generated polygon has a small bounded vertex count, so the centroid conversion is exact enough for the link probe"
)]
fn nearest_surface_point(tile: &BuiltTile, target: Vec3) -> Option<Vec3> {
    let raw = convert_rerecast_navmesh_to_landmass_navmesh(&tile.navmesh);
    raw.polygons
        .iter()
        .filter_map(|polygon| {
            let mut sum = Vec3::ZERO;
            let mut count = 0_u32;
            for index in polygon {
                let vertex = *raw.vertices.get(*index)?;
                sum += vertex;
                count = count.checked_add(1)?;
            }
            (count > 0).then(|| {
                let centroid = sum / count as f32;
                let distance_squared =
                    (centroid.x - target.x).powi(2) + (centroid.z - target.z).powi(2);
                (distance_squared, centroid)
            })
        })
        .min_by(|left, right| left.0.total_cmp(&right.0))
        .map(|(_, point)| point)
}

fn crosses_boundary(path: &[[f32; 3]], tiles: &[BuiltTile]) -> bool {
    path.windows(2)
        .any(|segment| tile_for_x(segment[0][0], tiles) != tile_for_x(segment[1][0], tiles))
}

fn path_length(path: &[[f32; 3]]) -> f32 {
    path.windows(2)
        .map(|segment| {
            let left = Vec3::from_array(segment[0]);
            let right = Vec3::from_array(segment[1]);
            left.distance(right)
        })
        .sum()
}

fn tile_for_x(x: f32, tiles: &[BuiltTile]) -> Option<usize> {
    tiles.iter().find_map(|tile| {
        (x >= tile.core_bounds_m[0] && x <= tile.core_bounds_m[1]).then_some(tile.index)
    })
}

fn generous_sample_distance() -> PointSampleDistance3d {
    PointSampleDistance3d {
        horizontal_distance: AGENT_RADIUS_M * 4.0,
        distance_above: 2.0,
        distance_below: 2.0,
        vertical_preference_ratio: 2.0,
        animation_link_max_vertical_distance: 1.0,
    }
}

fn crop_mesh(base: &MeshData, bounds: [f32; 4]) -> Result<MeshData, Box<dyn Error>> {
    let mut positions = Vec::new();
    let mut triangles = Vec::new();
    for triangle in &base.triangles {
        let source = triangle
            .iter()
            .map(|index| {
                base.positions
                    .get(usize::try_from(*index).map_err(|error| error.to_string())?)
                    .copied()
                    .ok_or_else(|| "canonical triangle index is out of range".to_owned())
            })
            .collect::<Result<Vec<_>, String>>()?;
        let x_min = source
            .iter()
            .map(|point| point[0])
            .fold(f32::INFINITY, f32::min);
        let x_max = source
            .iter()
            .map(|point| point[0])
            .fold(f32::NEG_INFINITY, f32::max);
        let z_min = source
            .iter()
            .map(|point| point[2])
            .fold(f32::INFINITY, f32::min);
        let z_max = source
            .iter()
            .map(|point| point[2])
            .fold(f32::NEG_INFINITY, f32::max);
        if x_max < bounds[0] || x_min > bounds[1] || z_max < bounds[2] || z_min > bounds[3] {
            continue;
        }
        let base_index = u32::try_from(positions.len())?;
        positions.extend(source);
        triangles.push([base_index, base_index + 1, base_index + 2]);
    }
    if triangles.is_empty() {
        return Err("adapter tile crop contains no canonical triangles".into());
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

fn mesh_hash(positions: &[[f32; 3]], triangles: &[[u32; 3]]) -> Result<String, Box<dyn Error>> {
    Ok(canonical_json_sha256(&(positions, triangles))?.to_string())
}

fn compare_states(before: &BuiltState, after: &BuiltState, removed: &BuiltState) -> Value {
    let before_hashes = tile_hashes(&before.report);
    let after_hashes = tile_hashes(&after.report);
    let removed_hashes = tile_hashes(&removed.report);
    let untouched = (0..3)
        .map(|index| {
            json!({
                "tile_index": index,
                "before_after_geometry_hash_unchanged": before_hashes[index].0 == after_hashes[index].0,
                "before_after_navmesh_hash_unchanged": before_hashes[index].1 == after_hashes[index].1,
                "before_removed_geometry_hash_unchanged": before_hashes[index].0 == removed_hashes[index].0,
                "before_removed_navmesh_hash_unchanged": before_hashes[index].1 == removed_hashes[index].1,
                "touched_by_obstacle": index == OBSTACLE_TILE,
            })
        })
        .collect::<Vec<_>>();
    json!({
        "obstacle_update_scope": { "requested_tile": OBSTACLE_TILE, "only_tile_1_geometry_changed": before_hashes[OBSTACLE_TILE].0 != after_hashes[OBSTACLE_TILE].0 },
        "untouched_tiles": untouched,
        "removed_restores_all_baseline_tile_hashes": before_hashes == removed_hashes,
        "before_after_routes_observed": route_observed(&before.report),
        "after_routes_observed": route_observed(&after.report),
        "removed_routes_observed": route_observed(&removed.report),
        "obstacle_detour": route_detour(&before.report, &after.report),
        "deterministic_states": {
            "before": before.report.tile_hashes_deterministic,
            "after_obstacle": after.report.tile_hashes_deterministic,
            "after_removal": removed.report.tile_hashes_deterministic,
        },
    })
}

fn route_detour(before: &StateReport, after: &StateReport) -> Value {
    let Some(before_route) = before.routes.as_ref() else {
        return json!({"status": "not_run"});
    };
    let Some(after_route) = after.routes.as_ref() else {
        return json!({"status": "not_run"});
    };
    let village_delta =
        after_route.village_to_forest.path_length_m - before_route.village_to_forest.path_length_m;
    let forest_delta =
        after_route.forest_to_village.path_length_m - before_route.forest_to_village.path_length_m;
    json!({
        "village_to_forest_before_m": before_route.village_to_forest.path_length_m,
        "village_to_forest_after_m": after_route.village_to_forest.path_length_m,
        "village_to_forest_delta_m": village_delta,
        "forest_to_village_before_m": before_route.forest_to_village.path_length_m,
        "forest_to_village_after_m": after_route.forest_to_village.path_length_m,
        "forest_to_village_delta_m": forest_delta,
        "detour_observed": before_route.village_to_forest.found
            && after_route.village_to_forest.found
            && village_delta > 0.5,
    })
}

fn tile_hashes(report: &StateReport) -> Vec<(String, String)> {
    report
        .tile_reports
        .iter()
        .map(|tile| {
            let navmesh_hash = tile
                .runs
                .first()
                .and_then(|run| run.navmesh_hash.clone())
                .unwrap_or_default();
            (tile.geometry_hash.clone(), navmesh_hash)
        })
        .collect()
}

fn route_observed(report: &StateReport) -> Value {
    report
        .routes
        .as_ref()
        .map_or_else(|| json!({"status": "not_run"}), |routes| {
            json!({
                "village_to_forest": routes.village_to_forest.status,
                "forest_to_village": routes.forest_to_village.status,
                "village_to_forest_path_length_m": routes.village_to_forest.path_length_m,
                "forest_to_village_path_length_m": routes.forest_to_village.path_length_m,
                "village_to_forest_crosses_boundary": routes.village_to_forest.crosses_adapter_boundaries,
                "forest_to_village_crosses_boundary": routes.forest_to_village.crosses_adapter_boundaries,
            })
        })
}

trait AllEqual: Iterator {
    fn all_equal(mut self) -> bool
    where
        Self::Item: PartialEq,
        Self: Sized,
    {
        let Some(first) = self.next() else {
            return true;
        };
        self.all(|item| item == first)
    }
}

impl<I: Iterator> AllEqual for I {}
