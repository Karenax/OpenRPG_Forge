//! Native destinations and authoring snapshots never come from the `WebView`.
use forge_core::{AreaId, AssetId, ContentHash, bytes_sha256};
use forge_delivery::module_builder::ExportedModule;
use forge_studio_frontend::module_export_protocol::{ModuleExportRequest, ModuleExportResult};
use std::collections::{BTreeMap, BTreeSet, VecDeque};
use std::sync::{Mutex, OnceLock};

const MAX_EXPORT_RECEIPTS: usize = 16;
static EXPORT_RECEIPTS: Mutex<VecDeque<ExportedModule>> = Mutex::new(VecDeque::new());
static BUNDLED_RESOURCES: OnceLock<tauri::AppHandle> = OnceLock::new();

pub(super) fn initialize_resources(app: tauri::AppHandle) -> Result<(), String> {
    BUNDLED_RESOURCES
        .set(app)
        .map_err(|_| "Ressources natives déjà initialisées".into())
}

pub(super) fn read_bundled(path: &str, limit: usize) -> Result<Option<Vec<u8>>, String> {
    if !bundled_path_allowed(path) {
        return Err("Chemin du kit livré invalide".into());
    }
    let app = BUNDLED_RESOURCES
        .get()
        .ok_or("Ressources natives indisponibles")?;
    let Some(asset) = app.asset_resolver().get(path.into()) else {
        return Ok(None);
    };
    // The SPA fallback may return index.html for an absent kit manifest.
    if matches!(path, "forge-kit/build.json" | "forge-game-export-v1.json")
        && asset.mime_type.starts_with("text/html")
    {
        return Ok(None);
    }
    if asset.bytes.len() > limit {
        return Err("Ressource du kit livré trop volumineuse".into());
    }
    Ok(Some(asset.bytes))
}

fn bundled_path_allowed(path: &str) -> bool {
    (path.starts_with("forge-kit/") || path == "forge-game-export-v1.json")
        && !path.split('/').any(|part| {
            part.is_empty() || part == "." || part == ".." || part.contains(['\\', ':', '%'])
        })
}

pub(super) fn receipt(head: ContentHash, package: ContentHash) -> Result<ExportedModule, String> {
    let receipts = EXPORT_RECEIPTS
        .lock()
        .map_err(|_| "Reçu d’export indisponible")?;
    matching_receipt(
        receipts.iter().find(|r| r.package_sha256 == package),
        head,
        package,
    )
}

fn remember_receipt(receipts: &mut VecDeque<ExportedModule>, receipt: ExportedModule) {
    receipts.retain(|r| r.package_sha256 != receipt.package_sha256);
    receipts.push_back(receipt);
    if receipts.len() > MAX_EXPORT_RECEIPTS {
        receipts.pop_front();
    }
}

fn matching_receipt(
    receipt: Option<&ExportedModule>,
    head: ContentHash,
    package: ContentHash,
) -> Result<ExportedModule, String> {
    receipt
        .filter(|r| r.head_hash == head && r.package_sha256 == package)
        .cloned()
        .ok_or_else(|| {
            "Cet export n’est plus disponible parmi les exports vérifiés de cette session. Exporter à nouveau cette carte."
                .into()
        })
}

pub(super) fn execute(request: ModuleExportRequest) -> Result<ModuleExportResult, String> {
    if request.schema_version
        != if request.expected_workspace_hash.is_some() {
            2
        } else {
            1
        }
        || request.license_text.len() > 32 * 1024
        || request.name.len() > 256
        || request.version.len() > 128
        || request.publisher.len() > 256
    {
        return Err("Version ou taille de requête d’export invalide".into());
    }
    let root = forge_studio_frontend::launch_configuration::authoring_root()?
        .ok_or("Ouvrir un espace auteur avant l’export")?;
    super::with_authoring_host(|host| {
        let module =
            host.module_export_snapshot(request.expected_head, request.expected_workspace_hash)?;
        let origin = host.game_module_origin(request.expected_head)?;
        let exported = build_snapshot(
            &root,
            &root.join("exports"),
            &module,
            origin,
            forge_delivery::module_builder::ExportMetadata {
                name: request.name,
                version: request.version,
                publisher: request.publisher,
                license_text: request.license_text,
                rights: if request.redistributable {
                    forge_project::module_package::ModuleRights::Redistributable
                } else {
                    forge_project::module_package::ModuleRights::PrivateLocalOnly
                },
            },
        )?;
        let result = ModuleExportResult {
            module_hash: exported.module_hash,
            areas: module.zones.len(),
            module_id: exported.module_id,
            area_id: exported.area_id,
            source_hash: exported.head_hash,
            package_sha256: exported.package_sha256,
            package_path: exported
                .package_path
                .to_str()
                .ok_or("Chemin d’export non Unicode")?
                .to_owned(),
            redistributable: request.redistributable,
        };
        remember_receipt(
            &mut *EXPORT_RECEIPTS
                .lock()
                .map_err(|_| "Reçu d’export indisponible")?,
            exported,
        );
        Ok(result)
    })
}

