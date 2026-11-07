//! Exercise the real native command, gameplay and Rapier schedules together.
use super::*;
use crate::cell_streaming::{
    PlayerCellStreamingPlugin, PlayerCellStreamingSet, PlayerCellStreamingStatus,
    StreamedTerrainCell,
};
use crate::runtime_static_admission::{
    SpeciesReadiness, StaticAdmissionSet, install_static_admission_with_species,
};
use crate::{PlayerControllerPlugin, PlayerRuntimeSnapshot, PlayerSimulationReady};
use bevy::time::TimeUpdateStrategy;
use bevy_rapier3d::prelude::{
    Collider, KinematicCharacterController, RapierConfiguration, RigidBody,
};
use std::time::{Duration, Instant};

fn app(directory: &Directory) -> (App, ZoneEditSession, PathBuf) {
    let (zone, document) = fixture();
    app_from_zone(directory, zone, &document)
}

fn app_from_zone(
    directory: &Directory,
    zone: ZoneEditSession,
    document: &GameplayDocument,
) -> (App, ZoneEditSession, PathBuf) {
    let save = directory
        .root
        .join(document.module_id.to_string())
        .join(document.area_id.to_string())
        .join("authoring.json");
    let mut app = crate::cell_streaming::tests::app_with_cell_budget(64);
    app.add_plugins((TransformPlugin, bevy::input::InputPlugin))
        .insert_resource(Assets::<Font>::default())
        .insert_resource(TimeUpdateStrategy::ManualDuration(Duration::from_secs_f64(
            1.0 / 60.0,
        )))
        .add_message::<bevy::app::AppExit>();
    crate::sampled_terrain_physics::register(&mut app);
    let spawn = document.actor(document.player).unwrap().position;
    app.add_plugins((
        PlayerControllerPlugin::new(
            forge_world::embedded_visual_valley_player_controller_profile().unwrap(),
            Vec3::new(spawn.x, spawn.y, spawn.z),
        )
        .with_interactive_input(false),
        PlayerCellStreamingPlugin,
    ));
    PreparedNativeGameplay::from_archive(&zone.archive_bytes().unwrap(), &directory.root, None)
        .unwrap()
        .unwrap()
        .install(&mut app)
        .unwrap();
    (app, zone, save)
}

fn ready(app: &mut App) {
    let deadline = Instant::now() + Duration::from_secs(10);
    loop {
        app.update();
        let runtime = app.world().resource::<GameplayRuntime>();
        assert!(runtime.error().is_none(), "{:?}", runtime.error());
        let streaming = app.world().resource::<PlayerCellStreamingStatus>();
        assert!(streaming.error.is_none(), "{:?}", streaming.error);
        if runtime.is_ready()
            && runtime.player_settled
            && app.world().resource::<PlayerRuntimeSnapshot>().grounded
        {
            return;
        }
        assert!(
            Instant::now() < deadline,
            "Native streamed gameplay never ready"
        );
        std::thread::sleep(Duration::from_millis(1));
    }
}

// A failed admission remains diagnosed while the retry is being prepared.
// Exercise the same explicit service exposed to the player rather than clearing
// CellStreamingStats or accepting an uncommitted request as ready.
fn ready_after_terrain_repair(app: &mut App) {
    let deadline = Instant::now() + Duration::from_secs(10);
    let mut retry_issued = false;
    loop {
        let error = app
            .world()
            .resource::<crate::cell_streaming::CellStreamingStats>()
            .last_error
            .clone();
        if let Some(error) = error {
            assert!(
                error.contains("active streamed terrain projection was removed or replaced"),
                "unexpected terrain failure: {error}"
            );
            if !retry_issued {
                retry_issued = crate::cell_streaming::retry_failed_terrain(app.world_mut());
            }
        }
        app.update();
        let runtime = app.world().resource::<GameplayRuntime>();
        assert!(runtime.error().is_none(), "{:?}", runtime.error());
        if runtime.is_ready()
            && runtime.player_settled
            && app.world().resource::<PlayerRuntimeSnapshot>().grounded
        {
            let stats = app
                .world()
                .resource::<crate::cell_streaming::CellStreamingStats>();
            assert!(!stats.pending);
            assert!(stats.last_error.is_none(), "{:?}", stats.last_error);
            assert!(app.world().resource::<PlayerSimulationReady>().0);
            return;
        }
        assert!(
            Instant::now() < deadline,
            "repaired ground did not regain admitted physics and gameplay"
        );
        std::thread::sleep(Duration::from_millis(1));
    }
}

