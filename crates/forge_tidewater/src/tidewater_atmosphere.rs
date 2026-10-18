//! Ordered Tidewater atmosphere kernels on Bevy's existing render device.
//! Callers supply verified WGSL and parameters; no filesystem or network access.
#![allow(clippy::needless_pass_by_value)]
mod acquisition;
pub mod controller;
mod live;
pub(crate) mod observer;
pub(crate) mod prepared;
pub mod readback;
pub use acquisition::from_acquisition;
use bevy::{
    asset::RenderAssetUsages,
    core_pipeline::schedule::camera_driver,
    prelude::*,
    render::{
        RenderApp,
        diagnostic::RecordDiagnostics,
        extract_resource::{ExtractResource, ExtractResourcePlugin},
        render_asset::RenderAssets,
        render_resource as gpu,
        renderer::{RenderContext, RenderDevice, RenderGraph, RenderGraphSystems},
        storage::{GpuShaderBuffer, ShaderBuffer},
        texture::GpuImage,
    },
};
use live::Work;
pub use live::update_parameters;
use std::sync::{
    Arc,
    atomic::{AtomicBool, Ordering},
};
const NAMES: [&str; 4] = ["transmittance", "multiScat", "skyView", "irradiance"];
const SIZES: [(u32, u32); 3] = [(256, 64), (32, 32), (192, 108)];
/// Atmosphere LUT updates precede cloud kernels on the same render schedule.
#[derive(SystemSet, Debug, Clone, Copy, PartialEq, Eq, Hash)]
pub struct AtmosphereCompute;
/// Validated inputs. Shader provenance and interface qualification belong to the caller.
#[derive(Resource)]
pub struct Input {
    shaders: [String; 4],
    params: Vec<u8>,
}
impl Input {
    /// Exactly four qualified kernels and sixteen finite parameter scalars.
    pub fn new(shaders: [String; 4], values: [f32; 16]) -> Result<Self, String> {
        if shaders
            .iter()
            .any(|s| s.trim().is_empty() || s.len() > 1_000_000)
            || values.iter().any(|v| !v.is_finite())
        {
            return Err("invalid atmosphere shader size or non-finite parameters".into());
        }
        Ok(Self {
            shaders,
            params: values.into_iter().flat_map(f32::to_le_bytes).collect(),
        })
    }
}
/// GPU outputs shared by sky, lighting and diagnostic consumers.
#[derive(Resource, Clone, ExtractResource)]
pub struct Targets {
    pub params: [Vec4; 4],
    pub images: Vec<Handle<Image>>,
    pub irradiance: Handle<ShaderBuffer>,
    /// Commands have been encoded; this is not a CPU GPU-completion fence.
    pub ready: Arc<AtomicBool>,
}
#[derive(Resource, Clone, ExtractResource)]
pub(crate) struct KernelInput(Arc<Input>);
#[derive(Resource)]
struct Registered;

/// Register extraction and execution once, after Bevy's render plugins.
/// Inputs may then be staged from an Update system after application startup.
pub fn register(app: &mut App) -> Result<(), String> {
    if app.world().contains_resource::<Registered>() {
        return Ok(());
    }
    if !app.world().contains_resource::<Assets<Image>>()
        || !app.world().contains_resource::<Assets<ShaderBuffer>>()
        || app.get_sub_app(RenderApp).is_none()
    {
        return Err("atmosphere requires initialized Bevy render and asset plugins".into());
    }
    app.add_plugins((
        ExtractResourcePlugin::<KernelInput>::default(),
        ExtractResourcePlugin::<Targets>::default(),
        ExtractResourcePlugin::<Work>::default(),
    ));
    app.sub_app_mut(RenderApp).add_systems(
        RenderGraph,
        compute
            .in_set(AtmosphereCompute)
            .in_set(RenderGraphSystems::Render)
            .before(camera_driver)
            .run_if(resource_exists::<KernelInput>)
            .run_if(resource_exists::<Targets>)
            .run_if(resource_exists::<Work>),
    );
    app.insert_resource(Registered);
    Ok(())
}