/// Reuse the same governed assets, origin proofs and packaging for game exports.
pub(super) fn build_snapshot(
    root: &std::path::Path,
    exports_root: &std::path::Path,
    module: &forge_project::workspace::export::ModuleExportSnapshot,
    origin_pin: Option<forge_project::workspace::ModuleOriginPin>,
    metadata: forge_delivery::module_builder::ExportMetadata,
) -> Result<ExportedModule, String> {
    let requirements = required_companions(module)?;
    let publish = metadata.rights == forge_project::module_package::ModuleRights::Redistributable;
    if publish && !requirements.is_empty() {
        return Err("Les droits de redistribution des effets et composants physiques de ce module ne sont pas encore établis.".into());
    }
    let origin = origin_pin
        .map(|pin| {
            forge_studio_frontend::module_workspace_bootstrap::read_export_origin(
                root,
                module.module_id,
                pin,
            )
        })
        .transpose()?;
    let origin_documents = origin
        .as_ref()
        .map(|origin| {
            forge_delivery::orpmod::read_origin_documents(std::sync::Arc::clone(
                &origin.package_bytes,
            ))
        })
        .transpose()?;
    let (retained_assets, mut origin_assets) = origin_documents
        .as_ref()
        .map(|documents| {
            let retained = documents
                .module
                .assets
                .iter()
                .map(|asset| asset.asset_id)
                .collect::<Vec<_>>();
            let supplied =
                origin_asset_pins(&documents.module.objects, &documents.module.fish_profiles);
            (retained, supplied)
        })
        .unwrap_or_default();
    let reference = if origin.is_none() && module.workspace_hash.is_some() {
        let (scene, objects) = installed_reference(module)?;
        origin_assets.extend(origin_asset_pins(&objects, &[]));
        scene
    } else {
        None
    };
    if publish && reference.is_some() {
        return Err("Les ressources historiques installées nécessitent un export privé.".into());
    }
    let selected = select_module_models(module, &origin_assets, !publish)?;
    let mut snapshot = export_snapshot(
        &selected.catalog,
        module
            .workspace_hash
            .is_some()
            .then_some(retained_assets.as_slice()),
        publish,
        reference.as_ref(),
    )?;
    validate_companion_carriers(&requirements, &snapshot.catalog)?;
    let remaining = ensure_source_budget(module, &snapshot)?;
    let mut captured_on = None;
    let candidates = capture_companions(
        &requirements,
        origin_documents.as_ref(),
        remaining,
        |kind, pin| {
            if captured_on.is_none() {
                let utc = super::utc_now_rfc3339()?;
                captured_on = Some(utc[..10].to_owned());
            }
            let date = captured_on.as_deref().ok_or("Date d’export indisponible")?;
            capture_vault_companion(kind, pin, date)
        },
    )?;
    for (path, bytes) in candidates {
        if snapshot.files.insert(path, bytes).is_some() {
            return Err(
                "Un fichier importé entre en conflit avec les compagnons de l’export.".into(),
            );
        }
    }
    let historical_models = capture_historical_models(
        &selected.history,
        reference.as_ref(),
        origin_documents.as_ref(),
        ensure_source_budget(module, &snapshot)?,
    )?;
    use forge_delivery::module_builder::{CompanionInput, build_module_with_history};
    let input = reference
        .as_ref()
        .map_or(CompanionInput::Origin(origin), CompanionInput::Reference);
    build_module_with_history(
        exports_root,
        module,
        metadata,
        snapshot.catalog,
        snapshot.files,
        input,
        historical_models,
    )
}

fn installed_reference(
    module: &forge_project::workspace::export::ModuleExportSnapshot,
) -> Result<
    (
        Option<forge_delivery::legacy_reference::ReferenceScene>,
        Vec<forge_project::module_package::ModuleObjectBinding>,
    ),
    String,
> {
    use forge_delivery::legacy_reference::ReferenceScene;
    use forge_project::zone_authoring::session::ZoneEditSession;

    let Some(pin) = std::env::var_os("FORGE_MODULE_ASSET_REFERENCE_MANIFEST_SHA256") else {
        return Ok((None, Vec::new()));
    };
    let mut scene = None;
    let mut objects = Vec::new();
    for area in &module.zones {
        let zone = ZoneEditSession::from_archive_bytes(&area.bytes)?;
        if ReferenceScene::terrain_pin(&zone)?.is_none() {
            continue;
        }
        let reference = match &mut scene {
            Some(reference) => reference,
            slot @ None => {
                let root = std::env::var_os("FORGE_MODULE_ASSET_ROOT")
                    .ok_or("Legacy reference scene absent")?;
                let revision =
                    ContentHash::parse(pin.to_str().ok_or("Invalid reference manifest pin")?)
                        .map_err(|error| error.to_string())?;
                slot.insert(ReferenceScene::open(std::path::Path::new(&root), revision)?)
            }
        };
        if let Some((binding, _)) = reference.objects(&zone)? {
            objects.push(binding);
        }
    }
    Ok((scene, objects))
}

