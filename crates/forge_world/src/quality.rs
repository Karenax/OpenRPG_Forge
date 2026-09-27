//! Pure quality decisions. A proposal is never reported as applied before adapter acknowledgement.
use crate::quality_application::{
    QualityApplicationFailure, QualityApplicationGuard, QualityApplicationIdentity,
    QualityApplicationObservation, QualityApplicationState,
};
use forge_core::ContentHash;
use forge_project::{
    DynamicQualityAdaptationProfile, VisualQualityTier, VisualValleyQualityProfileDocument,
    VisualValleyQualityProfileError,
};
use std::time::Duration;
use thiserror::Error;

#[cfg(test)]
mod application_tests;

#[derive(Clone, Copy, Debug, Eq, PartialEq)]
pub enum QualityControlMode {
    Adaptive,
    FixedBenchmark,
}

#[derive(Clone, Copy, Debug, Eq, PartialEq)]
pub enum QualityChangeReason {
    SustainedSlowFrames,
    SustainedFastFrames,
}

#[derive(Clone, Copy, Debug, Eq, PartialEq)]
pub struct QualityChange {
    pub sequence: u64,
    pub from: VisualQualityTier,
    pub to: VisualQualityTier,
    pub reason: QualityChangeReason,
}

#[derive(Debug, Error)]
pub enum QualityControlError {
    #[error(transparent)]
    Profile(#[from] VisualValleyQualityProfileError),
    #[error("applied quality tier is incompatible with the selected control mode or bounds")]
    IncompatibleAppliedTier,
    #[error("initial effective application is not ready: {0:?}")]
    InitialApplicationNotReady(QualityApplicationState),
    #[error("frame duration must be finite and strictly positive")]
    InvalidFrameDuration,
    #[error("quality acknowledgement does not match the pending decision")]
    StaleAcknowledgement,
    #[error("quality decision sequence exhausted")]
    SequenceExhausted,
}

/// Caller supplies the tier actually installed by its renderer, not merely the requested tier.
/// This ephemeral state neither edits the quality document nor owns any renderer resources.
pub struct QualityAdaptationController {
    policy_hash: ContentHash,
    policy: DynamicQualityAdaptationProfile,
    mode: QualityControlMode,
    applied_tier: VisualQualityTier,
    slow_frames: u16,
    fast_frames: u16,
    cooldown: u16,
    sequence: u64,
    pending: Option<QualityChange>,
    applied_identity: Option<QualityApplicationIdentity>,
    guarded_pending_identity: Option<QualityApplicationIdentity>,
    application_guard: Option<QualityApplicationGuard>,
    application_failure: Option<QualityApplicationFailure>,
    effective_only: bool,
}

impl QualityAdaptationController {
    /// Start from a freshly reobserved installation, preserving the guard's clock and
    /// failure history. The caller must supply current adapter evidence, not a saved DTO.
    /// Future decisions continue after this installation's session sequence. The runtime
    /// coordinator must still own sequence allocation while this controller is active.
    pub fn from_observed_application(
        document: &VisualValleyQualityProfileDocument,
        mode: QualityControlMode,
        guard: &QualityApplicationGuard,
        now: Duration,
        observation: QualityApplicationObservation,
    ) -> Result<Self, QualityControlError> {
        let mut controller = Self::from_applied_tier(document, mode, guard.plan().requested_tier)?;
        if guard.plan().requested_policy_hash != controller.policy_hash {
            return Err(QualityControlError::StaleAcknowledgement);
        }
        let mut owned_guard = guard.clone();
        let state = owned_guard.observe(now, observation);
        if state != QualityApplicationState::Ready(guard.identity()) {
            return Err(QualityControlError::InitialApplicationNotReady(state));
        }
        controller.sequence = guard.identity().sequence;
        controller.applied_identity = Some(guard.identity());
        controller.application_guard = Some(owned_guard);
        controller.cooldown = controller.policy.cooldown_frames;
        controller.effective_only = true;
        Ok(controller)
    }

    pub fn from_applied_tier(
        document: &VisualValleyQualityProfileDocument,
        mode: QualityControlMode,
        applied_tier: VisualQualityTier,
    ) -> Result<Self, QualityControlError> {
        let policy_hash = document.content_hash()?;
        let policy = document.adaptation;
        let compatible = match mode {
            QualityControlMode::Adaptive => {
                (policy.minimum_tier..=policy.maximum_tier).contains(&applied_tier)
            }
            QualityControlMode::FixedBenchmark => applied_tier == document.benchmark.fixed_tier,
        };
        if !compatible {
            return Err(QualityControlError::IncompatibleAppliedTier);
        }
        Ok(Self {
            policy_hash,
            policy,
            mode,
            applied_tier,
            slow_frames: 0,
            fast_frames: 0,
            cooldown: 0,
            sequence: 0,
            pending: None,
            applied_identity: None,
            guarded_pending_identity: None,
            application_guard: None,
            application_failure: None,
            effective_only: false,
        })
    }

