//! Typed display-control bridge. An installed host supplies the actual renderer and storage.
use bevy::prelude::*;
use forge_project::VisualQualityTier;

#[derive(Message, Clone, Copy, Debug)]
pub struct NativeGraphicsCommand(pub VisualQualityTier);

#[derive(Resource, Clone, Debug, serde::Serialize)]
pub struct NativeGraphicsStatus {
    pub request_sequence: u64,
    pub requested: VisualQualityTier,
    pub applied: Option<VisualQualityTier>,
    pub persisted: Option<VisualQualityTier>,
    pub menu_open: bool,
    pub ready: bool,
    pub error: Option<String>,
}
