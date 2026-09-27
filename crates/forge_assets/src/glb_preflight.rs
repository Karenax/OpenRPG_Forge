//! Bounded, engine-neutral admission checks for self-contained governed preview assets.
use serde_json::Value;
use thiserror::Error;

#[derive(Debug, Error)]
pub enum GovernedGlbError {
    #[error("invalid GLB v2 header or declared length")]
    Header,
    #[error("GLB JSON chunk is missing, unaligned or exceeds the 16 MiB preview limit")]
    JsonBounds,
    #[error("invalid GLB JSON: {0}")]
    Json(#[from] serde_json::Error),
    #[error("governed preview requires a glTF 2.0 object without URI references")]
    ExternalOrInvalidDocument,
}

/// Inspect only the fixed header before allocating a bounded JSON chunk.
pub fn governed_glb_json_length(
    header: &[u8; 20],
    file_length: u64,
) -> Result<usize, GovernedGlbError> {
    let number = |start: usize| {
        u32::from_le_bytes([
            header[start],
            header[start + 1],
            header[start + 2],
            header[start + 3],
        ])
    };
    if &header[..4] != b"glTF" || number(4) != 2 || u64::from(number(8)) != file_length {
        return Err(GovernedGlbError::Header);
    }
    let length = number(12);
    if &header[16..20] != b"JSON"
        || length == 0
        || length % 4 != 0
        || length > 16 * 1024 * 1024
        || u64::from(length) + 20 > file_length
    {
        return Err(GovernedGlbError::JsonBounds);
    }
    usize::try_from(length).map_err(|_| GovernedGlbError::JsonBounds)
}

/// Intentionally narrower than general glTF: all resources must reside in the pinned GLB.
/// URI keys in extensions are rejected too; extension resource loaders cannot bypass this rule.
/// This is not a complete glTF topology/accessor validator or an artistic qualification.
pub fn validate_governed_glb_json(bytes: &[u8]) -> Result<(), GovernedGlbError> {
    let document: Value = serde_json::from_slice(bytes)?;
    if document.pointer("/asset/version").and_then(Value::as_str) != Some("2.0")
        || contains_uri(&document)
    {
        return Err(GovernedGlbError::ExternalOrInvalidDocument);
    }
    Ok(())
}

fn contains_uri(value: &Value) -> bool {
    match value {
        Value::Object(fields) => fields.contains_key("uri") || fields.values().any(contains_uri),
        Value::Array(values) => values.iter().any(contains_uri),
        _ => false,
    }
}

#[cfg(test)]
mod tests {
    use super::*;

    #[test]
    fn embedded_resources_pass_but_uri_references_do_not() {
        assert!(
            validate_governed_glb_json(
                br#"{"asset":{"version":"2.0"},"images":[{"bufferView":0}]}"#
            )
            .is_ok()
        );
        for uri in [
            "texture.png",
            "../secret.png",
            "https://example.invalid/a",
            "data:image/png;base64,AA==",
        ] {
            let json = serde_json::json!({"asset":{"version":"2.0"},"images":[{"uri":uri}]});
            assert!(validate_governed_glb_json(json.to_string().as_bytes()).is_err());
        }
        assert!(
            validate_governed_glb_json(
                br#"{"asset":{"version":"2.0"},"extensions":{"custom":{"uri":"x"}}}"#
            )
            .is_err()
        );
        assert!(validate_governed_glb_json(b"{}").is_err());
        assert!(validate_governed_glb_json(b"not json").is_err());
    }

    #[test]
    fn header_rejects_truncation_and_unbounded_allocation() {
        let mut header = *b"glTF\x02\x00\x00\x00\x30\x00\x00\x00\x1c\x00\x00\x00JSON";
        assert_eq!(governed_glb_json_length(&header, 48).unwrap(), 28);
        assert!(governed_glb_json_length(&header, 47).is_err());
        header[12..16].copy_from_slice(&u32::MAX.to_le_bytes());
        assert!(governed_glb_json_length(&header, 48).is_err());
    }
}
