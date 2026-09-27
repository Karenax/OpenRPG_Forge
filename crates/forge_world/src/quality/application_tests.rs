use super::*;
use forge_project::effective_quality::{
    CapabilityStatus, QualityCapabilities, QualityCapability, QualityResolutionPolicy,
};

fn profile() -> VisualValleyQualityProfileDocument {
    VisualValleyQualityProfileDocument::from_json(include_str!(
        "../../../../examples/visual_valley/quality-profile.v1.json"
    ))
    .unwrap()
}

fn guard(
    source: &VisualValleyQualityProfileDocument,
    tier: VisualQualityTier,
    sequence: u64,
) -> QualityApplicationGuard {
    guard_with_deadline(source, tier, sequence, Duration::from_secs(10))
}

fn guard_with_deadline(
    source: &VisualValleyQualityProfileDocument,
    tier: VisualQualityTier,
    sequence: u64,
    deadline: Duration,
) -> QualityApplicationGuard {
    use QualityCapability::*;
    let capabilities = QualityCapabilities {
        schema_version: 1,
        adapter_id: "synthetic-controller-test".into(),
        generation: 11,
        terrain_constraints: None,
        source_bindings: Some(forge_project::effective_quality::QualitySourceBindings {
            terrain: source.content_hash().unwrap(),
            vegetation: source.content_hash().unwrap(),
            environment: source.content_hash().unwrap(),
            water: source.content_hash().unwrap(),
            weather: source.content_hash().unwrap(),
        }),
        capabilities: [
            Terrain,
            Vegetation,
            Shadows,
            Sky,
            Rain,
            PostProcess,
            EnvironmentReflection,
            ScreenReflection,
            SurfaceTint,
            DistanceFog,
            ReflectionIntensity,
        ]
        .into_iter()
        .map(|cap| (cap, CapabilityStatus::Supported))
        .chain(
            [ReflectionDistance, ScreenRefraction, VolumetricFog]
                .into_iter()
                .map(|cap| (cap, CapabilityStatus::Unavailable)),
        )
        .collect(),
    };
    QualityApplicationGuard::new(
        source,
        tier,
        &capabilities,
        QualityResolutionPolicy::Adr0011,
        sequence,
        deadline,
    )
    .unwrap()
}

#[test]
fn replacement_guard_cannot_extend_deadline_or_reset_observation_clock() {
    for (now, expected) in [
        (Duration::from_secs(11), QualityApplicationFailure::TimedOut),
        (
            Duration::from_millis(500),
            QualityApplicationFailure::ClockRegressed,
        ),
    ] {
        let (source, mut controller, decision) = pending();
        let initial = guard(&source, decision.to, decision.sequence);
        let mut observation = complete(&initial);
        observation.gpu_ready = None;
        controller
            .observe_application(&initial, Duration::from_secs(1), observation)
            .unwrap();
        let replacement = guard_with_deadline(
            &source,
            decision.to,
            decision.sequence,
            Duration::from_secs(100),
        );
        assert_eq!(initial.identity(), replacement.identity());
        let observation = complete(&replacement);
        assert_eq!(
            controller
                .observe_application(&replacement, now, observation)
                .unwrap(),
            QualityApplicationState::Failed(expected)
        );
        assert!(controller.applied_effective_identity().is_none());
    }
}

#[test]
fn replacement_guard_cannot_hide_loss_of_confirmed_gpu_evidence() {
    let (source, mut controller, decision) = pending();
    let initial = guard(&source, decision.to, decision.sequence);
    let observation = complete(&initial);
    controller
        .observe_application(&initial, Duration::from_secs(1), observation)
        .unwrap();
    let replacement = guard(&source, decision.to, decision.sequence);
    let mut observation = complete(&replacement);
    observation.gpu_ready = None;
    assert_eq!(
        controller
            .observe_application(&replacement, Duration::from_secs(2), observation)
            .unwrap(),
        QualityApplicationState::Failed(QualityApplicationFailure::Invalidated)
    );
    assert!(controller.applied_effective_identity().is_none());
}

