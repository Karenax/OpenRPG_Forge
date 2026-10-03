// SPDX-License-Identifier: MIT
// Adapted from Tidewater WaterMaterial.js and pinned native Forge probe.
// Tidewater copyright 2026 DRG Software Solutions LLC; revision 1438b1ab.
//! Source optical material for the resident FFT ocean.
//! Shore simulation remains a separate GPU stage; its state is explicit here.
use crate::{
    tidewater_ocean, tidewater_ocean_depth,
    tidewater_ocean_geometry::{GeometryMaterial, ResidentGeometry},
    tidewater_scene_environment::ResidentEnvironment,
    tidewater_shore_field,
    tidewater_terrain::{lod::LodMaterial, scene::ResidentTerrain},
    tidewater_terrain_edits::Corrections,
};
use bevy::{
    asset::{RenderAssetUsages, uuid_handle},
    image::{ImageAddressMode, ImageFilterMode, ImageSampler, ImageSamplerDescriptor},
    mesh::MeshVertexBufferLayoutRef,
    pbr::{MaterialPipeline, MaterialPipelineKey},
    prelude::*,
    render::render_resource::{
        AsBindGroup, Extent3d, RenderPipelineDescriptor, TextureDimension, TextureFormat,
    },
    shader::ShaderRef,
};
use forge_assets::tidewater_water_surface::WaterSurfacePackage;
use forge_project::TerrainDocument;
use forge_world::sampled_terrain::SampledTerrainPlan;

const SHADER: Handle<Shader> = uuid_handle!("177642ae-53ed-4f92-81d1-a0ef46a35413");
const UNDERWATER_SHADER: Handle<Shader> = uuid_handle!("e8cc8d3a-d2c7-4df6-ac13-5c3c449d0ac8");

#[derive(Resource)]
struct Registered;

#[derive(Resource)]
pub struct ResidentOptics {
    pub material: Handle<OpticalMaterial>,
    pub entity: Entity,
    pub shore_derived: bool,
    pub live_step: u32,
}

