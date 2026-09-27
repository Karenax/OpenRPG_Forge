use super::*;

fn fixture() -> (World, ApplicationState, EffectiveQualityEvidence) {
    let terrain = forge_world::embedded_visual_valley_terrain_document().unwrap();
    let policy = forge_project::VisualValleyQualityProfileDocument::from_json(include_str!(
        "../../../../../examples/visual_valley/quality-profile.v1.json"
    ))
    .unwrap();
    let guard = crate::global_quality::test_plan::guard(&policy, VisualQualityTier::High);
    let mut world = World::new();
    world.insert_resource(Time::<Real>::default());
    world.spawn((Camera3d::default(), Camera::default()));
    let sources = guard.plan().source_bindings.unwrap();
    let mut water =
        WaterQualityRequest::new(&terrain, policy.clone(), VisualQualityTier::High).unwrap();
    let mut environment =
        EnvironmentQualityRequest::new(&terrain, policy, VisualQualityTier::High).unwrap();
    assert!(water.request_effective(&guard, Duration::ZERO, guard.identity(), sources));
    assert!(environment.request_effective(&guard, Duration::ZERO, guard.identity(), sources));
    world.insert_resource(water);
    world.insert_resource(environment);
    let candidate = EffectiveQualityEvidence {
        status: "resolved_plan_not_applied",
        plan: Some(guard.plan().clone()),
        plan_hash: Some(guard.identity().plan_hash),
        error: None,
        gpu_ready: false,
    };
    (
        world,
        ApplicationState {
            sequence: 1,
            guard: Some(guard),
            evidence: None,
            renderer_epoch: None,
            recovery: None,
            observation: None,
        },
        candidate,
    )
}

#[test]
fn redispatch_discards_old_application_but_preserves_sequence_counter() {
    let (_, mut state, _) = fixture();
    let identity = state.guard.as_ref().unwrap().identity();
    state.renderer_epoch = Some(7);
    state.recovery = Some(identity);
    state.evidence = Some(QualityApplicationEvidence {
        status: "plan_application_ready",
        identity: Some(identity),
        projected_domains: [true; 7],
        gpu_ready: true,
        error: None,
    });
    state.observation = Some(QualityApplicationObservation {
        current: identity,
        projected_domains: [Some(identity); 7],
        gpu_ready: Some(identity),
        active_camera_count: 1,
        pipeline_rejected: false,
    });
    state.reset_for_dispatch();
    assert_eq!(state.sequence, identity.sequence);
    assert!(state.guard.is_none());
    assert!(state.evidence.is_none());
    assert!(state.observation.is_none());
    assert!(state.renderer_epoch.is_none());
    assert!(state.recovery.is_none());
    state.reset_for_dispatch();
    assert_eq!(state.sequence, identity.sequence);
}

#[test]
fn recovery_only_follows_a_previously_ready_renderer_loss() {
    let (_, mut state, _) = fixture();
    let identity = state.guard.as_ref().unwrap().identity();
    let loss = QualityApplicationState::Failed(QualityApplicationFailure::Invalidated);
    assert_eq!(
        recovery_after(&state, loss, identity, true, [true; 7]),
        None
    );
    state.evidence = Some(QualityApplicationEvidence {
        status: "plan_application_ready",
        identity: Some(identity),
        projected_domains: [true; 7],
        gpu_ready: true,
        error: None,
    });
    assert_eq!(
        recovery_after(&state, loss, identity, true, [true; 7]),
        Some(identity)
    );
    for failure in [
        QualityApplicationFailure::TimedOut,
        QualityApplicationFailure::PipelineRejected,
        QualityApplicationFailure::AmbiguousCamera,
        QualityApplicationFailure::ClockRegressed,
    ] {
        assert_eq!(
            recovery_after(
                &state,
                QualityApplicationState::Failed(failure),
                identity,
                true,
                [true; 7]
            ),
            None
        );
    }
    assert_eq!(
        recovery_after(&state, loss, identity, false, [true; 7]),
        None
    );
    assert_eq!(
        recovery_after(&state, loss, identity, true, [false; 7]),
        None
    );
    let mut other = identity;
    other.sequence += 1;
    assert_eq!(recovery_after(&state, loss, other, true, [true; 7]), None);
}

