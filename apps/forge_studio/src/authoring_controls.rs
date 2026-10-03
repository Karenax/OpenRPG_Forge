//! Authoring commands go to the native session; the preview scene is not canonical.
use super::{StudioCommand, StudioResponse};
use forge_core::CommandId;
use forge_project::{
    authoring::{AuthoringRequest, AuthoringState, new_terrain},
    terrain_edit::{TerrainAction, TerrainEdit, TerrainEditRequest},
};
use leptos::prelude::*;

mod creation;

#[derive(Clone)]
struct ActionEvent {
    kind: &'static str,
    message: String,
}

fn record_event(events: RwSignal<Vec<ActionEvent>>, kind: &'static str, message: &str) {
    events.update(|items| {
        items.push(ActionEvent {
            kind,
            message: message.chars().take(512).collect(),
        });
        if items.len() > 32 {
            items.remove(0);
        }
    });
}

fn command_label(request: &AuthoringRequest) -> &'static str {
    match request {
        AuthoringRequest::Preview { .. } | AuthoringRequest::PreviewZone { .. } => "Aperçu préparé",
        AuthoringRequest::Apply { .. } | AuthoringRequest::ApplyZone { .. } => "Retouche appliquée",
        AuthoringRequest::Save => "Zone sauvegardée",
        AuthoringRequest::Inspect => "Session relue",
        _ => "Commande terminée",
    }
}

#[derive(Clone, Copy, Debug, Eq, PartialEq)]
pub(super) enum AuthoringTab {
    Project,
    Terrain,
    Nature,
    Routes,
    Water,
    Objects,
    Buildings,
    FollowUp,
}

impl AuthoringTab {
    pub(super) const ALL: [Self; 8] = [
        Self::Project,
        Self::Terrain,
        Self::Nature,
        Self::Routes,
        Self::Water,
        Self::Objects,
        Self::Buildings,
        Self::FollowUp,
    ];

    pub(super) const fn index(self) -> usize {
        self as usize
    }

    pub(super) const fn label(self) -> &'static str {
        match self {
            Self::Project => "Projet",
            Self::Terrain => "Terrain",
            Self::Nature => "Nature",
            Self::Routes => "Chemins",
            Self::Water => "Eau",
            Self::Objects => "Objets",
            Self::Buildings => "Construire",
            Self::FollowUp => "Tester & suivi",
        }
    }

    const fn key(self) -> &'static str {
        match self {
            Self::Project => "project",
            Self::Terrain => "terrain",
            Self::Nature => "nature",
            Self::Routes => "routes",
            Self::Water => "water",
            Self::Objects => "objects",
            Self::Buildings => "buildings",
            Self::FollowUp => "follow-up",
        }
    }
}

