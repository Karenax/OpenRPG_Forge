//! Pinned preview particle payload; filesystem paths remain in the native Vault adapter.
use forge_assets::source_particle_emission::SourceEmissionEvent;
pub(super) use forge_engine::source_graph_mount::SourceParticlePayload;
use leptos::prelude::*;
use std::cell::RefCell;

#[derive(Clone, Copy)]
pub(super) enum ControlTarget {
    Preview,
    Placement(forge_core::EntityId),
}
pub(super) struct ControlRequest {
    pub target: ControlTarget,
    pub event: SourceEmissionEvent,
    pub reply: Callback<Result<(), String>>,
}
thread_local! {static CONTROLS:RefCell<Vec<ControlRequest>>=const{RefCell::new(Vec::new())};}
fn submit(request: ControlRequest) -> Result<(), String> {
    CONTROLS.with(|pending| {
        let mut pending = pending.borrow_mut();
        if pending.len() >= 1024 {
            return Err("Trop de commandes d’effet en attente".into());
        }
        pending.push(request);
        Ok(())
    })
}
pub(super) fn take_controls() -> Vec<ControlRequest> {
    CONTROLS.with(|pending| std::mem::take(&mut *pending.borrow_mut()))
}

#[component]
pub(super) fn SourceParticleControls(target: ControlTarget) -> impl IntoView {
    let status = RwSignal::new(String::new());
    let reply = Callback::new(move |result: Result<(), String>| {
        let _ = status.try_set(result.map_or_else(
            |error| format!("Effet refusé : {error}"),
            |()| "Commande d’effet transmise.".into(),
        ));
    });
    let action = move |event| {
        if let Err(error) = submit(ControlRequest {
            target,
            event,
            reply,
        }) {
            let _ = status.try_set(error);
        }
    };
    view! {<div class="asset-library-actions">
        <button on:click=move |_|action(SourceEmissionEvent::Play)>"Démarrer les effets"</button>
        <button on:click=move |_|action(SourceEmissionEvent::Stop)>"Arrêter l’émission"</button>
        <p role="status">{move ||status.get()}</p>
    </div>}
}
