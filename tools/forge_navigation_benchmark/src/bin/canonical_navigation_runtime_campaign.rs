//! Campaign 011: adapter-owned local reconstruction driven by Landmass and Rapier.
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
    Agent, AgentId, AgentState, AnimationLink, Archipelago, FindPathError, Island, IslandId,
    PermittedAnimationLinks, Transform,
};
use landmass_rerecast::convert_rerecast_navmesh_to_landmass_navmesh;
use rapier3d::control::{CharacterLength, KinematicCharacterController};
use rapier3d::prelude::{
    ActiveEvents, ChannelEventCollector, ColliderBuilder, ColliderHandle, CollisionEvent,
    PhysicsWorld, QueryFilter, Ray, RigidBodyBuilder, RigidBodyHandle, Vector,
};
use serde::{Deserialize, Serialize};
use serde_json::{Value, json};
use std::{collections::HashMap, error::Error, sync::Arc};

#[cfg(not(target_arch = "wasm32"))]
use std::{
    fs::{self, OpenOptions},
    io::Write,
    path::Path,
    time::Instant,
};

#[cfg(target_arch = "wasm32")]
use wasm_bindgen::prelude::*;

#[cfg(target_arch = "wasm32")]
#[wasm_bindgen(inline_js = "export function navigation_now_ms() { return performance.now(); }")]
extern "C" {
    fn navigation_now_ms() -> f64;
}

#[cfg(target_arch = "wasm32")]
struct Instant(f64);

#[cfg(target_arch = "wasm32")]
impl Instant {
    fn now() -> Self {
        Self(navigation_now_ms())
    }
    fn elapsed(&self) -> std::time::Duration {
        std::time::Duration::from_secs_f64((navigation_now_ms() - self.0).max(0.0) / 1000.0)
    }
}

#[path = "../navigation.rs"]
#[allow(
    dead_code,
    reason = "the campaign reuses the laboratory's canonical path probe types and provider helpers"
)]
mod navigation;
#[path = "../provider.rs"]
mod provider;

#[cfg(not(target_arch = "wasm32"))]
#[path = "../geometry.rs"]
#[allow(
    dead_code,
    reason = "the VisualValley partial probe reuses the exact global geometry builder"
)]
mod geometry;

const AGENT_RADIUS_M: f32 = 0.35;
const AGENT_HEIGHT_M: f32 = 1.9;
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
struct StaticColliderReport {
    schema: String,
    version: u32,
    source_hash: String,
    terrain_hash: String,
    raster_hash: String,
    geometry_hash: String,
    canonical_geometry: CanonicalGeometry,
    static_obstacles: Value,
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
    terrain_probe_geometry_hash: String,
    canonical_geometry_counts: Value,
    adapter: Value,
    states: Vec<StateReport>,
    comparisons: Value,
    provider_api_limits: Value,
    fixture_limits: Value,
    dependency_governance: Value,
    selection: Value,
    runtime: Value,
    no_go: bool,
    missing_proofs: Vec<String>,
}

#[cfg(not(target_arch = "wasm32"))]
fn main() -> Result<(), Box<dyn Error>> {
    let args: Vec<_> = std::env::args().skip(1).collect();
    if args.len() == 3 && args[0] == "--visual-valley-partial" {
        let report = visual_valley_partial_probe(&fs::read_to_string(&args[1])?)?;
        let mut output = OpenOptions::new()
            .write(true)
            .create_new(true)
            .open(&args[2])?;
        serde_json::to_writer_pretty(&mut output, &report)?;
        output.write_all(b"\n")?;
        output.sync_all()?;
        if report["passed"] != true {
            return Err("VisualValley partial contract failed; report preserved".into());
        }
        return Ok(());
    }
    if args.len() != 4 {
        return Err("usage: canonical_navigation_runtime_campaign <canonical-terrain-probe.v2.json> <canonical-navigation-diagnostics.v3.json> <canonical-terrain-with-static-colliders.v1.json> <new-report.v1.json>".into());
    }

    let input_path = Path::new(&args[0]);
    let obstacle_path = Path::new(&args[1]);
    let static_path = Path::new(&args[2]);
    let output_path = Path::new(&args[3]);
    let campaign = build_campaign(
        &fs::read_to_string(input_path)?,
        &fs::read_to_string(obstacle_path)?,
        &fs::read_to_string(static_path)?,
        &args[0],
        &args[1],
        &args[2],
    )?;
    let mut output = OpenOptions::new()
        .write(true)
        .create_new(true)
        .open(output_path)?;
    serde_json::to_writer_pretty(&mut output, &campaign)?;
    output.write_all(b"\n")?;
    output.sync_all()?;
    if !physical_contract_passed(&campaign) {
        return Err("bounded physical runtime contract failed; full report was preserved".into());
    }
    Ok(())
}

#[cfg(target_arch = "wasm32")]
fn main() {}

#[cfg(target_arch = "wasm32")]
#[wasm_bindgen]
pub fn run_canonical_navigation_runtime(
    input_json: &str,
    obstacle_json: &str,
    static_json: &str,
) -> Result<String, JsValue> {
    let campaign = build_campaign(
        input_json,
        obstacle_json,
        static_json,
        "canonical-terrain-probe.v2.json",
        "canonical-navigation-diagnostics.v3.json",
        "terrain-with-colliders.v1.json",
    )
    .map_err(|error| JsValue::from_str(&error.to_string()))?;
    // Preserve the complete failed report too; the page asserts the physical contract.
    let passed = physical_contract_passed(&campaign);
    serde_json::to_string(&json!({"passed": passed, "campaign": campaign}))
        .map_err(|error| JsValue::from_str(&error.to_string()))
}

fn physical_contract_passed(campaign: &CampaignReport) -> bool {
    campaign.runtime["status"] == "completed"
        && campaign.runtime["rapier"]["physical_obstacle_control"]["passed"] == true
}

