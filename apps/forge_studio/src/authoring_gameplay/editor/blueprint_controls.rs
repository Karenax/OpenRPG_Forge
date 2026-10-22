//! Blueprint changes use the same validated preview/apply/history path as actor edits.
use super::{Edit, edit, id};
use forge_core::{EntityId, canonical_json_sha256};
use forge_gameplay::{
    ActorBlueprintDefaults, ActorBlueprintDefinition, ActorBlueprintOverrides, ActorRole,
    CreatorSurface,
};
use forge_project::authoring::AuthoringState;
use leptos::prelude::*;

#[derive(Clone, Copy)]
struct DefaultsFields {
    role: RwSignal<String>,
    vitality: RwSignal<String>,
    protection: RwSignal<String>,
    capacity: RwSignal<String>,
    flees: RwSignal<bool>,
}

impl DefaultsFields {
    fn set(self, defaults: &ActorBlueprintDefaults) {
        self.role.set(
            match defaults.role {
                ActorRole::Player => "player",
                ActorRole::Friendly => "friendly",
                ActorRole::Enemy => "enemy",
            }
            .into(),
        );
        self.vitality.set(defaults.max_vitality.to_string());
        self.protection.set(defaults.protection.to_string());
        self.capacity.set(defaults.inventory_capacity.to_string());
        self.flees.set(defaults.flees_danger);
    }

    fn read(self) -> Result<ActorBlueprintDefaults, String> {
        let integer = |value: String| {
            value
                .trim()
                .parse::<u32>()
                .map_err(|_| "Saisir un entier positif ou nul.".to_owned())
        };
        Ok(ActorBlueprintDefaults {
            role: match self.role.get_untracked().as_str() {
                "player" => ActorRole::Player,
                "friendly" => ActorRole::Friendly,
                "enemy" => ActorRole::Enemy,
                _ => return Err("Choisir un rôle disponible.".into()),
            },
            max_vitality: integer(self.vitality.get_untracked())?,
            protection: integer(self.protection.get_untracked())?,
            inventory_capacity: integer(self.capacity.get_untracked())?,
            flees_danger: self.flees.get_untracked(),
        })
    }
}

