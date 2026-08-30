use forge_core::{
    AreaId, CanonicalJsonError, ContentHash, CoordinateSystem, ProjectId, Revision,
    canonical_json_sha256,
};
use serde::{Deserialize, Serialize};
use std::collections::HashSet;
use std::fmt;
use thiserror::Error;

pub const TERRAIN_DOCUMENT_SCHEMA: &str = "forge.terrain";
pub const TERRAIN_DOCUMENT_SCHEMA_VERSION: u32 = 1;
pub const MIN_TERRAIN_MATERIAL_LAYERS: usize = 1;
pub const MAX_TERRAIN_MATERIAL_LAYERS: usize = 32;
pub const MAX_TERRAIN_SAMPLE_INTERVALS_PER_AXIS: u16 = 8_192;

/// Persistent, renderer-independent source of truth for one area's terrain.
#[derive(Clone, Debug, PartialEq, Serialize, Deserialize)]
#[serde(deny_unknown_fields)]
pub struct TerrainDocument {
    pub schema: String,
    pub schema_version: u32,
    pub project_id: ProjectId,
    pub area_id: AreaId,
    pub revision: Revision,
    pub coordinate_system: CoordinateSystem,
    pub name: String,
    pub seed: u64,
    pub extent_m: TerrainExtent,
    pub height_sampling: HeightSamplingParameters,
    pub material_layers: Vec<TerrainMaterialLayer>,
}

impl TerrainDocument {
    #[must_use]
    #[allow(clippy::too_many_arguments)]
    pub fn new(
        project_id: ProjectId,
        area_id: AreaId,
        revision: Revision,
        name: impl Into<String>,
        seed: u64,
        extent_m: TerrainExtent,
        height_sampling: HeightSamplingParameters,
        material_layers: Vec<TerrainMaterialLayer>,
    ) -> Self {
        Self {
            schema: TERRAIN_DOCUMENT_SCHEMA.to_owned(),
            schema_version: TERRAIN_DOCUMENT_SCHEMA_VERSION,
            project_id,
            area_id,
            revision,
            coordinate_system: CoordinateSystem::METRES_Y_UP_RIGHT_HANDED,
            name: name.into(),
            seed,
            extent_m,
            height_sampling,
            material_layers,
        }
    }

    pub fn validate(&self) -> Result<(), TerrainDocumentError> {
        if self.schema != TERRAIN_DOCUMENT_SCHEMA {
            return Err(TerrainDocumentError::InvalidSchema(self.schema.clone()));
        }
        if self.schema_version != TERRAIN_DOCUMENT_SCHEMA_VERSION {
            return Err(TerrainDocumentError::UnsupportedVersion(
                self.schema_version,
            ));
        }
        if self.coordinate_system != CoordinateSystem::METRES_Y_UP_RIGHT_HANDED {
            return Err(TerrainDocumentError::UnsupportedCoordinateSystem);
        }

        let trimmed_name = self.name.trim();
        if trimmed_name.is_empty() {
            return Err(TerrainDocumentError::EmptyName);
        }
        if trimmed_name.len() > 128 {
            return Err(TerrainDocumentError::NameTooLong(trimmed_name.len()));
        }

        self.extent_m.validate()?;
        self.height_sampling.validate(self.extent_m)?;

        if self.material_layers.len() < MIN_TERRAIN_MATERIAL_LAYERS {
            return Err(TerrainDocumentError::TooFewMaterialLayers(
                self.material_layers.len(),
            ));
        }
        if self.material_layers.len() > MAX_TERRAIN_MATERIAL_LAYERS {
            return Err(TerrainDocumentError::TooManyMaterialLayers(
                self.material_layers.len(),
            ));
        }

        let mut identifiers = HashSet::with_capacity(self.material_layers.len());
        for layer in &self.material_layers {
            layer.validate()?;
            if !identifiers.insert(layer.id.as_str()) {
                return Err(TerrainDocumentError::DuplicateMaterialLayerId(
                    layer.id.to_string(),
                ));
            }
        }

        Ok(())
    }

    pub fn from_json(json: &str) -> Result<Self, TerrainDocumentError> {
        let document: Self = serde_json::from_str(json)?;
        document.validate()?;
        Ok(document)
    }

    pub fn to_pretty_json(&self) -> Result<String, TerrainDocumentError> {
        self.validate()?;
        Ok(serde_json::to_string_pretty(self)?)
    }

