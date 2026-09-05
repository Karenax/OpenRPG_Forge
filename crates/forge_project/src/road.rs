use forge_core::{
    AreaId, CanonicalJsonError, ContentHash, CoordinateSystem, ProjectId, Revision,
    canonical_json_sha256,
};
use serde::{Deserialize, Serialize};
use thiserror::Error;

use crate::PathId;

pub const ROAD_DOCUMENT_SCHEMA: &str = "forge.road";
pub const ROAD_DOCUMENT_SCHEMA_VERSION: u32 = 1;
pub const MAX_ROADS_PER_DOCUMENT: usize = 64;
pub const MAX_ROAD_DOCUMENT_JSON_BYTES: usize = 64 * 1_024;
pub const MAX_TOTAL_ROAD_LENGTH_M: f64 = 8_192.0;
pub const MIN_ROAD_WIDTH_M: f32 = 1.0;
pub const MAX_ROAD_WIDTH_M: f32 = 16.0;
pub const MAX_ROAD_CROWN_HEIGHT_M: f32 = 0.30;
pub const MAX_ROAD_CROWN_TO_WIDTH_RATIO: f32 = 0.05;

const MAX_NAME_BYTES: usize = 128;

/// Versioned, renderer-independent road assignments for one area.
#[derive(Clone, Debug, PartialEq, Serialize, Deserialize)]
#[serde(deny_unknown_fields)]
pub struct RoadDocument {
    pub schema: String,
    pub schema_version: u32,
    pub project_id: ProjectId,
    pub area_id: AreaId,
    pub revision: Revision,
    pub coordinate_system: CoordinateSystem,
    pub name: String,
    /// Strictly sorted by `path_id`; one road profile may be assigned to each generic path.
    pub roads: Vec<AuthoredRoad>,
}

impl RoadDocument {
    #[must_use]
    pub fn new(
        project_id: ProjectId,
        area_id: AreaId,
        revision: Revision,
        name: impl Into<String>,
        roads: Vec<AuthoredRoad>,
    ) -> Self {
        Self {
            schema: ROAD_DOCUMENT_SCHEMA.to_owned(),
            schema_version: ROAD_DOCUMENT_SCHEMA_VERSION,
            project_id,
            area_id,
            revision,
            coordinate_system: CoordinateSystem::METRES_Y_UP_RIGHT_HANDED,
            name: name.into(),
            roads,
        }
    }

    pub fn validate(&self) -> Result<(), RoadDocumentError> {
        if self.schema != ROAD_DOCUMENT_SCHEMA {
            return Err(RoadDocumentError::InvalidSchema(self.schema.clone()));
        }
        if self.schema_version != ROAD_DOCUMENT_SCHEMA_VERSION {
            return Err(RoadDocumentError::UnsupportedVersion(self.schema_version));
        }
        if self.project_id.as_uuid().is_nil() {
            return Err(RoadDocumentError::NilIdentity("project_id"));
        }
        if self.area_id.as_uuid().is_nil() {
            return Err(RoadDocumentError::NilIdentity("area_id"));
        }
        if self.revision.get() == 0 {
            return Err(RoadDocumentError::InvalidRevision);
        }
        if self.coordinate_system != CoordinateSystem::METRES_Y_UP_RIGHT_HANDED {
            return Err(RoadDocumentError::UnsupportedCoordinateSystem);
        }
        validate_name(&self.name)?;
        if self.roads.len() > MAX_ROADS_PER_DOCUMENT {
            return Err(RoadDocumentError::TooManyRoads(self.roads.len()));
        }

        let mut previous_id = None;
        for road in &self.roads {
            if previous_id.is_some_and(|previous| previous >= road.path_id) {
                return Err(RoadDocumentError::RoadsNotCanonical);
            }
            previous_id = Some(road.path_id);
            road.validate()?;
        }

        ensure_json_budget(serde_json::to_string_pretty(self)?.len())
    }

    pub fn from_json(json: &str) -> Result<Self, RoadDocumentError> {
        ensure_json_budget(json.len())?;
        let document: Self = serde_json::from_str(json)?;
        document.validate()?;
        Ok(document)
    }

    pub fn to_pretty_json(&self) -> Result<String, RoadDocumentError> {
        self.validate()?;
        let json = serde_json::to_string_pretty(self)?;
        ensure_json_budget(json.len())?;
        Ok(json)
    }

    pub fn content_hash(&self) -> Result<ContentHash, RoadDocumentError> {
        self.validate()?;
        Ok(canonical_json_sha256(self)?)
    }
}

/// One persistent road profile assigned one-to-one to an existing generic path.
#[derive(Clone, Copy, Debug, PartialEq, Serialize, Deserialize)]
#[serde(deny_unknown_fields)]
pub struct AuthoredRoad {
    pub path_id: PathId,
    pub profile: RoadProfile,
    pub surface: RoadSurface,
}

