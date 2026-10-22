//! Structured authoring through the zone's existing preview/apply/history boundary.
use std::collections::BTreeSet;

use forge_core::{CommandId, EntityId, Vec3};
use forge_gameplay::{
    ActorDefinition, ActorRole, Condition, DialogueChoice, DialogueDefinition, DialogueEffect,
    DialogueNode, GameplayDocument, ItemDefinition, ObjectDefinition, ObjectKind, ObjectPhysics,
    PlaceDefinition, QuestDefinition, QuestStatus, RulesProfile,
};
use forge_project::authoring::{AuthoringRequest, AuthoringState};
use leptos::prelude::*;

type Edit = Result<AuthoringRequest, String>;

type FieldSource = (String, Option<forge_core::ContentHash>);

fn source_hash(state: RwSignal<Option<AuthoringState>>) -> Option<forge_core::ContentHash> {
    state
        .get()
        .and_then(|state| state.zone_head)
        .and_then(|head| head.content_hash().ok())
}

mod blueprint_controls;
mod cinematic_controls;
mod container_controls;
mod creator_controls;
mod dialogue_controls;
mod faction_controls;
mod item_controls;
mod quest_flow_controls;
mod script_controls;
mod temporal_controls;
mod variable_controls;
mod variable_value_field;

struct PhysicsFields {
    mass: String,
    push: bool,
    throwing: bool,
    flammable: bool,
    heat_damage_per_tick: String,
    break_energy: String,
    dimensions: [String; 3],
}

impl PhysicsFields {
    fn apply(&self, properties: &mut ObjectPhysics, door: bool) -> Result<(), String> {
        let mass = number(&self.mass)?;
        if door && (mass != 0.0 || self.push || self.throwing) {
            return Err("Une porte doit rester fixe pour être ouverte ou fermée.".into());
        }
        properties.mass_kg = mass;
        properties.pushable = self.push;
        properties.throwable = self.throwing;
        let heat_damage_per_tick =
            self.heat_damage_per_tick
                .trim()
                .parse::<u32>()
                .map_err(|_| {
                    "Les dégâts de chaleur doivent être un entier positif ou nul.".to_owned()
                })?;
        if heat_damage_per_tick > 1_000_000 {
            return Err("Les dégâts de chaleur ne peuvent pas dépasser 1 000 000 par tick.".into());
        }
        properties.flammable = self.flammable;
        properties.heat_damage_per_tick = heat_damage_per_tick;
        properties.break_energy_j = if self.break_energy.trim().is_empty() {
            None
        } else {
            Some(number(&self.break_energy)?)
        };
        properties.body_half_extents_m = Vec3::new(
            number(&self.dimensions[0])? * 0.5,
            number(&self.dimensions[1])? * 0.5,
            number(&self.dimensions[2])? * 0.5,
        );
        Ok(())
    }
}

