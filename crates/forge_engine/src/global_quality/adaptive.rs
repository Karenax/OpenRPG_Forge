//! Optional presentation-only adaptation. No author document mutations or benchmark opt-in.
use super::{GlobalQualityRequest, GlobalQualityStats, application::ApplicationState};
use bevy::{
    ecs::message::MessageCursor,
    prelude::*,
    window::{PrimaryWindow, WindowOccluded},
};
use forge_project::{VisualQualityTier, VisualValleyQualityProfileDocument};
use forge_world::{
    QualityAdaptationController, QualityControlMode,
    quality_application::{
        QualityApplicationGuard, QualityApplicationIdentity, QualityApplicationObservation,
        QualityApplicationState,
    },
};
use serde::Serialize;
use std::time::Duration;

#[derive(Resource, Default)]
struct WindowVisibility {
    events: MessageCursor<WindowOccluded>,
    occluded: std::collections::HashSet<Entity>,
    status: &'static str,
}

fn has_admissible_window(world: &mut World) -> bool {
    // Visibility survives manual/adaptive resets and message-buffer rotation.
    let mut visibility = world
        .remove_resource::<WindowVisibility>()
        .unwrap_or_default();
    if let Some(events) = world.get_resource::<Messages<WindowOccluded>>() {
        for event in visibility.events.read(events) {
            if event.occluded {
                visibility.occluded.insert(event.window);
            } else {
                visibility.occluded.remove(&event.window);
            }
        }
    }
    visibility
        .occluded
        .retain(|entity| world.get::<Window>(*entity).is_some());
    let status = world
        .query_filtered::<(Entity, &Window), With<PrimaryWindow>>()
        .single(world)
        .map_or("missing_or_ambiguous", |(entity, window)| {
            if !window.visible {
                "hidden"
            } else if visibility.occluded.contains(&entity) {
                "occluded"
            } else if !window.focused {
                "unfocused"
            } else if window.physical_width() == 0 || window.physical_height() == 0 {
                "zero_extent"
            } else {
                "eligible"
            }
        });
    visibility.status = status;
    world.insert_resource(visibility);
    status == "eligible"
}

#[derive(Clone, Debug, PartialEq, Serialize)]
pub struct QualityAdaptationStats {
    pub status: &'static str,
    pub sampling_window: &'static str,
    pub eligible_samples: u64,
    pub proposals: u64,
    pub acknowledged: Option<QualityApplicationIdentity>,
    pub applied_tier: Option<VisualQualityTier>,
    pub error: Option<String>,
}

impl Default for QualityAdaptationStats {
    fn default() -> Self {
        Self {
            status: "disabled",
            sampling_window: "not_observed",
            eligible_samples: 0,
            proposals: 0,
            acknowledged: None,
            applied_tier: None,
            error: None,
        }
    }
}

#[derive(Resource, Default)]
pub(super) struct AdaptiveState {
    controller: Option<QualityAdaptationController>,
    stats: QualityAdaptationStats,
    pending_since: Option<Duration>,
    pending_bound: bool,
    sources: Option<forge_project::effective_quality::QualitySourceBindings>,
}

impl AdaptiveState {
    fn stop(&mut self, error: impl Into<String>) -> Option<VisualQualityTier> {
        self.stats.status = "stopped";
        self.stats.error = Some(error.into());
        self.stats.acknowledged = None;
        self.controller = None;
        None
    }

