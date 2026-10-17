use super::{
    AssetId, AssetImportStore, ContentHash, DurableAssetSource, GOVERNED_IMPORT_SCHEMA,
    GOVERNED_IMPORT_VERSION, GovernedImportAsset, GovernedImportCatalog, bytes_sha256, pin_file,
    storage,
};
use std::collections::{BTreeMap, BTreeSet};

/// This adapter returns owned buffers. Larger libraries require a streaming
/// exporter rather than silently lifting this per-export allocation budget.
pub const MAX_EXPORT_BYTES: u64 = 512 * 1024 * 1024;

#[derive(Clone, Debug)]
pub struct ImportedExportSnapshot {
    pub catalog: GovernedImportCatalog,
    pub files: BTreeMap<String, Vec<u8>>,
}

impl AssetImportStore {
    pub fn export_snapshot(
        &self,
        required_pins: &[(AssetId, ContentHash)],
        publish: bool,
    ) -> Result<ImportedExportSnapshot, String> {
        let _lock = self.lock()?;
        let head = self.head()?;
        let required = requested(required_pins)?;
        let mut selected = Selected::default();
        let mut unresolved = required.clone();
        let mut snapshots = Vec::new();
        if let Some(hash) = head.current.catalog_sha256 {
            let catalog = self.catalog(hash)?;
            selected.seed(&catalog);
            snapshots.push(hash);
        }
        for hash in head.catalogs.iter().rev() {
            if !snapshots.contains(hash) {
                snapshots.push(*hash);
            }
        }
        for hash in snapshots {
            if unresolved.is_empty() {
                break;
            }
            let catalog = self.catalog(hash)?;
            for asset in &catalog.assets {
                if unresolved.get(&asset.id) == Some(&asset.payload.sha256) {
                    selected.resolve(asset, &catalog, &required)?;
                    unresolved.remove(&asset.id);
                }
            }
        }
        if let Some((id, pin)) = unresolved.first_key_value() {
            return Err(format!(
                "No preserved imported asset {id} with content pin {pin}"
            ));
        }
        selected.verify_lineage()?;
        let catalog = GovernedImportCatalog {
            schema: GOVERNED_IMPORT_SCHEMA.into(),
            schema_version: GOVERNED_IMPORT_VERSION,
            assets: selected.assets.into_values().collect(),
        };
        if catalog.assets.is_empty() {
            return Err("No imported assets to export".into());
        }
        if publish {
            catalog.validate_for_publication()
        } else {
            catalog.validate_for_local_use()
        }
        .map_err(|e| e.to_string())?;
        if serde_json::to_vec(&catalog)
            .map_err(|e| e.to_string())?
            .len()
            > storage::MAX_DOCUMENT
        {
            return Err("Export catalogue metadata budget exceeded".into());
        }
        let mut files = self.export_pinned_files(&catalog)?;
        self.export_evidence(&catalog, &mut files)?;
        Ok(ImportedExportSnapshot { catalog, files })
    }

    fn export_pinned_files(
        &self,
        catalog: &GovernedImportCatalog,
    ) -> Result<BTreeMap<String, Vec<u8>>, String> {
        let mut declared = BTreeMap::new();
        for asset in &catalog.assets {
            declared.insert(asset.payload.path.clone(), &asset.payload);
            if let Some(file) = &asset.thumbnail {
                declared.insert(file.path.clone(), file);
            }
            if let Some(
                DurableAssetSource::AuthoredFile { file } | DurableAssetSource::Recipe { file },
            ) = &asset.durable_source
            {
                declared.insert(file.path.clone(), file);
            }
        }
        let total = declared
            .values()
            .try_fold(0_u64, |total, file| add(total, file.bytes))?;
        if total > MAX_EXPORT_BYTES {
            return Err("Export snapshot byte budget exceeded".into());
        }
        let mut files = BTreeMap::new();
        for (path, pin) in declared {
            let limit = usize::try_from(pin.bytes).map_err(|e| e.to_string())?;
            let bytes = self.read_bytes(&path, limit)?;
            if u64::try_from(bytes.len()).ok() != Some(pin.bytes)
                || bytes_sha256(&bytes) != pin.sha256
            {
                return Err(format!(
                    "Export asset file differs from its content pin: {path}"
                ));
            }
            files.insert(path, bytes);
        }
        Ok(files)
    }

