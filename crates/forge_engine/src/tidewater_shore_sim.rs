// SPDX-License-Identifier: MIT
// Adapted from Tidewater ShoreSim.js, copyright 2026 DRG Software Solutions LLC.
// Revision 1438b1abfcaee3267092b75573014f4d9b4a983c; third_party/tidewater/LICENSE.
//! Resident source `ShoreSim` on Bevy's render device, with authored-terrain derivatives.
#![allow(clippy::too_many_lines)]

use crate::{tidewater_ocean, tidewater_shore_field, tidewater_terrain_edits::Corrections};
use bevy::{
    asset::RenderAssetUsages,
    core_pipeline::schedule::camera_driver,
    prelude::*,
    render::{
        RenderApp,
        extract_resource::{ExtractResource, ExtractResourcePlugin},
        render_asset::RenderAssets,
        renderer::{RenderContext, RenderDevice, RenderGraph, RenderQueue},
        texture::GpuImage,
    },
};
use forge_assets::{
    tidewater_shore_sim::ShoreSimPackage, tidewater_water_surface::WaterSurfacePackage,
};
use forge_core::bytes_sha256;
use forge_project::TerrainDocument;
use forge_world::sampled_terrain::SampledTerrainPlan;
use std::sync::{
    Arc,
    atomic::{AtomicBool, AtomicU32, Ordering},
};
use wgpu::util::DeviceExt;

const STATE_SIDE: u32 = 768;
#[derive(SystemSet, Debug, Clone, Copy, PartialEq, Eq, Hash)]
pub struct ShoreComputeSet;
const SHORE_SIDE: u32 = 512;
const HEIGHT_SIDE: u32 = 2048;
const BOOTSTRAP_STEPS: u32 = 120;
const HASH_DEFINITION: &str =
    "fn shoreHash1( x: f32 ) -> f32 { return fract( sin( x * 127.1 + 311.7 ) * 43758.5453 ); }";

struct Input {
    kernel: String,
    heights: Vec<u8>,
    shore: Vec<u8>,
    direction: [u8; 16],
    lace: Vec<Vec<u8>>,
    simulation_params: [u8; 48],
    terrain_params: [u8; 32],
    shore_params: [u8; 64],
    deltas: Vec<u8>,
    delta_side: u32,
}

/// Fully checked CPU input for a new authored terrain generation.
pub struct PreparedShoreSim(Input);

impl PreparedShoreSim {
    pub fn new(
        package: &ShoreSimPackage,
        surface: &WaterSurfacePackage,
        document: &TerrainDocument,
        plan: &SampledTerrainPlan,
        source_heights: &[u8],
    ) -> Result<Self, String> {
        let file = |name: &str| {
            package
                .file(name)
                .ok_or_else(|| format!("Pinned ShoreSim file missing: {name}"))
        };
        if file("heights.f32.bin")? != source_heights
            || document
                .sampled_source
                .as_ref()
                .is_none_or(|source| source.samples_sha256 != bytes_sha256(source_heights))
        {
            return Err("ShoreSim terrain differs from the editable source".into());
        }
        for (shore_name, surface_name) in [
            ("shore-field.rgba32.bin", "shore-field.rgba32.bin"),
            ("shore-params.bin", "uniform-shoreP.bin"),
            ("simulation-params.bin", "uniform-shoreSimP.bin"),
        ] {
            if Some(file(shore_name)?) != surface.file(surface_name) {
                return Err(format!("ShoreSim and optical surface differ: {shore_name}"));
            }
        }
        let source = std::str::from_utf8(file("kernel.wgsl")?).map_err(|e| e.to_string())?;
        if source.matches(HASH_DEFINITION).count() != 1 {
            return Err("Original ShoreSim hash definition changed".into());
        }
        let mut kernel = source.replace(
            HASH_DEFINITION,
            include_str!("../../../scripts/tidewater/native-shore-hash.wgsl"),
        );
        let corrections = Corrections::from_document(document)?;
        let edited = !document.height_deltas.is_empty() || corrections.elevation != 0.0;
        if edited {
            kernel = corrections.shader_existing(&kernel)?;
        }
        let shore = if edited {
            tidewater_shore_field::bake(|x, z| plan.height_at(x, z))
                .map_err(|error| error.to_string())?
                .into_iter()
                .flat_map(f32::to_le_bytes)
                .collect()
        } else {
            file("shore-field.rgba32.bin")?.to_vec()
        };
        let lace = (0..10)
            .map(|mip| file(&format!("lace-m{mip}.rgba8.bin")).map(<[u8]>::to_vec))
            .collect::<Result<Vec<_>, _>>()?;
        Ok(Self(Input {
            kernel,
            heights: source_heights.to_vec(),
            shore,
            direction: file("shore-direction.rgba32.bin")?
                .try_into()
                .map_err(|_| "Shore direction size mismatch")?,
            lace,
            simulation_params: file("simulation-params.bin")?
                .try_into()
                .map_err(|_| "Shore simulation parameter size mismatch")?,
            terrain_params: file("terrain-params.bin")?
                .try_into()
                .map_err(|_| "Shore terrain parameter size mismatch")?,
            shore_params: file("shore-params.bin")?
                .try_into()
                .map_err(|_| "Shore parameter size mismatch")?,
            deltas: corrections.bytes,
            delta_side: corrections.side,
        }))
    }

