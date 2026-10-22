//! Faction authoring uses the same atomic preview and history as other RPG data.
use super::{Edit, edit, id, source_hash};
use forge_core::{EntityId, canonical_json_sha256};
use forge_gameplay::{
    CreatorSurface, FactionDefinition, FactionRelation, FactionsDefinition, GameplayDocument,
};
use forge_project::authoring::AuthoringState;
use leptos::prelude::*;
use std::collections::{BTreeMap, BTreeSet};

fn document(state: RwSignal<Option<AuthoringState>>) -> Option<GameplayDocument> {
    state
        .get()?
        .zone_head?
        .corrections
        .gameplay
        .map(|document| *document)
}

fn definitions(source: &GameplayDocument) -> FactionsDefinition {
    source.factions.clone().unwrap_or(FactionsDefinition {
        version: 1,
        factions: vec![],
        memberships: BTreeMap::new(),
        relations: vec![],
    })
}

fn group_name(source: &GameplayDocument, group: EntityId) -> String {
    source
        .factions
        .as_ref()
        .and_then(|definitions| definitions.faction(group))
        .map_or_else(|| "Faction absente".into(), |group| group.name.clone())
}

#[derive(Clone, Copy)]
struct GroupFields {
    selected: RwSignal<String>,
    name: RwSignal<String>,
    range: RwSignal<String>,
    penalty: RwSignal<String>,
    protects: RwSignal<BTreeSet<EntityId>>,
}

impl GroupFields {
    fn preview(self, state: Option<AuthoringState>, create: bool) -> Edit {
        edit(state, move |_, source| {
            let hash = canonical_json_sha256(source).map_err(|error| error.to_string())?;
            let mut factions = definitions(source);
            let group =
                FactionDefinition {
                    id: if create {
                        EntityId::deterministic(format!("faction/{}/{hash}", source.area_id))
                    } else {
                        id(&self.selected.get_untracked())?
                    },
                    name: self.name.get_untracked(),
                    perception_range_m: self
                        .range
                        .get_untracked()
                        .trim()
                        .parse()
                        .map_err(|_| "Saisir une portée en mètres.".to_owned())?,
                    protects_factions: self.protects.get_untracked().into_iter().collect(),
                    assault_delta: self.penalty.get_untracked().trim().parse().map_err(|_| {
                        "Saisir une variation entière, négative ou nulle.".to_owned()
                    })?,
                };
            let group_id = group.id;
            if create {
                factions.factions.push(group);
            } else {
                *factions
                    .factions
                    .iter_mut()
                    .find(|entry| entry.id == group_id)
                    .ok_or("Choisir une faction courante")? = group;
            }
            *source = source
                .with_factions_definition(Some(factions))
                .map_err(|error| error.to_string())?;
            Ok(())
        })
    }

    fn remove(self, state: Option<AuthoringState>) -> Edit {
        edit(state, move |_, source| {
            let group = id(&self.selected.get_untracked())?;
            let mut factions = definitions(source);
            if factions.faction(group).is_none() {
                return Err("Choisir une faction courante".into());
            }
            if factions
                .memberships
                .values()
                .any(|current| *current == group)
                || factions
                    .relations
                    .iter()
                    .any(|relation| relation.from == group || relation.to == group)
                || factions
                    .factions
                    .iter()
                    .filter(|entry| entry.id != group)
                    .any(|entry| entry.protects_factions.contains(&group))
            {
                return Err("Retirer d’abord les appartenances, relations et protections qui utilisent cette faction.".into());
            }
            factions.factions.retain(|entry| entry.id != group);
            let candidate = if factions.factions.is_empty() {
                None
            } else {
                Some(factions)
            };
            *source = source
                .with_factions_definition(candidate)
                .map_err(|error| error.to_string())?;
            Ok(())
        })
    }
}

#[component]
pub(super) fn FactionControls(
    state: RwSignal<Option<AuthoringState>>,
    busy: RwSignal<bool>,
    send: Callback<Edit>,
) -> impl IntoView {
    let disabled = move || {
        busy.get()
            || state
                .get()
                .is_none_or(|state| state.zone_head.is_none() || state.playtest_hash.is_some())
    };
    view! {
        <Show when=move ||document(state).is_some_and(|document|document.surface_visible(CreatorSurface::Factions))>
            <details class="creator-factions"><summary>"Factions et conséquences locales"</summary><fieldset disabled=disabled>
                <p>"Ces groupes appartiennent à la carte. Après une agression directe, seuls les personnages proches qui ont vu l’agresseur et la victime peuvent changer d’opinion. Un obstacle peut masquer l’événement ; aucune réputation ne se propage au groupe entier."</p>
                <p>{format!("Au maximum {} factions, {} observateurs proches par agression et {} relations personnelles modifiées.",forge_gameplay::MAX_FACTIONS,forge_gameplay::MAX_FACTION_WITNESSES,forge_gameplay::MAX_PERSONAL_RELATIONS)}</p>
                <GroupControls state=state send=send/>
                <MembershipControls state=state send=send/>
                <RelationControls state=state send=send/>
                <p>"Changer les appartenances, relations initiales ou règles de perception peut rendre une sauvegarde incompatible. Renommer une faction conserve sa signification. Examiner le diff, puis appliquer et sauvegarder par les commandes habituelles."</p>
            </fieldset></details>
        </Show>
    }
}