    pub fn content_hash(&self) -> Result<ContentHash, TerrainDocumentError> {
        self.validate()?;
        Ok(canonical_json_sha256(self)?)
    }
}

/// Horizontal terrain dimensions in metres. Heights use the document's Y axis.
#[derive(Clone, Copy, Debug, PartialEq, Serialize, Deserialize)]
#[serde(deny_unknown_fields)]
pub struct TerrainExtent {
    pub size_x_m: f32,
    pub size_z_m: f32,
}

impl TerrainExtent {
    #[must_use]
    pub const fn new(width_m: f32, depth_m: f32) -> Self {
        Self {
            size_x_m: width_m,
            size_z_m: depth_m,
        }
    }

    fn validate(self) -> Result<(), TerrainDocumentError> {
        if !positive_finite(self.size_x_m) {
            return Err(TerrainDocumentError::InvalidExtent("size_x_m"));
        }
        if !positive_finite(self.size_z_m) {
            return Err(TerrainDocumentError::InvalidExtent("size_z_m"));
        }
        Ok(())
    }
}

/// Versioned sampling method interpreted later by `forge_world`.
#[derive(Clone, Copy, Debug, Eq, PartialEq, Serialize, Deserialize)]
#[serde(rename_all = "snake_case")]
pub enum HeightSamplingAlgorithm {
    FractalValueNoiseV1,
}

/// Engine-neutral parameters needed to reproduce the terrain height source.
#[derive(Clone, Copy, Debug, PartialEq, Serialize, Deserialize)]
#[serde(deny_unknown_fields)]
pub struct HeightSamplingParameters {
    pub algorithm: HeightSamplingAlgorithm,
    pub sample_spacing_m: f32,
    pub base_elevation_m: f32,
    pub amplitude_m: f32,
    pub base_frequency_cycles_per_m: f32,
    pub octaves: u8,
    pub lacunarity: f32,
    pub persistence: f32,
}

impl HeightSamplingParameters {
    fn validate(self, extent: TerrainExtent) -> Result<(), TerrainDocumentError> {
        if !positive_finite(self.sample_spacing_m)
            || self.sample_spacing_m > extent.size_x_m
            || self.sample_spacing_m > extent.size_z_m
        {
            return Err(TerrainDocumentError::InvalidHeightSampling(
                "sample_spacing_m",
            ));
        }
        validate_grid_axis(extent.size_x_m, self.sample_spacing_m, "x")?;
        validate_grid_axis(extent.size_z_m, self.sample_spacing_m, "z")?;
        if !self.base_elevation_m.is_finite() {
            return Err(TerrainDocumentError::InvalidHeightSampling(
                "base_elevation_m",
            ));
        }
        if !self.amplitude_m.is_finite() || self.amplitude_m < 0.0 {
            return Err(TerrainDocumentError::InvalidHeightSampling("amplitude_m"));
        }
        if !positive_finite(self.base_frequency_cycles_per_m) {
            return Err(TerrainDocumentError::InvalidHeightSampling(
                "base_frequency_cycles_per_m",
            ));
        }
        if !(1..=12).contains(&self.octaves) {
            return Err(TerrainDocumentError::InvalidHeightSampling("octaves"));
        }
        if !self.lacunarity.is_finite() || self.lacunarity <= 1.0 {
            return Err(TerrainDocumentError::InvalidHeightSampling("lacunarity"));
        }
        if !self.persistence.is_finite() || self.persistence <= 0.0 || self.persistence > 1.0 {
            return Err(TerrainDocumentError::InvalidHeightSampling("persistence"));
        }
        Ok(())
    }
}

/// Stable logical material identity, deliberately unrelated to an engine handle or asset path.
#[derive(Clone, Debug, Eq, Hash, Ord, PartialEq, PartialOrd, Serialize, Deserialize)]
#[serde(transparent)]
pub struct TerrainMaterialLayerId(String);

impl TerrainMaterialLayerId {
    #[must_use]
    pub fn new(value: impl Into<String>) -> Self {
        Self(value.into())
    }

    #[must_use]
    pub fn as_str(&self) -> &str {
        &self.0
    }
}

impl fmt::Display for TerrainMaterialLayerId {
    fn fmt(&self, formatter: &mut fmt::Formatter<'_>) -> fmt::Result {
        self.0.fmt(formatter)
    }
}

/// Logical layer metadata. Renderer bindings and texture assets are derived elsewhere.
#[derive(Clone, Debug, Eq, PartialEq, Serialize, Deserialize)]
#[serde(deny_unknown_fields)]
pub struct TerrainMaterialLayer {
    pub id: TerrainMaterialLayerId,
    pub display_name: String,
}

