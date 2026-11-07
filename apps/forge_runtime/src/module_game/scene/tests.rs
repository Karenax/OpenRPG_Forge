#![allow(
    clippy::float_cmp,
    reason = "These tests require exactly no movement after input is released."
)]

use super::*;
use bevy::input::{
    ButtonState, InputPlugin,
    keyboard::{Key, KeyboardFocusLost},
};
use bevy::time::TimeUpdateStrategy;
use bevy_rapier3d::prelude::{
    KinematicCharacterControllerOutput, NoUserData, RapierPhysicsPlugin, TimestepMode,
};
use forge_engine::{
    ForgePlayerCharacter, PlayerCameraMode, PlayerControllerPlugin, PlayerRuntimeSnapshot,
};
use std::time::Duration;

#[test]
fn module_f11_respects_focus_menus_and_explicit_player_priority() {
    use bevy::window::{MonitorSelection, WindowMode};
    use forge_engine::native_display::{DisplayOutcome, InitialDisplayStatus};
    use forge_project::{VisualQualityTier, workspace::launcher::DisplayMode};

    assert!(allow_module_window_toggle(true, false, false, false));
    assert!(!allow_module_window_toggle(false, false, false, false));
    assert!(!allow_module_window_toggle(true, true, false, false));
    assert!(!allow_module_window_toggle(true, false, true, false));
    assert!(!allow_module_window_toggle(true, false, false, true));

    let mut app = App::new();
    app.init_resource::<ButtonInput<KeyCode>>()
        .insert_resource(super::super::graphics::GraphicsUi::new(
            VisualQualityTier::High,
            false,
        ))
        .insert_resource(InitialDisplayStatus::new(DisplayMode::Fullscreen))
        .add_systems(Update, toggle_module_window);
    let window = app
        .world_mut()
        .spawn((Window::default(), PrimaryWindow))
        .id();
    let press = |app: &mut App, keys: &[KeyCode]| {
        {
            let mut input = app.world_mut().resource_mut::<ButtonInput<KeyCode>>();
            input.reset_all();
            for key in keys {
                input.press(*key);
            }
        }
        app.update();
    };

    app.world_mut().get_mut::<Window>(window).unwrap().focused = false;
    press(&mut app, &[KeyCode::F11]);
    assert_eq!(
        app.world().get::<Window>(window).unwrap().mode,
        WindowMode::Windowed
    );
    app.world_mut().get_mut::<Window>(window).unwrap().focused = true;
    app.world_mut()
        .resource_mut::<super::super::graphics::GraphicsUi>()
        .open = true;
    press(&mut app, &[KeyCode::F11]);
    assert_eq!(
        app.world().get::<Window>(window).unwrap().mode,
        WindowMode::Windowed
    );
    app.world_mut()
        .resource_mut::<super::super::graphics::GraphicsUi>()
        .open = false;
    press(&mut app, &[KeyCode::F11, KeyCode::F2]);
    assert_eq!(
        app.world().get::<Window>(window).unwrap().mode,
        WindowMode::Windowed
    );
    press(&mut app, &[KeyCode::F11]);
    assert_eq!(
        app.world().get::<Window>(window).unwrap().mode,
        WindowMode::BorderlessFullscreen(MonitorSelection::Primary)
    );
    assert_eq!(
        app.world().resource::<InitialDisplayStatus>().outcome,
        DisplayOutcome::SupersededByPlayer
    );
    press(&mut app, &[KeyCode::F11]);
    assert_eq!(
        app.world().get::<Window>(window).unwrap().mode,
        WindowMode::Windowed
    );
}

#[test]
fn early_asset_refusal_survives_the_runner_without_gameplay_evidence() {
    let mut app = App::new();
    app.init_resource::<super::super::manual::ModuleAssetFailure>()
        .add_systems(Update, |world: &mut World| {
            super::super::manual::refuse(world, "Original asset refusal".into());
        });
    let reason = run_app(app, None, false).unwrap_err();
    assert_eq!(
        reason,
        "Native module asset refused: Original asset refusal"
    );
}

#[test]
fn static_render_control_is_explicit_and_rejects_unknown_values() {
    assert!(static_hlod_settings(None).unwrap().enabled);
    assert!(
        static_hlod_settings(Some("hierarchy".into()))
            .unwrap()
            .enabled
    );
    assert!(!static_hlod_settings(Some("detail".into())).unwrap().enabled);
    assert!(static_hlod_settings(Some("automatic".into())).is_err());
}

