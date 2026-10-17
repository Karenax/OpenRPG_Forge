//! User-owned import store. No Asset Vault writes and no implicit placement adoption.
use crate::asset_import_protocol::{
    ImportCandidate, ImportReadRole, ImportReceipt, ImportRequest, ImportResponse, ImportSource,
    ImportSourceKind, ImportState, ImportUpload, ImportedFile, MAX_THUMBNAIL_BYTES,
    MAX_UPLOAD_BYTES, PreparedImport,
};
use forge_assets::governed_import::{
    AssetTransformation, DurableAssetSource, GOVERNED_IMPORT_SCHEMA, GOVERNED_IMPORT_VERSION,
    GovernedAssetFile, GovernedAssetKind, GovernedImportAsset, GovernedImportCatalog,
    RedistributionRights,
};
use forge_core::{AssetId, CommandId, ContentHash, bytes_sha256};
use serde::{Deserialize, Serialize};
use std::path::{Path, PathBuf};
mod export;
mod media;
mod storage;
pub use export::ImportedExportSnapshot;

const MAX_HISTORY: usize = 256;
#[derive(Clone, Default, Serialize, Deserialize)]
#[serde(deny_unknown_fields)]
struct Snapshot {
    revision: u64,
    catalog_sha256: Option<ContentHash>,
    last_receipt: Option<ImportReceipt>,
    last_plan: Option<ContentHash>,
}
#[derive(Default, Serialize, Deserialize)]
#[serde(deny_unknown_fields)]
struct Head {
    version: u32,
    current: Snapshot,
    history: Vec<Snapshot>,
    catalogs: Vec<ContentHash>,
}
#[derive(Serialize, Deserialize)]
#[serde(deny_unknown_fields)]
struct Plan {
    version: u32,
    command_id: CommandId,
    base_revision: u64,
    base_catalog: Option<ContentHash>,
    asset_id: AssetId,
    previous_asset_sha256: Option<ContentHash>,
    catalog: GovernedImportCatalog,
    extra_files: Vec<GovernedAssetFile>,
}

pub struct AssetImportStore {
    root: PathBuf,
}