#[derive(Asset, TypePath, AsBindGroup, Debug, Clone)]
pub struct OpticalMaterial {
    #[uniform(0, visibility(vertex, fragment, compute))]
    morph: [Vec4; 12],
    #[uniform(1, visibility(vertex, fragment, compute))]
    ocean: [UVec4; 14],
    #[uniform(2, visibility(vertex, fragment, compute))]
    surface: [Vec4; 2],
    #[texture(3, filterable = false, visibility(vertex, fragment, compute))]
    height: Handle<Image>,
    #[texture(50, filterable = false, visibility(vertex, fragment, compute))]
    corrections: Handle<Image>,
    #[texture(4, dimension = "2d_array", visibility(vertex, fragment, compute))]
    #[sampler(5, visibility(vertex, fragment, compute))]
    displacement: Handle<Image>,
    #[texture(6, sample_type = "depth", visibility(vertex, fragment, compute))]
    scene_depth: Handle<Image>,
    #[uniform(7, visibility(vertex, fragment, compute))]
    frame: [UVec4; 45],
    #[texture(8, dimension = "2d_array", visibility(vertex, fragment, compute))]
    derivatives: Handle<Image>,
    #[texture(10, visibility(vertex, fragment, compute))]
    #[sampler(9, visibility(vertex, fragment, compute))]
    foam: Handle<Image>,
    #[texture(11, visibility(vertex, fragment, compute))]
    #[sampler(12, visibility(vertex, fragment, compute))]
    terrain_normal: Handle<Image>,
    #[texture(13, visibility(vertex, fragment, compute))]
    hill: Handle<Image>,
    #[texture(
        15,
        dimension = "2d_array",
        sample_type = "depth",
        visibility(vertex, fragment, compute)
    )]
    #[sampler(16, sampler_type = "comparison", visibility(vertex, fragment, compute))]
    object: Handle<Image>,
    #[uniform(17, visibility(vertex, fragment, compute))]
    object_settings: crate::tidewater_material::ObjectShadowSettings,
    #[storage(44, read_only, visibility(vertex, fragment, compute))]
    live_shadows: Handle<bevy::render::storage::ShaderBuffer>,
    #[uniform(18, visibility(vertex, fragment, compute))]
    atmosphere: [Vec4; 4],
    #[uniform(19, visibility(vertex, fragment, compute))]
    constants: [UVec4; 27],
    #[texture(20, visibility(vertex, fragment, compute))]
    transmittance: Handle<Image>,
    #[texture(21, visibility(vertex, fragment, compute))]
    sky_lut: Handle<Image>,
    #[texture(24, filterable = false, visibility(vertex, fragment, compute))]
    shore_field: Handle<Image>,
    #[texture(26, filterable = false, visibility(vertex, fragment, compute))]
    shore_direction: Handle<Image>,
    #[texture(27, visibility(vertex, fragment, compute))]
    sim_state: Handle<Image>,
    #[texture(29, visibility(vertex, fragment, compute))]
    sim_lace: Handle<Image>,
    #[texture(30, visibility(vertex, fragment, compute))]
    detail_noise: Handle<Image>,
    #[texture(33, visibility(vertex, fragment, compute))]
    cloud_panorama: Option<Handle<Image>>,
    #[texture(34, filterable = false, visibility(vertex, fragment, compute))]
    cloud_shadow: Option<Handle<Image>>,
    #[texture(51, visibility(fragment))]
    refraction_color: Handle<Image>,
    #[texture(52, sample_type = "depth", visibility(fragment))]
    refraction_depth: Handle<Image>,
    #[uniform(53, visibility(fragment))]
    pub(crate) refraction_ready: Vec4,
    #[texture(54, filterable = false, visibility(fragment))]
    hull_distance: Handle<Image>,
    #[storage(55, read_only, visibility(fragment))]
    hull_readiness: Handle<bevy::render::storage::ShaderBuffer>,
}

