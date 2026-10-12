//! Read the placement's exact historical pins through the governed native IPC.
use crate::asset_library_protocol::LibrarySavedPhysicsRequest;
use forge_core::{AssetId, ContentHash};
use forge_project::zone_authoring::placements::PhysicsPackageReference;
use leptos::prelude::*;
#[path = "saved_physics_cache.rs"]
mod cache;
thread_local! {
    static CACHE: std::cell::RefCell<cache::Cache> = std::cell::RefCell::new(cache::Cache::new(128 * 1024 * 1024));
}

fn request(
    asset_id: AssetId,
    model_sha256: ContentHash,
    pin: &PhysicsPackageReference,
) -> Result<LibrarySavedPhysicsRequest, String> {
    if pin.schema_version != 1 || pin.carrier_sha256 != model_sha256 {
        return Err("Référence physique incompatible avec le modèle sauvegardé".into());
    }
    Ok(LibrarySavedPhysicsRequest {
        schema_version: 1,
        asset_id,
        carrier_sha256: model_sha256,
        manifest_sha256: pin.manifest_sha256,
        package_sha256: pin.package_sha256,
    })
}

/// Read historical bytes without substituting the current catalogue package.
pub(in crate::authoring_assets) async fn load_for_model(
    asset_id: AssetId,
    model_sha256: ContentHash,
    pin: &PhysicsPackageReference,
) -> Result<super::super::source_graph_physics::SourcePhysicsPayload, String> {
    load_saved(&request(asset_id, model_sha256, pin)?).await
}

/// The complete identity remains attached to the payload for its eventual host.
pub(super) async fn load_saved(
    request: &LibrarySavedPhysicsRequest,
) -> Result<super::super::source_graph_physics::SourcePhysicsPayload, String> {
    if request.schema_version != 1 {
        return Err("Version de référence physique sauvegardée inconnue".into());
    }
    let key = cache::Key {
        asset_id: request.asset_id,
        carrier: request.carrier_sha256,
        manifest: request.manifest_sha256,
        package: request.package_sha256,
    };
    let payload = |wire| super::super::source_graph_physics::SourcePhysicsPayload {
        wire,
        carrier_sha256: key.carrier,
        manifest_sha256: key.manifest,
        package_sha256: key.package,
    };
    if let Some(wire) = CACHE.with(|cache| cache.borrow_mut().get(key)) {
        return Ok(payload(wire));
    }
    let _permit = super::MODEL_READ_GATE.with(|gate| gate.acquire())?.await;
    // Another request may have populated this exact identity while queued.
    if let Some(wire) = CACHE.with(|cache| cache.borrow_mut().get(key)) {
        return Ok(payload(wire));
    }
    let wire = super::read_wire_from(
        "read_asset_physics",
        request,
        forge_assets::source_physics_package::MAX_PACKAGE_BYTES,
    )
    .await?;
    let wire = CACHE.with(|cache| cache.borrow_mut().insert(key, wire))?;
    Ok(payload(wire))
}

async fn read(request: &LibrarySavedPhysicsRequest) -> Result<usize, String> {
    let payload = load_saved(request).await?;
    let package = payload.decode_for_carrier(request.carrier_sha256)?;
    let manifest: serde_json::Value =
        serde_json::from_slice(&package.manifest).map_err(|e| e.to_string())?;
    let hash = request.carrier_sha256.to_string();
    let row = manifest["rows"]
        .as_array()
        .and_then(|rows| {
            rows.iter()
                .find(|row| row["carrier_sha256"].as_str() == Some(&hash))
        })
        .ok_or("Modèle sauvegardé absent du manifeste physique")?;
    Ok(row["components"]
        .as_array()
        .ok_or("Composants physiques absents")?
        .len())
}

/// Read-only inspection does not admit render, mount colliders or edit the head.
#[component]
pub(crate) fn SavedPhysicsDetails(
    asset_id: AssetId,
    model_sha256: ContentHash,
    pin: PhysicsPackageReference,
) -> impl IntoView {
    let working = RwSignal::new(false);
    let status = RwSignal::new(String::new());
    let summary = format!(
        "Collision sauvegardée · manifeste {} · paquet {}",
        pin.manifest_sha256, pin.package_sha256
    );
    let action = move |_| {
        if working.get_untracked() {
            return;
        }
        let request = match request(asset_id, model_sha256, &pin) {
            Ok(request) => request,
            Err(error) => {
                status.set(error);
                return;
            }
        };
        working.set(true);
        status.set("Vérification de la référence physique sauvegardée…".into());
        wasm_bindgen_futures::spawn_local(async move {
            let result = read(&request).await;
            let _ = working.try_set(false);
            let _ = status.try_set(match result {
                Ok(count) => format!("Référence vérifiée : {count} composant(s). Montage et simulation restent à qualifier."),
                Err(error) => format!("Référence physique refusée : {error}"),
            });
        });
    };
    view! {
        <details><summary>"Collision sauvegardée"</summary>
            <p>{summary}</p>
            <button disabled=move ||working.get() on:click=action>"Vérifier la référence sauvegardée"</button>
            <p role="status">{move ||status.get()}</p>
        </details>
    }
}