#[cfg(not(target_arch = "wasm32"))]
fn visual_valley_partial_probe(encoded: &str) -> Result<Value, Box<dyn Error>> {
    let sketch = forge_world::WorldSketchDocument::from_json(encoded)?;
    let compiled = forge_world::compile_world_sketch(&sketch)?;
    let terrain =
        forge_world::TerrainGenerationPlan::from_document(&compiled.carved_terrain)?.generate()?;
    let bridge = compiled
        .bridge_placements
        .placements
        .first()
        .ok_or("VisualValley bridge missing")?;
    let mesh =
        geometry::exterior_bridge_mesh(&terrain, &compiled.traversal_audit, bridge, 256.0, true)?;
    let bounds = mesh.compute_aabb().ok_or("VisualValley geometry empty")?;
    let positions = mesh
        .vertices
        .iter()
        .map(Vec3A::to_array)
        .collect::<Vec<_>>();
    let triangles = mesh.indices.iter().map(UVec3::to_array).collect::<Vec<_>>();
    let geometry_hash = mesh_hash(&positions, &triangles)?;
    let start = bridge.deck_start_surface_m;
    let end = bridge.deck_end_surface_m;
    let crop = [
        f64::from(bounds.min.x),
        f64::from(bounds.max.x),
        f64::from(bounds.min.z),
        f64::from(bounds.max.z),
    ];
    let width = (crop[1] - crop[0]) / 3.0;
    let input = CanonicalReport {
        schema: "forge.navigation.visual-valley-derived-triangles".to_owned(),
        version: 1,
        area_id: "VisualValley benchmark fixture".to_owned(),
        source_hash: sketch.content_hash()?.to_string(),
        terrain_hash: compiled.carved_terrain.content_hash()?.to_string(),
        raster_hash: "procedural_source_no_imported_raster".to_owned(),
        geometry_hash: geometry_hash.clone(),
        crop_xz_m: crop,
        start_m: start,
        end_m: end,
        canonical_geometry: CanonicalGeometry {
            positions_m: positions.clone(),
            triangles: triangles.clone(),
            geometry_hash: geometry_hash.clone(),
        },
        tiles: (0_u32..3)
            .map(|index| CanonicalTile {
                index: index as usize,
                crop_xz_m: [
                    crop[0] + f64::from(index) * width,
                    crop[0] + f64::from(index + 1) * width,
                    crop[2],
                    crop[3],
                ],
            })
            .collect(),
    };
    let base = mesh_from_geometry(&input.canonical_geometry);
    let tile_geometry = make_tile_geometry(&base, &input)?;
    let mut built = Vec::new();
    let mut initial = Vec::new();
    for tile in &tile_geometry {
        let mut attempts = Vec::new();
        let mut first = None;
        for _ in 0..REPRODUCIBILITY_RUNS {
            let (navmesh, _, _, duration_ms) = rebuild_runtime_tile(tile, None)?;
            convert_rerecast_navmesh_to_landmass_navmesh(&navmesh).validate()?;
            attempts.push(json!({"navmesh_hash":canonical_json_sha256(&navmesh)?.to_string(),"duration_ms":duration_ms}));
            if first.is_none() {
                first = Some(navmesh);
            }
        }
        initial.push(json!({"tile":tile.index,"core_bounds_m":tile.core_bounds_m,
            "geometry_hash":mesh_hash(&tile.geometry.positions,&tile.geometry.triangles)?,"runs":attempts}));
        built.push(first.ok_or("VisualValley tile missing")?);
    }
    let before_hashes = built
        .iter()
        .map(canonical_json_sha256)
        .collect::<Result<Vec<_>, _>>()?;
    let before_path = navigation::query_rerecast_path(
        &built[OBSTACLE_TILE],
        Vec3::from_array(start),
        Vec3::from_array(end),
        AGENT_RADIUS_M,
    )?;
    let obstacle = ObstacleBox {
        center_xz_m: [start[0].midpoint(end[0]), start[2].midpoint(end[2])],
        half_width_m: 1.5,
        half_depth_m: 1.5,
        bottom_y_m: bounds.min.y - 1.0,
        top_y_m: bounds.max.y + 2.0,
    };
    let (changed, _, _, insert_ms) =
        rebuild_runtime_tile(&tile_geometry[OBSTACLE_TILE], Some(&obstacle))?;
    convert_rerecast_navmesh_to_landmass_navmesh(&changed).validate()?;
    let after_path = navigation::query_rerecast_path(
        &changed,
        Vec3::from_array(start),
        Vec3::from_array(end),
        AGENT_RADIUS_M,
    )?;
    built[OBSTACLE_TILE] = changed;
    let after_hashes = built
        .iter()
        .map(canonical_json_sha256)
        .collect::<Result<Vec<_>, _>>()?;
    let (restored, _, _, remove_ms) = rebuild_runtime_tile(&tile_geometry[OBSTACLE_TILE], None)?;
    convert_rerecast_navmesh_to_landmass_navmesh(&restored).validate()?;
    let restored_path = navigation::query_rerecast_path(
        &restored,
        Vec3::from_array(start),
        Vec3::from_array(end),
        AGENT_RADIUS_M,
    )?;
    built[OBSTACLE_TILE] = restored;
    let restored_hashes = built
        .iter()
        .map(canonical_json_sha256)
        .collect::<Result<Vec<_>, _>>()?;
    let path_length = |path: &navigation::PathProbe| {
        path.waypoints_m
            .windows(2)
            .map(|pair| Vec3::from_array(pair[0]).distance(Vec3::from_array(pair[1])))
            .sum::<f32>()
    };
    let static_report = StaticColliderReport {
        schema: "forge.navigation.visual-valley-shared-physics".to_owned(),
        version: 1,
        source_hash: input.source_hash.clone(),
        terrain_hash: input.terrain_hash.clone(),
        raster_hash: input.raster_hash.clone(),
        geometry_hash: geometry_hash.clone(),
        canonical_geometry: input.canonical_geometry,
        static_obstacles: json!([]),
    };
    let (mut world, body, actor, static_collider) = make_runtime_physics(&static_report, start)?;
    let mut support = MovementEvidence::default();
    let controller = runtime_character_controller();
    for tick in 0..30 {
        apply_character_movement(
            &mut world,
            body,
            actor,
            static_collider,
            None,
            &controller,
            Vector::new(
                0.0,
                -9.81 * RUNTIME_DELTA_SECONDS * RUNTIME_DELTA_SECONDS,
                0.0,
            ),
            tick,
            &mut support,
        )?;
        world.step();
    }
    let obstacle_handle = world.colliders.insert(
        ColliderBuilder::cuboid(
            obstacle.half_width_m,
            (obstacle.top_y_m - obstacle.bottom_y_m) * 0.5,
            obstacle.half_depth_m,
        )
        .translation(Vector::new(
            obstacle.center_xz_m[0],
            obstacle.top_y_m.midpoint(obstacle.bottom_y_m),
            obstacle.center_xz_m[1],
        ))
        .build(),
    );
    world.step();
    let live_obstacle = world.colliders.get(obstacle_handle).is_some();
    world
        .colliders
        .remove(obstacle_handle, &mut world.islands, &mut world.bodies, true);
    world.step();
    let changed_only_one = before_hashes[0] == after_hashes[0]
        && before_hashes[2] == after_hashes[2]
        && before_hashes[1] != after_hashes[1];
    let restored_all = before_hashes == restored_hashes;
    let passed = before_path.found
        && restored_path.found
        && changed_only_one
        && restored_all
        && support.grounded_ticks > 0
        && support.static_hits > 0
        && live_obstacle;
    Ok(
        json!({"schema":"forge.navigation.visual-valley-partial-adapter-probe","version":1,"passed":passed,
        "source_world_sketch_sha256":input.source_hash,"source_carved_terrain_sha256":input.terrain_hash,
        "canonical_geometry_hash":geometry_hash,"canonical_geometry":{"positions_m":positions,"triangles":triangles},
        "strategy":"adapter-owned three bands with explicit two-metre border; actual rebuild only tile1",
        "agent_height_m":AGENT_HEIGHT_M,"agent_radius_m":AGENT_RADIUS_M,"initial_tiles":initial,
        "before_hashes":before_hashes,"after_hashes":after_hashes,"restored_hashes":restored_hashes,
        "changed_only_tile1":changed_only_one,"restored_all_hashes":restored_all,
        "reconstruction":{"insertion_ms":insert_ms,"removal_ms":remove_ms,"actual_calls_after_initialization":[1,1]},
        "obstacle":obstacle,"obstacle_origin":"explicit LAB box placed at the canonical compiled bridge deck midpoint",
        "paths":{"before":before_path,"after":after_path,"restored":restored_path},
        "path_lengths_m":{"before":path_length(&before_path),"after":path_length(&after_path),"restored":path_length(&restored_path)},
        "physics":{"geometry_hash":static_report.geometry_hash,"same_canonical_triangles":true,
            "initial_support_kcc":{"queries":support.queries,"grounded_ticks":support.grounded_ticks,
                "canonical_shape_contacts":support.static_hits,"initial_grounded":support.initial_grounded,"samples":support.samples},
            "obstacle_installed_then_removed":live_obstacle},
        "limits":["native partial reconstruction on VisualValley; complete KCC corridor proof is the distinct Tidewater campaign",
            "no native provider tile-cache API; no full-zone product integration or owner acceptance"]}),
    )
}