fn complete(guard: &QualityApplicationGuard) -> QualityApplicationObservation {
    QualityApplicationObservation {
        current: guard.identity(),
        projected_domains: [Some(guard.identity()); 7],
        gpu_ready: Some(guard.identity()),
        active_camera_count: 1,
        pipeline_rejected: false,
    }
}

#[test]
fn observed_start_preserves_identity_sequence_and_initial_cooldown() {
    let source = profile();
    let initial = guard(&source, VisualQualityTier::High, 19);
    let mut controller = QualityAdaptationController::from_observed_application(
        &source,
        QualityControlMode::Adaptive,
        &initial,
        Duration::from_secs(1),
        complete(&initial),
    )
    .unwrap();
    assert_eq!(
        controller.applied_effective_identity(),
        Some(initial.identity())
    );
    for _ in 0..source.adaptation.cooldown_frames + source.adaptation.downgrade_sustain_frames - 1 {
        assert!(controller.observe_frame(30.0).unwrap().is_none());
    }
    let decision = controller.observe_frame(30.0).unwrap().unwrap();
    assert_eq!(decision.sequence, 20);
    assert_eq!(decision.to, VisualQualityTier::Medium);
    assert!(controller.acknowledge_applied(decision).is_err());
    assert!(controller.applied_effective_identity().is_none());
    let next = guard(&source, decision.to, decision.sequence);
    assert_eq!(
        controller
            .observe_application(&next, Duration::from_secs(2), complete(&next))
            .unwrap(),
        QualityApplicationState::Ready(next.identity())
    );
}

#[test]
fn observed_start_requires_current_complete_evidence() {
    let source = profile();
    let initial = guard(&source, VisualQualityTier::High, 7);
    let ready = complete(&initial);
    let mut cases = vec![ready; 11];
    cases[0].gpu_ready = None;
    for domain in 0..7 {
        cases[domain + 1].projected_domains[domain] = None;
    }
    cases[8].active_camera_count = 2;
    cases[9].pipeline_rejected = true;
    cases[10].current.sequence += 1;
    for observation in cases {
        assert!(matches!(
            QualityAdaptationController::from_observed_application(
                &source,
                QualityControlMode::Adaptive,
                &initial,
                Duration::from_secs(1),
                observation
            ),
            Err(QualityControlError::InitialApplicationNotReady(_))
        ));
    }
}

#[test]
fn observed_start_preserves_terminal_failure_and_clock_history() {
    let source = profile();
    let mut initial = guard(&source, VisualQualityTier::High, 7);
    let ready = complete(&initial);
    initial.observe(Duration::from_secs(2), ready);
    assert!(matches!(
        QualityAdaptationController::from_observed_application(
            &source,
            QualityControlMode::Adaptive,
            &initial,
            Duration::from_secs(1),
            ready
        ),
        Err(QualityControlError::InitialApplicationNotReady(
            QualityApplicationState::Failed(QualityApplicationFailure::ClockRegressed)
        ))
    ));
    let mut missing = ready;
    missing.gpu_ready = None;
    initial.observe(Duration::from_secs(3), missing);
    assert!(matches!(
        QualityAdaptationController::from_observed_application(
            &source,
            QualityControlMode::Adaptive,
            &initial,
            Duration::from_secs(4),
            ready
        ),
        Err(QualityControlError::InitialApplicationNotReady(
            QualityApplicationState::Failed(QualityApplicationFailure::Invalidated)
        ))
    ));
}

#[test]
fn observed_start_rejects_policy_mismatch_and_keeps_benchmark_fixed() {
    let mut source = profile();
    let initial = guard(&source, VisualQualityTier::High, 7);
    let mut controller = QualityAdaptationController::from_observed_application(
        &source,
        QualityControlMode::FixedBenchmark,
        &initial,
        Duration::from_secs(1),
        complete(&initial),
    )
    .unwrap();
    for duration in [5.0, 50.0] {
        for _ in 0..10_000 {
            assert!(controller.observe_frame(duration).unwrap().is_none());
        }
    }
    source.adaptation.cooldown_frames += 1;
    assert!(matches!(
        QualityAdaptationController::from_observed_application(
            &source,
            QualityControlMode::Adaptive,
            &initial,
            Duration::from_secs(1),
            complete(&initial)
        ),
        Err(QualityControlError::StaleAcknowledgement)
    ));
}