fn vegetation_guard_imported_source(
    layer: &forge_project::VegetationLayerId,
    extent: forge_project::VegetationExtent,
) -> forge_project::imported_vegetation::ImportedVegetationSource {
    use forge_assets::source_vegetation_package::{packing, profile::PARAMETERS};
    use forge_core::bytes_sha256;
    use forge_project::imported_vegetation::{
        ImportedInstanceEncoding, ImportedVegetationBlock, ImportedVegetationSource,
    };
    let mut bytes = vec![0_u8; 32];
    for value in [
        0.0_f64, 0.0, 0.0, 0.0, 0.0, 0.0, 1.0, 1.0, 1.0, 1.0, 0.0, 0.0, 2.0, 0.5, 1.0,
    ] {
        bytes.extend_from_slice(&value.to_le_bytes());
    }
    let block = ImportedVegetationBlock {
        layer_id: layer.to_string(),
        prototype_key: "tidewater_trees".into(),
        count: 1,
        encoding: ImportedInstanceEncoding::TransformF64LeV1,
        instances_sha256: bytes_sha256(&bytes),
        parameter_names: PARAMETERS.iter().map(|name| (*name).to_owned()).collect(),
    };
    let records = block.decode(&bytes, extent).unwrap();
    assert_eq!(records.len(), 1);
    let record = &records[0];
    assert_eq!(
        packing::pack(
            &block.prototype_key,
            &block.parameter_names,
            record.position_m,
            record.rotation_xyzw,
            record.scale,
            &record.parameters,
        )
        .unwrap()
        .group,
        "canopy"
    );
    ImportedVegetationSource {
        schema: "forge.imported-vegetation".into(),
        version: 1,
        blocks: vec![block],
        grass: None,
    }
}

/// Exercise the scene guard with an actually valid, nonempty authored source.
/// It does not construct a complete installed module or start a renderer.
fn vegetation_guard_head(
    imported: bool,
    selected: bool,
) -> forge_project::zone_authoring::ZoneAuthoringHead {
    use forge_core::bytes_sha256;
    use forge_project::{
        HeightSamplingAlgorithm, TERRAIN_DOCUMENT_SCHEMA_VERSION_V4, TerrainExtent,
        VegetationDocument, VegetationKind,
        sampled_terrain::{HeightEncoding, SAMPLED_TERRAIN_SCHEMA, SampledTerrainSource},
        zone_authoring::{
            session::ZoneEditSession,
            species::{LayerSpecies, SpeciesReference},
        },
    };
    let mut terrain = forge_project::authoring::new_terrain("guard", "area", "Guard").unwrap();
    if imported {
        let raster = vec![0_u8; 512 * 512 * 4];
        terrain.schema_version = TERRAIN_DOCUMENT_SCHEMA_VERSION_V4;
        terrain.extent_m = TerrainExtent::new(512.0, 512.0);
        terrain.height_sampling.algorithm = HeightSamplingAlgorithm::SampledRasterV1;
        terrain.height_sampling.sample_spacing_m = 1.0;
        terrain.height_sampling.base_elevation_m = 0.0;
        terrain.height_sampling.amplitude_m = 0.0;
        terrain.height_sampling.base_frequency_cycles_per_m = 1.0;
        terrain.height_sampling.octaves = 1;
        terrain.height_sampling.lacunarity = 2.0;
        terrain.height_sampling.persistence = 0.5;
        terrain.sampled_source = Some(SampledTerrainSource {
            schema: SAMPLED_TERRAIN_SCHEMA.into(),
            schema_version: 1,
            width: 512,
            height: 512,
            origin_xz_m: [-256.0; 2],
            sample_spacing_m: 1.0,
            outside_height_m: 0.0,
            encoding: HeightEncoding::Float32Le,
            samples_sha256: bytes_sha256(&raster),
        });
    }
    let mut vegetation = VegetationDocument::from_json(include_str!(concat!(
        env!("CARGO_MANIFEST_DIR"),
        "/../../examples/visual_valley/vegetation.v1.json"
    )))
    .unwrap();
    vegetation.project_id = terrain.project_id;
    vegetation.area_id = terrain.area_id;
    vegetation
        .layers
        .retain(|layer| layer.kind == VegetationKind::Tree);
    assert_eq!(vegetation.layers.len(), 1);
    vegetation.layers[0].material_filter = None;
    vegetation.layers[0].density_mask = None;
    vegetation.exclusions.clear();
    let layer = vegetation.layers[0].id.clone();
    if imported {
        vegetation.schema_version = 2;
        vegetation.imported_source = Some(vegetation_guard_imported_source(
            &layer,
            vegetation.extent_m,
        ));
    }
    let mut head = ZoneEditSession::from_initial_terrain(terrain)
        .unwrap()
        .head()
        .clone();
    head.base.vegetation = Some(vegetation);
    if selected {
        head.corrections.species = Some(vec![LayerSpecies {
            layer_id: layer.clone(),
            prototypes: vec![SpeciesReference {
                catalog_sha256: bytes_sha256(b"synthetic pinned species catalogue"),
                source_layer_id: layer,
                asset_id: "qualified-test-tree".into(),
            }],
        }]);
    }
    head.version = head.corrections.schema_version();
    head.validate().unwrap();
    head
}

