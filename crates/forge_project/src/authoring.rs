//! Shared application protocol. Paths and OS handles never cross this boundary.
use crate::{
    TerrainDocument,
    terrain_edit::{TerrainEditReceipt, TerrainEditRequest},
};
use forge_core::{AreaId, ContentHash};
use serde::{Deserialize, Serialize};

pub mod creation;
pub mod history;
pub mod playtest;
pub mod terrain_source;
pub mod vegetation_source;
pub mod workspace;

#[derive(Clone, Debug, PartialEq, Serialize, Deserialize)]
#[serde(tag = "action", rename_all = "snake_case", deny_unknown_fields)]
pub enum AuthoringRequest {
    /// New aggregate zone. Existing legacy archives still require explicit migration.
    CreateZone {
        document: Box<TerrainDocument>,
    },
    Create {
        document: Box<TerrainDocument>,
    },
    Open {
        area_id: AreaId,
    },
    OpenManaged {
        area_id: AreaId,
    },
    Inspect,
    /// Prepare the effective identity without entering simulation or writing sources.
    PreparePlaytest {
        expected_hash: ContentHash,
    },
    /// Read-only discovery; opening still revalidates the selected archive.
    ListZones,
    ReadHistory {
        expected_hash: ContentHash,
        offset: usize,
    },
    InspectRecovery {
        area_id: AreaId,
    },
    Recover {
        area_id: AreaId,
        expected_saved: Option<ContentHash>,
        expected_pending: ContentHash,
        promote: bool,
    },
    Preview {
        request: TerrainEditRequest,
    },
    Apply {
        request: TerrainEditRequest,
    },
    /// Review only; does not change the active legacy session or saved bytes.
    PrepareZoneMigration {
        expected_hash: ContentHash,
        generator: crate::zone_authoring::GeneratorStamp,
        vegetation: Option<Box<crate::VegetationDocument>>,
    },
    /// Requires the legacy session to be saved and closed. Save remains explicit.
    BeginZoneMigration {
        proposal: Box<crate::zone_authoring::migration::ZoneMigrationProposal>,
    },
    PreviewZone {
        request: crate::zone_authoring::session::ZoneRequest,
    },
    ApplyZone {
        request: crate::zone_authoring::session::ZoneRequest,
    },
    ReviewZoneCompaction,
    CompactZoneArchive {
        review: crate::zone_authoring::session::ZoneCompactionReview,
    },
    Save,
    Close,
    /// Read-only simulation of exactly the document approved by the caller.
    StartPlaytest {
        expected_hash: ContentHash,
    },
    /// Temporary surface position validated against the same approved snapshot.
    StartPlaytestAt {
        expected_hash: ContentHash,
        start: playtest::PlaytestStart,
    },
    /// Optional runtime observation preserves legacy `{"action":"stop_playtest"}` callers.
    StopPlaytest {
        #[serde(default, skip_serializing_if = "Option::is_none")]
        diagnostics: Option<playtest::PlaytestExitDiagnostics>,
    },
}

impl AuthoringRequest {
    #[must_use]
    pub const fn persists_audit_receipt(&self) -> bool {
        matches!(self, Self::Apply { .. } | Self::ApplyZone { .. })
    }

    #[must_use]
    pub const fn uses_audit_context(&self) -> bool {
        matches!(
            self,
            Self::Preview { .. }
                | Self::Apply { .. }
                | Self::PreviewZone { .. }
                | Self::ApplyZone { .. }
        )
    }
}

#[derive(Clone, Debug, PartialEq, Serialize, Deserialize)]
#[serde(deny_unknown_fields)]
pub struct AuthoringState {
    #[serde(default, skip_serializing_if = "Vec::is_empty")]
    pub managed_tabs: Vec<ManagedZoneTab>,
    /// Aggregate authority when present; document below is only its derived terrain view.
    #[serde(default, skip_serializing_if = "Option::is_none")]
    pub zone_head: Option<crate::zone_authoring::ZoneAuthoringHead>,
    #[serde(default, skip_serializing_if = "Option::is_none")]
    pub zone_receipt: Option<crate::zone_authoring::session::ZoneReceipt>,
    #[serde(default, skip_serializing_if = "Option::is_none")]
    pub zone_migration: Option<crate::zone_authoring::migration::ZoneMigrationProposal>,
    #[serde(default, skip_serializing_if = "Option::is_none")]
    pub zone_compaction: Option<crate::zone_authoring::session::ZoneCompactionReview>,
    pub document: Option<TerrainDocument>,
    pub dirty: bool,
    pub receipt: Option<TerrainEditReceipt>,
    /// Session mode only: never serialized into the terrain archive.
    pub playtest_hash: Option<ContentHash>,
    #[serde(default, skip_serializing_if = "Option::is_none")]
    pub playtest_identity: Option<playtest::PlaytestIdentity>,
    #[serde(default, skip_serializing_if = "Option::is_none")]
    pub playtest_start: Option<playtest::PlaytestStart>,
    /// Last real runtime observation after leaving playtest; session-only, never archived.
    #[serde(default, skip_serializing_if = "Option::is_none")]
    pub last_playtest_diagnostics: Option<playtest::PlaytestExitDiagnostics>,
    #[serde(default, skip_serializing_if = "Option::is_none")]
    pub catalog: Option<ZoneCatalog>,
    #[serde(default, skip_serializing_if = "Option::is_none")]
    pub recovery: Option<RecoveryReview>,
    #[serde(default, skip_serializing_if = "Option::is_none")]
    pub history: Option<history::HistoryPage>,
    #[serde(default)]
    pub can_undo: bool,
    #[serde(default)]
    pub can_redo: bool,
}

