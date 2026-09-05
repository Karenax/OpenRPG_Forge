use forge_core::{
    AreaId, AssetId, CanonicalJsonError, ContentHash, CoordinateSystem, EntityId, ProjectId,
    Revision, Transform, TransformError, canonical_json_sha256,
};
use serde::{Deserialize, Serialize};
use thiserror::Error;

pub const PLACEMENT_DOCUMENT_SCHEMA: &str = "forge.placement";
pub const PLACEMENT_DOCUMENT_SCHEMA_VERSION: u32 = 1;
pub const MAX_ASSET_PINS_PER_DOCUMENT: usize = 256;
pub const MAX_AUTHORED_PLACEMENTS_PER_DOCUMENT: usize = 1_024;
pub const MAX_PLACEMENT_DOCUMENT_JSON_BYTES: usize = 512 * 1_024;
pub const MIN_ASSET_BASE_SCALE: f32 = 0.01;
pub const MAX_ASSET_BASE_SCALE: f32 = 100.0;
pub const PLACEMENT_HORIZONTAL_HALF_EXTENT_M: f32 = 256.0;

const MAX_NAME_BYTES: usize = 128;
const MAX_STABLE_KEY_BYTES: usize = 256;
const MAX_SOURCE_FIELD_BYTES: usize = 256;
const MAX_PATH_BYTES: usize = 256;
const ASSET_ID_KEY_PREFIX: &str = "forge.asset-browser:model";

/// Versioned, renderer-neutral source for explicitly placed governed assets in one area.
#[derive(Clone, Debug, PartialEq, Serialize, Deserialize)]
#[serde(deny_unknown_fields)]
pub struct PlacementDocument {
    pub schema: String,
    pub schema_version: u32,
    pub project_id: ProjectId,
    pub area_id: AreaId,
    pub revision: Revision,
    pub coordinate_system: CoordinateSystem,
    pub name: String,
    /// Immutable source pins, strictly sorted by unique `asset_id`.
    pub asset_pins: Vec<AssetPin>,
    /// Authored instances, strictly sorted by unique persistent entity `id`.
    pub placements: Vec<AuthoredPlacement>,
}

impl PlacementDocument {
    #[must_use]
    pub fn new(
        project_id: ProjectId,
        area_id: AreaId,
        revision: Revision,
        name: impl Into<String>,
        asset_pins: Vec<AssetPin>,
        placements: Vec<AuthoredPlacement>,
    ) -> Self {
        Self {
            schema: PLACEMENT_DOCUMENT_SCHEMA.to_owned(),
            schema_version: PLACEMENT_DOCUMENT_SCHEMA_VERSION,
            project_id,
            area_id,
            revision,
            coordinate_system: CoordinateSystem::METRES_Y_UP_RIGHT_HANDED,
            name: name.into(),
            asset_pins,
            placements,
        }
    }

    pub fn validate(&self) -> Result<(), PlacementDocumentError> {
        validate_header(self)?;
        if self.asset_pins.len() > MAX_ASSET_PINS_PER_DOCUMENT {
            return Err(PlacementDocumentError::TooManyAssetPins(
                self.asset_pins.len(),
            ));
        }
        if self.placements.len() > MAX_AUTHORED_PLACEMENTS_PER_DOCUMENT {
            return Err(PlacementDocumentError::TooManyPlacements(
                self.placements.len(),
            ));
        }

        let mut previous_asset_id = None;
        let mut stable_keys = std::collections::BTreeSet::new();
        for pin in &self.asset_pins {
            if previous_asset_id.is_some_and(|previous| previous >= pin.asset_id) {
                return Err(PlacementDocumentError::AssetPinsNotCanonical);
            }
            previous_asset_id = Some(pin.asset_id);
            pin.validate()?;
            if !stable_keys.insert(pin.stable_key.as_str()) {
                return Err(PlacementDocumentError::DuplicateStableKey(
                    pin.stable_key.clone(),
                ));
            }
        }

        let mut previous_entity_id = None;
        for placement in &self.placements {
            if previous_entity_id.is_some_and(|previous| previous >= placement.id) {
                return Err(PlacementDocumentError::PlacementsNotCanonical);
            }
            previous_entity_id = Some(placement.id);
            placement.validate()?;
            if self
                .asset_pins
                .binary_search_by_key(&placement.asset_id, |pin| pin.asset_id)
                .is_err()
            {
                return Err(PlacementDocumentError::MissingAssetPin(placement.asset_id));
            }
        }

        ensure_json_budget(serde_json::to_string_pretty(self)?.len())
    }

