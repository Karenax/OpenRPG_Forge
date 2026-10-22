use forge_core::{AreaId, CommandId, ContentHash, EntityId, ModuleId, Vec3};
use forge_gameplay::{
    ActionObservation, ActorDefinition, ActorRole, GAMEPLAY_SCHEMA, GAMEPLAY_VERSION, GameAction,
    GameRequest, GameSession, GameplayDocument, ItemDefinition, ObjectDefinition, ObjectKind,
    ObjectPhysics, RulesProfile,
};
use forge_project::authoring::{
    AuthoringRequest as Request, game_save::GameSaveRequest, host::AuthoringHost, new_terrain,
};
use std::{
    fs,
    path::{Path, PathBuf},
};

#[path = "game_save_host/temporal.rs"]
mod temporal;

#[path = "game_save_host/presentation.rs"]
mod presentation;

#[derive(Clone, Copy)]
struct FixtureIds {
    player: EntityId,
    item: EntityId,
    pickup: EntityId,
}

struct Fixture {
    area_id: AreaId,
    gameplay: GameplayDocument,
    ids: FixtureIds,
    source_hash: ContentHash,
    source_bytes: Vec<u8>,
}

fn id(key: &str) -> EntityId {
    EntityId::deterministic(format!("game-save-host/{key}"))
}

fn gameplay(area_id: AreaId) -> (GameplayDocument, FixtureIds) {
    let ids = FixtureIds {
        player: id("actor/player"),
        item: id("item/key"),
        pickup: id("object/key"),
    };
    let document = GameplayDocument {
        narrative: None,
        factions: None,
        simulation: None,
        scripting: None,
        cinematics: None,
        creator: None,
        schema: GAMEPLAY_SCHEMA.into(),
        version: GAMEPLAY_VERSION,
        module_id: ModuleId::deterministic("game-save-host/module"),
        area_id,
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
            name: "Key".into(),
            max_stack: 2,
        }],
        objects: vec![ObjectDefinition {
            id: ids.pickup,
            name: "Key on a table".into(),
            position: Vec3::ZERO,
            kind: ObjectKind::Pickup {
                item: ids.item,
                quantity: 1,
            },
            physics: ObjectPhysics::default(),
        }],
        places: vec![],
        dialogues: vec![],
        quests: vec![],
    };
    (document, ids)
}

fn root(label: &str) -> PathBuf {
    let path = std::env::temp_dir().join(format!(
        "forge-game-save-host-{label}-{}",
        std::process::id()
    ));
    fs::create_dir(&path).unwrap();
    path
}

fn authoring_path(root: &Path, area_id: AreaId) -> PathBuf {
    root.join(area_id.to_string()).join("authoring.json")
}

fn setup(label: &str) -> (PathBuf, AuthoringHost, Fixture) {
    let root = root(label);
    let mut host = AuthoringHost::new(&root).unwrap();
    let terrain = new_terrain("game-save-host", "area", "Game save").unwrap();
    let area_id = terrain.area_id;
    host.execute(Request::CreateZone {
        document: Box::new(terrain),
    })
    .unwrap();
    host.execute(Request::Save).unwrap();

    let head = host
        .execute(Request::Inspect)
        .unwrap()
        .zone_head
        .expect("created aggregate zone has a head");
    let (gameplay, ids) = gameplay(area_id);
    let request = head
        .prepare_gameplay(
            Some(gameplay.clone()),
            CommandId::deterministic(format!("{label}/gameplay")),
        )
        .unwrap();
    let applied = host.execute(Request::ApplyZone { request }).unwrap();
    let source_hash = applied.zone_head.as_ref().unwrap().content_hash().unwrap();
    host.execute(Request::Save).unwrap();
    let source_bytes = fs::read(authoring_path(&root, area_id)).unwrap();
    (
        root,
        host,
        Fixture {
            area_id,
            gameplay,
            ids,
            source_hash,
            source_bytes,
        },
    )
}

fn game_save_json(fixture: &Fixture) -> String {
    let mut session = GameSession::new(fixture.gameplay.clone(), fixture.source_hash).unwrap();
    session
        .execute(
            &GameRequest {
                command_id: CommandId::deterministic("game-save-host/pickup"),
                expected_sequence: 0,
                actor: fixture.ids.player,
                action: GameAction::Pickup {
                    object: fixture.ids.pickup,
                },
            },
            ActionObservation {
                line_of_sight: true,
                confirmed_contact: true,
                water_contact: false,
            },
        )
        .unwrap();
    String::from_utf8(session.save_bytes().unwrap()).unwrap()
}

