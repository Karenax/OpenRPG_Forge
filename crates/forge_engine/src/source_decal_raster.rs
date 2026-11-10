//! GPU DBuffer generation for the admitted flat source mesh-decal profile.
//! Does not modify the view depth or the final color target. Opaque receivers
//! must consume the returned attachments before lighting.
use crate::source_decal_prepare::{PreparedSourceDecal, SourceDecalDraw, SourceDecalTextureFormat};
use bevy::{
    math::{Mat4, Vec3},
    render::{
        renderer::{RenderDevice, RenderQueue},
        texture::GpuImage,
        view::ViewDepthTexture,
    },
};
use wgpu::util::DeviceExt;

pub const TARGET_FORMAT: wgpu::TextureFormat = wgpu::TextureFormat::Rgba8Unorm;
pub const DEPTH_FORMAT: wgpu::TextureFormat = wgpu::TextureFormat::Depth32Float;
pub const NEUTRAL_COLOR: wgpu::Color = wgpu::Color {
    r: 0.,
    g: 0.,
    b: 0.,
    a: 1.,
};
pub const NEUTRAL_NORMAL: wgpu::Color = wgpu::Color {
    r: 128. / 255.,
    g: 128. / 255.,
    b: 128. / 255.,
    a: 1.,
};

const ATTRIBUTES: [wgpu::VertexAttribute; 2] =
    wgpu::vertex_attr_array![0 => Float32x4, 1 => Float32x2];

/// Scene-linear is the Forge path. Gamma replay is only for reproducing the
/// captured synthetic Unity controls; it is not a color-correction fallback.
#[derive(Clone, Copy)]
pub enum Sampling {
    SceneLinear,
    ReferenceGamma,
}

pub struct Raster {
    pipeline: wgpu::RenderPipeline,
    layout: wgpu::BindGroupLayout,
    image: wgpu::TextureView,
    sampler: wgpu::Sampler,
    indices: wgpu::Buffer,
    count: u32,
    geometry: SourceDecalDraw,
}

impl Raster {
    pub(crate) fn preview_frame(&self) -> Result<(Vec3, Vec3, f32), String> {
        let transform = self.geometry.root_from_mesh;
        let (min, max) = self.geometry.positions.iter().fold(
            (Vec3::splat(f32::INFINITY), Vec3::splat(f32::NEG_INFINITY)),
            |(min, max), point| {
                let point = transform.transform_point3(Vec3::from_array(*point));
                (min.min(point), max.max(point))
            },
        );
        let center = min.lerp(max, 0.5);
        let radius = (max - min).length() * 0.5;
        let normal = transform
            .inverse()
            .transpose()
            .transform_vector3(Vec3::from_array(self.geometry.normals[0]))
            .try_normalize()
            .ok_or("Normale de la toile invalide")?;
        if !center.is_finite() || !radius.is_finite() || !(0.01..=100.0).contains(&radius) {
            return Err("Dimensions de la toile incompatibles avec l’aperçu".into());
        }
        Ok((center, normal, radius))
    }