impl AuthoredRoad {
    pub fn validate(self) -> Result<(), RoadDocumentError> {
        if self.path_id.as_entity_id().as_uuid().is_nil() {
            return Err(RoadDocumentError::NilPathId);
        }
        self.profile.validate()
    }
}

/// Stable interpretation of the road cross-section. Terrain sampling and mesh output stay derived.
#[derive(Clone, Copy, Debug, PartialEq, Serialize, Deserialize)]
#[serde(deny_unknown_fields)]
pub struct RoadProfile {
    pub algorithm: RoadProfileAlgorithm,
    pub width_m: f32,
    pub crown_height_m: f32,
}

impl RoadProfile {
    pub fn validate(self) -> Result<(), RoadDocumentError> {
        if !self.width_m.is_finite()
            || !(MIN_ROAD_WIDTH_M..=MAX_ROAD_WIDTH_M).contains(&self.width_m)
        {
            return Err(RoadDocumentError::InvalidNumericValue("profile.width_m"));
        }
        if !self.crown_height_m.is_finite()
            || !(0.0..=MAX_ROAD_CROWN_HEIGHT_M).contains(&self.crown_height_m)
        {
            return Err(RoadDocumentError::InvalidNumericValue(
                "profile.crown_height_m",
            ));
        }
        if self.crown_height_m > self.width_m * MAX_ROAD_CROWN_TO_WIDTH_RATIO {
            return Err(RoadDocumentError::InvalidProfileRelation(
                "road crown height cannot exceed five percent of road width",
            ));
        }
        Ok(())
    }
}

/// Versioned cross-section generation contract for the derived road ribbon.
#[derive(Clone, Copy, Debug, Eq, PartialEq, Serialize, Deserialize)]
#[serde(rename_all = "snake_case")]
pub enum RoadProfileAlgorithm {
    TerrainDrapedCrownV1,
}

/// Logical road surface. The renderer maps it to Forge-owned procedural presentation.
#[derive(Clone, Copy, Debug, Eq, PartialEq, Serialize, Deserialize)]
#[serde(rename_all = "snake_case")]
pub enum RoadSurface {
    CompactedEarthV1,
}

fn validate_name(name: &str) -> Result<(), RoadDocumentError> {
    if name.trim().is_empty() || name.chars().any(char::is_control) {
        return Err(RoadDocumentError::InvalidName);
    }
    if name.len() > MAX_NAME_BYTES {
        return Err(RoadDocumentError::NameTooLong(name.len()));
    }
    Ok(())
}

fn ensure_json_budget(bytes: usize) -> Result<(), RoadDocumentError> {
    if bytes > MAX_ROAD_DOCUMENT_JSON_BYTES {
        Err(RoadDocumentError::JsonTooLarge {
            bytes,
            maximum: MAX_ROAD_DOCUMENT_JSON_BYTES,
        })
    } else {
        Ok(())
    }
}

