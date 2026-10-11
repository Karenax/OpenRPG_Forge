//! Picking prepares a regular, revision-bound zone proposal; never writes sources.
use forge_assets::asset_browser::AssetBrowser;
use forge_core::{CommandId, Transform, UnitQuaternion, Vec3};
use forge_project::{
    authoring::{AuthoringRequest, AuthoringState},
    zone_authoring::{
        ZoneAuthoringHead,
        placements::{ManualPlacement, PlacementEdit},
    },
};
use forge_world::placement_snap::PlacementSnap;
use leptos::prelude::*;

type SendCommand = Callback<Result<AuthoringRequest, String>>;
mod drag;
mod gizmo;
pub(super) mod insertion;
mod inspector;
mod local_translation;
mod orientation;
mod path_array;
mod pivot;
mod replacement;

#[component]
#[allow(
    clippy::too_many_lines,
    reason = "The Leptos placement view keeps its controls in one ownership scope"
)]
pub(super) fn PlacementControls(
    state: RwSignal<Option<AuthoringState>>,
    catalog: RwSignal<Option<AssetBrowser>>,
    selected: RwSignal<String>,
    inspected: RwSignal<Option<forge_core::EntityId>>,
    busy: RwSignal<bool>,
    host_busy: RwSignal<bool>,
    on_command: SendCommand,
) -> impl IntoView {
    let source_revision = super::placements::bind_sources();
    gizmo::bind_selection(state, on_command);
    // Availability can change without changing the inspected object or its draft.
    let inspected_placement = Memo::new(move |_| {
        let _ = source_revision.get();
        let head = state.get()?.zone_head?;
        let id = inspected.get()?;
        super::objects::lookup(&head, id)
            .ok()
            .map(|placement| (head.base.terrain.area_id, placement))
    });
    let scale = RwSignal::new("1".to_owned());
    let yaw = RwSignal::new("0".to_owned());
    let offset = RwSignal::new("0".to_owned());
    let grid_enabled = RwSignal::new(false);
    let grid_step = RwSignal::new("1".to_owned());
    let path_array = path_array::Draft::bind(state, inspected);
    view! {
        <fieldset class="atelier-placement-tools" disabled=move || busy.get() || host_busy.get() || state.get().is_none_or(|s| s.zone_head.is_none() || s.playtest_hash.is_some())>
            <legend>"Placer et manipuler"</legend>
            <details prop:open=move || inspected.get().is_none()><summary>"Ajouter un objet depuis le catalogue"</summary>
            <p>"Choisir un objet dans la bibliothèque, puis Placer et cliquer le terrain."</p>
            <div class="gold-inline-fields">
            <label>"Échelle uniforme"<input type="number" step="0.1" prop:value=move || scale.get() on:input=move |ev| scale.set(event_target_value(&ev))/></label>
            <label>"Rotation Y (degrés)"<input type="number" step="15" prop:value=move || yaw.get() on:input=move |ev| yaw.set(event_target_value(&ev))/></label>
            </div>
            <details><summary>"Grille et hauteur"</summary>
            <label>"Décalage vertical (m)"<input type="number" step="0.1" prop:value=move || offset.get() on:input=move |ev| offset.set(event_target_value(&ev))/></label>
            <label><input type="checkbox" prop:checked=move || grid_enabled.get()
                on:change=move |ev| grid_enabled.set(event_target_checked(&ev))/>
                "Aligner le clic sur la grille X/Z"</label>
            <label>"Pas de grille du placement (m)"<input type="number" min="0.01" max="128" step="0.01"
                disabled=move || !grid_enabled.get() prop:value=move || grid_step.get()
                on:input=move |ev| grid_step.set(event_target_value(&ev))/></label>
            </details>
            <button class="authoring-primary" disabled=move || busy.get() || host_busy.get() || selected.get().is_empty() on:click=move |_| {
                let grid = grid_enabled.get_untracked().then(|| grid_step.get_untracked());
                let settings = settings(&scale.get_untracked(), &yaw.get_untracked(), &offset.get_untracked(), grid.as_deref());
                match settings { Ok(settings) => arm(state, catalog, selected, busy, settings, on_command), Err(e) => on_command.run(Err(e)) }
            }>"Placer l’asset sélectionné sur le terrain"</button>
            </details>
            <details><summary>"Sélection et chargement"</summary>
            <label>"Objet placé à inspecter"
                <select prop:value=move || inspected.get().map_or_else(String::new, |id| id.to_string())
                    on:change=move |ev| {
                        let value = event_target_value(&ev);
                        if value.is_empty() {
                            super::placements::selection::choose(None);
                        } else {
                            let id = state.get_untracked().and_then(|s| s.zone_head).and_then(|h| super::objects::choices(&h,inspected.get_untracked()).ok()).and_then(|ps| ps.into_iter().find(|p| p.id.to_string() == value)).map(|p| p.id);
                            if let Some(id) = id { super::placements::selection::choose(Some(id)); }
                            else { on_command.run(Err("Objet absent de la zone courante ; actualiser la sélection".into())); }
                        }
                    }>
                    <option value="">"Choisir un objet…"</option>
                    {move || { let _ = source_revision.get(); state.get().and_then(|s| s.zone_head).and_then(|h| super::objects::choices(&h,inspected.get()).ok()).unwrap_or_default().into_iter().map(|p| view! {
                            <option value=p.id.to_string()>{p.name}</option>
                        }).collect_view()}}
                </select>
            </label>
            <p class="authoring-hint">"Cliquez directement sur un objet visible dans la carte pour ouvrir ses réglages ici."</p>
            <button hidden=move || inspected.get().is_none() on:click=move |_| super::placements::selection::choose(None)>"Fermer l’inspecteur de l’objet"</button>
            <super::placements::ProjectionStatus/>
            </details>
            {move || inspected_placement.get().into_iter().map(|(_, placement)| {
                view! { <inspector::PlacedObjectControls state=state placement=placement path_array=path_array catalog=catalog selected=selected busy=busy on_command=on_command/> }
            }).collect_view()}
        </fieldset>
    }
}

