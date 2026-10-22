//! Offline qualification bootstrap through the common import/workspace commands.
//! This native producer tool is not a Studio import UI or an installed-product proof.
use forge_core::{AreaId, ContentHash, ModuleId, ProjectId};
use forge_project::{module_package::ModuleRights, workspace::ModuleOriginPin};
use serde::{Deserialize, Serialize};
use std::path::{Path, PathBuf};
use std::sync::Arc;

mod files;
mod imports;
mod set;
mod workspace;
pub use set::{
    MAX_BOOTSTRAP_MODULES, ModuleSetInput, ModuleSetReport, ModuleSetRequest,
    prepare_module_set_workspace,
};

/// Qualification producer profile; leaves at least 128 store revisions available.
pub const MAX_BOOTSTRAP_ASSETS: usize = 128;

/// Paths are fixed by the native resolver, never supplied by this document.
#[derive(Clone, Debug, Serialize, Deserialize)]
#[serde(deny_unknown_fields)]
pub struct ModuleOriginReceipt {
    pub schema: String,
    pub version: u32,
    pub module_id: ModuleId,
    pub project_id: ProjectId,
    pub package_sha256: ContentHash,
    pub package_bytes: u64,
    pub module_manifest_sha256: ContentHash,
    pub origin_rights: ModuleRights,
}

#[derive(Clone, Debug)]
pub struct BootstrapRequest {
    pub package_path: PathBuf,
    pub expected_package_sha256: ContentHash,
    pub area_id: AreaId,
    pub destination: PathBuf,
}

#[derive(Clone, Debug, Serialize)]
pub struct BootstrapReport {
    pub workspace_root: PathBuf,
    pub project_id: ProjectId,
    pub module_id: ModuleId,
    pub area_id: AreaId,
    pub package_sha256: ContentHash,
    pub zone_archive_sha256: ContentHash,
    pub zone_head_sha256: ContentHash,
    pub imported_assets: usize,
    pub import_catalog_sha256: ContentHash,
    pub preserved_package: PathBuf,
    pub preserved_payload_root: PathBuf,
}

pub fn prepare_module_workspace(request: &BootstrapRequest) -> Result<BootstrapReport, String> {
    let parent = files::fresh_destination(&request.destination)?;
    let bytes: Arc<[u8]> = files::read_bounded(
        &request.package_path,
        forge_project::workspace::MAX_MODULE_ORIGIN_PACKAGE_BYTES,
    )?
    .into();
    if forge_core::bytes_sha256(&bytes) != request.expected_package_sha256 {
        return Err("Received package pin differs from --expected-package-sha256".into());
    }
    let documents = forge_delivery::orpmod::read_origin_documents(Arc::clone(&bytes))?;
    validate_producer_module(&documents.module, request.area_id)?;
    let stage = files::new_stage(&parent)?;
    let result = prepare_staged(request, &stage, &bytes, &documents);
    result.map_err(|error| {
        format!(
            "{error}; inactive bootstrap stage retained at {}",
            stage.display()
        )
    })
}

fn validate_producer_module(
    manifest: &forge_project::module_package::ModulePackageManifest,
    area: AreaId,
) -> Result<(), String> {
    if manifest.assets.len() > MAX_BOOTSTRAP_ASSETS {
        return Err(
            "Bootstrap profile accepts at most 128 assets, reserving import revisions".into(),
        );
    }
    if manifest
        .zones
        .iter()
        .any(|zone| !zone.source_blobs.is_empty())
        || manifest
            .files
            .iter()
            .any(|file| file.role == forge_project::module_package::ModuleFileRole::SourceBlob)
    {
        return Err(
            "Bootstrap profile does not support source blobs; no source was discarded".into(),
        );
    }
    if !manifest.zones.iter().any(|zone| zone.area_id == area) {
        return Err("Requested area is absent from the admitted package".into());
    }
    Ok(())
}

