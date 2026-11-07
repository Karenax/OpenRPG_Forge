//! Run explicitly on a hardware GPU; compares shader readback with the world plan.
use forge_engine::{sampled_terrain_corrections::Corrections, source_terrain::sampling};
use forge_project::{
    HeightSamplingAlgorithm, TERRAIN_DOCUMENT_SCHEMA_VERSION_V4, TerrainExtent, TerrainHeightDelta,
    authoring::new_terrain,
    sampled_terrain::{HeightEncoding, SAMPLED_TERRAIN_SCHEMA, SampledTerrainSource},
};
use forge_world::sampled_terrain::SampledTerrainPlan;
use wgpu::util::DeviceExt;

type BoundaryCase = ([f32; 4], [f32; 2], [f32; 2], [f32; 2]);

fn fixture(
    width: u16,
    height: u16,
    spacing: f32,
    outside: f32,
    edited: bool,
) -> SampledTerrainPlan {
    let mut doc = new_terrain("gpu-test", "authoring", "Independent terrain").unwrap();
    doc.schema_version = TERRAIN_DOCUMENT_SCHEMA_VERSION_V4;
    doc.extent_m = TerrainExtent::new(f32::from(width) * spacing, f32::from(height) * spacing);
    doc.height_sampling.algorithm = HeightSamplingAlgorithm::SampledRasterV1;
    doc.height_sampling.sample_spacing_m = spacing;
    doc.height_sampling.base_elevation_m = 3.0;
    doc.height_sampling.amplitude_m = 0.0;
    doc.height_sampling.base_frequency_cycles_per_m = 1.0;
    doc.height_sampling.octaves = 1;
    doc.height_sampling.lacunarity = 2.0;
    doc.height_sampling.persistence = 0.5;
    let bytes: Vec<_> = (0..height)
        .flat_map(|z| (0..width).map(move |x| f32::from(x) * 0.75 - f32::from(z) * 0.25))
        .flat_map(f32::to_le_bytes)
        .collect();
    doc.sampled_source = Some(SampledTerrainSource {
        schema: SAMPLED_TERRAIN_SCHEMA.into(),
        schema_version: 1,
        width: u32::from(width),
        height: u32::from(height),
        origin_xz_m: [
            -f64::from(doc.extent_m.size_x_m) * 0.5,
            -f64::from(doc.extent_m.size_z_m) * 0.5,
        ],
        sample_spacing_m: f64::from(spacing),
        outside_height_m: outside,
        encoding: HeightEncoding::Float32Le,
        samples_sha256: forge_core::bytes_sha256(&bytes),
    });
    if edited {
        doc.height_deltas = vec![
            TerrainHeightDelta::new(0, 0, -2.0),
            TerrainHeightDelta::new(2, 3, 4.0),
            TerrainHeightDelta::new(width, height, 8.0),
        ];
    }
    SampledTerrainPlan::from_document(&doc, &bytes).unwrap()
}