#[component]
#[allow(
    clippy::too_many_lines,
    reason = "Structured fields share one preview callback and selection owner"
)]
pub(crate) fn GameplayEditor(
    state: RwSignal<Option<AuthoringState>>,
    busy: RwSignal<bool>,
    send: Callback<Edit>,
) -> impl IntoView {
    let kind = RwSignal::new("friendly".to_owned());
    let name = RwSignal::new("Nouveau personnage".to_owned());
    let x = RwSignal::new("0".to_owned());
    let z = RwSignal::new("0".to_owned());
    let y = RwSignal::new(String::new());
    let selected = RwSignal::new(String::new());
    let reply = RwSignal::new("Bonjour. Que puis-je faire pour vous ?".to_owned());
    let mass = RwSignal::new("10".to_owned());
    let width = RwSignal::new("1".to_owned());
    let height = RwSignal::new("1".to_owned());
    let depth = RwSignal::new("1".to_owned());
    let push = RwSignal::new(false);
    let throwing = RwSignal::new(false);
    let flammable = RwSignal::new(false);
    let heat_damage_per_tick = RwSignal::new("0".to_owned());
    let break_energy = RwSignal::new(String::new());
    let giver = RwSignal::new(String::new());
    let required_item = RwSignal::new(String::new());
    let child = RwSignal::new(String::new());
    let destination = RwSignal::new(String::new());
    let vitality = RwSignal::new("100".to_owned());
    let protection = RwSignal::new("0".to_owned());
    let capacity = RwSignal::new("0".to_owned());
    let flees = RwSignal::new(true);
    let patrol = RwSignal::new(String::new());
    let physics_fields = move || PhysicsFields {
        mass: mass.get_untracked(),
        push: push.get_untracked(),
        throwing: throwing.get_untracked(),
        flammable: flammable.get_untracked(),
        heat_damage_per_tick: heat_damage_per_tick.get_untracked(),
        break_energy: break_energy.get_untracked(),
        dimensions: [
            width.get_untracked(),
            height.get_untracked(),
            depth.get_untracked(),
        ],
    };
    let disabled = move || {
        busy.get()
            || state
                .get()
                .is_none_or(|s| s.zone_head.is_none() || s.playtest_hash.is_some())
    };
    let document = move || {
        state
            .get()
            .and_then(|s| s.zone_head)
            .and_then(|h| h.corrections.gameplay.map(|d| *d))
    };
    let surface_visible = move |surface| document().is_some_and(|d| d.surface_visible(surface));
    Effect::new(move |_| {
        if let Some(document) = document()
            && !creator_controls::kind_visible(&document, &kind.get_untracked())
        {
            kind.set(String::new());
        }
    });
    Effect::new(move |previous: Option<Option<forge_core::AreaId>>| {
        let area = state.get().and_then(|s| s.document.map(|d| d.area_id));
        if previous != Some(area) {
            selected.set(String::new());
            giver.set(String::new());
            required_item.set(String::new());
            child.set(String::new());
            destination.set(String::new());
            let start = state
                .get_untracked()
                .and_then(|s| s.zone_head)
                .and_then(|h| h.corrections.start);
            x.set(start.as_ref().map_or(0.0, |s| s.x_m).to_string());
            z.set(start.as_ref().map_or(0.0, |s| s.z_m).to_string());
        }
        area
    });
    Effect::new(
        move |previous: Option<(String, Option<forge_core::ContentHash>)>| {
            let selection = selected.get();
            let session = state.get();
            let head = session.as_ref().and_then(|s| s.zone_head.as_ref());
            let key = (selection.clone(), head.and_then(|h| h.content_hash().ok()));
            if previous.as_ref() != Some(&key)
                && let Ok(id) = id(&selection)
                && let Some(document) = head.and_then(|h| h.corrections.gameplay.as_deref())
            {
                let point = if let Some(actor) = document.actor(id) {
                    name.set(actor.name.clone());
                    vitality.set(actor.max_vitality.to_string());
                    protection.set(actor.protection.to_string());
                    capacity.set(actor.inventory_capacity.to_string());
                    flees.set(actor.flees_danger);
                    patrol.set(
                        actor
                            .patrol
                            .iter()
                            .map(|point| format!("{} {} {}", point.x, point.z, point.y))
                            .collect::<Vec<_>>()
                            .join("\n"),
                    );
                    if let Some(node) = actor
                        .dialogue
                        .and_then(|id| document.dialogue(id))
                        .and_then(|d| d.nodes.iter().find(|n| n.id == d.start))
                    {
                        reply.set(node.text.clone());
                    }
                    Some(actor.position)
                } else if let Some(object) = document.object(id) {
                    name.set(object.name.clone());
                    mass.set(object.physics.mass_kg.to_string());
                    push.set(object.physics.pushable);
                    throwing.set(object.physics.throwable);
                    flammable.set(object.physics.flammable);
                    heat_damage_per_tick.set(object.physics.heat_damage_per_tick.to_string());
                    break_energy.set(
                        object
                            .physics
                            .break_energy_j
                            .map(|n| n.to_string())
                            .unwrap_or_default(),
                    );
                    width.set((object.physics.body_half_extents_m.x * 2.0).to_string());
                    height.set((object.physics.body_half_extents_m.y * 2.0).to_string());
                    depth.set((object.physics.body_half_extents_m.z * 2.0).to_string());
                    Some(object.position)
                } else {
                    document.places.iter().find(|p| p.id == id).map(|p| {
                        name.set(p.name.clone());
                        p.position
                    })
                };
                if let Some(point) = point {
                    x.set(point.x.to_string());
                    y.set(point.y.to_string());
                    z.set(point.z.to_string());
                } else {
                    selected.set(String::new());
                }
            }
            key
        },
    );
    view! {
        <details class="authoring-gameplay-editor"><summary>"Personnages, objets et objectifs"</summary>
        <fieldset disabled=disabled>
        <Show when=move ||document().is_none()>
            <button on:click=move |_|send.run(initialize(state.get_untracked()))>"Activer les règles de jeu dans cette zone"</button>
        </Show>
        <Show when=move ||document().is_some()>
            <creator_controls::CreatorControls state=state busy=busy send=send/>
            <For each=move ||state.get().and_then(|state|state.document.map(|document|document.area_id))
                key=|area|*area children=move |_|view! {
                    <item_controls::ItemControls state=state busy=busy send=send/>
                    <blueprint_controls::BlueprintControls state=state busy=busy send=send selected=selected/>
                    <container_controls::ContainerControls state=state busy=busy send=send selected=selected/>
                    <variable_controls::VariableControls state=state busy=busy send=send/>
                    <dialogue_controls::DialogueControls state=state busy=busy send=send/>
                    <quest_flow_controls::QuestFlowControls state=state busy=busy send=send/>
                    <faction_controls::FactionControls state=state busy=busy send=send/>
                    <temporal_controls::TemporalControls state=state busy=busy send=send/>
                    <script_controls::ScriptControls state=state busy=busy send=send/>
                    <cinematic_controls::CinematicControls state=state busy=busy send=send/>
                }/>
            <label>"Type"<select prop:value=move ||kind.get() on:change=move |e|{
                let value=event_target_value(&e);
                if value=="door" {width.set("1.2".into());height.set("2".into());depth.set("0.2".into());mass.set("0".into());push.set(false);throwing.set(false);}
                else {width.set("1".into());height.set("1".into());depth.set("1".into());mass.set("10".into());}
                kind.set(value);
            }>
                <option value="">"Choisir un type visible"</option>
                {move ||document().map(|d|[("friendly","PNJ et dialogue"),("enemy","Ennemi"),("pickup","Objet à ramasser"),("door","Porte"),("prop","Objet physique"),("place","Lieu")].into_iter().filter(|(kind,_)|creator_controls::kind_visible(&d,kind)).map(|(kind,label)|view!{<option value=kind>{label}</option>}).collect_view())}
            </select></label>
            <label>"Nom"<input prop:value=move ||name.get() on:input=move |e|name.set(event_target_value(&e))/></label>
            <label>"X (m)"<input type="number" prop:value=move ||x.get() on:input=move |e|x.set(event_target_value(&e))/></label>
            <label>"Z (m)"<input type="number" prop:value=move ||z.get() on:input=move |e|z.set(event_target_value(&e))/></label>
            <label>"Hauteur du centre (m, vide = terrain)"<input type="number" prop:value=move ||y.get() on:input=move |e|y.set(event_target_value(&e))/></label>
            <Show when=move ||surface_visible(forge_gameplay::CreatorSurface::Dialogues)><label>"Réplique"<textarea prop:value=move ||reply.get() on:input=move |e|reply.set(event_target_value(&e))/></label></Show>
            <Show when=move ||surface_visible(forge_gameplay::CreatorSurface::Objects)><details><summary>"Propriétés physiques"</summary>
                <label>"Masse (kg)"<input type="number" min="0" prop:value=move ||mass.get() on:input=move |e|mass.set(event_target_value(&e))/></label>
                <label>"Largeur (m)"<input type="number" min="0.05" prop:value=move ||width.get() on:input=move |e|width.set(event_target_value(&e))/></label>
                <label>"Hauteur (m)"<input type="number" min="0.05" prop:value=move ||height.get() on:input=move |e|height.set(event_target_value(&e))/></label>
                <label>"Profondeur (m)"<input type="number" min="0.05" prop:value=move ||depth.get() on:input=move |e|depth.set(event_target_value(&e))/></label>
                <label><input type="checkbox" prop:checked=move ||push.get() on:change=move |e|push.set(event_target_checked(&e))/>"Poussable"</label>
                <label><input type="checkbox" prop:checked=move ||throwing.get() on:change=move |e|throwing.set(event_target_checked(&e))/>"Lançable"</label>
                <label><input type="checkbox" prop:checked=move ||flammable.get() on:change=move |e|flammable.set(event_target_checked(&e))/>"Inflammable"</label>
                <label>"Dégâts de chaleur par tick (0 = aucun dégât)"<input type="number" min="0" max="1000000" step="1" prop:value=move ||heat_damage_per_tick.get() on:input=move |e|heat_damage_per_tick.set(event_target_value(&e))/></label>
                <label>"Énergie de rupture (J, vide = solide)"<input type="number" prop:value=move ||break_energy.get() on:input=move |e|break_energy.set(event_target_value(&e))/></label>
            </details></Show>
            <button on:click=move |_|send.run(add(state.get_untracked(),&kind.get_untracked(),&name.get_untracked(),(&x.get_untracked(),&y.get_untracked(),&z.get_untracked()),&reply.get_untracked(),&physics_fields()))>"Examiner l’ajout"</button>
            <label>"Élément à retoucher"<select prop:value=move ||selected.get() on:change=move |e|selected.set(event_target_value(&e))>
                <option value="">"Choisir"</option>
                {move ||document().map(|d| {
                    let actors=d.surface_visible(forge_gameplay::CreatorSurface::Actors);
                    let objects=d.surface_visible(forge_gameplay::CreatorSurface::Objects);
                    let places=d.surface_visible(forge_gameplay::CreatorSurface::Places);
                    d.actors.into_iter().filter(move |_|actors).map(|a|(a.id,a.name)).chain(d.objects.into_iter().filter(move |_|objects).map(|o|(o.id,o.name))).chain(d.places.into_iter().filter(move |_|places).map(|p|(p.id,p.name))).map(|(id,label)|view!{<option value=id.to_string()>{label}</option>}).collect_view()
                })}
            </select></label>
            <button on:click=move |_|send.run(retouch(state.get_untracked(),&selected.get_untracked(),&name.get_untracked(),(&x.get_untracked(),&y.get_untracked(),&z.get_untracked()),&reply.get_untracked()))>"Examiner le nom, la position et la réplique"</button>
            <Show when=move ||surface_visible(forge_gameplay::CreatorSurface::Objects)><button on:click=move |_|send.run(retouch_physics(state.get_untracked(),&selected.get_untracked(),&physics_fields()))>"Examiner les propriétés physiques"</button></Show>
            <button on:click=move |_|send.run(remove(state.get_untracked(),&selected.get_untracked()))>"Examiner la suppression de cet élément"</button>
            <Show when=move ||surface_visible(forge_gameplay::CreatorSurface::Actors)><details><summary>"Statistiques du personnage sélectionné"</summary>
                <label>"Vitalité maximale"<input type="number" min="1" step="1" prop:value=move ||vitality.get() on:input=move |e|vitality.set(event_target_value(&e))/></label>
                <label>"Protection"<input type="number" min="0" step="1" prop:value=move ||protection.get() on:input=move |e|protection.set(event_target_value(&e))/></label>
                <label>"Capacité d’inventaire"<input type="number" min="0" step="1" prop:value=move ||capacity.get() on:input=move |e|capacity.set(event_target_value(&e))/></label>
                <label><input type="checkbox" prop:checked=move ||flees.get() on:change=move |e|flees.set(event_target_checked(&e))/>"Réagit au danger par la fuite"</label>
                <button on:click=move |_|send.run(retouch_actor(state.get_untracked(),&selected.get_untracked(),&vitality.get_untracked(),&protection.get_untracked(),&capacity.get_untracked(),flees.get_untracked()))>"Examiner les statistiques"</button>
            </details>
            <details><summary>"Patrouille du personnage sélectionné"</summary>
                <label>"Points dans l’ordre : X Z hauteur du centre (m), un point par ligne. Hauteur facultative = terrain."<textarea aria-label="Points de patrouille" prop:value=move ||patrol.get() on:input=move |event|patrol.set(event_target_value(&event))/></label>
                <p>"Le personnage suit les points en boucle. Effacer tous les points arrête la patrouille. Les obstacles peuvent rendre un trajet inaccessible."</p>
                <button on:click=move |_|send.run(retouch_patrol(state.get_untracked(),&selected.get_untracked(),&patrol.get_untracked()))>"Examiner la patrouille"</button>
            </details></Show>
            <Show when=move ||surface_visible(forge_gameplay::CreatorSurface::Quests)><details><summary>"Quête de récupération"</summary>
                <label>"PNJ donneur"<select prop:value=move ||giver.get() on:change=move |e|giver.set(event_target_value(&e))><option value="">"Choisir"</option>{move ||document().map(|d|d.actors.into_iter().filter(|a|a.dialogue.is_some()).map(|a|view!{<option value=a.id.to_string()>{a.name}</option>}).collect_view())}</select></label>
                <label>"Objet demandé"<select prop:value=move ||required_item.get() on:change=move |e|required_item.set(event_target_value(&e))><option value="">"Choisir"</option>{move ||document().map(|d|d.items.into_iter().map(|i|view!{<option value=i.id.to_string()>{i.name}</option>}).collect_view())}</select></label>
                <button on:click=move |_|send.run(quest(state.get_untracked(),&name.get_untracked(),&giver.get_untracked(),&required_item.get_untracked()))>"Examiner la quête et ses réponses"</button>
            </details></Show>
            <Show when=move ||surface_visible(forge_gameplay::CreatorSurface::Objects)><details><summary>"Mécanismes et supports"</summary>
                <label>"Charge portée"<select prop:value=move ||child.get() on:change=move |e|child.set(event_target_value(&e))><option value="">"Choisir"</option>{move ||document().map(|d|d.objects.into_iter().map(|o|view!{<option value=o.id.to_string()>{o.name}</option>}).collect_view())}</select></label>
                <button on:click=move |_|send.run(support(state.get_untracked(),&selected.get_untracked(),&child.get_untracked()))>"Examiner le support et sa charge"</button>
                <button on:click=move |_|send.run(remove_support(state.get_untracked(),&selected.get_untracked(),&child.get_untracked()))>"Examiner le retrait de cette charge"</button>
            </details></Show>
            <Show when=move ||surface_visible(forge_gameplay::CreatorSurface::Places)><details><summary>"Liaisons entre lieux"</summary>
                <label>"Lieu de destination"<select prop:value=move ||destination.get() on:change=move |e|destination.set(event_target_value(&e))><option value="">"Choisir"</option>{move ||document().map(|d|d.places.into_iter().map(|p|view!{<option value=p.id.to_string()>{p.name}</option>}).collect_view())}</select></label>
                <button on:click=move |_|send.run(link(state.get_untracked(),&selected.get_untracked(),&destination.get_untracked()))>"Examiner la liaison entre les lieux"</button>
                <button on:click=move |_|send.run(remove_link(state.get_untracked(),&selected.get_untracked(),&destination.get_untracked()))>"Examiner le retrait de cette liaison"</button>
            </details></Show>
        </Show>
        </fieldset></details>
        <crate::authoring_gameplay_visuals::GameplayVisualControls state=state busy=busy send=send/>
        <crate::module_export_ui::ModuleExportControls state=state busy=busy/>
    }
}

