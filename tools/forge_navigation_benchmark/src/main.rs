mod geometry;
mod navigation;
mod provider;

use std::{
    env,
    ffi::OsString,
    fs::{self, OpenOptions},
    io::Write as _,
    path::{Path, PathBuf},
    time::Instant,
};

use bevy_math::Vec3;
use bevy_rerecast::{Navmesh, rerecast::TriMesh};
use forge_core::{CanonicalJsonError, ContentHash, canonical_json_sha256};
use forge_world::{
    TerrainGenerationPlan, WorldSketchDocument, compile_world_sketch,
    embedded_visual_valley_player_controller_profile,
};
use navigation::{
    AvoidanceProbe, NavigationError, PathProbe, PolygonWindingDiagnostic, WindingNormalization,
};
use provider::ProviderError;
use serde::Serialize;
use thiserror::Error;

const REPORT_SCHEMA: &str = "forge.navigation-provider-benchmark";
const REPORT_SCHEMA_VERSION: u32 = 1;
const LOCAL_EXTERIOR_HALF_EXTENT_M: f32 = 48.0;
const GLOBAL_EXTERIOR_HALF_EXTENT_M: f32 = 256.0;
const REPRODUCIBILITY_RUNS: usize = 3;
const RERECAST_VERSION: &str = "0.4.0";
const BEVY_RERECAST_VERSION: &str = "0.5.0";
const LANDMASS_VERSION: &str = "0.9.2";
const BEVY_LANDMASS_VERSION: &str = "0.12.0";
const LANDMASS_RERECAST_VERSION: &str = "0.3.0";
const BINCODE_ADVISORY: &str = "RUSTSEC-2025-0141";

fn main() -> Result<(), BenchmarkError> {
    let arguments = Arguments::parse(env::args_os())?;
    let report = run_benchmark(&arguments.world_sketch)?;
    write_report(&arguments.output, &report)?;
    println!(
        "navigation benchmark: status={}, report={}",
        report.selection.status,
        arguments.output.display()
    );
    if let Some(error) = report.global_valley.detail_height_mesh_path.error {
        return Err(BenchmarkError::GlobalNavigationRejected(error));
    }
    Ok(())
}

