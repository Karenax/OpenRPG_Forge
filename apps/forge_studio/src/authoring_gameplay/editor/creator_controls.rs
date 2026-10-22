//! The normal gameplay preview command owns all changes from these controls.
use super::{Edit, edit, source_hash};
use forge_gameplay::{CreatorSurface, GameplayCapability, GameplayDocument};
use forge_project::authoring::{AuthoringRequest, AuthoringState};
use leptos::prelude::*;
use std::collections::BTreeSet;

#[component]
pub(super) fn CreatorControls(
    state: RwSignal<Option<AuthoringState>>,
    busy: RwSignal<bool>,
    send: Callback<Edit>,
) -> impl IntoView {
    let document = move || {
        state
            .get()
            .and_then(|s| s.zone_head)
            .and_then(|h| h.corrections.gameplay.map(|d| *d))
    };
    let surfaces = RwSignal::new(BTreeSet::<CreatorSurface>::new());
    let capabilities = RwSignal::new(BTreeSet::<GameplayCapability>::new());
    Effect::new(move |previous| {
        let current = source_hash(state);
        if previous == Some(current) {
            return current;
        }
        if let Some(source) = document() {
            surfaces.set(
                source
                    .creator_configuration()
                    .surfaces
                    .into_iter()
                    .collect(),
            );
            capabilities.set(
                source
                    .creator_configuration()
                    .required_capabilities
                    .into_iter()
                    .collect(),
            );
        }
        current
    });
    let disabled = move || {
        busy.get() || document().is_none() || state.get().is_some_and(|s| s.playtest_hash.is_some())
    };
    let managed = move || {
        state
            .get()
            .is_some_and(|s| s.module_creator_context.is_some())
    };
    view! {
        <Show when=move ||document().is_some()>
            <details class="creator-configuration"><summary>{move || if managed() {"Surfaces et capacités du module"} else {"Surfaces et capacités de la carte"}}</summary>
                <fieldset disabled=disabled>
                    <p>"Masquer un outil conserve ses données et les règles du jeu. Les changements passent par l’aperçu et l’historique habituels."</p>
                    <Show when=managed>
                        <p>{move ||state.get().and_then(|s|s.module_creator_context).map(|context|format!("Module : {}. L’aperçu indique les cartes concernées avant toute application.",context.name))}</p>
                        <Show when=move ||state.get().and_then(|s|s.module_creator_context).is_some_and(|context|context.definition.is_none())>
                            <p>"Ce module utilise encore les profils de ses cartes. L’application d’une configuration commune alignera leurs règles et leurs outils."</p>
                        </Show>
                    </Show>
                    {CreatorSurface::ALL.into_iter().map(|surface| view! {
                        <label><input type="checkbox" prop:checked=move ||surfaces.get().contains(&surface)
                            on:change=move |event| surfaces.update(|selection| {
                                if event_target_checked(&event) { selection.insert(surface); }
                                else { selection.remove(&surface); }
                            })/>{surface.label()}</label>
                    }).collect_view()}
                    <p>"Capacités à déclarer : les données existantes conservent leurs exigences, même lorsque leur outil est masqué."</p>
                    {GameplayCapability::IMPLEMENTED.into_iter().map(|capability| view! {
                        <label><input type="checkbox" prop:checked=move ||capabilities.get().contains(&capability)
                            on:change=move |event|capabilities.update(|selection| {
                                if event_target_checked(&event) {selection.insert(capability);} else {selection.remove(&capability);}
                            })/>{capability.label()}</label>
                    }).collect_view()}
                    <button on:click=move |_| {
                        let selected = surfaces.get_untracked();
                        let requested = capabilities.get_untracked();
                        send.run(creator_edit(state.get_untracked(), move |source| {
                            let mut config = source.creator_configuration();
                            let mut required = source.used_capabilities();
                            required.extend(requested);
                            required.extend(selected.iter().filter_map(|s|s.capability()));
                            config.required_capabilities = required.into_iter().collect();
                            config.surfaces = selected.into_iter().collect();
                            *source = source.with_creator_configuration(config).map_err(|e|e.to_string())?;
                            Ok(())
                        }));
                    }>{move ||if managed() {"Examiner les surfaces du module"} else {"Examiner les surfaces de la carte"}}</button>
                    <p>"Capacités disponibles dans ce runtime : "{GameplayCapability::IMPLEMENTED.into_iter().map(GameplayCapability::label).collect::<Vec<_>>().join(", ")}</p>
                    <p>{move ||document().map(|d| format!("Capacités déclarées : {}",d.creator_configuration().required_capabilities.into_iter().map(GameplayCapability::label).collect::<Vec<_>>().join(", ")))}</p>
                    <Show when=managed>
                        <button disabled=move ||disabled() ||state.get().and_then(|s|s.module_creator_context).is_none_or(|context|!context.can_undo)
                            on:click=move |_|send.run(module_history(state.get_untracked(),false))>"Examiner l’annulation du module"</button>
                        <button disabled=move ||disabled() ||state.get().and_then(|s|s.module_creator_context).is_none_or(|context|!context.can_redo)
                            on:click=move |_|send.run(module_history(state.get_untracked(),true))>"Examiner le rétablissement du module"</button>
                    </Show>
                </fieldset>
            </details>
            <RulesControls state=state busy=busy send=send/>
        </Show>
    }
}