    #[must_use]
    pub const fn policy_hash(&self) -> ContentHash {
        self.policy_hash
    }

    #[must_use]
    pub const fn applied_tier(&self) -> VisualQualityTier {
        self.applied_tier
    }

    /// Current effective acknowledgement, absent during a transition or after failure.
    /// `applied_tier` alone remains the last known tier, not proof of rollback/readiness.
    #[must_use]
    pub fn applied_effective_identity(&self) -> Option<QualityApplicationIdentity> {
        if self.pending.is_none() && self.application_failure.is_none() {
            self.applied_identity
        } else {
            None
        }
    }

    #[must_use]
    pub const fn application_failure(&self) -> Option<QualityApplicationFailure> {
        self.application_failure
    }

    #[must_use]
    pub const fn pending_change(&self) -> Option<QualityChange> {
        self.pending
    }

    /// Call on resize, focus loss, shader warmup or other nonrepresentative samples.
    /// Such interruptions cannot bridge two runs of sustained pressure.
    pub const fn discard_observation_window(&mut self) {
        self.slow_frames = 0;
        self.fast_frames = 0;
    }

    /// Consecutive eligible frames, strict thresholds, bounded counters, no wall clock or allocation.
    /// While an adapter is applying a proposal, further proposals are suppressed.
    pub fn observe_frame(
        &mut self,
        milliseconds: f32,
    ) -> Result<Option<QualityChange>, QualityControlError> {
        if !milliseconds.is_finite() || milliseconds <= 0.0 {
            self.discard_observation_window();
            return Err(QualityControlError::InvalidFrameDuration);
        }
        if self.mode == QualityControlMode::FixedBenchmark
            || !self.policy.policy_enabled
            || self.pending.is_some()
            || self.application_failure.is_some()
        {
            return Ok(None);
        }
        if self.cooldown > 0 {
            self.cooldown -= 1;
            self.discard_observation_window();
            return Ok(None);
        }
        let reason = if milliseconds > self.policy.downgrade_above_frame_time_ms {
            self.fast_frames = 0;
            self.slow_frames = self.slow_frames.saturating_add(1);
            (self.slow_frames >= self.policy.downgrade_sustain_frames)
                .then_some(QualityChangeReason::SustainedSlowFrames)
        } else if milliseconds < self.policy.upgrade_below_frame_time_ms {
            self.slow_frames = 0;
            self.fast_frames = self.fast_frames.saturating_add(1);
            (self.fast_frames >= self.policy.upgrade_sustain_frames)
                .then_some(QualityChangeReason::SustainedFastFrames)
        } else {
            self.discard_observation_window();
            None
        };
        let Some(reason) = reason else {
            return Ok(None);
        };
        self.discard_observation_window();
        let mut target = self.applied_tier;
        for _ in 0..self.policy.maximum_tier_step {
            target = adjacent_tier(target, reason)
                .clamp(self.policy.minimum_tier, self.policy.maximum_tier);
        }
        if target == self.applied_tier {
            return Ok(None);
        }
        let sequence = self
            .sequence
            .checked_add(1)
            .ok_or(QualityControlError::SequenceExhausted)?;
        let decision = QualityChange {
            sequence,
            from: self.applied_tier,
            to: target,
            reason,
        };
        self.sequence = sequence;
        self.pending = Some(decision);
        Ok(Some(decision))
    }

    /// Only call after every affected adapter has successfully installed the requested preset.
    pub fn acknowledge_applied(
        &mut self,
        decision: QualityChange,
    ) -> Result<(), QualityControlError> {
        if self.effective_only {
            return Err(QualityControlError::StaleAcknowledgement);
        }
        self.resolve(decision, true)?;
        self.application_guard = None;
        Ok(())
    }

