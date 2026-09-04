use crate::{MAX_VEGETATION_LAYERS, VegetationLayerId};
use forge_core::{
    AreaId, CanonicalJsonError, ContentHash, CoordinateSystem, ProjectId, Revision,
    canonical_json_sha256,
};
use serde::{Deserialize, Serialize};
use std::collections::HashSet;
use thiserror::Error;

pub const VEGETATION_DENSITY_SCHEMA: &str = "forge.vegetation-density";
pub const VEGETATION_DENSITY_SCHEMA_VERSION: u32 = 1;
pub const MAX_VEGETATION_DENSITY_OVERRIDES: usize = 32_768;
pub const MAX_VEGETATION_DENSITY_JSON_BYTES: usize = 1_048_576;
pub const VEGETATION_DENSITY_CHUNKS_PER_AXIS: u16 = 8;

/// Sparse, quantized density mask for the procedural vegetation recipe of one area.
///
/// Coordinates are global vegetation-cell indices. Both axes start at the negative edge of the
/// 512 metre area and increase toward its positive edge. A density of 0 disables placement in a
/// cell; 255 preserves the layer recipe's complete acceptance probability.
#[derive(Clone, Debug, PartialEq, Serialize, Deserialize)]
#[serde(deny_unknown_fields)]
pub struct VegetationDensityDocument {
    pub schema: String,
    pub schema_version: u32,
    pub project_id: ProjectId,
    pub area_id: AreaId,
    pub revision: Revision,
    pub coordinate_system: CoordinateSystem,
    pub name: String,
    pub cells_per_chunk_axis: u16,
    pub layers: Vec<VegetationDensityLayer>,
}

impl VegetationDensityDocument {
    #[must_use]
    pub fn new(
        project_id: ProjectId,
        area_id: AreaId,
        revision: Revision,
        name: impl Into<String>,
        cells_per_chunk_axis: u16,
        layers: Vec<VegetationDensityLayer>,
    ) -> Self {
        Self {
            schema: VEGETATION_DENSITY_SCHEMA.to_owned(),
            schema_version: VEGETATION_DENSITY_SCHEMA_VERSION,
            project_id,
            area_id,
            revision,
            coordinate_system: CoordinateSystem::METRES_Y_UP_RIGHT_HANDED,
            name: name.into(),
            cells_per_chunk_axis,
            layers,
        }
    }

    pub fn validate(&self) -> Result<(), VegetationDensityError> {
        if self.schema != VEGETATION_DENSITY_SCHEMA {
            return Err(VegetationDensityError::InvalidSchema(self.schema.clone()));
        }
        if self.schema_version != VEGETATION_DENSITY_SCHEMA_VERSION {
            return Err(VegetationDensityError::UnsupportedVersion(
                self.schema_version,
            ));
        }
        if self.coordinate_system != CoordinateSystem::METRES_Y_UP_RIGHT_HANDED {
            return Err(VegetationDensityError::UnsupportedCoordinateSystem);
        }
        if self.project_id.as_uuid().is_nil() {
            return Err(VegetationDensityError::NilIdentity("project_id"));
        }
        if self.area_id.as_uuid().is_nil() {
            return Err(VegetationDensityError::NilIdentity("area_id"));
        }
        if self.revision.get() == 0 {
            return Err(VegetationDensityError::InvalidRevision);
        }
        if self.name.trim().is_empty() || self.name.chars().any(char::is_control) {
            return Err(VegetationDensityError::InvalidName);
        }
        if self.name.len() > 128 {
            return Err(VegetationDensityError::NameTooLong(self.name.len()));
        }
        if !(crate::MIN_VEGETATION_CELLS_PER_CHUNK_AXIS
            ..=crate::MAX_VEGETATION_CELLS_PER_CHUNK_AXIS)
            .contains(&self.cells_per_chunk_axis)
        {
            return Err(VegetationDensityError::InvalidCellsPerChunkAxis(
                self.cells_per_chunk_axis,
            ));
        }
        if self.layers.is_empty() || self.layers.len() > MAX_VEGETATION_LAYERS {
            return Err(VegetationDensityError::InvalidLayerCount(self.layers.len()));
        }

        let cells_per_axis = self.cells_per_axis();
        let mut layer_ids = HashSet::with_capacity(self.layers.len());
        let mut total_overrides = 0_usize;
        for layer in &self.layers {
            if !layer_ids.insert(layer.layer_id.as_str()) {
                return Err(VegetationDensityError::DuplicateLayerId(
                    layer.layer_id.to_string(),
                ));
            }
            total_overrides = total_overrides
                .checked_add(layer.overrides.len())
                .ok_or(VegetationDensityError::TooManyOverrides(usize::MAX))?;
            layer.validate(cells_per_axis)?;
        }
        if total_overrides > MAX_VEGETATION_DENSITY_OVERRIDES {
            return Err(VegetationDensityError::TooManyOverrides(total_overrides));
        }
        Ok(())
    }