fn initialize(state: Option<AuthoringState>) -> Edit {
    let state = state.ok_or("Ouvrir une zone")?;
    let head = state
        .zone_head
        .as_ref()
        .ok_or("Une zone agrégée est requise")?;
    if head.corrections.gameplay.is_some() {
        return Err("Les règles sont déjà présentes.".into());
    }
    let module_id = state
        .gameplay_module_id
        .ok_or("Module propriétaire absent")?;
    let area_id = head.base.terrain.area_id;
    let player = EntityId::deterministic(format!("gameplay/{area_id}/player"));
    let start = head.corrections.start.as_ref();
    let (x, z) = start.map_or((0.0, 0.0), |s| (s.x_m, s.z_m));
    let position = position(&state, &x.to_string(), "", &z.to_string(), 0.85)?;
    let document = GameplayDocument {
        narrative: None,
        factions: None,
        simulation: None,
        scripting: None,
        cinematics: None,
        creator: None,
        schema: forge_gameplay::GAMEPLAY_SCHEMA.into(),
        version: forge_gameplay::GAMEPLAY_VERSION,
        module_id,
        area_id,
        profile: RulesProfile {
            id: "forge.gate3-minimal".into(),
            version: 1,
            interaction_range_m: 3.0,
            attack_range_m: 2.0,
            attack_damage: 20,
            attack_stamina_cost: 5,
            max_stamina: 100,
            max_action_receipts: 4096,
            max_events_per_tick: 128,
        },
        player,
        actors: vec![ActorDefinition {
            id: player,
            name: "Joueur".into(),
            role: ActorRole::Player,
            position,
            max_vitality: 100,
            protection: 0,
            inventory_capacity: 24,
            dialogue: None,
            patrol: vec![],
            flees_danger: false,
        }],
        items: vec![],
        objects: vec![],
        places: vec![],
        dialogues: vec![],
        quests: vec![],
    };
    let document = if let Some(definition) = state
        .module_creator_context
        .as_ref()
        .and_then(|context| context.definition.as_ref())
    {
        definition
            .apply_to(&document)
            .map_err(|error| error.to_string())?
    } else {
        document
    };
    preview(state, document)
}

