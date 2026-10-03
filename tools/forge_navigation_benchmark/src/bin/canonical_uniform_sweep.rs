//! Bounded native causal sweep for the uniform contour simplification profile.
//!
//! Every input is fed through the same settings except for `max_simplification_error`. The
//! canonical triangles are deserialized and passed unchanged to the public Rerecast stages; this
//! binary does not repair winding, clip geometry, substitute bounds, or drop rejected scenes.
#![deny(unsafe_code)]
#![allow(
    clippy::cast_possible_truncation,
    clippy::cast_sign_loss,
    reason = "the report converts validated canonical u32 triangle indices into provider input"
)]

#[path = "../guarded_provider.rs"]
mod guarded_provider;

use bevy_math::{UVec3, Vec3A};
use bevy_rerecast::rerecast::{AreaType, TriMesh};
use forge_core::canonical_json_sha256;
use serde::Deserialize;
use serde_json::json;
use std::{error::Error, fs, path::Path, time::Instant};

const AGENT_RADIUS_M: f32 = 0.35;
const DEFAULT_AGENT_HEIGHT_M: f32 = 1.8;
const PROFILE_VALUES: [f32; 7] = [1.1, 0.75, 0.5, 0.25, 0.1, 0.05, 0.01];

#[derive(Debug, Deserialize)]
struct CanonicalReport {
    canonical_geometry: CanonicalGeometry,
}

#[derive(Debug, Deserialize)]
struct CanonicalGeometry {
    positions_m: Vec<[f32; 3]>,
    triangles: Vec<[u32; 3]>,
}

fn main() -> Result<(), Box<dyn Error>> {
    let agent_height_m = std::env::var("FORGE_AGENT_HEIGHT_M")
        .map_or(Ok(DEFAULT_AGENT_HEIGHT_M), |raw| raw.parse::<f32>())?;
    if !agent_height_m.is_finite() || agent_height_m <= 0.0 {
        return Err("FORGE_AGENT_HEIGHT_M must be finite and positive".into());
    }
    let mut args = std::env::args().skip(1);
    let output = args
        .next()
        .ok_or("usage: canonical_uniform_sweep <output.json> <canonical-input.json> [...inputs]")?;
    let inputs = args.collect::<Vec<_>>();
    if inputs.is_empty() {
        return Err("at least one canonical input is required".into());
    }

    let profile_values = profile_values();
    let mut cases = Vec::with_capacity(profile_values.len() * inputs.len());
    for max_simplification_error in &profile_values {
        for input_path in &inputs {
            cases.push(run_case(
                input_path,
                *max_simplification_error,
                agent_height_m,
            )?);
        }
    }
    let failed_cases = cases.iter().filter(|case| case["status"] != "ok").count();
    let summary = json!({
        "schema": "forge.navigation.uniform-conservative-native-sweep",
        "version": 1,
        "profile_values": profile_values,
        "agent_radius_m": AGENT_RADIUS_M,
        "agent_height_m": agent_height_m,
        "geometry_policy": "exact canonical positions and u32 triangles; no winding normalization, clipping, AABB substitution, or silent exclusion",
        "cases": cases,
        "failed_cases": failed_cases,
        "passed": failed_cases == 0,
    });
    let encoded = serde_json::to_vec_pretty(&summary)?;
    let output = Path::new(&output);
    if output.exists() {
        return Err(format!("refusing to overwrite existing report {}", output.display()).into());
    }
    fs::write(output, encoded)?;
    if failed_cases > 0 {
        return Err(
            format!("{failed_cases} provider cases failed; full report was preserved").into(),
        );
    }
    Ok(())
}

fn profile_values() -> Vec<f32> {
    std::env::var("FORGE_SWEEP_VALUES")
        .ok()
        .map(|raw| {
            raw.split(',')
                .filter_map(|value| value.trim().parse::<f32>().ok())
                .filter(|value| value.is_finite() && *value >= 0.0)
                .collect::<Vec<_>>()
        })
        .filter(|values| !values.is_empty())
        .unwrap_or_else(|| PROFILE_VALUES.to_vec())
}

fn run_case(
    input_path: &str,
    max_simplification_error: f32,
    agent_height_m: f32,
) -> Result<serde_json::Value, Box<dyn Error>> {
    let input = serde_json::from_str::<CanonicalReport>(&fs::read_to_string(input_path)?)?;
    let positions = input.canonical_geometry.positions_m;
    let triangles = input.canonical_geometry.triangles;
    let mesh = TriMesh {
        vertices: positions.iter().copied().map(Vec3A::from_array).collect(),
        indices: triangles.iter().copied().map(UVec3::from_array).collect(),
        area_types: vec![AreaType::NOT_WALKABLE; triangles.len()],
    };
    let mut settings = guarded_provider::settings_for_agent(AGENT_RADIUS_M, agent_height_m);
    settings.max_simplification_error = max_simplification_error;
    let started = Instant::now();
    let result = guarded_provider::generate_navmesh(mesh, settings);
    let duration_ms = started.elapsed().as_secs_f64() * 1000.0;
    let name = Path::new(input_path)
        .file_name()
        .and_then(|value| value.to_str())
        .unwrap_or(input_path);
    match result {
        Ok(navmesh) => {
            let validation =
                landmass_rerecast::convert_rerecast_navmesh_to_landmass_navmesh(&navmesh)
                    .validate();
            if let Err(error) = validation {
                return Ok(json!({"input": input_path, "name": name,
                    "max_simplification_error": max_simplification_error,
                    "status": "error", "stage": "raw_landmass_validation",
                    "duration_ms": duration_ms, "error": error.to_string(),
                    "vertices": positions.len(), "triangles": triangles.len(),
                    "winding_normalization_used": false,
                }));
            }
            Ok(json!({
                "input": input_path,
                "name": name,
                "max_simplification_error": max_simplification_error,
                "status": "ok",
                "raw_landmass_validation": "passed",
                "winding_normalization_used": false,
                "duration_ms": duration_ms,
                "vertices": positions.len(),
                "triangles": triangles.len(),
                "polygon_count": navmesh.polygon.polygon_count(),
                "navmesh_hash": canonical_json_sha256(&navmesh)?,
            }))
        }
        Err(error) => Ok(json!({
            "input": input_path,
            "name": name,
            "max_simplification_error": max_simplification_error,
            "status": "error",
            "duration_ms": duration_ms,
            "vertices": positions.len(),
            "triangles": triangles.len(),
            "error": error.to_string(),
        })),
    }
}
