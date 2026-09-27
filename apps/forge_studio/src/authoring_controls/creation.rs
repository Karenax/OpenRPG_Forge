//! Reviewed initial terrain draft; Create remains the single native writer.
use forge_project::{
    TerrainDocument,
    authoring::{
        AuthoringRequest, AuthoringState,
        creation::{NewTerrainSettings, prepare_terrain},
    },
};
use leptos::prelude::*;

#[component]
pub(super) fn CreationControls(
    project: RwSignal<String>,
    zone: RwSignal<String>,
    name: RwSignal<String>,
    state: RwSignal<Option<AuthoringState>>,
    busy: RwSignal<bool>,
    send: Callback<Result<AuthoringRequest, String>>,
) -> impl IntoView {
    let terrain_seed = RwSignal::new("0".to_owned());
    let elevation = RwSignal::new("0".to_owned());
    let amplitude = RwSignal::new("0".to_owned());
    let wavelength = RwSignal::new("100".to_owned());
    let reviewed = RwSignal::new(None::<TerrainDocument>);
    let message = RwSignal::new(String::new());
    // Parent keys/name can change independently of this panel. Never retain a
    // review of a different identity even when the numeric recipe is unchanged.
    Effect::new(move |_| {
        let _ = (project.get(), zone.get(), name.get());
        reviewed.set(None);
        message.set(String::new());
    });
    let draft = move || {
        let number = |value: String| {
            value
                .trim()
                .parse::<f32>()
                .map_err(|_| "Valeur numérique invalide".to_owned())
        };
        prepare_terrain(
            &project.get_untracked(),
            &zone.get_untracked(),
            &name.get_untracked(),
            NewTerrainSettings {
                seed: terrain_seed
                    .get_untracked()
                    .trim()
                    .parse::<u64>()
                    .map_err(|_| "Seed entière positive ou nulle requise".to_owned())?,
                elevation_m: number(elevation.get_untracked())?,
                amplitude_m: number(amplitude.get_untracked())?,
                wavelength_m: number(wavelength.get_untracked())?,
            },
        )
    };
    view! {
        <details>
            <summary>"Nouvelle zone extérieure"</summary>
            <p>"Fermer la zone courante avant création. Terrain de 512 × 512 m, pas de 2 m. Amplitude 0 : terrain plat. Aucun arbre, chemin ou bâtiment ajouté automatiquement."</p>
            <fieldset disabled=move || busy.get() || state.get().is_some_and(|s| s.document.is_some())>
                <label>"Seed initiale"<input inputmode="numeric" prop:value=move || terrain_seed.get() on:input=move |ev| { terrain_seed.set(event_target_value(&ev)); reviewed.set(None); }/></label>
                <CreationNumber label="Altitude initiale (m)" value=elevation reviewed=reviewed/>
                <CreationNumber label="Amplitude du relief initial (m)" value=amplitude reviewed=reviewed/>
                <CreationNumber label="Échelle du relief initial (m)" value=wavelength reviewed=reviewed/>
                <button on:click=move |_| {
                    match draft() {
                        Ok(document) => {
                            message.set(format!("À créer : {} · projet {} · zone {} · seed {} · altitude {} m · amplitude {} m · échelle {} m. Résumé uniquement, pas un aperçu 3D. Aucune création effectuée.",
                                document.name, project.get_untracked(), zone.get_untracked(), document.seed,
                                document.height_sampling.base_elevation_m, document.height_sampling.amplitude_m, wavelength.get_untracked()));
                            reviewed.set(Some(document));
                        }
                        Err(error) => { reviewed.set(None); message.set(error); }
                    }
                }>"Examiner la nouvelle zone"</button>
                <output aria-live="polite">{move || message.get()}</output>
                <button disabled=move || reviewed.get().is_none() on:click=move |_| {
                    let result = draft().and_then(|document| {
                        if reviewed.get_untracked().as_ref() != Some(&document) {
                            return Err("Les paramètres ou les clés ont changé : examiner la zone à nouveau".into());
                        }
                        Ok(AuthoringRequest::CreateZone { document: Box::new(document) })
                    });
                    reviewed.set(None);
                    message.set("Demande transmise ; consulter le résultat dans la barre de session.".into());
                    send.run(result);
                }>"Créer la zone examinée"</button>
                <button on:click=move |_| { reviewed.set(None); message.set(String::new()); }>"Abandonner cette création"</button>
            </fieldset>
        </details>
    }
}

#[component]
fn CreationNumber(
    label: &'static str,
    value: RwSignal<String>,
    reviewed: RwSignal<Option<TerrainDocument>>,
) -> impl IntoView {
    view! { <label>{label}<input type="number" step="1" prop:value=move || value.get()
    on:input=move |ev| { value.set(event_target_value(&ev)); reviewed.set(None); }/></label> }
}
