//! Resident volumetric pipelines and immutable per-pass uniforms.
//! Scene bindings and view-frame preflight must be connected before activation.
// SPDX-License-Identifier: MIT
// Tidewater AirHaze.js 1438b1ab; copyright 2026 DRG Software Solutions LLC.
use bevy::{
    core_pipeline::FullscreenShader,
    prelude::*,
    render::{
        render_resource::{
            BindGroupLayoutDescriptor, BindGroupLayoutEntries, BufferSize, CachedRenderPipelineId,
            ColorTargetState, ColorWrites, FragmentState, PipelineCache, RenderPipelineDescriptor,
            SamplerBindingType, ShaderStages, ShaderType, TextureFormat, TextureSampleType,
            UniformBuffer,
            binding_types::{
                sampler, storage_buffer_read_only_sized, texture_2d, texture_2d_array,
                texture_depth_2d, uniform_buffer,
            },
        },
        renderer::{RenderDevice, RenderQueue},
    },
    shader::Shader,
};

/// Exact `HazeVolume64` WGSL ABI. Source cadence and screen fade are explicit
/// inputs; this does not derive them from an unrelated render serial.
#[derive(Clone, Copy, Debug, ShaderType)]
pub struct Volume {
    pub control: Vec4,
    pub terrain_domain: Vec4,
    pub cloud_rectangle: Vec4,
    pub screen: Vec4,
}

/// Traced cloud-history camera, matching the 80-byte WGSL compact view ABI.
/// Populate from the producing cloud transaction, including its history size.
#[derive(Clone, Copy, Debug, ShaderType)]
pub struct CloudView {
    pub right: Vec4,
    pub up: Vec4,
    pub forward: Vec4,
    pub tangent_size: Vec4,
    pub validity: Vec4,
}

/// Actual source moon direction and accepted source night value, 16-byte ABI.
#[derive(Clone, Copy, Debug, ShaderType)]
pub struct Moon {
    pub direction_night: Vec4,
}

impl Volume {
    /// Keep source frame/domain/sun fields unchanged across the three passes.
    #[must_use]
    pub fn radial_passes(self) -> [Self; 3] {
        [0.0, 1.0, 2.0].map(|index| Self {
            screen: self.screen.with_w(index),
            ..self
        })
    }
}

/// Four distinct buffers prevent queue writes for later radial passes from
/// replacing the index seen by earlier passes in the same GPU submission.
pub struct Uniforms {
    pub march: UniformBuffer<Volume>,
    pub radial: [UniformBuffer<Volume>; 3],
}

impl Uniforms {
    #[must_use]
    pub fn upload(volume: Volume, device: &RenderDevice, queue: &RenderQueue) -> Self {
        let upload = |value| {
            let mut buffer = UniformBuffer::from(value);
            buffer.write_buffer(device, queue);
            buffer
        };
        Self {
            march: upload(volume),
            radial: volume.radial_passes().map(upload),
        }
    }
}

#[derive(Resource)]
pub struct Pipelines {
    pub march: CachedRenderPipelineId,
    pub mask: CachedRenderPipelineId,
    pub composite: CachedRenderPipelineId,
    pub radial: CachedRenderPipelineId,
    pub march_layout: BindGroupLayoutDescriptor,
    pub mask_layout: BindGroupLayoutDescriptor,
    pub composite_layout: BindGroupLayoutDescriptor,
    pub radial_layout: BindGroupLayoutDescriptor,
}

impl Pipelines {
    /// This covers only these two pipelines, not mask/composite or scene inputs.
    #[must_use]
    pub fn march_and_radial_ready(&self, cache: &PipelineCache) -> bool {
        cache.get_render_pipeline(self.march).is_some()
            && cache.get_render_pipeline(self.radial).is_some()
    }

    /// Still excludes composite and the frame's resident scene resources.
    #[must_use]
    pub fn march_mask_and_radial_ready(&self, cache: &PipelineCache) -> bool {
        self.march_and_radial_ready(cache) && cache.get_render_pipeline(self.mask).is_some()
    }

    /// All four programs only; this is not frame/resource preflight readiness.
    #[must_use]
    pub fn programs_ready(&self, cache: &PipelineCache) -> bool {
        self.march_mask_and_radial_ready(cache)
            && cache.get_render_pipeline(self.composite).is_some()
    }
}

fn composite_layout() -> BindGroupLayoutDescriptor {
    BindGroupLayoutDescriptor::new(
        "tidewater haze composite layout",
        &BindGroupLayoutEntries::sequential(
            ShaderStages::FRAGMENT,
            (
                texture_2d(TextureSampleType::Float { filterable: false }),
                texture_depth_2d(),
                texture_2d(TextureSampleType::Float { filterable: true }),
                sampler(SamplerBindingType::Filtering),
                uniform_buffer::<super::TidewaterAirHaze>(true),
                uniform_buffer::<Volume>(false),
                storage_buffer_read_only_sized(false, BufferSize::new(48)),
                texture_2d(TextureSampleType::Float { filterable: false }),
                texture_2d(TextureSampleType::Float { filterable: true }),
                texture_2d(TextureSampleType::Float { filterable: false }),
                uniform_buffer::<Moon>(false),
            ),
        ),
    )
}

