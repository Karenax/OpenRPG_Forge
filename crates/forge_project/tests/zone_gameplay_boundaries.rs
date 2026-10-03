use forge_core::{AreaId, CommandId, EntityId, ModuleId, Vec3};
use forge_gameplay::{
    ActorDefinition, ActorRole, Condition, DialogueChoice, DialogueDefinition, DialogueEffect,
    DialogueNode, GAMEPLAY_SCHEMA, GAMEPLAY_VERSION, GameplayDocument, ItemDefinition,
    ObjectDefinition, ObjectKind, ObjectPhysics, PlaceDefinition, QuestDefinition, RulesProfile,
};
use forge_project::{
    authoring::{new_terrain, playtest::PlaytestSnapshot},
    zone_authoring::session::{ZoneAction, ZoneEditSession, ZoneRequest},
};

fn id(key: &str) -> EntityId {
    EntityId::deterministic(format!("zone-gameplay-boundaries/{key}"))
}

fn profile() -> RulesProfile {
    RulesProfile {
        id: "forge.gate3-minimal".into(),
        version: 1,
        interaction_range_m: 3.0,
        attack_range_m: 2.0,
        attack_damage: 20,
        attack_stamina_cost: 5,
        max_stamina: 100,
        max_action_receipts: 512,
        max_events_per_tick: 128,
    }
}

fn actors(player: EntityId, villager: EntityId, dialogue: EntityId) -> Vec<ActorDefinition> {
    vec![
        ActorDefinition {
            id: player,
            name: "Player".into(),
            role: ActorRole::Player,
            position: Vec3::ZERO,
            max_vitality: 100,
            protection: 0,
            inventory_capacity: 12,
            dialogue: None,
            patrol: vec![],
            flees_danger: false,
        },
        ActorDefinition {
            id: villager,
            name: "Villager".into(),
            role: ActorRole::Friendly,
            position: Vec3::new(4.0, 0.0, 2.0),
            max_vitality: 100,
            protection: 0,
            inventory_capacity: 4,
            dialogue: Some(dialogue),
            patrol: vec![Vec3::new(4.0, 0.0, 2.0)],
            flees_danger: false,
        },
    ]
}

fn items(key: EntityId, reward: EntityId) -> Vec<ItemDefinition> {
    vec![
        ItemDefinition {
            id: key,
            name: "Key".into(),
            max_stack: 2,
        },
        ItemDefinition {
            id: reward,
            name: "Reward".into(),
            max_stack: 1,
        },
    ]
}

fn objects(key: EntityId, pickup: EntityId, door: EntityId) -> Vec<ObjectDefinition> {
    vec![
        ObjectDefinition {
            id: pickup,
            name: "Key on a table".into(),
            position: Vec3::new(2.0, 0.0, 1.0),
            kind: ObjectKind::Pickup {
                item: key,
                quantity: 1,
            },
            physics: ObjectPhysics::default(),
        },
        ObjectDefinition {
            id: door,
            name: "Village door".into(),
            position: Vec3::new(6.0, 0.0, 2.0),
            kind: ObjectKind::Door {
                initially_open: false,
            },
            physics: ObjectPhysics::default(),
        },
    ]
}

fn dialogues(
    dialogue: EntityId,
    node: EntityId,
    choice: EntityId,
    quest: EntityId,
    door: EntityId,
) -> Vec<DialogueDefinition> {
    vec![DialogueDefinition {
        id: dialogue,
        start: node,
        nodes: vec![DialogueNode {
            id: node,
            text: "Find the key.".into(),
            choices: vec![DialogueChoice {
                id: choice,
                text: "I will help.".into(),
                condition: Condition::Always,
                effects: vec![
                    DialogueEffect::AcceptQuest { quest },
                    DialogueEffect::SetDoor {
                        object: door,
                        open: true,
                    },
                ],
                next: None,
            }],
        }],
    }]
}

fn gameplay(area_id: AreaId) -> GameplayDocument {
    let player = id("actor/player");
    let villager = id("actor/villager");
    let key = id("item/key");
    let reward = id("item/reward");
    let pickup = id("object/pickup");
    let door = id("object/door");
    let village = id("place/village");
    let dialogue = id("dialogue/villager");
    let node = id("dialogue/villager/start");
    let choice = id("dialogue/villager/choice");
    let quest = id("quest/key");

    GameplayDocument {
        schema: GAMEPLAY_SCHEMA.into(),
        version: GAMEPLAY_VERSION,
        module_id: ModuleId::deterministic("zone-gameplay-boundaries/module"),
        area_id,
        profile: profile(),
        player,
        actors: actors(player, villager, dialogue),
        items: items(key, reward),
        objects: objects(key, pickup, door),
        places: vec![PlaceDefinition {
            id: village,
            name: "Village".into(),
            position: Vec3::new(8.0, 0.0, 4.0),
            links: vec![],
        }],
        dialogues: dialogues(dialogue, node, choice, quest, door),
        quests: vec![QuestDefinition {
            id: quest,
            name: "Find the key".into(),
            giver: villager,
            required_item: key,
            required_quantity: 1,
            reward_item: Some(reward),
            reward_quantity: 1,
        }],
    }
}

