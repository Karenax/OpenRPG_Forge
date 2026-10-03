// SPDX-License-Identifier: MIT
// Adapted from Tidewater OceanFFT.js, copyright 2026 DRG Software Solutions LLC.
// Revision 1438b1abfcaee3267092b75573014f4d9b4a983c; third_party/tidewater/LICENSE.
//! Resident four-cascade FFT on Bevy's existing render device.
//! The verified source package is transport-neutral; this module owns only GPU derivatives.
#![allow(clippy::needless_pass_by_value)]

use crate::ocean_spectrum::{OceanFrame, OceanSpectrum};
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
use forge_assets::tidewater_ocean::OceanPackage;
use serde_json::Value;
use std::sync::{
    Arc,
    atomic::{AtomicBool, AtomicU32, Ordering},
};

pub mod live_clock;

const NAMES: [&str; 9] = [
    "initSpectrum",
    "conjugate",
    "copyH0",
    "row",
    "column",
    "mipA-0",
    "mipA-1",
    "mipB-0",
    "mipB-1",
];
const TOTAL: usize = 256 * 256 * 4;
const BOOTSTRAP_STEPS: u32 = 120;
const TIME_SCALE: f64 = 1.5;

/// Downstream water and shore passes can consume these images after the FFT submission.
#[derive(SystemSet, Debug, Clone, Copy, PartialEq, Eq, Hash)]
pub struct OceanComputeSet;

struct Input {
    shaders: [String; 9],
    params: [u8; 224],
}

/// CPU validated source inputs, without any GPU or filesystem handle.
pub struct PreparedOcean(Input);

impl PreparedOcean {
    /// Match the native probe's exact initial and 120-step source uniforms.
    pub fn new(package: &OceanPackage) -> Result<Self, String> {
        let file = |name: &str| {
            package
                .file(name)
                .ok_or_else(|| format!("Ocean input missing: {name}"))
        };
        let metadata: Value =
            serde_json::from_slice(file("ocean.json")?).map_err(|error| error.to_string())?;
        let options: OceanSpectrum = metadata
            .get("simulation_options")
            .map(|value| serde_json::from_value(value.clone()))
            .transpose()
            .map_err(|error| error.to_string())?
            .unwrap_or_default();
        let frame: OceanFrame = metadata
            .get("simulation_frame")
            .map(|value| serde_json::from_value(value.clone()))
            .transpose()
            .map_err(|error| error.to_string())?
            .unwrap_or_default();
        if options.cascades != 4 {
            return Err("Ocean source requires four cascades".into());
        }
        let params = options.uniform_bytes(frame).map_err(str::to_owned)?;
        if file("params-initial.bin")? != params {
            return Err("Ocean initial uniforms differ from source".into());
        }
        let mut expected = params;
        let mut time = 0.0_f64;
        for _ in 0..BOOTSTRAP_STEPS {
            time += 1.0 / 60.0;
        }
        #[allow(clippy::cast_possible_truncation)]
        expected[212..216].copy_from_slice(&(time as f32).to_le_bytes());
        if file("frame-120-params.bin")? != expected {
            return Err("Ocean 120-step uniforms differ from source".into());
        }
        let shaders = NAMES
            .map(|name| {
                std::str::from_utf8(file(&format!("{name}.wgsl"))?)
                    .map(str::to_owned)
                    .map_err(|error| error.to_string())
            })
            .into_iter()
            .collect::<Result<Vec<_>, String>>()?
            .try_into()
            .map_err(|_| "Ocean kernel count mismatch".to_owned())?;
        Ok(Self(Input { shaders, params }))
    }

    /// Publish a new generation in the existing renderer. Previous generations remain
    /// referenced only by consumers holding their old handles.
    pub fn stage(self, world: &mut World) -> Result<Targets, String> {
        if !world.contains_resource::<Registered>()
            || !world.contains_resource::<Assets<Image>>()
            || !world.contains_resource::<Assets<ShaderBuffer>>()
        {
            return Err("Ocean renderer must be registered before staging".into());
        }
        let targets = create_targets(world, &self.0.params);
        let clock = live_clock::LiveClock::default();
        clock.set_time_scale(TIME_SCALE).map_err(str::to_owned)?;
        world.insert_resource(StagedInput(Arc::new(self.0)));
        world.insert_resource(targets.clone());
        world.insert_resource(Clock(clock));
        Ok(targets)
    }
}