#[allow(
    clippy::too_many_lines,
    reason = "the benchmark keeps its ordered contract probes in one auditable scenario"
)]
fn run_benchmark(world_sketch_path: &Path) -> Result<BenchmarkReport, BenchmarkError> {
    let json = fs::read_to_string(world_sketch_path)?;
    let sketch = WorldSketchDocument::from_json(&json)
        .map_err(|error| BenchmarkError::WorldSketch(error.to_string()))?;
    let source_world_sketch_sha256 = sketch
        .content_hash()
        .map_err(|error| BenchmarkError::WorldSketch(error.to_string()))?;
    let compiled = compile_world_sketch(&sketch)
        .map_err(|error| BenchmarkError::WorldSketch(error.to_string()))?;
    let terrain = TerrainGenerationPlan::from_document(&compiled.carved_terrain)
        .map_err(|error| BenchmarkError::Terrain(error.to_string()))?
        .generate()
        .map_err(|error| BenchmarkError::Terrain(error.to_string()))?;
    let bridge = compiled
        .bridge_placements
        .placements
        .first()
        .ok_or(BenchmarkError::MissingBridge)?;
    let controller = embedded_visual_valley_player_controller_profile()
        .map_err(|error| BenchmarkError::Controller(error.to_string()))?;
    let radius = controller.controller.capsule_radius_m;
    let height = controller.controller.total_capsule_height_m();

    let exterior_input = geometry::exterior_bridge_mesh(
        &terrain,
        &compiled.traversal_audit,
        bridge,
        LOCAL_EXTERIOR_HALF_EXTENT_M,
        true,
    )?;
    let exterior_runs = repeated_generation(
        &exterior_input,
        &provider::settings_for_agent(radius, height),
    )?;
    let exterior_navmesh = exterior_runs
        .navmesh
        .as_ref()
        .ok_or(BenchmarkError::MissingProviderOutput)?;
    let exterior_path = navigation::query_rerecast_path(
        exterior_navmesh,
        Vec3::from_array(bridge.deck_start_surface_m),
        Vec3::from_array(bridge.deck_end_surface_m),
        radius,
    )?;
    let exterior = ExteriorBridgeProbe {
        crop_size_m: LOCAL_EXTERIOR_HALF_EXTENT_M * 2.0,
        source_triangle_count: exterior_input.indices.len(),
        output_polygon_count: exterior_navmesh.polygon.polygon_count(),
        output_detail_triangle_count: exterior_navmesh.detail.triangles.len(),
        generation_duration_us: exterior_runs.duration_us,
        output_hashes: exterior_runs.hashes.clone(),
        deterministic: exterior_runs
            .hashes
            .windows(2)
            .all(|pair| pair[0] == pair[1]),
        path: exterior_path,
        without_bridge_path: {
            let without_bridge_input = geometry::exterior_bridge_mesh(
                &terrain,
                &compiled.traversal_audit,
                bridge,
                LOCAL_EXTERIOR_HALF_EXTENT_M,
                false,
            )?;
            let without_bridge_run = single_generation(
                &without_bridge_input,
                provider::settings_for_agent(radius, height),
            )?;
            navigation::query_rerecast_path(
                &without_bridge_run.navmesh,
                Vec3::from_array(bridge.deck_start_surface_m),
                Vec3::from_array(bridge.deck_end_surface_m),
                radius,
            )?
        },
    };

    let global_input = geometry::exterior_bridge_mesh(
        &terrain,
        &compiled.traversal_audit,
        bridge,
        GLOBAL_EXTERIOR_HALF_EXTENT_M,
        true,
    )?;
    let global_runs =
        repeated_generation(&global_input, &provider::settings_for_agent(radius, height))?;
    let global_navmesh = global_runs
        .navmesh
        .as_ref()
        .ok_or(BenchmarkError::MissingProviderOutput)?;
    let global_detail_path = PathProbeOutcome::capture(navigation::query_rerecast_path(
        global_navmesh,
        Vec3::from_array(bridge.deck_start_surface_m),
        Vec3::from_array(bridge.deck_end_surface_m),
        radius,
    ));
    let global_polygon_only_fallback =
        PathProbeOutcome::capture(navigation::query_rerecast_path_without_height_mesh(
            global_navmesh,
            Vec3::from_array(bridge.deck_start_surface_m),
            Vec3::from_array(bridge.deck_end_surface_m),
            radius,
        ));
    let global_normalized_winding = match navigation::normalize_rerecast_winding(global_navmesh) {
        Ok((normalization, raw)) => NormalizedPathProbeOutcome::capture(
            normalization,
            navigation::query_raw_path(
                raw,
                Vec3::from_array(bridge.deck_start_surface_m),
                Vec3::from_array(bridge.deck_end_surface_m),
                radius,
            ),
        ),
        Err(error) => NormalizedPathProbeOutcome::normalization_failed(&error),
    };
    let global_valley = GlobalValleyProbe {
        canonical_geometry_hash: canonical_json_sha256(&(
            global_input
                .vertices
                .iter()
                .map(bevy_math::Vec3A::to_array)
                .collect::<Vec<_>>(),
            global_input
                .indices
                .iter()
                .map(bevy_math::UVec3::to_array)
                .collect::<Vec<_>>(),
        ))?,
        extent_m: GLOBAL_EXTERIOR_HALF_EXTENT_M * 2.0,
        source_triangle_count: global_input.indices.len(),
        output_polygon_count: global_navmesh.polygon.polygon_count(),
        output_detail_triangle_count: global_navmesh.detail.triangles.len(),
        generation_duration_us: global_runs.duration_us,
        output_hashes: global_runs.hashes.clone(),
        deterministic: global_runs.hashes.windows(2).all(|pair| pair[0] == pair[1]),
        detail_height_mesh_path: global_detail_path,
        polygon_only_fallback_path: global_polygon_only_fallback,
        polygon_only_fallback_accepted: false,
        normalized_winding_path: global_normalized_winding,
        winding_normalization_accepted: false,
        invalid_polygon_winding: navigation::invalid_polygon_winding(global_navmesh),
    };

    let open_input = geometry::interior_mesh(true, 2.0)?;
    let open_run = single_generation(&open_input, provider::settings_for_agent(radius, height))?;
    let open_path = navigation::query_rerecast_path(
        &open_run.navmesh,
        Vec3::new(-8.0, 0.0, 0.0),
        Vec3::new(8.0, 0.0, 0.0),
        radius,
    )?;
    let closed_input = geometry::interior_mesh(false, 2.0)?;
    let closed_run =
        single_generation(&closed_input, provider::settings_for_agent(radius, height))?;
    let closed_path = navigation::query_rerecast_path(
        &closed_run.navmesh,
        Vec3::new(-8.0, 0.0, 0.0),
        Vec3::new(8.0, 0.0, 0.0),
        radius,
    )?;
    let reopened_run =
        single_generation(&open_input, provider::settings_for_agent(radius, height))?;
    let reopened_path = navigation::query_rerecast_path(
        &reopened_run.navmesh,
        Vec3::new(-8.0, 0.0, 0.0),
        Vec3::new(8.0, 0.0, 0.0),
        radius,
    )?;
    let door = DoorRegenerationProbe {
        open_path,
        closed_path,
        reopened_path,
        open_generation_us: open_run.duration_us,
        closed_full_regeneration_us: closed_run.duration_us,
        reopened_full_regeneration_us: reopened_run.duration_us,
        reopened_matches_open: open_run.hash == reopened_run.hash,
        partial_regeneration_available: false,
    };

    let narrow_input = geometry::interior_mesh(true, 1.2)?;
    let small_radius = 0.25;
    let small_run = single_generation(
        &narrow_input,
        provider::settings_for_agent(small_radius, height),
    )?;
    let small_path = navigation::query_rerecast_path(
        &small_run.navmesh,
        Vec3::new(-8.0, 0.0, 0.0),
        Vec3::new(8.0, 0.0, 0.0),
        small_radius,
    )?;
    let large_radius = 0.65;
    let large_run = single_generation(
        &narrow_input,
        provider::settings_for_agent(large_radius, height),
    )?;
    let large_path = navigation::query_rerecast_path(
        &large_run.navmesh,
        Vec3::new(-8.0, 0.0, 0.0),
        Vec3::new(8.0, 0.0, 0.0),
        large_radius,
    )?;
    let agent_profiles = AgentProfilesProbe {
        doorway_width_m: 1.2,
        small_radius_m: small_radius,
        small_path,
        large_radius_m: large_radius,
        large_path,
    };

    let (without_link, with_link) = navigation::off_mesh_link_probe()?;
    let off_mesh_link = OffMeshLinkProbe {
        without_link,
        with_link,
    };
    let area_cost = navigation::area_cost_probe()?;
    let area_cost_prefers_inexpensive_route = area_cost.found
        && area_cost.waypoints_m.iter().any(|point| point[2] <= -0.99)
        && area_cost.waypoints_m.iter().all(|point| point[2] <= 0.01);
    let local_avoidance = navigation::local_avoidance_probe(&open_run.navmesh)?;
    let capabilities = ProviderCapabilities {
        asynchronous_bevy_generation: true,
        debug_visualization: true,
        area_costs: true,
        off_mesh_links: true,
        local_avoidance: true,
        multiple_agent_profiles: true,
        full_regeneration: true,
        native_tiled_generation: false,
        partial_regeneration: false,
        dynamic_obstacle_carving: false,
        maintained_rapier_backend: false,
    };
    let bincode_present_in_lab_lock = include_str!("../Cargo.lock").contains("name = \"bincode\"");
    let governance = ProviderGovernance {
        license_policy_passed: true,
        advisory_policy_passed: false,
        blocking_advisories: if bincode_present_in_lab_lock {
            vec![format!(
                "{BINCODE_ADVISORY}: bevy_rerecast_core 0.5.0 depends on unmaintained bincode 2.0.1"
            )]
        } else {
            Vec::new()
        },
    };

    let all_non_global_feature_probes_passed = exterior.deterministic
        && exterior.path.found
        && !exterior.without_bridge_path.found
        && door.open_path.found
        && !door.closed_path.found
        && door.reopened_path.found
        && door.reopened_matches_open
        && agent_profiles.small_path.found
        && !agent_profiles.large_path.found
        && !off_mesh_link.without_link.found
        && off_mesh_link.with_link.found
        && off_mesh_link.with_link.animation_link_count == 1
        && area_cost_prefers_inexpensive_route
        && local_avoidance.reached_targets
        && local_avoidance.maximum_lateral_speed_mps > 0.01
        && local_avoidance.minimum_separation_m >= radius * 1.8;
    let all_required_contract_probes_passed = all_non_global_feature_probes_passed
        && global_valley.deterministic
        && global_valley
            .detail_height_mesh_path
            .path
            .as_ref()
            .is_some_and(|path| path.found)
        && governance.advisory_policy_passed
        && capabilities.asynchronous_bevy_generation
        && capabilities.debug_visualization
        && capabilities.area_costs
        && capabilities.off_mesh_links
        && capabilities.local_avoidance
        && capabilities.multiple_agent_profiles
        && capabilities.full_regeneration
        && capabilities.native_tiled_generation
        && capabilities.partial_regeneration
        && capabilities.dynamic_obstacle_carving
        && capabilities.maintained_rapier_backend;

    let mut blocking_gaps = vec![
        "Rerecast 0.4/Bevy Rerecast 0.5 does not provide native tiled generation".to_owned(),
        "Bevy Rerecast 0.5 only performs full regeneration".to_owned(),
        "the candidate has no native tile-cache or dynamic-obstacle carving path".to_owned(),
        "the maintained upstream physics backend targets Avian, while Forge uses Rapier".to_owned(),
        "fresh external dependency-policy evidence and explicit owner acceptance are required; absence of bincode in the lab lock is not an audit".to_owned(),
    ];
    if bincode_present_in_lab_lock {
        blocking_gaps.push(format!(
            "unmaintained bincode remains in the lab lock ({BINCODE_ADVISORY})"
        ));
    }
    if let Some(error) = &global_valley.detail_height_mesh_path.error {
        blocking_gaps.push(format!(
            "the full 512 m Landmass detail-mesh conversion is rejected: {error}"
        ));
    }

    Ok(BenchmarkReport {
        schema: REPORT_SCHEMA,
        schema_version: REPORT_SCHEMA_VERSION,
        source_world_sketch_sha256,
        source_carved_terrain_sha256: compiled.report.carved_terrain_sha256,
        source_bridge_placements_sha256: compiled.report.bridge_placements_sha256,
        provider: ProviderStack {
            generation: format!(
                "rerecast {RERECAST_VERSION} / bevy_rerecast {BEVY_RERECAST_VERSION}"
            ),
            navigation: format!(
                "landmass {LANDMASS_VERSION} / bevy_landmass {BEVY_LANDMASS_VERSION}"
            ),
            bridge: format!("landmass_rerecast {LANDMASS_RERECAST_VERSION}"),
            license: "MIT OR Apache-2.0",
            cross_platform_determinism_feature: "bevy_rerecast/libm",
        },
        governance,
        exterior_bridge: exterior,
        global_valley,
        door_regeneration: door,
        agent_profiles,
        off_mesh_link,
        area_cost,
        area_cost_prefers_inexpensive_route,
        local_avoidance,
        capabilities,
        all_non_global_feature_probes_passed,
        all_required_contract_probes_passed,
        selection: Selection {
            status: "blocked_pending_contract_and_governance_gaps",
            preferred_stack: "Rerecast + Landmass",
            blocking_gaps,
        },
    })
}

