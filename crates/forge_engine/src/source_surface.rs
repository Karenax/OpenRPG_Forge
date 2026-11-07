//! Source-profile surface rendering shared by every scene integration.
//! Input shaders and textures must already be admitted by the caller.
// SPDX-License-Identifier: MIT
// Transferred from forge_tidewater; source shader attribution remains in third_party/tidewater/LICENSE.
use crate::{
    source_lighting::{SceneLighting, scene_buffer},
    source_shadows::ObjectShadowSettings,
};
use bevy::{
    mesh::MeshVertexBufferLayoutRef,
    pbr::{MaterialPipeline, MaterialPipelineKey},
    prelude::*,
    render::render_resource::{AsBindGroup, Face, RenderPipelineDescriptor, ShaderType},
    shader::ShaderRef,
};
use mesh::{DATA, TINT};
pub mod capture;
mod flare_sun;
pub mod geometry;
pub mod mesh;
pub mod nature;
#[cfg(feature = "physics-rapier")]
pub mod objects;
pub mod package;
pub mod receivers;
pub mod scene;
pub mod stall_material;
pub mod stall_programs;
pub mod stall_scene;
pub mod stall_shader;
pub mod underwater;
pub use flare_sun::{ImportedSunRelayPlugin, SurfaceSunPublication};

/// Runtime-owned scene lighting resources; never serialized into an asset.
#[derive(Clone)]
pub struct LightingBindings {
    pub scene_lighting: SceneLighting,
    pub environment: Handle<Image>,
    pub hill_shadow: Handle<Image>,
    pub object_shadow: Handle<Image>,
    pub object_settings: ObjectShadowSettings,
    pub clouds: [Vec4; 5],
    pub cloud_shadow: Option<Handle<Image>>,
    pub wind: Vec4,
    pub flags: Vec4,
    pub caustics: underwater::CausticMaterialInputs,
    pub ground_bounce: Option<Handle<Image>>,
    pub bounce_settings: Vec4,
}

pub const SURFACE_SHADER: Handle<Shader> =
    bevy::asset::uuid_handle!("a903e588-5665-48a5-aac3-d0deecff072a");
/// Engine program available even when a module declares no surface objects.
/// Texture, geometry and environment data still require their explicit sources.
pub const PROFILE_PROGRAM: &str = include_str!("source_surface/program.wgsl");

#[must_use]
pub const fn uses_wind(kind: u8) -> bool {
    matches!(kind, 5 | 6)
}

/// Adopt an explicitly prepared shader without package lookup or replacement resources.
pub fn stage_prepared_shader(world: &mut World, source: &str) -> Result<(), String> {
    if source.trim().is_empty() || source.len() > 2_000_000 {
        return Err("Invalid prepared source surface shader size".into());
    }
    world
        .get_resource_mut::<Assets<Shader>>()
        .ok_or("Source surface shader assets unavailable")?
        .insert(
            SURFACE_SHADER.id(),
            Shader::from_wgsl(source.to_owned(), "source-surface.wgsl"),
        )
        .map_err(|error| error.to_string())?;
    Ok(())
}

/// An untextured opaque surface; the source lighting path remains unchanged.
pub const PROCEDURAL_SOLID_KIND: u8 = 7;

const NEUTRAL_SURFACE: &str = r"
#ifdef VILLAGE_KIND_7
const PI: f32 = 3.141592653589793;
const TWO_PI: f32 = 6.283185307179586;
const INV_PI: f32 = 0.3183098861837907;
const EPS: f32 = 1e-5;
fn sat(x:f32)->f32 { return clamp(x,0.0,1.0); }
fn sourceVertex(v:ptr<function,SourceVertex>,o:ptr<function,Output>) {
    o.vTint=v.tint;
    o.vData=v.vdata;
}
fn sourceSurface(input:FragInput,s:ptr<function,Surface>) {}
#endif
";

pub fn neutral_surface_shader_source(source: &str) -> Result<String, String> {
    if source.contains("#ifdef VILLAGE_KIND_7") || !source.contains("#ifdef VILLAGE_KIND_0") {
        return Err("Village source neutral-surface seam is absent or already installed".into());
    }
    Ok(format!("{source}\n{NEUTRAL_SURFACE}"))
}

