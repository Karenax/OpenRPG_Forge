//! Immutable simulation input and engine-neutral projection identity.
use crate::{TerrainDocument, VegetationDocument, zone_authoring::ZoneAuthoringHead};
use forge_core::ContentHash;
use serde::{Deserialize, Serialize};

/// An explicit launch override is temporary; the same neutral value may be authored in a zone.
pub type PlaytestStart = crate::zone_authoring::start::ZoneStart;

#[derive(Clone, Copy, Debug, PartialEq, Eq, Serialize, Deserialize)]
#[serde(deny_unknown_fields)]
pub struct PlaytestIdentity {
    pub source_hash: ContentHash,
    pub terrain_hash: ContentHash,
    pub vegetation_hash: Option<ContentHash>,
}

/// Runtime observation returned when a playtest exits. It is session-only
/// evidence: it must never be written into an authored zone archive.
#[derive(Clone, Debug, PartialEq, Serialize, Deserialize)]
#[serde(deny_unknown_fields)]
pub struct PlaytestExitDiagnostics {
    /// Binds the observation to the immutable snapshot that was actually started.
    pub identity: PlaytestIdentity,
    /// Last fixed simulation tick observed by the player runtime.
    pub fixed_tick: u64,
    /// Last player position, in world metres, captured before returning to edit mode.
    pub final_position_m: [f32; 3],
    pub grounded: bool,
    /// Rapier contacts reported by the player runtime, including the ground when applicable.
    pub collisions: usize,
    /// Whether the player simulation had accepted the derived projections at capture time.
    pub simulation_ready: bool,
    /// Human-readable runtime diagnostic available at the same capture point.
    pub runtime_status: String,
    /// Explicit end reason supplied by the UI, never inferred from an archive mutation.
    pub end_reason: String,
}

impl PlaytestExitDiagnostics {
    pub fn validate_for(&self, identity: PlaytestIdentity) -> Result<(), String> {
        if self.identity != identity {
            return Err("playtest diagnostics identity does not match the active snapshot".into());
        }
        if !self.final_position_m.iter().all(|value| value.is_finite()) {
            return Err("playtest diagnostics final position must be finite".into());
        }
        if self.runtime_status.trim().is_empty() || self.end_reason.trim().is_empty() {
            return Err("playtest diagnostics require runtime status and an end reason".into());
        }
        if self.runtime_status.len() > 16 * 1024 || self.end_reason.len() > 256 {
            return Err("playtest diagnostics text exceeds its bounded size".into());
        }
        Ok(())
    }
}

#[derive(Clone, Copy, Debug, PartialEq, Eq)]
pub enum ProjectionReadiness {
    StaleSource,
    WaitingTerrain,
    WaitingVegetation,
    Ready,
}

impl PlaytestIdentity {
    /// CPU/source barrier only: does not attest a presented frame or GPU completion.
    #[must_use]
    pub fn readiness(
        &self,
        active_source: ContentHash,
        terrain: Option<ContentHash>,
        vegetation: Option<ContentHash>,
    ) -> ProjectionReadiness {
        if active_source != self.source_hash {
            ProjectionReadiness::StaleSource
        } else if terrain != Some(self.terrain_hash) {
            ProjectionReadiness::WaitingTerrain
        } else if vegetation != self.vegetation_hash {
            ProjectionReadiness::WaitingVegetation
        } else {
            ProjectionReadiness::Ready
        }
    }
}

/// No public mutable fields and no deserializer: construct from a validated authority.
#[derive(Clone, Debug, PartialEq)]
pub struct PlaytestSnapshot {
    gameplay: Option<forge_gameplay::GameplayDocument>,
    identity: PlaytestIdentity,
    terrain: TerrainDocument,
    vegetation: Option<VegetationDocument>,
    placements: Vec<crate::zone_authoring::placements::ManualPlacement>,
    paths: Vec<crate::zone_authoring::paths::AuthoredPath>,
    rivers: Vec<crate::zone_authoring::rivers::AuthoredRiver>,
    buildings: Vec<crate::zone_authoring::buildings::AuthoredBuilding>,
    promotions: Vec<crate::zone_authoring::promotion::PromotedInstance>,
    start: Option<PlaytestStart>,
}

impl PlaytestSnapshot {
    /// Validates the full native response before a renderer starts a simulation.
    /// A preparation identity without an active playtest hash does not start play.
    pub fn from_state(state: &super::AuthoringState) -> Result<Option<Self>, String> {
        let Some(expected) = state.playtest_hash else {
            return Ok(None);
        };
        let document = state.document.as_ref().ok_or("playtest document missing")?;
        let snapshot = if let Some(head) = &state.zone_head {
            Self::from_zone(head, expected)?
        } else {
            Self::from_terrain(document, expected)?
        }
        .with_start(state.playtest_start)?;
        if snapshot.terrain() != document || state.playtest_identity != Some(snapshot.identity()) {
            return Err("playtest identity or effective document mismatch".into());
        }
        Ok(Some(snapshot))
    }

