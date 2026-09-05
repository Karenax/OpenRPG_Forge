use forge_core::{
    AreaId, CanonicalJsonError, ContentHash, CoordinateSystem, ProjectId, Revision,
    canonical_json_sha256,
};
use serde::{Deserialize, Serialize};
use thiserror::Error;

use crate::PathId;

pub const RIVER_DOCUMENT_SCHEMA: &str = "forge.river";
pub const RIVER_DOCUMENT_SCHEMA_VERSION: u32 = 1;
pub const MAX_RIVERS_PER_DOCUMENT: usize = 16;
pub const MAX_RIVER_DOCUMENT_JSON_BYTES: usize = 64 * 1_024;
pub const MAX_TOTAL_RIVER_LENGTH_M: f64 = 8_192.0;
pub const MIN_RIVER_SURFACE_WIDTH_M: f32 = 2.0;
pub const MAX_RIVER_SURFACE_WIDTH_M: f32 = 32.0;
pub const MIN_RIVER_BED_DEPTH_M: f32 = 0.15;
pub const MAX_RIVER_BED_DEPTH_M: f32 = 8.0;
pub const MIN_RIVER_BANK_BLEND_WIDTH_M: f32 = 1.0;
pub const MAX_RIVER_BANK_BLEND_WIDTH_M: f32 = 16.0;

const MAX_NAME_BYTES: usize = 128;

/// Versioned, renderer-independent river assignments for one area.
#[derive(Clone, Debug, PartialEq, Serialize, Deserialize)]
#[serde(deny_unknown_fields)]
pub struct RiverDocument {
    pub schema: String,
    pub schema_version: u32,
    pub project_id: ProjectId,
    pub area_id: AreaId,
    pub revision: Revision,
    pub coordinate_system: CoordinateSystem,
    pub name: String,
    /// Strictly sorted by `path_id`; one river channel may be assigned to each generic path.
    pub rivers: Vec<AuthoredRiver>,
}

impl RiverDocument {
    #[must_use]
    pub fn new(
        project_id: ProjectId,
        area_id: AreaId,
        revision: Revision,
        name: impl Into<String>,
        rivers: Vec<AuthoredRiver>,
    ) -> Self {
        Self {
            schema: RIVER_DOCUMENT_SCHEMA.to_owned(),
            schema_version: RIVER_DOCUMENT_SCHEMA_VERSION,
            project_id,
            area_id,
            revision,
            coordinate_system: CoordinateSystem::METRES_Y_UP_RIGHT_HANDED,
            name: name.into(),
            rivers,
        }
    }

    pub fn validate(&self) -> Result<(), RiverDocumentError> {
        if self.schema != RIVER_DOCUMENT_SCHEMA {
            return Err(RiverDocumentError::InvalidSchema(self.schema.clone()));
        }
        if self.schema_version != RIVER_DOCUMENT_SCHEMA_VERSION {
            return Err(RiverDocumentError::UnsupportedVersion(self.schema_version));
        }
        if self.project_id.as_uuid().is_nil() {
            return Err(RiverDocumentError::NilIdentity("project_id"));
        }
        if self.area_id.as_uuid().is_nil() {
            return Err(RiverDocumentError::NilIdentity("area_id"));
        }
        if self.revision.get() == 0 {
            return Err(RiverDocumentError::InvalidRevision);
        }
        if self.coordinate_system != CoordinateSystem::METRES_Y_UP_RIGHT_HANDED {
            return Err(RiverDocumentError::UnsupportedCoordinateSystem);
        }
        validate_name(&self.name)?;
        if self.rivers.len() > MAX_RIVERS_PER_DOCUMENT {
            return Err(RiverDocumentError::TooManyRivers(self.rivers.len()));
        }

        let mut previous_id = None;
        for river in &self.rivers {
            if previous_id.is_some_and(|previous| previous >= river.path_id) {
                return Err(RiverDocumentError::RiversNotCanonical);
            }
            previous_id = Some(river.path_id);
            river.validate()?;
        }

        ensure_json_budget(serde_json::to_string_pretty(self)?.len())
    }