impl Material for OpticalMaterial {
    fn reads_view_transmission_texture(&self) -> bool {
        true
    }
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
        false
    }
    fn specialize(
        pipeline: &MaterialPipeline,
        descriptor: &mut RenderPipelineDescriptor,
        layout: &MeshVertexBufferLayoutRef,
        key: MaterialPipelineKey<Self>,
    ) -> Result<(), bevy::material::specialize::SpecializedMeshPipelineError> {
        <GeometryMaterial as Material>::specialize(
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

pub fn register(app: &mut App) -> Result<(), String> {
    if app.world().contains_resource::<Registered>() {
        return Ok(());
    }
    tidewater_ocean_depth::register(app)?;
    crate::tidewater_refraction::register(app)?;
    crate::tidewater_hull_mask::register(app)?;
    app.add_plugins(MaterialPlugin::<OpticalMaterial>::default());
    crate::tidewater_surface_queries::register(app)?;
    app.insert_resource(Registered);
    Ok(())
}

/// Connect the source immersion pass to the same optical material and GPU packet.
pub fn register_underwater(app: &mut App, package: &WaterSurfacePackage) -> Result<(), String> {
    use crate::tidewater_underwater::{TidewaterUnderwaterOptics, TidewaterUnderwaterPlugin};
    if app
        .world()
        .contains_resource::<crate::tidewater_underwater::TidewaterUnderwaterControl>()
    {
        return Ok(());
    }
    let frame = words::<45>(
        package
            .file("water-frame.bin")
            .ok_or("Water frame absent")?,
    )?;
    let vector = |index: usize| {
        let words = frame[index].to_array();
        Vec3::from_array([words[0], words[1], words[2]].map(f32::from_bits))
    };
    let optics =
        TidewaterUnderwaterOptics::new(vector(41), vector(42), vector(37), vector(38), vector(39))?;
    let targets = app
        .world()
        .get_resource::<crate::tidewater_surface_queries::Targets>()
        .ok_or("Immersion requires registered optical surface queries")?;
    let plugin =
        TidewaterUnderwaterPlugin::new(targets.queries.clone(), targets.packet.clone(), optics)
            .with_surface_shader(
                UNDERWATER_SHADER,
                OpticalMaterial::bind_group_layout_descriptor,
            );
    let ocean = app
        .world()
        .get_resource::<tidewater_ocean::Targets>()
        .ok_or("Immersion shafts require the resident FFT")?
        .clone();
    let maps = crate::tidewater_caustics::prepare_maps(
        app,
        ocean.images[1].clone(),
        ocean.sizes[3].x,
        ocean.sizes[2].x,
        optics.sun_direction.normalize(),
        ocean.completed_steps.clone(),
    )?;
    app.sub_app_mut(bevy::render::RenderApp).add_systems(
        bevy::render::renderer::RenderGraph,
        crate::tidewater_caustics::render_maps
            .after(tidewater_ocean::OceanComputeSet)
            .before(bevy::core_pipeline::schedule::camera_driver),
    );
    crate::tidewater_underwater_lighting::register(
        app,
        OpticalMaterial::bind_group_layout_descriptor,
    )?;
    let light_maps = app
        .world()
        .resource::<crate::tidewater_underwater_lighting::Targets>()
        .bindings
        .clone();
    let shore = words::<4>(
        package
            .file("uniform-shoreP.bin")
            .ok_or("Shore parameters absent")?,
    )?;
    let amplitude = f32::from_bits(shore[0].y);
    let reach = if amplitude > 0.0 {
        amplitude * 1.5 + 1.2
    } else {
        3.0
    };
    let params = crate::tidewater_caustics::CausticSampleParams::new(
        ocean.sizes[3].x,
        ocean.sizes[2].x,
        optics.sun_direction.normalize(),
        0.75,
    )?
    .with_underwater(
        ocean.sizes.map(|size| size.x),
        f32::from_bits(frame[36].w),
        reach,
        optics.absorption,
        optics.scattering,
    )?;
    app.insert_resource(crate::tidewater_caustics::CausticMaterialInputs {
        fine: maps.fine.clone(),
        broad: maps.broad.clone(),
        light_maps,
        params,
    });
    let plugin = plugin.with_caustic_maps(maps, ocean.sizes[3].x, ocean.sizes[2].x)?;
    app.add_plugins(plugin);
    Ok(())
}

pub struct PreparedOptics {
    shader: String,
    query_shader: String,
    underwater_shader: String,
    frame: [UVec4; 45],
    constants: [UVec4; 22],
    foam: Vec<u8>,
    shore_field: Vec<u8>,
    lace: Vec<u8>,
    detail_noise: Vec<u8>,
    shore_derived: bool,
}

impl PreparedOptics {
    /// Validate all source and authored inputs before replacing a resident material.
    pub fn new(
        package: &WaterSurfacePackage,
        document: &TerrainDocument,
        plan: &SampledTerrainPlan,
        derived_shore: Option<&[u8]>,
    ) -> Result<Self, String> {
        let file = |name: &str| {
            package
                .file(name)
                .ok_or_else(|| format!("Pinned water surface file missing: {name}"))
        };
        let source =
            std::str::from_utf8(file("water-optics.wgsl")?).map_err(|error| error.to_string())?;
        let corrections = Corrections::from_document(document)?;
        let shore_derived = !document.height_deltas.is_empty() || corrections.elevation != 0.0;
        let shader = if shore_derived {
            corrections.shader(source, "#{MATERIAL_BIND_GROUP}", 50)?
        } else {
            source.to_owned()
        };
        let shader = pack_optical_uniforms(
            &crate::tidewater_object_shadow::receiver::shader_source(&shader)?,
        )?;
        let shader = crate::tidewater_underwater::visible_water_mask_shader_source(&shader)?;
        let shader = crate::tidewater_refraction::water_shader_source(&shader)?;
        let shader = crate::tidewater_hull_mask::water_shader_source(&shader)?;
        let query_shader = crate::tidewater_surface_queries::shader_source(&shader)?;
        let underwater_shader = crate::tidewater_underwater::surface_query_shader_source(&shader)?;
        let shore_field = if shore_derived {
            if let Some(bytes) = derived_shore {
                if bytes.len() != 512 * 512 * 16 {
                    return Err("Derived shore field size mismatch".into());
                }
                bytes.to_vec()
            } else {
                tidewater_shore_field::bake(|x, z| plan.height_at(x, z))
                    .map_err(|error| error.to_string())?
                    .into_iter()
                    .flat_map(f32::to_le_bytes)
                    .collect()
            }
        } else {
            file("shore-field.rgba32.bin")?.to_vec()
        };
        let mut foam = Vec::new();
        for mip in 0..11 {
            foam.extend_from_slice(file(&format!("foam-m{mip}.rgba16.bin"))?);
        }
        let mut lace = Vec::new();
        for mip in 0..10 {
            lace.extend_from_slice(file(&format!("lace-m{mip}.rgba8.bin"))?);
        }
        Ok(Self {
            shader,
            query_shader,
            underwater_shader,
            frame: words(file("water-frame.bin")?)?,
            constants: {
                let mut values = [UVec4::ZERO; 22];
                values[0..2].copy_from_slice(&words::<2>(file("uniform-skyParams.bin")?)?);
                values[2..7].copy_from_slice(&words::<5>(file("uniform-mat.bin")?)?);
                values[7..13].copy_from_slice(&words::<6>(file("uniform-whaleMarks.bin")?)?);
                values[13..17].copy_from_slice(&words::<4>(file("uniform-shoreP.bin")?)?);
                values[17..20].copy_from_slice(&words::<3>(file("uniform-shoreSimP.bin")?)?);
                values[20..22].copy_from_slice(&words::<2>(file("uniform-seaDetailParams.bin")?)?);
                values
            },
            foam,
            shore_field,
            lace,
            detail_noise: file("sea-detail-noise.rgba16.bin")?.to_vec(),
            shore_derived,
        })
    }

    /// Replace the diagnostic material only after terrain, sky and depth are resident.
    #[allow(
        clippy::too_many_lines,
        reason = "Installs one verified GPU resource bundle in its explicit shader binding order."
    )]
    pub fn stage(self, world: &mut World) -> Result<(), String> {
        if !world.contains_resource::<Registered>() {
            return Err("Ocean optics renderer not registered".into());
        }
        let entity = world
            .get_resource::<ResidentGeometry>()
            .ok_or("Ocean geometry not staged")?
            .entity;
        let geometry_handle = world
            .get::<MeshMaterial3d<GeometryMaterial>>(entity)
            .ok_or("Ocean source geometry material missing")?
            .0
            .clone();
        let geometry = world
            .resource::<Assets<GeometryMaterial>>()
            .get(&geometry_handle)
            .ok_or("Ocean source geometry material unavailable")?
            .clone();
        let ocean = world.resource::<tidewater_ocean::Targets>().clone();
        let depth = world
            .resource::<tidewater_ocean_depth::SceneDepth>()
            .0
            .clone();
        let environment = world.resource::<ResidentEnvironment>();
        let atmosphere = environment.atmosphere.clone();
        let cloud_view = environment.cloud_view;
        let cloud_panorama = environment.clouds.images[9].clone();
        let cloud_shadow = environment.clouds.images[2].clone();
        let terrain = world.resource::<ResidentTerrain>().clone();
        let terrain_material = terrain
            .material
            .as_ref()
            .and_then(|handle| world.resource::<Assets<LodMaterial>>().get(handle))
            .ok_or("Ocean optics requires the rendered terrain material")?
            .base
            .clone();
        let shore_sim = world
            .get_resource::<crate::tidewater_shore_sim::Targets>()
            .cloned();
        crate::tidewater_object_shadow::receiver::install(world)?;
        if world.contains_resource::<crate::tidewater_underwater_lighting::Targets>() {
            crate::tidewater_underwater_lighting::install_shader(world, &self.shader)?;
        }
        world
            .resource_mut::<Assets<Shader>>()
            .insert(
                SHADER.id(),
                Shader::from_wgsl(self.shader, "tidewater-water-optics.wgsl"),
            )
            .map_err(|error| error.to_string())?;
        let (detail_noise, foam, shore_field, shore_direction, sim_state, sim_lace) = {
            let mut images = world.resource_mut::<Assets<Image>>();
            let detail_noise = images.add(Image::new(
                extent(256, 256),
                TextureDimension::D2,
                self.detail_noise,
                TextureFormat::Rgba16Float,
                RenderAssetUsages::default(),
            ));
            let mut foam = Image::new_uninit(
                extent(1024, 1024),
                TextureDimension::D2,
                TextureFormat::Rgba16Float,
                RenderAssetUsages::default(),
            );
            foam.data = Some(self.foam);
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
                extent(512, 512),
                TextureDimension::D2,
                self.shore_field,
                TextureFormat::Rgba32Float,
                RenderAssetUsages::default(),
            ));
            let shore_direction = images.add(Image::new(
                extent(1, 1),
                TextureDimension::D2,
                [1.0_f32, 0.0, 0.0, 1.0]
                    .into_iter()
                    .flat_map(f32::to_le_bytes)
                    .collect(),
                TextureFormat::Rgba32Float,
                RenderAssetUsages::default(),
            ));
            // The source shader always declares swash inputs. The live ShoreSim
            // producer is a separate stage; zero initial state keeps the binding
            // valid while optical integration is measured.
            let sim_state = if let Some(shore) = &shore_sim {
                shore.state.clone()
            } else {
                images.add(Image::new_fill(
                    extent(768, 768),
                    TextureDimension::D2,
                    &[0; 8],
                    TextureFormat::Rgba16Float,
                    RenderAssetUsages::default(),
                ))
            };
            let sim_lace = if let Some(shore) = &shore_sim {
                shore.lace.clone()
            } else {
                images.add(crate::tidewater_plants::textures::mipped(
                    crate::tidewater_plants::textures::Layout {
                        width: 512,
                        height: 512,
                        mips: 10,
                    },
                    &self.lace,
                )?)
            };
            (
                detail_noise,
                foam,
                shore_field,
                shore_direction,
                sim_state,
                sim_lace,
            )
        };
        let mut constants = [UVec4::ZERO; 27];
        constants[..22].copy_from_slice(&self.constants);
        for (index, value) in cloud_view.into_iter().enumerate() {
            constants[22 + index] = UVec4::from_array(value.to_array().map(f32::to_bits));
        }
        let refraction = world
            .resource::<crate::tidewater_refraction::Targets>()
            .clone();
        let hull_distance = world
            .resource::<crate::tidewater_hull_mask::Targets>()
            .distance
            .clone();
        let hull_readiness = world
            .resource::<crate::tidewater_hull_mask::Targets>()
            .readiness
            .clone();
        let material = world
            .resource_mut::<Assets<OpticalMaterial>>()
            .add(OpticalMaterial {
                morph: geometry.morph,
                ocean: ocean.parameters,
                surface: geometry.surface,
                height: geometry.height,
                corrections: geometry.corrections,
                displacement: ocean.images[0].clone(),
                scene_depth: depth,
                frame: self.frame,
                derivatives: ocean.images[1].clone(),
                foam,
                terrain_normal: terrain.normals.image,
                hill: terrain_material.hill_shadow,
                object: terrain_material.object_shadow,
                object_settings: terrain_material.object_settings,
                live_shadows: crate::tidewater_object_shadow::receiver::DEFAULT_BUFFER,
                atmosphere: atmosphere.params,
                constants,
                transmittance: atmosphere.images[0].clone(),
                sky_lut: atmosphere.images[2].clone(),
                shore_field,
                shore_direction,
                sim_state,
                sim_lace,
                detail_noise,
                cloud_panorama: Some(cloud_panorama),
                cloud_shadow: Some(cloud_shadow),
                refraction_color: refraction.color,
                refraction_depth: refraction.depth,
                refraction_ready: Vec4::ZERO,
                hull_distance,
                hull_readiness,
            });
        world
            .entity_mut(entity)
            .remove::<MeshMaterial3d<GeometryMaterial>>()
            .insert(MeshMaterial3d(material.clone()));
        world.insert_resource(ResidentOptics {
            material: material.clone(),
            entity,
            shore_derived: self.shore_derived,
            live_step: 0,
        });
        crate::tidewater_surface_queries::stage(world, self.query_shader, material.id().untyped())?;
        if world.contains_resource::<crate::tidewater_underwater_lighting::Targets>() {
            world.insert_resource(crate::tidewater_underwater_lighting::Surface(
                material.id().untyped(),
            ));
        }
        if world.contains_resource::<crate::tidewater_underwater::TidewaterUnderwaterControl>() {
            world
                .resource_mut::<Assets<Shader>>()
                .insert(
                    UNDERWATER_SHADER.id(),
                    Shader::from_wgsl(self.underwater_shader, "tidewater-underwater-surface.wgsl"),
                )
                .map_err(|error| error.to_string())?;
            world.insert_resource(
                crate::tidewater_underwater::TidewaterUnderwaterSurfaceMaterial(
                    material.id().untyped(),
                ),
            );
        }
        Ok(())
    }
}

