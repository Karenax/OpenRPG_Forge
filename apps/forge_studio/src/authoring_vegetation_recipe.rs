//! Per-layer recipe controls using the ordinary transactional regeneration route.
use crate::authoring_zone::reconciliation::ReconciliationControls;
use crate::reconciliation_protocol::ReconciliationRecipe;
use forge_project::{
    VegetationLayer,
    authoring::{AuthoringRequest, AuthoringState},
};
use leptos::prelude::*;

#[component]
pub(super) fn LayerControls(
    state: RwSignal<Option<AuthoringState>>,
    busy: RwSignal<bool>,
    selected: RwSignal<String>,
    on_command: Callback<Result<AuthoringRequest, String>>,
) -> impl IntoView {
    let expanded = RwSignal::new(false);
    Effect::new(move |_| {
        if !selected.get().is_empty() {
            expanded.set(true);
        }
    });
    view! {
        <details open=move || expanded.get()>
            <summary>"Couches : nom, ordre, densité et espacement"</summary>
            <p>"La densité est une probabilité de placement, pas un nombre garanti d’arbres. Masques peints, exclusions et relief continuent de s’appliquer. Les masques verrouillés sont conservés ; ils ne figent pas la recette."</p>
            <button on:click=move |_| selected.set(String::new())>"Afficher toutes les couches"</button>
            <For each=move || {
                let key = selected.get();
                state.get().and_then(|s| s.zone_head).and_then(|h| h.base.vegetation)
                    .map_or_else(Vec::new, |v| v.layers.into_iter().filter(|l| key.is_empty() || l.id.to_string() == key).collect())
                }
                key=move |layer| (state.get_untracked().and_then(|s| s.zone_head).map(|h| h.base.terrain.area_id), layer.id.clone(), layer.display_name.clone(),
                    layer.acceptance_probability.to_bits(), layer.minimum_spacing_m.to_bits(),
                    layer.minimum_altitude_m.to_bits(), layer.maximum_altitude_m.to_bits(),
                    layer.maximum_slope_gradient.to_bits())
                children=move |layer| view! {
                    <LayerRecipe layer=layer state=state busy=busy on_command=on_command/>
                }/>
        </details>
    }
}

#[component]
fn LayerRecipe(
    layer: VegetationLayer,
    state: RwSignal<Option<AuthoringState>>,
    busy: RwSignal<bool>,
    on_command: Callback<Result<AuthoringRequest, String>>,
) -> impl IntoView {
    let probability = RwSignal::new((layer.acceptance_probability * 100.0).to_string());
    let spacing = RwSignal::new(layer.minimum_spacing_m.to_string());
    let habitat = layer.clone();
    let id = layer.id;
    let order_id = id.clone();
    let membership_id = id.clone();
    let name_id = id.clone();
    let name = RwSignal::new(layer.display_name.clone());
    let on_command = crate::authoring_draft_scope::protect(state, on_command);
    let target = id.clone();
    let recipe = Memo::new(move |_| {
        Ok(ReconciliationRecipe::Layer {
            layer_id: target.clone(),
            acceptance_probability: recipe_number(&probability.get())? / 100.0,
            minimum_spacing_m: recipe_number(&spacing.get())?,
        })
    });
    view! {
        <fieldset disabled=move || busy.get() || state.get().is_none_or(|s| s.playtest_hash.is_some())>
            <legend>{layer.display_name}</legend>
            <HabitatControls layer=habitat state=state busy=busy on_command=on_command/>
            <label>"Nom de la couche"<input type="text" prop:value=move || name.get()
                on:input=move |ev| {
                    name.set(event_target_value(&ev));
                    super::authoring_shortcuts::cancel_preview();
                }/></label>
            <button on:click=move |_| {
                let result = state.get_untracked().and_then(|s| s.zone_head)
                    .ok_or_else(|| "Zone absente".to_owned())
                    .and_then(|head| forge_world::editor_vegetation::prepare_layer_name_request(
                        &head, &name_id, &name.get_untracked()))
                    .map(|request| AuthoringRequest::PreviewZone { request });
                on_command.run(result);
            }>"Examiner le nouveau nom"</button>
            <p>"Réordonner conserve les identifiants, la seed et les masques peints. Examiner puis Appliquer."</p>
            {[true, false].into_iter().map(|up| {
                let id = order_id.clone();
                view! { <button on:click=move |_| {
                    let result = state.get_untracked().and_then(|s| s.zone_head)
                        .ok_or_else(|| "Zone absente".to_owned())
                        .and_then(|head| forge_world::editor_vegetation::prepare_layer_order_request(&head, &id, up))
                        .map(|request| AuthoringRequest::PreviewZone { request });
                    on_command.run(result);
                }>{if up { "Monter cette couche" } else { "Descendre cette couche" }}</button> }
            }).collect_view()}
            <p>"Copier reprend les réglages acceptés, sans masque peint. Retirer refuse les masques orphelins ; aucune correction n’est effacée automatiquement."</p>
            {[forge_world::editor_vegetation::LayerMembershipEdit::Copy, forge_world::editor_vegetation::LayerMembershipEdit::Remove].into_iter().map(|edit| {
                let id = membership_id.clone();
                view! { <button on:click=move |_| {
                    let result = state.get_untracked().and_then(|s| s.zone_head)
                        .ok_or_else(|| "Zone absente".to_owned())
                        .and_then(|head| forge_world::editor_vegetation::prepare_layer_membership_request(&head, &id, edit))
                        .map(|request| AuthoringRequest::PreviewZone { request });
                    on_command.run(result);
                }>{match edit { forge_world::editor_vegetation::LayerMembershipEdit::Copy => "Examiner une copie de recette", forge_world::editor_vegetation::LayerMembershipEdit::Remove => "Examiner le retrait de cette couche" }}</button> }
            }).collect_view()}
            <label>"Densité de la recette (%)"<input type="number" min="0" max="100" step="1"
                prop:value=move || probability.get() on:input=move |ev| {
                    probability.set(event_target_value(&ev));
                    super::authoring_shortcuts::cancel_preview();
                }/></label>
            <label>"Espacement minimum (m)"<input type="number" min="0" step="0.1"
                prop:value=move || spacing.get() on:input=move |ev| {
                    spacing.set(event_target_value(&ev));
                    super::authoring_shortcuts::cancel_preview();
                }/></label>
            <button on:click=move |_| {
                let result = (|| {
                    let head = state.get_untracked().and_then(|s| s.zone_head).ok_or("Zone absente")?;
                    let number = |value: String| value.trim().parse::<f32>().map_err(|_| "Nombre invalide");
                    forge_world::editor_vegetation::prepare_layer_request(
                        &head, &id, number(probability.get_untracked())? / 100.0,
                        number(spacing.get_untracked())?,
                    ).map(|request| AuthoringRequest::PreviewZone { request })
                })();
                on_command.run(result);
            }>"Examiner les réglages de cette couche"</button>
            <ReconciliationControls state busy recipe on_command/>
        </fieldset>
    }
}

