//! Shared source-compatible direct light and diffuse environment.
use bevy::{prelude::*, render::render_resource::ShaderType};
pub mod frame;
pub mod local_lights;
pub mod scene_buffer;
pub mod scene_uniform;
pub mod surface_library;
pub mod surfaces;
/// Scene-owned direct light and diffuse environment, independently updateable
/// from building textures. Vec4 storage preserves WGSL uniform-array alignment.
#[derive(Clone, Debug, PartialEq, ShaderType)]
pub struct SceneLighting {
    pub sun_direction: Vec4,
    pub sun_color: Vec4,
    pub environment_sh: [Vec4; 9],
}

impl SceneLighting {
    pub fn new(direction: [f32; 3], color: [f32; 3], sh: [[f32; 3]; 9]) -> Result<Self, String> {
        let direction = Vec3::from_array(direction);
        let color = Vec3::from_array(color);
        if !direction.is_finite()
            || (direction.length_squared() - 1.0).abs() > 0.001
            || !color.is_finite()
            || color.min_element() < 0.0
            || sh.iter().flatten().any(|v| !v.is_finite())
        {
            return Err("Invalid building scene lighting".into());
        }
        Ok(Self {
            sun_direction: direction.extend(0.0),
            sun_color: color.extend(0.0),
            environment_sh: sh.map(|v| Vec3::from_array(v).extend(0.0)),
        })
    }
}