impl AssetImportStore {
    pub fn new(root: &Path) -> Result<Self, String> {
        if !root.is_absolute() {
            return Err("Import store requires an absolute user-data root".into());
        }
        storage::no_links(root)?;
        Ok(Self {
            root: root.to_path_buf(),
        })
    }
    #[must_use]
    pub fn root(&self) -> &Path {
        &self.root
    }
    pub fn state(&self) -> Result<ImportState, String> {
        let head = self.head()?;
        Ok(ImportState {
            revision: head.current.revision,
            catalog_sha256: head.current.catalog_sha256,
            catalog: head
                .current
                .catalog_sha256
                .map(|hash| self.catalog(hash))
                .transpose()?,
        })
    }
    pub fn prepare(&self, candidate: &ImportCandidate) -> Result<PreparedImport, String> {
        let _lock = self.lock()?;
        let head = self.head()?;
        if candidate.expected_revision != head.current.revision {
            return Err("Collection changed; review the import again".into());
        }
        let mut catalog = match head.current.catalog_sha256 {
            Some(hash) => self.catalog(hash)?,
            None => GovernedImportCatalog {
                schema: GOVERNED_IMPORT_SCHEMA.into(),
                schema_version: GOVERNED_IMPORT_VERSION,
                assets: Vec::new(),
            },
        };
        let previous = catalog.assets.iter().find(|a| a.id == candidate.asset_id);
        Self::validate_variant(candidate, previous, &catalog)?;
        match (previous, candidate.expected_asset_sha256) {
            (None, None) => {}
            (Some(old), Some(pin)) if old.payload.sha256 == pin && old.kind == candidate.kind => {}
            _ => {
                return Err(
                    "Existing asset identity/content/type differs; explicit current pin required"
                        .into(),
                );
            }
        }
        if candidate.authored_changes && candidate.source.is_none() {
            return Err("Retouches require a preserved authored source or recipe".into());
        }
        let (asset, extra_files) = self.stage_asset(candidate, previous, &catalog)?;
        catalog.assets.retain(|a| a.id != candidate.asset_id);
        catalog.assets.push(asset.clone());
        catalog.assets.sort_by_key(|a| a.id);
        catalog
            .validate_for_local_use()
            .map_err(|e| e.to_string())?;
        let plan = Plan {
            version: 1,
            command_id: candidate.command_id,
            base_revision: head.current.revision,
            base_catalog: head.current.catalog_sha256,
            asset_id: asset.id,
            previous_asset_sha256: candidate.expected_asset_sha256,
            catalog,
            extra_files,
        };
        let bytes = serde_json::to_vec(&plan).map_err(|e| e.to_string())?;
        if bytes.len() > storage::MAX_DOCUMENT {
            return Err("Import plan exceeds metadata limit".into());
        }
        let hash = bytes_sha256(&bytes);
        self.publish_bytes(
            &format!("commands/{}.json", candidate.command_id),
            &serde_json::to_vec(&hash).map_err(|e| e.to_string())?,
        )?;
        self.publish_bytes(&format!("plans/{hash}.json"), &bytes)?;
        Ok(PreparedImport {
            command_id: plan.command_id,
            plan_sha256: hash,
            base_revision: plan.base_revision,
            previous_asset_sha256: plan.previous_asset_sha256,
            asset,
            catalog_sha256: catalog_hash(&plan.catalog)?,
        })
    }
    pub fn apply(
        &self,
        command_id: CommandId,
        plan_sha256: ContentHash,
    ) -> Result<ImportReceipt, String> {
        let _lock = self.lock()?;
        let mut head = self.head()?;
        if let Some(receipt) = &head.current.last_receipt
            && receipt.command_id == command_id
        {
            if head.current.last_plan == Some(plan_sha256) && !receipt.undo {
                return Ok(receipt.clone());
            }
            return Err("Command identity was already used differently".into());
        }
        let bytes = self.read_bytes(&format!("plans/{plan_sha256}.json"), storage::MAX_DOCUMENT)?;
        if bytes_sha256(&bytes) != plan_sha256 {
            return Err("Import plan SHA-256 changed".into());
        }
        let plan: Plan = serde_json::from_slice(&bytes).map_err(|e| e.to_string())?;
        if plan.version != 1
            || plan.command_id != command_id
            || plan.base_revision != head.current.revision
            || plan.base_catalog != head.current.catalog_sha256
        {
            return Err("Import plan is stale or belongs to another command".into());
        }
        plan.catalog
            .validate_for_local_use()
            .map_err(|e| e.to_string())?;
        self.verify_catalog_files(&plan.catalog)?;
        for file in &plan.extra_files {
            self.verify_file(file)?;
        }
        let catalog_bytes = serde_json::to_vec(&plan.catalog).map_err(|e| e.to_string())?;
        let hash = bytes_sha256(&catalog_bytes);
        self.publish_bytes(&format!("catalogs/{hash}.json"), &catalog_bytes)?;
        let asset = plan
            .catalog
            .assets
            .iter()
            .find(|a| a.id == plan.asset_id)
            .ok_or("Planned asset absent")?;
        let receipt = ImportReceipt {
            command_id,
            revision: head
                .current
                .revision
                .checked_add(1)
                .ok_or("Import revision overflow")?,
            previous_catalog_sha256: head.current.catalog_sha256,
            catalog_sha256: Some(hash),
            asset_id: Some(asset.id),
            previous_asset_sha256: plan.previous_asset_sha256,
            asset_sha256: Some(asset.payload.sha256),
            undo: false,
        };
        if head.history.len() >= MAX_HISTORY
            || (!head.catalogs.contains(&hash) && head.catalogs.len() >= MAX_HISTORY)
        {
            return Err(
                "Import history limit reached; preserve/export history before adding revisions"
                    .into(),
            );
        }
        head.history.push(head.current.clone());
        if !head.catalogs.contains(&hash) {
            head.catalogs.push(hash);
        }
        head.current = Snapshot {
            revision: receipt.revision,
            catalog_sha256: Some(hash),
            last_receipt: Some(receipt.clone()),
            last_plan: Some(plan_sha256),
        };
        self.publish_bytes(
            &format!("receipts/{}-{command_id}.json", receipt.revision),
            &serde_json::to_vec(&receipt).map_err(|e| e.to_string())?,
        )?;
        self.write_head(&head)?;
        Ok(receipt)
    }
    pub fn undo(
        &self,
        command_id: CommandId,
        expected_revision: u64,
    ) -> Result<ImportReceipt, String> {
        let _lock = self.lock()?;
        let mut head = self.head()?;
        if let Some(receipt) = &head.current.last_receipt
            && receipt.command_id == command_id
            && receipt.undo
        {
            if receipt.revision.checked_sub(1) == Some(expected_revision) {
                return Ok(receipt.clone());
            }
            return Err("Undo command identity was already used differently".into());
        }
        if expected_revision != head.current.revision {
            return Err("Collection changed; review Undo again".into());
        }
        let previous = head.history.pop().ok_or("No imported revision to undo")?;
        if let Some(hash) = previous.catalog_sha256 {
            self.verify_catalog_files(&self.catalog(hash)?)?;
        }
        let receipt = ImportReceipt {
            command_id,
            revision: head
                .current
                .revision
                .checked_add(1)
                .ok_or("Import revision overflow")?,
            previous_catalog_sha256: head.current.catalog_sha256,
            catalog_sha256: previous.catalog_sha256,
            asset_id: None,
            previous_asset_sha256: None,
            asset_sha256: None,
            undo: true,
        };
        self.publish_bytes(
            &format!("commands/{command_id}.json"),
            &serde_json::to_vec(&receipt).map_err(|e| e.to_string())?,
        )?;
        head.current = Snapshot {
            revision: receipt.revision,
            catalog_sha256: previous.catalog_sha256,
            last_receipt: Some(receipt.clone()),
            last_plan: None,
        };
        self.publish_bytes(
            &format!("receipts/{}-{command_id}.json", receipt.revision),
            &serde_json::to_vec(&receipt).map_err(|e| e.to_string())?,
        )?;
        self.write_head(&head)?;
        Ok(receipt)
    }
    pub fn read(
        &self,
        asset_id: AssetId,
        sha256: ContentHash,
        role: ImportReadRole,
    ) -> Result<ImportedFile, String> {
        self.lookup(asset_id, sha256, role)?
            .ok_or_else(|| format!("Unknown imported identity {asset_id}"))
    }
    /// Only a never-imported identity returns None. A known identity with a stale
    /// pin, corrupt history or unreadable payload must fail without a Vault fallback.
    pub fn lookup_model(
        &self,
        asset_id: AssetId,
        sha256: ContentHash,
    ) -> Result<Option<ImportedFile>, String> {
        let file = self.lookup(asset_id, sha256, ImportReadRole::Payload)?;
        if file
            .as_ref()
            .is_some_and(|f| f.kind != GovernedAssetKind::Mesh)
        {
            return Err("Imported identity is not a 3D model".into());
        }
        Ok(file)
    }
    pub fn execute(&self, request: ImportRequest) -> Result<ImportResponse, String> {
        match request {
            ImportRequest::State => self.state().map(|state| ImportResponse::State { state }),
            ImportRequest::Prepare { candidate } => {
                self.prepare(&candidate)
                    .map(|plan| ImportResponse::Prepared {
                        plan: Box::new(plan),
                    })
            }
            ImportRequest::Apply {
                command_id,
                plan_sha256,
            } => self
                .apply(command_id, plan_sha256)
                .map(|receipt| ImportResponse::Applied { receipt }),
            ImportRequest::Undo {
                command_id,
                expected_revision,
            } => self
                .undo(command_id, expected_revision)
                .map(|receipt| ImportResponse::Applied { receipt }),
            ImportRequest::Read {
                asset_id,
                sha256,
                role,
            } => self
                .read(asset_id, sha256, role)
                .map(|file| ImportResponse::File { file }),
        }
    }

