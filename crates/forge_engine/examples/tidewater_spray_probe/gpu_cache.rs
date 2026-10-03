//! GPU resources owned for the lifetime of the immutable probe Input.
//! Per-step uniforms, mutable simulation buffers and FFT images remain separate.
use super::{Input, texture};
use bevy::{
    prelude::default,
    render::renderer::{RenderDevice, RenderQueue},
};
use wgpu::util::DeviceExt;

pub(super) struct Resources {
    pub layout0: wgpu::BindGroupLayout,
    pub layout1: wgpu::BindGroupLayout,
    pub pipeline: wgpu::ComputePipeline,
    pub samplers: Vec<wgpu::Sampler>,
    pub terrain: wgpu::Buffer,
    pub shore: wgpu::Buffer,
    pub stations: wgpu::Buffer,
    pub height: wgpu::TextureView,
    pub deltas: wgpu::TextureView,
    pub field: wgpu::TextureView,
    pub dummy: wgpu::TextureView,
    pub integration: Option<Integration>,
}

pub(super) struct Integration {
    pub layout: wgpu::BindGroupLayout,
    pub pipeline: wgpu::ComputePipeline,
    pub query: wgpu::Buffer,
    pub requests: wgpu::Buffer,
    pub surface: wgpu::Buffer,
    pub sim: wgpu::Buffer,
    pub water_queries: forge_engine::tidewater_water_query::Prepared,
}

