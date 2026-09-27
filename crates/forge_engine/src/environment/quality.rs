use super::{EnvironmentProjectionStats, VisualValleyEnvironmentProfile};
use bevy::{
    light::{Atmosphere, AtmosphereEnvironmentMapLight},
    pbr::AtmosphereSettings,
    prelude::*,
};
use forge_project::{
    EnvironmentQualitySettings, TerrainDocument, VisualQualityTier,
    VisualValleyQualityProfileDocument, VisualValleyQualityProfileError,
};
use forge_world::quality_application::{QualityApplicationGuard, QualityApplicationIdentity};
use serde::Serialize;
pub(super) mod probe;
#[cfg(test)]
mod tests;

#[derive(Resource)]
pub struct EnvironmentQualityRequest {
    document: VisualValleyQualityProfileDocument,
    hash: String,
    tier: VisualQualityTier,
    effective: Option<(QualityApplicationIdentity, EnvironmentQualitySettings)>,
}

impl EnvironmentQualityRequest {
    /// Requested tier only; does not claim that all environment effects are applied.
    pub(crate) const fn requested_tier(&self) -> VisualQualityTier {
        self.tier
    }

    pub(crate) fn clear_effective(&mut self) {
        self.effective = None;
    }

    pub(crate) fn effective_identity(&self) -> Option<QualityApplicationIdentity> {
        self.effective.map(|(identity, _)| identity)
    }
    pub fn new(
        terrain: &TerrainDocument,
        document: VisualValleyQualityProfileDocument,
        tier: VisualQualityTier,
    ) -> Result<Self, VisualValleyQualityProfileError> {
        document.validate_identity(terrain.project_id, terrain.area_id, terrain.revision)?;
        let hash = document.content_hash()?.to_string();
        Ok(Self {
            document,
            hash,
            tier,
            effective: None,
        })
    }

    pub const fn request(&mut self, tier: VisualQualityTier) {
        self.tier = tier;
        self.effective = None;
    }

    /// Transient installation; callers own current source/capability snapshots and invalidation.
    /// Rejection clears an older override, never edits the authored document or acknowledges GPU.
    pub fn request_effective(
        &mut self,
        guard: &QualityApplicationGuard,
        now: std::time::Duration,
        current: QualityApplicationIdentity,
        sources: forge_project::effective_quality::QualitySourceBindings,
    ) -> bool {
        self.effective = None;
        let Some(plan) = guard
            .installation_plan(now, current, sources)
            .filter(|plan| {
                plan.requested_policy_hash.to_string() == self.hash
                    && plan.requested_tier == self.tier
            })
        else {
            return false;
        };
        self.effective = Some((current, plan.effective.environment));
        true
    }

    pub(super) fn settings(&self) -> EnvironmentQualitySettings {
        self.effective.map_or(
            self.document.preset(self.tier).environment,
            |(_, settings)| settings,
        )
    }

    pub(super) fn observe(
        &self,
        stats: &EnvironmentProjectionStats,
        authored_sky: bool,
        texture_bound: bool,
    ) -> EnvironmentQualityObservation {
        let settings = self.settings();
        let enabled = authored_sky && settings.atmosphere_enabled;
        let sky_budget_projected = stats.active_camera_3d_entities == 1
            && matches!(
                stats.sun.weather_response.status,
                "disabled" | "applied_cpu"
            )
            && stats.sky.atmosphere_settings == enabled
            && stats.sky.atmosphere_ibl == enabled
            && stats.atmosphere_entities == usize::from(enabled)
            && (!enabled
                || stats.sky.atmosphere_ibl_resolution
                    == Some([u32::from(settings.environment_map_resolution); 2]));
        EnvironmentQualityObservation {
            policy_hash: self.hash.clone(),
            requested_tier: self.tier,
            requested: settings,
            authored_requested: self.document.preset(self.tier).environment,
            effective_request: self.effective.map(|(identity, _)| identity),
            sky_budget_projected,
            generated_texture_bound: texture_bound,
            volumetric_status: if settings.volumetric_fog_enabled {
                "unavailable_distance_fog_fallback_retained"
            } else {
                "disabled_no_volumetric_pass"
            },
            full_preset_applied: false,
        }
    }
}

