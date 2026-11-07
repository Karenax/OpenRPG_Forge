//! Bevy adapter for the engine-independent native gameplay save service.
use bevy::prelude::Resource;
use forge_gameplay::{GameplayDocument, native_storage};
use std::{
    ops::{Deref, DerefMut},
    path::Path,
};

#[derive(Resource)]
pub(super) struct GameStorage(native_storage::GameStorage);

impl GameStorage {
    pub(super) fn open(root: &Path, document: &GameplayDocument) -> Result<Self, String> {
        native_storage::GameStorage::open(root, document).map(Self)
    }

    pub(super) fn save(&mut self, bytes: &[u8]) -> Result<(), String> {
        self.0.save_with_diagnostic(bytes, |error| {
            tracing::warn!(target: "forge::save", %error, "Invalid save ownership envelope before storage");
        })
    }
}

impl Deref for GameStorage {
    type Target = native_storage::GameStorage;

    fn deref(&self) -> &Self::Target {
        &self.0
    }
}

impl DerefMut for GameStorage {
    fn deref_mut(&mut self) -> &mut Self::Target {
        &mut self.0
    }
}