/// Install the initial atmosphere before application startup.
pub fn install(app: &mut App, input: Input) -> Result<Targets, String> {
    if app.world().contains_resource::<Targets>() {
        return Err("atmosphere already installed".into());
    }
    register(app)?;
    stage(app.world_mut(), input)
}

/// Stage a fresh atmosphere, replacing any previous generation atomically.
/// Consumers must adopt the returned handles; old handles remain valid while held.
pub fn stage(world: &mut World, input: Input) -> Result<Targets, String> {
    ensure_registered(world)?;
    let images = SIZES
        .into_iter()
        .map(|(width, height)| {
            let mut image = Image::new_uninit(
                gpu::Extent3d {
                    width,
                    height,
                    depth_or_array_layers: 1,
                },
                gpu::TextureDimension::D2,
                gpu::TextureFormat::Rgba16Float,
                RenderAssetUsages::RENDER_WORLD,
            );
            image.texture_descriptor.usage = gpu::TextureUsages::TEXTURE_BINDING
                | gpu::TextureUsages::STORAGE_BINDING
                | gpu::TextureUsages::COPY_SRC;
            image.sampler =
                bevy::image::ImageSampler::Descriptor(bevy::image::ImageSamplerDescriptor {
                    min_filter: bevy::image::ImageFilterMode::Linear,
                    mag_filter: bevy::image::ImageFilterMode::Linear,
                    ..default()
                });
            world.resource_mut::<Assets<Image>>().add(image)
        })
        .collect();
    let mut buffer = ShaderBuffer::from(vec![Vec4::ZERO; 4]);
    buffer.buffer_description.usage |= gpu::BufferUsages::COPY_SRC;
    let irradiance = world.resource_mut::<Assets<ShaderBuffer>>().add(buffer);
    let targets = Targets {
        params: std::array::from_fn(|i| {
            Vec4::from_array(std::array::from_fn(|j| {
                f32::from_le_bytes(
                    input.params[(i * 4 + j) * 4..(i * 4 + j + 1) * 4]
                        .try_into()
                        .expect("validated parameter size"),
                )
            }))
        }),
        images,
        irradiance,
        ready: Arc::new(AtomicBool::new(false)),
    };
    world.insert_resource(KernelInput(Arc::new(input)));
    world.insert_resource(targets.clone());
    world.insert_resource(Work::default());
    Ok(targets)
}
#[allow(clippy::too_many_lines, clippy::too_many_arguments)] // Four ordered kernels share native LUTs and one parameter block.
fn compute(
    mut context: RenderContext,
    input: Res<KernelInput>,
    targets: Res<Targets>,
    work: Res<Work>,
    device: Res<RenderDevice>,
    images: Res<RenderAssets<GpuImage>>,
    buffers: Res<RenderAssets<GpuShaderBuffer>>,
    mut prepared: Local<Option<prepared::Prepared>>,
) {
    if work.encoded.load(Ordering::Acquire) == work.revision {
        return;
    }
    let Some(irr) = buffers.get(&targets.irradiance) else {
        return;
    };
    let views: Vec<_> = targets
        .images
        .iter()
        .filter_map(|h| images.get(h))
        .collect();
    if views.len() != 3 {
        return;
    }
    let diagnostics = context.diagnostic_recorder();
    let recorder = diagnostics.as_deref();
    let compute_span = recorder.time_span(context.command_encoder(), "tidewater_atmosphere");
    let params: Vec<_> = targets
        .params
        .iter()
        .flat_map(Vec4::to_array)
        .flat_map(f32::to_le_bytes)
        .collect();
    encode(
        &input,
        &targets,
        &mut prepared,
        &params,
        work.mask,
        &device,
        context.command_encoder(),
        &views,
        irr,
    );
    compute_span.end(context.command_encoder());
    targets.ready.store(true, Ordering::Release);
    work.encoded.store(work.revision, Ordering::Release);
}

pub(crate) fn ensure_registered(world: &World) -> Result<(), String> {
    if !world.contains_resource::<Registered>()
        || !world.contains_resource::<Assets<Image>>()
        || !world.contains_resource::<Assets<ShaderBuffer>>()
    {
        return Err("atmosphere must be registered before staging".into());
    }
    Ok(())
}