#[test]
fn observed_start_sequence_exhaustion_never_wraps() {
    let source = profile();
    let initial = guard(&source, VisualQualityTier::High, u64::MAX);
    let mut controller = QualityAdaptationController::from_observed_application(
        &source,
        QualityControlMode::Adaptive,
        &initial,
        Duration::from_secs(1),
        complete(&initial),
    )
    .unwrap();
    for _ in 0..source.adaptation.cooldown_frames + source.adaptation.downgrade_sustain_frames - 1 {
        assert!(controller.observe_frame(30.0).unwrap().is_none());
    }
    assert!(matches!(
        controller.observe_frame(30.0),
        Err(QualityControlError::SequenceExhausted)
    ));
    assert!(controller.pending_change().is_none());
}

#[test]
fn explicit_binding_is_one_shot_and_cannot_return_to_legacy_acknowledgement() {
    let (source, mut controller, decision) = pending();
    let installed = guard(&source, decision.to, decision.sequence + 4);
    assert!(
        controller
            .observe_application(&installed, Duration::from_secs(1), complete(&installed))
            .is_err()
    );
    controller
        .bind_pending_application(decision, &installed)
        .unwrap();
    assert!(
        controller
            .bind_pending_application(decision, &installed)
            .is_err()
    );
    assert_eq!(
        controller
            .observe_application(&installed, Duration::from_secs(1), complete(&installed))
            .unwrap(),
        QualityApplicationState::Ready(installed.identity())
    );
    for _ in 0..source.adaptation.cooldown_frames + source.adaptation.downgrade_sustain_frames - 1 {
        assert!(controller.observe_frame(30.0).unwrap().is_none());
    }
    let next = controller.observe_frame(30.0).unwrap().unwrap();
    assert_eq!(next.sequence, installed.identity().sequence + 1);
    controller.reject(next).unwrap();
    for _ in 0..source.adaptation.cooldown_frames + source.adaptation.downgrade_sustain_frames - 1 {
        assert!(controller.observe_frame(30.0).unwrap().is_none());
    }
    let retry = controller.observe_frame(30.0).unwrap().unwrap();
    assert!(controller.acknowledge_applied(retry).is_err());
}

fn pending() -> (
    VisualValleyQualityProfileDocument,
    QualityAdaptationController,
    QualityChange,
) {
    let source = profile();
    let mut controller = QualityAdaptationController::from_applied_tier(
        &source,
        QualityControlMode::Adaptive,
        VisualQualityTier::High,
    )
    .unwrap();
    for _ in 1..source.adaptation.downgrade_sustain_frames {
        assert!(controller.observe_frame(30.0).unwrap().is_none());
    }
    let decision = controller.observe_frame(30.0).unwrap().unwrap();
    (source, controller, decision)
}

#[test]
fn effective_acknowledgement_requires_gpu_and_duplicate_observation_keeps_cooldown() {
    let (source, mut controller, decision) = pending();
    let guard = guard(&source, decision.to, decision.sequence);
    let ready = complete(&guard);
    let mut cpu_only = ready;
    cpu_only.gpu_ready = None;
    assert_eq!(
        controller
            .observe_application(&guard, Duration::from_secs(1), cpu_only)
            .unwrap(),
        QualityApplicationState::Pending
    );
    assert_eq!(controller.applied_tier(), decision.from);
    assert!(controller.applied_effective_identity().is_none());
    assert_eq!(controller.pending_change(), Some(decision));
    assert!(controller.acknowledge_applied(decision).is_err());
    assert_eq!(
        controller
            .observe_application(&guard, Duration::from_secs(2), ready)
            .unwrap(),
        QualityApplicationState::Ready(ready.current)
    );
    assert_eq!(controller.applied_tier(), decision.to);
    assert_eq!(controller.applied_effective_identity(), Some(ready.current));
    assert!(controller.observe_frame(30.0).unwrap().is_none());
    let cooldown = controller.cooldown;
    controller
        .observe_application(&guard, Duration::from_secs(3), ready)
        .unwrap();
    assert_eq!(controller.cooldown, cooldown);
    for _ in 0..cooldown {
        assert!(controller.observe_frame(30.0).unwrap().is_none());
    }
    for _ in 1..source.adaptation.downgrade_sustain_frames {
        assert!(controller.observe_frame(30.0).unwrap().is_none());
    }
    let next = controller.observe_frame(30.0).unwrap().unwrap();
    assert!(controller.applied_effective_identity().is_none());
    assert!(matches!(
        controller.observe_application(&guard, Duration::from_secs(4), ready),
        Err(QualityControlError::StaleAcknowledgement)
    ));
    assert_eq!(controller.pending_change(), Some(next));
}