#[test]
fn vegetation_guard_fixtures_preserve_nonempty_source_invariants() {
    let mut head = vegetation_guard_head(true, true);
    let vegetation = head.base.vegetation.as_ref().unwrap();
    let mut empty_source = vegetation.imported_source.clone().unwrap();
    empty_source.blocks.clear();
    assert!(
        empty_source
            .validate(vegetation.extent_m, &vegetation.layers)
            .is_err()
    );
    let mut zero_count = vegetation.imported_source.clone().unwrap();
    zero_count.blocks[0].count = 0;
    assert!(
        zero_count
            .validate(vegetation.extent_m, &vegetation.layers)
            .is_err()
    );
    head.corrections.species.as_mut().unwrap()[0]
        .prototypes
        .clear();
    assert!(head.validate().is_err());
}

fn test_app() -> App {
    let mut app = App::new();
    app.add_plugins((MinimalPlugins, TransformPlugin))
        .insert_resource(Time::<Fixed>::from_hz(60.0))
        .insert_resource(TimeUpdateStrategy::ManualDuration(Duration::from_secs_f64(
            1.0 / 60.0,
        )))
        .insert_resource(TimestepMode::Fixed {
            dt: 1.0 / 60.0,
            substeps: 1,
        })
        .add_plugins(RapierPhysicsPlugin::<NoUserData>::default().in_fixed_schedule())
        .add_plugins(PlayerControllerPlugin::new(
            forge_world::embedded_visual_valley_player_controller_profile().unwrap(),
            Vec3::Y * 2.0,
        ))
        .insert_resource(PlayerInputFocus(false))
        .init_resource::<ButtonInput<KeyCode>>()
        .init_resource::<ButtonInput<MouseButton>>()
        .init_resource::<bevy::diagnostic::FrameCount>()
        .add_message::<KeyboardInput>()
        .add_message::<MouseButtonInput>();
    install_player_input(&mut app);
    app.world_mut().spawn((
        Window {
            focused: true,
            ..default()
        },
        PrimaryWindow,
        CursorOptions::default(),
    ));
    app.update();
    app
}

fn click_to_play(app: &mut App) {
    app.world_mut()
        .resource_mut::<ButtonInput<MouseButton>>()
        .press(MouseButton::Left);
    app.update();
    app.world_mut()
        .resource_mut::<ButtonInput<MouseButton>>()
        .reset_all();
    assert!(app.world().resource::<PlayerInputFocus>().0);
}

fn queue_keyboard_actions(app: &mut App) {
    let mut keys = app.world_mut().resource_mut::<ButtonInput<KeyCode>>();
    keys.reset_all();
    keys.press(KeyCode::KeyD);
    keys.press(KeyCode::KeyV);
    app.world_mut().run_schedule(PreUpdate);
}

fn snapshot(app: &App) -> PlayerRuntimeSnapshot {
    app.world().resource::<PlayerRuntimeSnapshot>().clone()
}

fn assert_stationary_third_person(app: &App) {
    let observed = snapshot(app);
    assert_eq!(observed.horizontal_velocity, Vec3::ZERO);
    assert_eq!(observed.desired_translation.x, 0.0);
    assert_eq!(observed.camera_mode, PlayerCameraMode::ThirdPerson);
}

