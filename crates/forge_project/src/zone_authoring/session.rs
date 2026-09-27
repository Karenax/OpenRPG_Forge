//! In-memory aggregate transactions and a single self-verifying archive.
use super::{
    MAX_ZONE_HEAD_BYTES, ZoneAuthoringHead, ZoneBase, ZoneCorrections,
    migration::ZoneMigrationProposal,
};
use crate::audit::AuditStamp;
use forge_core::{CommandId, ContentHash, Revision, canonical_json_bytes};
use serde::{Deserialize, Serialize};
mod compact;
mod delta;
mod source_blobs;

#[derive(Clone, Debug, PartialEq, Serialize, Deserialize)]
#[serde(deny_unknown_fields)]
pub struct ZoneCompactionReview {
    pub head_hash: ContentHash,
    pub source_version: u32,
    pub source_bytes: usize,
    pub target_bytes: usize,
    pub retained_commands: usize,
}

#[derive(Clone, Debug, PartialEq, Serialize, Deserialize)]
#[serde(
    tag = "kind",
    content = "corrections",
    rename_all = "snake_case",
    deny_unknown_fields
)]
pub enum ZoneAction {
    Edit(Box<ZoneCorrections>),
    Regenerate(Box<ZoneBase>),
    ReconcilePromotions(Box<super::promotion::PromotionReconciliation>),
    Undo,
    Redo,
}

impl ZoneAction {
    /// Keep requests small without changing the serialized Edit payload.
    #[must_use]
    pub fn edit(corrections: ZoneCorrections) -> Self {
        Self::Edit(Box::new(corrections))
    }
}

#[derive(Clone, Debug, PartialEq, Serialize, Deserialize)]
#[serde(deny_unknown_fields)]
pub struct ZoneRequest {
    pub command_id: CommandId,
    pub expected_revision: Revision,
    pub expected_hash: ContentHash,
    pub action: ZoneAction,
}

#[derive(Clone, Debug, PartialEq, Serialize, Deserialize)]
#[serde(deny_unknown_fields)]
pub struct ZoneReceipt {
    pub request: ZoneRequest,
    pub applied: ZoneCorrections,
    pub inverse: ZoneCorrections,
    #[serde(default, skip_serializing_if = "Option::is_none")]
    pub base_change: Option<Box<ZoneBaseChange>>,
    pub resulting_revision: Revision,
    pub resulting_hash: ContentHash,
    #[serde(default, skip_serializing_if = "AuditStamp::is_legacy")]
    pub audit: AuditStamp,
}

#[derive(Clone, Debug, PartialEq, Serialize, Deserialize)]
#[serde(deny_unknown_fields)]
pub struct ZoneBaseChange {
    pub applied: ZoneBase,
    pub inverse: ZoneBase,
}

#[derive(Serialize, Deserialize)]
#[serde(deny_unknown_fields)]
struct Archive {
    schema: String,
    version: u32,
    migration: ZoneMigrationProposal,
    /// Current corrections persist independently of the undo timeline.
    head: ZoneAuthoringHead,
    receipts: Vec<ZoneReceipt>,
}

#[derive(Serialize)]
struct PlainArchive<'a> {
    schema: &'static str,
    version: u32,
    migration: &'a ZoneMigrationProposal,
    head: &'a ZoneAuthoringHead,
    receipts: &'a [ZoneReceipt],
}

pub struct ZoneEditSession {
    migration: ZoneMigrationProposal,
    head: ZoneAuthoringHead,
    receipts: Vec<ZoneReceipt>,
    timeline: Vec<ZoneReceipt>,
    cursor: usize,
    compact_archive: bool,
    delta_archive: bool,
}

impl ZoneEditSession {
    /// A fresh aggregate, with an immutable zero-command terrain seed checkpoint.
    /// Reuses the verified archive codec; no legacy session is created on disk.
    pub fn from_initial_terrain(document: crate::TerrainDocument) -> Result<Self, String> {
        if document.revision != Revision::INITIAL
            || !document.height_deltas.is_empty()
            || !document.material_overrides.is_empty()
        {
            return Err("New zone requires an initial terrain without authored corrections".into());
        }
        let hash = document.content_hash().map_err(|e| e.to_string())?;
        let seed =
            crate::terrain_edit::TerrainEditSession::new(document).map_err(|e| e.to_string())?;
        let archive = seed.archive_bytes().map_err(|e| e.to_string())?;
        let proposal = ZoneMigrationProposal::prepare(
            &archive,
            hash,
            super::GeneratorStamp {
                name: "forge-new-zone".into(),
                version: "1".into(),
                inputs_hash: hash,
            },
            None,
        )?;
        Self::from_migration(proposal)
    }

