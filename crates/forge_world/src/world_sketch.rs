use crate::{
    TERRAIN_GLOBAL_VERTICES_PER_AXIS, TERRAIN_HALF_EXTENT_M, TERRAIN_SAMPLE_SPACING_M,
    TerrainGenerationPlan, VisualValleyVegetationRecipe,
    embedded_visual_valley_player_controller_profile,
};
use forge_core::{
    AreaId, CanonicalJsonError, ContentHash, CoordinateSystem, ProjectId, Revision,
    canonical_json_sha256,
};
use forge_project::{
    HeightSamplingParameters, PlayerControllerProfileDocument, PlayerControllerProfileError,
    TerrainDocument, TerrainDocumentError, TerrainExtent, TerrainHeightDelta, TerrainMaterialLayer,
    VegetationDocument, VegetationDocumentError, VegetationExclusion, VegetationExclusionId,
    VegetationExtent,
};
use serde::{Deserialize, Serialize};
use std::collections::{HashMap, HashSet, VecDeque};
use thiserror::Error;

pub const WORLD_SKETCH_SCHEMA: &str = "forge.world-sketch";
pub const WORLD_SKETCH_SCHEMA_VERSION: u32 = 1;
pub const VISUAL_VALLEY_WORLD_SKETCH_JSON: &str =
    include_str!("../../../examples/visual_valley/world-sketch.v1.json");

const MAX_SOURCE_IMAGES: usize = 16;
const MAX_SEMANTIC_REGIONS: usize = 64;
const MAX_SPLINES: usize = 64;
const MAX_LANDMARKS: usize = 64;
const MAX_UNCERTAINTY_REGIONS: usize = 32;
const MAX_VALIDATION_CAMERAS: usize = 16;
const MAX_POLYGON_POINTS: usize = 128;
const MAX_SPLINE_POINTS: usize = 128;
const MAX_LOCKS: usize = 128;
const MAX_EXTENT_M: f32 = 16_384.0;
const VISUAL_VALLEY_EXTENT_M: f32 = 512.0;
const SPLINE_SAMPLES_PER_SEGMENT: usize = 12;
const MATERIALIZED_HEIGHT_DELTA_EPSILON_M: f32 = 0.000_1;
const WATER_BLOCKED_MASK_THRESHOLD: u8 = 64;
const MAX_BRIDGE_PLACEMENTS: usize = 16;
const BRIDGE_LANDING_CLEARANCE_M: f32 = 0.12;
const BRIDGE_LANDING_EXTENSION_STEP_M: f32 = 2.0;
const BRIDGE_MAX_LANDING_EXTENSION_M: f32 = 16.0;
const BRIDGE_LANDING_AUDIT_DISTANCE_M: f32 = 4.0;
const BRIDGE_LANDING_AUDIT_STEP_M: f32 = 0.5;
const BRIDGE_MIN_LONGITUDINAL_CLEARANCE_M: f32 = 0.10;
const BRIDGE_DECK_THICKNESS_M: f32 = 1.2;
const BRIDGE_ROAD_MARGIN_M: f32 = 1.5;
const BRIDGE_PARAPET_HEIGHT_M: f32 = 1.35;
const BRIDGE_PARAPET_THICKNESS_M: f32 = 0.55;

/// Editable, versioned bridge between visual references and canonical Forge world documents.
#[derive(Clone, Debug, PartialEq, Serialize, Deserialize)]
#[serde(deny_unknown_fields)]
pub struct WorldSketchDocument {
    pub schema: String,
    pub schema_version: u32,
    pub project_id: ProjectId,
    pub area_id: AreaId,
    pub revision: Revision,
    pub coordinate_system: CoordinateSystem,
    pub name: String,
    pub extent_m: WorldSketchExtent,
    pub source_images: Vec<WorldSketchSourceImage>,
    pub calibration: WorldSketchCalibration,
    pub terrain_seed: u64,
    pub terrain_height_sampling: HeightSamplingParameters,
    pub vegetation_seed: u64,
    /// Opt-in authored forest recipe; omitted preserves historical scatter and hashes.
    #[serde(default, skip_serializing_if = "Option::is_none")]
    pub forest_scatter: Option<WorldSketchForestScatter>,
    pub semantic_regions: Vec<WorldSketchSemanticRegion>,
    pub splines: Vec<WorldSketchSpline>,
    pub landmarks: Vec<WorldSketchLandmark>,
    pub uncertainty_regions: Vec<WorldSketchUncertaintyRegion>,
    pub locked_feature_ids: Vec<String>,
    pub validation_cameras: Vec<WorldSketchCamera>,
}

/// Reusable authored population settings; regions supply location, not mesh placements.
#[derive(Clone, Debug, PartialEq, Serialize, Deserialize)]
#[serde(deny_unknown_fields)]
pub struct WorldSketchForestScatter {
    pub acceptance_probability: f32,
    pub attempts_per_cell: u8,
    pub minimum_spacing_m: f32,
    pub minimum_scale: f32,
    pub maximum_scale: f32,
}

impl WorldSketchDocument {
    pub fn validate(&self) -> Result<(), WorldSketchError> {
        if self.schema != WORLD_SKETCH_SCHEMA {
            return Err(WorldSketchError::InvalidSchema(self.schema.clone()));
        }
        if self.schema_version != WORLD_SKETCH_SCHEMA_VERSION {
            return Err(WorldSketchError::UnsupportedVersion(self.schema_version));
        }
        if self.coordinate_system != CoordinateSystem::METRES_Y_UP_RIGHT_HANDED {
            return Err(WorldSketchError::UnsupportedCoordinateSystem);
        }
        validate_name("world sketch", &self.name)?;
        self.extent_m.validate()?;
        self.validate_sources()?;
        self.validate_terrain_intent()?;
        self.validate_collection_limits()?;
        if let Some(forest) = &self.forest_scatter
            && (!(0.0..=1.0).contains(&forest.acceptance_probability)
                || !(1..=8).contains(&forest.attempts_per_cell)
                || !(0.1..=64.0).contains(&forest.minimum_spacing_m)
                || !(0.1..=4.0).contains(&forest.minimum_scale)
                || !(forest.minimum_scale..=4.0).contains(&forest.maximum_scale))
        {
            return Err(WorldSketchError::InvalidForestScatter);
        }

        let mut feature_ids = HashSet::new();
        for region in &self.semantic_regions {
            validate_unique_feature_id(&mut feature_ids, &region.id)?;
            region.validate(self.extent_m)?;
        }
        for spline in &self.splines {
            validate_unique_feature_id(&mut feature_ids, &spline.id)?;
            spline.validate(self.extent_m)?;
        }
        for landmark in &self.landmarks {
            validate_unique_feature_id(&mut feature_ids, &landmark.id)?;
            landmark.validate(self.extent_m)?;
        }
        for region in &self.uncertainty_regions {
            validate_unique_feature_id(&mut feature_ids, &region.id)?;
            region.validate(self.extent_m)?;
        }

        let mut previous_lock: Option<&str> = None;
        for feature_id in &self.locked_feature_ids {
            if !feature_ids.contains(feature_id.as_str()) {
                return Err(WorldSketchError::UnknownLockedFeature(feature_id.clone()));
            }
            if previous_lock.is_some_and(|previous| previous >= feature_id.as_str()) {
                return Err(WorldSketchError::LocksNotStrictlySorted);
            }
            previous_lock = Some(feature_id);
        }

        let source_roles = self
            .source_images
            .iter()
            .map(|source| (source.id.as_str(), source.role))
            .collect::<HashMap<_, _>>();
        let mut camera_ids = HashSet::with_capacity(self.validation_cameras.len());
        for camera in &self.validation_cameras {
            validate_logical_id("camera", &camera.id)?;
            if !camera_ids.insert(camera.id.as_str()) {
                return Err(WorldSketchError::DuplicateCameraId(camera.id.clone()));
            }
            camera.validate()?;
            if let Some(source_image_id) = &camera.source_image_id {
                let Some(role) = source_roles.get(source_image_id.as_str()) else {
                    return Err(WorldSketchError::UnknownCameraSource {
                        camera_id: camera.id.clone(),
                        source_image_id: source_image_id.clone(),
                    });
                };
                if *role != WorldSketchSourceRole::PerspectiveConcept {
                    return Err(WorldSketchError::InvalidCameraSourceRole {
                        camera_id: camera.id.clone(),
                        source_image_id: source_image_id.clone(),
                    });
                }
            }
        }

        Ok(())
    }

    fn validate_collection_limits(&self) -> Result<(), WorldSketchError> {
        for (collection, count, maximum) in [
            (
                "semantic_regions",
                self.semantic_regions.len(),
                MAX_SEMANTIC_REGIONS,
            ),
            ("splines", self.splines.len(), MAX_SPLINES),
            ("landmarks", self.landmarks.len(), MAX_LANDMARKS),
            (
                "uncertainty_regions",
                self.uncertainty_regions.len(),
                MAX_UNCERTAINTY_REGIONS,
            ),
            (
                "locked_feature_ids",
                self.locked_feature_ids.len(),
                MAX_LOCKS,
            ),
        ] {
            if count > maximum {
                return Err(WorldSketchError::CollectionLimitExceeded {
                    collection,
                    count,
                    maximum,
                });
            }
        }
        if self.validation_cameras.is_empty()
            || self.validation_cameras.len() > MAX_VALIDATION_CAMERAS
        {
            return Err(WorldSketchError::InvalidCameraCount(
                self.validation_cameras.len(),
            ));
        }
        Ok(())
    }

    fn validate_sources(&self) -> Result<(), WorldSketchError> {
        if self.source_images.is_empty() || self.source_images.len() > MAX_SOURCE_IMAGES {
            return Err(WorldSketchError::InvalidSourceCount(
                self.source_images.len(),
            ));
        }
        let mut source_ids = HashSet::with_capacity(self.source_images.len());
        let mut top_down_count = 0_usize;
        for source in &self.source_images {
            validate_logical_id("source image", &source.id)?;
            if !source_ids.insert(source.id.as_str()) {
                return Err(WorldSketchError::DuplicateSourceId(source.id.clone()));
            }
            source.validate()?;
            if source.role == WorldSketchSourceRole::TopDownPlan {
                top_down_count += 1;
            }
        }
        if top_down_count == 0 {
            return Err(WorldSketchError::MissingTopDownSource);
        }
        self.calibration.validate()?;
        let Some(top_down_source) = self
            .source_images
            .iter()
            .find(|source| source.id == self.calibration.top_down_source_id)
        else {
            return Err(WorldSketchError::UnknownCalibrationSource(
                self.calibration.top_down_source_id.clone(),
            ));
        };
        if top_down_source.role != WorldSketchSourceRole::TopDownPlan {
            return Err(WorldSketchError::InvalidCalibrationSourceRole(
                top_down_source.id.clone(),
            ));
        }
        if f64::from(self.calibration.origin_pixel[0]) > f64::from(top_down_source.pixel_width)
            || f64::from(self.calibration.origin_pixel[1]) > f64::from(top_down_source.pixel_height)
        {
            return Err(WorldSketchError::CalibrationOriginOutsideImage);
        }
        Ok(())
    }

    fn validate_terrain_intent(&self) -> Result<(), WorldSketchError> {
        let probe = TerrainDocument::new_v1(
            self.project_id,
            self.area_id,
            self.revision,
            "WorldSketch terrain validation",
            self.terrain_seed,
            TerrainExtent::new(self.extent_m.size_x_m, self.extent_m.size_z_m),
            self.terrain_height_sampling,
            default_material_layers(),
        );
        probe
            .validate()
            .map_err(WorldSketchError::InvalidTerrainIntent)
    }

    pub fn from_json(json: &str) -> Result<Self, WorldSketchError> {
        let document: Self = serde_json::from_str(json)?;
        document.validate()?;
        Ok(document)
    }

    pub fn to_pretty_json(&self) -> Result<String, WorldSketchError> {
        self.validate()?;
        Ok(serde_json::to_string_pretty(self)?)
    }

    pub fn content_hash(&self) -> Result<ContentHash, WorldSketchError> {
        self.validate()?;
        Ok(canonical_json_sha256(self)?)
    }
}

#[derive(Clone, Copy, Debug, PartialEq, Serialize, Deserialize)]
#[serde(deny_unknown_fields)]
pub struct WorldSketchExtent {
    pub size_x_m: f32,
    pub size_z_m: f32,
}

impl WorldSketchExtent {
    fn validate(self) -> Result<(), WorldSketchError> {
        for (field, value) in [("size_x_m", self.size_x_m), ("size_z_m", self.size_z_m)] {
            if !value.is_finite() || value <= 0.0 || value > MAX_EXTENT_M {
                return Err(WorldSketchError::InvalidExtent(field));
            }
        }
        Ok(())
    }
}

#[derive(Clone, Debug, PartialEq, Serialize, Deserialize)]
#[serde(deny_unknown_fields)]
pub struct WorldSketchSourceImage {
    pub id: String,
    pub role: WorldSketchSourceRole,
    pub path: String,
    pub sha256: ContentHash,
    pub pixel_width: u32,
    pub pixel_height: u32,
    pub provenance: WorldSketchSourceProvenance,
}

impl WorldSketchSourceImage {
    fn validate(&self) -> Result<(), WorldSketchError> {
        validate_relative_path("source image", &self.path)?;
        if self.pixel_width == 0 || self.pixel_height == 0 {
            return Err(WorldSketchError::InvalidSourceDimensions(self.id.clone()));
        }
        self.provenance.validate(&self.id)
    }
}

#[derive(Clone, Copy, Debug, Eq, PartialEq, Serialize, Deserialize)]
#[serde(rename_all = "snake_case")]
pub enum WorldSketchSourceRole {
    TopDownPlan,
    PerspectiveConcept,
    AssetMultiview,
}

#[derive(Clone, Debug, PartialEq, Serialize, Deserialize)]
#[serde(deny_unknown_fields)]
pub struct WorldSketchSourceProvenance {
    pub provider: String,
    pub tool: String,
    pub model: Option<String>,
    pub generated_utc: String,
    pub rights: String,
    pub prompt_path: String,
}

impl WorldSketchSourceProvenance {
    fn validate(&self, source_id: &str) -> Result<(), WorldSketchError> {
        for (field, value) in [
            ("provider", self.provider.as_str()),
            ("tool", self.tool.as_str()),
            ("generated_utc", self.generated_utc.as_str()),
            ("rights", self.rights.as_str()),
        ] {
            if value.trim().is_empty() {
                return Err(WorldSketchError::EmptyProvenanceField {
                    source_id: source_id.to_owned(),
                    field,
                });
            }
        }
        if self
            .model
            .as_ref()
            .is_some_and(|model| model.trim().is_empty())
        {
            return Err(WorldSketchError::EmptyProvenanceField {
                source_id: source_id.to_owned(),
                field: "model",
            });
        }
        validate_relative_path("prompt", &self.prompt_path)
    }
}

#[derive(Clone, Debug, PartialEq, Serialize, Deserialize)]
#[serde(deny_unknown_fields)]
pub struct WorldSketchCalibration {
    pub top_down_source_id: String,
    pub metres_per_pixel: f32,
    pub origin_pixel: [f32; 2],
    pub north_degrees_clockwise: f32,
    pub confidence: f32,
}

impl WorldSketchCalibration {
    fn validate(&self) -> Result<(), WorldSketchError> {
        validate_logical_id("calibration source", &self.top_down_source_id)?;
        if !positive_finite(self.metres_per_pixel)
            || !self
                .origin_pixel
                .iter()
                .all(|value| value.is_finite() && *value >= 0.0)
            || !self.north_degrees_clockwise.is_finite()
            || !(0.0..360.0).contains(&self.north_degrees_clockwise)
            || !unit_interval(self.confidence)
        {
            return Err(WorldSketchError::InvalidCalibration);
        }
        Ok(())
    }
}

#[derive(Clone, Copy, Debug, PartialEq, Serialize, Deserialize)]
#[serde(deny_unknown_fields)]
pub struct WorldSketchPoint2 {
    pub x_m: f32,
    pub z_m: f32,
}

impl WorldSketchPoint2 {
    fn validate(self, extent: WorldSketchExtent) -> Result<(), WorldSketchError> {
        let half_x = extent.size_x_m * 0.5;
        let half_z = extent.size_z_m * 0.5;
        if !self.x_m.is_finite()
            || !self.z_m.is_finite()
            || self.x_m < -half_x
            || self.x_m > half_x
            || self.z_m < -half_z
            || self.z_m > half_z
        {
            return Err(WorldSketchError::PointOutsideExtent {
                x_m: self.x_m,
                z_m: self.z_m,
            });
        }
        Ok(())
    }
}

#[derive(Clone, Debug, PartialEq, Serialize, Deserialize)]
#[serde(transparent)]
pub struct WorldSketchPolygon(pub Vec<WorldSketchPoint2>);

impl WorldSketchPolygon {
    fn validate(
        &self,
        extent: WorldSketchExtent,
        feature_id: &str,
    ) -> Result<(), WorldSketchError> {
        if self.0.len() < 3 || self.0.len() > MAX_POLYGON_POINTS {
            return Err(WorldSketchError::InvalidPolygonPointCount {
                feature_id: feature_id.to_owned(),
                count: self.0.len(),
            });
        }
        let mut previous = None;
        for point in &self.0 {
            point.validate(extent)?;
            if previous == Some((point.x_m.to_bits(), point.z_m.to_bits())) {
                return Err(WorldSketchError::RepeatedAdjacentPoint(
                    feature_id.to_owned(),
                ));
            }
            previous = Some((point.x_m.to_bits(), point.z_m.to_bits()));
        }
        Ok(())
    }
}

#[derive(Clone, Debug, PartialEq, Serialize, Deserialize)]
#[serde(deny_unknown_fields)]
pub struct WorldSketchSemanticRegion {
    pub id: String,
    pub kind: WorldSketchSemanticKind,
    pub polygon: WorldSketchPolygon,
    pub confidence: f32,
}

impl WorldSketchSemanticRegion {
    fn validate(&self, extent: WorldSketchExtent) -> Result<(), WorldSketchError> {
        self.polygon.validate(extent, &self.id)?;
        validate_confidence(&self.id, self.confidence)
    }
}

#[derive(Clone, Copy, Debug, Eq, PartialEq, Serialize, Deserialize)]
#[serde(rename_all = "snake_case")]
pub enum WorldSketchSemanticKind {
    Forest,
    Meadow,
    Rock,
    Water,
    Wetland,
    Buildable,
}

