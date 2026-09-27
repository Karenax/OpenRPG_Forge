use forge_project::VegetationDocument;
use forge_world::imported_vegetation::ImportedPopulation;
use std::{collections::BTreeSet, error::Error, fs, io::Write, path::PathBuf};

fn main() -> Result<(), Box<dyn Error>> {
    let args: Vec<_> = std::env::args_os().skip(1).collect();
    if !(2..=4).contains(&args.len()) {
        return Err(
            "usage: verify_imported_population IMPORT_DIRECTORY NEW_REPORT [TERRAIN_DOCUMENT [HEIGHT_BLOB]]"
                .into(),
        );
    }
    let root = PathBuf::from(&args[0]);
    let document =
        VegetationDocument::from_json(&fs::read_to_string(root.join("vegetation.v2.json"))?)?;
    let population = ImportedPopulation::resolve(&document, |hash, expected| {
        let path = root.join(format!("{hash}.bin"));
        if fs::metadata(&path).map_err(|e| e.to_string())?.len() != expected as u64 {
            return Err("size mismatch before read".into());
        }
        fs::read(path).map_err(|e| e.to_string())
    })?;
    let mut unique = BTreeSet::new();
    let mut chunk_visits = 0;
    for layer in population.layers() {
        for x in 0..64 {
            for z in 0..64 {
                for entity in layer.chunk([x, z]) {
                    if !unique.insert(entity.id) {
                        return Err("duplicate chunk membership".into());
                    }
                    chunk_visits += 1;
                }
            }
        }
    }
    if chunk_visits != population.instance_count() {
        return Err("missing chunk membership".into());
    }
    let exclusions = if let Some(terrain_path) = args.get(2) {
        Some(verify_exclusions(&population, terrain_path)?)
    } else {
        None
    };
    let report = serde_json::json!({"status":"pass", "instances":population.instance_count(),
        "unique_chunk_memberships":unique.len(), "layers":population.layers().len(),
        "document_sha256":population.document().content_hash()?,
        "exclusions":exclusions,
        "terrain_projection": if args.len() == 4 { Some(verify_terrain_projection(&population, &args[2], &args[3])?) } else { None },
        "scope":"world population resolver and optional authored exclusions; other corrections, renderer and Studio pending"});
    let mut file = fs::OpenOptions::new()
        .write(true)
        .create_new(true)
        .open(&args[1])?;
    writeln!(file, "{}", serde_json::to_string_pretty(&report)?)?;
    println!("Resolved {} instances", population.instance_count());
    Ok(())
}

fn verify_terrain_projection(
    population: &ImportedPopulation,
    terrain_path: &std::ffi::OsStr,
    heights_path: &std::ffi::OsStr,
) -> Result<serde_json::Value, Box<dyn Error>> {
    use forge_core::CommandId;
    use forge_project::{
        TerrainDocument,
        terrain_edit::{TerrainAction, TerrainEdit},
        zone_authoring::session::ZoneEditSession,
    };
    let terrain = TerrainDocument::from_json(&fs::read_to_string(terrain_path)?)?;
    let mut zone = ZoneEditSession::from_initial_terrain(terrain)?;
    let mut base = zone.head().base.clone();
    base.vegetation = Some(population.document().clone());
    zone.execute(
        &zone
            .head()
            .prepare_regeneration(base, CommandId::deterministic("ground-import"))?,
    )?;
    let heights = fs::read(heights_path)?;
    let initial = population.terrain_view(zone.head(), &heights)?;
    for (layer, original) in initial.layers.iter().zip(population.layers()) {
        if layer.instances != original.instances() {
            return Err("unedited projection changed source".into());
        }
    }
    zone.execute(&zone.head().prepare_terrain_action(
        TerrainAction::Edit(TerrainEdit::SetBaseElevation { metres: 3.0 }),
        CommandId::deterministic("ground-raise"),
    )?)?;
    let reopened = ZoneEditSession::from_archive_bytes(&zone.archive_bytes()?)?;
    let projected = population.terrain_view(reopened.head(), &heights)?;
    let mut maximum_error = 0.0_f64;
    for (layer, original) in projected.layers.iter().zip(population.layers()) {
        for (new, old) in layer.instances.iter().zip(original.instances()) {
            let mut expected = old.clone();
            maximum_error = maximum_error
                .max((new.source.position_m[1] - old.source.position_m[1] - 3.0).abs());
            expected.source.position_m[1] = new.source.position_m[1];
            if new != &expected {
                return Err("ground projection changed identity or other attributes".into());
            }
        }
    }
    if projected.moved_count != population.instance_count() || maximum_error > 1e-10 {
        return Err("ground projection displacement mismatch".into());
    }
    zone.execute(
        &zone
            .head()
            .prepare_terrain_action(TerrainAction::Undo, CommandId::deterministic("ground-undo"))?,
    )?;
    let undo = population.terrain_view(zone.head(), &heights)?;
    for (layer, original) in undo.layers.iter().zip(population.layers()) {
        if layer.instances != original.instances() {
            return Err("ground undo lost source placement".into());
        }
    }
    Ok(
        serde_json::json!({"moved":projected.moved_count,"maximum_vertical_error_m":maximum_error,
        "identities_and_other_attributes_exact":true,"undo_source_exact":true,"archive_reopen":true}),
    )
}

