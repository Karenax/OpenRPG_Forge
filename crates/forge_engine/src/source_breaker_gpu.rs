// SPDX-License-Identifier: MIT
// Adapted from Tidewater Breakers.js, copyright 2026 DRG Software Solutions LLC.
// Revision 1438b1ab; see third_party/tidewater/LICENSE.
//! Shared immutable GPU resources for source crest and spray simulation.
//! Callers admit the bytes and own simulation timing and mutable GPU state.

use bevy::{
    prelude::default,
    render::renderer::{RenderDevice, RenderQueue},
};
use std::collections::BTreeMap;
use wgpu::util::DeviceExt;

const SOURCE_SHORE_HASH: &str =
    "fn shoreHash1( x: f32 ) -> f32 { return fract( sin( x * 127.1 + 311.7 ) * 43758.5453 ); }";

/// Built-in source kernels for the common water simulation; no module path is required.
pub const COMMON_CREST_PROGRAM: &str = include_str!("source_breaker_kernels/crest.wgsl");
pub const COMMON_SPRAY_PROGRAM: &str = include_str!("source_breaker_kernels/spray.wgsl");

/// Guard baked into the pinned source program before adaptation to an admitted shore.
pub const COMMON_CREST_SOURCE_STATIONS: u32 = 577;

/// Adapt a previously admitted source program for the shared GPU implementation.
/// Terrain corrections must come from the same admitted terrain as the textures.
pub fn prepare_program(
    source: &str,
    corrections: Option<&crate::sampled_terrain_corrections::Corrections>,
) -> Result<String, String> {
    if corrections.is_some_and(|value| !value.supports_fixed_source_profile()) {
        return Err("Breaker program requires its admitted fixed raster profile".into());
    }
    if source.matches(SOURCE_SHORE_HASH).count() != 1 {
        return Err("Expected exactly one original shore hash definition".into());
    }
    let source = source.replace(
        SOURCE_SHORE_HASH,
        crate::source_shore_sim::SHORE_HASH_SOURCE,
    );
    if let Some(corrections) = corrections {
        corrections.shader_existing(&source)
    } else {
        Ok(source)
    }
}

/// Adapt the shared source program to the admitted terrain and shore frame.
/// Corrections are applied after the base height evaluator has been adapted.
pub(crate) fn prepare_terrain_program(
    source: &str,
    document: &forge_project::TerrainDocument,
    shore: crate::source_shore_field::Domain,
    corrections: Option<&crate::sampled_terrain_corrections::Corrections>,
) -> Result<String, String> {
    let source = shore.prepare_water_shader(source, document)?;
    let source = prepare_program(&source, None)?;
    if let Some(corrections) = corrections {
        corrections.shader_existing(&source)
    } else {
        Ok(source)
    }
}

/// Replace the pinned source's baked station guard with the admitted shore count.
pub fn prepare_station_guard(
    source: &str,
    source_count: u32,
    station_count: u32,
) -> Result<String, String> {
    if source_count > 4096 || station_count > 4096 {
        return Err("Breaker station count exceeds the supported limit".into());
    }
    let guard = format!("if ( i >= {source_count}u )");
    if source.matches(&guard).count() != 1 {
        return Err("Original breaker station guard differs".into());
    }
    Ok(source.replacen(&guard, &format!("if ( i >= {station_count}u )"), 1))
}

/// Bound a resident crest dispatch by its actual station binding, allowing a
/// later shoreline generation to change size without changing the program.
pub fn prepare_resident_station_guard(source: &str, source_count: u32) -> Result<String, String> {
    if source_count > 4096 {
        return Err("Breaker station count exceeds the supported limit".into());
    }
    let guard = format!("if ( i >= {source_count}u )");
    if source.matches(&guard).count() != 1 {
        return Err("Original breaker station guard differs".into());
    }
    Ok(source.replacen(&guard, "if ( i >= arrayLength( &breakersStations ) )", 1))
}

