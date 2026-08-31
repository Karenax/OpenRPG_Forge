#![deny(unsafe_code)]

//! Engine-neutral primitives shared by Forge documents and tools.

mod frame_distribution;

pub use frame_distribution::{
    FRAME_DISTRIBUTION_SCHEMA, FRAME_DISTRIBUTION_SCHEMA_VERSION, FRAME_TIME_30_FPS_BUDGET_MS,
    FRAME_TIME_60_FPS_BUDGET_MS, FrameDistribution, FrameDistributionError, FrameTimeHistogram,
};

use serde::{Deserialize, Deserializer, Serialize, Serializer};
use sha2::{Digest, Sha256};
use std::fmt;
use std::str::FromStr;
use thiserror::Error;
use uuid::Uuid;

/// Stable namespace used for deterministic Forge identities.
pub const FORGE_ID_NAMESPACE: Uuid = Uuid::from_u128(0x9e65f3d9_f8e2_5f10_8d1e_3e0b6e4d7c21);

/// Error returned when a persistent identity cannot be parsed.
#[derive(Debug, Error)]
#[error("invalid {kind}: {source}")]
pub struct IdParseError {
    kind: &'static str,
    #[source]
    source: uuid::Error,
}

macro_rules! domain_id {
    ($name:ident, $kind:literal, $domain:literal) => {
        #[doc = concat!("Persistent ", $kind, " identity, independent from engine entities.")]
        #[derive(
            Clone, Copy, Debug, Eq, Hash, Ord, PartialEq, PartialOrd, Serialize, Deserialize,
        )]
        #[serde(transparent)]
        pub struct $name(Uuid);

        impl $name {
            #[must_use]
            pub const fn from_uuid(value: Uuid) -> Self {
                Self(value)
            }

            #[must_use]
            pub const fn as_uuid(self) -> Uuid {
                self.0
            }

            /// Produces the same identifier for the same domain key on every platform.
            #[must_use]
            pub fn deterministic(key: impl AsRef<[u8]>) -> Self {
                let mut scoped = concat!("openrpg-forge:", $domain, ":").as_bytes().to_vec();
                scoped.extend_from_slice(key.as_ref());
                Self(Uuid::new_v5(&FORGE_ID_NAMESPACE, &scoped))
            }
        }

        impl fmt::Display for $name {
            fn fmt(&self, formatter: &mut fmt::Formatter<'_>) -> fmt::Result {
                self.0.fmt(formatter)
            }
        }

        impl FromStr for $name {
            type Err = IdParseError;

            fn from_str(value: &str) -> Result<Self, Self::Err> {
                Uuid::parse_str(value)
                    .map(Self)
                    .map_err(|source| IdParseError {
                        kind: $kind,
                        source,
                    })
            }
        }
    };
}

domain_id!(ProjectId, "project id", "project");
domain_id!(AreaId, "area id", "area");
domain_id!(EntityId, "entity id", "entity");
domain_id!(AssetId, "asset id", "asset");

/// Monotone document revision within a workspace.
#[derive(Clone, Copy, Debug, Eq, Hash, Ord, PartialEq, PartialOrd, Serialize, Deserialize)]
#[serde(transparent)]
pub struct Revision(u64);

impl Revision {
    pub const INITIAL: Self = Self(1);

    #[must_use]
    pub const fn new(value: u64) -> Self {
        Self(value)
    }

    #[must_use]
    pub const fn get(self) -> u64 {
        self.0
    }

    /// Advances the revision without silently wrapping.
    pub fn next(self) -> Result<Self, RevisionError> {
        self.0
            .checked_add(1)
            .map(Self)
            .ok_or(RevisionError::Overflow)
    }
}

#[derive(Clone, Copy, Debug, Error, Eq, PartialEq)]
pub enum RevisionError {
    #[error("revision overflow")]
    Overflow,
}

