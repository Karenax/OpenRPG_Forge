use forge_core::{
    AreaId, CanonicalJsonError, ContentHash, CoordinateSystem, EntityId, ProjectId, Revision, Vec3,
    canonical_json_sha256,
};
use serde::{Deserialize, Serialize};
use std::fmt;
use thiserror::Error;

pub const PATH_DOCUMENT_SCHEMA: &str = "forge.path";
pub const PATH_DOCUMENT_SCHEMA_VERSION: u32 = 1;
pub const MAX_PATHS_PER_DOCUMENT: usize = 64;
pub const MAX_PATH_CONTROL_POINTS: usize = 64;
pub const MAX_TOTAL_PATH_CONTROL_POINTS: usize = 2_048;
pub const MAX_PATH_LENGTH_M: f64 = 8_192.0;
pub const MAX_TOTAL_PATH_LENGTH_M: f64 = 32_768.0;
pub const MAX_PATH_DOCUMENT_JSON_BYTES: usize = 512 * 1_024;

const MAX_NAME_BYTES: usize = 128;
const MIN_OPEN_PATH_CONTROL_POINTS: usize = 2;
const MIN_CLOSED_PATH_CONTROL_POINTS: usize = 3;
const AREA_HALF_EXTENT_M: f32 = 256.0;
const MAX_PATH_HEIGHT_M: f32 = 10_000.0;
const MIN_CONTROL_POINT_DISTANCE_M: f64 = 0.5;

/// Persistent path identity, distinct from other authored entity domains.
#[derive(Clone, Copy, Debug, Eq, Hash, Ord, PartialEq, PartialOrd, Serialize, Deserialize)]
#[serde(transparent)]
pub struct PathId(EntityId);

impl PathId {
    #[must_use]
    pub const fn new(value: EntityId) -> Self {
        Self(value)
    }

    #[must_use]
    pub const fn as_entity_id(self) -> EntityId {
        self.0
    }

    /// Produces a stable path identity without sharing deterministic keys with other entities.
    #[must_use]
    pub fn deterministic(key: impl AsRef<[u8]>) -> Self {
        let mut scoped = b"path/".to_vec();
        scoped.extend_from_slice(key.as_ref());
        Self(EntityId::deterministic(scoped))
    }
}

impl From<EntityId> for PathId {
    fn from(value: EntityId) -> Self {
        Self::new(value)
    }
}

impl From<PathId> for EntityId {
    fn from(value: PathId) -> Self {
        value.as_entity_id()
    }
}

impl fmt::Display for PathId {
    fn fmt(&self, formatter: &mut fmt::Formatter<'_>) -> fmt::Result {
        self.0.fmt(formatter)
    }
}

/// Versioned, renderer-independent collection of authored paths for one area.
#[derive(Clone, Debug, PartialEq, Serialize, Deserialize)]
#[serde(deny_unknown_fields)]
pub struct PathDocument {
    pub schema: String,
    pub schema_version: u32,
    pub project_id: ProjectId,
    pub area_id: AreaId,
    pub revision: Revision,
    pub coordinate_system: CoordinateSystem,
    pub name: String,
    pub paths: Vec<AuthoredPath>,
}

impl PathDocument {
    #[must_use]
    pub fn new(
        project_id: ProjectId,
        area_id: AreaId,
        revision: Revision,
        name: impl Into<String>,
        paths: Vec<AuthoredPath>,
    ) -> Self {
        Self {
            schema: PATH_DOCUMENT_SCHEMA.to_owned(),
            schema_version: PATH_DOCUMENT_SCHEMA_VERSION,
            project_id,
            area_id,
            revision,
            coordinate_system: CoordinateSystem::METRES_Y_UP_RIGHT_HANDED,
            name: name.into(),
            paths,
        }
    }

