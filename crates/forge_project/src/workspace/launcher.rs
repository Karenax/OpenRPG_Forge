//! Project-owned game intent. Delivery and runtime adapters consume derived data.
use super::{MAX_MODULES, MAX_ZONES, ProjectEntry, WorkspaceError};
use crate::VisualQualityTier;
use forge_core::{AreaId, AssetId, ContentHash, ModuleId, ProjectId};
use serde::{Deserialize, Serialize};
use std::collections::BTreeSet;

pub const GAME_CONFIGURATION_VERSION: u32 = 1;
pub const MAX_GAME_SUMMARY_BYTES: usize = 4096;

/// A reference only: the existing asset catalogue remains responsible for
/// provenance, licence, file admission and resolution of these exact bytes.
#[derive(Clone, Copy, Debug, PartialEq, Eq, Serialize, Deserialize)]
#[serde(deny_unknown_fields)]
pub struct PresentationAsset {
    pub asset_id: AssetId,
    pub content_hash: ContentHash,
}

#[derive(Clone, Copy, Debug, PartialEq, Eq, Serialize, Deserialize)]
#[serde(rename_all = "snake_case")]
pub enum DisplayMode {
    Windowed,
    Borderless,
    Fullscreen,
}

/// Initial defaults, distinct from a player's persistent choices.
#[derive(Clone, Debug, PartialEq, Eq, Serialize, Deserialize)]
#[serde(deny_unknown_fields)]
pub struct PlayerDefaults {
    pub quality: VisualQualityTier,
    pub display_mode: DisplayMode,
    pub master_volume: u8,
    pub music_volume: u8,
    pub reduce_motion: bool,
    pub subtitles: bool,
}

#[derive(Clone, Copy, Debug, PartialEq, Eq, Serialize, Deserialize)]
#[serde(deny_unknown_fields)]
pub struct GameLocation {
    pub module_id: ModuleId,
    pub area_id: AreaId,
}

#[derive(Clone, Debug, PartialEq, Eq, Serialize, Deserialize)]
#[serde(deny_unknown_fields)]
pub struct GameContent {
    pub module_id: ModuleId,
    pub areas: Vec<AreaId>,
}

/// Authored once under the project; it is never a second delivery source.
#[derive(Clone, Debug, PartialEq, Eq, Serialize, Deserialize)]
#[serde(deny_unknown_fields)]
pub struct GameConfiguration {
    pub version: u32,
    pub title: String,
    pub summary: String,
    pub icon: Option<PresentationAsset>,
    pub logo: Option<PresentationAsset>,
    pub background: Option<PresentationAsset>,
    pub contents: Vec<GameContent>,
    pub entry: GameLocation,
    pub defaults: PlayerDefaults,
}

impl GameConfiguration {
    /// Validate portable content intent without consulting a workspace or files.
    pub fn validate_shape(&self) -> Result<(), WorkspaceError> {
        if self.version != GAME_CONFIGURATION_VERSION {
            return Err(WorkspaceError::Game("unsupported configuration version"));
        }
        super::validate_name(&self.title)?;
        if self.summary.len() > MAX_GAME_SUMMARY_BYTES
            || self
                .summary
                .chars()
                .any(|c| c.is_control() && !matches!(c, '\n' | '\t'))
        {
            return Err(WorkspaceError::Game("invalid presentation summary"));
        }
        if self.defaults.master_volume > 100 || self.defaults.music_volume > 100 {
            return Err(WorkspaceError::Game("volume out of range"));
        }
        if self.contents.is_empty() || self.contents.len() > MAX_MODULES {
            return Err(WorkspaceError::Game("invalid content count"));
        }
        let mut modules = BTreeSet::new();
        let mut total_areas = 0_usize;
        let mut includes_entry = false;
        for content in &self.contents {
            if !modules.insert(content.module_id) || content.areas.is_empty() {
                return Err(WorkspaceError::Game(
                    "duplicate module or empty area selection",
                ));
            }
            let mut areas = BTreeSet::new();
            for area in &content.areas {
                total_areas += 1;
                if total_areas > MAX_ZONES || !areas.insert(*area) {
                    return Err(WorkspaceError::Game("duplicate or excessive area"));
                }
                includes_entry |=
                    content.module_id == self.entry.module_id && *area == self.entry.area_id;
            }
        }
        if !includes_entry {
            return Err(WorkspaceError::Game("entry is not included in the game"));
        }
        Ok(())
    }

    pub(super) fn validate(&self, project: &ProjectEntry) -> Result<(), WorkspaceError> {
        self.validate_shape()?;
        if project.id.as_uuid().is_nil() {
            return Err(WorkspaceError::Game("game project identity cannot be nil"));
        }
        for content in &self.contents {
            let module = project
                .modules
                .iter()
                .find(|module| module.id == content.module_id)
                .ok_or(WorkspaceError::Game("module is not part of the project"))?;
            if content
                .areas
                .iter()
                .any(|area| !module.zones.iter().any(|zone| zone.area_id == *area))
            {
                return Err(WorkspaceError::Game("area is not part of the module"));
            }
        }
        Ok(())
    }
}

/// Read-only projection bound to an exact validated workspace. Packaging still
/// must resolve and admit all content, rights and platform-specific binaries.
#[derive(Clone, Debug, PartialEq, Eq, Serialize, Deserialize)]
#[serde(deny_unknown_fields)]
pub struct DerivedGameManifest {
    pub(super) schema: String,
    pub(super) version: u32,
    pub(super) game_id: ProjectId,
    pub(super) source_workspace_hash: ContentHash,
    pub(super) configuration: GameConfiguration,
    pub(super) save_namespace: String,
}

impl DerivedGameManifest {
    /// Parse a bounded delivery projection. Container integrity must be checked separately.
    pub fn from_bytes(bytes: &[u8]) -> Result<Self, WorkspaceError> {
        if bytes.len() > super::MAX_WORKSPACE_BYTES {
            return Err(WorkspaceError::Limit("game manifest bytes"));
        }
        let manifest: Self = serde_json::from_slice(bytes)?;
        manifest.validate()?;
        Ok(manifest)
    }

    pub fn validate(&self) -> Result<(), WorkspaceError> {
        if self.schema != "forge.game-launch-manifest"
            || self.version != 1
            || self.game_id.as_uuid().is_nil()
            || self.save_namespace != format!("games/{}/saves", self.game_id)
        {
            return Err(WorkspaceError::Game(
                "invalid delivered game identity or version",
            ));
        }
        self.configuration.validate_shape()
    }

    #[must_use]
    pub const fn game_id(&self) -> ProjectId {
        self.game_id
    }

    #[must_use]
    pub const fn source_workspace_hash(&self) -> ContentHash {
        self.source_workspace_hash
    }

    /// Portable relative namespace; creating or migrating directories is a
    /// separate delivery operation and must preserve existing player data.
    #[must_use]
    pub fn save_namespace(&self) -> &str {
        &self.save_namespace
    }

    #[must_use]
    pub const fn configuration(&self) -> &GameConfiguration {
        &self.configuration
    }
}