#[test]
fn keyboard_controls_survive_os_grab_rollback_after_a_real_input_click() {
    let mut app = test_app();
    queue_keyboard_actions(&mut app);
    app.world_mut().run_schedule(FixedUpdate);
    assert_stationary_third_person(&app);
    app.world_mut()
        .resource_mut::<ButtonInput<KeyCode>>()
        .reset_all();

    click_to_play(&mut app);
    let world = app.world_mut();
    let mut cursor = world
        .query::<&mut CursorOptions>()
        .single_mut(world)
        .unwrap();
    assert_eq!(cursor.grab_mode, CursorGrabMode::Locked);
    assert!(!cursor.visible);
    // Reproduce Bevy's component rollback after a denied native grab. This is
    // a CPU regression, not a claim to reproduce a kernel permission failure.
    cursor.grab_mode = CursorGrabMode::None;
    app.world_mut().run_schedule(Update);
    assert!(app.world().resource::<PlayerInputFocus>().0);

    queue_keyboard_actions(&mut app);
    app.world_mut().run_schedule(FixedUpdate);
    let observed = snapshot(&app);
    assert!(observed.horizontal_velocity.x > 0.0);
    assert!(observed.desired_translation.x > 0.0);
    assert_eq!(observed.camera_mode, PlayerCameraMode::FirstPerson);
    assert!(observed.last_applied_sequence.is_some());
    let world = app.world_mut();
    let output = world
        .query_filtered::<&KinematicCharacterControllerOutput, With<ForgePlayerCharacter>>()
        .single(world)
        .unwrap();
    assert!(output.desired_translation.x > 0.0);
    assert!(output.effective_translation.x > 0.0);
}

fn assert_window_release_cancels_pending(escape: bool) {
    let mut app = test_app();
    click_to_play(&mut app);
    let before = snapshot(&app);
    // The real Main schedule must sample D/V before its fixed loop. Do not
    // arrange Update before FixedUpdate: that would conceal the release race.
    let mut keys = app.world_mut().resource_mut::<ButtonInput<KeyCode>>();
    keys.reset_all();
    keys.press(KeyCode::KeyD);
    keys.press(KeyCode::KeyV);
    if escape {
        app.world_mut()
            .resource_mut::<ButtonInput<KeyCode>>()
            .press(KeyCode::Escape);
    } else {
        let world = app.world_mut();
        world
            .query::<&mut Window>()
            .single_mut(world)
            .unwrap()
            .focused = false;
    }
    app.update();
    assert!(!app.world().resource::<PlayerInputFocus>().0);
    assert_stationary_third_person(&app);
    let after = snapshot(&app);
    assert!(
        after.fixed_tick > before.fixed_tick,
        "Main must actually run a fixed tick"
    );
    assert_eq!(after.last_applied_sequence, before.last_applied_sequence);

    app.world_mut()
        .resource_mut::<ButtonInput<KeyCode>>()
        .reset_all();
    let world = app.world_mut();
    world
        .query::<&mut Window>()
        .single_mut(world)
        .unwrap()
        .focused = true;
    app.update();
    assert!(!app.world().resource::<PlayerInputFocus>().0);
    queue_keyboard_actions(&mut app);
    app.world_mut().run_schedule(FixedUpdate);
    assert_stationary_third_person(&app);
    click_to_play(&mut app);
    queue_keyboard_actions(&mut app);
    app.world_mut().run_schedule(FixedUpdate);
    assert!(snapshot(&app).desired_translation.x > 0.0);
    assert_eq!(snapshot(&app).camera_mode, PlayerCameraMode::FirstPerson);
}

#[test]
fn escape_cancels_queued_actions_and_requires_a_new_click() {
    assert_window_release_cancels_pending(true);
}

#[test]
fn graphics_open_close_and_f2_edges_cancel_the_actual_controller() {
    for barrier in 0..3 {
        let mut app = test_app();
        click_to_play(&mut app);
        queue_keyboard_actions(&mut app);
        let before = snapshot(&app).last_applied_sequence;
        let mut ui = crate::module_game::graphics::GraphicsUi::new(
            forge_project::VisualQualityTier::High,
            barrier == 0,
        );
        ui.consumed_pointer = barrier == 1;
        app.insert_resource(ui);
        if barrier == 2 {
            app.world_mut()
                .resource_mut::<ButtonInput<KeyCode>>()
                .press(KeyCode::F2);
        }
        app.update();
        assert_stationary_third_person(&app);
        assert!(!app.world().resource::<PlayerInputFocus>().0);
        assert_eq!(snapshot(&app).last_applied_sequence, before);
        let mut ui = app
            .world_mut()
            .resource_mut::<crate::module_game::graphics::GraphicsUi>();
        ui.open = false;
        ui.consumed_pointer = false;
        app.world_mut()
            .resource_mut::<ButtonInput<KeyCode>>()
            .reset_all();
        app.update();
        assert!(!app.world().resource::<PlayerInputFocus>().0);
        click_to_play(&mut app);
    }
}

