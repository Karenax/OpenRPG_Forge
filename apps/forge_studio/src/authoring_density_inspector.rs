//! Selected-layer read model; mutation remains in `DensityControls`.
use forge_project::{
    authoring::AuthoringState,
    zone_authoring::inspection::{DensityInspection, DensityMaskSummary},
};
use leptos::prelude::*;

pub(super) fn inspect(state: Option<&AuthoringState>, selected: &str) -> Option<DensityInspection> {
    state?
        .zone_head
        .as_ref()?
        .inspect_density(&forge_project::VegetationLayerId::new(selected))
        .ok()
}

#[component]
pub(super) fn DensityInspector(inspection: Memo<Option<DensityInspection>>) -> impl IntoView {
    view! {
        <section aria-label="Source et verrou de densité">
            <output aria-live="polite">{move || inspection.get().map_or_else(|| "Choisir une couche valide pour inspecter sa densité.".into(), |value| format!("{} · {} · {}", value.display_name, if value.has_override { "retouche manuelle" } else { "héritée de la recette" }, if value.locked { "verrouillée : déverrouiller avant peinture" } else { "modifiable" }))}</output>
            {move || inspection.get().map(|value| view! {
                <p>{format!("Recette : {}", mask(value.recipe_mask.as_ref()))}</p>
                <p>{format!("Effective : {}", mask(value.effective_mask.as_ref()))}</p>
                <p>"Les valeurs décrivent le masque spatial, pas le nombre d’arbres. Les autres règles de placement restent actives."</p>
                <details><summary>"Provenance et identités"</summary>
                    <pre style="white-space:pre-wrap;overflow-wrap:anywhere">{serde_json::to_string_pretty(&value).unwrap_or_default()}</pre>
                </details>
            })}
        </section>
    }
}

fn mask(value: Option<&DensityMaskSummary>) -> String {
    value.map_or_else(
        || "sans masque spatial (facteur 100 %)".into(),
        |value| {
            format!(
                "{} × {} · {} échantillons à zéro · plage {}/255 à {}/255",
                value.vertices_per_axis,
                value.vertices_per_axis,
                value.zero_samples,
                value.minimum,
                value.maximum
            )
        },
    )
}
