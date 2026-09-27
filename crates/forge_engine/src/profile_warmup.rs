//! Explicit preparation of the four quality variants for the current scene/weather.
//! Uses normal adapter requests, never author documents or a renderer fork.
use crate::{
    GlobalQualityRequest, GlobalQualityStats, GovernedAssetKitReadiness, WeatherRuntimeState,
};
use bevy::{
    prelude::*,
    render::{
        ExtractSchedule, MainWorld, RenderApp,
        render_resource::{CachedPipelineState, PipelineCache},
    },
    shader::ShaderCacheError,
};
use forge_project::{VisualQualityTier, WeatherState};
use serde::Serialize;

const TIMEOUT_SECONDS: f64 = 120.0;
const STABLE_OBSERVATIONS: u32 = 8;

#[derive(Clone, Copy, Debug, Default, PartialEq, Eq, Serialize)]
#[serde(rename_all = "snake_case")]
pub enum ProfileWarmupPhase {
    #[default]
    Idle,
    Warming,
    Ready,
    Failed,
    Cancelled,
}

#[derive(Clone, Debug, Default, Serialize)]
struct CacheSample {
    sequence: u64,
    total: usize,
    pending: usize,
    failed: usize,
}

#[derive(Clone, Debug, Serialize)]
pub struct PreparedQualityVariant {
    pub tier: VisualQualityTier,
    pub pipelines: usize,
    pub elapsed_seconds: f64,
}

#[derive(Clone, Debug, Default, Resource, Serialize)]
pub struct ProfileWarmup {
    pub phase: ProfileWarmupPhase,
    pub completed: Vec<PreparedQualityVariant>,
    pub error: Option<String>,
    pub elapsed_seconds: f64,
    #[serde(skip)]
    start_requested: bool,
    #[serde(skip)]
    original: Option<(VisualQualityTier, bool, WeatherState)>,
    #[serde(skip)]
    owned_revision: Option<u64>,
    #[serde(skip)]
    initial_revision: u64,
    #[serde(skip)]
    started: f64,
    #[serde(skip)]
    sample: CacheSample,
    #[serde(skip)]
    last_sample: CacheSample,
    #[serde(skip)]
    stable: u32,
}

impl ProfileWarmup {
    /// Explicit opt-in; repeated clicks while preparing do not restart the deadline.
    pub fn request(&mut self) {
        if self.phase != ProfileWarmupPhase::Warming {
            self.start_requested = true;
        }
    }
    #[must_use]
    pub fn is_warming(&self) -> bool {
        self.phase == ProfileWarmupPhase::Warming || self.start_requested
    }
    pub(crate) fn permits_capture(&self) -> bool {
        !self.start_requested
            && matches!(
                self.phase,
                ProfileWarmupPhase::Idle | ProfileWarmupPhase::Ready
            )
    }
    fn tier(&self) -> Option<VisualQualityTier> {
        match self.completed.len() {
            0 => Some(VisualQualityTier::Low),
            1 => Some(VisualQualityTier::Medium),
            2 => Some(VisualQualityTier::High),
            3 => Some(VisualQualityTier::Ultra),
            4 => self.original.map(|original| original.0),
            _ => None,
        }
    }
    fn cache_stable(&mut self, ready: bool) -> bool {
        if !ready || self.sample.total == 0 || self.sample.pending > 0 || self.sample.failed > 0 {
            self.stable = 0;
        } else if self.sample.sequence != self.last_sample.sequence {
            self.stable = if self.sample.total == self.last_sample.total {
                self.stable.saturating_add(1)
            } else {
                1
            };
        }
        self.last_sample = self.sample.clone();
        self.stable >= STABLE_OBSERVATIONS
    }
}

pub(super) fn install(app: &mut App) {
    app.init_resource::<ProfileWarmup>()
        .add_systems(Last, advance.after(crate::global_quality::observe))
        .add_systems(Update, publish);
    if let Some(render) = app.get_sub_app_mut(RenderApp) {
        render.add_systems(ExtractSchedule, observe_cache);
    }
}

