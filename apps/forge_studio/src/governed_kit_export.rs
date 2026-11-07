//! Capture exact deployed-kit pins; no `WebView` paths, source writes or inferred rights.
use crate::asset_import_native::{
    AvailableExportSnapshot, ImportedExportSnapshot, MAX_EXPORT_BYTES,
};
use forge_assets::{
    GovernedAssetKitBuildReport,
    asset_browser::{AssetBrowser, AssetResolutionError},
    governed_import::{
        AssetProvenance, AssetRights, DurableAssetSource, GOVERNED_IMPORT_SCHEMA,
        GOVERNED_IMPORT_VERSION, GovernedAssetFile, GovernedAssetKind, GovernedImportAsset,
        GovernedImportCatalog, MAX_IMPORT_ASSETS, RedistributionRights,
    },
    media_validation::{MediaFormat, MediaLimits, validate_declared_media},
    source_graph_glb::SourceGraphGlbReadBudget,
};
use forge_core::{AssetId, ContentHash, bytes_sha256};
use std::collections::{BTreeMap, BTreeSet};

const MAX_REPORT_BYTES: usize = 1024 * 1024;
const MAX_MODEL_BYTES: usize = 64 * 1024 * 1024;

/// Only unknown identities stay unresolved. Invalid manifests, changed known pins
/// and invalid payloads are errors, never reasons to fall back to another store.
pub fn export_available_models(
    pins: &[(AssetId, ContentHash)],
    captured_on: &str,
    maximum_bytes: u64,
    mut read: impl FnMut(&str, usize) -> Result<Option<Vec<u8>>, String>,
) -> Result<AvailableExportSnapshot, String> {
    if pins.len() > MAX_IMPORT_ASSETS || maximum_bytes > MAX_EXPORT_BYTES {
        return Err("Sélection ou budget du kit livré invalide.".into());
    }
    if pins.iter().map(|(id, _)| id).collect::<BTreeSet<_>>().len() != pins.len() {
        return Err("Identité répétée dans la sélection du kit livré.".into());
    }
    let mut result = AvailableExportSnapshot {
        snapshot: ImportedExportSnapshot {
            catalog: GovernedImportCatalog {
                schema: GOVERNED_IMPORT_SCHEMA.into(),
                schema_version: GOVERNED_IMPORT_VERSION,
                assets: Vec::new(),
            },
            files: BTreeMap::new(),
        },
        unresolved: Vec::new(),
    };
    if pins.is_empty() {
        return Ok(result);
    }
    let Some(report_bytes) = read("forge-kit/build.json", MAX_REPORT_BYTES)? else {
        result.unresolved = pins.to_vec();
        return Ok(result);
    };
    if report_bytes.len() > MAX_REPORT_BYTES {
        return Err("Manifeste du kit livré trop volumineux.".into());
    }
    let metadata: serde_json::Value =
        serde_json::from_slice(&report_bytes).map_err(|e| e.to_string())?;
    let author = metadata
        .pointer("/provenance/author")
        .and_then(serde_json::Value::as_str)
        .filter(|value| !value.trim().is_empty())
        .unwrap_or("Auteur non renseigné par le manifeste du kit")
        .to_owned();
    let report: GovernedAssetKitBuildReport =
        serde_json::from_value(metadata).map_err(|e| e.to_string())?;
    let browser = AssetBrowser::new(report).map_err(|e| e.to_string())?;
    let proof = serde_json::to_vec_pretty(&serde_json::json!({
        "schema":"forge.private-deployed-kit-proof.v1",
        "manifest_sha256":bytes_sha256(&report_bytes),
        "manifest_utf8":std::str::from_utf8(&report_bytes).map_err(|e| e.to_string())?,
        "package_copy_acquired_on":captured_on,
        "date_semantics":"Capture into this private package, not original acquisition.",
        "rights_semantics":"Declared generated-kit provenance retained; no ownership or redistribution permission inferred."
    })).map_err(|e| e.to_string())?;
    let evidence = format!("licenses/{}.txt", bytes_sha256(&proof));
    let mut remaining = maximum_bytes;
    for (id, pin) in pins {
        let asset = match browser.resolve(*id, *pin) {
            Ok(asset) => asset,
            Err(AssetResolutionError::Missing(_)) => {
                result.unresolved.push((*id, *pin));
                continue;
            }
            Err(error) => return Err(error.to_string()),
        };
        let payload = capture_payload(
            &mut result.snapshot.files,
            &asset.glb.path,
            *pin,
            &mut remaining,
            &mut read,
        )?;
        insert(
            &mut result.snapshot.files,
            evidence.clone(),
            proof.clone(),
            &mut remaining,
        )?;
        result.snapshot.catalog.assets.push(entry(
            *id,
            &asset.id,
            payload,
            browser.report(),
            &evidence,
            captured_on,
            &author,
        ));
    }
    validate_snapshot(&mut result.snapshot)?;
    Ok(result)
}

