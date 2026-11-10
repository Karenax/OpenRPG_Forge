use forge_aurora_bridge::load_bundle;
use forge_nwn_runtime::{
    MODULE_OBJECT, NcsProgram, NcsValue, PLAYER_OBJECT, RuntimeState, VmContext, execute,
    execute_limited, resume,
};
use std::collections::BTreeMap;
use std::path::PathBuf;

fn fixture_bundle() -> PathBuf {
    PathBuf::from(env!("CARGO_MANIFEST_DIR"))
        .join("../forge_aurora_bridge/tests/fixtures/ab_proving.module-migration-v1")
}

fn programs() -> BTreeMap<String, NcsProgram> {
    let imported = load_bundle(&fixture_bundle()).expect("bundle");
    let mut programs = BTreeMap::new();
    for entry in &imported.manifest.files {
        if entry.path.starts_with("scripts/")
            && entry
                .path
                .rsplit_once('.')
                .is_some_and(|(_, extension)| extension.eq_ignore_ascii_case("ncs"))
        {
            let resref = entry
                .path
                .trim_start_matches("scripts/")
                .trim_end_matches(".ncs")
                .to_owned();
            let bytes = std::fs::read(imported.root.join(&entry.path)).expect("ncs");
            let program = NcsProgram::parse(&resref, &bytes).expect("parsed NCS");
            assert!(
                program.unsupported().next().is_none(),
                "fixture script {resref} uses an unsupported instruction"
            );
            programs.insert(resref, program);
        }
    }
    assert_eq!(programs.len(), 14);
    programs
}

fn run(
    programs: &BTreeMap<String, NcsProgram>,
    name: &str,
    context: VmContext,
    state: &mut RuntimeState,
) -> Option<NcsValue> {
    let program = programs
        .get(name)
        .unwrap_or_else(|| panic!("missing {name}"));
    execute(program, context, state)
        .unwrap_or_else(|error| panic!("{name}: {error}"))
        .result
}

#[test]
fn parses_every_fixture_script_without_unsupported_instructions() {
    let programs = programs();
    assert!(programs.contains_key("ab_mod_load"));
    assert!(programs.contains_key("ab_cond_start"));
}

#[test]
fn executes_module_load_and_the_quest_branches_deterministically() {
    let programs = programs();
    let mut state = RuntimeState::default();
    let module = VmContext::self_only(MODULE_OBJECT);

    assert!(run(&programs, "ab_mod_load", module, &mut state).is_none());
    assert_eq!(state.local_int(MODULE_OBJECT, "ab_initialized"), 1);
    assert_eq!(state.local_int(MODULE_OBJECT, "ab_quest_started"), 0);
    assert_eq!(
        run(&programs, "ab_cond_start", module, &mut state),
        Some(NcsValue::Int(1))
    );

    let camp = VmContext::area_enter(MODULE_OBJECT, PLAYER_OBJECT);
    run(&programs, "ab_camp_enter", camp, &mut state);
    assert!(
        state
            .messages
            .iter()
            .any(|message| message.contains("pont rompu"))
    );
    assert!(state.has_journal_entry("ab_quest", 10));

    run(&programs, "ab_act_start", module, &mut state);
    assert_eq!(state.local_int(MODULE_OBJECT, "ab_quest_started"), 1);
    assert!(state.has_journal_entry("ab_quest", 20));
    assert_eq!(
        run(&programs, "ab_cond_active", module, &mut state),
        Some(NcsValue::Int(1))
    );
    assert_eq!(
        run(&programs, "ab_cond_relic", module, &mut state),
        Some(NcsValue::Int(0))
    );

    run(&programs, "ab_chest_dist", module, &mut state);
    assert_eq!(state.local_int(MODULE_OBJECT, "ab_has_relic"), 1);
    assert_eq!(
        run(&programs, "ab_cond_relic", module, &mut state),
        Some(NcsValue::Int(1))
    );

    run(&programs, "ab_guard_death", module, &mut state);
    assert_eq!(state.local_int(MODULE_OBJECT, "ab_combat_won"), 1);

    run(&programs, "ab_reward", module, &mut state);
    assert_eq!(state.local_int(MODULE_OBJECT, "ab_quest_complete"), 1);
    assert_eq!(state.local_int(MODULE_OBJECT, "ab_reward_given"), 1);
    assert_eq!(state.gold, 50);
    assert!(state.has_journal_entry("ab_quest", 30));
    run(&programs, "ab_reward", module, &mut state);
    assert_eq!(state.gold, 50, "the reward must be idempotent");
    assert_eq!(
        run(&programs, "ab_cond_done", module, &mut state),
        Some(NcsValue::Int(1))
    );

    let gate = VmContext::object_used(MODULE_OBJECT, PLAYER_OBJECT);
    run(&programs, "ab_gate_used", gate, &mut state);
    assert!(
        state
            .messages
            .iter()
            .any(|message| message.contains("crypte"))
    );
    assert!(state.executed_scripts.contains(&"ab_mod_load".to_owned()));
}

#[test]
fn malformed_bytecode_is_refused_without_panicking() {
    assert!(NcsProgram::parse("bad", b"").is_err());
    assert!(NcsProgram::parse("bad", b"NCS V1.0").is_err());
    let mut bytes = b"NCS V1.0".to_vec();
    bytes.push(0x42);
    bytes.extend_from_slice(&[0, 0, 0, 1]);
    bytes.push(0xFF);
    assert!(NcsProgram::parse("bad", &bytes).is_err());
}

