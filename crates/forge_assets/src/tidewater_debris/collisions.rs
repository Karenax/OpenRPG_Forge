//! Hash-pinned source collider frames, independent of ECS and physics backends.
use forge_core::{ContentHash, bytes_sha256};
use serde::Deserialize;
use std::collections::{BTreeMap, BTreeSet};

const PIN: &str = "8f46a42c3c94fdea04fba2174bc7c85a26c0f56c61887b1c7b8a017c185c7ac2";

/// Intermediate collision revision; no contact, gameplay or licence qualification.
///
/// # Panics
/// Panics only if the compiled SHA-256 literal is invalid.
#[must_use]
pub fn revision() -> ContentHash {
    PIN.parse().expect("Pinned Debris collision hash is valid")
}

#[derive(Clone, Copy, Debug, Deserialize, PartialEq, Eq)]
#[serde(rename_all = "lowercase")]
pub enum Category {
    Procedural,
    Scanned,
}

#[derive(Clone, Debug, Deserialize)]
#[serde(deny_unknown_fields)]
pub struct Frame {
    pub translation_m: [f64; 3],
    pub rotation_xyzw: [f64; 4],
    pub scale: [f64; 3],
}

#[derive(Clone, Debug, Deserialize)]
#[serde(tag = "shape", rename_all = "snake_case", deny_unknown_fields)]
pub enum LocalShape {
    Box {
        translation_m: [f64; 3],
        half_m: [f64; 3],
        rotation_y_rad: f64,
        solid: bool,
        walkable: bool,
    },
    Cylinder {
        translation_m: [f64; 3],
        radius_m: f64,
        half_height_m: f64,
    },
    ConvexBox {
        vertices: Vec<[f32; 3]>,
        indices: Vec<u32>,
        solid: bool,
        walkable: bool,
    },
}

#[derive(Clone, Debug, Deserialize)]
#[serde(deny_unknown_fields)]
pub struct Primitive {
    pub source_shape: String,
    pub source_index: usize,
    pub tag: String,
    pub local: LocalShape,
}

#[derive(Clone, Debug, Deserialize)]
#[serde(deny_unknown_fields)]
pub struct Owner {
    pub source_id: String,
    pub category: Category,
    pub source_frame: Frame,
    pub source_matrix_column_major: [f64; 16],
    pub colliders: Vec<Primitive>,
}

#[derive(Deserialize)]
#[serde(deny_unknown_fields)]
struct Document {
    schema: String,
    version: u32,
    source_commit: String,
    debris_package_sha256: ContentHash,
    source_ownership_sha256: ContentHash,
    source_colliders_sha256: ContentHash,
    scanned_geometry_sha256: ContentHash,
    ownership_receipt_sha256: ContentHash,
    preparation_sha256: ContentHash,
    owners: Vec<Owner>,
    counts: BTreeMap<String, usize>,
    max_float32_local_reconstruction_error_m: f64,
    limits: Vec<String>,
}

/// Immutable source references retained for geometry and acquisition audits.
#[derive(Clone, Debug)]
pub struct Provenance {
    pub source_ownership: ContentHash,
    pub source_colliders: ContentHash,
    pub scanned_geometry: ContentHash,
    pub ownership_receipt: ContentHash,
    pub preparation: ContentHash,
}

pub struct CollisionFrames {
    owners: Vec<Owner>,
    by_id: BTreeMap<String, usize>,
    provenance: Provenance,
}

