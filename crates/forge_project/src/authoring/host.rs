//! Single active editor session rooted by the native host, never by a UI path.
use super::{AuthoringRequest, AuthoringState};
use crate::{audit::AuditStamp, terrain_edit::file_session::FileTerrainSession};
use std::path::{Path, PathBuf};

mod catalog;
mod compaction;
mod game_save;
mod gameplay;
mod managed;
mod recovery;
mod session;
mod terrain_source;
mod vegetation_source;
mod workspace;
use crate::zone_authoring::{file_session::FileZoneSession, migration::ZoneMigrationProposal};
use session::ActiveSession;

type ZoneValidator = fn(&crate::zone_authoring::ZoneAuthoringHead) -> Result<(), String>;

pub struct AuthoringHost {
    vegetation_source_cache: Option<vegetation_source::CachedBlock>,
    terrain_source_cache: Option<terrain_source::CachedRaster>,
    root: PathBuf,
    active: Option<ActiveSession>,
    playtest: Option<super::playtest::PlaytestSnapshot>,
    last_playtest_diagnostics: Option<super::playtest::PlaytestExitDiagnostics>,
    workspace: Option<crate::workspace::file_session::FileWorkspaceSession>,
    managed_review: Option<(
        crate::zone_authoring::session::ZoneRequest,
        crate::workspace::session::WorkspaceRequest,
        AuditStamp,
    )>,
    managed_tabs: Vec<super::ManagedZoneTab>,
    managed_compaction: Option<crate::workspace::session::WorkspaceRequest>,
    zone_validator: Option<ZoneValidator>,
    pending_audit: Option<AuditStamp>,
}

impl AuthoringHost {
    pub fn new(authorized_root: &Path) -> Result<Self, String> {
        let root = authorized_root
            .canonicalize()
            .map_err(|error| error.to_string())?;
        if !root.is_dir() {
            return Err("authoring root must be an existing directory".into());
        }
        Ok(Self {
            vegetation_source_cache: None,
            terrain_source_cache: None,
            root,
            active: None,
            playtest: None,
            last_playtest_diagnostics: None,
            workspace: None,
            managed_review: None,
            managed_tabs: Vec::new(),
            managed_compaction: None,
            zone_validator: None,
            pending_audit: None,
        })
    }

    /// Application policy for derived-world constraints, before any zone mutation.
    /// The project domain remains independent of the world generator.
    #[must_use]
    pub fn with_zone_validator(
        mut self,
        validator: fn(&crate::zone_authoring::ZoneAuthoringHead) -> Result<(), String>,
    ) -> Self {
        self.zone_validator = Some(validator);
        self
    }

