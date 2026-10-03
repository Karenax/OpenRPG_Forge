//! A session choice is a reload hint, never an exemption from package verification.
use forge_core::{AreaId, ContentHash};
use forge_project::{TerrainDocument, sampled_terrain::SampledTerrainSource};
use serde::{Deserialize, Serialize};

#[derive(Clone, Debug, Serialize, Deserialize)]
#[serde(deny_unknown_fields)]
pub(crate) struct LibraryResume {
    version: u32,
    area_id: AreaId,
    source: SampledTerrainSource,
    candidate_hash: ContentHash,
}

impl LibraryResume {
    pub(crate) fn new(document: &TerrainDocument, candidate_hash: ContentHash) -> Option<Self> {
        let source = document.sampled_source.as_ref()?;
        source.validate().ok()?;
        Some(Self {
            version: 1,
            area_id: document.area_id,
            source: source.clone(),
            candidate_hash,
        })
    }

    pub(crate) fn matches(&self, document: &TerrainDocument) -> bool {
        self.version == 1
            && self.source.validate().is_ok()
            && self.area_id == document.area_id
            && document.sampled_source.as_ref() == Some(&self.source)
    }

    pub(crate) fn verify_candidate(&self, actual: ContentHash) -> Result<(), String> {
        if actual != self.candidate_hash {
            return Err(
                "La bibliothèque a changé. Vérifiez et chargez explicitement sa nouvelle version."
                    .into(),
            );
        }
        Ok(())
    }
}

#[cfg(test)]
mod tests {
    use super::*;
    use forge_core::bytes_sha256;
    use forge_project::sampled_terrain::{HeightEncoding, SAMPLED_TERRAIN_SCHEMA};

    fn document() -> TerrainDocument {
        let mut document = forge_world::embedded_visual_valley_terrain_document().unwrap();
        document.sampled_source = Some(SampledTerrainSource {
            schema: SAMPLED_TERRAIN_SCHEMA.into(),
            schema_version: 1,
            width: 2,
            height: 2,
            origin_xz_m: [0.0, 0.0],
            sample_spacing_m: 1.0,
            outside_height_m: 0.0,
            encoding: HeightEncoding::Float32Le,
            samples_sha256: bytes_sha256(&[0; 16]),
        });
        document
    }

    #[test]
    fn reload_retains_choice_after_authoring_edits() {
        let mut document = document();
        let pin = bytes_sha256(b"verified library candidate");
        let choice = LibraryResume::new(&document, pin).unwrap();
        let choice: LibraryResume =
            serde_json::from_slice(&serde_json::to_vec(&choice).unwrap()).unwrap();
        document.name = "Retouched zone".into();
        document.height_sampling.base_elevation_m += 1.0;
        assert!(choice.matches(&document));
        choice.verify_candidate(pin).unwrap();
    }

    #[test]
    fn another_zone_or_changed_source_cannot_resume() {
        let mut document = document();
        let choice = LibraryResume::new(&document, bytes_sha256(b"library")).unwrap();
        document.area_id =
            serde_json::from_str("\"aaaaaaaa-aaaa-4aaa-8aaa-aaaaaaaaaaaa\"").unwrap();
        assert!(!choice.matches(&document));
        document = self::document();
        document.sampled_source.as_mut().unwrap().origin_xz_m[0] = 2.0;
        assert!(!choice.matches(&document));
        document = self::document();
        document.sampled_source.as_mut().unwrap().samples_sha256 = bytes_sha256(b"new terrain");
        assert!(!choice.matches(&document));
    }

    #[test]
    fn changed_library_or_unknown_version_is_refused() {
        let document = document();
        let mut choice = LibraryResume::new(&document, bytes_sha256(b"library")).unwrap();
        assert!(
            choice
                .verify_candidate(bytes_sha256(b"replacement"))
                .is_err()
        );
        choice.version = 2;
        assert!(!choice.matches(&document));
    }

    #[test]
    fn absent_or_invalid_source_cannot_be_remembered() {
        let mut document = document();
        document.sampled_source.as_mut().unwrap().width = 0;
        assert!(LibraryResume::new(&document, bytes_sha256(b"library")).is_none());
        document.sampled_source = None;
        assert!(LibraryResume::new(&document, bytes_sha256(b"library")).is_none());
    }
}
