use crate::{
    EnvironmentProjectionStats, EnvironmentQualityRequest, PostProcessQualityRequest,
    ShadowQualityRequest, TerrainLodProjectionStats, TerrainQualityRequest,
    VegetationProjectionStats, VegetationQualityRequest, VisualValleyShadowStats,
    WaterProjectionStats, WaterQualityRequest, WeatherProjectionStats, WeatherRuntimeState,
};
use bevy::prelude::*;
use forge_project::{VisualQualityTier, WeatherQualityTier};
use serde::Serialize;

mod adaptive;
mod application;
mod effective;
mod gpu_submission;
pub use adaptive::QualityAdaptationStats;
pub use application::QualityApplicationEvidence;
pub use effective::EffectiveQualityEvidence;
pub use gpu_submission::GpuSubmissionEvidence;

#[derive(Resource)]
pub struct GlobalQualityRequest {
    tier: VisualQualityTier,
    sent: Option<VisualQualityTier>,
    adaptive: bool,
    restart_adaptation: bool,
    revision: u64,
}
impl GlobalQualityRequest {
    pub fn request(&mut self, tier: VisualQualityTier) {
        self.revision = self.revision.wrapping_add(1);
        self.tier = tier;
        // An explicit command reapplies the tier after individual adapter changes.
        // Dispatch consumes it once; equal tier labels are not equal live settings.
        self.sent = None;
        self.adaptive = false;
        self.restart_adaptation = false;
    }

    /// Explicit opt-in. A subsequent manual global request always disables adaptation.
    pub fn enable_adaptation(&mut self) {
        self.revision = self.revision.wrapping_add(1);
        self.adaptive = true;
        self.restart_adaptation = true;
    }

    pub(super) const fn snapshot(&self) -> (VisualQualityTier, bool, u64) {
        (self.tier, self.adaptive, self.revision)
    }
}

#[derive(Clone, Copy, Debug, Eq, PartialEq, Serialize)]
#[serde(rename_all = "snake_case")]
pub enum QualityDomainProjection {
    Pending,
    CpuProjected,
    Partial,
}

/// Effective-plan readiness is separate from full authored-preset and visual qualification.
#[derive(Clone, Debug, PartialEq, Resource, Serialize)]
pub struct GlobalQualityStats {
    pub requested_tier: VisualQualityTier,
    /// Terrain, vegetation, shadows, environment, water, rain, post-process, in that order.
    pub domains: [QualityDomainProjection; 7],
    pub requests_dispatched: bool,
    pub available_budgets_projected: bool,
    pub full_preset_applied: bool,
    /// Resolution and application status; never a complete authored-preset acknowledgment.
    pub effective: Option<EffectiveQualityEvidence>,
    pub application: Option<QualityApplicationEvidence>,
    pub gpu_submission: GpuSubmissionEvidence,
    pub adaptation: QualityAdaptationStats,
}

pub struct GlobalQualityPlugin(pub VisualQualityTier);
impl Plugin for GlobalQualityPlugin {
    fn build(&self, app: &mut App) {
        app.insert_resource(GlobalQualityRequest {
            tier: self.0,
            sent: None,
            adaptive: false,
            restart_adaptation: false,
            revision: 0,
        })
        .insert_resource(GlobalQualityStats {
            requested_tier: self.0,
            domains: [QualityDomainProjection::Pending; 7],
            requests_dispatched: false,
            available_budgets_projected: false,
            full_preset_applied: false,
            effective: None,
            application: None,
            gpu_submission: GpuSubmissionEvidence::default(),
            adaptation: QualityAdaptationStats::default(),
        })
        .init_resource::<effective::EffectiveQualityCache>()
        .init_resource::<application::ApplicationState>()
        .init_resource::<adaptive::AdaptiveState>()
        .add_systems(PreUpdate, dispatch)
        .add_systems(
            Last,
            (observe, adaptive::update)
                .chain()
                .before(crate::write_metrics_and_exit),
        );
        gpu_submission::install(app);
        crate::profile_warmup::install(app);
    }
}

