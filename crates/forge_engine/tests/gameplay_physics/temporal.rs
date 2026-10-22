use super::*;
use forge_gameplay::{
    AbilityDefinition, AbilityEffect, AbilityTarget, CalendarEventDefinition, Condition,
    EffectStacking, EncounterDefinition, EncounterRearm, SimulationDefinition,
    TimedEffectDefinition,
};
use std::collections::BTreeMap;

fn document(encounter: bool) -> (GameplayDocument, EntityId) {
    let mut source = player_save_fixture();
    let participant = id("temporal/participant");
    source.actors.push(actor(
        participant,
        "Participant",
        ActorRole::Enemy,
        CoreVec3::new(0.75, 0.95, -6.0),
    ));
    let mut simulation = SimulationDefinition {
        effects: vec![TimedEffectDefinition {
            id: id("temporal/ward"),
            name: "Ward".into(),
            duration_ms: 1000,
            attack_protection_bonus: 10,
            stacking: EffectStacking::Reject,
            exclusive_with: vec![],
        }],
        abilities: vec![AbilityDefinition {
            id: id("temporal/protect"),
            name: "Protect".into(),
            target: AbilityTarget::AnyLivingActor,
            range_m: 2.0,
            condition: Condition::Always,
            stamina_cost: 5,
            item_costs: vec![],
            effects: vec![AbilityEffect::Timed {
                effect: id("temporal/ward"),
            }],
        }],
        actor_abilities: BTreeMap::from([(source.player, vec![id("temporal/protect")])]),
        ..SimulationDefinition::default()
    };
    if encounter {
        simulation.clock.milliseconds_per_minute = 1;
        simulation.events.push(CalendarEventDefinition {
            id: id("temporal/arrival"),
            name: "Arrival".into(),
            first_minute: 5000,
            repeat_every_minutes: None,
        });
        simulation.encounters.push(EncounterDefinition {
            id: id("temporal/encounter"),
            name: "Encounter".into(),
            event: id("temporal/arrival"),
            participants: vec![participant],
            condition: Condition::Always,
            rearm: EncounterRearm::Once,
        });
    }
    (
        source.with_simulation_definition(Some(simulation)).unwrap(),
        participant,
    )
}

fn request(app: &mut App, action: GameAction, name: &str) {
    let mut runtime = app.world_mut().resource_mut::<GameplayRuntime>();
    let expected_sequence = runtime.session().state().sequence;
    let actor = runtime.session().document().player;
    runtime
        .queue(GameRequest {
            command_id: CommandId::deterministic(name),
            expected_sequence,
            actor,
            action,
        })
        .unwrap();
    for _ in 0..8 {
        app.update();
        if !app
            .world()
            .resource::<GameplayRuntime>()
            .has_queued_actions()
        {
            return;
        }
    }
    panic!("physical action queue did not finish");
}

#[test]
fn temporal_ability_and_expiration_use_real_fixed_steps_and_survive_physical_resume() {
    let (source, target) = document(false);
    let hash = ContentHash::from_bytes([0x88; 32]);
    let mut live = app(source.clone(), hash);
    wait_for_player_saveable(&mut live);
    wait_for_body(&mut live, target, RigidBody::KinematicPositionBased);
    request(
        &mut live,
        GameAction::UseAbility {
            ability: id("temporal/protect"),
            target,
        },
        "temporal/physical-protect",
    );
    assert_eq!(
        live.world()
            .resource::<GameplayRuntime>()
            .session()
            .state()
            .sequence,
        1
    );
    queue_stationary_attack(&mut live, target, "temporal/protected-attack");
    let runtime = live.world().resource::<GameplayRuntime>();
    assert_eq!(runtime.session().state().actors[&target].vitality, 90);
    let expiry = runtime
        .session()
        .state()
        .simulation
        .as_ref()
        .unwrap()
        .effects
        .values()
        .next()
        .unwrap()
        .expires_at_ns;
    let save = runtime.save_bytes().unwrap();
    let mut resumed = app_with_save(source.clone(), hash, Some(&save));
    wait_for_player_saveable(&mut resumed);
    for _ in 0..90 {
        resumed.update();
        let simulation = resumed
            .world()
            .resource::<GameplayRuntime>()
            .session()
            .state()
            .simulation
            .as_ref()
            .unwrap();
        if simulation.elapsed_ns >= expiry && simulation.effects.is_empty() {
            break;
        }
    }
    let runtime = resumed.world().resource::<GameplayRuntime>();
    assert!(runtime.error().is_none(), "{:?}", runtime.error());
    assert!(
        runtime
            .session()
            .state()
            .simulation
            .as_ref()
            .unwrap()
            .effects
            .is_empty()
    );
    queue_stationary_attack(&mut resumed, target, "temporal/after-expiry");
    let runtime = resumed.world().resource::<GameplayRuntime>();
    assert_eq!(
        runtime.session().state().sequence,
        3,
        "{}",
        runtime.feedback()
    );
    assert_eq!(runtime.session().state().actors[&target].vitality, 70);
    assert_eq!(runtime.session().state().actors[&source.player].stamina, 85);
    assert_eq!(runtime.session().document(), &source);
}

