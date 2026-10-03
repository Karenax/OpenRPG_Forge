//! Explicit dry-run migration. The original verified archive is retained byte-for-byte.
use super::{GeneratorStamp, MAX_ZONE_HEAD_BYTES, ZoneAuthoringHead, ZoneBase, ZoneCorrections};
use crate::{VegetationDocument, terrain_edit::TerrainEditSession};
use forge_core::ContentHash;
use serde::{Deserialize, Serialize};

#[derive(Clone, Debug, PartialEq, Serialize, Deserialize)]
#[serde(deny_unknown_fields)]
pub struct ZoneMigrationProposal {
    source_hash: ContentHash,
    /// Frozen terrain checkpoint (historical archive or fresh zero-command seed),
    /// never another writable source. Fresh seeds are stamped `forge-new-zone`.
    legacy_archive: Vec<u8>,
    head: ZoneAuthoringHead,
}

impl ZoneMigrationProposal {
    pub fn prepare(
        archive: &[u8],
        expected_source_hash: ContentHash,
        generator: GeneratorStamp,
        vegetation: Option<VegetationDocument>,
    ) -> Result<Self, String> {
        // Verify historical hashes and inverses BEFORE changing the document format.
        let session = TerrainEditSession::from_archive_bytes(archive).map_err(|e| e.to_string())?;
        let source = session.document();
        if source.content_hash().map_err(|e| e.to_string())? != expected_source_hash {
            return Err("Stale terrain migration source".into());
        }
        let initial = session.initial_document();
        let mut base = initial.clone();
        // Sampled terrain requires v4; upgrading legacy procedural documents to
        // v3 must never downgrade an already validated imported source.
        base.schema_version = base.schema_version.max(3);
        let corrections = ZoneCorrections {
            source_object_edits: None,
            gameplay: None,
            imported_plant_offsets: None,
            imported_plant_placements: None,
            vegetation_exclusions: None,
            promotions: None,
            species: None,
            start: None,
            name: (source.name != initial.name).then(|| source.name.clone()),
            base_elevation_m: (source.height_sampling.base_elevation_m.to_bits()
                != initial.height_sampling.base_elevation_m.to_bits())
            .then_some(source.height_sampling.base_elevation_m),
            height_deltas: (source.height_deltas != initial.height_deltas)
                .then(|| source.height_deltas.clone()),
            material_overrides: (source.material_overrides != initial.material_overrides)
                .then(|| source.material_overrides.clone()),
            density: Vec::new(),
            placements: None,
            paths: None,
            rivers: None,
            buildings: None,
        };
        let head = ZoneAuthoringHead {
            schema: "forge.zone-authoring".into(),
            version: 1,
            revision: source.revision,
            base: ZoneBase {
                generator,
                terrain: base,
                vegetation,
            },
            corrections,
        };
        let (effective, _) = head.effective()?;
        let mut expected = source.clone();
        expected.schema_version = expected.schema_version.max(3);
        if effective != expected {
            return Err("Migration cannot represent all legacy edits".into());
        }
        let proposal = Self {
            source_hash: expected_source_hash,
            legacy_archive: archive.to_vec(),
            head,
        };
        if serde_json::to_vec(&proposal)
            .map_err(|e| e.to_string())?
            .len()
            > MAX_ZONE_HEAD_BYTES
        {
            return Err("Migration with retained history exceeds 4 MiB".into());
        }
        Ok(proposal)
    }

    #[must_use]
    pub const fn head(&self) -> &ZoneAuthoringHead {
        &self.head
    }

    #[must_use]
    pub fn legacy_archive(&self) -> &[u8] {
        &self.legacy_archive
    }

    /// Recheck the stored checkpoint and recompute the candidate; reject tampering.
    pub fn from_bytes(bytes: &[u8]) -> Result<Self, String> {
        if bytes.len() > MAX_ZONE_HEAD_BYTES {
            return Err("Migration exceeds 4 MiB".into());
        }
        let encoded: Self = serde_json::from_slice(bytes).map_err(|e| e.to_string())?;
        let checked = Self::prepare(
            &encoded.legacy_archive,
            encoded.source_hash,
            encoded.head.base.generator.clone(),
            encoded.head.base.vegetation.clone(),
        )?;
        if checked.head != encoded.head {
            return Err("Migration head differs from verified legacy state".into());
        }
        Ok(checked)
    }

    pub fn to_bytes(&self) -> Result<Vec<u8>, String> {
        let bytes = serde_json::to_vec(self).map_err(|e| e.to_string())?;
        Self::from_bytes(&bytes)?;
        Ok(bytes)
    }
}
