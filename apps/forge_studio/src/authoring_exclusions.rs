//! Authoring controls for canonical vegetation exclusion shapes.
use forge_project::{
    VegetationExclusion, VegetationExclusionId,
    authoring::{AuthoringRequest, AuthoringState},
    zone_authoring::exclusions::VegetationExclusionEdit,
};
use leptos::prelude::*;

fn number(value: &str, label: &str) -> Result<f32, String> {
    value
        .trim()
        .parse()
        .map_err(|_| format!("{label} invalide"))
}

fn request(
    state: Option<AuthoringState>,
    edit: VegetationExclusionEdit,
) -> Result<AuthoringRequest, String> {
    let head = state
        .and_then(|state| state.zone_head)
        .ok_or_else(|| "Ouvrir une zone avec végétation".to_owned())?;
    let identity = forge_core::canonical_json_sha256(&(head.content_hash()?, &edit))
        .map_err(|error| error.to_string())?;
    head.prepare_vegetation_exclusions(
        edit,
        forge_core::CommandId::deterministic(identity.to_string()),
    )
    .map(|request| AuthoringRequest::PreviewZone { request })
}

fn effective(
    head: &forge_project::zone_authoring::ZoneAuthoringHead,
) -> Result<Vec<VegetationExclusion>, String> {
    head.effective()?
        .1
        .ok_or_else(|| "Végétation absente".to_owned())
        .map(|vegetation| vegetation.exclusions)
}

fn ids(exclusions: &[VegetationExclusion]) -> String {
    exclusions
        .iter()
        .map(|exclusion| exclusion.id().to_string())
        .collect::<Vec<_>>()
        .join(", ")
}

pub(super) fn preview_label(
    state: &AuthoringState,
    receipt: &forge_project::zone_authoring::session::ZoneReceipt,
) -> Option<String> {
    if receipt.applied.vegetation_exclusions == receipt.inverse.vegetation_exclusions {
        return None;
    }
    crate::authoring_preview::clear();
    let result = state
        .zone_head
        .as_ref()
        .ok_or_else(|| "Zone absente".to_owned())
        .and_then(|head| crate::authoring_vegetation::preview_exclusions(head, receipt));
    Some(format!(
        "Exclusions de végétation · non appliquées. {}. Appliquer puis sauvegarder ; Abandonner restaure la recette acceptée.",
        result.map_or_else(
            |error| format!("Aperçu indisponible : {error}"),
            |()| "Projection de végétation demandée ; consulter son état".into(),
        )
    ))
}