    pub fn from_json(json: &str) -> Result<Self, PlacementDocumentError> {
        ensure_json_budget(json.len())?;
        let document: Self = serde_json::from_str(json)?;
        document.validate()?;
        Ok(document)
    }

    pub fn to_pretty_json(&self) -> Result<String, PlacementDocumentError> {
        self.validate()?;
        let json = serde_json::to_string_pretty(self)?;
        ensure_json_budget(json.len())?;
        Ok(json)
    }

    pub fn content_hash(&self) -> Result<ContentHash, PlacementDocumentError> {
        self.validate()?;
        Ok(canonical_json_sha256(self)?)
    }
}

/// Complete immutable provenance required to reopen a placed asset without consulting the browser.
#[derive(Clone, Debug, PartialEq, Serialize, Deserialize)]
#[serde(deny_unknown_fields)]
pub struct AssetPin {
    pub asset_id: AssetId,
    pub stable_key: String,
    pub subset_id: String,
    pub pack_id: String,
    pub source_version: String,
    pub author: String,
    pub source_path: String,
    pub scene_path: String,
    pub scene_content_hash: ContentHash,
    pub manifest_hash: ContentHash,
    pub license_spdx: String,
    pub license_evidence_hash: ContentHash,
    pub base_scale: f32,
}

impl AssetPin {
    pub fn validate(&self) -> Result<(), PlacementDocumentError> {
        if self.asset_id.as_uuid().is_nil() {
            return Err(PlacementDocumentError::NilIdentity("asset_id"));
        }
        validate_stable_key(&self.stable_key)?;
        if self.asset_id != asset_id_from_stable_key(&self.stable_key) {
            return Err(PlacementDocumentError::AssetIdentityMismatch(self.asset_id));
        }
        validate_source_field("subset_id", &self.subset_id)?;
        validate_source_field("pack_id", &self.pack_id)?;
        let (stable_pack_id, model_id) = self
            .stable_key
            .split_once('/')
            .ok_or_else(|| PlacementDocumentError::InvalidStableKey(self.stable_key.clone()))?;
        if stable_pack_id != self.pack_id.to_ascii_lowercase()
            || model_id.contains('/')
            || matches!(model_id, "." | "..")
        {
            return Err(PlacementDocumentError::StableKeyPackMismatch);
        }
        validate_source_field("source_version", &self.source_version)?;
        validate_source_field("author", &self.author)?;
        validate_source_field("license_spdx", &self.license_spdx)?;
        validate_relative_path("source_path", &self.source_path)?;
        validate_relative_path("scene_path", &self.scene_path)?;
        for (field, hash) in [
            ("scene_content_hash", self.scene_content_hash),
            ("manifest_hash", self.manifest_hash),
            ("license_evidence_hash", self.license_evidence_hash),
        ] {
            if hash.as_bytes().iter().all(|byte| *byte == 0) {
                return Err(PlacementDocumentError::ZeroContentHash(field));
            }
        }
        if !self.base_scale.is_finite()
            || !(MIN_ASSET_BASE_SCALE..=MAX_ASSET_BASE_SCALE).contains(&self.base_scale)
        {
            return Err(PlacementDocumentError::InvalidBaseScale(self.base_scale));
        }
        Ok(())
    }
}

/// One persistent instance of a pinned asset.
#[derive(Clone, Copy, Debug, PartialEq, Serialize, Deserialize)]
#[serde(deny_unknown_fields)]
pub struct AuthoredPlacement {
    pub id: EntityId,
    pub asset_id: AssetId,
    pub transform: Transform,
}