fn save(app: &mut App) {
    app.world_mut().write_message(NativeGameplayCommand::Save);
    app.update();
    assert!(matches!(
        app.world().resource::<CommandResult>().last,
        Some((NativeGameplayCommand::Save, Ok(())))
    ));
}

fn assert_held_commands_preserve_save(app: &mut App, path: &Path, bytes: &[u8]) {
    let before = app
        .world()
        .resource::<GameplayRuntime>()
        .session()
        .state()
        .clone();
    app.world_mut().write_message(NativeGameplayCommand::Save);
    app.update();
    assert!(matches!(
        app.world().resource::<CommandResult>().last,
        Some((NativeGameplayCommand::Save, Err(_)))
    ));
    let object = app
        .world()
        .resource::<GameplayRuntime>()
        .session()
        .document()
        .objects[0]
        .id;
    app.world_mut()
        .write_message(NativeGameplayCommand::Action(GameAction::SetDoor {
            object,
            open: true,
        }));
    for _ in 0..8 {
        app.update();
        assert!(!app.world().resource::<PlayerSimulationReady>().0);
        assert!(!app.world().resource::<GameplayRuntime>().is_ready());
        assert_eq!(
            app.world().resource::<GameplayRuntime>().session().state(),
            &before
        );
    }
    assert!(matches!(
        app.world().resource::<CommandResult>().last,
        Some((NativeGameplayCommand::Action(_), Err(_)))
    ));
    assert_eq!(fs::read(path).unwrap(), bytes);
    assert!(!path.with_file_name("authoring.pending").exists());
}

// This publication is deliberately synthetic; GPU species evidence is exercised
// by the actual runtime adapter, not manufactured by this CPU fixture.
#[derive(Resource)]
struct TestSpecies(Option<bool>);

#[derive(Resource, Default)]
struct TerrainRequestObservation {
    ready: bool,
    error: Option<String>,
}

fn observe_terrain_request(world: &mut World) {
    let status = world.resource::<PlayerCellStreamingStatus>();
    let ready = status.ready;
    let error = status.error.clone();
    let mut observed = world.resource_mut::<TerrainRequestObservation>();
    observed.ready = ready;
    observed.error = error;
}

fn report_test_species(world: &mut World) {
    if let Some(ready) = world.resource::<TestSpecies>().0
        && let Some(mut species) = world.get_resource_mut::<SpeciesReadiness>()
    {
        species.publish(ready);
    }
}

#[derive(Resource, Default)]
struct TestGraphicsHold {
    blocked: bool,
    hold: crate::cell_streaming::TemporarySimulationHold,
}

fn release_test_graphics(world: &mut World) {
    world.resource_scope(|world, mut graphics: Mut<TestGraphicsHold>| graphics.hold.release(world));
}

fn hold_test_graphics(world: &mut World) {
    world.resource_scope(|world, mut graphics: Mut<TestGraphicsHold>| {
        if graphics.blocked {
            graphics.hold.hold(world);
        }
    });
}

