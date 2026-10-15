//! Exercise imported building pins through real aggregate placement commands.
use forge_assets::AssetCatalog;
use forge_core::{CommandId, EntityId, Transform, UnitQuaternion, Vec3, bytes_sha256};
use forge_project::zone_authoring::{
    placements::{ManualPlacement, PlacementEdit},
    session::{ZoneAction, ZoneEditSession, ZoneRequest},
};
use serde::Deserialize;
use std::{error::Error, fs, path::Path};

#[derive(Deserialize)]
struct Descriptor {
    building: Building,
}
#[derive(Deserialize)]
struct Building {
    source_id: String,
    source_frame: Frame,
}
#[derive(Deserialize)]
struct Frame {
    translation_m: [f32; 3],
    rotation_xyzw: [f32; 4],
    scale: [f32; 3],
}

fn save(zone: &mut ZoneEditSession, root: &Path, name: &str) -> Result<(), Box<dyn Error>> {
    let bytes = zone.archive_bytes()?;
    fs::write(root.join(format!("{name}.json")), &bytes)?;
    let reopened = ZoneEditSession::from_archive_bytes(&bytes)?;
    assert_eq!(zone.head(), reopened.head());
    *zone = reopened;
    println!("{name}: {}", bytes_sha256(&bytes));
    Ok(())
}

fn undo(zone: &mut ZoneEditSession, name: &str) -> Result<(), String> {
    zone.execute(&ZoneRequest {
        command_id: CommandId::deterministic(name),
        expected_revision: zone.head().revision,
        expected_hash: zone.head().content_hash()?,
        action: ZoneAction::Undo,
    })?;
    Ok(())
}

fn build_placements(
    catalog: &AssetCatalog,
    catalog_root: &Path,
    zone: &ZoneEditSession,
) -> Result<Vec<ManualPlacement>, Box<dyn Error>> {
    let base = &zone.head().base;
    let mut placements = Vec::new();
    for record in &catalog.assets {
        catalog.resolve_import_pin(record.id, record.content_hash)?;
        let bytes = fs::read(catalog_root.join(format!("{}.json", record.content_hash)))?;
        if bytes_sha256(&bytes) != record.content_hash {
            return Err("Asset descriptor content mismatch".into());
        }
        let descriptor: Descriptor = serde_json::from_slice(&bytes)?;
        let b = descriptor.building;
        if b.source_id != record.logical_name {
            return Err("Asset source identity mismatch".into());
        }
        let [x, y, z] = b.source_frame.translation_m;
        let [qx, qy, qz, qw] = b.source_frame.rotation_xyzw;
        let [sx, sy, sz] = b.source_frame.scale;
        let transform = Transform {
            translation_m: Vec3::new(x, y, z),
            rotation: UnitQuaternion::new(qx, qy, qz, qw),
            scale: Vec3::new(sx, sy, sz),
        };
        transform.validate()?;
        placements.push(ManualPlacement {
            particles: None,
            physics: None,
            id: EntityId::deterministic(format!(
                "{}:{}:{}",
                base.terrain.project_id, base.terrain.area_id, b.source_id
            )),
            name: b.source_id,
            asset_id: record.id,
            asset_hash: record.content_hash,
            transform,
            locked: false,
        });
    }
    Ok(placements)
}

fn main() -> Result<(), Box<dyn Error>> {
    let args: Vec<_> = std::env::args_os().skip(1).collect();
    if args.len() != 3 {
        return Err("INITIAL_ARCHIVE CATALOG_DIRECTORY NEW_OUTPUT required".into());
    }
    let mut zone = ZoneEditSession::from_archive_bytes(&fs::read(&args[0])?)?;
    let catalog_root = Path::new(&args[1]);
    let output = Path::new(&args[2]);
    let catalog: AssetCatalog =
        serde_json::from_slice(&fs::read(catalog_root.join("catalog.json"))?)?;
    catalog.validate_import_metadata()?;
    if catalog.assets.len() != 23
        || zone
            .head()
            .corrections
            .placements
            .as_ref()
            .is_some_and(|p| !p.is_empty())
    {
        return Err("Expected 23 imported buildings and no existing placements".into());
    }
    let base = zone.head().base.clone();
    let mut placements = build_placements(&catalog, catalog_root, &zone)?;
    placements.sort_by_key(|p| p.id);
    let original = placements.clone();
    let target = placements
        .iter()
        .find(|p| p.name.ends_with(":building:16"))
        .ok_or("Missing foreground building")?
        .clone();
    zone.execute(&zone.head().prepare_placement(
        CommandId::deterministic("tidewater-buildings-insert"),
        PlacementEdit::InsertMany { placements },
    )?)?;
    fs::create_dir(output)?;
    save(&mut zone, output, "initial")?;
    let mut moved = target.transform;
    moved.translation_m.x += 12.0;
    zone.execute(&zone.head().prepare_placement(
        CommandId::deterministic("tidewater-building-move"),
        PlacementEdit::Transform {
            id: target.id,
            transform: moved,
        },
    )?)?;
    save(&mut zone, output, "moved")?;
    assert_eq!(
        zone.head()
            .corrections
            .placements
            .as_ref()
            .unwrap()
            .iter()
            .find(|p| p.id == target.id)
            .unwrap()
            .transform,
        moved
    );
    undo(&mut zone, "tidewater-building-move-undo")?;
    assert_eq!(zone.head().corrections.placements.as_ref(), Some(&original));
    save(&mut zone, output, "move-undo")?;
    zone.execute(&zone.head().prepare_placement(
        CommandId::deterministic("tidewater-building-remove"),
        PlacementEdit::Remove { id: target.id },
    )?)?;
    save(&mut zone, output, "removed")?;
    assert_eq!(
        zone.head().corrections.placements.as_ref().unwrap().len(),
        22
    );
    undo(&mut zone, "tidewater-building-remove-undo")?;
    assert_eq!(zone.head().corrections.placements.as_ref(), Some(&original));
    assert_eq!(zone.head().base, base);
    save(&mut zone, output, "remove-undo")?;
    Ok(())
}
