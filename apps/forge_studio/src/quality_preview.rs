use super::gpu_memory_preview::MemoryStatus;
use super::water_preview::WaterTintControls;
use bevy::prelude::{App, DetectChanges, Last, Local, PreUpdate, Res, ResMut};
use forge_engine::{
    EnvironmentQualityRequest, GlobalQualityPlugin, GlobalQualityRequest, GlobalQualityStats,
    PostProcessQualityRequest, VegetationQualityRequest, VisualValleyShadowQualityPlugin,
    VisualValleyTerrainQualityPlugin, WaterQualityRequest,
};
use forge_project::{
    TerrainDocument, VegetationDocument, VisualQualityTier, VisualValleyQualityProfileDocument,
};
use leptos::prelude::*;
use std::cell::RefCell;

enum QualityPreviewRequest {
    Manual(VisualQualityTier),
    Adaptive,
    Prewarm,
}

thread_local! {
    static METRICS_REQUEST: RefCell<Option<bool>> = const { RefCell::new(None) };
    static METRICS_OUTPUT: RefCell<Option<WriteSignal<bool>>> = const { RefCell::new(None) };
    static FOG_REQUEST: RefCell<Option<bool>> = const { RefCell::new(None) };
    static FOG_OUTPUT: RefCell<Option<WriteSignal<String>>> = const { RefCell::new(None) };
    static REQUEST: RefCell<Option<QualityPreviewRequest>> = const { RefCell::new(None) };
    static PREWARM_OUTPUT: RefCell<Option<WriteSignal<Option<forge_engine::ProfileWarmup>>>> = const { RefCell::new(None) };
    static OUTPUT: RefCell<Option<WriteSignal<Option<GlobalQualityStats>>>> = const { RefCell::new(None) };
    static VEGETATION_OUTPUT: RefCell<Option<WriteSignal<Option<forge_engine::VegetationProjectionStats>>>> = const { RefCell::new(None) };
}

pub(super) fn request_manual(tier: VisualQualityTier) {
    REQUEST.with(|slot| slot.replace(Some(QualityPreviewRequest::Manual(tier))));
}