pub fn read_export_origin(
    authoring_root: &Path,
    module_id: ModuleId,
    pin: ModuleOriginPin,
) -> Result<forge_delivery::module_builder::ExportOrigin, String> {
    pin.validate().map_err(|error| error.to_string())?;
    let root = authoring_root
        .join("module-imports")
        .join(module_id.to_string());
    let receipt_bytes = files::read_bounded(&root.join("receipt.json"), 16 * 1024)
        .map_err(|error| format!("Module origin receipt: {error}"))?;
    if forge_core::bytes_sha256(&receipt_bytes) != pin.receipt_sha256 {
        return Err("Module origin receipt differs from the canonical receipt pin".into());
    }
    let receipt: ModuleOriginReceipt = serde_json::from_slice(&receipt_bytes)
        .map_err(|error| format!("Module origin receipt JSON: {error}"))?;
    if receipt.schema != "forge.module-import-origin" || receipt.version != 1 {
        return Err("Unsupported module origin receipt schema/version".into());
    }
    if receipt.module_id != module_id {
        return Err("Module origin receipt identity differs from the current module".into());
    }
    if receipt.package_bytes != pin.package_bytes || receipt.package_sha256 != pin.package_sha256 {
        return Err("Module origin receipt/package pins differ from the canonical anchor".into());
    }
    let bytes: Arc<[u8]> =
        files::read_bounded(&root.join("original.orpmod"), pin.package_bytes)?.into();
    if u64::try_from(bytes.len()).ok() != Some(pin.package_bytes)
        || forge_core::bytes_sha256(&bytes) != pin.package_sha256
    {
        return Err("Module origin package size/hash differs from the canonical anchor".into());
    }
    let documents = forge_delivery::orpmod::read_origin_documents(Arc::clone(&bytes))?;
    if documents.module.module_id != module_id || documents.module.project_id != receipt.project_id
    {
        return Err("Module origin package identity differs from the receipt".into());
    }
    if forge_core::bytes_sha256(&documents.module_manifest) != receipt.module_manifest_sha256
        || documents.module.provenance.rights != receipt.origin_rights
    {
        return Err("Module origin raw manifest or rights differ from the receipt".into());
    }
    Ok(forge_delivery::module_builder::ExportOrigin {
        package_bytes: bytes,
        expected_package_sha256: pin.package_sha256,
    })
}

fn prepare_staged(
    request: &BootstrapRequest,
    stage: &Path,
    bytes: &[u8],
    documents: &forge_delivery::orpmod::OriginDocuments,
) -> Result<BootstrapReport, String> {
    let report = populate_staged(request, stage, bytes, documents)?;
    files::activate(stage, &request.destination)?;
    Ok(report)
}

fn populate_staged(
    request: &BootstrapRequest,
    stage: &Path,
    bytes: &[u8],
    documents: &forge_delivery::orpmod::OriginDocuments,
) -> Result<BootstrapReport, String> {
    let module = &documents.module;
    let relative = PathBuf::from("module-imports").join(module.module_id.to_string());
    let origin_root = stage.join(&relative);
    std::fs::create_dir_all(&origin_root).map_err(|error| error.to_string())?;
    let original = origin_root.join("original.orpmod");
    files::write_new(&original, bytes)?;
    let receipt = ModuleOriginReceipt {
        schema: "forge.module-import-origin".into(),
        version: 1,
        module_id: module.module_id,
        project_id: module.project_id,
        package_sha256: request.expected_package_sha256,
        package_bytes: u64::try_from(bytes.len()).map_err(|error| error.to_string())?,
        module_manifest_sha256: forge_core::bytes_sha256(&documents.module_manifest),
        origin_rights: module.provenance.rights,
    };
    let receipt_bytes = serde_json::to_vec(&receipt).map_err(|error| error.to_string())?;
    files::write_new(&origin_root.join("receipt.json"), &receipt_bytes)?;
    let pin = ModuleOriginPin {
        package_sha256: receipt.package_sha256,
        package_bytes: receipt.package_bytes,
        receipt_sha256: forge_core::bytes_sha256(&receipt_bytes),
    };
    let loaded = forge_delivery::module_runtime::load(
        &original,
        request.area_id,
        Some(&stage.join("module-cache")),
    )?;
    if loaded.manifest != *module {
        return Err("Staged package manifest differs from admitted original buffer".into());
    }
    let payload_root = origin_root.join("payloads");
    for file in &module.files {
        let payload = loaded.read_payload(&file.path, 256 * 1024 * 1024)?;
        let output = forge_delivery::manifest::confined(&payload_root, &file.path)?;
        std::fs::create_dir_all(output.parent().ok_or("Payload parent absent")?)
            .map_err(|error| error.to_string())?;
        files::write_new(&output, &payload)?;
    }
    files::write_new(
        &payload_root.join("module.json"),
        &documents.module_manifest,
    )?;
    let catalog_hash = imports::import_catalog(stage, &loaded)?;
    workspace::seed(stage, &loaded, pin)?;
    workspace::verify(stage, &loaded, pin)?;
    read_export_origin(stage, module.module_id, pin)?;
    Ok(BootstrapReport {
        workspace_root: request.destination.clone(),
        project_id: module.project_id,
        module_id: module.module_id,
        area_id: request.area_id,
        package_sha256: receipt.package_sha256,
        zone_archive_sha256: forge_core::bytes_sha256(&loaded.archive),
        zone_head_sha256: loaded.zone.head().content_hash()?,
        imported_assets: module.assets.len(),
        import_catalog_sha256: catalog_hash,
        preserved_package: request.destination.join(&relative).join("original.orpmod"),
        preserved_payload_root: request.destination.join(&relative).join("payloads"),
    })
}
