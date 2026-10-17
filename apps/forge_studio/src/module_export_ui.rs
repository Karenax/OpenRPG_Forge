//! Local package export of the currently reviewed RPG map.
use crate::module_export_protocol::{ModuleExportRequest, ModuleExportResult};
use forge_project::authoring::AuthoringState;
use leptos::prelude::*;
use wasm_bindgen::JsValue;

#[component]
pub(crate) fn ModuleExportControls(
    state: RwSignal<Option<AuthoringState>>,
    busy: RwSignal<bool>,
) -> impl IntoView {
    let name = RwSignal::new(String::new());
    let version = RwSignal::new("1.0.0".to_owned());
    let publisher = RwSignal::new(String::new());
    let license = RwSignal::new(String::new());
    let publish = RwSignal::new(false);
    let exporting = RwSignal::new(false);
    let status = RwSignal::new(String::new());
    let last = RwSignal::new(None::<ModuleExportResult>);
    let disabled = move || {
        crate::authoring_native_game::paused()
            || busy.get()
            || exporting.get()
            || state.get().is_none_or(|s| {
                s.playtest_hash.is_some()
                    || s.zone_head.is_none_or(|h| h.corrections.gameplay.is_none())
            })
    };
    view! {
        <details class="gameplay-module-export"><summary>"Exporter cette carte en module"</summary>
            <p>"Le package contient cette carte RPG, ses modèles, images et sons importés, leurs sources, recettes et licences. Il se charge dans le jeu séparé."</p>
            <label>"Nom du module"<input prop:value=move||name.get() on:input=move|e|name.set(event_target_value(&e))/></label>
            <label>"Version"<input prop:value=move||version.get() on:input=move|e|version.set(event_target_value(&e))/></label>
            <label>"Auteur / éditeur"<input prop:value=move||publisher.get() on:input=move|e|publisher.set(event_target_value(&e))/></label>
            <label>"Conditions du module et de vos retouches"<textarea rows="4" prop:value=move||license.get() on:input=move|e|license.set(event_target_value(&e))/></label>
            <p>"Les droits du contenu importé restent applicables et ses preuves sont conservées dans l’export."</p>
            <label><input type="checkbox" prop:checked=move||publish.get() on:change=move|e|publish.set(event_target_checked(&e))/>"Préparer pour redistribution : vérifier les droits du module et de chaque asset"</label>
            <button type="button" disabled=disabled on:click=move |_| {
                let request = (|| {
                    let head = state.get_untracked().and_then(|s|s.zone_head).ok_or("Ouvrir une carte RPG")?;
                    Ok::<_,String>(ModuleExportRequest {schema_version:1, expected_head:head.content_hash()?,
                        name:name.get_untracked(),version:version.get_untracked(),publisher:publisher.get_untracked(),
                        license_text:license.get_untracked(),redistributable:publish.get_untracked()})
                })();
                let request = match request {Ok(r)=>r,Err(e)=>{status.set(e);return;}};
                exporting.set(true);
                last.set(None);
                status.set("Vérification de la carte, des médias et des droits…".into());
                wasm_bindgen_futures::spawn_local(async move {
                    let result = export(&request).await;
                    let _ = exporting.try_set(false);
                    if let Ok(ref receipt) = result { let _ = last.try_set(Some(receipt.clone())); }
                    let message = result.map_or_else(|e|format!("Export refusé : {e}"),|r|
                        format!("Module créé : {} · SHA-256 {}. {}",r.package_path,r.package_sha256,
                            if r.redistributable {"Droits de redistribution déclarés et contrôlés."}
                            else {"Usage local ; redistribution non validée."}));
                    let _ = status.try_set(message);
                });
            }>"Créer le fichier .orpmod"</button>
            <button type="button" disabled=move ||disabled() || last.get().is_none_or(|r|
                state.get().and_then(|s|s.zone_head).and_then(|h|h.content_hash().ok())!=Some(r.source_hash))
                on:click=move |_| {
                    let Some(receipt) = last.get_untracked() else {return;};
                    crate::authoring_native_game::exported_game_starting();
                    exporting.set(true);
                    status.set("Préparation du module exporté pour le jeu…".into());
                    wasm_bindgen_futures::spawn_local(async move {
                        let result = launch(&receipt).await;
                        if result.is_err() {crate::authoring_native_game::exported_game_refused().await;}
                        let _ = exporting.try_set(false);
                        let message = result.map_or_else(|error|error,|()| {
                            crate::authoring_native_game::exported_game_opened();
                            "Le module est ouvert dans le jeu séparé. Fermer le jeu pour reprendre l’édition.".into()
                        });
                        let _ = status.try_set(message);
                    });
                }>"Jouer cet export"</button>
            <output aria-live="polite">{move||status.get()}</output>
        </details>
    }
}

async fn launch(receipt: &ModuleExportResult) -> Result<(), String> {
    let arguments = js_sys::Object::new();
    for (key, value) in [
        ("expectedHash", receipt.source_hash.to_string()),
        ("packageSha256", receipt.package_sha256.to_string()),
    ] {
        js_sys::Reflect::set(
            &arguments,
            &JsValue::from_str(key),
            &JsValue::from_str(&value),
        )
        .map_err(|_| "Préparation du lancement impossible".to_owned())?;
    }
    crate::web::invoke_tauri_raw("launch_exported_module", arguments.as_ref())
        .await
        .map(|_| ())
}

async fn export(request: &ModuleExportRequest) -> Result<ModuleExportResult, String> {
    let arguments = js_sys::Object::new();
    js_sys::Reflect::set(
        &arguments,
        &JsValue::from_str("request"),
        &JsValue::from_str(&serde_json::to_string(request).map_err(|e| e.to_string())?),
    )
    .map_err(|_| "Préparation de la requête d’export impossible".to_owned())?;
    let value = crate::web::invoke_tauri_raw("export_module", arguments.as_ref()).await?;
    serde_wasm_bindgen::from_value(value).map_err(|e| e.to_string())
}
