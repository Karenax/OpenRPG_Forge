use super::*;
mod streaming;
use forge_core::{EntityId, ModuleId};
use forge_gameplay::{
    ActorDefinition, ActorRole, ObjectDefinition, ObjectKind, ObjectPhysics, RulesProfile,
};
use forge_project::zone_authoring::session::{ZoneAction, ZoneRequest};
use std::{
    fs,
    path::PathBuf,
    sync::atomic::{AtomicU64, Ordering},
    time::{SystemTime, UNIX_EPOCH},
};

pub(super) struct Directory {
    pub(super) root: PathBuf,
    parent: PathBuf,
}
impl Directory {
    pub(super) fn new() -> Self {
        static NEXT_DIRECTORY: AtomicU64 = AtomicU64::new(0);
        let parent = std::env::temp_dir().canonicalize().unwrap();
        let stamp = SystemTime::now()
            .duration_since(UNIX_EPOCH)
            .unwrap()
            .as_nanos();
        loop {
            let sequence = NEXT_DIRECTORY.fetch_add(1, Ordering::Relaxed);
            let root = parent.join(format!(
                "forge-native-gameplay-test-{}-{stamp}-{sequence}",
                std::process::id()
            ));
            match fs::create_dir(&root) {
                Ok(()) => return Self { root, parent },
                Err(error) if error.kind() == std::io::ErrorKind::AlreadyExists => {}
                Err(error) => panic!("Cannot create isolated gameplay test directory: {error}"),
            }
        }
    }
}
impl Drop for Directory {
    fn drop(&mut self) {
        if self
            .root
            .canonicalize()
            .ok()
            .as_ref()
            .and_then(|path| path.parent())
            == Some(self.parent.as_path())
        {
            let _ = fs::remove_dir_all(&self.root);
        }
    }
}
pub(super) fn fixture() -> (ZoneEditSession, GameplayDocument) {
    let mut zone = ZoneEditSession::from_initial_terrain(
        forge_project::authoring::new_terrain("native-generic", "zone", "Test").unwrap(),
    )
    .unwrap();
    let player = EntityId::deterministic("native/player");
    let document = GameplayDocument {
        narrative: None,
        factions: None,
        simulation: None,
        scripting: None,
        cinematics: None,
        creator: None,
        schema: forge_gameplay::GAMEPLAY_SCHEMA.into(),
        version: forge_gameplay::GAMEPLAY_VERSION,
        module_id: ModuleId::deterministic("native/module"),
        area_id: zone.head().base.terrain.area_id,
        player,
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
        actors: vec![ActorDefinition {
            id: player,
            name: "Joueur".into(),
            role: ActorRole::Player,
            position: forge_core::Vec3::new(0.0, 1.0, 0.0),
            max_vitality: 100,
            protection: 0,
            inventory_capacity: 12,
            dialogue: None,
            patrol: vec![],
            flees_danger: false,
        }],
        objects: vec![ObjectDefinition {
            id: EntityId::deterministic("native/door"),
            name: "Porte".into(),
            position: forge_core::Vec3::new(0.0, 1.0, 1.0),
            kind: ObjectKind::Door {
                initially_open: false,
            },
            physics: ObjectPhysics::default(),
        }],
        items: vec![],
        places: vec![],
        dialogues: vec![],
        quests: vec![],
    };
    let mut corrections = zone.head().corrections.clone();
    corrections.gameplay = Some(Box::new(document.clone()));
    zone.execute(&ZoneRequest {
        command_id: CommandId::deterministic("native/author"),
        expected_revision: zone.head().revision,
        expected_hash: zone.head().content_hash().unwrap(),
        action: ZoneAction::edit(corrections),
    })
    .unwrap();
    (zone, document)
}

#[test]
fn native_archive_load_is_generic_and_preserves_authored_bytes() {
    let directory = Directory::new();
    let (zone, document) = fixture();
    let archive = zone.archive_bytes().unwrap();
    let prepared =
        PreparedNativeGameplay::from_archive(&archive, &directory.root.join("saves"), None)
            .unwrap()
            .unwrap();
    assert_eq!(prepared.runtime.session().document(), &document);
    assert_eq!(
        prepared.runtime.session().source_hash(),
        zone.head().content_hash().unwrap()
    );
    assert_eq!(zone.archive_bytes().unwrap(), archive);
    let mut bad = archive.clone();
    bad[0] = b'!';
    assert!(PreparedNativeGameplay::from_archive(&bad, &directory.root, None).is_err());
}