    #[allow(
        clippy::too_many_lines,
        reason = "the exhaustive protocol dispatch stays centralized so every request shares the same state projection"
    )]
    pub fn execute(&mut self, command: AuthoringRequest) -> Result<AuthoringState, String> {
        self.validate_mode(&command)?;
        let mut receipt = None;
        let mut catalog = None;
        let mut recovery = None;
        let mut history = None;
        let mut zone_receipt = None;
        let mut zone_migration = None;
        let mut zone_compaction = None;
        match command {
            AuthoringRequest::PreparePlaytest { expected_hash } => {
                return self.prepare_playtest(expected_hash);
            }
            AuthoringRequest::ReviewZoneCompaction => {
                zone_compaction = Some(self.review_zone_compaction()?);
            }
            AuthoringRequest::CompactZoneArchive { review } => {
                self.compact_zone(&review)?;
            }
            AuthoringRequest::PrepareZoneMigration {
                expected_hash,
                generator,
                vegetation,
            } => {
                zone_migration = Some(self.prepare_migration(
                    expected_hash,
                    generator,
                    vegetation.map(|v| *v),
                )?);
            }
            AuthoringRequest::BeginZoneMigration { proposal } => {
                self.begin_migration(&proposal)?;
            }
            AuthoringRequest::PreviewZone { request } => {
                zone_receipt = Some(self.zone_command(&request, true)?);
            }
            AuthoringRequest::ApplyZone { request } => {
                zone_receipt = Some(self.zone_command(&request, false)?);
            }
            AuthoringRequest::ReadHistory {
                expected_hash,
                offset,
            } => {
                history = Some(self.read_history(expected_hash, offset)?);
            }
            AuthoringRequest::InspectRecovery { area_id } => {
                self.require_closed()?;
                recovery = Some(recovery::inspect(&self.root, area_id)?);
            }
            AuthoringRequest::Recover {
                area_id,
                expected_saved,
                expected_pending,
                promote,
            } => {
                self.require_closed()?;
                recovery::recover(
                    &self.root,
                    area_id,
                    expected_saved,
                    expected_pending,
                    promote,
                )?;
                // Recovery does not open a session or replace the viewport.
            }
            AuthoringRequest::StartPlaytest { expected_hash } => {
                self.start_playtest(expected_hash, None)?;
            }
            AuthoringRequest::StartPlaytestAt {
                expected_hash,
                start,
            } => {
                self.start_playtest(expected_hash, Some(start))?;
            }
            AuthoringRequest::StopPlaytest { diagnostics } => {
                self.stop_playtest(diagnostics)?;
            }
            AuthoringRequest::CreateZone { document } => {
                self.create_zone(*document)?;
            }
            AuthoringRequest::Create { document } => {
                self.create(*document)?;
            }
            AuthoringRequest::Open { area_id } => {
                self.open(area_id)?;
            }
            AuthoringRequest::OpenManaged { area_id } => self.open_managed(area_id)?,
            AuthoringRequest::Inspect => (),
            AuthoringRequest::ListZones => catalog = Some(catalog::list(&self.root)?),
            AuthoringRequest::Preview { request } => {
                receipt = Some(self.terrain_receipt(&request, true)?);
            }
            AuthoringRequest::Apply { request } => {
                receipt = Some(self.terrain_receipt(&request, false)?);
            }
            AuthoringRequest::Save => self.save()?,
            AuthoringRequest::Close => self.close()?,
        }
        let mut state = self.state(receipt, catalog, recovery)?;
        state.history = history;
        state.zone_receipt = zone_receipt;
        state.zone_migration = zone_migration;
        state.zone_compaction = zone_compaction;
        Ok(state)
    }

    pub fn execute_with_audit(
        &mut self,
        command: AuthoringRequest,
        audit: AuditStamp,
    ) -> Result<AuthoringState, String> {
        audit.validate()?;
        if command.persists_audit_receipt() && audit.is_legacy() {
            return Err(
                "new authoring receipts require an explicit human or AI audit stamp".into(),
            );
        }
        self.pending_audit = Some(audit);
        let result = self.execute(command);
        self.pending_audit = None;
        result
    }

    fn read_history(
        &self,
        expected_hash: forge_core::ContentHash,
        offset: usize,
    ) -> Result<super::history::HistoryPage, String> {
        self.active
            .as_ref()
            .ok_or("no authoring session open")?
            .history(expected_hash, offset)
    }

    fn prepare_playtest(
        &self,
        expected_hash: forge_core::ContentHash,
    ) -> Result<AuthoringState, String> {
        let snapshot = self
            .active
            .as_ref()
            .ok_or("no authoring session open")?
            .playtest_snapshot(expected_hash)?;
        let mut state = self.state(None, None, None)?;
        state.playtest_identity = Some(snapshot.identity());
        Ok(state)
    }

    fn terrain_receipt(
        &mut self,
        request: &crate::terrain_edit::TerrainEditRequest,
        preview: bool,
    ) -> Result<crate::terrain_edit::TerrainEditReceipt, String> {
        let audit = self
            .pending_audit
            .clone()
            .unwrap_or_else(AuditStamp::legacy);
        let session = self.active()?;
        if preview {
            session
                .session()
                .preview_with_audit(request, audit)
                .map_err(|error| error.to_string())
        } else {
            session
                .execute_with_audit(request, audit)
                .map_err(|error| error.to_string())
        }
    }

    fn validate_mode(&self, command: &AuthoringRequest) -> Result<(), String> {
        // A WebView remount may inspect a simulation, never replace its sources.
        if self.playtest.is_some()
            && !matches!(
                command,
                AuthoringRequest::Inspect
                    | AuthoringRequest::ListZones
                    | AuthoringRequest::ReadHistory { .. }
                    | AuthoringRequest::PreparePlaytest { .. }
                    | AuthoringRequest::StartPlaytest { .. }
                    | AuthoringRequest::StopPlaytest { .. }
            )
        {
            return Err("stop the playtest before editing, saving or changing zones".into());
        }
        Ok(())
    }

    fn prepare_migration(
        &mut self,
        expected_hash: forge_core::ContentHash,
        generator: crate::zone_authoring::GeneratorStamp,
        vegetation: Option<crate::VegetationDocument>,
    ) -> Result<ZoneMigrationProposal, String> {
        let legacy = self.active()?;
        ZoneMigrationProposal::prepare(
            &legacy.saved_archive().map_err(|e| e.to_string())?,
            expected_hash,
            generator,
            vegetation,
        )
    }

    fn close(&mut self) -> Result<(), String> {
        if self.active.as_ref().is_some_and(ActiveSession::is_dirty) {
            return Err("unsaved edits: save before closing".into());
        }
        if let Some(area) = self.managed_area() {
            // Load the neighbour before removing anything: a damaged archive must
            // leave the current tab and its projection available for diagnosis.
            let index = self.managed_tabs.iter().position(|t| t.area_id == area);
            let neighbour = index
                .and_then(|i| {
                    self.managed_tabs.get(i + 1).or_else(|| {
                        i.checked_sub(1)
                            .and_then(|previous| self.managed_tabs.get(previous))
                    })
                })
                .map(|t| t.area_id);
            if let Some(neighbour) = neighbour {
                self.open_managed(neighbour).map_err(|error| {
                    format!("Cannot close zone: neighbouring zone {neighbour} could not be loaded; current tab kept: {error}")
                })?;
                self.managed_tabs.retain(|t| t.area_id != area);
                return Ok(());
            }
            self.managed_tabs.retain(|t| t.area_id != area);
        }
        self.active = None;
        self.terrain_source_cache = None;
        self.vegetation_source_cache = None;
        self.managed_review = None;
        self.managed_compaction = None;
        Ok(())
    }

    fn begin_migration(&mut self, proposal: &ZoneMigrationProposal) -> Result<(), String> {
        self.require_closed()?;
        let directory = self.directory(proposal.head().base.terrain.area_id)?;
        self.active = Some(ActiveSession::Zone(Box::new(
            FileZoneSession::prepare_migration(&directory, proposal)?,
        )));
        Ok(())
    }

    fn start_playtest(
        &mut self,
        expected_hash: forge_core::ContentHash,
        start: Option<super::playtest::PlaytestStart>,
    ) -> Result<(), String> {
        let mut snapshot = self
            .active
            .as_ref()
            .ok_or("no authoring session open")?
            .playtest_snapshot(expected_hash)?;
        if start.is_some() {
            snapshot = snapshot.with_start(start)?;
        }
        self.playtest = Some(snapshot);
        self.last_playtest_diagnostics = None;
        Ok(())
    }

    fn stop_playtest(
        &mut self,
        diagnostics: Option<super::playtest::PlaytestExitDiagnostics>,
    ) -> Result<(), String> {
        match (&self.playtest, diagnostics) {
            (Some(snapshot), Some(diagnostics)) => {
                diagnostics.validate_for(snapshot.identity())?;
                self.last_playtest_diagnostics = Some(diagnostics);
            }
            (None, Some(_)) => return Err("playtest diagnostics require an active snapshot".into()),
            (_, None) => {}
        }
        self.playtest = None;
        Ok(())
    }

    fn save(&mut self) -> Result<(), String> {
        if self.managed_area().is_some() {
            return self.save_managed();
        }
        self.active
            .as_mut()
            .ok_or("no authoring session open")?
            .save()
    }

    fn create_zone(&mut self, document: crate::TerrainDocument) -> Result<(), String> {
        self.require_closed()?;
        let area_id = document.area_id;
        crate::workspace::ownership::require_unmanaged(&self.root, area_id)?;
        let session =
            crate::zone_authoring::session::ZoneEditSession::from_initial_terrain(document)?;
        let directory = self.root.join(area_id.to_string());
        // Validate everything before reserving a new directory; never reuse an archive.
        std::fs::create_dir(&directory).map_err(|e| e.to_string())?;
        self.active = Some(ActiveSession::Zone(Box::new(FileZoneSession::create(
            &directory, session,
        )?)));
        self.last_playtest_diagnostics = None;
        Ok(())
    }

    fn create(&mut self, document: crate::TerrainDocument) -> Result<(), String> {
        self.require_closed()?;
        document.validate().map_err(|e| e.to_string())?;
        crate::workspace::ownership::require_unmanaged(&self.root, document.area_id)?;
        let directory = self.root.join(document.area_id.to_string());
        // Never overwrite an existing zone or follow a pre-existing link.
        std::fs::create_dir(&directory).map_err(|e| e.to_string())?;
        self.active = Some(ActiveSession::Terrain(Box::new(
            FileTerrainSession::create(&directory, document).map_err(|e| e.to_string())?,
        )));
        self.last_playtest_diagnostics = None;
        Ok(())
    }

    fn open(&mut self, area_id: forge_core::AreaId) -> Result<(), String> {
        self.require_closed()?;
        let resolved = self.directory(area_id)?;
        let session = ActiveSession::open(&resolved)?;
        if session.document()?.area_id != area_id {
            return Err("zone identity does not match directory".into());
        }
        crate::workspace::ownership::require_unmanaged(&self.root, area_id)?;
        self.active = Some(session);
        self.last_playtest_diagnostics = None;
        Ok(())
    }

    fn directory(&self, area_id: forge_core::AreaId) -> Result<PathBuf, String> {
        let resolved = self
            .root
            .join(area_id.to_string())
            .canonicalize()
            .map_err(|e| e.to_string())?;
        if resolved.parent() != Some(self.root.as_path()) {
            return Err("zone path leaves authorized root".into());
        }
        Ok(resolved)
    }

    fn state(
        &self,
        receipt: Option<crate::terrain_edit::TerrainEditReceipt>,
        catalog: Option<super::ZoneCatalog>,
        recovery: Option<super::RecoveryReview>,
    ) -> Result<AuthoringState, String> {
        Ok(AuthoringState {
            gameplay_module_id: self.gameplay_module_id(),
            managed_tabs: self.managed_tab_state(),
            zone_head: self.active.as_ref().and_then(ActiveSession::head).cloned(),
            zone_receipt: None,
            zone_migration: None,
            zone_compaction: None,
            document: self
                .active
                .as_ref()
                .map(ActiveSession::document)
                .transpose()?,
            dirty: self.active.as_ref().is_some_and(ActiveSession::is_dirty),
            receipt,
            playtest_hash: self.playtest.as_ref().map(|s| s.identity().source_hash),
            playtest_start: self
                .playtest
                .as_ref()
                .and_then(super::playtest::PlaytestSnapshot::start),
            playtest_identity: self
                .playtest
                .as_ref()
                .map(super::playtest::PlaytestSnapshot::identity),
            last_playtest_diagnostics: self.last_playtest_diagnostics.clone(),
            catalog,
            recovery,
            history: None,
            can_undo: self.active.as_ref().is_some_and(ActiveSession::can_undo),
            can_redo: self.active.as_ref().is_some_and(ActiveSession::can_redo),
        })
    }

    fn active(&mut self) -> Result<&mut FileTerrainSession, String> {
        match self.active.as_mut() {
            Some(ActiveSession::Terrain(s)) => Ok(s),
            Some(ActiveSession::Zone(_) | ActiveSession::Managed { .. }) => {
                Err("aggregate zone requires zone commands; legacy operation unavailable".into())
            }
            None => Err("no authoring session open".into()),
        }
    }

    fn zone(&mut self) -> Result<&mut FileZoneSession, String> {
        match self.active.as_mut() {
            Some(ActiveSession::Zone(s)) => Ok(s),
            Some(ActiveSession::Terrain(_)) => {
                Err("review and explicitly migrate the legacy zone first".into())
            }
            Some(ActiveSession::Managed { .. }) => {
                Err("Managed-zone compaction is not yet exposed".into())
            }
            None => Err("no authoring session open".into()),
        }
    }

    fn require_closed(&self) -> Result<(), String> {
        if self.active.is_some() {
            return Err("close the current session first".into());
        }
        Ok(())
    }
}

