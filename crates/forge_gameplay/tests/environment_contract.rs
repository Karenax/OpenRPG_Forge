use forge_core::{AreaId, CommandId, ContentHash, EntityId, ModuleId, Vec3};
use forge_gameplay::{
    ActionObservation, ActorDefinition, ActorRole, ContactObservation, DangerObservation,
    GameAction, GameRequest, GameSession, GameplayDocument, GameplayEvent, HeatExposure,
    ObjectDefinition, ObjectKind, ObjectObservation, ObjectPhysics, PhysicsFrame, RulesProfile,
};

fn id(key: &str) -> EntityId {
    EntityId::deterministic(format!("environment/{key}"))
}
fn document() -> GameplayDocument {
    GameplayDocument {
        narrative: None,
        factions: None,
        simulation: None,
        scripting: None,
        cinematics: None,
        creator: None,
        schema: "forge.gameplay".into(),
        version: 1,
        module_id: ModuleId::deterministic("environment/module"),
        area_id: AreaId::deterministic("environment/area"),
        player: id("player"),
        profile: RulesProfile {
            id: "forge.gate3-minimal".into(),
            version: 1,
            interaction_range_m: 3.0,
            attack_range_m: 2.0,
            attack_damage: 20,
            attack_stamina_cost: 5,
            max_stamina: 100,
            max_action_receipts: 512,
            max_events_per_tick: 128,
        },
        actors: [
            ("player", ActorRole::Player, false, 100),
            ("enemy", ActorRole::Enemy, true, 80),
        ]
        .into_iter()
        .map(|(key, role, flees_danger, max_vitality)| ActorDefinition {
            id: id(key),
            name: key.into(),
            role,
            position: Vec3::ZERO,
            max_vitality,
            protection: 0,
            inventory_capacity: 4,
            dialogue: None,
            patrol: vec![],
            flees_danger,
        })
        .collect(),
        objects: vec![
            ObjectDefinition {
                id: id("crate"),
                name: "Crate".into(),
                position: Vec3::ZERO,
                kind: ObjectKind::Prop,
                physics: ObjectPhysics {
                    mass_kg: 10.0,
                    pushable: true,
                    throwable: true,
                    ..Default::default()
                },
            },
            ObjectDefinition {
                id: id("support"),
                name: "Support".into(),
                position: Vec3::ZERO,
                kind: ObjectKind::Prop,
                physics: ObjectPhysics {
                    mass_kg: 10.0,
                    break_energy_j: Some(30.0),
                    supports: vec![id("charge")],
                    flammable: true,
                    heat_damage_per_tick: 4,
                    ..Default::default()
                },
            },
            ObjectDefinition {
                id: id("charge"),
                name: "Charge".into(),
                position: Vec3::new(0.0, 2.0, 0.0),
                kind: ObjectKind::Prop,
                physics: ObjectPhysics {
                    mass_kg: 30.0,
                    ..Default::default()
                },
            },
        ],
        items: vec![],
        places: vec![],
        dialogues: vec![],
        quests: vec![],
    }
}
fn session() -> GameSession {
    GameSession::new(document(), ContentHash::from_bytes([11; 32])).unwrap()
}
fn frame(game: &GameSession) -> PhysicsFrame {
    PhysicsFrame {
        simulation_step_ns: None,
        source_hash: game.source_hash(),
        tick: game.state().tick + 1,
        actors: vec![],
        objects: vec![],
        contacts: vec![],
        heat: vec![],
        danger: vec![],
    }
}

