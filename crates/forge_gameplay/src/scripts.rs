//! A finite typed language with an explicitly supplied, bounded game API.
use crate::{NarrativeComparison, NarrativeValue};
use forge_core::EntityId;
use serde::{Deserialize, Serialize};
use std::collections::BTreeSet;

pub const MAX_SCRIPTS: usize = 32;
pub const MAX_SCRIPT_NODES: usize = 128;
pub const MAX_SCRIPT_DEPTH: usize = 8;
pub const MAX_SCRIPT_ITERATIONS: u8 = 16;
pub const MAX_SCRIPT_STEPS: usize = 256;
pub const MAX_SCRIPT_MESSAGE_BYTES: usize = 1024;

#[derive(Clone, Debug, Default, PartialEq, Eq, Serialize, Deserialize)]
#[serde(deny_unknown_fields)]
pub struct ScriptPermissions {
    pub read_variables: BTreeSet<EntityId>,
    pub write_variables: BTreeSet<EntityId>,
    pub doors: BTreeSet<EntityId>,
}

#[derive(Clone, Debug, PartialEq, Eq, Serialize, Deserialize)]
#[serde(deny_unknown_fields)]
pub struct ScriptInstruction {
    pub id: EntityId,
    pub operation: ScriptOperation,
}

#[derive(Clone, Debug, PartialEq, Eq, Serialize, Deserialize)]
#[serde(tag = "kind", rename_all = "snake_case", deny_unknown_fields)]
pub enum ScriptOperation {
    SetVariable {
        variable: EntityId,
        value: NarrativeValue,
    },
    SetDoor {
        object: EntityId,
        open: bool,
    },
    Message {
        text: String,
    },
    If {
        variable: EntityId,
        comparison: NarrativeComparison,
        value: NarrativeValue,
        then_steps: Vec<ScriptInstruction>,
        else_steps: Vec<ScriptInstruction>,
    },
    Repeat {
        iterations: u8,
        steps: Vec<ScriptInstruction>,
    },
}

#[derive(Clone, Debug, PartialEq, Eq, Serialize, Deserialize)]
#[serde(deny_unknown_fields)]
pub struct ScriptDefinition {
    pub id: EntityId,
    pub name: String,
    pub permissions: ScriptPermissions,
    pub steps: Vec<ScriptInstruction>,
}

#[derive(Clone, Debug, PartialEq, Eq, Serialize, Deserialize)]
#[serde(deny_unknown_fields)]
pub struct ScriptingDefinition {
    pub version: u32,
    pub scripts: Vec<ScriptDefinition>,
}

#[derive(Clone, Copy, Debug, PartialEq, Eq, Serialize, Deserialize)]
#[serde(rename_all = "snake_case")]
pub enum ScriptDiagnosticKind {
    Permission,
    Budget,
    InvalidSource,
    GameApi,
}

#[derive(Clone, Debug, PartialEq, Eq, Serialize, Deserialize)]
#[serde(deny_unknown_fields)]
pub struct ScriptDiagnostic {
    pub script: EntityId,
    pub instruction: Option<EntityId>,
    pub kind: ScriptDiagnosticKind,
    pub cause: String,
    pub remedy: String,
}

impl ScriptDefinition {
    pub(crate) fn diagnostic(
        &self,
        instruction: Option<EntityId>,
        kind: ScriptDiagnosticKind,
        cause: impl Into<String>,
    ) -> ScriptDiagnostic {
        let remedy = match kind {
            ScriptDiagnosticKind::Permission => {
                "Declare the required reference in this script's permissions."
            }
            ScriptDiagnosticKind::Budget => {
                "Reduce repetitions or split this program into bounded calls."
            }
            ScriptDiagnosticKind::InvalidSource => {
                "Correct this instruction's references, value or structural bounds."
            }
            ScriptDiagnosticKind::GameApi => {
                "Inspect the referenced game state and satisfy the action preconditions."
            }
        };
        ScriptDiagnostic {
            script: self.id,
            instruction,
            kind,
            cause: cause.into(),
            remedy: remedy.into(),
        }
    }
}

/// Keep resource exhaustion distinct from ordinary game preconditions so the
/// instruction diagnostic can suggest a correction without parsing error text.
#[derive(Clone, Debug)]
pub(crate) enum ScriptApiError {
    EventBudget,
    State(String),
}

impl From<String> for ScriptApiError {
    fn from(cause: String) -> Self {
        Self::State(cause)
    }
}

impl From<&str> for ScriptApiError {
    fn from(cause: &str) -> Self {
        Self::State(cause.into())
    }
}

impl ScriptApiError {
    fn diagnostic(self, definition: &ScriptDefinition, instruction: EntityId) -> ScriptDiagnostic {
        let (kind, cause) = match self {
            Self::EventBudget => (
                ScriptDiagnosticKind::Budget,
                "script output exceeds the remaining game event budget".into(),
            ),
            Self::State(cause) => (ScriptDiagnosticKind::GameApi, cause),
        };
        definition.diagnostic(Some(instruction), kind, cause)
    }
}