    /// Validate the checked shader and its real pipeline before admitting a draw.
    /// This profile requires device BC support; the caller must surface refusal.
    pub async fn prepare(
        device: &RenderDevice,
        queue: &RenderQueue,
        material: &PreparedSourceDecal,
        draw: SourceDecalDraw,
        sampling: Sampling,
        front_face: wgpu::FrontFace,
    ) -> Result<Self, String> {
        let device = device.wgpu_device();
        let texture = &material.texture;
        if draw.material_index != material.material_index
            || texture.format != SourceDecalTextureFormat::Bc3RgbaUnormSrgb
            || !device
                .features()
                .contains(wgpu::Features::TEXTURE_COMPRESSION_BC)
            || ![texture.width, texture.height].into_iter().all(|size| {
                size != 0
                    && size.is_multiple_of(4)
                    && size <= device.limits().max_texture_dimension_2d
            })
            || texture.mips.is_empty()
            || texture.mips.len() as u32 > texture.width.max(texture.height).ilog2() + 1
        {
            return Err("Unsupported source decal GPU profile".into());
        }
        let mut uploads = Vec::with_capacity(texture.mips.len());
        for (level, mip) in texture.mips.iter().enumerate() {
            let cols = mip.width.div_ceil(4);
            let rows = mip.height.div_ceil(4);
            if mip.width != (texture.width >> level).max(1)
                || mip.height != (texture.height >> level).max(1)
                || (u64::from(cols) * u64::from(rows) * 16) != mip.bytes as u64
            {
                return Err("Invalid BC3 mip extent".into());
            }
            let bytes = mip
                .offset
                .checked_add(mip.bytes)
                .and_then(|end| texture.storage.get(mip.offset..end))
                .ok_or("BC3 mip lies outside verified storage")?;
            uploads.push((cols, rows, bytes));
        }
        let index_bytes: Vec<_> = draw.indices.iter().flat_map(|n| n.to_le_bytes()).collect();
        let count = u32::try_from(draw.indices.len()).map_err(|_| "Decal index count overflow")?;
        if count == 0
            || !count.is_multiple_of(3)
            || draw
                .indices
                .iter()
                .any(|&index| index as usize >= draw.positions.len())
        {
            return Err("Invalid source decal GPU indices".into());
        }
        let scope = device.push_error_scope(wgpu::ErrorFilter::Validation);
        let storage = device.create_texture(&wgpu::TextureDescriptor {
            label: Some("source decal captured BC3"),
            size: extent(texture.width, texture.height),
            mip_level_count: uploads.len() as u32,
            sample_count: 1,
            dimension: wgpu::TextureDimension::D2,
            format: match sampling {
                Sampling::SceneLinear => wgpu::TextureFormat::Bc3RgbaUnormSrgb,
                Sampling::ReferenceGamma => wgpu::TextureFormat::Bc3RgbaUnorm,
            },
            usage: wgpu::TextureUsages::COPY_DST | wgpu::TextureUsages::TEXTURE_BINDING,
            view_formats: &[],
        });
        for (level, (cols, rows, bytes)) in uploads.into_iter().enumerate() {
            queue.write_texture(
                wgpu::TexelCopyTextureInfo {
                    texture: &storage,
                    mip_level: level as u32,
                    origin: wgpu::Origin3d::ZERO,
                    aspect: wgpu::TextureAspect::All,
                },
                bytes,
                wgpu::TexelCopyBufferLayout {
                    offset: 0,
                    bytes_per_row: Some(cols * 16),
                    rows_per_image: Some(rows),
                },
                extent(cols * 4, rows * 4),
            );
        }
        let gamma = matches!(sampling, Sampling::ReferenceGamma);
        let sampler = device.create_sampler(&wgpu::SamplerDescriptor {
            address_mode_u: wgpu::AddressMode::Repeat,
            address_mode_v: wgpu::AddressMode::Repeat,
            mag_filter: wgpu::FilterMode::Linear,
            min_filter: wgpu::FilterMode::Linear,
            mipmap_filter: if gamma {
                wgpu::MipmapFilterMode::Linear
            } else {
                wgpu::MipmapFilterMode::Nearest
            },
            anisotropy_clamp: if gamma { 9 } else { 1 },
            ..Default::default()
        });
        let binding = |binding, ty| wgpu::BindGroupLayoutEntry {
            binding,
            ty,
            visibility: wgpu::ShaderStages::FRAGMENT,
            count: None,
        };
        let layout = device.create_bind_group_layout(&wgpu::BindGroupLayoutDescriptor {
            label: Some("source decal image and world normal"),
            entries: &[
                binding(
                    0,
                    wgpu::BindingType::Texture {
                        sample_type: wgpu::TextureSampleType::Float { filterable: true },
                        view_dimension: wgpu::TextureViewDimension::D2,
                        multisampled: false,
                    },
                ),
                binding(
                    1,
                    wgpu::BindingType::Sampler(wgpu::SamplerBindingType::Filtering),
                ),
                binding(
                    2,
                    wgpu::BindingType::Buffer {
                        ty: wgpu::BufferBindingType::Uniform,
                        has_dynamic_offset: false,
                        min_binding_size: std::num::NonZeroU64::new(16),
                    },
                ),
            ],
        });
        let pipeline_layout = device.create_pipeline_layout(&wgpu::PipelineLayoutDescriptor {
            label: Some("source decal raster"),
            bind_group_layouts: &[Some(&layout)],
            immediate_size: 0,
        });
        let shader = device.create_shader_module(wgpu::ShaderModuleDescriptor {
            label: Some("source decal validated generation WGSL"),
            source: wgpu::ShaderSource::Wgsl(material.programs.dbuffer_generation.as_str().into()),
        });
        let target = Some(wgpu::ColorTargetState {
            format: TARGET_FORMAT,
            blend: Some(wgpu::BlendState {
                color: wgpu::BlendComponent {
                    src_factor: wgpu::BlendFactor::SrcAlpha,
                    dst_factor: wgpu::BlendFactor::OneMinusSrcAlpha,
                    operation: wgpu::BlendOperation::Add,
                },
                alpha: wgpu::BlendComponent {
                    src_factor: wgpu::BlendFactor::Zero,
                    dst_factor: wgpu::BlendFactor::OneMinusSrcAlpha,
                    operation: wgpu::BlendOperation::Add,
                },
            }),
            write_mask: wgpu::ColorWrites::ALL,
        });
        let pipeline = device.create_render_pipeline(&wgpu::RenderPipelineDescriptor {
            label: Some("source decal DBuffer color + normal"),
            layout: Some(&pipeline_layout),
            vertex: wgpu::VertexState {
                module: &shader,
                entry_point: Some("vert"),
                compilation_options: Default::default(),
                buffers: &[wgpu::VertexBufferLayout {
                    array_stride: 24,
                    step_mode: wgpu::VertexStepMode::Vertex,
                    attributes: &ATTRIBUTES,
                }],
            },
            primitive: wgpu::PrimitiveState {
                front_face,
                cull_mode: Some(wgpu::Face::Back),
                ..Default::default()
            },
            depth_stencil: Some(wgpu::DepthStencilState {
                format: DEPTH_FORMAT,
                depth_write_enabled: Some(false),
                depth_compare: Some(wgpu::CompareFunction::GreaterEqual),
                stencil: Default::default(),
                bias: Default::default(),
            }),
            multisample: Default::default(),
            fragment: Some(wgpu::FragmentState {
                module: &shader,
                entry_point: Some("frag"),
                compilation_options: Default::default(),
                targets: &[target.clone(), target],
            }),
            multiview_mask: None,
            cache: None,
        });
        let indices = init_buffer(
            device,
            "source decal indices",
            wgpu::BufferUsages::INDEX,
            &index_bytes,
        );
        if let Some(error) = scope.pop().await {
            return Err(format!("Source decal GPU validation: {error}"));
        }
        Ok(Self {
            pipeline,
            layout,
            image: storage.create_view(&Default::default()),
            sampler,
            indices,
            count,
            geometry: draw,
        })
    }

