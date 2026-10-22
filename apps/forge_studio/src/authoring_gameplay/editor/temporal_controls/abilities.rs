use super::condition::{ConditionControls, ConditionFields};
use super::{Edit, document, edit, id, identity, integer, source_hash};
use forge_core::EntityId;
use forge_gameplay::{
    AbilityDefinition, AbilityEffect, AbilityTarget, Condition, QuestConsumption,
};
use forge_project::authoring::AuthoringState;
use leptos::prelude::*;
use std::collections::BTreeSet;

#[derive(Clone, Copy)]
struct Fields {
    selected: RwSignal<String>,
    name: RwSignal<String>,
    target: RwSignal<String>,
    range: RwSignal<String>,
    stamina: RwSignal<String>,
    condition: ConditionFields,
    costs: RwSignal<Vec<QuestConsumption>>,
    effects: RwSignal<Vec<AbilityEffect>>,
    item: RwSignal<String>,
    quantity: RwSignal<String>,
    effect_kind: RwSignal<String>,
    points: RwSignal<String>,
    timed_effect: RwSignal<String>,
    error: RwSignal<Option<String>>,
}

impl Fields {
    fn preview(self, state: Option<AuthoringState>, create: bool) -> Edit {
        edit(state, move |_, source| {
            let ability_id = identity(source, "ability", &self.selected.get_untracked(), create)?;
            let previous = source
                .simulation
                .as_ref()
                .and_then(|simulation| simulation.ability(ability_id));
            if !create && previous.is_none() {
                return Err("Choisir une capacité courante.".into());
            }
            let ability = AbilityDefinition {
                id: ability_id,
                name: self.name.get_untracked(),
                target: match self.target.get_untracked().as_str() {
                    "self" => AbilityTarget::SelfOnly,
                    "living" => AbilityTarget::AnyLivingActor,
                    _ => return Err("Choisir une règle de cible courante.".into()),
                },
                range_m: self
                    .range
                    .get_untracked()
                    .trim()
                    .parse()
                    .map_err(|_| "Saisir une portée en mètres.".to_owned())?,
                stamina_cost: integer(&self.stamina.get_untracked(), "Coût en endurance")?,
                condition: self
                    .condition
                    .resolve(source, previous.map(|ability| &ability.condition))?,
                item_costs: self.costs.get_untracked(),
                effects: self.effects.get_untracked(),
            };
            *source = source
                .with_ability_definition(ability)
                .map_err(|error| error.to_string())?;
            Ok(())
        })
    }

    fn remove(self, state: Option<AuthoringState>) -> Edit {
        edit(state, move |_, source| {
            let ability = id(&self.selected.get_untracked())?;
            let mut simulation = source
                .simulation
                .clone()
                .ok_or("Aucune capacité à retoucher.")?;
            if simulation.ability(ability).is_none() {
                return Err("Choisir une capacité courante.".into());
            }
            if simulation
                .actor_abilities
                .values()
                .any(|abilities| abilities.contains(&ability))
            {
                return Err(
                    "Retirer d’abord cette capacité des personnages qui l’ont acquise.".into(),
                );
            }
            simulation.abilities.retain(|entry| entry.id != ability);
            *source = source
                .with_simulation_definition(Some(simulation))
                .map_err(|error| error.to_string())?;
            Ok(())
        })
    }

    fn load(self, current: Option<AbilityDefinition>) {
        self.error.set(None);
        if let Some(current) = current {
            self.name.set(current.name);
            self.range.set(current.range_m.to_string());
            self.stamina.set(current.stamina_cost.to_string());
            self.condition.load(current.condition);
            self.target.set(
                match current.target {
                    AbilityTarget::SelfOnly => "self",
                    AbilityTarget::AnyLivingActor => "living",
                }
                .into(),
            );
            self.costs.set(current.item_costs);
            self.effects.set(current.effects);
        } else {
            self.name.set("Nouvelle capacité".into());
            self.range.set("0".into());
            self.stamina.set("5".into());
            self.condition.load(Condition::Always);
            self.target.set("self".into());
            self.costs.set(vec![]);
            self.effects.set(vec![]);
        }
    }

    fn add_cost(self) -> Result<(), String> {
        let item = id(&self.item.get_untracked())?;
        let quantity = integer::<u32>(&self.quantity.get_untracked(), "Quantité consommée")?;
        if quantity == 0 {
            return Err("La quantité consommée doit être positive.".into());
        }
        let mut costs = self.costs.get_untracked();
        if costs.len() >= forge_gameplay::MAX_ABILITY_ITEM_COSTS
            || costs.iter().any(|cost| cost.item == item)
        {
            return Err("Chaque objet a un coût unique ; au plus huit coûts par capacité.".into());
        }
        costs.push(QuestConsumption { item, quantity });
        self.costs.set(costs);
        Ok(())
    }