#[derive(Clone, Debug, PartialEq, Serialize, Deserialize)]
#[serde(deny_unknown_fields)]
pub struct WorldSketchSpline {
    pub id: String,
    pub kind: WorldSketchSplineKind,
    pub width_m: f32,
    /// Explicit earthwork budget; absent preserves the original per-kind recipe.
    #[serde(default, skip_serializing_if = "Option::is_none")]
    pub maximum_cut_m: Option<f32>,
    /// Optional bounded fill, restricted to an explicitly authored water profile.
    #[serde(default, skip_serializing_if = "Option::is_none")]
    pub maximum_fill_m: Option<f32>,
    /// Metres, Y up: one altitude per XZ control, interpolated without overshoot.
    /// Absent preserves the legacy terrain-derived profile exactly.
    #[serde(default, skip_serializing_if = "Option::is_none")]
    pub surface_heights_m: Option<Vec<f32>>,
    /// Optional broad valley regrading beyond the bank corridor, in metres.
    #[serde(default, skip_serializing_if = "Option::is_none")]
    pub valley_blend_width_m: Option<f32>,
    pub points: Vec<WorldSketchPoint2>,
    pub confidence: f32,
}

impl WorldSketchSpline {
    fn validate(&self, extent: WorldSketchExtent) -> Result<(), WorldSketchError> {
        if self
            .maximum_cut_m
            .is_some_and(|cut| !positive_finite(cut) || cut > 64.0)
        {
            return Err(WorldSketchError::InvalidSplineCutBudget(self.id.clone()));
        }
        if self.points.len() < 2 || self.points.len() > MAX_SPLINE_POINTS {
            return Err(WorldSketchError::InvalidSplinePointCount {
                feature_id: self.id.clone(),
                count: self.points.len(),
            });
        }
        if let Some(heights) = &self.surface_heights_m
            && (self.kind == WorldSketchSplineKind::Road
                || heights.len() != self.points.len()
                || heights
                    .iter()
                    .any(|height| !height.is_finite() || height.abs() > 1024.0)
                || heights.windows(2).any(|pair| pair[1] > pair[0]))
        {
            return Err(WorldSketchError::InvalidSplineSurfaceProfile(
                self.id.clone(),
            ));
        }
        if self.maximum_fill_m.is_some_and(|fill| {
            !fill.is_finite() || !(0.0..=64.0).contains(&fill) || self.surface_heights_m.is_none()
        }) {
            return Err(WorldSketchError::InvalidSplineFillBudget(self.id.clone()));
        }
        if self.valley_blend_width_m.is_some_and(|width| {
            !width.is_finite()
                || !(16.0..=192.0).contains(&width)
                || self.surface_heights_m.is_none()
                || self.maximum_cut_m.is_none()
                || self.maximum_fill_m.is_none()
        }) {
            return Err(WorldSketchError::InvalidValleyBlendWidth(self.id.clone()));
        }
        if !positive_finite(self.width_m) || self.width_m > extent.size_x_m.min(extent.size_z_m) {
            return Err(WorldSketchError::InvalidSplineWidth(self.id.clone()));
        }
        let mut previous = None;
        for point in &self.points {
            point.validate(extent)?;
            let key = (point.x_m.to_bits(), point.z_m.to_bits());
            if previous == Some(key) {
                return Err(WorldSketchError::RepeatedAdjacentPoint(self.id.clone()));
            }
            previous = Some(key);
        }
        validate_confidence(&self.id, self.confidence)
    }
}

#[derive(Clone, Copy, Debug, Eq, PartialEq, Serialize, Deserialize)]
#[serde(rename_all = "snake_case")]
pub enum WorldSketchSplineKind {
    Road,
    River,
    Creek,
}

#[derive(Clone, Debug, PartialEq, Serialize, Deserialize)]
#[serde(deny_unknown_fields)]
pub struct WorldSketchLandmark {
    pub id: String,
    pub kind: WorldSketchLandmarkKind,
    pub position: WorldSketchPoint2,
    pub influence_radius_m: f32,
    pub confidence: f32,
}

impl WorldSketchLandmark {
    fn validate(&self, extent: WorldSketchExtent) -> Result<(), WorldSketchError> {
        self.position.validate(extent)?;
        if !positive_finite(self.influence_radius_m)
            || self.influence_radius_m > extent.size_x_m.min(extent.size_z_m) * 0.5
        {
            return Err(WorldSketchError::InvalidLandmarkRadius(self.id.clone()));
        }
        validate_confidence(&self.id, self.confidence)
    }
}

#[derive(Clone, Copy, Debug, Eq, PartialEq, Serialize, Deserialize)]
#[serde(rename_all = "snake_case")]
pub enum WorldSketchLandmarkKind {
    Bridge,
    Village,
    Keep,
    Waterfall,
    PlayerSpawn,
    Vista,
}

#[derive(Clone, Debug, PartialEq, Serialize, Deserialize)]
#[serde(deny_unknown_fields)]
pub struct WorldSketchUncertaintyRegion {
    pub id: String,
    pub polygon: WorldSketchPolygon,
    pub confidence: f32,
    pub reason: String,
}

impl WorldSketchUncertaintyRegion {
    fn validate(&self, extent: WorldSketchExtent) -> Result<(), WorldSketchError> {
        self.polygon.validate(extent, &self.id)?;
        validate_confidence(&self.id, self.confidence)?;
        validate_name("uncertainty reason", &self.reason)
    }
}

#[derive(Clone, Debug, PartialEq, Serialize, Deserialize)]
#[serde(deny_unknown_fields)]
pub struct WorldSketchCamera {
    pub id: String,
    pub role: WorldSketchCameraRole,
    pub source_image_id: Option<String>,
    pub position_m: [f32; 3],
    pub target_m: [f32; 3],
    pub vertical_fov_degrees: f32,
    pub near_m: f32,
    pub far_m: f32,
}

impl WorldSketchCamera {
    fn validate(&self) -> Result<(), WorldSketchError> {
        if !self
            .position_m
            .iter()
            .chain(self.target_m.iter())
            .all(|value| value.is_finite())
            || self
                .position_m
                .iter()
                .zip(self.target_m.iter())
                .all(|(position, target)| position.to_bits() == target.to_bits())
            || !self.vertical_fov_degrees.is_finite()
            || self.vertical_fov_degrees <= 1.0
            || self.vertical_fov_degrees >= 179.0
            || !positive_finite(self.near_m)
            || !self.far_m.is_finite()
            || self.far_m <= self.near_m
        {
            return Err(WorldSketchError::InvalidCamera(self.id.clone()));
        }
        Ok(())
    }
}

#[derive(Clone, Copy, Debug, Eq, PartialEq, Serialize, Deserialize)]
#[serde(rename_all = "snake_case")]
pub enum WorldSketchCameraRole {
    ReferenceMatch,
    HumanHeight,
    Overview,
    Landmark,
}

/// Derived outputs for the first bounded Visual Valley compiler.
#[derive(Clone, Debug, PartialEq)]
pub struct CompiledWorldSketch {
    pub terrain: TerrainDocument,
    pub carved_terrain: TerrainDocument,
    pub vegetation: VegetationDocument,
    pub semantic_masks: WorldSketchSemanticMaskDocument,
    pub spline_carving: WorldSketchSplineCarvingDocument,
    pub traversal_audit: WorldSketchTraversalAuditDocument,
    pub bridge_placements: WorldSketchBridgePlacementDocument,
    pub blender_input: WorldSketchBlenderInput,
    pub report: WorldSketchCompilationReport,
}

/// Deterministic, derived interchange consumed by the Blender greybox worker.
#[derive(Clone, Debug, PartialEq, Serialize, Deserialize)]
#[serde(deny_unknown_fields)]
pub struct WorldSketchBlenderInput {
    pub schema: String,
    pub schema_version: u32,
    pub source_world_sketch_sha256: ContentHash,
    pub extent_m: WorldSketchExtent,
    pub reference_images: Vec<WorldSketchSourceImage>,
    pub height_grid: WorldSketchHeightGrid,
    pub semantic_masks: WorldSketchSemanticMaskDocument,
    pub spline_carving: WorldSketchSplineCarvingDocument,
    pub traversal_audit: WorldSketchTraversalAuditDocument,
    pub bridge_placements: WorldSketchBridgePlacementDocument,
    pub semantic_regions: Vec<WorldSketchSemanticRegion>,
    pub splines: Vec<WorldSketchSpline>,
    pub landmarks: Vec<WorldSketchLandmark>,
    pub uncertainty_regions: Vec<WorldSketchUncertaintyRegion>,
    pub locked_feature_ids: Vec<String>,
    pub validation_cameras: Vec<WorldSketchCamera>,
}

impl WorldSketchBlenderInput {
    pub fn to_pretty_json(&self) -> Result<String, WorldSketchCompilationError> {
        Ok(serde_json::to_string_pretty(self)?)
    }

    pub fn content_hash(&self) -> Result<ContentHash, WorldSketchCompilationError> {
        Ok(canonical_json_sha256(self)?)
    }
}

#[derive(Clone, Debug, PartialEq, Serialize, Deserialize)]
#[serde(deny_unknown_fields)]
pub struct WorldSketchHeightGrid {
    pub vertices_x: u16,
    pub vertices_z: u16,
    pub sample_spacing_m: f32,
    pub origin_x_m: f32,
    pub origin_z_m: f32,
    pub minimum_height_m: f32,
    pub maximum_height_m: f32,
    pub samples_m: Vec<f32>,
}

/// One deterministic grid shared by semantic-mask layers.
#[derive(Clone, Copy, Debug, PartialEq, Serialize, Deserialize)]
#[serde(deny_unknown_fields)]
pub struct WorldSketchMaskGrid {
    pub vertices_x: u16,
    pub vertices_z: u16,
    pub sample_spacing_m: f32,
    pub origin_x_m: f32,
    pub origin_z_m: f32,
}

/// One editable semantic polygon rasterized into a feathered, engine-neutral mask.
#[derive(Clone, Debug, PartialEq, Serialize, Deserialize)]
#[serde(deny_unknown_fields)]
pub struct WorldSketchSemanticMaskLayer {
    pub feature_id: String,
    pub kind: WorldSketchSemanticKind,
    pub confidence: f32,
    pub feather_width_m: f32,
    pub samples_u8: Vec<u8>,
}

/// Derived semantic masks. The polygons in `WorldSketch` remain the canonical editable source.
#[derive(Clone, Debug, PartialEq, Serialize, Deserialize)]
#[serde(deny_unknown_fields)]
pub struct WorldSketchSemanticMaskDocument {
    pub schema: String,
    pub schema_version: u32,
    pub source_world_sketch_sha256: ContentHash,
    pub grid: WorldSketchMaskGrid,
    pub layers: Vec<WorldSketchSemanticMaskLayer>,
}

impl WorldSketchSemanticMaskDocument {
    pub fn to_pretty_json(&self) -> Result<String, WorldSketchCompilationError> {
        Ok(serde_json::to_string_pretty(self)?)
    }

    pub fn content_hash(&self) -> Result<ContentHash, WorldSketchCompilationError> {
        Ok(canonical_json_sha256(self)?)
    }
}

/// One sampled centreline used by the deterministic carving cache and Blender worker.
#[derive(Clone, Copy, Debug, PartialEq, Serialize, Deserialize)]
#[serde(deny_unknown_fields)]
pub struct WorldSketchCarvingCenterlineSample {
    pub x_m: f32,
    pub z_m: f32,
    pub surface_height_m: f32,
    pub terrain_target_height_m: f32,
}

/// Per-spline policy and bounded materialization statistics.
#[derive(Clone, Debug, PartialEq, Serialize, Deserialize)]
#[serde(deny_unknown_fields)]
pub struct WorldSketchCarvedSpline {
    pub feature_id: String,
    pub kind: WorldSketchSplineKind,
    pub authored_width_m: f32,
    pub shoulder_width_m: f32,
    pub bed_depth_m: f32,
    pub maximum_cut_m: f32,
    pub maximum_fill_m: f32,
    pub affected_sample_count: usize,
    pub centerline_samples: Vec<WorldSketchCarvingCenterlineSample>,
}

/// Deterministic terrain sculpture and combined masks derived from authored splines.
#[derive(Clone, Debug, PartialEq, Serialize, Deserialize)]
#[serde(deny_unknown_fields)]
pub struct WorldSketchSplineCarvingDocument {
    pub schema: String,
    pub schema_version: u32,
    pub source_world_sketch_sha256: ContentHash,
    pub source_height_grid_sha256: ContentHash,
    pub carved_height_grid: WorldSketchHeightGrid,
    pub road_surface_mask_u8: Vec<u8>,
    pub water_bed_mask_u8: Vec<u8>,
    pub bank_mask_u8: Vec<u8>,
    pub vegetation_exclusion_mask_u8: Vec<u8>,
    pub minimum_height_delta_m: f32,
    pub maximum_height_delta_m: f32,
    pub modified_sample_count: usize,
    pub vegetation_exclusion_sample_count: usize,
    pub splines: Vec<WorldSketchCarvedSpline>,
}

impl WorldSketchSplineCarvingDocument {
    pub fn to_pretty_json(&self) -> Result<String, WorldSketchCompilationError> {
        Ok(serde_json::to_string_pretty(self)?)
    }

    pub fn content_hash(&self) -> Result<ContentHash, WorldSketchCompilationError> {
        Ok(canonical_json_sha256(self)?)
    }
}

/// One authored road evaluated against the derived terrain-only walkability field.
#[derive(Clone, Debug, PartialEq, Serialize, Deserialize)]
#[serde(deny_unknown_fields)]
pub struct WorldSketchRouteTraversalAudit {
    pub feature_id: String,
    pub centerline_sample_count: usize,
    pub slope_walkable_sample_count: usize,
    pub water_blocked_sample_count: usize,
    pub land_walkable_sample_count: usize,
    pub centerline_continuous_without_structures: bool,
    pub endpoints_land_walkable: bool,
    pub endpoints_connected_without_structures: bool,
}

/// Why an authored road/water crossing is not yet a qualified runtime traversal link.
#[derive(Clone, Copy, Debug, Eq, PartialEq, Serialize, Deserialize)]
#[serde(rename_all = "snake_case")]
pub enum WorldSketchCrossingRuntimeStatus {
    BridgeRuntimeProbeRequired,
    MissingBridgeLandmark,
}

/// One detected overlap between an authored road and a river or creek.
#[derive(Clone, Debug, PartialEq, Serialize, Deserialize)]
#[serde(deny_unknown_fields)]
pub struct WorldSketchCrossingTraversalAudit {
    pub road_feature_id: String,
    pub water_feature_id: String,
    pub bridge_landmark_id: Option<String>,
    pub center_m: [f32; 2],
    pub overlap_sample_count: usize,
    pub runtime_status: WorldSketchCrossingRuntimeStatus,
}

/// This audit deliberately remains provider-neutral; it is not a navmesh or pathfinder.
#[derive(Clone, Copy, Debug, Eq, PartialEq, Serialize, Deserialize)]
#[serde(rename_all = "snake_case")]
pub enum WorldSketchNavigationProviderStatus {
    NotSelected,
}

/// Deterministic terrain walkability evidence derived from the player slope contract.
///
/// Water remains blocked and bridge candidates remain non-walkable until runtime geometry and a
/// collider exist. This prevents a concept landmark from becoming a false gameplay guarantee.
#[derive(Clone, Debug, PartialEq, Serialize, Deserialize)]
#[serde(deny_unknown_fields)]
pub struct WorldSketchTraversalAuditDocument {
    pub schema: String,
    pub schema_version: u32,
    pub source_world_sketch_sha256: ContentHash,
    pub source_terrain_sha256: ContentHash,
    pub source_player_controller_sha256: ContentHash,
    pub grid: WorldSketchMaskGrid,
    pub maximum_walkable_slope_degrees: f32,
    pub maximum_observed_slope_degrees: f32,
    pub slope_degrees_u8: Vec<u8>,
    pub slope_walkable_mask_u8: Vec<u8>,
    pub water_blocked_mask_u8: Vec<u8>,
    pub land_walkable_mask_u8: Vec<u8>,
    pub bridge_candidate_mask_u8: Vec<u8>,
    pub slope_walkable_sample_count: usize,
    pub water_blocked_sample_count: usize,
    pub land_walkable_sample_count: usize,
    pub connected_land_component_count: usize,
    pub largest_land_component_sample_count: usize,
    pub routes: Vec<WorldSketchRouteTraversalAudit>,
    pub crossings: Vec<WorldSketchCrossingTraversalAudit>,
    pub navigation_provider_status: WorldSketchNavigationProviderStatus,
}

impl WorldSketchTraversalAuditDocument {
    pub fn to_pretty_json(&self) -> Result<String, WorldSketchCompilationError> {
        Ok(serde_json::to_string_pretty(self)?)
    }

    pub fn content_hash(&self) -> Result<ContentHash, WorldSketchCompilationError> {
        Ok(canonical_json_sha256(self)?)
    }
}

/// Collider primitive materialized by Forge for a bridge deck.
///
/// The engine-neutral document owns dimensions and endpoints. Bevy/Rapier handles remain runtime
/// adapter state and never cross this boundary.
#[derive(Clone, Copy, Debug, Eq, PartialEq, Serialize, Deserialize)]
#[serde(rename_all = "snake_case")]
pub enum WorldSketchBridgeCollisionShape {
    OrientedBox,
}

/// One terrain support required by the procedural bridge presentation.
#[derive(Clone, Copy, Debug, PartialEq, Serialize, Deserialize)]
#[serde(deny_unknown_fields)]
pub struct WorldSketchBridgeSupport {
    /// Position along the deck centreline, strictly between zero and one.
    pub center_fraction: f32,
    pub ground_height_m: f32,
    pub width_along_bridge_m: f32,
    pub depth_across_bridge_m: f32,
}

/// One deterministic bridge placement linked to an authored landmark and detected crossing.
#[derive(Clone, Debug, PartialEq, Serialize, Deserialize)]
#[serde(deny_unknown_fields)]
pub struct WorldSketchBridgePlacement {
    pub landmark_id: String,
    pub road_feature_id: String,
    pub water_feature_id: String,
    pub crossing_center_m: [f32; 2],
    pub water_direction_xz: [f32; 2],
    pub water_width_m: f32,
    pub crossing_bed_height_m: f32,
    /// World-space top-centre of the first deck landing.
    pub deck_start_surface_m: [f32; 3],
    /// World-space top-centre of the second deck landing.
    pub deck_end_surface_m: [f32; 3],
    pub deck_width_m: f32,
    pub deck_thickness_m: f32,
    pub slope_degrees: f32,
    pub parapet_height_m: f32,
    pub parapet_thickness_m: f32,
    pub collision_shape: WorldSketchBridgeCollisionShape,
    pub supports: Vec<WorldSketchBridgeSupport>,
}

