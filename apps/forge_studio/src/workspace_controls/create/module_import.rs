//! Native package bytes go through the same bounded binary IPC as asset imports.
use super::Creation;
use forge_project::{
    authoring::workspace::WorkspaceHostState, workspace::MAX_MODULE_ORIGIN_PACKAGE_BYTES,
};
use leptos::{html, prelude::*};

#[component]
pub(super) fn ModuleImportControls(controls: Creation, blocked: Signal<bool>) -> impl IntoView {
    let input = NodeRef::<html::Input>::new();
    let selected = RwSignal::new(false);
    let status = RwSignal::new(String::new());
    let error = RwSignal::new(String::new());
    let import = move |_| {
        let Some(file) = input
            .get_untracked()
            .and_then(|input| input.files()?.get(0))
        else {
            return;
        };
        error.set(String::new());
        status.set(String::new());
        if !file.name().to_ascii_lowercase().ends_with(".orpmod") {
            error.set("Choisissez un fichier au format .orpmod.".into());
            return;
        }
        if file.size() == 0.0 || file.size() > MAX_MODULE_ORIGIN_PACKAGE_BYTES as f64 {
            error.set("Choisissez un fichier .orpmod de 256 Mio maximum.".into());
            return;
        }
        controls.busy.set(true);
        status.set("Vérification du module et de ses assets…".into());
        wasm_bindgen_futures::spawn_local(async move {
            let result = import_file(file).await;
            let _ = controls.busy.try_set(false);
            match result {
                Ok(state) => {
                    let _ = controls.state.try_set(Some(state));
                    if let Some(input) = input.get_untracked() {
                        input.set_value("");
                    }
                    let _ = selected.try_set(false);
                    let _ = status.try_set(
                        "Module ajouté. Sauvegardez le projet pour conserver l’import.".into(),
                    );
                }
                Err(reason) => {
                    let _ = status.try_set(String::new());
                    let _ = error.try_set(format!("Import impossible. {reason}"));
                }
            }
        });
    };
    view! {
        <details>
            <summary>"Importer un module"</summary>
            <fieldset disabled=move || blocked.get()>
                <label>"Fichier du module (.orpmod)"
                    <input type="file" accept=".orpmod" node_ref=input on:change=move |_| {
                        selected.set(input.get_untracked().is_some_and(|input| input.files().is_some_and(|files| files.length() == 1)));
                        status.set(String::new());
                        error.set(String::new());
                    }/>
                </label>
                <button type="button" disabled=move || !selected.get() on:click=import>"Importer le module"</button>
            </fieldset>
            <p role="status">{move || status.get()}</p>
            <Show when=move || !error.get().is_empty()><p role="alert">{move || error.get()}</p></Show>
        </details>
    }
}

async fn import_file(file: web_sys::File) -> Result<WorkspaceHostState, String> {
    let buffer = wasm_bindgen_futures::JsFuture::from(file.array_buffer())
        .await
        .map_err(|_| "Impossible de lire le fichier sélectionné.")?;
    let bytes = js_sys::Uint8Array::new(&buffer);
    let result = crate::web::invoke_tauri_raw("import_module_package", bytes.as_ref()).await?;
    serde_wasm_bindgen::from_value(result).map_err(|error| error.to_string())
}
