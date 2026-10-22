//! Authored candidates are promoted only by the caller's normal command bus.
use crate::{DialogueDefinition, GameplayDocument, GameplayError, NarrativeDefinition, QuestFlow};

impl GameplayDocument {
    pub fn with_narrative_definition(
        &self,
        narrative: Option<NarrativeDefinition>,
    ) -> Result<Self, GameplayError> {
        self.validate()?;
        let mut candidate = self.clone();
        candidate.narrative = narrative;
        if candidate.narrative.is_some() && candidate.creator.is_none() {
            let configuration = candidate.creator_configuration();
            candidate.creator = Some(configuration);
            candidate.version = crate::GAMEPLAY_CREATOR_VERSION;
        }
        candidate.validate()?;
        Ok(candidate)
    }

    pub fn with_dialogue_definition(
        &self,
        dialogue: DialogueDefinition,
    ) -> Result<Self, GameplayError> {
        self.validate()?;
        let mut candidate = self.clone();
        if let Some(previous) = candidate
            .dialogues
            .iter_mut()
            .find(|entry| entry.id == dialogue.id)
        {
            *previous = dialogue;
        } else {
            candidate.dialogues.push(dialogue);
        }
        candidate.validate()?;
        Ok(candidate)
    }

    pub fn with_quest_flow(&self, flow: QuestFlow) -> Result<Self, GameplayError> {
        let mut narrative = self.narrative.clone().unwrap_or(NarrativeDefinition {
            version: 1,
            variables: vec![],
            quests: vec![],
        });
        if let Some(previous) = narrative
            .quests
            .iter_mut()
            .find(|entry| entry.quest == flow.quest)
        {
            *previous = flow;
        } else {
            narrative.quests.push(flow);
        }
        self.with_narrative_definition(Some(narrative))
    }

    /// Explicit previewable adoption: pin existing turn-ins to one chosen solution.
    pub fn with_quest_flow_and_legacy_solution(
        &self,
        flow: QuestFlow,
        legacy_solution: forge_core::EntityId,
    ) -> Result<Self, GameplayError> {
        self.validate()?;
        if !flow
            .solutions
            .iter()
            .any(|solution| solution.id == legacy_solution)
        {
            return Err(GameplayError::MissingReference(format!(
                "legacy solution {legacy_solution}"
            )));
        }
        let quest = flow.quest;
        let mut candidate = self.clone();
        let mut narrative = candidate.narrative.take().unwrap_or(NarrativeDefinition {
            version: 1,
            variables: vec![],
            quests: vec![],
        });
        if let Some(previous) = narrative
            .quests
            .iter_mut()
            .find(|entry| entry.quest == quest)
        {
            *previous = flow;
        } else {
            narrative.quests.push(flow);
        }
        candidate.narrative = Some(narrative);
        for effect in candidate
            .dialogues
            .iter_mut()
            .flat_map(|dialogue| &mut dialogue.nodes)
            .flat_map(|node| &mut node.choices)
            .flat_map(|choice| &mut choice.effects)
        {
            if matches!(effect,crate::DialogueEffect::CompleteQuest {quest:id} if *id==quest) {
                *effect = crate::DialogueEffect::CompleteQuestSolution {
                    quest,
                    solution: legacy_solution,
                };
            }
        }
        if candidate.creator.is_none() {
            candidate.creator = Some(candidate.creator_configuration());
            candidate.version = crate::GAMEPLAY_CREATOR_VERSION;
        }
        candidate.validate()?;
        Ok(candidate)
    }
}
