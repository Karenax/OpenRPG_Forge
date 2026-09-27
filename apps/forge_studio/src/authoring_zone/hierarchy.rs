//! Derived authoring index, not a second scene or a durable selection.
use forge_core::EntityId;
use forge_project::authoring::AuthoringState;
use leptos::prelude::*;

use crate::authoring_index_page::{PAGE_SIZE, page_range};
use crate::authoring_scene_index::{Target, rows};

#[component]
pub(crate) fn Hierarchy(
    state: RwSignal<Option<AuthoringState>>,
    busy: RwSignal<bool>,
    active_tab: RwSignal<crate::authoring_controls::AuthoringTab>,
    path: RwSignal<String>,
    river: RwSignal<String>,
    building: RwSignal<String>,
    placement: RwSignal<Option<EntityId>>,
    vegetation: RwSignal<String>,
) -> impl IntoView {
    let filter = RwSignal::new(String::new());
    let page = RwSignal::new(0_usize);
    let entries = Memo::new(move |_| {
        state
            .get()
            .and_then(|s| s.zone_head)
            .map(|head| rows(&head, &filter.get()))
            .unwrap_or_default()
    });
    let range = Memo::new(move |_| page_range(entries.with(Vec::len), page.get()));
    view! {
        <Show when=move || state.get().is_some_and(|s| s.zone_head.is_some())>
            <details open><summary>"Objets et couches de la zone"</summary>
                <label>"Rechercher dans la scène"
                    <input prop:value=move || filter.get() on:input=move |ev| {
                        page.set(0);
                        filter.set(event_target_value(&ev));
                    }/>
                </label>
                <fieldset disabled=move || busy.get() || state.get().is_none_or(|s| s.playtest_hash.is_some())>
                    <p aria-live="polite">{move || {
                        let range = range.get();
                        let first = if range.is_empty() { 0 } else { range.start + 1 };
                        format!("Objets {first}–{} sur {}", range.end, entries.with(Vec::len))
                    }}</p>
                    <button hidden=move || entries.with(Vec::len) <= PAGE_SIZE disabled=move || range.get().start == 0
                        on:click=move |_| page.set((range.get_untracked().start / PAGE_SIZE).saturating_sub(1))>"Objets précédents"</button>
                    <button hidden=move || entries.with(Vec::len) <= PAGE_SIZE disabled=move || { range.get().end >= entries.with(Vec::len) }
                        on:click=move |_| page.set(range.get_untracked().start / PAGE_SIZE + 1)>"Objets suivants"</button>
                    {move || {
                        let entries = entries.with(|entries| entries[range.get()].to_vec());
                        if entries.is_empty() {
                            return view! { <p>"Aucun objet correspondant."</p> }.into_any();
                        }
                        view! { <ul>{entries.into_iter().map(|(target, label)| {
                            let selected = match &target { Target::Path(_) => Some(path), Target::River(_) => Some(river), Target::Building(_) => Some(building), Target::Vegetation(_) => Some(vegetation), Target::Placement(_) => None };
                            let destination = match &target {
                                Target::Path(_) => crate::authoring_controls::AuthoringTab::Routes,
                                Target::River(_) => crate::authoring_controls::AuthoringTab::Water,
                                Target::Vegetation(_) => crate::authoring_controls::AuthoringTab::Nature,
                                Target::Building(_) => crate::authoring_controls::AuthoringTab::Buildings,
                                Target::Placement(_) => crate::authoring_controls::AuthoringTab::Objects,
                            };
                            let placement_id = match &target { Target::Placement(id) => Some(*id), _ => None };
                            let active_id = target.id();
                            let title = active_id.clone();
                            view! { <li><button title=title
                                aria-pressed=move || selected.map_or_else(|| placement.get() == placement_id, |selected| selected.get() == active_id).to_string()
                                on:click=move |_| {
                                    crate::authoring_shortcuts::cancel_preview();
                                    crate::authoring_brush_pointer::cancel();
                                    crate::authoring_building_projection::selection::cancel();
                                    crate::authoring_assets::placements::selection::cancel();
                                    if let Some(selected) = selected {
                                        selected.set(target.id());
                                    } else {
                                        crate::authoring_assets::placements::selection::choose(placement_id);
                                    }
                                    active_tab.set(destination);
                                }>{label}</button></li> }
                        }).collect_view()}</ul> }.into_any()
                    }}
                </fieldset>
            </details>
        </Show>
    }
}
