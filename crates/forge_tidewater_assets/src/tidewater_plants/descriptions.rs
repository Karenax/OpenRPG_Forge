use super::{BASE, COMMIT, Manifest, valid_name};
use serde::Deserialize;
use std::collections::{BTreeMap, BTreeSet};
#[derive(Deserialize)]
struct Vegetation {
    schema: String,
    version: u32,
    groups: Vec<Group>,
}
#[derive(Deserialize)]
struct Group {
    name: String,
    count: usize,
    levels: Vec<Level>,
}
#[derive(Deserialize)]
struct Level {
    attributes: BTreeMap<String, String>,
    indices: String,
}
#[derive(Deserialize)]
struct Grass {
    schema: String,
    version: u32,
    source_commit: String,
    height_sha256: forge_core::ContentHash,
    cells_per_side: u32,
    flags: String,
    min_y: String,
    max_y: String,
    mask: Mask,
    levels: Vec<Level>,
}
#[derive(Deserialize)]
struct Mask {
    width: u32,
    height: u32,
    file: String,
}
#[derive(Deserialize, PartialEq, Eq)]
#[serde(deny_unknown_fields)]
struct Texture {
    width: u32,
    height: u32,
    mips: u32,
}
#[derive(Deserialize)]
#[serde(deny_unknown_fields)]
struct Textures {
    schema: String,
    version: u32,
    leaf: Texture,
    impostor: Texture,
}
fn parse<T: serde::de::DeserializeOwned>(
    files: &BTreeMap<String, Vec<u8>>,
    name: &str,
) -> Result<T, String> {
    serde_json::from_slice(files.get(name).ok_or("Missing plant description")?)
        .map_err(|e| e.to_string())
}
pub(super) fn validate(m: &Manifest, files: &BTreeMap<String, Vec<u8>>) -> Result<(), String> {
    let plants: Vegetation = parse(files, "vegetation-scene.json")?;
    let grass: Grass = parse(files, "grass.json")?;
    let textures: Textures = parse(files, "plant-textures.json")?;
    let groups = plants
        .groups
        .iter()
        .map(|g| g.name.as_str())
        .collect::<BTreeSet<_>>();
    if plants.schema != "forge.tidewater.vegetation-scene-probe"
        || plants.version != 1
        || plants.groups.len() != 6
        || groups
            != BTreeSet::from([
                "palms",
                "bananas",
                "understory",
                "monsteras",
                "broadleaf",
                "canopy",
            ])
        || plants
            .groups
            .iter()
            .any(|g| g.count == 0 || g.count > 65_383 || !(1..=2).contains(&g.levels.len()))
        || plants.groups.iter().map(|g| g.count).sum::<usize>() != 65_383
        || grass.schema != "forge.tidewater.grass-probe"
        || grass.version != 1
        || grass.source_commit != COMMIT
        || grass.cells_per_side != 256
        || grass.levels.len() != 3
        || grass.mask.width != 1024
        || grass.mask.height != 1024
        || textures.schema != "forge.tidewater.plant-textures"
        || textures.version != 1
        || textures.leaf
            != (Texture {
                width: 1024,
                height: 1024,
                mips: 11,
            })
        || textures.impostor
            != (Texture {
                width: 3840,
                height: 768,
                mips: 12,
            })
    {
        return Err("Unsupported plant descriptions or texture dimensions".into());
    }
    // The source terrain identity is retained in grass.json for the scene adapter.
    let _ = grass.height_sha256;
    let mut required = BASE.into_iter().map(str::to_owned).collect::<BTreeSet<_>>();
    required.extend([
        grass.flags.clone(),
        grass.min_y.clone(),
        grass.max_y.clone(),
        grass.mask.file.clone(),
    ]);
    for level in plants
        .groups
        .iter()
        .flat_map(|g| g.levels.iter())
        .chain(&grass.levels)
    {
        if level.attributes.is_empty()
            || level.attributes.len() > 16
            || !level.attributes.contains_key("position")
            || !level.attributes.contains_key("normal")
        {
            return Err("Incomplete plant attributes".into());
        }
        required.insert(level.indices.clone());
        required.extend(level.attributes.values().cloned());
    }
    if required.iter().any(|n| !valid_name(n)) || required != m.files.keys().cloned().collect() {
        return Err("Plant dependency closure differs".into());
    }
    for (name, size) in [
        ("detail.bin", 1_048_576),
        ("leaf.bin", 5_592_404),
        ("impostor-a.bin", 15_728_624),
        ("impostor-b.bin", 15_728_624),
        (&grass.flags, 65_536),
        (&grass.min_y, 262_144),
        (&grass.max_y, 262_144),
        (&grass.mask.file, 4_194_304),
    ] {
        if m.files.get(name).is_none_or(|e| e.bytes != size) {
            return Err(format!("Plant resource shape differs: {name}"));
        }
    }
    Ok(())
}
