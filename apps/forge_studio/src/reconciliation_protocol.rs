//! Bounded read-only transport; population caches remain native and derived.
use forge_core::{ContentHash, EntityId};
pub use forge_world::promotion_preflight::ReconciliationRecipe;
use serde::{Deserialize, Serialize};
use std::collections::BTreeMap;

#[derive(Clone, Debug, PartialEq, Serialize, Deserialize)]
#[serde(deny_unknown_fields)]
pub struct Query {
    pub source_hash: ContentHash,
    pub recipe: ReconciliationRecipe,
    pub offset: usize,
    pub placement_id: EntityId,
    pub selections: Option<BTreeMap<EntityId, EntityId>>,
}

#[derive(Clone, Debug, PartialEq, Serialize, Deserialize)]
#[serde(deny_unknown_fields)]
pub struct TreeChoice {
    pub id: EntityId,
    pub layer: forge_project::VegetationLayerId,
    pub position: [f32; 3],
    pub distance_m: Option<f64>,
}

#[derive(Clone, Debug, PartialEq, Serialize, Deserialize)]
#[serde(deny_unknown_fields)]
pub struct Page {
    pub source_hash: ContentHash,
    pub recipe: ReconciliationRecipe,
    pub offset: usize,
    pub placement_id: EntityId,
    pub total: usize,
    pub trees: Vec<TreeChoice>,
    pub request: Option<forge_project::zone_authoring::session::ZoneRequest>,
}
