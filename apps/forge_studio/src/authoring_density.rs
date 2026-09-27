//! Density gestures use the shared picker and aggregate command path.
use forge_project::{
    authoring::{AuthoringRequest, AuthoringState},
    zone_authoring::{ZoneAuthoringHead, vegetation_commands::VegetationEdit},
};
use leptos::prelude::*;

fn head(state: Option<AuthoringState>) -> Result<ZoneAuthoringHead, String> {
    state
        .and_then(|s| s.zone_head)
        .ok_or("Ouvrir une zone unifiée avec végétation".into())
}

fn lock_request(
    state: Option<AuthoringState>,
    key: String,
    operation: u8,
) -> Result<AuthoringRequest, String> {
    let head = head(state)?;
    let layer_id = forge_project::VegetationLayerId::new(key);
    let edit = if operation == 2 {
        VegetationEdit::ClearDensityOverride { layer_id }
    } else {
        VegetationEdit::SetDensityLock {
            layer_id,
            locked: operation == 1,
        }
    };
    let identity = forge_core::canonical_json_sha256(&(head.content_hash()?, &edit))
        .map_err(|e| e.to_string())?;
    let request = head.prepare_vegetation_edit(
        edit,
        forge_core::CommandId::deterministic(identity.to_string()),
    )?;
    Ok(AuthoringRequest::PreviewZone { request })
}

fn parse_radius(value: &str) -> Result<f32, String> {
    let radius = value.parse::<f32>().map_err(|_| "Rayon invalide")?;
    if !radius.is_finite() || !(2.0..=128.0).contains(&radius) {
        return Err("Rayon requis : 2 à 128 mètres".into());
    }
    Ok(radius)
}

fn parse_strength(value: &str) -> Result<f32, String> {
    let strength = value
        .trim()
        .parse::<f32>()
        .map_err(|_| "Intensité invalide")?;
    if !strength.is_finite() || !(0.01..=1.0).contains(&strength) {
        return Err("Intensité requise : 0,01 à 1".into());
    }
    Ok(strength)
}

fn editable_layer(
    head: &ZoneAuthoringHead,
    key: &str,
) -> Result<forge_project::VegetationLayerId, String> {
    let vegetation = head
        .effective()?
        .1
        .ok_or("Initialiser une recette de végétation")?;
    let layer_id = vegetation
        .layers
        .iter()
        .find(|l| l.id.to_string() == key)
        .ok_or("Choisir une couche de végétation")?
        .id
        .clone();
    if head
        .corrections
        .density
        .iter()
        .any(|c| c.layer_id == layer_id && c.locked)
    {
        return Err("Déverrouiller la couche avant de peindre".into());
    }
    Ok(layer_id)
}

