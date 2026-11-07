//! Source terrain surface material for the shared Bevy renderer.
use bevy::{
    prelude::*,
    render::render_resource::{AsBindGroup, ShaderType},
    shader::ShaderRef,
};

/// Identify the acquired reference raster where exact legacy arithmetic and
/// bytes must be preserved. Other admitted domains use the same engine owners.
pub(crate) fn supports_fixed_source_raster(
    source: &forge_project::sampled_terrain::SampledTerrainSource,
) -> bool {
    source.width == 2048
        && source.height == 2048
        && source.origin_xz_m.map(f64::to_bits) == [(-1024.0_f64).to_bits(); 2]
        && source.sample_spacing_m.to_bits() == 1.0_f64.to_bits()
        && source.outside_height_m.to_bits() == (-90.0_f32).to_bits()
}

pub mod sampling;

#[derive(Clone, Debug, PartialEq, ShaderType)]
pub struct TerrainSettings {
    pub wind: Vec4,
    pub sun: Vec4,
    pub domain: Vec4,
    pub flags: Vec4,
}

#[derive(Asset, TypePath, AsBindGroup, Debug, Clone, PartialEq)]
pub struct TerrainMaterial {
    #[storage(57, read_only)]
    #[dependency]
    pub local_lights: Handle<bevy::render::storage::ShaderBuffer>,
    #[storage(60, read_only)]
    #[dependency]
    pub paint_weights: Handle<bevy::render::storage::ShaderBuffer>,
    #[uniform(61)]
    pub paint_parameters: paint::Parameters,
    #[storage(54, read_only)]
    #[dependency]
    pub scene_lighting: Handle<bevy::render::storage::ShaderBuffer>,
    #[texture(0)]
    #[sampler(1)]
    #[dependency]
    pub normal: Handle<Image>,
    #[texture(2)]
    #[dependency]
    pub splat: Handle<Image>,
    #[texture(3)]
    #[sampler(4)]
    #[dependency]
    pub detail: Handle<Image>,
    #[sampler(5)]
    #[texture(7)]
    #[dependency]
    pub linear_detail: Handle<Image>,
    #[uniform(6)]
    pub settings: TerrainSettings,
    #[texture(10, dimension = "cube")]
    #[sampler(11)]
    #[dependency]
    pub environment: Handle<Image>,
    #[texture(12)]
    #[dependency]
    pub hill_shadow: Handle<Image>,
    #[texture(15, dimension = "2d_array", sample_type = "depth")]
    #[sampler(16, sampler_type = "comparison")]
    #[dependency]
    pub object_shadow: Handle<Image>,
    #[uniform(40)]
    pub clouds: [Vec4; 5],
    #[texture(41, filterable = false)]
    #[dependency]
    pub cloud_shadow: Option<Handle<Image>>,
    #[uniform(17)]
    pub object_settings: crate::source_shadows::ObjectShadowSettings,
    #[storage(44, read_only)]
    #[dependency]
    pub live_shadows: Handle<bevy::render::storage::ShaderBuffer>,
    #[texture(18)]
    #[dependency]
    pub shore_state: Handle<Image>,
    #[texture(19)]
    #[dependency]
    pub shore_lace: Handle<Image>,
    #[uniform(20)]
    pub shore_params: [UVec4; 3],
    #[texture(45)]
    #[sampler(47)]
    #[dependency]
    pub caustic_fine: Handle<Image>,
    #[texture(46)]
    #[dependency]
    pub caustic_broad: Handle<Image>,
    #[uniform(48)]
    pub caustic_params: crate::source_surface::underwater::CausticSampleParams,
    #[texture(49, dimension = "2d_array")]
    #[dependency]
    pub underwater_waves: Handle<Image>,
    #[texture(50, dimension = "2d_array")]
    #[dependency]
    pub underwater_level: Handle<Image>,
    #[storage(53, read_only)]
    #[dependency]
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

pub mod host;
pub mod lod;
pub mod normals;
pub mod paint;
mod programs;
pub mod resident_tiles;
pub mod scene;
pub mod textures;
pub const TERRAIN_SHADER: Handle<Shader> =
    bevy::asset::uuid_handle!("918ad5a6-34b9-49b1-8345-f09bf9f707e5");

/// Obtain the terrain vertex program for another engine-owned consumer such as
/// the terrain shadow pass. The package's format determines legacy compatibility.
pub fn lod_program(
    package: &forge_assets::source_terrain_package::TerrainPackage,
) -> Result<&str, String> {
    programs::for_package(package).map(|(_, lod)| lod)
}

/// Install the caller's verified shader source, with no filesystem fallback.
pub fn install_shader(app: &mut App, source: &str) -> Result<(), String> {
    if source.trim().is_empty() || source.len() > 1_000_000 {
        return Err("Invalid terrain surface shader".into());
    }
    let lighting = crate::source_lighting::scene_uniform::prepare(source)?;
    crate::source_lighting::scene_buffer::install(app.world_mut(), &lighting.seed)?;
    paint::install_neutral(app.world_mut())?;
    let source = crate::source_shadows::receiver::shader_source(&lighting.shader)?;
    let source = crate::source_surface::underwater::receivers::terrain_shader_source(&source)?;
    let source = crate::source_lighting::local_lights::receiver::terrain_shader_source(&source)?;
    crate::source_lighting::local_lights::gpu::install_neutral(app.world_mut())?;
    crate::source_shadows::receiver::install(app.world_mut())?;
    app.world_mut()
        .get_resource_mut::<Assets<Shader>>()
        .ok_or("Renderer shader assets unavailable")?
        .insert(
            TERRAIN_SHADER.id(),
            Shader::from_wgsl(source, "source-package-terrain.wgsl"),
        )
        .map_err(|e| e.to_string())?;
    Ok(())
}
