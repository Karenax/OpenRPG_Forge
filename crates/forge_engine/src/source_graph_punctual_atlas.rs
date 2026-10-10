//! Explicit 2D D16 storage for resolved source punctual shadows.
//! This is allocation/pass infrastructure, not automatic light routing or
//! qualification of a source shader, projection, culling policy or receiver.
use wgpu::{
    CommandEncoder, DepthBiasState, DepthStencilState, Device, Extent3d, LoadOp, Operations,
    RenderPass, RenderPassDepthStencilAttachment, RenderPassDescriptor, StoreOp, Texture,
    TextureDescriptor, TextureDimension, TextureFormat, TextureUsages, TextureView,
    TextureViewDescriptor,
};

/// Float viewport values are retained; casting them to integer face sizes changes
/// projection coverage. Bounds use the same f32 addition as the GPU API.
#[derive(Clone, Copy, Debug, PartialEq)]
pub struct SourceGraphPunctualViewport {
    x: f32,
    y: f32,
    width: f32,
    height: f32,
}

impl SourceGraphPunctualViewport {
    pub fn resolved(x: f32, y: f32, width: f32, height: f32) -> Result<Self, &'static str> {
        if ![x, y, width, height].into_iter().all(f32::is_finite)
            || x < 0.0
            || y < 0.0
            || width <= 0.0
            || height <= 0.0
        {
            return Err("Punctual viewport requires finite nonnegative origin and positive size");
        }
        Ok(Self {
            x,
            y,
            width,
            height,
        })
    }

    #[must_use]
    pub fn values(self) -> [f32; 4] {
        [self.x, self.y, self.width, self.height]
    }

    #[expect(
        clippy::cast_precision_loss,
        reason = "The layout constructor restricts dimensions to 2^24 so every integer dimension is exactly representable in Float32"
    )]
    pub fn validate(self, atlas: SourceGraphPunctualAtlasLayout) -> Result<(), &'static str> {
        Self::resolved(self.x, self.y, self.width, self.height)?;
        if self.x + self.width > atlas.width as f32 || self.y + self.height > atlas.height as f32 {
            return Err("Punctual viewport extends outside its declared atlas");
        }
        Ok(())
    }
}

#[derive(Clone, Copy, Debug, PartialEq, Eq)]
pub struct SourceGraphPunctualAtlasLayout {
    width: u32,
    height: u32,
}

impl SourceGraphPunctualAtlasLayout {
    pub fn resolved(width: u32, height: u32) -> Result<Self, &'static str> {
        // Above this limit f32 cannot represent every integer atlas coordinate.
        if width == 0 || height == 0 || width > (1 << 24) || height > (1 << 24) {
            return Err("Punctual atlas requires positive exactly representable dimensions");
        }
        Ok(Self { width, height })
    }

    #[must_use]
    pub fn descriptor(self) -> TextureDescriptor<'static> {
        TextureDescriptor {
            label: Some("source-punctual-d16-atlas"),
            size: Extent3d {
                width: self.width,
                height: self.height,
                depth_or_array_layers: 1,
            },
            mip_level_count: 1,
            sample_count: 1,
            dimension: TextureDimension::D2,
            format: TextureFormat::Depth16Unorm,
            // Readback is performed through a depth-sampling shader. Some
            // backends cannot copy a D16 depth aspect directly into a buffer.
            usage: TextureUsages::RENDER_ATTACHMENT | TextureUsages::TEXTURE_BINDING,
            view_formats: &[],
        }
    }

    /// Resolved backend units/sign, explicitly separate from receiver normal bias.
    pub fn reversed_depth_state(
        constant: i32,
        slope_scale: f32,
        clamp: f32,
    ) -> Result<DepthStencilState, &'static str> {
        if !slope_scale.is_finite() || !clamp.is_finite() || clamp < 0.0 {
            return Err("Punctual raster bias must be finite with nonnegative clamp");
        }
        Ok(DepthStencilState {
            format: TextureFormat::Depth16Unorm,
            depth_write_enabled: Some(true),
            depth_compare: Some(wgpu::CompareFunction::GreaterEqual),
            stencil: wgpu::StencilState::default(),
            bias: DepthBiasState {
                constant,
                slope_scale,
                clamp,
            },
        })
    }

    pub fn allocate(self, device: &Device) -> Result<SourceGraphPunctualAtlas, &'static str> {
        if self.width > device.limits().max_texture_dimension_2d
            || self.height > device.limits().max_texture_dimension_2d
        {
            return Err("Punctual atlas exceeds the actual device texture limit");
        }
        let texture = device.create_texture(&self.descriptor());
        let view = texture.create_view(&TextureViewDescriptor::default());
        Ok(SourceGraphPunctualAtlas {
            layout: self,
            texture,
            view,
        })
    }
}

