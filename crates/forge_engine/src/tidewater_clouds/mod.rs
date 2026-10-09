//! Bounded Tidewater cloud compute graph on Bevy's existing render device.
//! Verified shader/data acquisition is owned by the caller. No filesystem access.
#![allow(clippy::needless_pass_by_value)]
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
        storage::GpuShaderBuffer,
        texture::GpuImage,
    },
};
use std::sync::{
    Arc,
    atomic::{AtomicBool, AtomicU32, Ordering},
};
pub mod acquisition_controller;
pub mod live;
pub mod quality;
pub mod sampling;
pub(crate) mod validation;
use validation::validate;
pub mod shadow;
pub mod trace;
#[derive(Resource)]
pub struct Input {
    pub stepped: bool,
    pub shaders: [String; 2],
    pub settings: Vec<u8>,
    pub shadow: Option<shadow::Input>,
}
#[derive(Resource, Clone)]
pub struct SharedAtmosphere {
    pub images: Vec<Handle<Image>>,
    pub ready: Arc<AtomicBool>,
    pub params: [Vec4; 4],
}
#[derive(Resource, Clone, ExtractResource)]
pub struct Targets {
    pub completed_steps: Arc<AtomicU32>,
    pub submissions: Arc<AtomicU32>,
    pub images: Vec<Handle<Image>>,
    pub ready: Arc<AtomicBool>,
}
#[derive(SystemSet, Debug, Clone, Copy, PartialEq, Eq, Hash)]
pub struct CloudCompute;
const SIZES: [u32; 10] = [1024, 64, 256, 240, 240, 960, 960, 960, 960, 512];
const HEIGHTS: [u32; 10] = [1024, 64, 256, 135, 135, 540, 540, 540, 540, 160];
const NAMES: [&str; 10] = [
    "weather", "bounds", "shadow", "radiance", "metadata", "color0", "meta0", "color1", "meta1",
    "panorama",
];
#[derive(Default)]
struct Execution {
    generation: Option<Arc<AtomicBool>>,
    next: usize,
    settings: Option<bevy::render::render_resource::Buffer>,
    shadow: Option<shadow::Cache>,
    observer: Option<Vec<u8>>,
    atmosphere_prepared: Option<crate::tidewater_atmosphere::prepared::Prepared>,
}
#[allow(clippy::struct_excessive_bools)]
pub(super) struct FrameView<'a> {
    pub step: u32,
    pub final_acquired_frame: bool,
    pub local: &'a [u8],
    pub global: &'a [u8],
    pub atmosphere: Option<&'a [u8]>,
    pub shadow_full: bool,
    pub panorama_full: bool,
    pub trace_view: bool,
    pub view_index: usize,
}
#[allow(clippy::too_many_lines, clippy::too_many_arguments)]
fn compute(
    mut context: RenderContext,
    mut execution: Local<Execution>,
    staged: Res<StagedInput>,
    targets: Res<Targets>,
    device: Res<RenderDevice>,
    images: Res<RenderAssets<GpuImage>>,
    queue: Res<bevy::render::renderer::RenderQueue>,
    pending: Res<live::Pending>,
    mut sampling: ResMut<sampling::Sampling>,
    atmosphere_input: Option<Res<crate::tidewater_atmosphere::KernelInput>>,
    atmosphere_targets: Option<Res<crate::tidewater_atmosphere::Targets>>,
    buffers: Option<Res<RenderAssets<GpuShaderBuffer>>>,
) {
    let live = pending.0.as_deref().filter(|f| {
        targets.ready.load(Ordering::Acquire)
            && Some(f.step)
                == targets
                    .completed_steps
                    .load(Ordering::Acquire)
                    .checked_add(1)
    });
    if targets.ready.load(Ordering::Acquire) && live.is_none() {
        return;
    }
    if !execution
        .generation
        .as_ref()
        .is_some_and(|g| Arc::ptr_eq(g, &targets.ready))
    {
        sampling.reset();
        *execution = Execution {
            generation: Some(targets.ready.clone()),
            ..default()
        };
    }
    let input = &staged.input;
    let atmosphere = &staged.atmosphere;
    let views: Vec<_> = targets
        .images
        .iter()
        .filter_map(|h| images.get(h))
        .collect();
    if views.len() != targets.images.len() {
        return;
    }
    let atmosphere_views = if let Some(atmosphere) = &atmosphere {
        if !atmosphere.ready.load(Ordering::Acquire) {
            return;
        }
        let (Some(transmittance), Some(sky)) = (
            images.get(&atmosphere.images[0]),
            images.get(&atmosphere.images[2]),
        ) else {
            return;
        };
        Some([&*transmittance.texture_view, &*sky.texture_view])
    } else {
        None
    };
    if live.is_none()
        && let Some(observers) = input
            .shadow
            .as_ref()
            .and_then(|shadow| shadow.atmosphere_frames.as_ref())
    {
        let Some(params) = observers.get(execution.next) else {
            return;
        };
        let (Some(kernel), Some(target), Some(buffers), Some(shared)) = (
            atmosphere_input.as_ref(),
            atmosphere_targets.as_ref(),
            buffers.as_ref(),
            atmosphere.as_ref(),
        ) else {
            return;
        };
        if !Arc::ptr_eq(&shared.ready, &target.ready) {
            return;
        }
        if execution.observer.as_deref() != Some(params.as_slice()) {
            if !crate::tidewater_atmosphere::encode_observer(
                kernel,
                target,
                params,
                &device,
                context.command_encoder(),
                &images,
                buffers,
                &mut execution.atmosphere_prepared,
            ) {
                return;
            }
            execution.observer = Some(params.clone());
        }
    }
    let diagnostics = context.diagnostic_recorder();
    let recorder = diagnostics.as_deref();
    let compute_span = recorder.time_span(context.command_encoder(), "tidewater_clouds");
    let Execution {
        next,
        settings,
        shadow: shadow_cache,
        ..
    } = &mut *execution;
    let settings = settings.get_or_insert_with(|| {
        device.create_buffer_with_data(&gpu::BufferInitDescriptor {
            label: Some("source cloud settings"),
            contents: &input.settings,
            usage: gpu::BufferUsages::UNIFORM,
        })
    });
    if *next == 0 {
        for index in 0..2 {
            let shader = device.create_and_validate_shader_module(gpu::ShaderModuleDescriptor {
                label: Some(NAMES[index]),
                source: gpu::ShaderSource::Wgsl(input.shaders[index].as_str().into()),
            });
            let pipeline = device.create_compute_pipeline(&gpu::RawComputePipelineDescriptor {
                label: Some(NAMES[index]),
                layout: None,
                module: &shader,
                entry_point: Some("main"),
                compilation_options: default(),
                cache: None,
            });
            let empty = device.create_bind_group(
                "cloud empty group",
                &pipeline.get_bind_group_layout(0).into(),
                &[],
            );
            let entries = if index == 0 {
                vec![
                    gpu::BindGroupEntry {
                        binding: 1,
                        resource: settings.as_entire_binding(),
                    },
                    gpu::BindGroupEntry {
                        binding: 2,
                        resource: gpu::BindingResource::TextureView(&views[0].texture_view),
                    },
                ]
            } else {
                vec![
                    gpu::BindGroupEntry {
                        binding: 3,
                        resource: gpu::BindingResource::TextureView(&views[0].texture_view),
                    },
                    gpu::BindGroupEntry {
                        binding: 4,
                        resource: gpu::BindingResource::TextureView(&views[1].texture_view),
                    },
                ]
            };
            let bindings = device.create_bind_group(
                NAMES[index],
                &pipeline.get_bind_group_layout(1).into(),
                &entries,
            );
            let mut pass =
                context
                    .command_encoder()
                    .begin_compute_pass(&gpu::ComputePassDescriptor {
                        label: Some(NAMES[index]),
                        ..default()
                    });
            pass.set_pipeline(&pipeline);
            pass.set_bind_group(0, &*empty, &[]);
            pass.set_bind_group(1, &*bindings, &[]);
            pass.dispatch_workgroups(SIZES[index] / 8, SIZES[index] / 8, 1);
        }
    }
    let bootstrap_frames = input
        .shadow
        .as_ref()
        .map_or(120, |shadow| shadow.frames.len());
    let end = if let Some(frame) = live {
        frame.step as usize
    } else if input.stepped {
        (*next + 1).min(bootstrap_frames)
    } else {
        bootstrap_frames
    };
    if let Some(shadow) = &input.shadow {
        let frames: Vec<_> = if let Some(frame) = live {
            vec![frame.view()]
        } else {
            shadow
                .frames
                .iter()
                .enumerate()
                .skip(*next)
                .take(end - *next)
                .map(|(index, (local, global))| FrameView {
                    step: u32::try_from(index + 1).expect("bounded cloud bootstrap"),
                    final_acquired_frame: index + 1 == bootstrap_frames,
                    local,
                    global,
                    atmosphere: shadow
                        .atmosphere_frames
                        .as_ref()
                        .map(|params| params[index].as_slice()),
                    shadow_full: shadow
                        .schedule
                        .as_ref()
                        .map_or(index == 0, |s| s[index].shadow_full),
                    panorama_full: shadow
                        .schedule
                        .as_ref()
                        .map_or(index == 0, |s| s[index].panorama_full),
                    trace_view: shadow.schedule.as_ref().is_none_or(|s| s[index].trace_view),
                    view_index: shadow
                        .schedule
                        .as_ref()
                        .map_or(index % 2, |s| s[index].view_index),
                })
                .collect()
        };
        // Immutable per-submission settings preserve already queued frames.
        let live_settings = live.map(|frame| {
            device.create_buffer_with_data(&gpu::BufferInitDescriptor {
                label: Some("live cloud settings"),
                contents: &frame.settings,
                usage: gpu::BufferUsages::UNIFORM,
            })
        });
        let encoded = shadow.encode(
            &device,
            &queue,
            context.command_encoder(),
            live_settings.as_ref().unwrap_or(settings),
            &views[0].texture_view,
            &views[2].texture_view,
            (views.len() >= 5).then(|| {
                [
                    &*views[1].texture_view,
                    &*views[3].texture_view,
                    &*views[4].texture_view,
                ]
            }),
            (views.len() == 10).then(|| {
                [
                    &*views[5].texture_view,
                    &*views[6].texture_view,
                    &*views[7].texture_view,
                    &*views[8].texture_view,
                    &*views[9].texture_view,
                ]
            }),
            atmosphere_views,
            &frames,
            shadow_cache,
        );
        if let Some(encoded) = encoded {
            let _ = sampling.record(encoded, &targets);
        }
    }
    compute_span.end(context.command_encoder());
    *next = end;
    targets.completed_steps.store(
        u32::try_from(end).expect("bounded cloud step"),
        Ordering::Release,
    );
    targets.submissions.fetch_add(1, Ordering::AcqRel);
    if end == bootstrap_frames {
        targets.ready.store(true, Ordering::Release);
    }
}
#[derive(Resource, Clone, ExtractResource)]
struct StagedInput {
    input: Arc<Input>,
    atmosphere: Option<SharedAtmosphere>,
    quality: quality::CloudTraceQuality,
}
#[derive(Resource)]
struct Registered;

