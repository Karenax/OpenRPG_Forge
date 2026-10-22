//! Structural checks are shared by dialogue choices and authored quest solutions.
use crate::{
    Condition, DialogueEffect, GameplayDocument, GameplayError, NarrativeComparison,
    NarrativeValue, ObjectKind,
};

fn invalid(message: impl Into<String>) -> GameplayError {
    GameplayError::InvalidDocument(format!("narrative condition/effect: {}", message.into()))
}

impl GameplayDocument {
    pub(crate) fn validate_condition(&self, condition: &Condition) -> Result<(), GameplayError> {
        self.validate_condition_node(condition, 0, &mut 0)
    }

    fn validate_condition_node(
        &self,
        condition: &Condition,
        depth: usize,
        nodes: &mut usize,
    ) -> Result<(), GameplayError> {
        *nodes += 1;
        if depth > 8 || *nodes > 128 {
            return Err(invalid("condition exceeds 8 levels or 128 nodes"));
        }
        let reference = |present, id| {
            if present {
                Ok(())
            } else {
                Err(GameplayError::MissingReference(format!("{id}")))
            }
        };
        match condition {
            Condition::Relation {
                observer,
                subject,
                threshold,
                ..
            } => {
                reference(self.actor(*observer).is_some(), *observer)?;
                reference(self.actor(*subject).is_some(), *subject)?;
                if self
                    .factions
                    .as_ref()
                    .and_then(|factions| factions.actor_faction(*observer))
                    .is_none()
                    || !(crate::MIN_RELATION..=crate::MAX_RELATION).contains(threshold)
                {
                    return Err(invalid(
                        "relation requires an affiliated observer and a threshold between -100 and 100",
                    ));
                }
                Ok(())
            }
            Condition::Always => Ok(()),
            Condition::Quest { quest, .. } => reference(self.quest(*quest).is_some(), *quest),
            Condition::HasItem { item, quantity } => {
                reference(self.item(*item).is_some(), *item)?;
                if *quantity == 0
                    || self
                        .item(*item)
                        .is_some_and(|entry| *quantity > entry.max_stack)
                {
                    return Err(invalid(
                        "item condition quantity is outside its stack limit",
                    ));
                }
                Ok(())
            }
            Condition::ActorDefeated { actor } => reference(self.actor(*actor).is_some(), *actor),
            Condition::ObjectDestroyed { object } => {
                reference(self.object(*object).is_some(), *object)
            }
            Condition::Variable {
                variable,
                comparison,
                value,
            } => {
                self.validate_variable_value(*variable, value)?;
                if matches!(
                    comparison,
                    NarrativeComparison::AtLeast | NarrativeComparison::AtMost
                ) && !matches!(value, NarrativeValue::Integer(_))
                {
                    return Err(invalid("ordered comparison requires an integer variable"));
                }
                Ok(())
            }
            Condition::All { conditions } | Condition::Any { conditions } => {
                if conditions.is_empty() || conditions.len() > 16 {
                    return Err(invalid("combined condition requires 1 to 16 children"));
                }
                for child in conditions {
                    self.validate_condition_node(child, depth + 1, nodes)?;
                }
                Ok(())
            }
        }
    }

    pub(crate) fn validate_variable_value(
        &self,
        variable: forge_core::EntityId,
        value: &NarrativeValue,
    ) -> Result<(), GameplayError> {
        let definition = self
            .narrative
            .as_ref()
            .and_then(|story| story.variable(variable))
            .ok_or_else(|| GameplayError::MissingReference(format!("variable {variable}")))?;
        if !definition.initial.same_type(value) {
            return Err(invalid(format!("type differs from variable {variable}")));
        }
        value.validate()
    }

    pub(crate) fn validate_dialogue_effect(
        &self,
        effect: &DialogueEffect,
    ) -> Result<(), GameplayError> {
        match effect {
            DialogueEffect::SetVariable { variable, value } => {
                self.validate_variable_value(*variable, value)
            }
            DialogueEffect::AcceptQuest { quest } => {
                if self.quest(*quest).is_none() {
                    return Err(GameplayError::MissingReference(quest.to_string()));
                }
                Ok(())
            }
            DialogueEffect::CompleteQuest { quest } => {
                if self.quest(*quest).is_none() {
                    return Err(GameplayError::MissingReference(quest.to_string()));
                }
                if self.quest_flow(*quest).is_some() {
                    return Err(invalid(format!(
                        "quest {quest} requires an explicit solution"
                    )));
                }
                Ok(())
            }
            DialogueEffect::CompleteQuestSolution { quest, solution } => {
                if self.quest_solution(*quest, *solution).is_none() {
                    return Err(GameplayError::MissingReference(format!(
                        "quest {quest}/solution {solution}"
                    )));
                }
                Ok(())
            }
            DialogueEffect::FailQuest { quest } => {
                if self
                    .quest_flow(*quest)
                    .is_none_or(|flow| flow.failure_condition.is_none())
                {
                    return Err(invalid(format!("quest {quest} has no failure condition")));
                }
                Ok(())
            }
            DialogueEffect::SetDoor { object, .. } => {
                if !self
                    .object(*object)
                    .is_some_and(|entry| matches!(entry.kind, ObjectKind::Door { .. }))
                {
                    return Err(GameplayError::MissingReference(object.to_string()));
                }
                Ok(())
            }
        }
    }
}
