//! Project-owned game settings use the same reviewed workspace command path.
use super::{Creation, game_form::GameForm};
use forge_project::{
    VisualQualityTier,
    authoring::workspace::WorkspaceHostState,
    workspace::{
        ProjectEntry,
        launcher::{DisplayMode, GameLocation},
    },
};
use leptos::prelude::*;

fn project(state: Option<WorkspaceHostState>, id: &str) -> Option<ProjectEntry> {
    state?
        .head?
        .projects
        .into_iter()
        .find(|project| project.id.to_string() == id)
}

#[component]
pub(super) fn GameControls(
    state: RwSignal<Option<WorkspaceHostState>>,
    controls: Creation,
    draft_dirty: RwSignal<bool>,
    selected: RwSignal<String>,
) -> impl IntoView {
    let form = RwSignal::new(None::<GameForm>);
    let error = RwSignal::new(String::new());
    let registry = expect_context::<crate::authoring_controls::DraftRegistry>();
    let registration = registry.register();
    let observed_reset = StoredValue::new(registry.reset_generation());
    on_cleanup(move || registry.mark(registration, false));

    // Rebase only clean drafts, or a draft that was just accepted. A conflicting
    // external edit keeps the user's text until they explicitly reload it.
    Effect::new(move |_| {
        let loaded = state.get();
        let Some(current) = form.get_untracked() else {
            return;
        };
        let Some(project) = project(loaded, &current.project_id.to_string()) else {
            return;
        };
        let accepted = current
            .configuration()
            .is_ok_and(|game| project.game.as_ref() == Some(&game));
        if current.before != project.game && (!current.is_dirty() || accepted) {
            form.set(Some(GameForm::from_project(&project)));
        }
    });
    Effect::new(move |_| {
        let dirty = form.get().as_ref().is_some_and(GameForm::is_dirty);
        draft_dirty.set(dirty);
        registry.mark(registration, dirty);
    });
    Effect::new(move |_| {
        let generation = registry.reset_generation();
        if generation != observed_reset.get_value() {
            observed_reset.set_value(generation);
            controls.pending.set(None);
            let id = selected.get_untracked();
            form.set(
                project(state.get_untracked(), &id)
                    .as_ref()
                    .map(GameForm::from_project),
            );
            error.set(String::new());
        }
    });

    let conflict = move || {
        let Some(draft) = form.get() else {
            return false;
        };
        project(state.get(), &draft.project_id.to_string())
            .is_none_or(|project| project.game != draft.before)
    };
    let reload = move || {
        controls.pending.set(None);
        let id = selected.get_untracked();
        form.set(
            project(state.get_untracked(), &id)
                .as_ref()
                .map(GameForm::from_project),
        );
        error.set(String::new());
    };
    view! {
        <details class="workspace-game">
            <summary>"Configurer le jeu"</summary>
            <GameProjectSelect state=state selected=selected form=form error=error draft_dirty=draft_dirty/>
            <Show when=move || form.get().is_some()>
                <GameIdentityControls form=form/>
                <GameContentsControls state=state selected=selected form=form/>
                <PlayerDefaultsControls form=form/>
                <Show when=conflict><p role="alert">"La configuration a changé depuis son ouverture. Abandonnez les saisies pour la recharger."</p></Show>
                <Show when=move || !error.get().is_empty()><p role="alert">{move || error.get()}</p></Show>
                <button type="button" disabled=conflict on:click=move |_| {
                    let Some(draft) = form.get_untracked() else { return };
                    match draft.action() {
                        Ok(action) => {
                            error.set(String::new());
                            let name = project(state.get_untracked(), &draft.project_id.to_string()).map_or_else(|| "Projet".into(), |project| project.name);
                            controls.review(action, format!("Configurer le jeu du projet « {name} »"));
                        }
                        Err(reason) => error.set(reason),
                    }
                }>"Examiner la configuration du jeu"</button>
                <Show when=move ||draft_dirty.get() || conflict()>
                    <button type="button" on:click=move |_|reload()>"Abandonner les saisies du jeu"</button>
                </Show>
            </Show>
        </details>
    }
}

