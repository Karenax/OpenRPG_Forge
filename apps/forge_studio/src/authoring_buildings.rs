//! Building drafts use the canonical zone commands; no independent persistence.
use forge_core::{CommandId, EntityId, Vec3};
use forge_project::{
    authoring::{AuthoringRequest, AuthoringState},
    building::RoomDimensions,
    zone_authoring::{
        ZoneAuthoringHead,
        buildings::{AuthoredBuilding, BuildingEdit},
    },
};
use leptos::prelude::*;

type SendCommand = Callback<Result<AuthoringRequest, String>>;

pub(super) fn preview_label(
    state: &AuthoringState,
    receipt: &forge_project::zone_authoring::session::ZoneReceipt,
) -> Option<String> {
    let head = state.zone_head.as_ref()?;
    if head.corrections.buildings == receipt.applied.buildings {
        return None;
    }
    super::authoring_preview::clear();
    let before = head.corrections.buildings.as_deref().unwrap_or_default();
    let after = receipt.applied.buildings.as_deref().unwrap_or_default();
    let projection = super::authoring_building_projection::preview(head, receipt).map_or_else(
        |error| format!("Aperçu 3D refusé : {error}"),
        |()| "Aperçu cyan sans collision demandé ; bâtiments acceptés conservés".into(),
    );
    Some(format!(
        "Proposition bâtiments · {} → {} pièces · non appliquée. {}. {projection}. Appliquer puis sauvegarder explicitement.",
        before.len(),
        after.len(),
        describe_changes(before, after),
    ))
}

fn describe_changes(before: &[AuthoredBuilding], after: &[AuthoredBuilding]) -> String {
    let mut lines = Vec::new();
    for building in after {
        if before.iter().any(|old| old == building) {
            continue;
        }
        let d = building.dimensions;
        lines.push(format!(
            "{} : {} × {} × {} m ; murs {} m ; plancher/toit {} m ; passage {} × {} m ; porte X={} m ({}) ; {} ; position ({}, {}, {}) m, orientation {}°",
            building.name,
            d.width_m,
            d.depth_m,
            d.clear_height_m,
            d.wall_m,
            d.slab_m,
            d.door_width_m,
            d.door_height_m,
            building.door_offset_override_m.unwrap_or(d.door_offset_x_m),
            if building.door_offset_override_m.is_some() {
                "retouche"
            } else {
                "recette"
            },
            if building.locked {
                "verrouillé"
            } else {
                "modifiable"
            },
            building.origin_m.x, building.origin_m.y, building.origin_m.z, building.yaw_degrees,
        ));
    }
    for removed in before
        .iter()
        .filter(|old| !after.iter().any(|new| new.id == old.id))
    {
        lines.push(format!("Suppression : {} ({})", removed.name, removed.id));
    }
    lines.join(" · ")
}

fn head(state: Option<AuthoringState>) -> Result<ZoneAuthoringHead, String> {
    let state = state.ok_or("Ouvrir une zone d’abord")?;
    if state.playtest_hash.is_some() {
        return Err("Revenir en édition avant de modifier un bâtiment".into());
    }
    state
        .zone_head
        .ok_or_else(|| "Convertir la zone au format agrégé d’abord".into())
}

fn prepare(state: Option<AuthoringState>, edit: BuildingEdit) -> Result<AuthoringRequest, String> {
    let head = head(state)?;
    let key = serde_json::to_string(&("building", head.content_hash()?, &edit))
        .map_err(|e| e.to_string())?;
    Ok(AuthoringRequest::PreviewZone {
        request: head.prepare_building(CommandId::deterministic(key), edit)?,
    })
}

fn number(text: &str) -> Result<f32, String> {
    text.parse::<f32>()
        .ok()
        .filter(|n| n.is_finite())
        .ok_or_else(|| "Saisir une valeur numérique finie en mètres".into())
}