fn verify_exclusions(
    population: &ImportedPopulation,
    terrain_path: &std::ffi::OsStr,
) -> Result<serde_json::Value, Box<dyn Error>> {
    use forge_core::CommandId;
    use forge_project::{
        TerrainDocument, VegetationExclusion, VegetationExclusionId,
        zone_authoring::{exclusions::VegetationExclusionEdit, session::ZoneEditSession},
    };
    let terrain = TerrainDocument::from_json(&fs::read_to_string(terrain_path)?)?;
    let mut zone = ZoneEditSession::from_initial_terrain(terrain)?;
    let mut base = zone.head().base.clone();
    base.vegetation = Some(population.document().clone());
    let request = zone
        .head()
        .prepare_regeneration(base, CommandId::deterministic("import-exclusion-fixture"))?;
    zone.execute(&request)?;
    if population.exclusion_view(zone.head())?.excluded_count != 0 {
        return Err("initial import unexpectedly excluded".into());
    }
    let request = zone.head().prepare_vegetation_exclusions(
        VegetationExclusionEdit::Replace {
            exclusions: vec![VegetationExclusion::Circle {
                id: VegetationExclusionId::new("proof_clearing"),
                center_x_m: 27.0,
                center_z_m: -379.0,
                radius_m: 50.0,
            }],
        },
        CommandId::deterministic("import-exclusion-circle"),
    )?;
    zone.execute(&request)?;
    let mut restored = ZoneEditSession::from_archive_bytes(&zone.archive_bytes()?)?;
    let view = population.exclusion_view(restored.head())?;
    let expected = population
        .layers()
        .iter()
        .flat_map(forge_world::imported_vegetation::ImportedLayer::instances)
        .filter(|e| {
            let [x, _, z] = e.source.position_m;
            (x - 27.0).powi(2) + (z + 379.0).powi(2) <= 2500.0
        })
        .count();
    if expected == 0 || expected != view.excluded_count {
        return Err("incorrect clearing membership".into());
    }
    let visible: usize = view.layers.iter().map(|l| l.instances.len()).sum();
    if visible + expected != population.instance_count() {
        return Err("exclusion lost base records".into());
    }
    let density = verify_density(population, &mut restored)?;
    Ok(
        serde_json::json!({"excluded":expected,"visible":visible,"archive_roundtrip":true,
        "base_instances":population.instance_count(), "density":density}),
    )
}

fn paint_density(
    zone: &mut forge_project::zone_authoring::session::ZoneEditSession,
    value: Option<u8>,
) -> Result<(), String> {
    use forge_project::{
        VegetationDensityMask, zone_authoring::vegetation_commands::VegetationEdit,
    };
    let layers: Vec<_> = zone
        .head()
        .base
        .vegetation
        .as_ref()
        .ok_or("missing vegetation")?
        .layers
        .iter()
        .map(|l| l.id.clone())
        .collect();
    for layer_id in layers {
        let command = forge_core::CommandId::deterministic(format!(
            "density-proof/{:?}/{value:?}/{layer_id}",
            zone.head().revision
        ));
        let edit = value.map_or_else(
            || VegetationEdit::ClearDensityOverride {
                layer_id: layer_id.clone(),
            },
            |v| VegetationEdit::SetDensityMask {
                layer_id: layer_id.clone(),
                mask: VegetationDensityMask {
                    vertices_per_axis: 2,
                    samples_u8: vec![v; 4],
                },
            },
        );
        let request = zone.head().prepare_vegetation_edit(edit, command)?;
        zone.execute(&request)?;
    }
    Ok(())
}

fn verify_density(
    population: &ImportedPopulation,
    zone: &mut forge_project::zone_authoring::session::ZoneEditSession,
) -> Result<serde_json::Value, String> {
    use forge_project::zone_authoring::session::ZoneEditSession;
    let ids = |zone: &ZoneEditSession| -> Result<BTreeSet<_>, String> {
        Ok(population
            .authored_view(zone.head())?
            .layers
            .iter()
            .flat_map(|l| l.instances.iter().map(|e| e.id))
            .collect())
    };
    let initial = ids(zone)?;
    paint_density(zone, Some(128))?;
    let half = ids(zone)?;
    let mut restored = ZoneEditSession::from_archive_bytes(&zone.archive_bytes()?)?;
    if ids(&restored)? != half {
        return Err("density archive changed survivors".into());
    }
    paint_density(&mut restored, Some(64))?;
    let low = ids(&restored)?;
    if low.is_empty()
        || low.len() >= half.len()
        || !low.is_subset(&half)
        || !half.is_subset(&initial)
    {
        return Err("density survivors are not nested".into());
    }
    paint_density(&mut restored, Some(255))?;
    if ids(&restored)? != initial {
        return Err("full density did not restore source survivors".into());
    }
    paint_density(&mut restored, Some(0))?;
    if !ids(&restored)?.is_empty() {
        return Err("zero density left instances".into());
    }
    paint_density(&mut restored, None)?;
    if ids(&restored)? != initial {
        return Err("clearing density override did not restore inheritance".into());
    }
    Ok(
        serde_json::json!({"initial":initial.len(),"half":half.len(),"quarter":low.len(),"nested":true,"archive_survivors_exact":true,"zero_empty":true,"full_and_clear_restore":true}),
    )
}