/// Derived placement/collision contract. `WorldSketch` vectors remain the editable source.
#[derive(Clone, Debug, PartialEq, Serialize, Deserialize)]
#[serde(deny_unknown_fields)]
pub struct WorldSketchBridgePlacementDocument {
    pub schema: String,
    pub schema_version: u32,
    pub source_world_sketch_sha256: ContentHash,
    pub source_carved_terrain_sha256: ContentHash,
    pub source_traversal_audit_sha256: ContentHash,
    pub placements: Vec<WorldSketchBridgePlacement>,
}

impl WorldSketchBridgePlacementDocument {
    pub fn validate(&self) -> Result<(), WorldSketchBridgePlacementError> {
        if self.schema != "forge.world-bridge-placements" {
            return Err(WorldSketchBridgePlacementError::InvalidSchema(
                self.schema.clone(),
            ));
        }
        if self.schema_version != 1 {
            return Err(WorldSketchBridgePlacementError::UnsupportedVersion(
                self.schema_version,
            ));
        }
        if self.placements.len() > MAX_BRIDGE_PLACEMENTS {
            return Err(WorldSketchBridgePlacementError::InvalidPlacementCount(
                self.placements.len(),
            ));
        }

        let mut landmark_ids = HashSet::with_capacity(self.placements.len());
        for placement in &self.placements {
            if placement.landmark_id.trim().is_empty()
                || placement.road_feature_id.trim().is_empty()
                || placement.water_feature_id.trim().is_empty()
            {
                return Err(WorldSketchBridgePlacementError::InvalidPlacement(
                    placement.landmark_id.clone(),
                ));
            }
            if !landmark_ids.insert(placement.landmark_id.as_str()) {
                return Err(WorldSketchBridgePlacementError::DuplicateLandmark(
                    placement.landmark_id.clone(),
                ));
            }
            let finite_geometry = placement
                .crossing_center_m
                .iter()
                .chain(placement.water_direction_xz.iter())
                .chain(placement.deck_start_surface_m.iter())
                .chain(placement.deck_end_surface_m.iter())
                .all(|value| value.is_finite());
            let start = placement.deck_start_surface_m;
            let end = placement.deck_end_surface_m;
            let horizontal_length = (end[0] - start[0]).hypot(end[2] - start[2]);
            let derived_slope = (end[1] - start[1])
                .atan2(horizontal_length)
                .to_degrees()
                .abs();
            let valid_dimensions = positive_finite(placement.deck_width_m)
                && positive_finite(placement.deck_thickness_m)
                && positive_finite(placement.water_width_m)
                && positive_finite(placement.parapet_height_m)
                && positive_finite(placement.parapet_thickness_m)
                && placement.parapet_thickness_m < placement.deck_width_m * 0.5;
            let supports_valid = !placement.supports.is_empty()
                && placement.supports.iter().all(|support| {
                    support.center_fraction.is_finite()
                        && support.center_fraction > 0.0
                        && support.center_fraction < 1.0
                        && support.ground_height_m.is_finite()
                        && positive_finite(support.width_along_bridge_m)
                        && positive_finite(support.depth_across_bridge_m)
                });
            if !finite_geometry
                || horizontal_length <= 1.0
                || horizontal_length > 256.0
                || !placement.crossing_bed_height_m.is_finite()
                || (placement.water_direction_xz[0].hypot(placement.water_direction_xz[1]) - 1.0)
                    .abs()
                    > 1.0e-4
                || !valid_dimensions
                || !placement.slope_degrees.is_finite()
                || placement.slope_degrees < 0.0
                || placement.slope_degrees > 45.0
                || (placement.slope_degrees - derived_slope).abs() > 0.01
                || !supports_valid
            {
                return Err(WorldSketchBridgePlacementError::InvalidPlacement(
                    placement.landmark_id.clone(),
                ));
            }
        }
        Ok(())
    }

    pub fn from_json(json: &str) -> Result<Self, WorldSketchBridgePlacementError> {
        let document: Self = serde_json::from_str(json)?;
        document.validate()?;
        Ok(document)
    }

    pub fn to_pretty_json(&self) -> Result<String, WorldSketchBridgePlacementError> {
        self.validate()?;
        Ok(serde_json::to_string_pretty(self)?)
    }

    pub fn content_hash(&self) -> Result<ContentHash, WorldSketchBridgePlacementError> {
        self.validate()?;
        Ok(canonical_json_sha256(self)?)
    }
}