    /// Bind the current renderer snapshot to the pending proposal (or revalidate the
    /// installed effective plan when idle). The first matching guard is copied into
    /// controller-owned state; later templates cannot reset its clock/deadline/history.
    /// Call even without GPU evidence for timeout. The input guard is not advanced.
    /// Failure stops this controller permanently; reconstruct only from freshly observed
    /// installation, not from the remembered tier. No automatic rollback is presumed.
    pub fn observe_application(
        &mut self,
        guard: &QualityApplicationGuard,
        now: Duration,
        observation: QualityApplicationObservation,
    ) -> Result<QualityApplicationState, QualityControlError> {
        if let Some(failure) = self.application_failure {
            return Ok(QualityApplicationState::Failed(failure));
        }
        let identity = guard.identity();
        let matches = guard.plan().requested_policy_hash == self.policy_hash
            && if let Some(decision) = self.pending {
                (identity.sequence == decision.sequence
                    || self.guarded_pending_identity == Some(identity))
                    && guard.plan().requested_tier == decision.to
                    && self
                        .guarded_pending_identity
                        .is_none_or(|bound| bound == identity)
            } else {
                self.applied_identity == Some(identity)
                    && guard.plan().requested_tier == self.applied_tier
            };
        if !matches {
            return Err(QualityControlError::StaleAcknowledgement);
        }
        if self.pending.is_some() {
            self.effective_only = true;
            if self.guarded_pending_identity.is_none() {
                self.application_guard = Some(guard.clone());
            }
            self.guarded_pending_identity = Some(identity);
        }
        let state = self
            .application_guard
            .as_mut()
            .ok_or(QualityControlError::StaleAcknowledgement)?
            .observe(now, observation);
        match state {
            QualityApplicationState::Ready(_) => {
                if let Some(decision) = self.pending {
                    self.resolve(decision, true)?;
                    self.sequence = identity.sequence;
                    self.applied_identity = Some(identity);
                }
            }
            QualityApplicationState::Failed(failure) => {
                self.application_failure = Some(failure);
                self.application_guard = None;
                self.pending = None;
                self.guarded_pending_identity = None;
                self.applied_identity = None;
                self.discard_observation_window();
            }
            QualityApplicationState::Pending => {}
        }
        Ok(state)
    }

    /// Explicitly bind a proposal to the coordinator's newer installation sequence.
    /// Intermediate capability snapshots may consume sequences before the final plan.
    /// Binding is one-shot and never acknowledges application or resets guard history.
    pub fn bind_pending_application(
        &mut self,
        decision: QualityChange,
        guard: &QualityApplicationGuard,
    ) -> Result<(), QualityControlError> {
        let identity = guard.identity();
        if self.application_failure.is_some()
            || self.pending != Some(decision)
            || self.guarded_pending_identity.is_some()
            || identity.sequence < decision.sequence
            || guard.plan().requested_tier != decision.to
            || guard.plan().requested_policy_hash != self.policy_hash
        {
            return Err(QualityControlError::StaleAcknowledgement);
        }
        self.effective_only = true;
        self.guarded_pending_identity = Some(identity);
        self.application_guard = Some(guard.clone());
        Ok(())
    }

    /// Rejection keeps the previous tier and imposes cooldown to avoid an unsupported-tier retry loop.
    pub fn reject(&mut self, decision: QualityChange) -> Result<(), QualityControlError> {
        self.resolve(decision, false)?;
        self.application_guard = None;
        Ok(())
    }

    fn resolve(
        &mut self,
        decision: QualityChange,
        applied: bool,
    ) -> Result<(), QualityControlError> {
        if self.pending != Some(decision) {
            return Err(QualityControlError::StaleAcknowledgement);
        }
        if applied {
            self.applied_tier = decision.to;
        }
        self.pending = None;
        self.applied_identity = None;
        self.guarded_pending_identity = None;
        self.discard_observation_window();
        self.cooldown = self.policy.cooldown_frames;
        Ok(())
    }
}

const fn adjacent_tier(tier: VisualQualityTier, reason: QualityChangeReason) -> VisualQualityTier {
    use VisualQualityTier::{High, Low, Medium, Ultra};
    match reason {
        QualityChangeReason::SustainedSlowFrames => match tier {
            Low | Medium => Low,
            High => Medium,
            Ultra => High,
        },
        QualityChangeReason::SustainedFastFrames => match tier {
            Low => Medium,
            Medium => High,
            High | Ultra => Ultra,
        },
    }
}

#[cfg(test)]
mod tests {
    use super::*;
    fn profile() -> VisualValleyQualityProfileDocument {
        VisualValleyQualityProfileDocument::from_json(include_str!(
            "../../../examples/visual_valley/quality-profile.v1.json"
        ))
        .unwrap()
    }
    fn controller(document: &VisualValleyQualityProfileDocument) -> QualityAdaptationController {
        QualityAdaptationController::from_applied_tier(
            document,
            QualityControlMode::Adaptive,
            document.default_tier,
        )
        .unwrap()
    }
    fn samples(control: &mut QualityAdaptationController, count: u16, milliseconds: f32) {
        for _ in 0..count {
            assert!(control.observe_frame(milliseconds).unwrap().is_none());
        }
    }