#[component]
#[allow(
    clippy::too_many_lines,
    reason = "One session owner keeps mounted tool panels and native command state consistent"
)]
pub fn AuthoringControls() -> impl IntoView {
    let project = RwSignal::new("mon-projet".to_owned());
    let zone = RwSignal::new("zone-1".to_owned());
    let name = RwSignal::new("Nouvelle zone".to_owned());
    let elevation = RwSignal::new("0".to_owned());
    let state = RwSignal::new(None::<AuthoringState>);
    let busy = RwSignal::new(false);
    Effect::new(move |_| super::authoring_brush_pointer::set_command_busy(busy.get()));
    let command_timing = RwSignal::new(String::new());
    let workspace_refresh = RwSignal::new(0_u64);
    let preview = RwSignal::new(None::<AuthoringRequest>);
    let placement_selection = super::authoring_assets::placements::selection::bind();
    let vegetation_selection = bind_vegetation_selection(state);
    let preview_cancelled = RwSignal::new(false);
    let difference = RwSignal::new(String::new());
    let status = RwSignal::new("Édition terrain · backend Tauri requis".to_owned());
    let viewport_status = use_context::<ReadSignal<String>>();
    let events = RwSignal::new(vec![ActionEvent {
        kind: "info",
        message: "Journal prêt · les aperçus, validations et refus apparaissent ici.".into(),
    }]);
    let authoring_route = super::authoring_viewport::requested();
    let active_tab = use_context::<RwSignal<AuthoringTab>>()
        .unwrap_or_else(|| RwSignal::new(AuthoringTab::Project));
    Effect::new(move |_| {
        let open = state.get().is_some_and(|s| s.document.is_some());
        if authoring_route
            && let Some(root) = web_sys::window()
                .and_then(|w| w.document())
                .and_then(|d| d.document_element())
        {
            let _ = root.set_attribute("data-forge-zone-open", if open { "true" } else { "false" });
        }
    });
    let panel_layout = super::authoring_dock::PanelLayout::new(active_tab);
    let path = RwSignal::new(String::new());
    let river = RwSignal::new(String::new());
    let building = super::authoring_building_projection::selection::bind();
    Effect::new(move |previous: Option<Option<forge_core::EntityId>>| {
        let selected = placement_selection.get();
        if selected.is_some() && previous != Some(selected) {
            active_tab.set(AuthoringTab::Objects);
        }
        selected
    });
    Effect::new(move |_| {
        let _ = active_tab.get();
        if let Some(panel) = web_sys::window()
            .and_then(|window| window.document())
            .and_then(|document| {
                document
                    .query_selector(".authoring-panel-scroll")
                    .ok()
                    .flatten()
            })
        {
            panel.set_scroll_top(0);
        }
    });
    Effect::new(move |previous: Option<Option<forge_core::AreaId>>| {
        let area = state
            .get()
            .and_then(|session| session.document.map(|doc| doc.area_id));
        if previous != Some(area) && area.is_some() {
            active_tab.set(AuthoringTab::Terrain);
        }
        area
    });
    let cancel_preview = connect_preview_cancel(
        authoring_route,
        preview_cancelled,
        preview,
        difference,
        status,
        events,
    );
    let send = move |request: Result<AuthoringRequest, String>| {
        if busy.get_untracked() {
            record_event(
                events,
                "warning",
                "Commande en cours · attendez son résultat avant de recommencer.",
            );
            return;
        }
        // A rejected local input must not discard an already reviewed proposal.
        let Some(request) = accepted_request(request, status, state, events) else {
            return;
        };
        if matches!(request, AuthoringRequest::Save) && preview.get_untracked().is_some() {
            let message = "Aperçu non appliqué : appliquer ou abandonner la proposition avant d’enregistrer. Votre aperçu est conservé.";
            status.set(message.into());
            record_event(events, "warning", message);
            return;
        }
        let label = command_label(&request);
        // Only one native request may be in flight. Cancellation remains sticky until
        // the next accepted request, so a late preview cannot restore its Apply button.
        preview_cancelled.set(false);
        preview.set(None);
        difference.set(String::new());
        super::authoring_preview::clear();
        let preview_request = pending_preview(&request);
        // Only a successful native session change may dispose the old viewport.
        // The native session survives navigation, including its unsaved edits.
        let replace_viewport = replaces_projection(&request);
        let refresh_workspace = refreshes_workspace(&request);
        busy.set(true);
        wasm_bindgen_futures::spawn_local(async move {
            match invoke_timed(request, command_timing).await {
                Ok(StudioResponse::Authoring { state: result }) => {
                    notify_workspace(refresh_workspace, workspace_refresh);
                    if !observe_or_report(&result, state, status, busy, replace_viewport) {
                        record_event(
                            events,
                            if replace_viewport { "info" } else { "warning" },
                            &status.get_untracked(),
                        );
                        return;
                    }
                    if !preview_cancelled.get_untracked()
                        && let Some((apply, label)) =
                            reviewed_preview(&result, preview_request.as_ref())
                    {
                        difference.set(label);
                        preview.set(Some(apply));
                    }
                    super::authoring_brush_pointer::set_preview_ready(
                        preview.get_untracked().is_some(),
                    );
                    let detail = if preview.get_untracked().is_some() {
                        format!(
                            "{label} · {} · Appliquer pour confirmer.",
                            difference.get_untracked()
                        )
                    } else {
                        format!("{label} · {}.", session_status(&result))
                    };
                    record_event(events, "success", &detail);
                    sync_fields(&result, preview_request.is_some(), name, elevation);
                    status.set(session_status(&result).into());
                    state.set(Some(*result));
                }
                Ok(StudioResponse::Rejected { reason }) | Err(reason) => {
                    let message = super::authoring_feedback::rejection(reason);
                    record_event(events, "error", &message);
                    status.set(message);
                    super::authoring_brush_pointer::set_preview_ready(false);
                }
                Ok(_) => {
                    let message = "Réponse inattendue du backend".to_owned();
                    record_event(events, "error", &message);
                    status.set(message);
                    super::authoring_brush_pointer::set_preview_ready(false);
                }
            }
            busy.set(false);
        });
    };
    if authoring_route {
        super::atelier_ui::bind_main(super::atelier_ui::MainBindings {
            state,
            status,
            active: active_tab,
            preview,
            path,
            river,
            building,
            vegetation: vegetation_selection,
            placement: placement_selection,
            busy,
            send: Callback::new(send),
        });
    }
    restore_session(authoring_route, Callback::new(send));
    connect_shortcuts(
        authoring_route,
        state,
        busy,
        status,
        preview,
        Callback::new(send),
    );
    view! {
        <super::authoring_close::CloseDialog state=state busy=busy preview=preview/>
        <details class="scene-navigation authoring-editor" open=authoring_route>
            <summary>"Édition de zone · Gate 2"</summary>
            <div aria-label="Édition terrain">
                <div class="authoring-toolbar" aria-busy=move || busy.get().to_string()>
                    <div class="authoring-context" aria-live="polite">{move || state.get().and_then(|session| session.document.map(|doc| doc.name)).unwrap_or_else(|| "Aucune zone ouverte".into())}</div>
                    <CommandBar state=state busy=busy preview=preview difference=difference status=status send=Callback::new(send) cancel_preview=cancel_preview/>
                    <super::authoring_playtest_controls::PlaytestActions state=state busy=busy send=Callback::new(send)/>
                    <nav class="authoring-main-tabs" aria-label="Choisir un outil d’édition">
                        {AuthoringTab::ALL.into_iter().map(move |tab| view! {
                            <button type="button" data-authoring-tab-button=tab.key() title=tab.label()
                                aria-current=move || if active_tab.get() == tab { "page" } else { "false" }
                                on:click=move |_| {
                                    super::authoring_brush_pointer::cancel();
                                    active_tab.set(tab);
                                }><svg class="authoring-tool-icon" viewBox="0 0 24 24" aria-hidden="true" focusable="false"><path d=match tab {
                                    AuthoringTab::Project => "M3 7h7l2 2h9v11H3Z M3 7V4h7l2 3",
                                    AuthoringTab::Terrain => "M2 20 9 5l4 8 3-5 6 12Z M6 11l3 2 3-2",
                                    AuthoringTab::Nature => "M12 22v-6 M12 2 5 11h4l-5 6h16l-5-6h4Z",
                                    AuthoringTab::Routes => "M3 21c0-8 15-4 15-10S7 7 7 2 M8 21c0-5 15-2 15-10",
                                    AuthoringTab::Water => "M12 2C9 7 5 11 5 15a7 7 0 0 0 14 0c0-4-4-8-7-13Z",
                                    AuthoringTab::Objects => "m12 2 9 5v10l-9 5-9-5V7Z M3 7l9 5 9-5 M12 12v10",
                                    AuthoringTab::Buildings => "m2 11 10-8 10 8 M5 9v12h14V9 M10 21v-7h4v7",
                                    AuthoringTab::FollowUp => "m8 4 12 8-12 8Z",
                                }/></svg><span>{tab.label()}</span></button>
                        }).collect_view()}
                    </nav>
                </div>
                <aside class="authoring-scene-tree" aria-label="Scène">
                    <div class="forge-project-card">
                        <svg viewBox="0 0 220 82" aria-hidden="true"><path d="M0 82V62L45 15l48 43 36-30 91 54Z"/><path d="m17 82 52-47 46 47M115 82l43-46 62 46"/><circle cx="180" cy="18" r="9"/></svg>
                        <span>"ESPACE DE CRÉATION"</span>
                        <h3>{move || state.get().and_then(|s| s.document.map(|d| d.name)).unwrap_or_else(|| "Votre prochain monde".into())}</h3>
                        <button on:click=move |_| active_tab.set(AuthoringTab::Project)>"Gérer le projet"</button>
                    </div>
                    <h2>"Scène"</h2>
                    <Show when=move || state.get().is_none_or(|s| s.document.is_none())><p class="forge-scene-empty">"Ouvrez une zone : ses objets et ses couches apparaîtront ici."</p></Show>
                    <super::authoring_zone::hierarchy::Hierarchy state=state busy=busy active_tab=active_tab path=path river=river building=building placement=placement_selection vegetation=vegetation_selection/>
                </aside>
                <div class="forge-topic-window" data-dock=move || panel_layout.current().dock.key()
                    data-collapsed=move || panel_layout.current().collapsed.to_string() style=move || panel_layout.style()>
                    <super::authoring_dock::PanelHeader layout=panel_layout/>
                <div class="authoring-panel-scroll" hidden=move || panel_layout.current().collapsed>
                    <section class="authoring-panel" data-authoring-tab="project" hidden=move || active_tab.get() != AuthoringTab::Project>
                        <h2>"Projet et zone"</h2>
                        <p>"Ouvrir une zone ou en créer une, puis revenir au terrain pour dessiner."</p>
                        <super::workspace_controls::WorkspaceControls host_busy=busy refresh=workspace_refresh on_saved=Callback::new(move |()| send(Ok(AuthoringRequest::Inspect))) on_open=Callback::new(move |area_id| send(Ok(AuthoringRequest::OpenManaged { area_id })))/>
                        <super::workspace_controls::ManagedTabs state=state busy=busy on_open=Callback::new(move |area_id| send(Ok(AuthoringRequest::OpenManaged { area_id })))/>
                        <super::authoring_catalog::ZoneChooser on_open=Callback::new(move |area_id| send(Ok(AuthoringRequest::Open { area_id })))/>
                        <Show when=move || !authoring_route>
                            <p>"Cette scène de preuve n’est pas la zone éditée."</p>
                            <a href="?authoring=1">"Ouvrir l’espace terrain édité"</a>
                        </Show>
                        <details><summary>"Créer ou modifier la zone"</summary>
                            <ZoneSessionControls project=project zone=zone name=name elevation=elevation state=state busy=busy send=Callback::new(send)/>
                        </details>
                        <super::authoring_zone_information::ZoneInformation state=state/>
                    </section>
                    <section class="authoring-panel" data-authoring-tab="terrain" hidden=move || active_tab.get() != AuthoringTab::Terrain>
                        <h2>"Terrain"</h2>
                        <p>"Choisir une brosse, dessiner dans la vue, examiner l’aperçu puis appliquer en haut."</p>
                        <TerrainEditControls state=state busy=busy send=Callback::new(send)/>
                    </section>
                    <super::authoring_zone::SourceControls state=state busy=busy active_tab=active_tab path=path river=river building=building vegetation_selection=vegetation_selection on_command=Callback::new(send)/>
                    <section class="authoring-panel" data-authoring-tab="objects" hidden=move || active_tab.get() != AuthoringTab::Objects>
                        <super::authoring_assets::AssetBrowserControls state=state host_busy=busy inspected=placement_selection on_command=Callback::new(send)/>
                        <super::authoring_gameplay::GameplayEditor state=state busy=busy send=Callback::new(send)/>
                    </section>
                    <section class="authoring-panel" data-authoring-tab="follow-up" hidden=move || active_tab.get() != AuthoringTab::FollowUp>
                        <super::authoring_playtest_controls::PlaytestControls state=state busy=busy send=Callback::new(send)/>
                        <super::authoring_gameplay::GameplayControls state=state busy=busy send=Callback::new(send)/>
                        <super::authoring_history::HistoryControls state=state/>
                        <super::authoring_input::PlaytestStatus/>
                        <details><summary>"Aide et raccourcis"</summary>
                            <p>"Ctrl+Entrée applique l’aperçu · Ctrl+S sauvegarde · Ctrl+Z annule · Ctrl+Y ou Ctrl+Maj+Z rétablit. Appliquer avant de sauvegarder."</p>
                            <output>{move || command_timing.get()}</output>
                        </details>
                    </section>
                </div>
                </div>
                <super::authoring_projection_diagnostics::ProjectionDiagnosticLedger/>
                <section class="gold-review-status forge-event-console" aria-label="Journal des actions">
                    <div class="forge-event-console-heading"><strong>"Journal des actions"</strong><span>{move || format!("{} événement(s)", events.get().len())}</span></div>
                    <output class="forge-event-viewport" aria-live="polite">{move || viewport_status.map(|source| source.get()).unwrap_or_default()}</output>
                    <ol aria-live="polite">{move || events.get().into_iter().rev().take(3).map(|event| view! {
                        <li data-kind=event.kind>{event.message}</li>
                    }).collect_view()}</ol>
                    <details><summary>"Voir les événements précédents"</summary><ol>{move || events.get().into_iter().rev().skip(3).map(|event| view! {
                        <li data-kind=event.kind>{event.message}</li>
                    }).collect_view()}</ol></details>
                </section>
            </div>
        </details>
    }
}

