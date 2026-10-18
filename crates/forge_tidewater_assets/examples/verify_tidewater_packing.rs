use forge_project::VegetationDocument;
use std::{error::Error, fs, path::PathBuf};
use {
    forge_tidewater_assets::tidewater_vegetation::pack,
    forge_world::imported_vegetation::ImportedPopulation,
};
fn main() -> Result<(), Box<dyn Error>> {
    let args: Vec<_> = std::env::args_os().skip(1).collect();
    if args.len() != 2 {
        return Err("usage: verify_tidewater_packing IMPORT_DIRECTORY NEW_OUTPUT".into());
    }
    let root = PathBuf::from(&args[0]);
    let out = PathBuf::from(&args[1]);
    let doc = VegetationDocument::from_json(&fs::read_to_string(root.join("vegetation.v2.json"))?)?;
    let population = ImportedPopulation::resolve(&doc, |hash, size| {
        let path = root.join(format!("{hash}.bin"));
        if fs::metadata(&path).map_err(|e| e.to_string())?.len() != size as u64 {
            return Err("blob size".into());
        }
        fs::read(path).map_err(|e| e.to_string())
    })?;
    fs::create_dir(&out)?;
    let mut layers = Vec::new();
    let mut ordered =
        std::collections::BTreeMap::<&str, std::collections::BTreeMap<u32, Vec<u8>>>::new();
    for layer in population.layers() {
        let mut bytes = Vec::new();
        let mut group = "";
        for entity in layer.instances() {
            let packed = pack(&layer.prototype_key, &layer.parameter_names, entity)?;
            group = packed.group;
            bytes.extend(entity.source.source_key.as_bytes());
            let start = bytes.len();
            for v in packed
                .matrix
                .iter()
                .chain(&packed.position)
                .chain(&packed.data)
                .chain(std::iter::once(&packed.query_radius_squared))
            {
                bytes.extend(v.to_le_bytes());
            }
            if let Some(order) = packed.source_order
                && ordered
                    .entry(group)
                    .or_default()
                    .insert(order, bytes[start..].to_vec())
                    .is_some()
            {
                return Err("duplicate source order in group".into());
            }
        }
        let name = format!("{}.bin", layer.layer_id);
        fs::write(out.join(&name), &bytes)?;
        layers.push(serde_json::json!({"layer":layer.layer_id,"group":group,"file":name,"count":layer.instances().len(),"sha256":forge_core::bytes_sha256(&bytes)}));
    }
    let mut groups = Vec::new();
    for (group, records) in ordered {
        if records.keys().copied().ne(0..u32::try_from(records.len())?) {
            return Err("source order has gaps".into());
        }
        let count = records.len();
        let bytes: Vec<_> = records.into_values().flatten().collect();
        let file = format!("group-{group}.bin");
        fs::write(out.join(&file), &bytes)?;
        groups.push(serde_json::json!({"name":group,"file":file,"count":count,"sha256":forge_core::bytes_sha256(&bytes)}));
    }
    fs::write(
        out.join("packing.json"),
        serde_json::to_vec_pretty(
            &serde_json::json!({"layers":layers,"groups":groups,"count":population.instance_count()}),
        )?,
    )?;
    println!("Packed {} canonical plants", population.instance_count());
    Ok(())
}