#[derive(Resource, Clone, ExtractResource)]
struct StagedInput(Arc<Input>);

#[derive(Resource, Clone, ExtractResource)]
pub(crate) struct Clock(live_clock::LiveClock);

impl Clock {
    pub(crate) fn completed(&self, step: u32) -> Option<live_clock::LiveStep> {
        self.0.completed(step)
    }
}

#[derive(Resource, Clone, ExtractResource)]
pub struct Targets {
    pub completed_steps: Arc<AtomicU32>,
    pub ready: Arc<AtomicBool>,
    pub images: Vec<Handle<Image>>,
    pub sizes: [Vec4; 4],
    pub parameters: [UVec4; 14],
    buffers: Vec<Handle<ShaderBuffer>>,
}

/// Last coherent FFT simulation timestamp for visible water materials.
pub fn completed_frame(world: &World) -> Option<live_clock::LiveStep> {
    let step = world
        .get_resource::<Targets>()?
        .completed_steps
        .load(Ordering::Acquire);
    world.get_resource::<Clock>()?.0.completed(step)
}

/// Shared timeline owner for a bounded pause/resume of the resident simulation.
#[must_use]
pub fn live_clock(world: &World) -> Option<live_clock::LiveClock> {
    world.get_resource::<Clock>().map(|clock| clock.0.clone())
}

#[derive(Resource)]
struct Registered;

/// Register once after Bevy's render plugins. Inputs may arrive after startup.
pub fn register(app: &mut App) -> Result<(), String> {
    if app.world().contains_resource::<Registered>() {
        return Ok(());
    }
    if !app.world().contains_resource::<Assets<Image>>()
        || !app.world().contains_resource::<Assets<ShaderBuffer>>()
        || app.get_sub_app(RenderApp).is_none()
    {
        return Err("Ocean renderer absent".into());
    }
    app.add_plugins((
        ExtractResourcePlugin::<StagedInput>::default(),
        ExtractResourcePlugin::<Targets>::default(),
        ExtractResourcePlugin::<Clock>::default(),
    ));
    app.add_systems(Update, offer_live_frame);
    app.sub_app_mut(RenderApp).add_systems(
        RenderGraph,
        compute
            .in_set(OceanComputeSet)
            .before(camera_driver)
            .run_if(resource_exists::<StagedInput>)
            .run_if(resource_exists::<Targets>)
            .run_if(resource_exists::<Clock>),
    );
    app.insert_resource(Registered);
    Ok(())
}

fn offer_live_frame(
    time: Res<Time<Real>>,
    targets: Option<Res<Targets>>,
    clock: Option<Res<Clock>>,
) {
    let (Some(targets), Some(clock)) = (targets, clock) else {
        return;
    };
    if targets.ready.load(Ordering::Acquire) {
        clock.0.offer(time.elapsed_secs_f64());
    }
}

