#![deny(unsafe_code)]
//! Authored definitions and simulation state are separate; no engine or UI handles.

mod actions;
mod document;
mod environment;
mod integrity;
mod save;
mod simulation;
mod validation;

pub use actions::{DialogueOption, DialogueView};
pub use document::*;
pub use environment::{
    ActorObservation, ContactObservation, ContactReceipt, DangerObservation, HeatExposure,
    ObjectObservation, PhysicsFrame, PhysicsFrameReceipt,
};
pub use simulation::*;

use thiserror::Error;

#[derive(Clone, Debug, Error, PartialEq, Eq)]
pub enum GameplayError {
    #[error("invalid gameplay document: {0}")]
    InvalidDocument(String),
    #[error("gameplay reference missing: {0}")]
    MissingReference(String),
    #[error("action refused: {0}")]
    ActionRefused(String),
    #[error("save incompatible: {0}")]
    IncompatibleSave(String),
    #[error("gameplay codec: {0}")]
    Codec(String),
}