#[test]
fn native_save_is_separate_and_resume_requires_an_explicit_request() {
    let directory = Directory::new();
    let (zone, document) = fixture();
    let archive = zone.archive_bytes().unwrap();
    let author = directory.root.join("zone.json");
    fs::write(&author, &archive).unwrap();
    let root = directory.root.join("saves");
    let mut first = PreparedNativeGameplay::from_archive(&archive, &root, None)
        .unwrap()
        .unwrap();
    first
        .runtime
        .session
        .observe_position(document.player, forge_core::Vec3::new(5.0, 1.0, 8.0))
        .unwrap();
    let bytes = first.runtime.session().save_bytes().unwrap();
    first.storage.save(&bytes).unwrap();
    drop(first);
    let mut fresh = PreparedNativeGameplay::from_archive(&archive, &root, None)
        .unwrap()
        .unwrap();
    assert!(!fresh.resumed);
    assert_eq!(
        fresh.runtime.session().state().actors[&document.player].position,
        document.actors[0].position
    );
    fresh
        .storage
        .request_resume(&document, zone.head().content_hash().unwrap(), false)
        .unwrap();
    drop(fresh);
    let mut resumed = PreparedNativeGameplay::from_archive(&archive, &root, None)
        .unwrap()
        .unwrap();
    assert!(resumed.resumed);
    assert_eq!(
        resumed.runtime.session().state().actors[&document.player].position,
        forge_core::Vec3::new(5.0, 1.0, 8.0)
    );
    resumed.storage.consume_resume_request().unwrap();
    drop(resumed);
    assert_eq!(fs::read(author).unwrap(), archive);
    let fresh = PreparedNativeGameplay::from_archive(&archive, &root, None)
        .unwrap()
        .unwrap();
    assert!(!fresh.resumed);
}

#[test]
fn shared_resume_inspection_and_pinned_request_preserve_the_saved_game() {
    use forge_gameplay::native_storage::{GameStorage, ResumeRequestDisposition};
    let directory = Directory::new();
    let (zone, document) = fixture();
    let root = directory.root.join("saves");
    let source = zone.head().content_hash().unwrap();
    assert!(
        GameStorage::open_existing(&root, &document)
            .unwrap()
            .is_none()
    );
    assert!(
        !root.exists(),
        "inspection must not create a save directory"
    );

    let archive = zone.archive_bytes().unwrap();
    let mut game = PreparedNativeGameplay::from_archive(&archive, &root, None)
        .unwrap()
        .unwrap();
    game.runtime
        .session
        .observe_position(document.player, forge_core::Vec3::new(5.0, 1.0, 8.0))
        .unwrap();
    let saved = game.runtime.session.save_bytes().unwrap();
    game.storage.save(&saved).unwrap();
    drop(game);

    let area = root
        .join(document.module_id.to_string())
        .join(document.area_id.to_string());
    let save_path = area.join("authoring.json");
    let request_path = area.join("resume.json");
    let mut store = GameStorage::open_existing(&root, &document)
        .unwrap()
        .unwrap();
    let pin = store
        .resume_candidate(&document, source, false)
        .unwrap()
        .unwrap();
    assert_eq!(pin, forge_core::bytes_sha256(&saved));
    let mut foreign = document.clone();
    foreign.module_id = ModuleId::deterministic("native/foreign-module");
    assert!(store.resume_candidate(&foreign, source, false).is_err());
    assert!(
        store
            .resume_candidate(&document, ContentHash::from_bytes([7; 32]), false)
            .is_err()
    );
    assert!(!request_path.exists());
    let mut changed = saved.clone();
    changed.push(b' ');
    fs::write(&save_path, &changed).unwrap();
    assert!(
        store
            .request_pinned_resume(&document, source, false, pin)
            .is_err()
    );
    assert!(!request_path.exists());
    assert_eq!(fs::read(&save_path).unwrap(), changed);
    fs::write(&save_path, &saved).unwrap();

    assert_eq!(
        store
            .request_pinned_resume(&document, source, false, pin)
            .unwrap(),
        ResumeRequestDisposition::Created
    );
    let request = fs::read(&request_path).unwrap();
    assert_eq!(
        store
            .request_pinned_resume(&document, source, false, pin)
            .unwrap(),
        ResumeRequestDisposition::Existing
    );
    assert!(store.cancel_resume(&document).is_err());
    assert_eq!(fs::read(&request_path).unwrap(), request);
    assert!(
        store
            .request_pinned_resume(&document, source, true, pin)
            .is_err()
    );
    assert_eq!(fs::read(&request_path).unwrap(), request);
    drop(store);

    let resumed = PreparedNativeGameplay::from_archive(&archive, &root, None)
        .unwrap()
        .unwrap();
    assert!(resumed.resumed);
    assert_eq!(
        resumed.runtime.session().state().actors[&document.player].position,
        forge_core::Vec3::new(5.0, 1.0, 8.0)
    );
    assert_eq!(fs::read(save_path).unwrap(), saved);
}

