use crate::{
    asset_import_native::AssetImportStore,
    asset_import_protocol::{ImportCandidate, ImportSource, ImportSourceKind, ImportUpload},
};
use forge_assets::governed_import::{
    DurableAssetSource, GovernedAssetFile, GovernedImportAsset, GovernedImportCatalog,
};
use forge_core::{AssetId, CommandId, ContentHash};
use forge_delivery::orpmod::OriginDocuments;
use std::{collections::BTreeSet, path::Path};

pub(super) fn import_catalog(
    root: &Path,
    origin: &OriginDocuments,
    package_sha256: ContentHash,
) -> Result<Option<ContentHash>, String> {
    let bytes = origin.read_payload(&origin.module.asset_catalog_path, 16 * 1024 * 1024)?;
    let catalog: GovernedImportCatalog =
        serde_json::from_slice(&bytes).map_err(|error| error.to_string())?;
    forge_delivery::orpmod::validate_module_catalog(&catalog, false)?;
    let store = AssetImportStore::new(&root.join("asset-imports"))?;
    if catalog.assets.is_empty() {
        return Ok(store.state()?.catalog_sha256);
    }
    let mut imported = BTreeSet::<AssetId>::new();
    let mut ordered = Vec::with_capacity(catalog.assets.len());
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
            ordered.push(asset);
            imported.insert(asset.id);
        }
        if imported.len() == before {
            return Err("No parent-first traversal of the admitted asset lineage".into());
        }
    }
    let command = CommandId::deterministic(format!("module-assets/{package_sha256}"));
    store.merge_package_assets(
        command,
        ordered
            .into_iter()
            .map(|asset| candidate(origin, asset, command)),
    )
}

fn candidate(
    origin: &OriginDocuments,
    asset: &GovernedImportAsset,
    command_id: CommandId,
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
                upload: upload(origin, file)?,
            })
        })
        .transpose()?;
    Ok(ImportCandidate {
        command_id,
        // The package is additive under the collection lock. There is no
        // separately reviewed replacement or per-asset compare-and-swap.
        expected_revision: 0,
        asset_id: asset.id,
        expected_asset_sha256: None,
        derived_from: asset.derived_from,
        name: asset.name.clone(),
        kind: asset.kind,
        provenance: asset.provenance.clone(),
        license_text: license_text(origin, asset)?,
        payload: upload(origin, &asset.payload)?,
        authored_changes: asset.authored_changes,
        source,
        thumbnail: asset
            .thumbnail
            .as_ref()
            .map(|file| upload(origin, file))
            .transpose()?,
    })
}

fn upload(origin: &OriginDocuments, file: &GovernedAssetFile) -> Result<ImportUpload, String> {
    let extension = file
        .path
        .rsplit_once('.')
        .map(|(_, extension)| extension)
        .ok_or("Source file extension absent")?;
    let bytes = origin.read_payload(&file.path, 256 * 1024 * 1024)?;
    Ok(ImportUpload {
        name: format!("original.{extension}"),
        byte_length: file.bytes,
        bytes,
    })
}

fn license_text(origin: &OriginDocuments, asset: &GovernedImportAsset) -> Result<String, String> {
    let mut paths = vec![asset.provenance.evidence.as_str()];
    if let Some(path) = asset.provenance.rights.redistribution_evidence.as_deref()
        && !paths.contains(&path)
    {
        paths.push(path);
    }
    let mut text = String::new();
    for path in paths {
        let bytes = origin.read_payload(path, 65536)?;
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