#[cfg(test)]
mod tests {
    use super::*;
    use crate::{
        TerrainDocument,
        terrain_edit::{TerrainAction, TerrainEdit, TerrainEditRequest},
    };
    use forge_core::CommandId;

    #[test]
    fn playtest_freezes_exact_unsaved_document_without_touching_archive() {
        let directory =
            std::env::temp_dir().join(format!("forge-authoring-playtest-{}", std::process::id()));
        std::fs::create_dir(&directory).unwrap();
        let mut host = AuthoringHost::new(&directory).unwrap();
        let document = super::super::new_terrain("playtest", "zone", "Initial").unwrap();
        let old_hash = document.content_hash().unwrap();
        assert!(
            host.execute(AuthoringRequest::StartPlaytest {
                expected_hash: old_hash
            })
            .is_err()
        );
        host.execute(AuthoringRequest::Create {
            document: Box::new(document.clone()),
        })
        .unwrap();
        host.execute(AuthoringRequest::Save).unwrap();
        let path = directory
            .join(document.area_id.to_string())
            .join("authoring.json");
        let bytes = std::fs::read(&path).unwrap();
        let request = TerrainEditRequest {
            command_id: CommandId::deterministic("playtest-altitude"),
            expected_revision: document.revision,
            expected_hash: old_hash,
            action: TerrainAction::Edit(TerrainEdit::SetBaseElevation { metres: 8.0 }),
        };
        let edited = host
            .execute(AuthoringRequest::Apply {
                request: request.clone(),
            })
            .unwrap();
        let hash = edited.document.as_ref().unwrap().content_hash().unwrap();
        assert!(
            host.execute(AuthoringRequest::StartPlaytest {
                expected_hash: old_hash
            })
            .is_err()
        );
        let start = AuthoringRequest::StartPlaytest {
            expected_hash: hash,
        };
        let encoded = serde_json::to_vec(&start).unwrap();
        let playing = host
            .execute(serde_json::from_slice(&encoded).unwrap())
            .unwrap();
        assert_eq!(playing.playtest_hash, Some(hash));
        assert_eq!(playing.document, edited.document);
        assert!(playing.dirty);
        assert_eq!(host.execute(start).unwrap(), playing);
        for forbidden in [
            AuthoringRequest::Apply {
                request: request.clone(),
            },
            AuthoringRequest::Preview { request },
            AuthoringRequest::Save,
            AuthoringRequest::Close,
            AuthoringRequest::Open {
                area_id: document.area_id,
            },
            AuthoringRequest::Create {
                document: Box::new(document.clone()),
            },
        ] {
            assert!(host.execute(forbidden).is_err());
            assert_eq!(host.execute(AuthoringRequest::Inspect).unwrap(), playing);
            assert_eq!(std::fs::read(&path).unwrap(), bytes);
        }
        let stopped = host
            .execute(AuthoringRequest::StopPlaytest { diagnostics: None })
            .unwrap();
        assert_eq!(stopped, edited_without_receipt(edited));
        assert_eq!(
            host.execute(AuthoringRequest::StopPlaytest { diagnostics: None })
                .unwrap(),
            stopped
        );
        assert_eq!(std::fs::read(&path).unwrap(), bytes);
        host.execute(AuthoringRequest::Save).unwrap();
        host.execute(AuthoringRequest::Close).unwrap();
        let reopened = host
            .execute(AuthoringRequest::Open {
                area_id: document.area_id,
            })
            .unwrap();
        assert_eq!(reopened.document, stopped.document);
        assert_eq!(reopened.playtest_hash, None);
    }

