// SPDX-License-Identifier: MIT
// Adapted from Tidewater WaterMaterial.js and pinned native Forge probe.
// Tidewater copyright 2026 DRG Software Solutions LLC; revision 1438b1ab.
//! Source optical material for the resident FFT ocean.
//! Shore simulation remains a separate GPU stage; its state is explicit here.
use crate::sampled_terrain_corrections::Corrections;
use crate::source_environment::scene::ResidentEnvironment;
use crate::source_ocean;
use crate::source_ocean_geometry::{GeometryMaterial, ResidentGeometry};
use crate::source_scene_depth;
use crate::source_shore_field;
use crate::source_terrain::lod::LodMaterial;
use crate::source_terrain::scene::ResidentTerrain;
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
use forge_assets::source_shore_sim_package::ShoreSimPackage;
use forge_assets::source_water_surface_package::WaterSurfacePackage;
use forge_project::TerrainDocument;
use forge_world::sampled_terrain::SampledTerrainPlan;

/// Shared optical shader used by every source-water adapter.
pub const SHADER: Handle<Shader> = uuid_handle!("177642ae-53ed-4f92-81d1-a0ef46a35413");
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

/// Source optical parameters shared by water consumers. Each value is kept as
/// source bits, including signed zero; no floating-point conversion occurs.
#[derive(Clone, Copy, Debug, PartialEq, Eq)]
pub struct OpticalConstants {
    pub sky: [UVec4; 2],
    pub material: [UVec4; 5],
    pub whales: [UVec4; 6],
    pub shore: [UVec4; 4],
    pub simulation: [UVec4; 3],
    pub detail: [UVec4; 2],
}

impl OpticalConstants {
    /// Preserve the original uniform bytes in the common WebGPU block. Clouds
    /// occupy the last five rows and remain independent of the source packet.
    #[must_use]
    pub fn gpu_words(&self, clouds: [Vec4; 5]) -> [UVec4; 27] {
        let mut words = [UVec4::ZERO; 27];
        words[0..2].copy_from_slice(&self.sky);
        words[2..7].copy_from_slice(&self.material);
        words[7..13].copy_from_slice(&self.whales);
        words[13..17].copy_from_slice(&self.shore);
        words[17..20].copy_from_slice(&self.simulation);
        words[20..22].copy_from_slice(&self.detail);
        for (index, value) in clouds.into_iter().enumerate() {
            words[22 + index] = UVec4::from_array(value.to_array().map(f32::to_bits));
        }
        words
    }
}

