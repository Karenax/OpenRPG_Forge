//! Phase metadata for source graph materials. Original graph code retains
//! all alpha and shadow terminal expressions; no native cutoff replaces them.
use super::{SourceGraphExtension, SourceGraphMaterial};
use bevy::{material::OpaqueRendererMethod, prelude::*};

#[derive(Copy, Clone, Debug, PartialEq, Eq, Hash, Reflect)]
pub enum SourceGraphSurfacePhase {
    Opaque,
    AlphaTest,
    Transparent,
}
impl SourceGraphSurfacePhase {
    pub fn from_original_flags(transparent: bool, alpha_test: bool) -> Self {
        if transparent {
            Self::Transparent
        } else if alpha_test {
            Self::AlphaTest
        } else {
            Self::Opaque
        }
    }
    /// Bevy classifies the phase by AlphaMode's variant, independent of the
    /// Mask payload. Zero here is a routing marker, NOT a source alpha cutoff.
    /// All source vertex/fragment/prepass stages replace native PBR stages and
    /// perform their own graph-derived clipping. Never pass this base alone to
    /// a MeshMaterial3d<StandardMaterial> or native clipping shader.
    fn base(self) -> StandardMaterial {
        StandardMaterial {
            alpha_mode: match self {
                Self::Opaque => AlphaMode::Opaque,
                Self::AlphaTest => AlphaMode::Mask(0.0),
                Self::Transparent => AlphaMode::Blend,
            },
            opaque_render_method: OpaqueRendererMethod::Forward,
            ..Default::default()
        }
    }
}
/// Source stages own clipping, BRDF, and RGBA blending. The base supplies only
/// native phase metadata; SourceGraphForwardState sets the actual blend factors.
pub fn source_graph_material(
    extension: SourceGraphExtension,
    phase: SourceGraphSurfacePhase,
) -> SourceGraphMaterial {
    SourceGraphMaterial {
        base: phase.base(),
        extension,
    }
}

#[cfg(test)]
mod tests {
    use super::*;
    use bevy::pbr::Material;
    use bevy::pbr::{MeshPipelineKey, alpha_mode_pipeline_key};
    #[test]
    fn source_graph_phase_all48_physical_materials_follow_original_keywords() {
        let cases = [
            (true, true, SourceGraphSurfacePhase::Transparent),
            (false, false, SourceGraphSurfacePhase::Opaque),
            (true, true, SourceGraphSurfacePhase::Transparent),
            (true, true, SourceGraphSurfacePhase::Transparent),
            (false, false, SourceGraphSurfacePhase::Opaque),
            (false, false, SourceGraphSurfacePhase::Opaque),
            (true, true, SourceGraphSurfacePhase::Transparent),
            (false, false, SourceGraphSurfacePhase::Opaque),
            (true, true, SourceGraphSurfacePhase::Transparent),
            (false, false, SourceGraphSurfacePhase::Opaque),
            (false, false, SourceGraphSurfacePhase::Opaque),
            (false, false, SourceGraphSurfacePhase::Opaque),
            (false, false, SourceGraphSurfacePhase::Opaque),
            (false, false, SourceGraphSurfacePhase::Opaque),
            (false, true, SourceGraphSurfacePhase::AlphaTest),
            (false, false, SourceGraphSurfacePhase::Opaque),
            (false, false, SourceGraphSurfacePhase::Opaque),
            (false, true, SourceGraphSurfacePhase::AlphaTest),
            (false, false, SourceGraphSurfacePhase::Opaque),
            (false, false, SourceGraphSurfacePhase::Opaque),
            (false, false, SourceGraphSurfacePhase::Opaque),
            (true, true, SourceGraphSurfacePhase::Transparent),
            (true, true, SourceGraphSurfacePhase::Transparent),
            (false, false, SourceGraphSurfacePhase::Opaque),
            (false, true, SourceGraphSurfacePhase::AlphaTest),
            (false, false, SourceGraphSurfacePhase::Opaque),
            (true, true, SourceGraphSurfacePhase::Transparent),
            (false, true, SourceGraphSurfacePhase::AlphaTest),
            (false, false, SourceGraphSurfacePhase::Opaque),
            (false, false, SourceGraphSurfacePhase::Opaque),
            (false, true, SourceGraphSurfacePhase::AlphaTest),
            (false, false, SourceGraphSurfacePhase::Opaque),
            (false, true, SourceGraphSurfacePhase::AlphaTest),
            (false, false, SourceGraphSurfacePhase::Opaque),
            (false, false, SourceGraphSurfacePhase::Opaque),
            (false, false, SourceGraphSurfacePhase::Opaque),
            (false, false, SourceGraphSurfacePhase::Opaque),
            (false, false, SourceGraphSurfacePhase::Opaque),
            (false, true, SourceGraphSurfacePhase::AlphaTest),
            (false, false, SourceGraphSurfacePhase::Opaque),
            (false, false, SourceGraphSurfacePhase::Opaque),
            (false, true, SourceGraphSurfacePhase::AlphaTest),
            (false, false, SourceGraphSurfacePhase::Opaque),
            (false, false, SourceGraphSurfacePhase::Opaque),
            (false, false, SourceGraphSurfacePhase::Opaque),
            (false, true, SourceGraphSurfacePhase::AlphaTest),
            (false, true, SourceGraphSurfacePhase::AlphaTest),
            (false, false, SourceGraphSurfacePhase::Opaque),
        ];
        assert_eq!(cases.len(), 48);
        for (transparent, alpha, phase) in cases {
            assert_eq!(
                SourceGraphSurfacePhase::from_original_flags(transparent, alpha),
                phase
            );
            let base = phase.base();
            assert_eq!(
                Material::opaque_render_method(&base),
                OpaqueRendererMethod::Forward
            );
            match phase {
                SourceGraphSurfacePhase::Opaque => {
                    assert_eq!(Material::alpha_mode(&base), AlphaMode::Opaque)
                }
                SourceGraphSurfacePhase::AlphaTest => {
                    assert!(matches!(Material::alpha_mode(&base), AlphaMode::Mask(_)))
                }
                SourceGraphSurfacePhase::Transparent => {
                    assert_eq!(Material::alpha_mode(&base), AlphaMode::Blend)
                }
            }
        }
    }
    #[test]
    fn source_graph_phase_transparent_alpha_test_remains_sorted_transparent() {
        let phase = SourceGraphSurfacePhase::from_original_flags(true, true);
        assert_eq!(phase, SourceGraphSurfacePhase::Transparent);
        let key = alpha_mode_pipeline_key(Material::alpha_mode(&phase.base()), &Msaa::Sample4);
        assert!(key.contains(MeshPipelineKey::BLEND_ALPHA));
        assert!(!key.contains(MeshPipelineKey::MAY_DISCARD));
    }
    #[test]
    fn source_graph_phase_cutout_uses_discard_phase_without_native_alpha_coverage() {
        let phase = SourceGraphSurfacePhase::from_original_flags(false, true);
        let key = alpha_mode_pipeline_key(Material::alpha_mode(&phase.base()), &Msaa::Sample4);
        assert!(key.contains(MeshPipelineKey::MAY_DISCARD));
        assert!(!key.contains(MeshPipelineKey::BLEND_ALPHA));
    }
}
