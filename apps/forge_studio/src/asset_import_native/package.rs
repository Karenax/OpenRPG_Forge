//! Admit a complete package catalogue as one additive collection revision.
use super::*;

impl AssetImportStore {
    pub(crate) fn merge_package_assets(
        &self,
        command_id: CommandId,
        candidates: impl IntoIterator<Item = Result<ImportCandidate, String>>,
    ) -> Result<Option<ContentHash>, String> {
        let _lock = self.lock()?;
        let head = self.head()?;
        let mut catalog = match head.current.catalog_sha256 {
            Some(hash) => self.catalog(hash)?,
            None => GovernedImportCatalog {
                schema: GOVERNED_IMPORT_SCHEMA.into(),
                schema_version: GOVERNED_IMPORT_VERSION,
                assets: Vec::new(),
            },
        };
        let mut added = false;
        for candidate in candidates {
            let candidate = candidate?;
            let previous = catalog
                .assets
                .iter()
                .find(|asset| asset.id == candidate.asset_id);
            Self::validate_variant(&candidate, previous, &catalog)?;
            if candidate.authored_changes && candidate.source.is_none() {
                return Err("Retouches require a preserved authored source or recipe".into());
            }
            let (asset, _) = self
                .stage_asset(&candidate, previous, &catalog)
                .map_err(|error| format!("Import asset {}: {error}", candidate.asset_id))?;
            if let Some(previous) = previous {
                if previous != &asset {
                    return Err(format!(
                        "Shared asset {} has conflicting metadata or revision; existing modules were not overwritten",
                        asset.id
                    ));
                }
            } else {
                catalog.assets.push(asset);
                added = true;
            }
        }
        self.verify_catalog_files(&catalog)?;
        if !added {
            return Ok(head.current.catalog_sha256);
        }
        catalog.assets.sort_by_key(|asset| asset.id);
        let bytes = serde_json::to_vec(&catalog).map_err(|error| error.to_string())?;
        if bytes.len() > storage::MAX_DOCUMENT {
            return Err("Import catalogue exceeds metadata limit".into());
        }
        let hash = bytes_sha256(&bytes);
        self.publish_bytes(&format!("catalogs/{hash}.json"), &bytes)?;
        let receipt = ImportReceipt {
            command_id,
            revision: head
                .current
                .revision
                .checked_add(1)
                .ok_or("Import revision overflow")?,
            previous_catalog_sha256: head.current.catalog_sha256,
            catalog_sha256: Some(hash),
            asset_id: None,
            previous_asset_sha256: None,
            asset_sha256: None,
            undo: false,
        };
        self.accept_catalog(head, &receipt, None)?;
        Ok(Some(hash))
    }
}