#[test]
fn temporal_encounter_enables_one_existing_body_then_resumes_without_duplicate() {
    let (source, participant) = document(true);
    let hash = ContentHash::from_bytes([0x89; 32]);
    let mut live = app(source.clone(), hash);
    wait_for_player_saveable(&mut live);
    let entity = entity_for(&mut live, participant);
    assert!(
        !live
            .world()
            .resource::<GameplayRuntime>()
            .session()
            .state()
            .actors[&participant]
            .active
    );
    assert!(live.world().get::<ColliderDisabled>(entity).is_some());
    assert_eq!(
        live.world().get::<Visibility>(entity),
        Some(&Visibility::Hidden)
    );
    for _ in 0..360 {
        live.update();
        if live
            .world()
            .resource::<GameplayRuntime>()
            .session()
            .state()
            .actors[&participant]
            .active
            && live.world().get::<ColliderDisabled>(entity).is_none()
        {
            break;
        }
    }
    for _ in 0..2 {
        live.update();
    }
    let runtime = live.world().resource::<GameplayRuntime>();
    assert!(runtime.error().is_none(), "{:?}", runtime.error());
    assert!(runtime.session().state().actors[&participant].active);
    assert_eq!(
        runtime
            .session()
            .state()
            .simulation
            .as_ref()
            .unwrap()
            .activations
            .len(),
        1
    );
    assert_eq!(
        live.world().get::<Visibility>(entity),
        Some(&Visibility::Visible)
    );
    assert!(live.world().get::<ColliderDisabled>(entity).is_none());
    queue_stationary_attack(&mut live, participant, "temporal/activated-contact");
    let runtime = live.world().resource::<GameplayRuntime>();
    assert_eq!(
        runtime.session().state().actors[&participant].vitality,
        80,
        "{}",
        runtime.feedback()
    );
    let save = runtime.save_bytes().unwrap();
    assert_eq!(entity_for(&mut live, participant), entity);
    let mut resumed = app_with_save(source.clone(), hash, Some(&save));
    wait_for_player_saveable(&mut resumed);
    wait_for_body(&mut resumed, participant, RigidBody::KinematicPositionBased);
    for _ in 0..120 {
        resumed.update();
    }
    let runtime = resumed.world().resource::<GameplayRuntime>();
    assert!(runtime.error().is_none(), "{:?}", runtime.error());
    assert_eq!(
        runtime
            .session()
            .state()
            .simulation
            .as_ref()
            .unwrap()
            .activations
            .len(),
        1
    );
    assert_eq!(runtime.session().document(), &source);
    assert_eq!(runtime.session().state().actors[&participant].vitality, 80);
    let count = resumed
        .world_mut()
        .query::<&DomainEntityId>()
        .iter(resumed.world())
        .filter(|id| id.0 == participant)
        .count();
    assert_eq!(count, 1);
}
