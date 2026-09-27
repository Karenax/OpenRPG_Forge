//! Original optical shader with native FFT, atmosphere and scene-copy inputs.
use bevy::{
    asset::RenderAssetUsages,
    image::{ImageAddressMode, ImageFilterMode, ImageSampler, ImageSamplerDescriptor},
    mesh::MeshVertexBufferLayoutRef,
    pbr::{MaterialPipeline, MaterialPipelineKey},
    prelude::*,
    render::render_resource::{
        AsBindGroup, Extent3d, RenderPipelineDescriptor, TextureDimension, TextureFormat,
    },
    shader::ShaderRef,
};
use std::{collections::BTreeMap, error::Error};

#[derive(Resource)]
pub struct Inputs {
    frame: [UVec4; 45],
    material: [UVec4; 5],
    sky: [UVec4; 2],
    whales: [UVec4; 6],
    foam: Vec<u8>,
    shore_field: Vec<u8>,
    shore_size: u32,
    shore_params: [UVec4; 4],
    sim_params: [UVec4; 3],
    lace: Vec<u8>,
    with_sim: bool,
    detail_noise: Vec<u8>,
    detail_params: [UVec4; 2],
}
pub fn shore_field(app: &App) -> Vec<u8> {
    app.world().resource::<Inputs>().shore_field.clone()
}
fn detail_inputs(
    files: &BTreeMap<String, Vec<u8>>,
) -> Result<(Vec<u8>, [UVec4; 2]), Box<dyn Error>> {
    let Some(noise) = files.get("sea-detail-noise.rgba16.bin") else {
        if files.contains_key("uniform-seaDetailParams.bin") {
            return Err("missing sea detail noise".into());
        }
        return Ok((vec![0; 8], [UVec4::ZERO; 2]));
    };
    if noise.len() != 256 * 256 * 8
        || noise
            .as_chunks::<2>()
            .0
            .iter()
            .any(|v| u16::from_le_bytes(*v) & 0x7c00 == 0x7c00)
    {
        return Err("invalid sea detail noise".into());
    }
    let params = files
        .get("uniform-seaDetailParams.bin")
        .ok_or("missing sea detail parameters")?;
    if params
        .as_chunks::<4>()
        .0
        .iter()
        .any(|v| !f32::from_le_bytes(*v).is_finite())
    {
        return Err("nonfinite sea detail parameters".into());
    }
    Ok((noise.clone(), words(params)?))
}
fn words<const N: usize>(bytes: &[u8]) -> Result<[UVec4; N], Box<dyn Error>> {
    if bytes.len() != N * 16 {
        return Err("water uniform size".into());
    }
    Ok(std::array::from_fn(|i| {
        UVec4::from_array(std::array::from_fn(|j| {
            u32::from_le_bytes(
                bytes[i * 16 + j * 4..i * 16 + j * 4 + 4]
                    .try_into()
                    .expect("checked uniform"),
            )
        }))
    }))
}
pub fn install(app: &mut App, files: &BTreeMap<String, Vec<u8>>) -> Result<(), Box<dyn Error>> {
    let get = |name: &str| files.get(name).ok_or("missing water input");
    let (detail_noise, detail_params) = detail_inputs(files)?;
    let mut foam = Vec::new();
    for mip in 0..11 {
        let bytes = get(&format!("foam-m{mip}.rgba16.bin"))?;
        if bytes.len() != (1024_usize >> mip).pow(2) * 8
            || bytes
                .as_chunks::<2>()
                .0
                .iter()
                .any(|v| u16::from_le_bytes(*v) & 0x7c00 == 0x7c00)
        {
            return Err("invalid foam mip".into());
        }
        foam.extend_from_slice(bytes);
    }
    let (shore_field, shore_size, shore_params) = if let Some(bytes) =
        files.get("shore-field.rgba32.bin")
    {
        let parameters: [UVec4; 4] = words(get("uniform-shoreP.bin")?)?;
        if parameters[3].y != 0 {
            return Err(
                "filtered shore direction texture is not part of the pinned source route".into(),
            );
        }
        if bytes.len() != 512 * 512 * 16
            || bytes
                .as_chunks::<4>()
                .0
                .iter()
                .any(|v| !f32::from_le_bytes(*v).is_finite())
        {
            return Err("invalid shore field".into());
        }
        let document: forge_project::TerrainDocument =
            serde_json::from_slice(get("terrain.json")?)?;
        let field = if document.height_deltas.is_empty()
            && document.height_sampling.base_elevation_m == 0.0
        {
            bytes.clone()
        } else {
            let plan = forge_world::sampled_terrain::SampledTerrainPlan::from_document(
                &document,
                get("heights.bin")?,
            )?;
            let data = super::shore_field::bake(|x, z| plan.height_at(x, z))?;
            let derived: Vec<u8> = data.into_iter().flat_map(f32::to_le_bytes).collect();
            eprintln!(
                "authored shore field: sha256={}",
                forge_core::bytes_sha256(&derived)
            );
            derived
        };
        (field, 512, parameters)
    } else {
        (vec![0; 16], 1, [UVec4::ZERO; 4])
    };
    let frame: [UVec4; 45] = words(get("water-frame.bin")?)?;
    if frame[43].w != 0 {
        return Err("water Frame.pad0 must be zero for native rounding".into());
    }
    let with_sim = files.contains_key("uniform-shoreSimP.bin");
    let mut lace = Vec::new();
    if with_sim {
        for mip in 0..10 {
            let bytes = get(&format!("lace-m{mip}.rgba8.bin"))?;
            if bytes.len() != (512_usize >> mip).pow(2) * 4 {
                return Err("invalid shore lace mip".into());
            }
            lace.extend_from_slice(bytes);
        }
    }
    app.insert_resource(Inputs {
        detail_noise,
        detail_params,
        sim_params: if with_sim {
            words(get("uniform-shoreSimP.bin")?)?
        } else {
            [UVec4::ZERO; 3]
        },
        with_sim,
        lace,
        frame,
        material: words(get("uniform-mat.bin")?)?,
        sky: words(get("uniform-skyParams.bin")?)?,
        whales: words(get("uniform-whaleMarks.bin")?)?,
        foam,
        shore_field,
        shore_size,
        shore_params,
    });
    Ok(())
}