    fn add_effect(self) -> Result<(), String> {
        let effect = match self.effect_kind.get_untracked().as_str() {
            "heal" => AbilityEffect::Heal {
                points: integer(&self.points.get_untracked(), "Points de soin")?,
            },
            "timed" => AbilityEffect::Timed {
                effect: id(&self.timed_effect.get_untracked())?,
            },
            _ => return Err("Choisir un effet courant.".into()),
        };
        let mut effects = self.effects.get_untracked();
        if effects.len() >= forge_gameplay::MAX_ABILITY_EFFECTS {
            return Err("Au plus huit effets par capacité.".into());
        }
        effects.push(effect);
        self.effects.set(effects);
        Ok(())
    }
}

#[component]
pub(super) fn AbilityControls(
    state: RwSignal<Option<AuthoringState>>,
    send: Callback<Edit>,
) -> impl IntoView {
    let fields = Fields {
        selected: RwSignal::new(String::new()),
        name: RwSignal::new("Nouvelle capacité".into()),
        target: RwSignal::new("self".into()),
        range: RwSignal::new("0".into()),
        stamina: RwSignal::new("5".into()),
        condition: ConditionFields::new(),
        costs: RwSignal::new(vec![]),
        effects: RwSignal::new(vec![]),
        item: RwSignal::new(String::new()),
        quantity: RwSignal::new("1".into()),
        effect_kind: RwSignal::new("heal".into()),
        points: RwSignal::new("25".into()),
        timed_effect: RwSignal::new(String::new()),
        error: RwSignal::new(None),
    };
    Effect::new(move |previous: Option<super::super::FieldSource>| {
        let key = (fields.selected.get(), source_hash(state));
        if previous.as_ref() != Some(&key) {
            fields.load(
                document(state)
                    .and_then(|source| source.simulation)
                    .and_then(|simulation| {
                        id(&key.0)
                            .ok()
                            .and_then(|ability| simulation.ability(ability).cloned())
                    }),
            );
        }
        key
    });
    view! {
        <h4>"Capacités, cibles et coûts"</h4>
        <p>"Les coûts ne sont consommés que si tous les effets réussissent. Un soin ne ressuscite pas ; une capacité sans effet est refusée."</p>
        <label>"Capacité à retoucher"<select prop:value=move ||fields.selected.get() on:change=move |event|fields.selected.set(event_target_value(&event))>
            <option value="">"Préparer une nouvelle capacité"</option>
            {move ||document(state).and_then(|source|source.simulation).map(|simulation|simulation.abilities.into_iter().map(|entry|view!{<option value=entry.id.to_string()>{entry.name}</option>}).collect_view())}
        </select></label>
        <label>"Nom de la capacité"<input maxlength="256" prop:value=move ||fields.name.get() on:input=move |event|fields.name.set(event_target_value(&event))/></label>
        <label>"Cible autorisée"<select prop:value=move ||fields.target.get() on:change=move |event|fields.target.set(event_target_value(&event))>
            <option value="self">"Le personnage qui l’utilise"</option><option value="living">"Un personnage présent et vivant à portée"</option>
        </select></label>
        <label>"Portée (m)"<input type="number" min="0" max=forge_gameplay::MAX_ABILITY_RANGE_M.to_string() step="0.5" prop:value=move ||fields.range.get() on:input=move |event|fields.range.set(event_target_value(&event))/></label>
        <label>"Coût en endurance (points)"<input type="number" min="0" step="1" prop:value=move ||fields.stamina.get() on:input=move |event|fields.stamina.set(event_target_value(&event))/></label>
        <ConditionControls fields=fields.condition state=state/>
        <CostControls fields=fields state=state/>
        <EffectListControls fields=fields state=state/>
        <p role="alert">{move ||fields.error.get()}</p>
        <button on:click=move |_|send.run(fields.preview(state.get_untracked(),true))>"Examiner une nouvelle capacité"</button>
        <button on:click=move |_|send.run(fields.preview(state.get_untracked(),false))>"Examiner la retouche de la capacité"</button>
        <button on:click=move |_|send.run(fields.remove(state.get_untracked()))>"Examiner la suppression de la capacité inutilisée"</button>
        <AssignmentControls state=state send=send/>
    }
}

#[component]
fn CostControls(fields: Fields, state: RwSignal<Option<AuthoringState>>) -> impl IntoView {
    view! {
        <p>"Objets consommés dans le brouillon de la capacité"</p>
        {move ||fields.costs.get().into_iter().enumerate().map(|(index,cost)| {
            let name=document(state).and_then(|source|source.item(cost.item).map(|item|item.name.clone())).unwrap_or_else(||"Objet absent".into());
            view!{<p>{format!("{} × {name}",cost.quantity)}<button on:click=move |_|fields.costs.update(|costs|{if index<costs.len(){costs.remove(index);}})>"Retirer ce coût du brouillon"</button></p>}
        }).collect_view()}
        <label>"Objet consommé"<select prop:value=move ||fields.item.get() on:change=move |event|fields.item.set(event_target_value(&event))>
            <option value="">"Choisir un objet du catalogue"</option>
            {move ||document(state).map(|source|source.items.into_iter().map(|item|view!{<option value=item.id.to_string()>{item.name}</option>}).collect_view())}
        </select></label>
        <label>"Quantité consommée"<input type="number" min="1" step="1" prop:value=move ||fields.quantity.get() on:input=move |event|fields.quantity.set(event_target_value(&event))/></label>
        <button on:click=move |_|fields.error.set(fields.add_cost().err())>"Ajouter ce coût au brouillon"</button>
    }
}

