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
    reason = "One ordered native probe retains GPU boundary search, adjacent-value checks and repeated readback evidence together"
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
    let device_info = serde_json::json!({"vendor":info.vendor,"device":info.device,"driver":info.driver.clone(),"driver_info":info.driver_info.clone()});
    let errors: Arc<Mutex<Vec<String>>> = Arc::new(Mutex::new(Vec::new()));
    let sink = errors.clone();
    device
        .wgpu_device()
        .on_uncaptured_error(Arc::new(move |e| sink.lock().unwrap().push(e.to_string())));

    let coarse: serde_json::Value =
        serde_json::from_slice(&std::fs::read(root.join("INPUT-BRACKETS.v1.json"))?)?;
    assert_eq!(coarse["device_info"], device_info);
    assert_eq!(coarse["adapter"], adapter);
    assert_eq!(coarse["backend"], backend);
    let rows = coarse["brackets"].as_array().ok_or("Brackets absent")?;
    assert_eq!(rows.len(), 255);
    let mut low = Vec::new();
    let mut high = Vec::new();
    for (i, row) in rows.iter().enumerate() {
        assert_eq!(row["code"].as_u64(), Some(i as u64 + 1));
        low.push(u32::try_from(
            row["low_bits"].as_u64().ok_or("Low bits absent")?,
        )?);
        high.push(u32::try_from(
            row["high_bits"].as_u64().ok_or("High bits absent")?,
        )?);
    }
    assert!(low.iter().zip(&high).all(|(l, h)| l < h));
    let gpu = device.wgpu_device();
    let shader=gpu.create_shader_module(ShaderModuleDescriptor{label:Some("Native sRGB boundary probes"),source:ShaderSource::Wgsl(Cow::Borrowed(r"
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
        label: Some("Real native sRGB boundaries"),
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
    let probe = |values: &[f32], tag: &str| -> Result<Vec<u8>, Box<dyn std::error::Error>> {
        let width = 64u32;
        assert_eq!(values.len() % 64, 0);
        let height = u32::try_from(values.len() / 64)?;
        assert!(values.iter().all(|x| x.is_finite()));
        let inputs: Vec<u8> = values.iter().flat_map(|x| x.to_le_bytes()).collect();
        std::fs::write(root.join(format!("{tag}.inputs.f32")), &inputs)?;
        let input = gpu.create_buffer(&BufferDescriptor {
            label: Some("Generated boundary values"),
            size: inputs.len() as u64,
            usage: BufferUsages::STORAGE | BufferUsages::COPY_DST,
            mapped_at_creation: false,
        });
        queue.write_buffer(&input, 0, &inputs);
        let group = gpu.create_bind_group(&BindGroupDescriptor {
            label: Some("Generated boundary inputs only"),
            layout: &layout,
            entries: &[BindGroupEntry {
                binding: 0,
                resource: input.as_entire_binding(),
            }],
        });
        let texture = gpu.create_texture(&TextureDescriptor {
            label: Some("Native sRGB boundary attachment"),
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
        let output = gpu.create_buffer(&BufferDescriptor {
            label: Some("Raw boundary readback"),
            size: inputs.len() as u64,
            usage: BufferUsages::MAP_READ | BufferUsages::COPY_DST,
            mapped_at_creation: false,
        });
        let mut encoder = gpu.create_command_encoder(&CommandEncoderDescriptor {
            label: Some("sRGB boundary pass"),
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
                label: Some("Native sRGB transfer without HDR intermediate"),
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
                return Err("sRGB boundary map timed out".into());
            }
            std::thread::sleep(Duration::from_millis(1));
        }
        let bytes = output.slice(..).get_mapped_range().to_vec();
        output.unmap();
        assert_eq!(bytes.len(), inputs.len());
        for p in bytes.as_chunks::<4>().0 {
            assert_eq!(p[0], p[1]);
            assert_eq!(p[0], p[2]);
            assert_eq!(p[3], 255);
        }
        std::fs::write(root.join(format!("{tag}.rgba8srgb")), &bytes)?;
        Ok(bytes)
    };
    let mut steps = 0;
    while low.iter().zip(&high).any(|(l, h)| h - l > 1) {
        assert!(steps < 32);
        let mids: Vec<u32> = low
            .iter()
            .zip(&high)
            .map(|(l, h)| l + (h - l) / 2)
            .collect();
        let mut values: Vec<f32> = mids.iter().map(|v| f32::from_bits(*v)).collect();
        values.push(0.0);
        let observed = probe(&values, &format!("SEARCH-{steps:02}"))?;
        for i in 0..255 {
            if high[i] - low[i] > 1 {
                if usize::from(observed[i * 4]) > i {
                    high[i] = mids[i];
                } else {
                    low[i] = mids[i];
                }
            }
        }
        steps += 1;
    }
    assert!(low.iter().zip(&high).all(|(l, h)| h - l == 1));
    assert!(high.windows(2).all(|p| p[0] < p[1]));
    let mut adjacent = Vec::new();
    for i in 0..255 {
        adjacent.push(f32::from_bits(low[i]));
        adjacent.push(f32::from_bits(high[i]));
    }
    adjacent.extend([0.0, 1.0]);
    let mut buffers = Vec::new();
    for repeat in 0..3 {
        buffers.push(probe(&adjacent, &format!("ADJACENT-{repeat}"))?);
    }
    assert_eq!(buffers[0], buffers[1]);
    assert_eq!(buffers[1], buffers[2]);
    let bytes = &buffers[0];
    for i in 0..255 {
        assert_eq!(bytes[i * 8], u8::try_from(i)?);
        assert_eq!(bytes[i * 8 + 4], u8::try_from(i + 1)?);
    }
    assert_eq!(bytes[510 * 4], 0);
    assert_eq!(bytes[511 * 4], 255);
    assert!(errors.lock().unwrap().is_empty(), "GPU validation errors");
    let mut thresholds = vec![0.0f32];
    thresholds.extend(high.iter().map(|b| f32::from_bits(*b)));
    let curve: Vec<u8> = thresholds.iter().flat_map(|x| x.to_le_bytes()).collect();
    std::fs::write(root.join("NATIVE-SRGB-INPUT-THRESHOLDS.f32"), &curve)?;
    let boundaries:Vec<_>=(0..255).map(|i|serde_json::json!({"code":i+1,"lower_bits":low[i],"threshold_bits":high[i],"lower_value":f32::from_bits(low[i]),"threshold_value":f32::from_bits(high[i]),"lower_observed_code":bytes[i*8],"threshold_observed_code":bytes[i*8+4]})).collect();
    let result = serde_json::json!({"status":"native_generated_srgb_boundaries_and_adjacent_values_verified","adapter":adapter,"backend":backend,"device_info":device_info,"search_steps":steps,"boundaries":boundaries,"exact_adjacent_float32_boundaries":255,"repeated_adjacent_raw_readbacks_byte_equal":3,"format":"Rgba8UnormSrgb","gpu_validation_errors":0,"purchased_asset_inputs":0,"unity_pixel_inputs":0,"source_pixel_fit":false,"installed_in_product":false,"full_import":false});
    std::fs::write(
        root.join("NATIVE-RESULT.v1.json"),
        serde_json::to_vec_pretty(&result)?,
    )?;
    Ok(())
}
