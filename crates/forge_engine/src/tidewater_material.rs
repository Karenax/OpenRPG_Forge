//! Tidewater village material shared by native and Studio render adapters.
//! Callers install the verified shader bytes and provide the same environment maps.
use crate::tidewater_mesh::{DATA, TINT};
use bevy::{
    mesh::MeshVertexBufferLayoutRef,
    pbr::{MaterialPipeline, MaterialPipelineKey},
    prelude::*,
    render::render_resource::{AsBindGroup, Face, RenderPipelineDescriptor, ShaderType},
    shader::ShaderRef,
};
pub const VILLAGE_SHADER: Handle<Shader> =
    bevy::asset::uuid_handle!("a903e588-5665-48a5-aac3-d0deecff072a");

/// Scene-owned direct light and diffuse environment, independently updateable
/// from building textures. Vec4 storage preserves WGSL uniform-array alignment.
#[derive(Clone, Debug, ShaderType)]
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

/// Install verified source without an `AssetServer` fallback to a local fixture.
pub fn install_shader(app: &mut App, source: &str) -> Result<(), Box<dyn std::error::Error>> {
    stage_shader(app.world_mut(), source).map_err(Into::into)
}

pub fn stage_shader(world: &mut World, source: &str) -> Result<(), String> {
    if source.trim().is_empty() || source.len() > 1_000_000 {
        return Err("Invalid building shader size".into());
    }
    world
        .get_resource_mut::<Assets<Shader>>()
        .ok_or("Building shader assets unavailable")?
        .insert(
            VILLAGE_SHADER.id(),
            Shader::from_wgsl(source.to_owned(), "tidewater-package-village.wgsl"),
        )
        .map_err(|e| e.to_string())?;
    Ok(())
}
#[derive(Clone, Debug, ShaderType)]
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
#[derive(Clone, Debug, ShaderType)]
pub struct Settings {
    pub wind: Vec4,
    pub color: Vec4,
    pub emissive: Vec4,
    pub surface: Vec4,
    pub flags: Vec4,
}
#[derive(Clone, Copy, Debug, PartialEq, Eq, Hash)]
pub struct Key {
    pub kind: u8,
}
#[derive(Asset, TypePath, AsBindGroup, Debug, Clone)]
#[bind_group_data(Key)]
pub struct VillageMaterial {
    #[uniform(18)]
    pub scene_lighting: SceneLighting,
    #[uniform(0)]
    pub settings: Settings,
    #[texture(1)]
    #[sampler(5)]
    pub first: Handle<Image>,
    #[texture(2)]
    pub second: Handle<Image>,
    #[texture(3)]
    pub third: Handle<Image>,
    #[texture(4)]
    pub fourth: Handle<Image>,
    #[texture(10, dimension = "cube")]
    #[sampler(11)]
    pub environment: Handle<Image>,
    #[texture(12)]
    pub hill_shadow: Handle<Image>,
    #[texture(15, dimension = "2d_array", sample_type = "depth")]
    #[sampler(16, sampler_type = "comparison")]
    pub object_shadow: Handle<Image>,
    #[uniform(40)]
    pub clouds: [Vec4; 5],
    #[texture(41, filterable = false)]
    pub cloud_shadow: Option<Handle<Image>>,
    #[uniform(17)]
    pub object_settings: ObjectShadowSettings,
    pub kind: u8,
}
impl From<&VillageMaterial> for Key {
    fn from(value: &VillageMaterial) -> Self {
        Self { kind: value.kind }
    }
}
impl Material for VillageMaterial {
    fn vertex_shader() -> ShaderRef {
        VILLAGE_SHADER.clone().into()
    }
    fn fragment_shader() -> ShaderRef {
        VILLAGE_SHADER.clone().into()
    }
    fn enable_prepass() -> bool {
        false
    }
    fn enable_shadows() -> bool {
        false
    }
    fn alpha_mode(&self) -> AlphaMode {
        if self.kind == 6 {
            AlphaMode::Blend
        } else {
            AlphaMode::Opaque
        }
    }
    fn specialize(
        _: &MaterialPipeline,
        descriptor: &mut RenderPipelineDescriptor,
        layout: &MeshVertexBufferLayoutRef,
        key: MaterialPipelineKey<Self>,
    ) -> Result<(), bevy::material::specialize::SpecializedMeshPipelineError> {
        descriptor.vertex.buffers = vec![layout.0.get_layout(&[
            Mesh::ATTRIBUTE_POSITION.at_shader_location(0),
            Mesh::ATTRIBUTE_NORMAL.at_shader_location(1),
            Mesh::ATTRIBUTE_UV_0.at_shader_location(2),
            TINT.at_shader_location(3),
            DATA.at_shader_location(4),
        ])?];
        let define = format!("VILLAGE_KIND_{}", key.bind_group_data.kind);
        descriptor.vertex.shader_defs.push(define.clone().into());
        if let Some(fragment) = &mut descriptor.fragment {
            fragment.shader_defs.push(define.into());
            if key.bind_group_data.kind >= 5 {
                fragment.shader_defs.push("VILLAGE_DOUBLE_SIDED".into());
            }
        }
        descriptor.primitive.cull_mode = if key.bind_group_data.kind >= 5 {
            None
        } else {
            Some(Face::Back)
        };
        Ok(())
    }
}
