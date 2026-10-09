//! Resident bind group for the complete source stall material.
//! Scene adapters must stage the complete shader and publish live receiver data.
use super::{FishStandMaterial, FishStandResident, LAYER, UV2, contact_frame};
use crate::tidewater_assets::materials::LightingBindings;
use crate::tidewater_material::{ObjectShadowSettings, SceneLighting, VillageMaterial};
use crate::tidewater_ocean_depth::DepthCopy;
use crate::tidewater_temporal::frame::ViewPacket;
use bevy::{
    mesh::MeshVertexBufferLayoutRef,
    pbr::{MaterialPipeline, MaterialPipelineKey},
    prelude::*,
    render::{
        render_resource::{AsBindGroup, Face, RenderPipelineDescriptor},
        storage::ShaderBuffer,
    },
    shader::ShaderRef,
};

pub const SHADER: Handle<Shader> =
    bevy::asset::uuid_handle!("f27b274e-0912-4fc5-aa0d-0314c7d56efe");

/// Stage the complete shader from the same verified lighting input as the village.
/// Invalid composition leaves the shader store unchanged.
pub fn stage_shader(world: &mut World, lighting_source: &str) -> Result<(), String> {
    let source = super::full_scene::shader_source(lighting_source)?;
    if !world.contains_resource::<Assets<Shader>>() {
        return Err("Complete stall requires initialized shader assets".into());
    }
    crate::tidewater_material::scene_buffer::receiver::install(world)?;
    super::contact_gpu::install_buffer(world)?;
    world
        .resource_mut::<Assets<Shader>>()
        .insert(
            SHADER.id(),
            Shader::from_wgsl(source, "tidewater-complete-stall.wgsl"),
        )
        .map_err(|error| error.to_string())?;
    Ok(())
}

#[derive(Asset, TypePath, AsBindGroup, Debug, Clone)]
#[bind_group_data(FullLightingKey)]
pub struct FullMaterial {
    #[texture(0, dimension = "2d_array")]
    #[sampler(1)]
    pub surf_a: Handle<Image>,
    #[texture(2, dimension = "2d_array")]
    pub surf_n: Handle<Image>,
    #[texture(3, dimension = "2d_array")]
    pub surf_r: Handle<Image>,
    #[texture(4, dimension = "2d_array")]
    pub prop_a: Handle<Image>,
    #[texture(5, dimension = "2d_array")]
    pub prop_n: Handle<Image>,
    #[texture(6, dimension = "2d_array")]
    pub prop_r: Handle<Image>,
    #[texture(7, dimension = "2d_array")]
    #[sampler(8)]
    pub signs: Handle<Image>,
    /// Time, night, emissive intensity, environment intensity.
    #[uniform(9)]
    pub surface_frame: Vec4,
    #[texture(10, dimension = "cube")]
    #[sampler(11)]
    pub environment: Handle<Image>,
    #[texture(12)]
    pub hill_shadow: Handle<Image>,
    #[texture(15, dimension = "2d_array", sample_type = "depth")]
    #[sampler(16, sampler_type = "comparison")]
    pub object_shadow: Handle<Image>,
    #[uniform(17)]
    pub object_settings: ObjectShadowSettings,
    #[uniform(18)]
    pub scene_lighting: SceneLighting,
    #[uniform(40)]
    pub clouds: [Vec4; 5],
    #[texture(41, filterable = false)]
    pub cloud_shadow: Option<Handle<Image>>,
    #[storage(44, read_only)]
    pub live_shadows: Handle<ShaderBuffer>,
    #[uniform(48)]
    pub underwater_params: crate::tidewater_caustics::CausticSampleParams,
    #[texture(49, dimension = "2d_array")]
    #[sampler(47)]
    pub underwater_level: Handle<Image>,
    #[texture(50, filterable = false)]
    pub ground_bounce: Option<Handle<Image>>,
    #[uniform(51)]
    pub bounce_settings: Vec4,
    #[storage(53, read_only)]
    pub underwater_map_frame: Handle<ShaderBuffer>,
    #[storage(54, read_only)]
    pub local_lights: Handle<ShaderBuffer>,
    #[storage(55, read_only)]
    pub scene_buffer: Handle<ShaderBuffer>,
    pub shared_scene_lighting: bool,
    #[texture(56, sample_type = "depth")]
    pub contact_depth: Handle<Image>,
    #[uniform(57)]
    pub contact_frame: contact_frame::Uniform,
    #[storage(58, read_only)]
    pub render_contact_frame: Handle<ShaderBuffer>,
    pub render_owned_contact: bool,
}

#[derive(Clone, Copy, Debug, PartialEq, Eq, Hash)]
pub struct FullLightingKey {
    shared_scene_lighting: bool,
    render_owned_contact: bool,
}