    pub fn from_migration(migration: ZoneMigrationProposal) -> Result<Self, String> {
        // A proposal may have been deserialized directly; never trust unchecked fields.
        let checked = ZoneMigrationProposal::from_bytes(&migration.to_bytes()?)?;
        let session = Self {
            head: checked.head().clone(),
            migration,
            receipts: Vec::new(),
            timeline: Vec::new(),
            cursor: 0,
            compact_archive: false,
            delta_archive: false,
        };
        session.archive_bytes()?;
        Ok(session)
    }

    #[must_use]
    pub const fn head(&self) -> &ZoneAuthoringHead {
        &self.head
    }

    #[must_use]
    pub fn audit(&self) -> &[ZoneReceipt] {
        &self.receipts
    }

    #[must_use]
    pub const fn can_undo(&self) -> bool {
        self.cursor > 0
    }

    #[must_use]
    pub fn can_redo(&self) -> bool {
        self.cursor < self.timeline.len()
    }

    fn previous(&self, request: &ZoneRequest) -> Result<Option<ZoneReceipt>, String> {
        if let Some(receipt) = self
            .receipts
            .iter()
            .find(|r| r.request.command_id == request.command_id)
        {
            if receipt.request != *request {
                return Err("Command ID reused with different parameters".into());
            }
            return Ok(Some(receipt.clone()));
        }
        Ok(None)
    }

    pub fn preview(&self, request: &ZoneRequest) -> Result<ZoneReceipt, String> {
        self.preview_with_audit(request, AuditStamp::legacy())
    }

    pub fn preview_with_audit(
        &self,
        request: &ZoneRequest,
        audit: AuditStamp,
    ) -> Result<ZoneReceipt, String> {
        audit.validate()?;
        if let Some(receipt) = self.previous(request)? {
            return Ok(receipt);
        }
        if self.receipts.len() >= 1024 {
            return Err("Zone command history limit reached".into());
        }
        if request.expected_revision != self.head.revision
            || request.expected_hash != self.head.content_hash()?
        {
            return Err("Stale zone command".into());
        }
        let (applied, base_change) = self.proposal(&request.action)?;
        if applied == self.head.corrections
            && base_change.as_ref().is_none_or(|c| c.applied == c.inverse)
        {
            return Err("Zone edit has no effect".into());
        }
        if matches!(request.action, ZoneAction::Edit(_)) {
            super::buildings::validate_locks(
                self.head
                    .corrections
                    .buildings
                    .as_deref()
                    .unwrap_or_default(),
                applied.buildings.as_deref().unwrap_or_default(),
            )?;
            super::rivers::validate_locks(
                self.head.corrections.rivers.as_deref().unwrap_or_default(),
                applied.rivers.as_deref().unwrap_or_default(),
            )?;
            super::paths::validate_locks(
                self.head.corrections.paths.as_deref().unwrap_or_default(),
                applied.paths.as_deref().unwrap_or_default(),
            )?;
            super::placements::validate_locks(
                self.head
                    .corrections
                    .placements
                    .as_deref()
                    .unwrap_or_default(),
                applied.placements.as_deref().unwrap_or_default(),
            )?;
            for old in self.head.corrections.density.iter().filter(|c| c.locked) {
                let replacement = applied.density.iter().find(|c| c.layer_id == old.layer_id);
                if replacement.is_none_or(|c| c.mask != old.mask) {
                    return Err("Unlock density correction before changing or removing it".into());
                }
            }
        }
        let mut candidate = self.head.clone();
        candidate.corrections = applied.clone();
        candidate.version = applied.schema_version();
        if let Some(change) = &base_change {
            candidate.base = change.applied.clone();
        }
        candidate.revision = candidate.revision.next().map_err(|e| e.to_string())?;
        let receipt = ZoneReceipt {
            request: request.clone(),
            applied,
            inverse: self.head.corrections.clone(),
            base_change,
            resulting_revision: candidate.revision,
            resulting_hash: candidate.content_hash()?,
            audit,
        };
        let mut receipts = self.receipts.clone();
        receipts.push(receipt.clone());
        self.encode(candidate, receipts)?;
        Ok(receipt)
    }

    pub fn execute(&mut self, request: &ZoneRequest) -> Result<ZoneReceipt, String> {
        self.execute_with_audit(request, AuditStamp::legacy())
    }

