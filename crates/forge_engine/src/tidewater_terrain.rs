//! Tidewater terrain surface material for the shared Bevy renderer.
use bevy::{
    prelude::*,
    render::render_resource::{AsBindGroup, ShaderType},
    shader::ShaderRef,
};

#[derive(Clone, Debug, ShaderType)]
pub struct TerrainSettings {
    pub wind: Vec4,
    pub sun: Vec4,
    pub domain: Vec4,
    pub flags: Vec4,
}

#[derive(Asset, TypePath, AsBindGroup, Debug, Clone)]
pub struct TerrainMaterial {
    #[texture(0)]
    #[sampler(1)]
    pub normal: Handle<Image>,
    #[texture(2)]
    pub splat: Handle<Image>,
    #[texture(3)]
    #[sampler(4)]
    pub detail: Handle<Image>,
    #[sampler(5)]
    #[texture(7)]
    pub linear_detail: Handle<Image>,
    #[uniform(6)]
    pub settings: TerrainSettings,
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
    pub object_settings: crate::tidewater_material::ObjectShadowSettings,
    #[storage(44, read_only)]
    pub live_shadows: Handle<bevy::render::storage::ShaderBuffer>,
    #[texture(18)]
    pub shore_state: Handle<Image>,
    #[texture(19)]
    pub shore_lace: Handle<Image>,
    #[uniform(20)]
    pub shore_params: [UVec4; 3],
    #[texture(45)]
    #[sampler(47)]
    pub caustic_fine: Handle<Image>,
    #[texture(46)]
    pub caustic_broad: Handle<Image>,
    #[uniform(48)]
    pub caustic_params: crate::tidewater_caustics::CausticSampleParams,
    #[texture(49, dimension = "2d_array")]
    pub underwater_waves: Handle<Image>,
    #[texture(50, dimension = "2d_array")]
    pub underwater_level: Handle<Image>,
    #[storage(53, read_only)]
    pub underwater_map_frame: Handle<bevy::render::storage::ShaderBuffer>,
}

impl Material for TerrainMaterial {
    fn fragment_shader() -> ShaderRef {
        TERRAIN_SHADER.into()
    }
    fn enable_prepass() -> bool {
        false
    }
    fn enable_shadows() -> bool {
        false
    }
}

pub mod lod;
pub mod normals;
pub mod scene;
pub mod textures;
pub const TERRAIN_SHADER: Handle<Shader> =
    bevy::asset::uuid_handle!("918ad5a6-34b9-49b1-8345-f09bf9f707e5");

/// Install the caller's verified shader source, with no filesystem fallback.
pub fn install_shader(app: &mut App, source: &str) -> Result<(), String> {
    if source.trim().is_empty() || source.len() > 1_000_000 {
        return Err("Invalid terrain surface shader".into());
    }
    let source = crate::tidewater_object_shadow::receiver::shader_source(source)?;
    let source = crate::tidewater_caustics::terrain_shader_source(&source)?;
    crate::tidewater_object_shadow::receiver::install(app.world_mut())?;
    app.world_mut()
        .get_resource_mut::<Assets<Shader>>()
        .ok_or("Renderer shader assets unavailable")?
        .insert(
            TERRAIN_SHADER.id(),
            Shader::from_wgsl(source, "tidewater-package-terrain.wgsl"),
        )
        .map_err(|e| e.to_string())?;
    Ok(())
}
