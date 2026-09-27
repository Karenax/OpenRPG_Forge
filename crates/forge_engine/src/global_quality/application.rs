use super::{EffectiveQualityEvidence, QualityDomainProjection, effective};
use crate::{
    EnvironmentProjectionStats, EnvironmentQualityRequest, WaterProjectionStats,
    WaterQualityRequest,
};
use bevy::prelude::*;
use forge_project::VisualQualityTier;
use forge_world::quality_application::{
    QualityApplicationFailure, QualityApplicationGuard, QualityApplicationIdentity,
    QualityApplicationObservation, QualityApplicationState,
};
use serde::Serialize;
use std::time::Duration;

const APPLICATION_TIMEOUT: Duration = Duration::from_secs(30);

#[derive(Clone, Debug, PartialEq, Serialize)]
pub struct QualityApplicationEvidence {
    pub status: &'static str,
    pub identity: Option<QualityApplicationIdentity>,
    /// Current CPU observations, never accumulated historical acknowledgments.
    pub projected_domains: [bool; 7],
    pub gpu_ready: bool,
    pub error: Option<String>,
}

#[derive(Resource, Default)]
pub(super) struct ApplicationState {
    sequence: u64,
    guard: Option<QualityApplicationGuard>,
    evidence: Option<QualityApplicationEvidence>,
    renderer_epoch: Option<u64>,
    recovery: Option<QualityApplicationIdentity>,
    observation: Option<QualityApplicationObservation>,
}

impl ApplicationState {
    /// A new fan-out clears installed optical overrides even for an equal plan hash.
    /// Reinstall under a fresh sequence; never reuse a previous GPU acknowledgment.
    pub(super) fn reset_for_dispatch(&mut self) {
        *self = Self {
            sequence: self.sequence,
            ..Self::default()
        };
    }

    pub(super) fn snapshot(
        &self,
    ) -> Option<(&QualityApplicationGuard, &QualityApplicationObservation)> {
        self.guard.as_ref().zip(self.observation.as_ref())
    }
}

fn clear_overrides(world: &mut World) {
    if let Some(mut request) = world.get_resource_mut::<WaterQualityRequest>() {
        request.clear_effective();
    }
    if let Some(mut request) = world.get_resource_mut::<EnvironmentQualityRequest>() {
        request.clear_effective();
    }
}

pub(super) fn update(
    world: &mut World,
    tier: VisualQualityTier,
    dispatched: bool,
    domains: [QualityDomainProjection; 7],
    candidate: Option<&EffectiveQualityEvidence>,
) -> Option<QualityApplicationEvidence> {
    let mut state = world
        .remove_resource::<ApplicationState>()
        .unwrap_or_default();
    let now = world
        .get_resource::<Time<Real>>()
        .map_or(Duration::ZERO, Time::elapsed);
    state.observation = None;
    let plan = candidate
        .and_then(|e| e.plan.as_ref())
        .filter(|p| dispatched && p.blockers.is_empty() && p.source_bindings.is_some());
    let current_hash = candidate.and_then(|e| e.plan_hash);
    if plan.is_none() || current_hash.is_none() {
        if let Some(guard) = state.guard.take() {
            state.renderer_epoch = None;
            state.recovery = None;
            clear_overrides(world);
            state.evidence = Some(QualityApplicationEvidence {
                status: "plan_application_invalidated",
                identity: Some(guard.identity()),
                projected_domains: [false; 7],
                gpu_ready: false,
                error: Some(
                    "current source, capability or adapter snapshot is no longer installable"
                        .into(),
                ),
            });
        }
    } else {
        advance(world, tier, now, domains, current_hash, &mut state);
    }
    let evidence = state.evidence.clone();
    world.insert_resource(state);
    evidence
}

