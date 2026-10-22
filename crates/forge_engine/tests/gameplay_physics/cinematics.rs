use super::*;
use forge_gameplay::{
    CinematicCamera, CinematicCue, CinematicDefinition, NarrativeDefinition, NarrativeValue,
    NarrativeVariable, ScriptDefinition, ScriptInstruction, ScriptOperation, ScriptPermissions,
};
use std::collections::BTreeSet;

fn document() -> GameplayDocument {
    player_save_fixture()
        .with_narrative_definition(Some(NarrativeDefinition {
            version: 1,
            variables: vec![NarrativeVariable {
                id: id("presentation/flag"),
                name: "Flag".into(),
                initial: NarrativeValue::Boolean(false),
            }],
            quests: vec![],
        }))
        .unwrap()
        .with_script_definition(ScriptDefinition {
            id: id("presentation/script"),
            name: "Remember arrival".into(),
            permissions: ScriptPermissions {
                write_variables: BTreeSet::from([id("presentation/flag")]),
                ..ScriptPermissions::default()
            },
            steps: vec![
                ScriptInstruction {
                    id: id("presentation/write"),
                    operation: ScriptOperation::SetVariable {
                        variable: id("presentation/flag"),
                        value: NarrativeValue::Boolean(true),
                    },
                },
                ScriptInstruction {
                    id: id("presentation/message"),
                    operation: ScriptOperation::Message {
                        text: "Arrival remembered".into(),
                    },
                },
            ],
        })
        .unwrap()
        .with_cinematic_definition(CinematicDefinition {
            id: id("presentation/sequence"),
            name: "Arrival".into(),
            cues: vec![CinematicCue {
                id: id("presentation/cue"),
                duration_ms: 2000,
                speaker: None,
                text: Some("Arrival".into()),
                camera: Some(CinematicCamera {
                    eye: CoreVec3::new(0.0, 3.0, 0.0),
                    target: CoreVec3::new(0.0, 3.0, -4.0),
                    vertical_fov_degrees: 45.0,
                }),
            }],
        })
        .unwrap()
}

fn request(app: &App, action: GameAction, name: &str) -> GameRequest {
    let runtime = app.world().resource::<GameplayRuntime>();
    GameRequest {
        command_id: CommandId::deterministic(name),
        expected_sequence: runtime.session().state().sequence,
        actor: runtime.session().document().player,
        action,
    }
}

fn queue(app: &mut App, action: GameAction, name: &str) -> GameRequest {
    let request = request(app, action, name);
    app.world_mut()
        .resource_mut::<GameplayRuntime>()
        .queue(request.clone())
        .unwrap();
    drain_actions(app);
    request
}

fn drain_actions(app: &mut App) {
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
    panic!("presentation action queue did not finish");
}

fn camera(app: &mut App) -> Entity {
    app.world_mut()
        .query_filtered::<Entity, With<forge_engine::ForgePlayerCamera>>()
        .single(app.world())
        .unwrap()
}

fn fov(app: &App, camera: Entity) -> f32 {
    let Projection::Perspective(settings) = app.world().get::<Projection>(camera).unwrap() else {
        panic!("player camera must remain perspective");
    };
    settings.fov
}

#[test]
fn presentation_camera_input_and_script_outputs_survive_real_physical_resume() {
    let source = document();
    let hash = ContentHash::from_bytes([0x91; 32]);
    let mut live = app(source.clone(), hash);
    wait_for_player_saveable(&mut live);
    let camera = camera(&mut live);
    let original_fov = fov(&live, camera);
    run_script_once(&mut live);
    queue(
        &mut live,
        GameAction::StartCinematic {
            sequence: id("presentation/sequence"),
        },
        "presentation/physical-start",
    );
    for _ in 0..2 {
        live.update();
    }
    assert_eq!(
        live.world().get::<Transform>(camera).unwrap().translation,
        Vec3::new(0.0, 3.0, 0.0)
    );
    assert!((fov(&live, camera) - 45.0_f32.to_radians()).abs() < 1.0e-6);
    assert_control_is_suspended(&mut live);
    let runtime = live.world().resource::<GameplayRuntime>();
    assert!(runtime.error().is_none(), "{:?}", runtime.error());
    let save = runtime.save_bytes().unwrap();
    let mut resumed = app_with_save(source.clone(), hash, Some(&save));
    wait_for_player_saveable(&mut resumed);
    let resumed_camera = self::camera(&mut resumed);
    assert!(
        resumed
            .world()
            .resource::<GameplayRuntime>()
            .session()
            .cinematic_controls_player()
    );
    assert!((fov(&resumed, resumed_camera) - 45.0_f32.to_radians()).abs() < 1.0e-6);
    finish_cinematic(&mut resumed, resumed_camera, original_fov, &source);
}