#[component]
pub fn QualityControls() -> impl IntoView {
    let (prewarm, set_prewarm) = signal::<Option<forge_engine::ProfileWarmup>>(None);
    PREWARM_OUTPUT.with(|slot| slot.replace(Some(set_prewarm)));
    let (metrics_visible, set_metrics_visible) = signal(false);
    METRICS_OUTPUT.with(|slot| slot.replace(Some(set_metrics_visible)));
    let (observed, set_observed) = signal::<Option<GlobalQualityStats>>(None);
    OUTPUT.with(|slot| slot.replace(Some(set_observed)));
    let (vegetation, set_vegetation) =
        signal::<Option<forge_engine::VegetationProjectionStats>>(None);
    VEGETATION_OUTPUT.with(|slot| slot.replace(Some(set_vegetation)));
    let (fog_enabled, set_fog_enabled) = signal(false);
    let (fog_ledger, set_fog_ledger) = signal(String::new());
    FOG_OUTPUT.with(|slot| slot.replace(Some(set_fog_ledger)));
    view! {
        <div class="weather-preview" aria-label="Qualité graphique sans sauvegarde">
            <p class="hint">"Qualité graphique · aperçu sans sauvegarde"</p>
            <button id="studio-prewarm-quality" type="button"
                disabled=move || observed.get().is_none() || prewarm.get().is_some_and(|p| p.is_warming())
                on:click=move |_| { REQUEST.with(|slot| slot.replace(Some(QualityPreviewRequest::Prewarm))); }
            >"Préparer les quatre profils"</button>
            <output id="studio-prewarm-status">{move || prewarm.get().map_or_else(String::new, |p|
                format!("{:?} · {}/5 variantes, retour au profil initial compris · {}",
                    p.phase, p.completed.len(), p.error.unwrap_or_default()))}</output>
            <pre id="studio-profile-warmup-ledger" hidden>{move || prewarm.get().map_or_else(String::new,
                |p| serde_json::to_string(&p).unwrap_or_default())}</pre>
            <p class="hint">"Préparation de la scène et météo courantes, sans sauvegarde. Un nouveau choix manuel annule la préparation. Ne garantit pas l’absence de saccade."</p>
            <p id="studio-shadow-candidate-status" class="hint"></p>
            <button id="studio-toggle-metrics" type="button" disabled=move || observed.get().is_none()
                aria-pressed=move || metrics_visible.get().to_string()
                on:click=move |_| { METRICS_REQUEST.with(|slot| slot.replace(Some(!metrics_visible.get_untracked()))); }
            >{move || if metrics_visible.get() { "Masquer les métriques dans la vue" } else { "Afficher les métriques dans la vue" }}</button>
            <p class="hint">"La collecte des diagnostics reste active."</p>
            <MemoryStatus/>
            <details>
                <summary>"Végétation · observation CPU"</summary>
                <output id="studio-vegetation-status">{move || vegetation.get().and_then(|stats| stats.live_meshes).map_or_else(
                    || "Meshes non observés pour une caméra unique".to_owned(),
                    |stats| format!("{} entités mesh · {} meshes uniques · {} triangles visibles CPU · {} meshes non comptables · pas des draw calls GPU",
                        stats.mesh_entities, stats.unique_mesh_assets, stats.cpu_visible_triangles, stats.uncountable_mesh_assets))}</output>
                <p class="hint">"Relevé au plus 4 fois/s, distinct des budgets procéduraux. LOD2 individuel ne signifie pas HLOD de chunk qualifié."</p>
            </details>
            <pre id="studio-vegetation-ledger" hidden>{move || vegetation.get().map_or_else(String::new,
                |stats| serde_json::to_string(&stats).unwrap_or_else(|error| format!("serialization failed: {error}")))}</pre>
            {[VisualQualityTier::Low, VisualQualityTier::Medium, VisualQualityTier::High, VisualQualityTier::Ultra]
                .into_iter().map(move |tier| view! {
                    <button type="button" disabled=move || observed.get().is_none()
                        on:click=move |_| request_manual(tier)
                    >{format!("{tier:?}")}</button>
                }).collect_view()}
            <output id="studio-quality-status">{move || observed.get().map_or_else(
                || "Rendu non observé".to_owned(),
                |stats| super::effective_quality_status::projection_summary(
                    stats.requested_tier, stats.available_budgets_projected,
                    stats.effective.as_ref().and_then(|evidence| evidence.plan.as_ref())))}</output>
            <pre id="studio-quality-ledger" hidden>{move || observed.get().map_or_else(String::new,
                |stats| serde_json::to_string(&stats).unwrap_or_else(|error| format!("serialization failed: {error}")))}</pre>
            <details>
                <summary>"Profil effectif et limites"</summary>
                <button id="studio-enable-adaptation" type="button"
                    disabled=move || observed.get().is_none()
                    on:click=move |_| { REQUEST.with(|slot| slot.replace(Some(QualityPreviewRequest::Adaptive))); }
                >"Tester ou relancer l’adaptation automatique"</button>
                <button id="studio-disable-adaptation" type="button"
                    disabled=move || observed.get().is_none()
                    on:click=move |_| {
                        if let Some(stats) = observed.get_untracked() {
                            REQUEST.with(|slot| slot.replace(Some(QualityPreviewRequest::Manual(stats.requested_tier))));
                        }
                    }
                >"Revenir au mode fixe sur le profil demandé"</button>
                <output id="studio-adaptation-status">{move || observed.get().map_or_else(
                    || "Adaptation non observée".to_owned(), |stats| {
                        let state = stats.adaptation;
                        super::effective_quality_status::adaptation_summary(state.status, state.acknowledged.is_some(), state.proposals, state.error.as_deref())
                    })}</output>
                <p class="hint">"Adaptation expérimentale Web non qualifiée, désactivée par défaut. Low/Medium/High/Ultra arrêtent l’automatisme. Aucun réglage n’est sauvegardé ; les transitions peuvent rester perceptibles."</p>
                <output id="studio-effective-quality-status">{move || observed.get()
                    .and_then(|stats| stats.effective)
                    .map_or_else(|| "Plan non disponible".to_owned(), |evidence|
                        evidence.plan.as_ref().map_or_else(
                            || format!("Plan indisponible : {}", evidence.error.as_deref().unwrap_or(evidence.status)),
                            super::effective_quality_status::summary))}</output>
                <p class="hint">{move || observed.get().and_then(|stats| stats.application)
                    .map_or_else(|| "Plan candidat uniquement · application complète et rendu GPU non confirmés".to_owned(), |application|
                        super::effective_quality_status::application_summary(application.status,
                            application.projected_domains.iter().filter(|projected| **projected).count(), application.error.as_deref(), application.gpu_ready))}</p>
            </details>
            <button type="button" disabled=move || observed.get().is_none()
                aria-pressed=move || fog_enabled.get().to_string()
                on:click=move |_| {
                    let enabled = !fog_enabled.get();
                    set_fog_enabled.set(enabled);
                    FOG_REQUEST.with(|slot| slot.replace(Some(enabled)));
                }>{move || if fog_enabled.get() { "Désactiver la brume expérimentale" } else { "Tester la brume expérimentale" }}</button>
            <p class="hint">"Brume non qualifiée · désactivée en Low · sans sauvegarde"</p>
            <pre id="studio-volumetric-ledger" hidden>{move || fog_ledger.get()}</pre>
            <WaterTintControls/>
        </div>
    }
}

