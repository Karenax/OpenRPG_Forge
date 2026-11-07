//! Studio confirmations for the same native MCP inbox served over stdio.
use crate::mcp_protocol::{McpRequest, McpView, ScopeRequest};
use forge_core::{CommandId, ContentHash};
use forge_project::authoring::AuthoringState;
use leptos::prelude::*;
use wasm_bindgen::JsValue;

#[derive(Clone, Copy)]
struct Controls {
    view: RwSignal<Option<McpView>>,
    status: RwSignal<String>,
    diagnostic: RwSignal<String>,
    working: RwSignal<bool>,
    refresh_pending: RwSignal<bool>,
    sharing: RwSignal<bool>,
    approval: RwSignal<Option<(CommandId, ContentHash)>>,
    host_busy: RwSignal<bool>,
    on_applied: Callback<AuthoringState>,
}
impl Controls {
    fn send(self, request: McpRequest) {
        if self.working.get_untracked() {
            if matches!(request, McpRequest::Status) {
                self.refresh_pending.set(true);
            }
            return;
        }
        let locked = !matches!(request, McpRequest::Status | McpRequest::Revoke);
        let status_request = matches!(request, McpRequest::Status);
        self.working.set(true);
        self.sharing.set(false);
        self.approval.set(None);
        if locked {
            self.host_busy.set(true);
        }
        wasm_bindgen_futures::spawn_local(async move {
            let result = invoke(&request).await;
            if locked {
                let _ = self.host_busy.try_set(false);
            }
            let _ = self.working.try_set(false);
            match result {
                Ok(mut view) => {
                    if !status_request {
                        let _ = self.diagnostic.try_set(String::new());
                    }
                    if let Some(authoring) = view.authoring.take() {
                        self.on_applied.run(*authoring);
                    }
                    if !status_request
                        || self
                            .diagnostic
                            .try_get_untracked()
                            .is_none_or(|message| message.is_empty())
                    {
                        let _ = self.status.try_set(view.message.clone());
                    }
                    let _ = self.view.try_set(Some(view));
                }
                Err(error) => {
                    let _ = self.diagnostic.try_set(error);
                    let _ = self.status.try_set("L’opération MCP n’a pas abouti. Consulter Diagnostics et vérifier l’état de la carte avant de confirmer à nouveau.".into());
                }
            }
            if self.refresh_pending.try_get_untracked() == Some(true) {
                let _ = self.refresh_pending.try_set(false);
                self.send(McpRequest::Status);
            }
        });
    }
}