fn run_script_once(live: &mut App) {
    let script = request(
        live,
        GameAction::RunScript {
            script: id("presentation/script"),
        },
        "presentation/physical-script",
    );
    {
        let mut runtime = live.world_mut().resource_mut::<GameplayRuntime>();
        runtime.queue(script.clone()).unwrap();
        runtime.queue(script).unwrap();
    }
    drain_actions(live);
    let runtime = live.world().resource::<GameplayRuntime>();
    assert!(!runtime.has_queued_actions());
    assert_eq!(runtime.session().state().sequence, 1);
    assert_eq!(runtime.session().state().receipts.len(), 1);
    assert_eq!(
        runtime
            .session()
            .state()
            .narrative
            .as_ref()
            .unwrap()
            .variables[&id("presentation/flag")],
        NarrativeValue::Boolean(true)
    );
    assert_eq!(
        runtime
            .last_events()
            .iter()
            .filter(|event| matches!(event, GameplayEvent::ScriptMessage { .. }))
            .count(),
        1
    );
}

fn assert_control_is_suspended(live: &mut App) {
    live.world_mut()
        .resource_mut::<PendingPlayerControlCommand>()
        .submit(PlayerControlCommand {
            move_axis: Vec2::Y,
            sprint: true,
            jump: true,
            look_delta: Vec2::ONE,
            toggle_camera: true,
        })
        .unwrap();
    live.update();
    let (pending_sequence, input) = live
        .world_mut()
        .resource_mut::<PendingPlayerControlCommand>()
        .drain_tick();
    assert!(pending_sequence.is_none());
    assert_eq!(input.move_axis, Vec2::ZERO);
    assert!(!input.jump && !input.toggle_camera);
    assert_eq!(input.look_delta, Vec2::ZERO);
}

fn finish_cinematic(
    app: &mut App,
    app_camera: Entity,
    original_fov: f32,
    source: &GameplayDocument,
) {
    for _ in 0..150 {
        app.update();
        if !app
            .world()
            .resource::<GameplayRuntime>()
            .session()
            .cinematic_controls_player()
        {
            break;
        }
    }
    let runtime = app.world().resource::<GameplayRuntime>();
    assert!(runtime.error().is_none(), "{:?}", runtime.error());
    assert!(!runtime.session().cinematic_controls_player());
    assert_eq!(runtime.session().state().sequence, 2);
    assert_eq!(
        runtime
            .session()
            .state()
            .narrative
            .as_ref()
            .unwrap()
            .variables[&id("presentation/flag")],
        NarrativeValue::Boolean(true)
    );
    assert_eq!(runtime.session().document(), source);
    assert!((fov(app, app_camera) - original_fov).abs() < 1.0e-6);
    assert_ne!(
        app.world()
            .get::<Transform>(app_camera)
            .unwrap()
            .translation,
        Vec3::new(0.0, 3.0, 0.0)
    );
    app.world_mut()
        .resource_mut::<PendingPlayerControlCommand>()
        .submit(PlayerControlCommand {
            move_axis: Vec2::Y,
            ..PlayerControlCommand::default()
        })
        .unwrap();
    for _ in 0..4 {
        app.update();
    }
    assert!(
        app.world()
            .resource::<PlayerRuntimeSnapshot>()
            .horizontal_velocity
            .length()
            > 0.0
    );
}

#[test]
fn presentation_skip_and_interrupt_restore_real_camera_and_player_motion() {
    for stop in [GameAction::SkipCinematic, GameAction::InterruptCinematic] {
        let source = document();
        let mut live = app(source.clone(), ContentHash::from_bytes([0x92; 32]));
        wait_for_player_saveable(&mut live);
        let camera = camera(&mut live);
        let original_fov = fov(&live, camera);
        queue(
            &mut live,
            GameAction::StartCinematic {
                sequence: id("presentation/sequence"),
            },
            "presentation/physical-interrupt-start",
        );
        for _ in 0..2 {
            live.update();
        }
        assert_eq!(
            live.world().get::<Transform>(camera).unwrap().translation,
            Vec3::new(0.0, 3.0, 0.0)
        );
        assert_control_is_suspended(&mut live);
        queue(&mut live, stop, "presentation/physical-stop");
        let runtime = live.world().resource::<GameplayRuntime>();
        assert!(runtime.error().is_none(), "{:?}", runtime.error());
        assert!(!runtime.session().cinematic_controls_player());
        assert_eq!(runtime.session().document(), &source);
        assert!((fov(&live, camera) - original_fov).abs() < 1.0e-6);
        assert_ne!(
            live.world().get::<Transform>(camera).unwrap().translation,
            Vec3::new(0.0, 3.0, 0.0)
        );
        live.world_mut()
            .resource_mut::<PendingPlayerControlCommand>()
            .submit(PlayerControlCommand {
                move_axis: Vec2::Y,
                ..PlayerControlCommand::default()
            })
            .unwrap();
        for _ in 0..4 {
            live.update();
        }
        assert!(
            live.world()
                .resource::<PlayerRuntimeSnapshot>()
                .horizontal_velocity
                .length()
                > 0.0
        );
    }
}
