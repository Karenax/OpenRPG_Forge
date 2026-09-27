//! Original spray sprite shading fed directly by native simulation buffers.
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
const SLOT: MeshVertexAttribute =
    MeshVertexAttribute::new("SpraySlot", 0x7457_0007, VertexFormat::Uint32);
const SPRITE_SHADER: Handle<Shader> =
    bevy::asset::uuid_handle!("6e8cc1d5-5811-4f49-a124-de4efc4132dd");
#[derive(Resource)]
struct Inputs {
    frame: [UVec4; 45],
    material: [UVec4; 3],
    puff: Vec<u8>,
    dots: Vec<u8>,
}
fn words<const N: usize>(v: &[u8]) -> Result<[UVec4; N], Box<dyn Error>> {
    if v.len() != N * 16 {
        return Err("spray uniform size".into());
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
    let get = |n: &str| files.get(n).ok_or("missing sprite input");
    let source = std::str::from_utf8(get("spray-sprites.wgsl")?)?;
    let stations = app.world().resource::<super::spray::Targets>().stations;
    let guard = "idx < 1154.0";
    if source.matches(guard).count() != 1 {
        return Err("sprite crest shadow guard mismatch".into());
    }
    let shader = source.replacen(guard, &format!("idx < {}.0", stations * 2), 1);
    app.world_mut().resource_mut::<Assets<Shader>>().insert(
        SPRITE_SHADER.id(),
        Shader::from_wgsl(shader, "spray-sprites.wgsl"),
    )?;
    let mips = |name: &str, side: usize, count: usize| -> Result<Vec<u8>, Box<dyn Error>> {
        let mut out = Vec::new();
        for i in 0..count {
            let data = get(&format!("sprite-{name}-m{i}.rgba8.bin"))?;
            if data.len() != (side >> i).pow(2) * 4 {
                return Err("sprite texture mip size".into());
            }
            out.extend_from_slice(data);
        }
        Ok(out)
    };
    app.insert_resource(Inputs {
        frame: words(get("water-frame.bin")?)?,
        material: words(get("sprite-material-params.bin")?)?,
        puff: mips("sprayPuff", 64, 7)?,
        dots: mips("sprayDots", 128, 8)?,
    });
    app.add_plugins(MaterialPlugin::<SprayMaterial>::default())
        .add_systems(Startup, setup);
    Ok(())
}
#[derive(Asset, TypePath, AsBindGroup, Debug, Clone)]
pub struct SprayMaterial {
    #[uniform(40)]
    clouds: [Vec4; 5],
    #[texture(41, filterable = false)]
    cloud_shadow: Option<Handle<Image>>,
    #[uniform(0)]
    frame: [UVec4; 45],
    #[storage(1, read_only)]
    pos: Handle<ShaderBuffer>,
    #[storage(2, read_only)]
    vel: Handle<ShaderBuffer>,
    #[storage(3, read_only)]
    info: Handle<ShaderBuffer>,
    #[storage(4, read_only)]
    crest: Handle<ShaderBuffer>,
    #[texture(5)]
    #[sampler(6)]
    puff: Handle<Image>,
    #[texture(7)]
    dots: Handle<Image>,
    #[texture(8, sample_type = "depth")]
    depth: Handle<Image>,
    #[uniform(9)]
    material: [UVec4; 3],
}
impl Material for SprayMaterial {
    fn vertex_shader() -> ShaderRef {
        SPRITE_SHADER.into()
    }
    fn fragment_shader() -> ShaderRef {
        SPRITE_SHADER.into()
    }
    fn alpha_mode(&self) -> AlphaMode {
        AlphaMode::Premultiplied
    }
    fn depth_bias(&self) -> f32 {
        -20.0
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
            SLOT.at_shader_location(1),
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
    clouds: Option<Res<super::sky::CloudInputs>>,
    spray: Res<super::spray::Targets>,
    depth: Res<super::water_scene::SceneDepth>,
    mut meshes: ResMut<Assets<Mesh>>,
    mut images: ResMut<Assets<Image>>,
    mut materials: ResMut<Assets<SprayMaterial>>,
) {
    let mut positions = Vec::with_capacity(40960 * 4);
    let mut slots = Vec::with_capacity(40960 * 4);
    let mut indices = Vec::with_capacity(40960 * 6);
    for slot in 0..40960_u32 {
        positions.extend([[-1., -1., 0.], [1., -1., 0.], [1., 1., 0.], [-1., 1., 0.]]);
        slots.extend([slot; 4]);
        indices.extend([0, 1, 2, 0, 2, 3].map(|i| slot * 4 + i));
    }
    let mut mesh = Mesh::new(
        PrimitiveTopology::TriangleList,
        RenderAssetUsages::default(),
    );
    mesh.insert_attribute(Mesh::ATTRIBUTE_POSITION, positions);
    mesh.insert_attribute(SLOT, slots);
    mesh.insert_indices(Indices::U32(indices));
    let mut texture = |side, mips, data: &Vec<u8>| {
        let mut image = Image::new_uninit(
            Extent3d {
                width: side,
                height: side,
                depth_or_array_layers: 1,
            },
            TextureDimension::D2,
            TextureFormat::Rgba8Unorm,
            RenderAssetUsages::default(),
        );
        image.data = Some(data.clone());
        image.texture_descriptor.mip_level_count = mips;
        image.sampler = ImageSampler::Descriptor(ImageSamplerDescriptor {
            address_mode_u: ImageAddressMode::Repeat,
            address_mode_v: ImageAddressMode::Repeat,
            mag_filter: ImageFilterMode::Linear,
            min_filter: ImageFilterMode::Linear,
            mipmap_filter: ImageFilterMode::Linear,
            ..default()
        });
        images.add(image)
    };
    let material = materials.add(SprayMaterial {
        clouds: clouds.as_ref().map_or([Vec4::ZERO; 5], |v| v.params),
        cloud_shadow: clouds.as_ref().map(|v| v.shadow.clone()),
        frame: inputs.frame,
        material: inputs.material,
        pos: spray.buffers[1].clone(),
        vel: spray.buffers[2].clone(),
        info: spray.buffers[3].clone(),
        crest: spray.buffers[0].clone(),
        depth: depth.0.clone(),
        puff: texture(64, 7, &inputs.puff),
        dots: texture(128, 8, &inputs.dots),
    });
    commands.spawn((
        Mesh3d(meshes.add(mesh)),
        MeshMaterial3d(material),
        Transform::default(),
        NoFrustumCulling,
        NotShadowCaster,
    ));
}