/// Borrow previously admitted source bytes without imposing a storage layout.
pub trait InputFiles {
    fn file(&self, name: &str) -> Option<&[u8]>;

    /// Explicit texture dimensions for an admitted terrain. Reference captures
    /// without this metadata keep the original square layout.
    fn terrain_layout(&self) -> Option<TerrainTextureLayout> {
        None
    }
}

impl InputFiles for BTreeMap<String, Vec<u8>> {
    fn file(&self, name: &str) -> Option<&[u8]> {
        self.get(name).map(Vec::as_slice)
    }
}

/// Previously admitted source bytes, held only while constructing GPU resources.
pub struct Inputs<'a> {
    pub files: &'a dyn InputFiles,
    pub stations: &'a [u8],
    pub integrate: bool,
}

/// Dimensions shared by the terrain, shoreline and authored-delta textures.
#[derive(Clone, Copy, Debug, Eq, PartialEq)]
pub struct TerrainTextureLayout {
    pub heights: [u32; 2],
    pub shore_side: u32,
    pub deltas: [u32; 2],
}

impl TerrainTextureLayout {
    pub const REFERENCE: Self = Self {
        heights: [2048, 2048],
        shore_side: 512,
        deltas: [1, 1],
    };

    pub(crate) fn byte_count(size: [u32; 2], bytes_per_pixel: usize) -> Result<usize, String> {
        let width = usize::try_from(size[0]).map_err(|_| "Texture width overflow")?;
        let height = usize::try_from(size[1]).map_err(|_| "Texture height overflow")?;
        width
            .checked_mul(height)
            .and_then(|cells| cells.checked_mul(bytes_per_pixel))
            .ok_or_else(|| "Terrain texture size overflow".into())
    }

    pub fn validate(self) -> Result<(), String> {
        let maximum = forge_project::sampled_terrain::MAX_SAMPLED_TERRAIN_AXIS;
        if self
            .heights
            .iter()
            .any(|side| !(2..=maximum).contains(side))
            || !(2..=maximum).contains(&self.shore_side)
            || (self.deltas != [1, 1] && self.deltas != self.heights.map(|side| side + 1))
        {
            return Err("Terrain texture dimensions differ from the admitted raster".into());
        }
        Ok(())
    }

    pub fn validate_device(self, limit: u32) -> Result<(), String> {
        self.validate()?;
        if self
            .heights
            .into_iter()
            .chain(self.deltas)
            .any(|side| side > limit)
            || self.shore_side > limit
        {
            return Err("Terrain or shoreline texture exceeds the render-device limit".into());
        }
        Ok(())
    }
}