fn install_composition(app: &mut App, zone: &ZoneEditSession, terrain_first: bool) {
    crate::runtime_buildings::install(app, zone.head()).unwrap();
    install_static_admission_with_species(app, vec![], true);
    app.insert_resource(TestSpecies(Some(false)))
        .init_resource::<TerrainRequestObservation>()
        .init_resource::<TestGraphicsHold>()
        .add_systems(
            FixedUpdate,
            report_test_species.in_set(StaticAdmissionSet::PublishSpecies),
        )
        .add_systems(FixedFirst, release_test_graphics)
        .add_systems(
            FixedUpdate,
            observe_terrain_request
                .after(PlayerCellStreamingSet::Request)
                .before(StaticAdmissionSet::Constrain),
        )
        .add_systems(
            FixedUpdate,
            hold_test_graphics
                .after(PlayerCellStreamingSet::Request)
                .before(crate::native_gameplay::NativeGameplayReadinessSet),
        );
    if terrain_first {
        app.configure_sets(
            FixedUpdate,
            PlayerCellStreamingSet::Request.before(StaticAdmissionSet::PublishSpecies),
        );
    } else {
        app.configure_sets(
            FixedUpdate,
            PlayerCellStreamingSet::Request.after(StaticAdmissionSet::PublishSpecies),
        );
    }
}

fn wait_for_terrain(app: &mut App) {
    let deadline = Instant::now() + Duration::from_secs(10);
    loop {
        app.update();
        // Enforce intentionally clears its public ready flag while any host
        // retains the simulation, so inspect the actual Request observation.
        let streaming = app.world().resource::<TerrainRequestObservation>();
        assert!(streaming.error.is_none(), "{:?}", streaming.error);
        if streaming.ready {
            return;
        }
        assert!(
            Instant::now() < deadline,
            "Protected terrain never became ready"
        );
        std::thread::sleep(Duration::from_millis(1));
    }
}

#[test]
fn streamed_admission_reserves_terrain_for_a_held_movable_prop() {
    let directory = Directory::new();
    let (mut zone, mut document) = fixture();
    document.actors[0].position = forge_core::Vec3::new(16.0, 1.0, 16.0);
    let prop = EntityId::deterministic("native/distant-prop");
    document.objects.push(ObjectDefinition {
        id: prop,
        name: "Bloc poussable".into(),
        position: forge_core::Vec3::new(112.0, 0.5, 112.0),
        kind: ObjectKind::Prop,
        physics: ObjectPhysics {
            body_half_extents_m: forge_core::Vec3::new(0.5, 0.5, 0.5),
            mass_kg: 10.0,
            pushable: true,
            ..Default::default()
        },
    });
    let mut corrections = zone.head().corrections.clone();
    corrections.gameplay = Some(Box::new(document.clone()));
    zone.execute(&ZoneRequest {
        command_id: CommandId::deterministic("native/author-distant-prop"),
        expected_revision: zone.head().revision,
        expected_hash: zone.head().content_hash().unwrap(),
        action: ZoneAction::edit(corrections),
    })
    .unwrap();
    let (mut app, zone, _) = app_from_zone(&directory, zone, &document);
    install_composition(&mut app, &zone, true);
    wait_for_terrain(&mut app);

    let world = app.world();
    let runtime = world.resource::<GameplayRuntime>();
    let entity = runtime.entities[&prop];
    assert!(!runtime.is_ready());
    assert_eq!(world.get::<RigidBody>(entity), Some(&RigidBody::Fixed));
    let prop_cell = forge_world::cell_streaming::CellKey { x: 7, z: 7 };
    let protection = world.resource::<crate::cell_streaming::CellStreamingProtection>();
    assert!(protection.cells.contains(&prop_cell));

    app.world_mut().resource_mut::<TestSpecies>().0 = Some(true);
    ready(&mut app);
    for _ in 0..20 {
        app.update();
        let world = app.world();
        assert!(
            world.resource::<GameplayRuntime>().is_ready(),
            "terrain requested {}, gameplay projection {}, streaming {:?}, cells {:?}, prop {:?}",
            world.resource::<TerrainRequestObservation>().ready,
            world
                .resource::<crate::gameplay::GameplayProjectionStatus>()
                .ready,
            world.resource::<PlayerCellStreamingStatus>(),
            world.resource::<crate::cell_streaming::CellStreamingStats>(),
            world.get::<Transform>(entity),
        );
        assert!(
            world
                .resource::<crate::cell_streaming::CellStreamingProtection>()
                .cells
                .contains(&prop_cell)
        );
    }
}

