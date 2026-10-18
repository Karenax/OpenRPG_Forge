//! Compare the native cascade fitter/scheduler against a clean source oracle.
use bevy::math::{DMat4, DVec3};
use forge_tidewater::sun_cascades::{CascadeConfig, CascadeView, SunCascades};
use serde::Deserialize;
use std::{error::Error, fs};

#[derive(Deserialize)]
struct Oracle {
    schema: String,
    version: u32,
    source_commit: String,
    config: Config,
    cases: Vec<Case>,
}
#[derive(Deserialize)]
struct Config {
    size: u32,
    splits: Vec<f64>,
    light_margin: f64,
    normal_bias: Vec<f64>,
}
#[derive(Deserialize)]
struct Case {
    name: String,
    enabled: bool,
    invalidate: bool,
    view: CameraInput,
    sun: [f64; 3],
    updated: Vec<usize>,
    sampling_enabled: bool,
    cascades: Vec<Cascade>,
}
#[derive(Deserialize)]
struct CameraInput {
    world_from_view: [f64; 16],
    near: f64,
    fov_degrees: f64,
    aspect: f64,
}
#[derive(Deserialize)]
struct Cascade {
    world: [f64; 16],
    view: [f64; 16],
    projection: [f64; 16],
    view_projection: [f64; 16],
    radius: f64,
    near: f64,
    far: f64,
    info: [f64; 4],
    blend: [f64; 4],
}

fn main() -> Result<(), Box<dyn Error>> {
    let path = std::env::args().nth(1).ok_or("ORACLE_JSON required")?;
    let oracle: Oracle = serde_json::from_slice(&fs::read(path)?)?;
    if oracle.schema != "forge.tidewater.sun-cascade-oracle"
        || oracle.version != 1
        || oracle.source_commit != "1438b1abfcaee3267092b75573014f4d9b4a983c"
        || oracle.cases.is_empty()
    {
        return Err("invalid source oracle".into());
    }
    let c = oracle.config;
    let mut native = SunCascades::new(CascadeConfig {
        size: c.size,
        splits: c.splits,
        light_margin: c.light_margin,
        normal_bias: c.normal_bias,
    })?;
    let mut max_error: f64 = 0.0;
    let mut scalars = 0;
    for case in &oracle.cases {
        if case.invalidate {
            native.invalidate();
        }
        let view = CascadeView {
            world_from_view: DMat4::from_cols_array(&case.view.world_from_view),
            near: case.view.near,
            fov_degrees: case.view.fov_degrees,
            aspect: case.view.aspect,
        };
        let updated = native.update(view, DVec3::from_array(case.sun), case.enabled)?;
        assert_eq!(updated, case.updated, "schedule {}", case.name);
        assert_eq!(
            native.sampling_enabled, case.sampling_enabled,
            "enable {}",
            case.name
        );
        assert_eq!(native.cascades().len(), case.cascades.len());
        for (actual, expected) in native.cascades().iter().zip(&case.cascades) {
            let a = actual.ok_or("missing initialized cascade")?;
            let pairs = [
                (a.world_from_view.to_cols_array(), expected.world),
                (a.view_from_world.to_cols_array(), expected.view),
                (a.clip_from_view.to_cols_array(), expected.projection),
                (a.clip_from_world.to_cols_array(), expected.view_projection),
            ];
            let left = pairs
                .iter()
                .flat_map(|(l, _)| l.iter().copied())
                .chain(a.info)
                .chain(a.blend)
                .chain([a.radius, a.near, a.far]);
            let right = pairs
                .iter()
                .flat_map(|(_, r)| r.iter().copied())
                .chain(expected.info)
                .chain(expected.blend)
                .chain([expected.radius, expected.near, expected.far]);
            for (a, b) in left.zip(right) {
                let error = (a - b).abs();
                assert!(
                    a.is_finite() && b.is_finite() && error <= 1e-9,
                    "{}: {a} != {b}, delta {error}",
                    case.name
                );
                max_error = max_error.max(error);
                scalars += 1;
            }
        }
    }
    println!(
        "{}",
        serde_json::json!({"status":"native_cascade_fit_and_schedule_verified_not_shadow_rendering", "cases":oracle.cases.len(),"compared_scalars":scalars,"max_absolute_error":max_error})
    );
    Ok(())
}
