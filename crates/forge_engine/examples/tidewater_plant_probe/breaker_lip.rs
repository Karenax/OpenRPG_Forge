//! Original transparent breaking lip, driven by the shared native crest buffer.
use bevy::{
    asset::RenderAssetUsages,
    camera::visibility::NoFrustumCulling,
    image::{ImageAddressMode, ImageFilterMode, ImageSampler, ImageSamplerDescriptor},
    light::NotShadowCaster,
    mesh::{Indices, MeshVertexAttribute, MeshVertexBufferLayoutRef},
    pbr::{MaterialPipeline, MaterialPipelineKey},
    prelude::*,
    render::{
        render_resource::{
            AsBindGroup, Extent3d, PrimitiveTopology, RenderPipelineDescriptor, TextureDimension,
            TextureFormat, VertexFormat,
        },
        storage::ShaderBuffer,
    },
    shader::ShaderRef,
};
use std::{collections::BTreeMap, error::Error};
const SHEET: MeshVertexAttribute =
    MeshVertexAttribute::new("BreakerSheet", 0x7457_0006, VertexFormat::Float32x4);
#[derive(Resource)]
struct Inputs {
    frame: [UVec4; 45],
    sky: [UVec4; 2],
    material: [UVec4; 3],
    lace: Vec<u8>,
}
fn words<const N: usize>(v: &[u8]) -> Result<[UVec4; N], Box<dyn Error>> {
    if v.len() != N * 16 {
        return Err("lip uniform size".into());
    }
    Ok(std::array::from_fn(|i| {
        UVec4::from_array(std::array::from_fn(|j| {
            u32::from_le_bytes(
                v[i * 16 + j * 4..i * 16 + j * 4 + 4]
                    .try_into()
                    .expect("checked"),
            )
        }))
    }))
}
pub fn install(app: &mut App, files: &BTreeMap<String, Vec<u8>>) -> Result<(), Box<dyn Error>> {
    if !files.contains_key("breaker-lip.wgsl") {
        return Ok(());
    }
    let get = |n: &str| files.get(n).ok_or("missing lip input");
    let mut lace = Vec::new();
    for mip in 0..10 {
        let data = get(&format!("lip-lace-m{mip}.rgba8.bin"))?;
        if data.len() != (512_usize >> mip).pow(2) * 4 {
            return Err("lip lace mip size".into());
        }
        lace.extend_from_slice(data);
    }
    app.insert_resource(Inputs {
        frame: words(get("water-frame.bin")?)?,
        sky: words(get("uniform-skyParams.bin")?)?,
        material: words(get("lip-material-params.bin")?)?,
        lace,
    });
    app.add_plugins(MaterialPlugin::<LipMaterial>::default())
        .add_systems(Startup, setup);
    Ok(())
}
#[derive(Asset, TypePath, AsBindGroup, Debug, Clone)]
pub struct LipMaterial {
    #[uniform(0)]
    pub(super) frame: [UVec4; 45],
    #[storage(1, read_only)]
    crest: Handle<ShaderBuffer>,
    #[texture(2)]
    #[sampler(3)]
    lace: Handle<Image>,
    #[uniform(4)]
    atmosphere: [Vec4; 4],
    #[uniform(5)]
    sky: [UVec4; 2],
    #[texture(6)]
    #[sampler(8)]
    transmittance: Handle<Image>,
    #[texture(7)]
    sky_lut: Handle<Image>,
    #[uniform(9)]
    material: [UVec4; 3],
    #[uniform(10)]
    pub(super) clouds: [Vec4; 5],
    #[texture(11)]
    #[sampler(12)]
    cloud_panorama: Option<Handle<Image>>,
    #[texture(13, filterable = false)]
    cloud_shadow: Option<Handle<Image>>,
}
impl Material for LipMaterial {
    fn vertex_shader() -> ShaderRef {
        "breaker-lip.wgsl".into()
    }
    fn fragment_shader() -> ShaderRef {
        "breaker-lip.wgsl".into()
    }
    fn alpha_mode(&self) -> AlphaMode {
        AlphaMode::Premultiplied
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
        descriptor.vertex.buffers = vec![layout.0.get_layout(&[
            Mesh::ATTRIBUTE_POSITION.at_shader_location(0),
            SHEET.at_shader_location(1),
        ])?];
        descriptor.primitive.cull_mode = None;
        if let Some(depth) = &mut descriptor.depth_stencil {
            depth.depth_write_enabled = Some(false);
        }
        Ok(())
    }
}
#[allow(clippy::needless_pass_by_value, clippy::too_many_arguments)]
fn setup(
    mut commands: Commands,
    inputs: Res<Inputs>,
    crests: Res<super::breakers::Targets>,
    atmosphere: Res<super::atmosphere::Targets>,
    clouds: Option<Res<super::sky::CloudInputs>>,
    mut meshes: ResMut<Assets<Mesh>>,
    mut images: ResMut<Assets<Image>>,
    mut materials: ResMut<Assets<LipMaterial>>,
) {
    let Some(topology) = forge_engine::breaker_geometry::lip_topology(crests.stations) else {
        return; // An authored terrain can have no continuous main-beach shoreline.
    };
    let mut mesh = Mesh::new(
        PrimitiveTopology::TriangleList,
        RenderAssetUsages::default(),
    );
    mesh.insert_attribute(
        Mesh::ATTRIBUTE_POSITION,
        vec![[0_f32; 3]; topology.sheet_ids.len()],
    );
    mesh.insert_attribute(SHEET, topology.sheet_ids);
    mesh.insert_indices(Indices::U32(topology.indices));
    let mut lace = Image::new_uninit(
        Extent3d {
            width: 512,
            height: 512,
            depth_or_array_layers: 1,
        },
        TextureDimension::D2,
        TextureFormat::Rgba8Unorm,
        RenderAssetUsages::default(),
    );
    lace.data = Some(inputs.lace.clone());
    lace.texture_descriptor.mip_level_count = 10;
    lace.sampler = ImageSampler::Descriptor(ImageSamplerDescriptor {
        address_mode_u: ImageAddressMode::Repeat,
        address_mode_v: ImageAddressMode::Repeat,
        mag_filter: ImageFilterMode::Linear,
        min_filter: ImageFilterMode::Linear,
        mipmap_filter: ImageFilterMode::Linear,
        anisotropy_clamp: 8,
        ..default()
    });
    let material = materials.add(LipMaterial {
        clouds: clouds.as_ref().map_or([Vec4::ZERO; 5], |v| v.params),
        cloud_panorama: clouds.as_ref().map(|v| v.panorama.clone()),
        cloud_shadow: clouds.as_ref().map(|v| v.shadow.clone()),
        frame: inputs.frame,
        crest: crests.buffers[0].clone(),
        lace: images.add(lace),
        atmosphere: atmosphere.params,
        sky: inputs.sky,
        transmittance: atmosphere.images[0].clone(),
        sky_lut: atmosphere.images[2].clone(),
        material: inputs.material,
    });
    commands.spawn((
        Mesh3d(meshes.add(mesh)),
        MeshMaterial3d(material),
        Transform::default(),
        NoFrustumCulling,
        NotShadowCaster,
    ));
}
