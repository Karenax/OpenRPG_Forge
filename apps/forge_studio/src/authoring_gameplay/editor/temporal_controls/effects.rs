use super::{Edit, document, edit, id, identity, integer, source_hash};
use forge_core::EntityId;
use forge_gameplay::{AbilityEffect, EffectStacking, TimedEffectDefinition};
use forge_project::authoring::AuthoringState;
use leptos::prelude::*;
use std::collections::BTreeSet;

#[derive(Clone, Copy)]
struct Fields {
    selected: RwSignal<String>,
    name: RwSignal<String>,
    duration: RwSignal<String>,
    protection: RwSignal<String>,
    stacking: RwSignal<String>,
    maximum: RwSignal<String>,
    exclusions: RwSignal<BTreeSet<EntityId>>,
}

impl Fields {
    fn preview(self, state: Option<AuthoringState>, create: bool) -> Edit {
        edit(state, move |_, source| {
            let effect_id = identity(
                source,
                "timed-effect",
                &self.selected.get_untracked(),
                create,
            )?;
            if !create
                && source
                    .simulation
                    .as_ref()
                    .and_then(|simulation| simulation.effect(effect_id))
                    .is_none()
            {
                return Err("Choisir un effet courant.".into());
            }
            let effect = TimedEffectDefinition {
                id: effect_id,
                name: self.name.get_untracked(),
                duration_ms: integer(&self.duration.get_untracked(), "Durée de l’effet")?,
                attack_protection_bonus: integer(
                    &self.protection.get_untracked(),
                    "Protection ajoutée",
                )?,
                stacking: match self.stacking.get_untracked().as_str() {
                    "reject" => EffectStacking::Reject,
                    "refresh" => EffectStacking::Refresh,
                    "stack" => EffectStacking::Stack {
                        max_instances: integer(
                            &self.maximum.get_untracked(),
                            "Maximum d’instances",
                        )?,
                    },
                    _ => return Err("Choisir une règle de cumul courante.".into()),
                },
                exclusive_with: self.exclusions.get_untracked().into_iter().collect(),
            };
            *source = source
                .with_timed_effect_definition(effect)
                .map_err(|error| error.to_string())?;
            Ok(())
        })
    }

    fn remove(self, state: Option<AuthoringState>) -> Edit {
        edit(state, move |_, source| {
            let effect = id(&self.selected.get_untracked())?;
            let mut simulation = source
                .simulation
                .clone()
                .ok_or("Aucun effet à retoucher.")?;
            if simulation.effect(effect).is_none() {
                return Err("Choisir un effet courant.".into());
            }
            if simulation.abilities.iter().flat_map(|ability|&ability.effects).any(|entry|matches!(entry,AbilityEffect::Timed { effect: used } if *used == effect))
                || simulation.effects.iter().filter(|entry|entry.id != effect).any(|entry|entry.exclusive_with.contains(&effect))
            {
                return Err("Retoucher d’abord les capacités et exclusions qui utilisent cet effet.".into());
            }
            simulation.effects.retain(|entry| entry.id != effect);
            *source = source
                .with_simulation_definition(Some(simulation))
                .map_err(|error| error.to_string())?;
            Ok(())
        })
    }

    fn load(self, current: Option<TimedEffectDefinition>) {
        if let Some(current) = current {
            self.name.set(current.name);
            self.duration.set(current.duration_ms.to_string());
            self.protection
                .set(current.attack_protection_bonus.to_string());
            self.exclusions
                .set(current.exclusive_with.into_iter().collect());
            match current.stacking {
                EffectStacking::Reject => {
                    self.stacking.set("reject".into());
                    self.maximum.set("2".into());
                }
                EffectStacking::Refresh => {
                    self.stacking.set("refresh".into());
                    self.maximum.set("2".into());
                }
                EffectStacking::Stack { max_instances } => {
                    self.stacking.set("stack".into());
                    self.maximum.set(max_instances.to_string());
                }
            }
        } else {
            self.name.set("Nouvelle protection".into());
            self.duration.set("10000".into());
            self.protection.set("5".into());
            self.stacking.set("reject".into());
            self.maximum.set("2".into());
            self.exclusions.set(BTreeSet::new());
        }
    }
}