fn create_targets(world: &mut World, params: &[u8; 224]) -> Targets {
    let buffers = [
        TOTAL * 4,
        TOTAL * 4,
        TOTAL * 8,
        TOTAL,
        TOTAL * 8,
        64 * 4 * 8,
    ]
    .into_iter()
    .map(|count| {
        world
            .resource_mut::<Assets<ShaderBuffer>>()
            .add(ShaderBuffer::from(vec![0.0_f32; count]))
    })
    .collect();
    let images = (0..2)
        .map(|_| {
            let mut image = Image::new_uninit(
                gpu::Extent3d {
                    width: 256,
                    height: 256,
                    depth_or_array_layers: 4,
                },
                gpu::TextureDimension::D2,
                gpu::TextureFormat::Rgba16Float,
                RenderAssetUsages::RENDER_WORLD,
            );
            image.texture_descriptor.mip_level_count = 9;
            image.texture_descriptor.usage = gpu::TextureUsages::STORAGE_BINDING
                | gpu::TextureUsages::TEXTURE_BINDING
                | gpu::TextureUsages::COPY_SRC;
            image.sampler =
                bevy::image::ImageSampler::Descriptor(bevy::image::ImageSamplerDescriptor {
                    address_mode_u: bevy::image::ImageAddressMode::Repeat,
                    address_mode_v: bevy::image::ImageAddressMode::Repeat,
                    min_filter: bevy::image::ImageFilterMode::Linear,
                    mag_filter: bevy::image::ImageFilterMode::Linear,
                    mipmap_filter: bevy::image::ImageFilterMode::Linear,
                    ..default()
                });
            world.resource_mut::<Assets<Image>>().add(image)
        })
        .collect();
    Targets {
        completed_steps: Arc::new(AtomicU32::new(0)),
        ready: Arc::new(AtomicBool::new(false)),
        images,
        sizes: std::array::from_fn(|i| {
            Vec4::new(
                f32::from_le_bytes(params[i * 16..i * 16 + 4].try_into().expect("ocean size")),
                0.0,
                0.0,
                0.0,
            )
        }),
        parameters: std::array::from_fn(|i| {
            UVec4::from_array(std::array::from_fn(|j| {
                u32::from_le_bytes(
                    params[i * 16 + j * 4..i * 16 + j * 4 + 4]
                        .try_into()
                        .expect("ocean parameter"),
                )
            }))
        }),
        buffers,
    }
}

#[derive(Default)]
struct Execution {
    generation: Option<Arc<AtomicBool>>,
    next: u32,
    time: f64,
    pipelines: Vec<gpu::ComputePipeline>,
}