#[derive(Asset, TypePath, AsBindGroup, Debug, Clone)]
pub struct WaterMaterial {
    #[uniform(0)]
    morph: [Vec4; 12],
    #[uniform(1)]
    ocean: [UVec4; 14],
    #[uniform(2)]
    surface: [Vec4; 2],
    #[texture(3, filterable = false)]
    height: Handle<Image>,
    #[texture(50, filterable = false)]
    corrections: Handle<Image>,
    #[texture(4, dimension = "2d_array")]
    #[sampler(5)]
    displacement: Handle<Image>,
    #[texture(6, sample_type = "depth")]
    scene_depth: Handle<Image>,
    #[uniform(7)]
    frame: [UVec4; 45],
    #[texture(8, dimension = "2d_array")]
    derivatives: Handle<Image>,
    #[texture(10)]
    #[sampler(9)]
    foam: Handle<Image>,
    #[texture(11)]
    #[sampler(12)]
    terrain_normal: Handle<Image>,
    #[texture(13)]
    hill: Handle<Image>,
    #[texture(15, dimension = "2d_array", sample_type = "depth")]
    #[sampler(16, sampler_type = "comparison")]
    object: Handle<Image>,
    #[uniform(17)]
    object_settings: super::object_shadow::Settings,
    #[uniform(18)]
    atmosphere: [Vec4; 4],
    #[uniform(19)]
    sky: [UVec4; 2],
    #[texture(20)]
    transmittance: Handle<Image>,
    #[texture(21)]
    sky_lut: Handle<Image>,
    #[uniform(22)]
    material: [UVec4; 5],
    #[uniform(23)]
    whales: [UVec4; 6],
    #[texture(24, filterable = false)]
    shore_field: Handle<Image>,
    #[uniform(25)]
    shore_params: [UVec4; 4],
    #[texture(26, filterable = false)]
    shore_direction: Handle<Image>,
    #[texture(27)]
    sim_state: Handle<Image>,
    #[uniform(28)]
    sim_params: [UVec4; 3],
    #[texture(29)]
    sim_lace: Handle<Image>,
    #[texture(30)]
    detail_noise: Handle<Image>,
    #[uniform(31)]
    detail_params: [UVec4; 2],
    #[uniform(32)]
    clouds: [Vec4; 5],
    #[texture(33)]
    cloud_panorama: Option<Handle<Image>>,
    #[texture(34, filterable = false)]
    cloud_shadow: Option<Handle<Image>>,
}
impl Material for WaterMaterial {
    fn vertex_shader() -> ShaderRef {
        super::ocean_geometry::OPTICS_SHADER.into()
    }
    fn fragment_shader() -> ShaderRef {
        super::ocean_geometry::OPTICS_SHADER.into()
    }
    fn enable_prepass() -> bool {
        false
    }
    fn enable_shadows() -> bool {
        false
    }
    fn reads_view_transmission_texture(&self) -> bool {
        true
    }
    fn specialize(
        pipeline: &MaterialPipeline,
        descriptor: &mut RenderPipelineDescriptor,
        layout: &MeshVertexBufferLayoutRef,
        key: MaterialPipelineKey<Self>,
    ) -> Result<(), bevy::material::specialize::SpecializedMeshPipelineError> {
        // Same vertex attributes and double-sided depth behavior as the verified grid.
        <super::ocean_geometry::OceanGeometryMaterial as Material>::specialize(
            pipeline,
            descriptor,
            layout,
            MaterialPipelineKey {
                mesh_key: key.mesh_key,
                bind_group_data: (),
            },
        )
    }
}

