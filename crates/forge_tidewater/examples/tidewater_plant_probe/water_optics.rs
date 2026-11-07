//! Original optical shader with native FFT, atmosphere and scene-copy inputs.
use bevy::{
    asset::RenderAssetUsages,
    image::{ImageAddressMode, ImageFilterMode, ImageSampler, ImageSamplerDescriptor},
    prelude::*,
    render::render_resource::{Extent3d, TextureDimension, TextureFormat},
};
use std::{collections::BTreeMap, error::Error};

/// Compute passes read the uploaded optical frame, whereas the visible water
/// shader replaces its private camera with Bevy's view. Keep both in agreement
/// every render frame, including when CDLOD selection has not moved a full metre.
// Bevy's camera query intentionally spells out the capture-camera exclusion;
// retaining it here prevents a proof camera from becoming the source camera.
#[allow(clippy::type_complexity)]
pub fn update_camera(
    cameras: Query<
        (&Camera, &GlobalTransform),
        (
            With<Camera3d>,
            Without<forge_engine::source_refraction::CaptureCamera>,
        ),
    >,
    mut materials: ResMut<Assets<WaterMaterial>>,
) {
    let mut active = cameras.iter().filter(|(camera, _)| camera.is_active);
    let Some((camera, pose)) = active.next() else {
        return;
    };
    if active.next().is_some() {
        return;
    }
    let camera_frame =
        forge_engine::source_water_frame::CameraFrame::new(camera.clip_from_view(), *pose);
    for (_, material) in materials.iter_mut() {
        camera_frame.write(&mut material.frame);
    }
}

/// Publish the resident atmosphere's completed irradiance to the native optical
/// frame using the presentation menu's fixed solar clock. A stale acquired sun
/// is refused by the shared readback contract.
/// This is not a source-history or producing-frame-tag synchronization proof.
pub fn update_lighting(world: &mut World) {
    use forge_engine::source_atmosphere as tidewater_atmosphere;
    use forge_engine::source_atmosphere::readback;
    use forge_engine::source_sky::frame_lighting::FrameLighting;
    use forge_engine::source_sky::solar;
    let Some(targets) = world
        .get_resource::<tidewater_atmosphere::Targets>()
        .cloned()
    else {
        return;
    };
    let Some(transmittance) = targets.images.first() else {
        return;
    };
    if world
        .get_resource::<Assets<WaterMaterial>>()
        .is_none_or(|materials| {
            !materials
                .iter()
                .any(|(_, material)| &material.transmittance == transmittance)
        })
    {
        return;
    }
    let frame = crate::walk::escape_menu::solar_frame(world).unwrap_or_else(|| {
        *solar::Clock::new(default())
            .expect("validated source solar defaults")
            .frame()
    });
    let Some(completed) = readback::latest(world, frame) else {
        // The presentation controller owns readback scheduling while its solar
        // clock is installed. Static probes retain their original one-shot path.
        if crate::walk::escape_menu::solar_frame(world).is_none()
            && targets.ready.load(std::sync::atomic::Ordering::Acquire)
            && !readback::is_pending(world)
            && let Err(error) = readback::request(world, frame)
        {
            warn!("Native optical lighting readback refused: {error}");
        }
        return;
    };
    let completed = match completed {
        Ok(completed) => completed,
        Err(error) => {
            warn!("Native optical lighting completion refused: {error}");
            // Clear a retired/error snapshot; never publish it to a new scene.
            readback::cancel(world);
            return;
        }
    };
    let lighting = match FrameLighting::new(frame, &completed.lighting) {
        Ok(lighting) => lighting,
        Err(error) => {
            warn!("Native optical lighting payload refused: {error}");
            return;
        }
    };
    let mut materials = world.resource_mut::<Assets<WaterMaterial>>();
    for (_, material) in materials.iter_mut() {
        if &material.transmittance != transmittance {
            continue;
        }
        let mut words = material.frame.map(|value| value.to_array());
        lighting.write_words(&mut words);
        let next = words.map(UVec4::from_array);
        if material.frame != next || material.atmosphere != targets.params {
            material.frame = next;
            material.atmosphere = targets.params;
        }
    }
}