    #[must_use]
    pub fn shore_field(&self) -> &[u8] {
        &self.0.shore
    }

    /// Publish a fresh output generation; old image handles remain valid until consumers replace them.
    pub fn stage(self, world: &mut World) -> Result<Targets, String> {
        if !world.contains_resource::<Registered>() {
            return Err("ShoreSim renderer not registered".into());
        }
        let mut image = Image::new_fill(
            wgpu::Extent3d {
                width: STATE_SIDE,
                height: STATE_SIDE,
                depth_or_array_layers: 1,
            },
            wgpu::TextureDimension::D2,
            &[0; 8],
            wgpu::TextureFormat::Rgba16Float,
            RenderAssetUsages::RENDER_WORLD,
        );
        image.texture_descriptor.usage = wgpu::TextureUsages::TEXTURE_BINDING
            | wgpu::TextureUsages::COPY_DST
            | wgpu::TextureUsages::COPY_SRC;
        let state = world.resource_mut::<Assets<Image>>().add(image);
        let mut lace = Vec::new();
        for mip in &self.0.lace {
            lace.extend_from_slice(mip);
        }
        let lace =
            world
                .resource_mut::<Assets<Image>>()
                .add(crate::tidewater_plants::textures::mipped(
                    crate::tidewater_plants::textures::Layout {
                        width: SHORE_SIDE,
                        height: SHORE_SIDE,
                        mips: 10,
                    },
                    &lace,
                )?);
        let params = std::array::from_fn(|row| {
            UVec4::from_array(std::array::from_fn(|column| {
                let offset = row * 16 + column * 4;
                u32::from_le_bytes(
                    self.0.simulation_params[offset..offset + 4]
                        .try_into()
                        .expect("fixed ShoreSim parameter size"),
                )
            }))
        });
        let targets = Targets {
            state,
            lace,
            params,
            completed_steps: Arc::new(AtomicU32::new(0)),
            ready: Arc::new(AtomicBool::new(false)),
        };
        world.insert_resource(StagedInput(Arc::new(self.0)));
        world.insert_resource(targets.clone());
        Ok(targets)
    }
}

#[derive(Resource, Clone, ExtractResource)]
struct StagedInput(Arc<Input>);

#[derive(Resource, Clone, ExtractResource)]
pub struct Targets {
    pub state: Handle<Image>,
    pub lace: Handle<Image>,
    pub params: [UVec4; 3],
    pub completed_steps: Arc<AtomicU32>,
    pub ready: Arc<AtomicBool>,
}

#[derive(Resource)]
struct Registered;

pub fn register(app: &mut App) -> Result<(), String> {
    if app.world().contains_resource::<Registered>() {
        return Ok(());
    }
    if app.get_sub_app(RenderApp).is_none() || !app.world().contains_resource::<Assets<Image>>() {
        return Err("ShoreSim requires Bevy's active render device".into());
    }
    app.add_plugins((
        ExtractResourcePlugin::<StagedInput>::default(),
        ExtractResourcePlugin::<Targets>::default(),
    ));
    app.sub_app_mut(RenderApp).add_systems(
        RenderGraph,
        compute
            .in_set(ShoreComputeSet)
            .after(tidewater_ocean::OceanComputeSet)
            .before(camera_driver)
            .run_if(resource_exists::<StagedInput>)
            .run_if(resource_exists::<Targets>)
            .run_if(resource_exists::<tidewater_ocean::Targets>)
            .run_if(resource_exists::<tidewater_ocean::Clock>),
    );
    app.insert_resource(Registered);
    Ok(())
}

struct Simulation {
    state_a: wgpu::Texture,
    state_b: wgpu::Texture,
    _deposit: wgpu::Buffer,
    pipeline: wgpu::ComputePipeline,
    group1: wgpu::BindGroup,
    layout0: wgpu::BindGroupLayout,
    samplers: Vec<wgpu::Sampler>,
}

