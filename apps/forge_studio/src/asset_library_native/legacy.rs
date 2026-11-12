//! Reconstruct a previously pinned GLB from its metadata prefix and shared tail.
use super::{MAX_MODEL, same_hash};
use serde_json::Value;

const MAX_PREFIX: usize = 16 * 1024 * 1024;
const MAX_INLINE: usize = 128 * 1024;

fn u32_at(bytes: &[u8], offset: usize) -> Result<u32, String> {
    let value: [u8; 4] = bytes
        .get(offset..offset + 4)
        .ok_or("GLB tronqué")?
        .try_into()
        .map_err(|_| "GLB tronqué")?;
    Ok(u32::from_le_bytes(value))
}

fn prefix_end(bytes: &[u8]) -> Result<usize, String> {
    if bytes.len() < 20 || &bytes[..4] != b"glTF" || u32_at(bytes, 4)? != 2 {
        return Err("en-tête GLB invalide".into());
    }
    let json_len = u32_at(bytes, 12)? as usize;
    if json_len % 4 != 0 || u32_at(bytes, 16)? != 0x4e4f_534a {
        return Err("bloc JSON GLB invalide".into());
    }
    20usize
        .checked_add(json_len)
        .ok_or_else(|| "taille GLB invalide".into())
}

fn hex_decode(value: &str) -> Result<Vec<u8>, String> {
    if value.len() % 2 != 0 || value.len() / 2 > MAX_PREFIX {
        return Err("préfixe historique trop grand ou invalide".into());
    }
    fn nibble(b: u8) -> Option<u8> {
        match b {
            b'0'..=b'9' => Some(b - b'0'),
            b'a'..=b'f' => Some(b - b'a' + 10),
            _ => None,
        }
    }
    value
        .as_bytes()
        .chunks_exact(2)
        .map(|pair| {
            Ok((nibble(pair[0]).ok_or("hexadécimal invalide")? << 4)
                | nibble(pair[1]).ok_or("hexadécimal invalide")?)
        })
        .collect()
}

pub(super) fn reconstruct_inline(recipe: &Value, expected: &str) -> Result<Vec<u8>, String> {
    if recipe.get("schema").and_then(Value::as_str) != Some("openrpg.legacy-inline-glb.v1")
        || recipe.get("logical_sha256").and_then(Value::as_str) != Some(expected)
    {
        return Err("recette historique incohérente".into());
    }
    let encoded = recipe.get("data_hex").and_then(Value::as_str).ok_or("modèle historique absent")?;
    if encoded.is_empty() || encoded.len() > 2 * MAX_INLINE {
        return Err("modèle historique trop grand".into());
    }
    let bytes = hex_decode(encoded)?;
    let end = prefix_end(&bytes)?;
    let tail = bytes.get(end..).ok_or("GLB historique tronqué")?;
    if tail.len() < 8 || u32_at(tail, 4)? != 0x004e_4942
        || (u32_at(tail, 0)? as usize).checked_add(8) != Some(tail.len())
        || u32_at(&bytes, 8)? as usize != bytes.len()
        || recipe.get("logical_bytes").and_then(Value::as_u64) != Some(bytes.len() as u64)
        || !same_hash(&bytes, expected)
    {
        return Err("modèle historique invalide".into());
    }
    Ok(bytes)
}

pub(super) fn reconstruct(
    retained: &[u8],
    recipe: &Value,
    expected: &str,
) -> Result<Vec<u8>, String> {
    if recipe.get("schema").and_then(Value::as_str) != Some("openrpg.legacy-glb-recipe.v1")
        || recipe.get("logical_sha256").and_then(Value::as_str) != Some(expected)
    {
        return Err("recette historique incohérente".into());
    }
    let prefix = hex_decode(
        recipe
            .get("prefix_hex")
            .and_then(Value::as_str)
            .ok_or("préfixe historique absent")?,
    )?;
    if prefix_end(&prefix)? != prefix.len() {
        return Err("préfixe historique GLB invalide".into());
    }
    let end = prefix_end(retained)?;
    let tail = retained.get(end..).ok_or("GLB conservé tronqué")?;
    if tail.len() < 8
        || u32_at(tail, 4)? != 0x004e_4942
        || (u32_at(tail, 0)? as usize).checked_add(8) != Some(tail.len())
    {
        return Err("bloc binaire conservé invalide".into());
    }
    let tail_pin = recipe
        .get("shared_tail_sha256")
        .and_then(Value::as_str)
        .ok_or("empreinte binaire absente")?;
    if !same_hash(tail, tail_pin) {
        return Err("empreinte binaire historique incohérente".into());
    }
    let total = prefix
        .len()
        .checked_add(tail.len())
        .ok_or("taille historique invalide")?;
    if total as u64 > MAX_MODEL
        || recipe.get("logical_bytes").and_then(Value::as_u64) != Some(total as u64)
        || u32_at(&prefix, 8)? as usize != total
    {
        return Err("taille historique GLB incohérente".into());
    }
    let mut result = Vec::with_capacity(total);
    result.extend_from_slice(&prefix);
    result.extend_from_slice(tail);
    if !same_hash(&result, expected) {
        return Err("empreinte historique incohérente".into());
    }
    Ok(result)
}