    pub fn from_json(json: &str) -> Result<Self, RiverDocumentError> {
        ensure_json_budget(json.len())?;
        let document: Self = serde_json::from_str(json)?;
        document.validate()?;
        Ok(document)
    }

    pub fn to_pretty_json(&self) -> Result<String, RiverDocumentError> {
        self.validate()?;
        let json = serde_json::to_string_pretty(self)?;
        ensure_json_budget(json.len())?;
        Ok(json)
    }

    pub fn content_hash(&self) -> Result<ContentHash, RiverDocumentError> {
        self.validate()?;
        Ok(canonical_json_sha256(self)?)
    }
}

/// One persistent river channel assigned one-to-one to an existing generic path.
#[derive(Clone, Copy, Debug, PartialEq, Serialize, Deserialize)]
#[serde(deny_unknown_fields)]
pub struct AuthoredRiver {
    pub path_id: PathId,
    pub channel: RiverChannelProfile,
    pub surface: RiverSurface,
}

impl AuthoredRiver {
    pub fn validate(self) -> Result<(), RiverDocumentError> {
        if self.path_id.as_entity_id().as_uuid().is_nil() {
            return Err(RiverDocumentError::NilPathId);
        }
        self.channel.validate()
    }
}

/// Stable source parameters for terrain carving and the derived water surface.
#[derive(Clone, Copy, Debug, PartialEq, Serialize, Deserialize)]
#[serde(deny_unknown_fields)]
pub struct RiverChannelProfile {
    pub algorithm: RiverChannelProfileAlgorithm,
    pub surface_width_m: f32,
    pub bed_depth_m: f32,
    pub bank_blend_width_m: f32,
}

impl RiverChannelProfile {
    pub fn validate(self) -> Result<(), RiverDocumentError> {
        validate_bounded(
            self.surface_width_m,
            MIN_RIVER_SURFACE_WIDTH_M,
            MAX_RIVER_SURFACE_WIDTH_M,
            "channel.surface_width_m",
        )?;
        validate_bounded(
            self.bed_depth_m,
            MIN_RIVER_BED_DEPTH_M,
            MAX_RIVER_BED_DEPTH_M,
            "channel.bed_depth_m",
        )?;
        validate_bounded(
            self.bank_blend_width_m,
            MIN_RIVER_BANK_BLEND_WIDTH_M,
            MAX_RIVER_BANK_BLEND_WIDTH_M,
            "channel.bank_blend_width_m",
        )
    }
}

#[derive(Clone, Copy, Debug, Eq, PartialEq, Serialize, Deserialize)]
#[serde(rename_all = "snake_case")]
pub enum RiverChannelProfileAlgorithm {
    TerrainCarvedChannelV1,
}

#[derive(Clone, Copy, Debug, Eq, PartialEq, Serialize, Deserialize)]
#[serde(rename_all = "snake_case")]
pub enum RiverSurface {
    ForgeWaterV1,
}

fn validate_bounded(
    value: f32,
    minimum: f32,
    maximum: f32,
    field: &'static str,
) -> Result<(), RiverDocumentError> {
    if !value.is_finite() || !(minimum..=maximum).contains(&value) {
        return Err(RiverDocumentError::InvalidNumericValue(field));
    }
    Ok(())
}

fn validate_name(name: &str) -> Result<(), RiverDocumentError> {
    if name.trim().is_empty() || name.chars().any(char::is_control) {
        return Err(RiverDocumentError::InvalidName);
    }
    if name.len() > MAX_NAME_BYTES {
        return Err(RiverDocumentError::NameTooLong(name.len()));
    }
    Ok(())
}

fn ensure_json_budget(bytes: usize) -> Result<(), RiverDocumentError> {
    if bytes > MAX_RIVER_DOCUMENT_JSON_BYTES {
        Err(RiverDocumentError::JsonTooLarge {
            bytes,
            maximum: MAX_RIVER_DOCUMENT_JSON_BYTES,
        })
    } else {
        Ok(())
    }
}