fn settings(
    scale: &str,
    yaw: &str,
    offset: &str,
    grid: Option<&str>,
) -> Result<(Transform, PlacementSnap), String> {
    let parse = |s: &str| {
        s.trim()
            .parse::<f32>()
            .map_err(|_| "Valeur numérique invalide".to_owned())
    };
    let scale = parse(scale)?;
    let yaw = parse(yaw)?;
    let offset = parse(offset)?;
    if !yaw.is_finite() || !offset.is_finite() {
        return Err("Valeur non finie refusée".into());
    }
    let half = (yaw % 360.0).to_radians() * 0.5;
    let transform = Transform {
        rotation: UnitQuaternion::new(0.0, half.sin(), 0.0, half.cos()),
        scale: Vec3::new(scale, scale, scale),
        ..Transform::IDENTITY
    };
    transform.validate().map_err(|e| e.to_string())?;
    let snap = PlacementSnap {
        grid_step_m: grid.map(parse).transpose()?,
        surface_offset_m: Some(offset),
    };
    snap.validate()?;
    Ok((transform, snap))
}

pub(super) fn head(state: Option<AuthoringState>) -> Result<ZoneAuthoringHead, String> {
    let state = state.ok_or("Ouvrir une zone")?;
    if state.playtest_hash.is_some() {
        return Err("Revenir à l’édition".into());
    }
    state
        .zone_head
        .ok_or_else(|| "Convertir la zone avant de placer des objets".into())
}

fn edit(state: Option<AuthoringState>, edit: PlacementEdit) -> Result<AuthoringRequest, String> {
    let head = head(state)?;
    let key = serde_json::to_string(&(head.content_hash()?, &edit)).map_err(|e| e.to_string())?;
    super::objects::edit(&head, edit, CommandId::deterministic(key))
        .map(|request| AuthoringRequest::PreviewZone { request })
}

fn arm(
    state: RwSignal<Option<AuthoringState>>,
    catalog: RwSignal<Option<AssetBrowser>>,
    selected: RwSignal<String>,
    busy: RwSignal<bool>,
    settings: (Transform, PlacementSnap),
    on_command: SendCommand,
) {
    let setup = (|| {
        let head = head(state.get_untracked())?;
        let name = selected.get_untracked();
        let catalog = super::catalog::resolve(catalog.get_untracked().as_ref(), &name)?;
        let reference = catalog.reference();
        Ok::<_, String>((head, catalog, name, reference))
    })();
    let (head, catalog, name, reference) = match setup {
        Ok(value) => value,
        Err(e) => {
            on_command.run(Err(e));
            return;
        }
    };
    super::cancel_preview();
    let ticket = super::preview::ticket();
    busy.set(true);
    super::placements::report("Vérification de l’asset avant placement…");
    wasm_bindgen_futures::spawn_local(async move {
        let result = catalog.check().await;
        busy.set(false);
        if !super::preview::is_current(ticket) || selected.get_untracked() != name {
            return;
        }
        if let Err(e) = result {
            on_command.run(Err(e));
            return;
        }
        super::placements::report(
            "Cliquer le terrain pour prévisualiser le placement. Échap annule.",
        );
        let display_name = catalog.label(&name);
        crate::authoring_brush_pointer::arm(
            2.0,
            false,
            Callback::new(move |points: Result<Vec<[f32; 2]>, String>| {
                let request = (|| {
                    catalog.still_available()?;
                    let current = self::head(state.get_untracked())?;
                    if current.content_hash()? != head.content_hash()? {
                        return Err(
                            "Zone modifiée depuis la demande ; recommencer le placement".into()
                        );
                    }
                    let point = *points?.first().ok_or("Cliquer un point du terrain")?;
                    insertion::prepare(&current, &display_name, reference, settings, point)
                })();
                if let Err(reason) = &request {
                    super::placements::report(format!("Placement refusé : {reason}"));
                }
                on_command.run(request);
            }),
        );
    });
}