#[test]
fn losing_window_focus_cancels_queued_actions_and_requires_a_new_click() {
    assert_window_release_cancels_pending(false);
}

#[test]
fn open_ui_cancels_queued_actions_and_closing_it_does_not_reacquire_control() {
    let mut app = test_app();
    click_to_play(&mut app);
    queue_keyboard_actions(&mut app);
    let before = snapshot(&app).last_applied_sequence;
    let mut requested = true;
    // The production system maps NativeGameplayUi::is_open() to this same
    // blocked transition, after the G handler has updated the actual panel.
    let focus = update_control_intent(
        &mut requested,
        true,
        true,
        &mut app
            .world_mut()
            .resource_mut::<PendingPlayerControlCommand>(),
    );
    app.world_mut().resource_mut::<PlayerInputFocus>().0 = focus;
    assert!(!focus && !requested);
    app.world_mut().run_schedule(FixedUpdate);
    assert_stationary_third_person(&app);
    assert_eq!(snapshot(&app).last_applied_sequence, before);
    let focus = update_control_intent(
        &mut requested,
        false,
        false,
        &mut app
            .world_mut()
            .resource_mut::<PendingPlayerControlCommand>(),
    );
    app.world_mut().resource_mut::<PlayerInputFocus>().0 = focus;
    queue_keyboard_actions(&mut app);
    app.world_mut().run_schedule(FixedUpdate);
    assert!(!focus);
    assert_stationary_third_person(&app);
    let focus = update_control_intent(
        &mut requested,
        false,
        true,
        &mut app
            .world_mut()
            .resource_mut::<PendingPlayerControlCommand>(),
    );
    app.world_mut().resource_mut::<PlayerInputFocus>().0 = focus;
    queue_keyboard_actions(&mut app);
    app.world_mut().run_schedule(FixedUpdate);
    assert!(snapshot(&app).desired_translation.x > 0.0);
    assert_eq!(snapshot(&app).camera_mode, PlayerCameraMode::FirstPerson);
}

fn input_event_app() -> App {
    let mut app = test_app();
    app.add_plugins(InputPlugin);
    let window = primary_window(&mut app);
    app.world_mut().write_message(MouseButtonInput {
        button: MouseButton::Left,
        state: ButtonState::Pressed,
        window,
    });
    app.update();
    app.world_mut().write_message(MouseButtonInput {
        button: MouseButton::Left,
        state: ButtonState::Released,
        window,
    });
    app.update();
    assert!(app.world().resource::<PlayerInputFocus>().0);
    app
}

fn primary_window(app: &mut App) -> Entity {
    let world = app.world_mut();
    world
        .query_filtered::<Entity, With<PrimaryWindow>>()
        .single(world)
        .unwrap()
}

fn forward_event(app: &mut App, state: ButtonState) {
    let window = primary_window(app);
    // Physical KeyW is labelled Z on AZERTY. The sampler consumes the physical
    // code; no repeated press, resource press(), or scenario injection follows.
    app.world_mut().write_message(KeyboardInput {
        key_code: KeyCode::KeyW,
        logical_key: Key::Character("z".into()),
        state,
        text: None,
        repeat: false,
        window,
    });
}

