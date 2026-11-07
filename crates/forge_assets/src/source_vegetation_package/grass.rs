//! Decoder for the pinned grass source profile. Rendering and selection are engine responsibilities.
use super::{PlantPackage, scene::Level};
use crate::source_vegetation_geometry::SourceVegetationGeometry;
use serde::Deserialize;

const CELL_COUNT: usize = 256 * 256;

#[derive(Deserialize)]
struct Description {
    schema: String,
    version: u32,
    cells_per_side: usize,
    flags: String,
    min_y: String,
    max_y: String,
    levels: Vec<Level>,
    #[serde(default)]
    height_sha256: Option<forge_core::ContentHash>,
}

/// Original grid and vertex/index order. No source population is generated here.
pub struct Decoded {
    pub flags: Vec<u8>,
    pub min_y: Vec<f32>,
    pub max_y: Vec<f32>,
    pub meshes: [SourceVegetationGeometry; 3],
    pub source_height: Option<forge_core::ContentHash>,
}

impl PlantPackage {
    pub fn grass(&self) -> Result<Decoded, String> {
        decode(|name| self.file(name), None)
    }

    /// Reuse geometry already decoded from this same closed package.
    pub fn grass_from_geometry(
        &self,
        meshes: Vec<SourceVegetationGeometry>,
    ) -> Result<Decoded, String> {
        let meshes = meshes.try_into().map_err(|_| "Invalid grass level count")?;
        decode(|name| self.file(name), Some(meshes))
    }
}

fn decode<'a>(
    read: impl Fn(&str) -> Option<&'a [u8]>,
    meshes: Option<[SourceVegetationGeometry; 3]>,
) -> Result<Decoded, String> {
    let get = |name: &str| read(name).ok_or_else(|| format!("Missing grass resource: {name}"));
    let description: Description =
        serde_json::from_slice(get("grass.json")?).map_err(|error| error.to_string())?;
    if description.schema != "forge.tidewater.grass-probe"
        || description.version != 1
        || description.cells_per_side != 256
        || description.levels.len() != 3
    {
        return Err("Unsupported grass description".into());
    }
    let heights = |name: &str| -> Result<Vec<f32>, String> {
        let bytes = get(name)?;
        if bytes.len() != CELL_COUNT * size_of::<f32>() {
            return Err(format!("Invalid grass height payload: {name}"));
        }
        Ok(bytes
            .as_chunks::<4>()
            .0
            .iter()
            .map(|chunk| f32::from_le_bytes(*chunk))
            .collect())
    };
    let flags = get(&description.flags)?.to_vec();
    let min_y = heights(&description.min_y)?;
    let max_y = heights(&description.max_y)?;
    if flags.len() != CELL_COUNT
        || flags.iter().any(|flag| *flag > 1)
        || min_y
            .iter()
            .zip(&max_y)
            .any(|(lo, hi)| !lo.is_finite() || !hi.is_finite() || lo > hi)
    {
        return Err("Invalid grass grid values".into());
    }
    let meshes = if let Some(meshes) = meshes {
        meshes
    } else {
        description
            .levels
            .iter()
            .map(|level| SourceVegetationGeometry::decode(&level.attributes, &level.indices, get))
            .collect::<Result<Vec<_>, _>>()?
            .try_into()
            .map_err(|_| "Invalid grass level count")?
    };
    Ok(Decoded {
        flags,
        min_y,
        max_y,
        meshes,
        source_height: description.height_sha256,
    })
}