#[allow(clippy::too_many_lines)]
fn initialize(input: &Input, device: &wgpu::Device, queue: &RenderQueue) -> Simulation {
    let texture = |side, format, mips, usage| {
        device.create_texture(&wgpu::TextureDescriptor {
            label: Some("Tidewater ShoreSim"),
            size: wgpu::Extent3d {
                width: side,
                height: side,
                depth_or_array_layers: 1,
            },
            mip_level_count: mips,
            sample_count: 1,
            dimension: wgpu::TextureDimension::D2,
            format,
            usage,
            view_formats: &[],
        })
    };
    let sampled = wgpu::TextureUsages::TEXTURE_BINDING | wgpu::TextureUsages::COPY_DST;
    let state_a = texture(
        STATE_SIDE,
        wgpu::TextureFormat::Rgba16Float,
        1,
        sampled | wgpu::TextureUsages::COPY_SRC,
    );
    let state_b = texture(
        STATE_SIDE,
        wgpu::TextureFormat::Rgba16Float,
        1,
        wgpu::TextureUsages::STORAGE_BINDING | wgpu::TextureUsages::COPY_SRC,
    );
    let height = texture(HEIGHT_SIDE, wgpu::TextureFormat::R32Float, 1, sampled);
    let shore = texture(SHORE_SIDE, wgpu::TextureFormat::Rgba32Float, 1, sampled);
    let direction = texture(1, wgpu::TextureFormat::Rgba32Float, 1, sampled);
    let lace = texture(SHORE_SIDE, wgpu::TextureFormat::Rgba8Unorm, 10, sampled);
    let dummy = texture(1, wgpu::TextureFormat::Rgba8Unorm, 1, sampled);
    let deltas = texture(input.delta_side, wgpu::TextureFormat::R32Float, 1, sampled);
    let upload = |tex: &wgpu::Texture, mip, side, bpp, data: &[u8]| {
        queue.write_texture(
            wgpu::TexelCopyTextureInfo {
                texture: tex,
                mip_level: mip,
                origin: wgpu::Origin3d::ZERO,
                aspect: wgpu::TextureAspect::All,
            },
            data,
            wgpu::TexelCopyBufferLayout {
                offset: 0,
                bytes_per_row: Some(side * bpp),
                rows_per_image: Some(side),
            },
            wgpu::Extent3d {
                width: side,
                height: side,
                depth_or_array_layers: 1,
            },
        );
    };
    upload(
        &state_a,
        0,
        STATE_SIDE,
        8,
        &vec![0; (STATE_SIDE * STATE_SIDE * 8) as usize],
    );
    upload(&height, 0, HEIGHT_SIDE, 4, &input.heights);
    upload(&shore, 0, SHORE_SIDE, 16, &input.shore);
    upload(&direction, 0, 1, 16, &input.direction);
    upload(&deltas, 0, input.delta_side, 4, &input.deltas);
    for (mip, data) in (0_u32..).zip(&input.lace) {
        upload(&lace, mip, SHORE_SIDE >> mip, 4, data);
    }
    let uniform = |bytes: &[u8]| {
        device.create_buffer_init(&wgpu::util::BufferInitDescriptor {
            label: Some("ShoreSim source parameters"),
            contents: bytes,
            usage: wgpu::BufferUsages::UNIFORM,
        })
    };
    let simulation_params = uniform(&input.simulation_params);
    let terrain_params = uniform(&input.terrain_params);
    let shore_params = uniform(&input.shore_params);
    let deposit = device.create_buffer_init(&wgpu::util::BufferInitDescriptor {
        label: Some("ShoreSim zero deposition"),
        contents: &vec![0; (STATE_SIDE * STATE_SIDE * 4) as usize],
        usage: wgpu::BufferUsages::STORAGE | wgpu::BufferUsages::COPY_SRC,
    });
    let ub = wgpu::BindingType::Buffer {
        ty: wgpu::BufferBindingType::Uniform,
        has_dynamic_offset: false,
        min_binding_size: None,
    };
    let tex = |filterable| wgpu::BindingType::Texture {
        sample_type: wgpu::TextureSampleType::Float { filterable },
        view_dimension: wgpu::TextureViewDimension::D2,
        multisampled: false,
    };
    let entry = |binding, ty| wgpu::BindGroupLayoutEntry {
        binding,
        visibility: wgpu::ShaderStages::COMPUTE,
        ty,
        count: None,
    };
    let mut e0 = vec![entry(0, ub)];
    for i in 1..10 {
        e0.push(entry(
            i,
            wgpu::BindingType::Sampler(if i == 9 {
                wgpu::SamplerBindingType::Comparison
            } else if i >= 7 {
                wgpu::SamplerBindingType::NonFiltering
            } else {
                wgpu::SamplerBindingType::Filtering
            }),
        ));
    }
    let layout0 = device.create_bind_group_layout(&wgpu::BindGroupLayoutDescriptor {
        label: Some("ShoreSim frame"),
        entries: &e0,
    });
    let e1: Vec<_> = (0..15)
        .map(|i| {
            entry(
                i,
                match i {
                    2 | 9 | 11 => ub,
                    12 => wgpu::BindingType::StorageTexture {
                        access: wgpu::StorageTextureAccess::WriteOnly,
                        format: wgpu::TextureFormat::Rgba16Float,
                        view_dimension: wgpu::TextureViewDimension::D2,
                    },
                    13 => wgpu::BindingType::Buffer {
                        ty: wgpu::BufferBindingType::Storage { read_only: false },
                        has_dynamic_offset: false,
                        min_binding_size: None,
                    },
                    3 | 6 | 7 | 10 => tex(false),
                    _ => tex(true),
                },
            )
        })
        .collect();
    let layout1 = device.create_bind_group_layout(&wgpu::BindGroupLayoutDescriptor {
        label: Some("ShoreSim resources"),
        entries: &e1,
    });
    let layout = device.create_pipeline_layout(&wgpu::PipelineLayoutDescriptor {
        label: Some("ShoreSim"),
        bind_group_layouts: &[Some(&layout0), Some(&layout1)],
        immediate_size: 0,
    });
    let shader = device.create_shader_module(wgpu::ShaderModuleDescriptor {
        label: Some("Tidewater ShoreSim source"),
        source: wgpu::ShaderSource::Wgsl(input.kernel.as_str().into()),
    });
    let pipeline = device.create_compute_pipeline(&wgpu::ComputePipelineDescriptor {
        label: Some("ShoreSim"),
        layout: Some(&layout),
        module: &shader,
        entry_point: Some("main"),
        compilation_options: default(),
        cache: None,
    });
    let samplers: Vec<_> = (1..10)
        .map(|i| {
            let address = match i {
                1 | 4 | 6 | 8 => wgpu::AddressMode::Repeat,
                3 => wgpu::AddressMode::MirrorRepeat,
                _ => wgpu::AddressMode::ClampToEdge,
            };
            let filter = if i >= 7 {
                wgpu::FilterMode::Nearest
            } else {
                wgpu::FilterMode::Linear
            };
            device.create_sampler(&wgpu::SamplerDescriptor {
                address_mode_u: address,
                address_mode_v: address,
                address_mode_w: address,
                min_filter: filter,
                mag_filter: filter,
                mipmap_filter: if i >= 7 {
                    wgpu::MipmapFilterMode::Nearest
                } else {
                    wgpu::MipmapFilterMode::Linear
                },
                anisotropy_clamp: match i {
                    4 | 5 => 8,
                    6 => 4,
                    _ => 1,
                },
                compare: if i == 9 {
                    Some(wgpu::CompareFunction::LessEqual)
                } else {
                    None
                },
                ..default()
            })
        })
        .collect();
    let views: Vec<_> = [
        &state_a, &lace, &height, &dummy, &shore, &direction, &state_b, &deltas,
    ]
    .into_iter()
    .map(|texture| texture.create_view(&default()))
    .collect();
    let view = |i| wgpu::BindingResource::TextureView(&views[i]);
    let resources = [
        view(0),
        view(1),
        simulation_params.as_entire_binding(),
        view(2),
        view(3),
        view(3),
        view(7),
        view(4),
        view(3),
        terrain_params.as_entire_binding(),
        view(5),
        shore_params.as_entire_binding(),
        view(6),
        deposit.as_entire_binding(),
        view(1),
    ];
    let entries: Vec<_> = resources
        .into_iter()
        .zip(0_u32..)
        .map(|(resource, binding)| wgpu::BindGroupEntry { binding, resource })
        .collect();
    let group1 = device.create_bind_group(&wgpu::BindGroupDescriptor {
        label: Some("ShoreSim resources"),
        layout: &layout1,
        entries: &entries,
    });
    Simulation {
        state_a,
        state_b,
        _deposit: deposit,
        pipeline,
        group1,
        layout0,
        samplers,
    }
}