impl From<&FullMaterial> for FullLightingKey {
    fn from(material: &FullMaterial) -> Self {
        Self {
            shared_scene_lighting: material.shared_scene_lighting,
            render_owned_contact: material.render_owned_contact,
        }
    }
}

impl Material for FullMaterial {
    fn vertex_shader() -> ShaderRef {
        SHADER.into()
    }
    fn fragment_shader() -> ShaderRef {
        SHADER.into()
    }
    fn enable_prepass() -> bool {
        false
    }
    fn enable_shadows() -> bool {
        // The source shadow graph requires its own opaque geometry submission.
        false
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
            Mesh::ATTRIBUTE_COLOR.at_shader_location(3),
            LAYER.at_shader_location(4),
            UV2.at_shader_location(5),
        ])?];
        descriptor.primitive.cull_mode = Some(Face::Back);
        if key.bind_group_data.shared_scene_lighting {
            let define = crate::tidewater_material::scene_buffer::receiver::DEFINE;
            descriptor.vertex.shader_defs.push(define.into());
            if let Some(fragment) = &mut descriptor.fragment {
                fragment.shader_defs.push(define.into());
            }
        }
        if key.bind_group_data.render_owned_contact {
            let define = super::full_shader::RENDER_CONTACT_DEFINE;
            descriptor.vertex.shader_defs.push(define.into());
            if let Some(fragment) = &mut descriptor.fragment {
                fragment.shader_defs.push(define.into());
            }
        }
        Ok(())
    }
}

/// Surface animation inputs from the caller's shared scene frame.
pub struct SurfaceFrame {
    pub time: f32,
    pub night: f32,
    pub frame_index: u32,
    pub sea_level: f32,
    pub contact_strength: f32,
}

impl FullMaterial {
    /// Refresh common scene resources while the RenderWorld owns camera history.
    /// Surface animation and contacts are supplied by the resident render buffer.
    pub fn update_render_receivers(&mut self, lighting: &VillageMaterial) -> Result<(), String> {
        if !self.render_owned_contact {
            return Err("Complete stall render receiver update requires render ownership".into());
        }
        SceneLighting::new(
            lighting.scene_lighting.sun_direction.truncate().to_array(),
            lighting.scene_lighting.sun_color.truncate().to_array(),
            lighting
                .scene_lighting
                .environment_sh
                .map(|v| v.truncate().to_array()),
        )?;
        self.apply_common(lighting);
        Ok(())
    }

    /// Publish current common receiver resources without restaging source arrays.
    /// A rejected camera/depth pair leaves every existing binding untouched.
    pub fn update_scene(
        &mut self,
        lighting: &VillageMaterial,
        camera: &ViewPacket,
        depth: &DepthCopy,
        contact_depth: Handle<Image>,
        frame: SurfaceFrame,
    ) -> Result<(), String> {
        if self.render_owned_contact {
            return Err("Complete stall camera history is owned by RenderWorld".into());
        }
        if contact_depth != depth.image {
            return Err(
                "Complete stall contact depth differs from the encoded copy receipt".into(),
            );
        }
        if !frame.time.is_finite()
            || !frame.night.is_finite()
            || !(0.0..=1.0).contains(&frame.night)
        {
            return Err("Invalid complete stall surface frame".into());
        }
        let contact = contact_frame::prepare(
            camera,
            Some(depth),
            &lighting.scene_lighting,
            frame.sea_level,
            frame.contact_strength,
            frame.frame_index,
        )?;
        self.surface_frame = Vec4::new(frame.time, frame.night, 1.0, 1.0);
        self.apply_common(lighting);
        self.contact_depth = contact_depth;
        self.contact_frame = contact;
        Ok(())
    }

    fn apply_common(&mut self, lighting: &VillageMaterial) {
        self.environment = lighting.environment.clone();
        self.hill_shadow = lighting.hill_shadow.clone();
        self.object_shadow = lighting.object_shadow.clone();
        self.object_settings = lighting.object_settings.clone();
        self.scene_lighting = lighting.scene_lighting.clone();
        self.clouds = lighting.clouds;
        self.cloud_shadow = lighting.cloud_shadow.clone();
        self.live_shadows = lighting.live_shadows.clone();
        self.underwater_params = lighting.underwater_params.clone();
        self.underwater_level = lighting.underwater_level.clone();
        self.underwater_map_frame = lighting.underwater_map_frame.clone();
        self.ground_bounce = lighting.ground_bounce.clone();
        self.bounce_settings = lighting.bounce_settings;
        self.local_lights = lighting.local_lights.clone();
        self.scene_buffer = lighting.scene_buffer.clone();
        self.shared_scene_lighting = lighting.shared_scene_lighting;
    }
}

