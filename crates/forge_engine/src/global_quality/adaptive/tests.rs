use super::*;
use crate::global_quality::test_plan::guard_with_sequence;

fn source() -> VisualValleyQualityProfileDocument {
    VisualValleyQualityProfileDocument::from_json(include_str!(
        "../../../../../examples/visual_valley/quality-profile.v1.json"
    ))
    .unwrap()
}

#[test]
fn occluded_window_never_accumulates_pressure_and_recovers_without_stale_samples() {
    let mut world = World::new();
    world.init_resource::<Messages<WindowOccluded>>();
    let window = world
        .spawn((
            PrimaryWindow,
            Window {
                focused: true,
                ..default()
            },
        ))
        .id();
    assert!(has_admissible_window(&mut world));
    world
        .resource_mut::<Messages<WindowOccluded>>()
        .write(WindowOccluded {
            window,
            occluded: true,
        });
    assert!(!has_admissible_window(&mut world));
    assert_eq!(world.resource::<WindowVisibility>().status, "occluded");
    world.resource_mut::<Messages<WindowOccluded>>().clear();
    let document = source();
    let guard = guard_with_sequence(&document, VisualQualityTier::High, 1);
    let mut state = AdaptiveState::default();
    for _ in 0..1000 {
        let sample = has_admissible_window(&mut world).then_some(1000.);
        assert!(
            state
                .advance(
                    &document,
                    Some((&guard, &complete(&guard))),
                    Duration::from_secs(1),
                    sample
                )
                .is_none()
        );
    }
    assert_eq!(state.stats.status, "waiting_focus");
    assert_eq!(state.stats.eligible_samples, 0);
    assert_eq!(state.stats.proposals, 0);
    world
        .resource_mut::<Messages<WindowOccluded>>()
        .write(WindowOccluded {
            window,
            occluded: false,
        });
    let sample = has_admissible_window(&mut world).then_some(30.);
    assert!(sample.is_some());
    assert!(
        state
            .advance(
                &document,
                Some((&guard, &complete(&guard))),
                Duration::from_secs(2),
                sample
            )
            .is_none()
    );
    assert_eq!(state.stats.eligible_samples, 1);
    world.get_mut::<Window>(window).unwrap().visible = false;
    assert!(!has_admissible_window(&mut world));
    world
        .resource_mut::<Messages<WindowOccluded>>()
        .write(WindowOccluded {
            window,
            occluded: true,
        });
    has_admissible_window(&mut world);
    world.despawn(window);
    assert!(!has_admissible_window(&mut world));
    assert!(world.resource::<WindowVisibility>().occluded.is_empty());
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
fn adaptive_transition_waits_for_acknowledgement_and_preserves_sources() {
    let document = source();
    let hash = document.content_hash().unwrap();
    let high = guard_with_sequence(&document, VisualQualityTier::High, 19);
    let mut state = AdaptiveState::default();
    for _ in
        0..document.adaptation.cooldown_frames + document.adaptation.downgrade_sustain_frames - 1
    {
        assert!(
            state
                .advance(
                    &document,
                    Some((&high, &complete(&high))),
                    Duration::from_secs(1),
                    Some(30.0)
                )
                .is_none()
        );
    }
    assert_eq!(
        state.advance(
            &document,
            Some((&high, &complete(&high))),
            Duration::from_secs(1),
            Some(30.0)
        ),
        Some(VisualQualityTier::Medium)
    );
    assert_eq!(state.stats.proposals, 1);
    assert_eq!(state.stats.applied_tier, Some(VisualQualityTier::High));
    // The coordinator may have consumed intermediate application sequences.
    let medium = guard_with_sequence(&document, VisualQualityTier::Medium, 23);
    let mut pending = complete(&medium);
    pending.gpu_ready = None;
    for _ in 0..1000 {
        assert!(
            state
                .advance(
                    &document,
                    Some((&medium, &pending)),
                    Duration::from_secs(2),
                    Some(30.0)
                )
                .is_none()
        );
    }
    assert!(state.stats.acknowledged.is_none());
    state.advance(
        &document,
        Some((&medium, &complete(&medium))),
        Duration::from_secs(3),
        Some(30.0),
    );
    assert_eq!(state.stats.acknowledged, Some(medium.identity()));
    assert_eq!(state.stats.applied_tier, Some(VisualQualityTier::Medium));
    assert_eq!(document.content_hash().unwrap(), hash);
}

#[test]
fn missing_gpu_and_focus_never_accumulate_pressure() {
    let document = source();
    let high = guard_with_sequence(&document, VisualQualityTier::High, 1);
    let mut state = AdaptiveState::default();
    let mut pending = complete(&high);
    pending.gpu_ready = None;
    for _ in 0..1000 {
        state.advance(
            &document,
            Some((&high, &pending)),
            Duration::from_secs(1),
            Some(50.0),
        );
    }
    assert_eq!(state.stats.eligible_samples, 0);
    state.advance(
        &document,
        Some((&high, &complete(&high))),
        Duration::from_secs(1),
        None,
    );
    for _ in
        0..document.adaptation.cooldown_frames + document.adaptation.downgrade_sustain_frames - 1
    {
        state.advance(
            &document,
            Some((&high, &complete(&high))),
            Duration::from_secs(2),
            Some(50.0),
        );
    }
    state.advance(
        &document,
        Some((&high, &complete(&high))),
        Duration::from_secs(2),
        None,
    );
    assert!(
        state
            .advance(
                &document,
                Some((&high, &complete(&high))),
                Duration::from_secs(2),
                Some(50.0)
            )
            .is_none()
    );
    assert_eq!(state.stats.proposals, 0);
}

#[test]
fn lost_evidence_stops_without_overriding_manual_settings() {
    let document = source();
    let high = guard_with_sequence(&document, VisualQualityTier::High, 1);
    let mut state = AdaptiveState::default();
    state.advance(
        &document,
        Some((&high, &complete(&high))),
        Duration::from_secs(1),
        Some(30.0),
    );
    let mut lost = complete(&high);
    lost.projected_domains[0] = None;
    assert!(
        state
            .advance(
                &document,
                Some((&high, &lost)),
                Duration::from_secs(2),
                Some(30.0)
            )
            .is_none()
    );
    assert_eq!(state.stats.status, "stopped");
    for _ in 0..1000 {
        assert!(
            state
                .advance(
                    &document,
                    Some((&high, &complete(&high))),
                    Duration::from_secs(3),
                    Some(30.0)
                )
                .is_none()
        );
    }
}

#[test]
fn global_manual_request_disables_adaptation_even_for_same_tier() {
    let mut app = App::new();
    app.add_plugins(crate::GlobalQualityPlugin(VisualQualityTier::High));
    let world = app.world_mut();
    world
        .resource_mut::<GlobalQualityRequest>()
        .enable_adaptation();
    world
        .resource_mut::<GlobalQualityRequest>()
        .request(VisualQualityTier::High);
    update(world);
    assert!(!world.resource::<GlobalQualityRequest>().adaptive);
    assert_eq!(
        world.resource::<GlobalQualityStats>().adaptation.status,
        "disabled"
    );
}

#[test]
fn engine_itself_refuses_adaptation_during_fixed_metrics() {
    let mut app = App::new();
    app.add_plugins(crate::GlobalQualityPlugin(VisualQualityTier::High));
    app.insert_resource(crate::RuntimeAutomation {
        metrics_path: Some("never-written.json".into()),
        ..Default::default()
    });
    let world = app.world_mut();
    world
        .resource_mut::<GlobalQualityRequest>()
        .enable_adaptation();
    update(world);
    assert_eq!(
        world.resource::<GlobalQualityStats>().adaptation.status,
        "stopped"
    );
    assert_eq!(
        world.resource::<GlobalQualityRequest>().tier,
        VisualQualityTier::High
    );
}

#[test]
fn missing_transition_snapshot_times_out_without_retry() {
    let document = source();
    let mut state = AdaptiveState {
        pending_since: Some(Duration::from_secs(1)),
        ..Default::default()
    };
    assert!(
        state
            .advance(&document, None, Duration::from_secs(31), Some(30.0))
            .is_none()
    );
    assert_eq!(state.stats.status, "stopped");
}

#[cfg(feature = "qualification")]
#[test]
fn fixed_proof_view_refuses_adaptation_before_and_after_camera_installation() {
    for installed in [false, true] {
        let mut app = App::new();
        app.add_plugins(crate::GlobalQualityPlugin(VisualQualityTier::High));
        app.insert_resource(crate::FixedProofViewStatus {
            camera_id: "human_bridge".into(),
            installed,
            error: None,
            stable_main_frames: 0,
            current_view_matches: false,
            virtual_elapsed_ns: 0,
            fixed_elapsed_ns: 0,
        });
        let world = app.world_mut();
        world
            .resource_mut::<GlobalQualityRequest>()
            .enable_adaptation();
        update(world);
        let stats = &world.resource::<GlobalQualityStats>().adaptation;
        assert_eq!(stats.status, "stopped");
        assert!(
            stats
                .error
                .as_deref()
                .is_some_and(|message| message.contains("fixed"))
        );
        assert_eq!(stats.proposals, 0);
        assert!(stats.acknowledged.is_none());
        assert_eq!(
            world.resource::<GlobalQualityRequest>().tier,
            VisualQualityTier::High
        );
        world
            .resource_mut::<GlobalQualityRequest>()
            .request(VisualQualityTier::High);
        update(world);
        assert_eq!(
            world.resource::<GlobalQualityStats>().adaptation.status,
            "disabled"
        );
    }
}

#[test]
fn explicit_reenable_restarts_observation_without_reusing_a_stopped_controller() {
    let mut app = App::new();
    app.add_plugins(crate::GlobalQualityPlugin(VisualQualityTier::High));
    let terrain = forge_world::embedded_visual_valley_terrain_document().unwrap();
    app.add_plugins(
        crate::VisualValleyTerrainQualityPlugin::new(&terrain, source(), VisualQualityTier::High)
            .unwrap(),
    );
    let world = app.world_mut();
    world.resource_mut::<AdaptiveState>().stop("previous fault");
    world
        .resource_mut::<GlobalQualityRequest>()
        .request(VisualQualityTier::High);
    world
        .resource_mut::<GlobalQualityRequest>()
        .enable_adaptation();
    update(world);
    let stats = &world.resource::<GlobalQualityStats>().adaptation;
    assert_eq!(stats.status, "waiting_application");
    assert!(stats.acknowledged.is_none());
    assert!(stats.error.is_none());
}
