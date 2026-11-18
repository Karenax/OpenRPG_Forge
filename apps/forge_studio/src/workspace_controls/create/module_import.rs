//! Read the selected file in bounded slices before native module admission.
use super::Creation;
use crate::module_import_protocol::{
    CHUNK_BYTES, Control, OFFSET_HEADER, UPLOAD_HEADER, size_error,
};
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
            error.set(size_error());
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

#[allow(
    clippy::cast_possible_truncation,
    clippy::cast_sign_loss,
    reason = "The file picker checks the exact byte length against the bounded import size."
)]
async fn import_file(file: web_sys::File) -> Result<WorkspaceHostState, String> {
    let length = file.size() as u32;
    let upload =
        serde_wasm_bindgen::from_value(send_control(Control::Start { bytes: length }).await?)
            .map_err(|error| error.to_string())?;
    let result = upload_file(&file, length, upload).await;
    if result.is_err() {
        let _ = send_control(Control::Cancel { upload }).await;
    }
    result
}

async fn upload_file(
    file: &web_sys::File,
    length: u32,
    upload: u32,
) -> Result<WorkspaceHostState, String> {
    let token = upload.to_string();
    for offset in (0..length).step_by(CHUNK_BYTES) {
        let end = length.min(offset + CHUNK_BYTES as u32);
        let slice = file
            .slice_with_f64_and_f64(f64::from(offset), f64::from(end))
            .map_err(|_| "Impossible de lire le fichier sélectionné.")?;
        let buffer = wasm_bindgen_futures::JsFuture::from(slice.array_buffer())
            .await
            .map_err(|_| "Impossible de lire le fichier sélectionné.")?;
        let bytes = js_sys::Uint8Array::new(&buffer);
        crate::web::invoke_tauri_with_headers(
            "import_module_package",
            bytes.as_ref(),
            &[
                (UPLOAD_HEADER, &token),
                (OFFSET_HEADER, &offset.to_string()),
            ],
        )
        .await?;
    }
    let result = send_control(Control::Finish { upload }).await?;
    serde_wasm_bindgen::from_value(result).map_err(|error| error.to_string())
}

async fn send_control(control: Control) -> Result<wasm_bindgen::JsValue, String> {
    let control = serde_wasm_bindgen::to_value(&control).map_err(|error| error.to_string())?;
    crate::web::invoke_tauri_raw("import_module_package", &control).await
}