impl Simulation {
    fn encode_step(
        &self,
        device: &wgpu::Device,
        encoder: &mut wgpu::CommandEncoder,
        step: u32,
        time: f64,
        dt: f32,
    ) {
        let mut frame = [0_u8; 720];
        frame[576..580].copy_from_slice(&step.to_le_bytes());
        #[allow(clippy::cast_possible_truncation)]
        frame[580..584].copy_from_slice(&(time as f32).to_le_bytes());
        frame[584..588].copy_from_slice(&dt.to_le_bytes());
        let frame_buffer = device.create_buffer_init(&wgpu::util::BufferInitDescriptor {
            label: Some("ShoreSim source frame"),
            contents: &frame,
            usage: wgpu::BufferUsages::UNIFORM,
        });
        let mut entries = vec![wgpu::BindGroupEntry {
            binding: 0,
            resource: frame_buffer.as_entire_binding(),
        }];
        for (index, sampler) in self.samplers.iter().enumerate() {
            entries.push(wgpu::BindGroupEntry {
                binding: u32::try_from(index + 1).expect("fixed ShoreSim sampler count"),
                resource: wgpu::BindingResource::Sampler(sampler),
            });
        }
        let group0 = device.create_bind_group(&wgpu::BindGroupDescriptor {
            label: Some("ShoreSim source frame"),
            layout: &self.layout0,
            entries: &entries,
        });
        {
            let mut pass = encoder.begin_compute_pass(&wgpu::ComputePassDescriptor {
                label: Some("ShoreSim source step"),
                ..default()
            });
            pass.set_pipeline(&self.pipeline);
            pass.set_bind_group(0, &group0, &[]);
            pass.set_bind_group(1, &self.group1, &[]);
            pass.dispatch_workgroups(96, 96, 1);
        }
        encoder.copy_texture_to_texture(
            self.state_b.as_image_copy(),
            self.state_a.as_image_copy(),
            wgpu::Extent3d {
                width: STATE_SIDE,
                height: STATE_SIDE,
                depth_or_array_layers: 1,
            },
        );
    }

