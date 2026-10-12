//! Versioned binary model IPC; no JSON expansion of the purchased GLB payload.
use crate::asset_library_protocol::{
    LibraryModel, ModuleCollisionPolicy, ModuleModel, ModuleModelReply, vault_asset_id,
};
use forge_core::{AssetId, ContentHash};
use serde::{Deserialize, Serialize};

// Explicit one-GiB ceiling for faithful autonomous source scenes. Metadata
// is checked before allocation; JSON/header and decoded-resource limits remain separate.
pub const MAX_MODEL_BYTES: usize = 1024 * 1024 * 1024;
const MAGIC: &[u8; 8] = b"ORPM\x01\0\0\0";
const MODULE_MAGIC: &[u8; 8] = b"ORPM\x02\0\0\0";
const MODULE_COLLISION_MAGIC: &[u8; 8] = b"ORPM\x03\0\0\0";
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

#[derive(Serialize, Deserialize)]
#[serde(tag = "status", rename_all = "snake_case", deny_unknown_fields)]
enum ModuleHeader {
    Model {
        asset_id: AssetId,
        sha256: ContentHash,
        catalog_sha256: ContentHash,
        byte_length: u64,
    },
    Missing {
        asset_id: AssetId,
        sha256: ContentHash,
    },
}

/// V3 alone carries the host-admitted collision policy. V2 headers remain exact.
#[derive(Serialize, Deserialize)]
#[serde(deny_unknown_fields)]
struct ModuleCollisionHeader {
    asset_id: AssetId,
    sha256: ContentHash,
    catalog_sha256: ContentHash,
    byte_length: u64,
    collision: ModuleCollisionPolicy,
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
    encode_envelope(*MAGIC, &header, model.bytes)
}

fn encode_envelope(
    magic: [u8; 8],
    header: &impl Serialize,
    bytes: Vec<u8>,
) -> Result<Vec<u8>, String> {
    let json = serde_json::to_vec(header).map_err(|e| e.to_string())?;
    if json.len() > MAX_HEADER {
        return Err("En-tête de modèle trop volumineux".into());
    }
    // Reuse the verified body instead of allocating and copying a second
    // full-size model for the IPC envelope. The reader can reserve header
    // headroom so this operation also avoids reallocating the original body.
    let mut wire = bytes;
    let body_len = wire.len();
    let body_start = PREFIX + json.len();
    let wire_len = body_start
        .checked_add(body_len)
        .ok_or("Taille du transfert de modèle invalide")?;
    wire.try_reserve_exact(body_start)
        .map_err(|e| e.to_string())?;
    wire.resize(wire_len, 0);
    wire.copy_within(..body_len, body_start);
    wire[..8].copy_from_slice(&magic);
    wire[8..PREFIX].copy_from_slice(
        &u32::try_from(json.len())
            .map_err(|_| "Model header exceeds wire format limits")?
            .to_le_bytes(),
    );
    wire[PREFIX..body_start].copy_from_slice(&json);
    Ok(wire)
}

