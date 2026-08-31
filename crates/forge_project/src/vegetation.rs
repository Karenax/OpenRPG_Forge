use crate::{TerrainMaterialLayerId, TerrainMaterialRole};
use forge_core::{
    AreaId, CanonicalJsonError, ContentHash, CoordinateSystem, ProjectId, Revision,
    canonical_json_sha256,
};
use serde::{Deserialize, Serialize};
use std::collections::HashSet;
use std::fmt;
use thiserror::Error;

pub const VEGETATION_DOCUMENT_SCHEMA: &str = "forge.vegetation";
pub const VEGETATION_DOCUMENT_SCHEMA_VERSION: u32 = 1;
pub const VEGETATION_EXTENT_M: f32 = 512.0;
pub const VEGETATION_CHUNK_SIZE_M: f32 = 64.0;
pub const MIN_VEGETATION_CELLS_PER_CHUNK_AXIS: u16 = 1;
pub const MAX_VEGETATION_CELLS_PER_CHUNK_AXIS: u16 = 16;
pub const MAX_VEGETATION_LAYERS: usize = 16;
pub const MAX_VEGETATION_EXCLUSIONS: usize = 32;
pub const MAX_VEGETATION_ATTEMPTS_PER_CELL: u8 = 8;
pub const MAX_GRASS_BLADES_PER_PATCH: u16 = 512;
pub const MAX_VEGETATION_DOCUMENT_JSON_BYTES: usize = 1_048_576;
pub const MAX_VEGETATION_PLACEMENT_ATTEMPTS: u64 = 131_072;
pub const MAX_VEGETATION_EXPANDED_GRASS_BLADES: u64 = 1_048_576;

const VEGETATION_CHUNKS_PER_AXIS: u64 = 8;

/// Persistent, renderer-independent source of truth for procedural vegetation.
#[derive(Clone, Debug, PartialEq, Serialize, Deserialize)]
#[serde(deny_unknown_fields)]
pub struct VegetationDocument {
    pub schema: String,
    pub schema_version: u32,
    pub project_id: ProjectId,
    pub area_id: AreaId,
    pub revision: Revision,
    pub coordinate_system: CoordinateSystem,
    pub name: String,
    pub seed: u64,
    pub extent_m: VegetationExtent,
    pub cells_per_chunk_axis: u16,
    pub layers: Vec<VegetationLayer>,
    #[serde(default, skip_serializing_if = "Vec::is_empty")]
    pub exclusions: Vec<VegetationExclusion>,
    pub presentation: VegetationPresentationProfile,
}

impl VegetationDocument {
    #[must_use]
    #[allow(clippy::too_many_arguments)]
    pub fn new(
        project_id: ProjectId,
        area_id: AreaId,
        revision: Revision,
        name: impl Into<String>,
        seed: u64,
        extent_m: VegetationExtent,
        cells_per_chunk_axis: u16,
        layers: Vec<VegetationLayer>,
        exclusions: Vec<VegetationExclusion>,
        presentation: VegetationPresentationProfile,
    ) -> Self {
        Self {
            schema: VEGETATION_DOCUMENT_SCHEMA.to_owned(),
            schema_version: VEGETATION_DOCUMENT_SCHEMA_VERSION,
            project_id,
            area_id,
            revision,
            coordinate_system: CoordinateSystem::METRES_Y_UP_RIGHT_HANDED,
            name: name.into(),
            seed,
            extent_m,
            cells_per_chunk_axis,
            layers,
            exclusions,
            presentation,
        }
    }

    pub fn validate(&self) -> Result<(), VegetationDocumentError> {
        if self.schema != VEGETATION_DOCUMENT_SCHEMA {
            return Err(VegetationDocumentError::InvalidSchema(self.schema.clone()));
        }
        if self.schema_version != VEGETATION_DOCUMENT_SCHEMA_VERSION {
            return Err(VegetationDocumentError::UnsupportedVersion(
                self.schema_version,
            ));
        }
        if self.coordinate_system != CoordinateSystem::METRES_Y_UP_RIGHT_HANDED {
            return Err(VegetationDocumentError::UnsupportedCoordinateSystem);
        }
        let trimmed_name = self.name.trim();
        if trimmed_name.is_empty() {
            return Err(VegetationDocumentError::EmptyName);
        }
        if self.name.len() > 128 {
            return Err(VegetationDocumentError::NameTooLong(self.name.len()));
        }

        self.extent_m.validate()?;
        validate_cells_per_chunk_axis(self.cells_per_chunk_axis)?;
        if self.layers.is_empty() {
            return Err(VegetationDocumentError::NoLayers);
        }
        if self.layers.len() > MAX_VEGETATION_LAYERS {
            return Err(VegetationDocumentError::TooManyLayers(self.layers.len()));
        }
        let mut identifiers = HashSet::with_capacity(self.layers.len());
        for layer in &self.layers {
            layer.validate()?;
            if !identifiers.insert(layer.id.as_str()) {
                return Err(VegetationDocumentError::DuplicateLayerId(
                    layer.id.to_string(),
                ));
            }
        }

        if self.exclusions.len() > MAX_VEGETATION_EXCLUSIONS {
            return Err(VegetationDocumentError::TooManyExclusions(
                self.exclusions.len(),
            ));
        }
        let mut exclusion_ids = HashSet::with_capacity(self.exclusions.len());
        for exclusion in &self.exclusions {
            exclusion.validate(self.extent_m)?;
            if !exclusion_ids.insert(exclusion.id().as_str()) {
                return Err(VegetationDocumentError::DuplicateExclusionId(
                    exclusion.id().to_string(),
                ));
            }
        }
        self.presentation.validate()?;
        self.validate_work_budget()?;
        for layer in &self.layers {
            if let Some(collision) = layer.collision
                && collision.activation_distance_m > self.presentation.cull_distance_m
            {
                return Err(VegetationDocumentError::CollisionBeyondCullDistance(
                    layer.id.to_string(),
                ));
            }
        }
        Ok(())
    }