impl Inputs<'_> {
    fn file(&self, name: &str) -> &[u8] {
        self.files.file(name).expect("validated breaker input")
    }

    #[must_use]
    pub fn terrain_layout(&self) -> TerrainTextureLayout {
        self.files.terrain_layout().unwrap_or_else(|| {
            let mut layout = TerrainTextureLayout::REFERENCE;
            if self
                .files
                .file("authored-height-deltas.bin")
                .is_some_and(|bytes| bytes.len() != 4)
            {
                layout.deltas = [2049, 2049];
            }
            layout
        })
    }

    /// Validate the GPU ABI before these bytes reach the render device.
    pub fn validate(&self) -> Result<(), String> {
        if !self.stations.len().is_multiple_of(16) || self.stations.len() / 16 > 4096 {
            return Err("Breaker station buffer size differs".into());
        }
        if self
            .stations
            .as_chunks::<4>()
            .0
            .iter()
            .any(|bytes| !f32::from_le_bytes(*bytes).is_finite())
        {
            return Err("Breaker station buffer contains nonfinite values".into());
        }
        let layout = self.terrain_layout();
        layout.validate()?;
        for (name, size) in [
            ("terrain-params.bin", 32),
            ("shore-params.bin", 64),
            (
                "heights.f32.bin",
                TerrainTextureLayout::byte_count(layout.heights, 4)?,
            ),
            (
                "shore-field.rgba32.bin",
                TerrainTextureLayout::byte_count([layout.shore_side; 2], 16)?,
            ),
        ] {
            if self
                .files
                .file(name)
                .is_none_or(|bytes| bytes.len() != size)
            {
                return Err(format!("Breaker input size differs: {name}"));
            }
        }
        let delta = self
            .files
            .file("authored-height-deltas.bin")
            .unwrap_or(&[0; 4]);
        if delta.len() != TerrainTextureLayout::byte_count(layout.deltas, 4)? {
            return Err("Breaker terrain delta size differs".into());
        }
        for name in
            std::iter::once("kernel.wgsl").chain(self.integrate.then_some("spray-update.wgsl"))
        {
            let bytes = self
                .files
                .file(name)
                .ok_or_else(|| format!("Breaker shader missing: {name}"))?;
            if std::str::from_utf8(bytes)
                .map_err(|error| error.to_string())?
                .trim()
                .is_empty()
            {
                return Err(format!("Breaker shader empty: {name}"));
            }
        }
        if self.integrate {
            for (name, size) in [("surface-params.bin", 32), ("shore-sim-params.bin", 48)] {
                if self
                    .files
                    .file(name)
                    .is_none_or(|bytes| bytes.len() != size)
                {
                    return Err(format!("Breaker integration input size differs: {name}"));
                }
            }
        }
        Ok(())
    }

    pub fn validate_for_device(&self, device: &wgpu::Device) -> Result<(), String> {
        self.validate()?;
        self.terrain_layout()
            .validate_device(device.limits().max_texture_dimension_2d)
    }
}

pub struct Resources {
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

pub struct Integration {
    pub layout: wgpu::BindGroupLayout,
    pub pipeline: wgpu::ComputePipeline,
    pub query: wgpu::Buffer,
    pub requests: wgpu::Buffer,
    pub surface: wgpu::Buffer,
    pub sim: wgpu::Buffer,
    pub water_queries: crate::source_water_queries::Prepared,
}

/// Per-step resources supplied by the simulation owner after GPU admission.
pub struct CrestFrame<'a> {
    pub frame: &'a wgpu::Buffer,
    pub ocean: &'a wgpu::Buffer,
    pub parameters: &'a wgpu::Buffer,
    pub spray_parameters: &'a wgpu::Buffer,
    pub displacement: &'a wgpu::TextureView,
    pub particles: [&'a wgpu::Buffer; 4],
    pub output: &'a wgpu::Buffer,
    pub station_count: u32,
    pub emit: bool,
}

/// Per-step inputs for the source spray integration pass.
struct SprayFrame<'a> {
    frame_group: &'a wgpu::BindGroup,
    ocean: &'a wgpu::Buffer,
    spray_parameters: &'a wgpu::Buffer,
    displacement: &'a wgpu::TextureView,
    particles: [&'a wgpu::Buffer; 4],
    shore_simulation: &'a wgpu::Buffer,
}

impl Resources {
    /// Encode the common source water order: optional shore, crests, then spray.
    /// A resident shore supplies its own deposit buffer, ensuring that spray
    /// writes back to the exact simulation which consumed the previous step.
    pub fn encode_water_step(
        &self,
        device: &wgpu::Device,
        encoder: &mut wgpu::CommandEncoder,
        crest: &CrestFrame<'_>,
        shore: Option<&crate::source_shore_sim::GpuSimulation>,
        deposit: Option<&wgpu::Buffer>,
    ) -> Result<(wgpu::BindGroup, Option<wgpu::BindGroup>), &'static str> {
        if (shore.is_some() && deposit.is_some())
            || ((shore.is_some() || deposit.is_some()) && self.integration.is_none())
        {
            return Err("Invalid coupled water step inputs");
        }
        if let Some(shore) = shore {
            shore.encode_frame_buffer(device, encoder, crest.frame);
        }
        let frame_group = self.encode_crests(device, encoder, crest);
        let deposit = shore
            .map(crate::source_shore_sim::GpuSimulation::deposit_buffer)
            .or(deposit);
        let spray_group = deposit.map(|shore_simulation| {
            self.encode_spray(
                device,
                encoder,
                &SprayFrame {
                    frame_group: &frame_group,
                    ocean: crest.ocean,
                    spray_parameters: crest.spray_parameters,
                    displacement: crest.displacement,
                    particles: crest.particles,
                    shore_simulation,
                },
            )
        });
        Ok((frame_group, spray_group))
    }