#[test]
#[ignore = "requires the explicitly supplied, pinned source terrain shader"]
fn acquired_source_shader_adapts_without_changing_other_functions() {
    let path = std::env::var_os("FORGE_QUALIFICATION_TERRAIN_WGSL")
        .expect("provide the pinned terrain-lod.wgsl path");
    let bytes = std::fs::read(&path).expect("read the source terrain shader");
    assert_eq!(
        forge_core::bytes_sha256(&bytes).to_string(),
        "a8e3e7558e0980702838bcad508ab61f46e399dcf1176e067b5cc5a9405b95a5"
    );
    let source = std::str::from_utf8(&bytes).unwrap();
    let plan = fixture(11, 19, 0.5, -12.0, true);
    let adapted = sampling::prepare_shader(source, plan.document()).unwrap();
    let before = "fn terrainHeightAt(";
    let after = "struct TerrainLodVertex";
    assert_eq!(
        source.split_once(before).unwrap().0,
        adapted.split_once(before).unwrap().0
    );
    assert_eq!(
        source.split_once(after).unwrap().1,
        adapted.split_once(after).unwrap().1
    );
    assert!(adapted.contains("f>=vec2f(10.0,18.0)"));
    assert!(adapted.contains("return -12.0;"));
    let clipped = sampling::clip_vertex_shader(&adapted, plan.document()).unwrap();
    assert!(clipped.contains("let forgeUnmorphedXZ=v.node.xy+v.position.xz*v.node.z;"));
    assert!(clipped.contains("terrainHeightAt(forgeClippedXZ)"));
    assert!(clipped.contains("let low=vec2f(-2.75,-4.75);"));
    let reference = fixture(2048, 2048, 1.0, -90.0, false);
    assert_eq!(
        sampling::clip_vertex_shader(source, reference.document()).unwrap(),
        source
    );
    let surface_path = std::path::PathBuf::from(path).with_file_name("terrain.wgsl");
    let surface_bytes = std::fs::read(surface_path).expect("read the source terrain surface");
    assert_eq!(
        forge_core::bytes_sha256(&surface_bytes).to_string(),
        "8651d33064d6ca6b0ecd56989d65dfe658abd3ce339d2bac21c39889e24baaad"
    );
    let surface = std::str::from_utf8(&surface_bytes).unwrap();
    let mapped = sampling::prepare_surface_shader(surface, plan.document()).unwrap();
    assert_eq!(
        surface.split_once("fn terrainUvOf(").unwrap().0,
        mapped.split_once("fn terrainUvOf(").unwrap().0
    );
    assert_eq!(
        surface.split_once("// exact bilinear height").unwrap().1,
        mapped.split_once("// exact bilinear height").unwrap().1
    );
    assert!(mapped.contains("(xz-vec2f(-2.75,-4.75))/vec2f(5.5,9.5)"));
    assert_eq!(
        sampling::prepare_surface_shader(surface, reference.document()).unwrap(),
        surface
    );
}

