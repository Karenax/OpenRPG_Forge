// SPDX-License-Identifier: MIT
// Adapted from dgreenheck/tidewater, copyright 2026 DRG Software Solutions LLC.
// Revision 1438b1ab; see third_party/tidewater/LICENSE.
//! Resident bind group for the complete source stall material.
//! Scene adapters must stage the complete shader and publish live receiver data.
use bevy::{
    mesh::{MeshVertexAttribute, MeshVertexBufferLayoutRef},
    pbr::{MaterialPipeline, MaterialPipelineKey},
    prelude::*,
    render::{
        render_resource::{AsBindGroup, Face, RenderPipelineDescriptor, VertexFormat},
        storage::ShaderBuffer,
    },
    shader::ShaderRef,
};
use forge_engine::source_lighting::SceneLighting;
use forge_engine::source_scene_depth::{DepthCopy, contact_frame, contact_gpu};
use forge_engine::source_shadows::ObjectShadowSettings;
use forge_engine::source_surface::LightingBindings;
use forge_engine::source_surface::SourceSurfaceMaterial;
use forge_engine::source_temporal::frame::ViewPacket;
use std::collections::BTreeMap;

/// Source vertex attributes shared with the module's verified mesh adapter.
pub const LAYER: MeshVertexAttribute =
    MeshVertexAttribute::new("FishStandLayer", 0x7457_0007, VertexFormat::Float32);
pub const UV2: MeshVertexAttribute =
    MeshVertexAttribute::new("FishStandUv2", 0x7457_0008, VertexFormat::Float32x2);

/// Explicit image arrays required by the source material, independent of module storage.
pub struct TextureArrays {
    pub surf_a: Handle<Image>,
    pub surf_n: Handle<Image>,
    pub surf_r: Handle<Image>,
    pub prop_a: Handle<Image>,
    pub prop_n: Handle<Image>,
    pub prop_r: Handle<Image>,
    pub signs: Handle<Image>,
}

impl TextureArrays {
    /// Select the material's named arrays from an already admitted resource set.
    ///
    /// # Errors
    /// Rejects missing arrays before a material is constructed.
    pub fn from_named(textures: &BTreeMap<String, Handle<Image>>) -> Result<Self, String> {
        let texture = |name| {
            textures
                .get(name)
                .cloned()
                .ok_or("Stall texture unresolved".to_owned())
        };
        Ok(Self {
            surf_a: texture("surf_a")?,
            surf_n: texture("surf_n")?,
            surf_r: texture("surf_r")?,
            prop_a: texture("prop_a")?,
            prop_n: texture("prop_n")?,
            prop_r: texture("prop_r")?,
            signs: texture("signs")?,
        })
    }
}

/// Supplies named, already admitted texture handles to the shared material.
pub trait TextureSource {
    fn textures(&self) -> &BTreeMap<String, Handle<Image>>;
}

impl TextureSource for BTreeMap<String, Handle<Image>> {
    fn textures(&self) -> &BTreeMap<String, Handle<Image>> {
        self
    }
}

pub const BASIC_SHADER: Handle<Shader> =
    bevy::asset::uuid_handle!("c1396585-3120-4a83-92b3-32de96c58445");

/// Compact source-array material. The full material retains additional lighting responses.
#[derive(Asset, TypePath, AsBindGroup, Debug, Clone)]
#[bind_group_data(BasicLightingKey)]
pub struct BasicMaterial {
    #[storage(55, read_only)]
    pub scene_buffer: Handle<ShaderBuffer>,
    pub shared_scene_lighting: bool,
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
    #[uniform(18)]
    pub scene_lighting: SceneLighting,
}

impl BasicMaterial {
    /// Build a compact material from the caller's admitted named images.
    ///
    /// # Errors
    /// Refuses an incomplete array set.
    pub fn from_resident(
        resident: &(impl TextureSource + ?Sized),
        scene_lighting: SceneLighting,
    ) -> Result<Self, String> {
        let textures = TextureArrays::from_named(resident.textures())?;
        Ok(Self {
            scene_buffer: crate::source_lighting::scene_buffer::HANDLE.clone(),
            shared_scene_lighting: false,
            surf_a: textures.surf_a,
            surf_n: textures.surf_n,
            surf_r: textures.surf_r,
            prop_a: textures.prop_a,
            prop_n: textures.prop_n,
            prop_r: textures.prop_r,
            signs: textures.signs,
            scene_lighting,
        })
    }
}

#[derive(Clone, Copy, Debug, PartialEq, Eq, Hash)]
pub struct BasicLightingKey(bool);

impl From<&BasicMaterial> for BasicLightingKey {
    fn from(material: &BasicMaterial) -> Self {
        Self(material.shared_scene_lighting)
    }
}

