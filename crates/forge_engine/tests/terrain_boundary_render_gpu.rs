//! Real raster coverage for the source CDLOD lattice and rectangular clipping.
use bevy::mesh::{Indices, Mesh, VertexAttributeValues};
use forge_engine::source_terrain::{
    lod::{PreparedLod, ViewGeometry},
    sampling,
};
use forge_project::{
    HeightSamplingAlgorithm, TERRAIN_DOCUMENT_SCHEMA_VERSION_V4, TerrainExtent,
    authoring::new_terrain,
    sampled_terrain::{HeightEncoding, SAMPLED_TERRAIN_SCHEMA, SampledTerrainSource},
};
use forge_world::{sampled_terrain::SampledTerrainPlan, terrain_lod::CdlodView};
use std::fmt::Write as _;
use wgpu::util::DeviceExt;

#[test]
#[ignore = "requires a hardware GPU and the pinned source terrain shader"]
#[allow(
    clippy::too_many_lines,
    clippy::cast_possible_truncation,
    clippy::cast_precision_loss
)]
fn clipped_rectangular_cdlod_raster_has_no_holes_or_outside_pixels() {
    bevy::tasks::block_on(async {
        let source_path = std::env::var_os("FORGE_QUALIFICATION_TERRAIN_WGSL").unwrap();
        let bytes = std::fs::read(source_path).unwrap();
        assert_eq!(
            forge_core::bytes_sha256(&bytes).to_string(),
            "a8e3e7558e0980702838bcad508ab61f46e399dcf1176e067b5cc5a9405b95a5"
        );
        let source = std::str::from_utf8(&bytes).unwrap();
        let functions = format!(
            "struct TerrainLodVertex{}",
            source.split_once("struct TerrainLodVertex").unwrap().1
        );
        let mut doc = new_terrain("gpu-coverage", "authoring", "Independent terrain").unwrap();
        doc.schema_version = TERRAIN_DOCUMENT_SCHEMA_VERSION_V4;
        doc.extent_m = TerrainExtent::new(23.0, 11.0);
        doc.height_sampling.algorithm = HeightSamplingAlgorithm::SampledRasterV1;
        doc.height_sampling.sample_spacing_m = 0.5;
        doc.height_sampling.base_elevation_m = 0.0;
        doc.height_sampling.amplitude_m = 0.0;
        doc.height_sampling.base_frequency_cycles_per_m = 1.0;
        doc.height_sampling.octaves = 1;
        doc.height_sampling.lacunarity = 2.0;
        doc.height_sampling.persistence = 0.5;
        let heights = vec![0_u8; 46 * 22 * 4];
        doc.sampled_source = Some(SampledTerrainSource {
            schema: SAMPLED_TERRAIN_SCHEMA.into(),
            schema_version: 1,
            width: 46,
            height: 22,
            origin_xz_m: [-11.5, -5.5],
            sample_spacing_m: 0.5,
            outside_height_m: -90.0,
            encoding: HeightEncoding::Float32Le,
            samples_sha256: forge_core::bytes_sha256(&heights),
        });
        doc.validate().unwrap();
        let plan = SampledTerrainPlan::from_document(&doc, &heights).unwrap();
        let geometry = ViewGeometry::new(&plan).unwrap();
        let config = ViewGeometry::config();
        let rows = config.morph_rows().unwrap();
        let mut assignments = String::new();
        for (i, r) in rows.iter().enumerate() {
            write!(
                assignments,
                "terrainLod.morph[{i}]=vec4f({:?},{:?},{:?},{:?});",
                r[0] as f32, r[1] as f32, r[2] as f32, r[3] as f32
            )
            .unwrap();
        }
        let shader = format!(
            r"
struct TerrainLodParams {{ morph:array<vec4f,9> }};
var<private> terrainLod:TerrainLodParams;
struct LodInput {{ @location(0) position:vec3f, @location(2) node:vec4f }};
fn terrainHeightAt(xz:vec2f)->f32 {{ return xz.x * 0.0; }}
@vertex fn vs(v:LodInput)->@builtin(position) vec4f {{
    {assignments}
    let snapped=terrainLodSnapped(v.node,v.position.xz);
    let cv=terrainLodMorph(v.node,v.position.xz,vec3f(-15.0,0.0,0.0),terrainHeightAt(snapped));
    let P=vec3f(cv.worldXZ.x,terrainHeightAt(cv.worldXZ),cv.worldXZ.y);
    return vec4f(P.x / 16.0,P.z / 16.0,0.0,1.0);
}}
@fragment fn fs()->@location(0) vec4f {{ return vec4f(1.0); }}
{functions}"
        );
        let shader = sampling::clip_vertex_shader(&shader, &doc).unwrap();
        let view = CdlodView {
            position: [-15.0, 0.0, 0.0],
            planes: [[0.0; 4]; 6],
        };
        let nodes = geometry.nodes(view).unwrap();
        assert!(nodes.iter().any(|n| n.lod == 0));
        assert!(nodes.iter().any(|n| n.lod == 1));
        let prepared = PreparedLod::new(&plan, &heights, view).unwrap().unwrap();
        assert_eq!(prepared.node_count, nodes.len());
        let mesh = &prepared.mesh;
        let Some(VertexAttributeValues::Float32x3(positions)) =
            mesh.attribute(Mesh::ATTRIBUTE_POSITION)
        else {
            panic!("terrain positions missing");
        };
        let (_, VertexAttributeValues::Float32x4(node_data)) = mesh
            .attributes()
            .find(|(attribute, _)| attribute.name == "TerrainLodNode")
            .expect("terrain node attribute")
        else {
            panic!("terrain node attribute has wrong type");
        };
        let Some(Indices::U32(indices)) = mesh.indices() else {
            panic!("terrain indices missing");
        };
        let mut vertices = Vec::new();
        for index in indices {
            let index = usize::try_from(*index).unwrap();
            for value in positions[index].iter().chain(&node_data[index]) {
                vertices.extend_from_slice(&value.to_le_bytes());
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
            "GPU: {} ({:?}, {:?}); selected nodes: {:?}",
            info.name, info.backend, info.device_type, nodes
        );
        let (device, queue) = adapter
            .request_device(&wgpu::DeviceDescriptor::default())
            .await
            .unwrap();
        let vertex = device.create_buffer_init(&wgpu::util::BufferInitDescriptor {
            label: Some("actual CDLOD vertices"),
            contents: &vertices,
            usage: wgpu::BufferUsages::VERTEX,
        });
        let module = device.create_shader_module(wgpu::ShaderModuleDescriptor {
            label: Some("clipped terrain raster"),
            source: wgpu::ShaderSource::Wgsl(shader.into()),
        });
        let attributes = [
            wgpu::VertexAttribute {
                format: wgpu::VertexFormat::Float32x3,
                offset: 0,
                shader_location: 0,
            },
            wgpu::VertexAttribute {
                format: wgpu::VertexFormat::Float32x4,
                offset: 12,
                shader_location: 2,
            },
        ];
        let vertex_layout = wgpu::VertexBufferLayout {
            array_stride: 28,
            step_mode: wgpu::VertexStepMode::Vertex,
            attributes: &attributes,
        };
        let targets = [Some(wgpu::ColorTargetState {
            format: wgpu::TextureFormat::Rgba8Unorm,
            blend: None,
            write_mask: wgpu::ColorWrites::ALL,
        })];
        let pipeline = device.create_render_pipeline(&wgpu::RenderPipelineDescriptor {
            label: Some("clipped CDLOD coverage"),
            layout: None,
            vertex: wgpu::VertexState {
                module: &module,
                entry_point: Some("vs"),
                compilation_options: wgpu::PipelineCompilationOptions::default(),
                buffers: &[vertex_layout],
            },
            primitive: wgpu::PrimitiveState::default(),
            depth_stencil: None,
            multisample: wgpu::MultisampleState::default(),
            fragment: Some(wgpu::FragmentState {
                module: &module,
                entry_point: Some("fs"),
                compilation_options: wgpu::PipelineCompilationOptions::default(),
                targets: &targets,
            }),
            multiview_mask: None,
            cache: None,
        });
        let size = wgpu::Extent3d {
            width: 64,
            height: 64,
            depth_or_array_layers: 1,
        };
        let target = device.create_texture(&wgpu::TextureDescriptor {
            label: Some("CDLOD coverage"),
            size,
            mip_level_count: 1,
            sample_count: 1,
            dimension: wgpu::TextureDimension::D2,
            format: wgpu::TextureFormat::Rgba8Unorm,
            usage: wgpu::TextureUsages::RENDER_ATTACHMENT | wgpu::TextureUsages::COPY_SRC,
            view_formats: &[],
        });
        let target_view = target.create_view(&wgpu::TextureViewDescriptor::default());
        let output = device.create_buffer(&wgpu::BufferDescriptor {
            label: Some("CDLOD pixels"),
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
                label: Some("CDLOD raster"),
                color_attachments: &attachments,
                depth_stencil_attachment: None,
                timestamp_writes: None,
                occlusion_query_set: None,
                multiview_mask: None,
            });
            pass.set_pipeline(&pipeline);
            pass.set_vertex_buffer(0, vertex.slice(..));
            pass.draw(0..u32::try_from(vertices.len() / 28).unwrap(), 0..1);
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
            size,
        );
        let submitted = queue.submit([encoder.finish()]);
        let (tx, rx) = std::sync::mpsc::channel();
        output
            .slice(..)
            .map_async(wgpu::MapMode::Read, move |r| tx.send(r).unwrap());
        device
            .poll(wgpu::PollType::Wait {
                submission_index: Some(submitted),
                timeout: Some(std::time::Duration::from_secs(30)),
            })
            .unwrap();
        rx.recv_timeout(std::time::Duration::from_secs(5))
            .unwrap()
            .unwrap();
        let mapped = output.slice(..).get_mapped_range();
        let mut inside = 0;
        for y in 0..64 {
            for x in 0..64 {
                let world_x = (x as f32 + 0.5 - 32.0) * 0.5;
                let world_z = (y as f32 + 0.5 - 32.0) * 0.5;
                let expected = world_x.abs() < 11.5 && world_z.abs() < 5.5;
                let pixel = mapped[(y * 64 + x) * 4];
                assert_eq!(
                    pixel,
                    if expected { 255 } else { 0 },
                    "pixel {x},{y} world {world_x},{world_z}"
                );
                inside += usize::from(expected);
            }
        }
        eprintln!(
            "validated {inside} interior and {} exterior pixels",
            64 * 64 - inside
        );
    });
}
