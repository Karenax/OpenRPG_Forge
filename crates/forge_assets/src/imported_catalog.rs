//! Explicit opt-in for imported metadata. Does not resolve a URI, grant rights,
//! validate payload bytes, or relax the Gate 0 procedural-only validator.
use crate::{ASSET_CATALOG_SCHEMA_VERSION, AssetCatalog, AssetRecord, AssetSource};
use forge_core::{AssetId, ContentHash, canonical_json_sha256};
use std::collections::HashSet;

impl AssetCatalog {
    /// Validate a bounded mixed catalog before discovery or pinned lookup.
    /// External source URIs are documentary, never filesystem authority.
    pub fn validate_import_metadata(&self) -> Result<(), String> {
        if self.schema_version != ASSET_CATALOG_SCHEMA_VERSION
            || self.assets.is_empty()
            || self.assets.len() > 4096
        {
            return Err("Unsupported or unbounded imported asset catalog".into());
        }
        let mut ids = HashSet::new();
        let mut names = HashSet::new();
        for asset in &self.assets {
            if !ids.insert(asset.id)
                || !names.insert(&asset.logical_name)
                || !text(&asset.logical_name, 256)
            {
                return Err("Duplicate asset identity or invalid logical name".into());
            }
            match &asset.source {
                AssetSource::External {
                    uri,
                    declared_license,
                } => {
                    if !text(uri, 2048)
                        || !declared_license.as_deref().is_some_and(|v| text(v, 256))
                        || asset.content_hash == ContentHash::from_bytes([0; 32])
                    {
                        return Err(
                            "Imported asset requires source, declared license and content pin"
                                .into(),
                        );
                    }
                }
                AssetSource::Procedural { recipe } => {
                    if !text(&recipe.generator, 256)
                        || recipe.generator_version == 0
                        || canonical_json_sha256(recipe).map_err(|e| e.to_string())?
                            != asset.content_hash
                    {
                        return Err("Invalid procedural asset recipe or content pin".into());
                    }
                }
            }
        }
        Ok(())
    }

    /// Resolve exactly the saved identity and content revision. No path fallback
    /// and no implicit adoption of a newer artifact with the same logical ID.
    pub fn resolve_import_pin(
        &self,
        id: AssetId,
        hash: ContentHash,
    ) -> Result<&AssetRecord, String> {
        self.validate_import_metadata()?;
        let record = self
            .assets
            .iter()
            .find(|asset| asset.id == id)
            .ok_or_else(|| format!("Imported asset {id} is unavailable"))?;
        if record.content_hash != hash {
            return Err(format!("Imported asset {id} has changed content"));
        }
        Ok(record)
    }
}

fn text(value: &str, max: usize) -> bool {
    !value.trim().is_empty() && value.len() <= max && !value.chars().any(char::is_control)
}

#[cfg(test)]
mod tests {
    use super::*;
    use crate::AssetKind;
    use forge_core::bytes_sha256;

    fn imported() -> AssetCatalog {
        AssetCatalog {
            schema_version: 1,
            assets: vec![AssetRecord {
                id: AssetId::deterministic(b"fixture/building/0"),
                logical_name: "fixture/building/0".into(),
                kind: AssetKind::Mesh,
                source: AssetSource::External {
                    uri: "https://example.invalid/fixture/building".into(),
                    declared_license: Some("MIT".into()),
                },
                content_hash: bytes_sha256(b"descriptor with native material bindings"),
            }],
        }
    }

    #[test]
    fn imported_pin_survives_serialization_without_relaxing_gate_zero() {
        let catalog = imported();
        assert!(catalog.validate_procedural_only().is_err());
        let reopened: AssetCatalog =
            serde_json::from_slice(&serde_json::to_vec(&catalog).unwrap()).unwrap();
        let record = &catalog.assets[0];
        assert_eq!(
            reopened
                .resolve_import_pin(record.id, record.content_hash)
                .unwrap(),
            record
        );
        assert!(
            reopened
                .resolve_import_pin(record.id, bytes_sha256(b"changed"))
                .is_err()
        );
        assert!(
            reopened
                .resolve_import_pin(AssetId::deterministic(b"missing"), record.content_hash)
                .is_err()
        );
    }

    #[test]
    fn malformed_provenance_and_duplicate_identity_are_rejected() {
        for source in [
            AssetSource::External {
                uri: "source".into(),
                declared_license: None,
            },
            AssetSource::External {
                uri: "\n".into(),
                declared_license: Some("MIT".into()),
            },
        ] {
            let mut catalog = imported();
            catalog.assets[0].source = source;
            assert!(catalog.validate_import_metadata().is_err());
        }
        let mut catalog = imported();
        catalog.assets.push(catalog.assets[0].clone());
        assert!(catalog.validate_import_metadata().is_err());
    }
}
