//! Prepared lot D candidates; normal project commands own preview/apply/history.
use crate::temporal::{
    AbilityDefinition, CalendarEventDefinition, EncounterDefinition, SimulationDefinition,
    TimedEffectDefinition,
};
use crate::{GameplayDocument, GameplayError};
use forge_core::EntityId;

impl GameplayDocument {
    pub fn with_simulation_definition(
        &self,
        simulation: Option<SimulationDefinition>,
    ) -> Result<Self, GameplayError> {
        self.validate()?;
        let mut candidate = self.clone();
        candidate.simulation = simulation;
        if candidate.simulation.is_some() && candidate.creator.is_none() {
            candidate.creator = Some(candidate.creator_configuration());
            candidate.version = crate::GAMEPLAY_CREATOR_VERSION;
        }
        candidate.validate()?;
        Ok(candidate)
    }

    pub(crate) fn actor_initially_present(&self, actor: EntityId) -> bool {
        self.simulation.as_ref().is_none_or(|simulation| {
            !simulation
                .encounters
                .iter()
                .any(|encounter| encounter.participants.contains(&actor))
        })
    }

    pub fn with_ability_definition(
        &self,
        ability: AbilityDefinition,
    ) -> Result<Self, GameplayError> {
        let mut simulation = self.simulation.clone().unwrap_or_default();
        if let Some(current) = simulation
            .abilities
            .iter_mut()
            .find(|current| current.id == ability.id)
        {
            *current = ability;
        } else {
            simulation.abilities.push(ability);
        }
        self.with_simulation_definition(Some(simulation))
    }

    pub fn with_timed_effect_definition(
        &self,
        effect: TimedEffectDefinition,
    ) -> Result<Self, GameplayError> {
        let mut simulation = self.simulation.clone().unwrap_or_default();
        if let Some(current) = simulation
            .effects
            .iter_mut()
            .find(|current| current.id == effect.id)
        {
            *current = effect;
        } else {
            simulation.effects.push(effect);
        }
        self.with_simulation_definition(Some(simulation))
    }

    pub fn with_calendar_event_definition(
        &self,
        event: CalendarEventDefinition,
    ) -> Result<Self, GameplayError> {
        let mut simulation = self.simulation.clone().unwrap_or_default();
        if let Some(current) = simulation
            .events
            .iter_mut()
            .find(|current| current.id == event.id)
        {
            *current = event;
        } else {
            simulation.events.push(event);
        }
        self.with_simulation_definition(Some(simulation))
    }

    pub fn with_encounter_definition(
        &self,
        encounter: EncounterDefinition,
    ) -> Result<Self, GameplayError> {
        let mut simulation = self.simulation.clone().unwrap_or_default();
        if let Some(current) = simulation
            .encounters
            .iter_mut()
            .find(|current| current.id == encounter.id)
        {
            *current = encounter;
        } else {
            simulation.encounters.push(encounter);
        }
        self.with_simulation_definition(Some(simulation))
    }
}
