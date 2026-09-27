//! Bounded read model of the existing command journal, never another saved history.
use crate::{
    audit::AuditStamp,
    terrain_edit::{TerrainAction, TerrainEdit, TerrainEditSession},
};
use forge_core::{CommandId, ContentHash, Revision};
use serde::{Deserialize, Serialize};
pub mod zone;

pub const PAGE_SIZE: usize = 32;

#[derive(Clone, Copy, Debug, PartialEq, Eq, Serialize, Deserialize)]
#[serde(rename_all = "snake_case")]
pub enum HistoryAction {
    Edit,
    Regenerate,
    Undo,
    Redo,
}

#[derive(Clone, Debug, PartialEq, Serialize, Deserialize)]
#[serde(tag = "kind", rename_all = "snake_case", deny_unknown_fields)]
pub enum HistoryChange {
    Zone {
        changes: Vec<zone::ZoneHistoryChange>,
    },
    SchemaVersion {
        before: u32,
        after: u32,
    },
    MaterialSamples {
        before: usize,
        after: usize,
    },
    Rename {
        before: String,
        after: String,
    },
    BaseElevation {
        before: f32,
        after: f32,
    },
    /// Counts summarize snapshots; they do not imply every sample changed.
    HeightSamples {
        before: usize,
        after: usize,
    },
}

#[derive(Clone, Debug, PartialEq, Serialize, Deserialize)]
#[serde(deny_unknown_fields)]
pub struct HistoryEntry {
    pub command_id: CommandId,
    pub revision: Revision,
    pub resulting_hash: ContentHash,
    pub action: HistoryAction,
    pub change: HistoryChange,
    #[serde(default, skip_serializing_if = "AuditStamp::is_legacy")]
    pub audit: AuditStamp,
}

#[derive(Clone, Debug, PartialEq, Serialize, Deserialize)]
#[serde(deny_unknown_fields)]
pub struct HistoryPage {
    pub source_hash: ContentHash,
    pub offset: usize,
    pub total: usize,
    /// Most recent first, including Undo/Redo; no sample arrays cross the bridge.
    pub entries: Vec<HistoryEntry>,
}

pub fn read(
    session: &TerrainEditSession,
    expected_hash: ContentHash,
    offset: usize,
) -> Result<HistoryPage, String> {
    let source_hash = session
        .document()
        .content_hash()
        .map_err(|e| e.to_string())?;
    if source_hash != expected_hash {
        return Err("history changed: refresh the current zone".into());
    }
    let audit = session.audit();
    if offset > audit.len() {
        return Err("history offset exceeds journal length".into());
    }
    let entries = audit
        .iter()
        .rev()
        .skip(offset)
        .take(PAGE_SIZE)
        .map(|receipt| {
            let change = match (&receipt.inverse, &receipt.applied) {
                (
                    TerrainEdit::SetSchemaVersion { version: before },
                    TerrainEdit::SetSchemaVersion { version: after },
                ) => HistoryChange::SchemaVersion {
                    before: *before,
                    after: *after,
                },
                (
                    TerrainEdit::SetMaterialOverrides { samples: before },
                    TerrainEdit::SetMaterialOverrides { samples: after },
                ) => HistoryChange::MaterialSamples {
                    before: before.len(),
                    after: after.len(),
                },
                (TerrainEdit::Rename { name: before }, TerrainEdit::Rename { name: after }) => {
                    HistoryChange::Rename {
                        before: before.clone(),
                        after: after.clone(),
                    }
                }
                (
                    TerrainEdit::SetBaseElevation { metres: before },
                    TerrainEdit::SetBaseElevation { metres: after },
                ) => HistoryChange::BaseElevation {
                    before: *before,
                    after: *after,
                },
                (
                    TerrainEdit::SetHeightDeltas { samples: before },
                    TerrainEdit::SetHeightDeltas { samples: after },
                ) => HistoryChange::HeightSamples {
                    before: before.len(),
                    after: after.len(),
                },
                _ => return Err("inconsistent journal edit and inverse".to_owned()),
            };
            Ok(HistoryEntry {
                command_id: receipt.request.command_id,
                revision: receipt.resulting_revision,
                resulting_hash: receipt.resulting_hash,
                action: match receipt.request.action {
                    TerrainAction::Edit(_) => HistoryAction::Edit,
                    TerrainAction::Undo => HistoryAction::Undo,
                    TerrainAction::Redo => HistoryAction::Redo,
                },
                change,
                audit: receipt.audit.clone(),
            })
        })
        .collect::<Result<Vec<_>, String>>()?;
    Ok(HistoryPage {
        source_hash,
        offset,
        total: audit.len(),
        entries,
    })
}