fn rain_tier(tier: VisualQualityTier) -> WeatherQualityTier {
    match tier {
        VisualQualityTier::Low => WeatherQualityTier::Low,
        VisualQualityTier::Medium => WeatherQualityTier::Medium,
        VisualQualityTier::High => WeatherQualityTier::High,
        VisualQualityTier::Ultra => WeatherQualityTier::Ultra,
    }
}

fn dispatch(world: &mut World) {
    let request = world.resource::<GlobalQualityRequest>();
    let tier = request.tier;
    // No partial fan-out when an adapter is absent. The observers report pending explicitly.
    if !(world.contains_resource::<TerrainQualityRequest>()
        && world.contains_resource::<VegetationQualityRequest>()
        && world.contains_resource::<ShadowQualityRequest>()
        && world.contains_resource::<EnvironmentQualityRequest>()
        && world.contains_resource::<WaterQualityRequest>()
        && world.contains_resource::<WeatherRuntimeState>()
        && world.contains_resource::<PostProcessQualityRequest>())
    {
        // An earlier fan-out is no longer valid after an adapter disappears. Keep the
        // requested tier, then resend it once all adapters are available again.
        world.resource_mut::<GlobalQualityRequest>().sent = None;
        return;
    }
    if world.resource::<GlobalQualityRequest>().sent == Some(tier) {
        return;
    }
    world
        .resource_mut::<application::ApplicationState>()
        .reset_for_dispatch();
    world.resource_mut::<TerrainQualityRequest>().request(tier);
    world
        .resource_mut::<VegetationQualityRequest>()
        .request(tier);
    world.resource_mut::<ShadowQualityRequest>().request(tier);
    world
        .resource_mut::<EnvironmentQualityRequest>()
        .request(tier);
    world.resource_mut::<WaterQualityRequest>().request(tier);
    world
        .resource_mut::<WeatherRuntimeState>()
        .request_rain_quality(rain_tier(tier));
    world
        .resource_mut::<PostProcessQualityRequest>()
        .request(tier);
    world.resource_mut::<GlobalQualityRequest>().sent = Some(tier);
}

fn state(matches: bool, partial: bool) -> QualityDomainProjection {
    if !matches {
        QualityDomainProjection::Pending
    } else if partial {
        QualityDomainProjection::Partial
    } else {
        QualityDomainProjection::CpuProjected
    }
}

fn observed_domains(world: &World, tier: VisualQualityTier) -> [QualityDomainProjection; 7] {
    let terrain = world.get_resource::<TerrainLodProjectionStats>();
    let vegetation = world
        .get_resource::<VegetationProjectionStats>()
        .and_then(|s| s.quality.as_ref());
    let shadows = world
        .get_resource::<VisualValleyShadowStats>()
        .and_then(|s| s.quality.as_ref());
    let environment = world
        .get_resource::<EnvironmentProjectionStats>()
        .and_then(|s| s.quality.as_ref());
    let water = world
        .get_resource::<WaterProjectionStats>()
        .and_then(|s| s.quality.as_ref());
    let forward_water = world.get_resource::<WaterProjectionStats>();
    let weather = world.get_resource::<WeatherProjectionStats>();
    let post = weather.and_then(|s| s.post_processing.quality.as_ref());
    // Reject mixed policy revisions rather than interpreting equal tier labels as equal presets.
    let policy = terrain
        .map(|s| s.policy_hash.as_str())
        .filter(|hash| !hash.is_empty());
    let same = |hash: &str| policy == Some(hash);
    [
        state(
            terrain
                .is_some_and(|s| s.requested_tier == Some(tier) && s.projected_tier == Some(tier)),
            false,
        ),
        state(
            vegetation.is_some_and(|s| {
                same(&s.policy_hash)
                    && s.requested_tier == tier
                    && s.projected_tier == tier
                    && !s.status.starts_with("rejected")
            }),
            false,
        ),
        state(
            shadows.is_some_and(|s| {
                same(&s.source_hash) && s.requested_tier == tier && s.projected_tier == Some(tier)
            }),
            false,
        ),
        state(
            environment.is_some_and(|s| {
                same(&s.policy_hash)
                    && s.requested_tier == tier
                    && s.sky_budget_projected
                    && s.generated_texture_bound
            }),
            true,
        ),
        state(
            water.is_some_and(|s| {
                same(&s.policy_hash)
                    && s.requested_tier == tier
                    && s.ssr_budget_projected
                    && s.surface_tint_projected
                    && s.material_reflectance_projected
            }) || forward_water.is_some_and(|s| {
                s.shader_uniforms_match_profile
                    && s.forward_reflection_quality
                        .as_ref()
                        .is_some_and(|observation| observation.matches_policy(policy, tier))
            }),
            true,
        ),
        state(
            weather.is_some_and(|s| {
                s.rain.requested_quality == Some(rain_tier(tier))
                    && s.rain.quality == Some(rain_tier(tier))
            }),
            false,
        ),
        state(
            post.is_some_and(|s| {
                same(&s.policy_hash) && s.requested_tier == tier && s.projected_tier == Some(tier)
            }),
            false,
        ),
    ]
}

