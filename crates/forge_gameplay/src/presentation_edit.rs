//! Document candidates; the common command bus owns persistence.
use crate::cinematics::{CinematicDefinition, CinematicsDefinition};
use crate::scripts::{ScriptDefinition, ScriptingDefinition};
use crate::{GameplayDocument, GameplayError};
use forge_core::EntityId;

impl GameplayDocument {
    #[must_use]
    pub fn script_definition(&self, script: EntityId) -> Option<&ScriptDefinition> {
        self.scripting
            .as_ref()?
            .scripts
            .iter()
            .find(|entry| entry.id == script)
    }

    #[must_use]
    pub fn cinematic_definition(&self, sequence: EntityId) -> Option<&CinematicDefinition> {
        self.cinematics
            .as_ref()?
            .sequences
            .iter()
            .find(|entry| entry.id == sequence)
    }

    pub fn with_scripting_definition(
        &self,
        scripting: Option<ScriptingDefinition>,
    ) -> Result<Self, GameplayError> {
        self.validate()?;
        let mut candidate = self.clone();
        candidate.scripting = scripting;
        candidate.enable_extended_creator();
        candidate.validate()?;
        Ok(candidate)
    }

    pub fn with_script_definition(&self, script: ScriptDefinition) -> Result<Self, GameplayError> {
        let mut scripting = self.scripting.clone().unwrap_or(ScriptingDefinition {
            version: 1,
            scripts: vec![],
        });
        if let Some(previous) = scripting
            .scripts
            .iter_mut()
            .find(|entry| entry.id == script.id)
        {
            *previous = script;
        } else {
            scripting.scripts.push(script);
        }
        self.with_scripting_definition(Some(scripting))
    }

    pub fn with_cinematics_definition(
        &self,
        cinematics: Option<CinematicsDefinition>,
    ) -> Result<Self, GameplayError> {
        self.validate()?;
        let mut candidate = self.clone();
        candidate.cinematics = cinematics;
        if candidate.cinematics.is_some() && candidate.simulation.is_none() {
            candidate.simulation = Some(crate::temporal::SimulationDefinition::default());
        }
        candidate.enable_extended_creator();
        candidate.validate()?;
        Ok(candidate)
    }

    pub fn with_cinematic_definition(
        &self,
        sequence: CinematicDefinition,
    ) -> Result<Self, GameplayError> {
        let mut cinematics = self.cinematics.clone().unwrap_or(CinematicsDefinition {
            version: 1,
            sequences: vec![],
        });
        if let Some(previous) = cinematics
            .sequences
            .iter_mut()
            .find(|entry| entry.id == sequence.id)
        {
            *previous = sequence;
        } else {
            cinematics.sequences.push(sequence);
        }
        self.with_cinematics_definition(Some(cinematics))
    }

    fn enable_extended_creator(&mut self) {
        if (self.scripting.is_some() || self.cinematics.is_some()) && self.creator.is_none() {
            self.creator = Some(self.creator_configuration());
            self.version = crate::GAMEPLAY_CREATOR_VERSION;
        }
    }
}