#[derive(Debug, Error)]
pub enum WorldSketchBridgePlacementError {
    #[error("invalid bridge-placement schema `{0}`")]
    InvalidSchema(String),
    #[error("unsupported bridge-placement schema version {0}")]
    UnsupportedVersion(u32),
    #[error(
        "bridge-placement document may contain at most {MAX_BRIDGE_PLACEMENTS} placements, found {0}"
    )]
    InvalidPlacementCount(usize),
    #[error("duplicate bridge landmark `{0}`")]
    DuplicateLandmark(String),
    #[error("bridge placement `{0}` has invalid geometry")]
    InvalidPlacement(String),
    #[error("invalid bridge-placement JSON: {0}")]
    Json(#[from] serde_json::Error),
    #[error("cannot hash bridge-placement document: {0}")]
    Hash(#[from] CanonicalJsonError),
}

#[derive(Clone, Debug, Eq, PartialEq, Serialize, Deserialize)]
#[serde(deny_unknown_fields)]
pub struct WorldSketchCompilationReport {
    pub schema: String,
    pub schema_version: u32,
    pub source_world_sketch_sha256: ContentHash,
    pub terrain_sha256: ContentHash,
    pub carved_terrain_sha256: ContentHash,
    pub vegetation_sha256: ContentHash,
    pub semantic_masks_sha256: ContentHash,
    pub spline_carving_sha256: ContentHash,
    pub traversal_audit_sha256: ContentHash,
    pub bridge_placements_sha256: ContentHash,
    pub blender_input_sha256: ContentHash,
    pub source_image_count: usize,
    pub semantic_region_count: usize,
    pub spline_count: usize,
    pub landmark_count: usize,
    pub validation_camera_count: usize,
    pub materialized_outputs: Vec<String>,
    pub validated_pending_outputs: Vec<String>,
}

impl WorldSketchCompilationReport {
    pub fn to_pretty_json(&self) -> Result<String, WorldSketchCompilationError> {
        Ok(serde_json::to_string_pretty(self)?)
    }
}

pub fn compile_world_sketch(
    sketch: &WorldSketchDocument,
) -> Result<CompiledWorldSketch, WorldSketchCompilationError> {
    sketch.validate()?;
    if sketch.extent_m.size_x_m.to_bits() != VISUAL_VALLEY_EXTENT_M.to_bits()
        || sketch.extent_m.size_z_m.to_bits() != VISUAL_VALLEY_EXTENT_M.to_bits()
    {
        return Err(WorldSketchCompilationError::UnsupportedVisualValleyExtent);
    }

    let terrain = TerrainDocument::new_v1(
        sketch.project_id,
        sketch.area_id,
        sketch.revision,
        "Visual Valley terrain",
        sketch.terrain_seed,
        TerrainExtent::new(sketch.extent_m.size_x_m, sketch.extent_m.size_z_m),
        sketch.terrain_height_sampling,
        default_material_layers(),
    );
    terrain.validate()?;

    let mut vegetation = base_sketch_vegetation(sketch)?;

    let terrain_plan = TerrainGenerationPlan::from_document(&terrain)?;
    let height_grid = build_height_grid(&terrain_plan)?;
    let source_world_sketch_sha256 = sketch.content_hash()?;
    let semantic_masks = build_semantic_masks(sketch, source_world_sketch_sha256);
    apply_forest_density(
        &mut vegetation,
        &semantic_masks,
        sketch.forest_scatter.as_ref(),
    );
    let mut spline_carving =
        build_spline_carving(sketch, &height_grid, source_world_sketch_sha256)?;
    let carved_terrain = build_carved_terrain(&terrain, &height_grid, &spline_carving)?;
    let carved_height_grid =
        build_height_grid(&TerrainGenerationPlan::from_document(&carved_terrain)?)?;
    replace_carved_height_grid(&mut spline_carving, &height_grid, carved_height_grid);
    vegetation.exclusions.extend(
        spline_carving
            .splines
            .iter()
            .map(spline_vegetation_exclusion),
    );
    vegetation.validate()?;
    let player_controller = embedded_visual_valley_player_controller_profile()?;
    let traversal_audit = build_traversal_audit(
        sketch,
        &carved_terrain,
        &player_controller,
        &spline_carving,
        source_world_sketch_sha256,
    )?;
    let bridge_placements = build_bridge_placements(
        sketch,
        &carved_terrain,
        &spline_carving,
        &traversal_audit,
        source_world_sketch_sha256,
    )?;
    let blender_input = build_blender_input(
        sketch,
        height_grid,
        semantic_masks.clone(),
        spline_carving.clone(),
        traversal_audit.clone(),
        bridge_placements.clone(),
    )?;

    let report = WorldSketchCompilationReport {
        schema: "forge.world-sketch-compilation".to_owned(),
        schema_version: 4,
        source_world_sketch_sha256,
        terrain_sha256: terrain.content_hash()?,
        carved_terrain_sha256: carved_terrain.content_hash()?,
        vegetation_sha256: vegetation.content_hash()?,
        semantic_masks_sha256: semantic_masks.content_hash()?,
        spline_carving_sha256: spline_carving.content_hash()?,
        traversal_audit_sha256: traversal_audit.content_hash()?,
        bridge_placements_sha256: bridge_placements.content_hash()?,
        blender_input_sha256: blender_input.content_hash()?,
        source_image_count: sketch.source_images.len(),
        semantic_region_count: sketch.semantic_regions.len(),
        spline_count: sketch.splines.len(),
        landmark_count: sketch.landmarks.len(),
        validation_camera_count: sketch.validation_cameras.len(),
        materialized_outputs: materialized_world_sketch_outputs(),
        validated_pending_outputs: pending_world_sketch_outputs(),
    };

    Ok(CompiledWorldSketch {
        terrain,
        carved_terrain,
        vegetation,
        semantic_masks,
        spline_carving,
        traversal_audit,
        bridge_placements,
        blender_input,
        report,
    })
}

fn base_sketch_vegetation(
    sketch: &WorldSketchDocument,
) -> Result<VegetationDocument, VegetationDocumentError> {
    let mut vegetation = VisualValleyVegetationRecipe {
        seed: sketch.vegetation_seed,
    }
    .build()?;
    vegetation.project_id = sketch.project_id;
    vegetation.area_id = sketch.area_id;
    vegetation.revision = sketch.revision;
    "Visual Valley vegetation".clone_into(&mut vegetation.name);
    vegetation.extent_m = VegetationExtent::new(sketch.extent_m.size_x_m, sketch.extent_m.size_z_m);
    // Replace the standalone fixture's exclusions with authored clearings.
    vegetation.exclusions = spawn_vegetation_exclusions(sketch);
    vegetation.validate()?;
    Ok(vegetation)
}

fn apply_forest_density(
    vegetation: &mut VegetationDocument,
    masks: &WorldSketchSemanticMaskDocument,
    forest: Option<&WorldSketchForestScatter>,
) {
    let Some(forest) = forest else {
        return;
    };
    let forests: Vec<_> = masks
        .layers
        .iter()
        .filter(|layer| layer.kind == WorldSketchSemanticKind::Forest)
        .collect();
    if forests.is_empty() {
        return; // Sketches without a forest retain their existing recipe.
    }
    let samples_u8: Vec<u8> = (0..usize::from(masks.grid.vertices_x).pow(2))
        .map(|index| {
            let forest = forests
                .iter()
                .map(|layer| layer.samples_u8[index])
                .max()
                .unwrap_or(0);
            let clearing = masks
                .layers
                .iter()
                .filter(|layer| layer.kind == WorldSketchSemanticKind::Buildable)
                .map(|layer| layer.samples_u8[index])
                .max()
                .unwrap_or(0);
            forest.saturating_sub(clearing)
        })
        .collect();
    for layer in &mut vegetation.layers {
        if layer.kind != forge_project::VegetationKind::Tree {
            continue;
        }
        layer.density_mask = Some(forge_project::VegetationDensityMask {
            vertices_per_axis: masks.grid.vertices_x,
            samples_u8: samples_u8.clone(),
        });
        layer.acceptance_probability = forest.acceptance_probability;
        layer.attempts_per_cell = forest.attempts_per_cell;
        layer.minimum_spacing_m = forest.minimum_spacing_m;
        // The valley's existing reference prototype is 5.65 m high. Scale the
        // generated population, not the separately authored kit or player.
        layer.minimum_scale = forest.minimum_scale;
        layer.maximum_scale = forest.maximum_scale;
        if let Some(collision) = &mut layer.collision {
            collision.radius_m = 0.20;
        }
        layer.density_pattern = Some(forge_project::VegetationDensityPattern {
            patch_size_m: 24.0,
            minimum_multiplier: 0.55,
            maximum_multiplier: 1.4,
        });
    }
}

fn spawn_vegetation_exclusions(sketch: &WorldSketchDocument) -> Vec<VegetationExclusion> {
    sketch
        .landmarks
        .iter()
        .filter(|landmark| landmark.kind == WorldSketchLandmarkKind::PlayerSpawn)
        .map(|landmark| VegetationExclusion::Circle {
            id: VegetationExclusionId::new(format!(
                "world_sketch_spawn_{}",
                forge_core::EntityId::deterministic(format!("spawn-exclusion/{}", landmark.id))
                    .to_string()
                    .replace('-', "_")
            )),
            center_x_m: landmark.position.x_m,
            center_z_m: landmark.position.z_m,
            radius_m: landmark.influence_radius_m,
        })
        .collect()
}

fn spline_vegetation_exclusion(spline: &WorldSketchCarvedSpline) -> VegetationExclusion {
    VegetationExclusion::PolylineCorridor {
        id: VegetationExclusionId::new(
            format!(
                "world-sketch-{}",
                forge_core::EntityId::deterministic(format!(
                    "spline-exclusion/{}",
                    spline.feature_id
                ))
            )
            .replace('-', "_"),
        ),
        points_xz_m: spline
            .centerline_samples
            .iter()
            .map(|p| [p.x_m, p.z_m])
            .collect(),
        // Match the fully excluded core of the existing carving mask; its feather
        // remains a DCC density hint, not an implicit runtime random threshold.
        half_width_m: spline.authored_width_m * 0.5 + spline.shoulder_width_m * 0.55,
    }
}

fn materialized_world_sketch_outputs() -> Vec<String> {
    [
        "terrain",
        "carved_terrain",
        "vegetation",
        "semantic_masks",
        "spline_carving",
        "traversal_audit",
        "bridge_placements",
        "blender_input",
    ]
    .map(str::to_owned)
    .to_vec()
}

fn pending_world_sketch_outputs() -> Vec<String> {
    [
        "non_bridge_landmark_placements",
        "bridge_runtime_crossing_qualification",
        "runtime_navigation_provider_binding",
        "validation_camera_runtime_binding",
    ]
    .map(str::to_owned)
    .to_vec()
}

fn build_height_grid(
    terrain_plan: &TerrainGenerationPlan,
) -> Result<WorldSketchHeightGrid, WorldSketchCompilationError> {
    let vertices = usize::from(TERRAIN_GLOBAL_VERTICES_PER_AXIS);
    let mut samples_m = Vec::with_capacity(vertices * vertices);
    let mut minimum_height_m = f32::INFINITY;
    let mut maximum_height_m = f32::NEG_INFINITY;
    for z in 0..TERRAIN_GLOBAL_VERTICES_PER_AXIS {
        let z_m = -TERRAIN_HALF_EXTENT_M + f32::from(z) * TERRAIN_SAMPLE_SPACING_M;
        for x in 0..TERRAIN_GLOBAL_VERTICES_PER_AXIS {
            let x_m = -TERRAIN_HALF_EXTENT_M + f32::from(x) * TERRAIN_SAMPLE_SPACING_M;
            let height_m = terrain_plan.height_at(x_m, z_m)?;
            minimum_height_m = minimum_height_m.min(height_m);
            maximum_height_m = maximum_height_m.max(height_m);
            samples_m.push(height_m);
        }
    }

    Ok(WorldSketchHeightGrid {
        vertices_x: TERRAIN_GLOBAL_VERTICES_PER_AXIS,
        vertices_z: TERRAIN_GLOBAL_VERTICES_PER_AXIS,
        sample_spacing_m: TERRAIN_SAMPLE_SPACING_M,
        origin_x_m: -TERRAIN_HALF_EXTENT_M,
        origin_z_m: -TERRAIN_HALF_EXTENT_M,
        minimum_height_m,
        maximum_height_m,
        samples_m,
    })
}

fn build_semantic_masks(
    sketch: &WorldSketchDocument,
    source_world_sketch_sha256: ContentHash,
) -> WorldSketchSemanticMaskDocument {
    let grid = WorldSketchMaskGrid {
        vertices_x: TERRAIN_GLOBAL_VERTICES_PER_AXIS,
        vertices_z: TERRAIN_GLOBAL_VERTICES_PER_AXIS,
        sample_spacing_m: TERRAIN_SAMPLE_SPACING_M,
        origin_x_m: -TERRAIN_HALF_EXTENT_M,
        origin_z_m: -TERRAIN_HALF_EXTENT_M,
    };
    let sample_count = usize::from(grid.vertices_x) * usize::from(grid.vertices_z);
    let mut layers = Vec::with_capacity(sketch.semantic_regions.len());
    for region in &sketch.semantic_regions {
        let feather_width_m = semantic_feather_width(region.kind);
        let mut samples_u8 = Vec::with_capacity(sample_count);
        for z in 0..grid.vertices_z {
            let z_m = grid.origin_z_m + f32::from(z) * grid.sample_spacing_m;
            for x in 0..grid.vertices_x {
                let x_m = grid.origin_x_m + f32::from(x) * grid.sample_spacing_m;
                samples_u8.push(quantize_unit(semantic_mask_weight(
                    x_m,
                    z_m,
                    &region.polygon,
                    feather_width_m,
                )));
            }
        }
        layers.push(WorldSketchSemanticMaskLayer {
            feature_id: region.id.clone(),
            kind: region.kind,
            confidence: region.confidence,
            feather_width_m,
            samples_u8,
        });
    }

    WorldSketchSemanticMaskDocument {
        schema: "forge.world-semantic-masks".to_owned(),
        schema_version: 1,
        source_world_sketch_sha256,
        grid,
        layers,
    }
}

fn semantic_feather_width(kind: WorldSketchSemanticKind) -> f32 {
    match kind {
        WorldSketchSemanticKind::Forest | WorldSketchSemanticKind::Meadow => 12.0,
        WorldSketchSemanticKind::Rock | WorldSketchSemanticKind::Wetland => 10.0,
        WorldSketchSemanticKind::Water => 8.0,
        WorldSketchSemanticKind::Buildable => 6.0,
    }
}

fn semantic_mask_weight(
    x_m: f32,
    z_m: f32,
    polygon: &WorldSketchPolygon,
    feather_width_m: f32,
) -> f32 {
    let distance_m = polygon
        .0
        .iter()
        .zip(polygon.0.iter().cycle().skip(1))
        .map(|(start, end)| distance_to_segment(x_m, z_m, *start, *end))
        .fold(f32::INFINITY, f32::min);
    let signed_distance_m = if point_in_polygon(x_m, z_m, polygon) {
        distance_m
    } else {
        -distance_m
    };
    smoothstep((0.5 + signed_distance_m / (2.0 * feather_width_m)).clamp(0.0, 1.0))
}

fn point_in_polygon(x_m: f32, z_m: f32, polygon: &WorldSketchPolygon) -> bool {
    let mut inside = false;
    let mut previous = polygon.0[polygon.0.len() - 1];
    for current in &polygon.0 {
        if ((previous.z_m > z_m) != (current.z_m > z_m))
            && x_m
                < (current.x_m - previous.x_m) * (z_m - previous.z_m) / (current.z_m - previous.z_m)
                    + previous.x_m
        {
            inside = !inside;
        }
        previous = *current;
    }
    inside
}

fn distance_to_segment(
    x_m: f32,
    z_m: f32,
    start: WorldSketchPoint2,
    end: WorldSketchPoint2,
) -> f32 {
    let segment_x = end.x_m - start.x_m;
    let segment_z = end.z_m - start.z_m;
    let length_squared = segment_x * segment_x + segment_z * segment_z;
    let fraction = if length_squared > 0.0 {
        ((x_m - start.x_m) * segment_x + (z_m - start.z_m) * segment_z) / length_squared
    } else {
        0.0
    }
    .clamp(0.0, 1.0);
    let nearest_x = start.x_m + segment_x * fraction;
    let nearest_z = start.z_m + segment_z * fraction;
    (x_m - nearest_x).hypot(z_m - nearest_z)
}

#[derive(Clone, Copy)]
#[allow(clippy::struct_field_names)]
struct SplineCarvingPolicy {
    shoulder_width_m: f32,
    bed_depth_m: f32,
    maximum_cut_m: f32,
    maximum_fill_m: f32,
}

fn spline_carving_policy(kind: WorldSketchSplineKind) -> SplineCarvingPolicy {
    match kind {
        WorldSketchSplineKind::Road => SplineCarvingPolicy {
            shoulder_width_m: 4.0,
            bed_depth_m: 0.0,
            maximum_cut_m: 1.5,
            maximum_fill_m: 1.0,
        },
        WorldSketchSplineKind::River => SplineCarvingPolicy {
            shoulder_width_m: 12.0,
            bed_depth_m: 2.8,
            maximum_cut_m: 5.0,
            maximum_fill_m: 0.0,
        },
        WorldSketchSplineKind::Creek => SplineCarvingPolicy {
            shoulder_width_m: 6.0,
            bed_depth_m: 1.4,
            maximum_cut_m: 2.8,
            maximum_fill_m: 0.0,
        },
    }
}

fn grade_river_valleys(
    sketch: &WorldSketchDocument,
    source: &WorldSketchHeightGrid,
) -> WorldSketchHeightGrid {
    let mut grid = source.clone();
    let mut rivers = sketch
        .splines
        .iter()
        .filter(|spline| spline.valley_blend_width_m.is_some())
        .collect::<Vec<_>>();
    rivers.sort_by(|left, right| left.id.cmp(&right.id));
    for river in rivers {
        // Validation requires all these values together; no implicit earthwork budget.
        let (Some(blend_width), Some(maximum_cut), Some(maximum_fill)) = (
            river.valley_blend_width_m,
            river.maximum_cut_m,
            river.maximum_fill_m,
        ) else {
            continue;
        };
        let policy = spline_carving_policy(river.kind);
        let centerline = build_carving_centerline(river, source, policy);
        let corridor = river.width_m * 0.5 + policy.shoulder_width_m;
        for z in 0..source.vertices_z {
            for x in 0..source.vertices_x {
                let x_m = source.origin_x_m + f32::from(x) * source.sample_spacing_m;
                let z_m = source.origin_z_m + f32::from(z) * source.sample_spacing_m;
                let (distance, water_height, _) = nearest_centerline_values(x_m, z_m, &centerline);
                let mut weight = 1.0 - smoothstep((distance - corridor) / blend_width);
                if weight <= 0.0 {
                    continue;
                }
                for landmark in &sketch.landmarks {
                    if matches!(
                        landmark.kind,
                        WorldSketchLandmarkKind::Village | WorldSketchLandmarkKind::Keep
                    ) && sketch.locked_feature_ids.contains(&landmark.id)
                    {
                        let distance =
                            (x_m - landmark.position.x_m).hypot(z_m - landmark.position.z_m);
                        weight *= smoothstep((distance - landmark.influence_radius_m) / 16.0);
                    }
                }
                let index = usize::from(z) * usize::from(source.vertices_x) + usize::from(x);
                let original = source.samples_m[index];
                let target = lerp(grid.samples_m[index], water_height + 1.5, weight);
                grid.samples_m[index] =
                    original + (target - original).clamp(-maximum_cut, maximum_fill);
            }
        }
    }
    grid.minimum_height_m = grid.samples_m.iter().copied().fold(f32::INFINITY, f32::min);
    grid.maximum_height_m = grid
        .samples_m
        .iter()
        .copied()
        .fold(f32::NEG_INFINITY, f32::max);
    grid
}

#[allow(clippy::too_many_lines)]
fn build_spline_carving(
    sketch: &WorldSketchDocument,
    source_grid: &WorldSketchHeightGrid,
    source_world_sketch_sha256: ContentHash,
) -> Result<WorldSketchSplineCarvingDocument, WorldSketchCompilationError> {
    let sample_count = source_grid.samples_m.len();
    let grading_grid = grade_river_valleys(sketch, source_grid);
    let mut carved_samples_m = grading_grid.samples_m.clone();
    let mut road_surface_mask_u8 = vec![0_u8; sample_count];
    let mut water_bed_mask_u8 = vec![0_u8; sample_count];
    let mut bank_mask_u8 = vec![0_u8; sample_count];
    let mut vegetation_exclusion_mask_u8 = vec![0_u8; sample_count];
    let mut splines = sketch.splines.iter().collect::<Vec<_>>();
    splines.sort_by(|left, right| {
        spline_precedence(left.kind)
            .cmp(&spline_precedence(right.kind))
            .then_with(|| left.id.cmp(&right.id))
    });

    let mut carved_splines = Vec::with_capacity(splines.len());
    for spline in splines {
        let mut policy = spline_carving_policy(spline.kind);
        if let Some(maximum_cut_m) = spline.maximum_cut_m {
            policy.maximum_cut_m = maximum_cut_m;
        }
        if let Some(maximum_fill_m) = spline.maximum_fill_m {
            policy.maximum_fill_m = maximum_fill_m;
        }
        let centerline_samples = build_carving_centerline(spline, &grading_grid, policy);
        let half_width_m = spline.width_m * 0.5;
        let mut affected_sample_count = 0_usize;
        for z in 0..source_grid.vertices_z {
            let z_m = source_grid.origin_z_m + f32::from(z) * source_grid.sample_spacing_m;
            for x in 0..source_grid.vertices_x {
                let x_m = source_grid.origin_x_m + f32::from(x) * source_grid.sample_spacing_m;
                let index = usize::from(z) * usize::from(source_grid.vertices_x) + usize::from(x);
                let source_height_m = grading_grid.samples_m[index];
                let (distance_m, surface_height_m, terrain_target_height_m) =
                    nearest_centerline_values(x_m, z_m, &centerline_samples);
                let before_height_m = carved_samples_m[index];

                match spline.kind {
                    WorldSketchSplineKind::Road => {
                        let weight =
                            shoulder_weight(distance_m, half_width_m, policy.shoulder_width_m);
                        if weight > 0.0 {
                            let delta_m = (terrain_target_height_m - source_height_m)
                                .clamp(-policy.maximum_cut_m, policy.maximum_fill_m)
                                * weight;
                            carved_samples_m[index] = source_height_m + delta_m;
                            update_mask(&mut road_surface_mask_u8[index], weight);
                        }
                    }
                    WorldSketchSplineKind::River | WorldSketchSplineKind::Creek => {
                        let outer_edge_m = half_width_m + policy.shoulder_width_m;
                        if distance_m <= outer_edge_m {
                            let candidate_height_m = if distance_m <= half_width_m {
                                let across = (distance_m / half_width_m).clamp(0.0, 1.0);
                                terrain_target_height_m
                                    + policy.bed_depth_m * 0.68 * smoothstep(across)
                            } else {
                                let bank_fraction = smoothstep(
                                    (distance_m - half_width_m) / policy.shoulder_width_m,
                                );
                                let edge_height_m = surface_height_m - policy.bed_depth_m * 0.32;
                                lerp(edge_height_m, source_height_m, bank_fraction)
                            };
                            let budget_source_height_m = if spline.valley_blend_width_m.is_some() {
                                source_grid.samples_m[index]
                            } else {
                                source_height_m
                            };
                            let delta_m = (candidate_height_m - budget_source_height_m)
                                .clamp(-policy.maximum_cut_m, policy.maximum_fill_m);
                            carved_samples_m[index] = if policy.maximum_fill_m > 0.0 {
                                // Explicit river earthworks take precedence only within this
                                // corridor, including fills; legacy water remains cut-only.
                                budget_source_height_m + delta_m
                            } else {
                                carved_samples_m[index].min(budget_source_height_m + delta_m)
                            };

                            let bed_weight = if distance_m <= half_width_m {
                                1.0 - smoothstep(distance_m / half_width_m)
                            } else {
                                0.0
                            };
                            update_mask(&mut water_bed_mask_u8[index], bed_weight);
                            let bank_weight = if distance_m <= half_width_m {
                                smoothstep(((distance_m / half_width_m) - 0.45) / 0.55)
                            } else {
                                1.0 - smoothstep(
                                    (distance_m - half_width_m) / policy.shoulder_width_m,
                                )
                            };
                            update_mask(&mut bank_mask_u8[index], bank_weight);
                        }
                    }
                }

                let exclusion_weight =
                    vegetation_exclusion_weight(distance_m, half_width_m, policy.shoulder_width_m);
                update_mask(&mut vegetation_exclusion_mask_u8[index], exclusion_weight);
                if (carved_samples_m[index] - before_height_m).abs() > 0.000_1 {
                    affected_sample_count += 1;
                }
            }
        }
        carved_splines.push(WorldSketchCarvedSpline {
            feature_id: spline.id.clone(),
            kind: spline.kind,
            authored_width_m: spline.width_m,
            shoulder_width_m: policy.shoulder_width_m,
            bed_depth_m: policy.bed_depth_m,
            maximum_cut_m: policy.maximum_cut_m,
            maximum_fill_m: policy.maximum_fill_m,
            affected_sample_count,
            centerline_samples,
        });
    }

    let mut minimum_height_m = f32::INFINITY;
    let mut maximum_height_m = f32::NEG_INFINITY;
    let mut minimum_height_delta_m = f32::INFINITY;
    let mut maximum_height_delta_m = f32::NEG_INFINITY;
    let mut modified_sample_count = 0_usize;
    for (source_height_m, carved_height_m) in source_grid.samples_m.iter().zip(&carved_samples_m) {
        let delta_m = carved_height_m - source_height_m;
        minimum_height_m = minimum_height_m.min(*carved_height_m);
        maximum_height_m = maximum_height_m.max(*carved_height_m);
        minimum_height_delta_m = minimum_height_delta_m.min(delta_m);
        maximum_height_delta_m = maximum_height_delta_m.max(delta_m);
        if delta_m.abs() > 0.000_1 {
            modified_sample_count += 1;
        }
    }
    let vegetation_exclusion_sample_count = vegetation_exclusion_mask_u8
        .iter()
        .filter(|sample| **sample > 0)
        .count();

    Ok(WorldSketchSplineCarvingDocument {
        schema: "forge.world-spline-carving".to_owned(),
        schema_version: 1,
        source_world_sketch_sha256,
        source_height_grid_sha256: canonical_json_sha256(source_grid)?,
        carved_height_grid: WorldSketchHeightGrid {
            vertices_x: source_grid.vertices_x,
            vertices_z: source_grid.vertices_z,
            sample_spacing_m: source_grid.sample_spacing_m,
            origin_x_m: source_grid.origin_x_m,
            origin_z_m: source_grid.origin_z_m,
            minimum_height_m,
            maximum_height_m,
            samples_m: carved_samples_m,
        },
        road_surface_mask_u8,
        water_bed_mask_u8,
        bank_mask_u8,
        vegetation_exclusion_mask_u8,
        minimum_height_delta_m,
        maximum_height_delta_m,
        modified_sample_count,
        vegetation_exclusion_sample_count,
        splines: carved_splines,
    })
}

fn build_carved_terrain(
    source_terrain: &TerrainDocument,
    source_grid: &WorldSketchHeightGrid,
    spline_carving: &WorldSketchSplineCarvingDocument,
) -> Result<TerrainDocument, WorldSketchCompilationError> {
    let mut document = source_terrain.migrate_to_latest()?;
    "Visual Valley terrain - WorldSketch carved candidate".clone_into(&mut document.name);
    document.height_deltas.clear();
    for z in 0..source_grid.vertices_z {
        for x in 0..source_grid.vertices_x {
            let index = usize::from(z) * usize::from(source_grid.vertices_x) + usize::from(x);
            let delta_m =
                spline_carving.carved_height_grid.samples_m[index] - source_grid.samples_m[index];
            if delta_m.abs() > MATERIALIZED_HEIGHT_DELTA_EPSILON_M {
                document
                    .height_deltas
                    .push(TerrainHeightDelta::new(x, z, delta_m));
            }
        }
    }
    document.validate()?;
    Ok(document)
}

fn replace_carved_height_grid(
    spline_carving: &mut WorldSketchSplineCarvingDocument,
    source_grid: &WorldSketchHeightGrid,
    carved_height_grid: WorldSketchHeightGrid,
) {
    let mut minimum_height_delta_m = f32::INFINITY;
    let mut maximum_height_delta_m = f32::NEG_INFINITY;
    let mut modified_sample_count = 0_usize;
    for (source_height_m, carved_height_m) in source_grid
        .samples_m
        .iter()
        .zip(&carved_height_grid.samples_m)
    {
        let delta_m = carved_height_m - source_height_m;
        minimum_height_delta_m = minimum_height_delta_m.min(delta_m);
        maximum_height_delta_m = maximum_height_delta_m.max(delta_m);
        if delta_m.abs() > MATERIALIZED_HEIGHT_DELTA_EPSILON_M {
            modified_sample_count += 1;
        }
    }
    spline_carving.carved_height_grid = carved_height_grid;
    spline_carving.minimum_height_delta_m = minimum_height_delta_m;
    spline_carving.maximum_height_delta_m = maximum_height_delta_m;
    spline_carving.modified_sample_count = modified_sample_count;
}

#[allow(clippy::too_many_lines)]
fn build_traversal_audit(
    sketch: &WorldSketchDocument,
    carved_terrain: &TerrainDocument,
    player_controller: &PlayerControllerProfileDocument,
    spline_carving: &WorldSketchSplineCarvingDocument,
    source_world_sketch_sha256: ContentHash,
) -> Result<WorldSketchTraversalAuditDocument, WorldSketchCompilationError> {
    let height_grid = &spline_carving.carved_height_grid;
    let grid = WorldSketchMaskGrid {
        vertices_x: height_grid.vertices_x,
        vertices_z: height_grid.vertices_z,
        sample_spacing_m: height_grid.sample_spacing_m,
        origin_x_m: height_grid.origin_x_m,
        origin_z_m: height_grid.origin_z_m,
    };
    let sample_count = height_grid.samples_m.len();
    let maximum_walkable_slope_degrees = player_controller.controller.max_slope_climb_degrees;
    let mut maximum_observed_slope_degrees = 0.0_f32;
    let mut slope_degrees_u8 = Vec::with_capacity(sample_count);
    let mut slope_walkable_mask_u8 = Vec::with_capacity(sample_count);
    let mut water_blocked_mask_u8 = Vec::with_capacity(sample_count);
    let mut land_walkable_mask_u8 = Vec::with_capacity(sample_count);
    let slope_degrees = build_max_adjacent_triangle_slopes(height_grid);

    for z in 0..grid.vertices_z {
        for x in 0..grid.vertices_x {
            let index = mask_index(grid, x, z);
            let sample_slope_degrees = slope_degrees[index];
            maximum_observed_slope_degrees =
                maximum_observed_slope_degrees.max(sample_slope_degrees);
            let slope_walkable = sample_slope_degrees <= maximum_walkable_slope_degrees;
            let water_blocked =
                spline_carving.water_bed_mask_u8[index] >= WATER_BLOCKED_MASK_THRESHOLD;
            slope_degrees_u8.push(quantize_unit(sample_slope_degrees / 90.0));
            slope_walkable_mask_u8.push(if slope_walkable { 255 } else { 0 });
            water_blocked_mask_u8.push(if water_blocked { 255 } else { 0 });
            land_walkable_mask_u8.push(if slope_walkable && !water_blocked {
                255
            } else {
                0
            });
        }
    }

    let slope_walkable_sample_count = slope_walkable_mask_u8
        .iter()
        .filter(|sample| **sample > 0)
        .count();
    let water_blocked_sample_count = water_blocked_mask_u8
        .iter()
        .filter(|sample| **sample > 0)
        .count();
    let land_walkable_sample_count = land_walkable_mask_u8
        .iter()
        .filter(|sample| **sample > 0)
        .count();
    let (component_labels, component_sizes) =
        label_walkable_components(&land_walkable_mask_u8, grid);
    let mut bridge_candidate_mask_u8 = vec![0_u8; sample_count];
    let crossings =
        build_crossing_audits(sketch, spline_carving, grid, &mut bridge_candidate_mask_u8);
    let routes = build_route_audits(
        spline_carving,
        grid,
        &slope_walkable_mask_u8,
        &water_blocked_mask_u8,
        &land_walkable_mask_u8,
        &component_labels,
    );

    Ok(WorldSketchTraversalAuditDocument {
        schema: "forge.world-traversal-audit".to_owned(),
        schema_version: 2,
        source_world_sketch_sha256,
        source_terrain_sha256: carved_terrain.content_hash()?,
        source_player_controller_sha256: player_controller.content_hash()?,
        grid,
        maximum_walkable_slope_degrees,
        maximum_observed_slope_degrees,
        slope_degrees_u8,
        slope_walkable_mask_u8,
        water_blocked_mask_u8,
        land_walkable_mask_u8,
        bridge_candidate_mask_u8,
        slope_walkable_sample_count,
        water_blocked_sample_count,
        land_walkable_sample_count,
        connected_land_component_count: component_sizes.len(),
        largest_land_component_sample_count: component_sizes.into_iter().max().unwrap_or_default(),
        routes,
        crossings,
        navigation_provider_status: WorldSketchNavigationProviderStatus::NotSelected,
    })
}

fn build_max_adjacent_triangle_slopes(grid: &WorldSketchHeightGrid) -> Vec<f32> {
    let mut slopes = vec![0.0_f32; grid.samples_m.len()];
    for z in 0..(grid.vertices_z - 1) {
        for x in 0..(grid.vertices_x - 1) {
            let a = height_grid_index(grid, x, z);
            let b = height_grid_index(grid, x + 1, z);
            let c = height_grid_index(grid, x, z + 1);
            let d = height_grid_index(grid, x + 1, z + 1);
            let first_slope = triangle_slope_degrees(
                grid.samples_m[a],
                grid.samples_m[b],
                grid.samples_m[c],
                grid.sample_spacing_m,
            );
            let second_slope = triangle_slope_degrees(
                grid.samples_m[d],
                grid.samples_m[c],
                grid.samples_m[b],
                grid.sample_spacing_m,
            );
            for index in [a, c, b] {
                slopes[index] = slopes[index].max(first_slope);
            }
            for index in [b, c, d] {
                slopes[index] = slopes[index].max(second_slope);
            }
        }
    }
    slopes
}

fn triangle_slope_degrees(origin: f32, x_neighbor: f32, z_neighbor: f32, spacing_m: f32) -> f32 {
    let gradient_x = (x_neighbor - origin) / spacing_m;
    let gradient_z = (z_neighbor - origin) / spacing_m;
    gradient_x.hypot(gradient_z).atan().to_degrees()
}

fn height_grid_index(grid: &WorldSketchHeightGrid, x: u16, z: u16) -> usize {
    usize::from(z) * usize::from(grid.vertices_x) + usize::from(x)
}

fn mask_index(grid: WorldSketchMaskGrid, x: u16, z: u16) -> usize {
    usize::from(z) * usize::from(grid.vertices_x) + usize::from(x)
}

fn world_to_mask_index(grid: WorldSketchMaskGrid, x_m: f32, z_m: f32) -> usize {
    #[allow(clippy::cast_possible_truncation, clippy::cast_sign_loss)]
    let x =
        (((x_m - grid.origin_x_m) / grid.sample_spacing_m).round() as u16).min(grid.vertices_x - 1);
    #[allow(clippy::cast_possible_truncation, clippy::cast_sign_loss)]
    let z =
        (((z_m - grid.origin_z_m) / grid.sample_spacing_m).round() as u16).min(grid.vertices_z - 1);
    mask_index(grid, x, z)
}

fn label_walkable_components(mask: &[u8], grid: WorldSketchMaskGrid) -> (Vec<u32>, Vec<usize>) {
    let width = usize::from(grid.vertices_x);
    let height = usize::from(grid.vertices_z);
    let mut labels = vec![0_u32; mask.len()];
    let mut sizes = Vec::new();
    let mut queue = VecDeque::new();
    for start in 0..mask.len() {
        if mask[start] == 0 || labels[start] != 0 {
            continue;
        }
        let label = u32::try_from(sizes.len() + 1).expect("terrain grid component count fits u32");
        labels[start] = label;
        queue.push_back(start);
        let mut size = 0_usize;
        while let Some(index) = queue.pop_front() {
            size += 1;
            let x = index % width;
            let z = index / width;
            if x > 0 {
                enqueue_walkable(index - 1, label, mask, &mut labels, &mut queue);
            }
            if x + 1 < width {
                enqueue_walkable(index + 1, label, mask, &mut labels, &mut queue);
            }
            if z > 0 {
                enqueue_walkable(index - width, label, mask, &mut labels, &mut queue);
            }
            if z + 1 < height {
                enqueue_walkable(index + width, label, mask, &mut labels, &mut queue);
            }
        }
        sizes.push(size);
    }
    (labels, sizes)
}

fn enqueue_walkable(
    index: usize,
    label: u32,
    mask: &[u8],
    labels: &mut [u32],
    queue: &mut VecDeque<usize>,
) {
    if mask[index] > 0 && labels[index] == 0 {
        labels[index] = label;
        queue.push_back(index);
    }
}

fn build_route_audits(
    spline_carving: &WorldSketchSplineCarvingDocument,
    grid: WorldSketchMaskGrid,
    slope_walkable_mask_u8: &[u8],
    water_blocked_mask_u8: &[u8],
    land_walkable_mask_u8: &[u8],
    component_labels: &[u32],
) -> Vec<WorldSketchRouteTraversalAudit> {
    spline_carving
        .splines
        .iter()
        .filter(|spline| spline.kind == WorldSketchSplineKind::Road)
        .map(|spline| {
            let mut indices = spline
                .centerline_samples
                .iter()
                .map(|sample| world_to_mask_index(grid, sample.x_m, sample.z_m))
                .collect::<Vec<_>>();
            indices.dedup();
            let first = indices[0];
            let last = indices[indices.len() - 1];
            let endpoints_land_walkable =
                land_walkable_mask_u8[first] > 0 && land_walkable_mask_u8[last] > 0;
            WorldSketchRouteTraversalAudit {
                feature_id: spline.feature_id.clone(),
                centerline_sample_count: indices.len(),
                slope_walkable_sample_count: indices
                    .iter()
                    .filter(|index| slope_walkable_mask_u8[**index] > 0)
                    .count(),
                water_blocked_sample_count: indices
                    .iter()
                    .filter(|index| water_blocked_mask_u8[**index] > 0)
                    .count(),
                land_walkable_sample_count: indices
                    .iter()
                    .filter(|index| land_walkable_mask_u8[**index] > 0)
                    .count(),
                centerline_continuous_without_structures: indices
                    .iter()
                    .all(|index| land_walkable_mask_u8[*index] > 0),
                endpoints_land_walkable,
                endpoints_connected_without_structures: endpoints_land_walkable
                    && component_labels[first] != 0
                    && component_labels[first] == component_labels[last],
            }
        })
        .collect()
}

#[allow(clippy::too_many_lines)]
fn build_crossing_audits(
    sketch: &WorldSketchDocument,
    spline_carving: &WorldSketchSplineCarvingDocument,
    grid: WorldSketchMaskGrid,
    bridge_candidate_mask_u8: &mut [u8],
) -> Vec<WorldSketchCrossingTraversalAudit> {
    let roads = spline_carving
        .splines
        .iter()
        .filter(|spline| spline.kind == WorldSketchSplineKind::Road)
        .collect::<Vec<_>>();
    let waters = spline_carving
        .splines
        .iter()
        .filter(|spline| {
            matches!(
                spline.kind,
                WorldSketchSplineKind::River | WorldSketchSplineKind::Creek
            )
        })
        .collect::<Vec<_>>();
    let mut crossings = Vec::new();
    for road in roads {
        for water in &waters {
            let mut overlap_sample_count = 0_usize;
            let mut center_sum_m = [0.0_f32; 2];
            for z in 0..grid.vertices_z {
                let z_m = grid.origin_z_m + f32::from(z) * grid.sample_spacing_m;
                for x in 0..grid.vertices_x {
                    let x_m = grid.origin_x_m + f32::from(x) * grid.sample_spacing_m;
                    let road_distance_m =
                        nearest_centerline_values(x_m, z_m, &road.centerline_samples).0;
                    let water_distance_m =
                        nearest_centerline_values(x_m, z_m, &water.centerline_samples).0;
                    if road_distance_m <= road.authored_width_m * 0.5
                        && water_distance_m <= water.authored_width_m * 0.5
                    {
                        overlap_sample_count += 1;
                        center_sum_m[0] += x_m;
                        center_sum_m[1] += z_m;
                    }
                }
            }
            if overlap_sample_count == 0 {
                continue;
            }
            #[allow(clippy::cast_precision_loss)]
            let center_m = [
                center_sum_m[0] / overlap_sample_count as f32,
                center_sum_m[1] / overlap_sample_count as f32,
            ];
            let bridge = sketch
                .landmarks
                .iter()
                .filter(|landmark| landmark.kind == WorldSketchLandmarkKind::Bridge)
                .filter(|landmark| {
                    (landmark.position.x_m - center_m[0]).hypot(landmark.position.z_m - center_m[1])
                        <= landmark.influence_radius_m
                })
                .min_by(|left, right| {
                    let left_distance =
                        (left.position.x_m - center_m[0]).hypot(left.position.z_m - center_m[1]);
                    let right_distance =
                        (right.position.x_m - center_m[0]).hypot(right.position.z_m - center_m[1]);
                    left_distance.total_cmp(&right_distance)
                });
            if let Some(bridge) = bridge {
                for z in 0..grid.vertices_z {
                    let z_m = grid.origin_z_m + f32::from(z) * grid.sample_spacing_m;
                    for x in 0..grid.vertices_x {
                        let x_m = grid.origin_x_m + f32::from(x) * grid.sample_spacing_m;
                        let index = mask_index(grid, x, z);
                        let inside_bridge = (x_m - bridge.position.x_m)
                            .hypot(z_m - bridge.position.z_m)
                            <= bridge.influence_radius_m;
                        if inside_bridge
                            && spline_carving.road_surface_mask_u8[index] > 0
                            && (spline_carving.water_bed_mask_u8[index] > 0
                                || spline_carving.bank_mask_u8[index] > 0)
                        {
                            bridge_candidate_mask_u8[index] = 255;
                        }
                    }
                }
            }
            crossings.push(WorldSketchCrossingTraversalAudit {
                road_feature_id: road.feature_id.clone(),
                water_feature_id: water.feature_id.clone(),
                bridge_landmark_id: bridge.map(|landmark| landmark.id.clone()),
                center_m,
                overlap_sample_count,
                runtime_status: if bridge.is_some() {
                    WorldSketchCrossingRuntimeStatus::BridgeRuntimeProbeRequired
                } else {
                    WorldSketchCrossingRuntimeStatus::MissingBridgeLandmark
                },
            });
        }
    }
    crossings
}

fn spline_precedence(kind: WorldSketchSplineKind) -> u8 {
    match kind {
        WorldSketchSplineKind::Road => 0,
        WorldSketchSplineKind::Creek => 1,
        WorldSketchSplineKind::River => 2,
    }
}

fn build_carving_centerline(
    spline: &WorldSketchSpline,
    source_grid: &WorldSketchHeightGrid,
    policy: SplineCarvingPolicy,
) -> Vec<WorldSketchCarvingCenterlineSample> {
    let sampled_points = sample_catmull_rom(&spline.points, source_grid);
    if let Some(heights) = &spline.surface_heights_m {
        return authored_carving_centerline(&sampled_points, heights, policy.bed_depth_m);
    }
    let raw_heights = sampled_points
        .iter()
        .map(|point| sample_height_grid(source_grid, point.x_m, point.z_m))
        .collect::<Vec<_>>();
    let smoothed_heights = raw_heights
        .iter()
        .enumerate()
        .map(|(index, height)| {
            let previous = raw_heights[index.saturating_sub(1)];
            let next = raw_heights[(index + 1).min(raw_heights.len() - 1)];
            (previous + *height * 2.0 + next) * 0.25
        })
        .collect::<Vec<_>>();

    let mut output = Vec::with_capacity(sampled_points.len());
    let mut previous_surface_height_m = f32::INFINITY;
    for (index, (point, smoothed_height_m)) in
        sampled_points.iter().zip(smoothed_heights).enumerate()
    {
        let mut surface_height_m = if spline.kind == WorldSketchSplineKind::Road {
            smoothed_height_m - 0.08
        } else {
            smoothed_height_m - 0.22
        };
        if spline.kind != WorldSketchSplineKind::Road && index > 0 {
            let previous_point = sampled_points[index - 1];
            let minimum_drop_m =
                (point.x_m - previous_point.x_m).hypot(point.z_m - previous_point.z_m) * 0.002;
            surface_height_m = surface_height_m.min(previous_surface_height_m - minimum_drop_m);
        }
        previous_surface_height_m = surface_height_m;
        output.push(WorldSketchCarvingCenterlineSample {
            x_m: point.x_m,
            z_m: point.z_m,
            surface_height_m,
            terrain_target_height_m: surface_height_m - policy.bed_depth_m,
        });
    }
    output
}

#[allow(clippy::cast_precision_loss)]
fn authored_carving_centerline(
    points: &[WorldSketchPoint2],
    heights: &[f32],
    bed_depth_m: f32,
) -> Vec<WorldSketchCarvingCenterlineSample> {
    points
        .iter()
        .enumerate()
        .map(|(index, point)| {
            let segment = (index / SPLINE_SAMPLES_PER_SEGMENT).min(heights.len() - 2);
            let fraction = (index - segment * SPLINE_SAMPLES_PER_SEGMENT) as f32
                / SPLINE_SAMPLES_PER_SEGMENT as f32;
            let surface_height_m = lerp(heights[segment], heights[segment + 1], fraction);
            WorldSketchCarvingCenterlineSample {
                x_m: point.x_m,
                z_m: point.z_m,
                surface_height_m,
                terrain_target_height_m: surface_height_m - bed_depth_m,
            }
        })
        .collect()
}

#[allow(clippy::cast_precision_loss)]
fn sample_catmull_rom(
    points: &[WorldSketchPoint2],
    source_grid: &WorldSketchHeightGrid,
) -> Vec<WorldSketchPoint2> {
    let mut output = Vec::with_capacity((points.len() - 1) * SPLINE_SAMPLES_PER_SEGMENT + 1);
    let maximum_m = [
        source_grid.origin_x_m
            + f32::from(source_grid.vertices_x - 1) * source_grid.sample_spacing_m,
        source_grid.origin_z_m
            + f32::from(source_grid.vertices_z - 1) * source_grid.sample_spacing_m,
    ];
    for segment in 0..(points.len() - 1) {
        let p0 = points[segment.saturating_sub(1)];
        let p1 = points[segment];
        let p2 = points[segment + 1];
        let p3 = points[(segment + 2).min(points.len() - 1)];
        for sample in 0..SPLINE_SAMPLES_PER_SEGMENT {
            let t = sample as f32 / SPLINE_SAMPLES_PER_SEGMENT as f32;
            let t2 = t * t;
            let t3 = t2 * t;
            let evaluate = |a: f32, b: f32, c: f32, d: f32| {
                0.5 * (2.0 * b
                    + (-a + c) * t
                    + (2.0 * a - 5.0 * b + 4.0 * c - d) * t2
                    + (-a + 3.0 * b - 3.0 * c + d) * t3)
            };
            output.push(WorldSketchPoint2 {
                x_m: evaluate(p0.x_m, p1.x_m, p2.x_m, p3.x_m)
                    .clamp(source_grid.origin_x_m, maximum_m[0]),
                z_m: evaluate(p0.z_m, p1.z_m, p2.z_m, p3.z_m)
                    .clamp(source_grid.origin_z_m, maximum_m[1]),
            });
        }
    }
    output.push(points[points.len() - 1]);
    output
}

fn sample_height_grid(grid: &WorldSketchHeightGrid, x_m: f32, z_m: f32) -> f32 {
    let grid_x = ((x_m - grid.origin_x_m) / grid.sample_spacing_m)
        .clamp(0.0, f32::from(grid.vertices_x - 1));
    let grid_z = ((z_m - grid.origin_z_m) / grid.sample_spacing_m)
        .clamp(0.0, f32::from(grid.vertices_z - 1));
    #[allow(clippy::cast_possible_truncation, clippy::cast_sign_loss)]
    let x0 = (grid_x.floor() as u16).min(grid.vertices_x - 2);
    #[allow(clippy::cast_possible_truncation, clippy::cast_sign_loss)]
    let z0 = (grid_z.floor() as u16).min(grid.vertices_z - 2);
    let x_fraction = grid_x - f32::from(x0);
    let z_fraction = grid_z - f32::from(z0);
    let x0 = usize::from(x0);
    let z0 = usize::from(z0);
    let width = usize::from(grid.vertices_x);
    let h00 = grid.samples_m[z0 * width + x0];
    let h10 = grid.samples_m[z0 * width + x0 + 1];
    let h01 = grid.samples_m[(z0 + 1) * width + x0];
    let h11 = grid.samples_m[(z0 + 1) * width + x0 + 1];
    lerp(
        lerp(h00, h10, x_fraction),
        lerp(h01, h11, x_fraction),
        z_fraction,
    )
}

fn nearest_centerline_values(
    x_m: f32,
    z_m: f32,
    samples: &[WorldSketchCarvingCenterlineSample],
) -> (f32, f32, f32) {
    let mut best_distance_squared = f32::INFINITY;
    let mut best_surface_height_m = samples[0].surface_height_m;
    let mut best_target_height_m = samples[0].terrain_target_height_m;
    for segment in samples.windows(2) {
        let start = segment[0];
        let end = segment[1];
        let segment_x = end.x_m - start.x_m;
        let segment_z = end.z_m - start.z_m;
        let length_squared = segment_x * segment_x + segment_z * segment_z;
        let fraction = if length_squared > 0.0 {
            ((x_m - start.x_m) * segment_x + (z_m - start.z_m) * segment_z) / length_squared
        } else {
            0.0
        }
        .clamp(0.0, 1.0);
        let nearest_x = start.x_m + segment_x * fraction;
        let nearest_z = start.z_m + segment_z * fraction;
        let distance_squared = (x_m - nearest_x).powi(2) + (z_m - nearest_z).powi(2);
        if distance_squared < best_distance_squared {
            best_distance_squared = distance_squared;
            best_surface_height_m = lerp(start.surface_height_m, end.surface_height_m, fraction);
            best_target_height_m = lerp(
                start.terrain_target_height_m,
                end.terrain_target_height_m,
                fraction,
            );
        }
    }
    (
        best_distance_squared.sqrt(),
        best_surface_height_m,
        best_target_height_m,
    )
}

fn shoulder_weight(distance_m: f32, half_width_m: f32, shoulder_width_m: f32) -> f32 {
    if distance_m <= half_width_m {
        1.0
    } else if distance_m < half_width_m + shoulder_width_m {
        1.0 - smoothstep((distance_m - half_width_m) / shoulder_width_m)
    } else {
        0.0
    }
}

fn vegetation_exclusion_weight(distance_m: f32, half_width_m: f32, shoulder_width_m: f32) -> f32 {
    let solid_edge_m = half_width_m + shoulder_width_m * 0.55;
    let outer_edge_m = half_width_m + shoulder_width_m;
    if distance_m <= solid_edge_m {
        1.0
    } else if distance_m < outer_edge_m {
        1.0 - smoothstep((distance_m - solid_edge_m) / (outer_edge_m - solid_edge_m))
    } else {
        0.0
    }
}

fn smoothstep(value: f32) -> f32 {
    let value = value.clamp(0.0, 1.0);
    value * value * (3.0 - 2.0 * value)
}

fn lerp(start: f32, end: f32, fraction: f32) -> f32 {
    start + (end - start) * fraction
}

#[allow(clippy::cast_possible_truncation, clippy::cast_sign_loss)]
fn quantize_unit(value: f32) -> u8 {
    (value.clamp(0.0, 1.0) * 255.0).round() as u8
}

fn update_mask(sample: &mut u8, weight: f32) {
    *sample = (*sample).max(quantize_unit(weight));
}

fn build_bridge_placements(
    sketch: &WorldSketchDocument,
    carved_terrain: &TerrainDocument,
    spline_carving: &WorldSketchSplineCarvingDocument,
    traversal_audit: &WorldSketchTraversalAuditDocument,
    source_world_sketch_sha256: ContentHash,
) -> Result<WorldSketchBridgePlacementDocument, WorldSketchCompilationError> {
    let mut placements = Vec::new();
    for crossing in &traversal_audit.crossings {
        let Some(landmark_id) = crossing.bridge_landmark_id.as_deref() else {
            continue;
        };
        let landmark = sketch
            .landmarks
            .iter()
            .find(|candidate| candidate.id == landmark_id)
            .ok_or_else(|| WorldSketchCompilationError::MissingBridgeLandmark {
                landmark_id: landmark_id.to_owned(),
            })?;
        let road = spline_carving
            .splines
            .iter()
            .find(|spline| spline.feature_id == crossing.road_feature_id)
            .ok_or_else(|| WorldSketchCompilationError::MissingBridgeRoad {
                landmark_id: landmark_id.to_owned(),
                road_feature_id: crossing.road_feature_id.clone(),
            })?;
        let water = spline_carving
            .splines
            .iter()
            .find(|spline| spline.feature_id == crossing.water_feature_id)
            .ok_or_else(|| WorldSketchCompilationError::MissingBridgeWater {
                landmark_id: landmark_id.to_owned(),
                water_feature_id: crossing.water_feature_id.clone(),
            })?;
        placements.push(build_bridge_placement(
            crossing,
            landmark,
            road,
            water,
            &spline_carving.carved_height_grid,
            traversal_audit.maximum_walkable_slope_degrees,
        )?);
    }

    let document = WorldSketchBridgePlacementDocument {
        schema: "forge.world-bridge-placements".to_owned(),
        schema_version: 1,
        source_world_sketch_sha256,
        source_carved_terrain_sha256: carved_terrain.content_hash()?,
        source_traversal_audit_sha256: traversal_audit.content_hash()?,
        placements,
    };
    document.validate()?;
    Ok(document)
}

fn build_bridge_placement(
    crossing: &WorldSketchCrossingTraversalAudit,
    landmark: &WorldSketchLandmark,
    road: &WorldSketchCarvedSpline,
    water: &WorldSketchCarvedSpline,
    grid: &WorldSketchHeightGrid,
    maximum_walkable_slope_degrees: f32,
) -> Result<WorldSketchBridgePlacement, WorldSketchCompilationError> {
    let landmark_id = crossing.bridge_landmark_id.as_deref().unwrap_or_default();
    let road_direction = bridge_road_direction(road, crossing.center_m).ok_or_else(|| {
        WorldSketchCompilationError::DegenerateBridgeRoad {
            landmark_id: landmark_id.to_owned(),
            road_feature_id: crossing.road_feature_id.clone(),
        }
    })?;
    let water_direction = bridge_road_direction(water, crossing.center_m).ok_or_else(|| {
        WorldSketchCompilationError::DegenerateBridgeWater {
            landmark_id: landmark_id.to_owned(),
            water_feature_id: crossing.water_feature_id.clone(),
        }
    })?;
    let half_length_m = select_bridge_half_length(
        grid,
        crossing.center_m,
        road_direction,
        landmark.influence_radius_m,
        maximum_walkable_slope_degrees,
    )
    .ok_or_else(
        || WorldSketchCompilationError::BridgeLandingExceedsPlayerContract {
            landmark_id: landmark_id.to_owned(),
            maximum_degrees: maximum_walkable_slope_degrees,
        },
    )?;
    let near_landing_xz_m = [
        crossing.center_m[0] - road_direction[0] * half_length_m,
        crossing.center_m[1] - road_direction[1] * half_length_m,
    ];
    let far_landing_xz_m = [
        crossing.center_m[0] + road_direction[0] * half_length_m,
        crossing.center_m[1] + road_direction[1] * half_length_m,
    ];
    let near_surface_height_m =
        sample_height_grid(grid, near_landing_xz_m[0], near_landing_xz_m[1])
            + BRIDGE_LANDING_CLEARANCE_M;
    let far_surface_height_m = sample_height_grid(grid, far_landing_xz_m[0], far_landing_xz_m[1])
        + BRIDGE_LANDING_CLEARANCE_M;
    let horizontal_length_m = (far_landing_xz_m[0] - near_landing_xz_m[0])
        .hypot(far_landing_xz_m[1] - near_landing_xz_m[1]);
    let slope_degrees = (far_surface_height_m - near_surface_height_m)
        .atan2(horizontal_length_m)
        .to_degrees()
        .abs();
    if slope_degrees > maximum_walkable_slope_degrees {
        return Err(
            WorldSketchCompilationError::BridgeSlopeExceedsPlayerContract {
                landmark_id: landmark_id.to_owned(),
                slope_degrees,
                maximum_degrees: maximum_walkable_slope_degrees,
            },
        );
    }
    let supports = build_bridge_supports(road, grid, near_landing_xz_m, far_landing_xz_m);
    // Validate the unrounded slope above. Only the persisted diagnostic is quantized:
    // native atan2f and WASM may differ by one ULP for identical landing positions.
    let slope_degrees = canonical_bridge_slope(slope_degrees);
    Ok(WorldSketchBridgePlacement {
        landmark_id: landmark_id.to_owned(),
        road_feature_id: crossing.road_feature_id.clone(),
        water_feature_id: crossing.water_feature_id.clone(),
        crossing_center_m: crossing.center_m,
        water_direction_xz: water_direction,
        water_width_m: water.authored_width_m,
        crossing_bed_height_m: sample_height_grid(grid, crossing.center_m[0], crossing.center_m[1]),
        deck_start_surface_m: [
            near_landing_xz_m[0],
            near_surface_height_m,
            near_landing_xz_m[1],
        ],
        deck_end_surface_m: [
            far_landing_xz_m[0],
            far_surface_height_m,
            far_landing_xz_m[1],
        ],
        deck_width_m: road.authored_width_m + BRIDGE_ROAD_MARGIN_M,
        deck_thickness_m: BRIDGE_DECK_THICKNESS_M,
        slope_degrees,
        parapet_height_m: BRIDGE_PARAPET_HEIGHT_M,
        parapet_thickness_m: BRIDGE_PARAPET_THICKNESS_M,
        collision_shape: WorldSketchBridgeCollisionShape::OrientedBox,
        supports,
    })
}

fn canonical_bridge_slope(degrees: f32) -> f32 {
    (degrees * 10_000.0).round() / 10_000.0
}

fn build_bridge_supports(
    road: &WorldSketchCarvedSpline,
    grid: &WorldSketchHeightGrid,
    near_landing_xz_m: [f32; 2],
    far_landing_xz_m: [f32; 2],
) -> Vec<WorldSketchBridgeSupport> {
    [1.0_f32 / 3.0, 2.0_f32 / 3.0]
        .into_iter()
        .map(|center_fraction| {
            let support_xz_m = [
                lerp(near_landing_xz_m[0], far_landing_xz_m[0], center_fraction),
                lerp(near_landing_xz_m[1], far_landing_xz_m[1], center_fraction),
            ];
            WorldSketchBridgeSupport {
                center_fraction,
                ground_height_m: sample_height_grid(grid, support_xz_m[0], support_xz_m[1]),
                width_along_bridge_m: 2.4,
                depth_across_bridge_m: road.authored_width_m + BRIDGE_ROAD_MARGIN_M - 1.0,
            }
        })
        .collect()
}

fn bridge_road_direction(
    road: &WorldSketchCarvedSpline,
    crossing_center_m: [f32; 2],
) -> Option<[f32; 2]> {
    let closest = road
        .centerline_samples
        .iter()
        .enumerate()
        .min_by(|(_, left), (_, right)| {
            let left_distance =
                (left.x_m - crossing_center_m[0]).hypot(left.z_m - crossing_center_m[1]);
            let right_distance =
                (right.x_m - crossing_center_m[0]).hypot(right.z_m - crossing_center_m[1]);
            left_distance.total_cmp(&right_distance)
        })?
        .0;
    let previous = closest.saturating_sub(1);
    let next = (closest + 1).min(road.centerline_samples.len().saturating_sub(1));
    let start = road.centerline_samples.get(previous)?;
    let end = road.centerline_samples.get(next)?;
    let x = end.x_m - start.x_m;
    let z = end.z_m - start.z_m;
    let length = x.hypot(z);
    (length > f32::EPSILON).then_some([x / length, z / length])
}

#[allow(
    clippy::cast_possible_truncation,
    clippy::cast_sign_loss,
    clippy::cast_precision_loss
)]
fn select_bridge_half_length(
    grid: &WorldSketchHeightGrid,
    center_m: [f32; 2],
    direction: [f32; 2],
    influence_radius_m: f32,
    maximum_walkable_slope_degrees: f32,
) -> Option<f32> {
    let candidate_count = (BRIDGE_MAX_LANDING_EXTENSION_M / BRIDGE_LANDING_EXTENSION_STEP_M) as u32;
    (1..=candidate_count)
        .map(|step| influence_radius_m + step as f32 * BRIDGE_LANDING_EXTENSION_STEP_M)
        .find(|half_length_m| {
            bridge_longitudinal_clearance_is_valid(grid, center_m, direction, *half_length_m)
                && bridge_landings_are_walkable(
                    grid,
                    center_m,
                    direction,
                    *half_length_m,
                    maximum_walkable_slope_degrees,
                )
        })
}