#[test]
fn streamed_admission_combines_both_publication_orders_and_preserves_holds() {
    for terrain_first in [true, false] {
        let directory = Directory::new();
        let (mut app, zone, path) = app(&directory);
        install_composition(&mut app, &zone, terrain_first);
        wait_for_terrain(&mut app);
        assert!(app.world().resource::<TerrainRequestObservation>().ready);
        assert!(!app.world().resource::<PlayerCellStreamingStatus>().ready);
        assert!(!app.world().resource::<PlayerSimulationReady>().0);
        assert!(!app.world().resource::<GameplayRuntime>().is_ready());
        let contexts = app
            .world_mut()
            .query::<&RapierConfiguration>()
            .iter(app.world())
            .map(|config| config.physics_pipeline_active)
            .collect::<Vec<_>>();
        assert_eq!(contexts, vec![false]);
        let requested_motion = app
            .world_mut()
            .query::<&KinematicCharacterController>()
            .iter(app.world())
            .map(|controller| controller.translation)
            .collect::<Vec<_>>();
        assert!(!requested_motion.is_empty());
        assert!(requested_motion.iter().all(Option::is_none));
        let state = app
            .world()
            .resource::<GameplayRuntime>()
            .session()
            .state()
            .clone();
        app.update();
        assert_eq!(
            app.world().resource::<GameplayRuntime>().session().state(),
            &state
        );
        app.world_mut().resource_mut::<TestSpecies>().0 = Some(true);
        ready(&mut app);
        save(&mut app);
        let committed = fs::read(&path).unwrap();

        // A disappeared species publisher cannot reuse its former positive report.
        app.world_mut().resource_mut::<TestSpecies>().0 = None;
        app.update();
        assert_held_commands_preserve_save(&mut app, &path, &committed);
        app.world_mut().resource_mut::<TestSpecies>().0 = Some(true);
        ready(&mut app);

        // Species ready must not reopen a real missing terrain collider.
        let ground = app
            .world_mut()
            .query::<(Entity, &StreamedTerrainCell)>()
            .iter(app.world())
            .find(|(_, cell)| cell.key.x == 4 && cell.key.z == 4)
            .map(|(entity, _)| entity)
            .unwrap();
        let collider = app.world().get::<Collider>(ground).unwrap().clone();
        app.world_mut().entity_mut(ground).remove::<Collider>();
        app.update();
        assert_held_commands_preserve_save(&mut app, &path, &committed);
        app.world_mut().entity_mut(ground).insert(collider);
        ready_after_terrain_repair(&mut app);

        // Use the actual temporary hold implementation employed by graphics.
        app.world_mut().resource_mut::<TestGraphicsHold>().blocked = true;
        app.update();
        assert_held_commands_preserve_save(&mut app, &path, &committed);
        app.world_mut().resource_mut::<TestGraphicsHold>().blocked = false;
        ready(&mut app);
        app.world_mut().resource_mut::<NativeGameplayUi>().recovery =
            Some("Refused resume fixture".into());
        app.update();
        assert_held_commands_preserve_save(&mut app, &path, &committed);
        app.world_mut().resource_mut::<NativeGameplayUi>().recovery = None;
        ready(&mut app);
    }
}