#[component]
fn EffectListControls(fields: Fields, state: RwSignal<Option<AuthoringState>>) -> impl IntoView {
    view! {
        <p>"Effets du brouillon, dans leur ordre d’application"</p>
        {move ||fields.effects.get().into_iter().enumerate().map(|(index,effect)| {
            let label=match effect {
                AbilityEffect::Heal { points } => format!("Soin : jusqu’à {points} points"),
                AbilityEffect::Timed { effect } => document(state).and_then(|source|source.simulation).and_then(|simulation|simulation.effect(effect).map(|effect|format!("Protection temporaire : {}",effect.name))).unwrap_or_else(||"Effet absent".into()),
            };
            view!{<p>{label}<button on:click=move |_|fields.effects.update(|effects|{if index<effects.len(){effects.remove(index);}})>"Retirer cet effet du brouillon"</button></p>}
        }).collect_view()}
        <label>"Effet à ajouter"<select prop:value=move ||fields.effect_kind.get() on:change=move |event|fields.effect_kind.set(event_target_value(&event))>
            <option value="heal">"Soin immédiat"</option><option value="timed">"Protection temporaire"</option>
        </select></label>
        <Show when=move ||fields.effect_kind.get()=="heal"><label>"Points de soin au maximum"<input type="number" min="1" max=forge_gameplay::MAX_ABILITY_POINTS.to_string() step="1" prop:value=move ||fields.points.get() on:input=move |event|fields.points.set(event_target_value(&event))/></label></Show>
        <Show when=move ||fields.effect_kind.get()=="timed"><label>"Effet temporaire"<select prop:value=move ||fields.timed_effect.get() on:change=move |event|fields.timed_effect.set(event_target_value(&event))>
            <option value="">"Choisir un effet"</option>
            {move ||document(state).and_then(|source|source.simulation).map(|simulation|simulation.effects.into_iter().map(|effect|view!{<option value=effect.id.to_string()>{effect.name}</option>}).collect_view())}
        </select></label></Show>
        <button on:click=move |_|fields.error.set(fields.add_effect().err())>"Ajouter cet effet au brouillon"</button>
    }
}

#[component]
fn AssignmentControls(
    state: RwSignal<Option<AuthoringState>>,
    send: Callback<Edit>,
) -> impl IntoView {
    let actor = RwSignal::new(String::new());
    let acquired = RwSignal::new(BTreeSet::<EntityId>::new());
    Effect::new(move |previous: Option<super::super::FieldSource>| {
        let key = (actor.get(), source_hash(state));
        if previous.as_ref() != Some(&key) {
            let current = document(state)
                .and_then(|source| source.simulation)
                .and_then(|simulation| {
                    id(&key.0)
                        .ok()
                        .and_then(|actor| simulation.actor_abilities.get(&actor).cloned())
                })
                .unwrap_or_default();
            acquired.set(current.into_iter().collect());
        }
        key
    });
    view! {
        <h4>"Capacités acquises par personnage"</h4>
        <label>"Personnage"<select prop:value=move ||actor.get() on:change=move |event|actor.set(event_target_value(&event))>
            <option value="">"Choisir un personnage"</option>
            {move ||document(state).map(|source|source.actors.into_iter().map(|actor|view!{<option value=actor.id.to_string()>{actor.name}</option>}).collect_view())}
        </select></label>
        {move ||document(state).and_then(|source|source.simulation).map(|simulation|simulation.abilities.into_iter().map(|ability|{
            let ability_id=ability.id;
            view!{<label><input type="checkbox" prop:checked=move ||acquired.get().contains(&ability_id) on:change=move |event|acquired.update(|abilities|{
                if event_target_checked(&event){abilities.insert(ability_id);}else{abilities.remove(&ability_id);}
            })/>{ability.name}</label>}
        }).collect_view())}
        <button on:click=move |_|send.run(edit(state.get_untracked(),move |_,source|{
            let actor=id(&actor.get_untracked())?;
            if source.actor(actor).is_none(){return Err("Choisir un personnage courant.".into());}
            let mut simulation=source.simulation.clone().unwrap_or_default();
            let selected=acquired.get_untracked();
            let mut abilities=simulation.actor_abilities.get(&actor).cloned().unwrap_or_default();
            abilities.retain(|ability|selected.contains(ability));
            for ability in selected{if !abilities.contains(&ability){abilities.push(ability);}}
            if abilities.is_empty(){simulation.actor_abilities.remove(&actor);}else{simulation.actor_abilities.insert(actor,abilities);}
            *source=source.with_simulation_definition(Some(simulation)).map_err(|error|error.to_string())?;Ok(())
        }))>"Examiner les capacités acquises"</button>
    }
}
