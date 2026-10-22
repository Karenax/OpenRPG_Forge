//! Narrative transitions share the action transaction; no reward can commit alone.
use crate::{
    DialogueEffect, GameSession, GameState, GameplayDocument, GameplayError, GameplayEvent,
    NarrativeState, QuestFlow, QuestSolution, QuestStatus,
};
use forge_core::EntityId;

fn refused(message: &str) -> GameplayError {
    GameplayError::ActionRefused(message.into())
}

#[derive(Clone, Debug, PartialEq, Eq, serde::Serialize, serde::Deserialize)]
#[serde(deny_unknown_fields)]
pub struct QuestJournalEntry {
    pub quest: EntityId,
    pub name: String,
    pub status: QuestStatus,
    pub text: String,
    pub solution: Option<EntityId>,
}

impl GameplayDocument {
    #[must_use]
    pub fn quest_flow(&self, quest: EntityId) -> Option<&QuestFlow> {
        self.narrative.as_ref()?.quest(quest)
    }

    #[must_use]
    pub fn quest_solution(&self, quest: EntityId, solution: EntityId) -> Option<&QuestSolution> {
        self.quest_flow(quest)?
            .solutions
            .iter()
            .find(|entry| entry.id == solution)
    }
}

impl GameSession {
    pub(crate) fn apply_narrative_effect(
        &self,
        state: &mut GameState,
        actor: EntityId,
        speaker: EntityId,
        effect: &DialogueEffect,
    ) -> Result<Vec<GameplayEvent>, GameplayError> {
        match effect {
            DialogueEffect::SetVariable { variable, value } => {
                let current = state
                    .narrative
                    .as_mut()
                    .and_then(|narrative| narrative.variables.get_mut(variable))
                    .ok_or_else(|| refused("narrative variable is absent"))?;
                if !current.same_type(value) {
                    return Err(refused("narrative variable type cannot change"));
                }
                current.clone_from(value);
                Ok(vec![GameplayEvent::VariableChanged {
                    variable: *variable,
                    value: value.clone(),
                }])
            }
            DialogueEffect::CompleteQuestSolution { quest, solution } => {
                self.complete_quest_solution(state, actor, speaker, *quest, *solution)?;
                Ok(vec![GameplayEvent::QuestChanged {
                    quest: *quest,
                    status: QuestStatus::Completed,
                }])
            }
            DialogueEffect::FailQuest { quest } => {
                let flow = self
                    .document
                    .quest_flow(*quest)
                    .ok_or_else(|| refused("quest has no authored failure"))?;
                self.require_active_quest(state, speaker, *quest)?;
                if !flow
                    .failure_condition
                    .as_ref()
                    .is_some_and(|condition| self.condition_for_state(state, actor, condition))
                {
                    return Err(refused("quest failure condition is not satisfied"));
                }
                state.quests.insert(*quest, QuestStatus::Failed);
                Ok(vec![GameplayEvent::QuestChanged {
                    quest: *quest,
                    status: QuestStatus::Failed,
                }])
            }
            _ => Err(refused("effect is not a narrative transition")),
        }
    }

    fn require_active_quest(
        &self,
        state: &GameState,
        speaker: EntityId,
        quest: EntityId,
    ) -> Result<(), GameplayError> {
        if self
            .document
            .quest(quest)
            .is_none_or(|definition| definition.giver != speaker)
            || state.quests.get(&quest) != Some(&QuestStatus::Active)
        {
            return Err(refused("quest is not active for this speaker"));
        }
        Ok(())
    }

    fn complete_quest_solution(
        &self,
        state: &mut GameState,
        actor: EntityId,
        speaker: EntityId,
        quest: EntityId,
        solution: EntityId,
    ) -> Result<(), GameplayError> {
        self.require_active_quest(state, speaker, quest)?;
        let selected = self
            .document
            .quest_solution(quest, solution)
            .ok_or_else(|| refused("quest solution is absent"))?;
        if !self.condition_for_state(state, actor, &selected.condition) {
            return Err(refused("quest solution condition is not satisfied"));
        }
        let inventory = &mut state
            .actors
            .get_mut(&actor)
            .ok_or_else(|| refused("quest actor is absent"))?
            .inventory;
        for cost in &selected.consume {
            let remaining = inventory
                .get(&cost.item)
                .copied()
                .unwrap_or_default()
                .checked_sub(cost.quantity)
                .ok_or_else(|| refused("quest solution item missing"))?;
            if remaining == 0 {
                inventory.remove(&cost.item);
            } else {
                inventory.insert(cost.item, remaining);
            }
        }
        let definition = self
            .document
            .quest(quest)
            .ok_or_else(|| refused("quest is absent"))?;
        if let Some(item) = definition.reward_item {
            self.add_item(state, actor, item, definition.reward_quantity)?;
        }
        state.quests.insert(quest, QuestStatus::Completed);
        state
            .narrative
            .as_mut()
            .ok_or_else(|| refused("narrative state is absent"))?
            .quest_solutions
            .insert(quest, solution);
        Ok(())
    }

