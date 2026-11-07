//! Source lighting over the existing standard and authored terrain surfaces.
//! Surface data, geometry and prepasses remain owned by their original adapters.
use crate::{
    source_environment::scene::ResidentEnvironment,
    source_shadows::{casters::graph, heightfield},
};
use bevy::{
    mesh::MeshVertexBufferLayoutRef,
    pbr::{MaterialExtension, MaterialExtensionKey, MaterialExtensionPipeline},
    prelude::*,
    render::{
        render_resource::{AsBindGroup, RenderPipelineDescriptor},
        storage::ShaderBuffer,
    },
    shader::ShaderRef,
};

pub mod host;
pub mod live;
pub mod scene;
const MODEL_SHADER: Handle<Shader> =
    bevy::asset::uuid_handle!("63b1073c-4580-4dcc-a811-51830c98ea48");
const TERRAIN_SHADER: Handle<Shader> =
    bevy::asset::uuid_handle!("6a62dc80-58dd-46f3-8454-3f9078fb6fda");
const VEGETATION_SHADER: Handle<Shader> =
    bevy::asset::uuid_handle!("965f9250-746c-4b8c-9320-37f725cb7f94");

#[derive(Asset, TypePath, AsBindGroup, Debug, Clone)]
#[bind_group_data(ReceiverKey)]
pub struct Receiver {
    #[storage(254, read_only)]
    #[dependency]
    pub scene: Handle<ShaderBuffer>,
    #[storage(244, read_only)]
    #[dependency]
    pub shadows: Handle<ShaderBuffer>,
    #[storage(256, read_only)]
    #[dependency]
    pub frame: Handle<ShaderBuffer>,
    #[storage(257, read_only)]
    #[dependency]
    pub local_lights: Handle<ShaderBuffer>,
    #[texture(210, dimension = "cube")]
    #[sampler(211)]
    #[dependency]
    pub environment: Handle<Image>,
    #[texture(212)]
    #[dependency]
    pub hill: Handle<Image>,
    #[texture(215, dimension = "2d_array", sample_type = "depth")]
    #[sampler(216, sampler_type = "comparison")]
    #[dependency]
    pub objects: Handle<Image>,
    #[uniform(217)]
    pub object_settings: crate::source_shadows::ObjectShadowSettings,
    #[uniform(240)]
    pub clouds: [Vec4; 5],
    #[texture(241, filterable = false)]
    #[dependency]
    pub cloud_shadow: Handle<Image>,
    #[uniform(255)]
    pub domain: Vec4,
    pub terrain: bool,
    pub vegetation: bool,
}

#[derive(Clone, Copy, Debug, PartialEq, Eq, Hash)]
pub struct ReceiverKey(bool, bool);
impl From<&Receiver> for ReceiverKey {
    fn from(value: &Receiver) -> Self {
        Self(value.terrain, value.vegetation)
    }
}

pub type ModelMaterial = crate::material_composition::ComposedMaterial<StandardMaterial, Receiver>;
pub type TerrainMaterial =
    crate::material_composition::ComposedMaterial<crate::VisualValleyTerrainMaterial, Receiver>;
pub type VegetationMaterial =
    crate::material_composition::ComposedMaterial<crate::VisualValleyVegetationMaterial, Receiver>;

impl MaterialExtension for Receiver {
    fn fragment_shader() -> ShaderRef {
        MODEL_SHADER.into()
    }
    fn enable_shadows() -> bool {
        // Keep the base caster path for the scene's native point/spot lights.
        // Source sun receivers also use the independently owned cascade graph.
        true
    }
    fn specialize(
        _: &MaterialExtensionPipeline,
        descriptor: &mut RenderPipelineDescriptor,
        _: &MeshVertexBufferLayoutRef,
        key: MaterialExtensionKey<Self>,
    ) -> Result<(), bevy::material::specialize::SpecializedMeshPipelineError> {
        // Preserve the base's real depth/normal/motion prepasses. Only the
        // forward source shader selected by this extension is specialized.
        if key.bind_group_data.0
            && let Some(fragment) = &mut descriptor.fragment
            && fragment.shader == MODEL_SHADER
        {
            fragment.shader = TERRAIN_SHADER;
        }
        if key.bind_group_data.1
            && let Some(fragment) = &mut descriptor.fragment
            && fragment.shader == MODEL_SHADER
        {
            fragment.shader = VEGETATION_SHADER;
        }
        Ok(())
    }
}