    pub fn validate(&self) -> Result<(), PathDocumentError> {
        if self.schema != PATH_DOCUMENT_SCHEMA {
            return Err(PathDocumentError::InvalidSchema(self.schema.clone()));
        }
        if self.schema_version != PATH_DOCUMENT_SCHEMA_VERSION {
            return Err(PathDocumentError::UnsupportedVersion(self.schema_version));
        }
        if self.project_id.as_uuid().is_nil() {
            return Err(PathDocumentError::NilIdentity("project_id"));
        }
        if self.area_id.as_uuid().is_nil() {
            return Err(PathDocumentError::NilIdentity("area_id"));
        }
        if self.revision.get() == 0 {
            return Err(PathDocumentError::InvalidRevision);
        }
        if self.coordinate_system != CoordinateSystem::METRES_Y_UP_RIGHT_HANDED {
            return Err(PathDocumentError::UnsupportedCoordinateSystem);
        }
        validate_name("document name", &self.name)?;
        if self.paths.len() > MAX_PATHS_PER_DOCUMENT {
            return Err(PathDocumentError::TooManyPaths(self.paths.len()));
        }

        let mut previous_id = None;
        let mut total_control_points = 0_usize;
        let mut total_length_m = 0.0_f64;
        for path in &self.paths {
            if previous_id.is_some_and(|previous| previous >= path.id) {
                return Err(PathDocumentError::PathsNotCanonical);
            }
            previous_id = Some(path.id);
            let length_m = path.validate()?;
            total_control_points = total_control_points
                .checked_add(path.control_points.len())
                .ok_or(PathDocumentError::TooManyTotalControlPoints(usize::MAX))?;
            if total_control_points > MAX_TOTAL_PATH_CONTROL_POINTS {
                return Err(PathDocumentError::TooManyTotalControlPoints(
                    total_control_points,
                ));
            }
            total_length_m += length_m;
            if !total_length_m.is_finite() || total_length_m > MAX_TOTAL_PATH_LENGTH_M {
                return Err(PathDocumentError::TotalPathLengthExceeded {
                    length_m: total_length_m,
                    maximum_m: MAX_TOTAL_PATH_LENGTH_M,
                });
            }
        }

        ensure_json_budget(serde_json::to_string_pretty(self)?.len())
    }

    pub fn from_json(json: &str) -> Result<Self, PathDocumentError> {
        ensure_json_budget(json.len())?;
        let document: Self = serde_json::from_str(json)?;
        document.validate()?;
        Ok(document)
    }

    pub fn to_pretty_json(&self) -> Result<String, PathDocumentError> {
        self.validate()?;
        let json = serde_json::to_string_pretty(self)?;
        ensure_json_budget(json.len())?;
        Ok(json)
    }

    pub fn content_hash(&self) -> Result<ContentHash, PathDocumentError> {
        self.validate()?;
        Ok(canonical_json_sha256(self)?)
    }
}

/// One generic authored path. Rendering, width, profiles and terrain effects are derived later.
#[derive(Clone, Debug, PartialEq, Serialize, Deserialize)]
#[serde(deny_unknown_fields)]
pub struct AuthoredPath {
    pub id: PathId,
    pub name: String,
    pub closed: bool,
    pub interpolation: PathInterpolation,
    pub control_points: Vec<PathControlPoint>,
}