#[component]
fn HabitatControls(
    layer: VegetationLayer,
    state: RwSignal<Option<AuthoringState>>,
    busy: RwSignal<bool>,
    on_command: Callback<Result<AuthoringRequest, String>>,
) -> impl IntoView {
    let minimum = RwSignal::new(layer.minimum_altitude_m.to_string());
    let maximum = RwSignal::new(layer.maximum_altitude_m.to_string());
    let slope = RwSignal::new((layer.maximum_slope_gradient * 100.0).to_string());
    let target = layer.id.clone();
    let recipe = Memo::new(move |_| {
        Ok(ReconciliationRecipe::Habitat {
            layer_id: target.clone(),
            minimum_altitude_m: recipe_number(&minimum.get())?,
            maximum_altitude_m: recipe_number(&maximum.get())?,
            maximum_slope_gradient: recipe_number(&slope.get())? / 100.0,
        })
    });
    view! {
        <details>
            <summary>"Répartition : altitude et pente"</summary>
            <p>"Limites de génération, appliquées aussi aux prochaines régénérations. Les masques peints sont conservés mais ne forcent pas la pousse hors de ces limites. Pente : 100 % = 1 m de montée pour 1 m horizontal."</p>
            {[("Altitude minimum (m)", minimum, "-10000", "10000"),
              ("Altitude maximum (m)", maximum, "-10000", "10000"),
              ("Pente maximum (%)", slope, "0", "1600")].into_iter().map(|(label, value, min, max)| view! {
                <label>{label}<input type="number" min=min max=max step="0.1"
                    prop:value=move || value.get() on:input=move |ev| {
                        value.set(event_target_value(&ev));
                        super::authoring_shortcuts::cancel_preview();
                    }/></label>
            }).collect_view()}
            <button on:click=move |_| {
                let result = (|| {
                    let head = state.get_untracked().and_then(|s| s.zone_head).ok_or("Zone absente")?;
                    let number = |value: String| value.trim().parse::<f32>().map_err(|_| "Nombre invalide");
                    forge_world::editor_vegetation::prepare_layer_habitat_request(
                        &head, &layer.id, number(minimum.get_untracked())?,
                        number(maximum.get_untracked())?, number(slope.get_untracked())? / 100.0,
                    ).map(|request| AuthoringRequest::PreviewZone { request })
                })();
                on_command.run(result);
            }>"Examiner les limites de répartition"</button>
            <ReconciliationControls state busy recipe on_command/>
        </details>
    }
}

fn recipe_number(value: &str) -> Result<f32, String> {
    value
        .trim()
        .parse::<f32>()
        .ok()
        .filter(|n| n.is_finite())
        .ok_or_else(|| "Réglage numérique invalide".into())
}