    /// Encode the source crest pass and return its frame group for coupled spray.
    /// The caller owns the ordering against ocean and shore simulation.
    #[must_use]
    fn encode_crests(
        &self,
        device: &wgpu::Device,
        encoder: &mut wgpu::CommandEncoder,
        input: &CrestFrame<'_>,
    ) -> wgpu::BindGroup {
        let mut entries = vec![wgpu::BindGroupEntry {
            binding: 0,
            resource: input.frame.as_entire_binding(),
        }];
        for (i, sampler) in self.samplers.iter().enumerate() {
            entries.push(wgpu::BindGroupEntry {
                binding: u32::try_from(i + 1).expect("binding"),
                resource: wgpu::BindingResource::Sampler(sampler),
            });
        }
        let group0 = device.create_bind_group(&wgpu::BindGroupDescriptor {
            label: Some("crest frame"),
            layout: &self.layout0,
            entries: &entries,
        });
        let view = wgpu::BindingResource::TextureView;
        let resources = [
            view(&self.height),
            view(&self.dummy),
            view(&self.dummy),
            view(&self.deltas),
            view(&self.field),
            view(&self.dummy),
            self.terrain.as_entire_binding(),
            view(&self.dummy),
            self.shore.as_entire_binding(),
            view(input.displacement),
            view(input.displacement),
            input.ocean.as_entire_binding(),
            input.particles[0].as_entire_binding(),
            input.particles[1].as_entire_binding(),
            input.particles[2].as_entire_binding(),
            input.particles[3].as_entire_binding(),
            input.spray_parameters.as_entire_binding(),
            input.parameters.as_entire_binding(),
            self.stations.as_entire_binding(),
            input.output.as_entire_binding(),
        ];
        let entries: Vec<_> = resources
            .into_iter()
            .enumerate()
            .map(|(i, resource)| wgpu::BindGroupEntry {
                binding: u32::try_from(i).expect("binding"),
                resource,
            })
            .collect();
        let group1 = device.create_bind_group(&wgpu::BindGroupDescriptor {
            label: Some("crest resources"),
            layout: &self.layout1,
            entries: &entries,
        });
        let mut pass = encoder.begin_compute_pass(&wgpu::ComputePassDescriptor {
            label: Some("native crests"),
            timestamp_writes: None,
        });
        pass.set_pipeline(&self.pipeline);
        pass.set_bind_group(0, &group0, &[]);
        pass.set_bind_group(1, &group1, &[]);
        if input.emit {
            pass.dispatch_workgroups(input.station_count.div_ceil(64), 1, 1);
        }
        drop(pass);
        group0
    }

