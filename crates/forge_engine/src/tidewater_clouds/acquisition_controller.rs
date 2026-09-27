//! Fixed-pose adapter from acquisition metadata to the native cloud controller.
use crate::cloud_controller::{Camera, Controller, Input, Output};
use serde_json::Value;
use std::error::Error;
#[allow(clippy::many_single_char_names)]
fn input(meta: &Value) -> Result<Input, Box<dyn Error>> {
    let n = |v: &Value| v.as_f64().ok_or("missing number");
    let c = &meta["camera"];
    let [x, y, z, w] = [
        n(&c["quaternion"]["_x"])?,
        n(&c["quaternion"]["_y"])?,
        n(&c["quaternion"]["_z"])?,
        n(&c["quaternion"]["_w"])?,
    ];
    let (x2, y2, z2) = (x + x, y + y, z + z);
    let camera = Camera {
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
pub fn generate(meta: &Value) -> Result<Vec<Output>, Box<dyn Error>> {
    let mut controller = Controller::default();
    if let Some(frames) = meta["controller_frames"].as_array() {
        if frames.is_empty() || frames.len() > 4096 {
            return Err("cloud acquisition frame count outside bounds".into());
        }
        frames
            .iter()
            .map(|frame| {
                match frame["action"].as_str() {
                    Some("reset") => controller.reset_history(),
                    Some("invalidate") => controller.invalidate(),
                    _ => {}
                }
                controller.update(input(frame)?).map_err(Into::into)
            })
            .collect()
    } else {
        let input = input(meta)?;
        (0..120)
            .map(|_| controller.update(input).map_err(Into::into))
            .collect()
    }
}
