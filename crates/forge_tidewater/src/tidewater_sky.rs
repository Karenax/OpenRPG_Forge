//! Clear-sky background using native atmosphere LUTs on the scene's device.
pub mod frame_lighting;
pub mod solar;
pub mod solar_lighting;
use bevy::{
    camera::visibility::NoFrustumCulling,
    light::NotShadowCaster,
    mesh::MeshVertexBufferLayoutRef,
    pbr::{MaterialPipeline, MaterialPipelineKey},
    prelude::*,
    render::render_resource::{AsBindGroup, RenderPipelineDescriptor, ShaderType},
    shader::ShaderRef,
};
#[derive(Clone, Debug, ShaderType)]
pub struct Settings {
    pub moon_disk: Vec4,
    pub stars_time_night: Vec4,
}
#[derive(Asset, TypePath, AsBindGroup, Debug, Clone)]
pub struct SkyMaterial {
    #[uniform(0)]
    pub atmosphere: [Vec4; 4],
    #[uniform(1)]
    pub settings: Settings,
    #[texture(2)]
    #[sampler(4)]
    pub transmittance: Handle<Image>,
    #[texture(3)]
    pub sky: Handle<Image>,
    #[uniform(5)]
    pub clouds: [Vec4; 5],
    #[texture(6)]
    #[sampler(8)]
    pub panorama: Option<Handle<Image>>,
    #[texture(7)]
    pub cloud_view: Option<Handle<Image>>,
}

pub const SKY_SHADER: Handle<Shader> =
    bevy::asset::uuid_handle!("53b6cd03-0c21-4de6-81ed-b25884ad01a6");

/// The caller supplies the verified sky shader; no `AssetServer` path fallback.
pub fn install_shader(app: &mut App, source: &str) -> Result<(), String> {
    stage_shader(app.world_mut(), source)
}

/// Update a verified shader after renderer startup.
pub fn stage_shader(world: &mut World, source: &str) -> Result<(), String> {
    if source.trim().is_empty() || source.len() > 1_000_000 {
        return Err("invalid sky shader size".into());
    }
    let Some(mut shaders) = world.get_resource_mut::<Assets<Shader>>() else {
        return Err("sky requires initialized shader assets".into());
    };
    shaders
        .insert(
            SKY_SHADER.id(),
            Shader::from_wgsl(filter_cloud_view(source), "tidewater-sky.wgsl"),
        )
        .map_err(|e| e.to_string())
}

fn filter_cloud_view(source: &str) -> String {
    let sample = "textureSampleLevel( cloudsView, smpLinearClamp, uv, 0.0 )";
    if !source.contains(sample) {
        return source.to_owned();
    }
    let filtered = source.replace(sample, "forgeCloudViewFiltered( uv )");
    format!(
        "{}\n{}",
        filtered,
        include_str!("tidewater_sky/cloud_filter.wgsl")
    )
}

/// A camera-facing background drawn at far depth; scene settings belong to the caller.
pub fn spawn(
    commands: &mut Commands,
    meshes: &mut Assets<Mesh>,
    materials: &mut Assets<SkyMaterial>,
    material: SkyMaterial,
) -> Entity {
    commands
        .spawn((
            Mesh3d(meshes.add(Rectangle::new(2.0, 2.0))),
            MeshMaterial3d(materials.add(material)),
            Transform::default(),
            NoFrustumCulling,
            NotShadowCaster,
        ))
        .id()
}
impl Material for SkyMaterial {
    fn vertex_shader() -> ShaderRef {
        SKY_SHADER.clone().into()
    }
    fn fragment_shader() -> ShaderRef {
        SKY_SHADER.clone().into()
    }
    fn enable_prepass() -> bool {
        false
    }
    fn enable_shadows() -> bool {
        false
    }
    fn specialize(
        _: &MaterialPipeline,
        descriptor: &mut RenderPipelineDescriptor,
        layout: &MeshVertexBufferLayoutRef,
        _: MaterialPipelineKey<Self>,
    ) -> Result<(), bevy::material::specialize::SpecializedMeshPipelineError> {
        descriptor.vertex.buffers = vec![
            layout
                .0
                .get_layout(&[Mesh::ATTRIBUTE_POSITION.at_shader_location(0)])?,
        ];
        descriptor.primitive.cull_mode = None;
        if let Some(depth) = &mut descriptor.depth_stencil {
            depth.depth_write_enabled = Some(false);
            depth.depth_compare = Some(bevy::render::render_resource::CompareFunction::Equal);
        }
        Ok(())
    }
}

#[cfg(feature = "physics-rapier")]
pub mod preview;