fn edit(
    state: Option<AuthoringState>,
    change: impl FnOnce(&AuthoringState, &mut GameplayDocument) -> Result<(), String>,
) -> Edit {
    let state = state.ok_or("Ouvrir une zone")?;
    let mut document = state
        .zone_head
        .as_ref()
        .and_then(|h| h.corrections.gameplay.as_deref())
        .cloned()
        .ok_or("Activer les règles de jeu")?;
    change(&state, &mut document)?;
    if document.creator.is_some() {
        let used = document.used_capabilities();
        let config = document
            .creator
            .as_mut()
            .ok_or("Configuration Creator absente")?;
        let mut required: BTreeSet<_> = config.required_capabilities.iter().copied().collect();
        required.extend(used);
        config.required_capabilities = required.into_iter().collect();
    }
    preview(state, document)
}

fn preview(state: AuthoringState, document: GameplayDocument) -> Edit {
    if state.playtest_hash.is_some() {
        return Err("Revenir à l’édition avant de retoucher.".into());
    }
    let head = state.zone_head.ok_or("Une zone agrégée est requise")?;
    let hash = forge_core::canonical_json_sha256(&document).map_err(|e| e.to_string())?;
    let key = format!("gameplay-edit/{}/{hash}", head.content_hash()?);
    head.prepare_gameplay(Some(document), CommandId::deterministic(key))
        .map(|request| AuthoringRequest::PreviewZone { request })
}