#[component]
fn GroupControls(state: RwSignal<Option<AuthoringState>>, send: Callback<Edit>) -> impl IntoView {
    let fields = GroupFields {
        selected: RwSignal::new(String::new()),
        name: RwSignal::new("Nouvelle faction".into()),
        range: RwSignal::new("15".into()),
        penalty: RwSignal::new("-10".into()),
        protects: RwSignal::new(BTreeSet::new()),
    };
    Effect::new(move |previous: Option<super::FieldSource>| {
        let key = (fields.selected.get(), source_hash(state));
        if previous.as_ref() == Some(&key) {
            return key;
        }
        let current = document(state)
            .and_then(|document| document.factions)
            .and_then(|factions| id(&key.0).ok().and_then(|id| factions.faction(id).cloned()));
        if let Some(current) = current {
            fields.name.set(current.name);
            fields.range.set(current.perception_range_m.to_string());
            fields.penalty.set(current.assault_delta.to_string());
            fields
                .protects
                .set(current.protects_factions.into_iter().collect());
        } else {
            fields.name.set("Nouvelle faction".into());
            fields.range.set("15".into());
            fields.penalty.set("-10".into());
            fields.protects.set(BTreeSet::new());
        }
        key
    });
    view! {
        <label>"Faction à retoucher"<select prop:value=move ||fields.selected.get() on:change=move |event|fields.selected.set(event_target_value(&event))>
            <option value="">"Préparer une nouvelle faction"</option>
            {move ||document(state).and_then(|document|document.factions).map(|definitions|definitions.factions.into_iter().map(|group|view!{<option value=group.id.to_string()>{group.name}</option>}).collect_view())}
        </select></label>
        <label>"Nom de la faction"<input maxlength="256" prop:value=move ||fields.name.get() on:input=move |event|fields.name.set(event_target_value(&event))/></label>
        <label>"Portée de perception (m)"<input type="number" min="0" max=forge_gameplay::MAX_PERCEPTION_RANGE_M.to_string() step="0.5" prop:value=move ||fields.range.get() on:input=move |event|fields.range.set(event_target_value(&event))/></label>
        <label>"Variation après une agression observée (points)"<input type="number" min=forge_gameplay::MIN_RELATION.to_string() max="0" step="1" prop:value=move ||fields.penalty.get() on:input=move |event|fields.penalty.set(event_target_value(&event))/></label>
        <p>"Une variation négative réduit l’opinion du témoin envers l’agresseur, entre −100 et 100 points. Choisir les groupes dont les membres sont protégés."</p>
        {move ||document(state).and_then(|document|document.factions).map(|definitions|definitions.factions.into_iter().map(|group| {
            let id=group.id;
            view!{<label><input type="checkbox" prop:checked=move ||fields.protects.get().contains(&id) on:change=move |event|fields.protects.update(|groups| {
                if event_target_checked(&event) {groups.insert(id);} else {groups.remove(&id);}
            })/>{group.name}</label>}
        }).collect_view())}
        <button on:click=move |_|send.run(fields.preview(state.get_untracked(),true))>"Examiner une nouvelle faction"</button>
        <button on:click=move |_|send.run(fields.preview(state.get_untracked(),false))>"Examiner la retouche de la faction"</button>
        <button on:click=move |_|send.run(fields.remove(state.get_untracked()))>"Examiner la suppression de la faction inutilisée"</button>
    }
}