    fn validate_work_budget(&self) -> Result<(), VegetationDocumentError> {
        let cells_per_axis = u64::from(self.cells_per_chunk_axis) * VEGETATION_CHUNKS_PER_AXIS;
        let cell_count = cells_per_axis * cells_per_axis;
        let attempts_per_cell = self
            .layers
            .iter()
            .map(|layer| u64::from(layer.attempts_per_cell))
            .sum::<u64>();
        let placement_attempts = cell_count * attempts_per_cell;
        if placement_attempts > MAX_VEGETATION_PLACEMENT_ATTEMPTS {
            return Err(VegetationDocumentError::PlacementAttemptBudgetExceeded {
                attempts: placement_attempts,
                maximum: MAX_VEGETATION_PLACEMENT_ATTEMPTS,
            });
        }
        let grass_attempts_per_cell = self
            .layers
            .iter()
            .filter(|layer| layer.kind == VegetationKind::Grass)
            .map(|layer| u64::from(layer.attempts_per_cell))
            .sum::<u64>();
        let expanded_grass_blades = cell_count
            * grass_attempts_per_cell
            * u64::from(self.presentation.grass_blades_per_patch);
        if expanded_grass_blades > MAX_VEGETATION_EXPANDED_GRASS_BLADES {
            return Err(VegetationDocumentError::ExpandedGrassBladeBudgetExceeded {
                blades: expanded_grass_blades,
                maximum: MAX_VEGETATION_EXPANDED_GRASS_BLADES,
            });
        }
        Ok(())
    }

    pub fn from_json(json: &str) -> Result<Self, VegetationDocumentError> {
        if json.len() > MAX_VEGETATION_DOCUMENT_JSON_BYTES {
            return Err(VegetationDocumentError::JsonTooLarge {
                bytes: json.len(),
                maximum: MAX_VEGETATION_DOCUMENT_JSON_BYTES,
            });
        }
        let document: Self = serde_json::from_str(json)?;
        document.validate()?;
        Ok(document)
    }

    pub fn to_pretty_json(&self) -> Result<String, VegetationDocumentError> {
        self.validate()?;
        Ok(serde_json::to_string_pretty(self)?)
    }

    pub fn content_hash(&self) -> Result<ContentHash, VegetationDocumentError> {
        self.validate()?;
        Ok(canonical_json_sha256(self)?)
    }

    #[must_use]
    pub fn cell_size_m(&self) -> f32 {
        VEGETATION_CHUNK_SIZE_M / f32::from(self.cells_per_chunk_axis)
    }
}

/// Horizontal vegetation bounds in metres, centred on the area's origin.
#[derive(Clone, Copy, Debug, PartialEq, Serialize, Deserialize)]
#[serde(deny_unknown_fields)]
pub struct VegetationExtent {
    pub size_x_m: f32,
    pub size_z_m: f32,
}

impl VegetationExtent {
    #[must_use]
    pub const fn new(width_m: f32, depth_m: f32) -> Self {
        Self {
            size_x_m: width_m,
            size_z_m: depth_m,
        }
    }

    fn validate(self) -> Result<(), VegetationDocumentError> {
        if !self.size_x_m.is_finite() || !self.size_z_m.is_finite() {
            return Err(VegetationDocumentError::NonFiniteExtent);
        }
        if self.size_x_m.to_bits() != VEGETATION_EXTENT_M.to_bits()
            || self.size_z_m.to_bits() != VEGETATION_EXTENT_M.to_bits()
        {
            return Err(VegetationDocumentError::UnsupportedExtent);
        }
        Ok(())
    }
}