#[test]
fn one_keyboard_event_keeps_walking_across_main_frames_until_release() {
    let mut app = input_event_app();
    let world = app.world_mut();
    world
        .query::<&mut CursorOptions>()
        .single_mut(world)
        .unwrap()
        .grab_mode = CursorGrabMode::None;
    let start = snapshot(&app);
    forward_event(&mut app, ButtonState::Pressed);
    for _ in 0..60 {
        let before = snapshot(&app);
        app.update();
        let after = snapshot(&app);
        assert!(after.fixed_tick > before.fixed_tick);
        assert!(after.player_position.z < before.player_position.z);
        assert!(after.desired_translation.z < 0.0);
        assert!(after.effective_translation.z < 0.0);
        assert!(
            app.world()
                .resource::<ButtonInput<KeyCode>>()
                .pressed(KeyCode::KeyW)
        );
        assert!(app.world().resource::<PlayerInputFocus>().0);
    }
    assert!(start.player_position.z - snapshot(&app).player_position.z > 1.0);
    forward_event(&mut app, ButtonState::Released);
    for _ in 0..30 {
        app.update();
    }
    assert!(
        !app.world()
            .resource::<ButtonInput<KeyCode>>()
            .pressed(KeyCode::KeyW)
    );
    assert_eq!(snapshot(&app).horizontal_velocity, Vec3::ZERO);
    let stopped = snapshot(&app).player_position.z;
    for _ in 0..10 {
        app.update();
    }
    assert_eq!(snapshot(&app).player_position.z, stopped);
}

#[test]
fn keyboard_focus_loss_releases_a_held_event_and_requires_a_new_click() {
    let mut app = input_event_app();
    forward_event(&mut app, ButtonState::Pressed);
    for _ in 0..15 {
        app.update();
    }
    assert!(snapshot(&app).horizontal_velocity.length() > 0.0);
    let world = app.world_mut();
    world
        .query::<&mut Window>()
        .single_mut(world)
        .unwrap()
        .focused = false;
    app.world_mut().write_message(KeyboardFocusLost);
    app.update();
    assert!(
        !app.world()
            .resource::<ButtonInput<KeyCode>>()
            .pressed(KeyCode::KeyW)
    );
    assert!(!app.world().resource::<PlayerInputFocus>().0);
    // The ordinary controller brakes; cancelling input does not teleport it.
    for _ in 0..30 {
        app.update();
    }
    assert_eq!(snapshot(&app).horizontal_velocity, Vec3::ZERO);
    let stopped = snapshot(&app).player_position.z;
    let world = app.world_mut();
    world
        .query::<&mut Window>()
        .single_mut(world)
        .unwrap()
        .focused = true;
    forward_event(&mut app, ButtonState::Pressed);
    for _ in 0..10 {
        app.update();
        assert!(!app.world().resource::<PlayerInputFocus>().0);
        assert_eq!(snapshot(&app).player_position.z, stopped);
    }
    let window = primary_window(&mut app);
    app.world_mut().write_message(MouseButtonInput {
        button: MouseButton::Left,
        state: ButtonState::Pressed,
        window,
    });
    app.update();
    app.update();
    assert!(app.world().resource::<PlayerInputFocus>().0);
    assert!(snapshot(&app).player_position.z < stopped);
}

#[test]
fn input_trace_cadence_ignores_unchanged_frames_and_stops_at_its_total_limit() {
    let state = InputTraceState {
        flags: InputTraceFlags::default(),
        ready: true,
        held_keys: 1,
        left_mouse: false,
    };
    let mut history = InputTraceHistory::default();
    assert!(history.should_record(state, 0.0, false));
    for frame in 1..=100 {
        history.should_record(state, f64::from(frame) / 100.0, false);
    }
    assert!(
        (4..=5).contains(&history.records),
        "held input must stay at most 4 Hz after its initial record"
    );
    let idle = InputTraceState {
        held_keys: 0,
        ..state
    };
    assert!(history.should_record(idle, 1.01, true));
    assert!(!history.should_record(idle, 100.0, false));
    let mut limited = InputTraceHistory::default();
    for index in 0..INPUT_TRACE_MAX_RECORDS {
        assert!(limited.should_record(state, f64::from(index), true));
    }
    assert!(!limited.should_record(idle, 1000.0, true));
    assert_eq!(limited.records, INPUT_TRACE_MAX_RECORDS);
}

fn spawn_gameplay(area: forge_core::AreaId) -> forge_gameplay::GameplayDocument {
    let player = forge_core::EntityId::deterministic("spawn/player");
    serde_json::from_value(serde_json::json!({
        "schema":forge_gameplay::GAMEPLAY_SCHEMA,"version":forge_gameplay::GAMEPLAY_VERSION,
        "module_id":forge_core::ModuleId::deterministic("spawn/module"),"area_id":area,
        "player":player,"profile":{"id":"forge.gate3-minimal","version":1,
            "interaction_range_m":3.0,"attack_range_m":2.0,"attack_damage":20,
            "attack_stamina_cost":5,"max_stamina":100,"max_action_receipts":512,
            "max_events_per_tick":128},
        "actors":[{"id":player,"name":"Player","role":"player",
            "position":{"x":0.5,"y":2.0,"z":0.5},"max_vitality":100,
            "protection":0,"inventory_capacity":12,"dialogue":null,"patrol":[],
            "flees_danger":false}],
        "objects":[],"items":[],"places":[],"dialogues":[],"quests":[]
    }))
    .unwrap()
}