/// Register the graph once after Bevy render plugins, without requiring inputs.
pub fn register(app: &mut App) -> Result<(), String> {
    if app.world().contains_resource::<Registered>() {
        return Ok(());
    }
    if !app.world().contains_resource::<Assets<Image>>() || app.get_sub_app(RenderApp).is_none() {
        return Err("cloud renderer absent".into());
    }
    app.add_plugins((
        ExtractResourcePlugin::<StagedInput>::default(),
        ExtractResourcePlugin::<Targets>::default(),
        ExtractResourcePlugin::<live::Pending>::default(),
    ));
    app.sub_app_mut(RenderApp)
        .init_resource::<sampling::Sampling>()
        .add_systems(
            RenderGraph,
            compute
                .in_set(CloudCompute)
                .after(crate::tidewater_atmosphere::AtmosphereCompute)
                .in_set(RenderGraphSystems::Render)
                .before(camera_driver)
                .run_if(resource_exists::<StagedInput>)
                .run_if(resource_exists::<Targets>),
        );
    app.insert_resource(Registered);
    Ok(())
}

pub fn install(app: &mut App, input: Input, count: usize) -> Result<Targets, String> {
    let shared = app
        .get_sub_app(RenderApp)
        .and_then(|r| r.world().get_resource::<SharedAtmosphere>())
        .cloned();
    validate_shared(&input, count, shared.as_ref())?;
    if app.world().contains_resource::<Targets>() {
        return Err("cloud renderer already installed".into());
    }
    register(app)?;
    stage(app.world_mut(), input, count, shared)
}

