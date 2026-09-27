//! One lock and one writable authority per open zone, including across formats.
use crate::{
    TerrainDocument,
    terrain_edit::file_session::FileTerrainSession,
    zone_authoring::{ZoneAuthoringHead, file_session::FileZoneSession},
};
use forge_core::document_store::DocumentStore;
use std::path::Path;

/// Validated summary for catalog/recovery, never a mutable second session.
pub(super) fn archive_summary(
    bytes: &[u8],
) -> Result<(TerrainDocument, forge_core::ContentHash), String> {
    if bytes.len() > crate::zone_authoring::MAX_ZONE_HEAD_BYTES {
        return Err("Authoring archive exceeds 4 MiB".into());
    }
    let value: serde_json::Value = serde_json::from_slice(bytes).map_err(|e| e.to_string())?;
    if value.get("schema").and_then(serde_json::Value::as_str) == Some("forge.zone-edit-session") {
        let session = crate::zone_authoring::session::ZoneEditSession::from_archive_bytes(bytes)?;
        Ok((
            session.head().effective()?.0,
            session.head().content_hash()?,
        ))
    } else {
        let session = crate::terrain_edit::TerrainEditSession::from_archive_bytes(bytes)
            .map_err(|e| e.to_string())?;
        Ok((
            session.document().clone(),
            session
                .document()
                .content_hash()
                .map_err(|e| e.to_string())?,
        ))
    }
}

pub(super) enum ActiveSession {
    Terrain(Box<FileTerrainSession>),
    Zone(Box<FileZoneSession>),
    /// Read-only projection of the workspace's accepted archive, refreshed after commands.
    Managed {
        session: Box<crate::zone_authoring::session::ZoneEditSession>,
        dirty: bool,
    },
}

impl ActiveSession {
    pub fn playtest_snapshot(
        &self,
        expected_hash: forge_core::ContentHash,
    ) -> Result<crate::authoring::playtest::PlaytestSnapshot, String> {
        use crate::authoring::playtest::PlaytestSnapshot;
        match self {
            Self::Terrain(s) => {
                PlaytestSnapshot::from_terrain(s.session().document(), expected_hash)
            }
            Self::Zone(s) => PlaytestSnapshot::from_zone(s.session().head(), expected_hash),
            Self::Managed { session, .. } => {
                PlaytestSnapshot::from_zone(session.head(), expected_hash)
            }
        }
    }

    pub fn history(
        &self,
        expected_hash: forge_core::ContentHash,
        offset: usize,
    ) -> Result<crate::authoring::history::HistoryPage, String> {
        match self {
            Self::Terrain(s) => crate::authoring::history::read(s.session(), expected_hash, offset),
            Self::Zone(s) => {
                crate::authoring::history::zone::read(s.session(), expected_hash, offset)
            }
            Self::Managed { session, .. } => {
                crate::authoring::history::zone::read(session, expected_hash, offset)
            }
        }
    }

    pub fn open(directory: &Path) -> Result<Self, String> {
        let store = DocumentStore::open(directory).map_err(|e| e.to_string())?;
        let stored = store
            .load()
            .map_err(|e| e.to_string())?
            .ok_or("No saved zone")?;
        // Routing is advisory only; the selected parser verifies every archive receipt.
        let value: serde_json::Value =
            serde_json::from_slice(&stored.bytes).map_err(|e| e.to_string())?;
        if value.get("schema").and_then(serde_json::Value::as_str)
            == Some("forge.zone-edit-session")
        {
            Ok(Self::Zone(Box::new(FileZoneSession::from_store(store)?)))
        } else {
            Ok(Self::Terrain(Box::new(
                FileTerrainSession::from_store(store).map_err(|e| e.to_string())?,
            )))
        }
    }

    pub fn document(&self) -> Result<TerrainDocument, String> {
        match self {
            Self::Terrain(s) => Ok(s.session().document().clone()),
            Self::Zone(s) => Ok(s.session().head().effective()?.0),
            Self::Managed { session, .. } => Ok(session.head().effective()?.0),
        }
    }

    pub fn head(&self) -> Option<&ZoneAuthoringHead> {
        match self {
            Self::Terrain(_) => None,
            Self::Zone(s) => Some(s.session().head()),
            Self::Managed { session, .. } => Some(session.head()),
        }
    }

    pub fn is_dirty(&self) -> bool {
        match self {
            Self::Terrain(s) => s.is_dirty(),
            Self::Zone(s) => s.is_dirty(),
            Self::Managed { dirty, .. } => *dirty,
        }
    }

    pub fn can_undo(&self) -> bool {
        match self {
            Self::Terrain(s) => s.session().can_undo(),
            Self::Zone(s) => s.session().can_undo(),
            Self::Managed { session, .. } => session.can_undo(),
        }
    }

    pub fn can_redo(&self) -> bool {
        match self {
            Self::Terrain(s) => s.session().can_redo(),
            Self::Zone(s) => s.session().can_redo(),
            Self::Managed { session, .. } => session.can_redo(),
        }
    }

    pub fn save(&mut self) -> Result<(), String> {
        match self {
            Self::Terrain(s) => s.save().map_err(|e| e.to_string()),
            Self::Zone(s) => s.save(),
            Self::Managed { .. } => Err("Managed zone must save through the workspace".into()),
        }
    }
}