    fn advance(
        &mut self,
        document: &VisualValleyQualityProfileDocument,
        snapshot: Option<(&QualityApplicationGuard, &QualityApplicationObservation)>,
        now: Duration,
        sample: Option<f32>,
    ) -> Option<VisualQualityTier> {
        if self.stats.status == "stopped" {
            return None;
        }
        if self.pending_since.is_some_and(|start| {
            now.checked_sub(start)
                .is_none_or(|age| age >= Duration::from_secs(30))
        }) {
            return self
                .stop("adaptive transition timed out or clock regressed; no rollback presumed");
        }
        let Some((guard, observation)) = snapshot else {
            if let Some(controller) = self.controller.as_mut() {
                controller.discard_observation_window();
            }
            self.stats.status = "waiting_application";
            self.stats.acknowledged = None;
            return None;
        };
        if self.controller.is_none() {
            // An incomplete initial snapshot is not a fault or permission to sample warmup.
            match QualityAdaptationController::from_observed_application(
                document,
                QualityControlMode::Adaptive,
                guard,
                now,
                *observation,
            ) {
                Ok(controller) => {
                    self.sources = guard.plan().source_bindings;
                    self.controller = Some(controller);
                }
                Err(error) => {
                    self.stats.status = "waiting_application";
                    self.stats.error = Some(error.to_string());
                    return None;
                }
            }
        }
        let controller = self.controller.as_mut()?;
        if self.sources != guard.plan().source_bindings
            || controller.policy_hash() != guard.plan().requested_policy_hash
        {
            return self.stop("source or quality policy changed; explicit re-enable required");
        }
        if let Some(decision) = controller.pending_change() {
            if guard.plan().requested_tier != decision.to {
                self.stats.status = "applying";
                return None;
            }
            if !self.pending_bound {
                if let Err(error) = controller.bind_pending_application(decision, guard) {
                    return self.stop(error.to_string());
                }
                self.pending_bound = true;
            }
        }
        match controller.observe_application(guard, now, *observation) {
            Ok(QualityApplicationState::Ready(identity)) => {
                self.stats.acknowledged = Some(identity);
                self.stats.applied_tier = Some(controller.applied_tier());
                self.pending_since = None;
                self.pending_bound = false;
                self.stats.error = None;
            }
            Ok(QualityApplicationState::Pending) => {
                self.stats.status = "applying";
                return None;
            }
            result => {
                return self.stop(format!(
                    "effective application lost: {result:?}; explicit re-enable required"
                ));
            }
        }
        let Some(milliseconds) = sample else {
            controller.discard_observation_window();
            self.stats.status = "waiting_focus";
            return None;
        };
        self.stats.status = "tracking";
        self.stats.eligible_samples = self.stats.eligible_samples.saturating_add(1);
        match controller.observe_frame(milliseconds) {
            Ok(Some(decision)) => {
                self.stats.status = "applying";
                self.stats.acknowledged = None;
                self.stats.proposals = self.stats.proposals.saturating_add(1);
                self.pending_since = Some(now);
                self.pending_bound = false;
                Some(decision.to)
            }
            Ok(None) => None,
            Err(error) => self.stop(error.to_string()),
        }
    }
}

pub(super) fn update(world: &mut World) {
    let mut state = world.remove_resource::<AdaptiveState>().unwrap_or_default();
    let previous = state.stats.clone();
    if std::mem::take(
        &mut world
            .resource_mut::<GlobalQualityRequest>()
            .restart_adaptation,
    ) {
        state = AdaptiveState::default();
    }
    let enabled = world.resource::<GlobalQualityRequest>().adaptive;
    let fixed_proof = world
        .get_resource::<crate::RuntimeAutomation>()
        .is_some_and(|automation| {
            automation.metrics_path.is_some() || automation.capture_path.is_some()
        });
    // A frozen Studio proof view has no native metrics/capture path, but must keep
    // the same quality policy throughout an optical comparison, even during setup.
    #[cfg(feature = "qualification")]
    let fixed_proof = fixed_proof || world.contains_resource::<crate::FixedProofViewStatus>();
    let focused = has_admissible_window(world);
    let time = world.get_resource::<Time<Real>>();
    let now = time.map_or(Duration::ZERO, Time::elapsed);
    let sample = time
        .filter(|_| focused)
        .map(|time| time.delta_secs() * 1000.0)
        .filter(|ms| ms.is_finite() && *ms > 0.0);
    let action = if !enabled {
        state = AdaptiveState::default();
        None
    } else if fixed_proof {
        state.stop("fixed proof/metrics/capture run forbids adaptation")
    } else if let Some(source) = world.get_resource::<crate::TerrainQualityRequest>() {
        state.advance(
            source.policy().0,
            world.resource::<ApplicationState>().snapshot(),
            now,
            sample,
        )
    } else {
        state.stop("missing quality policy")
    };
    if let Some(tier) = action {
        // Deliberately bypass the public MANUAL request, which disables adaptation.
        world.resource_mut::<GlobalQualityRequest>().tier = tier;
    }
    state.stats.sampling_window = world.resource::<WindowVisibility>().status;
    if previous.status != state.stats.status
        || previous.sampling_window != state.stats.sampling_window
        || previous.proposals != state.stats.proposals
        || previous.acknowledged != state.stats.acknowledged
    {
        tracing::info!(target: "forge::quality", schema = "forge.runtime-event", schema_version = 1_u64,
            event_kind = "quality_adaptation_changed", status = state.stats.status,
            sampling_window = state.stats.sampling_window,
            proposals = state.stats.proposals, eligible_samples = state.stats.eligible_samples,
            elapsed_seconds = now.as_secs_f64(), frame_time_ms = ?sample,
            source_bindings = ?state.sources,
            applied_tier = ?state.stats.applied_tier, requested_tier = ?world.resource::<GlobalQualityRequest>().tier,
            acknowledged = ?state.stats.acknowledged, error = ?state.stats.error,
            "optional effective quality adaptation");
    }
    world.resource_mut::<GlobalQualityStats>().adaptation = state.stats.clone();
    world.insert_resource(state);
}

#[cfg(test)]
mod tests;
