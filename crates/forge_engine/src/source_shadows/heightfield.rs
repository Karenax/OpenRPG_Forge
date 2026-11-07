//! Heightfield shadow kernel shared by native scene and editor adapters.
//! Callers supply verified WGSL and prepared height mipmaps on the existing device.
#![allow(clippy::needless_pass_by_value)]
mod heights;
use bevy::{
    asset::RenderAssetUsages,
    core_pipeline::schedule::camera_driver,
    prelude::*,
    render::{
        Render, RenderApp, RenderStartup, RenderSystems,
        extract_resource::{ExtractResource, ExtractResourcePlugin},
        render_asset::RenderAssets,
        render_resource::{
            BindGroup, BindGroupEntries, BindGroupLayoutDescriptor, BindGroupLayoutEntries,
            CachedComputePipelineId, ComputePassDescriptor, ComputePipelineDescriptor, Extent3d,
            PipelineCache, SamplerBindingType, ShaderStages, ShaderType, StorageTextureAccess,
            TextureDimension, TextureFormat, TextureSampleType, TextureUsages, UniformBuffer,
            binding_types::{sampler, texture_2d, texture_storage_2d, uniform_buffer},
        },
        renderer::{RenderContext, RenderDevice, RenderGraph, RenderQueue},
        texture::GpuImage,
    },
};
pub use heights::mip_chain;
/// Existing source kernel, promoted to engine code with its MIT attribution.
/// Callers can still supply an independently verified compatible shader explicitly.
pub const SOURCE_KERNEL: &str = include_str!("heightfield/kernel.wgsl");
use std::sync::{
    Arc,
    atomic::{AtomicBool, Ordering},
};

const HILL_SHADER: Handle<Shader> =
    bevy::asset::uuid_handle!("f6cd8549-b783-4cc9-9efb-645aaf3809af");

/// Install the verified kernel before startup; no filesystem fallback.
pub fn install_shader(app: &mut App, source: &str) -> Result<(), String> {
    if source.trim().is_empty() || source.len() > 1_000_000 {
        return Err("Invalid heightfield shadow shader size".into());
    }
    stage_shader(app.world_mut(), source)
}

fn stage_shader(world: &mut World, source: &str) -> Result<(), String> {
    world
        .get_resource_mut::<Assets<Shader>>()
        .ok_or("Heightfield shadows require shader assets")?
        .insert(
            HILL_SHADER.id(),
            Shader::from_wgsl(source.to_owned(), "source-heightfield-shadow.wgsl"),
        )
        .map_err(|e| e.to_string())
}

#[derive(Resource, Clone, ExtractResource)]
pub struct Images {
    pub input: Handle<Image>,
    pub output: Handle<Image>,
    /// Commands encoded for these handles; not a GPU completion fence.
    pub ready: Arc<AtomicBool>,
}
#[derive(Resource, Default)]
pub struct Ready(pub bool);
#[derive(Resource, Clone, ExtractResource, ShaderType)]
pub struct Settings {
    pub origin: f32,
    pub size: f32,
    pub sun_bake: Vec3,
}
/// Prepared 512-square `R16Float` height mip chain and the source light direction.
/// Immutable after validation; the caller retains responsibility for provenance.
#[derive(Clone)]
pub struct PreparedInput {
    shader: String,
    bytes: Vec<u8>,
    settings: Settings,
}

/// The source-compatible square shadow field prepared from a terrain document.
/// The square encloses the terrain; it does not stretch its rectangular axes.
pub(crate) struct PreparedHeights {
    shader: String,
    bytes: Vec<u8>,
    origin: f32,
    size: f32,
}

