//! Generated GPU calibration advanced once per frame; no wait loop or file input.
use super::source_srgb_curve::{NativeSrgbBoundaryWitness, NativeSrgbCurve, NativeSrgbDeviceKey};
#[cfg(not(target_arch = "wasm32"))]
use bevy::render::render_resource::PollType;
use bevy::{
    platform::time::Instant,
    render::{
        render_resource::{
            BindGroupDescriptor, BindGroupEntry, BufferAsyncError, BufferDescriptor, BufferUsages,
            ColorTargetState, ColorWrites, CommandEncoderDescriptor, Extent3d, LoadOp, MapMode,
            Operations, Origin3d, RawFragmentState, RawRenderPipelineDescriptor, RawVertexState,
            RenderPassColorAttachment, RenderPassDescriptor, ShaderModuleDescriptor, ShaderSource,
            StoreOp, TexelCopyBufferInfo, TexelCopyBufferLayout, TexelCopyTextureInfo,
            TextureAspect, TextureDescriptor, TextureDimension, TextureFormat, TextureUsages,
        },
        renderer::{RenderDevice, RenderQueue},
    },
};
use std::{
    borrow::Cow,
    sync::{Mutex, mpsc},
    time::Duration,
};

struct PendingProbe {
    output: wgpu::Buffer,
    ready: Mutex<mpsc::Receiver<Result<(), BufferAsyncError>>>,
    started: Instant,
    bytes: usize,
}
pub(super) struct Calibration {
    pipeline: wgpu::RenderPipeline,
    key: NativeSrgbDeviceKey,
    low: Vec<u32>,
    high: Vec<u32>,
    mids: Vec<u32>,
    searches: u8,
    witnesses: Vec<Vec<u8>>,
    pending: Option<PendingProbe>,
    witnessing: bool,
}
impl Calibration {
    pub(super) fn new(device: &RenderDevice, key: NativeSrgbDeviceKey) -> Self {
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
        Self {
            pipeline,
            key,
            low: vec![0; 255],
            high: vec![1.0f32.to_bits(); 255],
            mids: Vec::new(),
            searches: 0,
            witnesses: Vec::new(),
            pending: None,
            witnessing: false,
        }
    }
    #[expect(
        clippy::too_many_lines,
        reason = "The asynchronous GPU submission keeps buffer lifetimes and readback setup in one operation"
    )]
    fn submit(
        &mut self,
        device: &RenderDevice,
        queue: &RenderQueue,
        values: &[f32],
    ) -> Result<(), String> {
        let gpu = device.wgpu_device();
        let pipeline = &self.pipeline;
        let layout = pipeline.get_bind_group_layout(0);
        let width = 64u32;
        if values.is_empty() || !values.len().is_multiple_of(64) {
            return Err("Invalid generated probe length".into());
        }
        let height =
            u32::try_from(values.len() / 64).map_err(|_| "Generated probe exceeds GPU limits")?;
        if !values.iter().all(|x| x.is_finite()) {
            return Err("Nonfinite generated probe".into());
        }
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
            pass.set_pipeline(pipeline);
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
        output.slice(..).map_async(MapMode::Read, move |result| {
            let _ = tx.send(result);
        });
        self.pending = Some(PendingProbe {
            output,
            ready: Mutex::new(rx),
            started: Instant::now(),
            bytes: inputs.len(),
        });
        Ok(())
    }
    pub(super) fn poll(
        &mut self,
        device: &RenderDevice,
        queue: &RenderQueue,
    ) -> Result<Option<NativeSrgbCurve>, String> {
        // Browser map_async callbacks are driven by the JS event loop. Polling
        // once on native allows the very same producer to be GPU-tested there.
        #[cfg(not(target_arch = "wasm32"))]
        device
            .wgpu_device()
            .poll(PollType::Poll)
            .map_err(|e| e.to_string())?;
        if let Some(pending) = &self.pending {
            let result = pending
                .ready
                .lock()
                .map_err(|_| "Calibration callback lock poisoned")?
                .try_recv();
            match result {
                Ok(result) => result.map_err(|e| e.to_string())?,
                Err(mpsc::TryRecvError::Empty) => {
                    if pending.started.elapsed() > Duration::from_secs(30) {
                        return Err("Asynchronous sRGB map timed out".into());
                    }
                    return Ok(None);
                }
                Err(e) => return Err(e.to_string()),
            }
            let pending = self.pending.take().unwrap();
            let bytes = pending.output.slice(..).get_mapped_range().to_vec();
            pending.output.unmap();
            if bytes.len() != pending.bytes
                || bytes
                    .as_chunks::<4>()
                    .0
                    .iter()
                    .any(|p| p[0] != p[1] || p[0] != p[2] || p[3] != 255)
            {
                return Err("Asynchronous sRGB readback is inconsistent".into());
            }
            if self.witnessing {
                self.witnesses.push(bytes);
                if self.witnesses.len() == 3 {
                    if self.witnesses[0] != self.witnesses[1]
                        || self.witnesses[1] != self.witnesses[2]
                    {
                        return Err("Asynchronous sRGB witnesses are unstable".into());
                    }
                    let bytes = &self.witnesses[0];
                    if bytes[510 * 4] != 0 || bytes[511 * 4] != 255 {
                        return Err("Asynchronous sRGB endpoints failed".into());
                    }
                    let rows: Vec<_> = (0..255)
                        .map(|i| NativeSrgbBoundaryWitness {
                            code: u16::try_from(i + 1).expect("Boundary code is at most 255"),
                            lower_bits: self.low[i],
                            threshold_bits: self.high[i],
                            lower_observed_code: bytes[i * 8],
                            threshold_observed_code: bytes[i * 8 + 4],
                        })
                        .collect();
                    return NativeSrgbCurve::from_native_witnesses(self.key.clone(), &rows)
                        .map(Some)
                        .map_err(str::to_string);
                }
            } else {
                for i in 0..255 {
                    if self.high[i] - self.low[i] > 1 {
                        if bytes[i * 4] as usize > i {
                            self.high[i] = self.mids[i];
                        } else {
                            self.low[i] = self.mids[i];
                        }
                    }
                }
                self.searches += 1;
                self.witnessing = self.low.iter().zip(&self.high).all(|(l, h)| h - l == 1);
            }
        }
        let values = if self.witnessing {
            self.low
                .iter()
                .zip(&self.high)
                .flat_map(|(l, h)| [f32::from_bits(*l), f32::from_bits(*h)])
                .chain([0.0, 1.0])
                .collect::<Vec<_>>()
        } else {
            if self.searches >= 32 {
                return Err("Asynchronous sRGB search failed to converge".into());
            }
            self.mids = self
                .low
                .iter()
                .zip(&self.high)
                .map(|(l, h)| l + (h - l) / 2)
                .collect();
            self.mids
                .iter()
                .map(|v| f32::from_bits(*v))
                .chain([0.0])
                .collect::<Vec<_>>()
        };
        self.submit(device, queue, &values)?;
        Ok(None)
    }
}