pub fn update(world: &mut World, camera: Entity) -> Result<(), String> {
    crate::tidewater_refraction::select_camera(world, camera);
    crate::tidewater_hull_mask::select_camera(world, Some(camera));
    let size = tidewater_ocean_depth::resize(world, camera)?;
    let Some(resident) = world.get_resource::<ResidentOptics>() else {
        return Ok(());
    };
    let handle = resident.material.clone();
    let completed = tidewater_ocean::completed_frame(world);
    let clip = world.get::<Camera>(camera).map(Camera::clip_from_view);
    let pose = world.get::<GlobalTransform>(camera).copied();
    let immersion = world
        .get_resource::<crate::tidewater_underwater::TidewaterUnderwaterObservation>()
        .copied();
    {
        let mut materials = world.resource_mut::<Assets<OpticalMaterial>>();
        let mut material = materials
            .get_mut(&handle)
            .ok_or("Ocean optical material missing")?;
        if let Some(size) = size {
            let size = size.as_vec2();
            material.frame[34] = UVec4::new(
                size.x.to_bits(),
                size.y.to_bits(),
                (1.0 / size.x).to_bits(),
                (1.0 / size.y).to_bits(),
            );
            material.frame[35].x = size.x.to_bits();
            material.frame[35].y = size.y.to_bits();
        }
        if let (Some(clip), Some(pose)) = (clip, pose) {
            let inverse_view = pose.to_matrix();
            let view = inverse_view.inverse();
            let view_projection = clip * view;
            let matrices = [
                (0, view),
                (4, clip),
                (8, view_projection),
                (12, inverse_view),
                (16, clip.inverse()),
                (20, view_projection.inverse()),
                (24, view_projection),
                (28, view_projection),
            ];
            for (first, matrix) in matrices {
                let values = matrix.to_cols_array();
                for column in 0..4 {
                    material.frame[first + column] =
                        UVec4::from_array(std::array::from_fn(|row| {
                            values[column * 4 + row].to_bits()
                        }));
                }
            }
            let position = pose.translation();
            material.frame[32].x = position.x.to_bits();
            material.frame[32].y = position.y.to_bits();
            material.frame[32].z = position.z.to_bits();
            material.frame[33].x = position.x.to_bits();
            material.frame[33].y = position.y.to_bits();
            material.frame[33].z = position.z.to_bits();
        }
        if let Some(step) = completed {
            material.frame[36].x = step.step;
            #[allow(clippy::cast_possible_truncation)]
            {
                material.frame[36].y = (step.time as f32).to_bits();
            }
            material.frame[36].z = step.dt.to_bits();
        }
        if let Some(immersion) = immersion {
            material.frame[39].w = f32::from(immersion.camera_underwater()).to_bits();
            material.frame[40].w = immersion.water_height_m.to_bits();
        }
    }
    if let Some(step) = completed {
        world.resource_mut::<ResidentOptics>().live_step = step.step;
    }
    Ok(())
}