pub fn install(
    app: &mut App,
    terrain: &TerrainDocument,
    vegetation: &VegetationDocument,
) -> Result<(), String> {
    let mut policy = VisualValleyQualityProfileDocument::from_json(include_str!(
        "../../../examples/visual_valley/quality-profile.v1.json"
    ))
    .map_err(|error| error.to_string())?;
    policy.revision = terrain.revision;
    let window = web_sys::window().ok_or("Fenêtre du Studio absente")?;
    let search = window.location().search().map_err(|_| "URL inaccessible")?;
    let scene = crate::parse_studio_scene_selection(&search).scene;
    let balanced = super::shadow_candidate::requested(&search, scene)?;
    let policy = super::shadow_candidate::policy(&policy, balanced)?;
    if let Some(status) = window
        .document()
        .and_then(|doc| doc.get_element_by_id("studio-shadow-candidate-status"))
    {
        status.set_text_content(Some(if balanced {
            super::shadow_candidate::LABEL
        } else {
            "Ombres : politique de référence"
        }));
    }
    let tier = VisualQualityTier::High;
    // Validate every request before installing anything; identity is bound to the loaded revision.
    let shadows = VisualValleyShadowQualityPlugin::new(terrain, policy.clone(), tier)
        .map_err(|error| error.to_string())?;
    let terrain_quality = VisualValleyTerrainQualityPlugin::new(terrain, policy.clone(), tier)
        .map_err(|error| error.to_string())?;
    let environment = EnvironmentQualityRequest::new(terrain, policy.clone(), tier)
        .map_err(|error| error.to_string())?;
    let water = WaterQualityRequest::new(terrain, policy.clone(), tier)
        .map_err(|error| error.to_string())?;
    let post = PostProcessQualityRequest::new(terrain, policy.clone(), tier)
        .map_err(|error| error.to_string())?;
    let plants =
        VegetationQualityRequest::new(terrain, policy, tier).map_err(|error| error.to_string())?;
    plants
        .validate_vegetation(vegetation)
        .map_err(|error| error.to_string())?;
    super::water_preview::install(app);
    super::gpu_memory_preview::install(app);
    app.insert_resource(environment)
        .insert_resource(forge_engine::DiagnosticsOverlaySettings { visible: false })
        .insert_resource(water)
        .insert_resource(post)
        .insert_resource(plants)
        .add_plugins((shadows, terrain_quality, GlobalQualityPlugin(tier)))
        .add_systems(PreUpdate, apply_request)
        .add_systems(Last, (publish, publish_vegetation));
    Ok(())
}

fn apply_request(
    mut request: ResMut<GlobalQualityRequest>,
    mut prewarm: ResMut<forge_engine::ProfileWarmup>,
    mut fog: ResMut<forge_engine::VolumetricPreviewRequest>,
    mut metrics: ResMut<forge_engine::DiagnosticsOverlaySettings>,
) {
    if let Some(visible) = METRICS_REQUEST.with(|slot| slot.borrow_mut().take()) {
        metrics.visible = visible;
    }
    if let Some(enabled) = FOG_REQUEST.with(|slot| slot.borrow_mut().take()) {
        fog.enabled = enabled;
    }
    if let Some(action) = REQUEST.with(|slot| slot.borrow_mut().take()) {
        match action {
            QualityPreviewRequest::Manual(tier) => request.request(tier),
            QualityPreviewRequest::Adaptive => request.enable_adaptation(),
            QualityPreviewRequest::Prewarm => prewarm.request(),
        }
    }
}

#[allow(
    clippy::needless_pass_by_value,
    reason = "Bevy system parameter ownership"
)]
fn publish_vegetation(
    stats: Res<forge_engine::VegetationProjectionStats>,
    time: Res<bevy::prelude::Time<bevy::prelude::Real>>,
    mut last: Local<f64>,
) {
    let now = time.elapsed_secs_f64();
    if now - *last < 0.25 {
        return;
    }
    *last = now;
    VEGETATION_OUTPUT.with(|slot| {
        if let Some(output) = *slot.borrow() {
            output.set(Some(stats.clone()));
        }
    });
}

#[allow(
    clippy::needless_pass_by_value,
    reason = "Bevy system parameter ownership"
)]
fn publish(
    stats: Res<GlobalQualityStats>,
    prewarm: Res<forge_engine::ProfileWarmup>,
    mut last: Local<Option<GlobalQualityStats>>,
    environment: Option<Res<forge_engine::EnvironmentProjectionStats>>,
    mut last_fog: Local<String>,
    metrics: Res<forge_engine::DiagnosticsOverlaySettings>,
    mut last_metrics: Local<Option<bool>>,
) {
    if prewarm.is_changed() {
        PREWARM_OUTPUT.with(|slot| {
            if let Some(output) = *slot.borrow() {
                output.set(Some(prewarm.clone()));
            }
        });
    }
    if *last_metrics != Some(metrics.visible) {
        METRICS_OUTPUT.with(|slot| {
            if let Some(output) = *slot.borrow() {
                output.set(metrics.visible);
            }
        });
        *last_metrics = Some(metrics.visible);
    }
    if let Some(environment) = environment {
        let value = serde_json::to_string(&environment.volumetric)
            .unwrap_or_else(|error| format!("serialization failed: {error}"));
        if *last_fog != value {
            FOG_OUTPUT.with(|slot| {
                if let Some(output) = *slot.borrow() {
                    output.set(value.clone());
                }
            });
            *last_fog = value;
        }
    }
    if last.as_ref() == Some(stats.as_ref()) {
        return;
    }
    OUTPUT.with(|slot| {
        if let Some(output) = *slot.borrow() {
            output.set(Some(stats.clone()));
        }
    });
    *last = Some(stats.clone());
}