impl Resources {
    pub fn new(render_device: &RenderDevice, queue: &RenderQueue, input: &Input) -> Self {
        let device = render_device.wgpu_device();
        let ub = wgpu::BindingType::Buffer {
            ty: wgpu::BufferBindingType::Uniform,
            has_dynamic_offset: false,
            min_binding_size: None,
        };
        let tex = |filterable, array| wgpu::BindingType::Texture {
            sample_type: wgpu::TextureSampleType::Float { filterable },
            view_dimension: if array {
                wgpu::TextureViewDimension::D2Array
            } else {
                wgpu::TextureViewDimension::D2
            },
            multisampled: false,
        };
        let entry = |binding, ty| wgpu::BindGroupLayoutEntry {
            binding,
            visibility: wgpu::ShaderStages::COMPUTE,
            ty,
            count: None,
        };
        let mut entries0 = vec![entry(0, ub)];
        for i in 1..10 {
            entries0.push(entry(
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
            label: Some("crest frame"),
            entries: &entries0,
        });
        let entries1: Vec<_> = (0..20)
            .map(|i| {
                entry(
                    i,
                    match i {
                        6 | 8 | 11 | 16 | 17 => ub,
                        12..=15 | 18 | 19 => wgpu::BindingType::Buffer {
                            ty: wgpu::BufferBindingType::Storage { read_only: i == 18 },
                            has_dynamic_offset: false,
                            min_binding_size: None,
                        },
                        0 | 3 | 4 => tex(false, false),
                        9 | 10 => tex(true, true),
                        _ => tex(true, false),
                    },
                )
            })
            .collect();
        let layout1 = device.create_bind_group_layout(&wgpu::BindGroupLayoutDescriptor {
            label: Some("crest resources"),
            entries: &entries1,
        });
        let layout = device.create_pipeline_layout(&wgpu::PipelineLayoutDescriptor {
            label: Some("crest compute"),
            bind_group_layouts: &[Some(&layout0), Some(&layout1)],
            immediate_size: 0,
        });
        let shader = device.create_shader_module(wgpu::ShaderModuleDescriptor {
            label: Some("original crest kernel"),
            source: wgpu::ShaderSource::Wgsl(
                std::str::from_utf8(&input.files["kernel.wgsl"])
                    .expect("checked WGSL")
                    .into(),
            ),
        });
        let pipeline = device.create_compute_pipeline(&wgpu::ComputePipelineDescriptor {
            label: Some("crest compute"),
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
        let buffer = |data: &[u8], usage| {
            device.create_buffer_init(&wgpu::util::BufferInitDescriptor {
                label: Some("crest input"),
                contents: data,
                usage,
            })
        };
        let uniform = |data: &[u8]| buffer(data, wgpu::BufferUsages::UNIFORM);
        let terrain = uniform(&input.files["terrain-params.bin"]);
        let shore = uniform(&input.files["shore-params.bin"]);
        let station_bytes = if input.stations.is_empty() {
            &[0_u8; 16][..]
        } else {
            &input.stations
        };
        let stations = buffer(station_bytes, wgpu::BufferUsages::STORAGE);
        let height = texture(
            device,
            queue,
            2048,
            1,
            wgpu::TextureFormat::R32Float,
            &[&input.files["heights.f32.bin"]],
            4,
        );
        let delta_bytes = input
            .files
            .get("authored-height-deltas.bin")
            .map_or(&[0_u8; 4][..], Vec::as_slice);
        let delta_side = if delta_bytes.len() == 4 { 1 } else { 2049 };
        let deltas = texture(
            device,
            queue,
            delta_side,
            1,
            wgpu::TextureFormat::R32Float,
            &[delta_bytes],
            4,
        );
        let field = texture(
            device,
            queue,
            512,
            1,
            wgpu::TextureFormat::Rgba32Float,
            &[&input.files["shore-field.rgba32.bin"]],
            16,
        );
        let dummy = texture(
            device,
            queue,
            1,
            1,
            wgpu::TextureFormat::Rgba8Unorm,
            &[&[0; 4]],
            4,
        );
        let integration = input.integrate.then(|| {
            let entries: Vec<_> = (0..24)
                .map(|i| {
                    entry(
                        i,
                        match i {
                            0..=3 | 5 | 22 | 23 => wgpu::BindingType::Buffer {
                                ty: wgpu::BufferBindingType::Storage {
                                    read_only: i == 5 || i == 23,
                                },
                                has_dynamic_offset: false,
                                min_binding_size: None,
                            },
                            4 | 8 | 15 | 17 | 18 | 21 => ub,
                            6 | 7 => tex(true, true),
                            9 | 12 | 13 => tex(false, false),
                            _ => tex(true, false),
                        },
                    )
                })
                .collect();
            let update_layout = device.create_bind_group_layout(&wgpu::BindGroupLayoutDescriptor {
                label: Some("spray integration resources"),
                entries: &entries,
            });
            let layout = device.create_pipeline_layout(&wgpu::PipelineLayoutDescriptor {
                label: Some("spray integration"),
                bind_group_layouts: &[Some(&layout0), Some(&update_layout)],
                immediate_size: 0,
            });
            let shader = device.create_shader_module(wgpu::ShaderModuleDescriptor {
                label: Some("original spray update"),
                source: wgpu::ShaderSource::Wgsl(
                    std::str::from_utf8(&input.files["spray-update.wgsl"])
                        .expect("checked WGSL")
                        .into(),
                ),
            });
            let update = device.create_compute_pipeline(&wgpu::ComputePipelineDescriptor {
                label: Some("spray integration"),
                layout: Some(&layout),
                module: &shader,
                entry_point: Some("main"),
                compilation_options: default(),
                cache: None,
            });
            let query = buffer(&[0; 64 * 16], wgpu::BufferUsages::STORAGE);
            let requests = buffer(&[0; 32 * 4 * 16], wgpu::BufferUsages::STORAGE);
            let surface = uniform(&input.files["surface-params.bin"]);
            let sim = uniform(&input.files["shore-sim-params.bin"]);
            let water_queries = forge_engine::tidewater_water_query::Prepared::new(
                render_device,
                std::str::from_utf8(&input.files["spray-update.wgsl"]).expect("checked WGSL"),
                &layout0.clone().into(),
                &update_layout.clone().into(),
            );
            Integration {
                layout: update_layout,
                pipeline: update,
                query,
                requests,
                surface,
                sim,
                water_queries,
            }
        });
        Self {
            layout0,
            layout1,
            pipeline,
            samplers,
            terrain,
            shore,
            stations,
            height,
            deltas,
            field,
            dummy,
            integration,
        }
    }
}
