//! Compare native propagation with the pinned source field.
#[path = "tidewater_plant_probe/shore_field.rs"]
mod shore_field;
use std::{error::Error, fs, io::Write};
fn main() -> Result<(), Box<dyn Error>> {
    let args: Vec<_> = std::env::args_os().skip(1).collect();
    if args.len() != 4 {
        return Err("DOCUMENT HEIGHTS EXPECTED_FIELD NEW_REPORT required".into());
    }
    let document = serde_json::from_slice(&fs::read(&args[0])?)?;
    let plan = forge_world::sampled_terrain::SampledTerrainPlan::from_document(
        &document,
        &fs::read(&args[1])?,
    )?;
    let actual = shore_field::bake(|x, z| plan.height_at(x, z))?;
    let expected = fs::read(&args[2])?;
    if expected.len() != actual.len() * 4 {
        return Err("field size".into());
    }
    let differences = actual
        .iter()
        .zip(expected.as_chunks::<4>().0)
        .filter(|(a, b)| a.to_bits() != u32::from_le_bytes(**b))
        .count();
    let bytes: Vec<_> = actual.iter().flat_map(|v| v.to_le_bytes()).collect();
    let report = serde_json::json!({"samples":actual.len(),"differing_values":differences,
        "actual_sha256":forge_core::bytes_sha256(&bytes),"expected_sha256":forge_core::bytes_sha256(&expected)});
    let mut file = fs::OpenOptions::new()
        .write(true)
        .create_new(true)
        .open(&args[3])?;
    writeln!(file, "{}", serde_json::to_string_pretty(&report)?)?;
    println!("{report}");
    if differences != 0 {
        return Err("shore field differs".into());
    }
    Ok(())
}
