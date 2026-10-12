//! Generic native sRGB transfer probes. No purchased assets or Unity data loaded.
use bevy::{
    app::PluginsState,
    prelude::*,
    render::{
        RenderApp, RenderPlugin,
        pipelined_rendering::PipelinedRenderingPlugin,
        render_resource::{
            BindGroupDescriptor, BindGroupEntry, BufferDescriptor, BufferUsages, ColorTargetState,
            ColorWrites, CommandEncoderDescriptor, Extent3d, LoadOp, MapMode, Operations, Origin3d,
            PollType, RawFragmentState, RawRenderPipelineDescriptor, RawVertexState,
            RenderPassColorAttachment, RenderPassDescriptor, ShaderModuleDescriptor, ShaderSource,
            StoreOp, TexelCopyBufferInfo, TexelCopyBufferLayout, TexelCopyTextureInfo,
            TextureAspect, TextureDescriptor, TextureDimension, TextureFormat, TextureUsages,
        },
        renderer::{RenderAdapterInfo, RenderDevice, RenderQueue},
        settings::{Backends, RenderCreation, WgpuSettings},
    },
    window::ExitCondition,
    winit::WinitPlugin,
};
use std::{
    borrow::Cow,
    path::Path,
    sync::{Arc, Mutex, mpsc},
    time::{Duration, Instant},
};
#[allow(
    clippy::too_many_lines,
    reason = "One ordered native probe retains input generation, GPU draw, repeated readback and validation evidence together"
)]
fn main() -> Result<(), Box<dyn std::error::Error>> {
    let args: Vec<_> = std::env::args().collect();
    let root = Path::new(&args[1]);
    let mut app = App::new();
    app.add_plugins(
        DefaultPlugins
            .set(WindowPlugin {
                primary_window: None,
                exit_condition: ExitCondition::DontExit,
                ..default()
            })
            .set(RenderPlugin {
                render_creation: RenderCreation::Automatic(Box::new(WgpuSettings {
                    backends: Some(Backends::DX12),
                    ..default()
                })),
                ..default()
            })
            .disable::<WinitPlugin>()
            .disable::<PipelinedRenderingPlugin>(),
    );
    let deadline = Instant::now() + Duration::from_secs(30);
    while app.plugins_state() != PluginsState::Ready {
        if Instant::now() > deadline {
            return Err("GPU initialization timed out".into());
        }
        std::thread::sleep(Duration::from_millis(10));
    }
    app.finish();
    app.cleanup();
    app.update();
    let rw = app.sub_app(RenderApp).world();
    let device = rw.resource::<RenderDevice>().clone();
    let queue = rw.resource::<RenderQueue>().clone();
    let info = rw.resource::<RenderAdapterInfo>();
    let adapter = info.name.clone();
    let backend = format!("{:?}", info.backend);
    let device_info = format!("{:?}", info.0);
    let errors: Arc<Mutex<Vec<String>>> = Arc::new(Mutex::new(Vec::new()));
    let sink = errors.clone();
    device
        .wgpu_device()
        .on_uncaptured_error(Arc::new(move |e| sink.lock().unwrap().push(e.to_string())));
    let width = 64u32;
    let height = 256u32;
    let count = usize::try_from(width * height)?;
    let sample_count = u16::try_from(count)?;
    let values: Vec<f32> = (0..sample_count)
        .map(|i| (f32::from(i) - 1.0) / (f32::from(sample_count) - 3.0))
        .collect();
    let input_bytes: Vec<u8> = values.iter().flat_map(|x| x.to_le_bytes()).collect();
    std::fs::write(root.join("GENERATED-VALUES.f32"), &input_bytes)?;
    let gpu = device.wgpu_device();
    let input = gpu.create_buffer(&BufferDescriptor {
        label: Some("Generated sRGB linear probes"),
        size: input_bytes.len() as u64,
        usage: BufferUsages::STORAGE | BufferUsages::COPY_DST,
        mapped_at_creation: false,
    });
    queue.write_buffer(&input, 0, &input_bytes);
    let shader=gpu.create_shader_module(ShaderModuleDescriptor{label:Some("Native sRGB storage probes"),source:ShaderSource::Wgsl(Cow::Borrowed(r"
 @group(0) @binding(0) var<storage,read> values:array<f32>;
 @vertex fn vs(@builtin(vertex_index) i:u32)->@builtin(position) vec4<f32>{var p=array<vec2<f32>,3>(vec2<f32>(-1.,-1.),vec2<f32>(3.,-1.),vec2<f32>(-1.,3.));return vec4<f32>(p[i],0.,1.);}
 @fragment fn fs(@builtin(position) p:vec4<f32>)->@location(0) vec4<f32>{let i=u32(p.y)*64u+u32(p.x);let x=values[i];return vec4<f32>(x,x,x,1.);}
 "))});
    let targets = [Some(ColorTargetState {
        format: TextureFormat::Rgba8UnormSrgb,
        blend: None,
        write_mask: ColorWrites::ALL,
    })];
    let pipeline = gpu.create_render_pipeline(&RawRenderPipelineDescriptor {
        label: Some("Real native sRGB transfer"),
        layout: None,
        vertex: RawVertexState {
            module: &shader,
            entry_point: Some("vs"),
            compilation_options: default(),
            buffers: &[],
        },
        primitive: default(),
        depth_stencil: None,
        multisample: default(),
        fragment: Some(RawFragmentState {
            module: &shader,
            entry_point: Some("fs"),
            compilation_options: default(),
            targets: &targets,
        }),
        multiview_mask: None,
        cache: None,
    });
    let layout = pipeline.get_bind_group_layout(0);
    let group = gpu.create_bind_group(&BindGroupDescriptor {
        label: Some("Generated probe inputs only"),
        layout: &layout,
        entries: &[BindGroupEntry {
            binding: 0,
            resource: input.as_entire_binding(),
        }],
    });
    let texture = gpu.create_texture(&TextureDescriptor {
        label: Some("sRGB probe attachment"),
        size: Extent3d {
            width,
            height,
            depth_or_array_layers: 1,
        },
        mip_level_count: 1,
        sample_count: 1,
        dimension: TextureDimension::D2,
        format: TextureFormat::Rgba8UnormSrgb,
        usage: TextureUsages::RENDER_ATTACHMENT | TextureUsages::COPY_SRC,
        view_formats: &[],
    });
    let view = texture.create_view(&default());
    let mut outputs = Vec::new();
    for repeat in 0..3 {
        let output = gpu.create_buffer(&BufferDescriptor {
            label: Some("Raw sRGB probe readback"),
            size: (count * 4) as u64,
            usage: BufferUsages::MAP_READ | BufferUsages::COPY_DST,
            mapped_at_creation: false,
        });
        let mut encoder = gpu.create_command_encoder(&CommandEncoderDescriptor {
            label: Some("sRGB transfer probe"),
        });
        {
            let attachments = [Some(RenderPassColorAttachment {
                view: &view,
                resolve_target: None,
                depth_slice: None,
                ops: Operations {
                    load: LoadOp::Clear(default()),
                    store: StoreOp::Store,
                },
            })];
            let mut pass = encoder.begin_render_pass(&RenderPassDescriptor {
                label: Some("Direct sRGB output without HDR intermediate"),
                color_attachments: &attachments,
                depth_stencil_attachment: None,
                timestamp_writes: None,
                occlusion_query_set: None,
                multiview_mask: None,
            });
            pass.set_pipeline(&pipeline);
            pass.set_bind_group(0, &group, &[]);
            pass.draw(0..3, 0..1);
        }
        encoder.copy_texture_to_buffer(
            TexelCopyTextureInfo {
                texture: &texture,
                mip_level: 0,
                origin: Origin3d::ZERO,
                aspect: TextureAspect::All,
            },
            TexelCopyBufferInfo {
                buffer: &output,
                layout: TexelCopyBufferLayout {
                    offset: 0,
                    bytes_per_row: Some(width * 4),
                    rows_per_image: Some(height),
                },
            },
            Extent3d {
                width,
                height,
                depth_or_array_layers: 1,
            },
        );
        queue.submit([encoder.finish()]);
        let (tx, rx) = mpsc::channel();
        output.slice(..).map_async(MapMode::Read, move |r| {
            let _ = tx.send(r);
        });
        let deadline = Instant::now() + Duration::from_secs(30);
        loop {
            device.wgpu_device().poll(PollType::Poll)?;
            match rx.try_recv() {
                Ok(r) => {
                    r?;
                    break;
                }
                Err(mpsc::TryRecvError::Empty) => {}
                Err(e) => return Err(e.into()),
            }
            if Instant::now() > deadline {
                return Err("sRGB probe map timed out".into());
            }
            std::thread::sleep(Duration::from_millis(1));
        }
        let bytes = output.slice(..).get_mapped_range().to_vec();
        output.unmap();
        assert_eq!(bytes.len(), count * 4);
        for p in bytes.as_chunks::<4>().0 {
            assert_eq!(p[0], p[1]);
            assert_eq!(p[0], p[2]);
            assert_eq!(p[3], 255);
        }
        std::fs::write(root.join(format!("NATIVE-SRGB-{repeat}.rgba8srgb")), &bytes)?;
        outputs.push(bytes);
    }
    assert!(errors.lock().unwrap().is_empty(), "GPU validation errors");
    assert_eq!(outputs[0], outputs[1]);
    assert_eq!(outputs[1], outputs[2]);
    let codes: Vec<u8> = outputs[0].as_chunks::<4>().0.iter().map(|p| p[0]).collect();
    assert!(codes.windows(2).all(|p| p[0] <= p[1]));
    assert_eq!(codes[0], 0);
    assert_eq!(*codes.last().unwrap(), 255);
    let mut histogram = [0usize; 256];
    for code in codes {
        histogram[usize::from(code)] += 1;
    }
    assert!(histogram.iter().all(|n| *n > 0));
    let result = serde_json::json!({"status":"native_generated_srgb_transfer_ramp_rendered_and_verified","adapter":adapter,"backend":backend,"device_info":device_info,"probe_values":count,"format":"Rgba8UnormSrgb","repeated_raw_readbacks_byte_equal":3,"all_256_codes_observed":true,"monotonic_codes":true,"same_rgb_channels":true,"alpha_all_255":true,"gpu_validation_errors":0,"hardware_transfer_without_hdr_intermediate":true,"purchased_asset_inputs":0,"unity_pixel_inputs":0,"code_histogram":histogram.to_vec(),"threshold_boundaries_qualified":false,"installed_in_product":false,"full_import":false});
    std::fs::write(
        root.join("NATIVE-RESULT.v1.json"),
        serde_json::to_vec_pretty(&result)?,
    )?;
    Ok(())
}
