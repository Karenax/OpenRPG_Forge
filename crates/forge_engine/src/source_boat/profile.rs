//! Caller-supplied boat geometry and placement data, independent of its package.
use forge_assets::source_geometry::{AssetLocalFrame, ResolvedWaterExclusion};
use std::collections::BTreeMap;

/// The caller admits the asset, exact revision, provenance and any open hull
/// proxy before preparing this in-memory description for the shared renderer.
pub struct Profile {
    pub source_id: String,
    pub canonical_translation_m: [f64; 3],
    pub canonical_heading_radians: f64,
    pub nodes: Vec<Node>,
    pub parts: Vec<Part>,
    pub colliders: Vec<CollisionVolume>,
    pub water_exclusion: ResolvedWaterExclusion,
    pub triangles: usize,
}

pub struct Node {
    pub id: String,
    pub parent: Option<String>,
    pub transform: AssetLocalFrame,
    pub visible: bool,
}

pub struct Part {
    pub id: String,
    pub node: String,
    pub material: String,
    pub attributes: BTreeMap<String, Vec<f32>>,
    pub indices: Vec<u32>,
    pub cast_shadow: bool,
    pub receive_shadow: bool,
    pub render_order: i32,
}

pub struct CollisionVolume {
    pub id: String,
    pub tag: String,
    pub center_m: [f64; 3],
    pub half_extents_m: [f64; 3],
    pub walkable: bool,
    pub solid: bool,
}
