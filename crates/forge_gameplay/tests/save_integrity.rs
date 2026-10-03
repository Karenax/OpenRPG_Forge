use forge_core::{AreaId, CommandId, ContentHash, EntityId, ModuleId, Vec3, canonical_json_sha256};
use forge_gameplay::{
    ActionObservation, ActorDefinition, ActorRole, ContactObservation, ContactReceipt,
    GAMEPLAY_SCHEMA, GAMEPLAY_VERSION, GameAction, GameRequest, GameSession, GameState,
    GameplayDocument, GameplayEvent, ItemDefinition, ObjectDefinition, ObjectKind, ObjectPhysics,
    RulesProfile,
};
use serde_json::Value;

#[derive(Clone, Copy)]
struct FixtureIds {
    player: EntityId,
    item: EntityId,
    door: EntityId,
    prop: EntityId,
    pickup: EntityId,
}

fn fixture() -> (GameplayDocument, FixtureIds) {
    let ids = FixtureIds {
        player: EntityId::deterministic("save-integrity/player"),
        item: EntityId::deterministic("save-integrity/item"),
        door: EntityId::deterministic("save-integrity/door"),
        prop: EntityId::deterministic("save-integrity/prop"),
        pickup: EntityId::deterministic("save-integrity/pickup"),
    };

    let document = GameplayDocument {
        schema: GAMEPLAY_SCHEMA.into(),
        version: GAMEPLAY_VERSION,
        module_id: ModuleId::deterministic("save-integrity/module"),
        area_id: AreaId::deterministic("save-integrity/area"),
        profile: RulesProfile {
            id: "forge.gate3-minimal".into(),
            version: 1,
            interaction_range_m: 3.0,
            attack_range_m: 2.0,
            attack_damage: 20,
            attack_stamina_cost: 5,
            max_stamina: 100,
            max_action_receipts: 32,
            max_events_per_tick: 32,
        },
        player: ids.player,
        actors: vec![ActorDefinition {
            id: ids.player,
            name: "Player".into(),
            role: ActorRole::Player,
            position: Vec3::ZERO,
            max_vitality: 100,
            protection: 0,
            inventory_capacity: 4,
            dialogue: None,
            patrol: vec![],
            flees_danger: false,
        }],
        items: vec![ItemDefinition {
            id: ids.item,
            name: "Token".into(),
            max_stack: 4,
        }],
        objects: vec![
            ObjectDefinition {
                id: ids.door,
                name: "Door".into(),
                position: Vec3::new(1.0, 0.0, 0.0),
                kind: ObjectKind::Door {
                    initially_open: false,
                },
                physics: ObjectPhysics::default(),
            },
            ObjectDefinition {
                id: ids.prop,
                name: "Solid prop".into(),
                position: Vec3::new(1.0, 0.0, 1.0),
                kind: ObjectKind::Prop,
                physics: ObjectPhysics {
                    mass_kg: 1.0,
                    pushable: true,
                    ..ObjectPhysics::default()
                },
            },
            ObjectDefinition {
                id: ids.pickup,
                name: "Token pickup".into(),
                position: Vec3::new(1.0, 0.0, 0.0),
                kind: ObjectKind::Pickup {
                    item: ids.item,
                    quantity: 1,
                },
                physics: ObjectPhysics::default(),
            },
        ],
        places: vec![],
        dialogues: vec![],
        quests: vec![],
    };

    (document, ids)
}

fn source_hash() -> ContentHash {
    ContentHash::from_bytes([7; 32])
}

fn fresh_save() -> (GameplayDocument, FixtureIds, ContentHash, Vec<u8>) {
    let (document, ids) = fixture();
    let source_hash = source_hash();
    let session = GameSession::new(document.clone(), source_hash).unwrap();
    let bytes = session.save_bytes().unwrap();
    (document, ids, source_hash, bytes)
}