/// Logical vegetation category. Grass is one logical patch or tuft, never one blade.
#[derive(Clone, Copy, Debug, Eq, Hash, Ord, PartialEq, PartialOrd, Serialize, Deserialize)]
#[serde(rename_all = "snake_case")]
pub enum VegetationKind {
    Tree,
    Shrub,
    Fern,
    /// A renderer-independent patch expanded into blades only by a presentation adapter.
    Grass,
}

/// Stable logical layer identity, independent from a renderer resource handle.
#[derive(Clone, Debug, Eq, Hash, Ord, PartialEq, PartialOrd, Serialize, Deserialize)]
#[serde(transparent)]
pub struct VegetationLayerId(String);

impl VegetationLayerId {
    #[must_use]
    pub fn new(value: impl Into<String>) -> Self {
        Self(value.into())
    }

    #[must_use]
    pub fn as_str(&self) -> &str {
        &self.0
    }
}

impl fmt::Display for VegetationLayerId {
    fn fmt(&self, formatter: &mut fmt::Formatter<'_>) -> fmt::Result {
        self.0.fmt(formatter)
    }
}

/// Generic filter against one terrain document material layer.
#[derive(Clone, Debug, PartialEq, Serialize, Deserialize)]
#[serde(deny_unknown_fields)]
pub struct VegetationMaterialFilter {
    pub material_role: TerrainMaterialRole,
    pub material_layer_id: TerrainMaterialLayerId,
    pub minimum_weight: f32,
}

impl VegetationMaterialFilter {
    #[must_use]
    pub fn new(
        material_role: TerrainMaterialRole,
        material_layer_id: TerrainMaterialLayerId,
        minimum_weight: f32,
    ) -> Self {
        Self {
            material_role,
            material_layer_id,
            minimum_weight,
        }
    }

    fn validate(&self, layer_id: &str) -> Result<(), VegetationDocumentError> {
        if !valid_logical_id(self.material_layer_id.as_str()) {
            return Err(VegetationDocumentError::InvalidLayerValue {
                id: layer_id.to_owned(),
                field: "material_filter.material_layer_id",
            });
        }
        if !unit_interval(self.minimum_weight) {
            return Err(VegetationDocumentError::InvalidLayerValue {
                id: layer_id.to_owned(),
                field: "material_filter.minimum_weight",
            });
        }
        Ok(())
    }
}

/// Bounded placement rules for one logical vegetation layer.
#[derive(Clone, Debug, PartialEq, Serialize, Deserialize)]
#[serde(deny_unknown_fields)]
pub struct VegetationLayer {
    pub id: VegetationLayerId,
    pub display_name: String,
    pub kind: VegetationKind,
    /// Explicit probability that one deterministic attempt becomes a candidate.
    pub acceptance_probability: f32,
    pub attempts_per_cell: u8,
    pub minimum_spacing_m: f32,
    pub minimum_scale: f32,
    pub maximum_scale: f32,
    /// Maximum rise/run gradient. Generation compares its square without trigonometry.
    pub maximum_slope_gradient: f32,
    pub minimum_altitude_m: f32,
    pub maximum_altitude_m: f32,
    pub material_filter: Option<VegetationMaterialFilter>,
    pub collision: Option<VegetationCollisionProfile>,
    /// Normalized animation response. A renderer may interpret it but cannot alter placement.
    pub wind_response: f32,
}

impl VegetationLayer {
    #[must_use]
    #[allow(clippy::too_many_arguments)]
    pub fn new(
        id: impl Into<String>,
        display_name: impl Into<String>,
        kind: VegetationKind,
        acceptance_probability: f32,
        attempts_per_cell: u8,
        minimum_spacing_m: f32,
        minimum_scale: f32,
        maximum_scale: f32,
        maximum_slope_gradient: f32,
        minimum_altitude_m: f32,
        maximum_altitude_m: f32,
        material_filter: Option<VegetationMaterialFilter>,
        collision: Option<VegetationCollisionProfile>,
        wind_response: f32,
    ) -> Self {
        Self {
            id: VegetationLayerId::new(id),
            display_name: display_name.into(),
            kind,
            acceptance_probability,
            attempts_per_cell,
            minimum_spacing_m,
            minimum_scale,
            maximum_scale,
            maximum_slope_gradient,
            minimum_altitude_m,
            maximum_altitude_m,
            material_filter,
            collision,
            wind_response,
        }
    }

