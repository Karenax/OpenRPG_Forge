use super::{ModuleOriginReceipt, files, imports, validate_authoring_module};
use forge_core::{ContentHash, ModuleId, bytes_sha256};
use forge_delivery::{manifest::safe_chain, orpmod::OriginDocuments};
use forge_project::{
    audit::AuditStamp,
    workspace::{
        MAX_MODULE_ORIGIN_PACKAGE_BYTES, ModuleOriginPin, file_session::FileWorkspaceSession,
        session::WorkspaceReceipt,
    },
};
use std::{fs, path::Path, sync::Arc};

pub fn import_module_into_workspace(
    root: &Path,
    workspace: &mut FileWorkspaceSession,
    bytes: Vec<u8>,
    audit: AuditStamp,
) -> Result<WorkspaceReceipt, String> {
    if bytes.is_empty() || bytes.len() as u64 > MAX_MODULE_ORIGIN_PACKAGE_BYTES {
        return Err("Choisissez un fichier .orpmod de 256 Mio maximum.".into());
    }
    let bytes: Arc<[u8]> = bytes.into();
    let documents = forge_delivery::orpmod::read_origin_documents(Arc::clone(&bytes))?;
    validate_authoring_module(&documents.module)?;
    let (pin, receipt) = receipt(&bytes, &documents, bytes_sha256(&bytes))?;
    let read_payload = |path: &str, limit| documents.read_payload(path, limit);
    let prepared = workspace.prepare_module_import(&documents.module, pin, audit, read_payload)?;
    let module = documents.module.module_id;
    let destination = root.join("module-imports").join(module.to_string());
    check_existing(&destination, pin, &receipt)?;
    imports::import_catalog(root, &documents, pin.package_sha256)?;
    preserve(root, module, &bytes, pin, &receipt)?;
    workspace.apply_module_import(prepared, read_payload)
}

pub(super) fn receipt(
    bytes: &[u8],
    documents: &OriginDocuments,
    package_sha256: ContentHash,
) -> Result<(ModuleOriginPin, Vec<u8>), String> {
    let receipt = ModuleOriginReceipt {
        schema: "forge.module-import-origin".into(),
        version: 1,
        module_id: documents.module.module_id,
        project_id: documents.module.project_id,
        package_sha256,
        package_bytes: bytes.len() as u64,
        module_manifest_sha256: bytes_sha256(&documents.module_manifest),
        origin_rights: documents.module.provenance.rights,
    };
    let encoded = serde_json::to_vec(&receipt).map_err(|error| error.to_string())?;
    let pin = ModuleOriginPin {
        package_sha256,
        package_bytes: receipt.package_bytes,
        receipt_sha256: bytes_sha256(&encoded),
    };
    Ok((pin, encoded))
}

fn check_existing(
    destination: &Path,
    pin: ModuleOriginPin,
    receipt: &[u8],
) -> Result<bool, String> {
    safe_chain(destination)?;
    match fs::symlink_metadata(destination) {
        Err(error) if error.kind() == std::io::ErrorKind::NotFound => return Ok(false),
        Err(error) => return Err(error.to_string()),
        Ok(_) => (),
    }
    let original = destination.join("original.orpmod");
    safe_chain(&original)?;
    let metadata = fs::symlink_metadata(&original).map_err(|error| error.to_string())?;
    if files::read_bounded(&destination.join("receipt.json"), 16 * 1024)? != receipt
        || !metadata.is_file()
        || metadata.len() != pin.package_bytes
        || forge_delivery::file_hash(&original)? != pin.package_sha256.to_string()
    {
        return Err("Une autre version de ce module est déjà conservée dans cet espace.".into());
    }
    Ok(true)
}

pub(super) fn preserve(
    root: &Path,
    module: ModuleId,
    bytes: &[u8],
    pin: ModuleOriginPin,
    receipt: &[u8],
) -> Result<(), String> {
    let parent = root.join("module-imports");
    let destination = parent.join(module.to_string());
    if check_existing(&destination, pin, receipt)? {
        return Ok(());
    }
    fs::create_dir_all(&parent).map_err(|error| error.to_string())?;
    let stage = files::new_stage(&parent)?;
    let result = (|| {
        files::write_new(&stage.join("original.orpmod"), bytes)?;
        files::write_new(&stage.join("receipt.json"), receipt)?;
        files::activate(&stage, &destination)
    })();
    if result.is_err() {
        let _ = fs::remove_file(stage.join("receipt.json"));
        let _ = fs::remove_file(stage.join("original.orpmod"));
        let _ = fs::remove_dir(stage);
    }
    result
}
