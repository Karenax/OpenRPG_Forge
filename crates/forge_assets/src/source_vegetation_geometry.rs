//! Packed source geometry without renderer handles or population/dataset assumptions.
//! The caller must verify resource hashes and the closed package before decoding.
use std::collections::BTreeMap;

pub const MAX_SOURCE_VEGETATION_VERTICES: usize = 100_000;
const MAX_INDEX_BYTES: usize = 64 * 1024 * 1024;
const ATTRIBUTES: [&str; 6] = ["position", "normal", "uv", "aVeg", "aMat", "aLobe"];

/// Local-space streams in their original order and precision. Source absence is
/// preserved; any shader input defaults belong to the renderer adapter.
pub struct SourceVegetationGeometry {
    pub positions: Vec<[f32; 3]>,
    pub normals: Vec<[f32; 3]>,
    pub uv: Option<Vec<[f32; 2]>>,
    pub vegetation: Option<Vec<[f32; 4]>>,
    pub material: Option<Vec<[f32; 4]>>,
    pub lobe: Option<Vec<[f32; 4]>>,
    pub indices: Vec<u32>,
}

impl SourceVegetationGeometry {
    /// Decode the existing flat f32/u32 little-endian source layout. No GLB,
    /// normals, UV conversion, vertex welding, LOD or instance data are invented.
    pub fn decode<'a>(
        attributes: &BTreeMap<String, String>,
        indices: &str,
        mut read: impl FnMut(&str) -> Result<&'a [u8], String>,
    ) -> Result<Self, String> {
        if attributes
            .keys()
            .any(|name| !ATTRIBUTES.contains(&name.as_str()))
        {
            return Err("Unsupported source vegetation attribute".into());
        }
        let position = attributes
            .get("position")
            .ok_or("Missing source positions")?;
        let normal = attributes.get("normal").ok_or("Missing source normals")?;
        let bytes = read(position)?;
        if bytes.is_empty()
            || !bytes.len().is_multiple_of(12)
            || bytes.len() / 12 > MAX_SOURCE_VEGETATION_VERTICES
        {
            return Err("Source vegetation vertex budget or layout differs".into());
        }
        let count = bytes.len() / 12;
        let positions = vectors::<3>(bytes, count)?;
        let normals = vectors::<3>(read(normal)?, count)?;
        let mut optional = |name: &str| attributes.get(name).map(|path| read(path)).transpose();
        let uv = optional("uv")?
            .map(|b| vectors::<2>(b, count))
            .transpose()?;
        let vegetation = optional("aVeg")?
            .map(|b| vectors::<4>(b, count))
            .transpose()?;
        let material = optional("aMat")?
            .map(|b| vectors::<4>(b, count))
            .transpose()?;
        let lobe = optional("aLobe")?
            .map(|b| vectors::<4>(b, count))
            .transpose()?;
        let bytes = read(indices)?;
        if bytes.is_empty() || bytes.len() > MAX_INDEX_BYTES || !bytes.len().is_multiple_of(12) {
            return Err("Source vegetation triangle budget or layout differs".into());
        }
        let indices = bytes
            .as_chunks::<4>()
            .0
            .iter()
            .map(|value| {
                let index = u32::from_le_bytes(*value);
                if usize::try_from(index).is_ok_and(|index| index < count) {
                    Ok(index)
                } else {
                    Err("Source vegetation index references an absent vertex".to_owned())
                }
            })
            .collect::<Result<_, _>>()?;
        Ok(Self {
            positions,
            normals,
            uv,
            vegetation,
            material,
            lobe,
            indices,
        })
    }
}

fn vectors<const N: usize>(bytes: &[u8], count: usize) -> Result<Vec<[f32; N]>, String> {
    if count.checked_mul(N * 4) != Some(bytes.len()) {
        return Err("Source vegetation attribute count differs".into());
    }
    bytes
        .as_chunks::<4>()
        .0
        .as_chunks::<N>()
        .0
        .iter()
        .map(|chunk| {
            let values = std::array::from_fn(|index| f32::from_le_bytes(chunk[index]));
            if values.iter().all(|value| value.is_finite()) {
                Ok(values)
            } else {
                Err("Source vegetation attribute is nonfinite".into())
            }
        })
        .collect()
}