#[component]
pub(super) fn ExclusionControls(
    state: RwSignal<Option<AuthoringState>>,
    busy: RwSignal<bool>,
    on_command: Callback<Result<AuthoringRequest, String>>,
) -> impl IntoView {
    let kind = RwSignal::new("circle".to_owned());
    let id = RwSignal::new("author_clearance".to_owned());
    let x = RwSignal::new("0".to_owned());
    let z = RwSignal::new("0".to_owned());
    let end_x = RwSignal::new("20".to_owned());
    let end_z = RwSignal::new("0".to_owned());
    let size = RwSignal::new("8".to_owned());
    let previous_area = RwSignal::new(None::<String>);
    Effect::new(move |_| {
        let area = state
            .get()
            .and_then(|value| value.zone_head)
            .map(|head| head.base.terrain.area_id.to_string());
        if previous_area
            .get_untracked()
            .is_some_and(|previous| Some(previous) != area)
        {
            crate::authoring_shortcuts::cancel_preview();
        }
        previous_area.set(area);
    });
    let preview = move || {
        let result = (|| {
            let head = state
                .get_untracked()
                .and_then(|value| value.zone_head)
                .ok_or_else(|| "Zone absente".to_owned())?;
            let mut exclusions = effective(&head)?;
            let exclusion = if kind.get_untracked() == "circle" {
                VegetationExclusion::Circle {
                    id: VegetationExclusionId::new(id.get_untracked()),
                    center_x_m: number(&x.get_untracked(), "X (m)")?,
                    center_z_m: number(&z.get_untracked(), "Z (m)")?,
                    radius_m: number(&size.get_untracked(), "Rayon (m)")?,
                }
            } else {
                VegetationExclusion::CorridorSegment {
                    id: VegetationExclusionId::new(id.get_untracked()),
                    start_x_m: number(&x.get_untracked(), "Départ X (m)")?,
                    start_z_m: number(&z.get_untracked(), "Départ Z (m)")?,
                    end_x_m: number(&end_x.get_untracked(), "Fin X (m)")?,
                    end_z_m: number(&end_z.get_untracked(), "Fin Z (m)")?,
                    half_width_m: number(&size.get_untracked(), "Demi-largeur (m)")?,
                }
            };
            exclusions.retain(|value| value.id() != exclusion.id());
            exclusions.push(exclusion);
            request(
                state.get_untracked(),
                VegetationExclusionEdit::Replace { exclusions },
            )
        })();
        on_command.run(result);
    };
    view! {
        <details><summary>"Exclusions de végétation"</summary>
            <fieldset disabled=move || busy.get() || state.get().is_none_or(|value| value.zone_head.is_none())>
                <p>"Cercles et corridors en mètres X/Z. Examiner, puis Appliquer utilise la commande centrale ; Abandonner restaure la tête acceptée."</p>
                <output>{move || state.get().and_then(|value| value.zone_head).map_or_else(|| "Aucune zone".into(), |head| {
                    let source = if head.corrections.vegetation_exclusions.is_some() { "auteur" } else { "recette" };
                    effective(&head).map_or_else(|error| error, |items| format!("{} exclusion(s) effectives · {}", items.len(), source))
                })}</output>
                <p>{move || state.get().and_then(|value| value.zone_head).map_or_else(|| "Auteur : aucune substitution".into(), |head| {
                    head.corrections.vegetation_exclusions.map_or_else(
                        || "Auteur : hérite de la recette".into(),
                        |items| format!("Auteur : {}", ids(&items)),
                    )
                })}</p>
                <label>"Type"<select prop:value=move || kind.get() on:change=move |event| { kind.set(event_target_value(&event)); crate::authoring_shortcuts::cancel_preview(); }><option value="circle">"Cercle"</option><option value="corridor">"Segment corridor"</option></select></label>
                <label>"ID stable"<input prop:value=move || id.get() on:input=move |event| { id.set(event_target_value(&event)); crate::authoring_shortcuts::cancel_preview(); }/></label>
                <label>"X départ/centre (m)"<input type="number" prop:value=move || x.get() on:input=move |event| { x.set(event_target_value(&event)); crate::authoring_shortcuts::cancel_preview(); }/></label>
                <label>"Z départ/centre (m)"<input type="number" prop:value=move || z.get() on:input=move |event| { z.set(event_target_value(&event)); crate::authoring_shortcuts::cancel_preview(); }/></label>
                <Show when=move || kind.get() == "corridor"><label>"X fin (m)"<input type="number" prop:value=move || end_x.get() on:input=move |event| { end_x.set(event_target_value(&event)); crate::authoring_shortcuts::cancel_preview(); }/></label><label>"Z fin (m)"<input type="number" prop:value=move || end_z.get() on:input=move |event| { end_z.set(event_target_value(&event)); crate::authoring_shortcuts::cancel_preview(); }/></label></Show>
                <label>{move || if kind.get() == "circle" { "Rayon (m)" } else { "Demi-largeur (m)" }}<input type="number" min="0" prop:value=move || size.get() on:input=move |event| { size.set(event_target_value(&event)); crate::authoring_shortcuts::cancel_preview(); }/></label>
                <button on:click=move |_| preview()>"Examiner l’exclusion"</button>
                <button on:click=move |_| { let result = (|| { let head = state.get_untracked().and_then(|value| value.zone_head).ok_or_else(|| "Zone absente".to_owned())?; let mut exclusions = effective(&head)?; let selected = id.get_untracked(); exclusions.retain(|value| value.id().as_str() != selected); request(state.get_untracked(), VegetationExclusionEdit::Replace { exclusions }) })(); on_command.run(result); } >"Examiner la suppression"</button>
                <button on:click=move |_| on_command.run(request(state.get_untracked(), VegetationExclusionEdit::RestoreInheritance))>"Examiner l’héritage de recette"</button>
                <p>{move || state.get().and_then(|value| value.zone_head).map_or_else(|| "Effectives : aucune".into(), |head| effective(&head).map_or_else(|error| error, |items| format!("Effectives : {}", ids(&items))))}</p>
            </fieldset>
        </details>
    }
}