pub fn decode(mut wire: Vec<u8>) -> Result<LibraryModel, String> {
    let body_start = envelope_body_start(&wire, *MAGIC)?;
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

fn envelope_body_start(wire: &[u8], magic: [u8; 8]) -> Result<usize, String> {
    if wire.len() < PREFIX || wire[..8] != magic {
        return Err("Version du transfert de modèle invalide".into());
    }
    if wire.len() > PREFIX + MAX_HEADER + MAX_MODEL_BYTES {
        return Err("Transfert de modèle trop volumineux".into());
    }
    let length =
        u32::from_le_bytes(wire[8..12].try_into().map_err(|_| "En-tête tronqué")?) as usize;
    if length == 0 || length > MAX_HEADER || wire.len() < PREFIX + length {
        return Err("En-tête de modèle tronqué ou trop volumineux".into());
    }
    Ok(PREFIX + length)
}

/// V2 preserves ordinary module replies; explicit collision authority requires V3.
pub fn encode_module(reply: ModuleModelReply) -> Result<Vec<u8>, String> {
    let (header, bytes) = match reply {
        ModuleModelReply::Model(model) => {
            if model.asset_id.as_uuid().is_nil()
                || model.bytes.len() > MAX_MODEL_BYTES
                || forge_core::bytes_sha256(&model.bytes) != model.sha256
            {
                return Err("Modèle du module invalide".into());
            }
            if let Some(collision) = model.collision {
                return encode_envelope(
                    *MODULE_COLLISION_MAGIC,
                    &ModuleCollisionHeader {
                        asset_id: model.asset_id,
                        sha256: model.sha256,
                        catalog_sha256: model.catalog_sha256,
                        byte_length: model.bytes.len() as u64,
                        collision,
                    },
                    model.bytes,
                );
            }
            (
                ModuleHeader::Model {
                    asset_id: model.asset_id,
                    sha256: model.sha256,
                    catalog_sha256: model.catalog_sha256,
                    byte_length: model.bytes.len() as u64,
                },
                model.bytes,
            )
        }
        ModuleModelReply::Missing { asset_id, sha256 } => {
            (ModuleHeader::Missing { asset_id, sha256 }, Vec::new())
        }
    };
    encode_envelope(*MODULE_MAGIC, &header, bytes)
}

pub fn decode_module(
    mut wire: Vec<u8>,
    expected_id: AssetId,
    expected_hash: ContentHash,
) -> Result<ModuleModelReply, String> {
    if wire.starts_with(MODULE_COLLISION_MAGIC) {
        return decode_module_collision(wire, expected_id, expected_hash);
    }
    let body_start = envelope_body_start(&wire, *MODULE_MAGIC)?;
    let header: ModuleHeader =
        serde_json::from_slice(&wire[PREFIX..body_start]).map_err(|e| e.to_string())?;
    match header {
        ModuleHeader::Model {
            asset_id,
            sha256,
            catalog_sha256,
            byte_length,
        } => {
            if asset_id != expected_id
                || sha256 != expected_hash
                || asset_id.as_uuid().is_nil()
                || wire.len() - body_start > MAX_MODEL_BYTES
                || byte_length != (wire.len() - body_start) as u64
                || forge_core::bytes_sha256(&wire[body_start..]) != sha256
            {
                return Err("Référence du module différente de la réponse".into());
            }
            wire.drain(..body_start);
            Ok(ModuleModelReply::Model(ModuleModel {
                asset_id,
                sha256,
                catalog_sha256,
                collision: None,
                bytes: wire,
            }))
        }
        ModuleHeader::Missing { asset_id, sha256 } => {
            if asset_id != expected_id || sha256 != expected_hash || wire.len() != body_start {
                return Err("Référence absente du module invalide".into());
            }
            Ok(ModuleModelReply::Missing { asset_id, sha256 })
        }
    }
}

fn decode_module_collision(
    mut wire: Vec<u8>,
    expected_id: AssetId,
    expected_hash: ContentHash,
) -> Result<ModuleModelReply, String> {
    let body_start = envelope_body_start(&wire, *MODULE_COLLISION_MAGIC)?;
    let header: ModuleCollisionHeader =
        serde_json::from_slice(&wire[PREFIX..body_start]).map_err(|error| error.to_string())?;
    if header.asset_id != expected_id
        || header.sha256 != expected_hash
        || header.asset_id.as_uuid().is_nil()
        || wire.len() - body_start > MAX_MODEL_BYTES
        || header.byte_length != (wire.len() - body_start) as u64
        || forge_core::bytes_sha256(&wire[body_start..]) != header.sha256
    {
        return Err("Référence du module V3 différente de la réponse".into());
    }
    wire.drain(..body_start);
    Ok(ModuleModelReply::Model(ModuleModel {
        asset_id: header.asset_id,
        sha256: header.sha256,
        catalog_sha256: header.catalog_sha256,
        collision: Some(header.collision),
        bytes: wire,
    }))
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

    fn module_model() -> ModuleModel {
        let bytes = b"glTF original module bytes".to_vec();
        ModuleModel {
            asset_id: AssetId::deterministic("arbitrary-installed-module/house"),
            sha256: forge_core::bytes_sha256(&bytes),
            catalog_sha256: forge_core::bytes_sha256(b"trusted catalog"),
            collision: None,
            bytes,
        }
    }

    #[test]
    fn module_v2_preserves_explicit_identity_without_weakening_vault_v1() {
        let expected = module_model();
        let mut source = expected.clone();
        source.bytes.reserve_exact(MODEL_HEADER_HEADROOM);
        let pointer = source.bytes.as_ptr();
        let wire = encode_module(ModuleModelReply::Model(source)).unwrap();
        assert_eq!(wire.as_ptr(), pointer);
        assert!(decode(wire.clone()).is_err());
        let ModuleModelReply::Model(restored) =
            decode_module(wire, expected.asset_id, expected.sha256).unwrap()
        else {
            panic!("Model expected");
        };
        assert_eq!(restored.bytes, expected.bytes);
        assert_eq!(restored.bytes.as_ptr(), pointer);
        assert_eq!(restored.catalog_sha256, expected.catalog_sha256);
        assert_eq!(restored.collision, None);
        assert!(
            decode_module(encode(model()).unwrap(), expected.asset_id, expected.sha256).is_err()
        );
        assert!(
            encode(LibraryModel {
                id: "house".into(),
                asset_id: expected.asset_id,
                sha256: expected.sha256,
                bytes: expected.bytes
            })
            .is_err()
        );
    }

    #[test]
    fn module_v2_missing_is_bound_to_exact_request_and_has_no_payload() {
        let model = module_model();
        let wire = encode_module(ModuleModelReply::Missing {
            asset_id: model.asset_id,
            sha256: model.sha256,
        })
        .unwrap();
        assert!(matches!(
            decode_module(wire.clone(), model.asset_id, model.sha256).unwrap(),
            ModuleModelReply::Missing { .. }
        ));
        assert!(
            decode_module(wire.clone(), AssetId::deterministic("other"), model.sha256).is_err()
        );
        assert!(
            decode_module(
                wire.clone(),
                model.asset_id,
                ContentHash::from_bytes([0; 32])
            )
            .is_err()
        );
        let mut bad = wire;
        bad.push(0);
        assert!(decode_module(bad, model.asset_id, model.sha256).is_err());
    }

    #[test]
    fn module_v2_tampered_or_mismatched_models_are_errors_never_missing() {
        let model = module_model();
        let wire = encode_module(ModuleModelReply::Model(model.clone())).unwrap();
        for end in [0, 7, 11, 12, wire.len() - 1] {
            assert!(decode_module(wire[..end].to_vec(), model.asset_id, model.sha256).is_err());
        }
        assert!(
            decode_module(wire.clone(), AssetId::deterministic("other"), model.sha256).is_err()
        );
        let mut bad = wire.clone();
        bad[8..12].copy_from_slice(&u32::MAX.to_le_bytes());
        assert!(decode_module(bad, model.asset_id, model.sha256).is_err());
        let mut bad = wire;
        *bad.last_mut().unwrap() ^= 1;
        assert!(decode_module(bad, model.asset_id, model.sha256).is_err());
    }

    #[test]
    fn explicit_collision_v3_preserves_body_identity_catalog_pin_and_allocation() {
        let mut source = module_model();
        source.collision = Some(ModuleCollisionPolicy::StaticTriangleMeshes);
        let expected = source.clone();
        source.bytes.reserve_exact(MODEL_HEADER_HEADROOM);
        let pointer = source.bytes.as_ptr();
        let wire = encode_module(ModuleModelReply::Model(source)).unwrap();
        assert!(wire.starts_with(MODULE_COLLISION_MAGIC));
        assert_eq!(wire.as_ptr(), pointer);
        assert!(decode(wire.clone()).is_err());
        assert!(envelope_body_start(&wire, *MODULE_MAGIC).is_err());
        let ModuleModelReply::Model(restored) =
            decode_module(wire, expected.asset_id, expected.sha256).unwrap()
        else {
            panic!("V3 is a model, never Missing");
        };
        assert_eq!(restored.asset_id, expected.asset_id);
        assert_eq!(restored.sha256, expected.sha256);
        assert_eq!(restored.catalog_sha256, expected.catalog_sha256);
        assert_eq!(restored.collision, expected.collision);
        assert_eq!(restored.bytes, expected.bytes);
        assert_eq!(restored.bytes.as_ptr(), pointer);
    }

    fn collision_header(model: &ModuleModel) -> serde_json::Value {
        serde_json::json!({
            "asset_id": model.asset_id,
            "sha256": model.sha256,
            "catalog_sha256": model.catalog_sha256,
            "byte_length": model.bytes.len(),
            "collision": "static_triangle_meshes"
        })
    }

    #[test]
    fn v3_missing_unknown_null_or_v2_collision_headers_are_errors_never_fallbacks() {
        let model = module_model();
        for collision in [serde_json::Value::Null, serde_json::json!("convex")] {
            let mut header = collision_header(&model);
            header["collision"] = collision;
            let wire =
                encode_envelope(*MODULE_COLLISION_MAGIC, &header, model.bytes.clone()).unwrap();
            assert!(decode_module(wire, model.asset_id, model.sha256).is_err());
        }
        let mut header = collision_header(&model);
        header.as_object_mut().unwrap().remove("collision");
        let wire = encode_envelope(*MODULE_COLLISION_MAGIC, &header, model.bytes.clone()).unwrap();
        assert!(decode_module(wire, model.asset_id, model.sha256).is_err());
        let mut header = collision_header(&model);
        header["status"] = serde_json::json!("model");
        for magic in [*MODULE_MAGIC, *MODULE_COLLISION_MAGIC] {
            let wire = encode_envelope(magic, &header, model.bytes.clone()).unwrap();
            assert!(decode_module(wire, model.asset_id, model.sha256).is_err());
        }
        let missing = ModuleHeader::Missing {
            asset_id: model.asset_id,
            sha256: model.sha256,
        };
        let wire = encode_envelope(*MODULE_COLLISION_MAGIC, &missing, Vec::new()).unwrap();
        assert!(decode_module(wire, model.asset_id, model.sha256).is_err());
    }

    #[test]
    fn v3_tampering_or_request_mismatch_does_not_become_missing() {
        let mut model = module_model();
        model.collision = Some(ModuleCollisionPolicy::StaticTriangleMeshes);
        let wire = encode_module(ModuleModelReply::Model(model.clone())).unwrap();
        for end in [0, 7, 11, 12, wire.len() - 1] {
            assert!(decode_module(wire[..end].to_vec(), model.asset_id, model.sha256).is_err());
        }
        assert!(
            decode_module(wire.clone(), AssetId::deterministic("other"), model.sha256).is_err()
        );
        assert!(
            decode_module(
                wire.clone(),
                model.asset_id,
                ContentHash::from_bytes([0; 32])
            )
            .is_err()
        );
        let mut bad = wire.clone();
        bad[8..12].copy_from_slice(&u32::MAX.to_le_bytes());
        assert!(decode_module(bad, model.asset_id, model.sha256).is_err());
        let mut bad = wire;
        *bad.last_mut().unwrap() ^= 1;
        assert!(decode_module(bad, model.asset_id, model.sha256).is_err());
    }
}