impl AuthoredPlacement {
    pub fn validate(self) -> Result<(), PlacementDocumentError> {
        if self.id.as_uuid().is_nil() {
            return Err(PlacementDocumentError::NilIdentity("placement.id"));
        }
        if self.asset_id.as_uuid().is_nil() {
            return Err(PlacementDocumentError::NilIdentity("placement.asset_id"));
        }
        self.transform
            .validate()
            .map_err(|source| PlacementDocumentError::InvalidTransform {
                entity_id: self.id,
                source,
            })?;
        let translation = self.transform.translation_m;
        if translation.x < -PLACEMENT_HORIZONTAL_HALF_EXTENT_M
            || translation.x >= PLACEMENT_HORIZONTAL_HALF_EXTENT_M
            || translation.z < -PLACEMENT_HORIZONTAL_HALF_EXTENT_M
            || translation.z >= PLACEMENT_HORIZONTAL_HALF_EXTENT_M
        {
            return Err(PlacementDocumentError::PlacementOutOfBounds(self.id));
        }
        if [
            self.transform.scale.x,
            self.transform.scale.y,
            self.transform.scale.z,
        ]
        .into_iter()
        .any(|scale| !(MIN_ASSET_BASE_SCALE..=MAX_ASSET_BASE_SCALE).contains(&scale))
        {
            return Err(PlacementDocumentError::PlacementScaleOutOfBounds(self.id));
        }
        Ok(())
    }
}

/// Reproduces the source-governed R2-026 browser identity contract.
#[must_use]
pub fn asset_id_from_stable_key(stable_key: &str) -> AssetId {
    AssetId::deterministic(format!("{ASSET_ID_KEY_PREFIX}:{stable_key}").as_bytes())
}

fn validate_header(document: &PlacementDocument) -> Result<(), PlacementDocumentError> {
    if document.schema != PLACEMENT_DOCUMENT_SCHEMA {
        return Err(PlacementDocumentError::InvalidSchema(
            document.schema.clone(),
        ));
    }
    if document.schema_version != PLACEMENT_DOCUMENT_SCHEMA_VERSION {
        return Err(PlacementDocumentError::UnsupportedVersion(
            document.schema_version,
        ));
    }
    if document.project_id.as_uuid().is_nil() {
        return Err(PlacementDocumentError::NilIdentity("project_id"));
    }
    if document.area_id.as_uuid().is_nil() {
        return Err(PlacementDocumentError::NilIdentity("area_id"));
    }
    if document.revision.get() == 0 {
        return Err(PlacementDocumentError::InvalidRevision);
    }
    if document.coordinate_system != CoordinateSystem::METRES_Y_UP_RIGHT_HANDED {
        return Err(PlacementDocumentError::UnsupportedCoordinateSystem);
    }
    validate_bounded_text("name", &document.name, MAX_NAME_BYTES)
}

fn validate_stable_key(value: &str) -> Result<(), PlacementDocumentError> {
    validate_bounded_text("stable_key", value, MAX_STABLE_KEY_BYTES)?;
    if value.bytes().any(|byte| {
        !(byte.is_ascii_lowercase()
            || byte.is_ascii_digit()
            || matches!(byte, b'.' | b'_' | b'-' | b':' | b'/'))
    }) || value.starts_with('/')
        || value.ends_with('/')
        || !value.contains('/')
    {
        return Err(PlacementDocumentError::InvalidStableKey(value.to_owned()));
    }
    Ok(())
}

fn validate_source_field(field: &'static str, value: &str) -> Result<(), PlacementDocumentError> {
    validate_bounded_text(field, value, MAX_SOURCE_FIELD_BYTES)
}

fn validate_bounded_text(
    field: &'static str,
    value: &str,
    maximum: usize,
) -> Result<(), PlacementDocumentError> {
    if value.trim().is_empty() || value.chars().any(char::is_control) {
        return Err(PlacementDocumentError::InvalidText(field));
    }
    if value.len() > maximum {
        return Err(PlacementDocumentError::TextTooLong {
            field,
            bytes: value.len(),
            maximum,
        });
    }
    Ok(())
}