    /// New draw buffers are local to this frame/view. This avoids racing
    /// overwrites when multiple cameras share a carrier.
    pub fn project<'a>(
        &'a self,
        device: &RenderDevice,
        world_from_root: Mat4,
        clip_from_world: Mat4,
    ) -> Result<ViewDraw<'a>, String> {
        let (positions, normal) = project(&self.geometry, world_from_root, clip_from_world)?;
        let device = device.wgpu_device();
        let vertices = init_buffer(
            device,
            "view-local decal positions and UV",
            wgpu::BufferUsages::VERTEX,
            &positions,
        );
        let normal = init_buffer(
            device,
            "view-local decal world normal",
            wgpu::BufferUsages::UNIFORM,
            &normal,
        );
        let binding = device.create_bind_group(&wgpu::BindGroupDescriptor {
            label: Some("view-local source decal material"),
            layout: &self.layout,
            entries: &[
                wgpu::BindGroupEntry {
                    binding: 0,
                    resource: wgpu::BindingResource::TextureView(&self.image),
                },
                wgpu::BindGroupEntry {
                    binding: 1,
                    resource: wgpu::BindingResource::Sampler(&self.sampler),
                },
                wgpu::BindGroupEntry {
                    binding: 2,
                    resource: normal.as_entire_binding(),
                },
            ],
        });
        Ok(ViewDraw {
            raster: self,
            vertices,
            binding,
        })
    }
}

