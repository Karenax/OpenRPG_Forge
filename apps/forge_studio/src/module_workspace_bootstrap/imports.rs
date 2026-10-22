use crate::{
    asset_import_native::AssetImportStore,
    asset_import_protocol::{ImportCandidate, ImportSource, ImportSourceKind, ImportUpload},
};
use forge_assets::governed_import::{
    DurableAssetSource, GovernedAssetFile, GovernedAssetKind, GovernedImportAsset,
    GovernedImportCatalog,
};
use forge_core::{AssetId, CommandId, ContentHash};
use forge_delivery::module_runtime::LoadedModuleZone;
use std::{collections::BTreeSet, path::Path};

pub(super) fn import_catalog(
    root: &Path,
    loaded: &LoadedModuleZone,
) -> Result<ContentHash, String> {
    let bytes = loaded.read_payload(&loaded.manifest.asset_catalog_path, 16 * 1024 * 1024)?;
    let catalog: GovernedImportCatalog =
        serde_json::from_slice(&bytes).map_err(|error| error.to_string())?;
    catalog
        .validate_for_local_use()
        .map_err(|error| error.to_string())?;
    let store = AssetImportStore::new(&root.join("asset-imports"))?;
    let mut imported = BTreeSet::<AssetId>::new();
    while imported.len() < catalog.assets.len() {
        let before = imported.len();
        for asset in &catalog.assets {
            if imported.contains(&asset.id)
                || asset
                    .derived_from
                    .is_some_and(|parent| !imported.contains(&parent))
            {
                continue;
            }
            let state = store.state()?;
            let previous = state
                .catalog
                .as_ref()
                .and_then(|catalog| catalog.assets.iter().find(|entry| entry.id == asset.id));
            let mut candidate = candidate(loaded, asset, state.revision)?;
            candidate.expected_asset_sha256 = previous.map(|entry| entry.payload.sha256);
            let plan = store
                .prepare(&candidate)
                .map_err(|error| format!("Import asset {}: {error}", asset.id))?;
            if let Some(previous) = previous {
                if plan.asset != *previous {
                    return Err(format!(
                        "Shared asset {} has conflicting metadata or revision; existing modules were not overwritten",
                        asset.id
                    ));
                }
            } else {
                store.apply(plan.command_id, plan.plan_sha256)?;
            }
            imported.insert(asset.id);
        }
        if imported.len() == before {
            return Err("No parent-first traversal of the admitted asset lineage".into());
        }
    }
    // Snapshot checks the exact returned payload/source/thumbnail/licence buffers.
    let snapshot = store.export_snapshot(&[], false)?;
    for asset in &catalog.assets {
        let actual = snapshot
            .catalog
            .assets
            .iter()
            .find(|actual| actual.id == asset.id)
            .ok_or("Imported asset identity missing after import")?;
        if actual.payload.sha256 != asset.payload.sha256
            || actual.derived_from != asset.derived_from
            || actual.provenance.rights.local_use != asset.provenance.rights.local_use
            || actual.provenance.rights.redistribution != asset.provenance.rights.redistribution
        {
            return Err(format!(
                "Imported identity, payload, lineage or rights differ: {}",
                asset.id
            ));
        }
        if asset.kind == GovernedAssetKind::Mesh
            && store
                .lookup_model(asset.id, asset.payload.sha256)?
                .is_none()
        {
            return Err("Imported model cannot be resolved by its exact identity and pin".into());
        }
    }
    store
        .state()?
        .catalog_sha256
        .ok_or("Imported catalogue head is absent".into())
}

fn candidate(
    loaded: &LoadedModuleZone,
    asset: &GovernedImportAsset,
    revision: u64,
) -> Result<ImportCandidate, String> {
    let source = asset
        .durable_source
        .as_ref()
        .map(|source| {
            let (kind, file) = match source {
                DurableAssetSource::AuthoredFile { file } => (ImportSourceKind::AuthoredFile, file),
                DurableAssetSource::Recipe { file } => (ImportSourceKind::Recipe, file),
            };
            Ok::<_, String>(ImportSource {
                kind,
                upload: upload(loaded, file)?,
            })
        })
        .transpose()?;
    Ok(ImportCandidate {
        command_id: CommandId::deterministic(format!(
            "module-bootstrap/{}/{}/{}",
            loaded.manifest.module_id, asset.id, asset.payload.sha256
        )),
        expected_revision: revision,
        asset_id: asset.id,
        expected_asset_sha256: None,
        derived_from: asset.derived_from,
        name: asset.name.clone(),
        kind: asset.kind,
        provenance: asset.provenance.clone(),
        license_text: license_text(loaded, asset)?,
        payload: upload(loaded, &asset.payload)?,
        authored_changes: asset.authored_changes,
        source,
        thumbnail: asset
            .thumbnail
            .as_ref()
            .map(|file| upload(loaded, file))
            .transpose()?,
    })
}

fn upload(loaded: &LoadedModuleZone, file: &GovernedAssetFile) -> Result<ImportUpload, String> {
    let extension = file
        .path
        .rsplit_once('.')
        .map(|(_, extension)| extension)
        .ok_or("Source file extension absent")?;
    let bytes = loaded.read_payload(&file.path, 256 * 1024 * 1024)?;
    Ok(ImportUpload {
        name: format!("original.{extension}"),
        byte_length: file.bytes,
        bytes,
    })
}

fn license_text(loaded: &LoadedModuleZone, asset: &GovernedImportAsset) -> Result<String, String> {
    let mut paths = vec![asset.provenance.evidence.as_str()];
    if let Some(path) = asset.provenance.rights.redistribution_evidence.as_deref()
        && !paths.contains(&path)
    {
        paths.push(path);
    }
    let mut text = String::new();
    for path in paths {
        let bytes = loaded.read_payload(path, 65536)?;
        let evidence = String::from_utf8(bytes)
            .map_err(|_| format!("Import profile requires UTF-8 licence evidence: {path}"))?;
        if !text.is_empty() {
            text.push_str("\n\n--- Additional original redistribution evidence ---\n");
        }
        text.push_str(&evidence);
        if text.len() > 65536 {
            return Err(
                "Import profile licence evidence exceeds 64 KiB; no evidence was truncated".into(),
            );
        }
    }
    Ok(text)
}