#[derive(Debug, Error)]
pub enum RiverDocumentError {
    #[error("invalid river document schema `{0}`")]
    InvalidSchema(String),
    #[error("unsupported river document version {0}")]
    UnsupportedVersion(u32),
    #[error("{0} cannot be the nil UUID")]
    NilIdentity(&'static str),
    #[error("river document revision must be positive")]
    InvalidRevision,
    #[error("river document uses an unsupported coordinate system")]
    UnsupportedCoordinateSystem,
    #[error("river document name cannot be blank or contain control characters")]
    InvalidName,
    #[error("river document name contains {0} bytes; maximum is {MAX_NAME_BYTES}")]
    NameTooLong(usize),
    #[error("river document supports at most {MAX_RIVERS_PER_DOCUMENT} rivers, found {0}")]
    TooManyRivers(usize),
    #[error("rivers must be strictly sorted by unique path ID")]
    RiversNotCanonical,
    #[error("river path ID cannot be nil")]
    NilPathId,
    #[error("invalid river numeric value `{0}`")]
    InvalidNumericValue(&'static str),
    #[error("river JSON contains {bytes} bytes; maximum is {maximum}")]
    JsonTooLarge { bytes: usize, maximum: usize },
    #[error("invalid river JSON: {0}")]
    Json(#[from] serde_json::Error),
    #[error("cannot hash river document: {0}")]
    Hash(#[from] CanonicalJsonError),
}

#[cfg(test)]
mod tests {
    use super::*;
    use serde_json::json;

    fn river(key: &str) -> AuthoredRiver {
        AuthoredRiver {
            path_id: PathId::deterministic(format!("river-test/{key}")),
            channel: RiverChannelProfile {
                algorithm: RiverChannelProfileAlgorithm::TerrainCarvedChannelV1,
                surface_width_m: 8.0,
                bed_depth_m: 1.5,
                bank_blend_width_m: 4.0,
            },
            surface: RiverSurface::ForgeWaterV1,
        }
    }

    fn document(rivers: Vec<AuthoredRiver>) -> RiverDocument {
        RiverDocument::new(
            ProjectId::deterministic("river-test/project"),
            AreaId::deterministic("river-test/area"),
            Revision::INITIAL,
            "Rivières",
            rivers,
        )
    }

    #[test]
    fn canonical_round_trip_and_hash_are_stable() {
        let source = document(vec![river("a")]);
        let json = source.to_pretty_json().unwrap();
        let parsed = RiverDocument::from_json(&json).unwrap();
        assert_eq!(parsed, source);
        assert_eq!(
            parsed.content_hash().unwrap(),
            source.content_hash().unwrap()
        );
        assert!(json.contains("terrain_carved_channel_v1"));
        assert!(json.contains("forge_water_v1"));
    }

    #[test]
    fn strict_json_rejects_unknown_schema_and_oversized_input() {
        let source = document(vec![]);
        let mut value = serde_json::to_value(&source).unwrap();
        value["unknown"] = json!(true);
        assert!(matches!(
            RiverDocument::from_json(&value.to_string()),
            Err(RiverDocumentError::Json(_))
        ));
        let mut nested = serde_json::to_value(document(vec![river("nested")])).unwrap();
        nested["rivers"][0]["channel"]["unknown"] = json!(true);
        assert!(matches!(
            RiverDocument::from_json(&nested.to_string()),
            Err(RiverDocumentError::Json(_))
        ));
        for pointer in ["/rivers/0/channel/algorithm", "/rivers/0/surface"] {
            let mut unknown_variant =
                serde_json::to_value(document(vec![river("variant")])).unwrap();
            *unknown_variant.pointer_mut(pointer).unwrap() = json!("future_v2");
            assert!(matches!(
                RiverDocument::from_json(&unknown_variant.to_string()),
                Err(RiverDocumentError::Json(_))
            ));
        }

        let mut wrong_schema = source.clone();
        wrong_schema.schema = "forge.other".into();
        assert!(matches!(
            wrong_schema.validate(),
            Err(RiverDocumentError::InvalidSchema(_))
        ));
        let oversized = " ".repeat(MAX_RIVER_DOCUMENT_JSON_BYTES + 1);
        assert!(matches!(
            RiverDocument::from_json(&oversized),
            Err(RiverDocumentError::JsonTooLarge { .. })
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
            ("/coordinate_system", json!("left_handed")),
            ("/name", json!("\n")),
        ] {
            let mut value = serde_json::to_value(&source).unwrap();
            *value.pointer_mut(pointer).unwrap() = replacement;
            assert!(RiverDocument::from_json(&value.to_string()).is_err());
        }
        let mut too_long = source;
        too_long.name = "é".repeat(65);
        assert!(matches!(
            too_long.validate(),
            Err(RiverDocumentError::NameTooLong(130))
        ));
    }

    #[test]
    fn rivers_are_unique_sorted_non_nil_and_bounded() {
        let first = river("first");
        let second = river("second");
        let mut sorted = vec![first, second];
        sorted.sort_by_key(|candidate| candidate.path_id);
        assert!(document(sorted.clone()).validate().is_ok());

        let mut reversed = sorted.clone();
        reversed.reverse();
        assert!(matches!(
            document(reversed).validate(),
            Err(RiverDocumentError::RiversNotCanonical)
        ));
        assert!(matches!(
            document(vec![sorted[0], sorted[0]]).validate(),
            Err(RiverDocumentError::RiversNotCanonical)
        ));

        let mut nil = river("nil");
        nil.path_id =
            serde_json::from_value(json!("00000000-0000-0000-0000-000000000000")).unwrap();
        assert!(matches!(
            document(vec![nil]).validate(),
            Err(RiverDocumentError::NilPathId)
        ));

        let mut too_many = (0..=MAX_RIVERS_PER_DOCUMENT)
            .map(|index| river(&format!("limit-{index:02}")))
            .collect::<Vec<_>>();
        too_many.sort_by_key(|candidate| candidate.path_id);
        assert!(matches!(
            document(too_many).validate(),
            Err(RiverDocumentError::TooManyRivers(count)) if count == MAX_RIVERS_PER_DOCUMENT + 1
        ));
    }

    #[test]
    fn channel_numeric_limits_are_enforced() {
        for surface_width_m in [
            f32::NAN,
            MIN_RIVER_SURFACE_WIDTH_M - 0.01,
            MAX_RIVER_SURFACE_WIDTH_M + 0.01,
        ] {
            let mut invalid = river("width");
            invalid.channel.surface_width_m = surface_width_m;
            assert!(invalid.validate().is_err());
        }
        for bed_depth_m in [
            f32::INFINITY,
            MIN_RIVER_BED_DEPTH_M - 0.01,
            MAX_RIVER_BED_DEPTH_M + 0.01,
        ] {
            let mut invalid = river("depth");
            invalid.channel.bed_depth_m = bed_depth_m;
            assert!(invalid.validate().is_err());
        }
        for bank_blend_width_m in [
            f32::NEG_INFINITY,
            MIN_RIVER_BANK_BLEND_WIDTH_M - 0.01,
            MAX_RIVER_BANK_BLEND_WIDTH_M + 0.01,
        ] {
            let mut invalid = river("blend");
            invalid.channel.bank_blend_width_m = bank_blend_width_m;
            assert!(invalid.validate().is_err());
        }

        for (width, depth, blend) in [
            (
                MIN_RIVER_SURFACE_WIDTH_M,
                MIN_RIVER_BED_DEPTH_M,
                MIN_RIVER_BANK_BLEND_WIDTH_M,
            ),
            (
                MAX_RIVER_SURFACE_WIDTH_M,
                MAX_RIVER_BED_DEPTH_M,
                MAX_RIVER_BANK_BLEND_WIDTH_M,
            ),
        ] {
            let mut boundary = river("boundary");
            boundary.channel.surface_width_m = width;
            boundary.channel.bed_depth_m = depth;
            boundary.channel.bank_blend_width_m = blend;
            boundary.validate().unwrap();
        }
    }
}
