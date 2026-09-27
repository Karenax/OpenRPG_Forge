//! Ordered Tidewater atmosphere kernels on Bevy's existing render device.
//! Callers supply verified WGSL and parameters; no filesystem or network access.
#![allow(clippy::needless_pass_by_value)]
mod acquisition;
pub use acquisition::from_acquisition;
use bevy::{
    asset::RenderAssetUsages,
    core_pipeline::schedule::camera_driver,
    prelude::*,
    render::{
        RenderApp,
        extract_resource::{ExtractResource, ExtractResourcePlugin},
        render_asset::RenderAssets,
        render_resource as gpu,
        renderer::{RenderContext, RenderDevice, RenderGraph},
        storage::{GpuShaderBuffer, ShaderBuffer},
        texture::GpuImage,
    },
};
use std::sync::{
    Arc,
    atomic::{AtomicBool, Ordering},
};
const NAMES: [&str; 4] = ["transmittance", "multiScat", "skyView", "irradiance"];
const SIZES: [(u32, u32); 3] = [(256, 64), (32, 32), (192, 108)];
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
struct KernelInput(Arc<Input>);
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
    ));
    app.sub_app_mut(RenderApp).add_systems(
        RenderGraph,
        compute
            .before(camera_driver)
            .run_if(resource_exists::<KernelInput>)
            .run_if(resource_exists::<Targets>),
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
    Ok(targets)
}
#[allow(clippy::too_many_lines)] // Four ordered kernels share native LUTs and one parameter block.
fn compute(
    mut context: RenderContext,
    input: Res<KernelInput>,
    targets: Res<Targets>,
    device: Res<RenderDevice>,
    images: Res<RenderAssets<GpuImage>>,
    buffers: Res<RenderAssets<GpuShaderBuffer>>,
) {
    if targets.ready.load(Ordering::Acquire) {
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
    let input = &input.0;
    let uniform = device.create_buffer_with_data(&gpu::BufferInitDescriptor {
        label: Some("native atmosphere inputs"),
        contents: &input.params,
        usage: gpu::BufferUsages::UNIFORM,
    });
    let sampler = device.create_sampler(&gpu::SamplerDescriptor {
        mag_filter: gpu::FilterMode::Linear,
        min_filter: gpu::FilterMode::Linear,
        ..default()
    });
    for (index, source) in input.shaders.iter().enumerate() {
        let shader = device.create_and_validate_shader_module(gpu::ShaderModuleDescriptor {
            label: Some(NAMES[index]),
            source: gpu::ShaderSource::Wgsl(source.as_str().into()),
        });
        let pipeline = device.create_compute_pipeline(&gpu::RawComputePipelineDescriptor {
            label: Some(NAMES[index]),
            layout: None,
            module: &shader,
            entry_point: Some("main"),
            compilation_options: default(),
            cache: None,
        });
        let g0_entries = if index == 0 {
            vec![]
        } else {
            vec![gpu::BindGroupEntry {
                binding: 2,
                resource: gpu::BindingResource::Sampler(&sampler),
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
        let mut pass = context
            .command_encoder()
            .begin_compute_pass(&gpu::ComputePassDescriptor {
                label: Some(NAMES[index]),
                ..default()
            });
        pass.set_pipeline(&pipeline);
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
    targets.ready.store(true, Ordering::Release);
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