fn session() -> ZoneEditSession {
    ZoneEditSession::from_initial_terrain(
        new_terrain("gameplay-boundaries", "area", "Gameplay boundaries").unwrap(),
    )
    .unwrap()
}

fn raw_request(session: &ZoneEditSession, gameplay: &GameplayDocument, key: &str) -> ZoneRequest {
    let mut corrections = session.head().corrections.clone();
    corrections.gameplay = Some(Box::new(gameplay.clone()));
    ZoneRequest {
        command_id: CommandId::deterministic(key),
        expected_revision: session.head().revision,
        expected_hash: session.head().content_hash().unwrap(),
        action: ZoneAction::edit(corrections),
    }
}

fn action(session: &ZoneEditSession, action: ZoneAction, key: &str) -> ZoneRequest {
    ZoneRequest {
        command_id: CommandId::deterministic(key),
        expected_revision: session.head().revision,
        expected_hash: session.head().content_hash().unwrap(),
        action,
    }
}

fn assert_raw_rejected_without_mutation(
    session: &mut ZoneEditSession,
    candidate: &GameplayDocument,
    key: &str,
) {
    let before_head = session.head().clone();
    let before_archive = session.archive_bytes().unwrap();
    let before_audit = session.audit().to_vec();
    let request = raw_request(session, candidate, key);
    assert!(session.execute(&request).is_err());
    assert_eq!(
        session.head(),
        &before_head,
        "rejected command changed the head"
    );
    assert_eq!(session.archive_bytes().unwrap(), before_archive);
    assert_eq!(session.audit(), before_audit.as_slice());
}

#[test]
fn wrong_area_id_is_rejected_before_authoring_mutation() {
    let mut session = session();
    let area_id = session.head().base.terrain.area_id;
    let mut foreign = gameplay(area_id);
    foreign.area_id = AreaId::deterministic("zone-gameplay-boundaries/foreign-area");
    let before_head = session.head().clone();
    let before_archive = session.archive_bytes().unwrap();

    assert!(
        session
            .head()
            .prepare_gameplay(
                Some(foreign),
                CommandId::deterministic("wrong-area/prepare"),
            )
            .is_err()
    );
    assert_eq!(session.head(), &before_head);
    assert_eq!(session.archive_bytes().unwrap(), before_archive);

    let mut foreign = gameplay(area_id);
    foreign.area_id = AreaId::deterministic("zone-gameplay-boundaries/foreign-area-raw");
    assert_raw_rejected_without_mutation(&mut session, &foreign, "wrong-area/raw");
}

#[test]
fn positions_and_patrol_points_outside_terrain_are_rejected_atomically() {
    let session = session();
    let area_id = session.head().base.terrain.area_id;
    let mut actor_outside = gameplay(area_id);
    actor_outside.actors[1].position = Vec3::new(257.0, 0.0, 0.0);
    let mut patrol_outside = gameplay(area_id);
    patrol_outside.actors[1].patrol = vec![Vec3::new(0.0, 0.0, -257.0)];
    let mut object_outside = gameplay(area_id);
    object_outside.objects[0].position = Vec3::new(0.0, 0.0, 257.0);
    let mut place_outside = gameplay(area_id);
    place_outside.places[0].position = Vec3::new(-257.0, 0.0, 0.0);

    let before_head = session.head().clone();
    let before_archive = session.archive_bytes().unwrap();
    for (key, candidate) in [
        ("actor-position", actor_outside),
        ("patrol-point", patrol_outside),
        ("object-position", object_outside),
        ("place-position", place_outside),
    ] {
        assert!(
            session
                .head()
                .prepare_gameplay(Some(candidate), CommandId::deterministic(key))
                .is_err()
        );
        assert_eq!(session.head(), &before_head);
        assert_eq!(session.archive_bytes().unwrap(), before_archive);
    }
}

