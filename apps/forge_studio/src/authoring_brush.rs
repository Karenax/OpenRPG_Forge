//! Metric controls and pointer gestures share the same brush contract.
use forge_world::terrain_brush::{BrushOperation, TerrainBrush};
use forge_world::terrain_brush_stroke::TerrainBrushStroke;
use leptos::prelude::*;

fn read(value: RwSignal<String>) -> Result<f32, String> {
    value
        .get_untracked()
        .parse::<f32>()
        .map_err(|_| "Paramètre de brosse invalide".to_owned())
}

fn read_radius(value: RwSignal<String>) -> Result<f32, String> {
    let radius = read(value)?;
    if !radius.is_finite() || !(2.0..=128.0).contains(&radius) {
        return Err("Rayon requis : 2–128 m".into());
    }
    Ok(radius)
}

fn read_seed(value: RwSignal<String>) -> Result<u64, String> {
    value
        .get_untracked()
        .parse::<u64>()
        .map_err(|_| "Seed entière invalide".to_owned())
}

#[component]
#[allow(
    clippy::too_many_lines,
    reason = "Brush signals and gesture callbacks share one mounted form"
)]
pub(super) fn BrushControls(
    on_preview: Callback<Result<TerrainBrushStroke, String>>,
) -> impl IntoView {
    let radius = RwSignal::new("12".to_owned());
    let strength = RwSignal::new("0.5".to_owned());
    let height = RwSignal::new("2".to_owned());
    let selected = RwSignal::new(0_u8);
    let seed = RwSignal::new("1337".to_owned());
    let wavelength = RwSignal::new("24".to_owned());
    let slope_x = RwSignal::new("10".to_owned());
    let slope_z = RwSignal::new("0".to_owned());
    let prepare = move |kind, points: Result<Vec<[f32; 2]>, String>| {
        let result = (|| {
            let points = points?;
            let center = points
                .first()
                .ok_or("Aucun point de terrain dans le geste")?;
            let operation = match kind {
                0 => BrushOperation::RaiseLower {
                    metres: read(height)?,
                },
                1 => BrushOperation::Flatten {
                    height_m: read(height)?,
                },
                2 => BrushOperation::Smooth,
                3 => BrushOperation::Noise {
                    amplitude_m: read(height)?,
                    wavelength_m: read(wavelength)?,
                    seed: read_seed(seed)?,
                },
                _ => BrushOperation::Slope {
                    height_m: read(height)?,
                    gradient_x: read(slope_x)? / 100.0,
                    gradient_z: read(slope_z)? / 100.0,
                },
            };
            let brush = TerrainBrush {
                x_m: center[0],
                z_m: center[1],
                radius_m: read_radius(radius)?,
                strength: read(strength)?,
                operation,
            };
            let mut stroke = TerrainBrushStroke::single(brush);
            stroke.points = points;
            Ok(stroke)
        })();
        on_preview.run(result);
    };
    on_cleanup(super::authoring_brush_pointer::cancel);
    let pick = move |kind, continuous| {
        let radius = match read_radius(radius) {
            Ok(radius) => radius,
            Err(error) => {
                on_preview.run(Err(error));
                return;
            }
        };
        super::authoring_brush_pointer::arm(
            radius,
            continuous,
            Callback::new(move |points: Result<Vec<[f32; 2]>, String>| {
                prepare(kind, points);
            }),
        );
    };
    view! {
        <fieldset class="authoring-relief" on:input=move |_| super::authoring_brush_pointer::settings_changed()
            on:change=move |_| super::authoring_brush_pointer::settings_changed()>
            <legend>"Brosse de relief · aperçu avant application"</legend>
            <div class="authoring-field-grid">
            <label>"Outil"<select prop:value=move || selected.get().to_string() on:change=move |ev| selected.set(match event_target_value(&ev).as_str() { "1" => 1, "2" => 2, "3" => 3, "4" => 4, _ => 0 })>
                <option value="0">"Lever / abaisser"</option><option value="1">"Aplatir"</option>
                <option value="2">"Lisser"</option><option value="3">"Bruit"</option><option value="4">"Pente"</option>
            </select></label>
            <label>"Rayon (m)"<input type="number" min="2" max="128" prop:value=move || radius.get() on:input=move |ev| radius.set(event_target_value(&ev))/></label>
            <label>"Intensité (0–1)"<input type="number" min="0.01" max="1" step="0.1" prop:value=move || strength.get() on:input=move |ev| strength.set(event_target_value(&ev))/></label>
            <label>"Variation / cible (m)"<input type="number" prop:value=move || height.get() on:input=move |ev| height.set(event_target_value(&ev))/></label>
            <Show when=move || selected.get() == 3>
                <label>"Seed"<input prop:value=move || seed.get() on:input=move |ev| seed.set(event_target_value(&ev))/></label>
                <label>"Longueur d’onde (m)"<input type="number" prop:value=move || wavelength.get() on:input=move |ev| wavelength.set(event_target_value(&ev))/></label>
            </Show>
            <Show when=move || selected.get() == 4>
                <label>"Pente X (%)"<input type="number" prop:value=move || slope_x.get() on:input=move |ev| slope_x.set(event_target_value(&ev))/></label>
                <label>"Pente Z (%)"<input type="number" prop:value=move || slope_z.get() on:input=move |ev| slope_z.set(event_target_value(&ev))/></label>
            </Show>
            </div>
            <div class="authoring-brush-actions">
            <button class="authoring-primary" on:click=move |_| pick(selected.get_untracked(), true)>"Tracer dans la vue"</button>
            <button on:click=move |_| pick(selected.get_untracked(), false)>"Placer dans la vue"</button>
            <button on:click=move |_| super::authoring_brush_pointer::cancel()>"Quitter la brosse"</button>
            </div>
            <p class="authoring-hint">"Dessiner avec le bouton gauche, relâcher, puis Appliquer. Échap pour abandonner."</p>
            <BrushHelp/>
        </fieldset>
    }
}

#[component]
fn BrushHelp() -> impl IntoView {
    view! {
        <details><summary>"Conseils de sculpture"</summary>
            <p>"Tracé : maintenir le bouton gauche puis relâcher pour prévisualiser. Échap ou sortie de la vue : abandon. Un geste = un Undo après Appliquer."</p>
            <p>"Changer un réglage abandonne l’aperçu et désarme la brosse. Réactiver le geste avec les nouvelles valeurs."</p>
            <p>"Valeur négative pour abaisser ; amplitude pour le bruit ; altitude absolue au centre pour aplatir/pente. Appliquer, annuler et sauvegarder avec les commandes de zone."</p>
            </details>
    }
}
