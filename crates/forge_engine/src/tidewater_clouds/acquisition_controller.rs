//! Fixed-pose adapter from acquisition metadata to the native cloud controller.
use crate::cloud_controller::{Camera, Controller, Input, Output};
use serde_json::Value;
use std::error::Error;

pub struct Continuation {
    pub controller: crate::cloud_controller::Controller,
    pub input: crate::cloud_controller::Input,
    pub last: crate::cloud_controller::Output,
}

/// Preserve the exact source basis, including signed zero, across JSON metadata.
fn basis_column(basis: &Value, name: &str) -> Result<[f64; 3], Box<dyn Error>> {
    let values = basis[name]
        .as_array()
        .ok_or("missing camera basis column")?;
    if values.len() != 3 {
        return Err("camera basis column must contain three bit patterns".into());
    }
    let mut column = [0.0; 3];
    for (destination, value) in column.iter_mut().zip(values) {
        *destination = f64::from_bits(value.as_u64().ok_or("invalid camera basis bits")?);
        if !destination.is_finite() {
            return Err("non-finite camera basis".into());
        }
    }
    Ok(column)
}

#[allow(clippy::many_single_char_names)]
fn input(meta: &Value) -> Result<Input, Box<dyn Error>> {
    let n = |v: &Value| v.as_f64().ok_or("missing number");
    let c = &meta["camera"];
    let camera = if let Some(basis) = c.get("basis_bits") {
        Camera {
            position: basis_column(basis, "position")?,
            right: basis_column(basis, "right")?,
            up: basis_column(basis, "up")?,
            forward: basis_column(basis, "forward")?,
            fov_degrees: n(&c["fov"])?,
            aspect: n(&c["aspect"])?,
            zoom: n(&c["zoom"])?,
        }
    } else {
        let [x, y, z, w] = [
            n(&c["quaternion"]["_x"])?,
            n(&c["quaternion"]["_y"])?,
            n(&c["quaternion"]["_z"])?,
            n(&c["quaternion"]["_w"])?,
        ];
        let (x2, y2, z2) = (x + x, y + y, z + z);
        Camera {
            position: [
                n(&c["position"]["x"])?,
                n(&c["position"]["y"])?,
                n(&c["position"]["z"])?,
            ],
            right: [1.0 - (y * y2 + z * z2), x * y2 + w * z2, x * z2 - w * y2],
            up: [x * y2 - w * z2, 1.0 - (x * x2 + z * z2), y * z2 + w * x2],
            forward: [
                -(x * z2 + w * y2),
                -(y * z2 - w * x2),
                -(1.0 - (x * x2 + y * y2)),
            ],
            fov_degrees: n(&c["fov"])?,
            aspect: c["aspect"].as_f64().unwrap_or(1920.0 / 1080.0),
            zoom: c["zoom"].as_f64().unwrap_or(1.0),
        }
    };
    let input = Input {
        camera,
        output: [
            u32::try_from(meta["output"][0].as_u64().unwrap_or(1920))?,
            u32::try_from(meta["output"][1].as_u64().unwrap_or(1080))?,
        ],
        resolution_scale: meta["resolution_scale"].as_f64().unwrap_or(1.0),
        coverage: meta["cloud_coverage"].as_f64().unwrap_or(0.49),
        wind_direction: [
            n(&meta["wind"]["direction"][0])?,
            n(&meta["wind"]["direction"][1])?,
        ],
        light_direction: [
            n(&meta["sun_direction"][0])?,
            n(&meta["sun_direction"][1])?,
            n(&meta["sun_direction"][2])?,
        ],
        underwater: meta["underwater"].as_bool().unwrap_or(false),
        dt: meta["dt"].as_f64().unwrap_or(1.0 / 60.0),
    };
    Ok(input)
}

fn update(controller: &mut Controller, meta: &Value) -> Result<(Input, Output), Box<dyn Error>> {
    match meta["action"].as_str() {
        Some("reset") => controller.reset_history(),
        Some("invalidate") => controller.invalidate(),
        _ => {}
    }
    let input = input(meta)?;
    let output = controller.update(input)?;
    Ok((input, output))
}

fn replay(meta: &Value) -> Result<(Controller, Input, Vec<Output>), Box<dyn Error>> {
    let mut controller = Controller::default();
    if let Some(frames) = meta["controller_frames"].as_array() {
        if frames.is_empty() || frames.len() > 4096 {
            return Err("cloud acquisition frame count outside bounds".into());
        }
        let mut last_input = None;
        let mut outputs = Vec::with_capacity(frames.len());
        for frame in frames {
            let (input, output) = update(&mut controller, frame)?;
            last_input = Some(input);
            outputs.push(output);
        }
        Ok((
            controller,
            last_input.ok_or("cloud acquisition frame count outside bounds")?,
            outputs,
        ))
    } else {
        let input = input(meta)?;
        let mut outputs = Vec::with_capacity(120);
        for _ in 0..120 {
            outputs.push(controller.update(input)?);
        }
        Ok((controller, input, outputs))
    }
}