fn init_buffer(
    device: &wgpu::Device,
    label: &str,
    usage: wgpu::BufferUsages,
    contents: &[u8],
) -> wgpu::Buffer {
    device.create_buffer_init(&wgpu::util::BufferInitDescriptor {
        label: Some(label),
        contents,
        usage,
    })
}

fn project(
    draw: &SourceDecalDraw,
    world_from_root: Mat4,
    clip_from_world: Mat4,
) -> Result<(Vec<u8>, [u8; 16]), String> {
    let world_from_mesh = world_from_root * draw.root_from_mesh;
    let determinant = world_from_mesh.determinant();
    if !world_from_mesh.is_finite()
        || !clip_from_world.is_finite()
        || !determinant.is_finite()
        || determinant <= 0.
        || world_from_mesh.row(3).to_array() != [0., 0., 0., 1.]
        || draw.positions.is_empty()
        || draw.positions.len() != draw.uv.len()
        || draw.positions.len() != draw.normals.len()
    {
        return Err("Invalid or mirrored source decal placement".into());
    }
    let normal_matrix = world_from_mesh.inverse().transpose();
    let transform_normal = |normal| {
        normal_matrix
            .transform_vector3(Vec3::from_array(normal))
            .try_normalize()
            .filter(|v| v.is_finite())
    };
    let normal = transform_normal(draw.normals[0]).ok_or("Invalid source decal normal")?;
    if draw
        .normals
        .iter()
        .any(|&value| transform_normal(value).is_none_or(|n| n.distance_squared(normal) > 1e-6))
    {
        return Err("Nonflat source decal normal is outside this GPU profile".into());
    }
    let mut normal_bytes = [0; 16];
    for (chunk, value) in normal_bytes
        .chunks_exact_mut(4)
        .zip(normal.extend(0.).to_array())
    {
        chunk.copy_from_slice(&value.to_le_bytes());
    }
    let matrix = clip_from_world * world_from_mesh;
    let mut bytes = Vec::with_capacity(draw.positions.len() * 24);
    for (position, uv) in draw.positions.iter().zip(&draw.uv) {
        let clip = matrix * Vec3::from_array(*position).extend(1.);
        if !clip.is_finite() || uv.iter().any(|value| !value.is_finite()) {
            return Err("Invalid source decal projected vertex".into());
        }
        for value in clip.to_array().into_iter().chain(*uv) {
            bytes.extend(value.to_le_bytes());
        }
    }
    Ok((bytes, normal_bytes))
}

pub struct ViewDraw<'a> {
    raster: &'a Raster,
    vertices: wgpu::Buffer,
    binding: wgpu::BindGroup,
}

fn extent(width: u32, height: u32) -> wgpu::Extent3d {
    wgpu::Extent3d {
        width,
        height,
        depth_or_array_layers: 1,
    }
}

/// Allocate once per view and retain until its physical target size changes.
pub struct Targets {
    size: wgpu::Extent3d,
    pub color: wgpu::Texture,
    pub normal: wgpu::Texture,
    color_view: wgpu::TextureView,
    normal_view: wgpu::TextureView,
}

impl Targets {
    /// Reuse images that a material can bind through Bevy. Both outputs must
    /// belong to this view, have identical extents, and never alias each other.
    pub fn from_images(color: &GpuImage, normal: &GpuImage) -> Result<Self, String> {
        let valid = |image: &GpuImage| {
            image.texture.format() == TARGET_FORMAT
                && image.texture.sample_count() == 1
                && image.texture.dimension() == wgpu::TextureDimension::D2
                && image.texture.size().depth_or_array_layers == 1
                && image.texture.mip_level_count() == 1
                && image.texture_view_descriptor.is_none()
                && image.texture.usage().contains(
                    wgpu::TextureUsages::TEXTURE_BINDING | wgpu::TextureUsages::RENDER_ATTACHMENT,
                )
        };
        if !valid(color)
            || !valid(normal)
            || color.texture.size() != normal.texture.size()
            || color.texture.id() == normal.texture.id()
        {
            return Err("Incompatible source decal view images".into());
        }
        Ok(Self {
            size: color.texture.size(),
            color: (*color.texture).clone(),
            normal: (*normal.texture).clone(),
            color_view: (*color.texture_view).clone(),
            normal_view: (*normal.texture_view).clone(),
        })
    }