pub(super) fn init(
    mut commands: Commands,
    fullscreen: Res<FullscreenShader>,
    cache: Res<PipelineCache>,
) {
    let march_layout = BindGroupLayoutDescriptor::new(
        "tidewater haze march layout",
        &BindGroupLayoutEntries::sequential(
            ShaderStages::FRAGMENT,
            (
                texture_depth_2d(),
                uniform_buffer::<super::TidewaterAirHaze>(true),
                uniform_buffer::<Volume>(false),
                texture_2d_array(TextureSampleType::Depth),
                texture_2d(TextureSampleType::Float { filterable: false }),
                texture_2d(TextureSampleType::Float { filterable: false }),
                storage_buffer_read_only_sized(false, BufferSize::new(464)),
                storage_buffer_read_only_sized(false, BufferSize::new(48)),
            ),
        ),
    );
    let mask_layout = BindGroupLayoutDescriptor::new(
        "tidewater haze mask layout",
        &BindGroupLayoutEntries::sequential(
            ShaderStages::FRAGMENT,
            (
                texture_depth_2d(),
                uniform_buffer::<super::TidewaterAirHaze>(true),
                uniform_buffer::<Volume>(false),
                storage_buffer_read_only_sized(false, BufferSize::new(48)),
                uniform_buffer::<CloudView>(false),
                texture_2d(TextureSampleType::Float { filterable: true }),
                texture_2d(TextureSampleType::Float { filterable: true }),
                sampler(SamplerBindingType::Filtering),
                sampler(SamplerBindingType::Filtering),
            ),
        ),
    );
    let radial_layout = BindGroupLayoutDescriptor::new(
        "tidewater haze radial layout",
        &BindGroupLayoutEntries::sequential(
            ShaderStages::FRAGMENT,
            (
                texture_2d(TextureSampleType::Float { filterable: true }),
                sampler(SamplerBindingType::Filtering),
                uniform_buffer::<Volume>(false),
                uniform_buffer::<super::TidewaterAirHaze>(true),
                storage_buffer_read_only_sized(false, BufferSize::new(48)),
            ),
        ),
    );
    let composite_layout = composite_layout();
    let descriptor = |label: &'static str,
                      layout: &BindGroupLayoutDescriptor,
                      shader: &Handle<Shader>,
                      format| {
        RenderPipelineDescriptor {
            label: Some(label.into()),
            layout: vec![layout.clone()],
            vertex: fullscreen.to_vertex_state(),
            fragment: Some(FragmentState {
                shader: shader.clone(),
                entry_point: Some("fragment".into()),
                targets: vec![Some(ColorTargetState {
                    format,
                    blend: None,
                    write_mask: ColorWrites::ALL,
                })],
                ..default()
            }),
            ..default()
        }
    };
    commands.insert_resource(Pipelines {
        march: cache.queue_render_pipeline(descriptor(
            "tidewater haze march pipeline",
            &march_layout,
            &super::MARCH_SHADER_HANDLE,
            TextureFormat::Rgba16Float,
        )),
        radial: cache.queue_render_pipeline(descriptor(
            "tidewater haze radial pipeline",
            &radial_layout,
            &super::RADIAL_SHADER_HANDLE,
            TextureFormat::R16Float,
        )),
        mask: cache.queue_render_pipeline(descriptor(
            "tidewater haze mask pipeline",
            &mask_layout,
            &super::MASK_SHADER_HANDLE,
            TextureFormat::R16Float,
        )),
        composite: cache.queue_render_pipeline(descriptor(
            "tidewater haze composite pipeline",
            &composite_layout,
            &super::COMPOSITE_SHADER_HANDLE,
            TextureFormat::Rgba16Float,
        )),
        march_layout,
        mask_layout,
        composite_layout,
        radial_layout,
    });
}

#[cfg(test)]
mod tests {
    use super::*;
    #[test]
    fn uniform_span_and_radial_variants_preserve_the_source_frame() {
        assert_eq!(Volume::min_size().get(), 64);
        assert_eq!(CloudView::min_size().get(), 80);
        assert_eq!(Moon::min_size().get(), 16);
        let volume = Volume {
            control: Vec4::new(1.6, 1.0, 1.0, 124.0),
            terrain_domain: Vec4::new(-1024.0, -1024.0, 2048.0, 1.0),
            cloud_rectangle: Vec4::new(10.0, 20.0, 3000.0, 0.7),
            screen: Vec4::new(0.25, 0.75, 0.6, 99.0),
        };
        for (pass, expected_index) in volume.radial_passes().into_iter().zip([0.0_f32, 1.0, 2.0]) {
            assert_eq!(pass.control, volume.control);
            assert_eq!(pass.terrain_domain, volume.terrain_domain);
            assert_eq!(pass.cloud_rectangle, volume.cloud_rectangle);
            assert_eq!(pass.screen.truncate(), volume.screen.truncate());
            assert_eq!(pass.screen.w.to_bits(), expected_index.to_bits());
        }
    }
}