#[test]
fn waiting_recovery_is_cancelled_by_manual_override_or_domain_mismatch() {
    let (mut world, mut state, _) = fixture();
    let identity = state.guard.as_ref().unwrap().identity();
    let loss = QualityApplicationState::Failed(QualityApplicationFailure::Invalidated);
    state.recovery = Some(identity);
    assert_eq!(
        recovery_after(
            &state,
            loss,
            identity,
            installed(&world, identity),
            [true; 7]
        ),
        Some(identity)
    );
    assert!(ready_epoch(&mut world, identity, None).is_none());
    assert_eq!(state.sequence, 1);
    world
        .resource_mut::<WaterQualityRequest>()
        .request(VisualQualityTier::Low);
    assert!(!installed(&world, identity));
    assert_eq!(
        recovery_after(
            &state,
            loss,
            identity,
            installed(&world, identity),
            [true; 7]
        ),
        None
    );
    let mut projected = [true; 7];
    projected[4] = false;
    assert_eq!(
        recovery_after(&state, loss, identity, true, projected),
        None
    );
    assert_eq!(state.sequence, 1);
}

#[test]
fn missing_gpu_times_out_without_auto_retry_or_claimed_rollback() {
    let (mut world, mut state, candidate) = fixture();
    for (now, expected) in [
        (1, "plan_application_pending_gpu"),
        (10, "plan_application_failed"),
        (20, "plan_application_failed"),
    ] {
        advance(
            &mut world,
            VisualQualityTier::High,
            Duration::from_secs(now),
            [QualityDomainProjection::CpuProjected; 7],
            candidate.plan_hash,
            &mut state,
        );
        let evidence = state.evidence.as_ref().unwrap();
        assert_eq!(evidence.status, expected);
        assert!(!evidence.gpu_ready);
        assert!(!evidence.projected_domains[3]);
        assert!(!evidence.projected_domains[4]);
        assert_eq!(state.sequence, 1);
        assert!(
            world
                .resource::<WaterQualityRequest>()
                .effective_identity()
                .is_some()
        );
    }
    assert_eq!(state.evidence.unwrap().error.as_deref(), Some("TimedOut"));
}

#[test]
fn invalidation_clears_overrides_without_overwriting_manual_tier_or_preview() {
    let (mut world, state, mut candidate) = fixture();
    world.insert_resource(state);
    world
        .resource_mut::<WaterQualityRequest>()
        .preview_reflections_disabled(true);
    world
        .resource_mut::<WaterQualityRequest>()
        .request(VisualQualityTier::Low);
    candidate
        .plan
        .as_mut()
        .unwrap()
        .blockers
        .push(forge_project::effective_quality::QualityCapability::Sky);
    let result = update(
        &mut world,
        VisualQualityTier::High,
        true,
        [QualityDomainProjection::CpuProjected; 7],
        Some(&candidate),
    )
    .unwrap();
    assert_eq!(result.status, "plan_application_invalidated");
    assert!(
        world
            .resource::<EnvironmentQualityRequest>()
            .effective_identity()
            .is_none()
    );
    let request = world.resource::<WaterQualityRequest>();
    assert!(request.effective_identity().is_none());
    // A High guard must still reject the user's Low request after invalidation.
    let guard = crate::global_quality::test_plan::guard(
        &forge_project::VisualValleyQualityProfileDocument::from_json(include_str!(
            "../../../../../examples/visual_valley/quality-profile.v1.json"
        ))
        .unwrap(),
        VisualQualityTier::High,
    );
    assert!(
        !world
            .resource_mut::<WaterQualityRequest>()
            .request_effective(
                &guard,
                Duration::ZERO,
                guard.identity(),
                guard.plan().source_bindings.unwrap()
            )
    );
}

#[test]
fn camera_ambiguity_and_manual_reset_invalidate_without_gpu_success() {
    for manual_reset in [true, false] {
        let (mut world, mut state, candidate) = fixture();
        if manual_reset {
            world
                .resource_mut::<WaterQualityRequest>()
                .request(VisualQualityTier::High);
        } else {
            world.spawn((Camera3d::default(), Camera::default()));
        }
        advance(
            &mut world,
            VisualQualityTier::High,
            Duration::from_secs(1),
            [QualityDomainProjection::CpuProjected; 7],
            candidate.plan_hash,
            &mut state,
        );
        let evidence = state.evidence.unwrap();
        assert_eq!(evidence.status, "plan_application_failed");
        assert_eq!(
            evidence.error.as_deref(),
            Some(if manual_reset {
                "Invalidated"
            } else {
                "AmbiguousCamera"
            })
        );
        assert!(!evidence.gpu_ready);
    }
}