#[derive(Clone, Debug, PartialEq, Serialize)]
pub struct EnvironmentQualityObservation {
    pub policy_hash: String,
    pub requested_tier: VisualQualityTier,
    pub requested: EnvironmentQualitySettings,
    pub authored_requested: EnvironmentQualitySettings,
    /// Requested installation identity, not proof of projection or GPU readiness.
    pub effective_request: Option<QualityApplicationIdentity>,
    pub sky_budget_projected: bool,
    pub generated_texture_bound: bool,
    pub volumetric_status: &'static str,
    pub full_preset_applied: bool,
}

#[derive(Component)]
pub(super) struct PreparedAtmosphere(pub Atmosphere);

#[derive(Component)]
pub(super) struct PreparedSkyCamera(pub AtmosphereSettings, pub AtmosphereEnvironmentMapLight);

#[allow(
    clippy::needless_pass_by_value,
    clippy::type_complexity,
    reason = "Bevy system parameters and optional camera components"
)]
pub(super) fn apply(
    mut commands: Commands,
    request: Option<Res<EnvironmentQualityRequest>>,
    profile: Res<VisualValleyEnvironmentProfile>,
    cameras: Query<
        (
            Entity,
            &Camera,
            Option<&PreparedSkyCamera>,
            Option<&AtmosphereSettings>,
            Option<&AtmosphereEnvironmentMapLight>,
        ),
        With<Camera3d>,
    >,
    atmospheres: Query<(Entity, &PreparedAtmosphere, Option<&Atmosphere>)>,
    mut announced: Local<Option<VisualQualityTier>>,
    mut probe_state: ResMut<probe::SkyProbeState>,
) {
    let Some(request) = request else {
        return;
    };
    let mut active = cameras.iter().filter(|(_, camera, ..)| camera.is_active);
    let Some((camera, _, prepared, settings, map)) = active.next() else {
        return;
    };
    if active.next().is_some() {
        return;
    }
    let budget = request.settings();
    // A late opt-in cannot take ownership of the historical camera's private cache.
    if map.is_some() {
        if *announced != Some(request.tier) {
            warn!(target: "forge::environment", schema = "forge.runtime-event", schema_version = 1_u32,
                event_kind = "environment_quality_cache_blocked", tier = ?request.tier,
                "existing Bevy atmosphere cache cannot be resized through its public request component; projection retained");
            *announced = Some(request.tier);
        }
        return;
    }
    if profile.document.sky.enabled {
        let (Some(prepared), Ok((entity, atmosphere, actual))) = (prepared, atmospheres.single())
        else {
            return;
        };
        if budget.atmosphere_enabled {
            if actual.is_none() {
                commands.entity(entity).insert(atmosphere.0.clone());
            }
            if settings.is_none() {
                commands.entity(camera).insert(prepared.0.clone());
            }
            let size = UVec2::splat(u32::from(budget.environment_map_resolution));
            let mut next = prepared.1.clone();
            next.size = size;
            probe_state.request(&mut commands, camera, Some(next));
        } else {
            if actual.is_some() {
                commands.entity(entity).remove::<Atmosphere>();
            }
            probe_state.request(&mut commands, camera, None);
            if settings.is_some() {
                commands
                    .entity(camera)
                    .remove::<(AtmosphereSettings, AtmosphereEnvironmentMapLight)>();
            }
        }
    }
    if *announced != Some(request.tier) {
        warn!(target: "forge::environment", schema = "forge.runtime-event", schema_version = 1_u32,
            event_kind = "environment_quality_partial", tier = ?request.tier,
            policy_hash = %request.hash, "sky budget projected; volumetric qualification remains open, authored distance-fog fallback retained");
        *announced = Some(request.tier);
    }
}
