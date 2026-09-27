//! Compare native derived beach geometry against the pinned independent export.
use forge_engine::breaker_geometry::{beach_stations, lip_topology};
use std::{error::Error, fs, path::PathBuf};

#[allow(clippy::cast_possible_truncation, clippy::cast_sign_loss)] // Coordinates checked inside 2048².
fn height_at(heights: &[f32], x: f64, z: f64) -> f64 {
    let (fx, fz) = (x + 1024.0 - 0.5, z + 1024.0 - 0.5);
    if !(0.0..2047.0).contains(&fx) || !(0.0..2047.0).contains(&fz) {
        return -90.0;
    }
    let (column, row) = (fx.floor() as usize, fz.floor() as usize);
    let (tx, tz) = (fx - fx.floor(), fz - fz.floor());
    let base = row * 2048 + column;
    let (top_left, top_right, bottom_left, bottom_right) = (
        f64::from(heights[base]),
        f64::from(heights[base + 1]),
        f64::from(heights[base + 2048]),
        f64::from(heights[base + 2049]),
    );
    (top_left * (1.0 - tx) + top_right * tx) * (1.0 - tz)
        + (bottom_left * (1.0 - tx) + bottom_right * tx) * tz
}

fn main() -> Result<(), Box<dyn Error>> {
    let mut args = std::env::args_os().skip(1);
    let root = PathBuf::from(args.next().ok_or("source export required")?);
    let output = PathBuf::from(args.next().ok_or("new output directory required")?);
    if args.next().is_some() || output.exists() {
        return Err("new output and exactly two arguments required".into());
    }
    let meta: serde_json::Value = serde_json::from_slice(&fs::read(root.join("breakers.json"))?)?;
    if meta["source_commit"] != "1438b1abfcaee3267092b75573014f4d9b4a983c"
        || meta["schema"] != "forge.tidewater.breakers"
        || meta["version"] != 1
        || meta["terrain"] != serde_json::json!({"resolution":2048,"origin":-1024,"texel":1})
    {
        return Err("unexpected source or terrain".into());
    }
    let read = |name: &str| -> Result<Vec<u8>, Box<dyn Error>> {
        let bytes = fs::read(root.join(name))?;
        if meta["files"][name] != forge_core::bytes_sha256(&bytes).to_string() {
            return Err("source hash mismatch".into());
        }
        Ok(bytes)
    };
    let bytes = read("heights.f32.bin")?;
    if bytes.len() != 2048 * 2048 * 4 {
        return Err("invalid terrain length".into());
    }
    let heights: Vec<f32> = bytes
        .as_chunks::<4>()
        .0
        .iter()
        .map(|v| f32::from_le_bytes(*v))
        .collect();
    if heights.iter().any(|v| !v.is_finite()) {
        return Err("nonfinite terrain".into());
    }
    let stations = beach_stations(|x, z| height_at(&heights, x, z));
    let topology = lip_topology(u32::try_from(stations.len())?).ok_or("invalid station count")?;
    let station_bytes: Vec<u8> = stations
        .iter()
        .flatten()
        .flat_map(|v| v.to_le_bytes())
        .collect();
    let ids: Vec<u8> = topology
        .sheet_ids
        .iter()
        .flatten()
        .flat_map(|v| v.to_le_bytes())
        .collect();
    let indices: Vec<u8> = topology
        .indices
        .iter()
        .flat_map(|v| v.to_le_bytes())
        .collect();
    let mut checks = Vec::new();
    fs::create_dir_all(&output)?;
    for (name, actual) in [
        ("stations.f32.bin", station_bytes),
        ("sheet-ids.f32.bin", ids),
        ("indices.u32.bin", indices),
    ] {
        let expected = read(name)?;
        checks.push(serde_json::json!({"file":name,"exact":actual==expected,"bytes":actual.len(),
            "native_sha256":forge_core::bytes_sha256(&actual).to_string(),"source_sha256":forge_core::bytes_sha256(&expected).to_string()}));
        fs::write(output.join(name), actual)?;
    }
    let pass = checks.iter().all(|c| c["exact"] == true) && meta["stations"] == stations.len();
    let report = serde_json::json!({"stations":stations.len(),"vertices":topology.sheet_ids.len(),
        "indices":topology.indices.len(),"pass":pass,"checks":checks,
        "limits":["derived_geometry_only","no_native_crest_compute_or_render_verdict"]});
    fs::write(
        output.join("verification.json"),
        serde_json::to_vec_pretty(&report)?,
    )?;
    println!("{report}");
    if !pass {
        return Err("native geometry differs from source".into());
    }
    Ok(())
}