#[allow(clippy::needless_pass_by_value, reason = "Bevy extraction parameters")]
fn observe_cache(mut main: ResMut<MainWorld>, cache: Res<PipelineCache>) {
    let Some(mut state) = main.get_resource_mut::<ProfileWarmup>() else {
        return;
    };
    if !state.is_warming() {
        return;
    }
    let mut sample = CacheSample {
        sequence: state.sample.sequence.wrapping_add(1),
        ..default()
    };
    for pipeline in cache.pipelines() {
        sample.total += 1;
        match &pipeline.state {
            CachedPipelineState::Ok(_) => {}
            CachedPipelineState::Queued
            | CachedPipelineState::Creating(_)
            | CachedPipelineState::Err(
                ShaderCacheError::ShaderNotLoaded(_)
                | ShaderCacheError::ShaderImportNotYetAvailable,
            ) => sample.pending += 1,
            CachedPipelineState::Err(_) => sample.failed += 1,
        }
    }
    state.sample = sample;
}

fn applied(stats: &GlobalQualityStats, tier: VisualQualityTier) -> bool {
    stats.requested_tier == tier
        && stats.requests_dispatched
        && stats.available_budgets_projected
        && stats.application.as_ref().is_some_and(|a| {
            a.status == "plan_application_ready"
                && a.gpu_ready
                && a.error.is_none()
                && a.projected_domains.iter().all(|ready| *ready)
                && a.identity.is_some_and(|identity| {
                    stats.effective.as_ref().is_some_and(|e| {
                        Some(identity.plan_hash) == e.plan_hash
                            && e.plan.as_ref().is_some_and(|p| {
                                identity.adapter_generation == p.adapter_generation
                            })
                    })
                })
        })
        && stats.effective.as_ref().is_some_and(|e| {
            e.status == "plan_application_ready"
                && e.gpu_ready
                && e.error.is_none()
                && e.plan.as_ref().is_some_and(|p| {
                    p.requested_tier == tier && p.blockers.is_empty() && p.source_bindings.is_some()
                })
        })
}

#[allow(clippy::needless_pass_by_value, reason = "Bevy system parameters")]
fn advance(
    mut state: ResMut<ProfileWarmup>,
    mut request: ResMut<GlobalQualityRequest>,
    quality: Res<GlobalQualityStats>,
    weather: Option<Res<WeatherRuntimeState>>,
    kit: Option<Res<GovernedAssetKitReadiness>>,
    time: Option<Res<Time<Real>>>,
) {
    if !state.is_warming() {
        return;
    }
    let Some(time) = time else {
        state.start_requested = false;
        state.phase = ProfileWarmupPhase::Failed;
        state.error = Some("Preparation requires a real-time clock".into());
        return;
    };
    if state.start_requested {
        let (tier, adaptive, _) = request.snapshot();
        let Some(weather) = weather.as_ref() else {
            state.start_requested = false;
            state.phase = ProfileWarmupPhase::Failed;
            state.error = Some("Preparation requires the weather adapter".into());
            return;
        };
        let sample = state.sample.clone();
        request.request(tier); // Pause adaptation while preparing; restore it on success/failure.
        *state = ProfileWarmup {
            phase: ProfileWarmupPhase::Warming,
            original: Some((tier, adaptive, weather.target_state())),
            started: time.elapsed_secs_f64(),
            initial_revision: request.snapshot().2,
            sample,
            ..default()
        };
    }
    if state.phase != ProfileWarmupPhase::Warming {
        return;
    }
    state.elapsed_seconds = time.elapsed_secs_f64() - state.started;
    let Some((original_tier, adaptive, original_weather)) = state.original else {
        return;
    };
    let newer_quality =
        state.owned_revision.unwrap_or(state.initial_revision) != request.snapshot().2;
    let changed_weather = weather
        .as_ref()
        .is_none_or(|w| w.target_state() != original_weather);
    if newer_quality || changed_weather {
        if changed_weather && !newer_quality {
            request.request(original_tier);
            if adaptive {
                request.enable_adaptation();
            }
        }
        state.phase = ProfileWarmupPhase::Cancelled;
        state.error = Some("Preparation cancelled by a newer quality/weather request".into());
        return; // Never overwrite a new user choice with the saved original tier.
    }
    if !state.elapsed_seconds.is_finite()
        || state.elapsed_seconds >= TIMEOUT_SECONDS
        || state.sample.failed > 0
    {
        request.request(original_tier);
        if adaptive {
            request.enable_adaptation();
        }
        state.phase = ProfileWarmupPhase::Failed;
        state.error =
            Some("Preparation failed or timed out; original profile requested again".into());
        return;
    }
    let Some(tier) = state.tier() else {
        return;
    };
    if state.owned_revision.is_none() {
        if !applied(&quality, original_tier) || kit.as_ref().is_some_and(|k| !k.ready) {
            return;
        }
        request.request(tier);
        state.owned_revision = Some(request.snapshot().2);
        state.last_sample = state.sample.clone();
        return;
    }
    if !state.cache_stable(applied(&quality, tier) && kit.as_ref().is_none_or(|k| k.ready)) {
        return;
    }
    let record = PreparedQualityVariant {
        tier,
        pipelines: state.sample.total,
        elapsed_seconds: state.elapsed_seconds,
    };
    info!(target: "forge::proof", event_kind = "quality_variant_prepared", tier = ?tier,
        pipelines = record.pipelines, elapsed_seconds = record.elapsed_seconds,
        "Known quality variant prepared; not a hitch-free guarantee");
    state.completed.push(record);
    if let Some(next) = state.tier() {
        request.request(next);
        state.owned_revision = Some(request.snapshot().2);
        state.stable = 0;
        state.last_sample = state.sample.clone();
    } else {
        if adaptive {
            request.enable_adaptation();
        }
        state.phase = ProfileWarmupPhase::Ready;
    }
}