pub(super) fn observe(world: &mut World) {
    let request = world.resource::<GlobalQualityRequest>();
    let tier = request.tier;
    let dispatched = request.sent == Some(tier);
    let domains = observed_domains(world, tier);
    let mut effective = effective::observe(
        world,
        tier,
        if dispatched {
            domains
        } else {
            [QualityDomainProjection::Pending; 7]
        },
    );
    let application = application::update(world, tier, dispatched, domains, effective.as_ref());
    if let (Some(effective), Some(application)) = (&mut effective, &application)
        && application
            .identity
            .is_some_and(|identity| Some(identity.plan_hash) == effective.plan_hash)
    {
        effective.status = application.status;
        effective.gpu_ready = application.gpu_ready;
    }
    world.insert_resource(GlobalQualityStats {
        requested_tier: tier,
        requests_dispatched: dispatched,
        available_budgets_projected: dispatched
            && domains
                .iter()
                .all(|state| *state != QualityDomainProjection::Pending),
        domains,
        full_preset_applied: false,
        effective,
        application,
        gpu_submission: world
            .get_resource::<GpuSubmissionEvidence>()
            .cloned()
            .unwrap_or_default(),
        adaptation: world.resource::<GlobalQualityStats>().adaptation.clone(),
    });
}

#[cfg(test)]
mod tests {
    use super::*;

    fn coordinator_app(tier: VisualQualityTier) -> App {
        let terrain = forge_world::embedded_visual_valley_terrain_document().unwrap();
        let policy = forge_project::VisualValleyQualityProfileDocument::from_json(include_str!(
            "../../../examples/visual_valley/quality-profile.v1.json"
        ))
        .unwrap();
        let weather = forge_project::WeatherProfileDocument::from_json(include_str!(
            "../../../examples/visual_valley/weather.v1.json"
        ))
        .unwrap();
        let mut app = App::new();
        app.add_plugins((MinimalPlugins, AssetPlugin::default()));
        app.add_plugins((
            GlobalQualityPlugin(tier),
            crate::VisualValleyTerrainQualityPlugin::new(&terrain, policy.clone(), tier).unwrap(),
            crate::VisualValleyShadowQualityPlugin::new(&terrain, policy.clone(), tier).unwrap(),
            crate::VisualValleyWeatherPlugin::new(weather),
        ));
        app.insert_resource(
            EnvironmentQualityRequest::new(&terrain, policy.clone(), tier).unwrap(),
        );
        app.insert_resource(VegetationQualityRequest::new(&terrain, policy.clone(), tier).unwrap());
        app.insert_resource(WaterQualityRequest::new(&terrain, policy.clone(), tier).unwrap());
        app.insert_resource(PostProcessQualityRequest::new(&terrain, policy, tier).unwrap());
        app
    }