    fn edited_without_receipt(mut state: AuthoringState) -> AuthoringState {
        state.receipt = None;
        state
    }

    #[test]
    fn stopping_playtest_retains_runtime_diagnostics_without_mutating_the_zone() {
        use crate::authoring::playtest::PlaytestExitDiagnostics;

        let directory = std::env::temp_dir().join(format!(
            "forge-authoring-playtest-diagnostics-{}",
            std::process::id()
        ));
        std::fs::create_dir(&directory).unwrap();
        let mut host = AuthoringHost::new(&directory).unwrap();
        let document = super::super::new_terrain("diagnostics", "zone", "Initial").unwrap();
        let hash = document.content_hash().unwrap();
        host.execute(AuthoringRequest::Create {
            document: Box::new(document.clone()),
        })
        .unwrap();
        host.execute(AuthoringRequest::Save).unwrap();
        let archive = directory
            .join(document.area_id.to_string())
            .join("authoring.json");
        let bytes_before = std::fs::read(&archive).unwrap();

        let playing = host
            .execute(AuthoringRequest::StartPlaytest {
                expected_hash: hash,
            })
            .unwrap();
        let identity = playing.playtest_identity.unwrap();
        let diagnostics = PlaytestExitDiagnostics {
            identity,
            fixed_tick: 42,
            final_position_m: [12.5, 2.0, -3.25],
            grounded: true,
            collisions: 2,
            simulation_ready: true,
            runtime_status: "Simulation active · contacts Rapier disponibles".into(),
            end_reason: "retour_edition".into(),
        };
        let stopped = host
            .execute(AuthoringRequest::StopPlaytest {
                diagnostics: Some(diagnostics.clone()),
            })
            .unwrap();

        assert_eq!(stopped.playtest_hash, None);
        assert_eq!(stopped.playtest_identity, None);
        assert_eq!(stopped.last_playtest_diagnostics, Some(diagnostics));
        assert_eq!(stopped.document, playing.document);
        assert_eq!(std::fs::read(&archive).unwrap(), bytes_before);

        let legacy: AuthoringRequest =
            serde_json::from_str(r#"{"action":"stop_playtest"}"#).unwrap();
        assert_eq!(legacy, AuthoringRequest::StopPlaytest { diagnostics: None });
    }

    #[test]
    fn authoring_protocol_round_trip_and_unsaved_guards() {
        let directory =
            std::env::temp_dir().join(format!("forge-authoring-host-{}", std::process::id()));
        std::fs::create_dir(&directory).unwrap();
        let mut host = AuthoringHost::new(&directory).unwrap();
        let document: TerrainDocument = serde_json::from_str(include_str!(
            "../../../../examples/visual_valley/terrain.v1.json"
        ))
        .unwrap();
        let id = document.area_id;
        let request = AuthoringRequest::Create {
            document: Box::new(document.clone()),
        };
        let encoded = serde_json::to_vec(&request).unwrap();
        assert!(
            host.execute(serde_json::from_slice(&encoded).unwrap())
                .unwrap()
                .dirty
        );
        assert!(host.execute(AuthoringRequest::Close).is_err());
        assert!(
            host.execute(AuthoringRequest::Open { area_id: id })
                .is_err()
        );
        let edit = TerrainEditRequest {
            command_id: CommandId::deterministic("host-rename"),
            expected_revision: document.revision,
            expected_hash: document.content_hash().unwrap(),
            action: TerrainAction::Edit(TerrainEdit::Rename {
                name: "Zone auteur".into(),
            }),
        };
        let preview = host
            .execute(AuthoringRequest::Preview {
                request: edit.clone(),
            })
            .unwrap();
        assert_eq!(preview.document, Some(document));
        let applied = host
            .execute(AuthoringRequest::Apply { request: edit })
            .unwrap();
        assert_eq!(applied.receipt, preview.receipt);
        assert!(!host.execute(AuthoringRequest::Save).unwrap().dirty);
        host.execute(AuthoringRequest::Close).unwrap();
        let reopened = host
            .execute(AuthoringRequest::Open { area_id: id })
            .unwrap();
        assert_eq!(reopened.document, applied.document);
        assert!(!reopened.dirty);
        host.execute(AuthoringRequest::Close).unwrap();
        assert!(host.execute(AuthoringRequest::Save).is_err());
    }

    #[test]
    fn viewport_remount_inspection_preserves_unsaved_session_without_saving() {
        let directory =
            std::env::temp_dir().join(format!("forge-authoring-remount-{}", std::process::id()));
        std::fs::create_dir(&directory).unwrap();
        let mut host = AuthoringHost::new(&directory).unwrap();
        let document = super::super::new_terrain("remount", "zone", "Initial").unwrap();
        let id = document.area_id;
        host.execute(AuthoringRequest::Create {
            document: Box::new(document.clone()),
        })
        .unwrap();
        host.execute(AuthoringRequest::Save).unwrap();
        let saved_path = directory.join(id.to_string()).join("authoring.json");
        let saved_bytes = std::fs::read(&saved_path).unwrap();
        let applied = host
            .execute(AuthoringRequest::Apply {
                request: TerrainEditRequest {
                    command_id: CommandId::deterministic("remount-unsaved"),
                    expected_revision: document.revision,
                    expected_hash: document.content_hash().unwrap(),
                    action: TerrainAction::Edit(TerrainEdit::Rename {
                        name: "Retouche non enregistrée".into(),
                    }),
                },
            })
            .unwrap();
        // A WebView remount invokes Inspect, not Open/Create or implicit Save.
        for _ in 0..3 {
            let inspected = host.execute(AuthoringRequest::Inspect).unwrap();
            assert_eq!(inspected.document, applied.document);
            assert!(inspected.dirty);
            assert!(inspected.receipt.is_none());
            assert_eq!(std::fs::read(&saved_path).unwrap(), saved_bytes);
        }
        assert!(host.execute(AuthoringRequest::Close).is_err());
        assert!(
            host.execute(AuthoringRequest::Open { area_id: id })
                .is_err()
        );
        assert_eq!(
            host.execute(AuthoringRequest::Inspect).unwrap().document,
            applied.document
        );
        host.execute(AuthoringRequest::Save).unwrap();
        host.execute(AuthoringRequest::Close).unwrap();
        assert!(
            host.execute(AuthoringRequest::Inspect)
                .unwrap()
                .document
                .is_none()
        );
        let reopened = host
            .execute(AuthoringRequest::Open { area_id: id })
            .unwrap();
        assert_eq!(reopened.document, applied.document);
        assert!(!reopened.dirty);
    }
}
