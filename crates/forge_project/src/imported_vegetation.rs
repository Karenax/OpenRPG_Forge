//! Immutable, content-addressed placements referenced by the canonical vegetation document.
//! No filesystem path, GPU handle or second editable world is stored here.
use crate::{VegetationExtent, VegetationLayer};
use forge_core::{AreaId, ContentHash, EntityId, ProjectId, bytes_sha256};
use serde::{Deserialize, Serialize};
use std::collections::BTreeSet;

pub const MAX_IMPORTED_INSTANCES: u32 = 131_072;
pub const MAX_IMPORTED_PARAMETERS: usize = 16;

#[derive(Clone, Debug, PartialEq, Serialize, Deserialize)]
#[serde(deny_unknown_fields)]
pub struct ImportedVegetationSource {
    pub schema: String,
    pub version: u32,
    pub blocks: Vec<ImportedVegetationBlock>,
}

#[derive(Clone, Debug, PartialEq, Serialize, Deserialize)]
#[serde(deny_unknown_fields)]
pub struct ImportedVegetationBlock {
    pub layer_id: String,
    /// Logical prototype key; its governed asset binding is resolved separately.
    pub prototype_key: String,
    pub count: u32,
    pub encoding: ImportedInstanceEncoding,
    pub instances_sha256: ContentHash,
    /// Ordered prototype-specific values, independent of GPU attribute locations.
    pub parameter_names: Vec<String>,
}

#[derive(Clone, Copy, Debug, PartialEq, Eq, Serialize, Deserialize)]
#[serde(rename_all = "snake_case")]
pub enum ImportedInstanceEncoding {
    /// Sorted 32-byte source key, translation(3), quaternion XYZW(4), scale(3), parameters.
    /// All numbers are IEEE754 f64 little-endian. No padding, header or trailing bytes.
    TransformF64LeV1,
}

#[derive(Clone, Debug, PartialEq)]
pub struct ImportedVegetationInstance {
    pub source_key: ContentHash,
    pub position_m: [f64; 3],
    pub rotation_xyzw: [f64; 4],
    pub scale: [f64; 3],
    pub parameters: Vec<f64>,
}

fn key(value: &str) -> bool {
    !value.is_empty()
        && value.len() <= 64
        && value
            .bytes()
            .all(|c| c.is_ascii_lowercase() || c.is_ascii_digit() || c == b'_')
}

impl ImportedVegetationSource {
    pub fn validate(
        &self,
        extent: VegetationExtent,
        layers: &[VegetationLayer],
    ) -> Result<(), String> {
        if self.schema != "forge.imported-vegetation" || self.version != 1 {
            return Err("Unsupported imported vegetation source".into());
        }
        if self.blocks.is_empty() || self.blocks.len() > 16 || self.blocks.len() != layers.len() {
            return Err("Imported vegetation must cover every layer exactly once".into());
        }
        for size in [extent.size_x_m, extent.size_z_m] {
            if !size.is_finite() || !(64.0..=4096.0).contains(&size) || size % 64.0 != 0.0 {
                return Err("Imported vegetation extent must use 64 m chunks, up to 4096 m".into());
            }
        }
        let mut seen = BTreeSet::new();
        let mut count = 0_u64;
        for block in &self.blocks {
            block.validate()?;
            if !seen.insert(&block.layer_id)
                || !layers.iter().any(|l| l.id.as_str() == block.layer_id)
            {
                return Err("Duplicate or unknown imported vegetation layer".into());
            }
            count += u64::from(block.count);
        }
        if count > u64::from(MAX_IMPORTED_INSTANCES) {
            return Err("Imported vegetation instance budget exceeded".into());
        }
        Ok(())
    }
}

impl ImportedVegetationBlock {
    pub fn validate(&self) -> Result<(), String> {
        if !key(&self.layer_id)
            || !key(&self.prototype_key)
            || self.count == 0
            || self.count > MAX_IMPORTED_INSTANCES
        {
            return Err("Invalid imported vegetation block identity or count".into());
        }
        if self.parameter_names.len() > MAX_IMPORTED_PARAMETERS {
            return Err("Too many imported prototype parameters".into());
        }
        let mut names = BTreeSet::new();
        if self
            .parameter_names
            .iter()
            .any(|n| !key(n) || !names.insert(n))
        {
            return Err("Invalid or duplicate imported parameter name".into());
        }
        Ok(())
    }

    pub fn byte_len(&self) -> Result<usize, String> {
        self.validate()?;
        let count = usize::try_from(self.count).map_err(|_| "Imported count overflow")?;
        count
            .checked_mul(32 + 8 * (10 + self.parameter_names.len()))
            .ok_or_else(|| "Imported size overflow".into())
    }

    /// Validate bytes before exposing instances. Ordered source keys survive reimport and edits.
    pub fn decode(
        &self,
        bytes: &[u8],
        extent: VegetationExtent,
    ) -> Result<Vec<ImportedVegetationInstance>, String> {
        if bytes.len() != self.byte_len()? || bytes_sha256(bytes) != self.instances_sha256 {
            return Err("Imported instance length or hash mismatch".into());
        }
        if [extent.size_x_m, extent.size_z_m]
            .iter()
            .any(|v| !v.is_finite() || *v <= 0.0 || *v > 4096.0)
        {
            return Err("Invalid imported instance extent".into());
        }
        let stride = 32 + 8 * (10 + self.parameter_names.len());
        let mut previous = None;
        let mut records = Vec::new();
        for row in bytes.chunks_exact(stride) {
            let raw_key: [u8; 32] = row[..32].try_into().map_err(|_| "source key size")?;
            if previous.is_some_and(|p| p >= raw_key) {
                return Err("Imported source keys must be unique and sorted".into());
            }
            previous = Some(raw_key);
            let numbers: Vec<f64> = row[32..]
                .as_chunks::<8>()
                .0
                .iter()
                .map(|b| f64::from_le_bytes(*b))
                .collect();
            if numbers.iter().any(|v| !v.is_finite()) {
                return Err("Nonfinite imported instance".into());
            }
            let position_m = [numbers[0], numbers[1], numbers[2]];
            let rotation_xyzw = [numbers[3], numbers[4], numbers[5], numbers[6]];
            let scale = [numbers[7], numbers[8], numbers[9]];
            if position_m[0].abs() > f64::from(extent.size_x_m) * 0.5
                || position_m[2].abs() > f64::from(extent.size_z_m) * 0.5
                || position_m[1].abs() > 100_000.0
                || scale.iter().any(|v| *v <= 0.0 || *v > 10_000.0)
                || (rotation_xyzw.iter().map(|v| v * v).sum::<f64>() - 1.0).abs() > 1e-9
            {
                return Err("Invalid imported transform or out-of-area placement".into());
            }
            records.push(ImportedVegetationInstance {
                source_key: ContentHash::from_bytes(raw_key),
                position_m,
                rotation_xyzw,
                scale,
                parameters: numbers[10..].to_vec(),
            });
        }
        Ok(records)
    }

    /// Stable within an authored area, independent of blob hash, order, revision and LOD.
    #[must_use]
    pub fn instance_id(
        &self,
        project: ProjectId,
        area: AreaId,
        source_key: ContentHash,
    ) -> EntityId {
        EntityId::deterministic(format!(
            "imported-vegetation-v1/{project}/{area}/{}/{source_key}",
            self.layer_id
        ))
    }
}
