use forge_core::{
    AreaId, CanonicalJsonError, ContentHash, CoordinateSystem, ProjectId, Revision,
    canonical_json_sha256,
};
use serde::{Deserialize, Serialize};
use thiserror::Error;

use crate::PathId;

pub const WALL_DOCUMENT_SCHEMA: &str = "forge.wall";
pub const WALL_DOCUMENT_SCHEMA_VERSION: u32 = 1;
pub const MAX_WALLS_PER_DOCUMENT: usize = 64;
pub const MAX_WALL_DOCUMENT_JSON_BYTES: usize = 64 * 1_024;
pub const MAX_TOTAL_WALL_LENGTH_M: f64 = 8_192.0;
pub const MIN_WALL_HEIGHT_M: f32 = 0.25;
pub const MAX_WALL_HEIGHT_M: f32 = 8.0;
pub const MIN_WALL_THICKNESS_M: f32 = 0.10;
pub const MAX_WALL_THICKNESS_M: f32 = 2.0;

const MAX_NAME_BYTES: usize = 128;

/// Versioned, renderer-independent wall assignments for one area.
#[derive(Clone, Debug, PartialEq, Serialize, Deserialize)]
#[serde(deny_unknown_fields)]
pub struct WallDocument {
    pub schema: String,
    pub schema_version: u32,
    pub project_id: ProjectId,
    pub area_id: AreaId,
    pub revision: Revision,
    pub coordinate_system: CoordinateSystem,
    pub name: String,
    /// Strictly sorted by `path_id`; one wall profile may be assigned to each generic path.
    pub walls: Vec<AuthoredWall>,
}

impl WallDocument {
    #[must_use]
    pub fn new(
        project_id: ProjectId,
        area_id: AreaId,
        revision: Revision,
        name: impl Into<String>,
        walls: Vec<AuthoredWall>,
    ) -> Self {
        Self {
            schema: WALL_DOCUMENT_SCHEMA.to_owned(),
            schema_version: WALL_DOCUMENT_SCHEMA_VERSION,
            project_id,
            area_id,
            revision,
            coordinate_system: CoordinateSystem::METRES_Y_UP_RIGHT_HANDED,
            name: name.into(),
            walls,
        }
    }

    pub fn validate(&self) -> Result<(), WallDocumentError> {
        if self.schema != WALL_DOCUMENT_SCHEMA {
            return Err(WallDocumentError::InvalidSchema(self.schema.clone()));
        }
        if self.schema_version != WALL_DOCUMENT_SCHEMA_VERSION {
            return Err(WallDocumentError::UnsupportedVersion(self.schema_version));
        }
        if self.project_id.as_uuid().is_nil() {
            return Err(WallDocumentError::NilIdentity("project_id"));
        }
        if self.area_id.as_uuid().is_nil() {
            return Err(WallDocumentError::NilIdentity("area_id"));
        }
        if self.revision.get() == 0 {
            return Err(WallDocumentError::InvalidRevision);
        }
        if self.coordinate_system != CoordinateSystem::METRES_Y_UP_RIGHT_HANDED {
            return Err(WallDocumentError::UnsupportedCoordinateSystem);
        }
        validate_name(&self.name)?;
        if self.walls.len() > MAX_WALLS_PER_DOCUMENT {
            return Err(WallDocumentError::TooManyWalls(self.walls.len()));
        }

        let mut previous_id = None;
        for wall in &self.walls {
            if previous_id.is_some_and(|previous| previous >= wall.path_id) {
                return Err(WallDocumentError::WallsNotCanonical);
            }
            previous_id = Some(wall.path_id);
            wall.validate()?;
        }

        ensure_json_budget(serde_json::to_string_pretty(self)?.len())
    }

    pub fn from_json(json: &str) -> Result<Self, WallDocumentError> {
        ensure_json_budget(json.len())?;
        let document: Self = serde_json::from_str(json)?;
        document.validate()?;
        Ok(document)
    }

    pub fn to_pretty_json(&self) -> Result<String, WallDocumentError> {
        self.validate()?;
        let json = serde_json::to_string_pretty(self)?;
        ensure_json_budget(json.len())?;
        Ok(json)
    }

    pub fn content_hash(&self) -> Result<ContentHash, WallDocumentError> {
        self.validate()?;
        Ok(canonical_json_sha256(self)?)
    }
}

/// One persistent wall assigned one-to-one to an existing generic path.
#[derive(Clone, Copy, Debug, PartialEq, Serialize, Deserialize)]
#[serde(deny_unknown_fields)]
pub struct AuthoredWall {
    pub path_id: PathId,
    pub profile: WallProfile,
    pub surface: WallSurface,
}

