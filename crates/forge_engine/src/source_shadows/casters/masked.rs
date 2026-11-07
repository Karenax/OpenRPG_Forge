//! Shared masked position/data depth streams; source codecs supply admitted masks.
pub mod extraction;
pub mod gpu;
pub mod merge;
/// Ephemeral depth input for the current shared cascade target, never persisted.
#[derive(bevy::prelude::Resource, Clone, bevy::render::extract_resource::ExtractResource)]
pub struct Frame {
    pub target: bevy::prelude::Handle<bevy::prelude::Image>,
    pub shader: std::sync::Arc<String>,
    pub meshes: std::sync::Arc<Vec<MaskedMesh>>,
    pub poses: Vec<gpu::Pose>,
    pub time: f32,
}

/// Interleaved position/data stream for the masked depth entries.
#[derive(Clone)]
pub struct MaskedMesh {
    pub vertices: Vec<u8>,
    pub indices: Vec<u8>,
}