#[test]
fn standalone_start_intent_preserves_pending_requests_and_save_bytes() {
    use forge_gameplay::native_storage::{GameStorage, StartIntent, has_pending_resume};
    let directory = Directory::new();
    let (zone, document) = fixture();
    let root = directory.root.join("intent-saves");
    let source = zone.head().content_hash().unwrap();
    let archive = zone.archive_bytes().unwrap();
    assert!(!has_pending_resume(&root, document.module_id, document.area_id).unwrap());
    assert!(!root.exists());
    let mut fresh =
        PreparedNativeGameplay::from_archive_with_intent(&archive, &root, None, StartIntent::Fresh)
            .unwrap()
            .unwrap();
    let saved = fresh.runtime.session.save_bytes().unwrap();
    fresh.storage.save(&saved).unwrap();
    drop(fresh);
    let pin = forge_core::bytes_sha256(&saved);
    // A new standalone game cannot later overwrite an existing save, even
    // when there is no pending resume request yet.
    assert!(PreparedNativeGameplay::from_archive_with_intent(
        &archive,
        &root,
        None,
        StartIntent::Fresh,
    )
    .is_err());
    assert!(
        PreparedNativeGameplay::from_archive_with_intent(
            &archive,
            &root,
            None,
            StartIntent::Resume(pin),
        )
        .is_err()
    );

    let mut storage = GameStorage::open_existing(&root, &document)
        .unwrap()
        .unwrap();
    storage
        .request_pinned_resume(&document, source, false, pin)
        .unwrap();
    drop(storage);
    let area = root
        .join(document.module_id.to_string())
        .join(document.area_id.to_string());
    let save_path = area.join("authoring.json");
    let request_path = area.join("resume.json");
    let request = fs::read(&request_path).unwrap();
    assert!(has_pending_resume(&root, document.module_id, document.area_id).unwrap());
    // A pending request is still visible if its save was removed externally.
    let held_save = area.join("held-save.json");
    fs::rename(&save_path, &held_save).unwrap();
    assert!(has_pending_resume(&root, document.module_id, document.area_id).unwrap());
    fs::rename(&held_save, &save_path).unwrap();
    for intent in [
        StartIntent::Fresh,
        StartIntent::Resume(ContentHash::from_bytes([7; 32])),
    ] {
        assert!(
            PreparedNativeGameplay::from_archive_with_intent(&archive, &root, None, intent)
                .is_err()
        );
        assert_eq!(fs::read(&request_path).unwrap(), request);
        assert_eq!(fs::read(&save_path).unwrap(), saved);
    }
    let mut compatible: serde_json::Value = serde_json::from_slice(&request).unwrap();
    compatible["allow_compatible_edits"] = true.into();
    let compatible = serde_json::to_vec(&compatible).unwrap();
    fs::write(&request_path, &compatible).unwrap();
    assert!(
        PreparedNativeGameplay::from_archive_with_intent(
            &archive,
            &root,
            None,
            StartIntent::Resume(pin),
        )
        .is_err()
    );
    assert_eq!(fs::read(&request_path).unwrap(), compatible);
    fs::write(&request_path, &request).unwrap();
    let resumed = PreparedNativeGameplay::from_archive_with_intent(
        &archive,
        &root,
        None,
        StartIntent::Resume(pin),
    )
    .unwrap()
    .unwrap();
    assert!(resumed.resumed);
    assert_eq!(fs::read(save_path).unwrap(), saved);
}

#[test]
fn multi_zone_storage_refuses_foreign_save_without_touching_committed_bytes() {
    let directory = Directory::new();
    let (zone, document) = fixture();
    let source = zone.head().content_hash().unwrap();
    let original = forge_gameplay::GameSession::new(document.clone(), source)
        .unwrap()
        .save_bytes()
        .unwrap();
    let mut storage = storage::GameStorage::open(&directory.root, &document).unwrap();
    storage.save(&original).unwrap();
    let path = directory
        .root
        .join(document.module_id.to_string())
        .join(document.area_id.to_string())
        .join("authoring.json");
    for different_module in [false, true] {
        let mut foreign = document.clone();
        if different_module {
            foreign.module_id = ModuleId::deterministic("multi-zone/other-module");
        } else {
            foreign.area_id = forge_core::AreaId::deterministic("multi-zone/other-area");
        }
        // Deliberately reuse every local gameplay ID. Ownership must bind the
        // envelope to the storage slot, not infer it from actor/object IDs.
        let foreign = forge_gameplay::GameSession::new(foreign, source)
            .unwrap()
            .save_bytes()
            .unwrap();
        assert!(
            storage.save(&foreign).is_err(),
            "Foreign zone/module save was accepted"
        );
        assert_eq!(fs::read(&path).unwrap(), original);
        assert!(!path.with_file_name("authoring.pending").exists());
    }
    for malformed in [b"{".as_slice(), b"[]", b"null", b"{}"] {
        assert!(storage.save(malformed).is_err());
        assert_eq!(fs::read(&path).unwrap(), original);
    }
    assert!(storage.save(&vec![0; 4 * 1024 * 1024 + 1]).is_err());
    storage.save(&original).unwrap();
    assert_eq!(fs::read(path).unwrap(), original);
    assert_eq!(zone.head().content_hash().unwrap(), source);
}

