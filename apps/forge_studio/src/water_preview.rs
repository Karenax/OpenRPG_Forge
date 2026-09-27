//! Transient water presentation controls. The engine owns application and observed state.
use super::effective_quality_status::ReflectionPreviewMode;
use bevy::prelude::*;
use forge_engine::{WaterProjectionStats, WaterQualityObservation, WaterQualityRequest};
use leptos::prelude::*;
use std::cell::RefCell;

enum TintPreviewRequest {
    Set(bool),
    FollowProfile,
}

#[derive(Clone)]
struct RefractionView {
    description: String,
    disabled: bool,
    projected: bool,
}

thread_local! {
    static REQUEST: RefCell<Option<TintPreviewRequest>> = const { RefCell::new(None) };
    static REFLECTION_REQUEST: RefCell<Option<ReflectionPreviewMode>> = const { RefCell::new(None) };
    static REFRACTION_REQUEST: RefCell<Option<bool>> = const { RefCell::new(None) };
    static REFRACTION_OUTPUT: RefCell<Option<WriteSignal<Option<RefractionView>>>> = const { RefCell::new(None) };
    static OUTPUT: RefCell<Option<WriteSignal<Option<WaterQualityObservation>>>> = const { RefCell::new(None) };
    static OPTICAL_OUTPUT: RefCell<Option<WriteSignal<bool>>> = const { RefCell::new(None) };
    static FORWARD_OUTPUT: RefCell<Option<WriteSignal<Option<String>>>> = const { RefCell::new(None) };
    static LEDGER_OUTPUT: RefCell<Option<WriteSignal<String>>> = const { RefCell::new(None) };
}

#[component]
pub fn WaterTintControls() -> impl IntoView {
    let (observed, set_observed) = signal::<Option<WaterQualityObservation>>(None);
    OUTPUT.with(|slot| slot.replace(Some(set_observed)));
    let (optical, set_optical) = signal(false);
    let (forward, set_forward) = signal::<Option<String>>(None);
    FORWARD_OUTPUT.with(|slot| slot.replace(Some(set_forward)));
    let (refraction, set_refraction) = signal::<Option<RefractionView>>(None);
    REFRACTION_OUTPUT.with(|slot| slot.replace(Some(set_refraction)));
    let (ledger, set_ledger) = signal(String::new());
    OPTICAL_OUTPUT.with(|slot| slot.replace(Some(set_optical)));
    LEDGER_OUTPUT.with(|slot| slot.replace(Some(set_ledger)));
    view! {
        <div class="weather-preview" aria-label="Teinte de l’eau sans sauvegarde">
            <p class="hint">"Teinte de profondeur · aperçu sans sauvegarde"</p>
            {[ (Some(true), "Activer la teinte de l’eau"), (Some(false), "Désactiver la teinte de l’eau"), (None, "Suivre le profil d’eau") ]
                .into_iter().map(move |(value, label)| view! {
                    <button type="button" disabled=move || observed.get().is_none()
                        aria-pressed=move || observed.get().is_some_and(|state| state.surface_tint_preview == value && state.surface_tint_projected).to_string()
                        on:click=move |_| { REQUEST.with(|slot| slot.replace(Some(value.map_or(TintPreviewRequest::FollowProfile, TintPreviewRequest::Set)))); }
                    >{label}</button>
                }).collect_view()}
            <output id="studio-water-tint-status">{move || observed.get().map_or_else(
                || super::water_optics_request::unprofiled_status(optical.get()).to_owned(),
                |state| format!("{} · teinte {} · {} · observation CPU, réfraction écran non qualifiée",
                    if state.surface_tint_preview.is_some() { "Aperçu" } else { "Profil" },
                    state.surface_tint_strength.map_or_else(|| "indisponible".to_owned(), |value| format!("{value:.3}")),
                    if state.surface_tint_projected { "appliquée" } else { "application non confirmée" }))}</output>
            <p class="hint">"Reflets · aperçu sans sauvegarde"</p>
            {[(ReflectionPreviewMode::Disabled, "Désactiver les reflets d’eau"), (ReflectionPreviewMode::EnvironmentOnly, "Reflets d’environnement seuls"), (ReflectionPreviewMode::Profile, "Suivre les reflets du profil")]
                .into_iter().map(move |(mode, label)| view! {
                    <button type="button" disabled=move || observed.get().is_none() && forward.get().is_none()
                        aria-pressed=move || forward.get().is_none().then(|| observed.get().is_some_and(|state|
                            ReflectionPreviewMode::from_observed(state.reflections_disabled_preview, state.screen_reflections_disabled_preview) == mode && state.material_reflectance_projected && state.ssr_budget_projected).to_string())
                        on:click=move |_| { REFLECTION_REQUEST.with(|slot| slot.replace(Some(mode))); }
                    >{label}</button>
                }).collect_view()}
            <output id="studio-water-reflection-status">{move || forward.get().unwrap_or_else(|| observed.get().map_or_else(
                || super::water_optics_request::unprofiled_status(optical.get()).to_owned(),
                |state| format!("{} · réflectance {} · {} · observation CPU, rendu à vérifier",
                    ReflectionPreviewMode::from_observed(state.reflections_disabled_preview, state.screen_reflections_disabled_preview).label(),
                    state.material_reflectance.map_or_else(|| "indisponible".to_owned(), |value| format!("{value:.3}")),
                    if state.material_reflectance_projected && state.ssr_budget_projected { "appliquée" } else { "application non confirmée" })))}</output>
            <Show when=move || refraction.get().is_some()>
                <p class="hint">"Réfraction · aperçu sans sauvegarde"</p>
                {[(true, "Transmission approchée sans rayon"), (false, "Suivre la réfraction du profil")]
                    .into_iter().map(move |(disabled, label)| view! {
                        <button type="button"
                            aria-pressed=move || refraction.get().is_some_and(|state| state.disabled == disabled && state.projected).to_string()
                            on:click=move |_| { REFRACTION_REQUEST.with(|slot| slot.replace(Some(disabled))); }
                        >{label}</button>
                    }).collect_view()}
                <output id="studio-water-refraction-status">{move || refraction.get().map(|state| state.description).unwrap_or_default()}</output>
            </Show>
            <pre id="studio-water-optics-ledger" hidden>{move || ledger.get()}</pre>
        </div>
    }
}