#[test]
fn invalid_references_in_forged_commands_do_not_mutate_the_authoring_session() {
    let mut session = session();
    let area_id = session.head().base.terrain.area_id;

    let mut missing_dialogue = gameplay(area_id);
    missing_dialogue.actors[1].dialogue = Some(id("dialogue/missing"));
    let mut missing_pickup_item = gameplay(area_id);
    missing_pickup_item.objects[0].kind = ObjectKind::Pickup {
        item: id("item/missing"),
        quantity: 1,
    };
    let mut missing_support = gameplay(area_id);
    missing_support.objects[1].physics.supports = vec![id("object/missing")];
    let mut missing_place_link = gameplay(area_id);
    missing_place_link.places[0].links = vec![id("place/missing")];
    let mut missing_quest_giver = gameplay(area_id);
    missing_quest_giver.quests[0].giver = id("actor/missing");

    for (key, candidate) in [
        ("missing-dialogue", missing_dialogue),
        ("missing-pickup-item", missing_pickup_item),
        ("missing-support", missing_support),
        ("missing-place-link", missing_place_link),
        ("missing-quest-giver", missing_quest_giver),
    ] {
        assert_raw_rejected_without_mutation(&mut session, &candidate, key);
    }
}

#[test]
fn gameplay_removal_survives_save_reopen_undo_redo_on_all_three_codecs() {
    let mut authored = session();
    let area_id = authored.head().base.terrain.area_id;
    let document = gameplay(area_id);
    let insert = authored
        .head()
        .prepare_gameplay(
            Some(document.clone()),
            CommandId::deterministic("gameplay/insert"),
        )
        .unwrap();
    authored.execute(&insert).unwrap();
    let authored_head = authored.head().clone();

    for (codec, bytes) in [
        ("plain", authored.archive_bytes().unwrap()),
        ("compact", authored.compact_archive_bytes().unwrap()),
        ("delta", authored.delta_archive_bytes().unwrap()),
    ] {
        let mut reopened = ZoneEditSession::from_archive_bytes(&bytes).unwrap();
        assert_eq!(reopened.head(), &authored_head);

        let remove = reopened
            .head()
            .prepare_gameplay(
                None,
                CommandId::deterministic(format!("gameplay/remove/{codec}")),
            )
            .unwrap();
        reopened.execute(&remove).unwrap();
        assert!(reopened.head().corrections.gameplay.is_none());

        let saved = reopened.archive_bytes().unwrap();
        let mut saved_reopened = ZoneEditSession::from_archive_bytes(&saved).unwrap();
        assert!(saved_reopened.head().corrections.gameplay.is_none());

        let undo = action(
            &saved_reopened,
            ZoneAction::Undo,
            &format!("gameplay/undo/{codec}"),
        );
        saved_reopened.execute(&undo).unwrap();
        assert_eq!(
            saved_reopened.head().corrections.gameplay.as_deref(),
            Some(&document)
        );

        let redo = action(
            &saved_reopened,
            ZoneAction::Redo,
            &format!("gameplay/redo/{codec}"),
        );
        saved_reopened.execute(&redo).unwrap();
        assert!(saved_reopened.head().corrections.gameplay.is_none());
        let final_reopened =
            ZoneEditSession::from_archive_bytes(&saved_reopened.archive_bytes().unwrap()).unwrap();
        assert!(final_reopened.head().corrections.gameplay.is_none());
    }
}

#[test]
fn playtest_snapshot_clones_gameplay_and_binds_the_head_hash_without_source_mutation() {
    let mut session = session();
    let area_id = session.head().base.terrain.area_id;
    let document = gameplay(area_id);
    let insert = session
        .head()
        .prepare_gameplay(
            Some(document.clone()),
            CommandId::deterministic("snapshot/insert"),
        )
        .unwrap();
    session.execute(&insert).unwrap();

    let source_head = session.head().clone();
    let source_archive = session.archive_bytes().unwrap();
    let source_hash = source_head.content_hash().unwrap();
    let (effective_terrain, _) = source_head.effective().unwrap();
    let snapshot = PlaytestSnapshot::from_zone(&source_head, source_hash).unwrap();

    assert_eq!(snapshot.identity().source_hash, source_hash);
    assert_eq!(
        snapshot.identity().terrain_hash,
        effective_terrain.content_hash().unwrap()
    );
    assert_eq!(snapshot.gameplay(), Some(&document));
    assert_eq!(session.head(), &source_head);
    assert_eq!(session.archive_bytes().unwrap(), source_archive);

    let remove = session
        .head()
        .prepare_gameplay(None, CommandId::deterministic("snapshot/remove"))
        .unwrap();
    session.execute(&remove).unwrap();
    assert!(session.head().corrections.gameplay.is_none());
    assert_eq!(snapshot.gameplay(), Some(&document));
    assert_ne!(
        session.head().content_hash().unwrap(),
        snapshot.identity().source_hash
    );
}
