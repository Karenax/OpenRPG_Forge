//! Renderer identity shared by mesh picking and imported placement adapters.
use bevy::prelude::Component;

/// The canonical placement owning a visible mesh, independent of asset format.
#[derive(Component)]
pub struct ManualPlacementPart(pub forge_core::EntityId);
