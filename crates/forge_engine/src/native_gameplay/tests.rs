use super::*;
use forge_core::{EntityId, ModuleId};
use forge_gameplay::{
    ActorDefinition, ActorRole, ObjectDefinition, ObjectKind, ObjectPhysics, RulesProfile,
};
use forge_project::zone_authoring::session::{ZoneAction, ZoneRequest};
use std::{
    fs,
    path::PathBuf,
    time::{SystemTime, UNIX_EPOCH},
};

pub(super) struct Directory {
    pub(super) root: PathBuf,
    parent: PathBuf,
}
impl Directory {
    pub(super) fn new() -> Self {
        let parent = std::env::temp_dir().canonicalize().unwrap();
        let stamp = SystemTime::now()
            .duration_since(UNIX_EPOCH)
            .unwrap()
            .as_nanos();
        let root = parent.join(format!(
            "forge-native-gameplay-test-{}-{stamp}",
            std::process::id()
        ));
        fs::create_dir(&root).unwrap();
        Self { root, parent }
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
    assert_eq!(app.should_exit(), Some(AppExit::Success));
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
    assert_eq!(report["exit_success"], true);
    assert_eq!(exit["exit_success"], true);
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