#[component]
pub(super) fn WorkspaceActivities(active: RwSignal<AuthoringTab>) -> impl IntoView {
    view! {
        <nav class="forge-workspaces" aria-label="Activité de création">
            {[("Organiser", AuthoringTab::Project), ("Composer", AuthoringTab::Objects), ("Sculpter", AuthoringTab::Terrain), ("Habiller", AuthoringTab::Nature)].into_iter().map(move |(label,tab)| view! {
                <button aria-pressed=move || (active.get()==tab).to_string()
                    on:click=move |_| { super::authoring_brush_pointer::cancel(); active.set(tab); }>{label}</button>
            }).collect_view()}
        </nav>
    }
}

#[component]
pub(super) fn WorkspaceWelcome() -> impl IntoView {
    // Visibility is driven by the native-session observation, not a fake scene.
    view! {
        <div class="forge-workspace-welcome">
            <div class="forge-welcome-emblem" aria-hidden="true">"F"</div>
            <span>"VOTRE ATELIER DE MONDES"</span>
            <h2>"Tout commence par un lieu."</h2>
            <p>"Ouvrez une zone depuis le panneau Projet, ou créez votre premier terrain."</p>
            <div class="forge-welcome-steps"><span>"01  Ouvrir une zone"</span><span>"02  Dessiner le relief"</span><span>"03  Donner vie au décor"</span></div>
        </div>
    }
}

