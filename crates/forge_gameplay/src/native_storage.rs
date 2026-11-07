//! Native user-data storage. Authored archives are never storage destinations.
use crate::{GameSession, GameplayDocument};
use forge_core::{AreaId, ContentHash, ModuleId, document_store::DocumentStore};
use serde::{Deserialize, Serialize};
use std::{
    fs,
    io::Write,
    path::{Path, PathBuf},
};

#[derive(Serialize, Deserialize)]
#[serde(deny_unknown_fields)]
struct ResumeRequest {
    schema: String,
    version: u32,
    module: ModuleId,
    area: AreaId,
    saved_hash: ContentHash,
    allow_compatible_edits: bool,
}

/// Read-only ownership view of the existing canonical `GameSession` envelope.
/// `GameSession` remains responsible for state/schema validity before storage.
#[derive(Deserialize)]
struct SaveOwner {
    module_id: ModuleId,
    area_id: AreaId,
}

/// Whether a pinned resume request was created or was already present unchanged.
#[derive(Clone, Copy, Debug, PartialEq, Eq)]
pub enum ResumeRequestDisposition {
    Created,
    Existing,
}

/// Explicit standalone-game intent. Legacy callers retain their existing behavior.
#[derive(Clone, Copy, Debug, Default, PartialEq, Eq)]
pub enum StartIntent {
    #[default]
    Legacy,
    Fresh,
    Resume(ContentHash),
}

/// Inspect a slot without creating directories or changing a pending request.
/// The result is a snapshot; the runtime must recheck the intent under its lock.
///
/// # Errors
/// Refuses invalid directories, links and unreadable or oversized requests.
pub fn has_pending_resume(root: &Path, module: ModuleId, area: AreaId) -> Result<bool, String> {
    let Some(directory) = existing_directory(root, module, area)? else {
        return Ok(false);
    };
    Ok(read_request(&directory.join("resume.json"))?.is_some())
}

pub struct GameStorage {
    store: DocumentStore,
    directory: PathBuf,
    stored_hash: Option<ContentHash>,
    consumed_request: bool,
    module: ModuleId,
    area: AreaId,
    request_hash: Option<ContentHash>,
}
impl GameStorage {
    /// Copy an exact, validated save into an empty slot, preserving its bytes.
    /// A retry of the same copy returns the same hash.
    pub fn import_exact_save(
        source_root: &Path,
        target_root: &Path,
        document: &GameplayDocument,
        source: ContentHash,
        expected: Option<ContentHash>,
    ) -> Result<ContentHash, String> {
        let legacy = Self::open_existing(source_root, document)?
            .ok_or("Aucune sauvegarde historique pour cette carte.")?;
        let saved = legacy
            .store
            .load()
            .map_err(|error| error.to_string())?
            .ok_or("La sauvegarde historique est absente.")?;
        if expected.is_some_and(|hash| hash != saved.hash) {
            return Err("La sauvegarde historique a changé. Vérifiez-la à nouveau.".into());
        }
        GameSession::resume(document.clone(), source, &saved.bytes, false)
            .map_err(|error| error.to_string())?;
        let mut target = Self::open(target_root, document)?;
        if target.stored_hash == Some(saved.hash) {
            return Ok(saved.hash);
        }
        if target.stored_hash.is_some() {
            return Err("Ce jeu possède déjà une autre sauvegarde pour cette carte.".into());
        }
        target.save(&saved.bytes)?;
        Ok(saved.hash)
    }

    pub fn metadata(&self) -> Result<Option<serde_json::Value>, String> {
        let Some(saved) = self.store.load().map_err(|e| e.to_string())? else {
            return Ok(None);
        };
        let envelope: serde_json::Value =
            serde_json::from_slice(&saved.bytes).map_err(|e| e.to_string())?;
        Ok(Some(
            serde_json::json!({"path":self.directory.join("authoring.json"),"sha256":saved.hash,
            "state_hash":envelope.get("state_hash"),"source_hash":envelope.get("source_hash"),
            "functional_hash":envelope.get("functional_hash")}),
        ))
    }
    pub fn copy_committed_save(&self, output: &Path) -> Result<serde_json::Value, String> {
        let saved = self
            .store
            .load()
            .map_err(|e| e.to_string())?
            .ok_or("Committed save absent")?;
        let path = output.join(format!("saved-{}.json", saved.hash));
        let mut file = fs::OpenOptions::new()
            .write(true)
            .create_new(true)
            .open(&path)
            .map_err(|e| e.to_string())?;
        file.write_all(&saved.bytes)
            .and_then(|()| file.sync_all())
            .map_err(|e| e.to_string())?;
        Ok(serde_json::json!({"path":path,"sha256":saved.hash}))
    }
    pub fn open(root: &Path, document: &GameplayDocument) -> Result<Self, String> {
        fs::create_dir_all(root).map_err(|e| e.to_string())?;
        let root = root.canonicalize().map_err(|e| e.to_string())?;
        let mut directory = root.clone();
        for component in [document.module_id.to_string(), document.area_id.to_string()] {
            directory = directory.join(component);
            match fs::create_dir(&directory) {
                Ok(()) => (),
                Err(e) if e.kind() == std::io::ErrorKind::AlreadyExists => (),
                Err(e) => return Err(e.to_string()),
            }
            let metadata = fs::symlink_metadata(&directory).map_err(|e| e.to_string())?;
            let resolved = directory.canonicalize().map_err(|e| e.to_string())?;
            if !metadata.is_dir()
                || metadata.file_type().is_symlink()
                || !resolved.starts_with(&root)
            {
                return Err("Dossier de sauvegarde invalide.".into());
            }
            directory = resolved;
        }
        Self::open_directory(directory, document)
    }