    pub fn execute_with_audit(
        &mut self,
        request: &ZoneRequest,
        audit: AuditStamp,
    ) -> Result<ZoneReceipt, String> {
        if let Some(receipt) = self.previous(request)? {
            return Ok(receipt);
        }
        let receipt = self.preview_with_audit(request, audit)?;
        match request.action {
            ZoneAction::Edit(_)
            | ZoneAction::Regenerate(_)
            | ZoneAction::ReconcilePromotions(_) => {
                self.timeline.truncate(self.cursor);
                self.timeline.push(receipt.clone());
                self.cursor += 1;
            }
            ZoneAction::Undo => self.cursor -= 1,
            ZoneAction::Redo => self.cursor += 1,
        }
        self.head.corrections = receipt.applied.clone();
        self.head.version = receipt.applied.schema_version();
        if let Some(change) = &receipt.base_change {
            self.head.base = change.applied.clone();
        }
        self.head.revision = receipt.resulting_revision;
        self.receipts.push(receipt.clone());
        Ok(receipt)
    }

    fn proposal(
        &self,
        action: &ZoneAction,
    ) -> Result<(ZoneCorrections, Option<Box<ZoneBaseChange>>), String> {
        match action {
            ZoneAction::Edit(corrections) => Ok(((**corrections).clone(), None)),
            ZoneAction::ReconcilePromotions(proposal) => Ok((
                super::promotion::reconcile(&self.head, proposal)?,
                Some(Box::new(ZoneBaseChange {
                    applied: proposal.base.clone(),
                    inverse: self.head.base.clone(),
                })),
            )),
            ZoneAction::Regenerate(base) => {
                let review = self.head.review_regeneration(*base.clone())?;
                if let Some(message) = review.refusal_message() {
                    return Err(message);
                }
                Ok((
                    self.head.corrections.clone(),
                    Some(Box::new(ZoneBaseChange {
                        applied: *base.clone(),
                        inverse: self.head.base.clone(),
                    })),
                ))
            }
            ZoneAction::Undo => {
                let prior = self
                    .cursor
                    .checked_sub(1)
                    .and_then(|i| self.timeline.get(i))
                    .ok_or("Nothing to undo")?;
                Ok((
                    prior.inverse.clone(),
                    prior.base_change.as_ref().map(|c| {
                        Box::new(ZoneBaseChange {
                            applied: c.inverse.clone(),
                            inverse: c.applied.clone(),
                        })
                    }),
                ))
            }
            ZoneAction::Redo => {
                let prior = self.timeline.get(self.cursor).ok_or("Nothing to redo")?;
                Ok((prior.applied.clone(), prior.base_change.clone()))
            }
        }
    }

    fn encode(
        &self,
        head: ZoneAuthoringHead,
        receipts: Vec<ZoneReceipt>,
    ) -> Result<Vec<u8>, String> {
        if self.delta_archive {
            return delta::encode(&self.migration, &head, &receipts);
        }
        if self.compact_archive {
            return match compact::encode(&self.migration, &head, &receipts) {
                Ok(bytes) => Ok(bytes),
                Err(message) if message == "Zone archive exceeds 4 MiB before mutation" => {
                    delta::encode(&self.migration, &head, &receipts)
                }
                Err(message) => Err(message),
            };
        }
        let bytes = canonical_json_bytes(&PlainArchive {
            schema: "forge.zone-edit-session",
            version: archive_version(&receipts),
            migration: &self.migration,
            head: &head,
            receipts: &receipts,
        })
        .map_err(|e| e.to_string())?;
        if bytes.len() > MAX_ZONE_HEAD_BYTES {
            // Keep every command and receipt, but switch to the lossless delta
            // representation before the plain archive would block editing.
            return delta::encode(&self.migration, &head, &receipts);
        }
        Ok(bytes)
    }

    pub fn archive_bytes(&self) -> Result<Vec<u8>, String> {
        self.encode(self.head.clone(), self.receipts.clone())
    }

    /// Prepare a lossless compact encoding (v3 or v5) without changing the active session.
    /// Opening these bytes opts into compact encoding for subsequent saves.
    pub fn compact_archive_bytes(&self) -> Result<Vec<u8>, String> {
        compact::encode(&self.migration, &self.head, &self.receipts)
    }

    /// Explicit lossless v6 conversion; does not change this session or write files.
    pub fn delta_archive_bytes(&self) -> Result<Vec<u8>, String> {
        delta::encode(&self.migration, &self.head, &self.receipts)
    }

    pub fn review_compaction(&self) -> Result<ZoneCompactionReview, String> {
        if self.delta_archive {
            return Err("Archive already uses compact encoding".into());
        }
        Ok(ZoneCompactionReview {
            head_hash: self.head.content_hash()?,
            source_version: if self.compact_archive {
                compact::version(&self.receipts)
            } else {
                archive_version(&self.receipts)
            },
            source_bytes: self.archive_bytes()?.len(),
            target_bytes: self.delta_archive_bytes()?.len(),
            retained_commands: self.receipts.len(),
        })
    }