fn id(value: &str) -> Result<EntityId, String> {
    value
        .parse()
        .map_err(|_| "Choisir un élément valide".into())
}
fn number(value: &str) -> Result<f32, String> {
    value
        .trim()
        .parse::<f32>()
        .ok()
        .filter(|n| n.is_finite())
        .ok_or_else(|| "Valeur numérique invalide".into())
}

#[allow(
    clippy::cast_possible_truncation,
    reason = "Canonical terrain heights fit the validated world-coordinate range"
)]
fn position(
    state: &AuthoringState,
    x: &str,
    y: &str,
    z: &str,
    offset: f32,
) -> Result<Vec3, String> {
    let x = number(x)?;
    let z = number(z)?;
    let y = if y.trim().is_empty() {
        let head = state.zone_head.as_ref().ok_or("Zone absente")?;
        let terrain = crate::authoring_scene::terrain(head)?;
        let ground = if terrain.sampled_source.is_some() {
            let bytes = crate::authoring_raster::source_bytes(&terrain)?;
            forge_world::sampled_terrain::SampledTerrainPlan::from_document(&terrain, &bytes)
                .and_then(|plan| plan.height_at(f64::from(x), f64::from(z)))
                .map_err(|e| e.to_string())? as f32
        } else {
            forge_world::TerrainGenerationPlan::from_document(&terrain)
                .and_then(|plan| plan.height_at(x, z))
                .map_err(|e| e.to_string())?
        };
        ground + offset + 0.02
    } else {
        number(y)?
    };
    Ok(Vec3::new(x, y, z))
}

