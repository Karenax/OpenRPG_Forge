//! Compare derived station coordinates with a pinned JavaScript oracle.
use std::{error::Error, fs, io::Write};
fn main() -> Result<(), Box<dyn Error>> {
    let args: Vec<_> = std::env::args_os().skip(1).collect();
    if args.len() != 4 {
        return Err("DOCUMENT HEIGHTS ORACLE NEW_REPORT required".into());
    }
    let document = serde_json::from_slice(&fs::read(&args[0])?)?;
    let plan = forge_world::sampled_terrain::SampledTerrainPlan::from_document(
        &document,
        &fs::read(&args[1])?,
    )?;
    let stations = forge_tidewater::breaker_geometry::beach_stations(|x, z| {
        plan.height_at(x, z).expect("finite bounded coordinates")
    });
    let bytes: Vec<_> = stations
        .iter()
        .flatten()
        .flat_map(|v| v.to_le_bytes())
        .collect();
    let hash = forge_core::bytes_sha256(&bytes).to_string();
    let expected: serde_json::Value = serde_json::from_slice(&fs::read(&args[2])?)?;
    let exact = expected["sha256"] == hash && expected["count"] == stations.len();
    let report = serde_json::json!({"count":stations.len(),"sha256":hash,"source_exact":exact});
    let mut file = fs::OpenOptions::new()
        .write(true)
        .create_new(true)
        .open(&args[3])?;
    writeln!(file, "{}", serde_json::to_string_pretty(&report)?)?;
    println!("{report}");
    if !exact {
        return Err("station source mismatch".into());
    }
    Ok(())
}