    /// Encode spray integration and return its group for water queries.
    #[must_use]
    fn encode_spray(
        &self,
        device: &wgpu::Device,
        encoder: &mut wgpu::CommandEncoder,
        input: &SprayFrame<'_>,
    ) -> wgpu::BindGroup {
        let integration = self.integration.as_ref().expect("integration installed");
        let view = wgpu::BindingResource::TextureView;
        let resources = [
            input.particles[0].as_entire_binding(),
            input.particles[1].as_entire_binding(),
            input.particles[2].as_entire_binding(),
            input.particles[3].as_entire_binding(),
            input.spray_parameters.as_entire_binding(),
            integration.query.as_entire_binding(),
            view(input.displacement),
            view(input.displacement),
            input.ocean.as_entire_binding(),
            view(&self.height),
            view(&self.dummy),
            view(&self.dummy),
            view(&self.deltas),
            view(&self.field),
            view(&self.dummy),
            self.terrain.as_entire_binding(),
            view(&self.dummy),
            self.shore.as_entire_binding(),
            integration.surface.as_entire_binding(),
            view(&self.dummy),
            view(&self.dummy),
            integration.sim.as_entire_binding(),
            input.shore_simulation.as_entire_binding(),
            integration.requests.as_entire_binding(),
        ];
        let entries: Vec<_> = resources
            .into_iter()
            .enumerate()
            .map(|(i, resource)| wgpu::BindGroupEntry {
                binding: u32::try_from(i).expect("binding"),
                resource,
            })
            .collect();
        let group = device.create_bind_group(&wgpu::BindGroupDescriptor {
            label: Some("spray integration"),
            layout: &integration.layout,
            entries: &entries,
        });
        let mut pass = encoder.begin_compute_pass(&wgpu::ComputePassDescriptor {
            label: Some("native spray integration"),
            timestamp_writes: None,
        });
        pass.set_pipeline(&integration.pipeline);
        pass.set_bind_group(0, input.frame_group, &[]);
        pass.set_bind_group(1, &group, &[]);
        pass.dispatch_workgroups(40960 / 64, 1, 1);
        drop(pass);
        group
    }

