//! Original Tidewater collision revision and asset inventory.
use forge_core::ContentHash;
use std::{collections::BTreeSet, ops::Deref};

pub use forge_assets::source_debris_collisions::{
    Category, Frame, LocalShape, Owner, Primitive, Provenance,
};

const PIN: &str = "8f46a42c3c94fdea04fba2174bc7c85a26c0f56c61887b1c7b8a017c185c7ac2";

/// Intermediate collision revision; no contact, gameplay or licence qualification.
///
/// # Panics
/// Panics only if the compiled SHA-256 literal is invalid.
#[must_use]
pub fn revision() -> ContentHash {
    PIN.parse().expect("Pinned Debris collision hash is valid")
}

/// The original acquisition keeps its exact collision revision and source inventory checks.
pub struct CollisionFrames(forge_assets::source_debris_collisions::CollisionFrames);

impl CollisionFrames {
    /// Read the exact Tidewater artifact using the common collision reader.
    ///
    /// # Errors
    /// Refuses malformed frames and drift in the reference identity or the ordered inventory.
    pub fn load(raw: &[u8]) -> Result<Self, String> {
        let frames =
            forge_assets::source_debris_collisions::CollisionFrames::load(raw, revision())?;
        if frames.source_commit() != super::COMMIT
            || frames.debris_package() != super::package_hash()
            || frames.owners().len() != 946
        {
            return Err("Debris collision source contract differs".into());
        }
        let mut primitives = BTreeSet::new();
        let mut population = [0; 3];
        for (index, owner) in frames.owners().iter().enumerate() {
            let (category, ordinal, label) = if index < 805 {
                (Category::Procedural, index, "debris-cpu-owner")
            } else {
                (Category::Scanned, index - 805, "debris-scanned")
            };
            if owner.category != category
                || owner.source_id != format!("tidewater:{}:{label}:{ordinal}", super::COMMIT)
            {
                return Err("Debris collision owner identity differs".into());
            }
            for primitive in &owner.colliders {
                primitives.insert((primitive.source_shape.as_str(), primitive.source_index));
                let slot = match &primitive.local {
                    LocalShape::Box { .. } => 0,
                    LocalShape::Cylinder { .. } => 1,
                    LocalShape::ConvexBox { .. } => 2,
                };
                population[slot] += 1;
            }
        }
        let expected: BTreeSet<_> = (0..86)
            .map(|index| ("box", index))
            .chain((0..25).map(|index| ("cylinder", index)))
            .collect();
        if primitives != expected || population != [26, 25, 60] {
            return Err("Debris collision population differs".into());
        }
        Ok(Self(frames))
    }
}

impl Deref for CollisionFrames {
    type Target = forge_assets::source_debris_collisions::CollisionFrames;

    fn deref(&self) -> &Self::Target {
        &self.0
    }
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
        let mut document: serde_json::Value = serde_json::from_slice(&raw).unwrap();
        let translation = &mut document["owners"][0]["source_matrix_column_major"][12];
        *translation = (translation.as_f64().unwrap() + 0.25).into();
        let altered = serde_json::to_vec(&document).unwrap();
        assert!(
            forge_assets::source_debris_collisions::CollisionFrames::load(
                &altered,
                forge_core::bytes_sha256(&altered),
            )
            .err()
            .unwrap()
            .contains("Collision matrix")
        );
    }
}
