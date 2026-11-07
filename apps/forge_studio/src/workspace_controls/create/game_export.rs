//! Export the saved game through the native, path-confined service.
use super::{Creation, game_export_form::reviewed_workspace_hash};
use crate::game_export_protocol::{GameExportRequest, GameExportResult};
use forge_core::ProjectId;
use forge_project::authoring::workspace::WorkspaceHostState;
use leptos::prelude::*;
use wasm_bindgen::JsValue;

#[component]
pub(super) fn GameExportControls(
    state: RwSignal<Option<WorkspaceHostState>>,
    controls: Creation,
    selected: RwSignal<String>,
    blocked: Signal<bool>,
) -> impl IntoView {
    let drafts = expect_context::<crate::authoring_controls::DraftRegistry>();
    let version = RwSignal::new("1.0.0".to_owned());
    let publisher = RwSignal::new(String::new());
    let license = RwSignal::new(String::new());
    let status = RwSignal::new(String::new());
    let error = RwSignal::new(String::new());
    let previous_project = StoredValue::new(selected.get_untracked());
    Effect::new(move |_| {
        let current = selected.get();
        if current != previous_project.get_value() {
            previous_project.set_value(current);
            status.set(String::new());
            error.set(String::new());
        }
    });
    let has_game = move || {
        let id = selected.get();
        state
            .get()
            .and_then(|state| state.head)
            .is_some_and(|head| {
                head.projects
                    .into_iter()
                    .any(|project| project.id.to_string() == id && project.game.is_some())
            })
    };
    let needs_save = move || state.get().is_some_and(|state| state.dirty);
    let needs_review = move || drafts.is_dirty() || controls.pending.get().is_some();
    view! {
        <details class="workspace-game-export" aria-busy=move || controls.busy.get().to_string()>
            <summary>"Créer les paquets du jeu"</summary>
            <p>"Cet export prépare un dossier de paquets pour un usage privé. Son installation et sa redistribution ne sont pas encore validées."</p>
            <Show when=move || !has_game()><p>"Dans « Configurer le jeu », choisissez un projet et sauvegardez sa configuration."</p></Show>
            <Show when=needs_save><p>"Sauvegardez le projet et ses cartes avant l’export."</p></Show>
            <Show when=needs_review><p>"Examinez ou abandonnez les saisies et propositions en cours avant l’export."</p></Show>
            <Show when=move || blocked.get() && !controls.busy.get()><p>"Fermez la carte et terminez l’opération en cours avant l’export."</p></Show>
            <fieldset disabled=move || blocked.get() || controls.busy.get()>
            <legend>"Informations pour l’export"</legend>
            <label>"Version du jeu"<input maxlength="128" prop:value=move || version.get()
                on:input=move |event| version.set(event_target_value(&event))/></label>
            <label>"Auteur / éditeur"<input maxlength="256" prop:value=move || publisher.get()
                on:input=move |event| publisher.set(event_target_value(&event))/></label>
            <label>"Conditions du jeu et de vos retouches"<textarea rows="4" maxlength="32768" prop:value=move || license.get()
                on:input=move |event| license.set(event_target_value(&event))></textarea></label>
            <p>"Les droits des médias et contenus importés restent applicables."</p>
            <button type="button" disabled=move || blocked.get() || controls.busy.get() || needs_save() || needs_review() || !has_game()
                on:click=move |_| {
                    status.set(String::new());
                    error.set(String::new());
                    let prepared = (|| {
                        let project_id = selected.get_untracked().parse::<ProjectId>()
                            .map_err(|_| "Choisissez le projet du jeu.".to_owned())?;
                        let loaded = state.get_untracked().ok_or("Ouvrez un espace de travail.")?;
                        let expected_workspace_hash = reviewed_workspace_hash(
                            &loaded, project_id, drafts.is_dirty(), controls.pending.get_untracked().is_some(),
                        )?;
                        let request = GameExportRequest {
                            schema_version: 1, project_id, expected_workspace_hash,
                            version: version.get_untracked(), publisher: publisher.get_untracked(),
                            license_text: license.get_untracked(),
                        };
                        request.validate()?;
                        Ok::<_, String>(request)
                    })();
                    let request = match prepared {
                        Ok(request) => request,
                        Err(reason) => { error.set(reason); return; }
                    };
                    controls.busy.set(true);
                    status.set("Vérification du jeu, des médias et des composants…".into());
                    wasm_bindgen_futures::spawn_local(async move {
                        let result = export(&request).await;
                        let _ = controls.busy.try_set(false);
                        match result {
                            Ok(result) => {
                                let message = format!("Export privé créé : {} module(s), dans {}. Pour installer le jeu, ouvrez Installer.exe sur Windows ou Installer sur Linux dans ce dossier. Installation non vérifiée ; redistribution non qualifiée.", result.module_count, result.directory);
                                let _ = status.try_set(message);
                            }
                            Err(reason) => {
                                let _ = status.try_set(String::new());
                                let _ = error.try_set(format!("Export impossible : {reason}"));
                            }
                        }
                    });
                }>"Créer les paquets privés"</button>
            </fieldset>
            <Show when=move || !error.get().is_empty()><p role="alert">{move || error.get()}</p></Show>
            <output aria-live="polite">{move || status.get()}</output>
        </details>
    }
}

async fn export(request: &GameExportRequest) -> Result<GameExportResult, String> {
    let arguments = js_sys::Object::new();
    js_sys::Reflect::set(
        &arguments,
        &JsValue::from_str("request"),
        &JsValue::from_str(&serde_json::to_string(request).map_err(|error| error.to_string())?),
    )
    .map_err(|_| "Préparation de la demande d’export impossible".to_owned())?;
    let value = crate::web::invoke_tauri_raw("export_game", arguments.as_ref()).await?;
    serde_wasm_bindgen::from_value(value).map_err(|error| error.to_string())
}
