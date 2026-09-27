//! Read-only presentation of the currently accepted zone document.
use forge_project::{TerrainDocument, authoring::AuthoringState};
use leptos::prelude::*;

#[component]
pub(super) fn ZoneInformation(state: RwSignal<Option<AuthoringState>>) -> impl IntoView {
    view! {
        <details class="authoring-zone-information">
            <summary>"Informations de la zone"</summary>
            {move || content(state.get().and_then(|state| state.document))}
        </details>
    }
}

fn content(document: Option<TerrainDocument>) -> AnyView {
    let Some(document) = document else {
        return view! {
            <p>"Aucune zone ouverte : les informations acceptées apparaîtront ici après ouverture."</p>
        }
        .into_any();
    };
    view! {
        <dl>
            <dt>"Nom accepté"</dt>
            <dd>{document.name}</dd>
            <dt>"Dimensions"</dt>
            <dd>{format!("{:.1} × {:.1} m (X × Z)", document.extent_m.size_x_m, document.extent_m.size_z_m)}</dd>
            <dt>"Seed terrain"</dt>
            <dd>{document.seed}</dd>
            <dt>"Pas d’échantillonnage"</dt>
            <dd>{format!("{:.2} m", document.height_sampling.sample_spacing_m)}</dd>
        </dl>
        <details>
            <summary>"Identités techniques"</summary>
            <dl>
                <dt>"Projet"</dt>
                <dd>{document.project_id.to_string()}</dd>
                <dt>"Zone"</dt>
                <dd>{document.area_id.to_string()}</dd>
                <dt>"Révision"</dt>
                <dd>{document.revision.get()}</dd>
            </dl>
        </details>
    }
    .into_any()
}
