//! Saved local terrain edit and undo through the normal aggregate commands.
use forge_core::CommandId;
use forge_project::{
    TerrainHeightDelta,
    terrain_edit::{TerrainAction, TerrainEdit},
    zone_authoring::session::ZoneEditSession,
};
use std::{error::Error, fs, path::PathBuf};

fn main() -> Result<(), Box<dyn Error>> {
    let args: Vec<_> = std::env::args_os().skip(1).collect();
    if args.len() != 2 {
        return Err("INITIAL_ARCHIVE NEW_DIRECTORY required".into());
    }
    let mut zone = ZoneEditSession::from_archive_bytes(&fs::read(&args[0])?)?;
    let directory = PathBuf::from(&args[1]);
    fs::create_dir(&directory)?;
    let mut samples = Vec::new();
    for z in 1104_u16..=1224 {
        for x in 1214_u16..=1334 {
            let dx = f32::from(x) - 1274.0;
            let dz = f32::from(z) - 1164.0;
            let t = 1.0 - (dx * dx + dz * dz) / 3600.0;
            if t > 0.0 {
                samples.push(TerrainHeightDelta::new(x, z, 12.0 * t * t));
            }
        }
    }
    samples.sort_by_key(|v| (v.sample_z, v.sample_x));
    let request = zone.head().prepare_terrain_action(
        TerrainAction::Edit(TerrainEdit::SetHeightDeltas { samples }),
        CommandId::deterministic("tidewater-visible-relief"),
    )?;
    zone.execute(&request)?;
    fs::write(directory.join("raised.json"), zone.archive_bytes()?)?;
    zone.execute(&zone.head().prepare_terrain_action(
        TerrainAction::Undo,
        CommandId::deterministic("tidewater-visible-relief-undo"),
    )?)?;
    fs::write(directory.join("undo.json"), zone.archive_bytes()?)?;
    zone.execute(&zone.head().prepare_terrain_action(
        TerrainAction::Edit(TerrainEdit::SetBaseElevation { metres: 3.0 }),
        CommandId::deterministic("tidewater-global-elevation"),
    )?)?;
    fs::write(directory.join("global.json"), zone.archive_bytes()?)?;
    zone.execute(&zone.head().prepare_terrain_action(
        TerrainAction::Edit(TerrainEdit::SetBaseElevation { metres: 0.5 }),
        CommandId::deterministic("tidewater-shallow-elevation"),
    )?)?;
    fs::write(directory.join("shallow.json"), zone.archive_bytes()?)?;
    for name in ["raised", "undo", "global", "shallow"] {
        let archive = fs::read(directory.join(format!("{name}.json")))?;
        ZoneEditSession::from_archive_bytes(&archive)?;
        println!("{name}: {}", forge_core::bytes_sha256(&archive));
    }
    Ok(())
}