    fn validate(&self) -> Result<(), VegetationDocumentError> {
        let id = self.id.to_string();
        if !valid_logical_id(self.id.as_str()) {
            return Err(VegetationDocumentError::InvalidLayerId(id));
        }
        let trimmed_name = self.display_name.trim();
        if trimmed_name.is_empty() {
            return Err(VegetationDocumentError::EmptyLayerName(id));
        }
        if self.display_name.len() > 128 {
            return Err(VegetationDocumentError::LayerNameTooLong {
                id,
                length: self.display_name.len(),
            });
        }
        if !unit_interval(self.acceptance_probability) {
            return Err(VegetationDocumentError::InvalidLayerValue {
                id,
                field: "acceptance_probability",
            });
        }
        if !(1..=MAX_VEGETATION_ATTEMPTS_PER_CELL).contains(&self.attempts_per_cell) {
            return Err(VegetationDocumentError::InvalidAttemptsPerCell {
                id,
                attempts: self.attempts_per_cell,
            });
        }
        if !self.minimum_spacing_m.is_finite()
            || !(0.0..=VEGETATION_CHUNK_SIZE_M).contains(&self.minimum_spacing_m)
            || (self.kind != VegetationKind::Grass && self.minimum_spacing_m <= 0.0)
        {
            return Err(VegetationDocumentError::InvalidLayerValue {
                id,
                field: "minimum_spacing_m",
            });
        }
        if !positive_finite(self.minimum_scale)
            || !self.maximum_scale.is_finite()
            || self.maximum_scale < self.minimum_scale
            || self.maximum_scale > 8.0
        {
            return Err(VegetationDocumentError::InvalidLayerValue {
                id,
                field: "scale range",
            });
        }
        if !self.maximum_slope_gradient.is_finite()
            || !(0.0..=16.0).contains(&self.maximum_slope_gradient)
        {
            return Err(VegetationDocumentError::InvalidLayerValue {
                id,
                field: "maximum_slope_gradient",
            });
        }
        if !self.minimum_altitude_m.is_finite()
            || !self.maximum_altitude_m.is_finite()
            || self.minimum_altitude_m < -10_000.0
            || self.maximum_altitude_m > 10_000.0
            || self.maximum_altitude_m < self.minimum_altitude_m
        {
            return Err(VegetationDocumentError::InvalidLayerValue {
                id,
                field: "altitude range",
            });
        }
        if let Some(filter) = &self.material_filter {
            filter.validate(&id)?;
        }
        if let Some(collision) = self.collision {
            collision.validate(&id)?;
        }
        if !unit_interval(self.wind_response) {
            return Err(VegetationDocumentError::InvalidLayerValue {
                id,
                field: "wind_response",
            });
        }
        Ok(())
    }
}

/// Logical collision primitive interpreted later by a physics adapter.
#[derive(Clone, Copy, Debug, Eq, PartialEq, Serialize, Deserialize)]
#[serde(rename_all = "snake_case")]
pub enum VegetationCollisionShape {
    Capsule,
    Cylinder,
}

/// Optional, engine-neutral collision intent for one vegetation layer.
#[derive(Clone, Copy, Debug, PartialEq, Serialize, Deserialize)]
#[serde(deny_unknown_fields)]
pub struct VegetationCollisionProfile {
    pub shape: VegetationCollisionShape,
    pub radius_m: f32,
    pub height_m: f32,
    pub activation_distance_m: f32,
}

impl VegetationCollisionProfile {
    fn validate(self, layer_id: &str) -> Result<(), VegetationDocumentError> {
        let capsule_relation =
            self.shape != VegetationCollisionShape::Capsule || self.height_m >= 2.0 * self.radius_m;
        if !positive_finite(self.radius_m)
            || self.radius_m > 16.0
            || !positive_finite(self.height_m)
            || self.height_m > 128.0
            || !positive_finite(self.activation_distance_m)
            || self.activation_distance_m > 4_096.0
            || !capsule_relation
        {
            return Err(VegetationDocumentError::InvalidLayerValue {
                id: layer_id.to_owned(),
                field: "collision",
            });
        }
        Ok(())
    }
}

/// Stable logical exclusion identity.
#[derive(Clone, Debug, Eq, Hash, Ord, PartialEq, PartialOrd, Serialize, Deserialize)]
#[serde(transparent)]
pub struct VegetationExclusionId(String);

impl VegetationExclusionId {
    #[must_use]
    pub fn new(value: impl Into<String>) -> Self {
        Self(value.into())
    }

    #[must_use]
    pub fn as_str(&self) -> &str {
        &self.0
    }
}

impl fmt::Display for VegetationExclusionId {
    fn fmt(&self, formatter: &mut fmt::Formatter<'_>) -> fmt::Result {
        self.0.fmt(formatter)
    }
}

/// Asset-free horizontal keep-clear shapes in centred Forge world metres.
#[derive(Clone, Debug, PartialEq, Serialize, Deserialize)]
#[serde(tag = "shape", rename_all = "snake_case", deny_unknown_fields)]
pub enum VegetationExclusion {
    Circle {
        id: VegetationExclusionId,
        center_x_m: f32,
        center_z_m: f32,
        radius_m: f32,
    },
    CorridorSegment {
        id: VegetationExclusionId,
        start_x_m: f32,
        start_z_m: f32,
        end_x_m: f32,
        end_z_m: f32,
        half_width_m: f32,
    },
}