fn cleanup(root: PathBuf) {
    fs::remove_dir_all(root).unwrap();
}

fn altered_functional_hash(save_json: &str) -> String {
    let mut value: serde_json::Value = serde_json::from_str(save_json).unwrap();
    value["functional_hash"] =
        serde_json::Value::String(ContentHash::from_bytes([0xA5; 32]).to_string());
    serde_json::to_string(&value).unwrap()
}

fn attach_container(
    host: &mut AuthoringHost,
    fixture: &Fixture,
) -> (GameplayDocument, ContentHash, EntityId) {
    let chest = id("object/container");
    let mut document = fixture.gameplay.clone();
    document.objects.push(ObjectDefinition {
        id: chest,
        name: "Coffre".into(),
        position: Vec3::ZERO,
        kind: ObjectKind::Prop,
        physics: ObjectPhysics::default(),
    });
    let document = document
        .with_container_definition(
            chest,
            forge_gameplay::ContainerDefinition {
                capacity_units: 2,
                initial_items: std::collections::BTreeMap::from([(fixture.ids.item, 2)]),
                accepts: Vec::new(),
                key: None,
            },
        )
        .unwrap();
    let head = host.execute(Request::Inspect).unwrap().zone_head.unwrap();
    let request = head
        .prepare_gameplay(
            Some(document.clone()),
            CommandId::deterministic("game-save-host/container"),
        )
        .unwrap();
    let state = host.execute(Request::ApplyZone { request }).unwrap();
    let source = state.zone_head.unwrap().content_hash().unwrap();
    host.execute(Request::Save).unwrap();
    (document, source, chest)
}

fn attach_factions(host: &mut AuthoringHost, fixture: &Fixture) -> (GameplayDocument, ContentHash) {
    use forge_gameplay::{FactionDefinition, FactionsDefinition};
    let mut document = fixture.gameplay.clone();
    for (name, role, x) in [
        ("victim", ActorRole::Enemy, 1.0),
        ("observer", ActorRole::Friendly, 2.0),
    ] {
        let mut actor = document.actors[0].clone();
        actor.id = id(name);
        actor.name = name.into();
        actor.role = role;
        actor.position = Vec3::new(x, 0.0, 0.0);
        document.actors.push(actor);
    }
    let group = id("guards");
    let document = document
        .with_factions_definition(Some(FactionsDefinition {
            version: 1,
            factions: vec![FactionDefinition {
                id: group,
                name: "Guetteurs".into(),
                perception_range_m: 10.0,
                protects_factions: vec![group],
                assault_delta: -25,
            }],
            memberships: std::collections::BTreeMap::from([
                (id("victim"), group),
                (id("observer"), group),
            ]),
            relations: vec![],
        }))
        .unwrap();
    let head = host.execute(Request::Inspect).unwrap().zone_head.unwrap();
    let request = head
        .prepare_gameplay(
            Some(document.clone()),
            CommandId::deterministic("game-save-host/factions"),
        )
        .unwrap();
    let state = host.execute(Request::ApplyZone { request }).unwrap();
    let source = state.zone_head.unwrap().content_hash().unwrap();
    host.execute(Request::Save).unwrap();
    (document, source)
}

#[test]
fn faction_save_v4_reopens_through_native_host_without_changing_authored_baselines() {
    let (root, mut host, fixture) = setup("faction-roundtrip");
    let (document, source) = attach_factions(&mut host, &fixture);
    let authored = fs::read(authoring_path(&root, fixture.area_id)).unwrap();
    let mut game = GameSession::new(document.clone(), source).unwrap();
    game.execute_with_witnesses(
        &GameRequest {
            command_id: CommandId::deterministic("host/local-assault"),
            expected_sequence: 0,
            actor: fixture.ids.player,
            action: GameAction::Attack {
                target: id("victim"),
            },
        },
        ActionObservation {
            line_of_sight: true,
            confirmed_contact: true,
            water_contact: false,
        },
        &[forge_gameplay::FactionWitnessObservation {
            observer: id("observer"),
            line_of_sight: true,
        }],
    )
    .unwrap();
    let save_json = String::from_utf8(game.save_bytes().unwrap()).unwrap();
    assert_eq!(
        serde_json::from_str::<serde_json::Value>(&save_json).unwrap()["version"],
        4
    );
    host.execute(Request::StartPlaytest {
        expected_hash: source,
    })
    .unwrap();
    let saved = host
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
    assert_eq!(saved.stored_hash, loaded.stored_hash);
    let resumed =
        GameSession::resume(document.clone(), source, loaded.save_json.as_bytes(), false).unwrap();
    assert_eq!(resumed.state(), game.state());
    assert_eq!(
        resumed.personal_relation(id("observer"), fixture.ids.player),
        Some(-25)
    );
    assert_eq!(
        document
            .factions
            .as_ref()
            .unwrap()
            .base_relation(id("observer"), fixture.ids.player),
        0
    );
    assert_eq!(
        fs::read(authoring_path(&root, fixture.area_id)).unwrap(),
        authored
    );
    reopened.execute(Request::Close).unwrap();
    drop(reopened);
    cleanup(root);
}