#[derive(Clone, Debug, PartialEq, Serialize, Deserialize)]
#[serde(deny_unknown_fields)]
pub struct ManagedZoneTab {
    pub area_id: AreaId,
    pub name: String,
    pub dirty: bool,
}

/// Archive hashes bind an explicit choice; document summaries are not editable sources.
#[derive(Clone, Debug, PartialEq, Serialize, Deserialize)]
#[serde(deny_unknown_fields)]
pub struct RecoveryReview {
    pub area_id: AreaId,
    pub saved: Option<RecoveryVersion>,
    pub pending: RecoveryVersion,
}

#[derive(Clone, Debug, PartialEq, Serialize, Deserialize)]
#[serde(deny_unknown_fields)]
pub struct RecoveryVersion {
    pub archive_hash: ContentHash,
    pub document: Option<TerrainDocument>,
    pub error: Option<String>,
}

#[derive(Clone, Debug, PartialEq, Serialize, Deserialize)]
#[serde(deny_unknown_fields)]
pub struct SavedZone {
    pub area_id: AreaId,
    pub project_id: forge_core::ProjectId,
    pub name: String,
    pub saved_hash: ContentHash,
    pub pending_recovery: bool,
}

#[derive(Clone, Copy, Debug, PartialEq, Eq, Serialize, Deserialize)]
#[serde(rename_all = "snake_case")]
pub enum UnavailableZoneReason {
    MissingArchive,
    InterruptedSave,
    InvalidArchive,
    IdentityMismatch,
    UnsafePath,
    TooLarge,
    Unreadable,
}

#[derive(Clone, Debug, PartialEq, Serialize, Deserialize)]
#[serde(deny_unknown_fields)]
pub struct UnavailableZone {
    pub area_id: AreaId,
    pub reason: UnavailableZoneReason,
}

#[derive(Clone, Debug, Default, PartialEq, Serialize, Deserialize)]
#[serde(deny_unknown_fields)]
pub struct ZoneCatalog {
    pub zones: Vec<SavedZone>,
    pub unavailable: Vec<UnavailableZone>,
}

/// Initial empty terrain for the editor, not a copy of a demonstration map.
pub fn new_terrain(
    project_key: &str,
    area_key: &str,
    name: &str,
) -> Result<TerrainDocument, String> {
    use crate::{
        HeightSamplingAlgorithm, HeightSamplingParameters, TerrainExtent, TerrainMaterialLayer,
    };
    use forge_core::{ProjectId, Revision};
    for key in [project_key, area_key] {
        if key.is_empty()
            || key.len() > 64
            || !key
                .bytes()
                .all(|byte| byte.is_ascii_alphanumeric() || byte == b'-' || byte == b'_')
        {
            return Err("Project and zone keys require 1–64 ASCII letters, digits, - or _".into());
        }
    }
    let document = TerrainDocument::new(
        ProjectId::deterministic(project_key),
        AreaId::deterministic(format!("{project_key}/{area_key}")),
        Revision::INITIAL,
        name,
        0,
        // Current generator supports a fixed 512 m extent with 2 m samples.
        TerrainExtent::new(512.0, 512.0),
        HeightSamplingParameters {
            algorithm: HeightSamplingAlgorithm::FractalValueNoiseV1,
            sample_spacing_m: 2.0,
            base_elevation_m: 0.0,
            amplitude_m: 0.0,
            base_frequency_cycles_per_m: 0.01,
            octaves: 1,
            lacunarity: 2.0,
            persistence: 0.5,
        },
        vec![
            TerrainMaterialLayer::new("grass", "Herbe"),
            TerrainMaterialLayer::new("earth", "Terre"),
            TerrainMaterialLayer::new("rock", "Roche"),
        ],
    );
    document.validate().map_err(|error| error.to_string())?;
    Ok(document)
}

#[cfg(not(target_arch = "wasm32"))]
pub mod host;

#[cfg(test)]
mod tests {
    use super::*;

    #[test]
    fn new_zone_is_valid_flat_deterministic_and_project_scoped() {
        let first = new_terrain("project", "zone", "Zone").unwrap();
        assert_eq!(first, new_terrain("project", "zone", "Zone").unwrap());
        assert_eq!(
            first.height_sampling.amplitude_m.to_bits(),
            0.0_f32.to_bits()
        );
        assert!(first.height_deltas.is_empty());
        assert_ne!(
            first.area_id,
            new_terrain("other", "zone", "Zone").unwrap().area_id
        );
        for invalid in ["", "../escape", "a/b", "é", " "] {
            assert!(new_terrain(invalid, "zone", "Zone").is_err());
            assert!(new_terrain("project", invalid, "Zone").is_err());
        }
        assert!(new_terrain("project", "zone", " ").is_err());
    }
}
