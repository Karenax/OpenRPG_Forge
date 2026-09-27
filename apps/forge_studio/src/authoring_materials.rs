//! Presentation only; material edits use the shared terrain command and preview path.
use forge_project::{authoring::AuthoringState, terrain_edit::TerrainEdit};
use forge_world::terrain_material_brush::{MaterialBrushOperation, TerrainMaterialBrushStroke};
use leptos::prelude::*;

#[component]
#[allow(
    clippy::too_many_lines,
    reason = "Material controls share the same checked brush preparation"
)]
pub(super) fn MaterialControls(
    state: RwSignal<Option<AuthoringState>>,
    on_preview: Callback<Result<TerrainEdit, String>>,
) -> impl IntoView {
    let selected = RwSignal::new(String::new());
    let radius = RwSignal::new("12".to_owned());
    let strength = RwSignal::new("0.5".to_owned());
    let erase = RwSignal::new(false);
    let arm = move |continuous| {
        let preparation = (|| {
            let document = state
                .get_untracked()
                .and_then(|s| s.document)
                .ok_or("Ouvrir une zone")?;
            if document.schema_version != 3 {
                return Err(
                    "Prévisualiser puis appliquer la migration v3 avant de peindre".to_owned(),
                );
            }
            let radius_m = radius
                .get_untracked()
                .parse::<f32>()
                .map_err(|_| "Rayon invalide")?;
            let strength = strength
                .get_untracked()
                .parse::<f32>()
                .map_err(|_| "Intensité invalide")?;
            if !radius_m.is_finite()
                || !(2.0..=128.0).contains(&radius_m)
                || !strength.is_finite()
                || strength <= 0.0
                || strength > 1.0
            {
                return Err("Rayon 2–128 m et intensité (0, 1] requis".into());
            }
            let operation = if erase.get_untracked() {
                MaterialBrushOperation::Erase
            } else {
                let key = selected.get_untracked();
                let layer = document
                    .material_layers
                    .iter()
                    .take(3)
                    .find(|layer| layer.id.to_string() == key)
                    .ok_or("Choisir une couche de la palette")?;
                MaterialBrushOperation::Paint(layer.id.clone())
            };
            let hash = document.content_hash().map_err(|e| e.to_string())?;
            Ok((document, hash, radius_m, strength, operation))
        })();
        let (document, hash, radius_m, strength, operation) = match preparation {
            Ok(value) => value,
            Err(error) => {
                on_preview.run(Err(error));
                return;
            }
        };
        super::authoring_brush_pointer::arm(
            radius_m,
            continuous,
            Callback::new(move |points: Result<Vec<[f32; 2]>, String>| {
                let result = (|| {
                    let current = state
                        .get_untracked()
                        .and_then(|s| s.document)
                        .ok_or("Session fermée")?;
                    if current.content_hash().map_err(|e| e.to_string())? != hash {
                        return Err("Trait périmé : la zone a changé".into());
                    }
                    TerrainMaterialBrushStroke {
                        radius_m,
                        strength,
                        points: points?,
                        operation: operation.clone(),
                    }
                    .prepare(&document)
                })();
                on_preview.run(result);
            }),
        );
    };
    on_cleanup(super::authoring_brush_pointer::cancel);
    view! {
        <fieldset on:input=move |_| super::authoring_brush_pointer::settings_changed()
            on:change=move |_| super::authoring_brush_pointer::settings_changed()>
            <legend>"Peinture des matériaux · aperçu avant application"</legend>
            <Show when=move || state.get().and_then(|s| s.document).is_some_and(|d| d.schema_version != 3)>
            <button on:click=move |_| on_preview.run(Ok(TerrainEdit::SetSchemaVersion { version: 3 }))>"Prévisualiser la migration v3"</button>
            <p>"La migration est explicite et annulable. Utiliser Appliquer la retouche prévisualisée pour la confirmer."</p>
            </Show>
            <label>"Couche"<select prop:value=move || selected.get() on:change=move |ev| selected.set(event_target_value(&ev))>
                <option value="">"Choisir une couche"</option>
                {move || state.get().and_then(|s| s.document).map(|document| document.material_layers.into_iter().take(3).map(|layer| { let id = layer.id.to_string(); let label = id.clone(); view! { <option value=id>{label}</option> } }).collect_view())}
            </select></label>
            <div class="authoring-field-grid">
            <label>"Rayon (m)"<input type="number" min="2" max="128" prop:value=move || radius.get() on:input=move |ev| radius.set(event_target_value(&ev))/></label>
            <label>"Intensité (0–1)"<input type="number" min="0.01" max="1" step="0.1" prop:value=move || strength.get() on:input=move |ev| strength.set(event_target_value(&ev))/></label>
            </div>
            <label><input type="checkbox" prop:checked=move || erase.get() on:change=move |ev| erase.set(event_target_checked(&ev))/>"Effacer les corrections (règles procédurales)"</label>
            <button on:click=move |_| arm(false)>"Peindre un point dans la vue"</button>
            <button class="authoring-primary" on:click=move |_| arm(true)>"Tracer les matériaux dans la vue"</button>
            <button on:click=move |_| super::authoring_brush_pointer::cancel()>"Quitter la peinture"</button>
            <MaterialHelp/>
        </fieldset>
    }
}

#[component]
fn MaterialHelp() -> impl IntoView {
    view! {
        <details><summary>"Conseils de peinture"</summary>
            <p>"Relâcher pour prévisualiser, puis Appliquer. Un trait = une retouche annulable. L’effacement restaure entièrement la règle dans le rayon ; son intensité ne s’applique pas."</p>
            <p>"Changer un réglage abandonne l’aperçu et désarme la peinture. Réactiver le geste avec les nouvelles valeurs."</p>
            </details>
    }
}