#[derive(Asset, TypePath, AsBindGroup, Debug, Clone)]
pub struct OpticalMaterial {
    #[uniform(0, visibility(vertex, fragment, compute))]
    pub morph: [Vec4; 12],
    #[uniform(1, visibility(vertex, fragment, compute))]
    pub ocean: [UVec4; 14],
    #[uniform(2, visibility(vertex, fragment, compute))]
    pub surface: [Vec4; 2],
    #[texture(3, filterable = false, visibility(vertex, fragment, compute))]
    #[dependency]
    pub height: Handle<Image>,
    #[texture(50, filterable = false, visibility(vertex, fragment, compute))]
    #[dependency]
    pub corrections: Handle<Image>,
    #[texture(4, dimension = "2d_array", visibility(vertex, fragment, compute))]
    #[sampler(5, visibility(vertex, fragment, compute))]
    #[dependency]
    pub displacement: Handle<Image>,
    #[texture(6, sample_type = "depth", visibility(vertex, fragment, compute))]
    #[dependency]
    pub scene_depth: Handle<Image>,
    #[uniform(7, visibility(vertex, fragment, compute))]
    pub frame: [UVec4; 45],
    #[texture(8, dimension = "2d_array", visibility(vertex, fragment, compute))]
    #[dependency]
    pub derivatives: Handle<Image>,
    #[texture(10, visibility(vertex, fragment, compute))]
    #[sampler(9, visibility(vertex, fragment, compute))]
    #[dependency]
    pub foam: Handle<Image>,
    #[texture(11, visibility(vertex, fragment, compute))]
    #[sampler(12, visibility(vertex, fragment, compute))]
    #[dependency]
    pub terrain_normal: Handle<Image>,
    #[texture(13, visibility(vertex, fragment, compute))]
    #[dependency]
    pub hill: Handle<Image>,
    #[texture(
        15,
        dimension = "2d_array",
        sample_type = "depth",
        visibility(vertex, fragment, compute)
    )]
    #[sampler(16, sampler_type = "comparison", visibility(vertex, fragment, compute))]
    #[dependency]
    pub object: Handle<Image>,
    #[uniform(17, visibility(vertex, fragment, compute))]
    pub object_settings: crate::source_shadows::ObjectShadowSettings,
    #[storage(44, read_only, visibility(vertex, fragment, compute))]
    #[dependency]
    pub live_shadows: Handle<bevy::render::storage::ShaderBuffer>,
    #[uniform(18, visibility(vertex, fragment, compute))]
    pub atmosphere: [Vec4; 4],
    #[uniform(19, visibility(vertex, fragment, compute))]
    pub constants: [UVec4; 27],
    #[texture(20, visibility(vertex, fragment, compute))]
    #[dependency]
    pub transmittance: Handle<Image>,
    #[texture(21, visibility(vertex, fragment, compute))]
    #[dependency]
    pub sky_lut: Handle<Image>,
    #[texture(24, filterable = false, visibility(vertex, fragment, compute))]
    #[dependency]
    pub shore_field: Handle<Image>,
    #[texture(26, filterable = false, visibility(vertex, fragment, compute))]
    #[dependency]
    pub shore_direction: Handle<Image>,
    #[texture(27, visibility(vertex, fragment, compute))]
    #[dependency]
    pub sim_state: Handle<Image>,
    #[texture(29, visibility(vertex, fragment, compute))]
    #[dependency]
    pub sim_lace: Handle<Image>,
    #[texture(30, visibility(vertex, fragment, compute))]
    #[dependency]
    pub detail_noise: Handle<Image>,
    #[texture(33, visibility(vertex, fragment, compute))]
    #[dependency]
    pub cloud_panorama: Option<Handle<Image>>,
    #[texture(34, filterable = false, visibility(vertex, fragment, compute))]
    #[dependency]
    pub cloud_shadow: Option<Handle<Image>>,
    #[texture(51, visibility(fragment))]
    #[dependency]
    pub refraction_color: Handle<Image>,
    #[texture(52, sample_type = "depth", visibility(fragment))]
    #[dependency]
    pub refraction_depth: Handle<Image>,
    #[uniform(53, visibility(fragment))]
    pub refraction_ready: Vec4,
    #[texture(54, filterable = false, visibility(fragment))]
    #[dependency]
    pub hull_distance: Handle<Image>,
    #[storage(55, read_only, visibility(fragment))]
    #[dependency]
    pub hull_readiness: Handle<bevy::render::storage::ShaderBuffer>,
}

/// Frame and sky words shared by the ocean and its visible breaking effects.
pub(crate) struct EffectFrame {
    pub frame: [UVec4; 45],
    pub sky: [UVec4; 2],
}

pub(crate) fn effect_frame(world: &World) -> Result<Option<EffectFrame>, String> {
    let Some(resident) = world.get_resource::<ResidentOptics>() else {
        return Ok(None);
    };
    let material = world
        .resource::<Assets<OpticalMaterial>>()
        .get(&resident.material)
        .ok_or("Ocean optical material missing")?;
    Ok(Some(EffectFrame {
        frame: material.frame,
        sky: [material.constants[0], material.constants[1]],
    }))
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
    source_scene_depth::register(app)?;
    crate::source_refraction_capture::register(app)?;
    crate::source_hull_mask::register(app)?;
    app.add_plugins(MaterialPlugin::<OpticalMaterial>::default());
    crate::source_surface_queries::register(app)?;
    app.insert_resource(Registered);
    Ok(())
}