/// Three-dimensional vector expressed in Forge world units.
#[derive(Clone, Copy, Debug, PartialEq, Serialize, Deserialize)]
#[serde(deny_unknown_fields)]
pub struct Vec3 {
    pub x: f32,
    pub y: f32,
    pub z: f32,
}

impl Vec3 {
    pub const ZERO: Self = Self::new(0.0, 0.0, 0.0);
    pub const ONE: Self = Self::new(1.0, 1.0, 1.0);
    pub const Y: Self = Self::new(0.0, 1.0, 0.0);

    #[must_use]
    pub const fn new(x: f32, y: f32, z: f32) -> Self {
        Self { x, y, z }
    }

    #[must_use]
    pub fn is_finite(self) -> bool {
        self.x.is_finite() && self.y.is_finite() && self.z.is_finite()
    }
}

/// Unit quaternion in `(x, y, z, w)` order.
#[derive(Clone, Copy, Debug, PartialEq, Serialize, Deserialize)]
#[serde(deny_unknown_fields)]
pub struct UnitQuaternion {
    pub x: f32,
    pub y: f32,
    pub z: f32,
    pub w: f32,
}

impl UnitQuaternion {
    pub const IDENTITY: Self = Self::new(0.0, 0.0, 0.0, 1.0);

    #[must_use]
    pub const fn new(x: f32, y: f32, z: f32, w: f32) -> Self {
        Self { x, y, z, w }
    }

    #[must_use]
    pub fn is_finite_and_normalized(self) -> bool {
        if !(self.x.is_finite() && self.y.is_finite() && self.z.is_finite() && self.w.is_finite()) {
            return false;
        }
        let length_squared = self.x * self.x + self.y * self.y + self.z * self.z + self.w * self.w;
        (length_squared - 1.0).abs() <= 1.0e-4
    }
}

/// Persistent transform: metres, Y-up, right-handed.
#[derive(Clone, Copy, Debug, PartialEq, Serialize, Deserialize)]
#[serde(deny_unknown_fields)]
pub struct Transform {
    pub translation_m: Vec3,
    pub rotation: UnitQuaternion,
    pub scale: Vec3,
}

impl Transform {
    pub const IDENTITY: Self = Self {
        translation_m: Vec3::ZERO,
        rotation: UnitQuaternion::IDENTITY,
        scale: Vec3::ONE,
    };

    #[must_use]
    pub const fn from_translation(translation_m: Vec3) -> Self {
        Self {
            translation_m,
            ..Self::IDENTITY
        }
    }

    pub fn validate(self) -> Result<(), TransformError> {
        if !self.translation_m.is_finite() {
            return Err(TransformError::NonFiniteTranslation);
        }
        if !self.rotation.is_finite_and_normalized() {
            return Err(TransformError::InvalidRotation);
        }
        if !self.scale.is_finite()
            || self.scale.x <= 0.0
            || self.scale.y <= 0.0
            || self.scale.z <= 0.0
        {
            return Err(TransformError::InvalidScale);
        }
        Ok(())
    }
}

#[derive(Clone, Copy, Debug, Error, Eq, PartialEq)]
pub enum TransformError {
    #[error("transform translation must contain only finite metre values")]
    NonFiniteTranslation,
    #[error("transform rotation must be a finite unit quaternion")]
    InvalidRotation,
    #[error("transform scale must contain only finite, strictly positive values")]
    InvalidScale,
}

#[derive(Clone, Copy, Debug, Eq, PartialEq, Serialize, Deserialize)]
#[serde(rename_all = "snake_case")]
pub enum LengthUnit {
    Metre,
}

#[derive(Clone, Copy, Debug, Eq, PartialEq, Serialize, Deserialize)]
#[serde(rename_all = "snake_case")]
pub enum Axis {
    Y,
}

#[derive(Clone, Copy, Debug, Eq, PartialEq, Serialize, Deserialize)]
#[serde(rename_all = "snake_case")]
pub enum Handedness {
    Right,
}