pub struct SourceGraphPunctualAtlas {
    layout: SourceGraphPunctualAtlasLayout,
    texture: Texture,
    view: TextureView,
}

impl SourceGraphPunctualAtlas {
    #[must_use]
    pub fn texture(&self) -> &Texture {
        &self.texture
    }
    #[must_use]
    pub fn view(&self) -> &TextureView {
        &self.view
    }

    /// All viewports are admitted before encoding; a rejected later request
    /// cannot leave a partially encoded atlas. A fresh clear also prevents stale
    /// active shadows from becoming the next frame's inactive light state.
    /// Caller supplies the resolved matrices, source vertex/alpha program and
    /// explicit raster orientation/clipping; none are inferred here.
    pub fn render(
        &self,
        encoder: &mut CommandEncoder,
        viewports: &[SourceGraphPunctualViewport],
        draw: impl FnMut(usize, &mut RenderPass<'_>),
    ) -> Result<(), &'static str> {
        self.render_inner(encoder, viewports, false, draw)
    }

    /// Use a full-atlas hardware viewport and preserve fractional source mapping
    /// in the vertex/fragment program. The caller MUST use the paired WGSL
    /// mapping and coverage helpers below; this method does not rewrite shaders.
    /// This avoids backend-dependent hardware viewport edge quantization while
    /// retaining the original rect and perspective depth, not integer face sizes.
    pub fn render_shader_mapped(
        &self,
        encoder: &mut CommandEncoder,
        viewports: &[SourceGraphPunctualViewport],
        draw: impl FnMut(usize, &mut RenderPass<'_>),
    ) -> Result<(), &'static str> {
        self.render_inner(encoder, viewports, true, draw)
    }

    #[expect(
        clippy::cast_precision_loss,
        reason = "The layout constructor restricts dimensions to 2^24 so viewport dimensions are exactly representable in Float32"
    )]
    fn render_inner(
        &self,
        encoder: &mut CommandEncoder,
        viewports: &[SourceGraphPunctualViewport],
        shader_mapped: bool,
        mut draw: impl FnMut(usize, &mut RenderPass<'_>),
    ) -> Result<(), &'static str> {
        for viewport in viewports {
            viewport.validate(self.layout)?;
        }
        let mut pass = encoder.begin_render_pass(&RenderPassDescriptor {
            label: Some("source-punctual-d16-render"),
            color_attachments: &[],
            depth_stencil_attachment: Some(RenderPassDepthStencilAttachment {
                view: &self.view,
                depth_ops: Some(Operations {
                    load: LoadOp::Clear(0.0),
                    store: StoreOp::Store,
                }),
                stencil_ops: None,
            }),
            timestamp_writes: None,
            occlusion_query_set: None,
            multiview_mask: None,
        });
        for (index, viewport) in viewports.iter().enumerate() {
            let [x, y, width, height] = viewport.values();
            if shader_mapped {
                pass.set_viewport(
                    0.0,
                    0.0,
                    self.layout.width as f32,
                    self.layout.height as f32,
                    0.0,
                    1.0,
                );
            } else {
                pass.set_viewport(x, y, width, height, 0.0, 1.0);
            }
            // Avoid inheriting a narrower scissor set by the previous caller.
            pass.set_scissor_rect(0, 0, self.layout.width, self.layout.height);
            draw(index, &mut pass);
        }
        Ok(())
    }
}

/// Rect is (x, y, width, height) in source atlas pixels; extent is the full atlas.
/// Apply after resolved source clip projection, before emitting vertex position.
/// The paired fragment predicate is required to clip XY to the original face;
/// the hardware continues to clip perspective Z/W. Source alpha/displacement
/// remains the responsibility of the original shader program.
pub const SOURCE_GRAPH_PUNCTUAL_ATLAS_MAPPING_WGSL: &str = r"
fn source_punctual_atlas_position(clip: vec4<f32>, rect: vec4<f32>, extent: vec2<f32>) -> vec4<f32> {
    let scale = rect.zw / extent;
    let center = (2.0 * rect.xy + rect.zw) / extent;
    return vec4<f32>(clip.x * scale.x + clip.w * (center.x - 1.0),
                     clip.y * scale.y + clip.w * (1.0 - center.y), clip.zw);
}
fn source_punctual_atlas_contains(pixel: vec2<f32>, rect: vec4<f32>) -> bool {
    return all(pixel >= rect.xy) && all(pixel < rect.xy + rect.zw);
}
";

