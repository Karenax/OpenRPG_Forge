//! Exact queued retries share the normal physical fixture and runtime queue.
use super::*;

#[test]
fn local_faction_retry_does_not_promote_the_same_mutating_events_twice() {
    let (mut app, document, _, [victim, visible, _]) = faction_attack_app();
    let request = GameRequest {
        command_id: CommandId::deterministic("faction/exact-queued-retry"),
        expected_sequence: app
            .world()
            .resource::<GameplayRuntime>()
            .session()
            .state()
            .sequence,
        actor: document.player,
        action: GameAction::Attack { target: victim },
    };
    {
        let mut runtime = app.world_mut().resource_mut::<GameplayRuntime>();
        runtime.queue(request.clone()).unwrap();
        runtime.queue(request).unwrap();
    }
    for _ in 0..8 {
        app.update();
        if !app
            .world()
            .resource::<GameplayRuntime>()
            .has_queued_actions()
        {
            break;
        }
    }
    let runtime = app.world().resource::<GameplayRuntime>();
    assert!(!runtime.has_queued_actions());
    assert_eq!(runtime.session().state().sequence, 1);
    assert_eq!(runtime.session().state().actors[&victim].vitality, 80);
    assert_eq!(
        runtime.session().state().actors[&document.player].stamina,
        95
    );
    assert_eq!(
        runtime
            .session()
            .personal_relation(visible, document.player),
        Some(-10)
    );
    assert_eq!(
        runtime
            .last_events()
            .iter()
            .filter(|event| matches!(event,
        GameplayEvent::Damage {target,..} if *target==victim))
            .count(),
        1,
        "an idempotent domain receipt must not replay physical/render consequences"
    );
    assert_eq!(
        runtime
            .last_events()
            .iter()
            .filter(|event| matches!(event,
        GameplayEvent::FactionReaction {change} if change.observer==visible))
            .count(),
        1
    );
}
