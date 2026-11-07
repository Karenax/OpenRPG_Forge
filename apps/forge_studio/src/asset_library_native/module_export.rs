//! Capture exact Vault model pins into a private package, without adopting imports.
use super::{
    Library, LibraryModel, LibraryQuery, LibraryRequest, StoredAsset, required, vault_asset_id,
};
use crate::asset_import_native::{ImportedExportSnapshot, MAX_EXPORT_BYTES};
use forge_assets::{
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

impl Library {
    /// The date records admission of this copy to the local package, not a purchase.
    /// This output deliberately carries no permission for public redistribution.
    pub fn export_private_models(
        &mut self,
        pins: &[(AssetId, ContentHash)],
        captured_on: &str,
        maximum_bytes: u64,
    ) -> Result<ImportedExportSnapshot, String> {
        if pins.is_empty() || pins.len() > MAX_IMPORT_ASSETS || maximum_bytes > MAX_EXPORT_BYTES {
            return Err("Sélection ou budget d’export du Vault invalide.".into());
        }
        let wanted: BTreeSet<_> = pins.iter().map(|(id, _)| *id).collect();
        if wanted.len() != pins.len() {
            return Err("Identité de modèle répétée dans l’export.".into());
        }
        // Refresh and verify the existing bounded registry; never accept a caller path.
        self.execute(LibraryRequest::Search {
            query: LibraryQuery {
                limit: 1,
                ..LibraryQuery::default()
            },
        })?;
        let snapshot = self
            .snapshot
            .as_ref()
            .ok_or("Catalogue du Vault indisponible")?;
        let mut selected = BTreeMap::new();
        for asset in &snapshot.assets {
            let id = vault_asset_id(&asset.card.id);
            if wanted.contains(&id) && selected.insert(id, asset).is_some() {
                return Err("Identité de modèle ambiguë dans le Vault.".into());
            }
        }
        let mut files = BTreeMap::new();
        let mut remaining = maximum_bytes;
        let mut assets = Vec::new();
        for (id, pin) in pins {
            let current = selected
                .get(id)
                .ok_or("Modèle épinglé absent du Vault et des imports conservés")?;
            let asset = if ContentHash::parse(required(&current.details, "model_sha256")?)
                .map_err(|e| e.to_string())?
                == *pin
            {
                (*current).clone()
            } else {
                self.read_historical_asset(snapshot, current, *pin, None)?
            };
            let payload = capture_payload(self, &asset, *id, *pin, &mut files, &mut remaining)?;
            let (entry, proof) =
                capture_metadata(&asset, payload.clone(), &snapshot.revision, captured_on)?;
            insert_file(
                &mut files,
                entry.provenance.evidence.clone(),
                proof,
                &mut remaining,
            )?;
            assets.push(entry);
        }
        self.verify_registry_pins(
            &snapshot.registry_raw,
            snapshot.source_support_registry_raw.as_deref(),
        )?;
        let catalog = GovernedImportCatalog {
            schema: GOVERNED_IMPORT_SCHEMA.into(),
            schema_version: GOVERNED_IMPORT_VERSION,
            assets,
        };
        catalog
            .validate_for_local_use()
            .map_err(|e| e.to_string())?;
        catalog
            .verify_payloads(|pin| {
                files
                    .get(&pin.path)
                    .cloned()
                    .ok_or_else(|| "Fichier épinglé absent du snapshot".into())
            })
            .map_err(|e| e.to_string())?;
        Ok(ImportedExportSnapshot { catalog, files })
    }
}

fn capture_payload(
    library: &Library,
    asset: &StoredAsset,
    id: AssetId,
    pin: ContentHash,
    files: &mut BTreeMap<String, Vec<u8>>,
    remaining: &mut u64,
) -> Result<GovernedAssetFile, String> {
    let access = library.resolve_access(asset, true)?;
    let model_path = access
        .model
        .ok_or("La source du Vault n’est pas un modèle converti")?;
    let size =
        forge_delivery::vault_storage::logical_size(&library.root, &model_path, super::MAX_MODEL)?;
    let payload = GovernedAssetFile {
        path: format!("assets/{pin}.glb"),
        sha256: pin,
        bytes: size,
    };
    if let Some(bytes) = files.get(&payload.path) {
        if bytes.len() as u64 != size {
            return Err("Taille du modèle différente du contenu épinglé réutilisé.".into());
        }
    } else {
        if size > *remaining {
            return Err("L’export du Vault dépasse son budget de fichiers.".into());
        }
        let model = library.read_model(asset)?;
        if model.asset_id != id || model.sha256 != pin || model.bytes.len() as u64 != size {
            return Err("Le modèle ne correspond plus au pin demandé.".into());
        }
        validate_declared_media(
            &model.bytes,
            MediaFormat::Glb,
            MediaLimits::default(),
            SourceGraphGlbReadBudget::Standard,
        )
        .map_err(|e| e.to_string())?;
        insert_file(files, payload.path.clone(), model.bytes, remaining)?;
    }
    Ok(payload)
}

fn capture_metadata(
    asset: &StoredAsset,
    payload: GovernedAssetFile,
    revision: &str,
    captured_on: &str,
) -> Result<(GovernedImportAsset, Vec<u8>), String> {
    if asset.card.availability != "converted_model"
        || !asset.card.metadata_ready
        || asset.license.trim().is_empty()
    {
        return Err("Compléter la fiche et la licence du modèle avant son export local.".into());
    }
    if [&*asset.details, &*asset.catalog_asset]
        .iter()
        .any(|metadata| {
            [
                "/local_use",
                "/rights/local_use",
                "/provenance/rights/local_use",
            ]
            .iter()
            .any(|path| metadata.pointer(path).and_then(serde_json::Value::as_bool) == Some(false))
        })
    {
        return Err("La fiche du Vault refuse l’usage local de ce modèle.".into());
    }
    let author = asset
        .details
        .get("author")
        .and_then(serde_json::Value::as_str)
        .filter(|value| !value.trim().is_empty())
        .unwrap_or("Auteur non renseigné par le catalogue");
    let proof = serde_json::to_vec_pretty(&serde_json::json!({
        "schema":"forge.private-vault-package-proof.v1", "library_id":asset.card.id,
        "library_revision":revision, "model_sha256":payload.sha256,
        "license_declared":asset.license, "author_declared":asset.details.get("author"),
        "package_copy_acquired_on":captured_on,
        "original_acquisition_date":asset.details.get("acquired_on").or_else(||asset.details.pointer("/provenance/acquired_on")),
        "date_semantics":"Admission of this exact copy into the private package; not an original purchase date.",
        "rights_semantics":"Local authoring use only. No ownership or redistribution permission established by this capture.",
        "metadata":asset.details.as_ref(), "source_catalog_entry":asset.catalog_asset.as_ref(),
        "functional_profile":asset.profile
    })).map_err(|e| e.to_string())?;
    if proof.len() > 65536 || proof.windows(3).any(|bytes| bytes == [0xef, 0xbf, 0xbd]) {
        return Err(
            "La preuve du Vault dépasse 64 Kio ; compléter un import gouverné explicite.".into(),
        );
    }
    let evidence = format!("licenses/{}.txt", bytes_sha256(&proof));
    Ok((
        GovernedImportAsset {
            id: vault_asset_id(&asset.card.id),
            name: asset.card.name.clone(),
            kind: GovernedAssetKind::Mesh,
            durable_source: Some(DurableAssetSource::AuthoredFile {
                file: payload.clone(),
            }),
            payload,
            provenance: AssetProvenance {
                source: format!("Asset Vault local : {}", asset.card.id),
                author: author.into(),
                license: asset.license.clone(),
                evidence,
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
        },
        proof,
    ))
}

/// Preserve the exact selected metadata; this does not infer redistribution rights.
pub(super) fn companion_evidence(
    asset: &StoredAsset,
    model: &LibraryModel,
    revision: &str,
    captured_on: &str,
    kind: forge_project::module_package::ModulePlacementCompanionKind,
    manifest: ContentHash,
    package: ContentHash,
) -> Result<Vec<u8>, String> {
    use forge_project::module_package::ModulePlacementCompanionKind;

    let profile = match kind {
        ModulePlacementCompanionKind::Physics => "source_physics_package",
        ModulePlacementCompanionKind::Particles => "source_particle_package",
    };
    let declaration = &asset.profile[profile];
    for (name, expected) in [
        ("carrier_sha256", model.sha256),
        ("manifest_sha256", manifest),
        ("sha256", package),
    ] {
        if ContentHash::parse(required(declaration, name)?).map_err(|error| error.to_string())?
            != expected
        {
            return Err("Fiche du compagnon différente des empreintes sauvegardées".into());
        }
    }
    let payload = GovernedAssetFile {
        path: format!("assets/{}.glb", model.sha256),
        sha256: model.sha256,
        bytes: model.bytes.len() as u64,
    };
    let (_, model_evidence) = capture_metadata(asset, payload, revision, captured_on)?;
    let metadata: serde_json::Value =
        serde_json::from_slice(&model_evidence).map_err(|error| error.to_string())?;
    let evidence = serde_json::to_vec(&serde_json::json!({
        "schema":"forge.private-vault-companion-proof.v1",
        "kind":kind,
        "asset_id":model.asset_id,
        "carrier_sha256":model.sha256,
        "manifest_sha256":manifest,
        "package_sha256":package,
        "registry_snapshot_revision":revision,
        "selected_details_sha256":forge_core::canonical_json_sha256(asset.details.as_ref()).map_err(|error| error.to_string())?,
        "selected_profile_sha256":forge_core::canonical_json_sha256(&asset.profile).map_err(|error| error.to_string())?,
        "selected_catalog_entry_sha256":forge_core::canonical_json_sha256(asset.catalog_asset.as_ref()).map_err(|error| error.to_string())?,
        "rights_semantics":"Preserved catalog declarations only; no independent redistribution permission is established.",
        "metadata":metadata
    })).map_err(|error| error.to_string())?;
    if evidence.len() > 128 * 1024 {
        return Err("La preuve du compagnon dépasse 128 Kio".into());
    }
    Ok(evidence)
}

fn insert_file(
    files: &mut BTreeMap<String, Vec<u8>>,
    path: String,
    bytes: Vec<u8>,
    remaining: &mut u64,
) -> Result<(), String> {
    if let Some(existing) = files.get(&path) {
        return if existing == &bytes {
            Ok(())
        } else {
            Err("Collision de fichiers dans l’export du Vault.".into())
        };
    }
    *remaining = remaining
        .checked_sub(bytes.len() as u64)
        .ok_or("L’export du Vault dépasse son budget de fichiers")?;
    files.insert(path, bytes);
    Ok(())
}