#[component]
pub(super) fn BuildingControls(
    state: RwSignal<Option<AuthoringState>>,
    busy: RwSignal<bool>,
    selected: RwSignal<String>,
    on_command: SendCommand,
) -> impl IntoView {
    let x = RwSignal::new("0".to_owned());
    let y = RwSignal::new("0".to_owned());
    let z = RwSignal::new("0".to_owned());
    // Preview/status updates do not change the accepted building. Keep the
    // inspector mounted until its selected source actually changes.
    let inspected = Memo::new(move |_| {
        let chosen = selected.get();
        state
            .get()
            .and_then(|s| s.zone_head)
            .and_then(|h| h.corrections.buildings)
            .and_then(|buildings| buildings.into_iter().find(|b| b.id.to_string() == chosen))
    });
    super::authoring_draft_scope::invalidate_on_change(move || [x.get(), y.get(), z.get()]);
    view! {
        <div class="atelier-building-controls">
            <details><summary>"Conseils et état de construction"</summary><p>"Pièce de plain-pied. Aperçu cyan sans collision avant application ; Abandonner restaure l’affichage accepté. Le playtest attend les collisions synchronisées. Le passage joué reste à qualifier."</p>
            <super::authoring_building_projection::ProjectionStatus/>
            </details>
            <fieldset disabled=move || busy.get() || state.get().is_none_or(|s| s.zone_head.is_none() || s.playtest_hash.is_some())>
                <details open hidden=move || !selected.get().is_empty()>
                <summary>"Nouvelle pièce · 8 × 6 m · hauteur 3 m"</summary>
                <div class="atelier-building-origin" role="group" aria-label="Origine de la pièce en mètres ; Y est l’altitude du sol fini">
                    <NumberField label="X (m)" value=x/>
                    <NumberField label="Y · sol (m)" value=y/>
                    <NumberField label="Z (m)" value=z/>
                </div>
                <button on:click=move |_| on_command.run((|| {
                    let current = head(state.get_untracked())?;
                    let id = EntityId::deterministic(format!("building:{}", current.content_hash()?));
                    prepare(state.get_untracked(), BuildingEdit::Insert { building: AuthoredBuilding {
                        id, name: "Pièce".into(), generator_version: 1,
                        origin_m: Vec3::new(number(&x.get_untracked())?, number(&y.get_untracked())?, number(&z.get_untracked())?),
                        yaw_degrees: 0.0, locked: false, door_offset_override_m: None,
                        dimensions: RoomDimensions { width_m: 8.0, depth_m: 6.0,
                            clear_height_m: 3.0, wall_m: 0.25, slab_m: 0.2,
                            door_width_m: 1.4, door_height_m: 2.2, door_offset_x_m: 0.0 },
                    } })
                })())>"Prévisualiser une nouvelle pièce"</button>
                </details>
                <button on:click=move |_| super::authoring_building_projection::selection::arm()>"Sélectionner un bâtiment dans la vue"</button>
                <label>"Bâtiment à inspecter"
                    <select prop:value=move || selected.get()
                        on:change=move |ev| {
                            super::authoring_building_projection::selection::cancel();
                            selected.set(event_target_value(&ev));
                        }>
                        <option value="">"Nouvelle pièce…"</option>
                        {move || state.get().and_then(|s| s.zone_head).and_then(|h| h.corrections.buildings)
                            .unwrap_or_default().into_iter().map(|building| view! {
                                <option value=building.id.to_string()>{format!("{}{}", building.name,
                                    if building.locked { " (verrouillé)" } else { "" })}</option>
                            }).collect_view()}
                    </select>
                </label>
                {move || {
                    match inspected.get() {
                        Some(building) => view! {
                            <RoomInspector building=building state=state on_command=on_command/>
                        }.into_any(),
                        None => view! { <p>"Choisir un bâtiment existant pour le cadrer et le retoucher. La sélection ne modifie pas la zone."</p> }.into_any(),
                    }
                }}
            </fieldset>
        </div>
    }
}

#[component]
fn NumberField(label: &'static str, value: RwSignal<String>) -> impl IntoView {
    view! {
        <label>{label}<input type="number" step="0.1" prop:value=move || value.get()
            on:input=move |event| value.set(event_target_value(&event))/></label>
    }
}

