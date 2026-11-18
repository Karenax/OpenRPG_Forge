//! Byte-only desktop transport for the module selected in Studio.
use serde::{Deserialize, Serialize};

pub const CHUNK_BYTES: usize = 8 * 1024 * 1024;
pub const UPLOAD_HEADER: &str = "x-forge-module-upload";
pub const OFFSET_HEADER: &str = "x-forge-module-offset";

#[derive(Deserialize, Serialize)]
#[serde(tag = "kind", rename_all = "snake_case", deny_unknown_fields)]
pub enum Control {
    Start { bytes: u32 },
    Finish { upload: u32 },
    Cancel { upload: u32 },
}

#[must_use]
pub fn size_error() -> String {
    format!(
        "Choisissez un fichier .orpmod de {} Mio maximum.",
        forge_project::workspace::MAX_MODULE_ORIGIN_PACKAGE_BYTES / (1024 * 1024)
    )
}