#[component]
fn GameContentsControls(
    state: RwSignal<Option<WorkspaceHostState>>,
    selected: RwSignal<String>,
    form: RwSignal<Option<GameForm>>,
) -> impl IntoView {
    view! {
                <fieldset>
                    <legend>"Cartes incluses dans le jeu"</legend>
                    <Show when=move || project(state.get(), &selected.get()).is_some_and(|project| project.modules.is_empty())>
                        <p>"Créez un module et une carte pour configurer ce jeu."</p>
                    </Show>
                    {move || {
                        let loaded = state.get();
                        let Some(project) = project(loaded.clone(), &selected.get()) else { return Vec::new() };
                        let labels = loaded.map_or_else(Vec::new, |state| state.modules);
                        project.modules.into_iter().map(|module| {
                            let id = module.id;
                            let names = labels.iter().find(|item| item.id == id).map(|item| item.maps.clone()).unwrap_or_default();
                            let empty = module.zones.is_empty();
                            let maps = module.zones.into_iter().map(|zone| {
                                let area = zone.area_id;
                                let name = names.iter().find(|map| map.area_id == area).map_or_else(|| format!("Carte {area}"), |map| map.name.clone());
                                view! {
                                    <label><input type="checkbox"
                                        prop:checked=move || form.get().as_ref().is_some_and(|draft| draft.includes(id, area))
                                        on:change=move |event| form.update(|draft| if let Some(draft) = draft { draft.set_area(id, area, event_target_checked(&event)); })/>{name}</label>
                                }
                            }).collect_view();
                            view! { <fieldset><legend>{module.name}</legend>
                                <Show when=move ||empty><p>"Ce module ne contient aucune carte."</p></Show>{maps}
                            </fieldset> }
                        }).collect::<Vec<_>>()
                    }}
                </fieldset>
                <label>"Carte de départ"
                    <select prop:value=move || form.get().and_then(|draft| draft.fields.entry).map_or_else(String::new, GameForm::entry_key)
                        on:change=move |event| {
                            let value = event_target_value(&event);
                            form.update(|draft| if let Some(draft) = draft {
                                draft.fields.entry = draft.resolve_entry(&value);
                            });
                        }>
                        <option value="">"Choisir une carte…"</option>
                        {move || {
                            let loaded = state.get();
                            let Some(project) = project(loaded.clone(), &selected.get()) else { return Vec::new() };
                            let labels = loaded.map_or_else(Vec::new, |state| state.modules);
                            let draft = form.get();
                            project.modules.into_iter().flat_map(|module| {
                                let module_id = module.id;
                                let names = labels.iter().find(|item| item.id == module_id).map(|item| item.maps.clone()).unwrap_or_default();
                                let module_name = module.name;
                                let draft = draft.clone();
                                module.zones.into_iter().filter_map(move |zone| {
                                    let area = zone.area_id;
                                    if !draft.as_ref().is_some_and(|form| form.includes(module_id, area)) { return None; }
                                    let name = names.iter().find(|map| map.area_id == area).map_or_else(|| format!("Carte {area}"), |map| map.name.clone());
                                    let key = GameForm::entry_key(GameLocation { module_id, area_id: area });
                                    Some(view! { <option value=key>{format!("{module_name} · {name}")}</option> })
                                })
                            }).collect::<Vec<_>>()
                        }}
                    </select>
                </label>
                <Show when=move || {
                    let Some(draft) = form.get() else { return false };
                    let Some(project) = project(state.get(), &selected.get()) else { return false };
                    draft.fields.contents.iter().any(|content| project.modules.iter().find(|module| module.id == content.module_id)
                        .is_some_and(|module| content.areas.len() != module.zones.len()))
                }><p>"L’export du jeu demande actuellement toutes les cartes de chaque module sélectionné."</p></Show>
    }
}