#[test]
fn authored_start_projects_on_retouches_without_rewriting_the_rpg_player() {
    use forge_project::{
        HeightSamplingAlgorithm, TERRAIN_DOCUMENT_SCHEMA_VERSION_V4, TerrainExtent,
        TerrainHeightDelta,
        sampled_terrain::{HeightEncoding, SAMPLED_TERRAIN_SCHEMA, SampledTerrainSource},
        zone_authoring::{session::ZoneEditSession, start::ZoneStart},
    };
    let mut terrain = forge_project::authoring::new_terrain("spawn", "raster", "Raster").unwrap();
    let bytes: Vec<_> = (0..4096).flat_map(|_| 7.0_f32.to_le_bytes()).collect();
    terrain.schema_version = TERRAIN_DOCUMENT_SCHEMA_VERSION_V4;
    terrain.extent_m = TerrainExtent::new(64.0, 64.0);
    terrain.height_sampling.algorithm = HeightSamplingAlgorithm::SampledRasterV1;
    terrain.height_sampling.sample_spacing_m = 1.0;
    terrain.height_sampling.base_elevation_m = 0.0;
    terrain.height_sampling.amplitude_m = 0.0;
    terrain.height_sampling.base_frequency_cycles_per_m = 1.0;
    terrain.height_sampling.octaves = 1;
    terrain.height_sampling.lacunarity = 2.0;
    terrain.height_sampling.persistence = 0.5;
    terrain.sampled_source = Some(SampledTerrainSource {
        schema: SAMPLED_TERRAIN_SCHEMA.into(),
        schema_version: 1,
        width: 64,
        height: 64,
        origin_xz_m: [-32.0; 2],
        sample_spacing_m: 1.0,
        outside_height_m: -90.0,
        encoding: HeightEncoding::Float32Le,
        samples_sha256: forge_core::bytes_sha256(&bytes),
    });
    let zone = ZoneEditSession::from_initial_terrain(terrain).unwrap();
    let mut head = zone.head().clone();
    let document = spawn_gameplay(head.base.terrain.area_id);
    document.validate().unwrap();
    head.corrections.gameplay = Some(Box::new(document.clone()));
    head.corrections.height_deltas = Some(vec![TerrainHeightDelta::new(32, 32, 4.0)]);
    head.corrections.start = Some(Box::new(ZoneStart {
        x_m: 0.0,
        z_m: 0.0,
        facing_degrees: Some(90.0),
    }));
    head.version = head.corrections.schema_version();
    let (terrain, _) = head.effective().unwrap();
    let sampled = super::super::streamed::Prepared {
        policy: super::super::streamed::policy(terrain.sampled_source.as_ref().unwrap()).unwrap(),
        plan: std::sync::Arc::new(
            forge_world::sampled_terrain::SampledTerrainPlan::from_document(&terrain, &bytes)
                .unwrap(),
        ),
    };
    let profile = forge_world::embedded_visual_valley_player_controller_profile().unwrap();
    let before = head.content_hash().unwrap();
    assert!(initial_spawn(&head, None, None, &profile).is_err());
    let spawn = initial_spawn(&head, Some(&sampled), None, &profile).unwrap();
    assert_eq!(spawn.x.to_bits(), 0.0_f32.to_bits());
    assert_eq!(spawn.z.to_bits(), 0.0_f32.to_bits());
    assert!(
        (spawn.y - (11.0 + profile.controller.total_capsule_height_m() * 0.5 + 0.05)).abs() < 0.001
    );
    assert_eq!(head.content_hash().unwrap(), before);
    assert_eq!(head.corrections.gameplay.as_deref(), Some(&document));
    head.corrections.start = None;
    let fallback = initial_spawn(&head, Some(&sampled), None, &profile).unwrap();
    assert_eq!(fallback, Vec3::new(0.5, 2.0, 0.5));
}