pub fn install(app: &mut App) {
    app.add_systems(PreUpdate, apply)
        .add_systems(Last, (publish, publish_optics));
}

fn apply(mut request: ResMut<WaterQualityRequest>) {
    if let Some(disabled) = REFRACTION_REQUEST.with(|slot| slot.borrow_mut().take()) {
        request.preview_screen_refraction_disabled(disabled);
    }
    if let Some(value) = REQUEST.with(|slot| slot.borrow_mut().take()) {
        match value {
            TintPreviewRequest::Set(enabled) => request.preview_surface_tint(Some(enabled)),
            TintPreviewRequest::FollowProfile => request.preview_surface_tint(None),
        }
    }
    if let Some(mode) = REFLECTION_REQUEST.with(|slot| slot.borrow_mut().take()) {
        let (all_disabled, screen_disabled) = mode.flags();
        request.preview_reflections_disabled(all_disabled);
        request.preview_screen_reflections_disabled(screen_disabled);
    }
}

#[allow(
    clippy::needless_pass_by_value,
    reason = "Bevy system parameter ownership"
)]
fn publish(stats: Res<WaterProjectionStats>, mut previous: Local<Option<WaterQualityObservation>>) {
    if *previous != stats.quality {
        OUTPUT.with(|slot| {
            if let Some(output) = *slot.borrow() {
                output.set(stats.quality.clone());
            }
        });
        previous.clone_from(&stats.quality);
    }
}

#[allow(
    clippy::needless_pass_by_value,
    reason = "Bevy system parameter ownership"
)]
fn publish_optics(
    stats: Res<WaterProjectionStats>,
    runtime: Res<forge_engine::WaterRuntimeState>,
    time: Res<Time<Real>>,
    mut last: Local<Option<f64>>,
    mut previous_mode: Local<Option<bool>>,
) {
    let now = time.elapsed_secs_f64();
    if *previous_mode != Some(stats.optical_depth_preview) {
        OPTICAL_OUTPUT.with(|slot| {
            if let Some(output) = *slot.borrow() {
                output.set(stats.optical_depth_preview);
            }
        });
        *previous_mode = Some(stats.optical_depth_preview);
    }
    if last.is_some_and(|previous| now - previous < 1.0) {
        return;
    }
    *last = Some(now);
    FORWARD_OUTPUT.with(|slot| {
        if let Some(output) = *slot.borrow() {
            output.set(stats.forward_reflection_quality.as_ref().map(|state| {
                super::water_optics_request::forward_status(
                    state.linear_steps,
                    state.max_distance_m,
                    state.material_reflectance,
                    state.projection_matches,
                )
            }));
        }
    });
    REFRACTION_OUTPUT.with(|slot| {
        if let Some(output) = *slot.borrow() {
            output.set(
                stats
                    .forward_reflection_quality
                    .as_ref()
                    .map(|state| RefractionView {
                        description: super::water_optics_request::refraction_status(
                            state.refraction_linear_steps,
                            state.refraction_max_path_m,
                            state.projection_matches,
                        ),
                        disabled: state.screen_refraction_disabled_preview,
                        projected: state.projection_matches,
                    }),
            );
        }
    });
    let value = serde_json::json!({
        "schema": "forge.studio-water-optics-observation",
        "schema_version": 1,
        "sampled_elapsed_seconds": now,
        "evidence_source": "bevy_cpu_ecs_and_material_not_gpu_pixels",
        "current_phase_radians": runtime.current_phase_radians(),
        "projection": &*stats,
        "limits": "Authored optics resolution is intent; preview flag and runtime observation describe the candidate. No artistic or temporal GPU qualification.",
    });
    let text = serde_json::to_string(&value)
        .unwrap_or_else(|error| format!("serialization failed: {error}"));
    LEDGER_OUTPUT.with(|slot| {
        if let Some(output) = *slot.borrow() {
            output.set(text);
        }
    });
}