fn capture_historical_models(
    pins: &[(AssetId, ContentHash)],
    reference: Option<&forge_delivery::legacy_reference::ReferenceScene>,
    origin: Option<&forge_delivery::orpmod::OriginDocuments>,
    mut remaining: u64,
) -> Result<Vec<forge_delivery::module_builder::ExportHistoricalModel>, String> {
    use forge_delivery::module_builder::ExportHistoricalModel;
    use forge_studio_frontend::module_asset_native::{
        export_reference_historical_model, reference_history_from_environment,
    };
    if pins.is_empty() {
        return Ok(Vec::new());
    }
    let installed = reference
        .map(|scene| reference_history_from_environment(scene.revision()))
        .transpose()?
        .flatten();
    let mut models = Vec::with_capacity(pins.len());
    for &(asset_id, sha256) in pins {
        let limit = usize::try_from(remaining).map_err(|error| error.to_string())?;
        let from_origin = origin.and_then(|source| {
            source
                .module
                .historical_models
                .iter()
                .find(|model| model.asset_id == asset_id && model.payload.sha256 == sha256)
                .map(|model| (source, model))
        });
        let installed_model = match (reference, &installed) {
            (Some(scene), Some(history)) => {
                export_reference_historical_model(scene, history, asset_id, sha256, limit)?
            }
            _ => None,
        };
        let captured = if let Some((source, model)) = from_origin {
            ExportHistoricalModel {
                asset_id,
                expected_sha256: sha256,
                bytes: source.read_payload(&model.payload.path, limit)?,
                license: source.read_payload(&model.license_path, 1024 * 1024)?,
                static_triangle_meshes: model.static_triangle_meshes,
            }
        } else if let Some(model) = installed_model {
            model
        } else {
            let mut snapshot = export_snapshot(&[(asset_id, sha256)], Some(&[]), false, None)?;
            let asset = snapshot
                .catalog
                .assets
                .iter()
                .find(|asset| {
                    asset.id == asset_id
                        && asset.payload.sha256 == sha256
                        && asset.kind == forge_assets::governed_import::GovernedAssetKind::Mesh
                })
                .ok_or("Ancienne révision exacte de modèle introuvable.")?;
            ExportHistoricalModel {
                asset_id,
                expected_sha256: sha256,
                bytes: snapshot
                    .files
                    .remove(&asset.payload.path)
                    .ok_or("Modèle historique absent.")?,
                license: snapshot
                    .files
                    .remove(&asset.provenance.evidence)
                    .ok_or("Licence du modèle historique absente.")?,
                static_triangle_meshes: false,
            }
        };
        remaining = remaining
            .checked_sub(captured.bytes.len() as u64)
            .and_then(|remaining| remaining.checked_sub(captured.license.len() as u64))
            .ok_or("Budget des modèles d’historique dépassé.")?;
        models.push(captured);
    }
    Ok(models)
}

fn validate_companion_carriers(
    requirements: &CompanionRequirements,
    catalog: &forge_assets::governed_import::GovernedImportCatalog,
) -> Result<(), String> {
    for (_, pin) in requirements.keys() {
        if !catalog.assets.iter().any(|asset| {
            asset.id == pin.asset_id
                && asset.payload.sha256 == pin.carrier_sha256
                && asset.kind == forge_assets::governed_import::GovernedAssetKind::Mesh
        }) {
            return Err("Le porteur d’un effet ou composant physique n’est pas un modèle gouverné transportable.".into());
        }
    }
    Ok(())
}

fn capture_vault_companion(
    kind: forge_project::module_package::ModulePlacementCompanionKind,
    pin: forge_project::zone_authoring::session::SourceCompanionPin,
    date: &str,
) -> Result<(Vec<u8>, Vec<u8>), String> {
    super::with_asset_library(|library| match kind {
        forge_project::module_package::ModulePlacementCompanionKind::Physics => library
            .export_saved_physics_package(
                &forge_studio_frontend::asset_library_protocol::LibrarySavedPhysicsRequest {
                    schema_version: 1,
                    asset_id: pin.asset_id,
                    carrier_sha256: pin.carrier_sha256,
                    manifest_sha256: pin.manifest_sha256,
                    package_sha256: pin.package_sha256,
                },
                date,
            ),
        forge_project::module_package::ModulePlacementCompanionKind::Particles => library
            .export_saved_particle_package(
                &forge_studio_frontend::asset_library_protocol::LibrarySavedParticleRequest {
                    schema_version: 1,
                    asset_id: pin.asset_id,
                    carrier_sha256: pin.carrier_sha256,
                    manifest_sha256: pin.manifest_sha256,
                    package_sha256: pin.package_sha256,
                },
                date,
            ),
    })
}

fn ensure_source_budget(
    module: &forge_project::workspace::export::ModuleExportSnapshot,
    assets: &forge_studio_frontend::asset_import_native::ImportedExportSnapshot,
) -> Result<u64, String> {
    let mut remaining = remaining_bytes(assets)?;
    for bytes in module
        .source_blobs
        .values()
        .chain(module.zones.iter().map(|zone| &zone.bytes))
    {
        remaining = remaining
            .checked_sub(bytes.len() as u64)
            .ok_or("Budget d’export dépassé")?;
    }
    Ok(remaining)
}

fn export_snapshot(
    pins: &[(forge_core::AssetId, ContentHash)],
    retained: Option<&[forge_core::AssetId]>,
    publish: bool,
    reference: Option<&forge_delivery::legacy_reference::ReferenceScene>,
) -> Result<forge_studio_frontend::asset_import_native::ImportedExportSnapshot, String> {
    let mut available = super::with_asset_imports(|store| {
        store.export_available_snapshot(pins, retained, publish)
    })?;
    if available.unresolved.is_empty() {
        return Ok(available.snapshot);
    }
    if publish {
        return Err("Les modèles du kit livré ou du Vault non conservés dans les imports sont limités à l’export privé. Préparer un import gouverné avec droits de redistribution avant un export public.".into());
    }
    let utc = super::utc_now_rfc3339()?;
    let kit = forge_studio_frontend::governed_kit_export::export_available_models(
        &available.unresolved,
        &utc[..10],
        remaining_bytes(&available.snapshot)?,
        read_bundled,
    )?;
    available.unresolved = kit.unresolved;
    if !kit.snapshot.catalog.assets.is_empty() {
        available.snapshot = merge_snapshots(available.snapshot, kit.snapshot)?;
    }
    if available.unresolved.is_empty() {
        return Ok(available.snapshot);
    }
    if let Some(reference) = reference {
        let captured = forge_studio_frontend::module_asset_native::export_reference_models(
            reference,
            &available.unresolved,
            &utc[..10],
            remaining_bytes(&available.snapshot)?,
        )?;
        available.unresolved = captured.unresolved;
        available.snapshot = merge_snapshots(available.snapshot, captured.snapshot)?;
        if available.unresolved.is_empty() {
            return Ok(available.snapshot);
        }
    }
    let remaining = remaining_bytes(&available.snapshot)?;
    let vault = super::with_asset_library(|library| {
        library.export_private_models(&available.unresolved, &utc[..10], remaining)
    })?;
    merge_snapshots(available.snapshot, vault)
}