    #[test]
    fn manual_same_tier_reapplies_after_individual_override_without_continuous_fanout() {
        let tier = VisualQualityTier::Low;
        let mut app = coordinator_app(tier);
        let world = app.world_mut();
        dispatch(world);
        world
            .resource_mut::<PostProcessQualityRequest>()
            .request(VisualQualityTier::High);
        // Individual choices persist until another explicit global command.
        dispatch(world);
        assert!(
            world
                .resource::<PostProcessQualityRequest>()
                .settings()
                .bloom_enabled
        );
        world
            .resource_mut::<GlobalQualityRequest>()
            .enable_adaptation();
        world.resource_mut::<GlobalQualityRequest>().request(tier);
        dispatch(world);
        assert!(
            !world
                .resource::<PostProcessQualityRequest>()
                .settings()
                .bloom_enabled
        );
        assert!(!world.resource::<GlobalQualityRequest>().adaptive);
        assert!(!world.resource::<GlobalQualityRequest>().restart_adaptation);
        // The command is consumed once, not replayed on every frame.
        world
            .resource_mut::<PostProcessQualityRequest>()
            .request(VisualQualityTier::High);
        dispatch(world);
        assert!(
            world
                .resource::<PostProcessQualityRequest>()
                .settings()
                .bloom_enabled
        );
        observe(world);
        let stats = world.resource::<GlobalQualityStats>();
        assert!(!stats.available_budgets_projected);
        assert!(!stats.full_preset_applied);
    }

    #[test]
    fn adapter_loss_invalidates_dispatch_and_restoration_resends_the_same_tier() {
        let tier = VisualQualityTier::Low;
        let mut app = coordinator_app(tier);
        // Exercise the coordinator without running renderer systems or manufacturing GPU evidence.
        let world = app.world_mut();
        dispatch(world);
        assert_eq!(world.resource::<GlobalQualityRequest>().sent, Some(tier));
        let water = world.remove_resource::<WaterQualityRequest>().unwrap();
        dispatch(world);
        observe(world);
        let stats = world.resource::<GlobalQualityStats>();
        assert!(!stats.requests_dispatched);
        assert!(!stats.available_budgets_projected);
        assert!(!stats.full_preset_applied);
        assert_eq!(stats.requested_tier, tier);
        // A surviving adapter can have a different request while dispatch is suspended.
        world
            .resource_mut::<PostProcessQualityRequest>()
            .request(VisualQualityTier::High);
        dispatch(world);
        assert!(
            world
                .resource::<PostProcessQualityRequest>()
                .settings()
                .bloom_enabled
        );
        world.insert_resource(water);
        dispatch(world);
        assert_eq!(world.resource::<GlobalQualityRequest>().sent, Some(tier));
        assert!(
            !world
                .resource::<PostProcessQualityRequest>()
                .settings()
                .bloom_enabled
        );
        observe(world);
        assert!(world.resource::<GlobalQualityStats>().requests_dispatched);
        assert!(!world.resource::<GlobalQualityStats>().full_preset_applied);
    }
    #[test]
    fn absent_adapters_never_dispatch_or_acknowledge_a_global_tier() {
        let mut app = App::new();
        app.add_plugins(GlobalQualityPlugin(VisualQualityTier::High));
        for tier in [
            VisualQualityTier::High,
            VisualQualityTier::Low,
            VisualQualityTier::Ultra,
        ] {
            app.world_mut()
                .resource_mut::<GlobalQualityRequest>()
                .request(tier);
            app.update();
            let stats = app.world().resource::<GlobalQualityStats>();
            assert_eq!(stats.requested_tier, tier);
            assert!(!stats.requests_dispatched);
            assert!(!stats.available_budgets_projected);
            assert!(!stats.full_preset_applied);
            assert_eq!(stats.domains, [QualityDomainProjection::Pending; 7]);
        }
    }
}
#[cfg(test)]
pub(crate) mod test_plan;