#[test]
fn truncated_and_mutated_fixture_bytecode_never_panics() {
    let imported = load_bundle(&fixture_bundle()).expect("bundle");
    for entry in &imported.manifest.files {
        if !entry.path.starts_with("scripts/")
            || !entry
                .path
                .rsplit_once('.')
                .is_some_and(|(_, extension)| extension.eq_ignore_ascii_case("ncs"))
        {
            continue;
        }
        let bytes = std::fs::read(imported.root.join(&entry.path)).expect("ncs");
        for length in 0..bytes.len() {
            assert!(
                NcsProgram::parse("truncated", &bytes[..length]).is_err(),
                "{} prefix {length} must be refused",
                entry.path
            );
        }
        for index in (0..bytes.len()).step_by(7) {
            for value in [0x00_u8, 0x7F, 0xFF] {
                let mut mutated = bytes.clone();
                mutated[index] = value;
                let _ = NcsProgram::parse("mutated", &mutated);
            }
        }
    }
}

#[test]
fn unsupported_instruction_is_visible() {
    let mut bytes = b"NCS V1.0".to_vec();
    bytes.push(0x42);
    bytes.extend_from_slice(&[0, 0, 0, 0]);
    bytes.extend_from_slice(&[0x0D, 0x20]);
    let size = u32::try_from(bytes.len()).expect("test NCS size fits u32");
    bytes[9..13].copy_from_slice(&size.to_be_bytes());
    let program = NcsProgram::parse("unsupported", &bytes).expect("parse");
    assert!(program.unsupported().next().is_some());
    let mut state = RuntimeState::default();
    let error = execute(&program, VmContext::self_only(MODULE_OBJECT), &mut state)
        .expect_err("unsupported instruction");
    assert!(error.to_string().contains("unsupported NCS instruction"));
}

fn synthetic_program(body: &[u8]) -> NcsProgram {
    let mut bytes = b"NCS V1.0".to_vec();
    bytes.push(0x42);
    bytes.extend_from_slice(&[0, 0, 0, 0]);
    bytes.extend_from_slice(&[0x1E, 0x00, 0x00, 0x00, 0x00, 0x08]);
    bytes.extend_from_slice(&[0x20, 0x00]);
    bytes.extend_from_slice(body);
    let size = u32::try_from(bytes.len()).expect("test NCS size fits u32");
    bytes[9..13].copy_from_slice(&size.to_be_bytes());
    NcsProgram::parse("synthetic", &bytes).expect("parse synthetic NCS")
}

#[test]
fn declared_native_with_wrong_arity_is_visible_in_the_report() {
    // ACTION 7 (DelayCommand) with zero arguments, then RETN.
    let program = synthetic_program(&[0x05, 0x00, 0x00, 0x07, 0x00, 0x20, 0x00]);
    let mut state = RuntimeState::default();
    let error = execute(&program, VmContext::self_only(MODULE_OBJECT), &mut state)
        .expect_err("arity mismatch");
    assert!(error.to_string().contains("invalid native 7 arguments"));
    assert_eq!(state.unsupported.len(), 1);
    assert_eq!(state.unsupported[0].code, "native-arity");
    assert!(state.unsupported[0].detail.contains("DelayCommand"));
}

#[test]
fn native_arity_mismatch_is_visible_in_the_report() {
    // CONSTI 1, ACTION 242 (GetModule) with one argument, then RETN.
    let program = synthetic_program(&[
        0x04, 0x03, 0x00, 0x00, 0x00, 0x01, 0x05, 0x00, 0x00, 0xF2, 0x01, 0x20, 0x00,
    ]);
    let mut state = RuntimeState::default();
    let error = execute(&program, VmContext::self_only(MODULE_OBJECT), &mut state)
        .expect_err("arity mismatch");
    assert!(error.to_string().contains("invalid native 242 arguments"));
    assert_eq!(state.unsupported[0].code, "native-arity");
}

#[test]
fn infinite_ncs_loop_is_bounded_by_the_vm_budget() {
    // JMP with a zero delta jumps to itself forever.
    let program = synthetic_program(&[0x1D, 0x00, 0x00, 0x00, 0x00, 0x00]);
    let mut state = RuntimeState::default();
    let error =
        execute(&program, VmContext::self_only(MODULE_OBJECT), &mut state).expect_err("budget");
    assert!(error.to_string().contains("budget"));
}

#[test]
fn vm_can_suspend_and_resume_without_losing_state() {
    let programs = programs();
    let program = programs.get("ab_act_start").expect("script");
    let context = VmContext::self_only(MODULE_OBJECT);
    let mut full_state = RuntimeState::default();
    let full = execute(program, context, &mut full_state).expect("full execution");

    let mut state = RuntimeState::default();
    let mut run = execute_limited(program, context, &mut state, 2).expect("first slice");
    let mut slices = 1;
    while run.outcome.is_none() {
        let continuation = run.continuation.expect("continuation");
        run = resume(program, context, &mut state, continuation, 2).expect("resume");
        slices += 1;
        assert!(slices < 500, "suspension loop did not finish");
    }
    let suspended = run.outcome.expect("outcome");
    assert_eq!(suspended.result, full.result);
    assert_eq!(suspended.steps, full.steps);
    assert_eq!(state.module_locals, full_state.module_locals);
    assert_eq!(state.quest_entries, full_state.quest_entries);
    assert_eq!(state.executed_scripts, full_state.executed_scripts);
}
