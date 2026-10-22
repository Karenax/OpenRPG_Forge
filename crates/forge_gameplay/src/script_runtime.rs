//! Transactional game API adapter for the bounded script interpreter.
use crate::scripts::{ScriptApiError, ScriptGameApi, run_script};
use crate::{
    DialogueEffect, GameRequest, GameSession, GameState, GameplayError, GameplayEvent,
    NarrativeValue,
};
use forge_core::EntityId;

#[derive(Clone)]
struct ScriptCandidate<'a> {
    session: &'a GameSession,
    request: &'a GameRequest,
    script: EntityId,
    state: GameState,
    events: Vec<GameplayEvent>,
    event_limit: usize,
}

impl ScriptCandidate<'_> {
    fn append(
        &mut self,
        events: impl IntoIterator<Item = GameplayEvent>,
    ) -> Result<(), ScriptApiError> {
        for event in events {
            if self.events.len() >= self.event_limit {
                return Err(ScriptApiError::EventBudget);
            }
            self.events.push(event);
        }
        Ok(())
    }
}

impl ScriptGameApi for ScriptCandidate<'_> {
    fn read_variable(&self, variable: EntityId) -> Result<NarrativeValue, ScriptApiError> {
        self.state
            .narrative
            .as_ref()
            .and_then(|state| state.variables.get(&variable))
            .cloned()
            .ok_or_else(|| "narrative variable is absent".into())
    }

    fn write_variable(
        &mut self,
        variable: EntityId,
        value: &NarrativeValue,
    ) -> Result<(), ScriptApiError> {
        self.session
            .document
            .validate_variable_value(variable, value)
            .map_err(|error| error.to_string())?;
        let events = self
            .session
            .apply_narrative_effect(
                &mut self.state,
                self.request.actor,
                self.request.actor,
                &DialogueEffect::SetVariable {
                    variable,
                    value: value.clone(),
                },
            )
            .map_err(|error| error.to_string())?;
        self.append(events)
    }

    fn set_door(&mut self, object: EntityId, open: bool) -> Result<(), ScriptApiError> {
        // Use the ordinary range, identity and availability checks. The helper
        // becomes crate-visible at integration; its behavior is not duplicated.
        let events = self
            .session
            .apply_door(&mut self.state, self.request, object, open)
            .map_err(|error| error.to_string())?;
        self.append(events)
    }

    fn message(&mut self, text: &str) -> Result<(), ScriptApiError> {
        self.append([GameplayEvent::ScriptMessage {
            script: self.script,
            text: text.into(),
        }])
    }
}

impl GameSession {
    pub(crate) fn apply_script(
        &self,
        state: &mut GameState,
        request: &GameRequest,
        script: EntityId,
    ) -> Result<Vec<GameplayEvent>, GameplayError> {
        let definition = self
            .document
            .script_definition(script)
            .ok_or_else(|| GameplayError::MissingReference(format!("script {script}")))?;
        let event_limit = self
            .document
            .profile
            .max_events_per_tick
            .checked_sub(state.tick_events_used)
            .and_then(|remaining| remaining.checked_sub(1))
            .ok_or_else(|| {
                GameplayError::ActionRefused("no event budget for script execution".into())
            })?;
        let mut candidate = ScriptCandidate {
            session: self,
            request,
            script,
            state: state.clone(),
            events: vec![],
            event_limit,
        };
        let steps = run_script(definition, &mut candidate).map_err(GameplayError::ScriptRefused)?;
        candidate
            .events
            .push(GameplayEvent::ScriptExecuted { script, steps });
        *state = candidate.state;
        Ok(candidate.events)
    }
}