impl VegetationExclusion {
    #[must_use]
    pub fn id(&self) -> &VegetationExclusionId {
        match self {
            Self::Circle { id, .. } | Self::CorridorSegment { id, .. } => id,
        }
    }

    fn validate(&self, extent: VegetationExtent) -> Result<(), VegetationDocumentError> {
        if !valid_logical_id(self.id().as_str()) {
            return Err(VegetationDocumentError::InvalidExclusionId(
                self.id().to_string(),
            ));
        }
        let half_x = extent.size_x_m * 0.5;
        let half_z = extent.size_z_m * 0.5;
        match self {
            Self::Circle {
                center_x_m,
                center_z_m,
                radius_m,
                ..
            } => {
                if !positive_finite(*radius_m)
                    || !inside_axis(*center_x_m, *radius_m, half_x)
                    || !inside_axis(*center_z_m, *radius_m, half_z)
                {
                    return Err(VegetationDocumentError::InvalidExclusionGeometry(
                        self.id().to_string(),
                    ));
                }
            }
            Self::CorridorSegment {
                start_x_m,
                start_z_m,
                end_x_m,
                end_z_m,
                half_width_m,
                ..
            } => {
                let values = [*start_x_m, *start_z_m, *end_x_m, *end_z_m, *half_width_m];
                // Squared distance intentionally treats -0.0 and +0.0 as the same point.
                let delta_x = f64::from(*end_x_m) - f64::from(*start_x_m);
                let delta_z = f64::from(*end_z_m) - f64::from(*start_z_m);
                let distinct = delta_x * delta_x + delta_z * delta_z > 0.0;
                if !values.into_iter().all(f32::is_finite)
                    || *half_width_m <= 0.0
                    || !distinct
                    || !inside_axis(*start_x_m, *half_width_m, half_x)
                    || !inside_axis(*end_x_m, *half_width_m, half_x)
                    || !inside_axis(*start_z_m, *half_width_m, half_z)
                    || !inside_axis(*end_z_m, *half_width_m, half_z)
                {
                    return Err(VegetationDocumentError::InvalidExclusionGeometry(
                        self.id().to_string(),
                    ));
                }
            }
        }
        Ok(())
    }
}

/// Renderer-neutral presentation intent. Adapters may reduce quality but keep these relations.
#[derive(Clone, Copy, Debug, PartialEq, Serialize, Deserialize)]
#[serde(deny_unknown_fields)]
pub struct VegetationPresentationProfile {
    pub near_lod_distance_m: f32,
    pub medium_lod_distance_m: f32,
    pub hlod_distance_m: f32,
    pub cull_distance_m: f32,
    pub shadow_distance_m: f32,
    pub wind_strength: f32,
    pub wind_speed_hz: f32,
    pub interaction_radius_m: f32,
    pub interaction_strength: f32,
    /// Presentation expansion count for each logical `Grass` patch.
    pub grass_blades_per_patch: u16,
}

impl VegetationPresentationProfile {
    fn validate(self) -> Result<(), VegetationDocumentError> {
        let distances = [
            self.near_lod_distance_m,
            self.medium_lod_distance_m,
            self.hlod_distance_m,
            self.cull_distance_m,
            self.shadow_distance_m,
        ];
        let ordered_lod_distances = self.near_lod_distance_m < self.medium_lod_distance_m
            && self.medium_lod_distance_m < self.hlod_distance_m
            && self.hlod_distance_m < self.cull_distance_m;
        if !distances.into_iter().all(positive_finite)
            || !ordered_lod_distances
            || self.cull_distance_m > 4_096.0
            || self.shadow_distance_m > self.cull_distance_m
        {
            return Err(VegetationDocumentError::InvalidPresentationDistances);
        }
        if !unit_interval(self.wind_strength)
            || !self.wind_speed_hz.is_finite()
            || !(0.0..=10.0).contains(&self.wind_speed_hz)
        {
            return Err(VegetationDocumentError::InvalidPresentationWind);
        }
        if !self.interaction_radius_m.is_finite()
            || !(0.0..=64.0).contains(&self.interaction_radius_m)
            || !unit_interval(self.interaction_strength)
        {
            return Err(VegetationDocumentError::InvalidPresentationInteraction);
        }
        if !(1..=MAX_GRASS_BLADES_PER_PATCH).contains(&self.grass_blades_per_patch) {
            return Err(VegetationDocumentError::InvalidGrassBladesPerPatch(
                self.grass_blades_per_patch,
            ));
        }
        Ok(())
    }
}

fn validate_cells_per_chunk_axis(value: u16) -> Result<(), VegetationDocumentError> {
    if !(MIN_VEGETATION_CELLS_PER_CHUNK_AXIS..=MAX_VEGETATION_CELLS_PER_CHUNK_AXIS).contains(&value)
    {
        return Err(VegetationDocumentError::InvalidCellsPerChunkAxis(value));
    }
    Ok(())
}