/// Keep the derived view bound to the adapter's main camera, including the
/// static proof camera which does not carry `ForgePlayerCamera`.
pub fn select_refraction_camera(world: &mut World) {
    let mut cameras = world.query_filtered::<(Entity, &Camera), (
        With<Camera3d>,
        Without<forge_engine::source_refraction::CaptureCamera>,
    )>();
    let mut active = cameras.iter(world).filter(|(_, camera)| camera.is_active);
    let Some((entity, _)) = active.next() else {
        return;
    };
    if active.next().is_some() {
        return;
    }
    forge_engine::source_refraction_capture::select_camera(world, entity);
    forge_engine::source_hull_mask::select_camera(world, Some(entity));
}

/// Publish the result of the shared synchronization to the native material.
pub fn update_refraction(
    observation: Res<forge_engine::source_refraction_capture::Observation>,
    mut materials: ResMut<Assets<WaterMaterial>>,
) {
    let ready = f32::from(observation.ready);
    let changed: Vec<_> = materials
        .iter()
        .filter(|(_, material)| material.refraction_ready.x.to_bits() != ready.to_bits())
        .map(|(id, _)| id)
        .collect();
    for id in changed {
        if let Some(mut material) = materials.get_mut(id) {
            material.refraction_ready.x = ready;
        }
    }
}

#[derive(Resource)]
pub struct Inputs {
    frame: [UVec4; 45],
    constants: forge_engine::source_water_optics::OpticalConstants,
    foam: Vec<u8>,
    shore_field: Vec<u8>,
    shore_size: u32,
    lace: Vec<u8>,
    with_sim: bool,
    detail_noise: Vec<u8>,
}
pub fn shore_field(app: &App) -> Vec<u8> {
    app.world().resource::<Inputs>().shore_field.clone()
}
pub fn underwater_column(app: &App) -> (f32, f32) {
    let inputs = app.world().resource::<Inputs>();
    let amplitude = f32::from_bits(inputs.constants.shore[0].y);
    (
        f32::from_bits(inputs.frame[36].w),
        if amplitude > 0.0 {
            amplitude * 1.5 + 1.2
        } else {
            3.0
        },
    )
}

pub fn underwater_optics(
    app: &App,
) -> Result<forge_engine::source_underwater::SourceUnderwaterOptics, String> {
    let frame = &app.world().resource::<Inputs>().frame;
    let vector = |index: usize| {
        let words = frame[index].to_array();
        Vec3::new(
            f32::from_bits(words[0]),
            f32::from_bits(words[1]),
            f32::from_bits(words[2]),
        )
    };
    // Frame.js layout: sunDir 37, sunColor 38, skyIrradiance 39,
    // waterAbsorption 41 and waterScattering 42.
    forge_engine::source_underwater::SourceUnderwaterOptics::new(
        vector(41),
        vector(42),
        vector(37),
        vector(38),
        vector(39),
    )
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
        constants: forge_engine::source_water_optics::OpticalConstants {
            sky: words(get("uniform-skyParams.bin")?)?,
            material: words(get("uniform-mat.bin")?)?,
            whales: words(get("uniform-whaleMarks.bin")?)?,
            shore: shore_params,
            simulation: if with_sim {
                words(get("uniform-shoreSimP.bin")?)?
            } else {
                [UVec4::ZERO; 3]
            },
            detail: detail_params,
        },
        with_sim,
        lace,
        frame,
        foam,
        shore_field,
        shore_size,
    });
    Ok(())
}

pub use forge_engine::source_water_optics::OpticalMaterial as WaterMaterial;