    #[cfg(not(target_arch = "wasm32"))]
    pub(crate) fn apply_compaction(&mut self, review: &ZoneCompactionReview) -> Result<(), String> {
        if self.review_compaction()? != *review {
            return Err("Stale compaction review".into());
        }
        let candidate = Self::from_archive_bytes(&self.delta_archive_bytes()?)?;
        if candidate.head != self.head || candidate.receipts != self.receipts {
            return Err("Compaction changed the head or audit".into());
        }
        *self = candidate;
        Ok(())
    }

    pub fn from_archive_bytes(bytes: &[u8]) -> Result<Self, String> {
        #[derive(Deserialize)]
        struct Header {
            version: u32,
        }
        if bytes.len() > MAX_ZONE_HEAD_BYTES {
            return Err("Zone archive exceeds 4 MiB".into());
        }
        let header: Header = serde_json::from_slice(bytes).map_err(|e| e.to_string())?;
        if matches!(
            header.version,
            6 | 7 | 12 | 15 | 18 | 21 | 24 | 27 | 30 | 33
        ) {
            return delta::decode(bytes);
        }
        if matches!(
            header.version,
            3 | 5 | 9 | 11 | 14 | 17 | 20 | 23 | 26 | 29 | 32
        ) {
            return compact::decode(bytes);
        }
        let archive: Archive = serde_json::from_slice(bytes).map_err(|e| e.to_string())?;
        if archive.schema != "forge.zone-edit-session"
            || archive.version != archive_version(&archive.receipts)
            || archive.receipts.len() > 1024
        {
            return Err("Unsupported zone archive or command limit".into());
        }
        let mut session = Self::from_migration(archive.migration)?;
        for receipt in archive.receipts {
            if session.previous(&receipt.request)?.is_some() {
                return Err("Duplicate archived receipt".into());
            }
            if session.execute_with_audit(&receipt.request, receipt.audit.clone())? != receipt {
                return Err("Zone receipt replay mismatch".into());
            }
        }
        if session.head != archive.head {
            return Err("Zone head differs from audited result".into());
        }
        Ok(session)
    }
}

fn archive_version(receipts: &[ZoneReceipt]) -> u32 {
    if receipts.iter().any(|receipt| !receipt.audit.is_legacy()) {
        return 31;
    }
    if has_vegetation_exclusions(receipts) {
        return 28;
    }
    if has_reconciliation(receipts) {
        return 25;
    }
    if has_promotions(receipts) {
        return 22;
    }
    if has_species(receipts) {
        return 19;
    }
    if has_start(receipts) {
        return 16;
    }
    if has_buildings(receipts) {
        return 13;
    }
    if has_rivers(receipts) {
        return 10;
    }
    if has_paths(receipts) {
        return 8;
    }
    if receipts
        .iter()
        .any(|r| r.applied.placements.is_some() || r.inverse.placements.is_some())
    {
        return 4;
    }
    if receipts
        .iter()
        .any(|r| r.base_change.is_some() || matches!(r.request.action, ZoneAction::Regenerate(_)))
    {
        2
    } else {
        1
    }
}

fn has_paths(receipts: &[ZoneReceipt]) -> bool {
    receipts
        .iter()
        .any(|r| r.applied.paths.is_some() || r.inverse.paths.is_some())
}

fn has_vegetation_exclusions(receipts: &[ZoneReceipt]) -> bool {
    receipts.iter().any(|r| {
        r.applied.vegetation_exclusions.is_some() || r.inverse.vegetation_exclusions.is_some()
    })
}

fn has_rivers(receipts: &[ZoneReceipt]) -> bool {
    receipts
        .iter()
        .any(|r| r.applied.rivers.is_some() || r.inverse.rivers.is_some())
}

fn has_buildings(receipts: &[ZoneReceipt]) -> bool {
    receipts
        .iter()
        .any(|r| r.applied.buildings.is_some() || r.inverse.buildings.is_some())
}

fn has_start(receipts: &[ZoneReceipt]) -> bool {
    receipts
        .iter()
        .any(|r| r.applied.start.is_some() || r.inverse.start.is_some())
}

fn has_species(receipts: &[ZoneReceipt]) -> bool {
    receipts
        .iter()
        .any(|r| r.applied.species.is_some() || r.inverse.species.is_some())
}

fn has_promotions(receipts: &[ZoneReceipt]) -> bool {
    receipts
        .iter()
        .any(|r| r.applied.promotions.is_some() || r.inverse.promotions.is_some())
}

fn has_reconciliation(receipts: &[ZoneReceipt]) -> bool {
    receipts
        .iter()
        .any(|r| matches!(r.request.action, ZoneAction::ReconcilePromotions(_)))
}