#[test]
#[allow(
    clippy::too_many_lines,
    reason = "One A/B/A persistence scenario checks the entire state and repeated local IDs across both zones and another module"
)]
fn multi_zone_a_b_a_resume_retains_inventory_receipts_and_consumed_pickups_without_duplication() {
    let directory = Directory::new();
    let (_, mut a) = fixture();
    let item = EntityId::deterministic("multi-zone/shared-local-item");
    a.items = vec![forge_gameplay::ItemDefinition {
        id: item,
        name: "Jeton".into(),
        max_stack: 20,
    }];
    a.objects[0].kind = ObjectKind::Pickup { item, quantity: 2 };
    let mut b = a.clone();
    b.area_id = forge_core::AreaId::deterministic("multi-zone/b");
    b.objects[0].kind = ObjectKind::Pickup { item, quantity: 5 };
    let a_bytes = serde_json::to_vec(&a).unwrap();
    let b_bytes = serde_json::to_vec(&b).unwrap();
    let a_source = forge_core::bytes_sha256(&a_bytes);
    let b_source = forge_core::bytes_sha256(&b_bytes);
    let request = |document: &GameplayDocument| GameRequest {
        command_id: CommandId::deterministic("multi-zone/reused-local-request"),
        expected_sequence: 0,
        actor: document.player,
        action: GameAction::Pickup {
            object: document.objects[0].id,
        },
    };
    let observation = forge_gameplay::ActionObservation {
        line_of_sight: true,
        ..default()
    };
    let mut first =
        PreparedNativeGameplay::from_document(a.clone(), a_source, a_source, &directory.root, None)
            .unwrap();
    first
        .runtime
        .session
        .execute(&request(&a), observation)
        .unwrap();
    first
        .runtime
        .session
        .observe_position(a.player, forge_core::Vec3::new(1.0, 1.0, 2.0))
        .unwrap();
    let expected_a = first.runtime.session.state().clone();
    first
        .storage
        .save(&first.runtime.session.save_bytes().unwrap())
        .unwrap();
    first.storage.request_resume(&a, a_source, false).unwrap();
    drop(first);

    let mut second =
        PreparedNativeGameplay::from_document(b.clone(), b_source, b_source, &directory.root, None)
            .unwrap();
    assert!(!second.resumed);
    assert!(
        second.runtime.session.state().actors[&b.player]
            .inventory
            .is_empty()
    );
    second
        .runtime
        .session
        .execute(&request(&b), observation)
        .unwrap();
    let expected_b = second.runtime.session.state().clone();
    second
        .storage
        .save(&second.runtime.session.save_bytes().unwrap())
        .unwrap();
    second.storage.request_resume(&b, b_source, false).unwrap();
    drop(second);

    let mut returned =
        PreparedNativeGameplay::from_document(a.clone(), a_source, a_source, &directory.root, None)
            .unwrap();
    assert!(returned.resumed);
    assert_eq!(returned.runtime.session.state(), &expected_a);
    assert_eq!(
        returned.runtime.session.state().actors[&a.player].inventory[&item],
        2
    );
    // Replaying the same journalled command is idempotent. A distinct second
    // pickup is refused because this zone's object was already consumed.
    returned
        .runtime
        .session
        .execute(&request(&a), observation)
        .unwrap();
    let duplicate = GameRequest {
        command_id: CommandId::deterministic("multi-zone/new-duplicate"),
        expected_sequence: expected_a.sequence,
        ..request(&a)
    };
    assert!(
        returned
            .runtime
            .session
            .execute(&duplicate, observation)
            .is_err()
    );
    assert_eq!(returned.runtime.session.state(), &expected_a);
    returned.storage.consume_resume_request().unwrap();
    drop(returned);

    let mut returned_b =
        PreparedNativeGameplay::from_document(b.clone(), b_source, b_source, &directory.root, None)
            .unwrap();
    assert!(returned_b.resumed);
    assert_eq!(returned_b.runtime.session.state(), &expected_b);
    assert_eq!(
        returned_b.runtime.session.state().actors[&b.player].inventory[&item],
        5
    );
    returned_b.storage.consume_resume_request().unwrap();
    drop(returned_b);

    let mut other = a.clone();
    other.module_id = ModuleId::deterministic("multi-zone/other-module");
    let source = forge_core::canonical_json_sha256(&other).unwrap();
    let foreign =
        PreparedNativeGameplay::from_document(other, source, source, &directory.root, None)
            .unwrap();
    assert!(!foreign.resumed);
    assert!(
        foreign.runtime.session.state().actors[&a.player]
            .inventory
            .is_empty()
    );
    assert_eq!(serde_json::to_vec(&a).unwrap(), a_bytes);
    assert_eq!(serde_json::to_vec(&b).unwrap(), b_bytes);
}