fn remaining_bytes(
    snapshot: &forge_studio_frontend::asset_import_native::ImportedExportSnapshot,
) -> Result<u64, String> {
    let used = snapshot.files.values().try_fold(0_u64, |total, bytes| {
        total
            .checked_add(bytes.len() as u64)
            .ok_or("Budget d’export dépassé")
    })?;
    forge_studio_frontend::asset_import_native::MAX_EXPORT_BYTES
        .checked_sub(used)
        .ok_or_else(|| "Budget d’export dépassé".into())
}

fn merge_snapshots(
    mut imports: forge_studio_frontend::asset_import_native::ImportedExportSnapshot,
    vault: forge_studio_frontend::asset_import_native::ImportedExportSnapshot,
) -> Result<forge_studio_frontend::asset_import_native::ImportedExportSnapshot, String> {
    use forge_studio_frontend::asset_import_native::MAX_EXPORT_BYTES;
    let mut ids: std::collections::BTreeSet<_> = imports
        .catalog
        .assets
        .iter()
        .map(|asset| asset.id)
        .collect();
    for asset in vault.catalog.assets {
        if !ids.insert(asset.id) {
            return Err("Collision d’identités entre les imports et le Vault.".into());
        }
        imports.catalog.assets.push(asset);
    }
    imports.catalog.assets.sort_by_key(|asset| asset.id);
    for (path, bytes) in vault.files {
        if let Some(previous) = imports.files.get(&path) {
            if previous != &bytes {
                return Err("Collision de fichiers entre les imports et le Vault.".into());
            }
        } else {
            imports.files.insert(path, bytes);
        }
    }
    let total = imports.files.values().try_fold(0_u64, |total, bytes| {
        total
            .checked_add(bytes.len() as u64)
            .ok_or("Budget d’export dépassé")
    })?;
    if total > MAX_EXPORT_BYTES {
        return Err("Budget d’export dépassé".into());
    }
    imports
        .catalog
        .validate_for_local_use()
        .map_err(|e| e.to_string())?;
    Ok(imports)
}

type OriginAssetPins = BTreeSet<(AreaId, AssetId, ContentHash)>;

type CompanionRequirements = BTreeMap<
    (
        forge_project::module_package::ModulePlacementCompanionKind,
        forge_project::zone_authoring::session::SourceCompanionPin,
    ),
    BTreeSet<AreaId>,
>;

/// Resolve retained origin bytes first. Only an absent origin pin permits Vault lookup.
fn capture_companions(
    requirements: &CompanionRequirements,
    origin: Option<&forge_delivery::orpmod::OriginDocuments>,
    remaining: u64,
    from_vault: impl FnMut(
        forge_project::module_package::ModulePlacementCompanionKind,
        forge_project::zone_authoring::session::SourceCompanionPin,
    ) -> Result<(Vec<u8>, Vec<u8>), String>,
) -> Result<BTreeMap<String, Vec<u8>>, String> {
    capture_companions_from(
        requirements,
        remaining,
        |kind, pin| {
            let Some(origin) = origin else {
                return Ok(None);
            };
            let Some(retained) = origin
                .module
                .placement_companions
                .iter()
                .find(|companion| companion.kind == kind && companion.pin() == pin)
            else {
                return Ok(None);
            };
            let package_limit = companion_package_limit(kind);
            Ok(Some((
                origin.read_payload(&retained.package.path, package_limit)?,
                origin.read_payload(
                    &retained.proof.path,
                    forge_delivery::module_placement_companions::MAX_COMPANION_PROOF_BYTES,
                )?,
            )))
        },
        from_vault,
    )
}

fn companion_package_limit(
    kind: forge_project::module_package::ModulePlacementCompanionKind,
) -> usize {
    use forge_project::module_package::ModulePlacementCompanionKind;
    match kind {
        ModulePlacementCompanionKind::Physics => {
            forge_assets::source_physics_package::MAX_PACKAGE_BYTES
        }
        ModulePlacementCompanionKind::Particles => {
            forge_assets::source_particle_package::MAX_PACKAGE_BYTES
        }
    }
}