fn attach_narrative(
    host: &mut AuthoringHost,
    fixture: &Fixture,
) -> (GameplayDocument, ContentHash) {
    use forge_gameplay::{
        Condition, DialogueChoice, DialogueDefinition, DialogueEffect, DialogueNode,
        NarrativeDefinition, NarrativeValue, NarrativeVariable,
    };
    let mut document = fixture
        .gameplay
        .with_narrative_definition(Some(NarrativeDefinition {
            version: 1,
            variables: vec![NarrativeVariable {
                id: id("knows"),
                name: "Testimony heard".into(),
                initial: NarrativeValue::Boolean(false),
            }],
            quests: vec![],
        }))
        .unwrap();
    document.actors.push(ActorDefinition {
        id: id("witness"),
        name: "Witness".into(),
        role: ActorRole::Friendly,
        dialogue: Some(id("dialogue")),
        ..document.actors[0].clone()
    });
    document.dialogues.push(DialogueDefinition {
        id: id("dialogue"),
        start: id("node"),
        nodes: vec![DialogueNode {
            id: id("node"),
            text: "I saw it happen.".into(),
            choices: vec![DialogueChoice {
                id: id("choice"),
                text: "Remember testimony".into(),
                condition: Condition::Always,
                effects: vec![DialogueEffect::SetVariable {
                    variable: id("knows"),
                    value: NarrativeValue::Boolean(true),
                }],
                next: None,
            }],
        }],
    });
    let head = host.execute(Request::Inspect).unwrap().zone_head.unwrap();
    let request = head
        .prepare_gameplay(
            Some(document.clone()),
            CommandId::deterministic("game-save-host/narrative"),
        )
        .unwrap();
    let state = host.execute(Request::ApplyZone { request }).unwrap();
    let source = state.zone_head.unwrap().content_hash().unwrap();
    host.execute(Request::Save).unwrap();
    (document, source)
}

#[test]
fn narrative_save_v3_reopens_through_native_host_and_preserves_authored_initial_values() {
    use forge_gameplay::NarrativeValue;
    let (root, mut host, fixture) = setup("narrative-roundtrip");
    let (document, source) = attach_narrative(&mut host, &fixture);
    let authored = fs::read(authoring_path(&root, fixture.area_id)).unwrap();
    let mut game = GameSession::new(document.clone(), source).unwrap();
    game.execute(
        &GameRequest {
            command_id: CommandId::deterministic("game-save-host/hear-testimony"),
            expected_sequence: 0,
            actor: fixture.ids.player,
            action: GameAction::ChooseDialogue {
                speaker: id("witness"),
                node: id("node"),
                choice: id("choice"),
            },
        },
        ActionObservation {
            line_of_sight: true,
            ..Default::default()
        },
    )
    .unwrap();
    let save_json = String::from_utf8(game.save_bytes().unwrap()).unwrap();
    assert_eq!(
        serde_json::from_str::<serde_json::Value>(&save_json).unwrap()["version"],
        3
    );
    host.execute(Request::StartPlaytest {
        expected_hash: source,
    })
    .unwrap();
    let saved = host
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
    assert_eq!(loaded.stored_hash, saved.stored_hash);
    let resumed =
        GameSession::resume(document.clone(), source, loaded.save_json.as_bytes(), false).unwrap();
    assert_eq!(resumed.state(), game.state());
    assert_eq!(
        resumed.state().narrative.as_ref().unwrap().variables[&id("knows")],
        NarrativeValue::Boolean(true)
    );
    assert_eq!(
        document.narrative.as_ref().unwrap().variables[0].initial,
        NarrativeValue::Boolean(false)
    );
    assert_eq!(
        fs::read(authoring_path(&root, fixture.area_id)).unwrap(),
        authored
    );
    reopened.execute(Request::Close).unwrap();
    drop(reopened);
    cleanup(root);
}

