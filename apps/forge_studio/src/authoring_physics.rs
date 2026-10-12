//! Scene contact controls; only reviewed commands can change the accepted head.
use forge_project::authoring::{AuthoringRequest, AuthoringState};
use leptos::prelude::*;

#[component]
pub(super) fn PhysicsControls(
    state: RwSignal<Option<AuthoringState>>,
    busy: RwSignal<bool>,
    on_command: Callback<Result<AuthoringRequest, String>>,
) -> impl IntoView {
    let matrix = RwSignal::new(None::<[u32; 32]>);
    let skin = RwSignal::new(String::new());
    let evidence = RwSignal::new(String::new());
    let selected_layer = RwSignal::new(0usize);
    let accepted = Memo::new(move |_| {
        state.with(|state| {
            state
                .as_ref()
                .and_then(|state| state.zone_head.as_ref())
                .map(|head| (head.base.terrain.area_id, head.corrections.physics.clone()))
        })
    });
    Effect::new(move |_| {
        let settings = accepted.get().and_then(|(_, settings)| settings);
        matrix.set(settings.as_ref().map(|settings| settings.layer_matrix));
        skin.set(
            settings
                .as_ref()
                .map(|settings| settings.contact_skin_m.to_string())
                .unwrap_or_default(),
        );
        evidence.set(
            settings
                .map(|settings| settings.evidence)
                .unwrap_or_default(),
        );
    });
    let disabled = move || {
        busy.get()
            || state
                .get()
                .is_none_or(|state| state.zone_head.is_none() || state.playtest_hash.is_some())
    };
    let prepare = move |remove: bool| {
        let result = (|| {
            let current = state.get_untracked().ok_or("Aucune zone")?;
            if current.playtest_hash.is_some() {
                return Err("Revenir en édition pour régler la physique".into());
            }
            let head = current.zone_head.ok_or("Une zone agrégée est requise")?;
            let settings = if remove {
                None
            } else {
                Some(super::authoring_physics_request::draft(
                    matrix.get_untracked(),
                    &skin.get_untracked(),
                    &evidence.get_untracked(),
                )?)
            };
            super::authoring_physics_request::preview(&head, settings)
        })();
        on_command.run(result);
    };
    view! {
        <details class="authoring-physics-settings">
            <summary>"Collisions de la scène"</summary>
            <p>"Choisir les interactions entre couches et l’épaisseur cible pour les objets importés avec collisions. Examiner, puis Appliquer en haut et Sauvegarder."</p>
            <output aria-live="polite">{move || accepted.get().and_then(|(_, settings)| settings).map_or_else(
                || "Aucun réglage enregistré : l’aperçu des objets avec collisions attend une configuration explicite.".into(),
                |settings| format!("Réglages enregistrés · épaisseur {} m · {}", settings.contact_skin_m, settings.evidence),
            )}</output>
            <fieldset disabled=disabled>
                <legend>"Interactions entre couches"</legend>
                <button on:click=move |_| { matrix.set(Some([u32::MAX; 32])); super::authoring_shortcuts::cancel_preview(); }>"Activer toutes les interactions"</button>
                <button on:click=move |_| { matrix.set(Some([0; 32])); super::authoring_shortcuts::cancel_preview(); }>"Désactiver toutes les interactions"</button>
                <label>"Couche à régler"<select prop:value=move || selected_layer.get().to_string() on:change=move |ev| {
                    if let Ok(layer) = event_target_value(&ev).parse::<usize>() {
                        if layer < 32 { selected_layer.set(layer); }
                    }
                }>{(0..32).map(|layer| view! { <option value=layer.to_string()>{format!("Couche {layer}")}</option> }).collect_view()}</select></label>
                <Show when=move || matrix.get().is_some() fallback=|| view! { <p>"Choisir une configuration ci-dessus pour commencer."</p> }>
                    <div style="display:grid;grid-template-columns:repeat(4,minmax(0,1fr));gap:0.25rem">
                    {(0..32).map(|other| view! {
                        <label><input type="checkbox" prop:checked=move || matrix.get().is_some_and(|m| (m[selected_layer.get()] & (1u32 << other)) != 0) on:change=move |ev| {
                            let enabled = event_target_checked(&ev);
                            let Some(mut value) = matrix.get_untracked() else {
                                on_command.run(Err("Configuration de couches absente ; reprendre le brouillon".into()));
                                return;
                            };
                            match super::authoring_physics_request::set_pair(&mut value, selected_layer.get_untracked(), other, enabled) {
                                Ok(()) => { matrix.set(Some(value)); super::authoring_shortcuts::cancel_preview(); }
                                Err(error) => on_command.run(Err(error)),
                            }
                        }/>{format!("Couche {other}")}</label>
                    }).collect_view()}
                    </div>
                    <p>"Chaque choix s’applique dans les deux sens entre les couches."</p>
                </Show>
            </fieldset>
            <label>"Épaisseur de contact cible (m)"<input type="number" min="0" step="0.001" disabled=disabled prop:value=move || skin.get() on:input=move |ev| { skin.set(event_target_value(&ev)); super::authoring_shortcuts::cancel_preview(); }/></label>
            <label>"Origine ou justification du réglage"<input type="text" maxlength="2048" disabled=disabled prop:value=move || evidence.get() on:input=move |ev| { evidence.set(event_target_value(&ev)); super::authoring_shortcuts::cancel_preview(); }/></label>
            <button disabled=disabled on:click=move |_| prepare(false)>"Examiner les réglages de collision"</button>
            <button disabled=move || disabled() || accepted.get().is_none_or(|(_, settings)| settings.is_none()) on:click=move |_| prepare(true)>"Examiner le retrait des réglages"</button>
        </details>
    }
}
