use forge_assets::{GovernedAssetKitAsset, asset_browser::AssetBrowser};
use forge_core::{AssetId, ContentHash};
use leptos::prelude::*;
use sha2::{Digest, Sha256};

pub(super) fn start(
    browser: RwSignal<Option<AssetBrowser>>,
    selected: RwSignal<String>,
    busy: RwSignal<bool>,
    output: RwSignal<String>,
) {
    if busy.get_untracked() {
        return;
    }
    let id = selected.get_untracked();
    let Some(catalog) = browser.get_untracked() else {
        return;
    };
    let reference = match catalog.reference(&id) {
        Ok(reference) => reference,
        Err(reason) => {
            output.set(reason.to_string());
            return;
        }
    };
    busy.set(true);
    output.set("Vérification du fichier, sans chargement GPU…".into());
    wasm_bindgen_futures::spawn_local(async move {
        let result = check_reference(&catalog, reference.0, reference.1).await;
        if selected.get_untracked() == id {
            output.set(match result {
                Ok((_, bytes)) => format!("Fichier disponible : {} octets, SHA-256 conforme, précontrôle GLB autonome réussi. Ne qualifie ni géométrie complète, ni rendu, ni placement.", bytes.len()),
                Err(reason) => format!("Fichier refusé ou indisponible : {reason}"),
            });
        }
        busy.set(false);
    });
}

/// Shared route for a persisted reference and a newly selected catalog entry.
/// A changed pin fails before any file request or GPU allocation.
pub(super) async fn check_reference(
    catalog: &AssetBrowser,
    id: AssetId,
    hash: ContentHash,
) -> Result<(GovernedAssetKitAsset, Vec<u8>), String> {
    let asset = catalog.resolve(id, hash).map_err(|e| e.to_string())?;
    let bytes = check(asset).await?;
    Ok((asset.clone(), bytes))
}

pub(crate) async fn check(asset: &GovernedAssetKitAsset) -> Result<Vec<u8>, String> {
    // The browser has validated this relative path; encode each URL component.
    let path = asset
        .glb
        .path
        .split('/')
        .map(|p| String::from(js_sys::encode_uri_component(p)))
        .collect::<Vec<_>>()
        .join("/");
    let bytes = crate::governed_kit::transport::fetch(&path, 64 * 1024 * 1024).await?;
    if ContentHash::from_bytes(Sha256::digest(&bytes).into()) != asset.glb.sha256 {
        return Err("SHA-256 différent du manifeste".into());
    }
    let header: &[u8; 20] = bytes
        .get(..20)
        .and_then(|v| v.try_into().ok())
        .ok_or("GLB tronqué")?;
    let length = forge_assets::governed_glb_json_length(header, bytes.len() as u64)
        .map_err(|e| e.to_string())?;
    forge_assets::validate_governed_glb_json(&bytes[20..20 + length]).map_err(|e| e.to_string())?;
    Ok(bytes)
}
