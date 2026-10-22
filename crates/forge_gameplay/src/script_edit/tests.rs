use super::*;
use crate::{NarrativeComparison, NarrativeValue, ScriptPermissions};

fn id(name: &str) -> EntityId {
    EntityId::deterministic(format!("script-tree/{name}"))
}
fn message(name: &str) -> ScriptInstruction {
    ScriptInstruction {
        id: id(name),
        operation: ScriptOperation::Message { text: name.into() },
    }
}
fn script() -> ScriptDefinition {
    ScriptDefinition {
        id: id("program"),
        name: "Program".into(),
        permissions: ScriptPermissions::default(),
        steps: vec![ScriptInstruction {
            id: id("branch"),
            operation: ScriptOperation::If {
                variable: id("flag"),
                comparison: NarrativeComparison::Equal,
                value: NarrativeValue::Boolean(true),
                then_steps: vec![message("first")],
                else_steps: vec![message("other")],
            },
        }],
    }
}

#[test]
fn inserting_and_retouched_instruction_preserve_neighbor_and_branch_identities() {
    let original = script();
    let before = serde_json::to_vec(&original).unwrap();
    let inserted = original
        .draft_insert_instruction(
            ScriptTreeLocation {
                parent: Some(id("branch")),
                branch: ScriptBranch::Then,
                index: 1,
            },
            message("second"),
        )
        .unwrap();
    let retouched = inserted
        .draft_replace_instruction(ScriptInstruction {
            id: id("first"),
            operation: ScriptOperation::Message {
                text: "Retouched".into(),
            },
        })
        .unwrap();
    let ScriptOperation::If {
        then_steps,
        else_steps,
        ..
    } = &retouched.steps[0].operation
    else {
        panic!("branch preserved")
    };
    assert_eq!(retouched.steps[0].id, id("branch"));
    assert_eq!(then_steps[0].id, id("first"));
    assert_eq!(then_steps[1], message("second"));
    assert_eq!(else_steps, &vec![message("other")]);
    assert_eq!(serde_json::to_vec(&original).unwrap(), before);
}

#[test]
fn move_keeps_identity_and_removing_one_instruction_preserves_the_other_branch() {
    let original = script();
    let moved = original
        .draft_move_instruction(
            id("first"),
            ScriptTreeLocation {
                parent: Some(id("branch")),
                branch: ScriptBranch::Else,
                index: 1,
            },
        )
        .unwrap();
    let ScriptOperation::If {
        then_steps,
        else_steps,
        ..
    } = &moved.steps[0].operation
    else {
        panic!("branch preserved")
    };
    assert!(then_steps.is_empty());
    assert_eq!(else_steps, &vec![message("other"), message("first")]);
    let removed = moved.draft_remove_instruction(id("other")).unwrap();
    let ScriptOperation::If { else_steps, .. } = &removed.steps[0].operation else {
        panic!("branch preserved")
    };
    assert_eq!(else_steps, &vec![message("first")]);
}

#[test]
fn moving_a_parent_into_its_own_descendant_is_refused_without_mutation() {
    let original = script();
    let before = serde_json::to_vec(&original).unwrap();
    assert!(
        original
            .draft_move_instruction(
                id("branch"),
                ScriptTreeLocation {
                    parent: Some(id("first")),
                    branch: ScriptBranch::Then,
                    index: 0
                }
            )
            .is_err()
    );
    assert_eq!(serde_json::to_vec(&original).unwrap(), before);
}

#[test]
fn duplicate_id_missing_parent_wrong_branch_and_bad_index_are_refused() {
    let original = script();
    let before = serde_json::to_vec(&original).unwrap();
    for (location, node) in [
        (
            ScriptTreeLocation {
                parent: None,
                branch: ScriptBranch::Root,
                index: 1,
            },
            message("other"),
        ),
        (
            ScriptTreeLocation {
                parent: Some(id("absent")),
                branch: ScriptBranch::Then,
                index: 0,
            },
            message("new"),
        ),
        (
            ScriptTreeLocation {
                parent: Some(id("branch")),
                branch: ScriptBranch::RepeatBody,
                index: 0,
            },
            message("new"),
        ),
        (
            ScriptTreeLocation {
                parent: None,
                branch: ScriptBranch::Root,
                index: 2,
            },
            message("new"),
        ),
    ] {
        assert!(original.draft_insert_instruction(location, node).is_err());
    }
    assert_eq!(serde_json::to_vec(&original).unwrap(), before);
}

#[test]
fn node_and_depth_limits_apply_before_promoting_a_draft() {
    let mut original = ScriptDefinition {
        id: id("program"),
        name: "Program".into(),
        permissions: ScriptPermissions::default(),
        steps: vec![],
    };
    original.steps = (0..MAX_SCRIPT_NODES)
        .map(|index| message(&format!("node-{index}")))
        .collect();
    assert!(
        original
            .draft_insert_instruction(
                ScriptTreeLocation {
                    parent: None,
                    branch: ScriptBranch::Root,
                    index: MAX_SCRIPT_NODES
                },
                message("overflow")
            )
            .is_err()
    );
    let mut nested = message("deep");
    for depth in 0..MAX_SCRIPT_DEPTH {
        nested = ScriptInstruction {
            id: id(&format!("repeat-{depth}")),
            operation: ScriptOperation::Repeat {
                iterations: 1,
                steps: vec![nested],
            },
        };
    }
    let empty = ScriptDefinition {
        id: id("program"),
        name: "Program".into(),
        permissions: ScriptPermissions::default(),
        steps: vec![],
    };
    assert!(
        empty
            .draft_insert_instruction(
                ScriptTreeLocation {
                    parent: None,
                    branch: ScriptBranch::Root,
                    index: 0
                },
                nested
            )
            .is_err()
    );
}