#[component]
fn ZoneSessionControls(
    project: RwSignal<String>,
    zone: RwSignal<String>,
    name: RwSignal<String>,
    elevation: RwSignal<String>,
    state: RwSignal<Option<AuthoringState>>,
    busy: RwSignal<bool>,
    send: Callback<Result<AuthoringRequest, String>>,
) -> impl IntoView {
    view! {
        <label>"Clé projet"<input prop:value=move || project.get() on:input=move |ev| project.set(event_target_value(&ev))/></label>
        <label>"Clé zone"<input prop:value=move || zone.get() on:input=move |ev| zone.set(event_target_value(&ev))/></label>
        <label>"Nom"<input prop:value=move || name.get() on:input=move |ev| { name.set(event_target_value(&ev)); super::authoring_shortcuts::cancel_preview(); }/></label>
        <creation::CreationControls project=project zone=zone name=name state=state busy=busy send=send/>
        <button disabled=move || busy.get() on:click=move |_| send.run(new_terrain(&project.get_untracked(), &zone.get_untracked(), "Ouverture").map(|document| AuthoringRequest::Open { area_id: document.area_id }))>"Ouvrir"</button>
        <button disabled=move || busy.get() on:click=move |_| send.run(prepare_edit(state.get_untracked(), TerrainAction::Edit(TerrainEdit::Rename { name: name.get_untracked() })))>"Prévisualiser le nom"</button>
        <label>"Altitude de base (m)"<input type="number" step="0.1" prop:value=move || elevation.get() on:input=move |ev| { elevation.set(event_target_value(&ev)); super::authoring_shortcuts::cancel_preview(); }/></label>
        <button disabled=move || busy.get() on:click=move |_| send.run(elevation.get_untracked().parse::<f32>().map_err(|_| "Altitude invalide".to_owned()).and_then(|metres| prepare_edit(state.get_untracked(), TerrainAction::Edit(TerrainEdit::SetBaseElevation { metres }))))>"Prévisualiser l’altitude"</button>
        <button disabled=move || busy.get() on:click=move |_| send.run(Ok(AuthoringRequest::Close))>"Fermer la zone"</button>
        <button disabled=move || busy.get() on:click=move |_| send.run(Ok(AuthoringRequest::Inspect))>"Relire la session native"</button>
    }
}