#[component]
fn PlayerDefaultsControls(form: RwSignal<Option<GameForm>>) -> impl IntoView {
    view! {
                <fieldset>
                    <legend>"Réglages initiaux du joueur"</legend>
                    <p>"F11 bascule entre fenêtre et plein écran sans bordure. Le plein écran exclusif n’est pas encore appliqué : le jeu reste en fenêtre."</p>
                    <p>"La qualité du joueur est prioritaire. Les volumes, les sous-titres et la réduction des animations sont enregistrés, mais pas encore appliqués."</p>
                    <label>"Qualité graphique"
                        <select prop:value=move || form.get().map_or("ultra", |draft| match draft.fields.defaults.quality {
                            VisualQualityTier::Low => "low", VisualQualityTier::Medium => "medium", VisualQualityTier::High => "high", VisualQualityTier::Ultra => "ultra",
                        }) on:change=move |event| form.update(|draft| if let Some(draft) = draft {
                            draft.fields.defaults.quality = match event_target_value(&event).as_str() {
                                "low" => VisualQualityTier::Low, "medium" => VisualQualityTier::Medium, "high" => VisualQualityTier::High, _ => VisualQualityTier::Ultra,
                            };
                        })>
                            <option value="low">"Faible"</option><option value="medium">"Moyenne"</option><option value="high">"Élevée"</option><option value="ultra">"Ultra"</option>
                        </select>
                    </label>
                    <label>"Affichage"
                        <select prop:value=move || form.get().map_or("borderless", |draft| match draft.fields.defaults.display_mode {
                            DisplayMode::Windowed => "windowed", DisplayMode::Borderless => "borderless", DisplayMode::Fullscreen => "fullscreen",
                        }) on:change=move |event| form.update(|draft| if let Some(draft) = draft {
                            draft.fields.defaults.display_mode = match event_target_value(&event).as_str() {
                                "windowed" => DisplayMode::Windowed, "fullscreen" => DisplayMode::Fullscreen, _ => DisplayMode::Borderless,
                            };
                        })>
                            <option value="windowed">"Fenêtré"</option><option value="borderless">"Fenêtré plein écran"</option><option value="fullscreen">"Plein écran"</option>
                        </select>
                    </label>
                    <label>"Volume principal "{move || form.get().map_or(82, |draft| draft.fields.defaults.master_volume)}
                        <input type="range" min="0" max="100" prop:value=move || form.get().map_or(82, |draft| draft.fields.defaults.master_volume).to_string()
                            on:input=move |event| { if let Ok(value) = event_target_value(&event).parse::<u8>() { form.update(|draft| if let Some(draft) = draft { draft.fields.defaults.master_volume = value; }); } }/>
                    </label>
                    <label>"Volume de la musique "{move || form.get().map_or(64, |draft| draft.fields.defaults.music_volume)}
                        <input type="range" min="0" max="100" prop:value=move || form.get().map_or(64, |draft| draft.fields.defaults.music_volume).to_string()
                            on:input=move |event| { if let Ok(value) = event_target_value(&event).parse::<u8>() { form.update(|draft| if let Some(draft) = draft { draft.fields.defaults.music_volume = value; }); } }/>
                    </label>
                    <label><input type="checkbox" prop:checked=move || form.get().is_some_and(|draft| draft.fields.defaults.reduce_motion)
                        on:change=move |event| form.update(|draft| if let Some(draft) = draft { draft.fields.defaults.reduce_motion = event_target_checked(&event); })/>"Réduire les animations"</label>
                    <label><input type="checkbox" prop:checked=move || form.get().is_some_and(|draft| draft.fields.defaults.subtitles)
                        on:change=move |event| form.update(|draft| if let Some(draft) = draft { draft.fields.defaults.subtitles = event_target_checked(&event); })/>"Sous-titres"</label>
                </fieldset>
    }
}

#[component]
fn GameProjectSelect(
    state: RwSignal<Option<WorkspaceHostState>>,
    selected: RwSignal<String>,
    form: RwSignal<Option<GameForm>>,
    error: RwSignal<String>,
    draft_dirty: RwSignal<bool>,
) -> impl IntoView {
    view! {
            <label>"Projet du jeu"
                <select prop:value=move || selected.get() disabled=move || draft_dirty.get()
                    on:change=move |event| {
                        let value = event_target_value(&event);
                        let next = project(state.get_untracked(), &value);
                        selected.set(value);
                        form.set(next.as_ref().map(GameForm::from_project));
                        error.set(String::new());
                    }>
                    <option value="">"Choisir un projet…"</option>
                    {move || state.get().and_then(|state| state.head).map(|head| head.projects.into_iter().map(|project| view! {
                        <option value=project.id.to_string()>{project.name}</option>
                    }).collect_view())}
                </select>
            </label>
    }
}

#[component]
fn GameIdentityControls(form: RwSignal<Option<GameForm>>) -> impl IntoView {
    view! {
                <label>"Titre du jeu"
                    <input maxlength="256" prop:value=move || form.get().map_or_else(String::new, |form| form.fields.title)
                        on:input=move |event| form.update(|draft| if let Some(draft) = draft { draft.fields.title = event_target_value(&event); })/>
                </label>
                <label>"Résumé"
                    <textarea rows="3" maxlength="4096" prop:value=move || form.get().map_or_else(String::new, |form| form.fields.summary)
                        on:input=move |event| form.update(|draft| if let Some(draft) = draft { draft.fields.summary = event_target_value(&event); })></textarea>
                </label>
                <super::game_visuals::GameVisuals form=form/>
    }
}
