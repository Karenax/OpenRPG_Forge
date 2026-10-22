//! All-area assembly uses the same governed assets, immutable archives and publisher.
use super::*;
use forge_project::workspace::export::ModuleExportSnapshot;

pub fn build_module_with_origin(
    exports_root: &Path,
    snapshot: &ModuleExportSnapshot,
    metadata: ExportMetadata,
    catalog: GovernedImportCatalog,
    payloads: BTreeMap<String, Vec<u8>>,
    origin: Option<ExportOrigin>,
) -> Result<ExportedModule> {
    if !exports_root.is_absolute() {
        return Err("The native export root must be absolute".into());
    }
    crate::manifest::safe_chain(exports_root)?;
    check_snapshot(snapshot, &catalog)?;
    let primary = snapshot
        .zones
        .iter()
        .find(|zone| zone.area_id == snapshot.primary_area)
        .ok_or("Primary area is absent from module snapshot")?;
    let mut prepared = prepare(
        &primary.bytes,
        snapshot.primary_head,
        metadata,
        catalog,
        payloads,
        origin,
    )?;
    prepared.module.creator = snapshot.creator.clone();
    prepared.module.version = if snapshot.creator.is_some() {
        MODULE_PACKAGE_CREATOR_VERSION
    } else {
        MODULE_PACKAGE_VERSION
    };
    for source in snapshot
        .zones
        .iter()
        .filter(|zone| zone.area_id != snapshot.primary_area)
    {
        append_area(&mut prepared, source)?;
    }
    prepared.module.provenance.source =
        "All canonical areas of the reviewed Studio module and selected governed assets".into();
    prepared.module.provenance.source_revision = format!(
        "studio/module/{}",
        snapshot.workspace_hash.unwrap_or(snapshot.primary_head)
    );
    update_proof(&mut prepared, snapshot)?;
    for source in &snapshot.zones {
        prepared
            .module
            .validate_zone_archive(source.area_id, &source.bytes)
            .map_err(|error| error.to_string())?;
    }
    prepared.payloads.insert(
        MODULE_MANIFEST_PATH.into(),
        prepared
            .module
            .to_bytes()
            .map_err(|error| error.to_string())?,
    );
    let mut exported = publish_prepared(exports_root, prepared, snapshot.primary_head)?;
    exported.module_hash = snapshot
        .workspace_hash
        .map(|_| snapshot.content_hash())
        .transpose()?;
    Ok(exported)
}

fn check_snapshot(snapshot: &ModuleExportSnapshot, catalog: &GovernedImportCatalog) -> Result<()> {
    if snapshot.zones.is_empty() || snapshot.zones.len() > MAX_MODULE_ZONES {
        return Err("Module export area budget exceeded".into());
    }
    let mut seen = std::collections::BTreeSet::new();
    let mut remaining = 256 * 1024 * 1024;
    for source in &snapshot.zones {
        if !seen.insert(source.area_id) || source.bytes.len() > remaining {
            return Err("Duplicate area or module export byte budget exceeded".into());
        }
        remaining -= source.bytes.len();
        let zone = ZoneEditSession::from_archive_bytes(&source.bytes)?;
        let head = zone.head();
        if head.base.terrain.project_id != snapshot.project_id
            || head.base.terrain.area_id != source.area_id
            || head.content_hash()? != source.head_hash
        {
            return Err("Area identity or revision differs from module snapshot".into());
        }
        if source.area_id == snapshot.primary_area && source.head_hash != snapshot.primary_head {
            return Err("Primary area revision differs".into());
        }
        if !zone.required_source_blobs()?.is_empty() {
            return Err("Module export does not yet transport raster or imported-instance source blobs; no area was discarded".into());
        }
        if let Some(document) = head.corrections.gameplay.as_deref() {
            if document.module_id != snapshot.module_id {
                return Err("Area gameplay belongs to another module".into());
            }
            if let Some(definition) = &snapshot.creator {
                definition
                    .validate_document(document)
                    .map_err(|error| error.to_string())?;
            }
        }
        validate_models(head, catalog)?;
    }
    if let Some(definition) = &snapshot.creator {
        if definition.module_id != snapshot.module_id {
            return Err("Common profile belongs to another module".into());
        }
        definition.to_bytes().map_err(|error| error.to_string())?;
    }
    Ok(())
}

fn append_area(
    prepared: &mut Prepared,
    source: &forge_project::workspace::export::ModuleAreaArchive,
) -> Result<()> {
    let zone = ZoneEditSession::from_archive_bytes(&source.bytes)?;
    let path = format!("_forge-export/zones/{}.json", source.area_id);
    if prepared.payloads.contains_key(&path) {
        return Err("Generated module area path conflicts with an asset payload".into());
    }
    let hash = bytes_sha256(&source.bytes);
    prepared.module.files.push(ModuleFile {
        path: path.clone(),
        bytes: source.bytes.len() as u64,
        sha256: hash,
        role: ModuleFileRole::ZoneArchive,
    });
    prepared.module.zones.push(ModuleZone {
        area_id: source.area_id,
        archive_path: path.clone(),
        archive_sha256: hash,
        head_hash: source.head_hash,
        gameplay_sha256: zone
            .head()
            .corrections
            .gameplay
            .as_deref()
            .map(forge_core::canonical_json_sha256)
            .transpose()
            .map_err(|error| error.to_string())?,
        source_blobs: vec![],
    });
    prepared.module.gameplay_visuals.extend(
        zone.head()
            .corrections
            .gameplay_visuals
            .clone()
            .unwrap_or_default(),
    );
    prepared.payloads.insert(path, source.bytes.clone());
    Ok(())
}

fn update_proof(prepared: &mut Prepared, snapshot: &ModuleExportSnapshot) -> Result<()> {
    let mut proof: serde_json::Value =
        serde_json::from_slice(&prepared.payloads[SOURCE]).map_err(|error| error.to_string())?;
    proof["version"] = 2.into();
    proof["scope"] = "all reviewed module areas and their common Creator definition".into();
    proof["workspace_sha256"] =
        serde_json::to_value(snapshot.workspace_hash).map_err(|error| error.to_string())?;
    proof["areas"] = serde_json::Value::Array(snapshot.zones.iter().map(|zone|serde_json::json!({"area_id":zone.area_id,"head_sha256":zone.head_hash,"archive_sha256":bytes_sha256(&zone.bytes)})).collect());
    let bytes = crate::json(&proof)?;
    let file = prepared
        .module
        .files
        .iter_mut()
        .find(|file| file.path == SOURCE)
        .ok_or("Generated source proof is absent")?;
    file.bytes = bytes.len() as u64;
    file.sha256 = bytes_sha256(&bytes);
    prepared.payloads.insert(SOURCE.into(), bytes);
    Ok(())
}
