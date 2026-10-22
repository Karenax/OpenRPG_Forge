//! Local consequences share the outer action transaction and the saved receipt ledger.
use crate::{
    FactionConsequence, FactionWitnessObservation, FactionWitnessReceipt, FactionsDefinition,
    FactionsState, GameRequest, GameSession, GameState, GameplayDocument, GameplayError,
    GameplayEvent,
};
use forge_core::EntityId;

impl GameplayDocument {
    pub fn with_factions_definition(
        &self,
        factions: Option<FactionsDefinition>,
    ) -> Result<Self, GameplayError> {
        self.validate()?;
        let mut candidate = self.clone();
        candidate.factions = factions;
        if candidate.factions.is_some() && candidate.creator.is_none() {
            candidate.creator = Some(candidate.creator_configuration());
            candidate.version = crate::GAMEPLAY_CREATOR_VERSION;
        }
        candidate.validate()?;
        Ok(candidate)
    }
}

impl GameSession {
    #[must_use]
    pub fn personal_relation(&self, observer: EntityId, subject: EntityId) -> Option<i16> {
        self.relation_for_state(&self.state, observer, subject)
    }

    pub(crate) fn relation_for_state(
        &self,
        state: &GameState,
        observer: EntityId,
        subject: EntityId,
    ) -> Option<i16> {
        if !state.actors.contains_key(&observer) || !state.actors.contains_key(&subject) {
            return None;
        }
        Some(state.factions.as_ref()?.personal_relation(
            self.document.factions.as_ref()?,
            observer,
            subject,
        ))
    }

    pub(crate) fn snapshot_faction_witnesses(
        &self,
        request: &GameRequest,
        observations: &[FactionWitnessObservation],
    ) -> Result<Vec<FactionWitnessReceipt>, GameplayError> {
        let Some(definitions) = &self.document.factions else {
            return if observations.is_empty() {
                Ok(Vec::new())
            } else {
                Err(GameplayError::ActionRefused(
                    "factions are absent from this content".into(),
                ))
            };
        };
        crate::faction_observation::snapshot_witnesses(
            definitions,
            &self.state,
            request,
            observations,
        )
    }

    pub(crate) fn apply_faction_reactions(
        &self,
        state: &mut GameState,
        request: &GameRequest,
        witnesses: &[FactionWitnessReceipt],
    ) -> Result<Vec<GameplayEvent>, GameplayError> {
        let Some(definitions) = &self.document.factions else {
            return Ok(Vec::new());
        };
        let opinions = state
            .factions
            .as_mut()
            .ok_or_else(|| GameplayError::ActionRefused("local faction state is absent".into()))?;
        let changes = crate::faction_observation::consequences(
            definitions,
            &self.document,
            opinions,
            request,
            witnesses,
        )?;
        crate::faction_ledger::promote(opinions, &changes);
        Ok(changes
            .into_iter()
            .map(|change| GameplayEvent::FactionReaction { change })
            .collect())
    }

    pub(crate) fn validate_faction_state(&self) -> Result<(), GameplayError> {
        let invalid =
            |message: &str| GameplayError::IncompatibleSave(format!("local factions: {message}"));
        if self
            .state
            .contacts
            .values()
            .flat_map(|receipt| &receipt.events)
            .chain(
                self.state
                    .last_frame
                    .iter()
                    .flat_map(|receipt| &receipt.events),
            )
            .any(|event| matches!(event, GameplayEvent::FactionReaction { .. }))
        {
            return Err(invalid("reaction outside an authoritative action receipt"));
        }
        let mut expected = self
            .document
            .factions
            .as_ref()
            .map(|_| FactionsState::default());
        for receipt in &self.state.receipts {
            let changes: Vec<FactionConsequence> = receipt
                .events
                .iter()
                .filter_map(|event| {
                    if let GameplayEvent::FactionReaction { change } = event {
                        Some(change.clone())
                    } else {
                        None
                    }
                })
                .collect();
            let Some(definitions) = &self.document.factions else {
                if !changes.is_empty() || !receipt.faction_witnesses.is_empty() {
                    return Err(invalid("faction observations without authored definitions"));
                }
                continue;
            };
            let opinions = expected
                .as_mut()
                .ok_or_else(|| invalid("missing initial projection"))?;
            crate::faction_ledger::replay(
                definitions,
                &self.document,
                opinions,
                &crate::faction_ledger::LocalFactionAction {
                    receipt,
                    witnesses: &receipt.faction_witnesses,
                    changes: &changes,
                },
            )?;
        }
        if self.state.factions != expected {
            return Err(invalid("relations differ from their retained local causes"));
        }
        Ok(())
    }
}