#[test]
fn multi_zone_interrupted_staging_preserves_committed_save_and_does_not_block_another_zone() {
    let directory = Directory::new();
    let (zone, a) = fixture();
    let mut b = a.clone();
    b.area_id = forge_core::AreaId::deterministic("multi-zone/staging-b");
    let source = zone.head().content_hash().unwrap();
    let session_a = forge_gameplay::GameSession::new(a.clone(), source).unwrap();
    let mut session_b = forge_gameplay::GameSession::new(b.clone(), source).unwrap();
    let saved_a = session_a.save_bytes().unwrap();
    let mut store_a = storage::GameStorage::open(&directory.root, &a).unwrap();
    let mut store_b = storage::GameStorage::open(&directory.root, &b).unwrap();
    store_a.save(&saved_a).unwrap();
    store_b.save(&session_b.save_bytes().unwrap()).unwrap();
    let a_directory = directory
        .root
        .join(a.module_id.to_string())
        .join(a.area_id.to_string());
    let pending = a_directory.join("authoring.pending");
    fs::write(&pending, b"{\"interrupted\":").unwrap();
    drop(store_a);
    let mut reopened = storage::GameStorage::open(&directory.root, &a).unwrap();
    reopened.request_resume(&a, source, false).unwrap();
    let (bytes, compatible) = reopened.requested_resume(&a).unwrap();
    assert!(!compatible);
    let resumed = forge_gameplay::GameSession::resume(a, source, &bytes.unwrap(), false).unwrap();
    assert_eq!(resumed.state(), session_a.state());
    reopened.consume_resume_request().unwrap();
    assert!(reopened.save(&saved_a).is_err());
    assert_eq!(
        fs::read(a_directory.join("authoring.json")).unwrap(),
        saved_a
    );
    assert_eq!(fs::read(pending).unwrap(), b"{\"interrupted\":");
    session_b
        .observe_position(b.player, forge_core::Vec3::new(3.0, 1.0, 4.0))
        .unwrap();
    let changed_b = session_b.save_bytes().unwrap();
    store_b.save(&changed_b).unwrap();
    drop(store_b);
    let mut reopened_b = storage::GameStorage::open(&directory.root, &b).unwrap();
    reopened_b.request_resume(&b, source, false).unwrap();
    assert_eq!(
        reopened_b.requested_resume(&b).unwrap().0.unwrap(),
        changed_b
    );
}

#[test]
fn native_resume_rejects_a_changed_save_after_the_request() {
    let directory = Directory::new();
    let (zone, document) = fixture();
    let source = zone.head().content_hash().unwrap();
    let mut storage = storage::GameStorage::open(&directory.root, &document).unwrap();
    let mut session = forge_gameplay::GameSession::new(document.clone(), source).unwrap();
    storage.save(&session.save_bytes().unwrap()).unwrap();
    storage.request_resume(&document, source, false).unwrap();
    session
        .observe_position(document.player, forge_core::Vec3::new(2.0, 1.0, 0.0))
        .unwrap();
    // Deliberately simulate an external writer changing a valid save after its pin was requested.
    fs::write(
        directory
            .root
            .join(document.module_id.to_string())
            .join(document.area_id.to_string())
            .join("authoring.json"),
        session.save_bytes().unwrap(),
    )
    .unwrap();
    assert!(storage.requested_resume(&document).is_err());
}