fn save_with_one_receipt() -> (GameplayDocument, FixtureIds, ContentHash, Vec<u8>) {
    let (document, ids) = fixture();
    let source_hash = source_hash();
    let mut session = GameSession::new(document.clone(), source_hash).unwrap();
    let request = GameRequest {
        command_id: CommandId::deterministic("save-integrity/open-door"),
        expected_sequence: 0,
        actor: ids.player,
        action: GameAction::SetDoor {
            object: ids.door,
            open: true,
        },
    };
    session
        .execute(
            &request,
            ActionObservation {
                line_of_sight: true,
                confirmed_contact: false,
                water_contact: false,
            },
        )
        .unwrap();
    let bytes = session.save_bytes().unwrap();
    (document, ids, source_hash, bytes)
}

fn rewrite_state<F>(bytes: &[u8], mutate: F) -> Vec<u8>
where
    F: FnOnce(&mut GameState),
{
    let mut envelope: Value = serde_json::from_slice(bytes).unwrap();
    let state_value = envelope
        .get("state")
        .cloned()
        .expect("save envelope must contain state");
    let mut state: GameState = serde_json::from_value(state_value).unwrap();
    mutate(&mut state);

    let state_hash = canonical_json_sha256(&state).unwrap();
    envelope["state"] = serde_json::to_value(&state).unwrap();
    envelope["state_hash"] = serde_json::to_value(state_hash).unwrap();
    serde_json::to_vec(&envelope).unwrap()
}

fn assert_resume_rejects(document: &GameplayDocument, source_hash: ContentHash, bytes: &[u8]) {
    assert!(
        GameSession::resume(document.clone(), source_hash, bytes, false).is_err(),
        "tampered state must be rejected even with a recomputed state_hash"
    );
}

#[test]
fn nonzero_sequence_without_receipts_is_rejected_after_rehashed_tamper() {
    let (document, _ids, source_hash, bytes) = fresh_save();
    let bytes = rewrite_state(&bytes, |state| state.sequence = 1);
    assert_resume_rejects(&document, source_hash, &bytes);
}

#[test]
fn zero_tick_event_count_must_match_its_retained_actions() {
    let (document, _ids, source_hash, bytes) = save_with_one_receipt();
    let bytes = rewrite_state(&bytes, |state| state.tick_events_used = 0);
    assert_resume_rejects(&document, source_hash, &bytes);
}

#[test]
fn receipt_sequence_gap_is_rejected_after_rehashed_tamper() {
    let (document, _ids, source_hash, bytes) = save_with_one_receipt();
    let bytes = rewrite_state(&bytes, |state| {
        let receipt = state.receipts.first_mut().unwrap();
        receipt.sequence = 2;
        receipt.request.expected_sequence = 1;
        state.sequence = 2;
    });
    assert_resume_rejects(&document, source_hash, &bytes);
}

#[test]
fn receipt_expected_sequence_gap_is_rejected_after_rehashed_tamper() {
    let (document, _ids, source_hash, bytes) = save_with_one_receipt();
    let bytes = rewrite_state(&bytes, |state| {
        state
            .receipts
            .first_mut()
            .unwrap()
            .request
            .expected_sequence = 7;
    });
    assert_resume_rejects(&document, source_hash, &bytes);
}

#[test]
fn destroyed_flag_on_a_non_destructible_object_is_rejected() {
    let (document, ids, source_hash, bytes) = fresh_save();
    let bytes = rewrite_state(&bytes, |state| {
        state.objects.get_mut(&ids.prop).unwrap().destroyed = true;
    });
    assert_resume_rejects(&document, source_hash, &bytes);
}

#[test]
fn released_flag_without_throwable_or_supporting_physics_is_rejected() {
    let (document, ids, source_hash, bytes) = fresh_save();
    let bytes = rewrite_state(&bytes, |state| {
        state.objects.get_mut(&ids.prop).unwrap().released = true;
    });
    assert_resume_rejects(&document, source_hash, &bytes);
}

#[test]
fn fleeing_flag_on_an_actor_that_does_not_flee_is_rejected() {
    let (document, ids, source_hash, bytes) = fresh_save();
    let bytes = rewrite_state(&bytes, |state| {
        state.actors.get_mut(&ids.player).unwrap().fleeing = true;
    });
    assert_resume_rejects(&document, source_hash, &bytes);
}

