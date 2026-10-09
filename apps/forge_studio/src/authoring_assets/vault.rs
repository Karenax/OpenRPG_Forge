//! Private Vault bytes enter the same GLB admission and projection as kit assets.
use crate::asset_library_protocol::{LibraryModel, LibraryRequest, vault_asset_id};
use forge_core::{AssetId, ContentHash};
use leptos::prelude::*;
use wasm_bindgen::{JsCast, JsValue};

thread_local! {
    static MODEL_READ_GATE: super::model_read_gate::ModelReadGate = super::model_read_gate::ModelReadGate::default();
}

async fn read_model(request: LibraryRequest) -> Result<LibraryModel, String> {
    // Wait before invoking IPC, so pending requests hold only small metadata.
    // Keep admission through original SHA verification; cancellation releases it.
    let _permit = MODEL_READ_GATE.with(|gate| gate.acquire())?.await;
    let arguments = js_sys::Object::new();
    let text = serde_json::to_string(&request).map_err(|e| e.to_string())?;
    js_sys::Reflect::set(
        &arguments,
        &JsValue::from_str("request"),
        &JsValue::from_str(&text),
    )
    .map_err(|_| "Requête de modèle illisible".to_owned())?;
    let response = crate::web::invoke_tauri_raw("read_asset_model", arguments.as_ref()).await?;
    let bytes = if response.is_instance_of::<js_sys::ArrayBuffer>() {
        js_sys::Uint8Array::new(&response)
    } else if response.is_instance_of::<js_sys::Uint8Array>() {
        response.unchecked_into::<js_sys::Uint8Array>()
    } else {
        return Err("Réponse binaire de modèle attendue".into());
    };
    if bytes.length() as usize > crate::asset_library_model_wire::MAX_MODEL_BYTES + 4108 {
        return Err("Transfert de modèle trop volumineux".into());
    }
    let model = crate::asset_library_model_wire::decode(bytes.to_vec())?;
    if model.asset_id != vault_asset_id(&model.id) {
        return Err("Identité du modèle différente du Vault".into());
    }
    super::verify::check_bytes(&model.bytes, model.sha256)?;
    Ok(model)
}

pub(crate) async fn selected(id: String, revision: String) -> Result<LibraryModel, String> {
    let model = read_model(LibraryRequest::Model {
        id: id.clone(),
        revision,
    })
    .await?;
    if model.id != id {
        return Err("Sélection différente de la réponse".into());
    }
    Ok(model)
}

pub(crate) async fn reference(
    asset_id: AssetId,
    sha256: ContentHash,
) -> Result<LibraryModel, String> {
    let model = read_model(LibraryRequest::ModelReference { asset_id, sha256 }).await?;
    if model.asset_id != asset_id || model.sha256 != sha256 {
        return Err("Référence sauvegardée différente de la réponse".into());
    }
    Ok(model)
}

#[component]
pub(crate) fn VaultActions(
    card: crate::asset_library_protocol::LibraryCard,
    revision: String,
    selected: RwSignal<String>,
    close: Callback<()>,
) -> impl IntoView {
    let bindings = use_context::<crate::atelier_ui::MainBindings>().or_else(|| {
        use_context::<crate::atelier_ui::Link>().and_then(|link| link.main.get_untracked())
    });
    let working = RwSignal::new(false);
    let status = RwSignal::new(String::new());
    let placeable = card.availability == "converted_model";
    let action = Callback::new(move |place: bool| {
        let Some(bindings) = bindings else {
            return;
        };
        if working.get_untracked() || bindings.busy.get_untracked() {
            return;
        }
        let head = match super::placement_controls::head(bindings.state.get_untracked()) {
            Ok(head) if crate::authoring_viewport::can_preview_asset() => head,
            Ok(_) => {
                status.set("Ouvrir la carte en mode édition.".into());
                return;
            }
            Err(e) => {
                status.set(e);
                return;
            }
        };
        if !placeable {
            return;
        }
        let id = card.id.clone();
        let name = card.name.clone();
        let revision = revision.clone();
        super::cancel_preview();
        let ticket = super::preview::ticket();
        working.set(true);
        status.set("Vérification du modèle…".into());
        wasm_bindgen_futures::spawn_local(async move {
            let result = self::selected(id.clone(), revision).await;
            let _ = working.try_set(false);
            if selected.try_get_untracked().as_deref() != Some(id.as_str())
                || !super::preview::is_current(ticket)
                || !crate::authoring_viewport::can_preview_asset()
                || bindings.busy.get_untracked()
            {
                return;
            }
            let model = match result {
                Ok(model) => model,
                Err(e) => {
                    let _ = status.try_set(format!("Modèle refusé : {e}"));
                    return;
                }
            };
            let current_head = super::placement_controls::head(bindings.state.get_untracked());
            if current_head
                .as_ref()
                .ok()
                .and_then(|h| h.content_hash().ok())
                != head.content_hash().ok()
            {
                let _ = status.try_set("Zone modifiée ; recommencer.".into());
                return;
            }
            if place {
                let reference = (model.asset_id, model.sha256);
                // The regular projection reads this saved reference itself;
                // never inject bytes into a second map or bypass Preview/Apply.
                crate::authoring_brush_pointer::arm(
                    2.0,
                    false,
                    Callback::new(move |points: Result<Vec<[f32; 2]>, String>| {
                        let request = (|| {
                            let current =
                                super::placement_controls::head(bindings.state.get_untracked())?;
                            if current.content_hash()? != head.content_hash()? {
                                return Err("Zone modifiée ; recommencer le placement".into());
                            }
                            let point = *points?.first().ok_or("Cliquer le terrain")?;
                            super::placement_controls::insertion::prepare(
                                &current,
                                &name,
                                reference,
                                (
                                    forge_core::Transform::IDENTITY,
                                    forge_world::placement_snap::PlacementSnap {
                                        grid_step_m: None,
                                        surface_offset_m: Some(0.0),
                                    },
                                ),
                                point,
                            )
                        })();
                        bindings.send.run(request);
                    }),
                );
                bindings
                    .status
                    .set("Cliquer le terrain pour proposer le placement ; Échap annule.".into());
                close.run(());
            } else if super::preview::submit_scene(ticket, model.id, model.bytes) {
                bindings
                    .status
                    .set("Modèle vérifié ; chargement de l’aperçu 3D…".into());
                close.run(());
            }
        });
    });
    view! {
        <div class="asset-library-actions">
            <button disabled=move ||!placeable || bindings.is_none() ||working.get() ||bindings.is_some_and(|b|b.busy.get()) on:click=move |_|action.run(false)>"Aperçu 3D"</button>
            <button disabled=move ||!placeable || bindings.is_none() ||working.get() ||bindings.is_some_and(|b|b.busy.get()) on:click=move |_|action.run(true)>"Placer sur la carte"</button>
            <p role="status">{move ||status.get()}</p>
            {bindings.is_none().then(|| view!{<p>"Ouvrez cette bibliothèque depuis une carte pour utiliser l’aperçu et le placement."</p>})}
        </div>
    }
}