    #[allow(
        clippy::too_many_lines,
        reason = "Preserve the source GPU resource and binding setup"
    )]
    #[must_use]
    pub fn new(render_device: &RenderDevice, queue: &RenderQueue, input: &Inputs<'_>) -> Self {
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
                std::str::from_utf8(input.file("kernel.wgsl"))
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
        let terrain = uniform(input.file("terrain-params.bin"));
        let shore = uniform(input.file("shore-params.bin"));
        let station_bytes = if input.stations.is_empty() {
            &[0_u8; 16][..]
        } else {
            input.stations
        };
        let stations = buffer(station_bytes, wgpu::BufferUsages::STORAGE);
        let layout = input.terrain_layout();
        let height = texture_rect(
            device,
            queue,
            layout.heights,
            1,
            wgpu::TextureFormat::R32Float,
            &[input.file("heights.f32.bin")],
            4,
        );
        let delta_bytes = input
            .files
            .file("authored-height-deltas.bin")
            .unwrap_or(&[0_u8; 4]);
        let deltas = texture_rect(
            device,
            queue,
            layout.deltas,
            1,
            wgpu::TextureFormat::R32Float,
            &[delta_bytes],
            4,
        );
        let field = texture(
            device,
            queue,
            layout.shore_side,
            1,
            wgpu::TextureFormat::Rgba32Float,
            &[input.file("shore-field.rgba32.bin")],
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
                    std::str::from_utf8(input.file("spray-update.wgsl"))
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
            let surface = uniform(input.file("surface-params.bin"));
            let sim = uniform(input.file("shore-sim-params.bin"));
            let water_queries = crate::source_water_queries::Prepared::new(
                render_device,
                std::str::from_utf8(input.file("spray-update.wgsl")).expect("checked WGSL"),
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

#[must_use]
pub fn texture(
    device: &wgpu::Device,
    queue: &RenderQueue,
    side: u32,
    layers: u32,
    format: wgpu::TextureFormat,
    mips: &[&[u8]],
    bpp: u32,
) -> wgpu::TextureView {
    texture_rect(device, queue, [side, side], layers, format, mips, bpp)
}

fn texture_rect(
    device: &wgpu::Device,
    queue: &RenderQueue,
    size: [u32; 2],
    layers: u32,
    format: wgpu::TextureFormat,
    mips: &[&[u8]],
    bpp: u32,
) -> wgpu::TextureView {
    let texture = device.create_texture(&wgpu::TextureDescriptor {
        label: Some("crest input"),
        size: wgpu::Extent3d {
            width: size[0],
            height: size[1],
            depth_or_array_layers: layers,
        },
        mip_level_count: u32::try_from(mips.len()).expect("mips"),
        sample_count: 1,
        dimension: wgpu::TextureDimension::D2,
        format,
        usage: wgpu::TextureUsages::TEXTURE_BINDING | wgpu::TextureUsages::COPY_DST,
        view_formats: &[],
    });
    for (mip, data) in mips.iter().enumerate() {
        let width = (size[0] >> mip).max(1);
        let height = (size[1] >> mip).max(1);
        queue.write_texture(
            wgpu::TexelCopyTextureInfo {
                texture: &texture,
                mip_level: u32::try_from(mip).expect("mip"),
                origin: wgpu::Origin3d::ZERO,
                aspect: wgpu::TextureAspect::All,
            },
            data,
            wgpu::TexelCopyBufferLayout {
                offset: 0,
                bytes_per_row: Some(width * bpp),
                rows_per_image: Some(height),
            },
            wgpu::Extent3d {
                width,
                height,
                depth_or_array_layers: layers,
            },
        );
    }
    texture.create_view(&wgpu::TextureViewDescriptor {
        dimension: Some(if layers > 1 {
            wgpu::TextureViewDimension::D2Array
        } else {
            wgpu::TextureViewDimension::D2
        }),
        ..default()
    })
}

#[cfg(test)]
mod resident_guard_tests {
    use super::{InputFiles, Inputs, TerrainTextureLayout, prepare_resident_station_guard};
    use std::collections::BTreeMap;

    #[test]
    fn resident_guard_uses_the_bound_station_count_and_rejects_ambiguous_source() {
        let source = "if ( i >= 577u ) { return; }";
        assert_eq!(
            prepare_resident_station_guard(source, 577).unwrap(),
            "if ( i >= arrayLength( &breakersStations ) ) { return; }"
        );
        assert!(prepare_resident_station_guard(source, 576).is_err());
        assert!(prepare_resident_station_guard(&format!("{source}{source}"), 577).is_err());
        assert!(prepare_resident_station_guard(source, 4097).is_err());
    }

    #[test]
    fn breaker_inputs_validate_rectangular_textures_and_device_limit() {
        struct Files {
            entries: BTreeMap<String, Vec<u8>>,
            layout: TerrainTextureLayout,
        }
        impl InputFiles for Files {
            fn file(&self, name: &str) -> Option<&[u8]> {
                self.entries.get(name).map(Vec::as_slice)
            }
            fn terrain_layout(&self) -> Option<TerrainTextureLayout> {
                Some(self.layout)
            }
        }
        let layout = TerrainTextureLayout {
            heights: [4, 2],
            shore_side: 2,
            deltas: [5, 3],
        };
        let mut files = Files {
            layout,
            entries: BTreeMap::from([
                ("terrain-params.bin".into(), vec![0; 32]),
                ("shore-params.bin".into(), vec![0; 64]),
                ("heights.f32.bin".into(), vec![0; 4 * 2 * 4]),
                ("shore-field.rgba32.bin".into(), vec![0; 2 * 2 * 16]),
                ("authored-height-deltas.bin".into(), vec![0; 5 * 3 * 4]),
                ("kernel.wgsl".into(), b"valid source".to_vec()),
            ]),
        };
        assert!(
            Inputs {
                files: &files,
                stations: &[],
                integrate: false
            }
            .validate()
            .is_ok()
        );
        assert!(layout.validate_device(4).is_err());
        assert!(layout.validate_device(5).is_ok());
        let _ = files
            .entries
            .get_mut("authored-height-deltas.bin")
            .unwrap()
            .pop();
        assert!(
            Inputs {
                files: &files,
                stations: &[],
                integrate: false
            }
            .validate()
            .is_err()
        );
        assert!(
            TerrainTextureLayout {
                deltas: [4, 3],
                ..layout
            }
            .validate()
            .is_err()
        );
    }
}