#[test]
fn compatible_resume_requires_current_geometry_before_actions_or_save() {
    let mut game = session();
    let observed = frame(&game);
    game.advance_frame(&observed).unwrap();
    let saved = game.save_bytes().unwrap();
    let mut edited = document();
    edited.actors[0].name = "Retouched name".into();
    let new_hash = ContentHash::from_bytes([12; 32]);
    let mut resumed = GameSession::resume(edited, new_hash, &saved, true).unwrap();
    assert!(resumed.state().requires_physics_revalidation);
    assert!(resumed.save_bytes().is_err());
    assert!(resumed.advance_frame(&observed).is_err());
    assert!(
        resumed
            .execute(
                &GameRequest {
                    command_id: CommandId::deterministic("pending-geometry-action"),
                    expected_sequence: 0,
                    actor: id("player"),
                    action: GameAction::Ignite {
                        object: id("support")
                    },
                },
                ActionObservation {
                    line_of_sight: true,
                    confirmed_contact: true,
                    water_contact: false
                }
            )
            .is_err()
    );
    resumed.advance_frame(&frame(&resumed)).unwrap();
    assert!(!resumed.state().requires_physics_revalidation);
    assert!(resumed.save_bytes().is_ok());
}

#[test]
fn saved_last_frame_must_match_its_source_even_after_rehash() {
    let mut game = session();
    game.advance_frame(&frame(&game)).unwrap();
    let mut envelope: serde_json::Value =
        serde_json::from_slice(&game.save_bytes().unwrap()).unwrap();
    let mut state: forge_gameplay::GameState =
        serde_json::from_value(envelope["state"].clone()).unwrap();
    state.last_frame.as_mut().unwrap().frame.source_hash = ContentHash::from_bytes([99; 32]);
    envelope["state_hash"] =
        serde_json::to_value(forge_core::canonical_json_sha256(&state).unwrap()).unwrap();
    envelope["state"] = serde_json::to_value(state).unwrap();
    assert!(
        GameSession::resume(
            document(),
            game.source_hash(),
            &serde_json::to_vec(&envelope).unwrap(),
            false
        )
        .is_err()
    );
}
fn act(game: &mut GameSession, key: &str, action: GameAction, water_contact: bool) {
    game.execute(
        &GameRequest {
            command_id: CommandId::deterministic(key),
            expected_sequence: game.state().sequence,
            actor: id("player"),
            action,
        },
        ActionObservation {
            line_of_sight: true,
            confirmed_contact: true,
            water_contact,
        },
    )
    .unwrap();
}

#[test]
fn support_break_releases_charge_and_preserves_initiator_through_impact_and_resume() {
    let mut game = session();
    act(
        &mut game,
        "throw",
        GameAction::Throw {
            object: id("crate"),
            impulse: Vec3::new(20.0, 0.0, 0.0),
        },
        false,
    );
    let mut hit = frame(&game);
    hit.contacts.push(ContactObservation {
        command_id: CommandId::deterministic("crate-support"),
        source: id("crate"),
        target: id("support"),
        initiator: Some(id("player")),
        normal_speed_m_s: 3.0,
    });
    let receipt = game.advance_frame(&hit).unwrap();
    assert!(receipt.events.contains(&GameplayEvent::Destroyed {
        object: id("support")
    }));
    assert!(receipt.events.contains(&GameplayEvent::Released {
        object: id("charge")
    }));
    assert!(game.state().objects[&id("charge")].in_motion);
    assert_eq!(game.advance_frame(&hit).unwrap(), receipt);
    assert!(game.save_bytes().is_err());
    let mut fall = frame(&game);
    fall.contacts.push(ContactObservation {
        command_id: CommandId::deterministic("charge-enemy"),
        source: id("charge"),
        target: id("enemy"),
        initiator: Some(id("player")),
        normal_speed_m_s: 4.0,
    });
    fall.objects = vec![
        ObjectObservation {
            object: id("crate"),
            position: Vec3::ZERO,
            rotation_xyzw: [0.0, 0.0, 0.0, 1.0],
            in_motion: false,
        },
        ObjectObservation {
            object: id("charge"),
            position: Vec3::ZERO,
            rotation_xyzw: [0.0, 0.0, 0.0, 1.0],
            in_motion: false,
        },
    ];
    game.advance_frame(&fall).unwrap();
    assert_eq!(game.state().actors[&id("enemy")].vitality, 0);
    let bytes = game.save_bytes().unwrap();
    let mut loaded = GameSession::resume(document(), game.source_hash(), &bytes, false).unwrap();
    assert_eq!(loaded.state(), game.state());
    let mut retry = frame(&loaded);
    retry.contacts = fall.contacts;
    assert!(loaded.advance_frame(&retry).unwrap().events.is_empty());
    assert_eq!(loaded.state().contacts.len(), 2);
}