fn build_campaign(
    input_json: &str,
    obstacle_json: &str,
    static_json: &str,
    input_label: &str,
    obstacle_label: &str,
    static_label: &str,
) -> Result<CampaignReport, Box<dyn Error>> {
    let input: CanonicalReport = serde_json::from_str(input_json)?;
    let obstacle_report: Value = serde_json::from_str(obstacle_json)?;
    let static_report: StaticColliderReport = serde_json::from_str(static_json)?;
    validate_input(&input)?;
    validate_static_report(&input, &static_report)?;
    let obstacle = obstacle_from_v3(&obstacle_report)?;
    validate_obstacle(&obstacle, &input)?;

    // The navmesh and Rapier collider are both derived from this same canonical
    // static-collider document. The v2 report supplies the bounded tile policy
    // and authored village/forest endpoints.
    let base = mesh_from_geometry(&static_report.canonical_geometry);
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
    let runtime = run_runtime_campaign(
        &input,
        &static_report,
        &tile_geometry,
        &before,
        &after,
        &removed,
        &obstacle,
    )?;
    let campaign = CampaignReport {
        schema: "forge.navigation.canonical-navigation-runtime-campaign",
        version: 2,
        input_report: input_label.to_owned(),
        obstacle_report: obstacle_label.to_owned(),
        area_id: input.area_id.clone(),
        source_hash: input.source_hash.clone(),
        terrain_hash: input.terrain_hash.clone(),
        raster_hash: input.raster_hash.clone(),
        canonical_geometry_hash: static_report.geometry_hash.clone(),
        terrain_probe_geometry_hash: input.geometry_hash.clone(),
        canonical_geometry_counts: json!({
            "vertices": static_report.canonical_geometry.positions_m.len(),
            "triangles": static_report.canonical_geometry.triangles.len(),
            "crop_xz_m": input.crop_xz_m,
            "source_document": static_label,
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
            "canonical_obstacle_source": obstacle_label,
            "metadata_substitution": false,
            "water_and_static_collider_proof": "exact static triangles feed both Rerecast and Rapier KCC; water exclusions and the full authored indoor door route remain unexercised",
        }),
        dependency_governance: json!({
            "advisory_audit": "see dependency-advisory-compare.json",
            "cargo_deny_ignore_added": false,
            "direct_rerecast_vs_full_bevy_wrapper": "lab-only forks remove unused asset-loader edges; fresh external audit and owner maintenance acceptance remain separate",
        }),
        selection: json!({
            "status": "blocked_pending_adr_owner_acceptance_and_runtime_world_contract",
            "adapter_observation_is_not_provider_qualification": true,
        }),
        runtime,
        no_go: true,
        missing_proofs: vec![
            "ADR-0009 tile strategy and measured local partial reconstruction still require owner acceptance".to_owned(),
            "canonical Landmass seam acceptance and collision governance".to_owned(),
            "static mesh-import room/door traversal and water exclusions are not fully exercised by this corridor".to_owned(),
            "browser execution of this campaign and human acceptance".to_owned(),
            "explicit ADR acceptance and maintenance governance for local provider forks".to_owned(),
        ],
    };

    Ok(campaign)
}