fn repeated_generation(
    input: &TriMesh,
    settings: &bevy_rerecast::NavmeshSettings,
) -> Result<RepeatedGeneration, BenchmarkError> {
    let mut hashes = Vec::with_capacity(REPRODUCIBILITY_RUNS);
    let mut duration_us = Vec::with_capacity(REPRODUCIBILITY_RUNS);
    let mut first_navmesh = None;
    for _ in 0..REPRODUCIBILITY_RUNS {
        let run = single_generation(input, settings.clone())?;
        hashes.push(run.hash);
        duration_us.push(run.duration_us);
        if first_navmesh.is_none() {
            first_navmesh = Some(run.navmesh);
        }
    }
    Ok(RepeatedGeneration {
        hashes,
        duration_us,
        navmesh: first_navmesh,
    })
}

fn single_generation(
    input: &TriMesh,
    settings: bevy_rerecast::NavmeshSettings,
) -> Result<GenerationRun, BenchmarkError> {
    let started = Instant::now();
    let navmesh = provider::generate_navmesh(input.clone(), settings)?;
    let duration_us = started.elapsed().as_micros();
    let hash = canonical_json_sha256(&navmesh)?;
    Ok(GenerationRun {
        navmesh,
        hash,
        duration_us,
    })
}

fn write_report(path: &Path, report: &BenchmarkReport) -> Result<(), BenchmarkError> {
    let mut file = OpenOptions::new().write(true).create_new(true).open(path)?;
    let json = serde_json::to_string_pretty(report)?;
    file.write_all(json.as_bytes())?;
    file.write_all(b"\n")?;
    Ok(())
}

