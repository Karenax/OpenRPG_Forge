//! Verified local debris collision frames for module assets.
use forge_core::{ContentHash, bytes_sha256};
use serde::Deserialize;
use std::collections::{BTreeMap, BTreeSet};

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
    source_commit: String,
    debris_package: ContentHash,
    by_id: BTreeMap<String, usize>,
    provenance: Provenance,
}

impl CollisionFrames {
    /// Decode the verified local collision artifact without transport or renderer dependencies.
    ///
    /// # Errors
    /// Rejects oversize/altered bytes, wrong source revisions or incomplete primitive ownership.
    pub fn load(raw: &[u8], expected_revision: ContentHash) -> Result<Self, String> {
        if raw.len() > 1_048_576 || bytes_sha256(raw) != expected_revision {
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
            source_commit: document.source_commit,
            debris_package: document.debris_package_sha256,
            by_id,
        })
    }

    #[must_use]
    pub fn source_commit(&self) -> &str {
        &self.source_commit
    }

    #[must_use]
    pub const fn debris_package(&self) -> ContentHash {
        self.debris_package
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

    /// Verify that scanned collision ownership matches the exact decoded placement frames.
    ///
    /// # Errors
    /// Rejects missing or extra scanned owners and any mismatch of the immutable local frame.
    #[allow(
        clippy::float_cmp,
        reason = "Two representations of the same pinned source frame must agree exactly."
    )]
    pub fn validate_scanned(
        &self,
        geometry: &crate::source_scanned_package::geometry::Geometry,
    ) -> Result<(), String> {
        let owner_count = self
            .owners
            .iter()
            .filter(|owner| owner.category == Category::Scanned)
            .count();
        if owner_count != geometry.instances.len() {
            return Err("Scanned collision owner count differs".into());
        }
        for instance in &geometry.instances {
            let source = &instance.source;
            let owner = self
                .owner(&instance.source_id)
                .ok_or("Scanned collision owner absent")?;
            if owner.category != Category::Scanned
                || owner.source_frame.translation_m != [source.x, source.y, source.z]
                || owner.source_frame.rotation_xyzw != instance.quaternion_xyzw
                || owner.source_frame.scale != [source.sx, source.sy, source.sz]
                || owner.source_matrix_column_major != instance.matrix_column_major
            {
                return Err(format!(
                    "Scanned collision frame differs: {}",
                    instance.source_id
                ));
            }
        }
        Ok(())
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
        || document.source_commit.is_empty()
        || document.source_commit.len() > 128
        || document.owners.is_empty()
        || document.owners.len() > 16_384
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
    let mut owners = BTreeSet::new();
    for owner in &document.owners {
        if !owners.insert(owner.source_id.as_str()) {
            return Err("Duplicate collision owner".into());
        }
        validate_owner_frame(owner)?;
        let category = owner.category;
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
    if counts != document.counts {
        return Err("Debris collision population differs".into());
    }
    Ok(())
}

#[allow(
    clippy::float_cmp,
    reason = "Hash-pinned procedural frames must have exactly identity rotation and unit scale."
)]
fn validate_owner_frame(owner: &Owner) -> Result<(), String> {
    if owner.source_id.is_empty()
        || owner.source_id.len() > 256
        || owner.source_id.chars().any(char::is_control)
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
    if owner.category == Category::Procedural
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