type OpticalSceneTargets<'w> = (
    Option<Res<'w, super::water_scene::SceneDepth>>,
    Option<Res<'w, forge_engine::source_refraction_capture::Targets>>,
    Option<Res<'w, forge_engine::source_hull_mask::Targets>>,
);

#[allow(clippy::too_many_arguments)]
#[allow(clippy::too_many_lines)] // Allocate optical inputs and connect the shared shore image to both materials.
pub fn setup(
    mut commands: Commands,
    inputs: Option<Res<Inputs>>,
    ocean: Option<Res<super::ocean::Targets>>,
    atmosphere: Option<Res<super::atmosphere::Targets>>,
    clouds: Option<Res<super::sky::CloudInputs>>,
    depth_and_refraction: OpticalSceneTargets,
    hill: Res<super::hill_shadow::Images>,
    object: Res<super::object_shadow::Target>,
    mut images: ResMut<Assets<Image>>,
    geometries: Res<Assets<forge_engine::source_ocean_geometry::DiagnosticMaterial>>,
    entities: Query<(
        Entity,
        &MeshMaterial3d<forge_engine::source_ocean_geometry::DiagnosticMaterial>,
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
    let (depth, refraction, hull_targets) = depth_and_refraction;
    let hull_targets = hull_targets.expect("water hull renderer registered");
    let depth = depth.expect("water scene depth");
    // Optical inputs are installed only alongside the shared capture targets.
    let refraction = refraction.expect("water refraction registered with optical inputs");
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
            material.shore_params = inputs.constants.simulation;
        }
    }
    for (entity, handle) in &entities {
        let geometry = geometries.get(&handle.0).expect("water geometry");
        let material = materials.add(WaterMaterial {
            constants: inputs
                .constants
                .gpu_words(clouds.as_ref().map_or([Vec4::ZERO; 5], |v| v.params)),
            cloud_panorama: clouds.as_ref().map(|v| v.panorama.clone()),
            cloud_shadow: clouds.as_ref().map(|v| v.shadow.clone()),
            detail_noise: detail_noise.clone(),
            morph: geometry.morph,
            ocean: ocean.parameters,
            surface: geometry.surface,
            height: geometry.height.clone(),
            corrections: geometry.corrections.clone(),
            displacement: ocean.images[0].clone(),
            derivatives: ocean.images[1].clone(),
            scene_depth: depth.0.clone(),
            refraction_color: refraction.color.clone(),
            refraction_depth: refraction.depth.clone(),
            refraction_ready: Vec4::ZERO,
            hull_distance: hull_targets.distance.clone(),
            hull_readiness: hull_targets.readiness.clone(),
            frame: inputs.frame,
            foam: foam.clone(),
            terrain_normal: normal.clone(),
            hill: hill.output.clone(),
            object: object.image.clone(),
            object_settings: object.settings.clone(),
            live_shadows: forge_engine::source_shadows::receiver::DEFAULT_BUFFER.clone(),
            atmosphere: atmosphere.params,
            transmittance: atmosphere.images[0].clone(),
            sky_lut: atmosphere.images[2].clone(),
            shore_field: shore_field.clone(),
            shore_direction: shore_direction.clone(),
            sim_state: sim_state.clone(),
            sim_lace: sim_lace.clone(),
        });
        commands.insert_resource(
            forge_engine::source_underwater::SourceUnderwaterSurfaceMaterial(
                material.id().untyped(),
            ),
        );
        commands.insert_resource(forge_engine::source_underwater_lighting::Surface(
            material.id().untyped(),
        ));
        commands
            .entity(entity)
            .remove::<MeshMaterial3d<forge_engine::source_ocean_geometry::DiagnosticMaterial>>()
            .insert((
                MeshMaterial3d(material),
                forge_engine::source_surface::capture::CaptureConsumer {
                    // This captured wave inventory remains inside the expanded
                    // rest bounds; all axes include ample FFT/shore displacement.
                    padding: Vec3::splat(32.0),
                },
            ));
    }
}