fn validate_relative_path(field: &'static str, value: &str) -> Result<(), PlacementDocumentError> {
    validate_bounded_text(field, value, MAX_PATH_BYTES)?;
    if value.contains('\\')
        || value.starts_with('/')
        || value.contains(':')
        || value.contains(['%', '?', '#'])
        || value
            .split('/')
            .any(|part| part.is_empty() || part == "." || part == "..")
        || value.to_ascii_lowercase().contains("cadle")
    {
        return Err(PlacementDocumentError::InvalidRelativePath {
            field,
            value: value.to_owned(),
        });
    }
    Ok(())
}

fn ensure_json_budget(bytes: usize) -> Result<(), PlacementDocumentError> {
    if bytes > MAX_PLACEMENT_DOCUMENT_JSON_BYTES {
        Err(PlacementDocumentError::JsonTooLarge {
            bytes,
            maximum: MAX_PLACEMENT_DOCUMENT_JSON_BYTES,
        })
    } else {
        Ok(())
    }
}

#[derive(Debug, Error)]
pub enum PlacementDocumentError {
    #[error("invalid placement document schema `{0}`")]
    InvalidSchema(String),
    #[error("unsupported placement document version {0}")]
    UnsupportedVersion(u32),
    #[error("{0} cannot be the nil UUID")]
    NilIdentity(&'static str),
    #[error("placement document revision must be positive")]
    InvalidRevision,
    #[error("placement document coordinate system must be metres, Y-up, right-handed")]
    UnsupportedCoordinateSystem,
    #[error("{0} cannot be blank or contain control characters")]
    InvalidText(&'static str),
    #[error("{field} contains {bytes} bytes; maximum is {maximum}")]
    TextTooLong {
        field: &'static str,
        bytes: usize,
        maximum: usize,
    },
    #[error("invalid source-governed stable key `{0}`")]
    InvalidStableKey(String),
    #[error("asset {0} does not match its source-governed stable key")]
    AssetIdentityMismatch(AssetId),
    #[error("asset stable key does not begin with its normalized source pack ID")]
    StableKeyPackMismatch,
    #[error("invalid portable relative {field} `{value}`")]
    InvalidRelativePath { field: &'static str, value: String },
    #[error("{0} cannot be the all-zero content hash")]
    ZeroContentHash(&'static str),
    #[error(
        "asset base scale {0} must be finite and inside [{MIN_ASSET_BASE_SCALE}, {MAX_ASSET_BASE_SCALE}]"
    )]
    InvalidBaseScale(f32),
    #[error(
        "placement document supports at most {MAX_ASSET_PINS_PER_DOCUMENT} asset pins, found {0}"
    )]
    TooManyAssetPins(usize),
    #[error("asset pins must be strictly sorted by unique asset ID")]
    AssetPinsNotCanonical,
    #[error("duplicate source-governed stable key `{0}`")]
    DuplicateStableKey(String),
    #[error(
        "placement document supports at most {MAX_AUTHORED_PLACEMENTS_PER_DOCUMENT} instances, found {0}"
    )]
    TooManyPlacements(usize),
    #[error("placements must be strictly sorted by unique entity ID")]
    PlacementsNotCanonical,
    #[error("placement references missing asset pin {0}")]
    MissingAssetPin(AssetId),
    #[error("placement {entity_id} has an invalid transform: {source}")]
    InvalidTransform {
        entity_id: EntityId,
        #[source]
        source: TransformError,
    },
    #[error("placement {0} lies outside the bounded authored area")]
    PlacementOutOfBounds(EntityId),
    #[error("placement {0} scale lies outside the bounded authored range")]
    PlacementScaleOutOfBounds(EntityId),
    #[error("placement JSON contains {bytes} bytes; maximum is {maximum}")]
    JsonTooLarge { bytes: usize, maximum: usize },
    #[error("invalid placement JSON: {0}")]
    Json(#[from] serde_json::Error),
    #[error("cannot hash placement document: {0}")]
    Hash(#[from] CanonicalJsonError),
}

#[cfg(test)]
mod tests {
    use super::*;
    use forge_core::{UnitQuaternion, Vec3};
    use serde_json::json;

    fn pin(key: &str) -> AssetPin {
        let stable_key = format!("pack/{key}");
        AssetPin {
            asset_id: asset_id_from_stable_key(&stable_key),
            stable_key,
            subset_id: "subset-1".to_owned(),
            pack_id: "pack".to_owned(),
            source_version: "1.0".to_owned(),
            author: "Author".to_owned(),
            source_path: format!("gltf/{key}.gltf"),
            scene_path: format!("governed/{key}.gltf"),
            scene_content_hash: ContentHash::from_bytes([1; 32]),
            manifest_hash: ContentHash::from_bytes([2; 32]),
            license_spdx: "CC0-1.0".to_owned(),
            license_evidence_hash: ContentHash::from_bytes([3; 32]),
            base_scale: 1.0,
        }
    }

    fn placement(key: &str, pin: &AssetPin) -> AuthoredPlacement {
        AuthoredPlacement {
            id: EntityId::deterministic(format!("placement/{key}")),
            asset_id: pin.asset_id,
            transform: Transform {
                translation_m: Vec3::new(4.0, 2.0, -3.0),
                rotation: UnitQuaternion::IDENTITY,
                scale: Vec3::ONE,
            },
        }
    }

    fn document(
        mut pins: Vec<AssetPin>,
        mut placements: Vec<AuthoredPlacement>,
    ) -> PlacementDocument {
        pins.sort_by_key(|pin| pin.asset_id);
        placements.sort_by_key(|placement| placement.id);
        PlacementDocument::new(
            ProjectId::deterministic("placement/project"),
            AreaId::deterministic("placement/area"),
            Revision::INITIAL,
            "Placements",
            pins,
            placements,
        )
    }

    #[test]
    fn canonical_round_trip_and_hash_are_stable() {
        let first = pin("tree");
        let second = pin("rock");
        let source = document(
            vec![first.clone(), second.clone()],
            vec![placement("tree", &first), placement("rock", &second)],
        );
        let pretty = source.to_pretty_json().unwrap();
        let compact = serde_json::to_string(&source).unwrap();
        for json in [pretty, compact] {
            let restored = PlacementDocument::from_json(&json).unwrap();
            assert_eq!(restored, source);
            assert_eq!(
                restored.content_hash().unwrap(),
                source.content_hash().unwrap()
            );
        }
    }

    #[test]
    fn canonical_order_duplicates_and_missing_pins_are_rejected() {
        let first = pin("tree");
        let second = pin("rock");
        let mut unordered = document(vec![first.clone(), second.clone()], vec![]);
        unordered.asset_pins.reverse();
        assert!(matches!(
            unordered.validate(),
            Err(PlacementDocumentError::AssetPinsNotCanonical)
        ));

        let mut duplicate_key = second.clone();
        duplicate_key.stable_key.clone_from(&first.stable_key);
        duplicate_key.asset_id = first.asset_id;
        let mut duplicate = document(vec![first.clone()], vec![]);
        duplicate.asset_pins.push(duplicate_key);
        assert!(matches!(
            duplicate.validate(),
            Err(PlacementDocumentError::AssetPinsNotCanonical)
        ));

        let first_placement = placement("tree", &first);
        let second_placement = placement("rock", &second);
        let mut unordered = document(
            vec![first.clone(), second],
            vec![first_placement, second_placement],
        );
        unordered.placements.reverse();
        assert!(matches!(
            unordered.validate(),
            Err(PlacementDocumentError::PlacementsNotCanonical)
        ));
        let mut duplicate = document(vec![first.clone()], vec![first_placement]);
        duplicate.placements.push(first_placement);
        assert!(matches!(
            duplicate.validate(),
            Err(PlacementDocumentError::PlacementsNotCanonical)
        ));

        let mut missing = document(vec![], vec![placement("tree", &first)]);
        missing.placements.sort_by_key(|candidate| candidate.id);
        assert!(matches!(
            missing.validate(),
            Err(PlacementDocumentError::MissingAssetPin(_))
        ));
    }

    #[test]
    fn pins_bind_identity_paths_hashes_and_scale() {
        let valid = pin("tree");
        valid.validate().unwrap();
        let mut invalid = valid.clone();
        invalid.asset_id = AssetId::deterministic("forged");
        assert!(matches!(
            invalid.validate(),
            Err(PlacementDocumentError::AssetIdentityMismatch(_))
        ));
        let mut invalid = valid.clone();
        invalid.pack_id = "other-pack".to_owned();
        assert!(matches!(
            invalid.validate(),
            Err(PlacementDocumentError::StableKeyPackMismatch)
        ));
        for path in [
            "/absolute.gltf",
            "../escape.gltf",
            "a\\b.gltf",
            "cadle/model.gltf",
            "pack/tree#variant.gltf",
            "pack/tree?variant.gltf",
            "pack/tree%23variant.gltf",
        ] {
            let mut invalid = valid.clone();
            invalid.scene_path = path.to_owned();
            assert!(matches!(
                invalid.validate(),
                Err(PlacementDocumentError::InvalidRelativePath { .. })
            ));
        }
        let mut invalid = valid.clone();
        invalid.manifest_hash = ContentHash::from_bytes([0; 32]);
        assert!(matches!(
            invalid.validate(),
            Err(PlacementDocumentError::ZeroContentHash("manifest_hash"))
        ));
        for scale in [
            f32::NAN,
            MIN_ASSET_BASE_SCALE / 2.0,
            MAX_ASSET_BASE_SCALE * 2.0,
        ] {
            let mut invalid = valid.clone();
            invalid.base_scale = scale;
            assert!(matches!(
                invalid.validate(),
                Err(PlacementDocumentError::InvalidBaseScale(_))
            ));
        }
    }

    #[test]
    fn transforms_must_be_finite_normalized_and_bounded() {
        let pin = pin("tree");
        let source = placement("tree", &pin);
        for mutation in 0..4 {
            let mut invalid = source;
            match mutation {
                0 => invalid.transform.translation_m.x = f32::NAN,
                1 => invalid.transform.rotation.w = 0.5,
                2 => invalid.transform.translation_m.x = PLACEMENT_HORIZONTAL_HALF_EXTENT_M,
                _ => invalid.transform.scale.x = MAX_ASSET_BASE_SCALE + 1.0,
            }
            assert!(invalid.validate().is_err());
        }
    }

    #[test]
    fn schema_json_and_collection_limits_are_enforced() {
        let source = document(vec![], vec![]);
        let mut value = serde_json::to_value(&source).unwrap();
        value["unknown"] = json!(true);
        assert!(matches!(
            PlacementDocument::from_json(&value.to_string()),
            Err(PlacementDocumentError::Json(_))
        ));
        assert!(matches!(
            PlacementDocument::from_json(&" ".repeat(MAX_PLACEMENT_DOCUMENT_JSON_BYTES + 1)),
            Err(PlacementDocumentError::JsonTooLarge { .. })
        ));

        let pins = (0..=MAX_ASSET_PINS_PER_DOCUMENT)
            .map(|index| pin(&format!("asset-{index:03}")))
            .collect();
        assert!(
            matches!(document(pins, vec![]).validate(), Err(PlacementDocumentError::TooManyAssetPins(count)) if count == MAX_ASSET_PINS_PER_DOCUMENT + 1)
        );

        let pin = pin("shared");
        let placements = (0..=MAX_AUTHORED_PLACEMENTS_PER_DOCUMENT)
            .map(|index| placement(&format!("instance-{index:04}"), &pin))
            .collect();
        assert!(
            matches!(document(vec![pin], placements).validate(), Err(PlacementDocumentError::TooManyPlacements(count)) if count == MAX_AUTHORED_PLACEMENTS_PER_DOCUMENT + 1)
        );
    }
}
