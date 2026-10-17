//! The native host resolves every visual pin before a canonical zone mutation.
use crate::asset_import_protocol::ImportedFile;
use forge_core::{AssetId, ContentHash};
use forge_project::module_gameplay_visuals::GameplayVisualBinding;
use std::collections::BTreeSet;

pub fn verify_models(
    bindings: &[GameplayVisualBinding],
    mut lookup: impl FnMut(AssetId, ContentHash) -> Result<Option<ImportedFile>, String>,
) -> Result<(), String> {
    if bindings.len() > forge_project::module_gameplay_visuals::MAX_GAMEPLAY_VISUAL_BINDINGS {
        return Err("Gameplay visual admission budget exceeded".into());
    }
    let mut checked = BTreeSet::new();
    let mut total = 0_u64;
    for binding in bindings {
        if !checked.insert(binding.asset) {
            continue;
        }
        let pin = binding.asset;
        let file = lookup(pin.asset_id, pin.sha256)?.ok_or_else(|| {
            format!(
                "Gameplay model is not in the imported library: {}",
                pin.asset_id
            )
        })?;
        total = total
            .checked_add(file.upload.byte_length)
            .ok_or("Gameplay model budget overflow")?;
        if total > 512 * 1024 * 1024 {
            return Err("Gameplay model admission exceeds 512 MiB".into());
        }
        if file.asset_id != pin.asset_id
            || file.sha256 != pin.sha256
            || file.kind != forge_assets::governed_import::GovernedAssetKind::Mesh
            || file.upload.byte_length != file.upload.bytes.len() as u64
            || forge_core::bytes_sha256(&file.upload.bytes) != pin.sha256
        {
            return Err(format!(
                "Native gameplay model differs from the requested pin: {}",
                pin.asset_id
            ));
        }
    }
    Ok(())
}