#[test]
#[ignore = "requires a hardware GPU and explicit qualification"]
#[allow(clippy::too_many_lines)] // Resource lifetime and GPU readback form one qualification.
fn rectangular_corrections_match_world_heights_on_gpu() {
    bevy::tasks::block_on(async {
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
            "GPU: {} ({:?}, {:?})",
            info.name, info.backend, info.device_type
        );
        let (device, queue) = adapter
            .request_device(&wgpu::DeviceDescriptor::default())
            .await
            .unwrap();
        for (width, height, spacing, outside, edited) in [
            (11_u16, 19_u16, 0.5_f32, -90.0_f32, true),
            (8, 4, 2.0, -12.0, true),
            (11, 19, 0.5, -90.0, false),
        ] {
            let plan = fixture(width, height, spacing, outside, edited);
            let corrections = Corrections::from_document(plan.document()).unwrap();
            let origin = [
                -f32::from(width) * spacing * 0.5,
                -f32::from(height) * spacing * 0.5,
            ];
            let points: Vec<[f32; 2]> = [
                [-0.25, 0.0],
                [0.0, 0.0],
                [0.5, 0.5],
                [1.0, 1.0],
                [1.5, 2.5],
                [2.0, 3.0],
                [2.25, 3.75],
                [f32::from(width) - 1.0, f32::from(height) - 1.0],
                [f32::from(width) - 0.625, 1.0],
                [f32::from(width) - 0.5, f32::from(height) - 0.5],
                [f32::from(width), f32::from(height)],
                [f32::from(width) + 0.25, f32::from(height)],
            ]
            .into_iter()
            .map(|p| [origin[0] + p[0] * spacing, origin[1] + p[1] * spacing])
            .collect();
            // Adapt a source evaluator, then apply the same authored correction
            // wrapper used by the terrain renderer to this nonuniform raster.
            let base = sampling::prepare_shader(
                "@group(0) @binding(3) var terrainHeightTex:texture_2d<f32>;\nfn terrainHeightAt(xz:vec2f)->f32 { return xz.x; }",
                plan.document(),
            )
            .unwrap();
            let shader = format!(
                "{}\n{}",
                corrections.shader(&base, "0", 0).unwrap(),
                r"
                @group(0) @binding(1) var<storage,read> points:array<vec2f>;
                @group(0) @binding(2) var<storage,read_write> values:array<f32>;
                @compute @workgroup_size(1) fn main(@builtin(global_invocation_id) id:vec3u) {
                    values[id.x]=terrainHeightAt(points[id.x]);
                }"
            );
            let texture = device.create_texture(&wgpu::TextureDescriptor {
                label: Some("independent terrain correction field"),
                size: wgpu::Extent3d {
                    width: corrections.width,
                    height: corrections.height,
                    depth_or_array_layers: 1,
                },
                mip_level_count: 1,
                sample_count: 1,
                dimension: wgpu::TextureDimension::D2,
                format: wgpu::TextureFormat::R32Float,
                usage: wgpu::TextureUsages::TEXTURE_BINDING | wgpu::TextureUsages::COPY_DST,
                view_formats: &[],
            });
            queue.write_texture(
                wgpu::TexelCopyTextureInfo {
                    texture: &texture,
                    mip_level: 0,
                    origin: wgpu::Origin3d::ZERO,
                    aspect: wgpu::TextureAspect::All,
                },
                &corrections.bytes,
                wgpu::TexelCopyBufferLayout {
                    offset: 0,
                    bytes_per_row: Some(corrections.width * 4),
                    rows_per_image: Some(corrections.height),
                },
                wgpu::Extent3d {
                    width: corrections.width,
                    height: corrections.height,
                    depth_or_array_layers: 1,
                },
            );
            let height_size = wgpu::Extent3d {
                width: u32::from(width),
                height: u32::from(height),
                depth_or_array_layers: 1,
            };
            let height_texture = device.create_texture(&wgpu::TextureDescriptor {
                label: Some("independent terrain source heights"),
                size: height_size,
                mip_level_count: 1,
                sample_count: 1,
                dimension: wgpu::TextureDimension::D2,
                format: wgpu::TextureFormat::R32Float,
                usage: wgpu::TextureUsages::TEXTURE_BINDING | wgpu::TextureUsages::COPY_DST,
                view_formats: &[],
            });
            let height_bytes: Vec<_> = plan
                .raster()
                .heights()
                .iter()
                .flat_map(|v| v.to_le_bytes())
                .collect();
            queue.write_texture(
                wgpu::TexelCopyTextureInfo {
                    texture: &height_texture,
                    mip_level: 0,
                    origin: wgpu::Origin3d::ZERO,
                    aspect: wgpu::TextureAspect::All,
                },
                &height_bytes,
                wgpu::TexelCopyBufferLayout {
                    offset: 0,
                    bytes_per_row: Some(u32::from(width) * 4),
                    rows_per_image: Some(u32::from(height)),
                },
                height_size,
            );
            let input: Vec<_> = points
                .iter()
                .flat_map(|point| point.iter())
                .flat_map(|value| value.to_le_bytes())
                .collect();
            let inputs = device.create_buffer_init(&wgpu::util::BufferInitDescriptor {
                label: Some("terrain sample positions"),
                contents: &input,
                usage: wgpu::BufferUsages::STORAGE,
            });
            let size = u64::try_from(points.len() * 4).unwrap();
            let output = device.create_buffer(&wgpu::BufferDescriptor {
                label: Some("terrain GPU heights"),
                size,
                usage: wgpu::BufferUsages::STORAGE | wgpu::BufferUsages::COPY_SRC,
                mapped_at_creation: false,
            });
            let readback = device.create_buffer(&wgpu::BufferDescriptor {
                label: Some("terrain heights readback"),
                size,
                usage: wgpu::BufferUsages::MAP_READ | wgpu::BufferUsages::COPY_DST,
                mapped_at_creation: false,
            });
            let module = device.create_shader_module(wgpu::ShaderModuleDescriptor {
                label: Some("terrain corrections"),
                source: wgpu::ShaderSource::Wgsl(shader.into()),
            });
            let pipeline = device.create_compute_pipeline(&wgpu::ComputePipelineDescriptor {
                label: Some("terrain corrections"),
                layout: None,
                module: &module,
                entry_point: Some("main"),
                compilation_options: wgpu::PipelineCompilationOptions::default(),
                cache: None,
            });
            let view = texture.create_view(&wgpu::TextureViewDescriptor::default());
            let height_view = height_texture.create_view(&wgpu::TextureViewDescriptor::default());
            let group = device.create_bind_group(&wgpu::BindGroupDescriptor {
                label: Some("terrain corrections"),
                layout: &pipeline.get_bind_group_layout(0),
                entries: &[
                    wgpu::BindGroupEntry {
                        binding: 0,
                        resource: wgpu::BindingResource::TextureView(&view),
                    },
                    wgpu::BindGroupEntry {
                        binding: 1,
                        resource: inputs.as_entire_binding(),
                    },
                    wgpu::BindGroupEntry {
                        binding: 2,
                        resource: output.as_entire_binding(),
                    },
                    wgpu::BindGroupEntry {
                        binding: 3,
                        resource: wgpu::BindingResource::TextureView(&height_view),
                    },
                ],
            });
            let mut encoder =
                device.create_command_encoder(&wgpu::CommandEncoderDescriptor::default());
            {
                let mut pass = encoder.begin_compute_pass(&wgpu::ComputePassDescriptor::default());
                pass.set_pipeline(&pipeline);
                pass.set_bind_group(0, &group, &[]);
                pass.dispatch_workgroups(u32::try_from(points.len()).unwrap(), 1, 1);
            }
            encoder.copy_buffer_to_buffer(&output, 0, &readback, 0, size);
            let submitted = queue.submit([encoder.finish()]);
            let (tx, rx) = std::sync::mpsc::channel();
            readback
                .slice(..)
                .map_async(wgpu::MapMode::Read, move |result| tx.send(result).unwrap());
            device
                .poll(wgpu::PollType::Wait {
                    submission_index: Some(submitted),
                    timeout: Some(std::time::Duration::from_secs(30)),
                })
                .unwrap();
            rx.recv_timeout(std::time::Duration::from_secs(5))
                .unwrap()
                .unwrap();
            let mapped = readback.slice(..).get_mapped_range();
            for (point, chunk) in points.iter().zip(mapped.as_chunks::<4>().0) {
                let actual = f32::from_le_bytes(*chunk);
                let expected = plan
                    .height_at(f64::from(point[0]), f64::from(point[1]))
                    .unwrap();
                assert!(
                    (f64::from(actual) - expected).abs() < 0.0001,
                    "{width}x{height} spacing={spacing}, edited={edited}, at {point:?}: GPU {actual}, world {expected}"
                );
            }
            eprintln!(
                "passed {width}x{height}, spacing={spacing}, outside={outside}, edited={edited}: {} points",
                points.len()
            );
        }
    });
}

