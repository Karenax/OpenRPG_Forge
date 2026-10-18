//! Marks temporary offscreen views excluded from interactive scene tools.
use bevy::prelude::*;

#[derive(Component, Clone, Copy, Debug, Default)]
pub struct OffscreenSceneCamera;