    pub fn from_zone(head: &ZoneAuthoringHead, expected_hash: ContentHash) -> Result<Self, String> {
        let source_hash = head.content_hash()?;
        if source_hash != expected_hash {
            return Err("stale playtest snapshot: inspect the current zone".into());
        }
        let (terrain, vegetation) = head.effective()?;
        let mut snapshot = Self::new(source_hash, terrain, vegetation)?;
        snapshot.placements = head.corrections.placements.clone().unwrap_or_default();
        snapshot.paths = head.corrections.paths.clone().unwrap_or_default();
        snapshot.rivers = head.corrections.rivers.clone().unwrap_or_default();
        snapshot.buildings = head.corrections.buildings.clone().unwrap_or_default();
        snapshot.promotions = head.corrections.promotions.clone().unwrap_or_default();
        snapshot.start = head.corrections.start.as_deref().copied();
        snapshot.gameplay = head.corrections.gameplay.as_deref().cloned();
        Ok(snapshot)
    }

    pub fn from_terrain(
        terrain: &TerrainDocument,
        expected_hash: ContentHash,
    ) -> Result<Self, String> {
        let source_hash = terrain.content_hash().map_err(|e| e.to_string())?;
        if source_hash != expected_hash {
            return Err("stale playtest snapshot: inspect the current zone".into());
        }
        Self::new(source_hash, terrain.clone(), None)
    }

    fn new(
        source_hash: ContentHash,
        terrain: TerrainDocument,
        vegetation: Option<VegetationDocument>,
    ) -> Result<Self, String> {
        let terrain_hash = terrain.content_hash().map_err(|e| e.to_string())?;
        let vegetation_hash = vegetation
            .as_ref()
            .map(VegetationDocument::content_hash)
            .transpose()
            .map_err(|e| e.to_string())?;
        Ok(Self {
            gameplay: None,
            identity: PlaytestIdentity {
                source_hash,
                terrain_hash,
                vegetation_hash,
            },
            terrain,
            vegetation,
            placements: Vec::new(),
            paths: Vec::new(),
            rivers: Vec::new(),
            buildings: Vec::new(),
            promotions: Vec::new(),
            start: None,
        })
    }

    #[must_use]
    pub const fn identity(&self) -> PlaytestIdentity {
        self.identity
    }

    /// Immutable authored definitions from the same validated source hash.
    #[must_use]
    pub const fn gameplay(&self) -> Option<&forge_gameplay::GameplayDocument> {
        self.gameplay.as_ref()
    }

    #[must_use]
    pub const fn terrain(&self) -> &TerrainDocument {
        &self.terrain
    }

    #[must_use]
    pub const fn vegetation(&self) -> Option<&VegetationDocument> {
        self.vegetation.as_ref()
    }

    #[must_use]
    pub fn placements(&self) -> &[crate::zone_authoring::placements::ManualPlacement] {
        &self.placements
    }

    /// Persistent exclusions from the same validated authority as the placements.
    #[must_use]
    pub fn promotions(&self) -> &[crate::zone_authoring::promotion::PromotedInstance] {
        &self.promotions
    }

    #[must_use]
    pub fn paths(&self) -> &[crate::zone_authoring::paths::AuthoredPath] {
        &self.paths
    }

    /// Immutable recipes, not a terrain carving or renderer readiness acknowledgement.
    #[must_use]
    pub fn rivers(&self) -> &[crate::zone_authoring::rivers::AuthoredRiver] {
        &self.rivers
    }

    #[must_use]
    pub fn buildings(&self) -> &[crate::zone_authoring::buildings::AuthoredBuilding] {
        &self.buildings
    }

    pub fn with_start(mut self, start: Option<PlaytestStart>) -> Result<Self, String> {
        if let Some(start) = start {
            start.validate(&self.terrain)?;
        }
        self.start = start;
        Ok(self)
    }

    #[must_use]
    pub const fn start(&self) -> Option<PlaytestStart> {
        self.start
    }
}

#[cfg(test)]
mod diagnostics_tests {
    use super::*;

    fn identity(byte: u8) -> PlaytestIdentity {
        PlaytestIdentity {
            source_hash: ContentHash::from_bytes([byte; 32]),
            terrain_hash: ContentHash::from_bytes([byte.wrapping_add(1); 32]),
            vegetation_hash: None,
        }
    }

    fn diagnostics() -> PlaytestExitDiagnostics {
        PlaytestExitDiagnostics {
            identity: identity(1),
            fixed_tick: 42,
            final_position_m: [1.0, 2.0, 3.0],
            grounded: true,
            collisions: 1,
            simulation_ready: true,
            runtime_status: "runtime ready".into(),
            end_reason: "return_to_editor".into(),
        }
    }

    #[test]
    fn exit_diagnostics_are_identity_bound_finite_and_size_bounded() {
        let valid = diagnostics();
        assert!(valid.validate_for(identity(1)).is_ok());
        assert!(valid.validate_for(identity(2)).is_err());

        let mut invalid = diagnostics();
        invalid.final_position_m[0] = f32::NAN;
        assert!(invalid.validate_for(identity(1)).is_err());

        let mut invalid = diagnostics();
        invalid.runtime_status = "x".repeat(16 * 1024 + 1);
        assert!(invalid.validate_for(identity(1)).is_err());

        let mut invalid = diagnostics();
        invalid.end_reason = "x".repeat(257);
        assert!(invalid.validate_for(identity(1)).is_err());
    }
}