    fn export_evidence(
        &self,
        catalog: &GovernedImportCatalog,
        files: &mut BTreeMap<String, Vec<u8>>,
    ) -> Result<(), String> {
        let mut total = files.values().try_fold(0_u64, |total, bytes| {
            add(
                total,
                u64::try_from(bytes.len()).map_err(|e| e.to_string())?,
            )
        })?;
        let mut proofs = BTreeSet::new();
        for asset in &catalog.assets {
            proofs.insert(asset.provenance.evidence.as_str());
            if let Some(path) = &asset.provenance.rights.redistribution_evidence {
                proofs.insert(path.as_str());
            }
        }
        for path in proofs {
            if files.contains_key(path) {
                return Err(format!("Licence evidence aliases an asset file: {path}"));
            }
            let remaining = MAX_EXPORT_BYTES
                .checked_sub(total)
                .ok_or("Export snapshot byte budget exceeded")?;
            let limit = usize::try_from(remaining.min(65536)).map_err(|e| e.to_string())?;
            let bytes = self.read_bytes(path, limit)?;
            if pin_file("licenses", "txt", &bytes)?.path != path {
                return Err(format!(
                    "Export licence evidence differs from its content pin: {path}"
                ));
            }
            let text = std::str::from_utf8(&bytes)
                .map_err(|e| format!("Invalid licence UTF-8 evidence {path}: {e}"))?;
            if text.trim().is_empty()
                || text.chars().any(|c| {
                    c == '\u{fffd}' || (c.is_control() && !matches!(c, '\n' | '\r' | '\t'))
                })
            {
                return Err(format!("Invalid licence evidence text: {path}"));
            }
            total = add(
                total,
                u64::try_from(bytes.len()).map_err(|e| e.to_string())?,
            )?;
            files.insert(path.into(), bytes);
        }
        Ok(())
    }
}

fn requested(pins: &[(AssetId, ContentHash)]) -> Result<BTreeMap<AssetId, ContentHash>, String> {
    if pins.len() > forge_assets::governed_import::MAX_IMPORT_ASSETS {
        return Err("Too many required imported asset pins".into());
    }
    let mut result = BTreeMap::new();
    for (id, hash) in pins {
        if result
            .insert(*id, *hash)
            .is_some_and(|previous| previous != *hash)
        {
            return Err(format!(
                "One export cannot represent two content hashes for imported asset {id}"
            ));
        }
    }
    Ok(result)
}

#[derive(Default)]
struct Selected {
    assets: BTreeMap<AssetId, GovernedImportAsset>,
    parents: BTreeMap<AssetId, (AssetId, ContentHash)>,
    constrained: BTreeMap<AssetId, ContentHash>,
}
impl Selected {
    fn seed(&mut self, catalog: &GovernedImportCatalog) {
        for asset in &catalog.assets {
            self.assets.insert(asset.id, asset.clone());
            if let Some(parent) = asset
                .derived_from
                .and_then(|id| catalog.assets.iter().find(|a| a.id == id))
            {
                self.parents
                    .insert(asset.id, (parent.id, parent.payload.sha256));
            }
        }
    }
    fn resolve(
        &mut self,
        asset: &GovernedImportAsset,
        catalog: &GovernedImportCatalog,
        required: &BTreeMap<AssetId, ContentHash>,
    ) -> Result<(), String> {
        let mut cursor = Some(asset);
        while let Some(asset) = cursor {
            if required
                .get(&asset.id)
                .is_some_and(|hash| *hash != asset.payload.sha256)
                || self
                    .constrained
                    .get(&asset.id)
                    .is_some_and(|hash| *hash != asset.payload.sha256)
            {
                return Err(format!(
                    "Historical lineage requires conflicting content pins for imported asset {}",
                    asset.id
                ));
            }
            if self.constrained.contains_key(&asset.id) {
                break;
            }
            if !self.assets.contains_key(&asset.id)
                && self.assets.len() >= forge_assets::governed_import::MAX_IMPORT_ASSETS
            {
                return Err("Export asset count budget exceeded".into());
            }
            self.constrained.insert(asset.id, asset.payload.sha256);
            self.assets.insert(asset.id, asset.clone());
            self.parents.remove(&asset.id);
            cursor = asset
                .derived_from
                .map(|id| {
                    catalog
                        .assets
                        .iter()
                        .find(|a| a.id == id)
                        .ok_or_else(|| format!("Historical lineage parent {id} is missing"))
                })
                .transpose()?;
            if let Some(parent) = cursor {
                self.parents
                    .insert(asset.id, (parent.id, parent.payload.sha256));
            }
        }
        Ok(())
    }
    fn verify_lineage(&self) -> Result<(), String> {
        for (id, (parent, pin)) in &self.parents {
            if self
                .assets
                .get(parent)
                .is_none_or(|asset| asset.payload.sha256 != *pin)
            {
                return Err(format!(
                    "Exported lineage for asset {id} requires another revision of parent {parent}"
                ));
            }
        }
        Ok(())
    }
}
fn add(total: u64, value: u64) -> Result<u64, String> {
    let total = total
        .checked_add(value)
        .ok_or("Export snapshot byte budget overflow")?;
    if total > MAX_EXPORT_BYTES {
        return Err("Export snapshot byte budget exceeded".into());
    }
    Ok(total)
}