impl PreparedHeights {
    #[allow(clippy::cast_possible_truncation)] // Plan admission bounds the combined finite heights.
    pub(crate) fn from_plan(
        plan: &forge_world::sampled_terrain::SampledTerrainPlan,
    ) -> Result<Self, String> {
        let (bytes, origin, size) = heights::for_plan(plan)?;
        let shader = if crate::source_terrain::supports_fixed_source_raster(plan.raster().source())
        {
            SOURCE_KERNEL.to_owned()
        } else {
            const START: &str = "var t = f32( 24 );";
            const STEP: &str = "var dt = f32( 6 );";
            const HEIGHT: &str = "let hq = textureSampleLevel( terrainShadowHeights, smpLinearClamp, uv, log2( max( dt * 0.25000000, 1.0 ) ) ).x;";
            if [START, STEP, HEIGHT]
                .iter()
                .any(|seam| SOURCE_KERNEL.matches(seam).count() != 1)
            {
                return Err("Heightfield mip sampling seam differs".into());
            }
            // The source's 24 m/6 m steps represent six/1.5 texels on a
            // 2048 m domain. Preserve those ratios for every admitted size.
            let outside = (f64::from(plan.raster().source().outside_height_m)
                + f64::from(plan.document().height_sampling.base_elevation_m))
                as f32;
            let outside = outside.clamp(-65504.0, 65504.0);
            let lookup = format!(
                "var hq = {outside:?};\n\t\tif all(uv >= vec2f(0.0)) && all(uv < vec2f(1.0)) {{\n\t\t\thq = textureSampleLevel(terrainShadowHeights, smpLinearClamp, uv, log2(max(dt * (512.0 / terrainParams.size), 1.0))).x;\n\t\t}}"
            );
            SOURCE_KERNEL
                .replacen(START, "var t = terrainParams.size * 0.01171875;", 1)
                .replacen(STEP, "var dt = terrainParams.size * 0.0029296875;", 1)
                .replacen(HEIGHT, &lookup, 1)
        };
        Ok(Self {
            shader,
            bytes,
            origin,
            size,
        })
    }

    pub(crate) fn with_sun(self, sun_bake: Vec3) -> Result<PreparedInput, String> {
        PreparedInput::new(
            self.shader,
            self.bytes,
            Settings {
                origin: self.origin,
                size: self.size,
                sun_bake,
            },
        )
    }
}

impl PreparedInput {
    /// Select the common source algorithm using the scene's actual heightfield.
    pub fn from_heights(heights: &[f32], settings: Settings) -> Result<Self, String> {
        Self::new(SOURCE_KERNEL.to_owned(), mip_chain(heights)?, settings)
    }

    pub fn new(shader: String, bytes: Vec<u8>, settings: Settings) -> Result<Self, String> {
        if shader.trim().is_empty()
            || shader.len() > 1_000_000
            || bytes.len() != 699_050
            || bytes
                .as_chunks::<2>()
                .0
                .iter()
                .any(|v| u16::from_le_bytes(*v) & 0x7c00 == 0x7c00)
            || !settings.origin.is_finite()
            || !settings.size.is_finite()
            || settings.size <= 0.0
            || !settings.sun_bake.is_finite()
            || (settings.sun_bake.length_squared() - 1.0).abs() > 0.001
        {
            return Err("Invalid heightfield shadow inputs".into());
        }
        Ok(Self {
            shader,
            bytes,
            settings,
        })
    }

    /// Allocate a new generation after the shared plugin has been registered.
    pub fn stage(self, world: &mut World) -> Result<Images, String> {
        if !world.contains_resource::<Registered>() || !world.contains_resource::<Assets<Image>>() {
            return Err("Heightfield shadows require initialized render plugins".into());
        }
        stage_shader(world, &self.shader)?;
        let mut input = Image::new_fill(
            Extent3d {
                width: 512,
                height: 512,
                depth_or_array_layers: 1,
            },
            TextureDimension::D2,
            &[0, 0],
            TextureFormat::R16Float,
            RenderAssetUsages::RENDER_WORLD,
        );
        input.data = Some(self.bytes);
        input.texture_descriptor.mip_level_count = 10;
        input.sampler = bevy::image::ImageSampler::linear();
        let mut output = Image::new_fill(
            Extent3d {
                width: 512,
                height: 512,
                depth_or_array_layers: 1,
            },
            TextureDimension::D2,
            &[0; 8],
            TextureFormat::Rgba16Float,
            RenderAssetUsages::RENDER_WORLD,
        );
        output.texture_descriptor.usage |= TextureUsages::STORAGE_BINDING | TextureUsages::COPY_SRC;
        let mut assets = world.resource_mut::<Assets<Image>>();
        let targets = Images {
            input: assets.add(input),
            output: assets.add(output),
            ready: Arc::new(AtomicBool::new(false)),
        };
        world.insert_resource(self.settings);
        world.insert_resource(targets.clone());
        Ok(targets)
    }
}