#[test]
fn container_save_v2_roundtrips_through_native_host_without_mutating_authored_contents() {
    let (root, mut host, fixture) = setup("container-roundtrip");
    let (document, source, chest) = attach_container(&mut host, &fixture);
    let source_bytes = fs::read(authoring_path(&root, fixture.area_id)).unwrap();
    let mut game = GameSession::new(document.clone(), source).unwrap();
    game.execute(
        &GameRequest {
            command_id: CommandId::deterministic("game-save-host/take-container"),
            expected_sequence: 0,
            actor: fixture.ids.player,
            action: GameAction::TransferItem {
                object: chest,
                item: fixture.ids.item,
                quantity: 1,
                direction: forge_gameplay::TransferDirection::Take,
            },
        },
        ActionObservation {
            line_of_sight: true,
            ..ActionObservation::default()
        },
    )
    .unwrap();
    let save_json = String::from_utf8(game.save_bytes().unwrap()).unwrap();
    assert_eq!(
        serde_json::from_str::<serde_json::Value>(&save_json).unwrap()["version"],
        2
    );
    host.execute(Request::StartPlaytest {
        expected_hash: source,
    })
    .unwrap();
    let saved = host
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
    reopened
        .execute(Request::Open {
            area_id: fixture.area_id,
        })
        .unwrap();
    let loaded = reopened
        .execute_game_save(GameSaveRequest::Load {
            expected_source: source,
            allow_compatible_edits: false,
        })
        .unwrap()
        .saved
        .unwrap();
    assert_eq!(loaded.stored_hash, saved.stored_hash);
    let resumed =
        GameSession::resume(document, source, loaded.save_json.as_bytes(), false).unwrap();
    assert_eq!(resumed.state(), game.state());
    assert_eq!(
        resumed.state().objects[&chest]
            .container
            .as_ref()
            .unwrap()
            .items
            .get(&fixture.ids.item),
        Some(&1)
    );
    assert_eq!(
        fs::read(authoring_path(&root, fixture.area_id)).unwrap(),
        source_bytes
    );
    reopened.execute(Request::Close).unwrap();
    drop(reopened);
    cleanup(root);
}

#[test]
fn save_close_reopen_and_load_resume_inventory_without_changing_authoring_source() {
    let (root, mut host, fixture) = setup("roundtrip");
    let save_json = game_save_json(&fixture);
    let before = host.execute(Request::Inspect).unwrap();
    let playing = host
        .execute(Request::StartPlaytest {
            expected_hash: fixture.source_hash,
        })
        .unwrap();
    assert_eq!(playing.playtest_hash, Some(fixture.source_hash));

    let saved = host
        .execute_game_save(GameSaveRequest::Save {
            expected_source: fixture.source_hash,
            expected_stored: None,
            save_json: save_json.clone(),
        })
        .unwrap()
        .saved
        .unwrap();
    assert_eq!(saved.source_hash, fixture.source_hash);
    assert_eq!(saved.save_json, save_json);
    assert_eq!(
        host.execute(Request::Inspect).unwrap().zone_head,
        before.zone_head
    );
    assert_eq!(
        fs::read(authoring_path(&root, fixture.area_id))
            .unwrap()
            .as_slice(),
        fixture.source_bytes.as_slice()
    );

    host.execute(Request::StopPlaytest { diagnostics: None })
        .unwrap();
    host.execute(Request::Close).unwrap();
    drop(host);

    let mut reopened_host = AuthoringHost::new(&root).unwrap();
    let reopened = reopened_host
        .execute(Request::Open {
            area_id: fixture.area_id,
        })
        .unwrap();
    assert_eq!(
        reopened.zone_head.as_ref().unwrap().content_hash().unwrap(),
        fixture.source_hash
    );
    assert!(!reopened.dirty);
    assert_eq!(
        fs::read(authoring_path(&root, fixture.area_id))
            .unwrap()
            .as_slice(),
        fixture.source_bytes.as_slice()
    );

    let loaded = reopened_host
        .execute_game_save(GameSaveRequest::Load {
            expected_source: fixture.source_hash,
            allow_compatible_edits: false,
        })
        .unwrap()
        .saved
        .unwrap();
    assert_eq!(loaded.stored_hash, saved.stored_hash);
    let resumed = GameSession::resume(
        fixture.gameplay.clone(),
        fixture.source_hash,
        loaded.save_json.as_bytes(),
        false,
    )
    .unwrap();
    assert_eq!(
        resumed.state().actors[&fixture.ids.player]
            .inventory
            .get(&fixture.ids.item),
        Some(&1)
    );
    assert_eq!(
        fs::read(authoring_path(&root, fixture.area_id))
            .unwrap()
            .as_slice(),
        fixture.source_bytes.as_slice()
    );
    reopened_host.execute(Request::Close).unwrap();
    drop(reopened_host);
    cleanup(root);
}

