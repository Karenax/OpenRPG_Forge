#![deny(unsafe_code)]
//! Authored definitions and simulation state are separate; no engine or UI handles.

mod ability_runtime;
mod actions;
mod blueprints;
mod cinematic_ledger;
mod cinematic_runtime;
mod cinematic_validation;
mod cinematics;
mod container_ledger;
mod containers;
mod creator;
mod document;
mod environment;
mod faction_ledger;
mod faction_observation;
mod faction_runtime;
mod factions;
mod integrity;
mod item_sources;
mod module_creator;
mod narrative;
mod narrative_conditions;
mod narrative_edit;
mod narrative_runtime;
mod presentation_edit;
mod rules;
mod save;
mod script_edit;
mod script_ledger;
mod script_runtime;
mod script_validation;
mod scripts;
mod simulation;
mod temporal;
mod temporal_clock;
mod temporal_edit;
mod temporal_integrity;
mod temporal_schedule;
mod temporal_validation;
mod validation;

pub use actions::{DialogueOption, DialogueView};
pub use blueprints::*;
pub use cinematics::*;
pub use containers::{ContainerDefinition, ContainerState, MAX_INVENTORY_UNITS, TransferDirection};
pub use creator::*;
pub use document::*;
pub use environment::{
    ActorObservation, ContactObservation, ContactReceipt, DangerObservation, HeatExposure,
    ObjectObservation, PhysicsFrame, PhysicsFrameReceipt,
};
pub use factions::*;
pub use module_creator::{
    MAX_MODULE_CREATOR_BYTES, MODULE_CREATOR_SCHEMA, MODULE_CREATOR_VERSION, ModuleCreatorProfile,
};
pub use narrative::*;
pub use narrative_runtime::QuestJournalEntry;
pub use rules::{RuleField, RuleKey, RuleUnit};
pub use script_edit::{ScriptBranch, ScriptTreeLocation};
pub use scripts::{
    MAX_SCRIPT_DEPTH, MAX_SCRIPT_ITERATIONS, MAX_SCRIPT_MESSAGE_BYTES, MAX_SCRIPT_NODES,
    MAX_SCRIPT_STEPS, MAX_SCRIPTS, ScriptDefinition, ScriptDiagnostic, ScriptDiagnosticKind,
    ScriptInstruction, ScriptOperation, ScriptPermissions, ScriptingDefinition,
};
pub use simulation::*;
pub use temporal::*;

use thiserror::Error;

#[derive(Clone, Debug, Error, PartialEq, Eq)]
pub enum GameplayError {
    #[error("invalid gameplay document: {0}")]
    InvalidDocument(String),
    #[error("gameplay reference missing: {0}")]
    MissingReference(String),
    #[error("action refused: {0}")]
    ActionRefused(String),
    #[error("script refused: {0:?}")]
    ScriptRefused(ScriptDiagnostic),
    #[error("save incompatible: {0}")]
    IncompatibleSave(String),
    #[error("gameplay codec: {0}")]
    Codec(String),
}
