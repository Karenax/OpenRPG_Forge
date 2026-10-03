//! Derived navigation for the existing gameplay/KCC schedule.
mod cache;
mod curved_obstacles;
mod geometry;
mod runtime;
#[cfg(target_arch = "wasm32")]
mod worker;
use bevy::prelude::*;
pub use cache::{
    ActorNavigationObservation, NavigationRuntime, NavigationStatus, NavigationTileObservation,
};
pub use forge_navigation::{AgentProfile, AreaVolume, PROVIDER_SOURCE_SHA256, TileKey};
pub(crate) use runtime::{guard, refresh};
use std::collections::BTreeMap;

#[derive(Clone, Debug, Hash, PartialEq, Eq, SystemSet)]
pub enum NavigationSet {
    Guard,
    Refresh,
}

/// Derived traversal areas/costs from a canonical document. Mutation requires
/// a new source hash/revision and invalidates affected cached geometry.
#[derive(Resource, Clone, Debug, Default)]
pub struct NavigationPolicy {
    source_hash: String,
    revision: u64,
    areas: Vec<AreaVolume>,
    costs: BTreeMap<u8, f32>,
}
impl NavigationPolicy {
    #[must_use]
    pub fn source_hash(&self) -> &str {
        &self.source_hash
    }
    #[must_use]
    pub const fn revision(&self) -> u64 {
        self.revision
    }
    #[must_use]
    pub fn areas(&self) -> &[AreaVolume] {
        &self.areas
    }
    pub fn replace(
        &mut self,
        source_hash: String,
        areas: Vec<AreaVolume>,
        costs: BTreeMap<u8, f32>,
    ) -> Result<(), String> {
        if source_hash.len() != 64
            || !source_hash.bytes().all(|c| c.is_ascii_hexdigit())
            || areas.len() > 256
            || costs.values().any(|v| !v.is_finite() || *v <= 0.0)
        {
            return Err("invalid canonical navigation policy".into());
        }
        for area in &areas {
            area.validate()?;
        }
        self.revision = self
            .revision
            .checked_add(1)
            .ok_or("navigation policy revision exhausted")?;
        self.source_hash = source_hash;
        self.areas = areas;
        self.costs = costs;
        Ok(())
    }
}