impl CollisionFrames {
    /// Decode the verified local collision artifact without transport or renderer dependencies.
    ///
    /// # Errors
    /// Rejects oversize/altered bytes, wrong source revisions or incomplete primitive ownership.
    pub fn load(raw: &[u8]) -> Result<Self, String> {
        if raw.len() > 1_048_576 || bytes_sha256(raw) != revision() {
            return Err("Debris collision artifact size or hash differs".into());
        }
        let document: Document = serde_json::from_slice(raw).map_err(|error| error.to_string())?;
        validate(&document)?;
        let by_id = document
            .owners
            .iter()
            .enumerate()
            .map(|(index, owner)| (owner.source_id.clone(), index))
            .collect();
        Ok(Self {
            provenance: Provenance {
                source_ownership: document.source_ownership_sha256,
                source_colliders: document.source_colliders_sha256,
                scanned_geometry: document.scanned_geometry_sha256,
                ownership_receipt: document.ownership_receipt_sha256,
                preparation: document.preparation_sha256,
            },
            owners: document.owners,
            by_id,
        })
    }

    #[must_use]
    pub fn owners(&self) -> &[Owner] {
        &self.owners
    }

    #[must_use]
    pub fn provenance(&self) -> &Provenance {
        &self.provenance
    }

    #[must_use]
    pub fn owner(&self, source_id: &str) -> Option<&Owner> {
        self.by_id.get(source_id).map(|&index| &self.owners[index])
    }
}

fn bounded(values: &[f64]) -> bool {
    values
        .iter()
        .all(|value| value.is_finite() && value.abs() < 1_000_000.0)
}

fn validate(document: &Document) -> Result<(), String> {
    if document.schema != "forge.tidewater.debris-collision-frames"
        || document.version != 1
        || document.source_commit != super::COMMIT
        || document.debris_package_sha256 != super::package_hash()
        || document.owners.len() != 946
        || !document
            .max_float32_local_reconstruction_error_m
            .is_finite()
        || !(0.0..0.0001).contains(&document.max_float32_local_reconstruction_error_m)
        || document.limits.is_empty()
    {
        return Err("Debris collision source contract differs".into());
    }
    let mut primitives = BTreeSet::new();
    let mut counts = BTreeMap::new();
    for (index, owner) in document.owners.iter().enumerate() {
        let (category, ordinal, label) = if index < 805 {
            (Category::Procedural, index, "debris-cpu-owner")
        } else {
            (Category::Scanned, index - 805, "debris-scanned")
        };
        validate_owner_frame(owner, category, ordinal, label)?;
        for primitive in &owner.colliders {
            if primitive.tag.len() > 64
                || !primitives.insert((primitive.source_shape.as_str(), primitive.source_index))
            {
                return Err("Duplicate or invalid collision primitive".into());
            }
            let kind = match &primitive.local {
                LocalShape::Box {
                    translation_m,
                    half_m,
                    rotation_y_rad,
                    ..
                } => {
                    if category != Category::Procedural
                        || primitive.source_shape != "box"
                        || !bounded(translation_m)
                        || !bounded(half_m)
                        || half_m.iter().any(|value| *value <= 0.0)
                        || !rotation_y_rad.is_finite()
                    {
                        return Err("Invalid local box".into());
                    }
                    "box"
                }
                LocalShape::Cylinder {
                    translation_m,
                    radius_m,
                    half_height_m,
                } => {
                    if category != Category::Procedural
                        || primitive.source_shape != "cylinder"
                        || !bounded(translation_m)
                        || !bounded(&[*radius_m, *half_height_m])
                        || *radius_m <= 0.0
                        || *half_height_m <= 0.0
                    {
                        return Err("Invalid local cylinder".into());
                    }
                    "cylinder"
                }
                LocalShape::ConvexBox {
                    vertices, indices, ..
                } => {
                    if category != Category::Scanned
                        || primitive.source_shape != "box"
                        || vertices.len() != 8
                        || indices.len() != 36
                        || vertices.iter().flatten().any(|value| !value.is_finite())
                        || indices.iter().any(|&value| value >= 8)
                    {
                        return Err("Invalid local convex box".into());
                    }
                    "convex_box"
                }
            };
            *counts.entry(kind.to_owned()).or_insert(0) += 1;
        }
    }
    let expected: BTreeSet<_> = (0..86)
        .map(|index| ("box", index))
        .chain((0..25).map(|index| ("cylinder", index)))
        .collect();
    if primitives != expected
        || counts != document.counts
        || counts
            != BTreeMap::from([
                ("box".into(), 26),
                ("cylinder".into(), 25),
                ("convex_box".into(), 60),
            ])
    {
        return Err("Debris collision population differs".into());
    }
    Ok(())
}