impl Receiver {
    pub fn from_scene(
        world: &World,
        frame: Handle<ShaderBuffer>,
        terrain: bool,
    ) -> Result<Self, String> {
        let env = world
            .get_resource::<ResidentEnvironment>()
            .ok_or("Source surfaces require the actual environment")?;
        let hill = world
            .get_resource::<heightfield::Images>()
            .ok_or("Source surfaces require the actual heightfield generation")?;
        let shadow = world
            .get_resource::<graph::Target>()
            .ok_or("Source surfaces require the actual caster generation")?;
        let settings = world
            .get_resource::<heightfield::Settings>()
            .ok_or("Source heightfield settings absent")?;
        if world
            .resource::<Assets<ShaderBuffer>>()
            .get(&frame)
            .and_then(|buffer| buffer.data.as_ref())
            .is_none_or(|bytes| bytes.len() != 176)
        {
            return Err("Source surface scene-frame layout differs".into());
        }
        Ok(Self {
            scene: super::scene_buffer::HANDLE,
            shadows: crate::source_shadows::receiver::DEFAULT_BUFFER,
            frame,
            local_lights: super::local_lights::gpu::BUFFER,
            environment: env.environment.clone(),
            hill: hill.output.clone(),
            objects: shadow.image.clone(),
            object_settings: shadow.settings.clone(),
            clouds: env.cloud_view,
            cloud_shadow: env
                .clouds
                .images
                .get(2)
                .ok_or("Source cloud shadow absent")?
                .clone(),
            domain: Vec4::new(settings.origin, settings.origin, settings.size, 0.0),
            terrain,
            vegetation: false,
        })
    }
}

/// Install the algorithm library before the first material reaches extraction.
pub fn stage_shaders(world: &mut World) -> Result<(), String> {
    super::local_lights::gpu::install_neutral(world)?;
    let functions = super::surface_library::extension_functions()?
        .replace("cloudsParams.", "sourceFrame.clouds.");
    let locals = super::local_lights::receiver::surface_shader_source("SourceSurface", 257)?;
    let common = format!(
        "{}\n{functions}\n{locals}\n{}\n{}",
        include_str!("surfaces/types.wgsl"),
        include_str!("surfaces/native_lights.wgsl"),
        include_str!("surfaces/lighting.wgsl")
    );
    let terrain = include_str!("../shaders/visual_valley_terrain.wgsl");
    let call = "out.color = apply_pbr_lighting(pbr_input);";
    if terrain.matches(call).count() != 1 {
        return Err("Authored terrain source-lighting seam drift".into());
    }
    let terrain = format!(
        "{}\n{common}",
        terrain.replace(call, "out.color = sourceSurfaceLighting(pbr_input);")
    );
    let model = format!("{}\n{common}", include_str!("surfaces/model.wgsl"));
    let vegetation = include_str!("../shaders/visual_valley_vegetation_deform.wgsl");
    if vegetation.matches(call).count() != 1 {
        return Err("Governed vegetation source-lighting seam drift".into());
    }
    let vegetation = format!(
        "{}\n{}",
        vegetation.replace(call, "out.color = sourceSurfaceLighting(pbr_input);"),
        // The wind shader already imports both names. naga_oil keeps duplicate
        // imports as separate candidates, even when their paths are identical.
        common.replace(
            "#import bevy_pbr::{pbr_types, mesh_view_bindings::view}",
            ""
        )
    );
    let mut shaders = world
        .get_resource_mut::<Assets<Shader>>()
        .ok_or("Source surface shader assets absent")?;
    shaders
        .insert(
            MODEL_SHADER.id(),
            Shader::from_wgsl(model, "source-model-surface.wgsl"),
        )
        .map_err(|error| error.to_string())?;
    shaders
        .insert(
            TERRAIN_SHADER.id(),
            Shader::from_wgsl(terrain, "source-authored-terrain-surface.wgsl"),
        )
        .map_err(|error| error.to_string())?;
    shaders
        .insert(
            VEGETATION_SHADER.id(),
            Shader::from_wgsl(vegetation, "source-governed-vegetation-surface.wgsl"),
        )
        .map_err(|error| error.to_string())?;
    Ok(())
}

/// The source BRDF consumes ordinary surface textures, colours, AO, normals and
/// emission. Refuse extra effects that have no admitted source-lighting mapping.
#[allow(clippy::float_cmp)]
pub fn validate_standard(material: &StandardMaterial) -> Result<(), String> {
    let defaults = StandardMaterial::default();
    if !Vec4::from_array(material.base_color.to_linear().to_f32_array()).is_finite()
        || !material.emissive.to_vec3().is_finite()
        || !material.emissive.alpha.is_finite()
        || !material.perceptual_roughness.is_finite()
        || !material.metallic.is_finite()
        || !material.reflectance.is_finite()
    {
        return Err("Source surface parameters must be finite".into());
    }
    if material.diffuse_transmission != defaults.diffuse_transmission
        || material.specular_transmission != defaults.specular_transmission
        || material.clearcoat != defaults.clearcoat
        || material.anisotropy_strength != defaults.anisotropy_strength
        || material.specular_tint != defaults.specular_tint
    {
        return Err("Standard surface requires an explicit source mapping for transmission, clearcoat, anisotropy or specular tint".into());
    }
    Ok(())
}