/// Connect the source immersion pass to the same optical material and GPU packet.
pub fn register_underwater(app: &mut App, package: &WaterSurfacePackage) -> Result<(), String> {
    use crate::source_underwater::{SourceUnderwaterOptics, SourceUnderwaterPlugin};
    if app
        .world()
        .contains_resource::<crate::source_underwater::SourceUnderwaterControl>()
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
        SourceUnderwaterOptics::new(vector(41), vector(42), vector(37), vector(38), vector(39))?;
    let targets = app
        .world()
        .get_resource::<crate::source_surface_queries::Targets>()
        .ok_or("Immersion requires registered optical surface queries")?;
    let plugin =
        SourceUnderwaterPlugin::new(targets.queries.clone(), targets.packet.clone(), optics)
            .with_surface_shader(
                UNDERWATER_SHADER,
                OpticalMaterial::bind_group_layout_descriptor,
            );
    let ocean = app
        .world()
        .get_resource::<source_ocean::Targets>()
        .ok_or("Immersion shafts require the resident FFT")?
        .clone();
    let maps = crate::source_caustics::prepare_maps(
        app,
        ocean.images[1].clone(),
        ocean.sizes[3].x,
        ocean.sizes[2].x,
        optics.sun_direction.normalize(),
        ocean.completed_steps.clone(),
    )?;
    app.sub_app_mut(bevy::render::RenderApp).add_systems(
        bevy::render::renderer::RenderGraph,
        crate::source_caustics::render_maps
            .after(crate::source_water_schedule::OceanComputeSet)
            .before(bevy::core_pipeline::schedule::camera_driver),
    );
    crate::source_underwater_lighting::register(
        app,
        OpticalMaterial::bind_group_layout_descriptor,
    )?;
    let light_maps = app
        .world()
        .resource::<crate::source_underwater_lighting::Targets>()
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
    let params = crate::source_surface::underwater::CausticSampleParams::new(
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
    app.insert_resource(crate::source_surface::underwater::CausticMaterialInputs {
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
    constants: OpticalConstants,
    foam: Vec<u8>,
    shore_field: Vec<u8>,
    shore_side: u32,
    lace: Vec<u8>,
    detail_noise: Vec<u8>,
    shore_derived: bool,
}

impl PreparedOptics {
    /// Publish prepared compute programs before their render-startup pipelines
    /// are queued. This does not create or acknowledge a visible water material.
    pub fn prime_programs(&self, world: &mut World) -> Result<(), String> {
        if !world.contains_resource::<Registered>()
            || !world.contains_resource::<crate::source_underwater_lighting::Targets>()
        {
            return Err("Water compute programs require the registered renderer".into());
        }
        crate::source_surface_queries::install_shader(world, self.query_shader.clone())?;
        crate::source_underwater_lighting::install_shader(world, &self.shader)?;
        world
            .resource_mut::<Assets<Shader>>()
            .insert(
                UNDERWATER_SHADER.id(),
                Shader::from_wgsl(
                    self.underwater_shader.clone(),
                    "source-underwater-surface.wgsl",
                ),
            )
            .map_err(|error| error.to_string())?;
        Ok(())
    }

    /// Terrain-dependent input selected for the optical surface before GPU staging.
    #[must_use]
    pub fn shore_field(&self) -> (&[u8], bool) {
        (&self.shore_field, self.shore_derived)
    }

    /// Exact source-bit representation of the simulation window used by optics.
    #[must_use]
    pub const fn simulation_parameters(&self) -> &[UVec4; 3] {
        &self.constants.simulation
    }

    pub(crate) fn set_simulation_parameters(&mut self, bytes: &[u8; 48]) {
        self.constants.simulation = words(bytes).expect("fixed simulation parameter size");
    }

    /// Validate all source and authored inputs before replacing a resident material.
    pub fn new(
        package: &WaterSurfacePackage,
        document: &TerrainDocument,
        plan: &SampledTerrainPlan,
        prepared_shore: Option<crate::source_shore_sim::ShoreField<'_>>,
        reference_shore: Option<&ShoreSimPackage>,
    ) -> Result<Self, String> {
        plan.require_document(document).map_err(|e| e.to_string())?;
        let file = |name: &str| {
            package
                .file(name)
                .ok_or_else(|| format!("Pinned water surface file missing: {name}"))
        };
        let source =
            std::str::from_utf8(file("water-optics.wgsl")?).map_err(|error| error.to_string())?;
        let water_level =
            crate::source_water_host::water_level_from_frame(file("water-frame.bin")?)?;
        let domain = source_shore_field::Domain::for_terrain(document, water_level)?;
        let shore_side = u32::try_from(domain.side()).map_err(|_| "Shore-field side overflow")?;
        let source = domain.prepare_water_shader(source, document)?;
        let corrections = Corrections::from_document(document)?;
        let edited = !document.height_deltas.is_empty() || corrections.elevation != 0.0;
        let shader = if edited {
            corrections.shader(&source, "#{MATERIAL_BIND_GROUP}", 50)?
        } else {
            source
        };
        let shader = prepare_optical_shader(&shader)?;
        let query_shader = crate::source_surface_queries::shader_source(&shader)?;
        let underwater_shader = crate::source_underwater::surface_query_shader_source(&shader)?;
        let captured_matches = domain == source_shore_field::Domain::REFERENCE
            && reference_shore.is_some_and(|reference| {
                reference.shore_field_sha256() == package.shore_field_sha256()
                    && reference.source_height_sha256() == plan.raster().source().samples_sha256
                    && reference.file("shore-params.bin") == package.file("uniform-shoreP.bin")
                    && reference.file("simulation-params.bin")
                        == package.file("uniform-shoreSimP.bin")
            });
        let (shore_field, shore_derived) = if let Some(field) = prepared_shore {
            if field.document_hash() != plan.document_hash()
                || field.surface_package_sha256() != package.identity()
                || field.domain() != domain
                || field.bytes().len()
                    != crate::source_breaker_gpu::TerrainTextureLayout::byte_count(
                        [shore_side; 2],
                        16,
                    )?
                || (edited && !field.is_derived())
            {
                return Err("Prepared shore field does not match the terrain and surface".into());
            }
            (field.bytes().to_vec(), field.is_derived())
        } else if edited || !captured_matches {
            (
                source_shore_field::bake_in_domain(domain, |x, z| plan.height_at(x, z))
                    .map_err(|error| error.to_string())?
                    .into_iter()
                    .flat_map(f32::to_le_bytes)
                    .collect(),
                true,
            )
        } else {
            (file("shore-field.rgba32.bin")?.to_vec(), false)
        };
        let simulation_params = if let Some(field) = prepared_shore {
            field.simulation_parameters().as_slice()
        } else {
            file("uniform-shoreSimP.bin")?
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
            constants: OpticalConstants {
                sky: words(file("uniform-skyParams.bin")?)?,
                material: words(file("uniform-mat.bin")?)?,
                whales: words(file("uniform-whaleMarks.bin")?)?,
                shore: words(file("uniform-shoreP.bin")?)?,
                simulation: words(simulation_params)?,
                detail: words(file("uniform-seaDetailParams.bin")?)?,
            },
            foam,
            shore_field,
            shore_side,
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
        let ocean = world.resource::<source_ocean::Targets>().clone();
        let depth = world.resource::<source_scene_depth::SceneDepth>().0.clone();
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
            .get_resource::<crate::source_shore_sim::Targets>()
            .cloned();
        crate::source_shadows::receiver::install(world)?;
        if world.contains_resource::<crate::source_underwater_lighting::Targets>() {
            crate::source_underwater_lighting::install_shader(world, &self.shader)?;
        }
        world
            .resource_mut::<Assets<Shader>>()
            .insert(
                SHADER.id(),
                Shader::from_wgsl(self.shader, "source-water-optics.wgsl"),
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
                extent(self.shore_side, self.shore_side),
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
                images.add(crate::source_vegetation::textures::mipped(
                    crate::source_vegetation::textures::Layout {
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
        let constants = self.constants.gpu_words(cloud_view);
        let refraction = world
            .resource::<crate::source_refraction_capture::Targets>()
            .clone();
        let hull_distance = world
            .resource::<crate::source_hull_mask::Targets>()
            .distance
            .clone();
        let hull_readiness = world
            .resource::<crate::source_hull_mask::Targets>()
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
                live_shadows: crate::source_shadows::receiver::DEFAULT_BUFFER,
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
        crate::source_surface_queries::stage(world, self.query_shader, material.id().untyped())?;
        if world.contains_resource::<crate::source_underwater_lighting::Targets>() {
            world.insert_resource(crate::source_underwater_lighting::Surface(
                material.id().untyped(),
            ));
        }
        if world.contains_resource::<crate::source_underwater::SourceUnderwaterControl>() {
            world
                .resource_mut::<Assets<Shader>>()
                .insert(
                    UNDERWATER_SHADER.id(),
                    Shader::from_wgsl(self.underwater_shader, "source-underwater-surface.wgsl"),
                )
                .map_err(|error| error.to_string())?;
            world.insert_resource(crate::source_underwater::SourceUnderwaterSurfaceMaterial(
                material.id().untyped(),
            ));
        }
        Ok(())
    }
}

/// Publish validated solar frame fields without changing camera/wave/immersion
/// state. The scene must supply atmosphere parameters from that same frame;
/// this operation alone does not refresh the environment or enable a solar cycle.
pub fn update_lighting(
    world: &mut World,
    lighting: &crate::source_sky::frame_lighting::FrameLighting,
    atmosphere: [Vec4; 4],
) -> Result<(), String> {
    if atmosphere.iter().any(|value| !value.is_finite()) {
        return Err("Invalid ocean atmosphere parameters".into());
    }
    let handle = world
        .get_resource::<ResidentOptics>()
        .ok_or("Ocean optics absent")?
        .material
        .clone();
    let mut materials = world
        .get_resource_mut::<Assets<OpticalMaterial>>()
        .ok_or("Ocean optical materials absent")?;
    let mut material = materials
        .get_mut(&handle)
        .ok_or("Ocean optical material missing")?;
    let mut words = material.frame.map(|value| value.to_array());
    lighting.write_words(&mut words);
    material.frame = words.map(UVec4::from_array);
    material.atmosphere = atmosphere;
    Ok(())
}

pub fn update(world: &mut World, camera: Entity) -> Result<(), String> {
    crate::source_refraction_capture::select_camera(world, camera);
    crate::source_hull_mask::select_camera(world, Some(camera));
    let size = source_scene_depth::resize(world, camera)?;
    let Some(resident) = world.get_resource::<ResidentOptics>() else {
        return Ok(());
    };
    let handle = resident.material.clone();
    let completed = source_ocean::completed_frame(world);
    let clip = world.get::<Camera>(camera).map(Camera::clip_from_view);
    let pose = world.get::<GlobalTransform>(camera).copied();
    let immersion = world
        .get_resource::<crate::source_underwater::UnderwaterObservation>()
        .copied();
    {
        let mut materials = world.resource_mut::<Assets<OpticalMaterial>>();
        let mut material = materials
            .get_mut(&handle)
            .ok_or("Ocean optical material missing")?;
        if let Some(size) = size {
            crate::source_water_frame::write_resolution(&mut material.frame, size);
        }
        if let (Some(clip), Some(pose)) = (clip, pose) {
            crate::source_water_frame::CameraFrame::new(clip, pose).write(&mut material.frame);
        }
        if let Some(step) = completed {
            crate::source_water_frame::write_time(
                &mut material.frame,
                step.step,
                step.time,
                step.dt,
            );
        }
        if let Some(immersion) = immersion {
            crate::source_water_frame::write_immersion(
                &mut material.frame,
                immersion.camera_underwater(),
                immersion.water_height_m,
            );
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

/// Adapt one verified source shader for the common optical material and its
/// shadow, refraction and hull bindings. Both module and reference consumers
/// must install the same transformed program.
pub fn prepare_optical_shader(source: &str) -> Result<String, String> {
    let shader = pack_optical_uniforms(&crate::source_shadows::receiver::shader_source(source)?)?;
    let shader = crate::source_refraction::visible_water_mask_shader_source(&shader)?;
    let shader = crate::source_refraction::water_shader_source(&shader)?;
    crate::source_hull_mask::water_shader_source(&shader)
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

#[cfg(test)]
mod tests {
    use super::*;
    use bevy::{asset::VisitAssetDependencies, render::storage::ShaderBuffer};
    use std::collections::HashSet;

    #[test]
    fn source_optical_constants_pack_in_shader_order_without_changing_bits() {
        let row = |n: u32| UVec4::new(n, n ^ 0x8000_0000, 0x7fc0_1234, !n);
        let source = OpticalConstants {
            sky: std::array::from_fn(|i| row(u32::try_from(i).unwrap())),
            material: std::array::from_fn(|i| row(u32::try_from(i).unwrap() + 2)),
            whales: std::array::from_fn(|i| row(u32::try_from(i).unwrap() + 7)),
            shore: std::array::from_fn(|i| row(u32::try_from(i).unwrap() + 13)),
            simulation: std::array::from_fn(|i| row(u32::try_from(i).unwrap() + 17)),
            detail: std::array::from_fn(|i| row(u32::try_from(i).unwrap() + 20)),
        };
        let clouds = [Vec4::new(-0.0, 0.0, 1.0, -1.0); 5];
        let words = source.gpu_words(clouds);
        for (index, actual) in words[..22].iter().enumerate() {
            assert_eq!(*actual, row(u32::try_from(index).unwrap()));
        }
        for (actual, cloud) in words[22..].iter().zip(clouds) {
            assert_eq!(
                *actual,
                UVec4::from_array(cloud.to_array().map(f32::to_bits))
            );
        }
    }

    #[test]
    fn optical_material_inventory_visits_every_bound_image_and_buffer() {
        let mut images = Assets::<Image>::default();
        let mut image = || images.add(Image::default());
        let mut buffers = Assets::<ShaderBuffer>::default();
        let material = OpticalMaterial {
            morph: [Vec4::ZERO; 12],
            ocean: [UVec4::ZERO; 14],
            surface: [Vec4::ZERO; 2],
            height: image(),
            corrections: image(),
            displacement: image(),
            scene_depth: image(),
            frame: [UVec4::ZERO; 45],
            derivatives: image(),
            foam: image(),
            terrain_normal: image(),
            hill: image(),
            object: image(),
            object_settings: default(),
            live_shadows: buffers.add(ShaderBuffer::default()),
            atmosphere: [Vec4::ZERO; 4],
            constants: [UVec4::ZERO; 27],
            transmittance: image(),
            sky_lut: image(),
            shore_field: image(),
            shore_direction: image(),
            sim_state: image(),
            sim_lace: image(),
            detail_noise: image(),
            cloud_panorama: Some(image()),
            cloud_shadow: Some(image()),
            refraction_color: image(),
            refraction_depth: image(),
            refraction_ready: Vec4::ZERO,
            hull_distance: image(),
            hull_readiness: buffers.add(ShaderBuffer::default()),
        };
        let expected: HashSet<_> = [
            material.height.id().untyped(),
            material.corrections.id().untyped(),
            material.displacement.id().untyped(),
            material.scene_depth.id().untyped(),
            material.derivatives.id().untyped(),
            material.foam.id().untyped(),
            material.terrain_normal.id().untyped(),
            material.hill.id().untyped(),
            material.object.id().untyped(),
            material.live_shadows.id().untyped(),
            material.transmittance.id().untyped(),
            material.sky_lut.id().untyped(),
            material.shore_field.id().untyped(),
            material.shore_direction.id().untyped(),
            material.sim_state.id().untyped(),
            material.sim_lace.id().untyped(),
            material.detail_noise.id().untyped(),
            material.cloud_panorama.as_ref().unwrap().id().untyped(),
            material.cloud_shadow.as_ref().unwrap().id().untyped(),
            material.refraction_color.id().untyped(),
            material.refraction_depth.id().untyped(),
            material.hull_distance.id().untyped(),
            material.hull_readiness.id().untyped(),
        ]
        .into_iter()
        .collect();
        let mut observed = HashSet::new();
        material.visit_dependencies(&mut |id| {
            observed.insert(id);
        });
        assert_eq!(observed, expected);
        assert_eq!(observed.len(), 23);
    }
}
