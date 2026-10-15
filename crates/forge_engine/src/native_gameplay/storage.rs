//! Native user-data storage. Authored archives are never storage destinations.
use bevy::prelude::Resource;
use forge_core::{AreaId, ContentHash, ModuleId, document_store::DocumentStore};
use forge_gameplay::{GameSession, GameplayDocument};
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

#[derive(Resource)]
pub(super) struct GameStorage {
    store: DocumentStore,
    directory: PathBuf,
    stored_hash: Option<ContentHash>,
    consumed_request: bool,
    module: ModuleId,
    area: AreaId,
    request_hash: Option<ContentHash>,
}
impl GameStorage {
    pub(super) fn metadata(&self) -> Result<Option<serde_json::Value>, String> {
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
    pub(super) fn copy_committed_save(&self, output: &Path) -> Result<serde_json::Value, String> {
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
    pub(super) fn open(root: &Path, document: &GameplayDocument) -> Result<Self, String> {
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
    pub(super) fn requested_resume(
        &mut self,
        document: &GameplayDocument,
    ) -> Result<(Option<Vec<u8>>, bool), String> {
        let path = self.directory.join("resume.json");
        let Some(bytes) = read_request(&path)? else {
            return Ok((None, false));
        };
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
        {
            return Err("La demande de reprise ne correspond plus à la sauvegarde.".into());
        }
        self.consumed_request = true;
        self.request_hash = Some(forge_core::bytes_sha256(&bytes));
        Ok((Some(saved.bytes), request.allow_compatible_edits))
    }
    pub(super) fn consume_resume_request(&mut self) -> Result<(), String> {
        if self.consumed_request {
            self.check_owned_request()?;
            fs::remove_file(self.directory.join("resume.json")).map_err(|e| e.to_string())?;
            self.consumed_request = false;
            self.request_hash = None;
        }
        Ok(())
    }
    pub(super) fn save(&mut self, bytes: &[u8]) -> Result<(), String> {
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
    pub(super) fn request_resume(
        &mut self,
        document: &GameplayDocument,
        source: ContentHash,
        compatible: bool,
    ) -> Result<(), String> {
        let saved = self
            .store
            .load()
            .map_err(|e| e.to_string())?
            .ok_or("Sauvegardez une partie avant de demander une reprise.")?;
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
            self.request_hash = Some(forge_core::bytes_sha256(&bytes));
            self.consumed_request = false;
            return Ok(());
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
        Ok(())
    }
    pub(super) fn cancel_resume(&mut self, document: &GameplayDocument) -> Result<(), String> {
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