    fn publish(&self, encoder: &mut wgpu::CommandEncoder, texture: &wgpu::Texture) {
        encoder.copy_texture_to_texture(
            self.state_a.as_image_copy(),
            texture.as_image_copy(),
            wgpu::Extent3d {
                width: STATE_SIDE,
                height: STATE_SIDE,
                depth_or_array_layers: 1,
            },
        );
    }
}

#[derive(Default)]
struct Execution {
    generation: Option<Arc<AtomicBool>>,
    simulation: Option<Simulation>,
    next_step: u32,
    time: f64,
}

#[allow(clippy::too_many_arguments)]
#[allow(
    clippy::needless_pass_by_value,
    reason = "Bevy render systems require owned system parameter wrappers."
)]
fn compute(
    mut context: RenderContext,
    staged: Res<StagedInput>,
    targets: Res<Targets>,
    ocean: Res<tidewater_ocean::Targets>,
    clock: Res<tidewater_ocean::Clock>,
    device: Res<RenderDevice>,
    queue: Res<RenderQueue>,
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
    let Some(output) = images.get(&targets.state) else {
        return;
    };
    let Execution {
        simulation,
        next_step,
        time,
        ..
    } = &mut *execution;
    let simulation =
        simulation.get_or_insert_with(|| initialize(&staged.0, device.wgpu_device(), &queue));
    let mut published = None;
    if *next_step <= BOOTSTRAP_STEPS {
        let end = (*next_step + 4).min(BOOTSTRAP_STEPS + 1);
        for step in *next_step..end {
            if step > 0 {
                *time += 1.0 / 60.0;
                simulation.encode_step(
                    device.wgpu_device(),
                    context.command_encoder(),
                    step,
                    *time,
                    1.0 / 60.0,
                );
                published = Some(step);
            }
        }
        *next_step = end;
    } else if ocean.ready.load(Ordering::Acquire) {
        let completed = ocean.completed_steps.load(Ordering::Acquire);
        // The ocean advances while ShoreSim finishes its 120-step bootstrap.
        // Join the latest completed source frame rather than waiting forever
        // for an ocean step that has already passed.
        if completed >= *next_step
            && let Some(frame) = clock.completed(completed)
        {
            simulation.encode_step(
                device.wgpu_device(),
                context.command_encoder(),
                frame.step,
                frame.time,
                frame.dt,
            );
            *time = frame.time;
            *next_step = completed.saturating_add(1);
            published = Some(frame.step);
        }
    }
    if let Some(step) = published {
        simulation.publish(context.command_encoder(), &output.texture);
        targets.completed_steps.store(step, Ordering::Release);
        if step >= BOOTSTRAP_STEPS {
            targets.ready.store(true, Ordering::Release);
        }
    }
}
