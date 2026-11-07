//! Source geometry and surfaces under shared world lighting, shadows and motion vectors.
use super::{LOBE, MAT, Params, PlantKey, Record, VEG};
use bevy::{
    mesh::MeshVertexBufferLayoutRef,
    pbr::{MaterialPipeline, MaterialPipelineKey},
    prelude::*,
    render::{
        render_resource::{AsBindGroup, RenderPipelineDescriptor},
        storage::ShaderBuffer,
    },
    shader::ShaderRef,
};
pub const SHADER: Handle<Shader> =
    bevy::asset::uuid_handle!("4df920ce-a0d9-45fb-8af3-c96e9f812ba5");
/// Required depth/comparison binding of the shared material layout. The Bevy
/// specialization does not sample it; it is never a source-shadow receipt.
pub const PBR_DEPTH_BINDING: Handle<Image> =
    bevy::asset::uuid_handle!("790ca68d-0537-4eb9-91d9-a276182a28bc");
pub trait SourceMaterial: Material + Clone {
    const MAX_KIND: u8 = 3;
    fn validate_records(_kind: u8, _records: &[Record]) -> Result<(), String> {
        Ok(())
    }
    fn params(&self) -> &Params;
    fn params_mut(&mut self) -> &mut Params;
    fn kind(&self) -> u8;
    fn set_source(
        &mut self,
        kind: u8,
        instances: Handle<ShaderBuffer>,
        models: Handle<ShaderBuffer>,
    );
}

#[derive(Asset, TypePath, AsBindGroup, Debug, Clone)]
#[bind_group_data(PlantKey)]
pub struct WorldPlantMaterial {
    #[uniform(0)]
    pub params: Params,
    #[texture(1)]
    #[sampler(2)]
    #[dependency]
    pub detail: Handle<Image>,
    #[texture(3)]
    #[sampler(4)]
    #[dependency]
    pub leaf: Handle<Image>,
    #[texture(5)]
    #[sampler(6)]
    #[dependency]
    pub impostor_a: Handle<Image>,
    #[texture(7)]
    #[dependency]
    pub impostor_b: Handle<Image>,
    #[storage(8, read_only, visibility(vertex))]
    #[dependency]
    pub instances: Handle<ShaderBuffer>,
    #[storage(9, read_only, visibility(vertex))]
    #[dependency]
    pub models: Handle<ShaderBuffer>,
    #[storage(18, read_only)]
    #[dependency]
    pub frame: Handle<ShaderBuffer>,
    #[storage(54, read_only)]
    #[dependency]
    pub scene_lighting: Handle<ShaderBuffer>,
    #[storage(44, read_only, visibility(fragment))]
    #[dependency]
    pub live_shadows: Handle<ShaderBuffer>,
    #[storage(56, read_only, visibility(fragment))]
    #[dependency]
    pub local_lights: Handle<ShaderBuffer>,
    #[texture(10, dimension = "cube")]
    #[sampler(11)]
    #[dependency]
    pub environment: Option<Handle<Image>>,
    #[texture(12)]
    #[dependency]
    pub hill_shadow: Option<Handle<Image>>,
    #[texture(15, dimension = "2d_array", sample_type = "depth")]
    #[sampler(16, sampler_type = "comparison")]
    #[dependency]
    pub object_shadow: Option<Handle<Image>>,
    #[uniform(17)]
    pub object_settings: crate::source_shadows::ObjectShadowSettings,
    #[uniform(40)]
    pub clouds: [Vec4; 5],
    #[texture(41, filterable = false)]
    #[dependency]
    pub cloud_shadow: Option<Handle<Image>>,
    pub source_lighting: bool,
    #[uniform(55)]
    pub hill_domain: Vec4,
    #[texture(13, sample_type = "float", filterable = false)]
    #[dependency]
    pub grass_heights: Option<Handle<Image>>,
    #[texture(14)]
    #[dependency]
    pub grass_mask: Option<Handle<Image>>,
    #[uniform(19)]
    pub grass_field: Vec4,
    #[texture(20, sample_type = "float", filterable = false)]
    #[dependency]
    pub grass_deltas: Option<Handle<Image>>,
    pub kind: u8,
}

impl From<&WorldPlantMaterial> for PlantKey {
    fn from(value: &WorldPlantMaterial) -> Self {
        Self(value.kind | if value.source_lighting { 0x80 } else { 0 })
    }
}
impl SourceMaterial for WorldPlantMaterial {
    const MAX_KIND: u8 = 2;
    fn validate_records(kind: u8, records: &[Record]) -> Result<(), String> {
        // Source canopy data.y is its signed vertical stretch, not a palm lean.
        // The impostor ray divides by it. Do not accept a near-only success for
        // a source population whose far representation is undefined.
        if kind > 0
            && records.iter().any(|record| {
                !record.i_dat.y.is_finite()
                    || record.i_dat.y == 0.0
                    || !record.i_dat.y.recip().is_finite()
            })
        {
            return Err("Canopy requires a finite nonzero source vertical stretch".into());
        }
        Ok(())
    }
    fn params(&self) -> &Params {
        &self.params
    }
    fn params_mut(&mut self) -> &mut Params {
        &mut self.params
    }
    fn kind(&self) -> u8 {
        self.kind
    }
    fn set_source(
        &mut self,
        kind: u8,
        instances: Handle<ShaderBuffer>,
        models: Handle<ShaderBuffer>,
    ) {
        self.kind = kind;
        self.instances = instances;
        self.models = models;
    }
}

impl Material for WorldPlantMaterial {
    fn alpha_mode(&self) -> AlphaMode {
        AlphaMode::Mask(0.5)
    }
    fn vertex_shader() -> ShaderRef {
        SHADER.into()
    }
    fn fragment_shader() -> ShaderRef {
        SHADER.into()
    }
    fn prepass_vertex_shader() -> ShaderRef {
        SHADER.into()
    }
    fn prepass_fragment_shader() -> ShaderRef {
        SHADER.into()
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
        let kind = format!("PLANT_KIND_{}", key.bind_group_data.0 & 0x7f);
        descriptor.vertex.shader_defs.push(kind.clone().into());
        if key.bind_group_data.0 & 0x80 != 0 {
            descriptor
                .vertex
                .shader_defs
                .push("FORGE_SOURCE_LIGHTING".into());
            if let Some(fragment) = &mut descriptor.fragment {
                fragment.shader_defs.push("FORGE_SOURCE_LIGHTING".into());
            }
        }
        if let Some(fragment) = &mut descriptor.fragment {
            for define in [
                kind.as_str(),
                "STANDARD_MATERIAL_DIFFUSE_TRANSMISSION",
                "STANDARD_MATERIAL_SPECULAR_OR_DIFFUSE_TRANSMISSION",
            ] {
                fragment.shader_defs.push(define.into());
            }
        }
        Ok(())
    }
}

mod lighting;
pub mod shader;
/// Derive a qualified source profile before replacing the shared world shader.
pub fn stage_shader(world: &mut World, source: &str) -> Result<(), String> {
    let source = shader::derive(source)?;
    crate::source_lighting::local_lights::gpu::install_neutral(world)?;
    lighting::install_pbr_binding(world)?;
    world
        .get_resource_mut::<Assets<Shader>>()
        .ok_or("Shader assets unavailable")?
        .insert(SHADER.id(), Shader::from_wgsl(source, "world-plants.wgsl"))
        .map_err(|error| error.to_string())?;
    Ok(())
}