/// Retain every live scene receiver while copying the actual plain surface parameters.
// Exact neutral-contract parameters, not measured values: preserve defaults,
// including infinity and signed-zero equality, without introducing a tolerance.
#[allow(clippy::float_cmp)]
pub fn neutral_scene_material(
    lighting: &SourceSurfaceMaterial,
    surface: &StandardMaterial,
) -> Result<SourceSurfaceMaterial, String> {
    let defaults = StandardMaterial::default();
    let color = Vec4::from_array(surface.base_color.to_linear().to_f32_array());
    let emissive = surface.emissive.to_vec3();
    if !lighting.shared_scene_lighting
        || lighting.settings.flags.z != 0.0
        || surface.unlit
        || surface.alpha_mode != AlphaMode::Opaque
    {
        return Err(
            "Procedural solid requires current lit scene receivers and an opaque surface".into(),
        );
    }
    if surface.base_color_texture.is_some()
        || surface.normal_map_texture.is_some()
        || surface.metallic_roughness_texture.is_some()
        || surface.emissive_texture.is_some()
        || surface.occlusion_texture.is_some()
        || surface.depth_map.is_some()
    {
        return Err("Procedural solid cannot discard StandardMaterial textures".into());
    }
    if !color.is_finite()
        || !emissive.is_finite()
        || !surface.emissive.alpha.is_finite()
        || !surface.perceptual_roughness.is_finite()
        || !surface.metallic.is_finite()
        || !(0.0..=1.0).contains(&surface.perceptual_roughness)
        || !(0.0..=1.0).contains(&surface.metallic)
    {
        return Err("Procedural solid surface parameters must be finite and valid".into());
    }
    // The neutral branch implements only color, emissive, roughness and metallic.
    // Refuse other surface effects rather than silently changing their meaning.
    if surface.emissive_exposure_weight != defaults.emissive_exposure_weight
        || surface.reflectance != defaults.reflectance
        || surface.specular_tint != defaults.specular_tint
        || surface.diffuse_transmission != defaults.diffuse_transmission
        || surface.specular_transmission != defaults.specular_transmission
        || surface.thickness != defaults.thickness
        || surface.ior != defaults.ior
        || surface.attenuation_color != defaults.attenuation_color
        || surface.attenuation_distance != defaults.attenuation_distance
        || surface.clearcoat != defaults.clearcoat
        || surface.clearcoat_perceptual_roughness != defaults.clearcoat_perceptual_roughness
        || surface.anisotropy_strength != defaults.anisotropy_strength
        || surface.anisotropy_rotation != defaults.anisotropy_rotation
        || surface.flip_normal_map_y != defaults.flip_normal_map_y
        || surface.double_sided
        || surface.cull_mode != Some(Face::Back)
        || surface.fog_enabled != defaults.fog_enabled
        || surface.depth_bias != defaults.depth_bias
        || surface.lightmap_exposure != defaults.lightmap_exposure
        || surface.uv_transform != defaults.uv_transform
    {
        return Err("Procedural solid cannot discard non-neutral StandardMaterial effects".into());
    }
    SceneLighting::new(
        lighting.scene_lighting.sun_direction.truncate().to_array(),
        lighting.scene_lighting.sun_color.truncate().to_array(),
        lighting
            .scene_lighting
            .environment_sh
            .map(|value| value.truncate().to_array()),
    )?;
    let mut material = lighting.clone();
    material.kind = PROCEDURAL_SOLID_KIND;
    material.settings.color = color;
    material.settings.emissive = emissive.extend(1.0);
    material.settings.surface = Vec4::new(surface.perceptual_roughness, surface.metallic, 0.0, 1.0);
    Ok(material)
}

fn source_double_sided(kind: u8) -> bool {
    matches!(kind, 5 | 6)
}