fn validate_shared(
    input: &Input,
    count: usize,
    shared: Option<&SharedAtmosphere>,
) -> Result<(), String> {
    if shared.is_some_and(|a| a.images.len() != 3) {
        return Err("invalid shared cloud atmosphere handles".into());
    }
    validate(input, count, shared.map(|a| &a.params))
}

/// Replace a generation and its atmosphere together. Invalid inputs preserve existing resources.
/// Consumers adopt the returned handles; temporal GPU history restarts from step zero.
pub fn stage(
    world: &mut World,
    input: Input,
    count: usize,
    shared: Option<SharedAtmosphere>,
) -> Result<Targets, String> {
    validate_shared(&input, count, shared.as_ref())?;
    ensure_registered(world)?;
    let quality = world
        .get_resource::<quality::CloudTraceQuality>()
        .copied()
        .unwrap_or_default();
    let mut input = input;
    quality.prepare(&mut input)?;
    let images = SIZES
        .into_iter()
        .take(count)
        .enumerate()
        .map(|(index, size)| {
            let [width, height] = match index {
                3 | 4 => quality.source_size(),
                9 => quality.panorama_size(),
                _ => [size, HEIGHTS[index]],
            };
            let mut image = Image::new_uninit(
                gpu::Extent3d {
                    width,
                    height,
                    depth_or_array_layers: 1,
                },
                gpu::TextureDimension::D2,
                match index {
                    0 | 1 => gpu::TextureFormat::Rgba8Unorm,
                    2 => gpu::TextureFormat::R32Float,
                    _ => gpu::TextureFormat::Rgba16Float,
                },
                RenderAssetUsages::RENDER_WORLD,
            );
            image.texture_descriptor.usage = gpu::TextureUsages::STORAGE_BINDING
                | gpu::TextureUsages::TEXTURE_BINDING
                | gpu::TextureUsages::COPY_SRC;
            image.sampler =
                bevy::image::ImageSampler::Descriptor(bevy::image::ImageSamplerDescriptor {
                    min_filter: bevy::image::ImageFilterMode::Linear,
                    mag_filter: bevy::image::ImageFilterMode::Linear,
                    address_mode_u: if index == 9 {
                        bevy::image::ImageAddressMode::Repeat
                    } else {
                        bevy::image::ImageAddressMode::ClampToEdge
                    },
                    address_mode_v: if index == 9 {
                        bevy::image::ImageAddressMode::Repeat
                    } else {
                        bevy::image::ImageAddressMode::ClampToEdge
                    },
                    ..default()
                });
            world.resource_mut::<Assets<Image>>().add(image)
        })
        .collect();
    let targets = Targets {
        completed_steps: Arc::new(AtomicU32::new(0)),
        submissions: Arc::new(AtomicU32::new(0)),
        images,
        ready: Arc::new(AtomicBool::new(false)),
    };
    world.insert_resource(StagedInput {
        input: Arc::new(input),
        atmosphere: shared,
        quality,
    });
    world.insert_resource(targets.clone());
    world.insert_resource(live::Pending::default());
    Ok(targets)
}

pub(crate) fn ensure_registered(world: &World) -> Result<(), String> {
    if !world.contains_resource::<Registered>() || !world.contains_resource::<Assets<Image>>() {
        return Err("cloud renderer must be registered before staging".into());
    }
    Ok(())
}
