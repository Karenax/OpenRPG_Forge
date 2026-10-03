//! WASM-only lab probe for the bounded Rerecast detail pass.
//!
//! This is a new campaign entry point.  It does not change the pinned browser bundle used by
//! campaigns 001/002.  A successful result proves that the guarded adapter can produce a
//! navmesh for the canonical input; a structured rejection proves that the input is outside the
//! provider's safe detail domain.  Neither result is a provider-selection decision.
#![deny(unsafe_code)]
#![allow(
    clippy::cast_possible_truncation,
    clippy::cast_sign_loss,
    reason = "the probe converts canonical u32 triangle indices after validating the source"
)]

#[path = "../guarded_provider.rs"]
mod guarded_provider;

use bevy_math::{UVec3, Vec3A};
use bevy_rerecast::rerecast::{AreaType, TriMesh};
use forge_core::canonical_json_sha256;
use serde::Deserialize;
use serde_json::json;
use std::fmt::Display;

#[cfg(target_arch = "wasm32")]
use wasm_bindgen::prelude::*;

#[derive(Debug, Deserialize)]
struct CanonicalReport {
    canonical_geometry: CanonicalGeometry,
}

#[derive(Debug, Deserialize)]
struct CanonicalGeometry {
    positions_m: Vec<[f32; 3]>,
    triangles: Vec<[u32; 3]>,
}

#[cfg(target_arch = "wasm32")]
#[wasm_bindgen]
pub fn run_canonical_browser_guarded_probe(input_json: &str) -> Result<String, JsValue> {
    run_probe(input_json, 1.8).map_err(|error| JsValue::from_str(&error))
}

#[cfg(target_arch = "wasm32")]
#[wasm_bindgen]
pub fn run_canonical_browser_profile_probe(
    input_json: &str,
    agent_height_m: f32,
) -> Result<String, JsValue> {
    run_probe(input_json, agent_height_m).map_err(|error| JsValue::from_str(&error))
}

fn run_probe(input_json: &str, agent_height_m: f32) -> Result<String, String> {
    if !agent_height_m.is_finite() || agent_height_m <= 0.0 {
        return Err("agent height must be finite and positive".to_owned());
    }
    let input: CanonicalReport = serde_json::from_str(input_json).map_err(display)?;
    if input.canonical_geometry.positions_m.is_empty()
        || input.canonical_geometry.triangles.is_empty()
    {
        return Err("canonical geometry is empty".to_owned());
    }
    let positions = &input.canonical_geometry.positions_m;
    if positions.iter().flatten().any(|value| !value.is_finite()) {
        return Err("canonical geometry contains a non-finite coordinate".to_owned());
    }
    for (triangle_index, triangle) in input.canonical_geometry.triangles.iter().enumerate() {
        if triangle
            .iter()
            .any(|index| usize::try_from(*index).map_or(true, |index| index >= positions.len()))
        {
            return Err(format!(
                "triangle {triangle_index} references a missing canonical vertex"
            ));
        }
    }
    let mesh = TriMesh {
        vertices: positions.iter().copied().map(Vec3A::from_array).collect(),
        indices: input
            .canonical_geometry
            .triangles
            .iter()
            .copied()
            .map(UVec3::from_array)
            .collect(),
        area_types: vec![AreaType::NOT_WALKABLE; input.canonical_geometry.triangles.len()],
    };
    let settings = guarded_provider::settings_for_agent(0.35, agent_height_m);
    let navmesh = guarded_provider::generate_navmesh(mesh, settings)
        .map_err(|error| format!("guarded provider rejected canonical input: {error}"))?;
    landmass_rerecast::convert_rerecast_navmesh_to_landmass_navmesh(&navmesh)
        .validate()
        .map_err(|error| format!("raw Landmass validation rejected provider output: {error}"))?;
    let navmesh_hash = canonical_json_sha256(&navmesh).map_err(display)?;
    serde_json::to_string(&json!({
        "schema": "forge.navigation.browser-guarded-probe",
        "version": 1,
        "runtime": if cfg!(target_arch = "wasm32") { "wasm32-unknown-unknown" } else { "native" },
        "status": "ok",
        "raw_landmass_validation": "passed",
        "winding_normalization_used": false,
        "agent_height_m": agent_height_m,
        "agent_radius_m": 0.35,
        "vertices": positions.len(),
        "triangles": input.canonical_geometry.triangles.len(),
        "polygon_count": navmesh.polygon.polygon_count(),
        "navmesh_hash": navmesh_hash,
        "max_simplification_error": guarded_provider::CONSERVATIVE_MAX_SIMPLIFICATION_ERROR,
        "parameter_profile": "uniform_conservative_v1",
        "detail_guard": "bounded_radius_preflight",
        "selection": "blocked_pending_full_world_contract"
    }))
    .map_err(display)
}

fn display(error: impl Display) -> String {
    error.to_string()
}

#[cfg(target_arch = "wasm32")]
fn main() {}

#[cfg(not(target_arch = "wasm32"))]
fn main() -> Result<(), Box<dyn std::error::Error>> {
    let path = std::env::args()
        .nth(1)
        .ok_or("usage: canonical_browser_guarded_probe <canonical-input.json>")?;
    let agent_height_m = std::env::args()
        .nth(2)
        .map_or(Ok(1.8), |raw| raw.parse::<f32>())?;
    let encoded = run_probe(&std::fs::read_to_string(path)?, agent_height_m)
        .map_err(std::io::Error::other)?;
    println!("{encoded}");
    Ok(())
}
