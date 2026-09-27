//! Native-window confirmation. Writes use the ordinary authoring command bus.
use crate::{StudioCommand, StudioResponse};
use forge_project::authoring::{AuthoringRequest, AuthoringState};
use leptos::prelude::*;

#[component]
pub(super) fn CloseDialog(
    state: RwSignal<Option<AuthoringState>>,
    busy: RwSignal<bool>,
    preview: RwSignal<Option<AuthoringRequest>>,
) -> impl IntoView {
    let dialog = NodeRef::<leptos::html::Dialog>::new();
    let error = RwSignal::new(String::new());
    let listener = leptos::leptos_dom::helpers::window_event_listener(
        leptos::ev::Custom::new("forge-close-requested"),
        move |_: web_sys::Event| {
            if let Some(dialog) = dialog.get() {
                error.set(String::new());
                if let Err(reason) = dialog.show_modal() {
                    error.set(format!("Confirmation indisponible : {reason:?}"));
                }
            }
        },
    );
    on_cleanup(move || listener.remove());
    let close = move |save: bool| {
        if busy.get_untracked() || (save && preview.get_untracked().is_some()) {
            return;
        }
        busy.set(true);
        wasm_bindgen_futures::spawn_local(async move {
            let result =
                async {
                    if save {
                        match crate::web::invoke_tauri(&StudioCommand::Authoring {
                            request: Box::new(AuthoringRequest::Save),
                        })
                        .await?
                        {
                            StudioResponse::Authoring { state: saved } if !saved.dirty => {
                                state.set(Some(*saved));
                            }
                            StudioResponse::Rejected { reason } => return Err(reason),
                            _ => return Err(
                                "La sauvegarde n’a pas été confirmée. La fenêtre reste ouverte."
                                    .into(),
                            ),
                        }
                    }
                    match crate::web::invoke_tauri(&StudioCommand::CloseWindow {
                        discard_unsaved: !save,
                    })
                    .await?
                    {
                        StudioResponse::Rejected { reason } => Err(reason),
                        _ => Ok(()),
                    }
                }
                .await;
            if let Err(reason) = result {
                error.set(reason);
            }
            busy.set(false);
        });
    };
    view! {
        <dialog node_ref=dialog class="gold-close-dialog" aria-labelledby="gold-close-title"
            on:keydown=move |event| event.stop_propagation()>
            <h2 id="gold-close-title">"Quitter le Studio ?"</h2>
            <p>"Les brouillons et aperçus non appliqués ne sont pas enregistrés. Restez dans le Studio pour les terminer."</p>
            <p class="gold-save-state">{move || if preview.get().is_some() { "Un aperçu attend votre validation." } else if state.get().is_some_and(|s| s.dirty) { "La carte contient des modifications non enregistrées." } else { "Les retouches appliquées sont enregistrées." }}</p>
            <output role="alert">{move || error.get()}</output>
            <div class="gold-dialog-actions">
                <button autofocus on:click=move |_| { if let Some(dialog) = dialog.get() { dialog.close(); } }>"Continuer à mapper"</button>
                <button disabled=move || busy.get() on:click=move |_| close(false)>"Quitter sans enregistrer"</button>
                <button class="authoring-primary" disabled=move || busy.get() || preview.get().is_some() || state.get().is_none_or(|s| s.document.is_none() || s.playtest_hash.is_some()) on:click=move |_| close(true)>"Enregistrer et quitter"</button>
            </div>
        </dialog>
    }
}
