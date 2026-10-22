use super::temporal::{execute, step};
use super::*;
use forge_gameplay::{
    CinematicCue, CinematicDefinition, NarrativeDefinition, NarrativeValue, NarrativeVariable,
    ScriptDefinition, ScriptInstruction, ScriptOperation, ScriptPermissions,
};
use std::collections::BTreeSet;

fn configured(host: &mut AuthoringHost, fixture: &Fixture) -> (GameplayDocument, ContentHash) {
    let document = fixture
        .gameplay
        .with_narrative_definition(Some(NarrativeDefinition {
            version: 1,
            variables: vec![NarrativeVariable {
                id: id("host/presentation/flag"),
                name: "Flag".into(),
                initial: NarrativeValue::Boolean(false),
            }],
            quests: vec![],
        }))
        .unwrap()
        .with_script_definition(ScriptDefinition {
            id: id("host/presentation/script"),
            name: "Remember".into(),
            permissions: ScriptPermissions {
                write_variables: BTreeSet::from([id("host/presentation/flag")]),
                ..ScriptPermissions::default()
            },
            steps: vec![ScriptInstruction {
                id: id("host/presentation/write"),
                operation: ScriptOperation::SetVariable {
                    variable: id("host/presentation/flag"),
                    value: NarrativeValue::Boolean(true),
                },
            }],
        })
        .unwrap()
        .with_cinematic_definition(CinematicDefinition {
            id: id("host/presentation/sequence"),
            name: "Arrival".into(),
            cues: vec![CinematicCue {
                id: id("host/presentation/cue"),
                duration_ms: 1000,
                speaker: None,
                text: Some("Arrival".into()),
                camera: None,
            }],
        })
        .unwrap();
    let head = host.execute(Request::Inspect).unwrap().zone_head.unwrap();
    let request = head
        .prepare_gameplay(
            Some(document.clone()),
            CommandId::deterministic("host/presentation-definition"),
        )
        .unwrap();
    let state = host.execute(Request::ApplyZone { request }).unwrap();
    let source = state.zone_head.unwrap().content_hash().unwrap();
    host.execute(Request::Save).unwrap();
    (document, source)
}

#[test]
fn presentation_v6_save_close_open_load_keeps_script_output_and_resumes_then_skips_sequence() {
    let (root, mut host, fixture) = setup("presentation-roundtrip");
    let (document, source) = configured(&mut host, &fixture);
    let authored = fs::read(authoring_path(&root, fixture.area_id)).unwrap();
    let mut game = GameSession::new(document.clone(), source).unwrap();
    let script = execute(
        &mut game,
        GameAction::RunScript {
            script: id("host/presentation/script"),
        },
    );
    execute(
        &mut game,
        GameAction::StartCinematic {
            sequence: id("host/presentation/sequence"),
        },
    );
    step(&mut game, 250_000_000);
    let presentation = game.cinematic_presentation().unwrap().unwrap();
    let save_json = String::from_utf8(game.save_bytes().unwrap()).unwrap();
    assert_eq!(
        serde_json::from_str::<serde_json::Value>(&save_json).unwrap()["version"],
        6
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
    assert_eq!(
        resumed.cinematic_presentation().unwrap(),
        Some(presentation)
    );
    let before_retry = resumed.save_bytes().unwrap();
    assert_eq!(
        resumed
            .execute(&script.request, ActionObservation::default())
            .unwrap(),
        script
    );
    assert_eq!(resumed.save_bytes().unwrap(), before_retry);
    execute(&mut resumed, GameAction::SkipCinematic);
    step(&mut resumed, 1_000_000_000);
    assert!(!resumed.cinematic_controls_player());
    assert_eq!(
        resumed.state().narrative.as_ref().unwrap().variables[&id("host/presentation/flag")],
        NarrativeValue::Boolean(true)
    );
    assert!(resumed.save_bytes().is_ok());
    assert_eq!(
        fs::read(authoring_path(&root, fixture.area_id)).unwrap(),
        authored
    );
    reopened.execute(Request::Close).unwrap();
    drop(reopened);
    cleanup(root);
}