impl TerrainMaterialLayer {
    #[must_use]
    pub fn new(id: impl Into<String>, display_name: impl Into<String>) -> Self {
        Self {
            id: TerrainMaterialLayerId::new(id),
            display_name: display_name.into(),
        }
    }

    fn validate(&self) -> Result<(), TerrainDocumentError> {
        if !valid_logical_id(self.id.as_str()) {
            return Err(TerrainDocumentError::InvalidMaterialLayerId(
                self.id.to_string(),
            ));
        }
        let trimmed_name = self.display_name.trim();
        if trimmed_name.is_empty() {
            return Err(TerrainDocumentError::EmptyMaterialLayerName(
                self.id.to_string(),
            ));
        }
        if trimmed_name.len() > 128 {
            return Err(TerrainDocumentError::MaterialLayerNameTooLong {
                id: self.id.to_string(),
                length: trimmed_name.len(),
            });
        }
        Ok(())
    }
}

fn positive_finite(value: f32) -> bool {
    value.is_finite() && value > 0.0
}

fn validate_grid_axis(
    extent_m: f32,
    sample_spacing_m: f32,
    axis: &'static str,
) -> Result<(), TerrainDocumentError> {
    let interval_count = extent_m / sample_spacing_m;
    let nearest_interval_count = interval_count.round();
    let reconstructed_extent_m = nearest_interval_count * sample_spacing_m;
    let tolerance_m = f32::EPSILON * 8.0 * extent_m.abs().max(sample_spacing_m);

    if (extent_m - reconstructed_extent_m).abs() > tolerance_m {
        return Err(TerrainDocumentError::MisalignedHeightGrid(axis));
    }
    if nearest_interval_count > f32::from(MAX_TERRAIN_SAMPLE_INTERVALS_PER_AXIS) {
        return Err(TerrainDocumentError::TooManyHeightSampleIntervals {
            axis,
            maximum: MAX_TERRAIN_SAMPLE_INTERVALS_PER_AXIS,
        });
    }
    Ok(())
}

fn valid_logical_id(value: &str) -> bool {
    if value.is_empty() || value.len() > 64 {
        return false;
    }
    let mut bytes = value.bytes();
    let Some(first) = bytes.next() else {
        return false;
    };
    first.is_ascii_lowercase()
        && bytes.all(|byte| byte.is_ascii_lowercase() || byte.is_ascii_digit() || byte == b'_')
}