#[derive(Debug, Error)]
pub enum RoadDocumentError {
    #[error("invalid road document schema `{0}`")]
    InvalidSchema(String),
    #[error("unsupported road document version {0}")]
    UnsupportedVersion(u32),
    #[error("{0} cannot be the nil UUID")]
    NilIdentity(&'static str),
    #[error("road document revision must be positive")]
    InvalidRevision,
    #[error("road document uses an unsupported coordinate system")]
    UnsupportedCoordinateSystem,
    #[error("road document name cannot be blank or contain control characters")]
    InvalidName,
    #[error("road document name contains {0} bytes; maximum is {MAX_NAME_BYTES}")]
    NameTooLong(usize),
    #[error("road document supports at most {MAX_ROADS_PER_DOCUMENT} roads, found {0}")]
    TooManyRoads(usize),
    #[error("roads must be strictly sorted by unique path ID")]
    RoadsNotCanonical,
    #[error("road path ID cannot be nil")]
    NilPathId,
    #[error("invalid road numeric value `{0}`")]
    InvalidNumericValue(&'static str),
    #[error("invalid road profile relation: {0}")]
    InvalidProfileRelation(&'static str),
    #[error("road JSON contains {bytes} bytes; maximum is {maximum}")]
    JsonTooLarge { bytes: usize, maximum: usize },
    #[error("invalid road JSON: {0}")]
    Json(#[from] serde_json::Error),
    #[error("cannot hash road document: {0}")]
    Hash(#[from] CanonicalJsonError),
}

#[cfg(test)]
mod tests {
    use super::*;
    use serde_json::json;

    fn road(key: &str) -> AuthoredRoad {
        AuthoredRoad {
            path_id: PathId::deterministic(format!("road/{key}")),
            profile: RoadProfile {
                algorithm: RoadProfileAlgorithm::TerrainDrapedCrownV1,
                width_m: 4.0,
                crown_height_m: 0.08,
            },
            surface: RoadSurface::CompactedEarthV1,
        }
    }

    fn document(mut roads: Vec<AuthoredRoad>) -> RoadDocument {
        roads.sort_by_key(|road| road.path_id);
        RoadDocument::new(
            ProjectId::deterministic("road-project"),
            AreaId::deterministic("road-area"),
            Revision::INITIAL,
            "Routes",
            roads,
        )
    }

    #[test]
    fn canonical_round_trip_and_hash_are_stable() {
        let source = document(vec![road("a"), road("b")]);
        let json = source.to_pretty_json().unwrap();
        let restored = RoadDocument::from_json(&json).unwrap();
        assert_eq!(restored, source);
        assert_eq!(
            restored.content_hash().unwrap(),
            source.content_hash().unwrap()
        );
        assert!(json.contains("\"terrain_draped_crown_v1\""));
        assert!(json.contains("\"compacted_earth_v1\""));
    }

    #[test]
    fn strict_json_rejects_unknown_schema_and_oversized_input() {
        let source = document(vec![road("strict")]);
        let mut value = serde_json::to_value(&source).unwrap();
        value["unknown"] = json!(true);
        assert!(matches!(
            RoadDocument::from_json(&value.to_string()),
            Err(RoadDocumentError::Json(_))
        ));
        let mut unknown_road = serde_json::to_value(&source).unwrap();
        unknown_road["roads"][0]["shoulder_width_m"] = json!(1.0);
        assert!(RoadDocument::from_json(&unknown_road.to_string()).is_err());
        let oversized = " ".repeat(MAX_ROAD_DOCUMENT_JSON_BYTES + 1);
        assert!(matches!(
            RoadDocument::from_json(&oversized),
            Err(RoadDocumentError::JsonTooLarge { .. })
        ));
    }

    #[test]
    fn identity_revision_name_and_schema_are_validated() {
        let source = document(vec![]);
        for (pointer, replacement) in [
            ("/schema", json!("other")),
            ("/schema_version", json!(2)),
            ("/project_id", json!("00000000-0000-0000-0000-000000000000")),
            ("/area_id", json!("00000000-0000-0000-0000-000000000000")),
            ("/revision", json!(0)),
            ("/name", json!("\n")),
        ] {
            let mut value = serde_json::to_value(&source).unwrap();
            *value.pointer_mut(pointer).unwrap() = replacement;
            assert!(RoadDocument::from_json(&value.to_string()).is_err());
        }
        let mut too_long = source;
        too_long.name = "é".repeat(65);
        assert!(matches!(
            too_long.validate(),
            Err(RoadDocumentError::NameTooLong(130))
        ));
    }

    #[test]
    fn roads_are_unique_sorted_non_nil_and_bounded() {
        let first = road("a");
        let second = road("b");
        let mut unordered = document(vec![first, second]);
        unordered.roads.reverse();
        assert!(matches!(
            unordered.validate(),
            Err(RoadDocumentError::RoadsNotCanonical)
        ));

        let mut duplicate = document(vec![first]);
        duplicate.roads.push(first);
        assert!(matches!(
            duplicate.validate(),
            Err(RoadDocumentError::RoadsNotCanonical)
        ));

        let mut nil = document(vec![first]);
        nil.roads[0].path_id =
            serde_json::from_value(json!("00000000-0000-0000-0000-000000000000")).unwrap();
        assert!(matches!(nil.validate(), Err(RoadDocumentError::NilPathId)));

        let mut roads = (0..=MAX_ROADS_PER_DOCUMENT)
            .map(|index| road(&format!("limit-{index:03}")))
            .collect::<Vec<_>>();
        roads.sort_by_key(|road| road.path_id);
        assert!(matches!(
            document(roads).validate(),
            Err(RoadDocumentError::TooManyRoads(count)) if count == MAX_ROADS_PER_DOCUMENT + 1
        ));
    }

    #[test]
    fn profile_numeric_limits_and_relation_are_enforced() {
        let mut source = document(vec![road("profile")]);
        for width_m in [f32::NAN, MIN_ROAD_WIDTH_M - 0.01, MAX_ROAD_WIDTH_M + 0.01] {
            source.roads[0].profile.width_m = width_m;
            assert!(matches!(
                source.validate(),
                Err(RoadDocumentError::InvalidNumericValue("profile.width_m"))
            ));
        }

        source.roads[0].profile.width_m = 4.0;
        for crown_height_m in [f32::NAN, -0.01, MAX_ROAD_CROWN_HEIGHT_M + 0.01] {
            source.roads[0].profile.crown_height_m = crown_height_m;
            assert!(matches!(
                source.validate(),
                Err(RoadDocumentError::InvalidNumericValue(
                    "profile.crown_height_m"
                ))
            ));
        }

        source.roads[0].profile.crown_height_m = 0.21;
        assert!(matches!(
            source.validate(),
            Err(RoadDocumentError::InvalidProfileRelation(_))
        ));
        source.roads[0].profile.crown_height_m = 0.20;
        source.validate().unwrap();
    }
}