#[test]
fn contact_receipt_with_an_unresolved_target_is_rejected() {
    let (document, ids, source_hash, bytes) = fresh_save();
    let command_id = CommandId::deterministic("save-integrity/invented-contact");
    let missing_target = EntityId::deterministic("save-integrity/missing-target");
    let bytes = rewrite_state(&bytes, |state| {
        state.contacts.insert(
            command_id,
            ContactReceipt {
                observation: ContactObservation {
                    command_id,
                    source: ids.prop,
                    target: missing_target,
                    initiator: Some(ids.player),
                    normal_speed_m_s: 1.0,
                },
                tick: state.tick,
                events: vec![GameplayEvent::Impact {
                    source: ids.prop,
                    target: missing_target,
                    initiator: Some(ids.player),
                    energy_j: 0.5,
                }],
            },
        );
    });
    assert_resume_rejects(&document, source_hash, &bytes);
}

#[test]
fn non_normalized_object_rotation_is_rejected_after_rehashed_tamper() {
    let (document, ids, source_hash, bytes) = fresh_save();
    let bytes = rewrite_state(&bytes, |state| {
        state.objects.get_mut(&ids.prop).unwrap().rotation_xyzw = [0.0, 0.0, 0.0, 2.0];
    });
    assert_resume_rejects(&document, source_hash, &bytes);
}

#[test]
fn settled_throw_impact_save_preserves_exact_float_energy_on_resume() {
    use forge_gameplay::{ObjectObservation, PhysicsFrame};

    let (mut document, ids) = fixture();
    let source = document
        .objects
        .iter_mut()
        .find(|object| object.id == ids.prop)
        .unwrap();
    source.physics.mass_kg = 10.0;
    source.physics.throwable = true;
    let source_position = source.position;
    let source_hash = source_hash();
    let mut session = GameSession::new(document.clone(), source_hash).unwrap();
    session
        .execute(
            &GameRequest {
                command_id: CommandId::deterministic("save-integrity/float-throw"),
                expected_sequence: 0,
                actor: ids.player,
                action: GameAction::Throw {
                    object: ids.prop,
                    impulse: Vec3::new(1.0, 0.0, 0.0),
                },
            },
            ActionObservation {
                line_of_sight: true,
                confirmed_contact: true,
                water_contact: false,
            },
        )
        .unwrap();

    // Controlled domain observation, not a runtime collision. This f32 speed
    // produces an f64 energy that used to drift by one bit in the native codec.
    let contact_id = CommandId::deterministic("save-integrity/float-contact");
    session
        .advance_frame(&PhysicsFrame {
            source_hash,
            tick: 1,
            actors: vec![],
            objects: vec![ObjectObservation {
                object: ids.prop,
                position: source_position,
                rotation_xyzw: [0.0, 0.0, 0.0, 1.0],
                in_motion: false,
            }],
            contacts: vec![ContactObservation {
                command_id: contact_id,
                source: ids.prop,
                target: ids.door,
                initiator: Some(ids.player),
                normal_speed_m_s: 0.136_673_15,
            }],
            heat: vec![],
            danger: vec![],
        })
        .unwrap();
    let impact_energy = |state: &GameState| {
        state.contacts[&contact_id]
            .events
            .iter()
            .find_map(|event| {
                if let GameplayEvent::Impact { energy_j, .. } = event {
                    Some(energy_j.to_bits())
                } else {
                    None
                }
            })
            .unwrap()
    };
    assert_eq!(impact_energy(session.state()), 0x3fb7_e8ea_46a6_2d00);
    let before_hash = canonical_json_sha256(session.state()).unwrap();
    let bytes = session.save_bytes().unwrap();
    let resumed = GameSession::resume(document, source_hash, &bytes, false)
        .expect("an untampered settled impact Save must Resume with the same state hash");
    assert_eq!(
        impact_energy(resumed.state()),
        impact_energy(session.state())
    );
    assert_eq!(canonical_json_sha256(resumed.state()).unwrap(), before_hash);
    assert_eq!(resumed.state(), session.state());
}
