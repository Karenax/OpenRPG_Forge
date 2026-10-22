//! Native destinations and authoring snapshots never come from the `WebView`.
use forge_core::ContentHash;
use forge_delivery::module_builder::ExportedModule;
use forge_studio_frontend::module_export_protocol::{ModuleExportRequest, ModuleExportResult};
use std::collections::VecDeque;
use std::sync::Mutex;

const MAX_EXPORT_RECEIPTS: usize = 16;
static EXPORT_RECEIPTS: Mutex<VecDeque<ExportedModule>> = Mutex::new(VecDeque::new());

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
        let module_id = module.module_id;
        let origin = host
            .game_module_origin(request.expected_head)?
            .map(|pin| {
                forge_studio_frontend::module_workspace_bootstrap::read_export_origin(
                    &root, module_id, pin,
                )
            })
            .transpose()?;
        let pins = required_module_assets(&module)?;
        let retained_assets = origin
            .as_ref()
            .map(|origin| {
                forge_delivery::orpmod::read_origin_documents(std::sync::Arc::clone(
                    &origin.package_bytes,
                ))
                .map(|documents| {
                    documents
                        .module
                        .assets
                        .into_iter()
                        .map(|asset| asset.asset_id)
                        .collect::<Vec<_>>()
                })
            })
            .transpose()?
            .unwrap_or_default();
        let snapshot = super::with_asset_imports(|store| {
            if module.workspace_hash.is_some() {
                store.export_module_snapshot(&pins, &retained_assets, request.redistributable)
            } else {
                store.export_snapshot(&pins, request.redistributable)
            }
        })?;
        let exported = forge_delivery::module_builder::build_module_with_origin(
            &root.join("exports"),
            &module,
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
            snapshot.catalog,
            snapshot.files,
            origin,
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

fn required_module_assets(
    module: &forge_project::workspace::export::ModuleExportSnapshot,
) -> Result<Vec<(forge_core::AssetId, ContentHash)>, String> {
    let mut pins = std::collections::BTreeSet::new();
    for source in &module.zones {
        let zone = forge_project::zone_authoring::session::ZoneEditSession::from_archive_bytes(
            &source.bytes,
        )?;
        pins.extend(required_assets(zone.head())?);
    }
    Ok(pins.into_iter().collect())
}

fn required_assets(
    head: &forge_project::zone_authoring::ZoneAuthoringHead,
) -> Result<Vec<(forge_core::AssetId, ContentHash)>, String> {
    let mut required = std::collections::BTreeSet::new();
    for binding in head
        .corrections
        .gameplay_visuals
        .as_deref()
        .unwrap_or_default()
    {
        required.insert((binding.asset.asset_id, binding.asset.sha256));
    }
    for placement in head.corrections.placements.as_deref().unwrap_or_default() {
        required.insert((placement.asset_id, placement.asset_hash));
    }
    for edit in head
        .corrections
        .source_object_edits
        .as_deref()
        .unwrap_or_default()
    {
        for placement in forge_project::zone_authoring::source_objects::resolve(head, &edit.source)?
        {
            required.insert((placement.asset_id, placement.asset_hash));
        }
    }
    Ok(required.into_iter().collect())
}

#[cfg(test)]
mod tests {
    use super::*;
    use forge_core::{AreaId, ModuleId, bytes_sha256};

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
