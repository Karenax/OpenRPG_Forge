#![deny(unsafe_code)]

//! Browser replay entry point for the isolated canonical navigation probe.
//!
//! The input is the native probe's exported canonical geometry. The replay keeps the same
//! provider and Landmass calls as the native probe and deliberately does not normalize winding.

#[allow(
    dead_code,
    reason = "the browser replay shares the path query while native-only probes remain in the laboratory binary"
)]
mod navigation;
mod provider;

use bevy_math::{UVec3, Vec3, Vec3A};
use bevy_rerecast::rerecast::{AreaType, TriMesh};
use forge_core::{AreaId, CanonicalJsonError, ContentHash, canonical_json_sha256};
use serde::{Deserialize, Serialize};
use thiserror::Error;

const AGENT_RADIUS_M: f32 = 0.35;
const AGENT_HEIGHT_M: f32 = 1.8;
const NATIVE_SCHEMA: &str = "forge.navigation.canonical-terrain-probe";
const SELECTION_STATUS: &str = "blocked_pending_full_world_contract";

#[derive(Debug, Deserialize)]
struct CanonicalTerrainReport {
    schema: String,
    version: u32,
    area_id: AreaId,
    source_hash: ContentHash,
    terrain_hash: ContentHash,
    raster_hash: ContentHash,
    geometry_hash: ContentHash,
    crop_xz_m: [f64; 4],
    canonical_geometry: CanonicalGeometry,
    start_m: [f32; 3],
    end_m: [f32; 3],
    selection: Selection,
}

#[derive(Debug, Deserialize)]
struct CanonicalGeometry {
    positions_m: Vec<[f32; 3]>,
    triangles: Vec<[u32; 3]>,
    geometry_hash: ContentHash,
    source_hash: ContentHash,
    terrain_hash: ContentHash,
    raster_hash: ContentHash,
    crop_xz_m: [f64; 4],
}

#[derive(Clone, Debug, Deserialize, Serialize)]
struct Selection {
    status: String,
}