#[derive(Clone, Debug, PartialEq, ShaderType)]
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
    pub shared_scene_lighting: bool,
}
#[derive(Asset, TypePath, AsBindGroup, Debug, Clone, PartialEq)]
#[bind_group_data(Key)]
pub struct SourceSurfaceMaterial {
    #[storage(55, read_only)]
    #[dependency]
    pub scene_buffer: Handle<bevy::render::storage::ShaderBuffer>,
    /// CPU metadata remains available to scene effects; this flag selects the GPU receiver.
    pub shared_scene_lighting: bool,
    #[storage(54, read_only)]
    #[dependency]
    pub local_lights: Handle<bevy::render::storage::ShaderBuffer>,
    #[uniform(18)]
    pub scene_lighting: SceneLighting,
    #[uniform(0)]
    pub settings: Settings,
    #[texture(1)]
    #[sampler(5)]
    #[dependency]
    pub first: Handle<Image>,
    #[texture(2)]
    #[dependency]
    pub second: Handle<Image>,
    #[texture(3)]
    #[dependency]
    pub third: Handle<Image>,
    #[texture(4)]
    #[dependency]
    pub fourth: Handle<Image>,
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
    pub object_settings: ObjectShadowSettings,
    #[storage(44, read_only)]
    #[dependency]
    pub live_shadows: Handle<bevy::render::storage::ShaderBuffer>,
    #[uniform(48)]
    pub underwater_params: underwater::CausticSampleParams,
    #[texture(49, dimension = "2d_array")]
    #[sampler(47)]
    #[dependency]
    pub underwater_level: Handle<Image>,
    #[storage(53, read_only)]
    #[dependency]
    pub underwater_map_frame: Handle<bevy::render::storage::ShaderBuffer>,
    #[texture(50, filterable = false)]
    #[dependency]
    pub ground_bounce: Option<Handle<Image>>,
    #[uniform(51)]
    pub bounce_settings: Vec4,
    pub kind: u8,
}
impl From<&SourceSurfaceMaterial> for Key {
    fn from(value: &SourceSurfaceMaterial) -> Self {
        Self {
            kind: value.kind,
            shared_scene_lighting: value.shared_scene_lighting,
        }
    }
}
impl Material for SourceSurfaceMaterial {
    fn vertex_shader() -> ShaderRef {
        SURFACE_SHADER.clone().into()
    }
    fn fragment_shader() -> ShaderRef {
        SURFACE_SHADER.clone().into()
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
        if key.bind_group_data.shared_scene_lighting {
            descriptor
                .vertex
                .shader_defs
                .push(scene_buffer::receiver::DEFINE.into());
            if let Some(fragment) = &mut descriptor.fragment {
                fragment
                    .shader_defs
                    .push(scene_buffer::receiver::DEFINE.into());
            }
        }
        descriptor.vertex.shader_defs.push(define.clone().into());
        if let Some(fragment) = &mut descriptor.fragment {
            fragment.shader_defs.push(define.into());
            if source_double_sided(key.bind_group_data.kind) {
                fragment.shader_defs.push("VILLAGE_DOUBLE_SIDED".into());
            }
        }
        descriptor.primitive.cull_mode = if source_double_sided(key.bind_group_data.kind) {
            None
        } else {
            Some(Face::Back)
        };
        Ok(())
    }
}

#[cfg(test)]
pub(crate) mod tests {
    use super::*;

    #[test]
    fn shared_surface_motion_covers_cloth_and_foliage_only() {
        for kind in 0..=PROCEDURAL_SOLID_KIND {
            assert_eq!(uses_wind(kind), matches!(kind, 5 | 6));
        }
    }

    #[test]
    fn shader_adoption_requires_assets_and_preserves_current_shader_on_refusal() {
        let mut world = World::new();
        assert!(stage_prepared_shader(&mut world, "fn sourceSurface() {}").is_err());
        world.init_resource::<Assets<Shader>>();
        stage_prepared_shader(&mut world, "fn sourceSurface() {}").unwrap();
        assert_eq!(world.resource::<Assets<Shader>>().len(), 1);
        assert!(stage_prepared_shader(&mut world, "  ").is_err());
        assert!(stage_prepared_shader(&mut world, &"a".repeat(2_000_001)).is_err());
        let current = world
            .resource::<Assets<Shader>>()
            .get(&SURFACE_SHADER)
            .unwrap();
        assert_eq!(current.source.as_str(), "fn sourceSurface() {}");
        assert_eq!(world.resource::<Assets<Shader>>().len(), 1);
    }

    pub(crate) fn lighting() -> SourceSurfaceMaterial {
        SourceSurfaceMaterial {
            scene_buffer: default(),
            shared_scene_lighting: true,
            local_lights: default(),
            scene_lighting: SceneLighting::new([0.0, 1.0, 0.0], [4.0, 3.0, 2.0], [[0.2; 3]; 9])
                .unwrap(),
            settings: Settings {
                wind: Vec4::ZERO,
                color: Vec4::ONE,
                emissive: Vec4::ZERO,
                surface: Vec4::new(0.85, 0.0, 0.0, 1.0),
                flags: Vec4::ZERO,
            },
            first: default(),
            second: default(),
            third: default(),
            fourth: default(),
            environment: default(),
            hill_shadow: default(),
            object_shadow: default(),
            clouds: [Vec4::ZERO; 5],
            cloud_shadow: None,
            object_settings: default(),
            live_shadows: default(),
            underwater_params: underwater::CausticSampleParams::new(96.0, 256.0, Vec3::Y, 0.0)
                .unwrap(),
            underwater_level: default(),
            underwater_map_frame: default(),
            ground_bounce: None,
            bounce_settings: Vec4::ZERO,
            kind: 0,
        }
    }