#[test]
fn resting_contact_is_observable_without_damage_and_stale_frames_are_atomic() {
    let mut game = session();
    let mut resting = frame(&game);
    resting.contacts.push(ContactObservation {
        command_id: CommandId::deterministic("rest"),
        source: id("crate"),
        target: id("enemy"),
        initiator: None,
        normal_speed_m_s: 0.0,
    });
    let receipt = game.advance_frame(&resting).unwrap();
    assert!(
        matches!(receipt.events.as_slice(),[GameplayEvent::Impact {energy_j,..}] if energy_j.to_bits()==0.0_f64.to_bits())
    );
    assert_eq!(game.state().actors[&id("enemy")].vitality, 80);
    let previous = game.state().clone();
    resting.source_hash = ContentHash::from_bytes([12; 32]);
    assert!(game.advance_frame(&resting).is_err());
    assert_eq!(game.state(), &previous);
}

#[test]
fn fire_damage_and_visible_danger_stop_after_observed_water_extinguishes_source() {
    let mut game = session();
    act(
        &mut game,
        "light",
        GameAction::Ignite {
            object: id("support"),
        },
        false,
    );
    let mut hot = frame(&game);
    hot.heat.push(HeatExposure {
        object: id("support"),
        actor: id("enemy"),
        line_of_sight: true,
    });
    hot.danger.push(DangerObservation {
        object: id("support"),
        actor: id("enemy"),
        line_of_sight: true,
    });
    game.advance_frame(&hot).unwrap();
    assert_eq!(game.state().actors[&id("enemy")].vitality, 76);
    assert!(game.state().actors[&id("enemy")].fleeing);
    let dry = GameRequest {
        command_id: CommandId::deterministic("dry"),
        expected_sequence: game.state().sequence,
        actor: id("player"),
        action: GameAction::Extinguish {
            object: id("support"),
        },
    };
    let previous = game.state().clone();
    assert!(
        game.execute(
            &dry,
            ActionObservation {
                line_of_sight: true,
                confirmed_contact: true,
                water_contact: false
            }
        )
        .is_err()
    );
    assert_eq!(game.state(), &previous);
    act(
        &mut game,
        "wet",
        GameAction::Extinguish {
            object: id("support"),
        },
        true,
    );
    hot.tick += 1;
    game.advance_frame(&hot).unwrap();
    assert_eq!(game.state().actors[&id("enemy")].vitality, 76);
    assert!(!game.state().actors[&id("enemy")].fleeing);
    assert!(!game.state().objects[&id("support")].burning);
}

#[test]
fn duplicate_contacts_and_cascades_over_quota_reject_entire_frame() {
    let mut doc = document();
    doc.profile.max_events_per_tick = 2;
    let mut game = GameSession::new(doc, ContentHash::from_bytes([11; 32])).unwrap();
    let mut hit = frame(&game);
    hit.contacts.push(ContactObservation {
        command_id: CommandId::deterministic("cascade"),
        source: id("crate"),
        target: id("support"),
        initiator: None,
        normal_speed_m_s: 3.0,
    });
    let initial = game.state().clone();
    assert!(game.advance_frame(&hit).is_err());
    assert_eq!(game.state(), &initial);
    hit.contacts[0].normal_speed_m_s = 0.0;
    hit.contacts.push(hit.contacts[0].clone());
    assert!(game.advance_frame(&hit).is_err());
    assert_eq!(game.state(), &initial);
}
