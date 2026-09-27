//! First Gate 2 authoring transaction. No renderer state or filesystem access.
//! Bounded history can be restored from a verified, versioned session archive.

use crate::{TerrainDocument, audit::AuditStamp};
use forge_core::{CommandId, ContentHash, Revision};
use serde::{Deserialize, Serialize};
use thiserror::Error;

const MAX_COMMANDS: usize = 1024;
const MAX_ARCHIVE_BYTES: usize = 4 * 1024 * 1024;

#[cfg(not(target_arch = "wasm32"))]
pub mod file_session;

#[derive(Serialize, Deserialize)]
#[serde(deny_unknown_fields)]
struct SessionArchive {
    schema: String,
    version: u32,
    initial_document: TerrainDocument,
    receipts: Vec<TerrainEditReceipt>,
    final_hash: ContentHash,
}

#[derive(Clone, Debug, PartialEq, Serialize, Deserialize)]
#[serde(tag = "kind", rename_all = "snake_case", deny_unknown_fields)]
pub enum TerrainEdit {
    SetSchemaVersion {
        version: u32,
    },
    SetMaterialOverrides {
        samples: Vec<crate::TerrainMaterialOverride>,
    },
    Rename {
        name: String,
    },
    SetBaseElevation {
        metres: f32,
    },
    SetHeightDeltas {
        samples: Vec<crate::TerrainHeightDelta>,
    },
}

impl TerrainEdit {
    fn replace(&self, document: &mut TerrainDocument) -> Self {
        match self {
            Self::SetSchemaVersion { version } => Self::SetSchemaVersion {
                version: std::mem::replace(&mut document.schema_version, *version),
            },
            Self::SetMaterialOverrides { samples } => Self::SetMaterialOverrides {
                samples: std::mem::replace(&mut document.material_overrides, samples.clone()),
            },
            Self::SetHeightDeltas { samples } => Self::SetHeightDeltas {
                samples: std::mem::replace(&mut document.height_deltas, samples.clone()),
            },
            Self::Rename { name } => Self::Rename {
                name: std::mem::replace(&mut document.name, name.clone()),
            },
            Self::SetBaseElevation { metres } => Self::SetBaseElevation {
                metres: std::mem::replace(&mut document.height_sampling.base_elevation_m, *metres),
            },
        }
    }

    fn needs_archive_v2(&self) -> bool {
        matches!(
            self,
            Self::SetSchemaVersion { .. } | Self::SetMaterialOverrides { .. }
        )
    }
}

#[derive(Clone, Debug, PartialEq, Serialize, Deserialize)]
#[serde(
    tag = "kind",
    content = "edit",
    rename_all = "snake_case",
    deny_unknown_fields
)]
pub enum TerrainAction {
    Edit(TerrainEdit),
    Undo,
    Redo,
}

#[derive(Clone, Debug, PartialEq, Serialize, Deserialize)]
#[serde(deny_unknown_fields)]
pub struct TerrainEditRequest {
    pub command_id: CommandId,
    pub expected_revision: Revision,
    pub expected_hash: ContentHash,
    pub action: TerrainAction,
}

#[derive(Clone, Debug, PartialEq, Serialize, Deserialize)]
#[serde(deny_unknown_fields)]
pub struct TerrainEditReceipt {
    pub request: TerrainEditRequest,
    pub applied: TerrainEdit,
    pub inverse: TerrainEdit,
    pub resulting_revision: Revision,
    pub resulting_hash: ContentHash,
    #[serde(default, skip_serializing_if = "AuditStamp::is_legacy")]
    pub audit: AuditStamp,
}

#[derive(Debug, Error)]
pub enum TerrainEditError {
    #[error("terrain validation or serialization: {0}")]
    Document(String),
    #[error("document changed since the request was prepared")]
    Conflict,
    #[error("command identity already used for a different request")]
    DuplicateIdentity,
    #[error("nothing to undo or redo")]
    EmptyHistory,
    #[error("edit has no effect")]
    NoChange,
    #[error("session command limit reached")]
    SessionLimit,
    #[error("revision overflow")]
    RevisionOverflow,
    #[error("invalid terrain session archive: {0}")]
    Archive(String),
}

pub struct TerrainEditSession {
    initial_document: TerrainDocument,
    document: TerrainDocument,
    timeline: Vec<(TerrainEdit, TerrainEdit)>,
    cursor: usize,
    receipts: Vec<TerrainEditReceipt>,
}

