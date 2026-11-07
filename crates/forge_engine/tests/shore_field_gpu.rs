//! Explicit hardware check of the shared shoreline field's world coordinates.
use forge_engine::{source_breaker_gpu, source_shore_field::Domain};
use wgpu::util::DeviceExt;

#[test]
#[ignore = "requires a hardware GPU and explicit qualification"]
#[allow(
    clippy::too_many_lines,
    clippy::cast_precision_loss,
    reason = "One bounded texture upload and readback exercise the real shader coordinates"
)]
fn expanded_shore_field_samples_the_same_world_frame_on_gpu() {
    const SIDE: u32 = 1024;
    const ORIGIN: [f32; 2] = [-2048.0, -1024.0];
    const SPACING: f32 = 4.0;
    let domain = Domain::preserving_spacing(
        ORIGIN.map(f64::from),
        4096.0,
        3.0,
        f64::from(SPACING),
        (SIDE * SIDE) as usize,
    )
    .unwrap();
    let source = source_breaker_gpu::COMMON_CREST_PROGRAM;
    let start = source.find("fn terrainShoreSample(").unwrap();
    let end = start + source[start..].find('}').unwrap() + 1;
    let function = domain.prepare_shader(&source[start..end]).unwrap();
    let shader = format!(
        "@group(0) @binding(0) var terrainShoreTex:texture_2d<f32>;\n\
         {function}\n\
         @group(0) @binding(1) var<storage, read> points:array<vec2f>;\n\
         @group(0) @binding(2) var<storage, read_write> values:array<vec4f>;\n\
         @compute @workgroup_size(1) fn main(@builtin(global_invocation_id) id:vec3u) {{\n\
             values[id.x] = terrainShoreSample(points[id.x]);\n\
         }}"
    );
    let indices = [
        [0.0_f32, 0.0_f32],
        [1.0, 2.0],
        [0.5, 0.5],
        [-5.0, 7.5],
        [513.25, 315.75],
        [1023.0, 500.0],
        [1022.0, 1022.0],
    ];
    let points: Vec<[f32; 2]> = indices
        .iter()
        .map(|index| {
            [
                ORIGIN[0] + (index[0] + 0.5) * SPACING,
                ORIGIN[1] + (index[1] + 0.5) * SPACING,
            ]
        })
        .collect();
    let mut pixels = Vec::with_capacity((SIDE * SIDE * 16) as usize);
    for z in 0..SIDE {
        for x in 0..SIDE {
            for value in [x as f32, z as f32, (x + z) as f32, 1.0] {
                pixels.extend_from_slice(&value.to_le_bytes());
            }
        }
    }
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
        let extent = wgpu::Extent3d {
            width: SIDE,
            height: SIDE,
            depth_or_array_layers: 1,
        };
        let texture = device.create_texture(&wgpu::TextureDescriptor {
            label: Some("expanded common shoreline field"),
            size: extent,
            mip_level_count: 1,
            sample_count: 1,
            dimension: wgpu::TextureDimension::D2,
            format: wgpu::TextureFormat::Rgba32Float,
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
            &pixels,
            wgpu::TexelCopyBufferLayout {
                offset: 0,
                bytes_per_row: Some(SIDE * 16),
                rows_per_image: Some(SIDE),
            },
            extent,
        );
        let point_bytes: Vec<u8> = points
            .iter()
            .flat_map(|point| point.iter())
            .flat_map(|value| value.to_le_bytes())
            .collect();
        let input = device.create_buffer_init(&wgpu::util::BufferInitDescriptor {
            label: Some("shoreline sample positions"),
            contents: &point_bytes,
            usage: wgpu::BufferUsages::STORAGE,
        });
        let size = u64::try_from(points.len() * 16).unwrap();
        let output = device.create_buffer(&wgpu::BufferDescriptor {
            label: Some("shoreline GPU samples"),
            size,
            usage: wgpu::BufferUsages::STORAGE | wgpu::BufferUsages::COPY_SRC,
            mapped_at_creation: false,
        });
        let readback = device.create_buffer(&wgpu::BufferDescriptor {
            label: Some("shoreline readback"),
            size,
            usage: wgpu::BufferUsages::MAP_READ | wgpu::BufferUsages::COPY_DST,
            mapped_at_creation: false,
        });
        let module = device.create_shader_module(wgpu::ShaderModuleDescriptor {
            label: Some("shared shoreline coordinate test"),
            source: wgpu::ShaderSource::Wgsl(shader.into()),
        });
        let pipeline = device.create_compute_pipeline(&wgpu::ComputePipelineDescriptor {
            label: Some("shared shoreline coordinate test"),
            layout: None,
            module: &module,
            entry_point: Some("main"),
            compilation_options: wgpu::PipelineCompilationOptions::default(),
            cache: None,
        });
        let view = texture.create_view(&wgpu::TextureViewDescriptor::default());
        let group = device.create_bind_group(&wgpu::BindGroupDescriptor {
            label: Some("shared shoreline coordinate test"),
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
        let mut encoder = device.create_command_encoder(&wgpu::CommandEncoderDescriptor::default());
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
        for (index, bytes) in indices.iter().zip(mapped.as_chunks::<16>().0.iter()) {
            let actual: Vec<f32> = bytes
                .as_chunks::<4>()
                .0
                .iter()
                .map(|chunk| f32::from_le_bytes(*chunk))
                .collect();
            let x = index[0].clamp(0.0, SIDE as f32 - 1.001);
            let z = index[1].clamp(0.0, SIDE as f32 - 1.001);
            for (actual, expected) in actual.iter().zip([x, z, x + z, 1.0]) {
                assert!(
                    (actual - expected).abs() < 0.001,
                    "shore index {index:?}: {actual} versus {expected}"
                );
            }
        }
        eprintln!(
            "passed expanded shoreline sampling: {} points",
            points.len()
        );
    });
}
