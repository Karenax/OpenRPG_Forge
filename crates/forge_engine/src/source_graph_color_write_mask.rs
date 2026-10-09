//! Candidate generic pass state. Must be registered in the actual material
//! specialization key before renderer admission; no asset-name routing.
use super::SourceGraphForwardState;
use bevy::{
    prelude::Reflect,
    render::render_resource::{ColorWrites, RenderPipelineDescriptor},
};

#[derive(Copy, Clone, Debug, PartialEq, Eq, Hash, Reflect)]
pub struct SourceGraphForwardPassState {
    pub forward: SourceGraphForwardState,
    /// Renderer bits (RED=1, GREEN=2, BLUE=4, ALPHA=8), not Unity's numeric
    /// ShaderLab mask. Producers must translate original named channels.
    pub color_write_mask: u32,
}

impl SourceGraphForwardPassState {
    pub fn validate(self) -> Result<(), &'static str> {
        ColorWrites::from_bits(self.color_write_mask)
            .ok_or("Unknown original color channel mask")?;
        self.forward.validate()
    }
    pub fn apply(self, descriptor: &mut RenderPipelineDescriptor) -> Result<(), &'static str> {
        let channels = ColorWrites::from_bits(self.color_write_mask)
            .ok_or("Unknown original color channel mask")?;
        // Existing forward-state admission validates enums and every required
        // attachment before mutation. A rejected mask cannot partially apply
        // blending, depth or culling either.
        self.forward.apply(descriptor)?;
        descriptor.fragment.as_mut().unwrap().targets[0]
            .as_mut()
            .unwrap()
            .write_mask = channels;
        Ok(())
    }
}

#[cfg(test)]
mod tests {
    use super::*;
    use bevy::render::render_resource::{
        ColorTargetState, CompareFunction, DepthStencilState, FragmentState, TextureFormat,
    };
    fn descriptor() -> RenderPipelineDescriptor {
        let mut result = RenderPipelineDescriptor::default();
        result.fragment = Some(FragmentState {
            targets: vec![
                Some(ColorTargetState {
                    format: TextureFormat::Rgba16Float,
                    blend: None,
                    write_mask: ColorWrites::ALL,
                }),
                Some(ColorTargetState {
                    format: TextureFormat::Rgba16Float,
                    blend: None,
                    write_mask: ColorWrites::ALPHA,
                }),
            ],
            ..Default::default()
        });
        result.depth_stencil = Some(DepthStencilState {
            format: TextureFormat::Depth32Float,
            depth_write_enabled: Some(true),
            depth_compare: Some(CompareFunction::GreaterEqual),
            stencil: Default::default(),
            bias: Default::default(),
        });
        result
    }
    fn source() -> SourceGraphForwardPassState {
        SourceGraphForwardPassState {
            forward: SourceGraphForwardState {
                color_src: 5,
                color_dst: 10,
                alpha_src: 5,
                alpha_dst: 10,
                cull: 0,
                compare: 4,
                write_depth: false,
            },
            color_write_mask: (ColorWrites::RED | ColorWrites::GREEN | ColorWrites::BLUE).bits(),
        }
    }
    #[test]
    fn original_rgb_mask_preserves_secondary_attachment_and_changes_specialization_key() {
        let rgb = source();
        assert_ne!(
            rgb,
            SourceGraphForwardPassState {
                color_write_mask: ColorWrites::ALL.bits(),
                ..rgb
            }
        );
        let mut result = descriptor();
        let secondary = result.fragment.as_ref().unwrap().targets[1].clone();
        rgb.apply(&mut result).unwrap();
        assert_eq!(
            result.fragment.as_ref().unwrap().targets[0]
                .as_ref()
                .unwrap()
                .write_mask
                .bits(),
            7
        );
        assert_eq!(result.fragment.as_ref().unwrap().targets[1], secondary);
    }
    #[test]
    fn invalid_channels_or_attachments_refuse_without_partial_state() {
        let mut result = descriptor();
        let before = result.clone();
        assert!(
            SourceGraphForwardPassState {
                color_write_mask: 16,
                ..source()
            }
            .apply(&mut result)
            .is_err()
        );
        assert_eq!(result, before);
        result.depth_stencil = None;
        let before = result.clone();
        assert!(source().apply(&mut result).is_err());
        assert_eq!(result, before);
    }
}