    #[must_use]
    pub fn quest_journal(&self) -> Vec<QuestJournalEntry> {
        self.document
            .quests
            .iter()
            .filter_map(|quest| {
                let status = *self.state.quests.get(&quest.id)?;
                let text = self.document.quest_flow(quest.id).map_or_else(
                    || quest.name.clone(),
                    |flow| {
                        match status {
                            QuestStatus::Available => &flow.journal_available,
                            QuestStatus::Active => &flow.journal_active,
                            QuestStatus::Completed => &flow.journal_completed,
                            QuestStatus::Failed => &flow.journal_failed,
                        }
                        .clone()
                    },
                );
                Some(QuestJournalEntry {
                    quest: quest.id,
                    name: quest.name.clone(),
                    status,
                    text,
                    solution: self
                        .state
                        .narrative
                        .as_ref()
                        .and_then(|narrative| narrative.quest_solutions.get(&quest.id))
                        .copied(),
                })
            })
            .collect()
    }

    pub(crate) fn validate_narrative_state(&self) -> Result<(), GameplayError> {
        let invalid = |message: &str| GameplayError::IncompatibleSave(message.into());
        if self
            .state
            .contacts
            .values()
            .flat_map(|contact| &contact.events)
            .chain(self.state.last_frame.iter().flat_map(|frame| &frame.events))
            .any(|event| matches!(event, GameplayEvent::VariableChanged { .. }))
        {
            return Err(invalid(
                "variable event outside an authoritative action receipt",
            ));
        }
        let mut expected = self
            .document
            .narrative
            .as_ref()
            .map(crate::NarrativeDefinition::initial_state);
        for receipt in &self.state.receipts {
            if matches!(receipt.request.action, crate::GameAction::RunScript { .. }) {
                self.replay_script_receipt(&mut expected, receipt)?;
                continue;
            }
            let crate::GameAction::ChooseDialogue {
                speaker,
                node,
                choice,
            } = receipt.request.action
            else {
                if receipt
                    .events
                    .iter()
                    .any(|event| matches!(event, GameplayEvent::VariableChanged { .. }))
                {
                    return Err(invalid("variable event outside dialogue receipt"));
                }
                continue;
            };
            let selected = self
                .document
                .actor(speaker)
                .and_then(|actor| actor.dialogue)
                .and_then(|id| self.document.dialogue(id))
                .and_then(|dialogue| dialogue.nodes.iter().find(|entry| entry.id == node))
                .and_then(|node| node.choices.iter().find(|entry| entry.id == choice))
                .ok_or_else(|| invalid("narrative receipt source is absent"))?;
            let mut variable_events = Vec::new();
            for effect in &selected.effects {
                replay_narrative_effect(&mut expected, effect, &mut variable_events)?;
            }
            let actual: Vec<_> = receipt
                .events
                .iter()
                .filter(|event| matches!(event, GameplayEvent::VariableChanged { .. }))
                .cloned()
                .collect();
            if actual != variable_events {
                return Err(invalid("variable events differ from authored effects"));
            }
        }
        if self.state.narrative != expected {
            return Err(invalid("narrative values differ from retained actions"));
        }
        for (quest, status) in &self.state.quests {
            if *status == QuestStatus::Completed
                && self.document.quest_flow(*quest).is_some()
                && expected
                    .as_ref()
                    .is_none_or(|state| !state.quest_solutions.contains_key(quest))
            {
                return Err(invalid("completed narrative quest has no solution"));
            }
        }
        Ok(())
    }
}

fn replay_narrative_effect(
    expected: &mut Option<NarrativeState>,
    effect: &DialogueEffect,
    events: &mut Vec<GameplayEvent>,
) -> Result<(), GameplayError> {
    let invalid = || GameplayError::IncompatibleSave("invalid narrative action journal".into());
    match effect {
        DialogueEffect::SetVariable { variable, value } => {
            let current = expected
                .as_mut()
                .and_then(|state| state.variables.get_mut(variable))
                .ok_or_else(invalid)?;
            if !current.same_type(value) {
                return Err(invalid());
            }
            current.clone_from(value);
            events.push(GameplayEvent::VariableChanged {
                variable: *variable,
                value: value.clone(),
            });
        }
        DialogueEffect::CompleteQuestSolution { quest, solution } => {
            let None = expected
                .as_mut()
                .ok_or_else(invalid)?
                .quest_solutions
                .insert(*quest, *solution)
            else {
                return Err(invalid());
            };
        }
        _ => (),
    }
    Ok(())
}
