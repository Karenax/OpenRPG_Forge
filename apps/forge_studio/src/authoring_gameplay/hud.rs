use super::timing::{self, TimingCommand};
use super::{
    GameView, PENDING, Pending, RESUME, Resume, SAVE, SaveCapture, VIEW, enqueue, generation,
};
use forge_core::ContentHash;
use forge_engine::playtest_timing::TimingState;
use forge_project::authoring::{AuthoringRequest, AuthoringState, game_save::GameSaveRequest};
use leptos::prelude::*;

#[component]
#[allow(
    clippy::too_many_lines,
    reason = "One save/load owner coordinates native persistence with the canvas snapshot"
)]
pub(crate) fn GameplayControls(
    state: RwSignal<Option<AuthoringState>>,
    busy: RwSignal<bool>,
    send: Callback<Result<AuthoringRequest, String>>,
) -> impl IntoView {
    let view = RwSignal::new(GameView::default());
    let captured = RwSignal::new(None::<SaveCapture>);
    let stored_hash = RwSignal::new(None::<ContentHash>);
    let saving = RwSignal::new(false);
    let message = RwSignal::new(String::new());
    let run = Memo::new(move |_| view.get().run);
    let timing_json = Memo::new(move |_| view.get().timing_json);
    Effect::new(move |_| {
        let _ = run.get();
        saving.set(false);
    });
    VIEW.with(|slot| *slot.borrow_mut() = Some(view));
    SAVE.with(|slot| *slot.borrow_mut() = Some(captured));
    on_cleanup(move || {
        VIEW.with(|slot| {
            if *slot.borrow() == Some(view) {
                slot.replace(None);
                PENDING.with(|queue| queue.borrow_mut().clear());
                timing::clear_request();
                RESUME.with(|resume| resume.replace(None));
            }
        });
        SAVE.with(|slot| {
            if *slot.borrow() == Some(captured) {
                slot.replace(None);
            }
        });
    });
    let area = Memo::new(move |_| state.get().and_then(|s| s.document.map(|d| d.area_id)));
    Effect::new(move |_| {
        let _ = area.get();
        stored_hash.set(None);
        view.set(GameView::default());
        captured.set(None);
        saving.set(false);
        PENDING.with(|queue| queue.borrow_mut().clear());
        timing::clear_request();
        RESUME.with(|resume| resume.replace(None));
    });
    Effect::new(move |_| {
        let Some(result) = captured.get() else {
            return;
        };
        captured.set(None);
        match result {
            Err(error) => {
                message.set(error);
                saving.set(false);
            }
            Ok((token, source, json)) => {
                let saved_area = area.get_untracked();
                let expected_stored = stored_hash.get_untracked();
                leptos::task::spawn_local(async move {
                    let response = crate::web::invoke_tauri(&crate::StudioCommand::GameSave {
                        request: Box::new(GameSaveRequest::Save {
                            expected_source: source,
                            expected_stored,
                            save_json: json,
                        }),
                    })
                    .await;
                    if token != generation() || area.try_get_untracked() != Some(saved_area) {
                        return;
                    }
                    match response {
                        Ok(crate::StudioResponse::GameSave { state }) => {
                            stored_hash.set(state.saved.map(|s| s.stored_hash));
                            message.set("Partie sauvegardée.".into());
                        }
                        Ok(crate::StudioResponse::Rejected { reason }) | Err(reason) => {
                            message.set(reason);
                        }
                        _ => message.set("Réponse inattendue pendant la sauvegarde.".into()),
                    }
                    saving.set(false);
                });
            }
        }
    });
    let load = move |compatible: bool| {
        let request = state
            .get_untracked()
            .and_then(|s| s.zone_head)
            .ok_or_else(|| "Ouvrir une zone".to_owned())
            .and_then(|head| head.content_hash());
        let Ok(source) = request else {
            message.set("Ouvrir une zone avant de reprendre.".into());
            return;
        };
        saving.set(true);
        let token = generation();
        let loaded_area = area.get_untracked();
        leptos::task::spawn_local(async move {
            let response = crate::web::invoke_tauri(&crate::StudioCommand::GameSave {
                request: Box::new(GameSaveRequest::Load {
                    expected_source: source,
                    allow_compatible_edits: compatible,
                }),
            })
            .await;
            let current_source = state
                .try_get_untracked()
                .flatten()
                .and_then(|s| s.zone_head)
                .and_then(|head| head.content_hash().ok());
            if token != generation() || area.try_get_untracked() != Some(loaded_area) {
                return;
            }
            if current_source != Some(source) {
                let _ = message.try_set("La zone a changé pendant la reprise.".into());
                let _ = saving.try_set(false);
                return;
            }
            match response {
                Ok(crate::StudioResponse::GameSave { state }) => match state.saved {
                    Some(saved) => {
                        stored_hash.set(Some(saved.stored_hash));
                        RESUME.with(|slot| {
                            *slot.borrow_mut() = Some(Resume {
                                source,
                                json: saved.save_json,
                                compatible,
                            });
                        });
                        send.run(Ok(AuthoringRequest::StartPlaytest {
                            expected_hash: source,
                        }));
                        message.set("Reprise préparée.".into());
                    }
                    None => message.set("Aucune partie sauvegardée pour cette zone.".into()),
                },
                Ok(crate::StudioResponse::Rejected { reason }) | Err(reason) => message.set(reason),
                _ => message.set("Réponse inattendue pendant la reprise.".into()),
            }
            saving.set(false);
        });
    };
    view! {
        <Show when=move || state.get().and_then(|s|s.zone_head).is_some_and(|h|h.corrections.gameplay.is_some())>
        <section class="authoring-gameplay-controls" aria-label="Partie RPG">
            <h3>"Partie RPG"</h3>
            <Show when=move || state.get().is_some_and(|s|s.playtest_hash.is_some())>
                <output aria-live="polite">{move ||format!("Vitalité {} · Endurance {}",view.get().vitality,view.get().stamina)}</output>
                <p>{move ||view.get().dialogue}</p>
                <div class="gameplay-actions">{move ||view.get().actions.into_iter().map(move |(label,action)|view! {
                    <button disabled=move ||busy.get() ||saving.get() ||!view.get().ready ||view.get().pending on:click=move |_|enqueue(&view.get_untracked(),action.clone())>{label}</button>
                }).collect_view()}</div>
                <p aria-live="polite">{move ||view.get().feedback}</p>
                <details class="authoring-navigation-diagnostic"><summary>"Déplacements des personnages"</summary>
                    <ul>{move ||view.get().navigation_summary.into_iter().map(|line|view!{<li>{line}</li>}).collect_view()}</ul>
                    <details><summary>"Diagnostic du trajet"</summary><pre data-forge-navigation-observation="v1">{move ||view.get().navigation_diagnostic}</pre></details>
                </details>
                <details class="authoring-playtest-timing"><summary>"Mesures du playtest"</summary>
                    <p>"Cadence de la boucle et durées réelles des opérations. Les durées inclusives ne s'additionnent pas ; aucune mesure GPU ni qualification High."</p>
                    <button id="authoring-playtest-timing-start" type="button" disabled=move ||busy.get() ||saving.get() ||!view.get().ready ||matches!(view.get().timing_state,Some(TimingState::Warmup | TimingState::Recording))
                        on:click=move |_|timing::enqueue(&view.get_untracked(),TimingCommand::Start)>"Démarrer la mesure"</button>
                    <button id="authoring-playtest-timing-stop" type="button" disabled=move ||!matches!(view.get().timing_state,Some(TimingState::Warmup | TimingState::Recording))
                        on:click=move |_|timing::enqueue(&view.get_untracked(),TimingCommand::Stop)>"Arrêter la mesure"</button>
                    <button id="authoring-playtest-timing-export" type="button" disabled=move ||!matches!(view.get().timing_state,Some(TimingState::Stopped | TimingState::LimitReached | TimingState::Invalidated))
                        on:click=move |_|message.set(match timing::export(&view.get_untracked()) {Ok(())=>"Export JSON demandé · aucune validation de gate.".into(),Err(error)=>error})>"Exporter les mesures JSON"</button>
                    <pre id="authoring-playtest-timing-ledger" data-forge-playtest-timing-observation="v1">{move ||timing_json.get().to_string()}</pre>
                </details>
                <details open><summary>"Inventaire et objectifs"</summary>
                <ul>{move ||view.get().inventory.into_iter().chain(view.get().quests).map(|line|view!{<li>{line}</li>}).collect_view()}</ul></details>
                <Show when=move ||!view.get().containers.is_empty()>
                    <details open><summary>"Conteneurs proches"</summary>
                        <ul>{move ||view.get().containers.into_iter().map(|line|view!{<li>{line}</li>}).collect_view()}</ul>
                    </details>
                </Show>
                <button disabled=move ||busy.get() ||saving.get() ||!view.get().ready ||view.get().pending on:click=move |_| {
                    let current=view.get_untracked();
                    if let Some(source)=current.source {
                        let queued=PENDING.with(|slot| {
                            let mut queue=slot.borrow_mut();
                            if current.ready && current.run==generation() && queue.is_empty() {
                                queue.push_back(Pending::Save(current.run,source)); true
                            } else { false }
                        });
                        if queued { saving.set(true); }
                    }
                }>"Sauvegarder la partie"</button>
            </Show>
            <Show when=move ||state.get().is_some_and(|s|s.playtest_hash.is_none())>
                <button disabled=move ||busy.get() ||saving.get() on:click=move |_|load(false)>"Reprendre la partie"</button>
                <button disabled=move ||busy.get() ||saving.get() on:click=move |_|load(true)>"Reprendre avec les retouches compatibles"</button>
            </Show>
            <output aria-live="polite">{move ||message.get()}</output>
        </section>
        </Show>
    }
}