#[test]
fn native_action_candidates_follow_current_positions_and_queue_readiness() {
    let (_zone, document) = fixture();
    let source = ContentHash::from_bytes([4; 32]);
    let mut runtime = GameplayRuntime::new(document.clone(), source, None, false).unwrap();
    let action = GameAction::SetDoor {
        object: document.objects[0].id,
        open: true,
    };
    assert!(queue_action(&mut runtime, action.clone()).is_err());
    runtime.set_enabled(true);
    assert!(
        presentation::game_view(runtime.session())
            .actions
            .iter()
            .any(|(_, a)| *a == action)
    );
    queue_action(&mut runtime, action.clone()).unwrap();
    let request = runtime.pending.front().unwrap();
    assert_eq!(request.actor, document.player);
    assert_eq!(request.expected_sequence, 0);
    assert!(queue_action(&mut runtime, action).is_err());
    runtime
        .session
        .observe_position(document.player, forge_core::Vec3::new(100.0, 1.0, 0.0))
        .unwrap();
    assert!(
        presentation::game_view(runtime.session())
            .actions
            .is_empty()
    );
}

#[test]
fn native_compatible_resume_remains_explicit_and_rejects_functional_changes() {
    let directory = Directory::new();
    let (zone, mut document) = fixture();
    let source = zone.head().content_hash().unwrap();
    let mut storage = storage::GameStorage::open(&directory.root, &document).unwrap();
    let session = forge_gameplay::GameSession::new(document.clone(), source).unwrap();
    storage.save(&session.save_bytes().unwrap()).unwrap();
    document.objects[0].position.x += 0.25;
    let edited_source = ContentHash::from_bytes([7; 32]);
    assert!(
        storage
            .request_resume(&document, edited_source, false)
            .is_err()
    );
    storage
        .request_resume(&document, edited_source, true)
        .unwrap();
    document.profile.attack_damage += 1;
    assert!(
        storage
            .request_resume(&document, edited_source, true)
            .is_err()
    );
}

#[test]
fn native_container_menu_keeps_full_pile_requests_when_only_one_unit_fits() {
    let (_zone, mut document) = fixture();
    let chest = document.objects[0].id;
    let item = EntityId::deterministic("native/container-item");
    document.objects[0].kind = ObjectKind::Prop;
    document.actors[0].inventory_capacity = 1;
    document.items.push(forge_gameplay::ItemDefinition {
        id: item,
        name: "Jeton".into(),
        max_stack: 4,
    });
    let document = document
        .with_container_definition(
            chest,
            forge_gameplay::ContainerDefinition {
                capacity_units: 4,
                initial_items: std::collections::BTreeMap::from([(item, 4)]),
                accepts: vec![item],
                key: None,
            },
        )
        .unwrap();
    let session =
        forge_gameplay::GameSession::new(document, ContentHash::from_bytes([4; 32])).unwrap();
    let view = presentation::game_view(&session);
    for quantity in [1, 4] {
        let action = GameAction::TransferItem {
            object: chest,
            item,
            quantity,
            direction: forge_gameplay::TransferDirection::Take,
        };
        assert!(
            view.actions
                .iter()
                .any(|(_, candidate)| *candidate == action)
        );
    }
}

#[test]
fn native_refused_resume_remains_paused_until_explicit_fresh_choice() {
    use bevy::ecs::system::RunSystemOnce;
    let directory = Directory::new();
    let (zone, mut document) = fixture();
    let source = zone.head().content_hash().unwrap();
    let mut original = PreparedNativeGameplay::from_document(
        document.clone(),
        source,
        source,
        &directory.root,
        None,
    )
    .unwrap();
    let save = original.runtime.session().save_bytes().unwrap();
    original.storage.save(&save).unwrap();
    original
        .storage
        .request_resume(&document, source, false)
        .unwrap();
    drop(original);
    document.objects[0].position.x += 0.25;
    let edited = ContentHash::from_bytes([8; 32]);
    let blocked = PreparedNativeGameplay::from_document(
        document.clone(),
        edited,
        edited,
        &directory.root,
        None,
    )
    .unwrap();
    assert!(!blocked.resumed);
    assert!(blocked.recovery.is_some());
    let mut app = App::new();
    app.insert_resource(blocked.runtime)
        .insert_resource(blocked.storage)
        .insert_resource(NativeGameplayUi {
            open: true,
            feedback: String::new(),
            last_view: None,
            recovery: blocked.recovery,
        })
        .insert_resource(crate::PlayerSimulationReady(true))
        .insert_resource(crate::PlayerInputFocus(true))
        .init_resource::<crate::PendingPlayerControlCommand>()
        .insert_resource(crate::gameplay::GameplayProjectionStatus { ready: true })
        .init_resource::<CommandResult>()
        .add_message::<NativeGameplayCommand>()
        .add_message::<bevy::app::AppExit>();
    app.world_mut().run_system_once(readiness).unwrap();
    assert!(!app.world().resource::<crate::PlayerSimulationReady>().0);
    assert!(!app.world().resource::<crate::PlayerInputFocus>().0);
    assert!(!app.world().resource::<GameplayRuntime>().is_ready());
    assert!(
        app.world()
            .resource::<GameplayRuntime>()
            .save_bytes()
            .is_err()
    );
    let save_path = directory
        .root
        .join(document.module_id.to_string())
        .join(document.area_id.to_string())
        .join("authoring.json");
    assert_eq!(fs::read(&save_path).unwrap(), save);
    app.world_mut()
        .write_message(NativeGameplayCommand::StartFresh);
    app.world_mut().run_system_once(process_commands).unwrap();
    assert!(
        app.world()
            .resource::<NativeGameplayUi>()
            .recovery
            .is_none()
    );
    assert_eq!(fs::read(&save_path).unwrap(), save);
    assert!(!save_path.parent().unwrap().join("resume.json").exists());
}