fn validate_snapshot(snapshot: &mut ImportedExportSnapshot) -> Result<(), String> {
    if !snapshot.catalog.assets.is_empty() {
        snapshot.catalog.assets.sort_by_key(|asset| asset.id);
        snapshot
            .catalog
            .validate_for_local_use()
            .map_err(|e| e.to_string())?;
        snapshot
            .catalog
            .verify_payloads(|file| {
                snapshot
                    .files
                    .get(&file.path)
                    .cloned()
                    .ok_or_else(|| "Ressource du snapshot absente".into())
            })
            .map_err(|e| e.to_string())?;
    }
    Ok(())
}

fn capture_payload(
    files: &mut BTreeMap<String, Vec<u8>>,
    relative: &str,
    pin: ContentHash,
    remaining: &mut u64,
    read: &mut impl FnMut(&str, usize) -> Result<Option<Vec<u8>>, String>,
) -> Result<GovernedAssetFile, String> {
    let path = format!("assets/{pin}.glb");
    let size = if let Some(existing) = files.get(&path) {
        existing.len()
    } else {
        // Path and identity come only from the validated deployed manifest.
        let bytes = read(&format!("forge-kit/{relative}"), MAX_MODEL_BYTES)?
            .ok_or("Modèle du kit livré absent des ressources du Studio")?;
        if bytes.len() > MAX_MODEL_BYTES || bytes_sha256(&bytes) != pin {
            return Err("Modèle du kit livré différent de son empreinte épinglée.".into());
        }
        validate_declared_media(
            &bytes,
            MediaFormat::Glb,
            MediaLimits::default(),
            SourceGraphGlbReadBudget::Standard,
        )
        .map_err(|e| e.to_string())?;
        let size = bytes.len();
        insert(files, path.clone(), bytes, remaining)?;
        size
    };
    Ok(GovernedAssetFile {
        path,
        sha256: pin,
        bytes: size as u64,
    })
}

fn entry(
    id: AssetId,
    name: &str,
    payload: GovernedAssetFile,
    report: &GovernedAssetKitBuildReport,
    evidence: &str,
    captured_on: &str,
    author: &str,
) -> GovernedImportAsset {
    GovernedImportAsset {
        id,
        name: name.into(),
        kind: GovernedAssetKind::Mesh,
        durable_source: Some(DurableAssetSource::AuthoredFile {
            file: payload.clone(),
        }),
        payload,
        provenance: AssetProvenance {
            source: format!(
                "Kit livré {} révision {}",
                report.asset_kit_id, report.asset_kit_revision
            ),
            author: author.into(),
            license: report.provenance.license.clone(),
            evidence: evidence.into(),
            acquired_on: captured_on.into(),
            rights: AssetRights {
                local_use: true,
                redistribution: RedistributionRights::Forbidden,
                redistribution_evidence: None,
            },
        },
        derived_from: None,
        transformations: Vec::new(),
        authored_changes: false,
        thumbnail: None,
    }
}

fn insert(
    files: &mut BTreeMap<String, Vec<u8>>,
    path: String,
    bytes: Vec<u8>,
    remaining: &mut u64,
) -> Result<(), String> {
    if let Some(previous) = files.get(&path) {
        return if previous == &bytes {
            Ok(())
        } else {
            Err("Collision de ressources du kit livré.".into())
        };
    }
    *remaining = remaining
        .checked_sub(bytes.len() as u64)
        .ok_or("Budget du kit livré dépassé")?;
    files.insert(path, bytes);
    Ok(())
}

#[cfg(test)]
mod tests;