#[component]
fn RoomInspector(
    building: AuthoredBuilding,
    state: RwSignal<Option<AuthoringState>>,
    on_command: SendCommand,
) -> impl IntoView {
    #[derive(Clone, Copy, PartialEq)]
    enum Subject {
        Pose,
        Volume,
        Door,
        Manage,
    }
    let subject = RwSignal::new(Subject::Pose);
    let id = building.id;
    let on_command = super::authoring_draft_scope::protect(state, on_command);
    let dimensions = building.dimensions;
    let locked = building.locked;
    let name = RwSignal::new(building.name.clone());
    let x = RwSignal::new(building.origin_m.x.to_string());
    let y = RwSignal::new(building.origin_m.y.to_string());
    let z = RwSignal::new(building.origin_m.z.to_string());
    let yaw = RwSignal::new(building.yaw_degrees.to_string());
    let room_view = super::authoring_building_view::frame(&building, false);
    let door_view = super::authoring_building_view::frame(&building, true);
    let width = RwSignal::new(dimensions.width_m.to_string());
    let depth = RwSignal::new(dimensions.depth_m.to_string());
    let height = RwSignal::new(dimensions.clear_height_m.to_string());
    let door_width = RwSignal::new(dimensions.door_width_m.to_string());
    let door_height = RwSignal::new(dimensions.door_height_m.to_string());
    let wall = RwSignal::new(dimensions.wall_m.to_string());
    let slab = RwSignal::new(dimensions.slab_m.to_string());
    let door = RwSignal::new(
        building
            .door_offset_override_m
            .unwrap_or(dimensions.door_offset_x_m)
            .to_string(),
    );
    super::authoring_draft_scope::invalidate_on_change(move || {
        [
            name.get(),
            x.get(),
            y.get(),
            z.get(),
            yaw.get(),
            width.get(),
            depth.get(),
            height.get(),
            door_width.get(),
            door_height.get(),
            wall.get(),
            slab.get(),
            door.get(),
        ]
    });
    view! {
        <div class="atelier-building-inspector">
            <nav class="atelier-building-subjects" aria-label="Réglages du bâtiment">
                {[(Subject::Pose, "Position"), (Subject::Volume, "Volume"), (Subject::Door, "Porte"), (Subject::Manage, "Gérer")].into_iter().map(move |(value, label)| view! {
                    <button type="button" aria-pressed=move || (subject.get() == value).to_string() on:click=move |_| subject.set(value)>{label}</button>
                }).collect_view()}
            </nav>
            <section hidden=move || subject.get() != Subject::Door>
            <details><summary>"Porte acceptée et repères"</summary>
            <p>{super::authoring_building_view::door_origin(dimensions.door_offset_x_m, building.door_offset_override_m, locked)}</p>
            </details>
            <button on:click=move |_| frame_view(door_view)>"Cadrer la porte"</button>
            <Show when=super::embedded_playtest_available>
                <button on:click=move |_| on_command.run(play_from_door(state.get_untracked(), id))>"Jouer devant cette porte"</button>
                <details><summary>"Départ temporaire du test"</summary><p>"Départ temporaire sur le terrain, 2 m devant la porte acceptée, vue orientée vers l’entrée. Aucun point sauvegardé ; les autres obstacles ne sont pas écartés."</p></details>
            </Show>
            </section>
            <section hidden=move || subject.get() != Subject::Manage>
            <details><summary>"Identité et valeurs acceptées"</summary>
            <p>{id.to_string()}</p>
            <p>{format!("Accepté : {} × {} × {} m · porte {} · position ({}, {}, {}) m",
                dimensions.width_m, dimensions.depth_m, dimensions.clear_height_m,
                building.door_offset_override_m.map_or_else(|| "héritée".into(), |v| format!("retouchée à {v} m")),
                building.origin_m.x, building.origin_m.y, building.origin_m.z)}</p></details>
            </section>
            <button hidden=move || subject.get() != Subject::Pose on:click=move |_| frame_view(room_view)>"Cadrer ce bâtiment"</button>
            <fieldset disabled=locked>
                <section hidden=move || subject.get() != Subject::Manage>
                <label>"Nom du bâtiment"<input maxlength="128" prop:value=move || name.get()
                    on:input=move |ev| name.set(event_target_value(&ev))/></label>
                <button on:click=move |_| on_command.run(prepare(state.get_untracked(),
                    BuildingEdit::SetName { id, name: name.get_untracked() }))>"Prévisualiser le nom du bâtiment"</button>
                </section>
                <section hidden=move || subject.get() != Subject::Pose>
                <div class="atelier-building-origin">
                <NumberField label="Position X (m)" value=x/>
                <NumberField label="Position Y (m)" value=y/>
                <NumberField label="Position Z (m)" value=z/>
                </div>
                <NumberField label="Orientation Y (degrés)" value=yaw/>
                <button on:click=move |_| on_command.run((|| prepare(state.get_untracked(), BuildingEdit::SetPose {
                    id, origin_m: Vec3::new(number(&x.get_untracked())?, number(&y.get_untracked())?, number(&z.get_untracked())?),
                    yaw_degrees: number(&yaw.get_untracked())?,
                }))())>"Prévisualiser position et orientation"</button>
                </section>
                <section hidden=move || subject.get() != Subject::Volume>
                <div class="atelier-building-origin">
                <NumberField label="Largeur (m)" value=width/>
                <NumberField label="Profondeur (m)" value=depth/>
                <NumberField label="Hauteur intérieure (m)" value=height/>
                </div>
                <div class="gold-inline-fields">
                <NumberField label="Largeur de passage de porte (m)" value=door_width/>
                <NumberField label="Hauteur de passage de porte (m)" value=door_height/>
                </div>
                <details><summary>"Épaisseurs et contraintes"</summary>
                <NumberField label="Épaisseur des murs (m)" value=wall/>
                <NumberField label="Épaisseur du plancher et du toit (m)" value=slab/>
                <p>"Dimensions en mètres. Le décalage de porte retouché est conservé ; une ouverture incompatible avec la pièce est refusée avant application."</p>
                </details>
                <button on:click=move |_| on_command.run((|| prepare(state.get_untracked(), BuildingEdit::SetDimensions {
                    id, dimensions: RoomDimensions { width_m: number(&width.get_untracked())?,
                        depth_m: number(&depth.get_untracked())?, clear_height_m: number(&height.get_untracked())?,
                        door_width_m: number(&door_width.get_untracked())?,
                        door_height_m: number(&door_height.get_untracked())?,
                        wall_m: number(&wall.get_untracked())?, slab_m: number(&slab.get_untracked())?,
                        ..dimensions },
                }))())>"Prévisualiser les dimensions"</button>
                </section>
                <section hidden=move || subject.get() != Subject::Door>
                <NumberField label="Retouche porte : X local absolu (m)" value=door/>
                <button on:click=move |_| on_command.run(number(&door.get_untracked()).and_then(|offset|
                    prepare(state.get_untracked(), BuildingEdit::SetDoorOffset { id, offset_m: Some(offset) })))>"Prévisualiser la retouche de porte"</button>
                <button on:click=move |_| on_command.run(prepare(state.get_untracked(), BuildingEdit::SetDoorOffset { id, offset_m: None }))>"Revenir à la porte de la recette"</button>
                </section>
                <section hidden=move || subject.get() != Subject::Manage>
                <button on:click=move |_| on_command.run(prepare(state.get_untracked(), BuildingEdit::Remove { id }))>"Prévisualiser la suppression"</button>
                </section>
            </fieldset>
            <section hidden=move || subject.get() != Subject::Manage>
            <DuplicateRoom building=building.clone() state on_command/>
            <button on:click=move |_| on_command.run(prepare(state.get_untracked(), BuildingEdit::SetLocked { id, locked: !locked }))>
                {if locked { "Prévisualiser le déverrouillage" } else { "Prévisualiser le verrouillage" }}
            </button>
            </section>
        </div>
    }
}

