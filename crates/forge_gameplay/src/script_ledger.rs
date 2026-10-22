//! Journal replay using the same bounded interpreter as execution.
use crate::scripts::{ScriptApiError, ScriptGameApi, run_script};
use crate::{
    GameAction, GameReceipt, GameSession, GameplayDocument, GameplayError, GameplayEvent,
    NarrativeState, NarrativeValue, ObjectKind,
};
use forge_core::EntityId;
use std::collections::BTreeMap;

fn invalid(reason: impl Into<String>) -> GameplayError {
    GameplayError::IncompatibleSave(format!("script ledger: {}", reason.into()))
}

#[derive(Clone)]
struct ReplayApi<'a> {
    document: &'a GameplayDocument,
    script: EntityId,
    narrative: Option<NarrativeState>,
    events: Vec<GameplayEvent>,
}

impl ScriptGameApi for ReplayApi<'_> {
    fn read_variable(&self, variable: EntityId) -> Result<NarrativeValue, ScriptApiError> {
        self.narrative
            .as_ref()
            .and_then(|state| state.variables.get(&variable))
            .cloned()
            .ok_or_else(|| "retained script reads an absent variable".into())
    }
    fn write_variable(
        &mut self,
        variable: EntityId,
        value: &NarrativeValue,
    ) -> Result<(), ScriptApiError> {
        self.document
            .validate_variable_value(variable, value)
            .map_err(|error| error.to_string())?;
        let current = self
            .narrative
            .as_mut()
            .and_then(|state| state.variables.get_mut(&variable))
            .ok_or_else(|| "retained script writes an absent variable".to_owned())?;
        current.clone_from(value);
        self.events.push(GameplayEvent::VariableChanged {
            variable,
            value: value.clone(),
        });
        Ok(())
    }
    fn set_door(&mut self, object: EntityId, open: bool) -> Result<(), ScriptApiError> {
        if !self
            .document
            .object(object)
            .is_some_and(|object| matches!(object.kind, ObjectKind::Door { .. }))
        {
            return Err("retained script writes an absent door".into());
        }
        self.events
            .push(GameplayEvent::DoorChanged { object, open });
        Ok(())
    }
    fn message(&mut self, text: &str) -> Result<(), ScriptApiError> {
        self.events.push(GameplayEvent::ScriptMessage {
            script: self.script,
            text: text.into(),
        });
        Ok(())
    }
}

impl GameSession {
    pub(crate) fn replay_script_receipt(
        &self,
        expected: &mut Option<NarrativeState>,
        receipt: &GameReceipt,
    ) -> Result<(), GameplayError> {
        let GameAction::RunScript { script } = receipt.request.action else {
            return Err(invalid("receipt is not a script call"));
        };
        let definition = self
            .document
            .script_definition(script)
            .ok_or_else(|| invalid("script source is absent"))?;
        let mut api = ReplayApi {
            document: &self.document,
            script,
            narrative: expected.clone(),
            events: vec![],
        };
        let steps = run_script(definition, &mut api)
            .map_err(|error| invalid(format!("{} at {:?}", error.cause, error.instruction)))?;
        api.events
            .push(GameplayEvent::ScriptExecuted { script, steps });
        if receipt.events != api.events {
            return Err(invalid("retained outputs differ from the authored program"));
        }
        *expected = api.narrative;
        Ok(())
    }

    pub(crate) fn validate_script_output_authority(&self) -> Result<(), GameplayError> {
        let is_script_output = |event: &GameplayEvent| {
            matches!(
                event,
                GameplayEvent::ScriptMessage { .. } | GameplayEvent::ScriptExecuted { .. }
            )
        };
        if self
            .state
            .contacts
            .values()
            .flat_map(|contact| &contact.events)
            .chain(self.state.last_frame.iter().flat_map(|frame| &frame.events))
            .any(is_script_output)
        {
            return Err(invalid("script output outside an action receipt"));
        }
        for receipt in &self.state.receipts {
            if !matches!(receipt.request.action, GameAction::RunScript { .. })
                && receipt.events.iter().any(is_script_output)
            {
                return Err(invalid("script output outside its source call"));
            }
        }
        Ok(())
    }

    /// Variable replay already verifies each script's exact door events. This
    /// projection also covers ordinary interactions and authored dialogue effects.
    pub(crate) fn validate_door_projection(&self) -> Result<(), GameplayError> {
        let mut doors: BTreeMap<_, _> = self
            .document
            .objects
            .iter()
            .filter_map(|object| {
                if let ObjectKind::Door { initially_open } = object.kind {
                    Some((object.id, initially_open))
                } else {
                    None
                }
            })
            .collect();
        if self
            .state
            .contacts
            .values()
            .flat_map(|contact| &contact.events)
            .chain(self.state.last_frame.iter().flat_map(|frame| &frame.events))
            .any(|event| matches!(event, GameplayEvent::DoorChanged { .. }))
        {
            return Err(invalid("door state event outside an action receipt"));
        }
        for receipt in &self.state.receipts {
            let actual: Vec<_> = receipt
                .events
                .iter()
                .filter_map(|event| {
                    if let GameplayEvent::DoorChanged { object, open } = event {
                        Some((*object, *open))
                    } else {
                        None
                    }
                })
                .collect();
            let expected = match receipt.request.action {
                GameAction::SetDoor { object, open } => vec![(object, open)],
                GameAction::ChooseDialogue {
                    speaker,
                    node,
                    choice,
                } => {
                    let selected = self
                        .document
                        .actor(speaker)
                        .and_then(|actor| actor.dialogue)
                        .and_then(|dialogue| self.document.dialogue(dialogue))
                        .and_then(|dialogue| dialogue.nodes.iter().find(|entry| entry.id == node))
                        .and_then(|node| node.choices.iter().find(|entry| entry.id == choice))
                        .ok_or_else(|| invalid("door dialogue source is absent"))?;
                    selected
                        .effects
                        .iter()
                        .filter_map(|effect| {
                            if let crate::DialogueEffect::SetDoor { object, open } = effect {
                                Some((*object, *open))
                            } else {
                                None
                            }
                        })
                        .collect()
                }
                GameAction::RunScript { .. } => actual.clone(),
                _ => vec![],
            };
            if actual != expected {
                return Err(invalid("door outputs differ from their source action"));
            }
            for (door, open) in actual {
                *doors
                    .get_mut(&door)
                    .ok_or_else(|| invalid("door projection reference is absent"))? = open;
            }
        }
        for (door, open) in doors {
            if self
                .state
                .objects
                .get(&door)
                .is_none_or(|state| state.open != open)
            {
                return Err(invalid("door state differs from retained actions"));
            }
        }
        Ok(())
    }
}
