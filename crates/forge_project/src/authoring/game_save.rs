//! Game-state storage is independent from authoring archives. No UI paths cross the protocol.
use forge_core::ContentHash;
use serde::{Deserialize, Serialize};

#[derive(Clone, Debug, PartialEq, Serialize, Deserialize)]
#[serde(tag = "action", rename_all = "snake_case", deny_unknown_fields)]
pub enum GameSaveRequest {
    Load {
        expected_source: ContentHash,
        allow_compatible_edits: bool,
    },
    Save {
        expected_source: ContentHash,
        expected_stored: Option<ContentHash>,
        save_json: String,
    },
}

#[derive(Clone, Debug, PartialEq, Serialize, Deserialize)]
#[serde(deny_unknown_fields)]
pub struct StoredGameSave {
    pub stored_hash: ContentHash,
    pub source_hash: ContentHash,
    pub save_json: String,
}

#[derive(Clone, Debug, PartialEq, Serialize, Deserialize)]
#[serde(deny_unknown_fields)]
pub struct GameSaveResponse {
    pub saved: Option<StoredGameSave>,
}
