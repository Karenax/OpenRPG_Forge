//! Shared source shadow receiver settings; scene adapters supply cascades.
use bevy::{prelude::*, render::render_resource::ShaderType};
pub mod casters;
pub mod heightfield;
pub mod receiver;
#[derive(Clone, Debug, PartialEq, ShaderType)]
pub struct ObjectShadowSettings {
    pub matrices: [Mat4; 4],
    pub cascades: [Vec4; 4],
    pub blend: [Vec4; 4],
    pub count: u32,
    pub map_size: f32,
    pub bias: f32,
    pub fade: f32,
    pub enabled: f32,
    pub pcss_cascades: u32,
    pub sun_angular_diameter: f32,
    pub frame_index: u32,
    pub camera: Vec4,
    pub forward: Vec4,
}
impl Default for ObjectShadowSettings {
    fn default() -> Self {
        Self {
            matrices: [Mat4::IDENTITY; 4],
            cascades: [Vec4::ZERO; 4],
            blend: [Vec4::ZERO; 4],
            count: 3,
            map_size: 2048.0,
            bias: 0.00002,
            fade: 1.0,
            enabled: 0.0,
            pcss_cascades: 1,
            sun_angular_diameter: 0.00925,
            frame_index: 124,
            camera: Vec4::ZERO,
            forward: Vec4::ZERO,
        }
    }
}