#[test]
fn native_refused_strict_resume_can_be_upgraded_explicitly_without_changing_save() {
    let directory = Directory::new();
    let (zone, mut document) = fixture();
    let source = zone.head().content_hash().unwrap();
    let mut first = PreparedNativeGameplay::from_document(
        document.clone(),
        source,
        source,
        &directory.root,
        None,
    )
    .unwrap();
    let saved = first.runtime.session().save_bytes().unwrap();
    first.storage.save(&saved).unwrap();
    first
        .storage
        .request_resume(&document, source, false)
        .unwrap();
    drop(first);
    document.objects[0].position.x += 0.25;
    let edited = ContentHash::from_bytes([9; 32]);
    let mut refused = PreparedNativeGameplay::from_document(
        document.clone(),
        edited,
        edited,
        &directory.root,
        None,
    )
    .unwrap();
    assert!(refused.recovery.is_some());
    assert!(!refused.resumed);
    refused
        .storage
        .request_resume(&document, edited, true)
        .unwrap();
    drop(refused);
    let recovered = PreparedNativeGameplay::from_document(
        document.clone(),
        edited,
        edited,
        &directory.root,
        None,
    )
    .unwrap();
    assert!(recovered.resumed);
    assert!(recovered.recovery.is_none());
    assert!(
        recovered
            .runtime
            .session()
            .state()
            .requires_physics_revalidation
    );
    let save_path = directory
        .root
        .join(document.module_id.to_string())
        .join(document.area_id.to_string())
        .join("authoring.json");
    assert_eq!(fs::read(save_path).unwrap(), saved);
}

#[test]
fn native_resume_cancel_refuses_a_changed_or_foreign_request() {
    let directory = Directory::new();
    let (zone, document) = fixture();
    let source = zone.head().content_hash().unwrap();
    let mut prepared = PreparedNativeGameplay::from_document(
        document.clone(),
        source,
        source,
        &directory.root,
        None,
    )
    .unwrap();
    prepared
        .storage
        .save(&prepared.runtime.session().save_bytes().unwrap())
        .unwrap();
    prepared
        .storage
        .request_resume(&document, source, false)
        .unwrap();
    let path = directory
        .root
        .join(document.module_id.to_string())
        .join(document.area_id.to_string())
        .join("resume.json");
    let bytes = fs::read(&path).unwrap();
    let mut foreign: serde_json::Value = serde_json::from_slice(&bytes).unwrap();
    foreign["module"] = serde_json::to_value(ModuleId::deterministic("foreign/module")).unwrap();
    let altered = serde_json::to_vec(&foreign).unwrap();
    fs::write(&path, &altered).unwrap();
    assert!(prepared.storage.cancel_resume(&document).is_err());
    assert_eq!(fs::read(path).unwrap(), altered);
}

#[derive(Clone, Copy)]
enum ExitTrigger {
    CloseWindow,
    NativeQuit,
    LateLast,
    MixedOutcomes,
}

fn trigger_exit(app: &mut App, trigger: ExitTrigger, window: Entity) {
    use bevy::{app::AppExit, window::WindowCloseRequested};
    match trigger {
        ExitTrigger::CloseWindow => {
            app.world_mut()
                .write_message(WindowCloseRequested { window });
            // Bevy marks ClosingWindow first; the next update performs closure.
            app.update();
            assert_eq!(app.should_exit(), None);
            assert!(
                app.world()
                    .get::<bevy::window::ClosingWindow>(window)
                    .is_some()
            );
        }
        ExitTrigger::NativeQuit => {
            app.add_systems(Update, process_commands);
            app.world_mut().write_message(NativeGameplayCommand::Quit);
        }
        ExitTrigger::LateLast => {
            app.add_systems(
                Last,
                (|mut exit: MessageWriter<AppExit>| {
                    exit.write(AppExit::Success);
                })
                .after(bevy::window::ExitSystems)
                .after(observations::exit)
                .after(report::exit),
            );
        }
        ExitTrigger::MixedOutcomes => {
            app.add_systems(PostUpdate, |mut exits: MessageWriter<AppExit>| {
                exits.write(AppExit::Success);
                exits.write(AppExit::error());
            });
        }
    }
}