#[component]
fn RulesControls(
    state: RwSignal<Option<AuthoringState>>,
    busy: RwSignal<bool>,
    send: Callback<Edit>,
) -> impl IntoView {
    let document = move || {
        state
            .get()
            .and_then(|state| state.zone_head)
            .and_then(|head| head.corrections.gameplay.map(|document| *document))
    };
    let rule_index = RwSignal::new(0_usize);
    let value = RwSignal::new(String::new());
    let profile_id = RwSignal::new(String::new());
    let profile_version = RwSignal::new(String::new());
    Effect::new(move |previous| {
        let current = source_hash(state);
        if previous == Some(current) {
            return current;
        }
        if let Some(source) = document() {
            profile_id.set(source.profile.id);
            profile_version.set(source.profile.version.to_string());
        }
        current
    });
    let field = move || {
        document().and_then(|document| {
            document
                .profile
                .rule_fields()
                .get(rule_index.get())
                .cloned()
        })
    };
    Effect::new(move |previous| {
        let current = (rule_index.get(), source_hash(state));
        if previous != Some(current) {
            value.set(field().map_or_else(String::new, |field| field.value));
        }
        current
    });
    let visible =
        move |surface| document().is_some_and(|document| document.surface_visible(surface));
    let disabled = move || {
        busy.get()
            || document().is_none()
            || state
                .get()
                .is_some_and(|state| state.playtest_hash.is_some())
    };
    view! {
            <Show when=move ||visible(CreatorSurface::Rules)>
                <details class="creator-rules"><summary>"Profil de règles et aide"</summary>
                    <fieldset disabled=disabled>
                        <p>{format!("Contrat exécuté : {}. Profil minimal ; le manuel complet n’est pas implémenté.",forge_gameplay::RulesProfile::CONTRACT)}</p>
                        <label>"Identifiant du profil"<input prop:value=move ||profile_id.get() on:input=move |e|profile_id.set(event_target_value(&e))/></label>
                        <label>"Version du profil"<input type="number" min="1" step="1" prop:value=move ||profile_version.get() on:input=move |e|profile_version.set(event_target_value(&e))/></label>
                        <button on:click=move |_|send.run(creator_edit(state.get_untracked(), move |source| {
                            source.profile.id = profile_id.get_untracked();
                            source.profile.version = profile_version.get_untracked().parse().map_err(|_|"La version doit être un entier strictement positif.".to_owned())?;
                            source.profile.validate().map_err(|e|e.to_string())
                        }))>"Examiner l’identité du profil"</button>
                        <label>"Règle"<select prop:value=move ||rule_index.get().to_string() on:change=move |event| {
                            if let Ok(index) = event_target_value(&event).parse::<usize>() { rule_index.set(index); }
                        }>{move ||document().map(|d|d.profile.rule_fields().into_iter().enumerate().map(|(index,f)|view!{<option value=index.to_string()>{f.label}</option>}).collect_view())}</select></label>
                        <p>{move ||field().map(|f|format!("{} — {} à {} {}. {}",f.label,f.minimum,f.maximum,f.unit.label(),f.help))}</p>
                        <label>"Valeur"<input type="number" step=move ||field().map_or("any",|f|if f.integer {"1"} else {"any"})
                            min=move ||field().map(|f|f.minimum.to_string()) max=move ||field().map(|f|f.maximum.to_string())
                            prop:value=move ||value.get() on:input=move |e|value.set(event_target_value(&e))/></label>
                        <p>"Une modification des règles peut rendre une sauvegarde antérieure incompatible ; aucune migration automatique n’est appliquée."</p>
                        <button on:click=move |_|send.run(creator_edit(state.get_untracked(), move |source| {
                            let key = source.profile.rule_fields().get(rule_index.get_untracked()).ok_or("Choisir une règle disponible")?.key;
                            source.profile = source.profile.with_rule_text(key,&value.get_untracked()).map_err(|e|e.to_string())?;
                            Ok(())
                        }))>"Examiner la règle"</button>
                    </fieldset>
                </details>
            </Show>
    }
}

fn creator_edit(
    state: Option<AuthoringState>,
    update: impl FnOnce(&mut GameplayDocument) -> Result<(), String>,
) -> Edit {
    let Some(context) = state
        .as_ref()
        .and_then(|state| state.module_creator_context.clone())
    else {
        return edit(state, move |_, source| update(source));
    };
    let mut source = state
        .and_then(|state| state.zone_head)
        .and_then(|head| head.corrections.gameplay.map(|document| *document))
        .ok_or("Ouvrir les données RPG d’une carte du module")?;
    update(&mut source)?;
    source.validate().map_err(|error| error.to_string())?;
    let surfaces = source.creator_configuration().surfaces;
    let required_capabilities = source.creator_configuration().required_capabilities;
    let hash =
        forge_core::canonical_json_sha256(&(&source.profile, &surfaces, &required_capabilities))
            .map_err(|error| error.to_string())?;
    Ok(AuthoringRequest::PreviewModuleCreator {
        expected_workspace_hash: context.workspace_hash,
        profile: source.profile,
        surfaces,
        required_capabilities,
        command_id: forge_core::CommandId::deterministic(format!(
            "module-creator/{}/{hash}",
            context.workspace_hash
        )),
    })
}

fn module_history(state: Option<AuthoringState>, redo: bool) -> Edit {
    let state = state.ok_or("Ouvrir une carte du module")?;
    if state.module_creator_context.is_none() {
        return Err("Ouvrir une carte du module".into());
    }
    crate::authoring_history_request::prepare(&state, redo)
}

pub(super) fn kind_visible(document: &GameplayDocument, kind: &str) -> bool {
    let surface = match kind {
        "friendly" | "enemy" => CreatorSurface::Actors,
        "place" => CreatorSurface::Places,
        "pickup" | "door" | "prop" => CreatorSurface::Objects,
        _ => return false,
    };
    document.surface_visible(surface)
}