#[component]
fn DuplicateRoom(
    building: AuthoredBuilding,
    state: RwSignal<Option<AuthoringState>>,
    on_command: SendCommand,
) -> impl IntoView {
    let id = building.id;
    let name = RwSignal::new(format!("{} — copie", building.name));
    let x = RwSignal::new((building.origin_m.x + building.dimensions.width_m + 2.0).to_string());
    let y = RwSignal::new(building.origin_m.y.to_string());
    let z = RwSignal::new(building.origin_m.z.to_string());
    super::authoring_draft_scope::invalidate_on_change(move || {
        [name.get(), x.get(), y.get(), z.get()]
    });
    view! {
        <details><summary>"Dupliquer ce bâtiment"</summary>
            <p>"Copie des réglages acceptés, porte retouchée comprise. Original intact, copie déverrouillée. Vérifier la position ; les chevauchements ne sont pas résolus automatiquement."</p>
            <label>"Nom de la copie"<input maxlength="128" prop:value=move || name.get()
                on:input=move |ev| name.set(event_target_value(&ev))/></label>
            <NumberField label="Copie : X (m)" value=x/>
            <NumberField label="Copie : Y (m)" value=y/>
            <NumberField label="Copie : Z (m)" value=z/>
            <button on:click=move |_| on_command.run((|| {
                let state = state.get_untracked();
                let current = head(state.clone())?;
                let name = name.get_untracked();
                let origin_m = Vec3::new(number(&x.get_untracked())?, number(&y.get_untracked())?, number(&z.get_untracked())?);
                let key = serde_json::to_string(&("duplicate-building", current.content_hash()?, id, &name, origin_m)).map_err(|e| e.to_string())?;
                prepare(state, BuildingEdit::Duplicate { id, new_id: EntityId::deterministic(key), name, origin_m })
            })())>"Prévisualiser la copie"</button>
        </details>
    }
}

fn frame_view((pivot, eye): (Vec3, Vec3)) {
    let convert = |p: Vec3| bevy::prelude::Vec3::new(p.x, p.y, p.z);
    super::authoring_camera::focus_from(convert(pivot), convert(eye));
}

fn play_from_door(state: Option<AuthoringState>, id: EntityId) -> Result<AuthoringRequest, String> {
    let current = state.as_ref().ok_or("Ouvrir une zone avant de jouer")?;
    let building = current
        .zone_head
        .as_ref()
        .and_then(|h| h.corrections.buildings.as_ref())
        .and_then(|buildings| buildings.iter().find(|b| b.id == id))
        .ok_or("Bâtiment absent de la zone courante")?;
    let start = super::authoring_building_view::entrance_start(building);
    super::authoring_playtest_controls::prepare_at(state, start)
}
