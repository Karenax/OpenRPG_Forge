//! Native renderer calibration using only generated probe values.
use super::source_srgb_curve::NativeSrgbDeviceKey;
#[cfg(not(target_arch = "wasm32"))]
use super::source_srgb_curve::{NativeSrgbBoundaryWitness, NativeSrgbCurve};
#[cfg(not(target_arch = "wasm32"))]
use bevy::render::render_resource::{
    BindGroupDescriptor, BindGroupEntry, BufferDescriptor, BufferUsages, ColorTargetState,
    ColorWrites, CommandEncoderDescriptor, Extent3d, LoadOp, MapMode, Operations, Origin3d,
    PollType, RawFragmentState, RawRenderPipelineDescriptor, RawVertexState,
    RenderPassColorAttachment, RenderPassDescriptor, ShaderModuleDescriptor, ShaderSource, StoreOp,
    TexelCopyBufferInfo, TexelCopyBufferLayout, TexelCopyTextureInfo, TextureAspect,
    TextureDescriptor, TextureDimension, TextureFormat, TextureUsages,
};
use bevy::render::renderer::{RenderAdapterInfo, RenderDevice, RenderQueue};
#[cfg(not(target_arch = "wasm32"))]
use std::{
    borrow::Cow,
    sync::mpsc,
    time::{Duration, Instant},
};
pub(super) fn device_key(info: &RenderAdapterInfo) -> NativeSrgbDeviceKey {
    NativeSrgbDeviceKey {
        backend: format!("{:?}", info.backend),
        adapter: info.name.clone(),
        vendor: info.vendor,
        device: info.device,
        driver: info.driver.clone(),
    }
}
#[cfg(not(target_arch = "wasm32"))]
#[expect(
    clippy::too_many_lines,
    reason = "Boundary search and repeated adjacent witnesses form one ordered GPU measurement protocol"
)]
pub(super) fn measure(
    device: &RenderDevice,
    queue: &RenderQueue,
    info: &RenderAdapterInfo,
) -> Result<NativeSrgbCurve, Box<dyn std::error::Error>> {
    let mut low = vec![0u32; 255];
    let mut high = vec![1.0f32.to_bits(); 255];
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
            compilation_options: wgpu::PipelineCompilationOptions::default(),
            buffers: &[],
        },
        primitive: wgpu::PrimitiveState::default(),
        depth_stencil: None,
        multisample: wgpu::MultisampleState::default(),
        fragment: Some(RawFragmentState {
            module: &shader,
            entry_point: Some("fs"),
            compilation_options: wgpu::PipelineCompilationOptions::default(),
            targets: &targets,
        }),
        multiview_mask: None,
        cache: None,
    });
    let layout = pipeline.get_bind_group_layout(0);
    let probe = |values: &[f32], _tag: &str| -> Result<Vec<u8>, Box<dyn std::error::Error>> {
        let width = 64u32;
        assert_eq!(values.len() % 64, 0);
        let height = u32::try_from(values.len() / 64)?;
        assert!(values.iter().all(|x| x.is_finite()));
        let inputs: Vec<u8> = values.iter().flat_map(|x| x.to_le_bytes()).collect();

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
        let view = texture.create_view(&wgpu::TextureViewDescriptor::default());
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
                    load: LoadOp::Clear(wgpu::Color::default()),
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
        if bytes.len() != inputs.len() {
            return Err("Native sRGB readback length mismatch".into());
        }
        for p in bytes.as_chunks::<4>().0 {
            if p[0] != p[1] || p[0] != p[2] || p[3] != 255 {
                return Err("Native sRGB probe channels are inconsistent".into());
            }
        }

        Ok(bytes)
    };
    let mut steps = 0;
    while low.iter().zip(&high).any(|(l, h)| h - l > 1) {
        if steps >= 32 {
            return Err("Native sRGB boundary search failed to converge".into());
        }
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
                if observed[i * 4] as usize > i {
                    high[i] = mids[i];
                } else {
                    low[i] = mids[i];
                }
            }
        }
        steps += 1;
    }
    if !low.iter().zip(&high).all(|(l, h)| h - l == 1) {
        return Err("Native sRGB boundaries are not adjacent".into());
    }
    if !high.windows(2).all(|p| p[0] < p[1]) {
        return Err("Native sRGB boundaries are not monotonic".into());
    }
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
    if buffers[0] != buffers[1] || buffers[1] != buffers[2] {
        return Err("Native sRGB adjacent probes are unstable".into());
    }
    let bytes = &buffers[0];
    for i in 0..255 {
        if bytes[i * 8] != u8::try_from(i).expect("Boundary index is below 255")
            || bytes[i * 8 + 4] != u8::try_from(i + 1).expect("Boundary code is at most 255")
        {
            return Err("Native sRGB boundary witness is contradictory".into());
        }
    }
    if bytes[510 * 4] != 0 || bytes[511 * 4] != 255 {
        return Err("Native sRGB endpoint probe failed".into());
    }

    let witnesses: Vec<_> = (0..255)
        .map(|i| NativeSrgbBoundaryWitness {
            code: u16::try_from(i + 1).expect("Boundary code is at most 255"),
            lower_bits: low[i],
            threshold_bits: high[i],
            lower_observed_code: bytes[i * 8],
            threshold_observed_code: bytes[i * 8 + 4],
        })
        .collect();
    NativeSrgbCurve::from_native_witnesses(device_key(info), &witnesses).map_err(Into::into)
}