#[allow(clippy::too_many_arguments)]
#[allow(clippy::too_many_lines)] // Allocate optical inputs and connect the shared shore image to both materials.
pub fn setup(
    mut commands: Commands,
    inputs: Option<Res<Inputs>>,
    ocean: Option<Res<super::ocean::Targets>>,
    atmosphere: Option<Res<super::atmosphere::Targets>>,
    clouds: Option<Res<super::sky::CloudInputs>>,
    depth: Option<Res<super::water_scene::SceneDepth>>,
    hill: Res<super::hill_shadow::Images>,
    object: Res<super::object_shadow::Target>,
    mut images: ResMut<Assets<Image>>,
    geometries: Res<Assets<super::ocean_geometry::OceanGeometryMaterial>>,
    entities: Query<(
        Entity,
        &MeshMaterial3d<super::ocean_geometry::OceanGeometryMaterial>,
    )>,
    mut materials: ResMut<Assets<WaterMaterial>>,
    shore: Option<Res<super::shore_sim::Targets>>,
    mut terrain_materials: ResMut<Assets<super::terrain_surface::TerrainMaterial>>,
    mut terrain_lod_materials: ResMut<Assets<super::terrain_lod::LodMaterial>>,
    normals: Res<super::terrain_normals::Shared>,
) {
    let Some(inputs) = inputs else {
        return;
    };
    let ocean = ocean.expect("water ocean graph");
    let atmosphere = atmosphere.expect("water atmosphere graph");
    let depth = depth.expect("water scene depth");
    let detail_size = if inputs.detail_noise.len() == 8 {
        1
    } else {
        256
    };
    let detail_noise = images.add(Image::new(
        Extent3d {
            width: detail_size,
            height: detail_size,
            depth_or_array_layers: 1,
        },
        TextureDimension::D2,
        inputs.detail_noise.clone(),
        TextureFormat::Rgba16Float,
        RenderAssetUsages::default(),
    ));
    let mut foam = Image::new_uninit(
        Extent3d {
            width: 1024,
            height: 1024,
            depth_or_array_layers: 1,
        },
        TextureDimension::D2,
        TextureFormat::Rgba16Float,
        RenderAssetUsages::default(),
    );
    foam.data = Some(inputs.foam.clone());
    foam.texture_descriptor.mip_level_count = 11;
    foam.sampler = ImageSampler::Descriptor(ImageSamplerDescriptor {
        address_mode_u: ImageAddressMode::Repeat,
        address_mode_v: ImageAddressMode::Repeat,
        mag_filter: ImageFilterMode::Linear,
        min_filter: ImageFilterMode::Linear,
        mipmap_filter: ImageFilterMode::Linear,
        anisotropy_clamp: 8,
        ..default()
    });
    let foam = images.add(foam);
    let shore_field = images.add(Image::new(
        Extent3d {
            width: inputs.shore_size,
            height: inputs.shore_size,
            depth_or_array_layers: 1,
        },
        TextureDimension::D2,
        inputs.shore_field.clone(),
        TextureFormat::Rgba32Float,
        RenderAssetUsages::default(),
    ));
    let direction_bytes = [1.0_f32, 0.0, 0.0, 1.0]
        .into_iter()
        .flat_map(f32::to_le_bytes)
        .collect();
    let shore_direction = images.add(Image::new(
        Extent3d {
            width: 1,
            height: 1,
            depth_or_array_layers: 1,
        },
        TextureDimension::D2,
        direction_bytes,
        TextureFormat::Rgba32Float,
        RenderAssetUsages::default(),
    ));
    let normal = normals.image.clone();
    let sim_state = if inputs.with_sim {
        shore.as_ref().expect("shore graph required").state.clone()
    } else {
        images.add(Image::default())
    };
    let sim_lace = if inputs.with_sim {
        images.add(super::mipped_image(
            &super::LeafTexture {
                width: 512,
                height: 512,
                mips: 10,
            },
            &inputs.lace,
        ))
    } else {
        images.add(Image::default())
    };
    if inputs.with_sim {
        // CDLOD clones its base into ExtendedMaterial during startup. Updating
        // only Assets<TerrainMaterial> leaves the rendered LOD copy on dummies.
        for material in terrain_materials
            .iter_mut()
            .map(|(_, material)| material)
            .chain(
                terrain_lod_materials
                    .iter_mut()
                    .map(|(_, material)| &mut material.base),
            )
        {
            material.shore_state = sim_state.clone();
            material.shore_lace = sim_lace.clone();
            material.shore_params = inputs.sim_params;
        }
    }
    for (entity, handle) in &entities {
        let geometry = geometries.get(&handle.0).expect("water geometry");
        let material = materials.add(WaterMaterial {
            clouds: clouds.as_ref().map_or([Vec4::ZERO; 5], |v| v.params),
            cloud_panorama: clouds.as_ref().map(|v| v.panorama.clone()),
            cloud_shadow: clouds.as_ref().map(|v| v.shadow.clone()),
            detail_noise: detail_noise.clone(),
            detail_params: inputs.detail_params,
            morph: geometry.morph,
            ocean: ocean.parameters,
            surface: geometry.surface,
            height: geometry.height.clone(),
            corrections: geometry.corrections.clone(),
            displacement: ocean.images[0].clone(),
            derivatives: ocean.images[1].clone(),
            scene_depth: depth.0.clone(),
            frame: inputs.frame,
            foam: foam.clone(),
            terrain_normal: normal.clone(),
            hill: hill.output.clone(),
            object: object.image.clone(),
            object_settings: object.settings.clone(),
            atmosphere: atmosphere.params,
            sky: inputs.sky,
            transmittance: atmosphere.images[0].clone(),
            sky_lut: atmosphere.images[2].clone(),
            material: inputs.material,
            whales: inputs.whales,
            shore_field: shore_field.clone(),
            shore_params: inputs.shore_params,
            shore_direction: shore_direction.clone(),
            sim_state: sim_state.clone(),
            sim_params: inputs.sim_params,
            sim_lace: sim_lace.clone(),
        });
        commands
            .entity(entity)
            .remove::<MeshMaterial3d<super::ocean_geometry::OceanGeometryMaterial>>()
            .insert(MeshMaterial3d(material));
    }
}