/// Rebuild observer-dependent LUTs before an acquired cloud frame on this device.
/// Static scattering parameters must match the resident atmosphere exactly.
#[allow(clippy::too_many_arguments)] // Observer commands use the existing device and generation cache.
pub(crate) fn encode_observer(
    input: &KernelInput,
    targets: &Targets,
    params: &[u8],
    device: &RenderDevice,
    encoder: &mut gpu::CommandEncoder,
    images: &RenderAssets<GpuImage>,
    buffers: &RenderAssets<GpuShaderBuffer>,
    prepared: &mut Option<prepared::Prepared>,
) -> bool {
    let baseline: Vec<_> = targets
        .params
        .iter()
        .flat_map(Vec4::to_array)
        .flat_map(f32::to_le_bytes)
        .collect();
    if !observer::validate(params, &baseline) {
        return false;
    }
    let Some(irr) = buffers.get(&targets.irradiance) else {
        return false;
    };
    let views: Vec<_> = targets
        .images
        .iter()
        .filter_map(|h| images.get(h))
        .collect();
    if views.len() != 3 {
        return false;
    }
    encode(
        input, targets, prepared, params, 0b1100, device, encoder, &views, irr,
    );
    true
}

#[allow(clippy::too_many_arguments, clippy::too_many_lines)]
fn encode(
    input: &KernelInput,
    targets: &Targets,
    cache: &mut Option<prepared::Prepared>,
    params: &[u8],
    mask: u8,
    device: &RenderDevice,
    encoder: &mut gpu::CommandEncoder,
    views: &[&GpuImage],
    irr: &GpuShaderBuffer,
) {
    let prepared = prepared::get(cache, input, targets, device);
    let uniform = device.create_buffer_with_data(&gpu::BufferInitDescriptor {
        label: Some("native atmosphere inputs"),
        contents: params,
        usage: gpu::BufferUsages::UNIFORM,
    });
    for (index, pipeline) in prepared.pipelines.iter().enumerate() {
        if mask & (1 << index) == 0 {
            continue;
        }
        let g0_entries = if index == 0 {
            vec![]
        } else {
            vec![gpu::BindGroupEntry {
                binding: 2,
                resource: gpu::BindingResource::Sampler(&prepared.sampler),
            }]
        };
        let g0 = device.create_bind_group(
            NAMES[index],
            &pipeline.get_bind_group_layout(0).into(),
            &g0_entries,
        );
        let mut entries = vec![gpu::BindGroupEntry {
            binding: 0,
            resource: uniform.as_entire_binding(),
        }];
        if index > 0 {
            entries.push(gpu::BindGroupEntry {
                binding: 1,
                resource: gpu::BindingResource::TextureView(&views[0].texture_view),
            });
        }
        if index >= 2 {
            entries.push(gpu::BindGroupEntry {
                binding: 2,
                resource: gpu::BindingResource::TextureView(
                    &views[if index == 2 { 1 } else { 2 }].texture_view,
                ),
            });
        }
        let binding = if index < 3 {
            u32::try_from(index + 1).expect("three LUTs")
        } else {
            3
        };
        let resource = if index < 3 {
            gpu::BindingResource::TextureView(&views[index].texture_view)
        } else {
            irr.buffer.as_entire_binding()
        };
        entries.push(gpu::BindGroupEntry { binding, resource });
        let g1 = device.create_bind_group(
            NAMES[index],
            &pipeline.get_bind_group_layout(1).into(),
            &entries,
        );
        let mut pass = encoder.begin_compute_pass(&gpu::ComputePassDescriptor {
            label: Some(NAMES[index]),
            ..default()
        });
        pass.set_pipeline(pipeline);
        pass.set_bind_group(0, &*g0, &[]);
        pass.set_bind_group(1, &*g1, &[]);
        let (x, y) = if index < 3 {
            let (w, h) = SIZES[index];
            (w.div_ceil(8), h.div_ceil(8))
        } else {
            (1, 1)
        };
        pass.dispatch_workgroups(x, y, 1);
    }
}