#[component]
fn MembershipControls(
    state: RwSignal<Option<AuthoringState>>,
    send: Callback<Edit>,
) -> impl IntoView {
    let actor = RwSignal::new(String::new());
    let group = RwSignal::new(String::new());
    Effect::new(move |previous: Option<super::FieldSource>| {
        let key = (actor.get(), source_hash(state));
        if previous.as_ref() != Some(&key) {
            let current = document(state)
                .and_then(|document| document.factions)
                .and_then(|factions| {
                    id(&key.0)
                        .ok()
                        .and_then(|actor| factions.memberships.get(&actor).copied())
                });
            group.set(current.map_or_else(String::new, |group| group.to_string()));
        }
        key
    });
    view! {
        <h4>"Appartenances"</h4>
        <label>"Personnage"<select prop:value=move ||actor.get() on:change=move |event|actor.set(event_target_value(&event))>
            <option value="">"Choisir un personnage"</option>{move ||document(state).map(|document|document.actors.into_iter().map(|actor|view!{<option value=actor.id.to_string()>{actor.name}</option>}).collect_view())}
        </select></label>
        <label>"Groupe du personnage"<select prop:value=move ||group.get() on:change=move |event|group.set(event_target_value(&event))>
            <option value="">"Sans faction"</option>{move ||document(state).and_then(|document|document.factions).map(|definitions|definitions.factions.into_iter().map(|group|view!{<option value=group.id.to_string()>{group.name}</option>}).collect_view())}
        </select></label>
        <button on:click=move |_|send.run(edit(state.get_untracked(),move |_,source| {
            let actor=id(&actor.get_untracked())?;
            if source.actor(actor).is_none() {return Err("Choisir un personnage courant".into());}
            let mut factions=source.factions.clone().ok_or("Créer d’abord une faction")?;
            let group=group.get_untracked();
            if group.is_empty() {factions.memberships.remove(&actor);} else {factions.memberships.insert(actor,id(&group)?);}
            *source=source.with_factions_definition(Some(factions)).map_err(|error|error.to_string())?;
            Ok(())
        }))>"Examiner l’appartenance du personnage"</button>
    }
}

#[component]
fn RelationControls(
    state: RwSignal<Option<AuthoringState>>,
    send: Callback<Edit>,
) -> impl IntoView {
    let from = RwSignal::new(String::new());
    let to = RwSignal::new(String::new());
    let value = RwSignal::new("0".to_owned());
    Effect::new(
        move |previous: Option<(String, String, Option<forge_core::ContentHash>)>| {
            let key = (from.get(), to.get(), source_hash(state));
            if previous.as_ref() != Some(&key) {
                let relation = document(state)
                    .and_then(|document| document.factions)
                    .and_then(|factions| {
                        let (from, to) = (id(&key.0).ok()?, id(&key.1).ok()?);
                        factions
                            .relations
                            .iter()
                            .find(|relation| relation.from == from && relation.to == to)
                            .map(|relation| relation.value)
                    });
                value.set(relation.unwrap_or(0).to_string());
            }
            key
        },
    );
    let preview = move |remove: bool| {
        edit(state.get_untracked(), move |_, source| {
            let (from, to) = (id(&from.get_untracked())?, id(&to.get_untracked())?);
            let mut factions = source
                .factions
                .clone()
                .ok_or("Créer d’abord les factions")?;
            factions
                .relations
                .retain(|relation| relation.from != from || relation.to != to);
            if !remove {
                factions.relations.push(FactionRelation {
                    from,
                    to,
                    value: value.get_untracked().trim().parse().map_err(|_| {
                        "Saisir une relation entière entre −100 et 100 points.".to_owned()
                    })?,
                });
            }
            *source = source
                .with_factions_definition(Some(factions))
                .map_err(|error| error.to_string())?;
            Ok(())
        })
    };
    view! {
        <h4>"Relations initiales"</h4><p>"La relation est orientée : l’opinion du premier groupe envers le second. Une paire absente vaut zéro ; les conséquences vécues restent personnelles."</p>
        <label>"Groupe observateur"<select prop:value=move ||from.get() on:change=move |event|from.set(event_target_value(&event))>
            <option value="">"Choisir une faction"</option>{move ||document(state).and_then(|document|document.factions).map(|definitions|definitions.factions.into_iter().map(|group|view!{<option value=group.id.to_string()>{group.name}</option>}).collect_view())}
        </select></label>
        <label>"Groupe observé"<select prop:value=move ||to.get() on:change=move |event|to.set(event_target_value(&event))>
            <option value="">"Choisir une faction"</option>{move ||document(state).and_then(|document|document.factions).map(|definitions|definitions.factions.into_iter().map(|group|view!{<option value=group.id.to_string()>{group.name}</option>}).collect_view())}
        </select></label>
        <label>"Relation initiale (points)"<input type="number" min=forge_gameplay::MIN_RELATION.to_string() max=forge_gameplay::MAX_RELATION.to_string() step="1" prop:value=move ||value.get() on:input=move |event|value.set(event_target_value(&event))/></label>
        <button on:click=move |_|send.run(preview(false))>"Examiner la relation initiale"</button>
        <button on:click=move |_|send.run(preview(true))>"Examiner le retour à la relation neutre"</button>
        <ul>{move ||document(state).map(|document|document.factions.as_ref().map(|definitions|definitions.relations.iter().map(|relation|view!{<li>{format!("{} → {} : {} points",group_name(&document,relation.from),group_name(&document,relation.to),relation.value)}</li>}).collect_view()))}</ul>
    }
}