#[allow(
    clippy::cast_possible_truncation,
    clippy::cast_sign_loss,
    clippy::cast_precision_loss
)]
fn bridge_longitudinal_clearance_is_valid(
    grid: &WorldSketchHeightGrid,
    center_m: [f32; 2],
    direction: [f32; 2],
    half_length_m: f32,
) -> bool {
    let near_landing_xz_m = [
        center_m[0] - direction[0] * half_length_m,
        center_m[1] - direction[1] * half_length_m,
    ];
    let far_landing_xz_m = [
        center_m[0] + direction[0] * half_length_m,
        center_m[1] + direction[1] * half_length_m,
    ];
    let near_surface_height_m =
        sample_height_grid(grid, near_landing_xz_m[0], near_landing_xz_m[1])
            + BRIDGE_LANDING_CLEARANCE_M;
    let far_surface_height_m = sample_height_grid(grid, far_landing_xz_m[0], far_landing_xz_m[1])
        + BRIDGE_LANDING_CLEARANCE_M;
    let sample_count = ((half_length_m * 2.0) / BRIDGE_LANDING_AUDIT_STEP_M).ceil() as u32;
    (0..=sample_count).all(|step| {
        let fraction = step as f32 / sample_count as f32;
        let sample_xz_m = [
            lerp(near_landing_xz_m[0], far_landing_xz_m[0], fraction),
            lerp(near_landing_xz_m[1], far_landing_xz_m[1], fraction),
        ];
        let deck_height_m = lerp(near_surface_height_m, far_surface_height_m, fraction);
        deck_height_m - sample_height_grid(grid, sample_xz_m[0], sample_xz_m[1])
            >= BRIDGE_MIN_LONGITUDINAL_CLEARANCE_M
    })
}

