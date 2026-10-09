//! Native forward render-state translation. Unity numeric values are retained;
//! native reverse-Z reverses ordered comparisons, never Equal/NotEqual.
use bevy::{
    prelude::Reflect,
    render::render_resource::{
        BlendComponent, BlendFactor, BlendOperation, BlendState, CompareFunction, Face,
        RenderPipelineDescriptor,
    },
};
#[derive(Copy, Clone, Debug, PartialEq, Eq, Hash, Reflect)]
pub struct SourceGraphForwardState {
    pub color_src: u8,
    pub color_dst: u8,
    pub alpha_src: u8,
    pub alpha_dst: u8,
    pub cull: u8,
    pub compare: u8,
    pub write_depth: bool,
}
fn factor(v: u8) -> Result<BlendFactor, &'static str> {
    Ok(match v {
        0 => BlendFactor::Zero,
        1 => BlendFactor::One,
        2 => BlendFactor::Dst,
        3 => BlendFactor::Src,
        4 => BlendFactor::OneMinusDst,
        5 => BlendFactor::SrcAlpha,
        6 => BlendFactor::OneMinusSrc,
        7 => BlendFactor::DstAlpha,
        8 => BlendFactor::OneMinusDstAlpha,
        9 => BlendFactor::SrcAlphaSaturated,
        10 => BlendFactor::OneMinusSrcAlpha,
        _ => return Err("Unknown original Unity blend factor"),
    })
}
fn reverse_z_compare(v: u8) -> Result<CompareFunction, &'static str> {
    Ok(match v {
        1 => CompareFunction::Never,
        2 => CompareFunction::Greater,
        3 => CompareFunction::Equal,
        4 => CompareFunction::GreaterEqual,
        5 => CompareFunction::Less,
        6 => CompareFunction::NotEqual,
        7 => CompareFunction::LessEqual,
        8 => CompareFunction::Always,
        _ => return Err("Disabled or unknown source depth comparison requires qualification"),
    })
}
impl SourceGraphForwardState {
    pub fn validate(self) -> Result<(), &'static str> {
        for v in [
            self.color_src,
            self.color_dst,
            self.alpha_src,
            self.alpha_dst,
        ] {
            factor(v)?;
        }
        reverse_z_compare(self.compare)?;
        if self.cull > 2 {
            return Err("Unknown source cull mode");
        }
        Ok(())
    }
    pub fn apply(self, descriptor: &mut RenderPipelineDescriptor) -> Result<(), &'static str> {
        self.validate()?;
        // Validate required attachments before any mutation; never keep a partial
        // native/default blend state on an unsupported source-graph pipeline.
        if descriptor.depth_stencil.is_none() {
            return Err("Source graph forward depth attachment missing");
        }
        if descriptor
            .fragment
            .as_ref()
            .and_then(|f| f.targets.first())
            .and_then(|t| t.as_ref())
            .is_none()
        {
            return Err("Source graph forward color attachment missing");
        }
        let color = BlendComponent {
            src_factor: factor(self.color_src)?,
            dst_factor: factor(self.color_dst)?,
            operation: BlendOperation::Add,
        };
        let alpha = BlendComponent {
            src_factor: factor(self.alpha_src)?,
            dst_factor: factor(self.alpha_dst)?,
            operation: BlendOperation::Add,
        };
        descriptor.fragment.as_mut().unwrap().targets[0]
            .as_mut()
            .unwrap()
            .blend = Some(BlendState { color, alpha });
        descriptor.primitive.cull_mode = match self.cull {
            0 => None,
            1 => Some(Face::Front),
            2 => Some(Face::Back),
            _ => unreachable!(),
        };
        let depth = descriptor.depth_stencil.as_mut().unwrap();
        depth.depth_compare = Some(reverse_z_compare(self.compare)?);
        depth.depth_write_enabled = Some(self.write_depth);
        Ok(())
    }
}
#[cfg(test)]
mod tests {
    use super::*;
    use bevy::render::render_resource::{
        ColorTargetState, ColorWrites, DepthStencilState, FragmentState, TextureFormat,
    };
    fn descriptor() -> RenderPipelineDescriptor {
        let mut d = RenderPipelineDescriptor::default();
        d.fragment = Some(FragmentState {
            targets: vec![
                Some(ColorTargetState {
                    format: TextureFormat::Rgba16Float,
                    blend: None,
                    write_mask: ColorWrites::ALL,
                }),
                Some(ColorTargetState {
                    format: TextureFormat::Rgba16Float,
                    blend: None,
                    write_mask: ColorWrites::RED,
                }),
            ],
            ..Default::default()
        });
        d.depth_stencil = Some(DepthStencilState {
            format: TextureFormat::Depth32Float,
            depth_write_enabled: Some(true),
            depth_compare: Some(CompareFunction::GreaterEqual),
            stencil: Default::default(),
            bias: Default::default(),
        });
        d
    }
    fn cloud_hdrp() -> SourceGraphForwardState {
        SourceGraphForwardState {
            color_src: 1,
            color_dst: 10,
            alpha_src: 1,
            alpha_dst: 10,
            cull: 0,
            compare: 4,
            write_depth: false,
        }
    }
    #[test]
    fn source_graph_render_state_urp_cloud_does_not_use_saved_hdrp_alpha_blend() {
        let h = cloud_hdrp();
        let u = SourceGraphForwardState {
            color_src: 5,
            alpha_src: 5,
            ..h
        };
        let mut a = descriptor();
        let mut b = descriptor();
        h.apply(&mut a).unwrap();
        u.apply(&mut b).unwrap();
        let ha = a.fragment.unwrap().targets[0]
            .as_ref()
            .unwrap()
            .blend
            .unwrap();
        let ua = b.fragment.unwrap().targets[0]
            .as_ref()
            .unwrap()
            .blend
            .unwrap();
        assert_eq!(ha.alpha.src_factor, BlendFactor::One);
        assert_eq!(ua.alpha.src_factor, BlendFactor::SrcAlpha);
        assert_ne!(h, u);
    }
    #[test]
    fn source_graph_render_state_reverse_z_preserves_equal_and_reverses_order() {
        assert_eq!(reverse_z_compare(3).unwrap(), CompareFunction::Equal);
        assert_eq!(reverse_z_compare(6).unwrap(), CompareFunction::NotEqual);
        assert_eq!(reverse_z_compare(4).unwrap(), CompareFunction::GreaterEqual);
        assert_eq!(reverse_z_compare(7).unwrap(), CompareFunction::LessEqual);
    }
    #[test]
    fn source_graph_render_state_missing_attachment_and_unknown_enum_reject_atomically() {
        let mut d = descriptor();
        d.depth_stencil = None;
        let before = d.clone();
        assert!(cloud_hdrp().apply(&mut d).is_err());
        assert_eq!(before, d);
        let mut d = descriptor();
        let before = d.clone();
        assert!(
            SourceGraphForwardState {
                color_src: 255,
                ..cloud_hdrp()
            }
            .apply(&mut d)
            .is_err()
        );
        assert_eq!(before, d);
    }
    #[test]
    fn source_graph_render_state_all48_physical_source_fixtures_preserve_secondary_target() {
        let rows = [
            SourceGraphForwardState {
                color_src: 1,
                color_dst: 10,
                alpha_src: 1,
                alpha_dst: 10,
                cull: 0,
                compare: 4,
                write_depth: true,
            },
            SourceGraphForwardState {
                color_src: 1,
                color_dst: 0,
                alpha_src: 1,
                alpha_dst: 0,
                cull: 2,
                compare: 3,
                write_depth: true,
            },
            SourceGraphForwardState {
                color_src: 1,
                color_dst: 10,
                alpha_src: 1,
                alpha_dst: 10,
                cull: 0,
                compare: 4,
                write_depth: false,
            },
            SourceGraphForwardState {
                color_src: 1,
                color_dst: 10,
                alpha_src: 1,
                alpha_dst: 10,
                cull: 0,
                compare: 4,
                write_depth: true,
            },
            SourceGraphForwardState {
                color_src: 1,
                color_dst: 0,
                alpha_src: 1,
                alpha_dst: 0,
                cull: 2,
                compare: 3,
                write_depth: true,
            },
            SourceGraphForwardState {
                color_src: 1,
                color_dst: 0,
                alpha_src: 1,
                alpha_dst: 0,
                cull: 2,
                compare: 3,
                write_depth: true,
            },
            SourceGraphForwardState {
                color_src: 1,
                color_dst: 10,
                alpha_src: 1,
                alpha_dst: 10,
                cull: 0,
                compare: 4,
                write_depth: true,
            },
            SourceGraphForwardState {
                color_src: 1,
                color_dst: 0,
                alpha_src: 1,
                alpha_dst: 0,
                cull: 2,
                compare: 3,
                write_depth: true,
            },
            SourceGraphForwardState {
                color_src: 1,
                color_dst: 10,
                alpha_src: 1,
                alpha_dst: 10,
                cull: 0,
                compare: 4,
                write_depth: true,
            },
            SourceGraphForwardState {
                color_src: 1,
                color_dst: 0,
                alpha_src: 1,
                alpha_dst: 0,
                cull: 2,
                compare: 3,
                write_depth: true,
            },
            SourceGraphForwardState {
                color_src: 1,
                color_dst: 0,
                alpha_src: 1,
                alpha_dst: 0,
                cull: 2,
                compare: 3,
                write_depth: true,
            },
            SourceGraphForwardState {
                color_src: 1,
                color_dst: 0,
                alpha_src: 1,
                alpha_dst: 0,
                cull: 2,
                compare: 3,
                write_depth: true,
            },
            SourceGraphForwardState {
                color_src: 1,
                color_dst: 0,
                alpha_src: 1,
                alpha_dst: 0,
                cull: 2,
                compare: 3,
                write_depth: true,
            },
            SourceGraphForwardState {
                color_src: 1,
                color_dst: 0,
                alpha_src: 1,
                alpha_dst: 0,
                cull: 2,
                compare: 3,
                write_depth: true,
            },
            SourceGraphForwardState {
                color_src: 1,
                color_dst: 0,
                alpha_src: 1,
                alpha_dst: 0,
                cull: 0,
                compare: 3,
                write_depth: true,
            },
            SourceGraphForwardState {
                color_src: 1,
                color_dst: 0,
                alpha_src: 1,
                alpha_dst: 0,
                cull: 2,
                compare: 3,
                write_depth: true,
            },
            SourceGraphForwardState {
                color_src: 1,
                color_dst: 0,
                alpha_src: 1,
                alpha_dst: 0,
                cull: 2,
                compare: 3,
                write_depth: true,
            },
            SourceGraphForwardState {
                color_src: 1,
                color_dst: 0,
                alpha_src: 1,
                alpha_dst: 0,
                cull: 0,
                compare: 3,
                write_depth: true,
            },
            SourceGraphForwardState {
                color_src: 1,
                color_dst: 0,
                alpha_src: 1,
                alpha_dst: 0,
                cull: 2,
                compare: 3,
                write_depth: true,
            },
            SourceGraphForwardState {
                color_src: 1,
                color_dst: 0,
                alpha_src: 1,
                alpha_dst: 0,
                cull: 2,
                compare: 3,
                write_depth: true,
            },
            SourceGraphForwardState {
                color_src: 1,
                color_dst: 0,
                alpha_src: 1,
                alpha_dst: 0,
                cull: 2,
                compare: 3,
                write_depth: true,
            },
            SourceGraphForwardState {
                color_src: 1,
                color_dst: 10,
                alpha_src: 1,
                alpha_dst: 10,
                cull: 0,
                compare: 4,
                write_depth: true,
            },
            SourceGraphForwardState {
                color_src: 1,
                color_dst: 10,
                alpha_src: 1,
                alpha_dst: 10,
                cull: 0,
                compare: 4,
                write_depth: true,
            },
            SourceGraphForwardState {
                color_src: 1,
                color_dst: 0,
                alpha_src: 1,
                alpha_dst: 0,
                cull: 2,
                compare: 3,
                write_depth: true,
            },
            SourceGraphForwardState {
                color_src: 1,
                color_dst: 0,
                alpha_src: 1,
                alpha_dst: 0,
                cull: 0,
                compare: 4,
                write_depth: true,
            },
            SourceGraphForwardState {
                color_src: 1,
                color_dst: 0,
                alpha_src: 1,
                alpha_dst: 0,
                cull: 2,
                compare: 4,
                write_depth: true,
            },
            SourceGraphForwardState {
                color_src: 5,
                color_dst: 10,
                alpha_src: 5,
                alpha_dst: 10,
                cull: 2,
                compare: 4,
                write_depth: false,
            },
            SourceGraphForwardState {
                color_src: 1,
                color_dst: 0,
                alpha_src: 1,
                alpha_dst: 0,
                cull: 0,
                compare: 4,
                write_depth: true,
            },
            SourceGraphForwardState {
                color_src: 1,
                color_dst: 0,
                alpha_src: 1,
                alpha_dst: 0,
                cull: 2,
                compare: 4,
                write_depth: true,
            },
            SourceGraphForwardState {
                color_src: 1,
                color_dst: 0,
                alpha_src: 1,
                alpha_dst: 0,
                cull: 2,
                compare: 4,
                write_depth: true,
            },
            SourceGraphForwardState {
                color_src: 1,
                color_dst: 0,
                alpha_src: 1,
                alpha_dst: 0,
                cull: 0,
                compare: 4,
                write_depth: true,
            },
            SourceGraphForwardState {
                color_src: 1,
                color_dst: 0,
                alpha_src: 1,
                alpha_dst: 0,
                cull: 2,
                compare: 4,
                write_depth: true,
            },
            SourceGraphForwardState {
                color_src: 1,
                color_dst: 0,
                alpha_src: 1,
                alpha_dst: 0,
                cull: 0,
                compare: 4,
                write_depth: true,
            },
            SourceGraphForwardState {
                color_src: 1,
                color_dst: 0,
                alpha_src: 1,
                alpha_dst: 0,
                cull: 2,
                compare: 4,
                write_depth: true,
            },
            SourceGraphForwardState {
                color_src: 1,
                color_dst: 0,
                alpha_src: 1,
                alpha_dst: 0,
                cull: 2,
                compare: 4,
                write_depth: true,
            },
            SourceGraphForwardState {
                color_src: 1,
                color_dst: 0,
                alpha_src: 1,
                alpha_dst: 0,
                cull: 2,
                compare: 4,
                write_depth: true,
            },
            SourceGraphForwardState {
                color_src: 1,
                color_dst: 0,
                alpha_src: 1,
                alpha_dst: 0,
                cull: 2,
                compare: 4,
                write_depth: true,
            },
            SourceGraphForwardState {
                color_src: 1,
                color_dst: 0,
                alpha_src: 1,
                alpha_dst: 0,
                cull: 2,
                compare: 4,
                write_depth: true,
            },
            SourceGraphForwardState {
                color_src: 1,
                color_dst: 0,
                alpha_src: 1,
                alpha_dst: 0,
                cull: 0,
                compare: 4,
                write_depth: true,
            },
            SourceGraphForwardState {
                color_src: 1,
                color_dst: 0,
                alpha_src: 1,
                alpha_dst: 0,
                cull: 2,
                compare: 4,
                write_depth: true,
            },
            SourceGraphForwardState {
                color_src: 1,
                color_dst: 0,
                alpha_src: 1,
                alpha_dst: 0,
                cull: 2,
                compare: 4,
                write_depth: true,
            },
            SourceGraphForwardState {
                color_src: 1,
                color_dst: 0,
                alpha_src: 1,
                alpha_dst: 0,
                cull: 2,
                compare: 4,
                write_depth: true,
            },
            SourceGraphForwardState {
                color_src: 1,
                color_dst: 0,
                alpha_src: 1,
                alpha_dst: 0,
                cull: 2,
                compare: 4,
                write_depth: true,
            },
            SourceGraphForwardState {
                color_src: 1,
                color_dst: 0,
                alpha_src: 1,
                alpha_dst: 0,
                cull: 2,
                compare: 4,
                write_depth: true,
            },
            SourceGraphForwardState {
                color_src: 1,
                color_dst: 0,
                alpha_src: 1,
                alpha_dst: 0,
                cull: 2,
                compare: 4,
                write_depth: true,
            },
            SourceGraphForwardState {
                color_src: 1,
                color_dst: 0,
                alpha_src: 1,
                alpha_dst: 0,
                cull: 0,
                compare: 4,
                write_depth: true,
            },
            SourceGraphForwardState {
                color_src: 1,
                color_dst: 0,
                alpha_src: 1,
                alpha_dst: 0,
                cull: 0,
                compare: 4,
                write_depth: true,
            },
            SourceGraphForwardState {
                color_src: 1,
                color_dst: 0,
                alpha_src: 1,
                alpha_dst: 0,
                cull: 2,
                compare: 4,
                write_depth: true,
            },
        ];
        assert_eq!(rows.len(), 48);
        for row in rows {
            let mut d = descriptor();
            let secondary = d.fragment.as_ref().unwrap().targets[1].clone();
            row.apply(&mut d).unwrap();
            assert_eq!(secondary, d.fragment.as_ref().unwrap().targets[1]);
            assert_eq!(
                d.depth_stencil.as_ref().unwrap().depth_write_enabled,
                Some(row.write_depth)
            );
        }
    }
}