#[test]
#[ignore = "requires a hardware GPU and explicit qualification"]
#[allow(clippy::too_many_lines)]
fn rectangular_vertex_boundaries_match_on_gpu() {
    bevy::tasks::block_on(async {
        let plan = fixture(11, 19, 0.5, -12.0, false);
        let source = r"
struct Input { node:vec4f, position:vec4f, morphed:vec4f };
struct LodInput { node:vec4f, position:vec3f };
struct LodVertex { worldXZ:vec2f };
@group(0) @binding(0) var<storage,read> inputs:array<Input>;
@group(0) @binding(1) var<storage,read_write> results:array<vec4f>;
fn terrainHeightAt(xz:vec2f)->f32 { return xz.x+xz.y; }
@compute @workgroup_size(1) fn main(@builtin(global_invocation_id) id:vec3u) {
    let item=inputs[id.x];
    let v=LodInput(item.node,item.position.xyz);
    let cv=LodVertex(item.morphed.xy);
    let P=vec3f(cv.worldXZ.x,terrainHeightAt(cv.worldXZ),cv.worldXZ.y);
    results[id.x]=vec4f(P.x,P.z,P.y,1.0);
}";
        let shader = sampling::clip_vertex_shader(source, plan.document()).unwrap();
        // node origin/size, local vertex (x, z), morphed vertex (x, z), expected.
        let cases: [BoundaryCase; 6] = [
            (
                [-2.75, -4.75, 8.0, 0.0],
                [0.0, 0.0],
                [-3.0, -5.0],
                [-2.75, -4.75],
            ),
            (
                [-5.25, -3.25, 8.0, 0.0],
                [1.0, 1.0],
                [2.5, 4.5],
                [2.75, 4.75],
            ),
            (
                [-2.75, -4.75, 8.0, 0.0],
                [0.5, 0.5],
                [1.0, -1.0],
                [1.0, -1.0],
            ),
            (
                [-2.75, -4.75, 8.0, 0.0],
                [0.5, 0.5],
                [20.0, -20.0],
                [2.75, -4.75],
            ),
            (
                [-2.75, -4.75, 8.0, 0.0],
                [1.0, 0.5],
                [4.5, -1.0],
                [2.75, -1.0],
            ),
            (
                [5.25, -4.75, 8.0, 0.0],
                [0.0, 0.5],
                [4.5, -1.0],
                [2.75, -1.0],
            ),
        ];
        let mut bytes = Vec::new();
        for (node, local, morphed, _) in cases {
            for value in node
                .into_iter()
                .chain([local[0], 0.0, local[1], 0.0])
                .chain([morphed[0], morphed[1], 0.0, 0.0])
            {
                bytes.extend_from_slice(&value.to_le_bytes());
            }
        }
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
            "GPU: {} ({:?}, {:?})",
            info.name, info.backend, info.device_type
        );
        let (device, queue) = adapter
            .request_device(&wgpu::DeviceDescriptor::default())
            .await
            .unwrap();
        let input = device.create_buffer_init(&wgpu::util::BufferInitDescriptor {
            label: Some("terrain clipping input"),
            contents: &bytes,
            usage: wgpu::BufferUsages::STORAGE,
        });
        let size = u64::try_from(cases.len() * 16).unwrap();
        let output = device.create_buffer(&wgpu::BufferDescriptor {
            label: Some("terrain clipping output"),
            size,
            usage: wgpu::BufferUsages::STORAGE | wgpu::BufferUsages::COPY_SRC,
            mapped_at_creation: false,
        });
        let readback = device.create_buffer(&wgpu::BufferDescriptor {
            label: Some("terrain clipping readback"),
            size,
            usage: wgpu::BufferUsages::MAP_READ | wgpu::BufferUsages::COPY_DST,
            mapped_at_creation: false,
        });
        let module = device.create_shader_module(wgpu::ShaderModuleDescriptor {
            label: Some("terrain boundary adaptation"),
            source: wgpu::ShaderSource::Wgsl(shader.into()),
        });
        let pipeline = device.create_compute_pipeline(&wgpu::ComputePipelineDescriptor {
            label: Some("terrain boundary adaptation"),
            layout: None,
            module: &module,
            entry_point: Some("main"),
            compilation_options: wgpu::PipelineCompilationOptions::default(),
            cache: None,
        });
        let group = device.create_bind_group(&wgpu::BindGroupDescriptor {
            label: Some("terrain boundary adaptation"),
            layout: &pipeline.get_bind_group_layout(0),
            entries: &[
                wgpu::BindGroupEntry {
                    binding: 0,
                    resource: input.as_entire_binding(),
                },
                wgpu::BindGroupEntry {
                    binding: 1,
                    resource: output.as_entire_binding(),
                },
            ],
        });
        let mut encoder = device.create_command_encoder(&wgpu::CommandEncoderDescriptor::default());
        {
            let mut pass = encoder.begin_compute_pass(&wgpu::ComputePassDescriptor::default());
            pass.set_pipeline(&pipeline);
            pass.set_bind_group(0, &group, &[]);
            pass.dispatch_workgroups(u32::try_from(cases.len()).unwrap(), 1, 1);
        }
        encoder.copy_buffer_to_buffer(&output, 0, &readback, 0, size);
        let submitted = queue.submit([encoder.finish()]);
        let (tx, rx) = std::sync::mpsc::channel();
        readback
            .slice(..)
            .map_async(wgpu::MapMode::Read, move |result| tx.send(result).unwrap());
        device
            .poll(wgpu::PollType::Wait {
                submission_index: Some(submitted),
                timeout: Some(std::time::Duration::from_secs(30)),
            })
            .unwrap();
        rx.recv_timeout(std::time::Duration::from_secs(5))
            .unwrap()
            .unwrap();
        let mapped = readback.slice(..).get_mapped_range();
        for (case, chunk) in cases.iter().zip(mapped.as_chunks::<16>().0) {
            let floats = chunk
                .as_chunks::<4>()
                .0
                .iter()
                .map(|value| f32::from_le_bytes(*value))
                .collect::<Vec<_>>();
            assert_eq!(
                [floats[0].to_bits(), floats[1].to_bits()],
                case.3.map(f32::to_bits)
            );
            assert_eq!(floats[2].to_bits(), (case.3[0] + case.3[1]).to_bits());
        }
        eprintln!(
            "passed {} boundary vertices on 11x19 half-metre terrain",
            cases.len()
        );
    });
}