fn capture_companions_from(
    requirements: &CompanionRequirements,
    mut remaining: u64,
    mut from_origin: impl FnMut(
        forge_project::module_package::ModulePlacementCompanionKind,
        forge_project::zone_authoring::session::SourceCompanionPin,
    ) -> Result<Option<(Vec<u8>, Vec<u8>)>, String>,
    mut from_vault: impl FnMut(
        forge_project::module_package::ModulePlacementCompanionKind,
        forge_project::zone_authoring::session::SourceCompanionPin,
    ) -> Result<(Vec<u8>, Vec<u8>), String>,
) -> Result<BTreeMap<String, Vec<u8>>, String> {
    let mut files = BTreeMap::new();
    let mut vault_revision = None;
    for &(kind, pin) in requirements.keys() {
        let package_limit = companion_package_limit(kind);
        let (package, proof) = if let Some(retained) = from_origin(kind, pin)? {
            retained
        } else {
            let (package, proof) = from_vault(kind, pin)?;
            if proof.len() > forge_delivery::module_placement_companions::MAX_COMPANION_PROOF_BYTES
            {
                return Err("Preuve de compagnon trop volumineuse".into());
            }
            let value: serde_json::Value = serde_json::from_slice(&proof)
                .map_err(|error| format!("Preuve de compagnon invalide : {error}"))?;
            let revision = ContentHash::parse(
                value["registry_snapshot_revision"]
                    .as_str()
                    .ok_or("Révision de la fiche du compagnon absente")?,
            )
            .map_err(|error| error.to_string())?;
            if vault_revision.is_some_and(|previous| previous != revision) {
                return Err(
                    "Le catalogue du Vault a changé pendant la capture des compagnons".into(),
                );
            }
            vault_revision = Some(revision);
            (package, proof)
        };
        if package.len() > package_limit
            || bytes_sha256(&package) != pin.package_sha256
            || proof.len() > forge_delivery::module_placement_companions::MAX_COMPANION_PROOF_BYTES
        {
            return Err("Octets ou budget d’un compagnon différents du pin sauvegardé".into());
        }
        let (package_path, proof_path) =
            forge_delivery::module_placement_companions::export_paths(kind, pin);
        for (path, bytes) in [(package_path, package), (proof_path, proof)] {
            if let Some(previous) = files.get(&path) {
                if previous != &bytes {
                    return Err("Deux révisions de compagnon ont le même chemin d’export".into());
                }
                continue;
            }
            remaining = remaining
                .checked_sub(bytes.len() as u64)
                .ok_or("Budget d’export dépassé")?;
            files.insert(path, bytes);
        }
    }
    Ok(files)
}

/// Collect exact source packages from every retained zone revision, not only the head.
fn required_companions(
    module: &forge_project::workspace::export::ModuleExportSnapshot,
) -> Result<CompanionRequirements, String> {
    use forge_project::module_package::ModulePlacementCompanionKind;

    let mut required = CompanionRequirements::new();
    let mut areas = BTreeSet::new();
    for source in &module.zones {
        if !areas.insert(source.area_id) {
            return Err("Zone répétée dans le snapshot d’export".into());
        }
        let zone = forge_project::zone_authoring::session::ZoneEditSession::from_archive_bytes(
            &source.bytes,
        )?;
        if zone.head().base.terrain.project_id != module.project_id
            || zone.head().base.terrain.area_id != source.area_id
            || zone.head().content_hash()? != source.head_hash
        {
            return Err("Zone ou révision différente du snapshot d’export".into());
        }
        let companions = zone.required_source_companions()?;
        for (kind, pins) in [
            (ModulePlacementCompanionKind::Physics, companions.physics),
            (
                ModulePlacementCompanionKind::Particles,
                companions.particles,
            ),
        ] {
            for pin in pins {
                required
                    .entry((kind, pin))
                    .or_default()
                    .insert(source.area_id);
            }
        }
    }
    Ok(required)
}

/// The admitted origin already transports its per-area object and fish assets.
fn origin_asset_pins(
    objects: &[forge_project::module_package::ModuleObjectBinding],
    fish: &[forge_project::module_package::ModuleFishBinding],
) -> OriginAssetPins {
    use forge_project::module_package::ModuleObjectSource;

    let mut supplied = OriginAssetPins::new();
    for binding in objects {
        let ModuleObjectSource::SourceSurfaces { assets, .. } = &binding.source;
        supplied.extend(
            assets
                .iter()
                .map(|asset| (binding.area_id, asset.asset_id, asset.sha256)),
        );
    }
    for binding in fish {
        supplied.extend(
            binding
                .assets
                .iter()
                .map(|asset| (binding.area_id, asset.asset_id, asset.sha256)),
        );
    }
    supplied
}

#[derive(Debug, Default)]
struct ModelSelection {
    catalog: Vec<(AssetId, ContentHash)>,
    history: Vec<(AssetId, ContentHash)>,
}

fn select_module_models(
    module: &forge_project::workspace::export::ModuleExportSnapshot,
    origin_assets: &OriginAssetPins,
    allow_history: bool,
) -> Result<ModelSelection, String> {
    let mut pins = BTreeSet::new();
    let mut active = BTreeSet::new();
    for source in &module.zones {
        let zone = forge_project::zone_authoring::session::ZoneEditSession::from_archive_bytes(
            &source.bytes,
        )?;
        for binding in zone
            .head()
            .corrections
            .gameplay_visuals
            .as_deref()
            .unwrap_or_default()
        {
            // Gameplay visuals must be in the governed catalog, even when an
            // origin also supplies an object with the same identity.
            active.insert((binding.asset.asset_id, binding.asset.sha256));
        }
        let pin_for = |placement: &forge_project::zone_authoring::placements::ManualPlacement| {
            let pin = (placement.asset_id, placement.asset_hash);
            (!origin_assets.contains(&(source.area_id, pin.0, pin.1))).then_some(pin)
        };
        zone.visit_current_model_placements(|placement| {
            if let Some(pin) = pin_for(placement) {
                active.insert(pin);
            }
            Ok(())
        })?;
        zone.visit_referenced_model_placements(|placement| {
            if let Some(pin) = pin_for(placement) {
                pins.insert(pin);
            }
            Ok(())
        })?;
    }
    let mut selected = ModelSelection::default();
    pins.extend(active.iter().copied());
    let pins = pins.into_iter().collect::<Vec<_>>();
    for revisions in pins.chunk_by(|a, b| a.0 == b.0) {
        if revisions.len() == 1 {
            selected.catalog.push(revisions[0]);
            continue;
        }
        let mut playable = revisions.iter().copied().filter(|pin| active.contains(pin));
        let chosen = playable.next();
        if !allow_history || playable.next().is_some() {
            return Err(format!(
                "One playable catalogue cannot represent two content hashes for imported asset {}",
                revisions[0].0
            ));
        }
        selected.catalog.extend(chosen);
        selected
            .history
            .extend(revisions.iter().copied().filter(|pin| Some(*pin) != chosen));
    }
    Ok(selected)
}

