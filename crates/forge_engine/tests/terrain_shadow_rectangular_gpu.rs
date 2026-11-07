//! Hardware coverage of the actual prepared rectangular terrain shadow program.
use bevy::math::{DMat4, DVec3};
use forge_engine::{
    source_shadows::casters::terrain::authored::PreparedSource, sun_cascades::CascadeView,
};
use forge_project::{
    HeightSamplingAlgorithm, TERRAIN_DOCUMENT_SCHEMA_VERSION_V4, TerrainExtent, TerrainHeightDelta,
    authoring::new_terrain,
    sampled_terrain::{HeightEncoding, SAMPLED_TERRAIN_SCHEMA, SampledTerrainSource},
};
use wgpu::util::DeviceExt;

#[test]
#[ignore = "requires a hardware GPU"]
#[allow(clippy::too_many_lines, clippy::cast_precision_loss)]
fn prepared_rectangular_shadow_renders_coverage_and_height_edit() {
    bevy::tasks::block_on(async {
        let heights = vec![0_u8; 46 * 22 * 4];
        let mut document = new_terrain("shadow-gpu", "authoring", "Independent terrain").unwrap();
        document.schema_version = TERRAIN_DOCUMENT_SCHEMA_VERSION_V4;
        document.extent_m = TerrainExtent::new(23.0, 11.0);
        document.height_sampling.algorithm = HeightSamplingAlgorithm::SampledRasterV1;
        document.height_sampling.sample_spacing_m = 0.5;
        document.height_sampling.base_elevation_m = 0.0;
        document.height_sampling.amplitude_m = 0.0;
        document.height_sampling.base_frequency_cycles_per_m = 1.0;
        document.height_sampling.octaves = 1;
        document.height_sampling.lacunarity = 2.0;
        document.height_sampling.persistence = 0.5;
        document.sampled_source = Some(SampledTerrainSource {
            schema: SAMPLED_TERRAIN_SCHEMA.into(),
            schema_version: 1,
            width: 46,
            height: 22,
            origin_xz_m: [-11.5, -5.5],
            sample_spacing_m: 0.5,
            outside_height_m: -12.0,
            encoding: HeightEncoding::Float32Le,
            samples_sha256: forge_core::bytes_sha256(&heights),
        });
        document
            .height_deltas
            .push(TerrainHeightDelta::new(3, 4, 4.0));
        let source = include_str!("../src/source_terrain/engine_lod.wgsl");
        let prepared = PreparedSource::new(&document, heights, source).unwrap();
        let input = prepared
            .at(CascadeView {
                world_from_view: DMat4::from_translation(DVec3::new(0.0, 2.0, 20.0)),
                near: 0.1,
                fov_degrees: 120.0,
                aspect: 2.0,
            })
            .unwrap();
        assert_eq!(input.height_size, [46, 22]);
        assert_eq!([input.edit_side, input.edit_height], [47, 23]);
        assert!(input.count > 0);

        let instance = wgpu::Instance::new(wgpu::InstanceDescriptor::new_without_display_handle());
        let adapter = instance
            .request_adapter(&wgpu::RequestAdapterOptions {
                power_preference: wgpu::PowerPreference::HighPerformance,
                force_fallback_adapter: false,
                compatible_surface: None,
            })
            .await
            .unwrap();
        let info = adapter.get_info();
        assert_ne!(info.device_type, wgpu::DeviceType::Cpu);
        eprintln!(
            "GPU: {} ({:?}, {:?}); nodes={}",
            info.name, info.backend, info.device_type, input.count
        );
        let (device, queue) = adapter
            .request_device(&wgpu::DeviceDescriptor::default())
            .await
            .unwrap();
        let source = format!(
            "{}\n@fragment fn fs(@builtin(position) p:vec4f)->@location(0) vec4f {{ return vec4f(p.z,0.0,0.0,1.0); }}",
            input.shader
        );
        let shader = device.create_shader_module(wgpu::ShaderModuleDescriptor {
            label: Some("prepared terrain shadow"),
            source: wgpu::ShaderSource::Wgsl(source.into()),
        });
        let grid_attributes = [wgpu::VertexAttribute {
            format: wgpu::VertexFormat::Float32x2,
            offset: 0,
            shader_location: 0,
        }];
        let node_attributes = [wgpu::VertexAttribute {
            format: wgpu::VertexFormat::Float32x4,
            offset: 0,
            shader_location: 1,
        }];
        let layouts = [
            wgpu::VertexBufferLayout {
                array_stride: 8,
                step_mode: wgpu::VertexStepMode::Vertex,
                attributes: &grid_attributes,
            },
            wgpu::VertexBufferLayout {
                array_stride: 16,
                step_mode: wgpu::VertexStepMode::Instance,
                attributes: &node_attributes,
            },
        ];
        let targets = [Some(wgpu::ColorTargetState {
            format: wgpu::TextureFormat::Rgba8Unorm,
            blend: None,
            write_mask: wgpu::ColorWrites::ALL,
        })];
        let pipeline = device.create_render_pipeline(&wgpu::RenderPipelineDescriptor {
            label: Some("prepared terrain shadow coverage"),
            layout: None,
            vertex: wgpu::VertexState {
                module: &shader,
                entry_point: Some("vertex"),
                compilation_options: wgpu::PipelineCompilationOptions::default(),
                buffers: &layouts,
            },
            primitive: wgpu::PrimitiveState::default(),
            depth_stencil: None,
            multisample: wgpu::MultisampleState::default(),
            fragment: Some(wgpu::FragmentState {
                module: &shader,
                entry_point: Some("fs"),
                compilation_options: wgpu::PipelineCompilationOptions::default(),
                targets: &targets,
            }),
            multiview_mask: None,
            cache: None,
        });

        let mut parameters = Vec::new();
        // Project world X/Z into the 32 m square image; encode height into depth.
        for column in [
            [1.0_f32 / 16.0, 0.0, 0.0, 0.0],
            [0.0, 0.0, 1.0 / 32.0, 0.0],
            [0.0, 1.0 / 16.0, 0.0, 0.0],
            [0.0, 0.0, 0.5, 1.0],
        ] {
            for value in column {
                parameters.extend_from_slice(&value.to_le_bytes());
            }
        }
        for value in input.view_position.to_array() {
            parameters.extend_from_slice(&value.to_le_bytes());
        }
        for row in input.morph {
            for value in row.to_array() {
                parameters.extend_from_slice(&value.to_le_bytes());
            }
        }
        assert_eq!(parameters.len(), 224);
        let uniform = device.create_buffer_init(&wgpu::util::BufferInitDescriptor {
            label: Some("shadow parameters"),
            contents: &parameters,
            usage: wgpu::BufferUsages::UNIFORM,
        });
        let texture = |name, size: [u32; 2], bytes: &[u8]| {
            let extent = wgpu::Extent3d {
                width: size[0],
                height: size[1],
                depth_or_array_layers: 1,
            };
            let texture = device.create_texture(&wgpu::TextureDescriptor {
                label: Some(name),
                size: extent,
                mip_level_count: 1,
                sample_count: 1,
                dimension: wgpu::TextureDimension::D2,
                format: wgpu::TextureFormat::R32Float,
                usage: wgpu::TextureUsages::TEXTURE_BINDING | wgpu::TextureUsages::COPY_DST,
                view_formats: &[],
            });
            queue.write_texture(
                texture.as_image_copy(),
                bytes,
                wgpu::TexelCopyBufferLayout {
                    offset: 0,
                    bytes_per_row: Some(size[0] * 4),
                    rows_per_image: Some(size[1]),
                },
                extent,
            );
            texture
        };
        let height = texture("rectangular heights", input.height_size, &input.heights);
        let edits = texture(
            "rectangular edits",
            [input.edit_side, input.edit_height],
            &input.edits,
        );
        let height_view = height.create_view(&wgpu::TextureViewDescriptor::default());
        let edits_view = edits.create_view(&wgpu::TextureViewDescriptor::default());
        let layout = pipeline.get_bind_group_layout(0);
        let group = device.create_bind_group(&wgpu::BindGroupDescriptor {
            label: Some("actual shadow inputs"),
            layout: &layout,
            entries: &[
                wgpu::BindGroupEntry {
                    binding: 0,
                    resource: uniform.as_entire_binding(),
                },
                wgpu::BindGroupEntry {
                    binding: 1,
                    resource: wgpu::BindingResource::TextureView(&height_view),
                },
                wgpu::BindGroupEntry {
                    binding: 2,
                    resource: wgpu::BindingResource::TextureView(&edits_view),
                },
            ],
        });
        let grid: Vec<_> = (0..=40_u16)
            .flat_map(|z| {
                (0..=40_u16).flat_map(move |x| [f32::from(x) / 40.0, f32::from(z) / 40.0])
            })
            .flat_map(f32::to_le_bytes)
            .collect();
        let indices: Vec<_> = (0..40_u32)
            .flat_map(|z| {
                (0..40_u32).flat_map(move |x| {
                    let a = z * 41 + x;
                    let b = a + 1;
                    let c = a + 41;
                    let d = c + 1;
                    if (x + z).is_multiple_of(2) {
                        [a, c, b, b, c, d]
                    } else {
                        [a, c, d, a, d, b]
                    }
                })
            })
            .flat_map(u32::to_le_bytes)
            .collect();
        let buffer = |label, data: &[u8], usage| {
            device.create_buffer_init(&wgpu::util::BufferInitDescriptor {
                label: Some(label),
                contents: data,
                usage,
            })
        };
        let grid = buffer("grid", &grid, wgpu::BufferUsages::VERTEX);
        let nodes = buffer("prepared nodes", &input.nodes, wgpu::BufferUsages::VERTEX);
        let indices = buffer("grid indices", &indices, wgpu::BufferUsages::INDEX);
        let extent = wgpu::Extent3d {
            width: 64,
            height: 64,
            depth_or_array_layers: 1,
        };
        let target = device.create_texture(&wgpu::TextureDescriptor {
            label: Some("coverage target"),
            size: extent,
            mip_level_count: 1,
            sample_count: 1,
            dimension: wgpu::TextureDimension::D2,
            format: wgpu::TextureFormat::Rgba8Unorm,
            usage: wgpu::TextureUsages::RENDER_ATTACHMENT | wgpu::TextureUsages::COPY_SRC,
            view_formats: &[],
        });
        let target_view = target.create_view(&wgpu::TextureViewDescriptor::default());
        let output = device.create_buffer(&wgpu::BufferDescriptor {
            label: Some("pixels"),
            size: 64 * 64 * 4,
            usage: wgpu::BufferUsages::MAP_READ | wgpu::BufferUsages::COPY_DST,
            mapped_at_creation: false,
        });
        let mut encoder = device.create_command_encoder(&wgpu::CommandEncoderDescriptor::default());
        {
            let attachments = [Some(wgpu::RenderPassColorAttachment {
                view: &target_view,
                resolve_target: None,
                depth_slice: None,
                ops: wgpu::Operations {
                    load: wgpu::LoadOp::Clear(wgpu::Color::TRANSPARENT),
                    store: wgpu::StoreOp::Store,
                },
            })];
            let mut pass = encoder.begin_render_pass(&wgpu::RenderPassDescriptor {
                label: Some("shadow coverage"),
                color_attachments: &attachments,
                depth_stencil_attachment: None,
                timestamp_writes: None,
                occlusion_query_set: None,
                multiview_mask: None,
            });
            pass.set_pipeline(&pipeline);
            pass.set_bind_group(0, &group, &[]);
            pass.set_vertex_buffer(0, grid.slice(..));
            pass.set_vertex_buffer(1, nodes.slice(..));
            pass.set_index_buffer(indices.slice(..), wgpu::IndexFormat::Uint32);
            pass.draw_indexed(0..9600, 0, 0..input.count);
        }
        encoder.copy_texture_to_buffer(
            wgpu::TexelCopyTextureInfo {
                texture: &target,
                mip_level: 0,
                origin: wgpu::Origin3d::ZERO,
                aspect: wgpu::TextureAspect::All,
            },
            wgpu::TexelCopyBufferInfo {
                buffer: &output,
                layout: wgpu::TexelCopyBufferLayout {
                    offset: 0,
                    bytes_per_row: Some(256),
                    rows_per_image: Some(64),
                },
            },
            extent,
        );
        let submission = queue.submit([encoder.finish()]);
        let (tx, rx) = std::sync::mpsc::channel();
        output
            .slice(..)
            .map_async(wgpu::MapMode::Read, move |result| tx.send(result).unwrap());
        device
            .poll(wgpu::PollType::Wait {
                submission_index: Some(submission),
                timeout: Some(std::time::Duration::from_secs(30)),
            })
            .unwrap();
        rx.recv_timeout(std::time::Duration::from_secs(5))
            .unwrap()
            .unwrap();
        let mapped = output.slice(..).get_mapped_range();
        let mut inside = 0;
        let mut edited = 0;
        for y in 0..64 {
            for x in 0..64 {
                let wx = (x as f32 + 0.5 - 32.0) * 0.5;
                let wz = (y as f32 + 0.5 - 32.0) * 0.5;
                let expected = wx.abs() < 11.5 && wz.abs() < 5.5;
                let offset = (y * 64 + x) * 4;
                assert_eq!(
                    mapped[offset + 3],
                    if expected { 255 } else { 0 },
                    "pixel {x},{y}"
                );
                inside += usize::from(expected);
                edited += usize::from(expected && mapped[offset] > 130);
            }
        }
        assert!(
            edited > 0,
            "height correction did not reach the rendered shader"
        );
        eprintln!(
            "validated {inside} interior pixels, {} exterior pixels, {edited} edited pixels",
            64 * 64 - inside
        );
    });
}
