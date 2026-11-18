//! Acquired scanned surface on the existing Village light and shadow receivers.
use super::SourceSurfaceMaterial;
use bevy::{
    pbr::{ExtendedMaterial, MaterialExtension},
    prelude::*,
    render::{
        render_resource::{AsBindGroup, TextureFormat},
        storage::ShaderBuffer,
    },
    shader::ShaderRef,
};

pub const SHADER: Handle<Shader> =
    bevy::asset::uuid_handle!("c6262aed-a1c4-4cf7-bf27-de02f8c76485");

#[derive(Asset, TypePath, AsBindGroup, Debug, Clone)]
pub struct ScannedExtension {
    #[texture(100, filterable = false)]
    #[dependency]
    pub heights: Handle<Image>,
    #[uniform(101)]
    pub terrain_domain: Vec4,
    #[uniform(101)]
    pub terrain_sun: Vec4,
    #[texture(102)]
    #[sampler(105)]
    #[dependency]
    pub albedo: Handle<Image>,
    #[texture(103)]
    #[dependency]
    pub normal: Handle<Image>,
    #[texture(104)]
    #[dependency]
    pub arm: Handle<Image>,
    #[storage(106, read_only)]
    #[dependency]
    pub instances: Handle<ShaderBuffer>,
}

impl MaterialExtension for ScannedExtension {
    fn vertex_shader() -> ShaderRef {
        SHADER.clone().into()
    }
    fn fragment_shader() -> ShaderRef {
        SHADER.clone().into()
    }
    fn enable_prepass() -> bool {
        false
    }
    fn enable_shadows() -> bool {
        false
    }
}

pub type ScannedMaterial = ExtendedMaterial<SourceSurfaceMaterial, ScannedExtension>;

/// Resources published by the area's resident terrain.
pub struct TerrainBindings {
    pub heights: Handle<Image>,
    pub domain: Vec4,
    pub sun: Vec4,
}

/// Stage the verified albedo, normal and ARM maps against the resident area lighting.
///
/// # Errors
/// Rejects a missing heightfield, incompatible terrain, shader or asset stores.
pub fn stage_material(
    world: &mut World,
    source: &str,
    base: SourceSurfaceMaterial,
    terrain: TerrainBindings,
    images: [Image; 3],
) -> Result<Handle<ScannedMaterial>, String> {
    let heights = world
        .get_resource::<Assets<Image>>()
        .and_then(|assets| assets.get(&terrain.heights))
        .ok_or("Scanned height image absent")?;
    if heights.texture_descriptor.format != TextureFormat::R32Float
        || heights.width() != heights.height()
        || heights.width() < 2
    {
        return Err("Scanned terrain image contract differs".into());
    }
    let resolution =
        u16::try_from(heights.width()).map_err(|_| "Scanned height texture too large")?;
    if !world.contains_resource::<Assets<ShaderBuffer>>()
        || !world.contains_resource::<Assets<ScannedMaterial>>()
    {
        return Err("Scanned material or instance-buffer store unavailable".into());
    }
    let mut material = prepare_material(
        base,
        ScannedExtension {
            heights: terrain.heights,
            terrain_domain: Vec4::new(
                terrain.domain.x,
                terrain.domain.y,
                f32::from(resolution),
                1.0,
            ),
            terrain_sun: terrain.sun,
            albedo: Handle::default(),
            normal: Handle::default(),
            arm: Handle::default(),
            instances: Handle::default(),
        },
    )?;
    stage_shader(world, source)?;
    let [albedo, normal, arm] =
        images.map(|image| world.resource_mut::<Assets<Image>>().add(image));
    material.extension.albedo = albedo;
    material.extension.normal = normal;
    material.extension.arm = arm;
    material.extension.instances = world
        .resource_mut::<Assets<ShaderBuffer>>()
        .add(ShaderBuffer::new(&[0; 16], default()));
    Ok(world
        .resource_mut::<Assets<ScannedMaterial>>()
        .add(material))
}

/// Apply source opaque/front material defaults while retaining live scene resources.
///
/// # Errors
/// Rejects malformed terrain parameters. Image layouts, storage record count and
/// mesh-tag indices must be checked by the scene adapter before adoption.
pub fn prepare_material(
    mut base: SourceSurfaceMaterial,
    extension: ScannedExtension,
) -> Result<ScannedMaterial, String> {
    let domain = extension.terrain_domain;
    let sun = extension.terrain_sun;
    if !domain.is_finite()
        || domain.y <= 0.0
        || domain.z < 2.0
        || domain.z.fract() != 0.0
        || domain.w < 1.0
        || !sun.is_finite()
        || (sun.truncate().length_squared() - 1.0).abs() > 0.001
        || !(0.0..=1.0).contains(&sun.w)
    {
        return Err("Invalid scanned terrain binding parameters".into());
    }
    base.kind = 0;
    base.settings.color = Vec4::ONE;
    base.settings.emissive = Vec4::new(0.0, 0.0, 0.0, 1.0);
    base.settings.surface = Vec4::new(0.85, 0.0, 0.0, 1.0);
    Ok(ScannedMaterial { base, extension })
}

/// Adapt a verified acquired instance program to the shared live receivers.
///
/// # Errors
/// Refuses drifted bindings/entry points and already adapted programs.
pub fn shader_source(source: &str) -> Result<String, String> {
    for marker in [
        "@binding(100) var terrainHeightTex:texture_2d<f32>;",
        "@binding(101) var<uniform> terrainParams:TerrainParams;",
        "@binding(102) var scanAlbedo:texture_2d<f32>;",
        "@binding(103) var scanNormal:texture_2d<f32>;",
        "@binding(104) var scanArm:texture_2d<f32>;",
        "@binding(105) var smpAnisoClamp:sampler;",
        "@binding(106) var<storage,read> scanInstances:array<vec4f>;",
        "v.iData=scanInstances[mesh_functions::get_tag(input.instance)];",
        "fn sourceShadow(in:FragInput)->bool{",
        "frame.cameraPos=view.world_position;",
        "frame.frameIndex=shadowParams.frameIndex;",
    ] {
        if source.matches(marker).count() != 1 {
            return Err("Scanned shader interface differs".into());
        }
    }
    super::receivers::scene_shader_source(source)
}

/// Install into the existing renderer; never fall back to a fixture shader.
///
/// # Errors
/// Propagates interface and resident asset-store errors.
pub fn stage_shader(world: &mut World, source: &str) -> Result<(), String> {
    let source = shader_source(source)?;
    super::receivers::install_scene_receivers(world)?;
    world
        .get_resource_mut::<Assets<Shader>>()
        .ok_or("Scanned shader assets unavailable")?
        .insert(
            SHADER.id(),
            Shader::from_wgsl(source, "tidewater-scanned-instance.wgsl"),
        )
        .map_err(|e| e.to_string())?;
    Ok(())
}