/// The interpreter has no ambient authority. The gameplay adapter supplies only
/// these operations on its candidate state, including the normal door rules.
pub(crate) trait ScriptGameApi: Clone {
    fn read_variable(&self, variable: EntityId) -> Result<NarrativeValue, ScriptApiError>;
    fn write_variable(
        &mut self,
        variable: EntityId,
        value: &NarrativeValue,
    ) -> Result<(), ScriptApiError>;
    fn set_door(&mut self, object: EntityId, open: bool) -> Result<(), ScriptApiError>;
    fn message(&mut self, text: &str) -> Result<(), ScriptApiError>;
}

pub(crate) fn run_script<H: ScriptGameApi>(
    definition: &ScriptDefinition,
    host: &mut H,
) -> Result<usize, ScriptDiagnostic> {
    let mut candidate = host.clone();
    let mut remaining = MAX_SCRIPT_STEPS;
    execute_steps(
        definition,
        &definition.steps,
        &mut candidate,
        &mut remaining,
        1,
    )?;
    *host = candidate;
    Ok(MAX_SCRIPT_STEPS - remaining)
}

fn execute_steps<H: ScriptGameApi>(
    definition: &ScriptDefinition,
    steps: &[ScriptInstruction],
    host: &mut H,
    remaining: &mut usize,
    depth: usize,
) -> Result<(), ScriptDiagnostic> {
    for instruction in steps {
        let error = |kind, cause: &str| definition.diagnostic(Some(instruction.id), kind, cause);
        if depth > MAX_SCRIPT_DEPTH || *remaining == 0 {
            return Err(error(
                ScriptDiagnosticKind::Budget,
                "instruction budget or depth exceeded",
            ));
        }
        *remaining -= 1;
        let result = match &instruction.operation {
            ScriptOperation::SetVariable { variable, value } => {
                require_permission(
                    definition,
                    instruction.id,
                    definition.permissions.write_variables.contains(variable),
                )?;
                host.write_variable(*variable, value)
            }
            ScriptOperation::SetDoor { object, open } => {
                require_permission(
                    definition,
                    instruction.id,
                    definition.permissions.doors.contains(object),
                )?;
                host.set_door(*object, *open)
            }
            ScriptOperation::Message { text } => {
                if text.trim().is_empty() || text.len() > MAX_SCRIPT_MESSAGE_BYTES {
                    return Err(error(
                        ScriptDiagnosticKind::InvalidSource,
                        "message length is invalid",
                    ));
                }
                host.message(text)
            }
            ScriptOperation::If {
                variable,
                comparison,
                value,
                then_steps,
                else_steps,
            } => {
                require_permission(
                    definition,
                    instruction.id,
                    definition.permissions.read_variables.contains(variable),
                )?;
                let actual = host
                    .read_variable(*variable)
                    .map_err(|cause| cause.diagnostic(definition, instruction.id))?;
                if !actual.same_type(value) {
                    return Err(error(
                        ScriptDiagnosticKind::InvalidSource,
                        "comparison has incompatible value types",
                    ));
                }
                if matches!(
                    comparison,
                    NarrativeComparison::AtLeast | NarrativeComparison::AtMost
                ) && !matches!(value, NarrativeValue::Integer(_))
                {
                    return Err(error(
                        ScriptDiagnosticKind::InvalidSource,
                        "ordered comparison requires integer values",
                    ));
                }
                let branch = if comparison.evaluate(&actual, value) {
                    then_steps
                } else {
                    else_steps
                };
                execute_steps(definition, branch, host, remaining, depth + 1)?;
                Ok(())
            }
            ScriptOperation::Repeat { iterations, steps } => {
                if !(1..=MAX_SCRIPT_ITERATIONS).contains(iterations) || steps.is_empty() {
                    return Err(error(
                        ScriptDiagnosticKind::InvalidSource,
                        "repeat count is outside its bounds",
                    ));
                }
                for _ in 0..*iterations {
                    execute_steps(definition, steps, host, remaining, depth + 1)?;
                }
                Ok(())
            }
        };
        result.map_err(|cause| cause.diagnostic(definition, instruction.id))?;
    }
    Ok(())
}

fn require_permission(
    definition: &ScriptDefinition,
    instruction: EntityId,
    allowed: bool,
) -> Result<(), ScriptDiagnostic> {
    if allowed {
        Ok(())
    } else {
        Err(definition.diagnostic(
            Some(instruction),
            ScriptDiagnosticKind::Permission,
            "reference is not permitted for this operation",
        ))
    }
}

#[cfg(test)]
#[path = "scripts/tests.rs"]
mod tests;