/// Explicit coordinate contract stored in versioned documents.
#[derive(Clone, Copy, Debug, Eq, PartialEq, Serialize, Deserialize)]
#[serde(deny_unknown_fields)]
pub struct CoordinateSystem {
    pub length_unit: LengthUnit,
    pub up_axis: Axis,
    pub handedness: Handedness,
}

impl CoordinateSystem {
    pub const METRES_Y_UP_RIGHT_HANDED: Self = Self {
        length_unit: LengthUnit::Metre,
        up_axis: Axis::Y,
        handedness: Handedness::Right,
    };
}

/// SHA-256 content digest serialized as 64 lowercase hexadecimal characters.
#[derive(Clone, Copy, Debug, Eq, Hash, Ord, PartialEq, PartialOrd)]
pub struct ContentHash([u8; 32]);

impl ContentHash {
    #[must_use]
    pub const fn from_bytes(bytes: [u8; 32]) -> Self {
        Self(bytes)
    }

    #[must_use]
    pub const fn as_bytes(&self) -> &[u8; 32] {
        &self.0
    }

    pub fn parse(value: &str) -> Result<Self, ContentHashParseError> {
        if value.len() != 64 {
            return Err(ContentHashParseError::InvalidLength(value.len()));
        }
        let mut bytes = [0_u8; 32];
        let (pairs, remainder) = value.as_bytes().as_chunks::<2>();
        debug_assert!(remainder.is_empty());
        for (index, pair) in pairs.iter().enumerate() {
            let high = decode_hex(pair[0]).ok_or(ContentHashParseError::InvalidHex)?;
            let low = decode_hex(pair[1]).ok_or(ContentHashParseError::InvalidHex)?;
            bytes[index] = (high << 4) | low;
        }
        Ok(Self(bytes))
    }
}

impl fmt::Display for ContentHash {
    fn fmt(&self, formatter: &mut fmt::Formatter<'_>) -> fmt::Result {
        for byte in self.0 {
            write!(formatter, "{byte:02x}")?;
        }
        Ok(())
    }
}

impl FromStr for ContentHash {
    type Err = ContentHashParseError;

    fn from_str(value: &str) -> Result<Self, Self::Err> {
        Self::parse(value)
    }
}

impl Serialize for ContentHash {
    fn serialize<S>(&self, serializer: S) -> Result<S::Ok, S::Error>
    where
        S: Serializer,
    {
        serializer.collect_str(self)
    }
}

impl<'de> Deserialize<'de> for ContentHash {
    fn deserialize<D>(deserializer: D) -> Result<Self, D::Error>
    where
        D: Deserializer<'de>,
    {
        let value = String::deserialize(deserializer)?;
        Self::parse(&value).map_err(serde::de::Error::custom)
    }
}

#[derive(Clone, Copy, Debug, Error, Eq, PartialEq)]
pub enum ContentHashParseError {
    #[error("content hash must contain 64 hexadecimal characters, got {0}")]
    InvalidLength(usize),
    #[error("content hash contains a non-hexadecimal character")]
    InvalidHex,
}