#[derive(Component)]
struct PreparationText;
#[allow(clippy::needless_pass_by_value, reason = "Bevy system parameters")]
fn publish(
    mut commands: Commands,
    state: Res<ProfileWarmup>,
    mut text: Query<(&mut Text, &mut Visibility), With<PreparationText>>,
) {
    if !state.is_changed() {
        return;
    }
    let label = if state.is_warming() {
        format!(
            "Preparation des profils : {}/5\nLa scene courante continue de rendre.",
            state.completed.len()
        )
    } else {
        state.error.clone().unwrap_or_default()
    };
    if let Ok((mut text, mut visibility)) = text.single_mut() {
        *visibility = if label.is_empty() {
            Visibility::Hidden
        } else {
            Visibility::Inherited
        };
        text.0 = label;
    } else if !label.is_empty() {
        commands.spawn((
            PreparationText,
            Visibility::Inherited,
            Text::new(label),
            TextFont {
                font_size: bevy::text::FontSize::Px(20.0),
                ..default()
            },
            TextColor(Color::WHITE),
            BackgroundColor(Color::srgb(0.02, 0.03, 0.05)),
            GlobalZIndex(1000),
            Node {
                position_type: PositionType::Absolute,
                left: px(16),
                bottom: px(80),
                padding: UiRect::all(px(16)),
                ..default()
            },
        ));
    }
}

#[cfg(test)]
mod tests {
    use super::*;
    use bevy::ecs::system::RunSystemOnce;

    fn test_app() -> App {
        let mut app = App::new();
        app.add_plugins(crate::GlobalQualityPlugin(VisualQualityTier::High));
        let document = serde_json::from_str(include_str!(
            "../../../examples/visual_valley/weather.v1.json"
        ))
        .unwrap();
        app.insert_resource(WeatherRuntimeState::at_state(
            &document,
            WeatherState::Clear,
        ));
        app.init_resource::<Time<Real>>();
        app
    }

    #[test]
    fn newer_manual_request_wins_even_before_first_variant_is_issued() {
        let mut app = test_app();
        app.world_mut().resource_mut::<ProfileWarmup>().request();
        app.world_mut().run_system_once(advance).unwrap();
        app.world_mut()
            .resource_mut::<GlobalQualityRequest>()
            .request(VisualQualityTier::Low);
        app.world_mut().run_system_once(advance).unwrap();
        assert_eq!(
            app.world().resource::<ProfileWarmup>().phase,
            ProfileWarmupPhase::Cancelled
        );
        assert_eq!(
            app.world().resource::<GlobalQualityRequest>().snapshot().0,
            VisualQualityTier::Low
        );
    }