#[component]
fn TerrainEditControls(
    state: RwSignal<Option<AuthoringState>>,
    busy: RwSignal<bool>,
    send: Callback<Result<AuthoringRequest, String>>,
) -> impl IntoView {
    let paint = RwSignal::new(false);
    view! {
        <div class="gold-subtools">
            <button aria-pressed=move || (!paint.get()).to_string() on:click=move |_| { super::authoring_brush_pointer::cancel(); paint.set(false); }>"Sculpter"</button>
            <button aria-pressed=move || paint.get().to_string() on:click=move |_| { super::authoring_brush_pointer::cancel(); paint.set(true); }>"Peindre"</button>
        </div>
        <fieldset class="authoring-brush-gate" disabled=move || busy.get() || state.get().is_none_or(|s| s.document.is_none() || s.playtest_hash.is_some())>
            <div hidden=move || paint.get()>
            <super::authoring_brush::BrushControls on_preview=Callback::new(move |brush| send.run(prepare_brush(state.get_untracked(), brush))) />
            </div><div hidden=move || !paint.get()>
            <super::authoring_materials::MaterialControls state=state on_preview=Callback::new(move |result: Result<TerrainEdit, String>| send.run(result.and_then(|edit| prepare_edit(state.get_untracked(), TerrainAction::Edit(edit)))))/>
            </div>
        </fieldset>
    }
}

#[component]
pub(super) fn VegetationDensityControls(
    state: RwSignal<Option<AuthoringState>>,
    busy: RwSignal<bool>,
    vegetation_selection: RwSignal<String>,
    send: Callback<Result<AuthoringRequest, String>>,
) -> impl IntoView {
    view! {
        <fieldset class="authoring-brush-gate" disabled=move || busy.get() || state.get().is_none_or(|s| s.zone_head.is_none() || s.playtest_hash.is_some())>
            <super::authoring_density::DensityControls state=state selected=vegetation_selection on_command=send/>
        </fieldset>
    }
}

fn bind_vegetation_selection(state: RwSignal<Option<AuthoringState>>) -> RwSignal<String> {
    let selected = RwSignal::new(String::new());
    Effect::new(move |previous: Option<Option<forge_core::AreaId>>| {
        let head = state.get().and_then(|s| s.zone_head);
        let area = head.as_ref().map(|h| h.base.terrain.area_id);
        let key = selected.get_untracked();
        let exists = head
            .as_ref()
            .and_then(|h| h.base.vegetation.as_ref())
            .is_some_and(|v| v.layers.iter().any(|l| l.id.to_string() == key));
        if previous != Some(area) || (!key.is_empty() && !exists) {
            selected.set(String::new());
        }
        area
    });
    selected
}

async fn invoke_authoring(request: AuthoringRequest) -> Result<StudioResponse, String> {
    super::web::invoke_tauri(&StudioCommand::Authoring {
        request: Box::new(request),
    })
    .await
}

async fn invoke_timed(
    request: AuthoringRequest,
    timing: RwSignal<String>,
) -> Result<StudioResponse, String> {
    let started = web_sys::window()
        .and_then(|w| w.performance())
        .map(|p| p.now());
    let result = invoke_authoring(request).await;
    timing.set(command_elapsed(started));
    result
}