#[test]
fn streamed_pause_discards_old_input_but_preserves_new_ready_commands() {
    let directory = Directory::new();
    let (mut app, zone, _) = app(&directory);
    install_composition(&mut app, &zone, true);
    app.world_mut().resource_mut::<TestSpecies>().0 = Some(true);
    ready(&mut app);

    app.world_mut().resource_mut::<TestSpecies>().0 = Some(false);
    app.update();
    assert!(!app.world().resource::<PlayerSimulationReady>().0);
    let state = app
        .world()
        .resource::<GameplayRuntime>()
        .session()
        .state()
        .clone();
    app.world_mut().run_schedule(FixedUpdate);
    app.world_mut().run_schedule(FixedUpdate);
    assert_eq!(
        app.world().resource::<GameplayRuntime>().session().state(),
        &state
    );
    assert!(
        app.world_mut()
            .query::<&KinematicCharacterController>()
            .iter(app.world())
            .all(|controller| controller.translation.is_none())
    );
    let paused = app
        .world_mut()
        .resource_mut::<crate::PendingPlayerControlCommand>()
        .submit(crate::PlayerControlCommand {
            move_axis: Vec2::Y,
            jump: true,
            ..default()
        })
        .unwrap();
    // Simulate a PreUpdate owner briefly releasing the shared boolean in a
    // rendered frame that contains no fixed simulation step.
    app.world_mut().run_schedule(PreUpdate);
    app.world_mut().resource_mut::<PlayerSimulationReady>().0 = true;
    app.world_mut().run_schedule(PostUpdate);
    assert_eq!(
        app.world_mut()
            .resource_mut::<crate::PendingPlayerControlCommand>()
            .drain_tick()
            .0,
        None
    );
    app.world_mut().resource_mut::<TestSpecies>().0 = Some(true);
    ready(&mut app);
    assert_ne!(
        app.world()
            .resource::<PlayerRuntimeSnapshot>()
            .last_applied_sequence,
        Some(paused)
    );
    let fresh = app
        .world_mut()
        .resource_mut::<crate::PendingPlayerControlCommand>()
        .submit(crate::PlayerControlCommand {
            move_axis: Vec2::X,
            ..default()
        })
        .unwrap();
    app.update();
    assert_eq!(
        app.world()
            .resource::<PlayerRuntimeSnapshot>()
            .last_applied_sequence,
        Some(fresh)
    );
}

#[test]
fn streamed_native_gameplay_refuses_save_and_actions_while_ground_is_missing_then_recovers() {
    let directory = Directory::new();
    let (mut app, zone, path) = app(&directory);
    let archive = zone.archive_bytes().unwrap();
    ready(&mut app);
    save(&mut app);
    let committed = fs::read(&path).unwrap();
    let ground = app
        .world_mut()
        .query::<(Entity, &StreamedTerrainCell)>()
        .iter(app.world())
        .find(|(_, cell)| cell.key.x == 4 && cell.key.z == 4)
        .map(|(entity, _)| entity)
        .unwrap();
    let collider = app.world().get::<Collider>(ground).unwrap().clone();
    app.world_mut().entity_mut(ground).remove::<Collider>();
    app.update();
    assert_held_commands_preserve_save(&mut app, &path, &committed);
    app.world_mut().entity_mut(ground).insert(collider);
    ready_after_terrain_repair(&mut app);
    save(&mut app);
    assert_ne!(fs::read(&path).unwrap(), committed);
    assert_eq!(zone.archive_bytes().unwrap(), archive);
}

#[test]
fn streamed_native_resume_recovery_keeps_gameplay_and_saves_held_despite_ready_ground() {
    let directory = Directory::new();
    let (mut app, zone, path) = app(&directory);
    let archive = zone.archive_bytes().unwrap();
    ready(&mut app);
    save(&mut app);
    let committed = fs::read(&path).unwrap();
    app.world_mut().resource_mut::<NativeGameplayUi>().recovery =
        Some("Refused resume fixture".into());
    app.update();
    assert_held_commands_preserve_save(&mut app, &path, &committed);
    app.world_mut().resource_mut::<NativeGameplayUi>().recovery = None;
    ready(&mut app);
    assert_eq!(zone.archive_bytes().unwrap(), archive);
}