#[cfg(test)]
mod tests {
    use super::*;
    use forge_core::{CommandId, EntityId, ModuleId, Transform, bytes_sha256};
    use forge_project::{
        authoring::new_terrain,
        workspace::export::{ModuleAreaArchive, ModuleExportSnapshot},
        zone_authoring::{
            placements::{
                ManualPlacement, ParticlePackageReference, PhysicsPackageReference, PlacementEdit,
            },
            session::ZoneEditSession,
        },
    };

    fn snapshot_with_removed_placements(
        asset_id: AssetId,
        hashes: &[ContentHash],
    ) -> ModuleExportSnapshot {
        let terrain = new_terrain("history-project", "history-area", "Historique").unwrap();
        let mut zone = ZoneEditSession::from_initial_terrain(terrain).unwrap();
        for (index, hash) in hashes.iter().enumerate() {
            let id = EntityId::deterministic(format!("history-placement-{index}"));
            let placement = ManualPlacement {
                id,
                name: "Modèle retiré".into(),
                asset_id,
                asset_hash: *hash,
                physics: None,
                particles: None,
                local_lights: None,
                transform: Transform::IDENTITY,
                locked: false,
            };
            let insert = zone
                .head()
                .prepare_placement(
                    CommandId::deterministic(format!("history-insert-{index}")),
                    PlacementEdit::Insert { placement },
                )
                .unwrap();
            zone.execute(&insert).unwrap();
            let remove = zone
                .head()
                .prepare_placement(
                    CommandId::deterministic(format!("history-remove-{index}")),
                    PlacementEdit::Remove { id },
                )
                .unwrap();
            zone.execute(&remove).unwrap();
        }
        let project_id = zone.head().base.terrain.project_id;
        let area_id = zone.head().base.terrain.area_id;
        let head_hash = zone.head().content_hash().unwrap();
        ModuleExportSnapshot {
            module_id: ModuleId::deterministic("history-module"),
            project_id,
            primary_area: area_id,
            primary_head: head_hash,
            workspace_hash: None,
            creator: None,
            zones: vec![ModuleAreaArchive {
                area_id,
                head_hash,
                bytes: zone.archive_bytes().unwrap(),
            }],
            source_blobs: std::collections::BTreeMap::new(),
        }
    }

    #[test]
    fn module_export_selects_placement_models_retained_only_in_history() {
        let id = AssetId::deterministic("history-model");
        let hash = bytes_sha256(b"history-model-content");
        let snapshot = snapshot_with_removed_placements(id, &[hash]);
        let zone = ZoneEditSession::from_archive_bytes(&snapshot.zones[0].bytes).unwrap();
        assert!(
            zone.head()
                .corrections
                .placements
                .as_deref()
                .unwrap_or_default()
                .is_empty()
        );
        let mut found_in_history = false;
        zone.visit_referenced_placements(|placement| {
            found_in_history |= placement.asset_id == id && placement.asset_hash == hash;
            Ok(())
        })
        .unwrap();
        assert!(found_in_history);
        assert_eq!(
            select_module_models(&snapshot, &OriginAssetPins::new(), true)
                .unwrap()
                .catalog,
            vec![(id, hash)]
        );
    }

    #[test]
    fn module_export_does_not_require_origin_object_or_fish_assets_from_the_vault() {
        use forge_project::module_package::{
            ModuleFilePin, ModuleFishBinding, ModuleObjectAsset, ModuleObjectBinding,
            ModuleObjectFiles, ModuleObjectSource,
        };

        let id = AssetId::deterministic("origin-model");
        let hash = bytes_sha256(b"origin-resource");
        let snapshot = snapshot_with_removed_placements(id, &[hash]);
        let area = snapshot.primary_area;
        let manifest = ModuleFilePin {
            path: "source/manifest.json".into(),
            sha256: hash,
        };
        let resource = ModuleFilePin {
            path: "source/resource.bin".into(),
            sha256: bytes_sha256(b"origin-resource-content"),
        };
        let package = ModuleObjectFiles {
            manifest: manifest.clone(),
            resources: vec![resource],
        };
        let asset = ModuleObjectAsset {
            asset_id: id,
            sha256: hash,
        };
        let object = ModuleObjectBinding {
            area_id: area,
            source: ModuleObjectSource::SourceSurfaces {
                package: package.clone(),
                assets: vec![asset],
            },
        };
        let fish = ModuleFishBinding {
            area_id: area,
            package,
            catalog: manifest,
            excluded_source_scripts: Vec::new(),
            description: "fish.json".into(),
            prototypes: 1,
            instances: 1,
            assets: vec![asset],
        };
        for supplied in [
            origin_asset_pins(std::slice::from_ref(&object), &[]),
            origin_asset_pins(&[], std::slice::from_ref(&fish)),
        ] {
            assert!(
                select_module_models(&snapshot, &supplied, true)
                    .unwrap()
                    .catalog
                    .is_empty()
            );
        }
        let mut other_area = object;
        other_area.area_id = AreaId::deterministic("other-area");
        let supplied = origin_asset_pins(&[other_area], &[]);
        assert_eq!(
            select_module_models(&snapshot, &supplied, true)
                .unwrap()
                .catalog,
            vec![(id, hash)]
        );
    }