impl AuthoredPath {
    /// Validates the path and returns its horizontal control-polygon length in metres.
    pub fn validate(&self) -> Result<f64, PathDocumentError> {
        if self.id.as_entity_id().as_uuid().is_nil() {
            return Err(PathDocumentError::NilPathId);
        }
        validate_name("path name", &self.name)?;
        let minimum = if self.closed {
            MIN_CLOSED_PATH_CONTROL_POINTS
        } else {
            MIN_OPEN_PATH_CONTROL_POINTS
        };
        if self.control_points.len() < minimum {
            return Err(PathDocumentError::TooFewControlPoints {
                id: self.id,
                found: self.control_points.len(),
                minimum,
            });
        }
        if self.control_points.len() > MAX_PATH_CONTROL_POINTS {
            return Err(PathDocumentError::TooManyControlPoints {
                id: self.id,
                found: self.control_points.len(),
            });
        }
        for (index, point) in self.control_points.iter().enumerate() {
            point.validate(self.id, index)?;
        }

        let mut length_m = 0.0_f64;
        for (index, pair) in self.control_points.windows(2).enumerate() {
            length_m += validate_segment(self.id, index, index + 1, pair[0], pair[1])?;
        }
        if self.closed {
            let last_index = self.control_points.len() - 1;
            length_m += validate_segment(
                self.id,
                last_index,
                0,
                self.control_points[last_index],
                self.control_points[0],
            )?;
        }
        if !length_m.is_finite() || length_m > MAX_PATH_LENGTH_M {
            return Err(PathDocumentError::PathTooLong {
                id: self.id,
                length_m,
                maximum_m: MAX_PATH_LENGTH_M,
            });
        }
        Ok(length_m)
    }
}

/// Stable continuous-curve interpretation for the ordered authored points.
#[derive(Clone, Copy, Debug, Eq, PartialEq, Serialize, Deserialize)]
#[serde(rename_all = "snake_case")]
pub enum PathInterpolation {
    CentripetalCatmullRomV1,
}

#[derive(Clone, Copy, Debug, PartialEq, Serialize, Deserialize)]
#[serde(deny_unknown_fields)]
pub struct PathControlPoint {
    pub position_m: Vec3,
}

impl PathControlPoint {
    #[must_use]
    pub const fn new(position_m: Vec3) -> Self {
        Self { position_m }
    }

    fn validate(self, id: PathId, index: usize) -> Result<(), PathDocumentError> {
        let position = self.position_m;
        if !position.is_finite()
            || !(-AREA_HALF_EXTENT_M..AREA_HALF_EXTENT_M).contains(&position.x)
            || !(-MAX_PATH_HEIGHT_M..=MAX_PATH_HEIGHT_M).contains(&position.y)
            || !(-AREA_HALF_EXTENT_M..AREA_HALF_EXTENT_M).contains(&position.z)
        {
            return Err(PathDocumentError::InvalidControlPoint { id, index });
        }
        Ok(())
    }
}

fn validate_segment(
    id: PathId,
    first_index: usize,
    second_index: usize,
    first: PathControlPoint,
    second: PathControlPoint,
) -> Result<f64, PathDocumentError> {
    let delta_x = f64::from(second.position_m.x) - f64::from(first.position_m.x);
    let delta_z = f64::from(second.position_m.z) - f64::from(first.position_m.z);
    let distance_m = delta_x.hypot(delta_z);
    if !distance_m.is_finite() || distance_m < MIN_CONTROL_POINT_DISTANCE_M {
        return Err(PathDocumentError::ControlPointsTooClose {
            id,
            first_index,
            second_index,
        });
    }
    Ok(distance_m)
}

fn validate_name(field: &'static str, name: &str) -> Result<(), PathDocumentError> {
    if name.trim().is_empty() || name.chars().any(char::is_control) {
        return Err(PathDocumentError::InvalidName(field));
    }
    if name.len() > MAX_NAME_BYTES {
        return Err(PathDocumentError::NameTooLong {
            field,
            bytes: name.len(),
        });
    }
    Ok(())
}

fn ensure_json_budget(bytes: usize) -> Result<(), PathDocumentError> {
    if bytes > MAX_PATH_DOCUMENT_JSON_BYTES {
        Err(PathDocumentError::JsonTooLarge {
            bytes,
            maximum: MAX_PATH_DOCUMENT_JSON_BYTES,
        })
    } else {
        Ok(())
    }
}

