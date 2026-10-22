use super::*;
use std::collections::BTreeMap;

fn id(name: &str) -> EntityId {
    EntityId::deterministic(format!("script-test/{name}"))
}

#[derive(Clone, Debug, Default, PartialEq, Eq)]
struct Host {
    variables: BTreeMap<EntityId, NarrativeValue>,
    doors: BTreeMap<EntityId, bool>,
    messages: Vec<String>,
}

impl ScriptGameApi for Host {
    fn read_variable(&self, variable: EntityId) -> Result<NarrativeValue, ScriptApiError> {
        self.variables
            .get(&variable)
            .cloned()
            .ok_or_else(|| "missing variable".into())
    }
    fn write_variable(
        &mut self,
        variable: EntityId,
        value: &NarrativeValue,
    ) -> Result<(), ScriptApiError> {
        let current = self
            .variables
            .get_mut(&variable)
            .ok_or_else(|| "missing variable".to_owned())?;
        if !current.same_type(value) {
            return Err("incompatible value".into());
        }
        value.validate().map_err(|error| error.to_string())?;
        *current = value.clone();
        Ok(())
    }
    fn set_door(&mut self, object: EntityId, open: bool) -> Result<(), ScriptApiError> {
        *self
            .doors
            .get_mut(&object)
            .ok_or_else(|| "missing door".to_owned())? = open;
        Ok(())
    }
    fn message(&mut self, text: &str) -> Result<(), ScriptApiError> {
        self.messages.push(text.into());
        Ok(())
    }
}

fn step(name: &str, operation: ScriptOperation) -> ScriptInstruction {
    ScriptInstruction {
        id: id(name),
        operation,
    }
}

fn fixture(steps: Vec<ScriptInstruction>) -> (ScriptDefinition, Host) {
    (
        ScriptDefinition {
            id: id("program"),
            name: "Program".into(),
            permissions: ScriptPermissions {
                read_variables: BTreeSet::from([id("flag")]),
                write_variables: BTreeSet::from([id("flag")]),
                doors: BTreeSet::from([id("door")]),
            },
            steps,
        },
        Host {
            variables: BTreeMap::from([(id("flag"), NarrativeValue::Boolean(false))]),
            doors: BTreeMap::from([(id("door"), false)]),
            messages: vec![],
        },
    )
}

fn set_flag() -> ScriptInstruction {
    step(
        "write",
        ScriptOperation::SetVariable {
            variable: id("flag"),
            value: NarrativeValue::Boolean(true),
        },
    )
}

#[test]
fn allowed_program_observes_its_own_writes_and_promotes_once() {
    let (script, mut host) = fixture(vec![
        set_flag(),
        step(
            "branch",
            ScriptOperation::If {
                variable: id("flag"),
                comparison: NarrativeComparison::Equal,
                value: NarrativeValue::Boolean(true),
                then_steps: vec![step(
                    "door-write",
                    ScriptOperation::SetDoor {
                        object: id("door"),
                        open: true,
                    },
                )],
                else_steps: vec![step(
                    "unexpected",
                    ScriptOperation::Message {
                        text: "wrong branch".into(),
                    },
                )],
            },
        ),
    ]);
    let source = serde_json::to_vec(&script).unwrap();
    assert_eq!(run_script(&script, &mut host).unwrap(), 3);
    assert_eq!(host.variables[&id("flag")], NarrativeValue::Boolean(true));
    assert!(host.doors[&id("door")]);
    assert!(host.messages.is_empty());
    assert_eq!(serde_json::to_vec(&script).unwrap(), source);
}

#[test]
fn denied_door_after_variable_write_rolls_back_entire_program() {
    let (mut script, mut host) = fixture(vec![
        set_flag(),
        step(
            "denied",
            ScriptOperation::SetDoor {
                object: id("door"),
                open: true,
            },
        ),
    ]);
    script.permissions.doors.clear();
    let before = host.clone();
    let error = run_script(&script, &mut host).unwrap_err();
    assert_eq!(error.kind, ScriptDiagnosticKind::Permission);
    assert_eq!(error.script, script.id);
    assert_eq!(error.instruction, Some(id("denied")));
    assert!(!error.remedy.is_empty());
    assert_eq!(host, before);
}

#[test]
fn read_permission_is_independent_from_write_permission() {
    let (mut script, mut host) = fixture(vec![
        set_flag(),
        step(
            "read",
            ScriptOperation::If {
                variable: id("flag"),
                comparison: NarrativeComparison::Equal,
                value: NarrativeValue::Boolean(true),
                then_steps: vec![],
                else_steps: vec![],
            },
        ),
    ]);
    script.permissions.read_variables.clear();
    let before = host.clone();
    assert_eq!(
        run_script(&script, &mut host).unwrap_err().instruction,
        Some(id("read"))
    );
    assert_eq!(host, before);
}

#[test]
fn nested_bounded_repetitions_stop_at_execution_budget_without_partial_messages() {
    let (script, mut host) = fixture(vec![
        set_flag(),
        step(
            "outer",
            ScriptOperation::Repeat {
                iterations: 16,
                steps: vec![step(
                    "inner",
                    ScriptOperation::Repeat {
                        iterations: 16,
                        steps: vec![step(
                            "message",
                            ScriptOperation::Message {
                                text: "hello".into(),
                            },
                        )],
                    },
                )],
            },
        ),
    ]);
    let before = host.clone();
    let error = run_script(&script, &mut host).unwrap_err();
    assert_eq!(error.kind, ScriptDiagnosticKind::Budget);
    assert_eq!(host, before);
}

#[test]
fn game_api_failure_after_write_is_atomic_and_names_the_failing_instruction() {
    let (mut script, mut host) = fixture(vec![
        set_flag(),
        step(
            "absent",
            ScriptOperation::SetDoor {
                object: id("absent-door"),
                open: true,
            },
        ),
    ]);
    script.permissions.doors.insert(id("absent-door"));
    let before = host.clone();
    let error = run_script(&script, &mut host).unwrap_err();
    assert_eq!(error.kind, ScriptDiagnosticKind::GameApi);
    assert_eq!(error.instruction, Some(id("absent")));
    assert_eq!(host, before);
}

#[test]
fn malformed_message_or_repeat_is_refused_atomically() {
    for operation in [
        ScriptOperation::Message {
            text: "x".repeat(MAX_SCRIPT_MESSAGE_BYTES + 1),
        },
        ScriptOperation::Repeat {
            iterations: 0,
            steps: vec![set_flag()],
        },
        ScriptOperation::Repeat {
            iterations: 17,
            steps: vec![set_flag()],
        },
    ] {
        let (script, mut host) = fixture(vec![set_flag(), step("invalid", operation)]);
        let before = host.clone();
        assert_eq!(
            run_script(&script, &mut host).unwrap_err().kind,
            ScriptDiagnosticKind::InvalidSource
        );
        assert_eq!(host, before);
    }
}
