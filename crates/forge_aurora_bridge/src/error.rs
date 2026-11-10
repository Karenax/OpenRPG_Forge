#[derive(Debug, thiserror::Error)]
pub enum BridgeError {
    #[error("I/O error at {path}: {source}")]
    Io {
        path: String,
        #[source]
        source: std::io::Error,
    },
    #[error("JSON error at {path}: {source}")]
    Json {
        path: String,
        #[source]
        source: serde_json::Error,
    },
    #[error("invalid bundle: {0}")]
    Invalid(String),
    #[error("unsupported bundle version: {0}")]
    UnsupportedVersion(String),
    #[error("unsafe bundle path: {0}")]
    UnsafePath(String),
    #[error("bundle size limit exceeded: {0}")]
    SizeLimit(String),
    #[error("bundle hash mismatch: {0}")]
    HashMismatch(String),
    #[error("bundle file missing: {0}")]
    Missing(String),
    #[error("bundle rights block import: {0}")]
    RightsBlocked(String),
}

impl BridgeError {
    pub fn io(path: impl Into<String>, source: std::io::Error) -> Self {
        Self::Io {
            path: path.into(),
            source,
        }
    }

    pub fn json(path: impl Into<String>, source: serde_json::Error) -> Self {
        Self::Json {
            path: path.into(),
            source,
        }
    }

    pub fn invalid(detail: impl Into<String>) -> Self {
        Self::Invalid(detail.into())
    }
}

pub type Result<T> = std::result::Result<T, BridgeError>;

#[must_use]
pub fn display_path(path: &std::path::Path) -> String {
    path.to_string_lossy().into_owned()
}