#[allow(
    clippy::too_many_arguments,
    reason = "Inputs come from the structured authoring form"
)]
fn add(
    state: Option<AuthoringState>,
    kind: &str,
    name: &str,
    xyz: (&str, &str, &str),
    reply: &str,
    physics: &PhysicsFields,
) -> Edit {
    edit(state, |state, document| {
        let key = format!(
            "gameplay/{}/{kind}/{name}/{}",
            document.area_id,
            document.actors.len() + document.objects.len() + document.places.len()
        );
        let id = EntityId::deterministic(&key);
        let position = position(
            state,
            xyz.0,
            xyz.1,
            xyz.2,
            if matches!(kind, "friendly" | "enemy") {
                0.95
            } else {
                number(&physics.dimensions[1])? * 0.5
            },
        )?;
        match kind {
            "friendly" | "enemy" => {
                let dialogue = (kind == "friendly")
                    .then(|| EntityId::deterministic(format!("{key}/dialogue")));
                document.actors.push(ActorDefinition {
                    id,
                    name: name.into(),
                    role: if kind == "enemy" {
                        ActorRole::Enemy
                    } else {
                        ActorRole::Friendly
                    },
                    position,
                    max_vitality: 100,
                    protection: 0,
                    inventory_capacity: 0,
                    dialogue,
                    patrol: vec![],
                    flees_danger: true,
                });
                if let Some(dialogue) = dialogue {
                    let node = EntityId::deterministic(format!("{key}/node"));
                    document.dialogues.push(DialogueDefinition {
                        id: dialogue,
                        start: node,
                        nodes: vec![DialogueNode {
                            id: node,
                            text: reply.into(),
                            choices: vec![],
                        }],
                    });
                }
            }
            "place" => document.places.push(PlaceDefinition {
                id,
                name: name.into(),
                position,
                links: vec![],
            }),
            "pickup" | "door" | "prop" => {
                let kind = match kind {
                    "pickup" => {
                        let item = EntityId::deterministic(format!("{key}/item"));
                        document.items.push(ItemDefinition {
                            id: item,
                            name: name.into(),
                            max_stack: 24,
                        });
                        ObjectKind::Pickup { item, quantity: 1 }
                    }
                    "door" => ObjectKind::Door {
                        initially_open: false,
                    },
                    _ => ObjectKind::Prop,
                };
                let mut properties = ObjectPhysics::default();
                physics.apply(&mut properties, matches!(kind, ObjectKind::Door { .. }))?;
                document.objects.push(ObjectDefinition {
                    id,
                    name: name.into(),
                    position,
                    kind,
                    physics: properties,
                });
            }
            _ => return Err("Type inconnu".into()),
        }
        Ok(())
    })
}

fn retouch(
    state: Option<AuthoringState>,
    selected: &str,
    name: &str,
    xyz: (&str, &str, &str),
    reply: &str,
) -> Edit {
    let selected = id(selected)?;
    edit(state, |state, document| {
        let position = position(
            state,
            xyz.0,
            xyz.1,
            xyz.2,
            if document.actor(selected).is_some() {
                0.95
            } else {
                document
                    .object(selected)
                    .map_or(0.5, |o| o.physics.body_half_extents_m.y)
            },
        )?;
        if let Some(actor) = document.actors.iter_mut().find(|a| a.id == selected) {
            actor.name = name.into();
            actor.position = position;
            if let Some(dialogue) = actor
                .dialogue
                .and_then(|id| document.dialogues.iter_mut().find(|d| d.id == id))
                && let Some(node) = dialogue.nodes.iter_mut().find(|n| n.id == dialogue.start)
            {
                node.text = reply.into();
            }
        } else if let Some(object) = document.objects.iter_mut().find(|o| o.id == selected) {
            object.name = name.into();
            object.position = position;
        } else if let Some(place) = document.places.iter_mut().find(|p| p.id == selected) {
            place.name = name.into();
            place.position = position;
        } else {
            return Err(format!("Élément absent : {selected}"));
        }
        Ok(())
    })
}