struct Arguments {
    world_sketch: PathBuf,
    output: PathBuf,
}

impl Arguments {
    fn parse(arguments: impl IntoIterator<Item = OsString>) -> Result<Self, BenchmarkError> {
        let mut arguments = arguments.into_iter();
        let _program = arguments.next();
        let world_sketch = arguments.next().ok_or(BenchmarkError::Usage)?;
        let output = arguments.next().ok_or(BenchmarkError::Usage)?;
        if arguments.next().is_some() {
            return Err(BenchmarkError::Usage);
        }
        Ok(Self {
            world_sketch: PathBuf::from(world_sketch),
            output: PathBuf::from(output),
        })
    }
}

struct GenerationRun {
    navmesh: Navmesh,
    hash: ContentHash,
    duration_us: u128,
}

struct RepeatedGeneration {
    hashes: Vec<ContentHash>,
    duration_us: Vec<u128>,
    navmesh: Option<Navmesh>,
}

#[derive(Serialize)]
struct BenchmarkReport {
    schema: &'static str,
    schema_version: u32,
    source_world_sketch_sha256: ContentHash,
    source_carved_terrain_sha256: ContentHash,
    source_bridge_placements_sha256: ContentHash,
    provider: ProviderStack,
    governance: ProviderGovernance,
    exterior_bridge: ExteriorBridgeProbe,
    global_valley: GlobalValleyProbe,
    door_regeneration: DoorRegenerationProbe,
    agent_profiles: AgentProfilesProbe,
    off_mesh_link: OffMeshLinkProbe,
    area_cost: PathProbe,
    area_cost_prefers_inexpensive_route: bool,
    local_avoidance: AvoidanceProbe,
    capabilities: ProviderCapabilities,
    all_non_global_feature_probes_passed: bool,
    all_required_contract_probes_passed: bool,
    selection: Selection,
}

