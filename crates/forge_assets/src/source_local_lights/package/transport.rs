//! Existing inert source-light transport shared by native and browser readers.
use base64::{Engine, engine::general_purpose::STANDARD};
use forge_core::ContentHash;
const MARKER: &[u8] = b"b64:";

#[must_use]
pub fn member_path(prefix: &str, hash: ContentHash, license: ContentHash) -> String {
    if hash == license {
        format!("{prefix}license.txt")
    } else {
        format!("{prefix}members/{hash}.source-data")
    }
}

pub fn encoded_bound(maximum: usize, raw: bool) -> Result<usize, String> {
    if raw {
        return Ok(maximum);
    }
    maximum
        .div_ceil(3)
        .checked_mul(4)
        .and_then(|n| n.checked_add(MARKER.len()))
        .ok_or_else(|| "Source-light encoding size overflow".into())
}

pub fn decode(bytes: Vec<u8>, raw: bool, maximum: usize) -> Result<Vec<u8>, String> {
    if bytes.len() > encoded_bound(maximum, raw)? {
        return Err("Source-light transport member exceeds its budget".into());
    }
    let decoded = if raw {
        bytes
    } else {
        STANDARD
            .decode(
                bytes
                    .strip_prefix(MARKER)
                    .ok_or("Source-light data encoding marker differs")?,
            )
            .map_err(|e| e.to_string())?
    };
    if decoded.len() > maximum {
        return Err("Decoded source-light member exceeds its budget".into());
    }
    Ok(decoded)
}
