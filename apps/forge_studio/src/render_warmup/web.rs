use super::{Phase, Warmup};
use bevy::{
    ecs as bevy_ecs,
    prelude::*,
    render::{
        ExtractSchedule, MainWorld, RenderApp,
        render_resource::{CachedPipelineState, PipelineCache},
    },
    shader::ShaderCacheError,
};
use forge_engine::{
    EnvironmentProjectionStats, EnvironmentProjectionStatus, FixedProofViewStatus,
    GovernedAssetKitReadiness, GovernedAssetKitRuntimeStats,
};
use leptos::prelude::*;
use std::cell::RefCell;

#[derive(Resource, Default)]
struct Observation(Warmup);

thread_local! {
    static OUTPUT: RefCell<Option<WriteSignal<Warmup>>> = const { RefCell::new(None) };
}

#[component]
pub fn Loading() -> impl IntoView {
    let (state, output) = signal(Warmup::default());
    OUTPUT.with(|slot| slot.replace(Some(output)));
    view! {
        <div class="render-warmup" id="studio-render-warmup" role="status"
            hidden=move || state.get().phase == Phase::Prepared>
            <strong>{move || match state.get().phase {
                Phase::Preparing => "Préparation du rendu…",
                Phase::Prepared => "Pipelines préparés",
                Phase::Failed => "Échec de préparation des shaders",
                Phase::TimedOut => "Le rendu n’est pas prêt après 120 secondes",
            }}</strong>
            <span>{move || {
                let value = state.get();
                format!("{} pipelines · {} en attente · {} en erreur", value.total, value.pending, value.failed)
            }}</span>
            <span>"Le rendu continue en arrière-plan. Ce contrôle ne valide ni les pixels ni la qualité graphique."</span>
        </div>
        <pre id="studio-render-warmup-ledger" hidden>{move || serde_json::to_string(&state.get()).unwrap_or_default()}</pre>
    }
}

pub fn install(app: &mut App) {
    app.init_resource::<Observation>()
        .add_systems(bevy::prelude::Update, publish);
    if let Some(render) = app.get_sub_app_mut(RenderApp) {
        render.add_systems(ExtractSchedule, observe);
    }
}

#[allow(clippy::needless_pass_by_value, reason = "Bevy extraction parameters")]
fn observe(mut main: ResMut<MainWorld>, pipelines: Res<PipelineCache>) {
    if main.resource::<Observation>().0.phase != Phase::Preparing {
        return;
    }
    let elapsed = main.resource::<Time<Real>>().elapsed_secs_f64() * 1000.0;
    let scene_ready = main
        .get_resource::<EnvironmentProjectionStats>()
        .is_some_and(|s| s.status == EnvironmentProjectionStatus::ObservedApproximation)
        && main
            .get_resource::<GovernedAssetKitReadiness>()
            .is_none_or(|kit| {
                if main
                    .get_resource::<FixedProofViewStatus>()
                    .is_some_and(|view| {
                        view.installed && view.error.is_none() && view.current_view_matches
                    })
                    && main.resource::<Time<Virtual>>().is_paused()
                {
                    // The frozen view cannot register backend colliders until a physics tick.
                    // Still require complete scenes, all LODs and valid collider shapes.
                    main.get_resource::<GovernedAssetKitRuntimeStats>()
                        .is_some_and(|stats| kit.prepared_for_static_view(stats))
                } else {
                    kit.ready
                }
            });
    let (mut total, mut pending, mut failed) = (0, 0, 0);
    let mut transient = 0;
    for pipeline in pipelines.pipelines() {
        total += 1;
        match &pipeline.state {
            CachedPipelineState::Ok(_) => {}
            CachedPipelineState::Queued | CachedPipelineState::Creating(_) => pending += 1,
            // Match Bevy's retry policy exactly, not every Err and not its display string.
            CachedPipelineState::Err(
                ShaderCacheError::ShaderNotLoaded(_)
                | ShaderCacheError::ShaderImportNotYetAvailable,
            ) => {
                pending += 1;
                transient += 1;
            }
            CachedPipelineState::Err(_) => failed += 1,
        }
    }
    let mut observation = main.resource_mut::<Observation>();
    observation.0.transient_shader_errors_peak =
        observation.0.transient_shader_errors_peak.max(transient);
    observation
        .0
        .observe(total, pending, failed, scene_ready, elapsed);
}

#[allow(clippy::needless_pass_by_value, reason = "Bevy system parameters")]
fn publish(mut observation: ResMut<Observation>, time: Res<Time<Real>>) {
    // If the render sub-app never observes a cache, the main loop still exposes the timeout.
    if observation.0.phase == Phase::Preparing
        && time.elapsed_secs_f64() * 1000.0 >= super::TIMEOUT_MS
    {
        observation.0.phase = Phase::TimedOut;
        observation.0.elapsed_ms = time.elapsed_secs_f64() * 1000.0;
    }
    if observation.is_changed() {
        OUTPUT.with(|slot| {
            if let Some(output) = *slot.borrow() {
                output.set(observation.0.clone());
            }
        });
    }
}