    #[test]
    fn neutral_surface_keeps_authoring_parameters_and_all_source_receivers() {
        let source = lighting();
        let surface = StandardMaterial {
            base_color: Color::srgb(0.55, 0.48, 0.37),
            perceptual_roughness: 0.9,
            ..default()
        };
        let neutral = neutral_scene_material(&source, &surface).unwrap();
        assert_eq!(neutral.kind, PROCEDURAL_SOLID_KIND);
        assert_eq!(
            neutral.settings.color,
            Vec4::from_array(surface.base_color.to_linear().to_f32_array())
        );
        assert_eq!(neutral.settings.surface, Vec4::new(0.9, 0.0, 0.0, 1.0));
        assert_eq!(neutral.settings.emissive, Vec4::W);
        let mut restored = neutral.clone();
        restored.kind = source.kind;
        restored.settings = source.settings.clone();
        assert_eq!(
            restored, source,
            "every receiver binding must be cloned from the real resident scene"
        );
        assert!(!source_double_sided(PROCEDURAL_SOLID_KIND));
        assert!(source_double_sided(5) && source_double_sided(6));
        let mut unlit = source;
        unlit.settings.flags.z = 1.0;
        assert!(neutral_scene_material(&unlit, &surface).is_err());
    }

    #[test]
    fn neutral_surface_rejects_textures_non_neutral_and_non_finite_parameters() {
        let source = lighting();
        let mutations: [fn(&mut StandardMaterial); 19] = [
            |surface| surface.base_color_texture = Some(default()),
            |surface| surface.normal_map_texture = Some(default()),
            |surface| surface.metallic_roughness_texture = Some(default()),
            |surface| surface.emissive_texture = Some(default()),
            |surface| surface.occlusion_texture = Some(default()),
            |surface| surface.depth_map = Some(default()),
            |surface| surface.base_color = Color::linear_rgba(f32::NAN, 0.0, 0.0, 1.0),
            |surface| surface.emissive.red = f32::INFINITY,
            |surface| surface.perceptual_roughness = f32::NAN,
            |surface| surface.metallic = 1.5,
            |surface| surface.alpha_mode = AlphaMode::Blend,
            |surface| surface.unlit = true,
            |surface| surface.reflectance = 0.25,
            |surface| surface.diffuse_transmission = 0.2,
            |surface| surface.clearcoat = 0.5,
            |surface| surface.anisotropy_strength = 0.4,
            |surface| surface.double_sided = true,
            |surface| surface.depth_bias = 0.01,
            |surface| surface.emissive_exposure_weight = 1.0,
        ];
        for (index, mutate) in mutations.into_iter().enumerate() {
            let mut surface = StandardMaterial::default();
            mutate(&mut surface);
            assert!(
                neutral_scene_material(&source, &surface).is_err(),
                "mutation {index} was silently accepted"
            );
        }
    }

    #[test]
    fn neutral_shader_branch_is_valid_and_does_not_change_the_surface() {
        let structs = r"
struct SourceVertex { position:vec3f,normal:vec3f,uv:vec2f,tint:vec3f,vdata:vec4f };
struct Output { vTint:vec3f,vData:vec4f };
struct FragInput { P:vec3f,N:vec3f };
struct Surface { albedo:vec3f,alpha:f32,normal:vec3f,roughness:f32,metalness:f32,
    emissive:vec3f,ao:f32,envIntensity:f32 };
";
        let branch = NEUTRAL_SURFACE
            .replace("#ifdef VILLAGE_KIND_7", "")
            .replace("#endif", "");
        let module = naga::front::wgsl::parse_str(&format!("{structs}\n{branch}")).unwrap();
        naga::valid::Validator::new(
            naga::valid::ValidationFlags::all(),
            naga::valid::Capabilities::all(),
        )
        .validate(&module)
        .unwrap();
        let surface = module
            .functions
            .iter()
            .find(|(_, value)| value.name.as_deref() == Some("sourceSurface"))
            .unwrap()
            .1;
        assert!(
            surface.body.len() == 1
                && matches!(surface.body[0], naga::Statement::Return { value: None }),
            "neutral surface must only contain Naga's implicit void return: {:?}",
            surface.body
        );
        let derived = neutral_surface_shader_source("#ifdef VILLAGE_KIND_0\n#endif").unwrap();
        assert!(derived.starts_with("#ifdef VILLAGE_KIND_0\n#endif"));
        assert!(neutral_surface_shader_source(&derived).is_err());
    }
}