impl AuthoredWall {
    pub fn validate(self) -> Result<(), WallDocumentError> {
        if self.path_id.as_entity_id().as_uuid().is_nil() {
            return Err(WallDocumentError::NilPathId);
        }
        self.profile.validate()
    }
}

/// Stable source parameters for a terrain-draped solid wall.
#[derive(Clone, Copy, Debug, PartialEq, Serialize, Deserialize)]
#[serde(deny_unknown_fields)]
pub struct WallProfile {
    pub algorithm: WallProfileAlgorithm,
    pub height_m: f32,
    pub thickness_m: f32,
}

impl WallProfile {
    pub fn validate(self) -> Result<(), WallDocumentError> {
        validate_bounded(
            self.height_m,
            MIN_WALL_HEIGHT_M,
            MAX_WALL_HEIGHT_M,
            "profile.height_m",
        )?;
        validate_bounded(
            self.thickness_m,
            MIN_WALL_THICKNESS_M,
            MAX_WALL_THICKNESS_M,
            "profile.thickness_m",
        )
    }
}

/// Versioned generation contract for the derived wall solid.
#[derive(Clone, Copy, Debug, Eq, PartialEq, Serialize, Deserialize)]
#[serde(rename_all = "snake_case")]
pub enum WallProfileAlgorithm {
    TerrainDrapedSolidV1,
}

/// Logical wall surface. The renderer maps it to a Forge-owned procedural presentation.
#[derive(Clone, Copy, Debug, Eq, PartialEq, Serialize, Deserialize)]
#[serde(rename_all = "snake_case")]
pub enum WallSurface {
    ForgeStoneV1,
}

fn validate_bounded(
    value: f32,
    minimum: f32,
    maximum: f32,
    field: &'static str,
) -> Result<(), WallDocumentError> {
    if !value.is_finite() || !(minimum..=maximum).contains(&value) {
        return Err(WallDocumentError::InvalidNumericValue(field));
    }
    Ok(())
}

fn validate_name(name: &str) -> Result<(), WallDocumentError> {
    if name.trim().is_empty() || name.chars().any(char::is_control) {
        return Err(WallDocumentError::InvalidName);
    }
    if name.len() > MAX_NAME_BYTES {
        return Err(WallDocumentError::NameTooLong(name.len()));
    }
    Ok(())
}

fn ensure_json_budget(bytes: usize) -> Result<(), WallDocumentError> {
    if bytes > MAX_WALL_DOCUMENT_JSON_BYTES {
        Err(WallDocumentError::JsonTooLarge {
            bytes,
            maximum: MAX_WALL_DOCUMENT_JSON_BYTES,
        })
    } else {
        Ok(())
    }
}