#[component]
#[allow(
    clippy::too_many_lines,
    reason = "One selection owns the blueprint form and preview callback"
)]
pub(super) fn BlueprintControls(
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
    let blueprint = RwSignal::new(String::new());
    let name = RwSignal::new("Nouveau blueprint".to_owned());
    let restore = RwSignal::new("all".to_owned());
    let fields = DefaultsFields {
        role: RwSignal::new("enemy".to_owned()),
        vitality: RwSignal::new("100".to_owned()),
        protection: RwSignal::new("0".to_owned()),
        capacity: RwSignal::new("0".to_owned()),
        flees: RwSignal::new(true),
    };
    let definition = move || {
        document().and_then(|d| {
            d.actor_blueprints().and_then(|library| {
                library
                    .definitions
                    .iter()
                    .find(|b| b.id.to_string() == blueprint.get())
                    .cloned()
            })
        })
    };
    let binding = move || {
        document().and_then(|d| {
            let actor = id(&selected.get()).ok()?;
            d.actor_blueprints()
                .and_then(|library| library.bindings.iter().find(|b| b.actor == actor))
                .cloned()
        })
    };
    Effect::new(move |previous: Option<super::FieldSource>| {
        let current = (blueprint.get(), super::source_hash(state));
        if previous.as_ref() == Some(&current) {
            return current;
        }
        if let Some(current) = definition() {
            name.set(current.name);
            fields.set(&current.defaults);
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
        <Show when=move ||document().is_some_and(|d|d.surface_visible(CreatorSurface::Blueprints))>
            <details class="creator-blueprints"><summary>"Blueprints de personnages"</summary>
                <fieldset disabled=disabled>
                    <p>"Un blueprint partage les statistiques et le rôle. Chaque personnage conserve son nom, sa position, sa patrouille et ses liens de dialogue. Toutes les modifications passent par l’aperçu avant application."</p>
                    <label>"Personnage du blueprint"<select prop:value=move ||selected.get() on:change=move |e|selected.set(event_target_value(&e))>
                        <option value="">"Choisir un personnage"</option>
                        {move ||document().map(|d|d.actors.into_iter().map(|a|view!{
                            <option value=a.id.to_string()>{a.name}</option>
                        }).collect_view())}
                    </select></label>
                    <p>{move || document().and_then(|d|id(&selected.get()).ok().and_then(|actor|d.actor(actor).map(|a|format!("Personnage sélectionné : {}",a.name)))).unwrap_or_else(||"Choisir un personnage dans « Élément à retoucher ».".into())}</p>
                    <label>"Blueprint"<select prop:value=move ||blueprint.get() on:change=move |e|blueprint.set(event_target_value(&e))>
                        <option value="">"Choisir un blueprint"</option>
                        {move || document().map(|d|d.actor_blueprints().map(|library|library.definitions.iter().map(|b|view!{
                            <option value=b.id.to_string()>{format!("{} · v{}",b.name,b.version)}</option>
                        }).collect_view()))}
                    </select></label>
                    <label>"Nom du blueprint"<input prop:value=move ||name.get() on:input=move |e|name.set(event_target_value(&e))/></label>
                    <button on:click=move |_|send.run(edit(state.get_untracked(),move |_,source| {
                        let actor = source.actor(id(&selected.get_untracked())?).ok_or("Choisir un personnage")?;
                        let hash = canonical_json_sha256(source).map_err(|e|e.to_string())?;
                        let definition = ActorBlueprintDefinition {
                            id: EntityId::deterministic(format!("blueprint/{}/{hash}",source.area_id)),
                            version: 1, name: name.get_untracked(), defaults: ActorBlueprintDefaults::from_actor(actor),
                        };
                        *source = source.with_actor_blueprint(definition).map_err(|e|e.to_string())?;
                        Ok(())
                    }))>"Examiner un nouveau blueprint depuis ce personnage"</button>
                    <Show when=move ||definition().is_some()>
                        <label>"Rôle hérité"<select prop:value=move ||fields.role.get() on:change=move |e|fields.role.set(event_target_value(&e))>
                            <option value="player">"Joueur"</option><option value="friendly">"Allié"</option><option value="enemy">"Ennemi"</option>
                        </select></label>
                        <label>"Vitalité héritée"<input type="number" min="1" max="1000000" prop:value=move ||fields.vitality.get() on:input=move |e|fields.vitality.set(event_target_value(&e))/></label>
                        <label>"Protection héritée"<input type="number" min="0" max="1000000" prop:value=move ||fields.protection.get() on:input=move |e|fields.protection.set(event_target_value(&e))/></label>
                        <label>"Capacité d’inventaire héritée"<input type="number" min="0" max=forge_gameplay::MAX_INVENTORY_UNITS.to_string() prop:value=move ||fields.capacity.get() on:input=move |e|fields.capacity.set(event_target_value(&e))/></label>
                        <label><input type="checkbox" prop:checked=move ||fields.flees.get() on:change=move |e|fields.flees.set(event_target_checked(&e))/>"Fuir le danger par défaut"</label>
                        <p>"Réviser propage les valeurs héritées à tous les personnages liés. Les modifications locales restent prioritaires. Une modification des statistiques peut rendre une sauvegarde incompatible."</p>
                        <button on:click=move |_|send.run(edit(state.get_untracked(),move |_,source| {
                            let selected = id(&blueprint.get_untracked())?;
                            let mut revised = source.actor_blueprints().and_then(|l|l.definitions.iter().find(|b|b.id == selected)).cloned().ok_or("Blueprint absent")?;
                            revised.version = revised.version.checked_add(1).ok_or("Version maximale atteinte")?;
                            revised.name = name.get_untracked();
                            revised.defaults = fields.read()?;
                            *source = source.with_actor_blueprint(revised).map_err(|e|e.to_string())?;
                            Ok(())
                        }))>"Examiner la révision du blueprint et ses personnages"</button>
                        <p>"Lier remplace les statistiques héritables du personnage par celles du blueprint. Son identité et sa position sont conservées ; un lien précédent est remplacé. Vérifier l’aperçu avant application."</p>
                        <button on:click=move |_|send.run(edit(state.get_untracked(),move |_,source| {
                            *source = source.with_actor_blueprint_binding(id(&selected.get_untracked())?,id(&blueprint.get_untracked())?,ActorBlueprintOverrides::default()).map_err(|e|e.to_string())?;
                            Ok(())
                        }))>"Examiner le lien au blueprint sélectionné"</button>
                        <button on:click=move |_|send.run(edit(state.get_untracked(),move |_,source| {
                            *source = source.without_actor_blueprint(id(&blueprint.get_untracked())?).map_err(|e|e.to_string())?;
                            Ok(())
                        }))>"Examiner la suppression du blueprint inutilisé"</button>
                    </Show>
                    <p>{move ||binding().map(|b| {
                        let origin = |local|if local {"local"} else {"hérité"};
                        let label = document().and_then(|d|d.actor_blueprints().and_then(|l|l.definitions.iter().find(|definition|definition.id == b.blueprint).map(|definition|definition.name.clone()))).unwrap_or_else(||"Blueprint absent".into());
                        format!("Lien {label} v{} — rôle : {}, vitalité : {}, protection : {}, inventaire : {}, fuite : {}. Retoucher les statistiques du personnage crée des modifications locales pour les seuls champs changés.",b.blueprint_version,origin(b.overrides.role.is_some()),origin(b.overrides.max_vitality.is_some()),origin(b.overrides.protection.is_some()),origin(b.overrides.inventory_capacity.is_some()),origin(b.overrides.flees_danger.is_some()))
                    })}</p>
                    <label>"Rétablir l’héritage"<select prop:value=move ||restore.get() on:change=move |e|restore.set(event_target_value(&e))>
                        <option value="all">"Tous les champs"</option><option value="role">"Rôle"</option><option value="vitality">"Vitalité"</option><option value="protection">"Protection"</option><option value="capacity">"Inventaire"</option><option value="flees">"Fuite"</option>
                    </select></label>
                    <button on:click=move |_|send.run(edit(state.get_untracked(),move |_,source| {
                        let actor = id(&selected.get_untracked())?;
                        let mut overrides = source.actor_blueprints().and_then(|l|l.bindings.iter().find(|b|b.actor == actor)).ok_or("Ce personnage n’est pas lié à un blueprint")?.overrides.clone();
                        match restore.get_untracked().as_str() {
                            "all" => overrides = ActorBlueprintOverrides::default(),
                            "role" => overrides.role = None, "vitality" => overrides.max_vitality = None,
                            "protection" => overrides.protection = None, "capacity" => overrides.inventory_capacity = None,
                            "flees" => overrides.flees_danger = None, _ => return Err("Choisir un champ".into()),
                        }
                        *source = source.with_actor_blueprint_overrides(actor,overrides).map_err(|e|e.to_string())?;
                        Ok(())
                    }))>"Examiner le retour aux valeurs héritées"</button>
                    <button on:click=move |_|send.run(edit(state.get_untracked(),move |_,source| {
                        *source = source.without_actor_blueprint_binding(id(&selected.get_untracked())?).map_err(|e|e.to_string())?;
                        Ok(())
                    }))>"Examiner le détachement en conservant les statistiques"</button>
                </fieldset>
            </details>
        </Show>
    }
}