fn install(
    world: &mut World,
    tier: VisualQualityTier,
    now: Duration,
    sequence: &mut u64,
) -> Result<QualityApplicationGuard, String> {
    clear_overrides(world);
    *sequence = sequence
        .checked_add(1)
        .ok_or("application sequence exhausted")?;
    let deadline = now
        .checked_add(APPLICATION_TIMEOUT)
        .ok_or("application deadline exhausted")?;
    let guard = effective::application_guard(world, tier, *sequence, deadline)?;
    let sources = guard
        .plan()
        .source_bindings
        .ok_or("missing source bindings")?;
    let identity = guard.identity();
    let water = world
        .get_resource_mut::<WaterQualityRequest>()
        .is_some_and(|mut r| r.request_effective(&guard, now, identity, sources));
    let environment = world
        .get_resource_mut::<EnvironmentQualityRequest>()
        .is_some_and(|mut r| r.request_effective(&guard, now, identity, sources));
    if !water || !environment {
        clear_overrides(world);
        return Err("optical adapter rejected installation".into());
    }
    Ok(guard)
}

fn advance(
    world: &mut World,
    tier: VisualQualityTier,
    now: Duration,
    domains: [QualityDomainProjection; 7],
    current_hash: Option<forge_core::ContentHash>,
    state: &mut ApplicationState,
) {
    let recovering = state.recovery.is_some_and(|identity| {
        installed(world, identity) && ready_epoch(world, identity, None).is_some()
    });
    if state
        .guard
        .as_ref()
        .is_none_or(|g| Some(g.identity().plan_hash) != current_hash)
        || recovering
    {
        state.guard = None;
        state.renderer_epoch = None;
        state.recovery = None;
        let result = install(world, tier, now, &mut state.sequence);
        match result {
            Ok(guard) => state.guard = Some(guard),
            Err(error) => {
                state.evidence = Some(QualityApplicationEvidence {
                    status: "plan_application_rejected",
                    identity: None,
                    projected_domains: [false; 7],
                    gpu_ready: false,
                    error: Some(error),
                });
                return;
            }
        }
    }
    let Some(guard) = state.guard.as_mut() else {
        return;
    };
    let identity = guard.identity();
    let installed = installed(world, identity);
    let projected = projected_domains(world, guard, domains);
    let mut cameras = world.query_filtered::<&Camera, With<Camera3d>>();
    let active_camera_count = cameras
        .iter(world)
        .filter(|camera| camera.is_active)
        .count();
    let pipeline_rejected = world
        .get_resource::<super::GpuSubmissionEvidence>()
        .zip(world.get_resource::<bevy::diagnostic::FrameCount>())
        .is_some_and(|(gpu, frame)| gpu.pipeline_failure_for(identity, frame.0));
    let epoch = ready_epoch(world, identity, state.renderer_epoch);
    if let Some(epoch) = epoch {
        state.renderer_epoch = Some(epoch);
    }
    let observation = QualityApplicationObservation {
        current: if installed {
            identity
        } else {
            QualityApplicationIdentity {
                sequence: 0,
                ..identity
            }
        },
        projected_domains: projected.map(|yes| yes.then_some(identity)),
        gpu_ready: epoch.map(|_| identity),
        active_camera_count,
        pipeline_rejected,
    };
    let result = guard.observe(now, observation);
    state.observation = Some(observation);
    state.recovery = recovery_after(state, result, identity, installed, projected);
    let evidence = QualityApplicationEvidence {
        status: match result {
            QualityApplicationState::Pending => "plan_application_pending_gpu",
            QualityApplicationState::Ready(_) => "plan_application_ready",
            QualityApplicationState::Failed(_) if state.recovery.is_some() => {
                "plan_application_waiting_renderer"
            }
            QualityApplicationState::Failed(_) => "plan_application_failed",
        },
        identity: Some(identity),
        projected_domains: projected,
        gpu_ready: matches!(result, QualityApplicationState::Ready(_)),
        error: match result {
            QualityApplicationState::Failed(error) => Some(format!("{error:?}")),
            _ => None,
        },
    };
    record_transition(
        state.evidence.as_ref(),
        &evidence,
        now,
        state.renderer_epoch,
    );
    state.evidence = Some(evidence);
    // Failure does not claim rollback. Source/capability invalidation clears overrides above;
    // timeout retains the installed values visibly unacknowledged, without retrying every frame.
}