    /// Open an existing saved game without creating a root, module or area
    /// directory. A missing committed save is reported as `None`.
    ///
    /// # Errors
    /// Refuses invalid paths, links, unreadable files and an occupied store.
    pub fn open_existing(root: &Path, document: &GameplayDocument) -> Result<Option<Self>, String> {
        let Some(directory) = existing_directory(root, document.module_id, document.area_id)?
        else {
            return Ok(None);
        };
        match fs::symlink_metadata(directory.join("authoring.json")) {
            Ok(metadata) if metadata.is_file() && !metadata.file_type().is_symlink() => (),
            Ok(_) => return Err("Fichier de sauvegarde invalide.".into()),
            Err(error) if error.kind() == std::io::ErrorKind::NotFound => return Ok(None),
            Err(error) => return Err(error.to_string()),
        }
        let storage = Self::open_directory(directory, document)?;
        Ok(storage.stored_hash.is_some().then_some(storage))
    }

    fn open_directory(directory: PathBuf, document: &GameplayDocument) -> Result<Self, String> {
        let store = DocumentStore::open(&directory).map_err(|e| e.to_string())?;
        let stored_hash = store
            .load()
            .map_err(|e| e.to_string())?
            .map(|save| save.hash);
        Ok(Self {
            store,
            directory,
            stored_hash,
            consumed_request: false,
            module: document.module_id,
            area: document.area_id,
            request_hash: None,
        })
    }

    /// Validate an existing save against the admitted gameplay document and
    /// return its exact hash without creating a resume request.
    ///
    /// # Errors
    /// Refuses a different owner, unreadable save, or incompatible document.
    pub fn resume_candidate(
        &self,
        document: &GameplayDocument,
        source: ContentHash,
        compatible: bool,
    ) -> Result<Option<ContentHash>, String> {
        if document.module_id != self.module || document.area_id != self.area {
            return Err("Cette sauvegarde concerne une autre zone ou aventure.".into());
        }
        let Some(saved) = self.store.load().map_err(|error| error.to_string())? else {
            return Ok(None);
        };
        GameSession::resume(document.clone(), source, &saved.bytes, compatible)
            .map_err(|error| error.to_string())?;
        Ok(Some(saved.hash))
    }
    pub fn requested_resume(
        &mut self,
        document: &GameplayDocument,
    ) -> Result<(Option<Vec<u8>>, bool), String> {
        self.requested_resume_with_intent(document, StartIntent::Legacy)
    }

    /// Recheck a standalone launch intent while this storage owns its lock.
    ///
    /// # Errors
    /// Preserves and refuses a missing, contradictory or changed resume request.
    pub fn requested_resume_with_intent(
        &mut self,
        document: &GameplayDocument,
        intent: StartIntent,
    ) -> Result<(Option<Vec<u8>>, bool), String> {
        if document.module_id != self.module || document.area_id != self.area {
            return Err("Cette sauvegarde concerne une autre zone ou aventure.".into());
        }
        if intent == StartIntent::Fresh
            && self
                .store
                .load()
                .map_err(|error| error.to_string())?
                .is_some()
        {
            return Err("Une sauvegarde existe. Choisissez de reprendre cette partie.".into());
        }
        let path = self.directory.join("resume.json");
        let Some(bytes) = read_request(&path)? else {
            return if matches!(intent, StartIntent::Resume(_)) {
                Err("La demande de reprise est absente.".into())
            } else {
                Ok((None, false))
            };
        };
        if intent == StartIntent::Fresh {
            return Err(
                "Une reprise est en attente ; choisissez explicitement de reprendre.".into(),
            );
        }
        let request: ResumeRequest = serde_json::from_slice(&bytes).map_err(|e| e.to_string())?;
        let saved = self
            .store
            .load()
            .map_err(|e| e.to_string())?
            .ok_or("Sauvegarde demandée absente.")?;
        if request.schema != "forge.native-game-resume"
            || request.version != 1
            || request.module != document.module_id
            || request.area != document.area_id
            || request.saved_hash != saved.hash
            || matches!(intent, StartIntent::Resume(expected) if expected != saved.hash || request.allow_compatible_edits)
        {
            return Err("La demande de reprise ne correspond plus à la sauvegarde.".into());
        }
        self.consumed_request = true;
        self.request_hash = Some(forge_core::bytes_sha256(&bytes));
        Ok((Some(saved.bytes), request.allow_compatible_edits))
    }
    pub fn consume_resume_request(&mut self) -> Result<(), String> {
        if self.consumed_request {
            self.check_owned_request()?;
            fs::remove_file(self.directory.join("resume.json")).map_err(|e| e.to_string())?;
            self.consumed_request = false;
            self.request_hash = None;
        }
        Ok(())
    }
    pub fn save(&mut self, bytes: &[u8]) -> Result<(), String> {
        self.save_with_diagnostic(bytes, |_| {})
    }

