//! Prepare reversible authored states through canonical zone commands.
use forge_core::CommandId;
use forge_project::{
    TerrainDocument, VegetationDensityMask, VegetationDocument, VegetationExclusion,
    VegetationExclusionId,
    zone_authoring::{
        exclusions::VegetationExclusionEdit,
        session::{ZoneAction, ZoneEditSession, ZoneRequest},
        vegetation_commands::VegetationEdit,
    },
};
use std::{error::Error, fs, path::PathBuf};
fn main() -> Result<(), Box<dyn Error>> {
    let args: Vec<_> = std::env::args_os().skip(1).collect();
    if args.len() != 3 {
        return Err(
            "usage: prepare_tidewater_authoring_proof VEGETATION TERRAIN NEW_DIRECTORY".into(),
        );
    }
    let vegetation = VegetationDocument::from_json(&fs::read_to_string(&args[0])?)?;
    let terrain = TerrainDocument::from_json(&fs::read_to_string(&args[1])?)?;
    let out = PathBuf::from(&args[2]);
    fs::create_dir(&out)?;
    let mut zone = ZoneEditSession::from_initial_terrain(terrain)?;
    let mut base = zone.head().base.clone();
    base.vegetation = Some(vegetation.clone());
    let request = zone
        .head()
        .prepare_regeneration(base, CommandId::deterministic("render-proof-import"))?;
    zone.execute(&request)?;
    fs::write(out.join("initial.json"), zone.archive_bytes()?)?;
    // Source placements projected through the fixed camera locate this visible slope.
    let request = zone.head().prepare_vegetation_exclusions(
        VegetationExclusionEdit::Replace {
            exclusions: vec![VegetationExclusion::Circle {
                id: VegetationExclusionId::new("coastal_clearing"),
                center_x_m: 250.0,
                center_z_m: 140.0,
                radius_m: 110.0,
            }],
        },
        CommandId::deterministic("render-proof-clearing"),
    )?;
    zone.execute(&request)?;
    fs::write(out.join("clearing.json"), zone.archive_bytes()?)?;
    let undo = ZoneRequest {
        command_id: CommandId::deterministic("render-proof-undo"),
        expected_revision: zone.head().revision,
        expected_hash: zone.head().content_hash()?,
        action: ZoneAction::Undo,
    };
    zone.execute(&undo)?;
    fs::write(out.join("undo.json"), zone.archive_bytes()?)?;
    for layer in &vegetation.layers {
        let request = zone.head().prepare_vegetation_edit(
            VegetationEdit::SetDensityMask {
                layer_id: layer.id.clone(),
                mask: VegetationDensityMask {
                    vertices_per_axis: 2,
                    samples_u8: vec![0; 4],
                },
            },
            CommandId::deterministic(format!("render-proof-zero/{}", layer.id)),
        )?;
        zone.execute(&request)?;
    }
    fs::write(out.join("zero.json"), zone.archive_bytes()?)?;
    for name in ["initial", "clearing", "undo", "zero"] {
        let bytes = fs::read(out.join(format!("{name}.json")))?;
        ZoneEditSession::from_archive_bytes(&bytes)?;
        println!("{name}: {}", forge_core::bytes_sha256(&bytes));
    }
    Ok(())
}
