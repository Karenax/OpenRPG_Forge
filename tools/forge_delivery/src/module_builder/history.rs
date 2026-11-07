//! Bounded immutable models retained for authoring history, outside the play catalog.
use super::*;
use forge_core::AssetId;

pub struct ExportHistoricalModel {
    pub asset_id: AssetId,
    pub bytes: Vec<u8>,
    pub expected_sha256: ContentHash,
    pub license: Vec<u8>,
    pub static_triangle_meshes: bool,
}

pub(super) fn capture(
    models: Vec<ExportHistoricalModel>,
    publishing: bool,
    payloads: &mut BTreeMap<String, Vec<u8>>,
    inventory: &mut Inventory,
) -> Result<Vec<ModuleHistoricalModel>> {
    if !models.is_empty() && publishing {
        return Err("Historical model revisions may only be exported privately".into());
    }
    if models.len() > MAX_MODULE_ASSETS {
        return Err("Too many historical models".into());
    }
    let mut revisions = Vec::with_capacity(models.len());
    for model in models {
        if model.bytes.is_empty()
            || model.bytes.len() > forge_assets::governed_import::MAX_IMPORT_FILE_BYTES as usize
            || bytes_sha256(&model.bytes) != model.expected_sha256
        {
            return Err("Historical model differs from its exact asset pin or size limit".into());
        }
        if model.license.len() > 1024 * 1024
            || std::str::from_utf8(&model.license).map_or(true, |text| text.trim().is_empty())
        {
            return Err("A bounded original licence is required for each historical model".into());
        }
        let path = format!("models/history/{}.glb", model.expected_sha256);
        let license_path = format!("licenses/{}.txt", bytes_sha256(&model.license));
        insert(
            payloads,
            inventory,
            &path,
            model.bytes,
            ModuleFileRole::HistoricalModel,
        )?;
        inventory.kind(&path, GovernedAssetKind::Mesh)?;
        insert(
            payloads,
            inventory,
            &license_path,
            model.license,
            ModuleFileRole::License,
        )?;
        revisions.push(ModuleHistoricalModel {
            asset_id: model.asset_id,
            payload: ModuleFilePin {
                path,
                sha256: model.expected_sha256,
            },
            license_path,
            static_triangle_meshes: model.static_triangle_meshes,
        });
    }
    revisions.sort_by_key(|model| (model.asset_id, model.payload.sha256));
    Ok(revisions)
}

fn insert(
    payloads: &mut BTreeMap<String, Vec<u8>>,
    inventory: &mut Inventory,
    path: &str,
    bytes: Vec<u8>,
    role: ModuleFileRole,
) -> Result<()> {
    if let Some(previous) = payloads.get(path) {
        if previous != &bytes {
            return Err("Historical model payload conflicts with another source".into());
        }
    } else {
        payloads.insert(path.into(), bytes);
    }
    inventory.role(path, role)
}