    #[test]
    fn private_export_separates_historical_revisions_of_one_imported_identity() {
        let id = AssetId::deterministic("history-multiple-revisions");
        let snapshot = snapshot_with_removed_placements(
            id,
            &[
                bytes_sha256(b"first revision"),
                bytes_sha256(b"second revision"),
            ],
        );
        let private = select_module_models(&snapshot, &OriginAssetPins::new(), true).unwrap();
        assert!(private.catalog.is_empty());
        assert_eq!(private.history.len(), 2);
        let error = select_module_models(&snapshot, &OriginAssetPins::new(), false).unwrap_err();
        assert!(error.contains("two content hashes"));
    }

    #[test]
    fn companion_collection_keeps_exact_pins_from_all_area_histories() {
        let id = AssetId::deterministic("history-companion-model");
        let carrier = bytes_sha256(b"history-companion-carrier");
        let mut snapshot = snapshot_with_removed_placements(id, &[carrier]);
        let mut zones = [
            ZoneEditSession::from_archive_bytes(&snapshot.zones[0].bytes).unwrap(),
            ZoneEditSession::from_initial_terrain(
                new_terrain("history-project", "another-area", "Autre carte").unwrap(),
            )
            .unwrap(),
        ];
        let physics = PhysicsPackageReference {
            schema_version: 1,
            carrier_sha256: carrier,
            manifest_sha256: bytes_sha256(b"physics-manifest"),
            package_sha256: bytes_sha256(b"physics-package"),
        };
        let particles = ParticlePackageReference {
            schema_version: 1,
            carrier_sha256: carrier,
            manifest_sha256: bytes_sha256(b"particle-manifest"),
            package_sha256: bytes_sha256(b"particle-package"),
        };
        let mut areas = BTreeSet::new();
        snapshot.zones.clear();
        for (index, zone) in zones.iter_mut().enumerate() {
            let entity = EntityId::deterministic(format!("companion-history-{index}"));
            let placement = ManualPlacement {
                id: entity,
                name: "Effets retirés".into(),
                asset_id: id,
                asset_hash: carrier,
                physics: Some(Box::new(physics.clone())),
                particles: Some(Box::new(particles.clone())),
                local_lights: None,
                transform: Transform::IDENTITY,
                locked: false,
            };
            for (action, edit) in [
                ("insert", PlacementEdit::Insert { placement }),
                ("remove", PlacementEdit::Remove { id: entity }),
            ] {
                let request = zone
                    .head()
                    .prepare_placement(
                        CommandId::deterministic(format!("{action}-companion-{index}")),
                        edit,
                    )
                    .unwrap();
                zone.execute(&request).unwrap();
            }
            let area = zone.head().base.terrain.area_id;
            areas.insert(area);
            snapshot.zones.push(ModuleAreaArchive {
                area_id: area,
                head_hash: zone.head().content_hash().unwrap(),
                bytes: zone.archive_bytes().unwrap(),
            });
        }
        snapshot.primary_head = snapshot.zones[0].head_hash;
        let required = required_companions(&snapshot).unwrap();
        assert_eq!(required.len(), 2);
        for ((kind, pin), declared_areas) in required {
            assert_eq!(declared_areas, areas);
            assert_eq!(pin.asset_id, id);
            assert_eq!(pin.carrier_sha256, carrier);
            match kind {
                forge_project::module_package::ModulePlacementCompanionKind::Physics => {
                    assert_eq!(pin.manifest_sha256, physics.manifest_sha256);
                    assert_eq!(pin.package_sha256, physics.package_sha256);
                }
                forge_project::module_package::ModulePlacementCompanionKind::Particles => {
                    assert_eq!(pin.manifest_sha256, particles.manifest_sha256);
                    assert_eq!(pin.package_sha256, particles.package_sha256);
                }
            }
        }
        snapshot.zones[1].head_hash = bytes_sha256(b"stale revision");
        assert!(required_companions(&snapshot).is_err());
    }