/// Build the actual live material with the village's resident receiver handles.
/// The inactive initial contact metadata is explicit; `contact_gpu::register`
/// must be installed by the scene adapter before this material is displayed.
pub fn prepare_render_owned(
    resident: &FishStandResident,
    lighting: &VillageMaterial,
    contact_depth: Handle<Image>,
    inputs: &super::contact_gpu::Inputs,
) -> Result<FullMaterial, String> {
    let seed = super::contact_gpu::binding_seed(inputs)?;
    let textures = FishStandMaterial::from_resident(resident, lighting.scene_lighting.clone())?;
    Ok(FullMaterial {
        surf_a: textures.surf_a,
        surf_n: textures.surf_n,
        surf_r: textures.surf_r,
        prop_a: textures.prop_a,
        prop_n: textures.prop_n,
        prop_r: textures.prop_r,
        signs: textures.signs,
        surface_frame: seed.surface_parameters,
        environment: lighting.environment.clone(),
        hill_shadow: lighting.hill_shadow.clone(),
        object_shadow: lighting.object_shadow.clone(),
        object_settings: lighting.object_settings.clone(),
        scene_lighting: lighting.scene_lighting.clone(),
        clouds: lighting.clouds,
        cloud_shadow: lighting.cloud_shadow.clone(),
        live_shadows: lighting.live_shadows.clone(),
        underwater_params: lighting.underwater_params.clone(),
        underwater_level: lighting.underwater_level.clone(),
        underwater_map_frame: lighting.underwater_map_frame.clone(),
        ground_bounce: lighting.ground_bounce.clone(),
        bounce_settings: lighting.bounce_settings,
        local_lights: lighting.local_lights.clone(),
        scene_buffer: lighting.scene_buffer.clone(),
        shared_scene_lighting: lighting.shared_scene_lighting,
        contact_depth,
        contact_frame: seed.contact,
        render_contact_frame: super::contact_gpu::BUFFER.clone(),
        render_owned_contact: true,
    })
}

/// Resolve source textures and common lighting with matched opaque depth history.
/// Preparation does not publish a material, shader, caster or receiver updater.
///
/// # Errors
/// Missing textures, invalid animation or incoherent depth history fail before
/// an asset store changes. The caller must provide the real `SceneDepth` handle.
pub fn prepare(
    resident: &FishStandResident,
    lighting: &LightingBindings,
    camera: &ViewPacket,
    depth: &DepthCopy,
    contact_depth: Handle<Image>,
    frame: SurfaceFrame,
) -> Result<FullMaterial, String> {
    if contact_depth != depth.image {
        return Err("Complete stall contact depth differs from the encoded copy receipt".into());
    }
    if !frame.time.is_finite() || !frame.night.is_finite() || !(0.0..=1.0).contains(&frame.night) {
        return Err("Invalid complete stall surface frame".into());
    }
    let contact = contact_frame::prepare(
        camera,
        Some(depth),
        &lighting.scene_lighting,
        frame.sea_level,
        frame.contact_strength,
        frame.frame_index,
    )?;
    let textures = FishStandMaterial::from_resident(resident, lighting.scene_lighting.clone())?;
    Ok(FullMaterial {
        surf_a: textures.surf_a,
        surf_n: textures.surf_n,
        surf_r: textures.surf_r,
        prop_a: textures.prop_a,
        prop_n: textures.prop_n,
        prop_r: textures.prop_r,
        signs: textures.signs,
        surface_frame: Vec4::new(frame.time, frame.night, 1.0, 1.0),
        environment: lighting.environment.clone(),
        hill_shadow: lighting.hill_shadow.clone(),
        object_shadow: lighting.object_shadow.clone(),
        object_settings: lighting.object_settings.clone(),
        scene_lighting: lighting.scene_lighting.clone(),
        clouds: lighting.clouds,
        cloud_shadow: lighting.cloud_shadow.clone(),
        live_shadows: crate::tidewater_object_shadow::receiver::DEFAULT_BUFFER.clone(),
        underwater_params: lighting.caustics.params.clone(),
        underwater_level: lighting.caustics.light_maps.level.clone(),
        underwater_map_frame: lighting.caustics.light_maps.frame.clone(),
        ground_bounce: lighting.ground_bounce.clone(),
        bounce_settings: lighting.bounce_settings,
        local_lights: crate::tidewater_material::local_lights::gpu::BUFFER.clone(),
        scene_buffer: crate::tidewater_material::scene_buffer::HANDLE.clone(),
        shared_scene_lighting: false,
        contact_depth,
        contact_frame: contact,
        render_contact_frame: super::contact_gpu::BUFFER.clone(),
        render_owned_contact: false,
    })
}
