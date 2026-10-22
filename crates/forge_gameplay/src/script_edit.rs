//! Bounded local draft edits. Full source/reference validation occurs on promotion.
use crate::scripts::{
    MAX_SCRIPT_DEPTH, MAX_SCRIPT_ITERATIONS, MAX_SCRIPT_NODES, ScriptDefinition, ScriptDiagnostic,
    ScriptDiagnosticKind, ScriptInstruction, ScriptOperation,
};
use forge_core::EntityId;
use std::collections::BTreeSet;

#[derive(Clone, Copy, Debug, PartialEq, Eq)]
pub enum ScriptBranch {
    Root,
    Then,
    Else,
    RepeatBody,
}

#[derive(Clone, Copy, Debug, PartialEq, Eq)]
pub struct ScriptTreeLocation {
    pub parent: Option<EntityId>,
    pub branch: ScriptBranch,
    /// Insertion index in the destination after removal when moving a node.
    pub index: usize,
}

impl ScriptDefinition {
    pub fn draft_insert_instruction(
        &self,
        location: ScriptTreeLocation,
        instruction: ScriptInstruction,
    ) -> Result<Self, ScriptDiagnostic> {
        self.validate_draft_tree()?;
        let mut candidate = self.clone();
        let id = instruction.id;
        let list = destination(&mut candidate.steps, location.parent, location.branch).ok_or_else(
            || {
                self.diagnostic(
                    location.parent,
                    ScriptDiagnosticKind::InvalidSource,
                    "instruction destination is absent or has another branch kind",
                )
            },
        )?;
        if location.index > list.len() {
            return Err(self.diagnostic(
                Some(id),
                ScriptDiagnosticKind::InvalidSource,
                "insertion index is outside the selected branch",
            ));
        }
        list.insert(location.index, instruction);
        candidate.validate_draft_tree()?;
        Ok(candidate)
    }

    pub fn draft_replace_instruction(
        &self,
        instruction: ScriptInstruction,
    ) -> Result<Self, ScriptDiagnostic> {
        self.validate_draft_tree()?;
        let mut candidate = self.clone();
        let id = instruction.id;
        let current = find_mut(&mut candidate.steps, id).ok_or_else(|| {
            self.diagnostic(
                Some(id),
                ScriptDiagnosticKind::InvalidSource,
                "instruction to retouch is absent",
            )
        })?;
        *current = instruction;
        candidate.validate_draft_tree()?;
        Ok(candidate)
    }

    pub fn draft_remove_instruction(
        &self,
        instruction: EntityId,
    ) -> Result<Self, ScriptDiagnostic> {
        self.validate_draft_tree()?;
        let mut candidate = self.clone();
        if remove(&mut candidate.steps, instruction).is_none() {
            return Err(self.diagnostic(
                Some(instruction),
                ScriptDiagnosticKind::InvalidSource,
                "instruction to remove is absent",
            ));
        }
        candidate.validate_draft_tree()?;
        Ok(candidate)
    }

    pub fn draft_move_instruction(
        &self,
        instruction: EntityId,
        location: ScriptTreeLocation,
    ) -> Result<Self, ScriptDiagnostic> {
        self.validate_draft_tree()?;
        let mut candidate = self.clone();
        let removed = remove(&mut candidate.steps, instruction).ok_or_else(|| {
            self.diagnostic(
                Some(instruction),
                ScriptDiagnosticKind::InvalidSource,
                "instruction to move is absent",
            )
        })?;
        // Removing first also makes it impossible to move a subtree inside itself.
        candidate.draft_insert_instruction(location, removed)
    }

    fn validate_draft_tree(&self) -> Result<(), ScriptDiagnostic> {
        let mut ids = BTreeSet::from([self.id]);
        check_tree(self, &self.steps, &mut ids, 1)
    }
}

fn check_tree(
    script: &ScriptDefinition,
    steps: &[ScriptInstruction],
    ids: &mut BTreeSet<EntityId>,
    depth: usize,
) -> Result<(), ScriptDiagnostic> {
    for instruction in steps {
        if depth > MAX_SCRIPT_DEPTH || ids.len() > MAX_SCRIPT_NODES || !ids.insert(instruction.id) {
            return Err(script.diagnostic(
                Some(instruction.id),
                ScriptDiagnosticKind::InvalidSource,
                "draft instruction identity, depth or node quota is invalid",
            ));
        }
        match &instruction.operation {
            ScriptOperation::If {
                then_steps,
                else_steps,
                ..
            } => {
                check_tree(script, then_steps, ids, depth + 1)?;
                check_tree(script, else_steps, ids, depth + 1)?;
            }
            ScriptOperation::Repeat { iterations, steps } => {
                if !(1..=MAX_SCRIPT_ITERATIONS).contains(iterations) {
                    return Err(script.diagnostic(
                        Some(instruction.id),
                        ScriptDiagnosticKind::InvalidSource,
                        "draft repetition is outside its bounds",
                    ));
                }
                check_tree(script, steps, ids, depth + 1)?;
            }
            _ => (),
        }
    }
    Ok(())
}

fn destination(
    steps: &mut Vec<ScriptInstruction>,
    parent: Option<EntityId>,
    branch: ScriptBranch,
) -> Option<&mut Vec<ScriptInstruction>> {
    let Some(parent) = parent else {
        return (branch == ScriptBranch::Root).then_some(steps);
    };
    match (&mut find_mut(steps, parent)?.operation, branch) {
        (ScriptOperation::If { then_steps, .. }, ScriptBranch::Then) => Some(then_steps),
        (ScriptOperation::If { else_steps, .. }, ScriptBranch::Else) => Some(else_steps),
        (ScriptOperation::Repeat { steps, .. }, ScriptBranch::RepeatBody) => Some(steps),
        _ => None,
    }
}

fn find_mut(steps: &mut [ScriptInstruction], id: EntityId) -> Option<&mut ScriptInstruction> {
    for instruction in steps {
        if instruction.id == id {
            return Some(instruction);
        }
        match &mut instruction.operation {
            ScriptOperation::If {
                then_steps,
                else_steps,
                ..
            } => {
                if let Some(found) = find_mut(then_steps, id) {
                    return Some(found);
                }
                if let Some(found) = find_mut(else_steps, id) {
                    return Some(found);
                }
            }
            ScriptOperation::Repeat { steps, .. } => {
                if let Some(found) = find_mut(steps, id) {
                    return Some(found);
                }
            }
            _ => (),
        }
    }
    None
}

fn remove(steps: &mut Vec<ScriptInstruction>, id: EntityId) -> Option<ScriptInstruction> {
    if let Some(index) = steps.iter().position(|instruction| instruction.id == id) {
        return Some(steps.remove(index));
    }
    for instruction in steps {
        match &mut instruction.operation {
            ScriptOperation::If {
                then_steps,
                else_steps,
                ..
            } => {
                if let Some(found) = remove(then_steps, id) {
                    return Some(found);
                }
                if let Some(found) = remove(else_steps, id) {
                    return Some(found);
                }
            }
            ScriptOperation::Repeat { steps, .. } => {
                if let Some(found) = remove(steps, id) {
                    return Some(found);
                }
            }
            _ => (),
        }
    }
    None
}

#[cfg(test)]
#[path = "script_edit/tests.rs"]
mod tests;