#[test]
#[ignore = "requires a hardware GPU and explicit qualification"]
#[allow(clippy::too_many_lines)]
fn rectangular_surface_uvs_match_document_on_gpu() {
    bevy::tasks::block_on(async {
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
            "GPU: {} ({:?}, {:?})",
            info.name, info.backend, info.device_type
        );
        let (device, queue) = adapter
            .request_device(&wgpu::DeviceDescriptor::default())
            .await
            .unwrap();
        for (width, height, spacing) in [(11_u16, 19_u16, 0.5_f32), (8, 4, 2.0)] {
            let plan = fixture(width, height, spacing, -12.0, false);
            let size = [f32::from(width) * spacing, f32::from(height) * spacing];
            let expected = [
                [0.0_f32, 0.0_f32],
                [0.25, 0.75],
                [0.5, 0.5],
                [1.0, 1.0],
                [-0.25, 1.25],
            ];
            let points = expected.map(|uv| [(uv[0] - 0.5) * size[0], (uv[1] - 0.5) * size[1]]);
            let source = r"
fn terrainUvOf(xz:vec2f)->vec2f {return xz;}
@group(0) @binding(0) var<storage,read> points:array<vec2f>;
@group(0) @binding(1) var<storage,read_write> values:array<vec2f>;
@compute @workgroup_size(1) fn main(@builtin(global_invocation_id) id:vec3u) {
    values[id.x]=terrainUvOf(points[id.x]);
}";
            let shader = sampling::prepare_surface_shader(source, plan.document()).unwrap();
            let data: Vec<u8> = points
                .iter()
                .flatten()
                .flat_map(|value| value.to_le_bytes())
                .collect();
            let input = device.create_buffer_init(&wgpu::util::BufferInitDescriptor {
                label: Some("terrain UV positions"),
                contents: &data,
                usage: wgpu::BufferUsages::STORAGE,
            });
            let size_bytes = u64::try_from(data.len()).unwrap();
            let output = device.create_buffer(&wgpu::BufferDescriptor {
                label: Some("terrain UV result"),
                size: size_bytes,
                usage: wgpu::BufferUsages::STORAGE | wgpu::BufferUsages::COPY_SRC,
                mapped_at_creation: false,
            });
            let readback = device.create_buffer(&wgpu::BufferDescriptor {
                label: Some("terrain UV readback"),
                size: size_bytes,
                usage: wgpu::BufferUsages::MAP_READ | wgpu::BufferUsages::COPY_DST,
                mapped_at_creation: false,
            });
            let module = device.create_shader_module(wgpu::ShaderModuleDescriptor {
                label: Some("terrain surface UV"),
                source: wgpu::ShaderSource::Wgsl(shader.into()),
            });
            let pipeline = device.create_compute_pipeline(&wgpu::ComputePipelineDescriptor {
                label: Some("terrain surface UV"),
                layout: None,
                module: &module,
                entry_point: Some("main"),
                compilation_options: wgpu::PipelineCompilationOptions::default(),
                cache: None,
            });
            let group = device.create_bind_group(&wgpu::BindGroupDescriptor {
                label: Some("terrain surface UV"),
                layout: &pipeline.get_bind_group_layout(0),
                entries: &[
                    wgpu::BindGroupEntry {
                        binding: 0,
                        resource: input.as_entire_binding(),
                    },
                    wgpu::BindGroupEntry {
                        binding: 1,
                        resource: output.as_entire_binding(),
                    },
                ],
            });
            let mut encoder =
                device.create_command_encoder(&wgpu::CommandEncoderDescriptor::default());
            {
                let mut pass = encoder.begin_compute_pass(&wgpu::ComputePassDescriptor::default());
                pass.set_pipeline(&pipeline);
                pass.set_bind_group(0, &group, &[]);
                pass.dispatch_workgroups(u32::try_from(points.len()).unwrap(), 1, 1);
            }
            encoder.copy_buffer_to_buffer(&output, 0, &readback, 0, size_bytes);
            let submitted = queue.submit([encoder.finish()]);
            let (tx, rx) = std::sync::mpsc::channel();
            readback
                .slice(..)
                .map_async(wgpu::MapMode::Read, move |result| tx.send(result).unwrap());
            device
                .poll(wgpu::PollType::Wait {
                    submission_index: Some(submitted),
                    timeout: Some(std::time::Duration::from_secs(30)),
                })
                .unwrap();
            rx.recv_timeout(std::time::Duration::from_secs(5))
                .unwrap()
                .unwrap();
            let mapped = readback.slice(..).get_mapped_range();
            for (want, bytes) in expected.iter().zip(mapped.as_chunks::<8>().0) {
                let parts = bytes.as_chunks::<4>().0;
                let actual = [f32::from_le_bytes(parts[0]), f32::from_le_bytes(parts[1])];
                for axis in 0..2 {
                    assert!(
                        (actual[axis] - want[axis]).abs() < 1e-6,
                        "{width}x{height}: {actual:?} expected {want:?}"
                    );
                }
            }
            eprintln!(
                "passed {} surface UV coordinates for {width}x{height}",
                points.len()
            );
        }
    });
}