fn extent(width: u32, height: u32) -> Extent3d {
    Extent3d {
        width,
        height,
        depth_or_array_layers: 1,
    }
}

/// WebGPU guarantees twelve uniform buffers per shader stage. The source
/// material declares thirteen, so preserve their bytes and field layout in
/// one combined block instead of dropping a visual effect.
fn pack_optical_uniforms(source: &str) -> Result<String, String> {
    let mut shader = source.to_owned();
    for declaration in [
        "@group(#{MATERIAL_BIND_GROUP}) @binding(19) var<uniform> skyParams:SkyParams;",
        "@group(#{MATERIAL_BIND_GROUP}) @binding(22) var<uniform> mat:MaterialParams9;",
        "@group(#{MATERIAL_BIND_GROUP}) @binding(23) var<uniform> whaleMarks:WhaleMarks;",
        "@group(#{MATERIAL_BIND_GROUP}) @binding(25) var<uniform> shoreP:ShoreParams;",
        "@group(#{MATERIAL_BIND_GROUP}) @binding(28) var<uniform> shoreSimP:ShoreSimParams;",
        "@group(#{MATERIAL_BIND_GROUP}) @binding(31) var<uniform> seaDetailParams:SeaDetailParams;",
    ] {
        if !shader.contains(declaration) {
            return Err(format!("Source optical declaration missing: {declaration}"));
        }
        shader = shader.replace(declaration, "");
    }
    let clouds =
        "@group(#{MATERIAL_BIND_GROUP}) @binding(32) var<uniform> cloudsParams:CloudsParams;";
    if !shader.contains(clouds) {
        return Err("Source cloud optical declaration missing".into());
    }
    shader = shader.replace(
        clouds,
        "struct OpticalConstants { sky: SkyParams, mat: MaterialParams9, whales: WhaleMarks, @size(64) shore: ShoreParams, @size(48) sim: ShoreSimParams, @size(32) detail: SeaDetailParams, clouds: CloudsParams, };\n@group(#{MATERIAL_BIND_GROUP}) @binding(19) var<uniform> opticalConstants:OpticalConstants;",
    );
    for (from, to) in [
        ("skyParams.", "opticalConstants.sky."),
        ("mat.", "opticalConstants.mat."),
        ("whaleMarks.", "opticalConstants.whales."),
        ("shoreP.", "opticalConstants.shore."),
        ("shoreSimP.", "opticalConstants.sim."),
        ("seaDetailParams.", "opticalConstants.detail."),
        ("cloudsParams.", "opticalConstants.clouds."),
    ] {
        shader = shader.replace(from, to);
    }
    Ok(shader)
}

fn words<const N: usize>(bytes: &[u8]) -> Result<[UVec4; N], String> {
    if bytes.len() != N * 16 {
        return Err("Water uniform size mismatch".into());
    }
    Ok(std::array::from_fn(|row| {
        UVec4::from_array(std::array::from_fn(|column| {
            let start = row * 16 + column * 4;
            u32::from_le_bytes(bytes[start..start + 4].try_into().expect("checked size"))
        }))
    }))
}