#[test]
fn wrong_policy_tier_or_sequence_does_not_consume_pending_request() {
    let (source, mut controller, decision) = pending();
    let mut changed = source.clone();
    changed.presets.high.water.reflection_steps += 1;
    for mut wrong in [
        guard(&source, decision.to, decision.sequence + 1),
        guard(&source, decision.from, decision.sequence),
        guard(&changed, decision.to, decision.sequence),
    ] {
        let observation = complete(&wrong);
        assert!(matches!(
            controller.observe_application(&wrong, Duration::from_secs(10), observation),
            Err(QualityControlError::StaleAcknowledgement)
        ));
        assert_eq!(controller.pending_change(), Some(decision));
        assert_eq!(controller.applied_tier(), decision.from);
        // Rejection must not advance or expire an unrelated guard.
        assert!(matches!(
            wrong.observe(Duration::from_secs(1), observation),
            QualityApplicationState::Ready(_)
        ));
    }
}

#[test]
fn timeout_stops_adaptation_without_claiming_rollback_or_accepting_late_success() {
    let (source, mut controller, decision) = pending();
    let guard = guard(&source, decision.to, decision.sequence);
    let observation = complete(&guard);
    assert_eq!(
        controller
            .observe_application(&guard, Duration::from_secs(10), observation)
            .unwrap(),
        QualityApplicationState::Failed(QualityApplicationFailure::TimedOut)
    );
    assert_eq!(
        controller.application_failure(),
        Some(QualityApplicationFailure::TimedOut)
    );
    assert_eq!(controller.applied_tier(), decision.from); // last known, not restored
    assert!(controller.applied_effective_identity().is_none());
    assert!(controller.pending_change().is_none());
    for _ in 0..1000 {
        assert!(controller.observe_frame(30.0).unwrap().is_none());
    }
    assert!(controller.acknowledge_applied(decision).is_err());
    assert_eq!(
        controller
            .observe_application(&guard, Duration::from_secs(11), observation)
            .unwrap(),
        QualityApplicationState::Failed(QualityApplicationFailure::TimedOut)
    );
}

#[test]
fn loss_of_installed_gpu_evidence_revokes_identity_and_stops_future_proposals() {
    let (source, mut controller, decision) = pending();
    let guard = guard(&source, decision.to, decision.sequence);
    let mut observation = complete(&guard);
    controller
        .observe_application(&guard, Duration::from_secs(1), observation)
        .unwrap();
    observation.gpu_ready = None;
    assert_eq!(
        controller
            .observe_application(&guard, Duration::from_secs(2), observation)
            .unwrap(),
        QualityApplicationState::Failed(QualityApplicationFailure::Invalidated)
    );
    assert!(controller.applied_effective_identity().is_none());
    assert_eq!(controller.applied_tier(), decision.to);
    for _ in 0..1000 {
        assert!(controller.observe_frame(30.0).unwrap().is_none());
    }
}

#[test]
fn benchmark_cannot_accept_unsolicited_effective_transition() {
    let source = profile();
    let mut controller = QualityAdaptationController::from_applied_tier(
        &source,
        QualityControlMode::FixedBenchmark,
        VisualQualityTier::High,
    )
    .unwrap();
    let guard = guard(&source, VisualQualityTier::Medium, 1);
    let observation = complete(&guard);
    assert!(matches!(
        controller.observe_application(&guard, Duration::from_secs(1), observation),
        Err(QualityControlError::StaleAcknowledgement)
    ));
    assert_eq!(controller.applied_tier(), VisualQualityTier::High);
    assert!(controller.observe_frame(50.0).unwrap().is_none());
}