    #[must_use]
    pub const fn cells_per_axis(&self) -> u16 {
        self.cells_per_chunk_axis * VEGETATION_DENSITY_CHUNKS_PER_AXIS
    }

    /// Returns the quantized density for one global vegetation cell.
    ///
    /// `None` means that the layer is unknown or either coordinate is outside the mask grid.
    #[must_use]
    pub fn density_u8(&self, layer_id: &VegetationLayerId, cell_x: u16, cell_z: u16) -> Option<u8> {
        if cell_x >= self.cells_per_axis() || cell_z >= self.cells_per_axis() {
            return None;
        }
        let layer = self
            .layers
            .iter()
            .find(|layer| layer.layer_id == *layer_id)?;
        let key = (cell_z, cell_x);
        match layer
            .overrides
            .binary_search_by_key(&key, |entry| entry.sort_key())
        {
            Ok(index) => Some(layer.overrides[index].density()),
            Err(_) => Some(layer.default_density),
        }
    }

    /// Converts the quantized mask value to the multiplier applied to recipe acceptance.
    #[must_use]
    pub fn density_multiplier(
        &self,
        layer_id: &VegetationLayerId,
        cell_x: u16,
        cell_z: u16,
    ) -> Option<f32> {
        self.density_u8(layer_id, cell_x, cell_z)
            .map(|density| f32::from(density) / f32::from(u8::MAX))
    }

    pub fn from_json(json: &str) -> Result<Self, VegetationDensityError> {
        ensure_json_budget(json.len())?;
        let document: Self = serde_json::from_str(json)?;
        document.validate()?;
        ensure_json_budget(serde_json::to_string_pretty(&document)?.len())?;
        Ok(document)
    }

    pub fn to_pretty_json(&self) -> Result<String, VegetationDensityError> {
        self.validate()?;
        let json = serde_json::to_string_pretty(self)?;
        ensure_json_budget(json.len())?;
        Ok(json)
    }

    pub fn content_hash(&self) -> Result<ContentHash, VegetationDensityError> {
        self.validate()?;
        ensure_json_budget(serde_json::to_string_pretty(self)?.len())?;
        Ok(canonical_json_sha256(self)?)
    }
}

#[derive(Clone, Debug, PartialEq, Serialize, Deserialize)]
#[serde(deny_unknown_fields)]
pub struct VegetationDensityLayer {
    pub layer_id: VegetationLayerId,
    pub default_density: u8,
    #[serde(default, skip_serializing_if = "Vec::is_empty")]
    pub overrides: Vec<VegetationDensityOverride>,
}

impl VegetationDensityLayer {
    #[must_use]
    pub fn new(
        layer_id: VegetationLayerId,
        default_density: u8,
        overrides: Vec<VegetationDensityOverride>,
    ) -> Self {
        Self {
            layer_id,
            default_density,
            overrides,
        }
    }

    fn validate(&self, cells_per_axis: u16) -> Result<(), VegetationDensityError> {
        let id = self.layer_id.as_str();
        if id.is_empty()
            || id.len() > 64
            || !id.as_bytes()[0].is_ascii_lowercase()
            || !id
                .bytes()
                .all(|byte| byte.is_ascii_lowercase() || byte.is_ascii_digit() || byte == b'_')
        {
            return Err(VegetationDensityError::InvalidLayerId(id.to_owned()));
        }
        let mut previous = None;
        for entry in &self.overrides {
            let key = entry.sort_key();
            if entry.cell_x() >= cells_per_axis || entry.cell_z() >= cells_per_axis {
                return Err(VegetationDensityError::CellOutOfBounds {
                    layer_id: id.to_owned(),
                    cell_x: entry.cell_x(),
                    cell_z: entry.cell_z(),
                    cells_per_axis,
                });
            }
            if entry.density() == self.default_density {
                return Err(VegetationDensityError::RedundantOverride {
                    layer_id: id.to_owned(),
                    cell_x: entry.cell_x(),
                    cell_z: entry.cell_z(),
                });
            }
            if previous.is_some_and(|previous| previous >= key) {
                return Err(VegetationDensityError::OverridesNotCanonical(id.to_owned()));
            }
            previous = Some(key);
        }
        Ok(())
    }
}

