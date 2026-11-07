//! Decoded, derived CPU inputs; the renderer must still prepare and admit GPU resources.
use super::PlantPackage;
use crate::source_vegetation_geometry::SourceVegetationGeometry;
use serde::Deserialize;
use std::collections::BTreeMap;
pub const MAX_DECODED_PLANT_BYTES: usize = 256 * 1024 * 1024;

#[derive(Deserialize)]
struct Description {
    groups: Vec<Group>,
}
#[derive(Deserialize)]
struct Group {
    name: String,
    levels: Vec<Level>,
}
#[derive(Deserialize)]
struct Grass {
    levels: Vec<Level>,
}
#[derive(Deserialize)]
struct Level {
    attributes: BTreeMap<String, String>,
    indices: String,
}

pub struct PlantGeometry {
    pub groups: BTreeMap<String, Vec<SourceVegetationGeometry>>,
    pub grass: Vec<SourceVegetationGeometry>,
}

impl PlantPackage {
    pub fn decode_geometry(&self) -> Result<PlantGeometry, String> {
        let plants: Description =
            serde_json::from_slice(self.file("vegetation-scene.json").ok_or("Missing plants")?)
                .map_err(|e| e.to_string())?;
        let grass: Grass = serde_json::from_slice(self.file("grass.json").ok_or("Missing grass")?)
            .map_err(|e| e.to_string())?;
        let mut bytes = 0_usize;
        for level in plants
            .groups
            .iter()
            .flat_map(|group| &group.levels)
            .chain(&grass.levels)
        {
            for name in level
                .attributes
                .values()
                .map(String::as_str)
                .chain(std::iter::once(level.indices.as_str()))
            {
                bytes = bytes
                    .checked_add(self.file(name).ok_or("Missing geometry resource")?.len())
                    .ok_or("Decoded geometry byte count overflow")?;
                if bytes > MAX_DECODED_PLANT_BYTES {
                    return Err("Decoded plant geometry payload budget exceeded".into());
                }
            }
        }
        let decode = |level: Level| {
            SourceVegetationGeometry::decode(&level.attributes, &level.indices, |name| {
                self.file(name)
                    .ok_or_else(|| format!("Missing geometry resource: {name}"))
            })
        };
        let mut groups = BTreeMap::new();
        for group in plants.groups {
            if groups
                .insert(
                    group.name,
                    group
                        .levels
                        .into_iter()
                        .map(decode)
                        .collect::<Result<_, _>>()?,
                )
                .is_some()
            {
                return Err("Duplicate decoded plant group".into());
            }
        }
        Ok(PlantGeometry {
            groups,
            grass: grass
                .levels
                .into_iter()
                .map(decode)
                .collect::<Result<_, _>>()?,
        })
    }
}