    #[test]
    #[allow(
        clippy::too_many_lines,
        reason = "Keep the origin, budget, pin and snapshot refusal checks together"
    )]
    fn companion_capture_is_bounded_pinned_and_rejects_a_changed_vault_snapshot() {
        use forge_project::{
            module_package::ModulePlacementCompanionKind,
            zone_authoring::session::SourceCompanionPin,
        };
        let id = AssetId::deterministic("capture-model");
        let carrier = bytes_sha256(b"carrier");
        let physics = SourceCompanionPin {
            asset_id: id,
            carrier_sha256: carrier,
            manifest_sha256: bytes_sha256(b"physics-manifest"),
            package_sha256: bytes_sha256(b"physics-wire"),
        };
        let particle = SourceCompanionPin {
            asset_id: id,
            carrier_sha256: carrier,
            manifest_sha256: bytes_sha256(b"particle-manifest"),
            package_sha256: bytes_sha256(b"particle-wire"),
        };
        let area = AreaId::deterministic("capture-area");
        let requirements = CompanionRequirements::from([
            (
                (ModulePlacementCompanionKind::Physics, physics),
                BTreeSet::from([area]),
            ),
            (
                (ModulePlacementCompanionKind::Particles, particle),
                BTreeSet::from([area]),
            ),
        ]);
        let revision = bytes_sha256(b"vault-snapshot");
        let proof = serde_json::to_vec(&serde_json::json!({"registry_snapshot_revision":revision}))
            .unwrap();
        let read = |kind, _pin| {
            Ok((
                match kind {
                    ModulePlacementCompanionKind::Physics => b"physics-wire".to_vec(),
                    ModulePlacementCompanionKind::Particles => b"particle-wire".to_vec(),
                },
                proof.clone(),
            ))
        };
        let files = capture_companions(&requirements, None, 1024, read).unwrap();
        let (path, _) = forge_delivery::module_placement_companions::export_paths(
            ModulePlacementCompanionKind::Physics,
            physics,
        );
        assert_eq!(files[&path], b"physics-wire");
        assert_eq!(files.len(), 4);
        let origin_only = CompanionRequirements::from([(
            (ModulePlacementCompanionKind::Physics, physics),
            BTreeSet::from([area]),
        )]);
        let restored = capture_companions_from(
            &origin_only,
            1024,
            |_, _| Ok(Some((b"physics-wire".to_vec(), proof.clone()))),
            |_, _| panic!("retained origin must not read the Vault"),
        )
        .unwrap();
        assert_eq!(restored[&path], b"physics-wire");
        assert!(
            capture_companions_from(
                &origin_only,
                1024,
                |_, _| Err("origin corrupted".into()),
                |_, _| panic!("corrupted origin must not fall back"),
            )
            .is_err()
        );
        assert!(
            capture_companions_from(
                &origin_only,
                1024,
                |_, _| Ok(Some((b"changed".to_vec(), proof.clone()))),
                |_, _| panic!("mismatched origin must not fall back"),
            )
            .is_err()
        );
        assert!(capture_companions(&requirements, None, 0, read).is_err());
        assert!(
            capture_companions(&requirements, None, 1024, |_, _| Ok((
                b"wrong".to_vec(),
                proof.clone()
            )))
            .is_err()
        );
        let mut count = 0;
        let changed = capture_companions(&requirements, None, 1024, |kind, _| {
            count += 1;
            let wire = match kind {
                ModulePlacementCompanionKind::Physics => b"physics-wire".to_vec(),
                ModulePlacementCompanionKind::Particles => b"particle-wire".to_vec(),
            };
            let revision = bytes_sha256(&[count]);
            Ok((
                wire,
                serde_json::to_vec(&serde_json::json!({"registry_snapshot_revision":revision}))
                    .unwrap(),
            ))
        })
        .unwrap_err();
        assert!(changed.contains("catalogue du Vault a changé"));
    }

    #[test]
    fn bundled_game_manifest_has_one_fixed_native_path() {
        assert!(bundled_path_allowed("forge-game-export-v1.json"));
        assert!(bundled_path_allowed("forge-kit/build.json"));
        for path in [
            "forge-game-export-v1.json/other",
            "forge-kit/../forge-game-export-v1.json",
            "forge-kit/%2e%2e/file",
            "forge-kit//file",
            "C:/forge-game-export-v1.json",
            "other.json",
        ] {
            assert!(
                !bundled_path_allowed(path),
                "unexpected allowed path: {path}"
            );
        }
    }

    #[test]
    fn browser_pins_select_only_the_exact_native_receipt() {
        let head = bytes_sha256(b"reviewed zone");
        let package = bytes_sha256(b"verified package");
        let value = ExportedModule {
            module_hash: None,
            module_id: ModuleId::deterministic("receipt-module"),
            area_id: AreaId::deterministic("receipt-area"),
            head_hash: head,
            package_path: std::env::temp_dir().join("native-package.orpmod"),
            source_root: std::env::temp_dir().join("native-source"),
            package_sha256: package,
            transport_manifest_sha256: bytes_sha256(b"transport").to_string(),
        };
        assert!(matching_receipt(None, head, package).is_err());
        assert!(matching_receipt(Some(&value), bytes_sha256(b"other zone"), package).is_err());
        assert!(
            matching_receipt(Some(&value), head, bytes_sha256(b"substituted package")).is_err()
        );
        assert_eq!(
            matching_receipt(Some(&value), head, package).unwrap(),
            value
        );
    }

    #[test]
    fn automatic_play_snapshot_preserves_recent_explicit_export_receipts() {
        let mut receipts = VecDeque::new();
        let head = bytes_sha256(b"reviewed zone");
        let make_receipt = |index: usize| ExportedModule {
            module_hash: None,
            module_id: ModuleId::deterministic("receipt-module"),
            area_id: AreaId::deterministic("receipt-area"),
            head_hash: head,
            package_path: std::env::temp_dir().join(format!("module-{index}.orpmod")),
            source_root: std::env::temp_dir().join("native-source"),
            package_sha256: bytes_sha256(index.to_string().as_bytes()),
            transport_manifest_sha256: bytes_sha256(b"transport").to_string(),
        };
        let explicit = make_receipt(0);
        remember_receipt(&mut receipts, explicit.clone());
        remember_receipt(&mut receipts, make_receipt(1));
        assert_eq!(
            matching_receipt(receipts.front(), head, explicit.package_sha256).unwrap(),
            explicit,
        );
        remember_receipt(&mut receipts, explicit.clone());
        assert_eq!(receipts.len(), 2);
        for index in 2..=MAX_EXPORT_RECEIPTS + 1 {
            remember_receipt(&mut receipts, make_receipt(index));
        }
        assert_eq!(receipts.len(), MAX_EXPORT_RECEIPTS);
        assert!(
            !receipts
                .iter()
                .any(|r| r.package_sha256 == explicit.package_sha256)
        );
    }
}