fn installed(world: &World, identity: QualityApplicationIdentity) -> bool {
    world
        .get_resource::<WaterQualityRequest>()
        .and_then(WaterQualityRequest::effective_identity)
        == Some(identity)
        && world
            .get_resource::<EnvironmentQualityRequest>()
            .and_then(EnvironmentQualityRequest::effective_identity)
            == Some(identity)
}

fn projected_domains(
    world: &World,
    guard: &QualityApplicationGuard,
    domains: [QualityDomainProjection; 7],
) -> [bool; 7] {
    let identity = guard.identity();
    let mut projected = domains.map(|domain| domain == QualityDomainProjection::CpuProjected);
    projected[3] = world
        .get_resource::<EnvironmentProjectionStats>()
        .and_then(|s| s.quality.as_ref())
        .is_some_and(|s| {
            s.effective_request == Some(identity)
                && s.requested == guard.plan().effective.environment
                && s.sky_budget_projected
                && s.generated_texture_bound
        });
    let water = world.get_resource::<WaterProjectionStats>();
    projected[4] = water.and_then(|s| s.quality.as_ref()).is_some_and(|s| {
        s.effective_request == Some(identity)
            && s.renderer_requested == guard.plan().effective.water
            && s.ssr_budget_projected
            && s.surface_tint_projected
            && s.material_reflectance_projected
    }) || water.is_some_and(|s| {
        s.optical_depth_preview
            && s.shader_uniforms_match_profile
            && s.forward_reflection_quality
                .as_ref()
                .is_some_and(|observation| observation.matches_application(guard))
    });
    projected
}

fn recovery_after(
    state: &ApplicationState,
    result: QualityApplicationState,
    identity: QualityApplicationIdentity,
    installed: bool,
    projected: [bool; 7],
) -> Option<QualityApplicationIdentity> {
    let was_ready = state
        .evidence
        .as_ref()
        .is_some_and(|e| e.identity == Some(identity) && e.gpu_ready);
    ((was_ready || state.recovery == Some(identity))
        && result == QualityApplicationState::Failed(QualityApplicationFailure::Invalidated)
        && installed
        && projected.iter().all(|yes| *yes))
    .then_some(identity)
}

fn record_transition(
    previous: Option<&QualityApplicationEvidence>,
    current: &QualityApplicationEvidence,
    now: Duration,
    epoch: Option<u64>,
) {
    if previous.is_none_or(|p| p.identity != current.identity || p.status != current.status) {
        info!(target: "forge::quality", schema = "forge.runtime-event", schema_version = 1_u64,
            event_kind = "quality_application_changed", status = current.status,
            sequence = current.identity.map(|i| i.sequence),
            plan_hash = current.identity.map(|i| i.plan_hash.to_string()),
            adapter_generation = current.identity.map(|i| i.adapter_generation),
            gpu_ready = current.gpu_ready, elapsed_seconds = now.as_secs_f64(),
            renderer_epoch = epoch, error = current.error.as_deref(),
            "effective application transition; not a full-preset or artistic qualification");
    }
}

fn ready_epoch(
    world: &mut World,
    identity: QualityApplicationIdentity,
    epoch: Option<u64>,
) -> Option<u64> {
    let key = super::gpu_submission::current_key(world, identity)?;
    let gpu = world.get_resource::<super::GpuSubmissionEvidence>()?;
    let frame = world.get_resource::<bevy::diagnostic::FrameCount>()?;
    gpu.ready_for(&key, frame.0, epoch)
}

#[cfg(test)]
mod tests;
