//! Actual GPU execution of the port against retained native white-texture fixtures.
//! This is shader/blend/channel-mask evidence, not complete particle admission.
use forge_core::{bytes_sha256, ContentHash};
use serde_json::Value;
use std::{future::Future, sync::Arc, task::{Context, Poll, Wake, Waker}, time::Duration};
use wgpu::util::DeviceExt;
struct ThreadWake(std::thread::Thread);
impl Wake for ThreadWake {
    fn wake(self: Arc<Self>) { self.0.unpark(); }
    fn wake_by_ref(self: &Arc<Self>) { self.0.unpark(); }
}
fn block_on<T>(future: impl Future<Output=T>) -> T {
    let mut future = std::pin::pin!(future);
    let waker = Waker::from(Arc::new(ThreadWake(std::thread::current())));
    let mut context = Context::from_waker(&waker);
    loop { match future.as_mut().poll(&mut context) {
        Poll::Ready(value) => return value,
        Poll::Pending => std::thread::park_timeout(Duration::from_millis(1)),
    }}
}
fn pins(value: &Value) {
    for (path, digest) in value["input_pins"].as_object().unwrap() {
        assert_eq!(bytes_sha256(&std::fs::read(path).unwrap()), ContentHash::parse(digest.as_str().unwrap()).unwrap());
    }
}
fn write_new(path: &std::path::Path, bytes: &[u8]) {
    use std::io::Write;
    let mut file = std::fs::OpenOptions::new().write(true).create_new(true).open(path).unwrap();
    file.write_all(bytes).unwrap(); file.sync_all().unwrap();
}
fn half_to_float(value: u16) -> f32 {
    let sign = u32::from(value & 0x8000) << 16;
    let exponent = (value >> 10) & 31;
    let mantissa = value & 1023;
    if exponent == 0 {
        if mantissa == 0 { return f32::from_bits(sign); }
        let magnitude = f32::from(mantissa) * (1.0 / 16777216.0);
        return if sign == 0 { magnitude } else { -magnitude };
    }
    let output_exponent = if exponent == 31 { 255 } else { u32::from(exponent) + 112 };
    f32::from_bits(sign | (output_exponent << 23) | (u32::from(mantissa) << 13))
}
fn main() {
    let input_path = std::env::args().nth(1).unwrap();
    let output = std::path::PathBuf::from(std::env::args().nth(2).unwrap());
    assert!(!output.exists()); std::fs::create_dir(&output).unwrap();
    let input = std::fs::read(&input_path).unwrap();
    let value: Value = serde_json::from_slice(&input).unwrap(); pins(&value);
    let instance = wgpu::Instance::new(wgpu::InstanceDescriptor {
        backends: wgpu::Backends::DX12, ..wgpu::InstanceDescriptor::new_without_display_handle()
    });
    let adapter = block_on(instance.request_adapter(&wgpu::RequestAdapterOptions {
        power_preference: wgpu::PowerPreference::HighPerformance, force_fallback_adapter: false, compatible_surface: None
    })).unwrap();
    assert!(adapter.get_texture_format_features(wgpu::TextureFormat::Rgba16Float).flags.contains(wgpu::TextureFormatFeatureFlags::BLENDABLE), "Native HDR half blending unavailable");
    let (device, queue) = block_on(adapter.request_device(&wgpu::DeviceDescriptor {
        required_features: wgpu::Features::empty(), required_limits: adapter.limits(), ..Default::default()
    })).unwrap();
    let info = adapter.get_info();
    let shader = device.create_shader_module(wgpu::ShaderModuleDescriptor {
        label: Some("Pinned original alpha particle port"), source: wgpu::ShaderSource::Wgsl(include_str!("fixture.wgsl").into())
    });
    let blend = wgpu::BlendComponent { src_factor: wgpu::BlendFactor::SrcAlpha,
        dst_factor: wgpu::BlendFactor::OneMinusSrcAlpha, operation: wgpu::BlendOperation::Add };
    let targets = [Some(wgpu::ColorTargetState { format: wgpu::TextureFormat::Rgba16Float,
        blend: Some(wgpu::BlendState {color: blend, alpha: blend}),
        write_mask: wgpu::ColorWrites::RED | wgpu::ColorWrites::GREEN | wgpu::ColorWrites::BLUE })];
    let pipeline = device.create_render_pipeline(&wgpu::RenderPipelineDescriptor {
        label: Some("Original alpha blend and RGB channel mask"), layout: None,
        vertex: wgpu::VertexState { module: &shader, entry_point: Some("vertex"), compilation_options: Default::default(), buffers: &[] },
        primitive: wgpu::PrimitiveState { cull_mode: None, ..Default::default() },
        depth_stencil: Some(wgpu::DepthStencilState { format: wgpu::TextureFormat::Depth32Float,
            depth_write_enabled: Some(false), depth_compare: Some(wgpu::CompareFunction::GreaterEqual), stencil: Default::default(), bias: Default::default() }),
        multisample: Default::default(),
        fragment: Some(wgpu::FragmentState { module: &shader, entry_point: Some("fragment"), compilation_options: Default::default(), targets: &targets }),
        multiview_mask: None, cache: None,
    });
    let layout = pipeline.get_bind_group_layout(0);
    let mut results = Vec::new();
    for (index, fixture) in value["fixtures"].as_array().unwrap().iter().enumerate() {
        let material_bytes = std::fs::read(fixture["uniform_blob"].as_str().unwrap()).unwrap();
        assert_eq!(material_bytes.len(), 48);
        let material = device.create_buffer_init(&wgpu::util::BufferInitDescriptor { label: None,
            contents: &material_bytes, usage: wgpu::BufferUsages::UNIFORM });
        let vertex_bytes: Vec<u8> = fixture["vertex_color"].as_array().unwrap().iter()
            .flat_map(|v| (v.as_f64().unwrap() as f32).to_le_bytes()).collect();
        assert_eq!(vertex_bytes.len(), 16);
        let vertex = device.create_buffer_init(&wgpu::util::BufferInitDescriptor { label: None,
            contents: &vertex_bytes, usage: wgpu::BufferUsages::UNIFORM });
        let bindings = device.create_bind_group(&wgpu::BindGroupDescriptor { label: None, layout: &layout,
            entries: &[wgpu::BindGroupEntry { binding: 0, resource: material.as_entire_binding() },
                wgpu::BindGroupEntry {binding: 1, resource: vertex.as_entire_binding()}] });
        let extent = wgpu::Extent3d { width: 1, height: 1, depth_or_array_layers: 1 };
        let color = device.create_texture(&wgpu::TextureDescriptor {label: None, size: extent, mip_level_count: 1,
            sample_count: 1, dimension: wgpu::TextureDimension::D2, format: wgpu::TextureFormat::Rgba16Float,
            usage: wgpu::TextureUsages::RENDER_ATTACHMENT | wgpu::TextureUsages::COPY_SRC, view_formats: &[] });
        let depth = device.create_texture(&wgpu::TextureDescriptor {label: None, size: extent, mip_level_count: 1,
            sample_count: 1, dimension: wgpu::TextureDimension::D2, format: wgpu::TextureFormat::Depth32Float,
            usage: wgpu::TextureUsages::RENDER_ATTACHMENT, view_formats: &[] });
        let color_view = color.create_view(&Default::default()); let depth_view = depth.create_view(&Default::default());
        let background = fixture["background"].as_array().unwrap();
        let mut encoder = device.create_command_encoder(&Default::default());
        {
            let attachments = [Some(wgpu::RenderPassColorAttachment { view: &color_view, depth_slice: None,
                resolve_target: None, ops: wgpu::Operations { load: wgpu::LoadOp::Clear(wgpu::Color {
                    r: background[0].as_f64().unwrap(), g: background[1].as_f64().unwrap(),
                    b: background[2].as_f64().unwrap(), a: background[3].as_f64().unwrap() }), store: wgpu::StoreOp::Store } })];
            let mut pass = encoder.begin_render_pass(&wgpu::RenderPassDescriptor {label: None,
                color_attachments: &attachments, depth_stencil_attachment: Some(wgpu::RenderPassDepthStencilAttachment {
                    view: &depth_view, depth_ops: Some(wgpu::Operations { load: wgpu::LoadOp::Clear(0.0), store: wgpu::StoreOp::Discard }), stencil_ops: None }),
                ..Default::default() });
            pass.set_pipeline(&pipeline); pass.set_bind_group(0, &bindings, &[]); pass.draw(0..3, 0..1);
        }
        let readback = device.create_buffer(&wgpu::BufferDescriptor {label: None, size: 256,
            usage: wgpu::BufferUsages::MAP_READ | wgpu::BufferUsages::COPY_DST, mapped_at_creation: false });
        encoder.copy_texture_to_buffer(wgpu::TexelCopyTextureInfo {texture: &color, mip_level: 0, origin: wgpu::Origin3d::ZERO, aspect: wgpu::TextureAspect::All},
            wgpu::TexelCopyBufferInfo {buffer: &readback, layout: wgpu::TexelCopyBufferLayout {offset: 0, bytes_per_row: Some(256), rows_per_image: Some(1)}}, extent);
        let submission = queue.submit([encoder.finish()]);
        let (tx, rx) = std::sync::mpsc::channel();
        readback.slice(..).map_async(wgpu::MapMode::Read, move |result| tx.send(result).unwrap());
        device.poll(wgpu::PollType::Wait {submission_index: Some(submission), timeout: Some(Duration::from_secs(30))}).unwrap();
        rx.recv_timeout(Duration::from_secs(30)).unwrap().unwrap();
        let mapped = readback.slice(..).get_mapped_range();
        let pixels: Vec<f32> = mapped[..8].chunks_exact(2).map(|chunk| half_to_float(u16::from_le_bytes(chunk.try_into().unwrap()))).collect();
        assert!(pixels.iter().all(|v| v.is_finite()));
        let raw_path = output.join(format!("fixture-{index:02}.rgba.f16")); write_new(&raw_path, &mapped[..8]);
        let decoded: Vec<u8> = pixels.iter().flat_map(|v| v.to_le_bytes()).collect();
        let path = output.join(format!("fixture-{index:02}.rgba.f32")); write_new(&path, &decoded);
        results.push(serde_json::json!({"index":index,"material_guid":fixture["material_guid"],"rgba_f32":path,"sha256":bytes_sha256(&decoded).to_string(),"gpu_rgba_f16":raw_path,"gpu_rgba_f16_sha256":bytes_sha256(&mapped[..8]).to_string(),"rgba":pixels}));
        drop(mapped); readback.unmap();
    }
    assert_eq!(results.len(), 16); pins(&value);
    assert_eq!(bytes_sha256(&std::fs::read(input_path).unwrap()), bytes_sha256(&input));
    let report = serde_json::json!({"adapter_name":info.name,"backend":format!("{:?}",info.backend),"input_sha256":bytes_sha256(&input).to_string(),"float32_blendable_enabled":false,"render_target_format":"Rgba16Float","white_linear_fixture_only":true,"rows":results,"full_import":false,"renderer_admitted":false});
    write_new(&output.join("RESULT.v1.json"), &serde_json::to_vec_pretty(&report).unwrap());
}