fn retouch_patrol(state: Option<AuthoringState>, selected: &str, points: &str) -> Edit {
    let selected = id(selected)?;
    edit(state, |state, document| {
        let mut patrol = Vec::new();
        for (line_index, line) in points.lines().enumerate() {
            if line.trim().is_empty() {
                continue;
            }
            let values: Vec<_> = line.split_whitespace().collect();
            let (x, z, y) = match values.as_slice() {
                [x, z] => (*x, *z, ""),
                [x, z, y] => (*x, *z, *y),
                _ => {
                    return Err(format!(
                        "Point {} : saisir X Z et une hauteur facultative.",
                        line_index + 1
                    ));
                }
            };
            if patrol.len() >= 64 {
                return Err("La patrouille est limitée à 64 points.".into());
            }
            patrol.push(position(state, x, y, z, 0.95)?);
        }
        let actor = document
            .actors
            .iter_mut()
            .find(|actor| actor.id == selected)
            .ok_or("Choisir un personnage")?;
        if actor.role == ActorRole::Player {
            return Err("La patrouille concerne un personnage non joueur.".into());
        }
        actor.patrol = patrol;
        Ok(())
    })
}

fn retouch_physics(state: Option<AuthoringState>, selected: &str, physics: &PhysicsFields) -> Edit {
    let selected = id(selected)?;
    edit(state, |_, document| {
        let object = document
            .objects
            .iter_mut()
            .find(|o| o.id == selected)
            .ok_or("Choisir un objet physique")?;
        physics.apply(
            &mut object.physics,
            matches!(object.kind, ObjectKind::Door { .. }),
        )
    })
}

fn retouch_actor(
    state: Option<AuthoringState>,
    selected: &str,
    vitality: &str,
    protection: &str,
    capacity: &str,
    flees: bool,
) -> Edit {
    let selected = id(selected)?;
    let integer = |value: &str| {
        value
            .trim()
            .parse::<u32>()
            .map_err(|_| "Saisir un entier positif ou nul".to_owned())
    };
    let vitality = integer(vitality)?;
    let protection = integer(protection)?;
    let capacity = integer(capacity)?;
    edit(state, |_, document| {
        let actor = document
            .actors
            .iter_mut()
            .find(|a| a.id == selected)
            .ok_or("Choisir un personnage")?;
        let before = actor.clone();
        actor.max_vitality = vitality;
        actor.protection = protection;
        actor.inventory_capacity = capacity;
        actor.flees_danger = flees;
        if let Some(binding) = document
            .creator
            .as_mut()
            .and_then(|creator| creator.blueprints.as_mut())
            .and_then(|library| {
                library
                    .bindings
                    .iter_mut()
                    .find(|binding| binding.actor == selected)
            })
        {
            binding.overrides.capture_changes(&before, actor);
        }
        Ok(())
    })
}

fn remove(state: Option<AuthoringState>, selected: &str) -> Edit {
    let selected = id(selected)?;
    edit(state, |_, document| {
        if selected == document.player {
            return Err("Le joueur est requis pour cette zone.".into());
        }
        let references = references_to(document, selected);
        if !references.is_empty() {
            return Err(format!(
                "Suppression refusée : cet élément est utilisé par {}. Retouchez ces liens ou conditions avant de le supprimer.",
                references.join(" ; ")
            ));
        }
        let dialogue = document.actor(selected).and_then(|a| a.dialogue);
        let before = document.actors.len() + document.objects.len() + document.places.len();
        document.actors.retain(|a| a.id != selected);
        document.objects.retain(|o| o.id != selected);
        document.places.retain(|p| p.id != selected);
        if before == document.actors.len() + document.objects.len() + document.places.len() {
            return Err(
                "L’élément sélectionné n’existe plus. Choisissez un élément courant.".into(),
            );
        }
        if let Some(dialogue) = dialogue
            && !document.actors.iter().any(|a| a.dialogue == Some(dialogue))
        {
            document.dialogues.retain(|d| d.id != dialogue);
        }
        Ok(())
    })
}

fn references_to(document: &GameplayDocument, target: EntityId) -> Vec<String> {
    let mut references = Vec::new();
    if document.actor_blueprints().is_some_and(|library| {
        library
            .bindings
            .iter()
            .any(|binding| binding.actor == target)
    }) {
        references
            .push("un blueprint de personnage (détacher le personnage avant suppression)".into());
    }
    for quest in &document.quests {
        if quest.giver == target {
            references.push(format!("le donneur de la quête « {} »", quest.name));
        }
    }
    for object in &document.objects {
        if object.physics.supports.contains(&target) {
            references.push(format!("le support « {} »", object.name));
        }
    }
    references.extend(required_item_references(document, target));
    for place in &document.places {
        if place.links.contains(&target) {
            references.push(format!("la liaison du lieu « {} »", place.name));
        }
    }
    for dialogue in &document.dialogues {
        for node in &dialogue.nodes {
            for choice in &node.choices {
                let condition = matches!(choice.condition, Condition::ActorDefeated { actor } if actor == target)
                    || matches!(choice.condition, Condition::ObjectDestroyed { object } if object == target);
                let effect = choice.effects.iter().any(|effect| matches!(effect, DialogueEffect::SetDoor { object, .. } if *object == target));
                if condition || effect {
                    references.push(format!(
                        "la condition ou l’effet de la réponse « {} »",
                        choice.text
                    ));
                }
            }
        }
    }
    references
}