    #[test]
    fn timeout_restores_original_tier_and_adaptation_without_becoming_ready() {
        let mut app = test_app();
        app.world_mut()
            .resource_mut::<GlobalQualityRequest>()
            .enable_adaptation();
        app.world_mut().resource_mut::<ProfileWarmup>().request();
        app.world_mut().run_system_once(advance).unwrap();
        assert!(!app.world().resource::<GlobalQualityRequest>().snapshot().1);
        app.world_mut()
            .resource_mut::<Time<Real>>()
            .advance_by(std::time::Duration::from_secs(121));
        app.world_mut().run_system_once(advance).unwrap();
        let state = app.world().resource::<ProfileWarmup>();
        assert_eq!(state.phase, ProfileWarmupPhase::Failed);
        assert!(!state.permits_capture());
        let (tier, adaptive, _) = app.world().resource::<GlobalQualityRequest>().snapshot();
        assert_eq!(tier, VisualQualityTier::High);
        assert!(adaptive);
    }

    #[test]
    fn weather_cancellation_restores_owned_tier_but_preserves_new_weather() {
        let mut app = test_app();
        app.world_mut().resource_mut::<ProfileWarmup>().request();
        app.world_mut().run_system_once(advance).unwrap();
        app.world_mut()
            .resource_mut::<GlobalQualityRequest>()
            .request(VisualQualityTier::Low);
        let revision = app.world().resource::<GlobalQualityRequest>().snapshot().2;
        app.world_mut()
            .resource_mut::<ProfileWarmup>()
            .owned_revision = Some(revision);
        app.world_mut()
            .resource_mut::<WeatherRuntimeState>()
            .request_state(WeatherState::Rain);
        app.world_mut().run_system_once(advance).unwrap();
        assert_eq!(
            app.world().resource::<ProfileWarmup>().phase,
            ProfileWarmupPhase::Cancelled
        );
        assert_eq!(
            app.world().resource::<GlobalQualityRequest>().snapshot().0,
            VisualQualityTier::High
        );
        assert_eq!(
            app.world().resource::<WeatherRuntimeState>().target_state(),
            WeatherState::Rain
        );
    }
    #[test]
    fn cache_requires_fresh_stable_complete_observations() {
        let mut state = ProfileWarmup::default();
        for _ in 0..20 {
            assert!(!state.cache_stable(true));
        }
        for i in 1..=8 {
            state.sample = CacheSample {
                sequence: i,
                total: 10,
                ..default()
            };
            assert_eq!(state.cache_stable(true), i == 8);
        }
        state.sample.pending = 1;
        assert!(!state.cache_stable(true));
        state.sample.pending = 0;
        assert!(!state.cache_stable(true)); // Same observation cannot earn readiness.
    }
    #[test]
    fn ordered_variants_restore_original_profile() {
        let mut state = ProfileWarmup {
            original: Some((VisualQualityTier::Medium, false, WeatherState::Clear)),
            ..default()
        };
        for expected in [
            VisualQualityTier::Low,
            VisualQualityTier::Medium,
            VisualQualityTier::High,
            VisualQualityTier::Ultra,
            VisualQualityTier::Medium,
        ] {
            assert_eq!(state.tier(), Some(expected));
            state.completed.push(PreparedQualityVariant {
                tier: expected,
                pipelines: 1,
                elapsed_seconds: 1.0,
            });
        }
        assert_eq!(state.tier(), None);
    }
    #[test]
    fn incomplete_and_failed_preparation_never_permit_capture() {
        let mut state = ProfileWarmup::default();
        assert!(state.permits_capture());
        state.request();
        assert!(!state.permits_capture());
        state.start_requested = false;
        for phase in [
            ProfileWarmupPhase::Warming,
            ProfileWarmupPhase::Failed,
            ProfileWarmupPhase::Cancelled,
        ] {
            state.phase = phase;
            assert!(!state.permits_capture());
        }
        state.phase = ProfileWarmupPhase::Ready;
        assert!(state.permits_capture());
    }
}