    /// Preserve the caller's native diagnostics without coupling storage to a renderer.
    pub fn save_with_diagnostic(
        &mut self,
        bytes: &[u8],
        invalid_envelope: impl FnOnce(&serde_json::Error),
    ) -> Result<(), String> {
        // Reject wrong dispatch before touching any committed or staging file.
        // Use the same 4 MiB bound as the canonical game save and DocumentStore.
        if bytes.len() > 4 * 1024 * 1024 {
            return Err("La sauvegarde dépasse la limite de 4 Mio. Gardez la partie ouverte et consultez les diagnostics.".into());
        }
        let owner: SaveOwner = serde_json::from_slice(bytes).map_err(|error| {
            invalid_envelope(&error);
            "La sauvegarde ne peut pas être lue. Gardez la partie ouverte et consultez les diagnostics.".to_owned()
        })?;
        if owner.module_id != self.module || owner.area_id != self.area {
            return Err("Cette sauvegarde concerne une autre zone ou aventure. Gardez la partie ouverte et consultez les diagnostics.".into());
        }
        if read_request(&self.directory.join("resume.json"))?.is_some() {
            return Err("Une reprise est déjà demandée. Fermez puis relancez le jeu avant une autre sauvegarde.".into());
        }
        self.stored_hash = Some(
            self.store
                .save(self.stored_hash, bytes)
                .map_err(|e| e.to_string())?,
        );
        Ok(())
    }
    pub fn request_resume(
        &mut self,
        document: &GameplayDocument,
        source: ContentHash,
        compatible: bool,
    ) -> Result<(), String> {
        self.request_resume_inner(document, source, compatible, None)
            .map(|_| ())
    }

    /// Request a previously inspected save by its exact hash. A different
    /// existing request is preserved instead of being replaced.
    ///
    /// # Errors
    /// Refuses changed or incompatible saves and requests owned by another choice.
    pub fn request_pinned_resume(
        &mut self,
        document: &GameplayDocument,
        source: ContentHash,
        compatible: bool,
        expected: ContentHash,
    ) -> Result<ResumeRequestDisposition, String> {
        self.request_resume_inner(document, source, compatible, Some(expected))
    }