    fn head(&self) -> Result<Head, String> {
        storage::no_links(&self.root)?;
        if !self.path("head.json")?.exists() {
            return Ok(Head {
                version: 1,
                ..Head::default()
            });
        }
        let bytes = self.read_bytes("head.json", storage::MAX_DOCUMENT)?;
        let head: Head =
            serde_json::from_slice(&bytes).map_err(|e| format!("Invalid import head: {e}"))?;
        if head.version != 1
            || head.history.len() > MAX_HISTORY
            || head.catalogs.len() > MAX_HISTORY
            || head
                .current
                .catalog_sha256
                .is_some_and(|hash| !head.catalogs.contains(&hash))
        {
            return Err("Unsupported or inconsistent import head/history".into());
        }
        Ok(head)
    }
    fn catalog(&self, hash: ContentHash) -> Result<GovernedImportCatalog, String> {
        let bytes = self.read_bytes(&format!("catalogs/{hash}.json"), storage::MAX_DOCUMENT)?;
        if bytes_sha256(&bytes) != hash {
            return Err("Imported catalogue integrity mismatch".into());
        }
        let catalog: GovernedImportCatalog =
            serde_json::from_slice(&bytes).map_err(|e| e.to_string())?;
        catalog
            .validate_for_local_use()
            .map_err(|e| e.to_string())?;
        Ok(catalog)
    }
    fn lookup(
        &self,
        id: AssetId,
        pin: ContentHash,
        role: ImportReadRole,
    ) -> Result<Option<ImportedFile>, String> {
        let head = self.head()?;
        let mut known = false;
        let mut snapshots = Vec::new();
        if let Some(hash) = head.current.catalog_sha256 {
            snapshots.push(hash);
        }
        for hash in head.catalogs.iter().rev() {
            if !snapshots.contains(hash) {
                snapshots.push(*hash);
            }
        }
        for hash in snapshots {
            let catalog = self.catalog(hash)?;
            let Some(asset) = catalog.assets.into_iter().find(|asset| asset.id == id) else {
                continue;
            };
            known = true;
            if asset.payload.sha256 != pin {
                continue;
            }
            let file = match role {
                ImportReadRole::Payload => &asset.payload,
                ImportReadRole::Thumbnail => asset
                    .thumbnail
                    .as_ref()
                    .ok_or("No chosen/imported thumbnail for this revision")?,
                ImportReadRole::Source => match &asset.durable_source {
                    Some(
                        DurableAssetSource::AuthoredFile { file }
                        | DurableAssetSource::Recipe { file },
                    ) => file,
                    None => return Err("No durable source for this asset revision".into()),
                },
            };
            self.verify_file(file)?;
            let bytes = self.read_bytes(&file.path, MAX_UPLOAD_BYTES)?;
            if bytes_sha256(&bytes) != file.sha256 {
                return Err("Imported file changed after verification".into());
            }
            return Ok(Some(ImportedFile {
                asset_id: id,
                sha256: file.sha256,
                catalog_sha256: hash,
                kind: asset.kind,
                mime_type: media::mime(&file.path).into(),
                upload: ImportUpload {
                    name: file.path.rsplit('/').next().unwrap_or_default().into(),
                    byte_length: file.bytes,
                    bytes,
                },
            }));
        }
        if known {
            return Err(format!(
                "Known imported identity {id} has no preserved content pin {pin}"
            ));
        }
        Ok(None)
    }
    fn stage_asset(
        &self,
        candidate: &ImportCandidate,
        previous: Option<&GovernedImportAsset>,
        catalog: &GovernedImportCatalog,
    ) -> Result<(GovernedImportAsset, Vec<GovernedAssetFile>), String> {
        let ext = media::media(&candidate.payload, candidate.kind)?;
        let payload = pin_file("assets", &ext, &candidate.payload.bytes)?;
        let original = pin_file("sources", &ext, &candidate.payload.bytes)?;
        let (source, source_bytes) = if let Some(source) = &candidate.source {
            let ext = media::source(source)?;
            let pin = pin_file("sources", &ext, &source.upload.bytes)?;
            (
                match source.kind {
                    ImportSourceKind::AuthoredFile => {
                        DurableAssetSource::AuthoredFile { file: pin }
                    }
                    ImportSourceKind::Recipe => DurableAssetSource::Recipe { file: pin },
                },
                source.upload.bytes.as_slice(),
            )
        } else {
            (
                DurableAssetSource::AuthoredFile {
                    file: original.clone(),
                },
                candidate.payload.bytes.as_slice(),
            )
        };
        let derived_from = candidate
            .derived_from
            .or_else(|| previous.and_then(|asset| asset.derived_from));
        let inherited_proof = previous.or_else(|| {
            derived_from.and_then(|id| catalog.assets.iter().find(|asset| asset.id == id))
        });
        let license_text = self.license_text(candidate, inherited_proof)?;
        let license = pin_file("licenses", "txt", &license_text)?;
        let thumbnail = if let Some(upload) = &candidate.thumbnail {
            media::check_upload(upload, MAX_THUMBNAIL_BYTES)?;
            if media::media(upload, GovernedAssetKind::Texture)? != "png" {
                return Err("A chosen thumbnail must be PNG".into());
            }
            Some(pin_file("thumbnails", "png", &upload.bytes)?)
        } else if candidate.kind == GovernedAssetKind::Texture {
            Some(payload.clone())
        } else {
            None
        };
        let mut provenance = candidate.provenance.clone();
        provenance.evidence.clone_from(&license.path);
        provenance.rights.redistribution_evidence =
            if provenance.rights.redistribution == RedistributionRights::Allowed {
                Some(license.path.clone())
            } else {
                None
            };
        let transformations = if candidate.source.is_some() {
            vec![AssetTransformation {
                tool: "forge.asset-import.preserved-source".into(),
                version: "1".into(),
                parameters_sha256: bytes_sha256(source_bytes),
            }]
        } else {
            Vec::new()
        };
        let asset = GovernedImportAsset {
            id: candidate.asset_id,
            name: candidate.name.clone(),
            kind: candidate.kind,
            payload: payload.clone(),
            provenance,
            derived_from,
            transformations,
            authored_changes: candidate.authored_changes,
            durable_source: Some(source.clone()),
            thumbnail: thumbnail.clone(),
        };
        let mut proposed = catalog.clone();
        proposed.assets.retain(|existing| existing.id != asset.id);
        proposed.assets.push(asset.clone());
        proposed
            .validate_for_local_use()
            .map_err(|e| e.to_string())?;
        // Unreferenced immutable files are staging only; head.json is unchanged until Apply.
        self.publish_bytes(&payload.path, &candidate.payload.bytes)?;
        self.publish_bytes(&original.path, &candidate.payload.bytes)?;
        let source_pin = match &source {
            DurableAssetSource::AuthoredFile { file } | DurableAssetSource::Recipe { file } => file,
        };
        self.publish_bytes(&source_pin.path, source_bytes)?;
        self.publish_bytes(&license.path, &license_text)?;
        if let (Some(upload), Some(pin)) = (&candidate.thumbnail, &thumbnail) {
            self.publish_bytes(&pin.path, &upload.bytes)?;
        }
        Ok((asset, vec![original, license]))
    }
    fn validate_variant(
        candidate: &ImportCandidate,
        previous: Option<&GovernedImportAsset>,
        catalog: &GovernedImportCatalog,
    ) -> Result<(), String> {
        let previous_parent = previous.and_then(|asset| asset.derived_from);
        let Some(parent_id) = candidate.derived_from.or(previous_parent) else {
            return Ok(());
        };
        if parent_id == candidate.asset_id {
            return Err("variant identity must differ from its parent".into());
        }
        if previous.is_some() && previous_parent.is_none() {
            return Err(
                "variant identity must be new; existing standalone assets cannot be reparented"
                    .into(),
            );
        }
        if previous_parent.is_some_and(|parent| parent != parent_id) {
            return Err("variant parent cannot be changed during reimport".into());
        }
        let parent = catalog
            .assets
            .iter()
            .find(|asset| asset.id == parent_id)
            .ok_or_else(|| {
                format!("variant parent {parent_id} is not in the current collection")
            })?;
        if parent.kind != candidate.kind {
            return Err("variant media type differs from its parent".into());
        }
        if !parent.provenance.rights.local_use || !candidate.provenance.rights.local_use {
            return Err("variant local rights must be explicit for the parent and child".into());
        }
        if !candidate.authored_changes || candidate.source.is_none() {
            return Err("variant retouches require a preserved authored source or recipe".into());
        }
        Ok(())
    }
    fn license_text(
        &self,
        candidate: &ImportCandidate,
        previous: Option<&GovernedImportAsset>,
    ) -> Result<Vec<u8>, String> {
        if candidate.license_text.trim().is_empty() {
            let old = previous.ok_or("Provide licence/provenance evidence for a new import")?;
            let bytes = self.read_bytes(&old.provenance.evidence, 65536)?;
            let pin = pin_file("licenses", "txt", &bytes)?;
            if pin.path != old.provenance.evidence {
                return Err("Previous licence proof pin differs".into());
            }
            Ok(bytes)
        } else {
            if candidate.license_text.len() > 65536
                || candidate.license_text.chars().any(|c| {
                    c == '\u{fffd}' || (c.is_control() && !matches!(c, '\n' | '\r' | '\t'))
                })
            {
                return Err("Invalid or oversized licence evidence".into());
            }
            Ok(candidate.license_text.as_bytes().to_vec())
        }
    }
    fn verify_catalog_files(&self, catalog: &GovernedImportCatalog) -> Result<(), String> {
        let mut checked = std::collections::BTreeSet::new();
        for asset in &catalog.assets {
            let mut files = vec![&asset.payload];
            if let Some(file) = &asset.thumbnail {
                files.push(file);
            }
            if let Some(
                DurableAssetSource::AuthoredFile { file } | DurableAssetSource::Recipe { file },
            ) = &asset.durable_source
            {
                files.push(file);
            }
            for file in files {
                if checked.insert(file.path.as_str()) {
                    self.verify_file(file)?;
                }
            }
            let evidence = self.read_bytes(&asset.provenance.evidence, 65536)?;
            let pin = pin_file("licenses", "txt", &evidence)?;
            if pin.path != asset.provenance.evidence {
                return Err("Imported licence evidence changed".into());
            }
        }
        Ok(())
    }
}

fn catalog_hash(catalog: &GovernedImportCatalog) -> Result<ContentHash, String> {
    serde_json::to_vec(catalog)
        .map(|bytes| bytes_sha256(&bytes))
        .map_err(|e| e.to_string())
}
fn pin_file(folder: &str, extension: &str, bytes: &[u8]) -> Result<GovernedAssetFile, String> {
    let hash = bytes_sha256(bytes);
    Ok(GovernedAssetFile {
        path: format!("{folder}/{hash}.{extension}"),
        sha256: hash,
        bytes: u64::try_from(bytes.len()).map_err(|e| e.to_string())?,
    })
}