fn assert_exit_reports_in_one_update(trigger: ExitTrigger) {
    use bevy::app::AppExit;
    let directory = Directory::new();
    let (zone, document) = fixture();
    let archive = zone.archive_bytes().unwrap();
    let source = zone.head().content_hash().unwrap();
    let archive_hash = forge_core::bytes_sha256(&archive);
    let functional = document.functional_hash().unwrap();
    let observation_path = directory.root.join("observations.jsonl");
    let report_path = directory.root.join("game-report.json");
    let prepared = PreparedNativeGameplay::from_archive(
        &archive,
        &directory.root.join("saves"),
        Some(&observation_path),
    )
    .unwrap()
    .unwrap();
    let report = report::ReportSink::new(&report_path, source, archive_hash, functional).unwrap();
    let mut app = App::new();
    app.add_plugins(MinimalPlugins)
        .add_plugins(bevy::window::WindowPlugin::default())
        .add_message::<AppExit>()
        .add_message::<NativeGameplayCommand>()
        .insert_resource(prepared.runtime)
        .insert_resource(prepared.storage)
        .insert_resource(prepared.observations.unwrap())
        .insert_resource(NativeGameplayUi {
            open: false,
            feedback: String::new(),
            last_view: None,
            recovery: None,
        })
        .init_resource::<CommandResult>();
    install_exit_observers(&mut app, Some(report));
    let window = app
        .world_mut()
        .query_filtered::<Entity, With<Window>>()
        .single(app.world())
        .unwrap();
    trigger_exit(&mut app, trigger, window);
    assert!(fs::read(&report_path).unwrap().is_empty());
    app.update();
    let success = !matches!(trigger, ExitTrigger::MixedOutcomes);
    assert_eq!(
        app.should_exit(),
        Some(if success {
            AppExit::Success
        } else {
            AppExit::error()
        })
    );
    assert_eq!(
        app.world().get::<Window>(window).is_none(),
        matches!(trigger, ExitTrigger::CloseWindow)
    );
    let report_bytes = fs::read(&report_path).unwrap();
    assert!(!report_bytes.is_empty());
    let report: serde_json::Value = serde_json::from_slice(&report_bytes).unwrap();
    let rows: Vec<serde_json::Value> = fs::read_to_string(&observation_path)
        .unwrap()
        .lines()
        .map(|line| serde_json::from_str(line).unwrap())
        .collect();
    let exits: Vec<_> = rows.iter().filter(|row| row["kind"] == "exit").collect();
    assert_eq!(exits.len(), 1);
    let exit = exits[0];
    for key in [
        "pid",
        "source_hash",
        "archive_hash",
        "functional_hash",
        "tick",
        "sequence",
    ] {
        assert_eq!(report[key], exit[key], "Exit identity/state differs: {key}");
    }
    assert_eq!(report["pid"], std::process::id());
    assert_eq!(report["source_hash"], source.to_string());
    assert_eq!(report["archive_hash"], archive_hash.to_string());
    assert_eq!(report["functional_hash"], functional.to_string());
    assert_eq!(report["tick"], 0);
    assert_eq!(report["sequence"], 0);
    assert_eq!(report["exit_success"], success);
    assert_eq!(exit["exit_success"], success);
    assert!(exit["observation_error"].is_null());
    assert_eq!(zone.archive_bytes().unwrap(), archive);
}

#[test]
fn native_exit_reports_last_window_close_in_same_update() {
    assert_exit_reports_in_one_update(ExitTrigger::CloseWindow);
}

#[test]
fn native_exit_reports_native_quit_with_window_still_open() {
    assert_exit_reports_in_one_update(ExitTrigger::NativeQuit);
}

#[test]
fn native_exit_reports_late_last_writer_in_same_update() {
    assert_exit_reports_in_one_update(ExitTrigger::LateLast);
}

#[test]
fn native_exit_reports_multiple_outcomes_once_and_prioritizes_failure() {
    assert_exit_reports_in_one_update(ExitTrigger::MixedOutcomes);
}
