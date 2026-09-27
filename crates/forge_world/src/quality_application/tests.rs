use super::*;
use forge_project::effective_quality::{CapabilityStatus, QualityCapability};

fn inputs() -> (VisualValleyQualityProfileDocument, QualityCapabilities) {
    use QualityCapability::*;
    let source = VisualValleyQualityProfileDocument::from_json(include_str!(
        "../../../../examples/visual_valley/quality-profile.v1.json"
    ))
    .unwrap();
    let caps = QualityCapabilities {
        terrain_constraints: None,
        source_bindings: Some(forge_project::effective_quality::QualitySourceBindings {
            terrain: source.content_hash().unwrap(),
            vegetation: source.content_hash().unwrap(),
            environment: source.content_hash().unwrap(),
            water: source.content_hash().unwrap(),
            weather: source.content_hash().unwrap(),
        }),
        schema_version: 1,
        adapter_id: "synthetic-ack-test".into(),
        generation: 7,
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
    (source, caps)
}

fn guard() -> QualityApplicationGuard {
    let (source, caps) = inputs();
    QualityApplicationGuard::new(
        &source,
        VisualQualityTier::High,
        &caps,
        QualityResolutionPolicy::Adr0011,
        3,
        Duration::from_secs(10),
    )
    .unwrap()
}

#[test]
fn installation_requires_pending_fresh_identity_sources_and_time() {
    let mut guard = guard();
    let identity = guard.identity();
    let sources = guard.plan().source_bindings.unwrap();
    assert!(
        guard
            .installation_plan(Duration::ZERO, identity, sources)
            .is_some()
    );
    assert!(
        guard
            .installation_plan(Duration::from_secs(10), identity, sources)
            .is_none()
    );
    let mut stale = identity;
    stale.sequence += 1;
    assert!(
        guard
            .installation_plan(Duration::ZERO, stale, sources)
            .is_none()
    );
    let mut stale_sources = sources;
    stale_sources.water = "ab".repeat(32).parse().unwrap();
    assert!(
        guard
            .installation_plan(Duration::ZERO, identity, stale_sources)
            .is_none()
    );
    let mut observation = complete(&guard);
    observation.gpu_ready = None;
    guard.observe(Duration::from_secs(2), observation);
    assert!(
        guard
            .installation_plan(Duration::from_secs(1), identity, sources)
            .is_none()
    );
    assert!(
        guard
            .installation_plan(Duration::from_secs(2), identity, sources)
            .is_some()
    );
    guard.observe(Duration::from_secs(10), observation);
    assert!(
        guard
            .installation_plan(Duration::from_secs(2), identity, sources)
            .is_none()
    );
    let mut ready = self::guard();
    ready.observe(Duration::from_secs(1), complete(&ready));
    assert!(
        ready
            .installation_plan(Duration::from_secs(2), ready.identity(), sources)
            .is_none()
    );
}

#[test]
fn application_requires_source_bindings_and_invalidates_after_a_source_edit() {
    let (source, mut caps) = inputs();
    caps.source_bindings = None;
    assert!(matches!(
        QualityApplicationGuard::new(
            &source,
            VisualQualityTier::High,
            &caps,
            QualityResolutionPolicy::Adr0011,
            1,
            Duration::from_secs(10)
        ),
        Err(QualityApplicationError::UnboundSources)
    ));
    let (source, mut caps) = inputs();
    let mut original = QualityApplicationGuard::new(
        &source,
        VisualQualityTier::High,
        &caps,
        QualityResolutionPolicy::Adr0011,
        1,
        Duration::from_secs(10),
    )
    .unwrap();
    caps.source_bindings.as_mut().unwrap().water = "aa".repeat(32).parse().unwrap();
    let changed = QualityApplicationGuard::new(
        &source,
        VisualQualityTier::High,
        &caps,
        QualityResolutionPolicy::Adr0011,
        1,
        Duration::from_secs(10),
    )
    .unwrap();
    assert_ne!(original.identity(), changed.identity());
    assert_eq!(
        original.observe(
            Duration::from_secs(1),
            QualityApplicationObservation {
                current: changed.identity(),
                projected_domains: [Some(changed.identity()); 7],
                gpu_ready: Some(changed.identity()),
                active_camera_count: 1,
                pipeline_rejected: false,
            }
        ),
        QualityApplicationState::Failed(QualityApplicationFailure::Invalidated)
    );
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
fn cpu_only_and_every_stale_domain_or_gpu_identity_remain_pending() {
    let mut guard = guard();
    assert_eq!(guard.plan().substitutions.len(), 3);
    let mut observation = complete(&guard);
    observation.gpu_ready = None;
    assert_eq!(
        guard.observe(Duration::from_secs(1), observation),
        QualityApplicationState::Pending
    );
    let identity = guard.identity();
    let stale = [
        QualityApplicationIdentity {
            sequence: 2,
            ..identity
        },
        QualityApplicationIdentity {
            adapter_generation: 6,
            ..identity
        },
        QualityApplicationIdentity {
            plan_hash: inputs().0.content_hash().unwrap(),
            ..identity
        },
    ];
    for wrong in stale {
        for domain in 0..7 {
            let mut observation = complete(&guard);
            observation.projected_domains[domain] = Some(wrong);
            assert_eq!(
                guard.observe(Duration::from_secs(2), observation),
                QualityApplicationState::Pending
            );
        }
        let mut observation = complete(&guard);
        observation.gpu_ready = Some(wrong);
        assert_eq!(
            guard.observe(Duration::from_secs(2), observation),
            QualityApplicationState::Pending
        );
    }
    let observation = complete(&guard);
    assert_eq!(
        guard.observe(Duration::from_secs(3), observation),
        QualityApplicationState::Ready(identity)
    );
    assert_eq!(
        guard.observe(Duration::from_secs(11), observation),
        QualityApplicationState::Ready(identity)
    );
}

#[test]
fn failures_are_terminal_and_late_success_cannot_rescue_timeout() {
    use QualityApplicationFailure as F;
    for failure in [
        F::Invalidated,
        F::AmbiguousCamera,
        F::PipelineRejected,
        F::TimedOut,
        F::ClockRegressed,
    ] {
        let mut guard = guard();
        let mut waiting = complete(&guard);
        waiting.gpu_ready = None;
        guard.observe(Duration::from_secs(2), waiting);
        let mut observation = complete(&guard);
        let now = match failure {
            F::Invalidated => {
                observation.current.sequence += 1;
                Duration::from_secs(3)
            }
            F::AmbiguousCamera => {
                observation.active_camera_count = 2;
                Duration::from_secs(3)
            }
            F::PipelineRejected => {
                observation.pipeline_rejected = true;
                Duration::from_secs(3)
            }
            F::TimedOut => Duration::from_secs(10),
            F::ClockRegressed => Duration::from_secs(1),
        };
        assert_eq!(
            guard.observe(now, observation),
            QualityApplicationState::Failed(failure)
        );
        let observation = complete(&guard);
        assert_eq!(
            guard.observe(Duration::from_secs(12), observation),
            QualityApplicationState::Failed(failure)
        );
    }
}

#[test]
fn readiness_is_revoked_on_missing_evidence_and_domain_snapshots_do_not_accumulate() {
    let mut guard = guard();
    for missing in 0..7 {
        let mut observation = complete(&guard);
        observation.projected_domains[missing] = None;
        assert_eq!(
            guard.observe(Duration::from_secs(1), observation),
            QualityApplicationState::Pending
        );
    }
    let mut observation = complete(&guard);
    observation.active_camera_count = 0;
    assert_eq!(
        guard.observe(Duration::from_secs(2), observation),
        QualityApplicationState::Pending
    );
    observation.active_camera_count = 1;
    assert!(matches!(
        guard.observe(Duration::from_secs(3), observation),
        QualityApplicationState::Ready(_)
    ));
    observation.gpu_ready = None;
    assert_eq!(
        guard.observe(Duration::from_secs(4), observation),
        QualityApplicationState::Failed(QualityApplicationFailure::Invalidated)
    );
}

#[test]
fn unresolved_and_invalid_requests_never_start_application() {
    let (source, mut caps) = inputs();
    for (sequence, deadline) in [(0, Duration::from_secs(10)), (1, Duration::ZERO)] {
        assert!(matches!(
            QualityApplicationGuard::new(
                &source,
                VisualQualityTier::High,
                &caps,
                QualityResolutionPolicy::Adr0011,
                sequence,
                deadline
            ),
            Err(QualityApplicationError::InvalidRequest)
        ));
    }
    caps.capabilities.remove(&QualityCapability::SurfaceTint);
    assert!(matches!(
        QualityApplicationGuard::new(
            &source,
            VisualQualityTier::High,
            &caps,
            QualityResolutionPolicy::Adr0011,
            1,
            Duration::from_secs(10)
        ),
        Err(QualityApplicationError::Unresolved)
    ));
}

#[test]
fn source_or_adapter_replacement_revokes_ready_snapshot() {
    for change_generation in [false, true] {
        let mut guard = guard();
        let mut observation = complete(&guard);
        assert!(matches!(
            guard.observe(Duration::from_secs(1), observation),
            QualityApplicationState::Ready(_)
        ));
        if change_generation {
            observation.current.adapter_generation += 1;
        } else {
            observation.current.plan_hash = inputs().0.content_hash().unwrap();
        }
        assert_eq!(
            guard.observe(Duration::from_secs(2), observation),
            QualityApplicationState::Failed(QualityApplicationFailure::Invalidated)
        );
    }
}