#[derive(Resource)]
struct Registered;
pub struct HillShadowPlugin;
impl Plugin for HillShadowPlugin {
    fn build(&self, app: &mut App) {
        app.insert_resource(Registered);
        app.add_plugins((
            ExtractResourcePlugin::<Images>::default(),
            ExtractResourcePlugin::<Settings>::default(),
        ));
        app.sub_app_mut(RenderApp)
            .init_resource::<Ready>()
            .add_systems(RenderStartup, init_pipeline)
            .add_systems(Render, prepare.in_set(RenderSystems::PrepareBindGroups))
            .add_systems(RenderGraph, bake.before(camera_driver));
    }
}

#[derive(Resource)]
struct Pipeline {
    layout: BindGroupLayoutDescriptor,
    id: CachedComputePipelineId,
}
#[derive(Resource)]
struct Bindings(BindGroup, Arc<AtomicBool>);
fn init_pipeline(mut commands: Commands, cache: Res<PipelineCache>) {
    let layout = BindGroupLayoutDescriptor::new(
        "Source heightfield shadow",
        &BindGroupLayoutEntries::sequential(
            ShaderStages::COMPUTE,
            (
                texture_2d(TextureSampleType::Float { filterable: true }),
                sampler(SamplerBindingType::Filtering),
                texture_storage_2d(TextureFormat::Rgba16Float, StorageTextureAccess::WriteOnly),
                uniform_buffer::<Settings>(false),
            ),
        ),
    );
    let id = cache.queue_compute_pipeline(ComputePipelineDescriptor {
        layout: vec![layout.clone()],
        shader: HILL_SHADER,
        entry_point: Some("main".into()),
        ..default()
    });
    commands.insert_resource(Pipeline { layout, id });
}
#[allow(clippy::too_many_arguments)] // GPU resources and extracted authoring inputs.
fn prepare(
    mut commands: Commands,
    pipeline: Res<Pipeline>,
    images: Option<Res<Images>>,
    gpu: Res<RenderAssets<GpuImage>>,
    device: Res<RenderDevice>,
    cache: Res<PipelineCache>,
    bindings: Option<Res<Bindings>>,
    settings: Option<Res<Settings>>,
    queue: Res<RenderQueue>,
    mut ready: ResMut<Ready>,
) {
    let (Some(images), Some(settings)) = (images, settings) else {
        return;
    };
    if bindings.is_some() && !images.is_changed() && !settings.is_changed() {
        return;
    }
    let (Some(input), Some(output)) = (gpu.get(&images.input), gpu.get(&images.output)) else {
        return;
    };
    let mut uniform = UniformBuffer::from(settings.into_inner());
    uniform.write_buffer(&device, &queue);
    let bind = device.create_bind_group(
        "Source heightfield shadow",
        &cache.get_bind_group_layout(&pipeline.layout),
        &BindGroupEntries::sequential((
            &input.texture_view,
            &input.sampler,
            &output.texture_view,
            &uniform,
        )),
    );
    commands.insert_resource(Bindings(bind, images.ready.clone()));
    ready.0 = false;
}
fn bake(
    mut context: RenderContext,
    pipeline: Res<Pipeline>,
    cache: Res<PipelineCache>,
    bindings: Option<Res<Bindings>>,
    images: Option<Res<Images>>,
    mut ready: ResMut<Ready>,
) {
    if ready.0 {
        return;
    }
    let (Some(compute), Some(bind)) = (cache.get_compute_pipeline(pipeline.id), bindings) else {
        return;
    };
    if !images.is_some_and(|images| Arc::ptr_eq(&images.ready, &bind.1)) {
        return;
    }
    let mut pass = context
        .command_encoder()
        .begin_compute_pass(&ComputePassDescriptor::default());
    pass.set_pipeline(compute);
    pass.set_bind_group(0, &bind.0, &[]);
    pass.dispatch_workgroups(64, 64, 1);
    bind.1.store(true, Ordering::Release);
    ready.0 = true;
}

#[cfg(test)]
mod tests;