#[cfg(test)]
mod tests {
    use super::*;
    use serde_json::json;

    fn glb(json: &[u8], bin: &[u8]) -> Vec<u8> {
        let mut metadata = json.to_vec();
        while metadata.len() % 4 != 0 {
            metadata.push(b' ');
        }
        let size = 12 + 8 + metadata.len() + 8 + bin.len();
        let mut bytes = b"glTF".to_vec();
        for value in [2, size as u32, metadata.len() as u32, 0x4e4f_534a] {
            bytes.extend_from_slice(&value.to_le_bytes());
        }
        bytes.extend_from_slice(&metadata);
        bytes.extend_from_slice(&(bin.len() as u32).to_le_bytes());
        bytes.extend_from_slice(&0x004e_4942u32.to_le_bytes());
        bytes.extend_from_slice(bin);
        bytes
    }

    #[test]
    fn inline_recipe_preserves_bytes_and_rejects_corruption_and_oversize() {
        let old = glb(br#"{"id":"old"}"#, b"data");
        let pin = forge_core::bytes_sha256(&old).to_string();
        let text = old.iter().map(|byte| format!("{byte:02x}")).collect::<String>();
        let recipe = json!({"schema":"openrpg.legacy-inline-glb.v1", "data_hex":text,
                            "logical_sha256":pin, "logical_bytes":old.len()});
        assert_eq!(reconstruct_inline(&recipe, &pin).unwrap(), old);
        let mut corrupted = recipe.clone();
        corrupted["data_hex"] = json!("00");
        assert!(reconstruct_inline(&corrupted, &pin).is_err());
        corrupted["data_hex"] = json!("00".repeat(MAX_INLINE + 1));
        assert!(reconstruct_inline(&corrupted, &pin).is_err());
        assert!(reconstruct_inline(&recipe, &"0".repeat(64)).is_err());
    }

    #[test]
    fn restores_exact_old_bytes_and_rejects_damage() {
        let kept = glb(br#"{"id":"keep"}"#, b"data");
        let old = glb(br#"{"id":"old"}"#, b"data");
        let old_end = prefix_end(&old).unwrap();
        let prefix = old[..old_end]
            .iter()
            .map(|v| format!("{v:02x}"))
            .collect::<String>();
        let pin = forge_core::bytes_sha256(&old).to_string();
        let recipe = json!({"schema":"openrpg.legacy-glb-recipe.v1", "prefix_hex":prefix,
                            "logical_sha256":pin,"logical_bytes":old.len(),
                            "shared_tail_sha256":forge_core::bytes_sha256(&kept[prefix_end(&kept).unwrap()..]).to_string()});
        assert_eq!(reconstruct(&kept, &recipe, &pin).unwrap(), old);
        let mut damaged = kept.clone();
        *damaged.last_mut().unwrap() ^= 1;
        assert!(reconstruct(&damaged, &recipe, &pin).is_err());
        assert!(reconstruct(&kept, &recipe, &"0".repeat(64)).is_err());
        let mut bad_recipe = recipe;
        bad_recipe["prefix_hex"] = json!("xx");
        assert!(reconstruct(&kept, &bad_recipe, &pin).is_err());
    }

    #[test]
    #[ignore = "Requires a private Vault and prepared consolidation stage"]
    fn real_prepared_recipes_reconstruct_every_original_pin() {
        let stage = std::env::var("OPENRPG_CONSOLIDATION_STAGE").unwrap();
        let vault = std::env::var("OPENRPG_ASSET_VAULT").unwrap();
        let stage = std::path::Path::new(&stage);
        let vault = std::path::Path::new(&vault);
        let manifest: Value =
            serde_json::from_slice(&std::fs::read(stage.join("transition.json")).unwrap()).unwrap();
        assert_eq!(manifest["state"], "prepared_only");
        assert_eq!(manifest["includes_legacy_recipes"], true);
        let aliases = manifest["aliases_requiring_project_migration"]
            .as_array()
            .unwrap();
        let mut count = 0;
        for (pack, pins) in manifest["packs"].as_object().unwrap() {
            let raw = std::fs::read(stage.join(pack).join("details.v1.json")).unwrap();
            assert!(same_hash(&raw, pins["details.v1.json"].as_str().unwrap()));
            let details: Value = serde_json::from_slice(&raw).unwrap();
            for asset in details["assets"].as_array().unwrap() {
                let Some(variants) = asset.get("consolidated_variants").and_then(Value::as_array)
                else {
                    continue;
                };
                let path = std::path::Path::new(asset["model_path"].as_str().unwrap());
                let (bytes, actual) =
                    super::super::read_model_limited(vault, path, super::super::MAX_MODEL).unwrap();
                assert_eq!(actual, asset["model_sha256"].as_str().unwrap());
                for variant in variants {
                    let pin = variant["retired_model_sha256"].as_str().unwrap();
                    let old = reconstruct(&bytes, &variant["legacy_recipe"], pin).unwrap();
                    assert!(same_hash(&old, pin));
                    count += 1;
                }
            }
        }
        assert_eq!(count, aliases.len());
        assert!(count > 0);
        eprintln!(
            "Verified {count} real legacy SHA pins; no metadata published and no model removed"
        );
    }
}