#[allow(
    clippy::float_cmp,
    reason = "Hash-pinned procedural frames must have exactly identity rotation and unit scale."
)]
fn validate_owner_frame(
    owner: &Owner,
    category: Category,
    ordinal: usize,
    label: &str,
) -> Result<(), String> {
    if owner.category != category
        || owner.source_id != format!("tidewater:{}:{label}:{ordinal}", super::COMMIT)
        || !bounded(&owner.source_frame.translation_m)
        || !bounded(&owner.source_frame.rotation_xyzw)
        || !bounded(&owner.source_frame.scale)
        || owner.source_frame.scale.iter().any(|value| *value <= 0.0)
        || (owner
            .source_frame
            .rotation_xyzw
            .iter()
            .map(|value| value * value)
            .sum::<f64>()
            - 1.0)
            .abs()
            > 1e-12
        || !bounded(&owner.source_matrix_column_major)
        || owner.colliders.len() > 16
    {
        return Err("Debris collision owner frame differs".into());
    }
    if category == Category::Procedural
        && (owner.source_frame.rotation_xyzw != [0.0, 0.0, 0.0, 1.0]
            || owner.source_frame.scale != [1.0; 3])
    {
        return Err("Procedural collision source frame differs".into());
    }
    validate_matrix(owner)
}

fn validate_matrix(owner: &Owner) -> Result<(), String> {
    let [x, y, z, w] = owner.source_frame.rotation_xyzw;
    let columns = [
        [
            1.0 - 2.0 * (y * y + z * z),
            2.0 * (x * y + z * w),
            2.0 * (x * z - y * w),
        ],
        [
            2.0 * (x * y - z * w),
            1.0 - 2.0 * (x * x + z * z),
            2.0 * (y * z + x * w),
        ],
        [
            2.0 * (x * z + y * w),
            2.0 * (y * z - x * w),
            1.0 - 2.0 * (x * x + y * y),
        ],
    ];
    let mut expected = [0.0; 16];
    expected[15] = 1.0;
    for column in 0..3 {
        for row in 0..3 {
            expected[column * 4 + row] = columns[column][row] * owner.source_frame.scale[column];
        }
    }
    expected[12..15].copy_from_slice(&owner.source_frame.translation_m);
    if expected
        .iter()
        .zip(owner.source_matrix_column_major)
        .any(|(a, b)| (a - b).abs() > 1e-12)
    {
        return Err("Collision matrix disagrees with its source TRS".into());
    }
    Ok(())
}

#[cfg(test)]
mod tests {
    use super::*;

    #[test]
    #[ignore = "Requires the actual acquired collision-frame fixture"]
    fn real_frames_preserve_ownership_and_refuse_altered_data() {
        let root = std::env::var("FORGE_DEBRIS_COLLISION_FIXTURE").unwrap();
        let raw = std::fs::read(std::path::Path::new(&root).join("collision-frames.json")).unwrap();
        let loaded = CollisionFrames::load(&raw).unwrap();
        assert_eq!(loaded.owners().len(), 946);
        assert_eq!(
            loaded.owners()[..805]
                .iter()
                .map(|owner| owner.colliders.len())
                .sum::<usize>(),
            51
        );
        let mut changed = raw.clone();
        changed[0] ^= 1;
        assert!(CollisionFrames::load(&changed).is_err());
        let mut document: Document = serde_json::from_slice(&raw).unwrap();
        document.owners[0].source_matrix_column_major[12] += 0.25;
        assert!(validate(&document).is_err());
    }
}