fn command_elapsed(started: Option<f64>) -> String {
    let ended = web_sys::window()
        .and_then(|w| w.performance())
        .map(|p| p.now());
    match started.zip(ended) {
        Some((start, end)) if end >= start => format!(
            "Dernier appel natif terminé en {:.1} ms — transport et traitement inclus ; projection 3D exclue, succès non déduit.",
            end - start
        ),
        _ => "Durée de commande indisponible.".into(),
    }
}

fn replace_session_projection(
    result: AuthoringState,
    state: RwSignal<Option<AuthoringState>>,
    status: RwSignal<String>,
    busy: RwSignal<bool>,
) {
    // A different session is not the next revision of the existing terrain.
    // Invalidate only after host acceptance; never enqueue its document into
    // the old Bevy world. The fresh mount inspects the retained native session.
    super::authoring_viewport::observe(None, None);
    state.set(Some(result));
    status.set("Changement de session : reconstruction de la projection".into());
    if let Err(error) = reload_projection() {
        status.set(error);
    }
    busy.set(false);
}

fn refreshes_workspace(request: &AuthoringRequest) -> bool {
    matches!(
        request,
        AuthoringRequest::Save
            | AuthoringRequest::CompactZoneArchive { .. }
            | AuthoringRequest::ApplyZone { .. }
            | AuthoringRequest::OpenManaged { .. }
            | AuthoringRequest::Close
    )
}

fn notify_workspace(refresh: bool, generation: RwSignal<u64>) {
    if refresh {
        generation.update(|value| *value = value.wrapping_add(1));
    }
}

fn observe_or_report(
    result: &AuthoringState,
    state: RwSignal<Option<AuthoringState>>,
    status: RwSignal<String>,
    busy: RwSignal<bool>,
    replace_viewport: bool,
) -> bool {
    if replace_viewport {
        replace_session_projection(result.clone(), state, status, busy);
        return false;
    }
    if let Err(error) = observe_result(result) {
        status.set(error);
        state.set(Some(result.clone()));
        busy.set(false);
        return false;
    }
    true
}

fn pending_preview(request: &AuthoringRequest) -> Option<AuthoringRequest> {
    match request {
        AuthoringRequest::Preview { .. } | AuthoringRequest::PreviewZone { .. } => {
            Some(request.clone())
        }
        _ => None,
    }
}

fn connect_preview_cancel(
    authoring_route: bool,
    cancelled: RwSignal<bool>,
    preview: RwSignal<Option<AuthoringRequest>>,
    difference: RwSignal<String>,
    status: RwSignal<String>,
    events: RwSignal<Vec<ActionEvent>>,
) -> Callback<()> {
    let callback = Callback::new(move |()| {
        cancelled.set(true);
        if preview.get_untracked().is_some() {
            let message = "Aperçu abandonné ; carte appliquée inchangée.".to_owned();
            status.set(message.clone());
            record_event(events, "info", &message);
        }
        super::authoring_brush_pointer::set_preview_ready(false);
        preview.set(None);
        difference.set(String::new());
        super::authoring_assets::cancel_preview();
        super::authoring_preview::clear();
    });
    if authoring_route {
        super::authoring_shortcuts::install_cancel(callback);
    }
    callback
}

#[component]
fn CommandBar(
    state: RwSignal<Option<AuthoringState>>,
    busy: RwSignal<bool>,
    preview: RwSignal<Option<AuthoringRequest>>,
    difference: RwSignal<String>,
    status: RwSignal<String>,
    send: Callback<Result<AuthoringRequest, String>>,
    cancel_preview: Callback<()>,
) -> impl IntoView {
    let promotion_ready = super::authoring_vegetation::bind_promotion_ready();
    let apply_error = move || {
        let _ = promotion_ready.get();
        preview.get().and_then(|request| {
            super::authoring_vegetation::check_promotion_apply(state.get().as_ref(), &request).err()
        })
    };
    view! {
        <div class="authoring-command-bar" role="group" aria-label="Actions de la zone courante">
            <button class="authoring-apply" title="Appliquer la retouche prévisualisée · Ctrl+Entrée" disabled=move || busy.get() || preview.get().is_none() || apply_error().is_some() on:click=move |_| send.run(preview.get_untracked().ok_or_else(|| "Prévisualiser une retouche d’abord".into()))>"Appliquer"</button>
            <output aria-live="polite" hidden=move || apply_error().is_none()>{move || apply_error().unwrap_or_default()}</output>
            <button disabled=move || busy.get() || state.get().is_none_or(|s| !s.can_undo || s.playtest_hash.is_some()) on:click=move |_| send.run(prepare_edit(state.get_untracked(), TerrainAction::Undo))>"Annuler"</button>
            <button disabled=move || busy.get() || state.get().is_none_or(|s| !s.can_redo || s.playtest_hash.is_some()) on:click=move |_| send.run(prepare_edit(state.get_untracked(), TerrainAction::Redo))>"Rétablir"</button>
            <button title="Enregistrer les retouches appliquées · Ctrl+S" disabled=move || busy.get() || preview.get().is_some() || state.get().is_none_or(|s| s.document.is_none() || s.playtest_hash.is_some()) on:click=move |_| send.run(Ok(AuthoringRequest::Save))>"Sauvegarder"</button>
            <span class="gold-save-state" aria-live="polite">{move || if preview.get().is_some() { "Aperçu non appliqué" } else if state.get().is_some_and(|s| s.dirty) { "Non enregistré" } else { "" }}</span>
            <button hidden=move || preview.get().is_none() on:click=move |_| cancel_preview.run(())>"Abandonner l’aperçu"</button>
            <output class="authoring-command-diff" aria-live="polite" hidden=move || difference.get().is_empty()>{move || difference.get()}</output>
            <output class="authoring-command-status" aria-live="polite">{move || status.get()}</output>
        </div>
    }
}