    fn request_resume_inner(
        &mut self,
        document: &GameplayDocument,
        source: ContentHash,
        compatible: bool,
        expected: Option<ContentHash>,
    ) -> Result<ResumeRequestDisposition, String> {
        if document.module_id != self.module || document.area_id != self.area {
            return Err("Cette sauvegarde concerne une autre zone ou aventure.".into());
        }
        let saved = self
            .store
            .load()
            .map_err(|e| e.to_string())?
            .ok_or("Sauvegardez une partie avant de demander une reprise.")?;
        if expected.is_some_and(|hash| hash != saved.hash) {
            return Err("La sauvegarde a changé depuis sa vérification.".into());
        }
        GameSession::resume(document.clone(), source, &saved.bytes, compatible)
            .map_err(|e| e.to_string())?;
        let bytes = serde_json::to_vec(&ResumeRequest {
            schema: "forge.native-game-resume".into(),
            version: 1,
            module: document.module_id,
            area: document.area_id,
            saved_hash: saved.hash,
            allow_compatible_edits: compatible,
        })
        .map_err(|e| e.to_string())?;
        let path = self.directory.join("resume.json");
        if let Some(previous) = read_request(&path)? {
            let owned: ResumeRequest =
                serde_json::from_slice(&previous).map_err(|e| e.to_string())?;
            if owned.schema != "forge.native-game-resume"
                || owned.version != 1
                || owned.module != self.module
                || owned.area != self.area
                || owned.saved_hash != saved.hash
            {
                return Err(
                    "La demande de reprise a changé ou appartient à une autre partie.".into(),
                );
            }
            if previous != bytes {
                if expected.is_some() {
                    return Err("Une autre demande de reprise est déjà présente.".into());
                }
                let staging = self.directory.join("resume.pending");
                let mut file = fs::OpenOptions::new()
                    .write(true)
                    .create_new(true)
                    .open(&staging)
                    .map_err(|e| e.to_string())?;
                file.write_all(&bytes)
                    .and_then(|()| file.sync_all())
                    .map_err(|e| e.to_string())?;
                if read_request(&path)?.as_deref() != Some(previous.as_slice()) {
                    return Err("La demande de reprise a changé pendant le choix.".into());
                }
                fs::rename(staging, &path).map_err(|e| e.to_string())?;
            }
            // The pinned launcher path must not acquire ownership of a request
            // that was already present, even when its bytes happen to match.
            self.request_hash = expected.is_none().then(|| forge_core::bytes_sha256(&bytes));
            self.consumed_request = false;
            return Ok(ResumeRequestDisposition::Existing);
        }
        let mut file = fs::OpenOptions::new()
            .write(true)
            .create_new(true)
            .open(path)
            .map_err(|e| e.to_string())?;
        file.write_all(&bytes)
            .and_then(|()| file.sync_all())
            .map_err(|e| e.to_string())?;
        self.request_hash = Some(forge_core::bytes_sha256(&bytes));
        self.consumed_request = false;
        Ok(ResumeRequestDisposition::Created)
    }
    pub fn cancel_resume(&mut self, document: &GameplayDocument) -> Result<(), String> {
        if document.module_id != self.module || document.area_id != self.area {
            return Err("Une autre partie ne peut pas annuler cette reprise.".into());
        }
        self.check_owned_request()?;
        fs::remove_file(self.directory.join("resume.json")).map_err(|e| e.to_string())?;
        self.consumed_request = false;
        self.request_hash = None;
        Ok(())
    }
    fn check_owned_request(&self) -> Result<(), String> {
        let bytes = read_request(&self.directory.join("resume.json"))?
            .ok_or("Demande de reprise absente.")?;
        let request: ResumeRequest = serde_json::from_slice(&bytes).map_err(|e| e.to_string())?;
        let save = self
            .store
            .load()
            .map_err(|e| e.to_string())?
            .ok_or("Sauvegarde demandée absente.")?;
        if self.request_hash != Some(forge_core::bytes_sha256(&bytes))
            || request.schema != "forge.native-game-resume"
            || request.version != 1
            || request.module != self.module
            || request.area != self.area
            || request.saved_hash != save.hash
        {
            return Err("La demande de reprise ou la sauvegarde a changé.".into());
        }
        Ok(())
    }
}

fn existing_directory(
    root: &Path,
    module: ModuleId,
    area: AreaId,
) -> Result<Option<PathBuf>, String> {
    let metadata = match fs::symlink_metadata(root) {
        Ok(metadata) => metadata,
        Err(error) if error.kind() == std::io::ErrorKind::NotFound => return Ok(None),
        Err(error) => return Err(error.to_string()),
    };
    if !metadata.is_dir() || metadata.file_type().is_symlink() {
        return Err("Dossier de sauvegarde invalide.".into());
    }
    let root = root.canonicalize().map_err(|error| error.to_string())?;
    let mut directory = root.clone();
    for component in [module.to_string(), area.to_string()] {
        directory = directory.join(component);
        let metadata = match fs::symlink_metadata(&directory) {
            Ok(metadata) => metadata,
            Err(error) if error.kind() == std::io::ErrorKind::NotFound => return Ok(None),
            Err(error) => return Err(error.to_string()),
        };
        let resolved = directory
            .canonicalize()
            .map_err(|error| error.to_string())?;
        if !metadata.is_dir() || metadata.file_type().is_symlink() || !resolved.starts_with(&root) {
            return Err("Dossier de sauvegarde invalide.".into());
        }
        directory = resolved;
    }
    Ok(Some(directory))
}

fn read_request(path: &Path) -> Result<Option<Vec<u8>>, String> {
    let metadata = match fs::symlink_metadata(path) {
        Ok(metadata) => metadata,
        Err(error) if error.kind() == std::io::ErrorKind::NotFound => return Ok(None),
        Err(error) => return Err(error.to_string()),
    };
    if !metadata.is_file() || metadata.file_type().is_symlink() || metadata.len() > 4096 {
        return Err("Demande de reprise invalide.".into());
    }
    fs::read(path).map(Some).map_err(|e| e.to_string())
}