#[test]
fn streamed_native_rejects_a_replaced_ground_shape_even_after_backend_synchronization() {
    let directory = Directory::new();
    let (mut app, zone, path) = app(&directory);
    let archive = zone.archive_bytes().unwrap();
    ready(&mut app);
    save(&mut app);
    let committed = fs::read(&path).unwrap();
    let ground = app
        .world_mut()
        .query::<(Entity, &StreamedTerrainCell)>()
        .iter(app.world())
        .find(|(_, cell)| cell.key.x == 4 && cell.key.z == 4)
        .map(|(entity, _)| entity)
        .unwrap();
    let original = app.world().get::<Collider>(ground).unwrap().clone();
    app.world_mut()
        .entity_mut(ground)
        .insert(Collider::ball(0.05));
    // Let Rapier synchronize the replacement. Agreement with that backend alone
    // must not certify that the original terrain geometry is still present.
    for _ in 0..6 {
        app.update();
    }
    assert!(!app.world().resource::<PlayerSimulationReady>().0);
    assert_held_commands_preserve_save(&mut app, &path, &committed);
    app.world_mut().entity_mut(ground).insert(original);
    ready_after_terrain_repair(&mut app);
    assert_eq!(zone.archive_bytes().unwrap(), archive);
}

#[test]
fn streamed_native_rejects_moved_or_reparented_ground_despite_backend_synchronization() {
    for reparent in [false, true] {
        let directory = Directory::new();
        let (mut app, zone, path) = app(&directory);
        let archive = zone.archive_bytes().unwrap();
        ready(&mut app);
        save(&mut app);
        let committed = fs::read(&path).unwrap();
        let ground = app
            .world_mut()
            .query::<(Entity, &StreamedTerrainCell)>()
            .iter(app.world())
            .find(|(_, cell)| cell.key.x == 4 && cell.key.z == 4)
            .map(|(entity, _)| entity)
            .unwrap();
        if reparent {
            let parent = app
                .world_mut()
                .spawn(Transform::from_translation(Vec3::X * 0.25))
                .id();
            app.world_mut().entity_mut(ground).insert(ChildOf(parent));
        } else {
            app.world_mut()
                .entity_mut(ground)
                .insert(Transform::from_translation(Vec3::X * 0.25));
        }
        for _ in 0..6 {
            app.update();
        }
        assert!(!app.world().resource::<PlayerSimulationReady>().0);
        assert_held_commands_preserve_save(&mut app, &path, &committed);
        app.world_mut()
            .entity_mut(ground)
            .remove::<ChildOf>()
            .insert(Transform::IDENTITY);
        ready_after_terrain_repair(&mut app);
        assert_eq!(zone.archive_bytes().unwrap(), archive);
    }
}

#[derive(Resource, Default)]
struct InvalidMotion(bool);

#[allow(clippy::needless_pass_by_value, reason = "Bevy system parameters")]
fn late_motion(
    invalid: Res<InvalidMotion>,
    mut controllers: Query<&mut KinematicCharacterController>,
) {
    if invalid.0 {
        for mut controller in &mut controllers {
            controller.translation = Some(Vec3::X * 100.0);
        }
    }
}

#[test]
fn streamed_native_late_motion_refusal_prevents_same_tick_gameplay_and_save() {
    let directory = Directory::new();
    let (mut app, zone, path) = app(&directory);
    let archive = zone.archive_bytes().unwrap();
    app.init_resource::<InvalidMotion>().add_systems(
        FixedUpdate,
        late_motion
            .after(GameplaySet::NpcDrive)
            .before(PlayerCellStreamingSet::Enforce),
    );
    ready(&mut app);
    save(&mut app);
    let committed = fs::read(&path).unwrap();
    app.world_mut().resource_mut::<InvalidMotion>().0 = true;
    app.update();
    assert_held_commands_preserve_save(&mut app, &path, &committed);
    app.world_mut().resource_mut::<InvalidMotion>().0 = false;
    ready(&mut app);
    assert_eq!(zone.archive_bytes().unwrap(), archive);
}
