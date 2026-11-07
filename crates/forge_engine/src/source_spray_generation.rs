//! GPU handoff for source spray ring slots across shoreline generations.
use wgpu::util::DeviceExt;

/// Shared by the resident sprite shader and the GPU qualification probe.
pub const CLASSIFIER_WGSL: &str = r"
fn residentShadowIsCurrent(slot:u32) -> bool {
    if (residentShadowCutoff[1u] == 0u) { return true; }
    if (slot >= 32768u) { return false; }
    if (residentShadowCutoff[1u] == 2u) { return true; }
    let head = residentSprayHead[0u];
    let newer = head - residentShadowCutoff[0u];
    if (newer >= 32768u) { return true; }
    let age = (head - 1u - slot) & 32767u;
    return age < newer;
}
";

const RETIRE_WGSL: &str = r"
@group(0) @binding(0) var<storage,read> head:array<u32>;
@group(0) @binding(1) var<storage,read_write> cutoff:array<u32>;
@compute @workgroup_size(1)
fn main() {
    if (cutoff[1u] == 1u && head[0u] - cutoff[0u] >= 32768u) { cutoff[1u] = 2u; }
}
";

/// Encodes a cutoff at the exact GPU handoff and retires it after a full ring.
/// State 0 preserves the untouched source, 1 filters old ring slots, and 2
/// marks a renewed ring while keeping non-ring slots independent of old crests.
/// Call `retire` after every emission pass while state 1 can be active.
pub struct ShadowHandoff {
    active: wgpu::Buffer,
    layout: wgpu::BindGroupLayout,
    pipeline: wgpu::ComputePipeline,
}

impl ShadowHandoff {
    #[must_use]
    pub fn new(device: &wgpu::Device) -> Self {
        let active = device.create_buffer_init(&wgpu::util::BufferInitDescriptor {
            label: Some("spray generation active flag"),
            contents: &1_u32.to_le_bytes(),
            usage: wgpu::BufferUsages::COPY_SRC,
        });
        let entry = |binding, read_only| wgpu::BindGroupLayoutEntry {
            binding,
            visibility: wgpu::ShaderStages::COMPUTE,
            ty: wgpu::BindingType::Buffer {
                ty: wgpu::BufferBindingType::Storage { read_only },
                has_dynamic_offset: false,
                min_binding_size: None,
            },
            count: None,
        };
        let layout = device.create_bind_group_layout(&wgpu::BindGroupLayoutDescriptor {
            label: Some("spray generation retirement"),
            entries: &[entry(0, true), entry(1, false)],
        });
        let shader = device.create_shader_module(wgpu::ShaderModuleDescriptor {
            label: Some("spray generation retirement"),
            source: wgpu::ShaderSource::Wgsl(RETIRE_WGSL.into()),
        });
        let pipeline_layout = device.create_pipeline_layout(&wgpu::PipelineLayoutDescriptor {
            label: Some("spray generation retirement"),
            bind_group_layouts: &[Some(&layout)],
            immediate_size: 0,
        });
        let pipeline = device.create_compute_pipeline(&wgpu::ComputePipelineDescriptor {
            label: Some("spray generation retirement"),
            layout: Some(&pipeline_layout),
            module: &shader,
            entry_point: Some("main"),
            compilation_options: wgpu::PipelineCompilationOptions::default(),
            cache: None,
        });
        Self {
            active,
            layout,
            pipeline,
        }
    }

    pub fn validate(head: &wgpu::Buffer, cutoff: &wgpu::Buffer) -> Result<(), &'static str> {
        if head == cutoff
            || head.size() < 4
            || cutoff.size() < 8
            || !head
                .usage()
                .contains(wgpu::BufferUsages::STORAGE | wgpu::BufferUsages::COPY_SRC)
            || !cutoff
                .usage()
                .contains(wgpu::BufferUsages::STORAGE | wgpu::BufferUsages::COPY_DST)
        {
            return Err("Invalid spray generation buffers");
        }
        Ok(())
    }

    /// Must be encoded after the old generation and before new emissions.
    pub fn begin(
        &self,
        encoder: &mut wgpu::CommandEncoder,
        head: &wgpu::Buffer,
        cutoff: &wgpu::Buffer,
    ) -> Result<(), &'static str> {
        Self::validate(head, cutoff)?;
        encoder.copy_buffer_to_buffer(head, 0, cutoff, 0, 4);
        encoder.copy_buffer_to_buffer(&self.active, 0, cutoff, 4, 4);
        Ok(())
    }

    /// Bind the persistent head and cutoff once for repeated retirement passes.
    pub fn bind(
        &self,
        device: &wgpu::Device,
        head: &wgpu::Buffer,
        cutoff: &wgpu::Buffer,
    ) -> Result<wgpu::BindGroup, &'static str> {
        Self::validate(head, cutoff)?;
        Ok(device.create_bind_group(&wgpu::BindGroupDescriptor {
            label: Some("spray generation retirement"),
            layout: &self.layout,
            entries: &[
                wgpu::BindGroupEntry {
                    binding: 0,
                    resource: head.as_entire_binding(),
                },
                wgpu::BindGroupEntry {
                    binding: 1,
                    resource: cutoff.as_entire_binding(),
                },
            ],
        }))
    }

    /// Encode after new emissions and before the sprites use the cutoff.
    pub fn retire(&self, encoder: &mut wgpu::CommandEncoder, group: &wgpu::BindGroup) {
        let mut pass = encoder.begin_compute_pass(&wgpu::ComputePassDescriptor::default());
        pass.set_pipeline(&self.pipeline);
        pass.set_bind_group(0, group, &[]);
        pass.dispatch_workgroups(1, 1, 1);
    }
}
