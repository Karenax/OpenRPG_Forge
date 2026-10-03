//! Source WGSL pipelines on the caller's existing Bevy renderer device.
//! Preparation validates and creates pipelines; it does not supply scene
//! resources, queue meshes, draw a frame or qualify an appearance.

use bevy::render::{
    render_resource::{BindGroupLayout, RenderPipeline},
    renderer::{RenderAdapter, RenderDevice},
};
use forge_assets::source_mesh_shaders::{
    SourceBinding, SourceBlendComponent, SourceBlendFactor, SourceBlendOperation, SourceBufferKind,
    SourceCull, SourceDepthCompare, SourceFrontFace, SourceMeshMaterial, SourceMeshProgram,
    SourceMeshShaderPackage, SourcePassKind, SourcePipelineState, SourceSamplerKind,
    SourceTargetFormat, SourceTextureDimension, SourceTextureSample, SourceTopology,
    SourceVertexFormat, SourceVertexStep,
};
use forge_core::ContentHash;
use std::{borrow::Cow, error::Error, fmt, num::NonZeroU64};

#[derive(Debug)]
pub enum SourcePipelineError {
    Package(String),
    Limit {
        name: &'static str,
        needed: u64,
        available: u64,
    },
    Policy(&'static str),
    Format {
        format: wgpu::TextureFormat,
        reason: &'static str,
    },
    Validation {
        phase: &'static str,
        message: String,
    },
}

impl fmt::Display for SourcePipelineError {
    fn fmt(&self, f: &mut fmt::Formatter<'_>) -> fmt::Result {
        match self {
            Self::Package(message) => write!(f, "source pipeline package: {message}"),
            Self::Limit {
                name,
                needed,
                available,
            } => write!(
                f,
                "source pipeline {name}: needs {needed}, device supplies {available}"
            ),
            Self::Policy(message) => write!(f, "source pipeline policy: {message}"),
            Self::Format { format, reason } => write!(f, "source pipeline {format:?}: {reason}"),
            Self::Validation { phase, message } => write!(f, "source pipeline {phase}: {message}"),
        }
    }
}
impl Error for SourcePipelineError {}

/// Owns the exact source layouts and a validated Bevy pipeline. Bind groups,
/// Draw buffers, resources and render-phase ownership remain the caller's work.
pub struct PreparedSourceMeshProgram {
    pub package_sha256: ContentHash,
    pub code_sha256: ContentHash,
    pub material: String,
    pub pass: SourcePassKind,
    pub late: bool,
    pub group0: BindGroupLayout,
    pub group1: BindGroupLayout,
    pub draw: BindGroupLayout,
    pub pipeline: RenderPipeline,
}

/// Does not create an Instance, adapter, device, queue or second renderer.
/// Both resources must come from the same existing Bevy `RenderApp`. Browser
/// validation futures are awaited, rather than treated as ready on first poll.
pub async fn prepare(
    device: &RenderDevice,
    adapter: &RenderAdapter,
    package: &SourceMeshShaderPackage,
    program: &SourceMeshProgram,
) -> Result<PreparedSourceMeshProgram, SourcePipelineError> {
    let code = package
        .code(program)
        .map_err(|e| SourcePipelineError::Package(e.to_string()))?;
    let material = package
        .manifest()
        .materials
        .iter()
        .find(|m| m.name == program.material)
        .ok_or_else(|| SourcePipelineError::Package("material missing".into()))?;
    let policy = program.pipeline_state.as_ref().ok_or_else(|| {
        SourcePipelineError::Package("explicit pipeline state v2 required".into())
    })?;
    admit(&device.limits(), program, material)?;
    let targets = color_targets(policy);
    for target in &targets {
        check_format(
            device,
            adapter,
            target.format,
            target.blend.is_some(),
            policy.multisample.count,
        )?;
    }
    let depth_format = format(program.pass.depth_format);
    check_format(
        device,
        adapter,
        depth_format,
        false,
        policy.multisample.count,
    )?;
    let scope = device
        .wgpu_device()
        .push_error_scope(wgpu::ErrorFilter::Validation);
    // Bevy also exposes an unsafe unchecked entry point. Source packages must
    // always use the checked API, preserving WGSL validation/runtime checks.
    let shader = device.create_and_validate_shader_module(wgpu::ShaderModuleDescriptor {
        label: Some(&program.material),
        source: wgpu::ShaderSource::Wgsl(Cow::Borrowed(code)),
    });
    if let Some(error) = scope.pop().await {
        return Err(SourcePipelineError::Validation {
            phase: "shader",
            message: error.to_string(),
        });
    }
    create_pipeline(
        device, package, program, material, policy, &shader, &targets,
    )
    .await
}

async fn create_pipeline(
    device: &RenderDevice,
    package: &SourceMeshShaderPackage,
    program: &SourceMeshProgram,
    material: &SourceMeshMaterial,
    policy: &SourcePipelineState,
    shader: &wgpu::ShaderModule,
    targets: &[wgpu::ColorTargetState],
) -> Result<PreparedSourceMeshProgram, SourcePipelineError> {
    let scope = device
        .wgpu_device()
        .push_error_scope(wgpu::ErrorFilter::Validation);
    let group0 = device.create_bind_group_layout("source mesh frame", &entries(&program.group0));
    let group1 =
        device.create_bind_group_layout("source mesh resources", &entries(&program.group1));
    let draw = device.create_bind_group_layout(
        "source mesh Draw",
        &[wgpu::BindGroupLayoutEntry {
            binding: program.draw_binding.binding,
            visibility: wgpu::ShaderStages::VERTEX_FRAGMENT,
            ty: wgpu::BindingType::Buffer {
                ty: wgpu::BufferBindingType::Uniform,
                has_dynamic_offset: true,
                min_binding_size: NonZeroU64::new(program.draw_binding.bytes as u64),
            },
            count: None,
        }],
    );
    let layout = device.create_pipeline_layout(&wgpu::PipelineLayoutDescriptor {
        label: Some("source mesh groups 0/1/2"),
        bind_group_layouts: &[Some(&group0), Some(&group1), Some(&draw)],
        immediate_size: 0,
    });
    let attributes: Vec<Vec<_>> = material
        .vertex_buffers
        .iter()
        .map(|buffer| {
            buffer
                .attributes
                .iter()
                .map(|attribute| wgpu::VertexAttribute {
                    shader_location: attribute.shader_location,
                    offset: attribute.offset as u64,
                    format: vertex_format(attribute.format),
                })
                .collect()
        })
        .collect();
    let buffers: Vec<_> = material
        .vertex_buffers
        .iter()
        .zip(&attributes)
        .map(|(buffer, attributes)| wgpu::VertexBufferLayout {
            array_stride: buffer.array_stride as u64,
            step_mode: match buffer.step_mode {
                SourceVertexStep::Vertex => wgpu::VertexStepMode::Vertex,
                SourceVertexStep::Instance => wgpu::VertexStepMode::Instance,
            },
            attributes,
        })
        .collect();
    let targets: Vec<_> = targets.iter().cloned().map(Some).collect();
    let pipeline = device.create_render_pipeline(&wgpu::RenderPipelineDescriptor {
        label: Some(&format!(
            "source {} {:?} late={}",
            program.material, program.pass.kind, program.pass.late
        )),
        layout: Some(&layout),
        vertex: wgpu::VertexState {
            module: shader,
            entry_point: Some("vs"),
            buffers: &buffers,
            compilation_options: wgpu::PipelineCompilationOptions::default(),
        },
        fragment: program.has_fragment.then_some(wgpu::FragmentState {
            module: shader,
            entry_point: Some("fs"),
            targets: &targets,
            compilation_options: wgpu::PipelineCompilationOptions::default(),
        }),
        primitive: primitive(policy),
        depth_stencil: Some(depth_stencil(policy)),
        multisample: wgpu::MultisampleState {
            count: policy.multisample.count,
            mask: policy.multisample.mask,
            alpha_to_coverage_enabled: policy.multisample.alpha_to_coverage_enabled,
        },
        multiview_mask: None,
        cache: None,
    });
    if let Some(error) = scope.pop().await {
        return Err(SourcePipelineError::Validation {
            phase: "layout/pipeline",
            message: error.to_string(),
        });
    }
    Ok(PreparedSourceMeshProgram {
        package_sha256: package.hash(),
        code_sha256: program.code.sha256,
        material: program.material.clone(),
        pass: program.pass.kind,
        late: program.pass.late,
        group0,
        group1,
        draw,
        pipeline,
    })
}

fn limit(name: &'static str, needed: u64, available: u64) -> Result<(), SourcePipelineError> {
    if needed > available {
        Err(SourcePipelineError::Limit {
            name,
            needed,
            available,
        })
    } else {
        Ok(())
    }
}

/// CPU admission using the actual device limits, including declared unused
/// bindings. Kept public for adapters to report readiness before GPU preparation.
pub fn admit(
    limits: &wgpu::Limits,
    program: &SourceMeshProgram,
    material: &SourceMeshMaterial,
) -> Result<(), SourcePipelineError> {
    program.pipeline_state.as_ref().ok_or_else(|| {
        SourcePipelineError::Package("explicit pipeline state v2 required".into())
    })?;
    limit("bind groups", 3, u64::from(limits.max_bind_groups))?;
    limit(
        "dynamic uniforms",
        1,
        u64::from(limits.max_dynamic_uniform_buffers_per_pipeline_layout),
    )?;
    if !(program.draw_binding.dynamic_stride as u64)
        .is_multiple_of(u64::from(limits.min_uniform_buffer_offset_alignment))
    {
        return Err(SourcePipelineError::Policy(
            "Draw stride differs from device alignment",
        ));
    }
    admit_stages(limits, program)?;
    for group in [&program.group0, &program.group1] {
        limit(
            "bindings/group",
            group.len() as u64,
            u64::from(limits.max_bindings_per_bind_group),
        )?;
        for binding in group {
            if let (Some(buffer), Some(block)) = (&binding.layout.buffer, &binding.uniform) {
                let available = match buffer.kind {
                    SourceBufferKind::Uniform => limits.max_uniform_buffer_binding_size,
                    SourceBufferKind::ReadOnlyStorage => limits.max_storage_buffer_binding_size,
                };
                limit("buffer binding bytes", block.bytes as u64, available)?;
            }
        }
    }
    limit(
        "Draw binding bytes",
        program.draw_binding.bytes as u64,
        limits.max_uniform_buffer_binding_size,
    )?;
    limit(
        "vertex buffers",
        material.vertex_buffers.len() as u64,
        u64::from(limits.max_vertex_buffers),
    )?;
    limit(
        "vertex attributes",
        material.vertex_layout.len() as u64,
        u64::from(limits.max_vertex_attributes),
    )?;
    for buffer in &material.vertex_buffers {
        limit(
            "vertex stride",
            buffer.array_stride as u64,
            u64::from(limits.max_vertex_buffer_array_stride),
        )?;
    }
    limit(
        "color attachments",
        program.pass.color_formats.len() as u64,
        u64::from(limits.max_color_attachments),
    )?;
    let bytes: u64 = program
        .pass
        .color_formats
        .iter()
        .map(|f| match f {
            SourceTargetFormat::Rgba16Float => 8,
            SourceTargetFormat::Rgba8Unorm | SourceTargetFormat::Depth32Float => 4,
        })
        .sum();
    limit(
        "color attachment bytes/sample",
        bytes,
        u64::from(limits.max_color_attachment_bytes_per_sample),
    )
}

fn admit_stages(
    limits: &wgpu::Limits,
    program: &SourceMeshProgram,
) -> Result<(), SourcePipelineError> {
    let requirements = program.declared_requirements();
    for stage in [requirements.vertex, requirements.fragment] {
        for (name, needed, available) in [
            (
                "sampled textures/stage",
                stage.sampled_textures,
                limits.max_sampled_textures_per_shader_stage,
            ),
            (
                "samplers/stage",
                stage.samplers,
                limits.max_samplers_per_shader_stage,
            ),
            (
                "uniform buffers/stage",
                stage.uniform_buffers,
                limits.max_uniform_buffers_per_shader_stage,
            ),
            (
                "storage buffers/stage",
                stage.storage_buffers,
                limits.max_storage_buffers_per_shader_stage,
            ),
        ] {
            limit(name, u64::from(needed), u64::from(available))?;
        }
    }
    Ok(())
}

fn check_format(
    device: &RenderDevice,
    adapter: &RenderAdapter,
    format: wgpu::TextureFormat,
    blending: bool,
    samples: u32,
) -> Result<(), SourcePipelineError> {
    let features = if device
        .features()
        .contains(wgpu::Features::TEXTURE_ADAPTER_SPECIFIC_FORMAT_FEATURES)
    {
        adapter.get_texture_format_features(format)
    } else {
        format.guaranteed_format_features(device.features())
    };
    let reason = if !features
        .allowed_usages
        .contains(wgpu::TextureUsages::RENDER_ATTACHMENT)
    {
        Some("render attachment unsupported")
    } else if blending
        && !features
            .flags
            .contains(wgpu::TextureFormatFeatureFlags::BLENDABLE)
    {
        Some("blending unsupported")
    } else if !features.flags.sample_count_supported(samples) {
        Some("sample count unsupported")
    } else {
        None
    };
    match reason {
        Some(reason) => Err(SourcePipelineError::Format { format, reason }),
        None => Ok(()),
    }
}

fn entries(bindings: &[SourceBinding]) -> Vec<wgpu::BindGroupLayoutEntry> {
    bindings
        .iter()
        .map(|binding| {
            let ty = if let Some(buffer) = &binding.layout.buffer {
                wgpu::BindingType::Buffer {
                    ty: match buffer.kind {
                        SourceBufferKind::Uniform => wgpu::BufferBindingType::Uniform,
                        SourceBufferKind::ReadOnlyStorage => {
                            wgpu::BufferBindingType::Storage { read_only: true }
                        }
                    },
                    has_dynamic_offset: false,
                    min_binding_size: binding
                        .uniform
                        .as_ref()
                        .and_then(|b| NonZeroU64::new(b.bytes as u64)),
                }
            } else if let Some(sampler) = &binding.layout.sampler {
                wgpu::BindingType::Sampler(match sampler.kind {
                    SourceSamplerKind::Filtering => wgpu::SamplerBindingType::Filtering,
                    SourceSamplerKind::NonFiltering => wgpu::SamplerBindingType::NonFiltering,
                    SourceSamplerKind::Comparison => wgpu::SamplerBindingType::Comparison,
                })
            } else {
                // Verified packages require exactly one buffer/sampler/texture.
                let texture = binding
                    .layout
                    .texture
                    .as_ref()
                    .expect("verified source binding texture");
                wgpu::BindingType::Texture {
                    sample_type: match texture.sample_type {
                        SourceTextureSample::Float => {
                            wgpu::TextureSampleType::Float { filterable: true }
                        }
                        SourceTextureSample::UnfilterableFloat => {
                            wgpu::TextureSampleType::Float { filterable: false }
                        }
                        SourceTextureSample::Depth => wgpu::TextureSampleType::Depth,
                    },
                    view_dimension: match texture.view_dimension {
                        SourceTextureDimension::D2 => wgpu::TextureViewDimension::D2,
                        SourceTextureDimension::D2Array => wgpu::TextureViewDimension::D2Array,
                        SourceTextureDimension::Cube => wgpu::TextureViewDimension::Cube,
                    },
                    multisampled: texture.multisampled,
                }
            };
            wgpu::BindGroupLayoutEntry {
                binding: binding.binding,
                visibility: wgpu::ShaderStages::from_bits_truncate(binding.layout.visibility),
                ty,
                count: None,
            }
        })
        .collect()
}

fn blend_component(source: SourceBlendComponent) -> wgpu::BlendComponent {
    let factor = |value| match value {
        SourceBlendFactor::Zero => wgpu::BlendFactor::Zero,
        SourceBlendFactor::One => wgpu::BlendFactor::One,
        SourceBlendFactor::SrcAlpha => wgpu::BlendFactor::SrcAlpha,
        SourceBlendFactor::OneMinusSrcAlpha => wgpu::BlendFactor::OneMinusSrcAlpha,
        SourceBlendFactor::Dst => wgpu::BlendFactor::Dst,
    };
    wgpu::BlendComponent {
        src_factor: factor(source.src_factor),
        dst_factor: factor(source.dst_factor),
        operation: match source.operation {
            SourceBlendOperation::Add => wgpu::BlendOperation::Add,
            SourceBlendOperation::Min => wgpu::BlendOperation::Min,
            SourceBlendOperation::Max => wgpu::BlendOperation::Max,
        },
    }
}
fn color_targets(policy: &SourcePipelineState) -> Vec<wgpu::ColorTargetState> {
    policy
        .targets
        .iter()
        .map(|target| wgpu::ColorTargetState {
            format: format(target.format),
            blend: target.blend.as_ref().map(|blend| wgpu::BlendState {
                color: blend_component(blend.color),
                alpha: blend_component(blend.alpha),
            }),
            write_mask: wgpu::ColorWrites::from_bits_truncate(target.write_mask),
        })
        .collect()
}

fn format(source: SourceTargetFormat) -> wgpu::TextureFormat {
    match source {
        SourceTargetFormat::Rgba16Float => wgpu::TextureFormat::Rgba16Float,
        SourceTargetFormat::Rgba8Unorm => wgpu::TextureFormat::Rgba8Unorm,
        SourceTargetFormat::Depth32Float => wgpu::TextureFormat::Depth32Float,
    }
}
fn vertex_format(source: SourceVertexFormat) -> wgpu::VertexFormat {
    match source {
        SourceVertexFormat::Float32 => wgpu::VertexFormat::Float32,
        SourceVertexFormat::Float32x2 => wgpu::VertexFormat::Float32x2,
        SourceVertexFormat::Float32x3 => wgpu::VertexFormat::Float32x3,
        SourceVertexFormat::Float32x4 => wgpu::VertexFormat::Float32x4,
    }
}

fn primitive(policy: &SourcePipelineState) -> wgpu::PrimitiveState {
    wgpu::PrimitiveState {
        topology: match policy.primitive.topology {
            SourceTopology::PointList => wgpu::PrimitiveTopology::PointList,
            SourceTopology::LineList => wgpu::PrimitiveTopology::LineList,
            SourceTopology::TriangleList => wgpu::PrimitiveTopology::TriangleList,
        },
        front_face: match policy.primitive.front_face {
            SourceFrontFace::Ccw => wgpu::FrontFace::Ccw,
            SourceFrontFace::Cw => wgpu::FrontFace::Cw,
        },
        cull_mode: match policy.primitive.cull_mode {
            SourceCull::None => None,
            SourceCull::Front => Some(wgpu::Face::Front),
            SourceCull::Back => Some(wgpu::Face::Back),
        },
        ..Default::default()
    }
}

#[allow(
    clippy::cast_possible_truncation,
    reason = "source loader bounds pipeline slopes to finite f32 values"
)]
fn depth_stencil(policy: &SourcePipelineState) -> wgpu::DepthStencilState {
    wgpu::DepthStencilState {
        format: format(policy.depth_stencil.format),
        depth_write_enabled: Some(policy.depth_stencil.depth_write_enabled),
        depth_compare: Some(match policy.depth_stencil.depth_compare {
            SourceDepthCompare::GreaterEqual => wgpu::CompareFunction::GreaterEqual,
            SourceDepthCompare::LessEqual => wgpu::CompareFunction::LessEqual,
            SourceDepthCompare::Always => wgpu::CompareFunction::Always,
        }),
        stencil: wgpu::StencilState::default(),
        bias: wgpu::DepthBiasState {
            constant: policy.depth_stencil.depth_bias,
            slope_scale: policy.depth_stencil.depth_bias_slope_scale as f32,
            clamp: 0.0,
        },
    }
}
