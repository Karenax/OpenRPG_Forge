//! Retouch an existing quest without replacing its solutions or dialogue graph.
use super::{Edit, edit, id};
use forge_gameplay::{ActorRole, QuestDefinition};
use forge_project::authoring::AuthoringState;
use leptos::prelude::*;

#[component]
pub(super) fn QuestDefinitionControls(
    state: RwSignal<Option<AuthoringState>>,
    busy: RwSignal<bool>,
    selected: RwSignal<String>,
    send: Callback<Edit>,
) -> impl IntoView {
    let name = RwSignal::new(String::new());
    let giver = RwSignal::new(String::new());
    let item = RwSignal::new(String::new());
    let quantity = RwSignal::new("1".to_owned());
    let reward = RwSignal::new(String::new());
    let reward_quantity = RwSignal::new("1".to_owned());
    let document = move || {
        state
            .get()
            .and_then(|s| s.zone_head)
            .and_then(|h| h.corrections.gameplay)
    };
    let current = move || {
        document().and_then(|document| {
            id(&selected.get())
                .ok()
                .and_then(|id| document.quest(id).cloned())
        })
    };
    let notice = super::draft::bind(
        move || {
            (
                selected.get(),
                serde_json::to_value(current()).unwrap_or_default(),
            )
        },
        vec![
            super::draft::Field::new(name),
            super::draft::Field::new(giver),
            super::draft::Field::new(item),
            super::draft::Field::new(quantity),
            super::draft::Field::new(reward),
            super::draft::Field::new(reward_quantity),
        ],
        move || {
            if let Some(quest) = current() {
                name.set(quest.name);
                giver.set(quest.giver.to_string());
                item.set(quest.required_item.to_string());
                quantity.set(quest.required_quantity.to_string());
                reward.set(
                    quest
                        .reward_item
                        .map_or_else(String::new, |item| item.to_string()),
                );
                reward_quantity.set(quest.reward_quantity.max(1).to_string());
            }
        },
    );
    let preview = notice.submitter(send);
    let disabled = move || {
        busy.get() || current().is_none() || state.get().is_none_or(|s| s.playtest_hash.is_some())
    };
    view! {
        <Show when=move ||current().is_some()>
            <details class="creator-quest-definition"><summary>"Réglages de la quête"</summary>
                <fieldset disabled=disabled><super::draft::DraftNotice notice=notice/>
                    <label>"Nom de la quête"<input prop:value=move ||name.get() on:input=move |event|name.set(event_target_value(&event))/></label>
                    <label>"PNJ donneur de la quête"<select prop:value=move ||giver.get() on:change=move |event|giver.set(event_target_value(&event))>
                        <option value="">"Choisir un personnage"</option>
                        {move ||document().map(|document|document.actors.into_iter().filter(|actor|actor.role==ActorRole::Friendly).map(|actor|view!{<option value=actor.id.to_string()>{actor.name}</option>}).collect_view())}
                    </select></label>
                    <label>"Objet demandé pour une récupération simple"<select prop:value=move ||item.get() on:change=move |event|item.set(event_target_value(&event))>
                        <option value="">"Choisir un objet d’inventaire"</option>
                        {move ||document().map(|document|document.items.into_iter().map(|item|view!{<option value=item.id.to_string()>{item.name}</option>}).collect_view())}
                    </select></label>
                    <label>"Quantité demandée"<input type="number" min="1" step="1" aria-describedby="gameplay-field-error" prop:value=move ||quantity.get() on:input=move |event|quantity.set(event_target_value(&event))/></label>
                    <label>"Récompense de la quête"<select prop:value=move ||reward.get() on:change=move |event|reward.set(event_target_value(&event))>
                        <option value="">"Aucune récompense"</option>
                        {move ||document().map(|document|document.items.into_iter().map(|item|view!{<option value=item.id.to_string()>{item.name}</option>}).collect_view())}
                    </select></label>
                    <label>"Quantité de la récompense"<input type="number" min="1" step="1" disabled=move ||reward.get().is_empty() aria-describedby="gameplay-field-error" prop:value=move ||reward_quantity.get() on:input=move |event|reward_quantity.set(event_target_value(&event))/></label>
                    <p>"La récompense est attribuée une seule fois, quelle que soit la solution choisie. Les solutions et les dialogues sont conservés."</p>
                    <p>"Les conditions et consommations de chaque solution se règlent ci-dessous."</p>
                    <p>"Modifier le donneur, les objets ou leurs quantités peut rendre une ancienne sauvegarde incompatible."</p>
                    <button on:click=move |_|preview.run(edit(state.get_untracked(),move |_,source| {
                        let reward = reward.get_untracked();
                        let definition = QuestDefinition {
                            id:id(&selected.get_untracked())?, name:name.get_untracked(), giver:id(&giver.get_untracked())?,
                            required_item:id(&item.get_untracked())?, required_quantity:parse_quantity(&quantity.get_untracked(),"Quantité demandée")?,
                            reward_item:if reward.is_empty(){None}else{Some(id(&reward)?)},
                            reward_quantity:if reward.is_empty(){0}else{parse_quantity(&reward_quantity.get_untracked(),"Quantité de la récompense")?},
                        };
                        *source = crate::quest_settings::retouch(source,definition)?;
                        Ok(())
                    }))>"Examiner les réglages de la quête"</button>
                </fieldset>
            </details>
        </Show>
    }
}

fn parse_quantity(value: &str, label: &str) -> Result<u32, String> {
    value
        .trim()
        .parse()
        .map_err(|_| format!("{label} : saisir une quantité entière positive."))
}
