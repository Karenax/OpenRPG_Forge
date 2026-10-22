//! Container forms submit one candidate through the common gameplay preview.
use super::{Edit, edit, id};
use forge_core::EntityId;
use forge_gameplay::{ContainerDefinition, CreatorSurface, ObjectKind};
use forge_project::authoring::AuthoringState;
use leptos::prelude::*;
use std::collections::{BTreeMap, BTreeSet};

#[component]
#[allow(
    clippy::too_many_lines,
    reason = "One local form prepares a single atomic container preview"
)]
pub(super) fn ContainerControls(
    state: RwSignal<Option<AuthoringState>>,
    busy: RwSignal<bool>,
    send: Callback<Edit>,
    selected: RwSignal<String>,
) -> impl IntoView {
    let document = move || {
        state
            .get()
            .and_then(|s| s.zone_head)
            .and_then(|h| h.corrections.gameplay.map(|d| *d))
    };
    let capacity = RwSignal::new("6".to_owned());
    let key = RwSignal::new(String::new());
    let item = RwSignal::new(String::new());
    let quantity = RwSignal::new("1".to_owned());
    let contents = RwSignal::new(BTreeMap::<EntityId, u32>::new());
    let filters = RwSignal::new(BTreeSet::<EntityId>::new());
    Effect::new(move |previous: Option<super::FieldSource>| {
        let current = (selected.get(), super::source_hash(state));
        if previous.as_ref() == Some(&current) {
            return current;
        }
        if let Some(definition) = document().and_then(|d| {
            id(&selected.get())
                .ok()
                .and_then(|object| d.container_definition(object).cloned())
        }) {
            capacity.set(definition.capacity_units.to_string());
            contents.set(definition.initial_items);
            key.set(
                definition
                    .key
                    .map_or_else(String::new, |key| key.to_string()),
            );
            filters.set(definition.accepts.into_iter().collect());
        } else {
            capacity.set("6".into());
            contents.set(BTreeMap::new());
            filters.set(BTreeSet::new());
            key.set(String::new());
        }
        current
    });
    let disabled = move || {
        busy.get()
            || state
                .get()
                .is_none_or(|s| s.zone_head.is_none() || s.playtest_hash.is_some())
    };
    view! {
        <Show when=move ||document().is_some_and(|d|d.surface_visible(CreatorSurface::Containers))>
            <details class="creator-containers"><summary>"Conteneurs et contenu initial"</summary>
                <fieldset disabled=disabled>
                    <p>"Choisir un objet physique ou un conteneur. Pour créer un coffre, ajouter d’abord un objet physique. Son identité, son nom, sa position et ses propriétés physiques seront conservés."</p>
                    <label>"Objet du conteneur"<select prop:value=move ||selected.get() on:change=move |e|selected.set(event_target_value(&e))>
                        <option value="">"Choisir un objet"</option>
                        {move ||document().map(|d|d.objects.into_iter().filter(|o|matches!(o.kind,ObjectKind::Prop | ObjectKind::Container { .. }))
                            .map(|o|view!{<option value=o.id.to_string()>{o.name}</option>}).collect_view())}
                    </select></label>
                    <label>"Capacité (unités d’items)"<input type="number" min="0" max=forge_gameplay::MAX_INVENTORY_UNITS.to_string() step="1" prop:value=move ||capacity.get() on:input=move |e|capacity.set(event_target_value(&e))/></label>
                    <p>"La capacité compte les quantités, avec la même limite de pile que le catalogue. Ce profil ne calcule pas un poids."</p>
                    <label>"Clé initiale"<select prop:value=move ||key.get() on:change=move |e|key.set(event_target_value(&e))>
                        <option value="">"Sans serrure"</option>
                        {move ||document().map(|d|d.items.into_iter().map(|i|view!{<option value=i.id.to_string()>{i.name}</option>}).collect_view())}
                    </select></label>
                    <p>"Une clé choisie rend le conteneur verrouillé au début de la partie. La clé doit être possédée pour ouvrir ou verrouiller ; elle n’est pas consommée."</p>
                    <label>"Item à préparer"<select prop:value=move ||item.get() on:change=move |e|item.set(event_target_value(&e))>
                        <option value="">"Choisir dans le catalogue"</option>
                        {move ||document().map(|d|d.items.into_iter().map(|i|view!{<option value=i.id.to_string()>{format!("{} · pile max. {}",i.name,i.max_stack)}</option>}).collect_view())}
                    </select></label>
                    <label>"Quantité initiale"<input type="number" min="1" step="1" prop:value=move ||quantity.get() on:input=move |e|quantity.set(event_target_value(&e))/></label>
                    <button on:click=move |_| {
                        let prepared = (|| {
                            let item = id(&item.get_untracked())?;
                            let amount = quantity.get_untracked().trim().parse::<u32>().ok().filter(|value|*value > 0)
                                .ok_or("Saisir une quantité entière strictement positive".to_owned())?;
                            Ok::<_,String>((item,amount))
                        })();
                        match prepared {
                            Ok((item,amount)) => contents.update(|items|{items.insert(item,amount);}),
                            Err(error) => send.run(Err(error)),
                        }
                    }>"Préparer cette pile"</button>
                    <ul>{move ||contents.get().into_iter().map(|(item,quantity)| {
                        let name = document().and_then(|d|d.item(item).map(|i|i.name.clone())).unwrap_or_else(||"Référence absente".into());
                        view!{<li>{format!("{name} × {quantity}")}<button on:click=move |_|contents.update(|items|{items.remove(&item);})>"Retirer de la préparation"</button></li>}
                    }).collect_view()}</ul>
                    <details><summary>"Filtre des dépôts"</summary>
                        <p>"Sans sélection, tous les items déclarés sont admis. Le contenu initial doit aussi respecter le filtre."</p>
                        {move ||document().map(|d|d.items.into_iter().map(|item| {
                            let id = item.id;
                            view!{<label><input type="checkbox" prop:checked=move ||filters.get().contains(&id) on:change=move |e|filters.update(|items|{
                                if event_target_checked(&e) {items.insert(id);} else {items.remove(&id);}
                            })/>{item.name}</label>}
                        }).collect_view())}
                    </details>
                    <p>"Ces champs préparent un candidat. Examiner puis appliquer pour enregistrer dans l’historique. Modifier le contenu initial, le filtre ou la clé peut rendre une sauvegarde de partie incompatible."</p>
                    <button on:click=move |_|send.run(edit(state.get_untracked(),move |_,source| {
                        let definition = ContainerDefinition {
                            capacity_units: capacity.get_untracked().trim().parse().map_err(|_|"La capacité doit être un entier positif ou nul.".to_owned())?,
                            initial_items: contents.get_untracked(), accepts: filters.get_untracked().into_iter().collect(),
                            key: if key.get_untracked().is_empty() {None} else {Some(id(&key.get_untracked())?)},
                        };
                        *source = source.with_container_definition(id(&selected.get_untracked())?,definition).map_err(|e|e.to_string())?;
                        Ok(())
                    }))>"Examiner le conteneur et son contenu initial"</button>
                </fieldset>
            </details>
        </Show>
    }
}