#[derive(Debug, Error)]
pub enum PathDocumentError {
    #[error("invalid path document schema `{0}`")]
    InvalidSchema(String),
    #[error("unsupported path document version {0}")]
    UnsupportedVersion(u32),
    #[error("{0} cannot be the nil UUID")]
    NilIdentity(&'static str),
    #[error("path document revision must be positive")]
    InvalidRevision,
    #[error("path document uses an unsupported coordinate system")]
    UnsupportedCoordinateSystem,
    #[error("{0} cannot be blank or contain control characters")]
    InvalidName(&'static str),
    #[error("{field} contains {bytes} bytes; maximum is {MAX_NAME_BYTES}")]
    NameTooLong { field: &'static str, bytes: usize },
    #[error("path document supports at most {MAX_PATHS_PER_DOCUMENT} paths, found {0}")]
    TooManyPaths(usize),
    #[error("paths must be strictly sorted by unique path ID")]
    PathsNotCanonical,
    #[error("path ID cannot be nil")]
    NilPathId,
    #[error("path {id} needs at least {minimum} control points, found {found}")]
    TooFewControlPoints {
        id: PathId,
        found: usize,
        minimum: usize,
    },
    #[error("path {id} supports at most {MAX_PATH_CONTROL_POINTS} control points, found {found}")]
    TooManyControlPoints { id: PathId, found: usize },
    #[error(
        "path document supports at most {MAX_TOTAL_PATH_CONTROL_POINTS} control points, found {0}"
    )]
    TooManyTotalControlPoints(usize),
    #[error("path {id} control point {index} lies outside the supported finite area bounds")]
    InvalidControlPoint { id: PathId, index: usize },
    #[error("path {id} control points {first_index} and {second_index} are too close horizontally")]
    ControlPointsTooClose {
        id: PathId,
        first_index: usize,
        second_index: usize,
    },
    #[error("path {id} length {length_m} m exceeds {maximum_m} m")]
    PathTooLong {
        id: PathId,
        length_m: f64,
        maximum_m: f64,
    },
    #[error("total path length {length_m} m exceeds {maximum_m} m")]
    TotalPathLengthExceeded { length_m: f64, maximum_m: f64 },
    #[error("path JSON contains {bytes} bytes; maximum is {maximum}")]
    JsonTooLarge { bytes: usize, maximum: usize },
    #[error("invalid path JSON: {0}")]
    Json(#[from] serde_json::Error),
    #[error("cannot hash path document: {0}")]
    Hash(#[from] CanonicalJsonError),
}

#[cfg(test)]
mod tests {
    use super::*;
    use serde_json::json;

    fn path(key: &str, x_offset: f32) -> AuthoredPath {
        AuthoredPath {
            id: PathId::deterministic(key),
            name: format!("Chemin {key}"),
            closed: false,
            interpolation: PathInterpolation::CentripetalCatmullRomV1,
            control_points: vec![
                PathControlPoint::new(Vec3::new(-8.0 + x_offset, 0.0, -4.0)),
                PathControlPoint::new(Vec3::new(x_offset, 1.0, 0.0)),
                PathControlPoint::new(Vec3::new(8.0 + x_offset, 0.0, 4.0)),
            ],
        }
    }

    fn document(paths: Vec<AuthoredPath>) -> PathDocument {
        let mut paths = paths;
        paths.sort_by_key(|path| path.id);
        PathDocument::new(
            ProjectId::deterministic("path-test/project"),
            AreaId::deterministic("path-test/area"),
            Revision::INITIAL,
            "Zone de test",
            paths,
        )
    }

    #[test]
    fn versioned_json_round_trip_and_hash_are_canonical() {
        let document = document(vec![path("b", 12.0), path("a", -12.0)]);
        let pretty = document.to_pretty_json().unwrap();
        let compact = serde_json::to_string(&document).unwrap();
        for json in [pretty, compact] {
            let restored = PathDocument::from_json(&json).unwrap();
            assert_eq!(restored, document);
            assert_eq!(
                restored.content_hash().unwrap(),
                document.content_hash().unwrap()
            );
        }
        assert_ne!(PathId::deterministic("a"), PathId::deterministic("b"));
        assert_eq!(PathId::deterministic("a"), PathId::deterministic("a"));
    }

    #[test]
    fn schema_identity_revision_names_and_json_are_strict_and_bounded() {
        let valid = document(vec![]);
        for (pointer, replacement) in [
            ("/schema", json!("other")),
            ("/schema_version", json!(2)),
            ("/revision", json!(0)),
            ("/name", json!("\n")),
            ("/project_id", json!("00000000-0000-0000-0000-000000000000")),
        ] {
            let mut value = serde_json::to_value(&valid).unwrap();
            *value.pointer_mut(pointer).unwrap() = replacement;
            assert!(PathDocument::from_json(&value.to_string()).is_err());
        }
        let mut unknown = serde_json::to_value(&valid).unwrap();
        unknown["unknown"] = json!(true);
        assert!(PathDocument::from_json(&unknown.to_string()).is_err());
        let oversized = format!(
            "{{\"padding\":\"{}\"}}",
            "x".repeat(MAX_PATH_DOCUMENT_JSON_BYTES)
        );
        assert!(matches!(
            PathDocument::from_json(&oversized),
            Err(PathDocumentError::JsonTooLarge { .. })
        ));
    }

    #[test]
    fn path_bounds_minimums_and_closed_segment_are_validated() {
        let mut valid = path("bounds", 0.0);
        valid.control_points[0].position_m = Vec3::new(-256.0, -10_000.0, -256.0);
        valid.control_points[2].position_m = Vec3::new(255.999, 10_000.0, 255.999);
        document(vec![valid.clone()]).validate().unwrap();

        for invalid_position in [
            Vec3::new(256.0, 0.0, 0.0),
            Vec3::new(0.0, 10_000.1, 0.0),
            Vec3::new(f32::NAN, 0.0, 0.0),
        ] {
            let mut invalid = valid.clone();
            invalid.control_points[1].position_m = invalid_position;
            assert!(matches!(
                document(vec![invalid]).validate(),
                Err(PathDocumentError::InvalidControlPoint { .. })
            ));
        }

        let mut open = path("open", 0.0);
        open.control_points.truncate(1);
        assert!(matches!(
            document(vec![open]).validate(),
            Err(PathDocumentError::TooFewControlPoints { minimum: 2, .. })
        ));
        let mut closed = path("closed", 0.0);
        closed.closed = true;
        closed.control_points.truncate(2);
        assert!(matches!(
            document(vec![closed]).validate(),
            Err(PathDocumentError::TooFewControlPoints { minimum: 3, .. })
        ));
        let mut repeated_end = path("closed-repeat", 0.0);
        repeated_end.closed = true;
        repeated_end.control_points[2] = repeated_end.control_points[0];
        assert!(matches!(
            document(vec![repeated_end]).validate(),
            Err(PathDocumentError::ControlPointsTooClose {
                second_index: 0,
                ..
            })
        ));
    }

    #[test]
    fn path_identity_names_nested_shape_and_minimum_distance_are_strict() {
        let valid = document(vec![path("strict", 0.0)]);
        let mut nil_id = serde_json::to_value(&valid).unwrap();
        nil_id["paths"][0]["id"] = json!("00000000-0000-0000-0000-000000000000");
        assert!(matches!(
            PathDocument::from_json(&nil_id.to_string()),
            Err(PathDocumentError::NilPathId)
        ));

        for name in ["", "   ", "A\nB"] {
            let mut invalid = valid.clone();
            invalid.paths[0].name = name.to_owned();
            assert!(matches!(
                invalid.validate(),
                Err(PathDocumentError::InvalidName("path name"))
            ));
        }
        let mut too_long = valid.clone();
        too_long.paths[0].name = "é".repeat(65);
        assert!(matches!(
            too_long.validate(),
            Err(PathDocumentError::NameTooLong {
                field: "path name",
                bytes: 130
            })
        ));

        let mut unknown_path = serde_json::to_value(&valid).unwrap();
        unknown_path["paths"][0]["width_m"] = json!(2.0);
        assert!(PathDocument::from_json(&unknown_path.to_string()).is_err());
        let mut unknown_point = serde_json::to_value(&valid).unwrap();
        unknown_point["paths"][0]["control_points"][0]["handle"] = json!(true);
        assert!(PathDocument::from_json(&unknown_point.to_string()).is_err());

        let mut exact = path("exact-distance", 0.0);
        exact.control_points = vec![
            PathControlPoint::new(Vec3::new(0.0, 0.0, 0.0)),
            PathControlPoint::new(Vec3::new(0.5, 100.0, 0.0)),
        ];
        document(vec![exact.clone()]).validate().unwrap();
        exact.control_points[1].position_m.x = 0.499;
        assert!(matches!(
            document(vec![exact]).validate(),
            Err(PathDocumentError::ControlPointsTooClose { .. })
        ));
    }

    #[test]
    fn collections_order_counts_and_lengths_are_bounded() {
        let mut unordered = document(vec![path("first", 0.0), path("second", 16.0)]);
        unordered.paths.reverse();
        assert!(matches!(
            unordered.validate(),
            Err(PathDocumentError::PathsNotCanonical)
        ));
        let mut duplicate = document(vec![path("duplicate", 0.0)]);
        duplicate.paths.push(duplicate.paths[0].clone());
        assert!(matches!(
            duplicate.validate(),
            Err(PathDocumentError::PathsNotCanonical)
        ));

        let mut too_many = (0..=MAX_PATHS_PER_DOCUMENT)
            .map(|index| path(&format!("many-{index}"), 0.0))
            .collect::<Vec<_>>();
        too_many.sort_by_key(|path| path.id);
        assert!(matches!(
            document(too_many).validate(),
            Err(PathDocumentError::TooManyPaths(_))
        ));

        let mut long = path("long", 0.0);
        long.control_points = (0_u16..u16::try_from(MAX_PATH_CONTROL_POINTS).unwrap())
            .map(|index| {
                let x = if index % 2 == 0 { -255.0 } else { 255.0 };
                PathControlPoint::new(Vec3::new(x, 0.0, f32::from(index) * 0.5 - 16.0))
            })
            .collect();
        assert!(matches!(
            document(vec![long]).validate(),
            Err(PathDocumentError::PathTooLong { .. })
        ));

        let mut total = (0..46)
            .map(|index| {
                let mut path = path(&format!("total-{index}"), 0.0);
                path.control_points = vec![
                    PathControlPoint::new(Vec3::new(-255.0, 0.0, -255.0)),
                    PathControlPoint::new(Vec3::new(255.0, 0.0, 255.0)),
                ];
                path
            })
            .collect::<Vec<_>>();
        total.sort_by_key(|path| path.id);
        assert!(matches!(
            document(total).validate(),
            Err(PathDocumentError::TotalPathLengthExceeded { .. })
        ));
    }

    #[test]
    fn total_control_point_budget_is_enforced_after_per_path_limits() {
        let mut paths = (0_u16..33)
            .map(|path_index| {
                let mut path = path(&format!("points-{path_index}"), 0.0);
                path.control_points = (0_u16..u16::try_from(MAX_PATH_CONTROL_POINTS).unwrap())
                    .map(|point_index| {
                        PathControlPoint::new(Vec3::new(
                            f32::from(point_index) - 32.0,
                            0.0,
                            f32::from(path_index) - 16.0,
                        ))
                    })
                    .collect();
                path
            })
            .collect::<Vec<_>>();
        paths.sort_by_key(|path| path.id);
        assert!(matches!(
            document(paths).validate(),
            Err(PathDocumentError::TooManyTotalControlPoints(_))
        ));
    }
}
