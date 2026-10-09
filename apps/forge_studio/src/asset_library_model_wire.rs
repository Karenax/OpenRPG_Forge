//! Versioned binary model IPC; no JSON expansion of the purchased GLB payload.
use crate::asset_library_protocol::{LibraryModel, vault_asset_id};
use forge_core::{AssetId, ContentHash};
use serde::{Deserialize, Serialize};

// Explicit one-GiB ceiling for faithful autonomous source scenes. Metadata
// is checked before allocation; JSON/header and decoded-resource limits remain separate.
pub const MAX_MODEL_BYTES: usize = 1024 * 1024 * 1024;
const MAGIC: &[u8; 8] = b"ORPM\x01\0\0\0";
const MAX_HEADER: usize = 4096;
const PREFIX: usize = 12;
/// Maximum envelope headroom to reserve before reading the model body.
pub const MODEL_HEADER_HEADROOM: usize = PREFIX + MAX_HEADER;

#[derive(Serialize, Deserialize)]
#[serde(deny_unknown_fields)]
struct Header {
    id: String,
    asset_id: AssetId,
    sha256: ContentHash,
    byte_length: u64,
}

fn validate(header: &Header, bytes: &[u8]) -> Result<(), String> {
    if header.id.is_empty()
        || header.id.len() > 512
        || header.asset_id != vault_asset_id(&header.id)
    {
        return Err("Identité de modèle invalide".into());
    }
    if bytes.len() > MAX_MODEL_BYTES || header.byte_length != bytes.len() as u64 {
        return Err("Taille de modèle invalide ou budget dépassé".into());
    }
    if forge_core::bytes_sha256(bytes) != header.sha256 {
        return Err("Empreinte du transfert différente du modèle".into());
    }
    Ok(())
}

pub fn encode(model: LibraryModel) -> Result<Vec<u8>, String> {
    let header = Header {
        id: model.id,
        asset_id: model.asset_id,
        sha256: model.sha256,
        byte_length: model.bytes.len() as u64,
    };
    validate(&header, &model.bytes)?;
    let json = serde_json::to_vec(&header).map_err(|e| e.to_string())?;
    if json.len() > MAX_HEADER {
        return Err("En-tête de modèle trop volumineux".into());
    }
    // Reuse the verified body instead of allocating and copying a second
    // full-size model for the IPC envelope. The reader can reserve header
    // headroom so this operation also avoids reallocating the original body.
    let mut wire = model.bytes;
    let body_len = wire.len();
    let body_start = PREFIX + json.len();
    let wire_len = body_start
        .checked_add(body_len)
        .ok_or("Taille du transfert de modèle invalide")?;
    wire.try_reserve_exact(body_start)
        .map_err(|e| e.to_string())?;
    wire.resize(wire_len, 0);
    wire.copy_within(..body_len, body_start);
    wire[..8].copy_from_slice(MAGIC);
    wire[8..PREFIX].copy_from_slice(&(json.len() as u32).to_le_bytes());
    wire[PREFIX..body_start].copy_from_slice(&json);
    Ok(wire)
}

pub fn decode(mut wire: Vec<u8>) -> Result<LibraryModel, String> {
    if wire.len() < PREFIX || &wire[..8] != MAGIC {
        return Err("Version du transfert de modèle invalide".into());
    }
    if wire.len() > PREFIX + MAX_HEADER + MAX_MODEL_BYTES {
        return Err("Transfert de modèle trop volumineux".into());
    }
    let length = u32::from_le_bytes(wire[8..12].try_into().unwrap()) as usize;
    if length == 0 || length > MAX_HEADER || wire.len() < PREFIX + length {
        return Err("En-tête de modèle tronqué ou trop volumineux".into());
    }
    let body_start = PREFIX + length;
    let header: Header =
        serde_json::from_slice(&wire[PREFIX..body_start]).map_err(|e| e.to_string())?;
    validate(&header, &wire[body_start..])?;
    // Retain the allocation instead of cloning the whole payload after IPC.
    wire.drain(..body_start);
    Ok(LibraryModel {
        id: header.id,
        asset_id: header.asset_id,
        sha256: header.sha256,
        bytes: wire,
    })
}

#[cfg(test)]
mod tests {
    use super::*;
    fn model() -> LibraryModel {
        let id = "pack.model-variant-lod1".to_owned();
        let bytes = b"\0\xffglTF\0original bytes".to_vec();
        LibraryModel {
            asset_id: vault_asset_id(&id),
            sha256: forge_core::bytes_sha256(&bytes),
            id,
            bytes,
        }
    }
    #[test]
    fn binary_roundtrip_preserves_content_and_identity_without_json_payload() {
        let source = model();
        let wire = encode(source.clone()).unwrap();
        assert!(wire.len() < source.bytes.len() + 512);
        let restored = decode(wire).unwrap();
        assert_eq!(restored.bytes, source.bytes);
        assert_eq!(restored.asset_id, source.asset_id);
        assert_eq!(restored.sha256, source.sha256);
    }
    #[test]
    fn encoding_reuses_a_body_allocation_with_reserved_header_headroom() {
        let mut source = model();
        source.bytes.reserve_exact(PREFIX + MAX_HEADER);
        let original_pointer = source.bytes.as_ptr();
        let original_capacity = source.bytes.capacity();
        let expected = source.clone();
        let wire = encode(source).unwrap();
        assert_eq!(wire.as_ptr(), original_pointer);
        assert_eq!(wire.capacity(), original_capacity);
        let restored = decode(wire).unwrap();
        assert_eq!(restored.bytes.as_ptr(), original_pointer);
        assert_eq!(restored.bytes, expected.bytes);
        assert_eq!(restored.sha256, expected.sha256);
    }
    #[test]
    fn tampering_truncation_version_and_unbounded_headers_are_refused() {
        let wire = encode(model()).unwrap();
        for end in [0, 7, 11, 12, wire.len() - 1] {
            assert!(decode(wire[..end].to_vec()).is_err());
        }
        let mut bad = wire.clone();
        bad[0] ^= 1;
        assert!(decode(bad).is_err());
        let mut bad = wire.clone();
        bad[8..12].copy_from_slice(&u32::MAX.to_le_bytes());
        assert!(decode(bad).is_err());
        let mut bad = wire;
        let last = bad.len() - 1;
        bad[last] ^= 1;
        assert!(decode(bad).is_err());
        let mut wrong = model();
        wrong.asset_id = vault_asset_id("other");
        assert!(encode(wrong).is_err());
        let mut wrong = model();
        wrong.sha256 = ContentHash::from_bytes([0; 32]);
        assert!(encode(wrong).is_err());
    }
}