#[allow(
    clippy::cast_possible_truncation,
    clippy::cast_sign_loss,
    clippy::cast_precision_loss
)]
fn bridge_landings_are_walkable(
    grid: &WorldSketchHeightGrid,
    center_m: [f32; 2],
    direction: [f32; 2],
    half_length_m: f32,
    maximum_walkable_slope_degrees: f32,
) -> bool {
    [-1.0_f32, 1.0].into_iter().all(|side| {
        let mut previous_height_m = sample_height_grid(
            grid,
            center_m[0] + direction[0] * half_length_m * side,
            center_m[1] + direction[1] * half_length_m * side,
        );
        let step_count = (BRIDGE_LANDING_AUDIT_DISTANCE_M / BRIDGE_LANDING_AUDIT_STEP_M) as u32;
        (1..=step_count).all(|step| {
            let distance_m = half_length_m + step as f32 * BRIDGE_LANDING_AUDIT_STEP_M;
            let height_m = sample_height_grid(
                grid,
                center_m[0] + direction[0] * distance_m * side,
                center_m[1] + direction[1] * distance_m * side,
            );
            let slope_degrees = (height_m - previous_height_m)
                .abs()
                .atan2(BRIDGE_LANDING_AUDIT_STEP_M)
                .to_degrees();
            previous_height_m = height_m;
            slope_degrees <= maximum_walkable_slope_degrees
        })
    })
}

fn build_blender_input(
    sketch: &WorldSketchDocument,
    height_grid: WorldSketchHeightGrid,
    semantic_masks: WorldSketchSemanticMaskDocument,
    spline_carving: WorldSketchSplineCarvingDocument,
    traversal_audit: WorldSketchTraversalAuditDocument,
    bridge_placements: WorldSketchBridgePlacementDocument,
) -> Result<WorldSketchBlenderInput, WorldSketchCompilationError> {
    Ok(WorldSketchBlenderInput {
        schema: "forge.world-sketch-blender-input".to_owned(),
        schema_version: 4,
        source_world_sketch_sha256: sketch.content_hash()?,
        extent_m: sketch.extent_m,
        reference_images: sketch.source_images.clone(),
        height_grid,
        semantic_masks,
        spline_carving,
        traversal_audit,
        bridge_placements,
        semantic_regions: sketch.semantic_regions.clone(),
        splines: sketch.splines.clone(),
        landmarks: sketch.landmarks.clone(),
        uncertainty_regions: sketch.uncertainty_regions.clone(),
        locked_feature_ids: sketch.locked_feature_ids.clone(),
        validation_cameras: sketch.validation_cameras.clone(),
    })
}

pub fn embedded_visual_valley_world_sketch() -> Result<WorldSketchDocument, WorldSketchError> {
    WorldSketchDocument::from_json(VISUAL_VALLEY_WORLD_SKETCH_JSON)
}

fn default_material_layers() -> Vec<TerrainMaterialLayer> {
    vec![
        TerrainMaterialLayer::new("meadow_grass", "Meadow grass"),
        TerrainMaterialLayer::new("valley_earth", "Valley earth"),
        TerrainMaterialLayer::new("ridge_rock", "Ridge rock"),
    ]
}

fn validate_unique_feature_id<'a>(
    ids: &mut HashSet<&'a str>,
    id: &'a str,
) -> Result<(), WorldSketchError> {
    validate_logical_id("feature", id)?;
    if !ids.insert(id) {
        return Err(WorldSketchError::DuplicateFeatureId(id.to_owned()));
    }
    Ok(())
}

fn validate_confidence(feature_id: &str, confidence: f32) -> Result<(), WorldSketchError> {
    if !unit_interval(confidence) {
        return Err(WorldSketchError::InvalidConfidence(feature_id.to_owned()));
    }
    Ok(())
}

fn validate_name(field: &'static str, value: &str) -> Result<(), WorldSketchError> {
    let trimmed = value.trim();
    if trimmed.is_empty() || trimmed.len() > 256 {
        return Err(WorldSketchError::InvalidTextField(field));
    }
    Ok(())
}

fn validate_logical_id(field: &'static str, value: &str) -> Result<(), WorldSketchError> {
    let valid = !value.is_empty()
        && value.len() <= 64
        && value
            .bytes()
            .next()
            .is_some_and(|byte| byte.is_ascii_lowercase())
        && value
            .bytes()
            .all(|byte| byte.is_ascii_lowercase() || byte.is_ascii_digit() || byte == b'_');
    if !valid {
        return Err(WorldSketchError::InvalidLogicalId {
            field,
            value: value.to_owned(),
        });
    }
    Ok(())
}

fn validate_relative_path(field: &'static str, value: &str) -> Result<(), WorldSketchError> {
    let invalid = value.is_empty()
        || value.len() > 512
        || value.starts_with('/')
        || value.contains('\\')
        || value.contains(':')
        || value
            .split('/')
            .any(|component| component.is_empty() || component == "." || component == "..");
    if invalid {
        return Err(WorldSketchError::InvalidRelativePath {
            field,
            value: value.to_owned(),
        });
    }
    Ok(())
}

fn positive_finite(value: f32) -> bool {
    value.is_finite() && value > 0.0
}

