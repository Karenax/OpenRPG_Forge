use super::*;
use forge_gameplay::{
    AbilityDefinition, AbilityEffect, AbilityTarget, ActorObservation, Condition, EffectStacking,
    PhysicsFrame, QuestConsumption, SimulationDefinition, TimedEffectDefinition,
};
use std::collections::BTreeMap;

fn configured(host: &mut AuthoringHost, fixture: &Fixture) -> (GameplayDocument, ContentHash) {
    let document = fixture
        .gameplay
        .with_simulation_definition(Some(SimulationDefinition {
            effects: vec![TimedEffectDefinition {
                id: id("host/ward"),
                name: "Ward".into(),
                duration_ms: 1000,
                attack_protection_bonus: 10,
                stacking: EffectStacking::Reject,
                exclusive_with: vec![],
            }],
            abilities: vec![AbilityDefinition {
                id: id("host/protect"),
                name: "Protect".into(),
                target: AbilityTarget::SelfOnly,
                range_m: 0.0,
                condition: Condition::Always,
                stamina_cost: 7,
                item_costs: vec![QuestConsumption {
                    item: fixture.ids.item,
                    quantity: 1,
                }],
                effects: vec![AbilityEffect::Timed {
                    effect: id("host/ward"),
                }],
            }],
            actor_abilities: BTreeMap::from([(fixture.ids.player, vec![id("host/protect")])]),
            ..SimulationDefinition::default()
        }))
        .unwrap();
    let head = host.execute(Request::Inspect).unwrap().zone_head.unwrap();
    let request = head
        .prepare_gameplay(
            Some(document.clone()),
            CommandId::deterministic("host/temporal-definition"),
        )
        .unwrap();
    let state = host.execute(Request::ApplyZone { request }).unwrap();
    let source = state.zone_head.unwrap().content_hash().unwrap();
    host.execute(Request::Save).unwrap();
    (document, source)
}

pub(super) fn execute(game: &mut GameSession, action: GameAction) -> forge_gameplay::GameReceipt {
    let sequence = game.state().sequence;
    game.execute(
        &GameRequest {
            command_id: CommandId::deterministic(format!("host/temporal/{sequence}")),
            expected_sequence: sequence,
            actor: game.document().player,
            action,
        },
        ActionObservation {
            line_of_sight: true,
            confirmed_contact: true,
            water_contact: false,
        },
    )
    .unwrap()
}
pub(super) fn step(
    game: &mut GameSession,
    nanoseconds: u64,
) -> forge_gameplay::PhysicsFrameReceipt {
    let frame = PhysicsFrame {
        simulation_step_ns: Some(nanoseconds),
        source_hash: game.source_hash(),
        tick: game.state().tick + 1,
        actors: game
            .state()
            .actors
            .iter()
            .map(|(actor, state)| ActorObservation {
                actor: *actor,
                position: state.position,
            })
            .collect(),
        objects: vec![],
        contacts: vec![],
        heat: vec![],
        danger: vec![],
    };
    game.advance_frame(&frame).unwrap()
}

#[test]
fn temporal_v5_save_close_open_load_preserves_costs_and_effect_expiry_without_source_writes() {
    let (root, mut host, fixture) = setup("temporal-roundtrip");
    let (document, source) = configured(&mut host, &fixture);
    let authored = fs::read(authoring_path(&root, fixture.area_id)).unwrap();
    let mut game = GameSession::new(document.clone(), source).unwrap();
    execute(
        &mut game,
        GameAction::Pickup {
            object: fixture.ids.pickup,
        },
    );
    let receipt = execute(
        &mut game,
        GameAction::UseAbility {
            ability: id("host/protect"),
            target: fixture.ids.player,
        },
    );
    assert_eq!(game.state().actors[&fixture.ids.player].stamina, 93);
    assert!(
        game.state().actors[&fixture.ids.player]
            .inventory
            .is_empty()
    );
    let save_json = String::from_utf8(game.save_bytes().unwrap()).unwrap();
    assert_eq!(
        serde_json::from_str::<serde_json::Value>(&save_json).unwrap()["version"],
        5
    );
    host.execute(Request::StartPlaytest {
        expected_hash: source,
    })
    .unwrap();
    let stored = host
        .execute_game_save(GameSaveRequest::Save {
            expected_source: source,
            expected_stored: None,
            save_json,
        })
        .unwrap()
        .saved
        .unwrap();
    host.execute(Request::StopPlaytest { diagnostics: None })
        .unwrap();
    host.execute(Request::Close).unwrap();
    drop(host);
    let mut reopened = AuthoringHost::new(&root).unwrap();
    let state = reopened
        .execute(Request::Open {
            area_id: fixture.area_id,
        })
        .unwrap();
    assert_eq!(
        state.zone_head.unwrap().corrections.gameplay.as_deref(),
        Some(&document)
    );
    let loaded = reopened
        .execute_game_save(GameSaveRequest::Load {
            expected_source: source,
            allow_compatible_edits: false,
        })
        .unwrap()
        .saved
        .unwrap();
    assert_eq!(loaded.stored_hash, stored.stored_hash);
    let mut resumed =
        GameSession::resume(document, source, loaded.save_json.as_bytes(), false).unwrap();
    assert_eq!(resumed.state(), game.state());
    assert_costs_and_expiry_after_resume(&mut resumed, &receipt, fixture.ids.player);
    assert_eq!(
        fs::read(authoring_path(&root, fixture.area_id)).unwrap(),
        authored
    );
    reopened.execute(Request::Close).unwrap();
    drop(reopened);
    cleanup(root);
}

fn assert_costs_and_expiry_after_resume(
    resumed: &mut GameSession,
    receipt: &forge_gameplay::GameReceipt,
    player: EntityId,
) {
    step(resumed, 50_000_000);
    assert_eq!(
        resumed.state().simulation.as_ref().unwrap().effects.len(),
        1
    );
    step(resumed, 950_000_000);
    assert!(
        resumed
            .state()
            .simulation
            .as_ref()
            .unwrap()
            .effects
            .is_empty()
    );
    assert_eq!(
        resumed
            .execute(&receipt.request, ActionObservation::default())
            .unwrap(),
        *receipt
    );
    assert_eq!(resumed.state().actors[&player].stamina, 93);
    assert!(resumed.state().actors[&player].inventory.is_empty());
}