#[cfg(test)]
mod tests {
    use super::*;
    #[test]
    fn atlas_is_single_sample_single_layer_d16_with_sampling() {
        let d = SourceGraphPunctualAtlasLayout::resolved(4096, 4096)
            .unwrap()
            .descriptor();
        assert_eq!(d.format, TextureFormat::Depth16Unorm);
        assert_eq!(d.size.depth_or_array_layers, 1);
        assert_eq!(d.dimension, TextureDimension::D2);
        assert_eq!((d.sample_count, d.mip_level_count), (1, 1));
        assert_eq!(
            d.usage,
            TextureUsages::RENDER_ATTACHMENT | TextureUsages::TEXTURE_BINDING
        );
    }
    #[test]
    #[expect(
        clippy::float_cmp,
        reason = "Copied GPU wire values must remain exact; tolerance would weaken this preservation test"
    )]
    fn fractional_source_viewports_keep_their_exact_float_bits() {
        let a = SourceGraphPunctualAtlasLayout::resolved(4096, 4096).unwrap();
        let edge = f32::from_bits(0x44aa_aaab);
        for (x, y) in [
            (0.0, 0.0),
            (0.0, edge),
            (edge, 0.0),
            (edge, edge),
            (0.0, 2.0 * edge),
            (edge, 2.0 * edge),
        ] {
            let v = SourceGraphPunctualViewport::resolved(x, y, edge, edge).unwrap();
            assert_eq!(
                v.values().map(f32::to_bits),
                [x, y, edge, edge].map(f32::to_bits)
            );
            assert!(v.validate(a).is_ok());
        }
        assert_ne!(edge, edge.floor());
    }
    #[test]
    fn invalid_rectangles_and_outside_atlas_are_refused() {
        for v in [
            [f32::NAN, 0.0, 1.0, 1.0],
            [0.0, f32::INFINITY, 1.0, 1.0],
            [-1.0, 0.0, 1.0, 1.0],
            [0.0, 0.0, 0.0, 1.0],
            [0.0, 0.0, 1.0, -1.0],
        ] {
            assert!(SourceGraphPunctualViewport::resolved(v[0], v[1], v[2], v[3]).is_err());
        }
        let a = SourceGraphPunctualAtlasLayout::resolved(8, 8).unwrap();
        assert!(
            SourceGraphPunctualViewport::resolved(7.0, 0.0, 2.0, 1.0)
                .unwrap()
                .validate(a)
                .is_err()
        );
        assert!(
            SourceGraphPunctualViewport::resolved(0.0, 7.0, 1.0, 2.0)
                .unwrap()
                .validate(a)
                .is_err()
        );
    }
    #[test]
    fn invalid_dimensions_and_biases_are_refused() {
        for (w, h) in [(0, 1), (1, 0), (1 << 25, 1), (1, 1 << 25)] {
            assert!(SourceGraphPunctualAtlasLayout::resolved(w, h).is_err());
        }
        for (s, c) in [
            (f32::NAN, 0.0),
            (f32::INFINITY, 0.0),
            (1.0, -1.0),
            (1.0, f32::NAN),
        ] {
            assert!(SourceGraphPunctualAtlasLayout::reversed_depth_state(0, s, c).is_err());
        }
    }
    #[test]
    #[expect(
        clippy::float_cmp,
        reason = "Copied GPU wire values must remain exact; tolerance would weaken this preservation test"
    )]
    fn reversed_raster_is_explicit_and_retains_resolved_sign() {
        let d = SourceGraphPunctualAtlasLayout::reversed_depth_state(-1, -2.5, 0.0).unwrap();
        assert_eq!(d.format, TextureFormat::Depth16Unorm);
        assert_eq!(d.depth_compare, Some(wgpu::CompareFunction::GreaterEqual));
        assert_eq!(d.depth_write_enabled, Some(true));
        assert_eq!(d.bias.constant, -1);
        assert_eq!(d.bias.slope_scale, -2.5);
    }
}