#[derive(Serialize)]
struct ProviderStack {
    generation: String,
    navigation: String,
    bridge: String,
    license: &'static str,
    cross_platform_determinism_feature: &'static str,
}

#[derive(Serialize)]
struct ProviderGovernance {
    license_policy_passed: bool,
    advisory_policy_passed: bool,
    blocking_advisories: Vec<String>,
}

#[derive(Serialize)]
struct ExteriorBridgeProbe {
    crop_size_m: f32,
    source_triangle_count: usize,
    output_polygon_count: usize,
    output_detail_triangle_count: usize,
    generation_duration_us: Vec<u128>,
    output_hashes: Vec<ContentHash>,
    deterministic: bool,
    path: PathProbe,
    without_bridge_path: PathProbe,
}

#[derive(Serialize)]
struct GlobalValleyProbe {
    canonical_geometry_hash: ContentHash,
    extent_m: f32,
    source_triangle_count: usize,
    output_polygon_count: usize,
    output_detail_triangle_count: usize,
    generation_duration_us: Vec<u128>,
    output_hashes: Vec<ContentHash>,
    deterministic: bool,
    detail_height_mesh_path: PathProbeOutcome,
    polygon_only_fallback_path: PathProbeOutcome,
    polygon_only_fallback_accepted: bool,
    normalized_winding_path: NormalizedPathProbeOutcome,
    winding_normalization_accepted: bool,
    invalid_polygon_winding: Vec<PolygonWindingDiagnostic>,
}

