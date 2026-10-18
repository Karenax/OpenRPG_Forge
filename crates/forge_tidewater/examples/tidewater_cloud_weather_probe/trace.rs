//! Verified native fixture acquisition for the shared cloud inputs.
use forge_tidewater::tidewater_clouds::trace::Input;
use serde_json::Value;
use std::{error::Error, path::Path};
pub fn load(
    root: &Path,
    meta: &Value,
    with_history: bool,
    native_atmosphere: bool,
    native_controller: bool,
) -> Result<Input, Box<dyn Error>> {
    let shader = String::from_utf8(super::checked(root, meta, "cloud-trace.wgsl")?)?;
    let atmosphere = super::checked(root, meta, "cloud-step120-atmosphere.bin")?;
    if atmosphere.len() != 64
        || atmosphere
            .as_chunks::<4>()
            .0
            .iter()
            .any(|v| !f32::from_le_bytes(*v).is_finite())
    {
        return Err("cloud atmosphere parameters size".into());
    }
    let frame = if native_controller {
        forge_tidewater::cloud_controller::bytes(&super::controller::generate(meta)?[119].frame)
    } else {
        super::checked(root, meta, "cloud-step120-frame.bin")?
    };
    if frame.len() != 544
        || frame[128..144]
            != [1920_f32, 1080., 960., 540.]
                .into_iter()
                .flat_map(f32::to_le_bytes)
                .collect::<Vec<_>>()
    {
        return Err("cloud trace requires FHD acquisition".into());
    }
    if !native_controller
        && super::checked(root, meta, "cloud-step001-settings.bin")?
            != super::checked(root, meta, "cloud-step120-settings.bin")?
    {
        return Err("cloud trace requires constant settings".into());
    }
    let mut textures = Vec::new();
    if with_history {
        let settings = if native_controller {
            Vec::new()
        } else {
            super::checked(root, meta, "cloud-step001-settings.bin")?
        };
        for step in 1..=120 {
            if super::checked(root, meta, &format!("cloud-step{step:03}-atmosphere.bin"))?
                != atmosphere
                || (!native_controller
                    && super::checked(root, meta, &format!("cloud-step{step:03}-settings.bin"))?
                        != settings)
            {
                return Err("cloud history requires constant atmosphere and settings".into());
            }
        }
        for name in ["transmittance", "skyView"] {
            if native_atmosphere {
                continue;
            }
            if super::checked(root, meta, &format!("cloud-step001-{name}.bin"))?
                != super::checked(root, meta, &format!("cloud-step120-{name}.bin"))?
            {
                return Err("cloud history atmosphere LUT changed".into());
            }
        }
    }
    for (file, size) in [
        ("cloud-step120-transmittance.bin", 256 * 64 * 8),
        ("cloud-step120-skyView.bin", 192 * 108 * 8),
        ("cloud-blueNoise.bin", 4096),
    ] {
        if native_atmosphere && file != "cloud-blueNoise.bin" {
            textures.push(Vec::new());
            continue;
        }
        let bytes = super::checked(root, meta, file)?;
        if bytes.len() != size {
            return Err("cloud trace texture size".into());
        }
        textures.push(bytes);
    }
    Ok(Input {
        panorama: if with_history {
            Some(String::from_utf8(super::checked(
                root,
                meta,
                "cloud-pano.wgsl",
            )?)?)
        } else {
            None
        },
        temporal: if with_history {
            Some(String::from_utf8(super::checked(
                root,
                meta,
                "cloud-temporal.wgsl",
            )?)?)
        } else {
            None
        },
        shader,
        atmosphere,
        textures,
    })
}