impl Material for BasicMaterial {
    fn vertex_shader() -> ShaderRef {
        BASIC_SHADER.into()
    }
    fn fragment_shader() -> ShaderRef {
        BASIC_SHADER.into()
    }
    fn enable_shadows() -> bool {
        false
    }
    fn specialize(
        _: &MaterialPipeline,
        descriptor: &mut RenderPipelineDescriptor,
        layout: &MeshVertexBufferLayoutRef,
        key: MaterialPipelineKey<Self>,
    ) -> Result<(), bevy::material::specialize::SpecializedMeshPipelineError> {
        if descriptor.vertex.shader.id() == BASIC_SHADER.id() {
            descriptor.vertex.buffers = vec![layout.0.get_layout(&[
                Mesh::ATTRIBUTE_POSITION.at_shader_location(0),
                Mesh::ATTRIBUTE_NORMAL.at_shader_location(1),
                Mesh::ATTRIBUTE_UV_0.at_shader_location(2),
                Mesh::ATTRIBUTE_COLOR.at_shader_location(3),
                LAYER.at_shader_location(4),
                UV2.at_shader_location(5),
            ])?];
        }
        descriptor.primitive.cull_mode = Some(Face::Back);
        if key.bind_group_data.0 {
            let define = crate::source_lighting::scene_buffer::receiver::DEFINE;
            descriptor.vertex.shader_defs.push(define.into());
            if let Some(fragment) = &mut descriptor.fragment {
                fragment.shader_defs.push(define.into());
            }
        }
        Ok(())
    }
}

/// Install the compact source shader without an external asset lookup.
///
/// # Errors
/// Refuses unavailable shader assets or invalid shared scene receivers.
pub fn install_basic_shader(app: &mut App) -> Result<(), String> {
    crate::source_lighting::scene_buffer::receiver::install(app.world_mut())?;
    let source = crate::source_lighting::scene_buffer::receiver::shader_source(
        crate::source_surface::stall_programs::BASIC,
        "FishStandSceneLighting",
    )?;
    app.world_mut()
        .get_resource_mut::<Assets<Shader>>()
        .ok_or("Stall shader assets unavailable")?
        .insert(
            BASIC_SHADER.id(),
            Shader::from_wgsl(source, "forge-stall-basic.wgsl"),
        )
        .map_err(|error| error.to_string())?;
    Ok(())
}

pub const SHADER: Handle<Shader> =
    bevy::asset::uuid_handle!("f27b274e-0912-4fc5-aa0d-0314c7d56efe");

/// Stage the complete shader from the caller's verified scene lighting input.
/// Invalid composition leaves the shader store unchanged.
pub fn stage_shader(world: &mut World, lighting_source: &str) -> Result<(), String> {
    let source = forge_engine::source_surface::stall_scene::shader_source(lighting_source)?;
    if !world.contains_resource::<Assets<Shader>>() {
        return Err("Complete stall requires initialized shader assets".into());
    }
    forge_engine::source_lighting::scene_buffer::receiver::install(world)?;
    contact_gpu::install_buffer(world)?;
    world
        .resource_mut::<Assets<Shader>>()
        .insert(
            SHADER.id(),
            Shader::from_wgsl(source, "forge-complete-stall.wgsl"),
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
    pub underwater_params: forge_engine::source_surface::underwater::CausticSampleParams,
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
            let define = forge_engine::source_lighting::scene_buffer::receiver::DEFINE;
            descriptor.vertex.shader_defs.push(define.into());
            if let Some(fragment) = &mut descriptor.fragment {
                fragment.shader_defs.push(define.into());
            }
        }
        if key.bind_group_data.render_owned_contact {
            let define = forge_engine::source_surface::stall_shader::RENDER_CONTACT_DEFINE;
            descriptor.vertex.shader_defs.push(define.into());
            if let Some(fragment) = &mut descriptor.fragment {
                fragment.shader_defs.push(define.into());
            }
        }
        Ok(())
    }
}

/// Surface animation inputs from the caller's shared scene frame.
#[derive(Clone, Copy)]
pub struct SurfaceFrame {
    pub time: f32,
    pub night: f32,
    pub frame_index: u32,
    pub sea_level: f32,
    pub contact_strength: f32,
}

