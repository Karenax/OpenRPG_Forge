//! Shared source-compatible plant material, attributes and instance records.
use bevy::{
    mesh::{MeshVertexAttribute, MeshVertexBufferLayoutRef},
    pbr::{MaterialPipeline, MaterialPipelineKey},
    prelude::*,
    render::{
        render_resource::{AsBindGroup, RenderPipelineDescriptor, ShaderType, VertexFormat},
        storage::ShaderBuffer,
    },
    shader::ShaderRef,
};
pub mod canonical;
pub mod picking;
pub mod scene;
pub mod textures;
pub const PLANT_SHADER: Handle<Shader> =
    bevy::asset::uuid_handle!("04a33620-a592-4753-acf1-ec918dd6fa19");

/// Stage caller-verified WGSL without a filesystem or network fallback.
pub fn stage_shader(world: &mut World, source: &str) -> Result<(), String> {
    if source.trim().is_empty() || source.len() > 1_000_000 {
        return Err("Invalid plant shader source".into());
    }
    world
        .get_resource_mut::<Assets<Shader>>()
        .ok_or("Shader assets unavailable")?
        .insert(
            PLANT_SHADER.id(),
            Shader::from_wgsl(source.to_owned(), "tidewater-plants.wgsl"),
        )
        .map_err(|e| e.to_string())?;
    Ok(())
}
#[derive(Clone, Debug, ShaderType)]
pub struct Record {
    pub i_pos: Vec4,
    pub i_dat: Vec4,
}
pub const VEG: MeshVertexAttribute =
    MeshVertexAttribute::new("TideVeg", 0x7457_0001, VertexFormat::Float32x4);
pub const MAT: MeshVertexAttribute =
    MeshVertexAttribute::new("TideMat", 0x7457_0002, VertexFormat::Float32x4);
pub const LOBE: MeshVertexAttribute =
    MeshVertexAttribute::new("TideLobe", 0x7457_0003, VertexFormat::Float32x4);

#[derive(Clone, Debug, ShaderType)]
pub struct Params {
    pub i_pos: Vec4,
    pub i_dat: Vec4,
    pub wind: Vec4,
    pub gust_lod: Vec4,
    pub sun_night: Vec4,
    pub lod_end_frame: Vec4,
}
#[derive(Asset, TypePath, AsBindGroup, Debug, Clone)]
#[bind_group_data(PlantKey)]
pub struct PlantMaterial {
    #[uniform(0)]
    pub params: Params,
    #[texture(1)]
    #[sampler(2)]
    pub detail: Handle<Image>,
    #[texture(3)]
    #[sampler(4)]
    pub leaf: Handle<Image>,
    #[texture(5)]
    #[sampler(6)]
    pub impostor_a: Handle<Image>,
    #[texture(7)]
    pub impostor_b: Handle<Image>,
    #[storage(8, read_only)]
    pub instances: Handle<ShaderBuffer>,
    #[storage(9, read_only)]
    pub models: Handle<ShaderBuffer>,
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
    #[texture(13, sample_type = "float", filterable = false)]
    pub grass_heights: Handle<Image>,
    #[texture(14)]
    pub grass_mask: Handle<Image>,
    pub kind: u8,
}
#[derive(Clone, Copy, Debug, PartialEq, Eq, Hash)]
pub struct PlantKey(u8);
impl From<&PlantMaterial> for PlantKey {
    fn from(value: &PlantMaterial) -> Self {
        Self(value.kind)
    }
}
impl Material for PlantMaterial {
    fn enable_prepass() -> bool {
        false
    }
    fn enable_shadows() -> bool {
        false
    }
    fn vertex_shader() -> ShaderRef {
        PLANT_SHADER.into()
    }
    fn fragment_shader() -> ShaderRef {
        PLANT_SHADER.into()
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
            VEG.at_shader_location(3),
            MAT.at_shader_location(4),
            LOBE.at_shader_location(5),
        ])?];
        descriptor.primitive.cull_mode = None;
        let define = format!("PLANT_KIND_{}", key.bind_group_data.0);
        descriptor.vertex.shader_defs.push(define.clone().into());
        if let Some(fragment) = &mut descriptor.fragment {
            fragment.shader_defs.push(define.into());
        }
        Ok(())
    }
}