#[test]
#[ignore = "requires a hardware GPU"]
#[allow(clippy::too_many_lines)]
fn rectangular_hill_lookup_uses_the_enclosing_world_domain_on_gpu() {
    bevy::tasks::block_on(async {
        let plan = fixture(11, 19, 0.5, -12.0, false);
        let source = include_str!("../src/source_terrain/engine_surface.wgsl");
        let receiver_source =
            sampling::prepare_hill_shadow_shader(source, plan.document()).unwrap();
        let function = receiver_source
            .split_once("fn terrainSunShadowAt(")
            .unwrap()
            .1
            .split_once("struct ObjectShadowSettings")
            .unwrap()
            .0;
        let terrain_shader = format!(
            r"
@group(0) @binding(0) var terrainSunShadowTex:texture_2d<f32>;
@group(0) @binding(1) var<storage,read> points:array<vec4f>;
@group(0) @binding(2) var<storage,read_write> values:array<f32>;
fn terrainSunShadowAt({function}
@compute @workgroup_size(1) fn main(@builtin(global_invocation_id) id:vec3u) {{
 values[id.x]=terrainSunShadowAt(points[id.x].xyz);
}}"
        );
        let water_source = sampling::prepare_water_hill_shadow_shader(
            forge_engine::source_breaker_gpu::COMMON_CREST_PROGRAM,
            plan.document(),
        )
        .unwrap();
        let start = water_source.find("fn terrainSunShadowAt(").unwrap();
        let end = start + water_source[start..].find('}').unwrap() + 1;
        let water_shader = format!(
            r"
struct TerrainParams {{ sunBaked:f32, }};
var<private> terrainParams:TerrainParams;
@group(0) @binding(0) var terrainSunShadowTex:texture_2d<f32>;
@group(0) @binding(1) var<storage,read> points:array<vec4f>;
@group(0) @binding(2) var<storage,read_write> values:array<f32>;
{}
@compute @workgroup_size(1) fn main(@builtin(global_invocation_id) id:vec3u) {{
 terrainParams.sunBaked=1.0;
 values[id.x]=terrainSunShadowAt(points[id.x].xyz);
}}",
            &water_source[start..end]
        );
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
            "GPU: {} ({:?}, {:?})",
            info.name, info.backend, info.device_type
        );
        let (device, queue) = adapter
            .request_device(&wgpu::DeviceDescriptor::default())
            .await
            .unwrap();
        let image = device.create_texture(&wgpu::TextureDescriptor {
            label: Some("hill lookup texels"),
            size: wgpu::Extent3d {
                width: 2,
                height: 2,
                depth_or_array_layers: 1,
            },
            mip_level_count: 1,
            sample_count: 1,
            dimension: wgpu::TextureDimension::D2,
            format: wgpu::TextureFormat::Rgba32Float,
            usage: wgpu::TextureUsages::TEXTURE_BINDING | wgpu::TextureUsages::COPY_DST,
            view_formats: &[],
        });
        let texels: Vec<_> = [0.0_f32, 1.0, 2.0, 3.0]
            .into_iter()
            .flat_map(|h| [h, 0.0, 0.0, 1.0])
            .flat_map(f32::to_le_bytes)
            .collect();
        queue.write_texture(
            image.as_image_copy(),
            &texels,
            wgpu::TexelCopyBufferLayout {
                offset: 0,
                bytes_per_row: Some(32),
                rows_per_image: Some(2),
            },
            wgpu::Extent3d {
                width: 2,
                height: 2,
                depth_or_array_layers: 1,
            },
        );
        let view = image.create_view(&wgpu::TextureViewDescriptor::default());
        // A 9.5 m square encloses the 5.5 x 9.5 m rectangle.
        let points = [
            [-2.375_f32, 1.5, -2.375, 0.0],
            [2.375, 1.5, -2.375, 0.0],
            [-2.375, 1.5, 2.375, 0.0],
            [2.375, 1.5, 2.375, 0.0],
            [-1.1875, 0.75, -1.1875, 0.0],
            [1.1875, 1.25, -1.1875, 0.0],
            [-1.1875, 1.75, 1.1875, 0.0],
            [1.1875, 2.25, 1.1875, 0.0],
        ];
        let bytes: Vec<_> = points
            .iter()
            .flatten()
            .flat_map(|x| x.to_le_bytes())
            .collect();
        let input = device.create_buffer_init(&wgpu::util::BufferInitDescriptor {
            label: Some("world points"),
            contents: &bytes,
            usage: wgpu::BufferUsages::STORAGE,
        });
        let output = device.create_buffer(&wgpu::BufferDescriptor {
            label: Some("visibility"),
            size: 32,
            usage: wgpu::BufferUsages::STORAGE | wgpu::BufferUsages::COPY_SRC,
            mapped_at_creation: false,
        });
        let readback = device.create_buffer(&wgpu::BufferDescriptor {
            label: Some("visibility readback"),
            size: 32,
            usage: wgpu::BufferUsages::MAP_READ | wgpu::BufferUsages::COPY_DST,
            mapped_at_creation: false,
        });
        for (label, shader) in [("terrain", terrain_shader), ("water", water_shader)] {
            let module = device.create_shader_module(wgpu::ShaderModuleDescriptor {
                label: Some("adapted hill receiver"),
                source: wgpu::ShaderSource::Wgsl(shader.into()),
            });
            let pipeline = device.create_compute_pipeline(&wgpu::ComputePipelineDescriptor {
                label: Some("hill receiver lookup"),
                layout: None,
                module: &module,
                entry_point: Some("main"),
                compilation_options: wgpu::PipelineCompilationOptions::default(),
                cache: None,
            });
            let group = device.create_bind_group(&wgpu::BindGroupDescriptor {
                label: Some("hill lookup bindings"),
                layout: &pipeline.get_bind_group_layout(0),
                entries: &[
                    wgpu::BindGroupEntry {
                        binding: 0,
                        resource: wgpu::BindingResource::TextureView(&view),
                    },
                    wgpu::BindGroupEntry {
                        binding: 1,
                        resource: input.as_entire_binding(),
                    },
                    wgpu::BindGroupEntry {
                        binding: 2,
                        resource: output.as_entire_binding(),
                    },
                ],
            });
            let mut encoder =
                device.create_command_encoder(&wgpu::CommandEncoderDescriptor::default());
            {
                let mut pass = encoder.begin_compute_pass(&wgpu::ComputePassDescriptor::default());
                pass.set_pipeline(&pipeline);
                pass.set_bind_group(0, &group, &[]);
                pass.dispatch_workgroups(8, 1, 1);
            }
            encoder.copy_buffer_to_buffer(&output, 0, &readback, 0, 32);
            let submission = queue.submit([encoder.finish()]);
            let (tx, rx) = std::sync::mpsc::channel();
            readback
                .slice(..)
                .map_async(wgpu::MapMode::Read, move |r| tx.send(r).unwrap());
            device
                .poll(wgpu::PollType::Wait {
                    submission_index: Some(submission),
                    timeout: Some(std::time::Duration::from_secs(30)),
                })
                .unwrap();
            rx.recv_timeout(std::time::Duration::from_secs(5))
                .unwrap()
                .unwrap();
            let bytes = readback.slice(..).get_mapped_range();
            let values: Vec<_> = bytes
                .as_chunks::<4>()
                .0
                .iter()
                .map(|v| f32::from_le_bytes(*v))
                .collect();
            for (got, want) in values
                .iter()
                .zip([1.0_f32, 1.0, 0.0, 0.0, 0.5, 0.5, 0.5, 0.5])
            {
                assert!(
                    (got - want).abs() < 0.01,
                    "{label} visibility {got}, expected {want}"
                );
            }
            drop(bytes);
            readback.unmap();
            eprintln!("verified eight {label} hill receiver lookups on a centred 9.5 m square");
        }
    });
}
