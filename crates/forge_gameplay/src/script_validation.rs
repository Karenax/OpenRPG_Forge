//! Source validation for typed, bounded script definitions.
use crate::scripts::{
    MAX_SCRIPT_DEPTH, MAX_SCRIPT_ITERATIONS, MAX_SCRIPT_MESSAGE_BYTES, MAX_SCRIPT_NODES,
    MAX_SCRIPTS, ScriptDefinition, ScriptDiagnostic, ScriptDiagnosticKind, ScriptInstruction,
    ScriptOperation, ScriptingDefinition,
};
use crate::{GameplayDocument, NarrativeComparison, NarrativeValue, ObjectKind};
use forge_core::EntityId;
use std::collections::BTreeSet;

impl ScriptingDefinition {
    pub(crate) fn validate(
        &self,
        document: &GameplayDocument,
        ids: &mut BTreeSet<EntityId>,
    ) -> Result<(), ScriptDiagnostic> {
        if self.version != 1 || self.scripts.len() > MAX_SCRIPTS {
            return Err(ScriptDiagnostic {
                script: EntityId::deterministic("invalid-script-container"),
                instruction: None,
                kind: ScriptDiagnosticKind::InvalidSource,
                cause: "unsupported script version or script quota exceeded".into(),
                remedy: "Use version 1 and at most 32 scripts.".into(),
            });
        }
        for script in &self.scripts {
            let invalid =
                |cause| script.diagnostic(None, ScriptDiagnosticKind::InvalidSource, cause);
            if !ids.insert(script.id) || script.name.trim().is_empty() || script.name.len() > 256 {
                return Err(invalid("script identity or name is invalid"));
            }
            if script.permissions.read_variables.len() > crate::MAX_NARRATIVE_VARIABLES
                || script.permissions.write_variables.len() > crate::MAX_NARRATIVE_VARIABLES
                || script.permissions.doors.len() > crate::MAX_GAMEPLAY_ENTRIES
            {
                return Err(invalid("permission reference quota exceeded"));
            }
            for variable in script
                .permissions
                .read_variables
                .iter()
                .chain(&script.permissions.write_variables)
            {
                if document
                    .narrative
                    .as_ref()
                    .and_then(|narrative| narrative.variable(*variable))
                    .is_none()
                {
                    return Err(invalid("permission names an absent variable"));
                }
            }
            for door in &script.permissions.doors {
                if !document
                    .object(*door)
                    .is_some_and(|object| matches!(object.kind, ObjectKind::Door { .. }))
                {
                    return Err(invalid("permission names an absent door"));
                }
            }
            if script.steps.is_empty() {
                return Err(invalid("script must contain at least one instruction"));
            }
            validate_steps(script, &script.steps, document, ids, &mut 0, 1)?;
        }
        Ok(())
    }
}

fn validate_steps(
    script: &ScriptDefinition,
    steps: &[ScriptInstruction],
    document: &GameplayDocument,
    ids: &mut BTreeSet<EntityId>,
    nodes: &mut usize,
    depth: usize,
) -> Result<(), ScriptDiagnostic> {
    for instruction in steps {
        let invalid = |cause| {
            script.diagnostic(
                Some(instruction.id),
                ScriptDiagnosticKind::InvalidSource,
                cause,
            )
        };
        *nodes += 1;
        if depth > MAX_SCRIPT_DEPTH || *nodes > MAX_SCRIPT_NODES || !ids.insert(instruction.id) {
            return Err(invalid("script node identity, depth or quota is invalid"));
        }
        match &instruction.operation {
            ScriptOperation::SetVariable { variable, value } => {
                document
                    .validate_variable_value(*variable, value)
                    .map_err(|cause| {
                        script.diagnostic(
                            Some(instruction.id),
                            ScriptDiagnosticKind::InvalidSource,
                            cause.to_string(),
                        )
                    })?;
            }
            ScriptOperation::SetDoor { object, .. } => {
                if !document
                    .object(*object)
                    .is_some_and(|object| matches!(object.kind, ObjectKind::Door { .. }))
                {
                    return Err(invalid("instruction names an absent door"));
                }
            }
            ScriptOperation::Message { text } => {
                if text.trim().is_empty() || text.len() > MAX_SCRIPT_MESSAGE_BYTES {
                    return Err(invalid("message is empty or exceeds its byte limit"));
                }
            }
            ScriptOperation::If {
                variable,
                comparison,
                value,
                then_steps,
                else_steps,
            } => {
                document
                    .validate_variable_value(*variable, value)
                    .map_err(|cause| {
                        script.diagnostic(
                            Some(instruction.id),
                            ScriptDiagnosticKind::InvalidSource,
                            cause.to_string(),
                        )
                    })?;
                if matches!(
                    comparison,
                    NarrativeComparison::AtLeast | NarrativeComparison::AtMost
                ) && !matches!(value, NarrativeValue::Integer(_))
                {
                    return Err(invalid("ordered comparisons require integer variables"));
                }
                validate_steps(script, then_steps, document, ids, nodes, depth + 1)?;
                validate_steps(script, else_steps, document, ids, nodes, depth + 1)?;
            }
            ScriptOperation::Repeat { iterations, steps } => {
                if !(1..=MAX_SCRIPT_ITERATIONS).contains(iterations) || steps.is_empty() {
                    return Err(invalid(
                        "repeat requires one to 16 iterations and a nonempty body",
                    ));
                }
                validate_steps(script, steps, document, ids, nodes, depth + 1)?;
            }
        }
    }
    Ok(())
}
