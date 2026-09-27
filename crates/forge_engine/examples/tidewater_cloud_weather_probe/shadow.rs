//! Verified native fixture acquisition for the shared cloud inputs.
use forge_engine::tidewater_clouds::shadow::Input;
use serde_json::Value;
use std::{error::Error, path::Path};
#[allow(clippy::fn_params_excessive_bools)] // Independent diagnostic switches.
pub fn load(
    root: &Path,
    meta: &Value,
    with_trace: bool,
    with_history: bool,
    native_atmosphere: bool,
    native_controller: bool,
) -> Result<Input, Box<dyn Error>> {
    let shader = String::from_utf8(super::checked(root, meta, "cloud-shadow.wgsl")?)?;
    let mut noise = Vec::new();
    for level in 0..7 {
        let bytes = super::checked(root, meta, &format!("cloud-volume-m{level}.rgba8.bin"))?;
        if bytes.len() != 4 * (64_usize >> level).pow(3) {
            return Err("cloud volume size".into());
        }
        noise.push(bytes);
    }
    let generated = if native_controller {
        Some(super::controller::generate(meta)?)
    } else {
        None
    };
    let mut frames = Vec::new();
    for step in 1..=120 {
        let local = if let Some(generated) = &generated {
            forge_engine::cloud_controller::bytes(&generated[step - 1].frame)
        } else {
            super::checked(root, meta, &format!("cloud-step{step:03}-frame.bin"))?
        };
        let global = super::checked(root, meta, &format!("cloud-step{step:03}-global.bin"))?;
        if local.len() != 544 || global.len() != 720 {
            return Err("cloud frame size".into());
        }
        if local
            .as_chunks::<4>()
            .0
            .iter()
            .any(|v| !f32::from_le_bytes(*v).is_finite())
        {
            return Err("nonfinite cloud frame".into());
        }
        frames.push((local, global));
    }
    Ok(Input {
        shader,
        noise,
        frames,
        trace: if with_trace {
            Some(super::trace::load(
                root,
                meta,
                with_history,
                native_atmosphere,
                native_controller,
            )?)
        } else {
            None
        },
    })
}