#[derive(Debug, Error)]
pub enum TerrainDocumentError {
    #[error("invalid terrain schema `{0}`")]
    InvalidSchema(String),
    #[error("unsupported terrain schema version {0}")]
    UnsupportedVersion(u32),
    #[error("terrain coordinate system must be metres, Y-up, right-handed")]
    UnsupportedCoordinateSystem,
    #[error("terrain name cannot be empty")]
    EmptyName,
    #[error("terrain name exceeds 128 bytes: {0}")]
    NameTooLong(usize),
    #[error("terrain extent field `{0}` must be finite and strictly positive")]
    InvalidExtent(&'static str),
    #[error("invalid terrain height-sampling field `{0}`")]
    InvalidHeightSampling(&'static str),
    #[error("terrain extent on axis `{0}` is not aligned to sample_spacing_m")]
    MisalignedHeightGrid(&'static str),
    #[error("terrain height grid on axis `{axis}` exceeds {maximum} sample intervals")]
    TooManyHeightSampleIntervals { axis: &'static str, maximum: u16 },
    #[error(
        "terrain needs at least {MIN_TERRAIN_MATERIAL_LAYERS} logical material layers, found {0}"
    )]
    TooFewMaterialLayers(usize),
    #[error(
        "terrain supports at most {MAX_TERRAIN_MATERIAL_LAYERS} logical material layers, found {0}"
    )]
    TooManyMaterialLayers(usize),
    #[error("invalid logical terrain material layer id `{0}`")]
    InvalidMaterialLayerId(String),
    #[error("duplicate logical terrain material layer id `{0}`")]
    DuplicateMaterialLayerId(String),
    #[error("terrain material layer `{0}` has an empty display name")]
    EmptyMaterialLayerName(String),
    #[error("terrain material layer `{id}` display name exceeds 128 bytes: {length}")]
    MaterialLayerNameTooLong { id: String, length: usize },
    #[error("invalid terrain JSON: {0}")]
    Json(#[from] serde_json::Error),
    #[error("cannot hash terrain document: {0}")]
    Hash(#[from] CanonicalJsonError),
}

#[cfg(test)]
mod tests {
    use super::*;

    fn valid_document(seed: u64) -> TerrainDocument {
        TerrainDocument::new(
            ProjectId::deterministic("terrain-test-project"),
            AreaId::deterministic("terrain-test-area"),
            Revision::INITIAL,
            "Terrain contract test",
            seed,
            TerrainExtent::new(512.0, 512.0),
            HeightSamplingParameters {
                algorithm: HeightSamplingAlgorithm::FractalValueNoiseV1,
                sample_spacing_m: 2.0,
                base_elevation_m: 8.0,
                amplitude_m: 48.0,
                base_frequency_cycles_per_m: 1.0 / 256.0,
                octaves: 5,
                lacunarity: 2.0,
                persistence: 0.5,
            },
            vec![
                TerrainMaterialLayer::new("meadow_grass", "Meadow grass"),
                TerrainMaterialLayer::new("valley_earth", "Valley earth"),
                TerrainMaterialLayer::new("ridge_rock", "Ridge rock"),
            ],
        )
    }

    #[test]
    fn terrain_json_round_trip_preserves_document_and_hash() {
        let document = valid_document(42);
        let json = document.to_pretty_json().unwrap();
        let restored = TerrainDocument::from_json(&json).unwrap();

        assert_eq!(restored, document);
        assert_eq!(
            restored.content_hash().unwrap(),
            document.content_hash().unwrap()
        );
        assert!(json.contains("\"schema\": \"forge.terrain\""));
        assert!(json.contains("\"schema_version\": 1"));
    }

    #[test]
    fn same_seed_is_hash_deterministic_and_a_new_seed_changes_content() {
        let first = valid_document(42);
        let replay = valid_document(42);
        let changed = valid_document(43);

        assert_eq!(first, replay);
        assert_eq!(
            first.content_hash().unwrap(),
            replay.content_hash().unwrap()
        );
        assert_ne!(
            first.content_hash().unwrap(),
            changed.content_hash().unwrap()
        );
    }

    #[test]
    fn strict_json_rejects_unknown_fields() {
        let json = valid_document(42).to_pretty_json().unwrap();
        let unexpected = json.replacen(
            "  \"seed\": 42,",
            "  \"seed\": 42,\n  \"unexpected\": true,",
            1,
        );

        assert!(matches!(
            TerrainDocument::from_json(&unexpected),
            Err(TerrainDocumentError::Json(_))
        ));
    }

    #[test]
    fn unsupported_versions_and_invalid_sampling_are_rejected() {
        let mut document = valid_document(42);
        document.schema_version = 2;
        assert!(matches!(
            document.validate(),
            Err(TerrainDocumentError::UnsupportedVersion(2))
        ));

        document.schema_version = TERRAIN_DOCUMENT_SCHEMA_VERSION;
        document.height_sampling.sample_spacing_m = 1024.0;
        assert!(matches!(
            document.validate(),
            Err(TerrainDocumentError::InvalidHeightSampling(
                "sample_spacing_m"
            ))
        ));

        document.height_sampling.sample_spacing_m = 3.0;
        assert!(matches!(
            document.validate(),
            Err(TerrainDocumentError::MisalignedHeightGrid("x"))
        ));

        document.height_sampling.sample_spacing_m = 1.0 / 32.0;
        assert!(matches!(
            document.validate(),
            Err(TerrainDocumentError::TooManyHeightSampleIntervals {
                axis: "x",
                maximum: MAX_TERRAIN_SAMPLE_INTERVALS_PER_AXIS
            })
        ));
    }

    #[test]
    fn material_layer_count_ids_and_names_are_validated() {
        let mut document = valid_document(42);
        document.material_layers.clear();
        assert!(matches!(
            document.validate(),
            Err(TerrainDocumentError::TooFewMaterialLayers(0))
        ));

        document = valid_document(42);
        document.material_layers[2].id = document.material_layers[0].id.clone();
        assert!(matches!(
            document.validate(),
            Err(TerrainDocumentError::DuplicateMaterialLayerId(_))
        ));

        document = valid_document(42);
        document.material_layers[0].id = TerrainMaterialLayerId::new("Invalid Layer");
        assert!(matches!(
            document.validate(),
            Err(TerrainDocumentError::InvalidMaterialLayerId(_))
        ));
    }
}