    pub fn new(device: &RenderDevice, width: u32, height: u32) -> Result<Self, String> {
        let limit = device.limits().max_texture_dimension_2d;
        if width == 0 || height == 0 || width > limit || height > limit {
            return Err("Invalid DBuffer view size".into());
        }
        let size = extent(width, height);
        let make = |label| {
            device
                .wgpu_device()
                .create_texture(&wgpu::TextureDescriptor {
                    label: Some(label),
                    size,
                    mip_level_count: 1,
                    sample_count: 1,
                    dimension: wgpu::TextureDimension::D2,
                    format: TARGET_FORMAT,
                    usage: wgpu::TextureUsages::RENDER_ATTACHMENT
                        | wgpu::TextureUsages::TEXTURE_BINDING
                        | wgpu::TextureUsages::COPY_SRC,
                    view_formats: &[],
                })
        };
        let color = make("source decal color target");
        let normal = make("source decal normal target");
        let color_view = color.create_view(&Default::default());
        let normal_view = normal.create_view(&Default::default());
        Ok(Self {
            size,
            color,
            normal,
            color_view,
            normal_view,
        })
    }

    /// The caller must order this after the opaque depth prepass and before
    /// receiver shading. Only full-size non-MSAA views are admitted.
    pub fn clear(&self, encoder: &mut wgpu::CommandEncoder) {
        let _pass = encoder.begin_render_pass(&wgpu::RenderPassDescriptor {
            label: Some("clear source decal targets on refused view"),
            color_attachments: &self.attachments(),
            ..Default::default()
        });
    }

    fn attachments(&self) -> [Option<wgpu::RenderPassColorAttachment<'_>>; 2] {
        [
            (&self.color_view, NEUTRAL_COLOR),
            (&self.normal_view, NEUTRAL_NORMAL),
        ]
        .map(|(view, clear)| {
            Some(wgpu::RenderPassColorAttachment {
                view,
                resolve_target: None,
                depth_slice: None,
                ops: wgpu::Operations {
                    load: wgpu::LoadOp::Clear(clear),
                    store: wgpu::StoreOp::Store,
                },
            })
        })
    }

    pub fn encode(
        &self,
        encoder: &mut wgpu::CommandEncoder,
        depth: &ViewDepthTexture,
        draws: &[ViewDraw<'_>],
    ) -> Result<(), String> {
        if depth.texture.format() != DEPTH_FORMAT
            || depth.texture.sample_count() != 1
            || depth.texture.size() != self.size
        {
            return Err("Source decal depth does not match DBuffer targets".into());
        }
        let mut pass = encoder.begin_render_pass(&wgpu::RenderPassDescriptor {
            label: Some("generate source mesh DBuffer using opaque view depth"),
            color_attachments: &self.attachments(),
            depth_stencil_attachment: Some(wgpu::RenderPassDepthStencilAttachment {
                view: depth.view(),
                // Follow the Bevy prepass attachment convention. The explicit
                // load/store preserves the existing depth with writes disabled.
                depth_ops: Some(wgpu::Operations {
                    load: wgpu::LoadOp::Load,
                    store: wgpu::StoreOp::Store,
                }),
                stencil_ops: None,
            }),
            ..Default::default()
        });
        for draw in draws {
            pass.set_pipeline(&draw.raster.pipeline);
            pass.set_vertex_buffer(0, draw.vertices.slice(..));
            pass.set_index_buffer(draw.raster.indices.slice(..), wgpu::IndexFormat::Uint32);
            pass.set_bind_group(0, &draw.binding, &[]);
            pass.draw_indexed(0..draw.raster.count, 0, 0..1);
        }
        Ok(())
    }
}