#[component]
pub(super) fn EffectControls(
    state: RwSignal<Option<AuthoringState>>,
    send: Callback<Edit>,
) -> impl IntoView {
    let fields = Fields {
        selected: RwSignal::new(String::new()),
        name: RwSignal::new("Nouvelle protection".into()),
        duration: RwSignal::new("10000".into()),
        protection: RwSignal::new("5".into()),
        stacking: RwSignal::new("reject".into()),
        maximum: RwSignal::new("2".into()),
        exclusions: RwSignal::new(BTreeSet::new()),
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
                            .and_then(|effect| simulation.effect(effect).cloned())
                    }),
            );
        }
        key
    });
    view! {
        <p>"Une protection temporaire ajoute ses points contre les attaques directes pendant sa durée de simulation. Son expiration, son remplacement et son annulation gardent la cause de la capacité."</p>
        <label>"Effet à retoucher"<select prop:value=move ||fields.selected.get() on:change=move |event|fields.selected.set(event_target_value(&event))>
            <option value="">"Préparer un nouvel effet"</option>
            {move ||document(state).and_then(|source|source.simulation).map(|simulation|simulation.effects.into_iter().map(|entry|view!{<option value=entry.id.to_string()>{entry.name}</option>}).collect_view())}
        </select></label>
        <label>"Nom de l’effet"<input maxlength="256" prop:value=move ||fields.name.get() on:input=move |event|fields.name.set(event_target_value(&event))/></label>
        <label>"Durée (ms de simulation)"<input type="number" min="1" max=forge_gameplay::MAX_EFFECT_DURATION_MS.to_string() step="1" prop:value=move ||fields.duration.get() on:input=move |event|fields.duration.set(event_target_value(&event))/></label>
        <label>"Protection ajoutée (points)"<input type="number" min="1" max=forge_gameplay::MAX_ABILITY_POINTS.to_string() step="1" prop:value=move ||fields.protection.get() on:input=move |event|fields.protection.set(event_target_value(&event))/></label>
        <label>"Application quand cet effet est déjà actif"<select prop:value=move ||fields.stacking.get() on:change=move |event|fields.stacking.set(event_target_value(&event))>
            <option value="reject">"Refuser sans consommer le coût"</option><option value="refresh">"Remplacer et repartir pour une durée complète"</option><option value="stack">"Cumuler dans une limite"</option>
        </select></label>
        <Show when=move ||fields.stacking.get()=="stack"><label>"Instances simultanées au maximum"<input type="number" min="1" max="8" step="1" prop:value=move ||fields.maximum.get() on:input=move |event|fields.maximum.set(event_target_value(&event))/></label></Show>
        <p>"Effets incompatibles : une exclusion s’applique dans les deux sens, quel que soit l’ordre d’utilisation."</p>
        {move ||document(state).and_then(|source|source.simulation).map(|simulation|simulation.effects.into_iter().filter(|entry|entry.id.to_string()!=fields.selected.get()).map(|entry|{
            let effect_id=entry.id;
            view!{<label><input type="checkbox" prop:checked=move ||fields.exclusions.get().contains(&effect_id) on:change=move |event|fields.exclusions.update(|exclusions| {
                if event_target_checked(&event) { exclusions.insert(effect_id); } else { exclusions.remove(&effect_id); }
            })/>{entry.name}</label>}
        }).collect_view())}
        <button on:click=move |_|send.run(fields.preview(state.get_untracked(),true))>"Examiner un nouvel effet"</button>
        <button on:click=move |_|send.run(fields.preview(state.get_untracked(),false))>"Examiner la retouche de l’effet"</button>
        <button on:click=move |_|send.run(fields.remove(state.get_untracked()))>"Examiner la suppression de l’effet inutilisé"</button>
    }
}