#[derive(Serialize)]
struct PathProbeOutcome {
    path: Option<PathProbe>,
    error: Option<String>,
}

impl PathProbeOutcome {
    fn capture(result: Result<PathProbe, NavigationError>) -> Self {
        match result {
            Ok(path) => Self {
                path: Some(path),
                error: None,
            },
            Err(error) => Self {
                path: None,
                error: Some(error.to_string()),
            },
        }
    }
}

#[derive(Serialize)]
struct NormalizedPathProbeOutcome {
    normalization: Option<WindingNormalization>,
    path: Option<PathProbe>,
    error: Option<String>,
}

impl NormalizedPathProbeOutcome {
    fn capture(
        normalization: WindingNormalization,
        result: Result<PathProbe, NavigationError>,
    ) -> Self {
        match result {
            Ok(path) => Self {
                normalization: Some(normalization),
                path: Some(path),
                error: None,
            },
            Err(error) => Self {
                normalization: Some(normalization),
                path: None,
                error: Some(error.to_string()),
            },
        }
    }

    fn normalization_failed(error: &NavigationError) -> Self {
        Self {
            normalization: None,
            path: None,
            error: Some(error.to_string()),
        }
    }
}

#[derive(Serialize)]
struct DoorRegenerationProbe {
    open_path: PathProbe,
    closed_path: PathProbe,
    reopened_path: PathProbe,
    open_generation_us: u128,
    closed_full_regeneration_us: u128,
    reopened_full_regeneration_us: u128,
    reopened_matches_open: bool,
    partial_regeneration_available: bool,
}

#[derive(Serialize)]
struct AgentProfilesProbe {
    doorway_width_m: f32,
    small_radius_m: f32,
    small_path: PathProbe,
    large_radius_m: f32,
    large_path: PathProbe,
}

#[derive(Serialize)]
struct OffMeshLinkProbe {
    without_link: PathProbe,
    with_link: PathProbe,
}

#[derive(Serialize)]
#[allow(
    clippy::struct_excessive_bools,
    reason = "the serialized capability matrix preserves independent contract fields"
)]
struct ProviderCapabilities {
    asynchronous_bevy_generation: bool,
    debug_visualization: bool,
    area_costs: bool,
    off_mesh_links: bool,
    local_avoidance: bool,
    multiple_agent_profiles: bool,
    full_regeneration: bool,
    native_tiled_generation: bool,
    partial_regeneration: bool,
    dynamic_obstacle_carving: bool,
    maintained_rapier_backend: bool,
}

#[derive(Serialize)]
struct Selection {
    status: &'static str,
    preferred_stack: &'static str,
    blocking_gaps: Vec<String>,
}

#[derive(Debug, Error)]
enum BenchmarkError {
    #[error("usage: forge_navigation_benchmark <world-sketch.v1.json> <new-report-path.json>")]
    Usage,
    #[error("I/O error: {0}")]
    Io(#[from] std::io::Error),
    #[error("invalid WorldSketch input: {0}")]
    WorldSketch(String),
    #[error("cannot generate carved terrain: {0}")]
    Terrain(String),
    #[error("Visual Valley compilation did not produce a bridge placement")]
    MissingBridge,
    #[error("cannot load the player-controller contract: {0}")]
    Controller(String),
    #[error(transparent)]
    Provider(#[from] ProviderError),
    #[error(transparent)]
    Geometry(#[from] geometry::GeometryError),
    #[error(transparent)]
    Navigation(#[from] NavigationError),
    #[error("cannot hash provider output: {0}")]
    CanonicalJson(#[from] CanonicalJsonError),
    #[error("cannot serialize benchmark report: {0}")]
    Json(#[from] serde_json::Error),
    #[error("provider did not return a navigation mesh")]
    MissingProviderOutput,
    #[error("raw global navigation rejected; report preserved: {0}")]
    GlobalNavigationRejected(String),
}