#[derive(Debug, Serialize)]
struct BrowserProbeReport {
    schema: &'static str,
    version: u32,
    runtime: &'static str,
    input_schema: &'static str,
    area_id: AreaId,
    source_hash: ContentHash,
    terrain_hash: ContentHash,
    raster_hash: ContentHash,
    geometry_hash: ContentHash,
    crop_xz_m: [f64; 4],
    vertices: usize,
    triangles: usize,
    geometry_hash_verified: bool,
    triangle_indices_verified: bool,
    provider: ProviderDescription,
    navmesh_hash: ContentHash,
    landmass_path_found: bool,
    path: Result<navigation::PathProbe, String>,
    selection: Selection,
    missing_proofs: [&'static str; 7],
}

#[derive(Debug, Serialize)]
struct ProviderDescription {
    rerecast: &'static str,
    bevy_rerecast: &'static str,
    landmass: &'static str,
    agent_radius_m: f32,
    agent_height_m: f32,
}

#[derive(Debug, Error)]
enum BrowserProbeError {
    #[error("canonical report JSON is invalid: {0}")]
    Json(#[from] serde_json::Error),
    #[error("canonical report hash failed: {0}")]
    Hash(#[from] CanonicalJsonError),
    #[error("canonical report is invalid: {0}")]
    Invalid(String),
    #[error("Rerecast/Landmass replay failed: {0}")]
    Replay(String),
}

/// Replays the exported native report and returns a compact, verifiable JSON result.
pub fn replay_canonical_report_json(input_json: &str) -> Result<String, String> {
    replay_canonical_report(input_json)
        .and_then(|report| serde_json::to_string(&report).map_err(BrowserProbeError::Json))
        .map_err(|error| error.to_string())
}

#[cfg(target_arch = "wasm32")]
#[wasm_bindgen::prelude::wasm_bindgen]
pub fn run_canonical_browser_probe(input_json: &str) -> Result<String, wasm_bindgen::JsValue> {
    replay_canonical_report_json(input_json)
        .map_err(|error| wasm_bindgen::JsValue::from_str(&error))
}

fn replay_canonical_report(input_json: &str) -> Result<BrowserProbeReport, BrowserProbeError> {
    let input: CanonicalTerrainReport = serde_json::from_str(input_json)?;
    let (mesh, geometry_hash_verified, triangle_indices_verified) = validate_input(&input)?;
    let navmesh = provider::generate_navmesh(
        mesh,
        provider::settings_for_agent(AGENT_RADIUS_M, AGENT_HEIGHT_M),
    )
    .map_err(|error| BrowserProbeError::Replay(error.to_string()))?;
    let navmesh_hash = canonical_json_sha256(&navmesh)?;
    let path = navigation::query_rerecast_path(
        &navmesh,
        Vec3::from_array(input.start_m),
        Vec3::from_array(input.end_m),
        AGENT_RADIUS_M,
    )
    .map_err(|error| error.to_string());
    let landmass_path_found = path.as_ref().is_ok_and(|probe| probe.found);
    Ok(BrowserProbeReport {
        schema: "forge.navigation.browser-canonical-probe",
        version: 1,
        runtime: "wasm32-unknown-unknown",
        input_schema: NATIVE_SCHEMA,
        area_id: input.area_id,
        source_hash: input.source_hash,
        terrain_hash: input.terrain_hash,
        raster_hash: input.raster_hash,
        geometry_hash: input.geometry_hash,
        crop_xz_m: input.crop_xz_m,
        vertices: input.canonical_geometry.positions_m.len(),
        triangles: input.canonical_geometry.triangles.len(),
        geometry_hash_verified,
        triangle_indices_verified,
        provider: ProviderDescription {
            rerecast: "0.4.0",
            bevy_rerecast: "0.5.0",
            landmass: "0.9.2",
            agent_radius_m: AGENT_RADIUS_M,
            agent_height_m: AGENT_HEIGHT_M,
        },
        navmesh_hash,
        landmass_path_found,
        path,
        selection: input.selection,
        missing_proofs: [
            "cross-tile Landmass links",
            "native tile cache and partial reconstruction",
            "static colliders and water exclusions",
            "multi-island seams",
            "dynamic obstacles",
            "dependency governance and ADR acceptance",
            "human browser acceptance",
        ],
    })
}

fn validate_input(
    input: &CanonicalTerrainReport,
) -> Result<(TriMesh, bool, bool), BrowserProbeError> {
    if input.schema != NATIVE_SCHEMA || input.version != 2 {
        return Err(BrowserProbeError::Invalid(
            "expected canonical terrain probe schema v2".into(),
        ));
    }
    if input.selection.status != SELECTION_STATUS {
        return Err(BrowserProbeError::Invalid(
            "native selection status must remain blocked_pending_full_world_contract".into(),
        ));
    }
    let geometry = &input.canonical_geometry;
    if input.source_hash != geometry.source_hash
        || input.terrain_hash != geometry.terrain_hash
        || input.raster_hash != geometry.raster_hash
        || input.geometry_hash != geometry.geometry_hash
        || !same_bounds(&input.crop_xz_m, &geometry.crop_xz_m)
    {
        return Err(BrowserProbeError::Invalid(
            "canonical geometry metadata does not match the native report".into(),
        ));
    }
    if geometry.positions_m.is_empty() || geometry.triangles.is_empty() {
        return Err(BrowserProbeError::Invalid(
            "canonical geometry must contain vertices and triangles".into(),
        ));
    }
    if !valid_bounds(&input.crop_xz_m)
        || !point_inside_crop(&input.start_m, &input.crop_xz_m)
        || !point_inside_crop(&input.end_m, &input.crop_xz_m)
    {
        return Err(BrowserProbeError::Invalid(
            "canonical crop bounds or path endpoints are invalid".into(),
        ));
    }
    if geometry
        .positions_m
        .iter()
        .flatten()
        .any(|component| !component.is_finite())
    {
        return Err(BrowserProbeError::Invalid(
            "canonical geometry contains non-finite coordinates".into(),
        ));
    }
    for (triangle_index, triangle) in geometry.triangles.iter().enumerate() {
        if triangle.iter().any(|index| {
            usize::try_from(*index)
                .ok()
                .is_none_or(|index| index >= geometry.positions_m.len())
        }) {
            return Err(BrowserProbeError::Invalid(format!(
                "triangle {triangle_index} references a missing vertex"
            )));
        }
    }
    let actual_hash = canonical_json_sha256(&(&geometry.positions_m, &geometry.triangles))?;
    if actual_hash != input.geometry_hash {
        return Err(BrowserProbeError::Invalid(
            "canonical geometry hash does not match its vertices and triangles".into(),
        ));
    }
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
    Ok((mesh, true, true))
}

fn same_bounds(left: &[f64; 4], right: &[f64; 4]) -> bool {
    left.iter()
        .zip(right)
        .all(|(left, right)| left.to_bits() == right.to_bits())
}

fn valid_bounds(bounds: &[f64; 4]) -> bool {
    bounds.iter().all(|value| value.is_finite()) && bounds[0] < bounds[1] && bounds[2] < bounds[3]
}

fn point_inside_crop(point: &[f32; 3], bounds: &[f64; 4]) -> bool {
    point.iter().all(|value| value.is_finite())
        && f64::from(point[0]) >= bounds[0]
        && f64::from(point[0]) <= bounds[1]
        && f64::from(point[2]) >= bounds[2]
        && f64::from(point[2]) <= bounds[3]
}