impl FullMaterial {
    /// Refresh common scene resources while the `RenderWorld` owns camera history.
    /// Surface animation and contacts are supplied by the resident render buffer.
    pub fn update_render_receivers(
        &mut self,
        lighting: &SourceSurfaceMaterial,
    ) -> Result<(), String> {
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
        lighting: &SourceSurfaceMaterial,
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

    fn apply_common(&mut self, lighting: &SourceSurfaceMaterial) {
        self.environment = lighting.environment.clone();
        self.hill_shadow = lighting.hill_shadow.clone();
        self.object_shadow = lighting.object_shadow.clone();
        self.object_settings = lighting.object_settings.clone();
        self.scene_lighting = lighting.scene_lighting.clone();
        self.clouds = lighting.clouds;
        self.cloud_shadow.clone_from(&lighting.cloud_shadow);
        self.live_shadows = lighting.live_shadows.clone();
        self.underwater_params = lighting.underwater_params.clone();
        self.underwater_level = lighting.underwater_level.clone();
        self.underwater_map_frame = lighting.underwater_map_frame.clone();
        self.ground_bounce.clone_from(&lighting.ground_bounce);
        self.bounce_settings = lighting.bounce_settings;
        self.local_lights = lighting.local_lights.clone();
        self.scene_buffer = lighting.scene_buffer.clone();
        self.shared_scene_lighting = lighting.shared_scene_lighting;
    }
}

/// Build the actual live material with the scene's resident receiver handles.
/// The inactive initial contact metadata is explicit; `contact_gpu::register`
/// must be installed by the scene adapter before this material is displayed.
pub fn prepare_render_owned(
    textures: &TextureArrays,
    lighting: &SourceSurfaceMaterial,
    contact_depth: Handle<Image>,
    inputs: &contact_gpu::Inputs,
) -> Result<FullMaterial, String> {
    let seed = contact_gpu::binding_seed(inputs)?;
    Ok(FullMaterial {
        surf_a: textures.surf_a.clone(),
        surf_n: textures.surf_n.clone(),
        surf_r: textures.surf_r.clone(),
        prop_a: textures.prop_a.clone(),
        prop_n: textures.prop_n.clone(),
        prop_r: textures.prop_r.clone(),
        signs: textures.signs.clone(),
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
        render_contact_frame: contact_gpu::BUFFER.clone(),
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
    textures: &TextureArrays,
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
    Ok(FullMaterial {
        surf_a: textures.surf_a.clone(),
        surf_n: textures.surf_n.clone(),
        surf_r: textures.surf_r.clone(),
        prop_a: textures.prop_a.clone(),
        prop_n: textures.prop_n.clone(),
        prop_r: textures.prop_r.clone(),
        signs: textures.signs.clone(),
        surface_frame: Vec4::new(frame.time, frame.night, 1.0, 1.0),
        environment: lighting.environment.clone(),
        hill_shadow: lighting.hill_shadow.clone(),
        object_shadow: lighting.object_shadow.clone(),
        object_settings: lighting.object_settings.clone(),
        scene_lighting: lighting.scene_lighting.clone(),
        clouds: lighting.clouds,
        cloud_shadow: lighting.cloud_shadow.clone(),
        live_shadows: forge_engine::source_shadows::receiver::DEFAULT_BUFFER.clone(),
        underwater_params: lighting.caustics.params.clone(),
        underwater_level: lighting.caustics.light_maps.level.clone(),
        underwater_map_frame: lighting.caustics.light_maps.frame.clone(),
        ground_bounce: lighting.ground_bounce.clone(),
        bounce_settings: lighting.bounce_settings,
        local_lights: forge_engine::source_lighting::local_lights::gpu::BUFFER.clone(),
        scene_buffer: forge_engine::source_lighting::scene_buffer::HANDLE.clone(),
        shared_scene_lighting: false,
        contact_depth,
        contact_frame: contact,
        render_contact_frame: contact_gpu::BUFFER.clone(),
        render_owned_contact: false,
    })
}

#[cfg(test)]
mod tests {
    use super::*;

    #[test]
    fn arrays_use_only_the_explicit_named_inputs() {
        let mut images = Assets::<Image>::default();
        let mut named = BTreeMap::new();
        for name in [
            "surf_a", "surf_n", "surf_r", "prop_a", "prop_n", "prop_r", "signs",
        ] {
            named.insert(name.to_owned(), images.add(Image::default()));
        }
        let arrays = TextureArrays::from_named(&named).unwrap();
        assert_eq!(arrays.surf_a, named["surf_a"]);
        assert_eq!(arrays.surf_n, named["surf_n"]);
        assert_eq!(arrays.surf_r, named["surf_r"]);
        assert_eq!(arrays.prop_a, named["prop_a"]);
        assert_eq!(arrays.prop_n, named["prop_n"]);
        assert_eq!(arrays.prop_r, named["prop_r"]);
        assert_eq!(arrays.signs, named["signs"]);
        let lighting = SceneLighting::new([0.0, 1.0, 0.0], [1.0; 3], [[0.0; 3]; 9]).unwrap();
        let basic = BasicMaterial::from_resident(&named, lighting.clone()).unwrap();
        assert_eq!(basic.surf_a, named["surf_a"]);
        assert_eq!(basic.signs, named["signs"]);
        assert_eq!(basic.scene_lighting, lighting);
        named.remove("prop_r");
        assert!(TextureArrays::from_named(&named).is_err());
        assert!(BasicMaterial::from_resident(&named, lighting).is_err());
    }
}
