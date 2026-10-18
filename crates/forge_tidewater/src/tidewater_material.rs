//! Tidewater village material shared by native and Studio render adapters.
//! Callers install the verified shader bytes and provide the same environment maps.
use crate::tidewater_mesh::{DATA, TINT};
mod flare_sun;
pub mod local_lights;
pub use flare_sun::ImportedSunRelayPlugin;
pub mod scene_buffer;
pub mod scene_uniform;
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

/// Install verified source without an `AssetServer` fallback to a local fixture.
pub fn install_shader(app: &mut App, source: &str) -> Result<(), Box<dyn std::error::Error>> {
    stage_shader(app.world_mut(), source).map_err(Into::into)
}

pub fn stage_shader(world: &mut World, source: &str) -> Result<(), String> {
    let source = scene_shader_source(&neutral_surface_shader_source(source)?)?;
    install_scene_receivers(world)?;
    world
        .get_resource_mut::<Assets<Shader>>()
        .ok_or("Building shader assets unavailable")?
        .insert(
            VILLAGE_SHADER.id(),
            Shader::from_wgsl(source, "tidewater-package-village.wgsl"),
        )
        .map_err(|e| e.to_string())?;
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

fn neutral_surface_shader_source(source: &str) -> Result<String, String> {
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
    lighting: &VillageMaterial,
    surface: &StandardMaterial,
) -> Result<VillageMaterial, String> {
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

/// Shared scene receivers for procedural materials with the qualified lighting seams.
pub(crate) fn scene_shader_source(source: &str) -> Result<String, String> {
    if source.trim().is_empty() || source.len() > 1_000_000 {
        return Err("Invalid building shader size".into());
    }
    let source = crate::tidewater_object_shadow::receiver::shader_source(source)?;
    let source = crate::tidewater_caustics::village_shader_source(&source)?;
    let source = ground_bounce_shader_source(&source)?;
    let source = local_lights::shader_source(&source)?;
    scene_buffer::receiver::shader_source(&source, "VillageSceneLighting")
}

pub(crate) fn install_scene_receivers(world: &mut World) -> Result<(), String> {
    scene_buffer::receiver::install(world)?;
    crate::tidewater_object_shadow::receiver::install(world)?;
    crate::tidewater_underwater_lighting::install_neutral(world)?;
    local_lights::gpu::install_neutral(world)?;
    Ok(())
}

fn ground_bounce_shader_source(source: &str) -> Result<String, String> {
    const MARKER: &str = "+sourceEnvironmentLight(s,N,V)*villageUnderwaterAmbient(P)+s.emissive;";
    if !source.contains(MARKER) || source.contains("fn sourceGroundBounce(") {
        return Err("Building shader lacks the expected indirect-light seam".into());
    }
    let source = source.replace(
        MARKER,
        "+(sourceEnvironmentLight(s,N,V)+sourceGroundBounce(s,P,N))*villageUnderwaterAmbient(P)+s.emissive;",
    );
    Ok(format!(
        "{source}\n{}",
        r"
@group(#{MATERIAL_BIND_GROUP}) @binding(50) var groundBounceMap: texture_2d<f32>;
// x = source bounce strength, y = sea level. The map's alpha is ground height.
@group(#{MATERIAL_BIND_GROUP}) @binding(51) var<uniform> bounceSettings: vec4f;

fn sourceGroundBounce(s: LightingSurface, P: vec3f, N: vec3f) -> vec3f {
    let view = clamp(0.5 - N.y * 0.5, 0.0, 1.0);
    if (bounceSettings.x <= 0.0 || view <= 0.03 || P.y <= bounceSettings.y - 0.3) {
        return vec3f(0.0);
    }
    let size = vec2f(textureDimensions(groundBounceMap));
    let st = clamp(((P.xz + 1024.0) / 2048.0) * size - 0.5,
                   vec2f(0.0), size - vec2f(1.001));
    let i = vec2i(floor(st));
    let t = fract(st);
    let bounced = mix(
        mix(textureLoad(groundBounceMap, i, 0),
            textureLoad(groundBounceMap, i + vec2i(1, 0), 0), t.x),
        mix(textureLoad(groundBounceMap, i + vec2i(0, 1), 0),
            textureLoad(groundBounceMap, i + vec2i(1, 1), 0), t.x), t.y);
    let above = P.y - bounced.w;
    let fade = smoothstep(-0.3, 0.3, above)
        * mix(0.4, 1.0, 1.0 - smoothstep(4.0, 30.0, above));
    let L = sceneLighting.sunDirection.xyz;
    let Lh = L.xz / max(length(L.xz), 0.001);
    let selfShade = 1.0 - clamp(-dot(N.xz, Lh), 0.0, 1.0)
        * clamp(1.0 - L.y, 0.0, 1.0) * 0.6;
    let irradiance = bounced.xyz * sceneLighting.sunColor.xyz * cloudsShadow(P.xz)
        * (view * fade * selfShade * bounceSettings.x * 0.6);
    return s.albedo * (1.0 - s.metalness) * s.ao * irradiance * INV_PI;
}
"
    ))
}
#[derive(Clone, Debug, PartialEq, ShaderType)]
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
pub struct VillageMaterial {
    #[storage(55, read_only)]
    pub scene_buffer: Handle<bevy::render::storage::ShaderBuffer>,
    /// CPU metadata remains available to scene effects; this flag selects the GPU receiver.
    pub shared_scene_lighting: bool,
    #[storage(54, read_only)]
    pub local_lights: Handle<bevy::render::storage::ShaderBuffer>,
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
    #[storage(44, read_only)]
    pub live_shadows: Handle<bevy::render::storage::ShaderBuffer>,
    #[uniform(48)]
    pub underwater_params: crate::tidewater_caustics::CausticSampleParams,
    #[texture(49, dimension = "2d_array")]
    #[sampler(47)]
    pub underwater_level: Handle<Image>,
    #[storage(53, read_only)]
    pub underwater_map_frame: Handle<bevy::render::storage::ShaderBuffer>,
    #[texture(50, filterable = false)]
    pub ground_bounce: Option<Handle<Image>>,
    #[uniform(51)]
    pub bounce_settings: Vec4,
    pub kind: u8,
}
impl From<&VillageMaterial> for Key {
    fn from(value: &VillageMaterial) -> Self {
        Self {
            kind: value.kind,
            shared_scene_lighting: value.shared_scene_lighting,
        }
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

    pub(crate) fn lighting() -> VillageMaterial {
        VillageMaterial {
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
            underwater_params: crate::tidewater_caustics::CausticSampleParams::new(
                96.0,
                256.0,
                Vec3::Y,
                0.0,
            )
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