impl TerrainEditSession {
    pub fn new(document: TerrainDocument) -> Result<Self, TerrainEditError> {
        document
            .validate()
            .map_err(|e| TerrainEditError::Document(e.to_string()))?;
        let session = Self {
            initial_document: document.clone(),
            document,
            timeline: Vec::new(),
            cursor: 0,
            receipts: Vec::new(),
        };
        if session.document.schema_version >= 3 {
            session.archive_bytes()?;
        }
        Ok(session)
    }

    #[must_use]
    pub const fn document(&self) -> &TerrainDocument {
        &self.document
    }

    /// Read-only checkpoint for an explicitly requested aggregate migration.
    #[must_use]
    pub const fn initial_document(&self) -> &TerrainDocument {
        &self.initial_document
    }

    #[must_use]
    pub fn audit(&self) -> &[TerrainEditReceipt] {
        &self.receipts
    }

    #[must_use]
    pub const fn can_undo(&self) -> bool {
        self.cursor > 0 && self.receipts.len() < MAX_COMMANDS
    }

    #[must_use]
    pub fn can_redo(&self) -> bool {
        self.cursor < self.timeline.len() && self.receipts.len() < MAX_COMMANDS
    }

    /// Encodes the base document and verified command journal, not renderer caches.
    /// The caller owns filesystem persistence; this method does not save a file.
    pub fn archive_bytes(&self) -> Result<Vec<u8>, TerrainEditError> {
        let archive = SessionArchive {
            schema: "forge.terrain-edit-session".into(),
            version: self.archive_version(),
            initial_document: self.initial_document.clone(),
            receipts: self.receipts.clone(),
            final_hash: hash(&self.document)?,
        };
        let bytes = forge_core::canonical_json_bytes(&archive)
            .map_err(|e| TerrainEditError::Archive(e.to_string()))?;
        if bytes.len() > MAX_ARCHIVE_BYTES {
            return Err(TerrainEditError::Archive("archive exceeds 4 MiB".into()));
        }
        Ok(bytes)
    }

    /// Rebuilds undo/redo and retry identities by replaying validated commands.
    /// Never trusts serialized inverses, revisions, or hashes without recomputation.
    pub fn from_archive_bytes(bytes: &[u8]) -> Result<Self, TerrainEditError> {
        if bytes.len() > MAX_ARCHIVE_BYTES {
            return Err(TerrainEditError::Archive("archive exceeds 4 MiB".into()));
        }
        let archive: SessionArchive =
            serde_json::from_slice(bytes).map_err(|e| TerrainEditError::Archive(e.to_string()))?;
        if archive.schema != "forge.terrain-edit-session" || !matches!(archive.version, 1..=3) {
            return Err(TerrainEditError::Archive(
                "unsupported schema/version".into(),
            ));
        }
        if archive.version == 1
            && (archive.initial_document.schema_version >= 3
                || archive
                    .receipts
                    .iter()
                    .any(|r| r.applied.needs_archive_v2() || r.inverse.needs_archive_v2()))
        {
            return Err(TerrainEditError::Archive(
                "v3 document or material command requires archive v2".into(),
            ));
        }
        if archive.version < 3
            && archive
                .receipts
                .iter()
                .any(|receipt| !receipt.audit.is_legacy())
        {
            return Err(TerrainEditError::Archive(
                "stamped receipt requires terrain archive v3".into(),
            ));
        }
        if archive.receipts.len() > MAX_COMMANDS {
            return Err(TerrainEditError::SessionLimit);
        }
        let mut session = Self::new(archive.initial_document)?;
        for receipt in archive.receipts {
            if session.previous(&receipt.request)?.is_some() {
                return Err(TerrainEditError::Archive("duplicate journal entry".into()));
            }
            if session.execute_with_audit(&receipt.request, receipt.audit.clone())? != receipt {
                return Err(TerrainEditError::Archive("journal receipt mismatch".into()));
            }
        }
        if hash(session.document())? != archive.final_hash {
            return Err(TerrainEditError::Archive(
                "final document hash mismatch".into(),
            ));
        }
        Ok(session)
    }

    /// A retry returns the original receipt, even after subsequent commands.
    /// It does not claim that the original resulting revision is still current.
    fn previous(
        &self,
        request: &TerrainEditRequest,
    ) -> Result<Option<TerrainEditReceipt>, TerrainEditError> {
        if let Some(receipt) = self
            .receipts
            .iter()
            .find(|r| r.request.command_id == request.command_id)
        {
            if receipt.request != *request {
                return Err(TerrainEditError::DuplicateIdentity);
            }
            return Ok(Some(receipt.clone()));
        }
        Ok(None)
    }

    /// Validates and describes the exact change without mutating session state.
    pub fn preview(
        &self,
        request: &TerrainEditRequest,
    ) -> Result<TerrainEditReceipt, TerrainEditError> {
        self.preview_with_audit(request, AuditStamp::legacy())
    }