struct BuiltState {
    report: StateReport,
    tiles: Vec<BuiltTile>,
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

fn validate_static_report(
    input: &CanonicalReport,
    static_report: &StaticColliderReport,
) -> Result<(), Box<dyn Error>> {
    if static_report.schema != "forge.navigation.canonical-terrain-with-static-colliders"
        || static_report.version != 1
    {
        return Err("runtime campaign requires canonical static-collider report v1".into());
    }
    if static_report.source_hash != input.source_hash
        || static_report.terrain_hash != input.terrain_hash
        || static_report.raster_hash != input.raster_hash
    {
        return Err(
            "static-collider report source/raster metadata does not match terrain v2".into(),
        );
    }
    if static_report.static_obstacles.is_null() {
        return Err("static-collider report has no obstacle manifest".into());
    }
    let geometry = &static_report.canonical_geometry;
    if geometry.positions_m.is_empty() || geometry.triangles.is_empty() {
        return Err("static-collider canonical geometry is empty".into());
    }
    if geometry
        .positions_m
        .iter()
        .flatten()
        .any(|value| !value.is_finite())
    {
        return Err("static-collider geometry contains non-finite coordinates".into());
    }
    for triangle in &geometry.triangles {
        if triangle.iter().any(|index| {
            usize::try_from(*index).map_or(true, |index| index >= geometry.positions_m.len())
        }) {
            return Err("static-collider triangle index is out of range".into());
        }
    }
    let hash = mesh_hash(&geometry.positions_m, &geometry.triangles)?;
    if hash != static_report.geometry_hash || hash != geometry.geometry_hash {
        return Err(
            "static-collider geometry hash does not match its positions and indices".into(),
        );
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
        tiles: built_tiles,
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

const RUNTIME_DELTA_SECONDS: f32 = 1.0 / 30.0;
const RUNTIME_TICKS: usize = 1_800;
const INSERT_TICK: usize = 180;
const REMOVE_TICK: usize = 900;
const ACTOR_CENTER_HEIGHT_M: f32 = AGENT_HEIGHT_M * 0.5;

#[derive(Default)]
struct MovementEvidence {
    queries: usize,
    grounded_ticks: usize,
    static_hits: usize,
    obstacle_hits: usize,
    clipped_horizontal_ticks: usize,
    max_translation_m: f32,
    samples: Vec<Value>,
    last_grounded: bool,
    initial_grounded: Option<bool>,
}

fn run_runtime_campaign(
    input: &CanonicalReport,
    static_report: &StaticColliderReport,
    tile_geometry: &[TileGeometry],
    before: &BuiltState,
    after: &BuiltState,
    removed: &BuiltState,
    obstacle: &ObstacleBox,
) -> Result<Value, Box<dyn Error>> {
    let before_tiles = &before.tiles;
    let after_tiles = &after.tiles;
    let removed_tiles = &removed.tiles;
    if before_tiles.len() != tile_geometry.len()
        || after_tiles.len() != tile_geometry.len()
        || removed_tiles.len() != tile_geometry.len()
    {
        return Err("runtime campaign requires every canonical tile in each state".into());
    }
    let before_hashes = before_tiles
        .iter()
        .map(|tile| canonical_json_sha256(&tile.navmesh).map(|hash| hash.to_string()))
        .collect::<Result<Vec<_>, _>>()?;
    let after_hashes = after_tiles
        .iter()
        .map(|tile| canonical_json_sha256(&tile.navmesh).map(|hash| hash.to_string()))
        .collect::<Result<Vec<_>, _>>()?;
    let removed_hashes = removed_tiles
        .iter()
        .map(|tile| canonical_json_sha256(&tile.navmesh).map(|hash| hash.to_string()))
        .collect::<Result<Vec<_>, _>>()?;

    let (mut archipelago, island_ids, initial_agent_id, seam_links) =
        make_runtime_archipelago(before_tiles, input.start_m, input.end_m)?;
    let (mut world, actor_body, actor_collider, static_collider) =
        make_runtime_physics(static_report, input.start_m)?;
    let (collision_send, collision_receive) = std::sync::mpsc::channel();
    let (contact_send, _contact_receive) = std::sync::mpsc::channel();
    let event_handler = ChannelEventCollector::new(collision_send, contact_send);
    let mut obstacle_collider: Option<ColliderHandle> = None;
    let mut event_log = Vec::new();
    let mut state_counts = HashMap::<String, usize>::new();
    let mut link_traversals = 0_usize;
    let mut agent_id = initial_agent_id;
    let mut agent_rebinds = 0_usize;
    let mut phase_samples = Vec::new();
    let mut reached_target_tick = None;
    let mut insertion: Option<Value> = None;
    let mut removal: Option<Value> = None;
    let mut movement_evidence = MovementEvidence::default();
    let mut pending_link_end: Option<Vec3> = None;
    let mut vertical_velocity_mps = 0.0;
    let mut motion_trace = Vec::new();
    let controller = runtime_character_controller();
    let initial_feet = runtime_actor_feet(world.bodies[actor_body].translation());
    archipelago
        .get_agent_mut(agent_id)
        .ok_or("initial runtime agent missing")?
        .position = Vec3::new(initial_feet.x, initial_feet.y, initial_feet.z);

    for tick in 0..RUNTIME_TICKS {
        if tick == INSERT_TICK {
            let (navmesh, geometry_hash, navmesh_hash, generation_ms) =
                rebuild_runtime_tile(&tile_geometry[OBSTACLE_TILE], Some(obstacle))?;
            replace_runtime_island(&mut archipelago, island_ids[OBSTACLE_TILE], &navmesh)?;
            let position = world
                .bodies
                .get(actor_body)
                .ok_or("runtime Rapier actor body disappeared during insertion")?
                .translation();
            agent_id = rebind_runtime_agent(
                &mut archipelago,
                agent_id,
                runtime_actor_feet(position),
                input.end_m,
            );
            pending_link_end = None;
            agent_rebinds += 1;
            let [center_x, center_z] = obstacle.center_xz_m;
            let collider = ColliderBuilder::cuboid(
                obstacle.half_width_m,
                (obstacle.top_y_m - obstacle.bottom_y_m) * 0.5,
                obstacle.half_depth_m,
            )
            .translation(Vector::new(
                center_x,
                obstacle.top_y_m.midpoint(obstacle.bottom_y_m),
                center_z,
            ))
            .active_events(ActiveEvents::COLLISION_EVENTS)
            .build();
            obstacle_collider = Some(world.colliders.insert(collider));
            // Synchronize the broad phase before the next character shape query.
            world.step_with_events(&(), &event_handler);
            insertion = Some(json!({
                "tick": tick,
                "status": "inserted_and_rebuilt_one_tile",
                "tile_index": OBSTACLE_TILE,
                "generation_ms": generation_ms,
                "geometry_hash": geometry_hash,
                "navmesh_hash": navmesh_hash,
                "expected_full_state_hash": after_hashes[OBSTACLE_TILE],
                "reused_tile_indices": [0, 2],
                "source_geometry_mutated": false,
            }));
        }
        if tick == REMOVE_TICK {
            if let Some(handle) = obstacle_collider.take() {
                world
                    .colliders
                    .remove(handle, &mut world.islands, &mut world.bodies, true);
            }
            let (navmesh, geometry_hash, navmesh_hash, generation_ms) =
                rebuild_runtime_tile(&tile_geometry[OBSTACLE_TILE], None)?;
            replace_runtime_island(&mut archipelago, island_ids[OBSTACLE_TILE], &navmesh)?;
            let position = world
                .bodies
                .get(actor_body)
                .ok_or("runtime Rapier actor body disappeared during removal")?
                .translation();
            agent_id = rebind_runtime_agent(
                &mut archipelago,
                agent_id,
                runtime_actor_feet(position),
                input.end_m,
            );
            pending_link_end = None;
            agent_rebinds += 1;
            removal = Some(json!({
                "tick": tick,
                "status": "removed_and_rebuilt_one_tile",
                "tile_index": OBSTACLE_TILE,
                "generation_ms": generation_ms,
                "geometry_hash": geometry_hash,
                "navmesh_hash": navmesh_hash,
                "expected_baseline_hash": removed_hashes[OBSTACLE_TILE],
                "reused_tile_indices": [0, 2],
                "source_geometry_mutated": false,
            }));
            world.step_with_events(&(), &event_handler);
        }

        archipelago.update(RUNTIME_DELTA_SECONDS);
        let state = archipelago
            .get_agent(agent_id)
            .ok_or("runtime Landmass agent disappeared")?
            .state();
        *state_counts.entry(format!("{state:?}")).or_default() += 1;
        if [
            0,
            INSERT_TICK - 1,
            INSERT_TICK,
            INSERT_TICK + 1,
            REMOVE_TICK - 1,
            REMOVE_TICK,
            REMOVE_TICK + 1,
        ]
        .contains(&tick)
        {
            let position = world
                .bodies
                .get(actor_body)
                .ok_or("runtime Rapier actor body disappeared at phase sample")?
                .translation();
            phase_samples.push(json!({
                "tick": tick,
                "state": format!("{state:?}"),
                "position_m": [position.x, position.y, position.z],
            }));
        }

        if pending_link_end.is_none()
            && let Some(reached) = archipelago
                .get_agent(agent_id)
                .and_then(|agent| agent.reached_animation_link())
        {
            let end = reached.end_point;
            archipelago
                .get_agent_mut(agent_id)
                .ok_or("runtime Landmass agent disappeared before link start")?
                .start_animation_link()?;
            link_traversals += 1;
            pending_link_end = Some(end);
        }

        let current = world
            .bodies
            .get(actor_body)
            .ok_or("runtime Rapier actor body disappeared")?
            .translation();
        let feet = runtime_actor_feet(current);
        let desired_velocity = if let Some(end) = pending_link_end {
            let difference = end - Vec3::new(feet.x, feet.y, feet.z);
            let horizontal = Vec3::new(difference.x, 0.0, difference.z);
            horizontal.normalize_or_zero() * (horizontal.length() / RUNTIME_DELTA_SECONDS).min(4.0)
        } else {
            *archipelago
                .get_agent(agent_id)
                .ok_or("runtime Landmass agent disappeared before velocity read")?
                .get_desired_velocity()
        };
        // Landmass chooses horizontal intent. Rapier alone resolves terrain height,
        // walls, and obstacle collisions; no navmesh projection is applied to the body.
        if movement_evidence.last_grounded {
            vertical_velocity_mps = 0.0;
        }
        vertical_velocity_mps -= 9.81 * RUNTIME_DELTA_SECONDS;
        let desired = Vector::new(
            desired_velocity.x,
            vertical_velocity_mps,
            desired_velocity.z,
        ) * RUNTIME_DELTA_SECONDS;
        apply_character_movement(
            &mut world,
            actor_body,
            actor_collider,
            static_collider,
            obstacle_collider,
            &controller,
            desired,
            tick,
            &mut movement_evidence,
        )?;
        world.step_with_events(&(), &event_handler);

        let actual = world
            .bodies
            .get(actor_body)
            .ok_or("runtime Rapier actor body disappeared after step")?
            .translation();
        let actual_feet = runtime_actor_feet(actual);
        let actual_landmass = Vec3::new(actual_feet.x, actual_feet.y, actual_feet.z);
        if tick % 60 == 0 || tick + 1 == RUNTIME_TICKS {
            motion_trace.push(json!({"tick": tick, "state": format!("{state:?}"),
                "actual_feet_m": actual_landmass.to_array(),
                "desired_velocity_mps": desired_velocity.to_array(),
                "grounded": movement_evidence.last_grounded,
                "pending_link_end_m": pending_link_end.map(|end| end.to_array()),
            }));
        }
        let agent = archipelago
            .get_agent_mut(agent_id)
            .ok_or("runtime Landmass agent disappeared after step")?;
        agent.position = actual_landmass;
        if pending_link_end.is_some_and(|end| {
            let difference = actual_landmass - end;
            Vec3::new(difference.x, 0.0, difference.z).length() < 0.15
        }) {
            agent.end_animation_link()?;
            pending_link_end = None;
        }
        if state == AgentState::ReachedTarget && reached_target_tick.is_none() {
            reached_target_tick = Some(tick);
        }

        drain_collision_events(
            &collision_receive,
            tick,
            actor_collider,
            static_collider,
            obstacle_collider,
            &mut event_log,
        );
    }

    let final_position = world
        .bodies
        .get(actor_body)
        .ok_or("runtime Rapier actor body disappeared at final read")?
        .translation();
    let final_feet = runtime_actor_feet(final_position);
    let difference =
        Vec3::new(final_feet.x, final_feet.y, final_feet.z) - Vec3::from_array(input.end_m);
    let final_distance_m = Vec3::new(difference.x, 0.0, difference.z).length();
    let physical_obstacle_control = run_physical_obstacle_control(static_report, obstacle)?;
    let obstacle_collision_events = event_log
        .iter()
        .filter(|event| event["target"] == "dynamic_obstacle")
        .count();
    let seam_rejections = seam_links
        .iter()
        .filter(|link| link.status != "registered_with_canonical_navmesh_point_anchors")
        .count();
    let no_link_route = before
        .report
        .routes
        .as_ref()
        .map(|routes| &routes.village_to_forest_without_links);
    Ok(json!({
        "status": if reached_target_tick.is_some() { "completed" } else { "bounded_run_target_not_reached" },
        "route_id": "canonical-village-forest",
        "landmass": {
            "agent": "landmass::Agent<bevy_landmass::coords::ThreeD>",
            "desired_velocity_source": "Archipelago::update + Agent::get_desired_velocity",
            "simulation_ticks": RUNTIME_TICKS,
            "state_counts": state_counts,
            "link_traversals": link_traversals,
            "agent_rebinds_after_partial_navmesh_updates": agent_rebinds,
            "vertical_navmesh_projection_used": false,
            "animation_link_movement": "bounded horizontal intent resolved by the same Rapier character controller; no teleport",
            "phase_samples": phase_samples,
            "seam_link_count": seam_links.len(),
            "seam_rejections": seam_rejections,
            "no_link_route": no_link_route,
            "reached_target_tick": reached_target_tick,
            "final_distance_m": difference.length(),
            "final_horizontal_distance_m": final_distance_m,
            "distance_origin": "actual Rapier capsule foot position",
            "final_actual_feet_m": [final_feet.x, final_feet.y, final_feet.z],
            "motion_trace": motion_trace,
        },
        "rapier": {
            "world": "rapier3d::PhysicsWorld",
            "static_collider": "canonical static-collider geometry",
            "actor_body": "kinematic_position_based",
            "event_source": "ChannelEventCollector::handle_collision_event",
            "collision_event_count": event_log.len(),
            "obstacle_collision_event_count": obstacle_collision_events,
            "events": event_log,
            "movement_resolution": "KinematicCharacterController::move_shape with PhysicsWorld::query_pipeline_with_filter",
            "actor_shape": "capsule_y: height 1.9 m, radius 0.35 m; Landmass receives actual foot position",
            "agent_height_m": AGENT_HEIGHT_M,
            "agent_radius_m": AGENT_RADIUS_M,
            "controller_snap_to_ground_m": 0.3,
            "controller_autostep_enabled": false,
            "movement_queries": movement_evidence.queries,
            "grounded_ticks": movement_evidence.grounded_ticks,
            "initial_kcc_grounded": movement_evidence.initial_grounded,
            "gravity_integration": "vertical velocity accumulates -9.81 * dt while airborne, resets after a grounded move; displacement = velocity * dt",
            "canonical_static_shape_contacts": movement_evidence.static_hits,
            "dynamic_obstacle_shape_contacts": movement_evidence.obstacle_hits,
            "clipped_horizontal_ticks": movement_evidence.clipped_horizontal_ticks,
            "maximum_resolved_step_m": movement_evidence.max_translation_m,
            "shape_contact_samples": movement_evidence.samples,
            "physical_obstacle_control": physical_obstacle_control,
        },
        "local_reconstruction": {
            "strategy": "forge_adapter_owned_bounded_tiles_with_explicit_border",
            "provider_native_cache_required": false,
            "measured_scope": "tile 1 only at insertion and removal; tiles 0 and 2 reused",
            "before_navmesh_hashes": before_hashes,
            "after_state_navmesh_hashes": after_hashes,
            "after_removal_navmesh_hashes": removed_hashes,
            "untouched_tiles_stable": before_hashes[0] == after_hashes[0]
                && before_hashes[2] == after_hashes[2]
                && before_hashes[0] == removed_hashes[0]
                && before_hashes[2] == removed_hashes[2],
            "tile_1_restores_baseline": before_hashes[OBSTACLE_TILE] == removed_hashes[OBSTACLE_TILE],
            "insertion": insertion,
            "removal": removal,
        },
        "water": {
            "status": "rejected",
            "reason": "the canonical v2/static-collider documents carry no water exclusion contract; the lab refuses to invent one",
        },
        "static_fixture": {
            "source_schema": static_report.schema,
            "geometry_hash": static_report.geometry_hash,
            "canonical_geometry_used_for_rapier": true,
            "metadata_substitution": false,
        },
        "no_go": true,
        "no_go_reasons": [
            "runtime corridor does not prove the complete authored interior door route",
            "water exclusion is explicitly rejected because no canonical water contract is present",
            "owner acceptance and browser execution for this campaign remain open",
        ],
    }))
}

fn runtime_actor_feet(position: Vector) -> Vector {
    position - Vector::Y * ACTOR_CENTER_HEIGHT_M
}

fn runtime_character_controller() -> KinematicCharacterController {
    KinematicCharacterController {
        offset: CharacterLength::Absolute(0.01),
        snap_to_ground: Some(CharacterLength::Absolute(0.3)),
        ..Default::default()
    }
}

#[allow(
    clippy::too_many_arguments,
    reason = "explicit handles identify each canonical collider in the evidence"
)]
fn apply_character_movement(
    world: &mut PhysicsWorld,
    actor_body: RigidBodyHandle,
    actor_collider: ColliderHandle,
    static_collider: ColliderHandle,
    obstacle_collider: Option<ColliderHandle>,
    controller: &KinematicCharacterController,
    desired: Vector,
    tick: usize,
    evidence: &mut MovementEvidence,
) -> Result<(), Box<dyn Error>> {
    let current = world
        .bodies
        .get(actor_body)
        .ok_or("character body missing")?
        .translation();
    let collider = world
        .colliders
        .get(actor_collider)
        .ok_or("character collider missing")?;
    let query = world.query_pipeline_with_filter(
        QueryFilter::default()
            .exclude_rigid_body(actor_body)
            .exclude_sensors(),
    );
    let movement = controller.move_shape(RUNTIME_DELTA_SECONDS, &query,
        collider.shape(), collider.position(), desired, |hit| {
            let label = if hit.handle == static_collider {
                evidence.static_hits += 1;
                "canonical_static_geometry"
            } else if Some(hit.handle) == obstacle_collider {
                evidence.obstacle_hits += 1;
                "dynamic_obstacle"
            } else {
                "unknown"
            };
            if evidence.samples.len() < 16 || (label == "dynamic_obstacle" && evidence.samples.len() < 32) {
                evidence.samples.push(json!({
                    "tick": tick,
                    "source": "Rapier KinematicCharacterController::move_shape CharacterCollision callback",
                    "collider": label,
                    "translation_applied_m": [hit.translation_applied.x, hit.translation_applied.y, hit.translation_applied.z],
                    "translation_remaining_m": [hit.translation_remaining.x, hit.translation_remaining.y, hit.translation_remaining.z],
                }));
            }
        });
    if !movement.translation.is_finite() {
        return Err("Rapier returned non-finite character translation".into());
    }
    evidence.queries += 1;
    evidence.initial_grounded.get_or_insert(movement.grounded);
    evidence.last_grounded = movement.grounded;
    evidence.grounded_ticks += usize::from(movement.grounded);
    evidence.max_translation_m = evidence
        .max_translation_m
        .max(movement.translation.length());
    let clipped = desired - movement.translation;
    if Vector::new(clipped.x, 0.0, clipped.z).length() > 0.02 {
        evidence.clipped_horizontal_ticks += 1;
    }
    world
        .bodies
        .get_mut(actor_body)
        .ok_or("character body missing before resolved movement")?
        .set_next_kinematic_translation(current + movement.translation);
    Ok(())
}

fn run_physical_obstacle_control(
    static_report: &StaticColliderReport,
    obstacle: &ObstacleBox,
) -> Result<Value, Box<dyn Error>> {
    let start = [
        obstacle.center_xz_m[0] - obstacle.half_width_m - 2.0,
        obstacle.top_y_m + 2.0,
        obstacle.center_xz_m[1],
    ];
    let (mut world, body, actor, static_collider) = make_runtime_physics(static_report, start)?;
    let controller = runtime_character_controller();
    let mut evidence = MovementEvidence::default();
    // Settle on the exact canonical terrain before inserting the same authored box.
    for tick in 0..30 {
        apply_character_movement(
            &mut world,
            body,
            actor,
            static_collider,
            None,
            &controller,
            -Vector::Y * 9.81 * RUNTIME_DELTA_SECONDS * RUNTIME_DELTA_SECONDS,
            tick,
            &mut evidence,
        )?;
        world.step();
    }
    let obstacle_handle = world.colliders.insert(
        ColliderBuilder::cuboid(
            obstacle.half_width_m,
            (obstacle.top_y_m - obstacle.bottom_y_m) * 0.5,
            obstacle.half_depth_m,
        )
        .translation(Vector::new(
            obstacle.center_xz_m[0],
            obstacle.top_y_m.midpoint(obstacle.bottom_y_m),
            obstacle.center_xz_m[1],
        ))
        .build(),
    );
    world.step();
    let before = world.bodies[body].translation();
    let desired = Vector::new(4.0, -9.81 * RUNTIME_DELTA_SECONDS, 0.0) * RUNTIME_DELTA_SECONDS;
    for tick in 30..120 {
        apply_character_movement(
            &mut world,
            body,
            actor,
            static_collider,
            Some(obstacle_handle),
            &controller,
            desired,
            tick,
            &mut evidence,
        )?;
        world.step();
    }
    let blocked = world.bodies[body].translation();
    let obstacle_shape_contacts = evidence.obstacle_hits;
    let blocked_before_boundary =
        blocked.x <= obstacle.center_xz_m[0] - obstacle.half_width_m - AGENT_RADIUS_M + 0.05;
    world
        .colliders
        .remove(obstacle_handle, &mut world.islands, &mut world.bodies, true);
    world.step();
    for tick in 120..360 {
        apply_character_movement(
            &mut world,
            body,
            actor,
            static_collider,
            None,
            &controller,
            desired,
            tick,
            &mut evidence,
        )?;
        world.step();
    }
    let final_position = world.bodies[body].translation();
    let advance_after_removal_m = final_position.x - blocked.x;
    let passed = obstacle_shape_contacts > 0
        && blocked_before_boundary
        && advance_after_removal_m > obstacle.half_width_m * 2.0 + AGENT_RADIUS_M;
    Ok(json!({
        "purpose": "negative control: identical horizontal intent must collide with the inserted canonical box and advance after removal",
        "canonical_geometry_hash": static_report.geometry_hash,
        "canonical_geometry_used_without_rewrite": true,
        "obstacle": obstacle,
        "insert_tick": 30, "remove_tick": 120, "ticks": 360,
        "start_position_m": [before.x, before.y, before.z],
        "blocked_position_m": [blocked.x, blocked.y, blocked.z],
        "final_position_m": [final_position.x, final_position.y, final_position.z],
        "dynamic_obstacle_shape_contacts": obstacle_shape_contacts,
        "blocked_before_boundary": blocked_before_boundary,
        "advance_after_removal_m": advance_after_removal_m,
        "grounded_ticks": evidence.grounded_ticks,
        "maximum_resolved_step_m": evidence.max_translation_m,
        "shape_contact_samples": evidence.samples,
        "passed": passed,
    }))
}

fn rebind_runtime_agent(
    archipelago: &mut Archipelago<ThreeD>,
    old_agent_id: AgentId,
    position: Vector,
    target: [f32; 3],
) -> AgentId {
    archipelago.remove_agent(old_agent_id);
    let mut agent = Agent::<ThreeD>::create(
        Vec3::new(position.x, position.y, position.z),
        Vec3::ZERO,
        AGENT_RADIUS_M,
        4.0,
        5.0,
    );
    agent.current_target = Some(Vec3::from_array(target));
    agent.permitted_animation_links = PermittedAnimationLinks::All;
    archipelago.add_agent(agent)
}

type RuntimeArchipelago = (
    Archipelago<ThreeD>,
    Vec<IslandId>,
    AgentId,
    Vec<SeamLinkReport>,
);

fn make_runtime_archipelago(
    tiles: &[BuiltTile],
    start_m: [f32; 3],
    end_m: [f32; 3],
) -> Result<RuntimeArchipelago, Box<dyn Error>> {
    let mut options = ArchipelagoOptions::<ThreeD>::from_agent_radius(AGENT_RADIUS_M);
    options.point_sample_distance = generous_sample_distance();
    let mut archipelago = Archipelago::new(options);
    let mut island_ids = Vec::with_capacity(tiles.len());
    for tile in tiles {
        let raw = convert_rerecast_navmesh_to_landmass_navmesh(&tile.navmesh);
        let valid = raw.validate().map_err(|error| {
            format!(
                "tile {} rejected by Landmass validation: {error}",
                tile.index
            )
        })?;
        island_ids.push(archipelago.add_island(Island::new(Transform::default(), Arc::new(valid))));
    }
    let seam_links = seam_links(tiles, Vec3::from_array(start_m), Vec3::from_array(end_m));
    if seam_links
        .iter()
        .any(|link| link.status != "registered_with_canonical_navmesh_point_anchors")
    {
        return Err("canonical seam anchor rejected by Landmass runtime setup".into());
    }
    for link in &seam_links {
        archipelago.add_animation_link(AnimationLink::<ThreeD> {
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
        });
    }
    let mut agent = Agent::<ThreeD>::create(
        Vec3::from_array(start_m),
        Vec3::ZERO,
        AGENT_RADIUS_M,
        4.0,
        5.0,
    );
    agent.current_target = Some(Vec3::from_array(end_m));
    agent.permitted_animation_links = PermittedAnimationLinks::All;
    let agent_id = archipelago.add_agent(agent);
    archipelago.update(RUNTIME_DELTA_SECONDS);
    Ok((archipelago, island_ids, agent_id, seam_links))
}

fn make_runtime_physics(
    static_report: &StaticColliderReport,
    start_m: [f32; 3],
) -> Result<
    (
        PhysicsWorld,
        RigidBodyHandle,
        ColliderHandle,
        ColliderHandle,
    ),
    Box<dyn Error>,
> {
    let mut world = PhysicsWorld::new();
    world.integration_parameters.dt = RUNTIME_DELTA_SECONDS;
    let static_body = world.insert_body(RigidBodyBuilder::fixed());
    let vertices = static_report
        .canonical_geometry
        .positions_m
        .iter()
        .map(|[x, y, z]| Vector::new(*x, *y, *z))
        .collect::<Vec<_>>();
    let static_collider =
        ColliderBuilder::trimesh(vertices, static_report.canonical_geometry.triangles.clone())?
            .active_events(ActiveEvents::COLLISION_EVENTS)
            .build();
    let static_collider =
        world
            .colliders
            .insert_with_parent(static_collider, static_body, &mut world.bodies);
    world.step();
    let (lowest_y, highest_y) = static_report
        .canonical_geometry
        .positions_m
        .iter()
        .map(|point| point[1])
        .fold((f32::INFINITY, f32::NEG_INFINITY), |(low, high), y| {
            (low.min(y), high.max(y))
        });
    let ray_origin = Vector::new(start_m[0], highest_y + AGENT_HEIGHT_M + 1.0, start_m[2]);
    let ray = Ray::new(ray_origin, -Vector::Y);
    let (support, distance) = world
        .cast_ray(
            &ray,
            highest_y - lowest_y + AGENT_HEIGHT_M + 2.0,
            true,
            QueryFilter::default(),
        )
        .ok_or("canonical geometry provides no Rapier support at actor start")?;
    if support != static_collider {
        return Err("actor start ray did not hit the canonical static collider".into());
    }
    let start_y = ray_origin.y - distance + ACTOR_CENTER_HEIGHT_M + 0.02;
    let actor_body = world.bodies.insert(
        RigidBodyBuilder::kinematic_position_based()
            .translation(Vector::new(start_m[0], start_y, start_m[2])),
    );
    let actor_collider = world.colliders.insert_with_parent(
        ColliderBuilder::capsule_y(ACTOR_CENTER_HEIGHT_M - AGENT_RADIUS_M, AGENT_RADIUS_M)
            .active_events(ActiveEvents::COLLISION_EVENTS),
        actor_body,
        &mut world.bodies,
    );
    world.step();
    Ok((world, actor_body, actor_collider, static_collider))
}

fn rebuild_runtime_tile(
    tile: &TileGeometry,
    obstacle: Option<&ObstacleBox>,
) -> Result<(Navmesh, String, String, f64), Box<dyn Error>> {
    let geometry = match obstacle {
        Some(obstacle) => append_obstacle(&tile.geometry, obstacle)?,
        None => tile.geometry.clone(),
    };
    let geometry_hash = mesh_hash(&geometry.positions, &geometry.triangles)?;
    let mut settings = provider::settings_for_agent(AGENT_RADIUS_M, AGENT_HEIGHT_M);
    settings.tiling = true;
    settings.tile_size = TILE_SIZE_CELLS;
    let started = Instant::now();
    let navmesh = provider::generate_navmesh(geometry.mesh, settings)?;
    let generation_ms = started.elapsed().as_secs_f64() * 1000.0;
    let navmesh_hash = canonical_json_sha256(&navmesh)?.to_string();
    Ok((navmesh, geometry_hash, navmesh_hash, generation_ms))
}

fn replace_runtime_island(
    archipelago: &mut Archipelago<ThreeD>,
    island_id: IslandId,
    navmesh: &Navmesh,
) -> Result<(), Box<dyn Error>> {
    let raw = convert_rerecast_navmesh_to_landmass_navmesh(navmesh);
    let valid = raw
        .validate()
        .map_err(|error| format!("runtime partial tile rejected by Landmass: {error}"))?;
    let mut island = archipelago
        .get_island_mut(island_id)
        .ok_or("runtime partial tile island disappeared")?;
    island.set_nav_mesh(Arc::new(valid));
    Ok(())
}

fn drain_collision_events(
    receiver: &std::sync::mpsc::Receiver<CollisionEvent>,
    tick: usize,
    actor: ColliderHandle,
    static_collider: ColliderHandle,
    obstacle: Option<ColliderHandle>,
    events: &mut Vec<Value>,
) {
    while let Ok(event) = receiver.try_recv() {
        let (left, right, kind, flags) = match event {
            CollisionEvent::Started(left, right, flags) => (left, right, "started", flags),
            CollisionEvent::Stopped(left, right, flags) => (left, right, "stopped", flags),
        };
        let label = |handle: ColliderHandle| {
            if handle == actor {
                "npc_actor"
            } else if handle == static_collider {
                "canonical_static_collider"
            } else if obstacle == Some(handle) {
                "dynamic_obstacle"
            } else {
                "unknown_collider"
            }
        };
        events.push(json!({
            "tick": tick,
            "kind": kind,
            "source": label(left),
            "target": label(right),
            "flags": format!("{flags:?}"),
        }));
    }
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