#[derive(Debug, Error)]
pub enum WallDocumentError {
    #[error("invalid wall document schema `{0}`")]
    InvalidSchema(String),
    #[error("unsupported wall document version {0}")]
    UnsupportedVersion(u32),
    #[error("{0} cannot be the nil UUID")]
    NilIdentity(&'static str),
    #[error("wall document revision must be positive")]
    InvalidRevision,
    #[error("wall document uses an unsupported coordinate system")]
    UnsupportedCoordinateSystem,
    #[error("wall document name cannot be blank or contain control characters")]
    InvalidName,
    #[error("wall document name contains {0} bytes; maximum is {MAX_NAME_BYTES}")]
    NameTooLong(usize),
    #[error("wall document supports at most {MAX_WALLS_PER_DOCUMENT} walls, found {0}")]
    TooManyWalls(usize),
    #[error("walls must be strictly sorted by unique path ID")]
    WallsNotCanonical,
    #[error("wall path ID cannot be nil")]
    NilPathId,
    #[error("invalid wall numeric value `{0}`")]
    InvalidNumericValue(&'static str),
    #[error("wall JSON contains {bytes} bytes; maximum is {maximum}")]
    JsonTooLarge { bytes: usize, maximum: usize },
    #[error("invalid wall JSON: {0}")]
    Json(#[from] serde_json::Error),
    #[error("cannot hash wall document: {0}")]
    Hash(#[from] CanonicalJsonError),
}

#[cfg(test)]
mod tests {
    use super::*;
    use serde_json::json;

    fn wall(key: &str) -> AuthoredWall {
        AuthoredWall {
            path_id: PathId::deterministic(format!("wall/{key}")),
            profile: WallProfile {
                algorithm: WallProfileAlgorithm::TerrainDrapedSolidV1,
                height_m: 2.5,
                thickness_m: 0.4,
            },
            surface: WallSurface::ForgeStoneV1,
        }
    }

    fn document(mut walls: Vec<AuthoredWall>) -> WallDocument {
        walls.sort_by_key(|wall| wall.path_id);
        WallDocument::new(
            ProjectId::deterministic("wall-project"),
            AreaId::deterministic("wall-area"),
            Revision::INITIAL,
            "Murs",
            walls,
        )
    }

    #[test]
    fn canonical_round_trip_and_hash_are_stable() {
        let source = document(vec![wall("a"), wall("b")]);
        let pretty = source.to_pretty_json().unwrap();
        let compact = serde_json::to_string(&source).unwrap();
        for json in [pretty.clone(), compact] {
            let restored = WallDocument::from_json(&json).unwrap();
            assert_eq!(restored, source);
            assert_eq!(
                restored.content_hash().unwrap(),
                source.content_hash().unwrap()
            );
        }
        assert!(pretty.contains("\"terrain_draped_solid_v1\""));
        assert!(pretty.contains("\"forge_stone_v1\""));
    }

    #[test]
    fn strict_json_rejects_unknown_fields_and_oversized_input() {
        let source = document(vec![wall("strict")]);
        for pointer in ["", "/walls/0", "/walls/0/profile"] {
            let mut value = serde_json::to_value(&source).unwrap();
            value.pointer_mut(pointer).unwrap()["unknown"] = json!(true);
            assert!(matches!(
                WallDocument::from_json(&value.to_string()),
                Err(WallDocumentError::Json(_))
            ));
        }
        let oversized = " ".repeat(MAX_WALL_DOCUMENT_JSON_BYTES + 1);
        assert!(matches!(
            WallDocument::from_json(&oversized),
            Err(WallDocumentError::JsonTooLarge { .. })
        ));
    }

    #[test]
    fn schema_identity_revision_and_name_are_validated() {
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
            assert!(WallDocument::from_json(&value.to_string()).is_err());
        }
        let mut too_long = source;
        too_long.name = "é".repeat(65);
        assert!(matches!(
            too_long.validate(),
            Err(WallDocumentError::NameTooLong(130))
        ));
    }

    #[test]
    fn walls_are_unique_sorted_non_nil_and_bounded() {
        let first = wall("a");
        let second = wall("b");
        let mut unordered = document(vec![first, second]);
        unordered.walls.reverse();
        assert!(matches!(
            unordered.validate(),
            Err(WallDocumentError::WallsNotCanonical)
        ));

        let mut duplicate = document(vec![first]);
        duplicate.walls.push(first);
        assert!(matches!(
            duplicate.validate(),
            Err(WallDocumentError::WallsNotCanonical)
        ));

        let mut nil = document(vec![first]);
        nil.walls[0].path_id =
            serde_json::from_value(json!("00000000-0000-0000-0000-000000000000")).unwrap();
        assert!(matches!(nil.validate(), Err(WallDocumentError::NilPathId)));

        let mut walls = (0..=MAX_WALLS_PER_DOCUMENT)
            .map(|index| wall(&format!("limit-{index:03}")))
            .collect::<Vec<_>>();
        walls.sort_by_key(|wall| wall.path_id);
        assert!(matches!(
            document(walls).validate(),
            Err(WallDocumentError::TooManyWalls(count)) if count == MAX_WALLS_PER_DOCUMENT + 1
        ));
    }

    #[test]
    fn profile_numeric_limits_are_enforced_inclusively() {
        let mut source = document(vec![wall("profile")]);
        for height_m in [f32::NAN, MIN_WALL_HEIGHT_M - 0.01, MAX_WALL_HEIGHT_M + 0.01] {
            source.walls[0].profile.height_m = height_m;
            assert!(matches!(
                source.validate(),
                Err(WallDocumentError::InvalidNumericValue("profile.height_m"))
            ));
        }
        for height_m in [MIN_WALL_HEIGHT_M, MAX_WALL_HEIGHT_M] {
            source.walls[0].profile.height_m = height_m;
            source.validate().unwrap();
        }

        source.walls[0].profile.height_m = 2.5;
        for thickness_m in [
            f32::NAN,
            MIN_WALL_THICKNESS_M - 0.01,
            MAX_WALL_THICKNESS_M + 0.01,
        ] {
            source.walls[0].profile.thickness_m = thickness_m;
            assert!(matches!(
                source.validate(),
                Err(WallDocumentError::InvalidNumericValue(
                    "profile.thickness_m"
                ))
            ));
        }
        for thickness_m in [MIN_WALL_THICKNESS_M, MAX_WALL_THICKNESS_M] {
            source.walls[0].profile.thickness_m = thickness_m;
            source.validate().unwrap();
        }
    }
}