#[derive(Debug, Error)]
pub enum CanonicalJsonError {
    #[error("cannot serialize value as JSON: {0}")]
    Serialize(#[from] serde_json::Error),
}

/// Serializes a value using stable object-key ordering and no insignificant whitespace.
pub fn canonical_json_bytes<T: Serialize>(value: &T) -> Result<Vec<u8>, CanonicalJsonError> {
    let value = serde_json::to_value(value)?;
    let mut output = String::new();
    write_canonical_value(&value, &mut output)?;
    Ok(output.into_bytes())
}

/// Computes a deterministic SHA-256 over the canonical JSON representation.
pub fn canonical_json_sha256<T: Serialize>(value: &T) -> Result<ContentHash, CanonicalJsonError> {
    let bytes = canonical_json_bytes(value)?;
    Ok(ContentHash::from_bytes(Sha256::digest(bytes).into()))
}

fn write_canonical_value(
    value: &serde_json::Value,
    output: &mut String,
) -> Result<(), serde_json::Error> {
    match value {
        serde_json::Value::Null => output.push_str("null"),
        serde_json::Value::Bool(value) => output.push_str(if *value { "true" } else { "false" }),
        serde_json::Value::Number(value) => output.push_str(&value.to_string()),
        serde_json::Value::String(value) => output.push_str(&serde_json::to_string(value)?),
        serde_json::Value::Array(values) => {
            output.push('[');
            for (index, item) in values.iter().enumerate() {
                if index != 0 {
                    output.push(',');
                }
                write_canonical_value(item, output)?;
            }
            output.push(']');
        }
        serde_json::Value::Object(values) => {
            output.push('{');
            let mut entries: Vec<_> = values.iter().collect();
            entries.sort_unstable_by_key(|(key, _)| *key);
            for (index, (key, item)) in entries.into_iter().enumerate() {
                if index != 0 {
                    output.push(',');
                }
                output.push_str(&serde_json::to_string(key)?);
                output.push(':');
                write_canonical_value(item, output)?;
            }
            output.push('}');
        }
    }
    Ok(())
}

fn decode_hex(byte: u8) -> Option<u8> {
    match byte {
        b'0'..=b'9' => Some(byte - b'0'),
        b'a'..=b'f' => Some(byte - b'a' + 10),
        b'A'..=b'F' => Some(byte - b'A' + 10),
        _ => None,
    }
}

#[cfg(test)]
mod tests {
    use super::*;
    use serde_json::json;

    #[test]
    fn deterministic_ids_are_stable_and_domain_separated() {
        let first = EntityId::deterministic("scene/cube");
        let second = EntityId::deterministic("scene/cube");
        let asset = AssetId::deterministic("scene/cube");

        assert_eq!(first, second);
        assert_ne!(first.as_uuid(), asset.as_uuid());
        assert_eq!(first.to_string().parse::<EntityId>().unwrap(), first);
    }

    #[test]
    fn revision_never_wraps() {
        assert_eq!(Revision::INITIAL.next().unwrap(), Revision::new(2));
        assert_eq!(Revision::new(u64::MAX).next(), Err(RevisionError::Overflow));
    }

    #[test]
    fn transform_rejects_invalid_persistent_values() {
        assert!(Transform::IDENTITY.validate().is_ok());
        let invalid = Transform {
            scale: Vec3::new(1.0, 0.0, 1.0),
            ..Transform::IDENTITY
        };
        assert_eq!(invalid.validate(), Err(TransformError::InvalidScale));
    }

    #[test]
    fn canonical_hash_ignores_object_insertion_order() {
        let left = json!({"z": 1, "nested": {"b": true, "a": false}});
        let right = json!({"nested": {"a": false, "b": true}, "z": 1});

        assert_eq!(
            canonical_json_sha256(&left).unwrap(),
            canonical_json_sha256(&right).unwrap()
        );
        assert_eq!(
            canonical_json_bytes(&left).unwrap(),
            br#"{"nested":{"a":false,"b":true},"z":1}"#
        );
    }

    #[test]
    fn content_hash_round_trips_as_lowercase_hex() {
        let hash = canonical_json_sha256(&json!({"gate": 0})).unwrap();
        let text = hash.to_string();

        assert_eq!(text.len(), 64);
        assert_eq!(
            text,
            "a89fb628e71532fef77b4e516b25d975cc190aeb590cc5e77f46a6c7c76d6aa8"
        );
        assert_eq!(ContentHash::parse(&text).unwrap(), hash);
        assert_eq!(
            serde_json::from_str::<ContentHash>(&serde_json::to_string(&hash).unwrap()).unwrap(),
            hash
        );
    }
}