#[component]
pub(crate) fn McpControls(
    scope: Signal<Result<ScopeRequest, String>>,
    state: RwSignal<Option<AuthoringState>>,
    host_busy: RwSignal<bool>,
    on_applied: Callback<AuthoringState>,
) -> impl IntoView {
    let controls = Controls {
        view: RwSignal::new(None),
        status: RwSignal::new(String::new()),
        diagnostic: RwSignal::new(String::new()),
        working: RwSignal::new(false),
        refresh_pending: RwSignal::new(false),
        sharing: RwSignal::new(false),
        approval: RwSignal::new(None),
        host_busy,
        on_applied,
    };
    let unavailable =
        move || controls.working.get() || host_busy.get() || crate::authoring_native_game::paused();
    let outdated = Signal::derive(move || {
        controls
            .view
            .get()
            .and_then(|view| view.scope)
            .is_none_or(|scope| {
                state
                    .get()
                    .and_then(|state| state.zone_head)
                    .and_then(|head| head.content_hash().ok())
                    != Some(scope.expected_head)
            })
    });
    let same = Signal::derive(move || {
        scope
            .get()
            .ok()
            .zip(controls.view.get().and_then(|view| view.scope))
            .is_some_and(|(current, prepared)| current == prepared)
    });
    Effect::new(move |_| {
        let _ = scope.get();
        controls.sharing.set(false);
        controls.approval.set(None);
    });
    let listener = window_event_listener(
        leptos::ev::Custom::new("forge-mcp-changed"),
        move |_: web_sys::Event| controls.send(McpRequest::Status),
    );
    on_cleanup(move || listener.remove());
    controls.send(McpRequest::Status);
    view! {
        <details class="assistant-mcp-controls"><summary>"Client MCP · périmètre et propositions"</summary>
            <p>{move||controls.view.get().map(|view|format!("Transport stdio {} · partage {} · {} appel(s) d’outil traités",if view.stdio_open{"ouvert"}else{"fermé"},if view.authorized{"autorisé"}else{"non autorisé"},view.tool_calls))}</p>
            <p>"Vous approuvez chaque retouche. Préparer une nouvelle sélection révoque l’accès actuel et écarte ses propositions en attente."</p>
            <button type="button" disabled=unavailable on:click=move |_|match scope.get_untracked(){Ok(scope)=>controls.send(McpRequest::Preview{scope}),Err(error)=>controls.status.set(error)}>"Préparer le périmètre MCP sélectionné"</button>
            <Show when=move||controls.view.get().is_some_and(|view|view.context.is_some())>
                <details><summary>"Données exactes exposées au client MCP"</summary><pre style="white-space:pre-wrap;overflow-wrap:anywhere;max-height:24rem;overflow:auto">{move||controls.view.get().and_then(|view|view.context).and_then(|context|serde_json::to_string_pretty(&context).ok()).unwrap_or_default()}</pre></details>
                <label><input type="checkbox" prop:checked=move||controls.sharing.get() on:change=move|event|controls.sharing.set(event_target_checked(&event))/>"J’autorise ce client stdio à lire ce contexte et proposer des retouches"</label>
                <button type="button" disabled=move||unavailable() || outdated.get() || !same.get() || !controls.sharing.get() || !controls.view.get().is_some_and(|view|view.stdio_open) on:click=move |_|{
                    if let Some(view)=controls.view.get_untracked() && let(Some(scope),Some(hash))=(view.scope,view.context_hash){controls.send(McpRequest::Authorize{scope,expected_context_hash:hash});}
                }>"Autoriser ce périmètre MCP"</button>
            </Show>
            <button type="button" disabled=move||controls.working.get() on:click=move |_|controls.send(McpRequest::Status)>"Actualiser les propositions MCP"</button>
            <button type="button" disabled=move||controls.working.get() on:click=move |_|controls.send(McpRequest::Revoke)>"Révoquer le périmètre MCP"</button>
            <Show when=move||outdated.get() && controls.view.get().is_some_and(|view|view.authorized)><p>"La révision a changé ; le client ne peut plus utiliser ce périmètre. Préparer une nouvelle autorisation."</p></Show>
            {reviews(controls,outdated,state)}
            <p role="status">{move||controls.status.get()}</p>
            <p>"Les propositions sont conservées dans votre espace utilisateur. Après reconnexion, autoriser à nouveau le même périmètre puis relire les diffs."</p>
            <p>"Les anciennes approbations sont révoquées. Sauvegarder la carte après application ; l’historique commun reste disponible."</p>
            <Show when=move||!controls.diagnostic.get().is_empty()><details><summary>"Diagnostics MCP"</summary><pre style="white-space:pre-wrap;overflow-wrap:anywhere">{move||controls.diagnostic.get()}</pre></details></Show>
        </details>
    }
}

fn reviews(
    controls: Controls,
    outdated: Signal<bool>,
    state: RwSignal<Option<AuthoringState>>,
) -> impl IntoView {
    move || {
        controls.view.get().map(|view|{
        let inherited_name=view.context.as_ref().and_then(|context|context["zone_name"].as_str()).map(str::to_owned);
        view.proposals.into_iter().map(move|proposal|{
        let identity=(proposal.command_id,proposal.review_hash);
        let zone_name=inherited_name.clone();
        view! {<section class="assistant-mcp-proposal"><h4>{proposal.summary}</h4>
            <ol>{proposal.changes.into_iter().map(move|change|view!{<li><strong>{crate::assistant_ui::change_label(&change.path)}</strong><p>{format!("Avant : {}",crate::assistant_ui::change_before(&change,zone_name.as_deref()))}</p><p>{format!("Après : {}",change.after.map_or_else(||"absent".into(),|value|value.to_string()))}</p><code style="overflow-wrap:anywhere">{change.path}</code></li>}).collect_view()}</ol>
            <label><input type="checkbox" prop:checked=move||controls.approval.get()==Some(identity) on:change=move|event|controls.approval.set(event_target_checked(&event).then_some(identity))/>"J’approuve ce diff MCP pour cette révision"</label>
            <button type="button" disabled=move||controls.working.get() || controls.host_busy.get() || outdated.get() || crate::authoring_native_game::paused() || state.get().is_none() || controls.approval.get()!=Some(identity) on:click=move |_|controls.send(McpRequest::Apply{command_id:identity.0,review_hash:identity.1})>"Appliquer ce diff MCP approuvé"</button>
        </section>}
    }).collect_view()})
    }
}

async fn invoke(request: &McpRequest) -> Result<McpView, String> {
    let arguments = js_sys::Object::new();
    js_sys::Reflect::set(
        &arguments,
        &JsValue::from_str("request"),
        &JsValue::from_str(&serde_json::to_string(request).map_err(|error| error.to_string())?),
    )
    .map_err(|_| "Requête MCP impossible.")?;
    let result = crate::web::invoke_tauri_raw("execute_mcp", arguments.as_ref()).await?;
    serde_wasm_bindgen::from_value(result).map_err(|error| error.to_string())
}