#[allow(clippy::too_many_arguments, clippy::too_many_lines)]
fn compute(
    mut context: RenderContext,
    staged: Res<StagedInput>,
    targets: Res<Targets>,
    clock: Res<Clock>,
    device: Res<RenderDevice>,
    buffers: Res<RenderAssets<GpuShaderBuffer>>,
    images: Res<RenderAssets<GpuImage>>,
    mut execution: Local<Execution>,
) {
    if !execution
        .generation
        .as_ref()
        .is_some_and(|generation| Arc::ptr_eq(generation, &targets.ready))
    {
        *execution = Execution {
            generation: Some(targets.ready.clone()),
            ..default()
        };
    }
    let gpu: Vec<_> = targets
        .buffers
        .iter()
        .filter_map(|handle| buffers.get(handle))
        .collect();
    let textures: Vec<_> = targets
        .images
        .iter()
        .filter_map(|handle| images.get(handle))
        .collect();
    if gpu.len() != targets.buffers.len() || textures.len() != 2 {
        return;
    }
    let live = if targets.ready.load(Ordering::Acquire) {
        let Some(frame) = clock.0.pending(execution.next) else {
            return;
        };
        Some(frame)
    } else {
        None
    };
    if !targets.ready.load(Ordering::Acquire) && execution.next > BOOTSTRAP_STEPS {
        return;
    }
    let views: Vec<Vec<_>> = textures
        .iter()
        .map(|texture| {
            (0..9)
                .map(|mip| {
                    texture.texture.create_view(&gpu::TextureViewDescriptor {
                        dimension: Some(gpu::TextureViewDimension::D2Array),
                        base_mip_level: mip,
                        mip_level_count: Some(1),
                        ..default()
                    })
                })
                .collect()
        })
        .collect();
    if execution.pipelines.is_empty() {
        execution.pipelines = staged
            .0
            .shaders
            .iter()
            .enumerate()
            .map(|(index, source)| {
                let shader =
                    device.create_and_validate_shader_module(gpu::ShaderModuleDescriptor {
                        label: Some(NAMES[index]),
                        source: gpu::ShaderSource::Wgsl(source.as_str().into()),
                    });
                device.create_compute_pipeline(&gpu::RawComputePipelineDescriptor {
                    label: Some(NAMES[index]),
                    layout: None,
                    module: &shader,
                    entry_point: Some("main"),
                    compilation_options: default(),
                    cache: None,
                })
            })
            .collect();
    }
    let step = execution.next;
    let dt = live.map_or(1.0_f32 / 60.0, |frame| frame.dt);
    let mut frame_bytes = [0_u8; 720];
    frame_bytes[584..588].copy_from_slice(&dt.to_le_bytes());
    let frame_uniform = device.create_buffer_with_data(&gpu::BufferInitDescriptor {
        label: Some("tidewater ocean frame dt"),
        contents: &frame_bytes,
        usage: gpu::BufferUsages::UNIFORM,
    });
    let mut params = staged.0.params;
    if let Some(frame) = live {
        debug_assert_eq!(step, frame.step);
        execution.time = frame.time;
    } else if step > 0 {
        execution.time += 1.0 / 60.0;
    }
    #[allow(clippy::cast_possible_truncation)]
    params[212..216].copy_from_slice(&(execution.time as f32).to_le_bytes());
    let uniform = device.create_buffer_with_data(&gpu::BufferInitDescriptor {
        label: Some("tidewater ocean step"),
        contents: &params,
        usage: gpu::BufferUsages::UNIFORM,
    });
    let range = if step == 0 { 0..3 } else { 3..9 };
    for index in range {
        let pipeline = &execution.pipelines[index];
        let frame_entries = if index == 4 {
            vec![gpu::BindGroupEntry {
                binding: 0,
                resource: frame_uniform.as_entire_binding(),
            }]
        } else {
            vec![]
        };
        let group0 = device.create_bind_group(
            NAMES[index],
            &pipeline.get_bind_group_layout(0).into(),
            &frame_entries,
        );
        let buffer_binding = |i: usize| gpu[i].buffer.as_entire_binding();
        let texture_binding =
            |texture: usize, mip: usize| gpu::BindingResource::TextureView(&views[texture][mip]);
        let resources = match index {
            0 => vec![
                uniform.as_entire_binding(),
                buffer_binding(0),
                buffer_binding(1),
            ],
            1 => vec![buffer_binding(0), buffer_binding(2)],
            2 => vec![buffer_binding(0), buffer_binding(2), buffer_binding(3)],
            3 => vec![
                uniform.as_entire_binding(),
                buffer_binding(0),
                buffer_binding(1),
                buffer_binding(2),
            ],
            4 => vec![
                uniform.as_entire_binding(),
                buffer_binding(2),
                buffer_binding(3),
                buffer_binding(4),
                texture_binding(0, 0),
                texture_binding(1, 0),
            ],
            5 | 6 => {
                let texture = index - 5;
                vec![
                    buffer_binding(4),
                    buffer_binding(5),
                    texture_binding(texture, 1),
                    texture_binding(texture, 2),
                    texture_binding(texture, 3),
                    texture_binding(texture, 4),
                    texture_binding(texture, 5),
                ]
            }
            _ => {
                let texture = index - 7;
                vec![
                    buffer_binding(5),
                    texture_binding(texture, 6),
                    texture_binding(texture, 7),
                    texture_binding(texture, 8),
                ]
            }
        };
        let entries: Vec<_> = resources
            .into_iter()
            .enumerate()
            .map(|(binding, resource)| gpu::BindGroupEntry {
                binding: u32::try_from(binding).expect("small ocean binding index"),
                resource,
            })
            .collect();
        let group1 = device.create_bind_group(
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
        pass.set_pipeline(pipeline);
        pass.set_bind_group(0, &*group0, &[]);
        pass.set_bind_group(1, &*group1, &[]);
        let (x, y, z) = match index {
            0..=2 => (16, 16, 4),
            3 | 4 => (256, 4, 1),
            5 | 6 => (8, 8, 4),
            _ => (1, 1, 4),
        };
        pass.dispatch_workgroups(x, y, z);
    }
    targets.completed_steps.store(step, Ordering::Release);
    execution.next = step + 1;
    if step == BOOTSTRAP_STEPS {
        targets.ready.store(true, Ordering::Release);
    }
    if live.is_some() {
        clock.0.acknowledge(step);
    }
}