/// Compact JSON tuple `[cell_x, cell_z, density]` for one non-default cell.
#[derive(Clone, Copy, Debug, Eq, PartialEq, Serialize, Deserialize)]
pub struct VegetationDensityOverride(u16, u16, u8);

impl VegetationDensityOverride {
    #[must_use]
    pub const fn new(cell_x: u16, cell_z: u16, density: u8) -> Self {
        Self(cell_x, cell_z, density)
    }

    #[must_use]
    pub const fn cell_x(self) -> u16 {
        self.0
    }

    #[must_use]
    pub const fn cell_z(self) -> u16 {
        self.1
    }

    #[must_use]
    pub const fn density(self) -> u8 {
        self.2
    }

    const fn sort_key(self) -> (u16, u16) {
        (self.1, self.0)
    }
}

fn ensure_json_budget(bytes: usize) -> Result<(), VegetationDensityError> {
    if bytes > MAX_VEGETATION_DENSITY_JSON_BYTES {
        Err(VegetationDensityError::JsonTooLarge {
            bytes,
            maximum: MAX_VEGETATION_DENSITY_JSON_BYTES,
        })
    } else {
        Ok(())
    }
}

#[derive(Debug, Error)]
pub enum VegetationDensityError {
    #[error("invalid vegetation density schema `{0}`")]
    InvalidSchema(String),
    #[error("unsupported vegetation density schema version {0}")]
    UnsupportedVersion(u32),
    #[error("vegetation density coordinates must be metres, Y-up, right-handed")]
    UnsupportedCoordinateSystem,
    #[error("vegetation density {0} cannot be the nil UUID")]
    NilIdentity(&'static str),
    #[error("vegetation density revision must be positive")]
    InvalidRevision,
    #[error("vegetation density name cannot be blank or contain control characters")]
    InvalidName,
    #[error("vegetation density name exceeds 128 bytes: {0}")]
    NameTooLong(usize),
    #[error("vegetation density cells_per_chunk_axis is invalid: {0}")]
    InvalidCellsPerChunkAxis(u16),
    #[error("vegetation density layer count is invalid: {0}")]
    InvalidLayerCount(usize),
    #[error("invalid vegetation density layer id `{0}`")]
    InvalidLayerId(String),
    #[error("duplicate vegetation density layer id `{0}`")]
    DuplicateLayerId(String),
    #[error(
        "vegetation density supports at most {MAX_VEGETATION_DENSITY_OVERRIDES} overrides, found {0}"
    )]
    TooManyOverrides(usize),
    #[error("vegetation density overrides for `{0}` are not strictly ordered by cell z then x")]
    OverridesNotCanonical(String),
    #[error(
        "vegetation density override for `{layer_id}` at ({cell_x}, {cell_z}) is outside a {cells_per_axis} by {cells_per_axis} grid"
    )]
    CellOutOfBounds {
        layer_id: String,
        cell_x: u16,
        cell_z: u16,
        cells_per_axis: u16,
    },
    #[error(
        "vegetation density override for `{layer_id}` at ({cell_x}, {cell_z}) repeats the layer default"
    )]
    RedundantOverride {
        layer_id: String,
        cell_x: u16,
        cell_z: u16,
    },
    #[error("vegetation density JSON contains {bytes} bytes, exceeding the {maximum} byte limit")]
    JsonTooLarge { bytes: usize, maximum: usize },
    #[error("invalid vegetation density JSON: {0}")]
    Json(#[from] serde_json::Error),
    #[error("cannot hash vegetation density document: {0}")]
    Hash(#[from] CanonicalJsonError),
}

#[cfg(test)]
mod tests {
    use super::*;

    fn document() -> VegetationDensityDocument {
        VegetationDensityDocument::new(
            ProjectId::deterministic("density/project"),
            AreaId::deterministic("density/area"),
            Revision::INITIAL,
            "Density",
            8,
            vec![VegetationDensityLayer::new(
                VegetationLayerId::new("forge_grass"),
                0,
                vec![
                    VegetationDensityOverride::new(1, 2, 128),
                    VegetationDensityOverride::new(2, 2, 255),
                ],
            )],
        )
    }

    #[test]
    fn compact_mask_round_trips_and_hashes_deterministically() {
        let document = document();
        let pretty = document.to_pretty_json().unwrap();
        let compact = serde_json::to_string(&document).unwrap();
        assert!(compact.contains("[1,2,128]"));
        for json in [pretty, compact] {
            let restored = VegetationDensityDocument::from_json(&json).unwrap();
            assert_eq!(restored, document);
            assert_eq!(
                restored.content_hash().unwrap(),
                document.content_hash().unwrap()
            );
        }
    }

    #[test]
    fn lookup_uses_quantized_overrides_defaults_and_explicit_bounds() {
        let document = document();
        let layer = VegetationLayerId::new("forge_grass");
        assert_eq!(document.density_u8(&layer, 1, 2), Some(128));
        assert_eq!(document.density_u8(&layer, 0, 0), Some(0));
        assert_eq!(document.density_multiplier(&layer, 2, 2), Some(1.0));
        assert_eq!(document.density_u8(&layer, 64, 0), None);
        assert_eq!(
            document.density_u8(&VegetationLayerId::new("unknown"), 0, 0),
            None
        );
    }

    #[test]
    fn invalid_sparse_forms_fail_closed() {
        let mut changed = document();
        changed.layers[0].overrides.swap(0, 1);
        assert!(matches!(
            changed.validate(),
            Err(VegetationDensityError::OverridesNotCanonical(_))
        ));
        let mut changed = document();
        changed.layers[0].overrides[1] = VegetationDensityOverride::new(1, 2, 255);
        assert!(matches!(
            changed.validate(),
            Err(VegetationDensityError::OverridesNotCanonical(_))
        ));
        let mut changed = document();
        changed.layers[0].overrides[0] = VegetationDensityOverride::new(64, 0, 128);
        assert!(matches!(
            changed.validate(),
            Err(VegetationDensityError::CellOutOfBounds { .. })
        ));
        let mut changed = document();
        changed.layers[0].overrides[0] = VegetationDensityOverride::new(1, 2, 0);
        assert!(matches!(
            changed.validate(),
            Err(VegetationDensityError::RedundantOverride { .. })
        ));
    }

    #[test]
    fn schemas_layer_identities_and_tuple_shape_fail_closed() {
        let mut changed = document();
        changed.schema = "other.density".to_owned();
        assert!(matches!(
            changed.validate(),
            Err(VegetationDensityError::InvalidSchema(_))
        ));
        let mut changed = document();
        changed.schema_version += 1;
        assert!(matches!(
            changed.validate(),
            Err(VegetationDensityError::UnsupportedVersion(2))
        ));
        let mut changed = document();
        changed.layers.push(changed.layers[0].clone());
        assert!(matches!(
            changed.validate(),
            Err(VegetationDensityError::DuplicateLayerId(_))
        ));

        let mut value = serde_json::to_value(document()).unwrap();
        value["unknown"] = serde_json::json!(true);
        assert!(matches!(
            VegetationDensityDocument::from_json(&value.to_string()),
            Err(VegetationDensityError::Json(_))
        ));
        value.as_object_mut().unwrap().remove("unknown");
        value["layers"][0]["overrides"][0] = serde_json::json!([1, 2]);
        assert!(matches!(
            VegetationDensityDocument::from_json(&value.to_string()),
            Err(VegetationDensityError::Json(_))
        ));
    }

    #[test]
    fn standalone_json_budget_rejects_an_excessive_sparse_mask() {
        let overrides = (0_u16..128)
            .flat_map(|cell_z| {
                (0_u16..128)
                    .map(move |cell_x| VegetationDensityOverride::new(cell_x, cell_z, u8::MAX))
            })
            .collect::<Vec<_>>();
        let mut document = document();
        document.cells_per_chunk_axis = 16;
        document.layers = vec![
            VegetationDensityLayer::new(
                VegetationLayerId::new("forge_grass"),
                0,
                overrides.clone(),
            ),
            VegetationDensityLayer::new(VegetationLayerId::new("forge_ferns"), 0, overrides),
        ];
        document.validate().unwrap();
        assert!(matches!(
            document.to_pretty_json(),
            Err(VegetationDensityError::JsonTooLarge { .. })
        ));
        assert!(matches!(
            document.content_hash(),
            Err(VegetationDensityError::JsonTooLarge { .. })
        ));
        let compact = serde_json::to_string(&document).unwrap();
        assert!(compact.len() < MAX_VEGETATION_DENSITY_JSON_BYTES);
        assert!(matches!(
            VegetationDensityDocument::from_json(&compact),
            Err(VegetationDensityError::JsonTooLarge { .. })
        ));
    }
}