fn accepted_request(
    request: Result<AuthoringRequest, String>,
    status: RwSignal<String>,
    state: RwSignal<Option<AuthoringState>>,
    events: RwSignal<Vec<ActionEvent>>,
) -> Option<AuthoringRequest> {
    let request = request.and_then(|request| {
        super::authoring_vegetation::check_promotion_apply(
            state.get_untracked().as_ref(),
            &request,
        )?;
        Ok(request)
    });
    match request {
        Ok(request) => Some(request),
        Err(error) => {
            record_event(events, "error", &error);
            status.set(error);
            None
        }
    }
}

fn restore_session(authoring_route: bool, send: Callback<Result<AuthoringRequest, String>>) {
    // Restore once per mount, without creating or saving. send reads untracked.
    Effect::new(move |_| {
        if authoring_route {
            send.run(Ok(AuthoringRequest::Inspect));
        }
    });
}

fn observe_result(state: &AuthoringState) -> Result<(), String> {
    super::authoring_raster::observe(state);
    let terrain = super::authoring_scene::observe(state)?;
    let identity = super::authoring_playtest::observe(state)?;
    super::authoring_vegetation::observe(state);
    super::authoring_path_projection::observe(state);
    super::authoring_building_projection::observe(state);
    super::authoring_assets::placements::observe(state);
    super::authoring_viewport::observe(terrain.as_ref(), identity);
    Ok(())
}

fn connect_shortcuts(
    authoring_route: bool,
    state: RwSignal<Option<AuthoringState>>,
    busy: RwSignal<bool>,
    status: RwSignal<String>,
    preview: RwSignal<Option<AuthoringRequest>>,
    send: Callback<Result<AuthoringRequest, String>>,
) {
    if !authoring_route {
        return;
    }
    super::authoring_shortcuts::install(Callback::new(move |shortcut| {
        if busy.get_untracked() {
            return;
        }
        let apply = if shortcut == super::editor_shortcut::EditorShortcut::Apply {
            preview.get_untracked()
        } else {
            None
        };
        match shortcut_request(state.get_untracked(), shortcut, apply) {
            Ok(request) => {
                super::authoring_brush_pointer::cancel();
                super::authoring_camera::cancel();
                send.run(Ok(request));
            }
            Err(reason) => status.set(reason),
        }
    }));
}

fn shortcut_request(
    state: Option<AuthoringState>,
    shortcut: super::editor_shortcut::EditorShortcut,
    preview: Option<AuthoringRequest>,
) -> Result<AuthoringRequest, String> {
    use super::editor_shortcut::EditorShortcut;
    let current = state.as_ref().ok_or("Aucune session disponible")?;
    if current.document.is_none() {
        return Err("Ouvrir une zone avant d’utiliser les raccourcis d’édition".into());
    }
    if current.playtest_hash.is_some() {
        return Err("Revenir à l’édition avant de sauvegarder ou modifier la zone".into());
    }
    match shortcut {
        EditorShortcut::Apply => preview
            .filter(|r| {
                matches!(
                    r,
                    AuthoringRequest::Apply { .. } | AuthoringRequest::ApplyZone { .. }
                )
            })
            .ok_or_else(|| "Prévisualiser une retouche avant Ctrl+Entrée".into()),
        EditorShortcut::Save => Ok(AuthoringRequest::Save),
        EditorShortcut::Undo if current.can_undo => prepare_edit(state, TerrainAction::Undo),
        EditorShortcut::Redo if current.can_redo => prepare_edit(state, TerrainAction::Redo),
        _ => Err("Aucune retouche à annuler ou rétablir".into()),
    }
}

fn replaces_projection(request: &AuthoringRequest) -> bool {
    super::authoring_viewport::requested()
        && super::authoring_viewport::has_projection()
        && matches!(
            request,
            AuthoringRequest::Create { .. }
                | AuthoringRequest::CreateZone { .. }
                | AuthoringRequest::Open { .. }
                | AuthoringRequest::OpenManaged { .. }
                | AuthoringRequest::BeginZoneMigration { .. }
                | AuthoringRequest::Close
                | AuthoringRequest::StartPlaytest { .. }
                | AuthoringRequest::StartPlaytestAt { .. }
                | AuthoringRequest::StopPlaytest { .. }
        )
}

