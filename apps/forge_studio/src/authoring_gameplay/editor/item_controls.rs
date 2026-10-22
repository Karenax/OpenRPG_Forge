//! The catalog uses the same item definitions as pickup, container and quest commands.
use super::{Edit, edit, id};
use forge_core::{EntityId, canonical_json_sha256};
use forge_gameplay::{CreatorSurface, ItemDefinition, MAX_INVENTORY_UNITS};
use forge_project::authoring::AuthoringState;
use leptos::prelude::*;

#[component]
pub(super) fn ItemControls(
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
    let selected = RwSignal::new(String::new());
    let name = RwSignal::new("Nouvel item".to_owned());
    let stack = RwSignal::new("10".to_owned());
    Effect::new(move |previous: Option<super::FieldSource>| {
        let current = (selected.get(), super::source_hash(state));
        if previous.as_ref() == Some(&current) {
            return current;
        }
        if let Some(item) = document().and_then(|d| {
            id(&selected.get())
                .ok()
                .and_then(|key| d.item(key).cloned())
        }) {
            name.set(item.name);
            stack.set(item.max_stack.to_string());
        }
        current
    });
    let max_stack =
        move || {
            stack.get_untracked().trim().parse::<u32>().map_err(|_| {
                "Saisir une limite de pile entière et strictement positive.".to_owned()
            })
        };
    let disabled = move || {
        busy.get()
            || state
                .get()
                .is_none_or(|s| s.zone_head.is_none() || s.playtest_hash.is_some())
    };
    view! {
        <Show when=move ||document().is_some_and(|d|d.surface_visible(CreatorSurface::Objects) || d.surface_visible(CreatorSurface::Containers))>
            <details class="creator-item-catalog"><summary>"Catalogue des items"</summary>
                <fieldset disabled=disabled>
                    <p>"Les ramassages, quêtes, clés et conteneurs utilisent ce catalogue commun. Créer un item ne place pas de ramassage dans la scène."</p>
                    <label>"Item à retoucher"<select prop:value=move ||selected.get() on:change=move |e|selected.set(event_target_value(&e))>
                        <option value="">"Choisir un item"</option>
                        {move ||document().map(|d|d.items.into_iter().map(|item|view!{<option value=item.id.to_string()>{item.name}</option>}).collect_view())}
                    </select></label>
                    <label>"Nom de l’item"<input prop:value=move ||name.get() on:input=move |e|name.set(event_target_value(&e))/></label>
                    <label>"Limite d’une pile (unités)"<input type="number" min="1" max=MAX_INVENTORY_UNITS.to_string() step="1" prop:value=move ||stack.get() on:input=move |e|stack.set(event_target_value(&e))/></label>
                    <button on:click=move |_|send.run(edit(state.get_untracked(),move |_,source| {
                        let hash = canonical_json_sha256(source).map_err(|e|e.to_string())?;
                        let item = ItemDefinition {id:EntityId::deterministic(format!("catalog-item/{}/{hash}",source.area_id)),name:name.get_untracked(),max_stack:max_stack()?};
                        *source = source.with_item_definition(item).map_err(|e|e.to_string())?;
                        Ok(())
                    }))>"Examiner un nouvel item"</button>
                    <button on:click=move |_|send.run(edit(state.get_untracked(),move |_,source| {
                        let item = ItemDefinition {id:id(&selected.get_untracked())?,name:name.get_untracked(),max_stack:max_stack()?};
                        if source.item(item.id).is_none() { return Err("Choisir un item courant".into()); }
                        *source = source.with_item_definition(item).map_err(|e|e.to_string())?;
                        Ok(())
                    }))>"Examiner la retouche de l’item"</button>
                    <p>"Une pile initiale devenue trop grande ou une référence encore utilisée fait refuser la modification. Les changements passent par l’aperçu et l’historique. Changer la limite ou supprimer un item peut rendre une sauvegarde incompatible."</p>
                    <button on:click=move |_|send.run(edit(state.get_untracked(),move |_,source| {
                        *source = source.without_item_definition(id(&selected.get_untracked())?).map_err(|e|e.to_string())?;
                        Ok(())
                    }))>"Examiner la suppression de l’item inutilisé"</button>
                </fieldset>
            </details>
        </Show>
    }
}