#[test]
fn save_requires_playtest_and_rejects_stale_source() {
    let (root, mut host, fixture) = setup("stale-source");
    let save_json = game_save_json(&fixture);
    assert!(
        host.execute_game_save(GameSaveRequest::Save {
            expected_source: fixture.source_hash,
            expected_stored: None,
            save_json: save_json.clone(),
        })
        .is_err()
    );
    assert!(!root.join(".game-saves").exists());

    host.execute(Request::StartPlaytest {
        expected_hash: fixture.source_hash,
    })
    .unwrap();
    let stale = ContentHash::from_bytes([0x5A; 32]);
    assert!(
        host.execute_game_save(GameSaveRequest::Save {
            expected_source: stale,
            expected_stored: None,
            save_json,
        })
        .is_err()
    );
    assert!(
        host.execute_game_save(GameSaveRequest::Load {
            expected_source: stale,
            allow_compatible_edits: false,
        })
        .is_err()
    );
    assert_eq!(
        fs::read(authoring_path(&root, fixture.area_id))
            .unwrap()
            .as_slice(),
        fixture.source_bytes.as_slice()
    );
    host.execute(Request::StopPlaytest { diagnostics: None })
        .unwrap();
    host.execute(Request::Close).unwrap();
    drop(host);
    cleanup(root);
}

#[test]
fn incompatible_save_and_wrong_expected_stored_are_rejected_without_overwrite() {
    let (root, mut host, fixture) = setup("integrity");
    let save_json = game_save_json(&fixture);
    host.execute(Request::StartPlaytest {
        expected_hash: fixture.source_hash,
    })
    .unwrap();
    let saved = host
        .execute_game_save(GameSaveRequest::Save {
            expected_source: fixture.source_hash,
            expected_stored: None,
            save_json: save_json.clone(),
        })
        .unwrap()
        .saved
        .unwrap();
    let wrong_stored = ContentHash::from_bytes([0xC3; 32]);
    assert_ne!(wrong_stored, saved.stored_hash);
    assert!(
        host.execute_game_save(GameSaveRequest::Save {
            expected_source: fixture.source_hash,
            expected_stored: Some(wrong_stored),
            save_json: save_json.clone(),
        })
        .is_err()
    );

    let incompatible = altered_functional_hash(&save_json);
    assert!(
        host.execute_game_save(GameSaveRequest::Save {
            expected_source: fixture.source_hash,
            expected_stored: Some(saved.stored_hash),
            save_json: incompatible,
        })
        .is_err()
    );
    let loaded = host
        .execute_game_save(GameSaveRequest::Load {
            expected_source: fixture.source_hash,
            allow_compatible_edits: false,
        })
        .unwrap()
        .saved
        .unwrap();
    assert_eq!(loaded.stored_hash, saved.stored_hash);
    assert_eq!(loaded.save_json, save_json);
    assert_eq!(
        fs::read(authoring_path(&root, fixture.area_id))
            .unwrap()
            .as_slice(),
        fixture.source_bytes.as_slice()
    );
    host.execute(Request::StopPlaytest { diagnostics: None })
        .unwrap();
    host.execute(Request::Close).unwrap();
    drop(host);
    cleanup(root);
}

#[test]
fn save_directory_must_remain_a_direct_child_of_the_authorized_root() {
    let (root, mut host, fixture) = setup("root-lock");
    let save_json = game_save_json(&fixture);
    host.execute(Request::StartPlaytest {
        expected_hash: fixture.source_hash,
    })
    .unwrap();
    let lock = root.join(".game-saves");
    fs::write(&lock, b"reserved by the workspace").unwrap();
    assert!(
        host.execute_game_save(GameSaveRequest::Save {
            expected_source: fixture.source_hash,
            expected_stored: None,
            save_json,
        })
        .is_err()
    );
    assert_eq!(fs::read(&lock).unwrap(), b"reserved by the workspace");
    assert_eq!(
        fs::read(authoring_path(&root, fixture.area_id))
            .unwrap()
            .as_slice(),
        fixture.source_bytes.as_slice()
    );
    host.execute(Request::StopPlaytest { diagnostics: None })
        .unwrap();
    host.execute(Request::Close).unwrap();
    drop(host);
    cleanup(root);
}
