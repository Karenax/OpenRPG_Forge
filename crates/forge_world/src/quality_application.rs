//! Ephemeral application guard for ADR-0011, not a GPU probe or a persistent document.
use forge_core::ContentHash;
use forge_project::{
    VisualQualityTier, VisualValleyQualityProfileDocument,
    effective_quality::{
        EffectiveQualityPlan, QualityCapabilities, QualityResolutionError, QualityResolutionPolicy,
        resolve_effective_quality,
    },
};
use std::time::Duration;
use thiserror::Error;

#[derive(Clone, Copy, Debug, PartialEq, Eq, serde::Serialize)]
pub struct QualityApplicationIdentity {
    pub sequence: u64,
    pub plan_hash: ContentHash,
    pub adapter_generation: u64,
}

#[derive(Clone, Copy, Debug, PartialEq, Eq)]
pub enum QualityApplicationFailure {
    Invalidated,
    AmbiguousCamera,
    PipelineRejected,
    TimedOut,
    ClockRegressed,
}

#[derive(Clone, Copy, Debug, PartialEq, Eq)]
pub enum QualityApplicationState {
    Pending,
    Ready(QualityApplicationIdentity),
    Failed(QualityApplicationFailure),
}

/// The adapter must observe actual parameters, not echo request receipt. All observations
/// belong to one current snapshot; the guard never accumulates historical domain successes.
#[derive(Clone, Copy, Debug)]
pub struct QualityApplicationObservation {
    /// Current installation identity; change it on source/capability/adapter replacement.
    pub current: QualityApplicationIdentity,
    /// Terrain, vegetation, shadows, environment, water, rain, post-process, in that order.
    pub projected_domains: [Option<QualityApplicationIdentity>; 7],
    /// Separate renderer evidence for the same installation, not CPU component presence.
    pub gpu_ready: Option<QualityApplicationIdentity>,
    pub active_camera_count: usize,
    pub pipeline_rejected: bool,
}

#[derive(Debug, Error)]
pub enum QualityApplicationError {
    #[error(transparent)]
    Resolution(#[from] QualityResolutionError),
    #[error("effective quality plan still has unqualified capabilities")]
    Unresolved,
    #[error("effective quality plan is not bound to all source documents")]
    UnboundSources,
    #[error("application sequence and absolute monotonic deadline must be nonzero")]
    InvalidRequest,
}

#[derive(Clone)]
pub struct QualityApplicationGuard {
    plan: EffectiveQualityPlan,
    identity: QualityApplicationIdentity,
    deadline: Duration,
    last_observation: Duration,
    state: QualityApplicationState,
}

impl QualityApplicationGuard {
    /// Re-resolve the plan from authoritative inputs. Sequences are caller-owned and must
    /// not be reused within its session; this guard does not establish cross-process identity.
    pub fn new(
        source: &VisualValleyQualityProfileDocument,
        tier: VisualQualityTier,
        capabilities: &QualityCapabilities,
        policy: QualityResolutionPolicy,
        sequence: u64,
        deadline: Duration,
    ) -> Result<Self, QualityApplicationError> {
        if sequence == 0 || deadline.is_zero() {
            return Err(QualityApplicationError::InvalidRequest);
        }
        let plan = resolve_effective_quality(source, tier, capabilities, policy)?;
        if !plan.blockers.is_empty() {
            return Err(QualityApplicationError::Unresolved);
        }
        if plan.source_bindings.is_none() {
            return Err(QualityApplicationError::UnboundSources);
        }
        let identity = QualityApplicationIdentity {
            sequence,
            plan_hash: plan.content_hash()?,
            adapter_generation: plan.adapter_generation,
        };
        Ok(Self {
            plan,
            identity,
            deadline,
            last_observation: Duration::ZERO,
            state: QualityApplicationState::Pending,
        })
    }

    #[must_use]
    pub const fn plan(&self) -> &EffectiveQualityPlan {
        &self.plan
    }

    #[must_use]
    pub const fn identity(&self) -> QualityApplicationIdentity {
        self.identity
    }

    /// Borrow a pending plan for installation only against a fresh caller-owned snapshot.
    /// This is not an acknowledgment. The coordinator must still observe all domains and GPU
    /// evidence, invalidate installed overrides on changes, and never reuse a sequence.
    #[must_use]
    pub fn installation_plan(
        &self,
        now: Duration,
        current: QualityApplicationIdentity,
        sources: forge_project::effective_quality::QualitySourceBindings,
    ) -> Option<&EffectiveQualityPlan> {
        (self.state == QualityApplicationState::Pending
            && now >= self.last_observation
            && now < self.deadline
            && current == self.identity
            && self.plan.source_bindings == Some(sources))
        .then_some(&self.plan)
    }

    /// Call even without fresh GPU results so timeout is enforced. Failure is terminal;
    /// retry requires a new guard/sequence. No rollback or previous profile is presumed.
    /// A ready snapshot is not timeless: subsequent loss of evidence invalidates it.
    pub fn observe(
        &mut self,
        now: Duration,
        observation: QualityApplicationObservation,
    ) -> QualityApplicationState {
        use QualityApplicationFailure as F;
        use QualityApplicationState as S;
        if matches!(self.state, S::Failed(_)) {
            return self.state;
        }
        let failure = if now < self.last_observation {
            Some(F::ClockRegressed)
        } else if observation.current != self.identity {
            Some(F::Invalidated)
        } else if observation.active_camera_count > 1 {
            Some(F::AmbiguousCamera)
        } else if observation.pipeline_rejected {
            Some(F::PipelineRejected)
        } else {
            None
        };
        self.last_observation = now;
        let complete = observation.active_camera_count == 1
            && observation.gpu_ready == Some(self.identity)
            && observation
                .projected_domains
                .iter()
                .all(|domain| *domain == Some(self.identity));
        self.state = if let Some(failure) = failure {
            S::Failed(failure)
        } else if matches!(self.state, S::Ready(_)) {
            if complete {
                S::Ready(self.identity)
            } else {
                S::Failed(F::Invalidated)
            }
        } else if now >= self.deadline {
            S::Failed(F::TimedOut)
        } else if complete {
            S::Ready(self.identity)
        } else {
            S::Pending
        };
        self.state
    }
}

#[cfg(test)]
mod tests;