#[component]
pub(super) fn DensityControls(
    state: RwSignal<Option<AuthoringState>>,
    selected: RwSignal<String>,
    on_command: Callback<Result<AuthoringRequest, String>>,
) -> impl IntoView {
    let radius = RwSignal::new("24".to_owned());
    let strength = RwSignal::new("1".to_owned());
    let restore = RwSignal::new(false);
    let inspection = Memo::new(move |_| {
        state.with(|value| {
            super::authoring_density_inspector::inspect(value.as_ref(), &selected.get())
        })
    });
    let edit_lock = move |operation: u8| {
        on_command.run(lock_request(
            state.get_untracked(),
            selected.get_untracked(),
            operation,
        ));
    };
    let arm = move |continuous| {
        let prepared = (|| {
            let head = head(state.get_untracked())?;
            let hash = head.content_hash()?;
            let key = selected.get_untracked();
            let layer_id = editable_layer(&head, &key)?;
            let radius_m = parse_radius(&radius.get_untracked())?;
            let strength = parse_strength(&strength.get_untracked())?;
            Ok((
                head,
                hash,
                layer_id,
                radius_m,
                strength,
                if restore.get_untracked() { 255 } else { 0 },
            ))
        })();
        let (source, hash, layer_id, radius_m, strength, target_density) = match prepared {
            Ok(value) => value,
            Err(error) => {
                on_command.run(Err(error));
                return;
            }
        };
        super::authoring_brush_pointer::arm(
            radius_m,
            continuous,
            Callback::new(move |points: Result<Vec<[f32; 2]>, String>| {
                let result = (|| {
                    if head(state.get_untracked())?.content_hash()? != hash {
                        return Err("Trait périmé : la zone a changé".into());
                    }
                    let points = points?;
                    let identity = forge_core::canonical_json_sha256(&(
                        hash,
                        &layer_id,
                        radius_m,
                        strength,
                        target_density,
                        &points,
                    ))
                    .map_err(|e| e.to_string())?;
                    let request =
                        forge_world::vegetation_density_brush::VegetationDensityBrushStroke {
                            layer_id: layer_id.clone(),
                            radius_m,
                            strength,
                            target_density,
                            points,
                        }
                        .prepare_request(
                            &source,
                            forge_core::CommandId::deterministic(identity.to_string()),
                        )?;
                    Ok(AuthoringRequest::PreviewZone { request })
                })();
                on_command.run(result);
            }),
        );
    };
    on_cleanup(super::authoring_brush_pointer::cancel);
    view! {
        <Show when=move || state.get().and_then(|s| s.zone_head).is_some_and(|h| h.base.vegetation.is_some())>
            <fieldset><legend>"Densité de végétation"</legend>
                <label>"Couche de végétation"<select prop:value=move || selected.get() on:change=move |ev| {
                    super::authoring_brush_pointer::cancel();
                    super::authoring_shortcuts::cancel_preview();
                    selected.set(event_target_value(&ev));
                }>
                    <option value="">"Choisir une couche"</option>
                    {move || state.get().and_then(|s| s.zone_head).and_then(|h| h.base.vegetation).map(|v| v.layers.into_iter().map(|l| view! { <option value=l.id.to_string()>{l.display_name}</option> }).collect_view())}
                </select></label>
                <super::authoring_density_inspector::DensityInspector inspection=inspection/>
                <DensitySettings radius=radius strength=strength restore=restore/>
                <button disabled=move || inspection.get().is_none_or(|v| v.locked) on:click=move |_| arm(false)>"Peindre la densité en un point"</button>
                <button disabled=move || inspection.get().is_none_or(|v| v.locked) on:click=move |_| arm(true)>"Tracer une clairière dans la vue"</button>
                <button on:click=move |_| super::authoring_brush_pointer::cancel()>"Quitter la densité"</button>
                <button disabled=move || inspection.get().is_none_or(|v| !v.has_override || v.locked) on:click=move |_| edit_lock(1)>"Prévisualiser le verrou de densité"</button>
                <button disabled=move || inspection.get().is_none_or(|v| !v.locked) on:click=move |_| edit_lock(0)>"Prévisualiser le déverrouillage"</button>
                <button disabled=move || inspection.get().is_none_or(|v| !v.has_override || v.locked) on:click=move |_| edit_lock(2)>"Prévisualiser le retour complet à la recette"</button>
                <p>"Sans la case, le geste réduit la densité vers zéro avec un bord progressif. Avec la case, il remonte vers 255 : ce n’est pas une restauration du masque de recette. Le retour complet à la recette retire la correction de cette couche. Un geste = un Undo après Appliquer. Le verrou protège tout le masque sélectionné ; déverrouiller ne supprime rien."</p>
            </fieldset>
        </Show>
    }
}

#[component]
fn DensitySettings(
    radius: RwSignal<String>,
    strength: RwSignal<String>,
    restore: RwSignal<bool>,
) -> impl IntoView {
    let invalidate = super::authoring_brush_pointer::settings_changed;
    view! {
        <label>"Rayon de densité (m)"<input type="number" min="2" max="128"
            prop:value=move || radius.get() on:input=move |ev| {
                invalidate(); radius.set(event_target_value(&ev));
            }/></label>
        <label>"Intensité de densité (0–1)"<input type="number" min="0.01" max="1" step="0.01"
            prop:value=move || strength.get() on:input=move |ev| {
                invalidate(); strength.set(event_target_value(&ev));
            }/></label>
        <label><input type="checkbox" prop:checked=move || restore.get() on:change=move |ev| {
            invalidate(); restore.set(event_target_checked(&ev));
        }/>"Remonter le masque vers 100 % dans le geste"</label>
        <p>"1 atteint la cible au centre d’un point. Une intensité plus faible modifie progressivement le masque ; les passages superposés se cumulent. Changer ces réglages désarme le pinceau : réactiver le geste."</p>
    }
}