fn inside_axis(center_m: f32, margin_m: f32, half_extent_m: f32) -> bool {
    center_m.is_finite()
        && margin_m.is_finite()
        && center_m - margin_m >= -half_extent_m
        && center_m + margin_m <= half_extent_m
}

fn positive_finite(value: f32) -> bool {
    value.is_finite() && value > 0.0
}

fn unit_interval(value: f32) -> bool {
    value.is_finite() && (0.0..=1.0).contains(&value)
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
pub enum VegetationDocumentError {
    #[error("invalid vegetation schema `{0}`")]
    InvalidSchema(String),
    #[error("unsupported vegetation schema version {0}")]
    UnsupportedVersion(u32),
    #[error("vegetation coordinate system must be metres, Y-up, right-handed")]
    UnsupportedCoordinateSystem,
    #[error("vegetation name cannot be empty")]
    EmptyName,
    #[error("vegetation name exceeds 128 bytes: {0}")]
    NameTooLong(usize),
    #[error("vegetation JSON contains {bytes} bytes, exceeding the {maximum} byte limit")]
    JsonTooLarge { bytes: usize, maximum: usize },
    #[error("vegetation extent must be finite")]
    NonFiniteExtent,
    #[error("vegetation version 1 requires a 512 by 512 metre extent")]
    UnsupportedExtent,
    #[error(
        "vegetation cells_per_chunk_axis must be between {MIN_VEGETATION_CELLS_PER_CHUNK_AXIS} and {MAX_VEGETATION_CELLS_PER_CHUNK_AXIS}, found {0}"
    )]
    InvalidCellsPerChunkAxis(u16),
    #[error("vegetation must contain at least one logical layer")]
    NoLayers,
    #[error("vegetation supports at most {MAX_VEGETATION_LAYERS} logical layers, found {0}")]
    TooManyLayers(usize),
    #[error("invalid logical vegetation layer id `{0}`")]
    InvalidLayerId(String),
    #[error("duplicate logical vegetation layer id `{0}`")]
    DuplicateLayerId(String),
    #[error("vegetation layer `{0}` has an empty display name")]
    EmptyLayerName(String),
    #[error("vegetation layer `{id}` display name exceeds 128 bytes: {length}")]
    LayerNameTooLong { id: String, length: usize },
    #[error(
        "vegetation layer `{id}` attempts_per_cell must be between 1 and {MAX_VEGETATION_ATTEMPTS_PER_CELL}, found {attempts}"
    )]
    InvalidAttemptsPerCell { id: String, attempts: u8 },
    #[error("vegetation layer `{id}` has invalid {field}")]
    InvalidLayerValue { id: String, field: &'static str },
    #[error("vegetation layer `{0}` collision activation exceeds the presentation cull distance")]
    CollisionBeyondCullDistance(String),
    #[error("vegetation supports at most {MAX_VEGETATION_EXCLUSIONS} exclusions, found {0}")]
    TooManyExclusions(usize),
    #[error("invalid logical vegetation exclusion id `{0}`")]
    InvalidExclusionId(String),
    #[error("duplicate logical vegetation exclusion id `{0}`")]
    DuplicateExclusionId(String),
    #[error("vegetation exclusion `{0}` is degenerate or outside the 512 metre extent")]
    InvalidExclusionGeometry(String),
    #[error("vegetation presentation LOD, shadow, and cull distances are invalid")]
    InvalidPresentationDistances,
    #[error("vegetation presentation wind parameters are invalid")]
    InvalidPresentationWind,
    #[error("vegetation presentation interaction parameters are invalid")]
    InvalidPresentationInteraction,
    #[error(
        "vegetation grass_blades_per_patch must be between 1 and {MAX_GRASS_BLADES_PER_PATCH}, found {0}"
    )]
    InvalidGrassBladesPerPatch(u16),
    #[error(
        "vegetation requests {attempts} placement attempts, exceeding the bounded maximum {maximum}"
    )]
    PlacementAttemptBudgetExceeded { attempts: u64, maximum: u64 },
    #[error(
        "vegetation can expand to {blades} grass blades, exceeding the bounded maximum {maximum}"
    )]
    ExpandedGrassBladeBudgetExceeded { blades: u64, maximum: u64 },
    #[error("invalid vegetation JSON: {0}")]
    Json(#[from] serde_json::Error),
    #[error("cannot hash vegetation document: {0}")]
    Hash(#[from] CanonicalJsonError),
}

#[cfg(test)]
mod tests {
    use super::*;

    fn presentation() -> VegetationPresentationProfile {
        VegetationPresentationProfile {
            near_lod_distance_m: 24.0,
            medium_lod_distance_m: 64.0,
            hlod_distance_m: 160.0,
            cull_distance_m: 320.0,
            shadow_distance_m: 96.0,
            wind_strength: 0.5,
            wind_speed_hz: 0.2,
            interaction_radius_m: 2.0,
            interaction_strength: 0.7,
            grass_blades_per_patch: 24,
        }
    }

    fn valid_layer(id: &str, kind: VegetationKind) -> VegetationLayer {
        VegetationLayer::new(
            id,
            id,
            kind,
            0.25,
            2,
            if kind == VegetationKind::Grass {
                0.0
            } else {
                2.0
            },
            0.75,
            1.25,
            0.84,
            -32.0,
            64.0,
            Some(VegetationMaterialFilter::new(
                TerrainMaterialRole::Grass,
                TerrainMaterialLayerId::new("meadow_grass"),
                0.3,
            )),
            None,
            0.5,
        )
    }

    fn valid_document(seed: u64) -> VegetationDocument {
        VegetationDocument::new(
            ProjectId::deterministic("vegetation-test-project"),
            AreaId::deterministic("vegetation-test-area"),
            Revision::INITIAL,
            "Vegetation contract test",
            seed,
            VegetationExtent::new(512.0, 512.0),
            8,
            vec![
                valid_layer("trees", VegetationKind::Tree),
                valid_layer("shrubs", VegetationKind::Shrub),
                valid_layer("ferns", VegetationKind::Fern),
                valid_layer("grass", VegetationKind::Grass),
            ],
            vec![
                VegetationExclusion::Circle {
                    id: VegetationExclusionId::new("spawn_clearance"),
                    center_x_m: 0.0,
                    center_z_m: 0.0,
                    radius_m: 8.0,
                },
                VegetationExclusion::CorridorSegment {
                    id: VegetationExclusionId::new("movement_corridor"),
                    start_x_m: 0.0,
                    start_z_m: -200.0,
                    end_x_m: 0.0,
                    end_z_m: 200.0,
                    half_width_m: 4.0,
                },
            ],
            presentation(),
        )
    }

    #[test]
    fn json_round_trip_and_hash_are_canonical() {
        let document = valid_document(42);
        let json = document.to_pretty_json().unwrap();
        let restored = VegetationDocument::from_json(&json).unwrap();
        assert_eq!(restored, document);
        assert_eq!(
            restored.content_hash().unwrap(),
            document.content_hash().unwrap()
        );
        assert_eq!(restored.to_pretty_json().unwrap(), json);
        assert!(json.contains("\"schema\": \"forge.vegetation\""));
    }

    #[test]
    fn seed_changes_the_canonical_hash() {
        assert_ne!(
            valid_document(42).content_hash().unwrap(),
            valid_document(43).content_hash().unwrap()
        );
    }

    #[test]
    fn unknown_fields_versions_and_non_finite_values_are_rejected() {
        let json = valid_document(42).to_pretty_json().unwrap();
        let unknown = json.replacen('{', "{\n  \"renderer\": \"none\",", 1);
        assert!(matches!(
            VegetationDocument::from_json(&unknown),
            Err(VegetationDocumentError::Json(_))
        ));
        let mut document = valid_document(42);
        document.schema_version = 2;
        assert!(matches!(
            document.validate(),
            Err(VegetationDocumentError::UnsupportedVersion(2))
        ));
        document = valid_document(42);
        document.layers[0].acceptance_probability = f32::NAN;
        assert!(matches!(
            document.validate(),
            Err(VegetationDocumentError::InvalidLayerValue {
                field: "acceptance_probability",
                ..
            })
        ));
    }

    #[test]
    fn ranges_spacing_ids_and_cell_relations_are_enforced() {
        let mut document = valid_document(42);
        document.layers[0].maximum_scale = document.layers[0].minimum_scale - 0.1;
        assert!(matches!(
            document.validate(),
            Err(VegetationDocumentError::InvalidLayerValue {
                field: "scale range",
                ..
            })
        ));
        document = valid_document(42);
        document.layers[0].maximum_altitude_m = document.layers[0].minimum_altitude_m - 1.0;
        assert!(matches!(
            document.validate(),
            Err(VegetationDocumentError::InvalidLayerValue {
                field: "altitude range",
                ..
            })
        ));
        document = valid_document(42);
        document.layers[0].minimum_spacing_m = 0.0;
        assert!(matches!(
            document.validate(),
            Err(VegetationDocumentError::InvalidLayerValue {
                field: "minimum_spacing_m",
                ..
            })
        ));
        document = valid_document(42);
        document.layers[0].material_filter = Some(VegetationMaterialFilter::new(
            TerrainMaterialRole::Grass,
            TerrainMaterialLayerId::new("X".repeat(1_024)),
            0.5,
        ));
        assert!(matches!(
            document.validate(),
            Err(VegetationDocumentError::InvalidLayerValue {
                field: "material_filter.material_layer_id",
                ..
            })
        ));
        document = valid_document(42);
        document.layers[1].id = document.layers[0].id.clone();
        assert!(matches!(
            document.validate(),
            Err(VegetationDocumentError::DuplicateLayerId(_))
        ));
        document = valid_document(42);
        document.cells_per_chunk_axis = 0;
        assert!(matches!(
            document.validate(),
            Err(VegetationDocumentError::InvalidCellsPerChunkAxis(0))
        ));
    }

    #[test]
    fn raw_text_json_attempt_and_grass_expansion_budgets_are_enforced() {
        let mut document = valid_document(42);
        document.name = format!("{}x", " ".repeat(128));
        assert!(matches!(
            document.validate(),
            Err(VegetationDocumentError::NameTooLong(129))
        ));

        let oversized_json = " ".repeat(MAX_VEGETATION_DOCUMENT_JSON_BYTES + 1);
        assert!(matches!(
            VegetationDocument::from_json(&oversized_json),
            Err(VegetationDocumentError::JsonTooLarge { .. })
        ));

        document = valid_document(42);
        document.cells_per_chunk_axis = 16;
        document.layers[0].attempts_per_cell = 3;
        let error = document.validate().unwrap_err();
        assert!(
            matches!(
                error,
                VegetationDocumentError::PlacementAttemptBudgetExceeded { .. }
            ),
            "unexpected validation error: {error:?}"
        );

        document = valid_document(42);
        document
            .layers
            .retain(|layer| layer.kind == VegetationKind::Grass);
        document.presentation.grass_blades_per_patch = MAX_GRASS_BLADES_PER_PATCH;
        assert!(matches!(
            document.validate(),
            Err(VegetationDocumentError::ExpandedGrassBladeBudgetExceeded { .. })
        ));

        document.cells_per_chunk_axis = 16;
        document.layers[0].attempts_per_cell = MAX_VEGETATION_ATTEMPTS_PER_CELL;
        document.presentation.grass_blades_per_patch = 8;
        assert_eq!(
            u64::from(document.cells_per_chunk_axis).pow(2)
                * VEGETATION_CHUNKS_PER_AXIS.pow(2)
                * u64::from(document.layers[0].attempts_per_cell),
            MAX_VEGETATION_PLACEMENT_ATTEMPTS
        );
        assert_eq!(
            MAX_VEGETATION_PLACEMENT_ATTEMPTS
                * u64::from(document.presentation.grass_blades_per_patch),
            MAX_VEGETATION_EXPANDED_GRASS_BLADES
        );
        document.validate().unwrap();
    }

    #[test]
    fn exclusions_and_presentation_relations_are_enforced() {
        let mut document = valid_document(42);
        document.exclusions.push(document.exclusions[0].clone());
        assert!(matches!(
            document.validate(),
            Err(VegetationDocumentError::DuplicateExclusionId(_))
        ));
        document = valid_document(42);
        document.exclusions[0] = VegetationExclusion::Circle {
            id: VegetationExclusionId::new("outside"),
            center_x_m: 250.0,
            center_z_m: 0.0,
            radius_m: 12.0,
        };
        assert!(matches!(
            document.validate(),
            Err(VegetationDocumentError::InvalidExclusionGeometry(_))
        ));
        document = valid_document(42);
        document.exclusions[1] = VegetationExclusion::CorridorSegment {
            id: VegetationExclusionId::new("signed_zero_corridor"),
            start_x_m: -0.0,
            start_z_m: 0.0,
            end_x_m: 0.0,
            end_z_m: -0.0,
            half_width_m: 1.0,
        };
        assert!(matches!(
            document.validate(),
            Err(VegetationDocumentError::InvalidExclusionGeometry(_))
        ));
        document = valid_document(42);
        document.presentation.medium_lod_distance_m = document.presentation.near_lod_distance_m;
        assert!(matches!(
            document.validate(),
            Err(VegetationDocumentError::InvalidPresentationDistances)
        ));
        document = valid_document(42);
        document.presentation.shadow_distance_m = document.presentation.cull_distance_m + 1.0;
        assert!(matches!(
            document.validate(),
            Err(VegetationDocumentError::InvalidPresentationDistances)
        ));
        document = valid_document(42);
        document.presentation.grass_blades_per_patch = 0;
        assert!(matches!(
            document.validate(),
            Err(VegetationDocumentError::InvalidGrassBladesPerPatch(0))
        ));

        document = valid_document(42);
        document.layers[0].collision = Some(VegetationCollisionProfile {
            shape: VegetationCollisionShape::Capsule,
            radius_m: 1.0,
            height_m: 1.0,
            activation_distance_m: 32.0,
        });
        assert!(matches!(
            document.validate(),
            Err(VegetationDocumentError::InvalidLayerValue {
                field: "collision",
                ..
            })
        ));
    }
}