pub fn generate(meta: &Value) -> Result<Vec<Output>, Box<dyn Error>> {
    let (_, _, outputs) = replay(meta)?;
    Ok(outputs)
}

/// Return the verified fixed generation and its exact controller state for live
/// continuation without replaying the acquisition a second time.
pub fn generation_and_continuation(
    meta: &Value,
) -> Result<(Vec<Output>, Continuation), Box<dyn Error>> {
    let (controller, input, outputs) = replay(meta)?;
    let last = outputs
        .last()
        .cloned()
        .ok_or("cloud acquisition frame count outside bounds")?;
    Ok((
        outputs,
        Continuation {
            controller,
            input,
            last,
        },
    ))
}

pub fn continuation(meta: &Value) -> Result<Continuation, Box<dyn Error>> {
    let (controller, input, mut outputs) = replay(meta)?;
    let last = outputs
        .pop()
        .ok_or("cloud acquisition frame count outside bounds")?;
    Ok(Continuation {
        controller,
        input,
        last,
    })
}

#[cfg(test)]
mod tests {
    use super::*;
    use serde_json::json;

    fn meta() -> Value {
        json!({
            "camera": {
                "quaternion": { "_x": 0.0, "_y": 0.0, "_z": 0.0, "_w": 1.0 },
                "position": { "x": 0.0, "y": 14.0, "z": -90.0 },
                "fov": 62.0,
                "aspect": 16.0 / 9.0,
                "zoom": 1.0,
            },
            "output": [1920, 1080],
            "resolution_scale": 1.0,
            "cloud_coverage": 0.49,
            "wind": { "direction": [1.0, 0.0] },
            "sun_direction": [0.0, 1.0, 0.0],
            "underwater": false,
            "dt": 1.0 / 60.0,
        })
    }

    #[test]
    #[allow(
        clippy::float_cmp,
        reason = "Source frame counters are exact integer stamps encoded in float uniforms."
    )]
    fn continuation_matches_generation_and_advances_the_cloud_clock() {
        let meta = meta();
        let generated = generate(&meta).unwrap();
        let mut continuation = continuation(&meta).unwrap();

        assert_eq!(
            crate::cloud_controller::bytes(&generated[119].frame),
            crate::cloud_controller::bytes(&continuation.last.frame)
        );
        assert_eq!(
            crate::cloud_controller::bytes(&generated[119].settings),
            crate::cloud_controller::bytes(&continuation.last.settings)
        );
        assert_eq!(
            crate::cloud_controller::bytes(&generated[119].public),
            crate::cloud_controller::bytes(&continuation.last.public)
        );

        let next = continuation.controller.update(continuation.input).unwrap();
        assert_eq!(continuation.last.frame[10][0], 119.0);
        assert_eq!(next.frame[10][0], 120.0);
        assert!(next.frame[10][1] > continuation.last.frame[10][1]);
        assert_eq!(continuation.last.view_index, 1);
        assert_eq!(next.view_index, 0);
    }

    #[test]
    fn empty_controller_frame_scenario_is_rejected() {
        let meta = json!({ "controller_frames": [] });
        assert!(generate(&meta).is_err());
        assert!(continuation(&meta).is_err());
    }

    #[test]
    fn acquired_basis_preserves_signed_zero_without_a_quaternion() {
        let mut meta = meta();
        meta["camera"].as_object_mut().unwrap().remove("quaternion");
        let bits = |v: [f64; 3]| v.map(f64::to_bits);
        meta["camera"]["basis_bits"] = json!({
            "position": bits([42.0, 14.0, -90.0]),
            "right": bits([1.0, -0.0, 0.0]),
            "up": bits([0.0, 1.0, -0.0]),
            "forward": bits([-0.0, 0.0, -1.0]),
        });
        let acquired = input(&meta).unwrap();
        assert_eq!(acquired.camera.right[1].to_bits(), (-0.0_f64).to_bits());
        assert_eq!(acquired.camera.up[2].to_bits(), (-0.0_f64).to_bits());
        assert_eq!(acquired.camera.forward[0].to_bits(), (-0.0_f64).to_bits());
        assert!(generate(&meta).is_ok());
    }

    #[test]
    fn malformed_acquired_basis_is_rejected_without_quaternion_fallback() {
        for bad in [
            json!({}),
            json!({"right": [0, 0]}),
            json!({"right": [0, -1, 0]}),
        ] {
            let mut meta = meta();
            meta["camera"]["basis_bits"] = bad;
            assert!(generate(&meta).is_err());
        }
        let basis = json!({"position": [0, 0, 0], "right": [f64::NAN.to_bits(), 0, 0]});
        assert!(basis_column(&basis, "right").is_err());
    }
}