    #[test]
    fn sustained_pressure_proposes_but_never_claims_application() {
        let document = profile();
        let original = document.content_hash().unwrap();
        let mut control = controller(&document);
        samples(&mut control, 89, 30.0);
        let change = control.observe_frame(30.0).unwrap().unwrap();
        assert_eq!(
            (change.from, change.to),
            (VisualQualityTier::High, VisualQualityTier::Medium)
        );
        assert_eq!(control.applied_tier(), VisualQualityTier::High);
        samples(&mut control, 1_000, 30.0);
        assert_eq!(control.pending_change(), Some(change));
        control.acknowledge_applied(change).unwrap();
        assert_eq!(control.applied_tier(), VisualQualityTier::Medium);
        assert_eq!(control.policy_hash(), original);
        assert_eq!(document.content_hash().unwrap(), original);
    }

    #[test]
    fn hysteresis_and_invalid_or_discarded_samples_break_consecutive_pressure() {
        let document = profile();
        for interrupt in [
            Some(18.0),
            Some(13.5),
            Some(16.667),
            Some(f32::NAN),
            Some(f32::INFINITY),
            Some(0.0),
            Some(-1.0),
            None,
        ] {
            let mut control = controller(&document);
            samples(&mut control, 89, 30.0);
            if let Some(value) = interrupt {
                let result = control.observe_frame(value);
                if value.is_finite() && value > 0.0 {
                    assert!(result.unwrap().is_none());
                } else {
                    assert!(matches!(
                        result,
                        Err(QualityControlError::InvalidFrameDuration)
                    ));
                }
            } else {
                control.discard_observation_window();
            }
            samples(&mut control, 89, 30.0);
            assert!(control.observe_frame(30.0).unwrap().is_some());
        }
    }

    #[test]
    fn cooldown_and_longer_upgrade_window_prevent_flapping() {
        let document = profile();
        let mut control = controller(&document);
        samples(&mut control, 89, 30.0);
        let down = control.observe_frame(30.0).unwrap().unwrap();
        control.acknowledge_applied(down).unwrap();
        samples(&mut control, 240, 8.0);
        samples(&mut control, 599, 8.0);
        let up = control.observe_frame(8.0).unwrap().unwrap();
        assert_eq!(up.to, VisualQualityTier::High);
        assert_eq!(up.sequence, down.sequence + 1);
    }

    #[test]
    fn rejected_and_stale_acknowledgements_do_not_change_the_applied_tier() {
        let document = profile();
        let mut control = controller(&document);
        samples(&mut control, 89, 30.0);
        let rejected = control.observe_frame(30.0).unwrap().unwrap();
        let stale = QualityChange {
            sequence: rejected.sequence + 1,
            ..rejected
        };
        assert!(matches!(
            control.acknowledge_applied(stale),
            Err(QualityControlError::StaleAcknowledgement)
        ));
        assert_eq!(control.pending_change(), Some(rejected));
        control.reject(rejected).unwrap();
        assert_eq!(control.applied_tier(), VisualQualityTier::High);
        samples(&mut control, 240 + 89, 30.0);
        let retry = control.observe_frame(30.0).unwrap().unwrap();
        assert_ne!(retry.sequence, rejected.sequence);
        assert!(control.acknowledge_applied(rejected).is_err());
        control.acknowledge_applied(retry).unwrap();
    }

    #[test]
    fn benchmark_and_disabled_policy_never_adapt() {
        let mut document = profile();
        let mut benchmark = QualityAdaptationController::from_applied_tier(
            &document,
            QualityControlMode::FixedBenchmark,
            VisualQualityTier::High,
        )
        .unwrap();
        samples(&mut benchmark, 10_000, 50.0);
        samples(&mut benchmark, 10_000, 5.0);
        assert_eq!(benchmark.applied_tier(), VisualQualityTier::High);
        assert!(
            QualityAdaptationController::from_applied_tier(
                &document,
                QualityControlMode::FixedBenchmark,
                VisualQualityTier::Low
            )
            .is_err()
        );
        document.adaptation.policy_enabled = false;
        samples(&mut controller(&document), 10_000, 50.0);
    }

    #[test]
    fn configured_step_and_bounds_are_respected() {
        let mut document = profile();
        document.adaptation.maximum_tier_step = 3;
        let mut control = controller(&document);
        samples(&mut control, 89, 30.0);
        let down = control.observe_frame(30.0).unwrap().unwrap();
        assert_eq!(down.to, VisualQualityTier::Low);
        control.acknowledge_applied(down).unwrap();
        samples(&mut control, 10_000, 30.0);
        samples(&mut control, 599, 5.0);
        let up = control.observe_frame(5.0).unwrap().unwrap();
        assert_eq!(up.to, VisualQualityTier::Ultra);
        control.acknowledge_applied(up).unwrap();
        samples(&mut control, 10_000, 5.0);
    }
}