fn sync_fields(
    state: &AuthoringState,
    is_preview: bool,
    name: RwSignal<String>,
    elevation: RwSignal<String>,
) {
    if !is_preview && let Some(document) = &state.document {
        name.set(document.name.clone());
        elevation.set(document.height_sampling.base_elevation_m.to_string());
    }
}

fn session_status(state: &AuthoringState) -> &'static str {
    if state.playtest_hash.is_some() {
        "Playtest · sources gelées, aucune sauvegarde implicite"
    } else if state.document.is_none() {
        "Aucune zone ouverte"
    } else if state.dirty {
        "Retouches non sauvegardées"
    } else {
        "Session synchronisée"
    }
}

pub(super) fn prepare_playtest(state: Option<AuthoringState>) -> Result<AuthoringRequest, String> {
    let state = state.ok_or("Ouvrir une zone avant de jouer")?;
    let document = state
        .document
        .as_ref()
        .ok_or("Ouvrir une zone avant de jouer")?;
    let expected_hash = state.zone_head.as_ref().map_or_else(
        || document.content_hash().map_err(|error| error.to_string()),
        forge_project::zone_authoring::ZoneAuthoringHead::content_hash,
    )?;
    Ok(AuthoringRequest::StartPlaytest { expected_hash })
}

fn prepare_brush(
    state: Option<AuthoringState>,
    brush: Result<forge_world::terrain_brush_stroke::TerrainBrushStroke, String>,
) -> Result<AuthoringRequest, String> {
    let brush = brush?;
    let document = state
        .as_ref()
        .and_then(|state| state.document.as_ref())
        .ok_or("Ouvrir une zone avant de peindre son relief")?;
    let edit = match state.as_ref().and_then(|s| s.zone_head.as_ref()) {
        Some(head) => forge_world::river_projection::prepare_brush(head, &brush)?,
        None => brush.prepare(document)?,
    };
    prepare_edit(state, TerrainAction::Edit(edit))
}

fn reload_projection() -> Result<(), String> {
    web_sys::window()
        .ok_or_else(|| "Fenêtre navigateur indisponible".to_owned())?
        .location()
        .reload()
        .map_err(|error| format!("Rechargement de la projection refusé : {error:?}"))
}

fn edit_label(edit: &TerrainEdit) -> String {
    match edit {
        TerrainEdit::SetHeightDeltas { samples } => {
            format!("Relief : {} échantillons retouchés", samples.len())
        }
        TerrainEdit::SetSchemaVersion { version } => {
            format!("Migration explicite vers le format terrain v{version}")
        }
        TerrainEdit::SetMaterialOverrides { samples } => {
            format!("Matériaux : {} cases corrigées", samples.len())
        }
        TerrainEdit::Rename { name } => format!("Nom : {name}"),
        TerrainEdit::SetBaseElevation { metres } => format!("Altitude : {metres} m"),
    }
}

fn preview_difference(
    state: &AuthoringState,
    receipt: &forge_project::terrain_edit::TerrainEditReceipt,
) -> String {
    format!(
        "{} → {}{}",
        edit_label(&receipt.inverse),
        edit_label(&receipt.applied),
        super::authoring_preview::prepare(state, receipt)
    )
}

fn prepare_edit(
    state: Option<AuthoringState>,
    action: TerrainAction,
) -> Result<AuthoringRequest, String> {
    let state = state.ok_or("Aucune session disponible")?;
    if let Some(head) = state.zone_head {
        return super::authoring_zone::prepare_terrain(&head, action);
    }
    let document = state
        .document
        .ok_or("Ouvrir une zone avant de la retoucher")?;
    let hash = document.content_hash().map_err(|error| error.to_string())?;
    let identity = serde_json::to_string(&(hash, &action)).map_err(|error| error.to_string())?;
    let is_edit = matches!(action, TerrainAction::Edit(_));
    let request = TerrainEditRequest {
        command_id: CommandId::deterministic(identity),
        expected_revision: document.revision,
        expected_hash: hash,
        action,
    };
    Ok(if is_edit {
        AuthoringRequest::Preview { request }
    } else {
        AuthoringRequest::Apply { request }
    })
}

fn reviewed_preview(
    state: &AuthoringState,
    request: Option<&AuthoringRequest>,
) -> Option<(AuthoringRequest, String)> {
    match request? {
        AuthoringRequest::Preview { request } => {
            let receipt = state.receipt.as_ref().filter(|r| r.request == *request)?;
            Some((
                AuthoringRequest::Apply {
                    request: request.clone(),
                },
                preview_difference(state, receipt),
            ))
        }
        AuthoringRequest::PreviewZone { request } => {
            let receipt = state
                .zone_receipt
                .as_ref()
                .filter(|r| r.request == *request)?;
            Some((
                AuthoringRequest::ApplyZone {
                    request: request.clone(),
                },
                super::authoring_zone::preview_label(state, receipt),
            ))
        }
        _ => None,
    }
}
