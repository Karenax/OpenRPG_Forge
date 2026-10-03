//! Compare the Rust terrain AO channel against the acquired source map.
#[path = "../src/tidewater_terrain/normals/ao.rs"]
mod terrain_ao;
use std::{error::Error, fs, io::Write};
fn main() -> Result<(), Box<dyn Error>> {
    let args: Vec<_> = std::env::args_os().skip(1).collect();
    if args.len() != 3 {
        return Err("HEIGHTS NORMAL_MAP NEW_REPORT required".into());
    }
    let raw = fs::read(&args[0])?;
    if raw.len() != 2048 * 2048 * 4 {
        return Err("height size".into());
    }
    let heights: Vec<_> = raw
        .as_chunks::<4>()
        .0
        .iter()
        .map(|v| f32::from_le_bytes(*v))
        .collect();
    if heights.iter().any(|v| !v.is_finite()) {
        return Err("nonfinite height".into());
    }
    let normal = fs::read(&args[1])?;
    if normal.len() < raw.len() {
        return Err("normal size".into());
    }
    let baked = terrain_ao::bake(&heights);
    let differences = baked
        .iter()
        .enumerate()
        .filter(|(i, v)| **v != normal[i * 4 + 3])
        .count();
    let report = serde_json::json!({"samples":baked.len(),"differing_texels":differences,
        "ao_sha256":forge_core::bytes_sha256(&baked),"source_heights_sha256":forge_core::bytes_sha256(&raw)});
    let mut file = fs::OpenOptions::new()
        .write(true)
        .create_new(true)
        .open(&args[2])?;
    writeln!(file, "{}", serde_json::to_string_pretty(&report)?)?;
    println!("{}", serde_json::to_string(&report)?);
    if differences != 0 {
        return Err("AO differs from acquired source".into());
    }
    Ok(())
}