    pub fn preview_with_audit(
        &self,
        request: &TerrainEditRequest,
        audit: AuditStamp,
    ) -> Result<TerrainEditReceipt, TerrainEditError> {
        audit.validate().map_err(TerrainEditError::Archive)?;
        if let Some(receipt) = self.previous(request)? {
            return Ok(receipt);
        }
        if self.receipts.len() >= MAX_COMMANDS {
            return Err(TerrainEditError::SessionLimit);
        }
        if request.expected_revision != self.document.revision
            || request.expected_hash != hash(&self.document)?
        {
            return Err(TerrainEditError::Conflict);
        }
        let applied = match &request.action {
            TerrainAction::Edit(edit) => edit.clone(),
            TerrainAction::Undo => self
                .cursor
                .checked_sub(1)
                .and_then(|i| self.timeline.get(i))
                .map(|entry| entry.1.clone())
                .ok_or(TerrainEditError::EmptyHistory)?,
            TerrainAction::Redo => self
                .timeline
                .get(self.cursor)
                .map(|entry| entry.0.clone())
                .ok_or(TerrainEditError::EmptyHistory)?,
        };
        if matches!(&applied, TerrainEdit::Rename { name } if name.len() > 128) {
            return Err(TerrainEditError::Document("name exceeds 128 bytes".into()));
        }
        let mut candidate = self.document.clone();
        let inverse = applied.replace(&mut candidate);
        if candidate == self.document {
            return Err(TerrainEditError::NoChange);
        }
        candidate.revision = candidate
            .revision
            .next()
            .map_err(|_| TerrainEditError::RevisionOverflow)?;
        candidate
            .validate()
            .map_err(|e| TerrainEditError::Document(e.to_string()))?;
        let receipt = TerrainEditReceipt {
            request: request.clone(),
            applied,
            inverse,
            resulting_revision: candidate.revision,
            resulting_hash: hash(&candidate)?,
            audit,
        };
        // A new paint/migration journal must be saveable before Preview or Apply succeeds.
        if self.archive_version() >= 2
            || receipt.applied.needs_archive_v2()
            || !receipt.audit.is_legacy()
        {
            let mut receipts = self.receipts.clone();
            receipts.push(receipt.clone());
            let archive = SessionArchive {
                schema: "forge.terrain-edit-session".into(),
                version: if self.archive_version() == 3 || !receipt.audit.is_legacy() {
                    3
                } else {
                    2
                },
                initial_document: self.initial_document.clone(),
                receipts,
                final_hash: receipt.resulting_hash,
            };
            let bytes = forge_core::canonical_json_bytes(&archive)
                .map_err(|e| TerrainEditError::Archive(e.to_string()))?;
            if bytes.len() > MAX_ARCHIVE_BYTES {
                return Err(TerrainEditError::Archive(
                    "archive exceeds 4 MiB before mutation".into(),
                ));
            }
        }
        Ok(receipt)
    }

    pub fn execute(
        &mut self,
        request: &TerrainEditRequest,
    ) -> Result<TerrainEditReceipt, TerrainEditError> {
        self.execute_with_audit(request, AuditStamp::legacy())
    }

    pub fn execute_with_audit(
        &mut self,
        request: &TerrainEditRequest,
        audit: AuditStamp,
    ) -> Result<TerrainEditReceipt, TerrainEditError> {
        if let Some(receipt) = self.previous(request)? {
            return Ok(receipt);
        }
        let receipt = self.preview_with_audit(request, audit)?;
        // Every fallible check is complete before the first state mutation.
        receipt.applied.replace(&mut self.document);
        self.document.revision = receipt.resulting_revision;
        match request.action {
            TerrainAction::Edit(_) => {
                self.timeline.truncate(self.cursor);
                self.timeline
                    .push((receipt.applied.clone(), receipt.inverse.clone()));
                self.cursor += 1;
            }
            TerrainAction::Undo => self.cursor -= 1,
            TerrainAction::Redo => self.cursor += 1,
        }
        self.receipts.push(receipt.clone());
        Ok(receipt)
    }

    fn archive_version(&self) -> u32 {
        if self
            .receipts
            .iter()
            .any(|receipt| !receipt.audit.is_legacy())
        {
            return 3;
        }
        if self.initial_document.schema_version >= 3
            || self.receipts.iter().any(|r| r.applied.needs_archive_v2())
        {
            2
        } else {
            1
        }
    }
}

fn hash(document: &TerrainDocument) -> Result<ContentHash, TerrainEditError> {
    document
        .content_hash()
        .map_err(|e| TerrainEditError::Document(e.to_string()))
}

#[cfg(test)]
mod tests;