fn required_item_references(document: &GameplayDocument, target: EntityId) -> Vec<String> {
    let Some(object) = document.object(target) else {
        return Vec::new();
    };
    if !matches!(
        object.kind,
        ObjectKind::Pickup { .. } | ObjectKind::Container { .. }
    ) {
        return Vec::new();
    }
    document
        .quests
        .iter()
        .filter_map(|quest| {
            let required = u64::from(quest.required_quantity);
            let before = document.potential_item_quantities(
                None,
                quest.id,
                quest.required_item,
                required,
            );
            let after = document.potential_item_quantities(Some(target),quest.id,quest.required_item,required);
            let before_quantity = before.get(&quest.required_item).copied().unwrap_or_default();
            let after_quantity = after.get(&quest.required_item).copied().unwrap_or_default();
            let item_name = document.item(quest.required_item)
                .map_or_else(||quest.required_item.to_string(),|item|item.name.clone());
            (after_quantity < required && after_quantity < before_quantity).then(|| {
                format!(
                    "la quête « {} » n’a plus de source démontrée pour la quantité requise de « {} »",
                    quest.name, item_name
                )
            })
        })
        .collect()
}

fn quest(state: Option<AuthoringState>, name: &str, giver: &str, item: &str) -> Edit {
    let giver = id(giver)?;
    let item = id(item)?;
    edit(state, |_, document| {
        let quest = EntityId::deterministic(format!(
            "quest/{}/{name}/{}",
            document.area_id,
            document.quests.len()
        ));
        let dialogue = document
            .actor(giver)
            .and_then(|a| a.dialogue)
            .ok_or("Le donneur doit avoir un dialogue")?;
        let dialogue = document
            .dialogues
            .iter_mut()
            .find(|d| d.id == dialogue)
            .ok_or("Dialogue absent")?;
        let node = dialogue
            .nodes
            .iter_mut()
            .find(|n| n.id == dialogue.start)
            .ok_or("Réplique absente")?;
        for (label, status, effect) in [
            (
                "J’accepte.",
                QuestStatus::Available,
                DialogueEffect::AcceptQuest { quest },
            ),
            (
                "Voici l’objet demandé.",
                QuestStatus::Active,
                DialogueEffect::CompleteQuest { quest },
            ),
        ] {
            node.choices.push(DialogueChoice {
                id: EntityId::deterministic(format!("{quest}/{label}")),
                text: label.into(),
                condition: Condition::Quest { quest, status },
                effects: vec![effect],
                next: None,
            });
        }
        document.quests.push(QuestDefinition {
            id: quest,
            name: name.into(),
            giver,
            required_item: item,
            required_quantity: 1,
            reward_item: None,
            reward_quantity: 0,
        });
        Ok(())
    })
}

fn support(state: Option<AuthoringState>, parent: &str, child: &str) -> Edit {
    let parent = id(parent)?;
    let child = id(child)?;
    edit(state, |_, document| {
        let parent = document
            .objects
            .iter_mut()
            .find(|o| o.id == parent)
            .ok_or("Choisir un objet support")?;
        if !parent.physics.supports.contains(&child) {
            parent.physics.supports.push(child);
        }
        Ok(())
    })
}

fn link(state: Option<AuthoringState>, source: &str, destination: &str) -> Edit {
    let source = id(source)?;
    let destination = id(destination)?;
    edit(state, |_, document| {
        let source = document
            .places
            .iter_mut()
            .find(|p| p.id == source)
            .ok_or("Choisir un lieu de départ")?;
        if !source.links.contains(&destination) {
            source.links.push(destination);
        }
        Ok(())
    })
}

fn remove_support(state: Option<AuthoringState>, parent: &str, child: &str) -> Edit {
    let parent = id(parent)?;
    let child = id(child)?;
    edit(state, |_, document| {
        let object = document
            .objects
            .iter_mut()
            .find(|o| o.id == parent)
            .ok_or("Choisir un objet support")?;
        if !object.physics.supports.contains(&child) {
            return Err("Cette charge n’est pas portée par le support sélectionné.".into());
        }
        object.physics.supports.retain(|id| *id != child);
        Ok(())
    })
}

fn remove_link(state: Option<AuthoringState>, source: &str, destination: &str) -> Edit {
    let source = id(source)?;
    let destination = id(destination)?;
    edit(state, |_, document| {
        let place = document
            .places
            .iter_mut()
            .find(|p| p.id == source)
            .ok_or("Choisir un lieu de départ")?;
        if !place.links.contains(&destination) {
            return Err("Cette liaison n’existe pas sur le lieu sélectionné.".into());
        }
        place.links.retain(|id| *id != destination);
        Ok(())
    })
}
