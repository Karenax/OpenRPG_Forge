use super::AuthoringHost;
use crate::authoring::game_save::{GameSaveRequest, GameSaveResponse, StoredGameSave};
use forge_core::document_store::DocumentStore;
use forge_gameplay::GameSession;
use std::{
    fs, io,
    path::{Path, PathBuf},
};

impl AuthoringHost {
    pub fn execute_game_save(&self, request: GameSaveRequest) -> Result<GameSaveResponse, String> {
        let (source, allow_edits, saving) = match &request {
            GameSaveRequest::Load {
                expected_source,
                allow_compatible_edits,
            } => (*expected_source, *allow_compatible_edits, false),
            GameSaveRequest::Save {
                expected_source, ..
            } => (*expected_source, false, true),
        };
        let snapshot = if let Some(active) = &self.playtest {
            if active.identity().source_hash != source {
                return Err("Game save source differs from active playtest".into());
            }
            active.clone()
        } else {
            if saving {
                return Err("Start playtest before saving game state".into());
            }
            self.active
                .as_ref()
                .ok_or("No authored zone open")?
                .playtest_snapshot(source)?
        };
        let document = snapshot
            .gameplay()
            .ok_or("This zone has no authored gameplay")?;
        if self.gameplay_module_id() != Some(document.module_id) {
            return Err("Game save module differs from the zone owner".into());
        }
        let directory = game_directory(&self.root, document.module_id, document.area_id)?;
        let store = DocumentStore::open(&directory).map_err(|e| e.to_string())?;
        match request {
            GameSaveRequest::Load { .. } => {
                let saved = store
                    .load()
                    .map_err(|e| e.to_string())?
                    .map(|stored| -> Result<StoredGameSave, String> {
                        GameSession::resume(document.clone(), source, &stored.bytes, allow_edits)
                            .map_err(|e| e.to_string())?;
                        Ok(StoredGameSave {
                            stored_hash: stored.hash,
                            source_hash: source,
                            save_json: String::from_utf8(stored.bytes)
                                .map_err(|e| e.to_string())?,
                        })
                    })
                    .transpose()?;
                Ok(GameSaveResponse { saved })
            }
            GameSaveRequest::Save {
                expected_stored,
                save_json,
                ..
            } => {
                GameSession::resume(document.clone(), source, save_json.as_bytes(), false)
                    .map_err(|e| e.to_string())?;
                let stored_hash = store
                    .save(expected_stored, save_json.as_bytes())
                    .map_err(|e| e.to_string())?;
                Ok(GameSaveResponse {
                    saved: Some(StoredGameSave {
                        stored_hash,
                        source_hash: source,
                        save_json,
                    }),
                })
            }
        }
    }
}

fn game_directory(
    root: &Path,
    module: forge_core::ModuleId,
    area: forge_core::AreaId,
) -> Result<PathBuf, String> {
    let mut parent = root.to_path_buf();
    // All components are fixed or validated Forge identities; the frontend supplies no path or slot.
    for component in [
        ".game-saves".to_owned(),
        module.to_string(),
        area.to_string(),
    ] {
        let child = parent.join(component);
        match fs::create_dir(&child) {
            Ok(()) => (),
            Err(error) if error.kind() == io::ErrorKind::AlreadyExists => (),
            Err(error) => return Err(error.to_string()),
        }
        let metadata = fs::symlink_metadata(&child).map_err(|e| e.to_string())?;
        let resolved = child.canonicalize().map_err(|e| e.to_string())?;
        if !metadata.file_type().is_dir()
            || metadata.file_type().is_symlink()
            || resolved.parent() != Some(parent.as_path())
        {
            return Err(
                "Game-save directory must be a direct regular child of the authorized root".into(),
            );
        }
        parent = resolved;
    }
    Ok(parent)
}