fn unit_interval(value: f32) -> bool {
    value.is_finite() && (0.0..=1.0).contains(&value)
}

#[derive(Debug, Error)]
pub enum WorldSketchError {
    #[error("invalid world-sketch schema `{0}`")]
    InvalidSchema(String),
    #[error("unsupported world-sketch schema version {0}")]
    UnsupportedVersion(u32),
    #[error("world-sketch coordinate system must be metres, Y-up, right-handed")]
    UnsupportedCoordinateSystem,
    #[error("world-sketch text field `{0}` must contain between 1 and 256 bytes")]
    InvalidTextField(&'static str),
    #[error("world-sketch extent `{0}` must be finite, positive and bounded")]
    InvalidExtent(&'static str),
    #[error("world-sketch must contain between 1 and {MAX_SOURCE_IMAGES} source images, found {0}")]
    InvalidSourceCount(usize),
    #[error("world-sketch must contain at least one top-down source image")]
    MissingTopDownSource,
    #[error("duplicate world-sketch source id `{0}`")]
    DuplicateSourceId(String),
    #[error("source image `{0}` must have non-zero pixel dimensions")]
    InvalidSourceDimensions(String),
    #[error("invalid {field} logical id `{value}`")]
    InvalidLogicalId { field: &'static str, value: String },
    #[error("invalid relative {field} path `{value}`")]
    InvalidRelativePath { field: &'static str, value: String },
    #[error("source `{source_id}` has an empty provenance field `{field}`")]
    EmptyProvenanceField {
        source_id: String,
        field: &'static str,
    },
    #[error("world-sketch calibration is invalid")]
    InvalidCalibration,
    #[error("world-sketch calibration references unknown source `{0}`")]
    UnknownCalibrationSource(String),
    #[error("world-sketch calibration source `{0}` is not a top-down plan")]
    InvalidCalibrationSourceRole(String),
    #[error("world-sketch calibration origin lies outside the top-down source image")]
    CalibrationOriginOutsideImage,
    #[error("world-sketch terrain intent is invalid: {0}")]
    InvalidTerrainIntent(#[source] TerrainDocumentError),
    #[error(
        "world-sketch forest scatter contains invalid probability, spacing, scale or attempt budget"
    )]
    InvalidForestScatter,
    #[error("world-sketch collection `{collection}` has {count} entries, maximum {maximum}")]
    CollectionLimitExceeded {
        collection: &'static str,
        count: usize,
        maximum: usize,
    },
    #[error("duplicate world-sketch feature id `{0}`")]
    DuplicateFeatureId(String),
    #[error("feature `{feature_id}` polygon has {count} points; expected 3..={MAX_POLYGON_POINTS}")]
    InvalidPolygonPointCount { feature_id: String, count: usize },
    #[error("feature `{feature_id}` spline has {count} points; expected 2..={MAX_SPLINE_POINTS}")]
    InvalidSplinePointCount { feature_id: String, count: usize },
    #[error("world-sketch point ({x_m}, {z_m}) lies outside the declared extent")]
    PointOutsideExtent { x_m: f32, z_m: f32 },
    #[error("feature `{0}` repeats an adjacent point")]
    RepeatedAdjacentPoint(String),
    #[error("feature `{0}` has invalid confidence; expected a finite value in [0, 1]")]
    InvalidConfidence(String),
    #[error("spline `{0}` has invalid width")]
    InvalidSplineWidth(String),
    #[error("spline `{0}` maximum_cut_m must be finite and in (0, 64]")]
    InvalidSplineCutBudget(String),
    #[error(
        "spline `{0}` requires one finite Y altitude in [-1024, 1024] per point, non-increasing downstream, for water only"
    )]
    InvalidSplineSurfaceProfile(String),
    #[error(
        "spline `{0}` maximum_fill_m must be finite and in [0, 64], with an explicit water surface profile"
    )]
    InvalidSplineFillBudget(String),
    #[error(
        "spline `{0}` valley_blend_width_m must be in [16, 192] with explicit surface, cut and fill budgets"
    )]
    InvalidValleyBlendWidth(String),
    #[error("landmark `{0}` has invalid influence radius")]
    InvalidLandmarkRadius(String),
    #[error("locked feature `{0}` does not exist")]
    UnknownLockedFeature(String),
    #[error("locked_feature_ids must be unique and strictly sorted")]
    LocksNotStrictlySorted,
    #[error(
        "world-sketch requires between 1 and {MAX_VALIDATION_CAMERAS} validation cameras, found {0}"
    )]
    InvalidCameraCount(usize),
    #[error("duplicate validation camera id `{0}`")]
    DuplicateCameraId(String),
    #[error("validation camera `{0}` is invalid")]
    InvalidCamera(String),
    #[error("camera `{camera_id}` references unknown source image `{source_image_id}`")]
    UnknownCameraSource {
        camera_id: String,
        source_image_id: String,
    },
    #[error("camera `{camera_id}` source `{source_image_id}` is not a perspective concept")]
    InvalidCameraSourceRole {
        camera_id: String,
        source_image_id: String,
    },
    #[error("duplicate camera source role mapping")]
    DuplicateCameraSourceRole,
    #[error("invalid world-sketch JSON: {0}")]
    Json(#[from] serde_json::Error),
    #[error("cannot hash world-sketch: {0}")]
    Hash(#[from] CanonicalJsonError),
}

#[derive(Debug, Error)]
pub enum WorldSketchCompilationError {
    #[error("WorldSketch playtest requires exactly one player-spawn landmark, found {actual}")]
    PlayerSpawnCount { actual: usize },
    #[error("player-spawn terrain must match the WorldSketch project, area and revision")]
    PlayerSpawnTerrainIdentityMismatch,
    #[error(transparent)]
    InvalidWorldSketch(#[from] WorldSketchError),
    #[error("the first Visual Valley compiler supports exactly a 512 by 512 metre extent")]
    UnsupportedVisualValleyExtent,
    #[error("compiled terrain is invalid: {0}")]
    Terrain(#[from] TerrainDocumentError),
    #[error("compiled vegetation is invalid: {0}")]
    Vegetation(#[from] VegetationDocumentError),
    #[error("compiled traversal audit cannot use the player controller profile: {0}")]
    PlayerController(#[from] PlayerControllerProfileError),
    #[error("bridge placement references missing landmark `{landmark_id}`")]
    MissingBridgeLandmark { landmark_id: String },
    #[error("bridge `{landmark_id}` references missing road `{road_feature_id}`")]
    MissingBridgeRoad {
        landmark_id: String,
        road_feature_id: String,
    },
    #[error("bridge `{landmark_id}` references missing water spline `{water_feature_id}`")]
    MissingBridgeWater {
        landmark_id: String,
        water_feature_id: String,
    },
    #[error("bridge `{landmark_id}` cannot derive a direction from road `{road_feature_id}`")]
    DegenerateBridgeRoad {
        landmark_id: String,
        road_feature_id: String,
    },
    #[error(
        "bridge `{landmark_id}` cannot derive a direction from water spline `{water_feature_id}`"
    )]
    DegenerateBridgeWater {
        landmark_id: String,
        water_feature_id: String,
    },
    #[error(
        "bridge `{landmark_id}` slope {slope_degrees:.2} degrees exceeds the player contract {maximum_degrees:.2} degrees"
    )]
    BridgeSlopeExceedsPlayerContract {
        landmark_id: String,
        slope_degrees: f32,
        maximum_degrees: f32,
    },
    #[error(
        "bridge `{landmark_id}` cannot place both landings below the player slope contract {maximum_degrees:.2} degrees"
    )]
    BridgeLandingExceedsPlayerContract {
        landmark_id: String,
        maximum_degrees: f32,
    },
    #[error("compiled bridge placement is invalid: {0}")]
    BridgePlacement(#[from] WorldSketchBridgePlacementError),
    #[error("cannot generate Blender terrain input: {0}")]
    TerrainGeneration(#[from] crate::TerrainGenerationError),
    #[error("cannot hash a derived world-sketch artifact: {0}")]
    DerivedHash(#[from] CanonicalJsonError),
    #[error("cannot serialize world-sketch compilation report: {0}")]
    ReportJson(#[from] serde_json::Error),
}

/// Resolves the authored XZ landmark onto the actual rendered terrain triangles.
/// The result is a derived capsule centre, not an edit to the source landmark.
pub fn world_sketch_player_spawn(
    sketch: &WorldSketchDocument,
    terrain: &TerrainDocument,
    player: &PlayerControllerProfileDocument,
) -> Result<[f32; 3], WorldSketchCompilationError> {
    sketch.validate()?;
    player.validate()?;
    if sketch.project_id != terrain.project_id
        || sketch.area_id != terrain.area_id
        || sketch.revision != terrain.revision
    {
        return Err(WorldSketchCompilationError::PlayerSpawnTerrainIdentityMismatch);
    }
    let spawns: Vec<_> = sketch
        .landmarks
        .iter()
        .filter(|landmark| landmark.kind == WorldSketchLandmarkKind::PlayerSpawn)
        .collect();
    if spawns.len() != 1 {
        return Err(WorldSketchCompilationError::PlayerSpawnCount {
            actual: spawns.len(),
        });
    }
    let position = spawns[0].position;
    let surface =
        TerrainGenerationPlan::from_document(terrain)?.surface_at(position.x_m, position.z_m)?;
    let half_height = player.controller.capsule_radius_m + player.controller.capsule_half_segment_m;
    Ok([
        position.x_m,
        surface.height_m + half_height + 0.02,
        position.z_m,
    ])
}

#[cfg(test)]
mod tests {
    use super::*;
    use crate::{
        embedded_visual_valley_terrain_document, embedded_visual_valley_vegetation_document,
    };

    fn pre_valley_world_sketch() -> Result<WorldSketchDocument, WorldSketchError> {
        WorldSketchDocument::from_json(include_str!(
            "../tests/fixtures/world-sketch-pre-valley.v1.json"
        ))
    }

    #[test]
    fn bridge_slope_diagnostic_canonicalizes_observed_native_wasm_ulp_difference() {
        let native = 0.210_118_9_f32;
        let wasm = f32::from_bits(native.to_bits() - 1);
        assert_eq!(
            canonical_bridge_slope(native).to_bits(),
            canonical_bridge_slope(wasm).to_bits()
        );
        assert_eq!(
            canonical_bridge_slope(native).to_bits(),
            0.2101_f32.to_bits()
        );
        for degrees in [0.0, 0.210_118_9, 3.789_123, 30.000_08, 45.0] {
            assert!((canonical_bridge_slope(degrees) - degrees).abs() <= 0.000_051);
        }
    }

    #[test]
    fn embedded_valley_revision_preserves_authored_features_and_clears_water() {
        let original = pre_valley_world_sketch().unwrap();
        let mut current = super::embedded_visual_valley_world_sketch().unwrap();
        let compiled = compile_world_sketch(&current).unwrap();
        let river = compiled
            .spline_carving
            .splines
            .iter()
            .find(|s| s.feature_id == "river_main")
            .unwrap();
        let water = crate::water_from_carved_spline(
            crate::embedded_visual_valley_water_document().unwrap(),
            river,
        )
        .unwrap();
        let terrain = TerrainGenerationPlan::from_document(&compiled.carved_terrain).unwrap();
        for sample in crate::sample_river_centerline(&water).unwrap() {
            let ground = terrain
                .surface_at(sample.center_m.x, sample.center_m.z)
                .unwrap();
            assert!((0.14..=3.0).contains(&(sample.center_m.y - ground.height_m)));
        }
        assert!(compiled.bridge_placements.placements[0].slope_degrees < 1.0);
        let river = current
            .splines
            .iter_mut()
            .find(|s| s.id == "river_main")
            .unwrap();
        river.surface_heights_m = None;
        river.maximum_cut_m = None;
        river.maximum_fill_m = None;
        river.valley_blend_width_m = None;
        let camera = current
            .validation_cameras
            .iter_mut()
            .find(|camera| camera.id == "human_bridge")
            .unwrap();
        assert_eq!(
            camera.position_m.map(f32::to_bits),
            [-35.0_f32, 5.4, -205.0].map(f32::to_bits)
        );
        assert_eq!(
            camera.target_m.map(f32::to_bits),
            [-40.0_f32, 11.4, -90.0].map(f32::to_bits)
        );
        camera.position_m[1] = 2.0;
        camera.target_m[1] = 8.0;
        assert_ne!(current.revision, original.revision);
        current.revision = original.revision;
        assert!(current.forest_scatter.is_some());
        assert!(original.forest_scatter.is_none());
        current.forest_scatter = None;
        assert_eq!(current, original);
    }

    #[test]
    fn vegetation_clearance_follows_authored_spawn_without_template_corridors() {
        let mut sketch = super::embedded_visual_valley_world_sketch().unwrap();
        let original = compile_world_sketch(&sketch).unwrap();
        assert_eq!(
            original.vegetation.exclusions.len(),
            sketch.splines.len() + 1
        );
        assert!(
            original
                .vegetation
                .exclusions
                .iter()
                .all(|exclusion| !matches!(exclusion, VegetationExclusion::CorridorSegment { .. }))
        );
        let circle = |exclusions: &[VegetationExclusion]| {
            exclusions
                .iter()
                .find_map(|exclusion| match exclusion {
                    VegetationExclusion::Circle {
                        id,
                        center_x_m,
                        center_z_m,
                        radius_m,
                    } => Some((id.clone(), [*center_x_m, *center_z_m, *radius_m])),
                    _ => None,
                })
                .unwrap()
        };
        let (original_id, original_circle) = circle(&original.vegetation.exclusions);
        assert_eq!(
            original_circle.map(f32::to_bits),
            [15.0_f32, -225.0, 12.0].map(f32::to_bits)
        );
        let spawn = sketch
            .landmarks
            .iter_mut()
            .find(|landmark| landmark.kind == WorldSketchLandmarkKind::PlayerSpawn)
            .unwrap();
        spawn.position.x_m = 130.0;
        spawn.position.z_m = 100.0;
        spawn.influence_radius_m = 6.0;
        let moved = compile_world_sketch(&sketch).unwrap();
        let (moved_id, moved_circle) = circle(&moved.vegetation.exclusions);
        assert_eq!(original_id, moved_id);
        assert_eq!(
            moved_circle.map(f32::to_bits),
            [130.0_f32, 100.0, 6.0].map(f32::to_bits)
        );
        assert_eq!(original.carved_terrain, moved.carved_terrain);
        let corridors = |exclusions: &[VegetationExclusion]| {
            exclusions
                .iter()
                .filter(|exclusion| {
                    matches!(exclusion, VegetationExclusion::PolylineCorridor { .. })
                })
                .cloned()
                .collect::<Vec<_>>()
        };
        assert_eq!(
            corridors(&original.vegetation.exclusions),
            corridors(&moved.vegetation.exclusions)
        );
    }

    #[test]
    fn player_spawn_uses_authored_landmark_and_rendered_triangles() {
        let mut sketch = super::embedded_visual_valley_world_sketch().unwrap();
        let compiled = compile_world_sketch(&sketch).unwrap();
        let profile = embedded_visual_valley_player_controller_profile().unwrap();
        let position =
            super::world_sketch_player_spawn(&sketch, &compiled.carved_terrain, &profile).unwrap();
        assert_eq!(position[0].to_bits(), 15.0_f32.to_bits());
        assert_eq!(position[2].to_bits(), (-225.0_f32).to_bits());
        let ground = TerrainGenerationPlan::from_document(&compiled.carved_terrain)
            .unwrap()
            .surface_at(position[0], position[2])
            .unwrap();
        let half_height =
            profile.controller.capsule_radius_m + profile.controller.capsule_half_segment_m;
        assert!((position[1] - ground.height_m - half_height - 0.02).abs() < 0.00001);
        let mut wrong_terrain = compiled.carved_terrain.clone();
        wrong_terrain.revision = forge_core::Revision::INITIAL;
        assert!(matches!(
            super::world_sketch_player_spawn(&sketch, &wrong_terrain, &profile),
            Err(super::WorldSketchCompilationError::PlayerSpawnTerrainIdentityMismatch)
        ));
        sketch
            .landmarks
            .retain(|landmark| landmark.kind != super::WorldSketchLandmarkKind::PlayerSpawn);
        assert!(matches!(
            super::world_sketch_player_spawn(&sketch, &compiled.carved_terrain, &profile),
            Err(super::WorldSketchCompilationError::PlayerSpawnCount { actual: 0 })
        ));
    }

    #[test]
    #[allow(clippy::too_many_lines)]
    fn authored_water_altitudes_round_trip_without_moving_locked_features() {
        let original = pre_valley_world_sketch().unwrap();
        let baseline = compile_world_sketch(&original).unwrap();
        let mut candidate = original.clone();
        let river_index = candidate
            .splines
            .iter()
            .position(|s| s.id == "river_main")
            .unwrap();
        candidate.splines[river_index].surface_heights_m =
            Some(vec![28.0, 23.0, 1.0, -12.0, -12.2, -12.5, -12.9]);
        candidate.splines[river_index].maximum_cut_m = Some(24.0);
        candidate.splines[river_index].maximum_fill_m = Some(16.0);
        candidate.validate().unwrap();
        let reopened =
            WorldSketchDocument::from_json(&candidate.to_pretty_json().unwrap()).unwrap();
        assert_eq!(candidate, reopened);
        assert_ne!(
            candidate.content_hash().unwrap(),
            original.content_hash().unwrap()
        );
        let first = compile_world_sketch(&candidate).unwrap();
        assert_eq!(first, compile_world_sketch(&reopened).unwrap());
        assert_eq!(first, compile_world_sketch(&candidate).unwrap());
        let river = first
            .spline_carving
            .splines
            .iter()
            .find(|s| s.feature_id == "river_main")
            .unwrap();
        let old_river = baseline
            .spline_carving
            .splines
            .iter()
            .find(|s| s.feature_id == "river_main")
            .unwrap();
        assert_eq!(
            river.centerline_samples.len(),
            old_river.centerline_samples.len()
        );
        for (new, old) in river
            .centerline_samples
            .iter()
            .zip(&old_river.centerline_samples)
        {
            assert_eq!((new.x_m, new.z_m), (old.x_m, old.z_m));
        }
        let heights = candidate.splines[river_index]
            .surface_heights_m
            .as_ref()
            .unwrap();
        for (index, height) in heights.iter().enumerate() {
            assert!(
                (river.centerline_samples[index * SPLINE_SAMPLES_PER_SEGMENT].surface_height_m
                    - height)
                    .abs()
                    < 0.00001
            );
        }
        assert!(
            river
                .centerline_samples
                .windows(2)
                .all(|p| p[1].surface_height_m <= p[0].surface_height_m)
        );
        assert!(first.spline_carving.minimum_height_delta_m >= -24.001);
        assert!(first.spline_carving.maximum_height_delta_m <= 16.001);
        assert!(first.spline_carving.maximum_height_delta_m > 10.0);
        for spline in &baseline.spline_carving.splines {
            if spline.feature_id != "river_main" {
                assert_eq!(
                    Some(spline),
                    first
                        .spline_carving
                        .splines
                        .iter()
                        .find(|s| s.feature_id == spline.feature_id)
                );
            }
        }
        let grid = &baseline.spline_carving.carved_height_grid;
        for z in 0..grid.vertices_z {
            for x in 0..grid.vertices_x {
                let x_m = grid.origin_x_m + f32::from(x) * grid.sample_spacing_m;
                let z_m = grid.origin_z_m + f32::from(z) * grid.sample_spacing_m;
                let (distance, _, _) =
                    nearest_centerline_values(x_m, z_m, &river.centerline_samples);
                if distance > river.authored_width_m * 0.5 + river.shoulder_width_m {
                    let index = usize::from(z) * usize::from(grid.vertices_x) + usize::from(x);
                    assert_eq!(
                        grid.samples_m[index].to_bits(),
                        first.spline_carving.carved_height_grid.samples_m[index].to_bits()
                    );
                }
            }
        }
        candidate.splines[river_index] = original.splines[river_index].clone();
        assert_eq!(candidate, original); // Every other author value and lock is identical.
    }

    #[test]
    fn authored_water_profile_rejects_invalid_or_ambiguous_inputs() {
        let mut sketch = pre_valley_world_sketch().unwrap();
        let index = sketch
            .splines
            .iter()
            .position(|s| s.id == "river_main")
            .unwrap();
        for heights in [
            vec![],
            vec![0.0; 6],
            vec![f32::NAN; 7],
            vec![f32::INFINITY; 7],
            vec![1024.1; 7],
            vec![0.0, 1.0, 0.0, 0.0, 0.0, 0.0, 0.0],
        ] {
            sketch.splines[index].surface_heights_m = Some(heights);
            assert!(matches!(
                sketch.validate(),
                Err(WorldSketchError::InvalidSplineSurfaceProfile(_))
            ));
        }
        sketch.splines[index].surface_heights_m = Some(vec![0.0; 7]);
        for fill in [-0.1, 64.1, f32::NAN, f32::INFINITY] {
            sketch.splines[index].maximum_fill_m = Some(fill);
            assert!(matches!(
                sketch.validate(),
                Err(WorldSketchError::InvalidSplineFillBudget(_))
            ));
        }
        sketch.splines[index].maximum_fill_m = Some(0.0);
        sketch.validate().unwrap();
        sketch.splines[index].surface_heights_m = None;
        assert!(matches!(
            sketch.validate(),
            Err(WorldSketchError::InvalidSplineFillBudget(_))
        ));
        sketch.splines[index].surface_heights_m = Some(vec![0.0; 7]);
        sketch.splines[index].kind = WorldSketchSplineKind::Road;
        assert!(matches!(
            sketch.validate(),
            Err(WorldSketchError::InvalidSplineSurfaceProfile(_))
        ));
    }

    #[test]
    fn valley_grading_is_explicit_bounded_and_protects_authored_landmarks() {
        let original = pre_valley_world_sketch().unwrap();
        let baseline = compile_world_sketch(&original).unwrap();
        let mut candidate = original.clone();
        let index = candidate
            .splines
            .iter()
            .position(|s| s.id == "river_main")
            .unwrap();
        let river = &mut candidate.splines[index];
        river.surface_heights_m = Some(vec![28.0, 23.0, 8.0, 4.0, 3.0, 2.0, 0.5]);
        river.maximum_cut_m = Some(12.0);
        river.maximum_fill_m = Some(40.0);
        river.valley_blend_width_m = Some(120.0);
        candidate.validate().unwrap();
        let reopened =
            WorldSketchDocument::from_json(&candidate.to_pretty_json().unwrap()).unwrap();
        assert_eq!(candidate, reopened);
        let compiled = compile_world_sketch(&candidate).unwrap();
        assert_eq!(compiled, compile_world_sketch(&reopened).unwrap());
        assert_eq!(compiled, compile_world_sketch(&candidate).unwrap());
        assert_eq!(compiled.vegetation, baseline.vegetation); // No wider exclusion corridor.
        let source = &baseline.blender_input.height_grid;
        let graded = grade_river_valleys(&candidate, source);
        let river = &candidate.splines[index];
        let centerline = build_carving_centerline(river, source, spline_carving_policy(river.kind));
        for z in 0..source.vertices_z {
            for x in 0..source.vertices_x {
                let x_m = source.origin_x_m + f32::from(x) * source.sample_spacing_m;
                let z_m = source.origin_z_m + f32::from(z) * source.sample_spacing_m;
                let index = usize::from(z) * usize::from(source.vertices_x) + usize::from(x);
                let delta = graded.samples_m[index] - source.samples_m[index];
                assert!((-12.001..=40.001).contains(&delta));
                let (distance, _, _) = nearest_centerline_values(x_m, z_m, &centerline);
                let protected = candidate.landmarks.iter().any(|landmark| {
                    matches!(
                        landmark.kind,
                        WorldSketchLandmarkKind::Village | WorldSketchLandmarkKind::Keep
                    ) && candidate.locked_feature_ids.contains(&landmark.id)
                        && (x_m - landmark.position.x_m).hypot(z_m - landmark.position.z_m)
                            <= landmark.influence_radius_m
                });
                if distance >= 141.0 || protected {
                    assert_eq!(
                        graded.samples_m[index].to_bits(),
                        source.samples_m[index].to_bits()
                    );
                }
            }
        }
        assert!(graded.maximum_height_m.is_finite());
        assert!(graded.minimum_height_m.is_finite());
        candidate.splines[index] = original.splines[index].clone();
        assert_eq!(candidate, original);
    }

    #[test]
    fn valley_grading_rejects_missing_budgets_and_invalid_widths() {
        let mut sketch = pre_valley_world_sketch().unwrap();
        let index = sketch
            .splines
            .iter()
            .position(|s| s.id == "river_main")
            .unwrap();
        sketch.splines[index].valley_blend_width_m = Some(120.0);
        assert!(matches!(
            sketch.validate(),
            Err(WorldSketchError::InvalidValleyBlendWidth(_))
        ));
        sketch.splines[index].surface_heights_m = Some(vec![0.0; 7]);
        sketch.splines[index].maximum_cut_m = Some(12.0);
        assert!(matches!(
            sketch.validate(),
            Err(WorldSketchError::InvalidValleyBlendWidth(_))
        ));
        sketch.splines[index].maximum_fill_m = Some(40.0);
        for invalid in [0.0, 15.9, 192.1, f32::NAN, f32::INFINITY] {
            sketch.splines[index].valley_blend_width_m = Some(invalid);
            assert!(matches!(
                sketch.validate(),
                Err(WorldSketchError::InvalidValleyBlendWidth(_))
            ));
        }
        for valid in [16.0, 192.0] {
            sketch.splines[index].valley_blend_width_m = Some(valid);
            sketch.validate().unwrap();
        }
    }

    #[test]
    fn explicit_river_cut_budget_preserves_path_and_clears_the_sampled_bed() {
        let original = pre_valley_world_sketch().unwrap();
        let mut candidate = original.clone();
        let index = candidate
            .splines
            .iter()
            .position(|s| s.kind == WorldSketchSplineKind::River)
            .unwrap();
        candidate.splines[index].maximum_cut_m = Some(40.0);
        assert_eq!(
            candidate.splines[index].points,
            original.splines[index].points
        );
        assert_eq!(candidate.locked_feature_ids, original.locked_feature_ids);
        let reopened =
            WorldSketchDocument::from_json(&candidate.to_pretty_json().unwrap()).unwrap();
        assert_eq!(candidate, reopened);
        let compiled = compile_world_sketch(&candidate).unwrap();
        let river = compiled
            .spline_carving
            .splines
            .iter()
            .find(|s| s.kind == WorldSketchSplineKind::River)
            .unwrap();
        let water = crate::water_from_carved_spline(
            crate::embedded_visual_valley_water_document().unwrap(),
            river,
        )
        .unwrap();
        let terrain = TerrainGenerationPlan::from_document(&compiled.carved_terrain).unwrap();
        for sample in crate::sample_river_centerline(&water).unwrap() {
            let ground = terrain
                .surface_at(sample.center_m.x, sample.center_m.z)
                .unwrap();
            assert!(
                sample.center_m.y - ground.height_m >= 0.14,
                "water obscured at {:?}: ground {}",
                sample.center_m,
                ground.height_m
            );
        }
        assert!(compiled.spline_carving.minimum_height_delta_m >= -40.001);
        assert_eq!(original, pre_valley_world_sketch().unwrap());
        for invalid in [0.0, -1.0, 64.1, f32::NAN, f32::INFINITY] {
            candidate.splines[index].maximum_cut_m = Some(invalid);
            assert!(matches!(
                candidate.validate(),
                Err(WorldSketchError::InvalidSplineCutBudget(_))
            ));
        }
    }

    #[test]
    #[allow(clippy::cast_precision_loss, clippy::too_many_lines)]
    fn pre_valley_world_sketch_round_trips_and_compiles_deterministically() {
        let sketch = pre_valley_world_sketch().unwrap();
        let json = sketch.to_pretty_json().unwrap();
        let restored = WorldSketchDocument::from_json(&json).unwrap();
        assert_eq!(restored, sketch);
        assert_eq!(
            restored.content_hash().unwrap(),
            sketch.content_hash().unwrap()
        );

        let first = compile_world_sketch(&sketch).unwrap();
        let second = compile_world_sketch(&sketch).unwrap();
        assert_eq!(first, second);
        assert_eq!(
            first.terrain,
            embedded_visual_valley_terrain_document().unwrap()
        );
        let mut original_vegetation = first.vegetation.clone();
        assert_eq!(original_vegetation.exclusions.len(), 4);
        // The remaining recipe fields still match the standalone fixture; authored
        // WorldSketch clearings are covered independently by the moved-spawn test.
        original_vegetation.exclusions = embedded_visual_valley_vegetation_document()
            .unwrap()
            .exclusions;
        assert_eq!(
            original_vegetation,
            embedded_visual_valley_vegetation_document().unwrap()
        );
        assert_eq!(first.report.source_image_count, 2);
        assert_eq!(
            first.report.materialized_outputs,
            [
                "terrain",
                "carved_terrain",
                "vegetation",
                "semantic_masks",
                "spline_carving",
                "traversal_audit",
                "bridge_placements",
                "blender_input"
            ]
        );
        assert_eq!(first.report.schema_version, 4);
        assert_eq!(first.report.validated_pending_outputs.len(), 4);
        assert_eq!(
            first.blender_input.height_grid.samples_m.len(),
            usize::from(TERRAIN_GLOBAL_VERTICES_PER_AXIS).pow(2)
        );
        assert!(
            first.blender_input.height_grid.minimum_height_m
                < first.blender_input.height_grid.maximum_height_m
        );
        assert_eq!(first.semantic_masks.layers.len(), 5);
        assert!(first.semantic_masks.layers.iter().all(|layer| {
            layer.samples_u8.len() == usize::from(TERRAIN_GLOBAL_VERTICES_PER_AXIS).pow(2)
        }));
        assert!(first.semantic_masks.layers.iter().any(|layer| {
            layer
                .samples_u8
                .iter()
                .any(|sample| (1..=254).contains(sample))
        }));
        assert_eq!(first.spline_carving.splines.len(), 3);
        assert!(first.spline_carving.modified_sample_count > 1_000);
        assert!(first.spline_carving.vegetation_exclusion_sample_count > 1_000);
        assert!(first.spline_carving.minimum_height_delta_m < -1.0);
        assert_eq!(
            first.spline_carving.carved_height_grid.samples_m.len(),
            usize::from(TERRAIN_GLOBAL_VERTICES_PER_AXIS).pow(2)
        );
        assert_eq!(first.carved_terrain.schema_version, 2);
        assert_eq!(
            first.carved_terrain.height_deltas.len(),
            first.spline_carving.modified_sample_count
        );
        assert_eq!(
            first.report.carved_terrain_sha256,
            first.carved_terrain.content_hash().unwrap()
        );
        assert_eq!(first.traversal_audit.schema_version, 2);
        assert_eq!(first.traversal_audit.routes.len(), 1);
        assert_eq!(first.traversal_audit.crossings.len(), 1);
        assert_eq!(
            first.traversal_audit.crossings[0]
                .bridge_landmark_id
                .as_deref(),
            Some("bridge_south")
        );
        assert_eq!(
            first.traversal_audit.crossings[0].runtime_status,
            WorldSketchCrossingRuntimeStatus::BridgeRuntimeProbeRequired
        );
        assert!(first.traversal_audit.water_blocked_sample_count > 0);
        assert!(first.traversal_audit.land_walkable_sample_count > 0);
        assert!(first.traversal_audit.connected_land_component_count > 0);
        assert!(first.traversal_audit.routes[0].water_blocked_sample_count > 0);
        assert!(!first.traversal_audit.routes[0].centerline_continuous_without_structures);
        assert!(
            first
                .traversal_audit
                .bridge_candidate_mask_u8
                .iter()
                .any(|sample| *sample > 0)
        );
        assert_eq!(
            first.traversal_audit.source_terrain_sha256,
            first.carved_terrain.content_hash().unwrap()
        );
        assert_eq!(first.blender_input.traversal_audit, first.traversal_audit);
        assert_eq!(first.bridge_placements.schema_version, 1);
        assert_eq!(first.bridge_placements.placements.len(), 1);
        let bridge = &first.bridge_placements.placements[0];
        assert_eq!(bridge.landmark_id, "bridge_south");
        assert_eq!(bridge.road_feature_id, "road_main");
        assert_eq!(bridge.water_feature_id, "river_main");
        assert!(
            (bridge.water_direction_xz[0].hypot(bridge.water_direction_xz[1]) - 1.0).abs() < 1.0e-5
        );
        assert_eq!(bridge.water_width_m.to_bits(), 18.0_f32.to_bits());
        assert!(bridge.crossing_bed_height_m.is_finite());
        assert!(bridge.slope_degrees > 3.0);
        assert!(bridge.slope_degrees < first.traversal_audit.maximum_walkable_slope_degrees);
        let bridge_horizontal_length_m = (bridge.deck_end_surface_m[0]
            - bridge.deck_start_surface_m[0])
            .hypot(bridge.deck_end_surface_m[2] - bridge.deck_start_surface_m[2]);
        assert!((bridge_horizontal_length_m - 44.0).abs() < 1.0e-3);
        for step in 0..=72 {
            let fraction = step as f32 / 72.0;
            let x_m = lerp(
                bridge.deck_start_surface_m[0],
                bridge.deck_end_surface_m[0],
                fraction,
            );
            let z_m = lerp(
                bridge.deck_start_surface_m[2],
                bridge.deck_end_surface_m[2],
                fraction,
            );
            let deck_height_m = lerp(
                bridge.deck_start_surface_m[1],
                bridge.deck_end_surface_m[1],
                fraction,
            );
            let terrain_height_m =
                sample_height_grid(&first.spline_carving.carved_height_grid, x_m, z_m);
            assert!(deck_height_m - terrain_height_m >= 0.10);
        }
        assert_eq!(bridge.supports.len(), 2);
        assert_eq!(
            first.bridge_placements.source_carved_terrain_sha256,
            first.carved_terrain.content_hash().unwrap()
        );
        assert_eq!(
            first.report.bridge_placements_sha256,
            first.bridge_placements.content_hash().unwrap()
        );
        assert_eq!(
            first.blender_input.bridge_placements,
            first.bridge_placements
        );
        let bridge_json = first.bridge_placements.to_pretty_json().unwrap();
        assert_eq!(
            WorldSketchBridgePlacementDocument::from_json(&bridge_json).unwrap(),
            first.bridge_placements
        );
    }

    #[test]
    fn unknown_fields_are_rejected() {
        let mut value: serde_json::Value =
            serde_json::from_str(VISUAL_VALLEY_WORLD_SKETCH_JSON).unwrap();
        value
            .as_object_mut()
            .unwrap()
            .insert("unexpected".to_owned(), serde_json::Value::Bool(true));
        let error = WorldSketchDocument::from_json(&value.to_string()).unwrap_err();
        assert!(matches!(error, WorldSketchError::Json(_)));
    }

    #[test]
    fn locks_must_reference_existing_features_and_be_sorted() {
        let mut sketch = pre_valley_world_sketch().unwrap();
        sketch.locked_feature_ids.push("missing_feature".to_owned());
        assert!(matches!(
            sketch.validate(),
            Err(WorldSketchError::UnknownLockedFeature(feature)) if feature == "missing_feature"
        ));

        let mut sketch = pre_valley_world_sketch().unwrap();
        sketch.locked_feature_ids.swap(0, 1);
        assert!(matches!(
            sketch.validate(),
            Err(WorldSketchError::LocksNotStrictlySorted)
        ));
    }

    #[test]
    fn traversal_paths_and_non_perspective_camera_sources_are_rejected() {
        let mut sketch = pre_valley_world_sketch().unwrap();
        sketch.source_images[0].path = "../outside.png".to_owned();
        assert!(matches!(
            sketch.validate(),
            Err(WorldSketchError::InvalidRelativePath { .. })
        ));

        let mut sketch = pre_valley_world_sketch().unwrap();
        sketch.validation_cameras[0].source_image_id = Some("topdown_master_v1".to_owned());
        assert!(matches!(
            sketch.validate(),
            Err(WorldSketchError::InvalidCameraSourceRole { .. })
        ));
    }

    #[test]
    fn compiler_rejects_extents_not_yet_supported_by_visual_valley() {
        let mut sketch = pre_valley_world_sketch().unwrap();
        sketch.extent_m.size_x_m = 1_024.0;
        sketch.extent_m.size_z_m = 1_024.0;
        sketch.terrain_height_sampling.sample_spacing_m = 4.0;
        let error = compile_world_sketch(&sketch).unwrap_err();
        assert!(matches!(
            error,
            WorldSketchCompilationError::UnsupportedVisualValleyExtent
        ));
    }
}
