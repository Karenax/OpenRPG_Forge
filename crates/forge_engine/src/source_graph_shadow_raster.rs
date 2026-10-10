//! Explicit raster policy for source orthographic shadow passes.
//! Receiver normal/depth biases are separate shader inputs.
use bevy::{
    prelude::Reflect,
    render::render_resource::{
        CompareFunction, DepthBiasState, FrontFace, RenderPipelineDescriptor, TextureFormat,
    },
};

#[derive(Copy, Clone, Debug, PartialEq, Eq, Hash, Reflect)]
pub struct SourceGraphShadowRasterState {
    constant: i32,
    slope_bits: u32,
    clamp_bits: u32,
    clockwise_front_face: Option<bool>,
}
impl SourceGraphShadowRasterState {
    #[must_use]
    pub fn is_orthographic_view(key: bevy::pbr::MeshPipelineKey) -> bool {
        key.intersection(bevy::pbr::MeshPipelineKey::VIEW_PROJECTION_RESERVED_BITS)
            == bevy::pbr::MeshPipelineKey::VIEW_PROJECTION_ORTHOGRAPHIC
    }

    /// Resolved backend units/sign; this constructor does not infer source conventions.
    pub fn resolved(constant: i32, slope_scale: f32, clamp: f32) -> Result<Self, &'static str> {
        if !slope_scale.is_finite() || !clamp.is_finite() || clamp < 0.0 {
            return Err("Source raster bias must be finite with a nonnegative clamp");
        }
        Ok(Self {
            constant,
            slope_bits: if slope_scale == 0.0 {
                0
            } else {
                slope_scale.to_bits()
            },
            clamp_bits: if clamp == 0.0 { 0 } else { clamp.to_bits() },
            clockwise_front_face: None,
        })
    }
    /// Explicit backend orientation for the resolved source shadow projection.
    /// No convention is inferred from an asset identity or a default view.
    #[must_use]
    pub fn with_front_face(mut self, face: FrontFace) -> Self {
        self.clockwise_front_face = Some(face == FrontFace::Cw);
        self
    }
    #[must_use]
    pub fn front_face(self) -> Option<FrontFace> {
        self.clockwise_front_face
            .map(|cw| if cw { FrontFace::Cw } else { FrontFace::Ccw })
    }
    pub fn bias(self) -> Result<DepthBiasState, &'static str> {
        let slope_scale = f32::from_bits(self.slope_bits);
        let clamp = f32::from_bits(self.clamp_bits);
        Self::resolved(self.constant, slope_scale, clamp)?;
        Ok(DepthBiasState {
            constant: self.constant,
            slope_scale,
            clamp,
        })
    }
    /// Only an explicitly routed orthographic source shadow may receive this state.
    /// Check the full depth contract before touching the descriptor.
    pub fn apply(
        self,
        descriptor: &mut RenderPipelineDescriptor,
        shadow_only: bool,
        prepass: bool,
        orthographic: bool,
    ) -> Result<(), &'static str> {
        if !shadow_only || !prepass || !orthographic {
            return Ok(());
        }
        let bias = self.bias()?;
        let depth = descriptor
            .depth_stencil
            .as_mut()
            .ok_or("Source shadow depth attachment absent")?;
        if depth.format != TextureFormat::Depth32Float
            || depth.depth_compare != Some(CompareFunction::GreaterEqual)
            || depth.depth_write_enabled != Some(true)
        {
            return Err("Resolved source shadow raster requires writable reversed D32 depth");
        }
        depth.bias = bias;
        if let Some(face) = self.front_face() {
            descriptor.primitive.front_face = face;
        }
        Ok(())
    }
}
#[cfg(test)]
mod tests {
    use super::*;
    use bevy::render::render_resource::DepthStencilState;
    fn descriptor() -> RenderPipelineDescriptor {
        RenderPipelineDescriptor {
            depth_stencil: Some(DepthStencilState {
                format: TextureFormat::Depth32Float,
                depth_write_enabled: Some(true),
                depth_compare: Some(CompareFunction::GreaterEqual),
                stencil: bevy::render::render_resource::StencilState::default(),
                bias: DepthBiasState {
                    constant: 3,
                    slope_scale: 0.25,
                    clamp: 0.0,
                },
            }),
            ..Default::default()
        }
    }

    #[test]
    fn reserved_and_nonstandard_projection_keys_are_not_orthographic() {
        use bevy::pbr::MeshPipelineKey as K;
        for key in [
            K::VIEW_PROJECTION_NONSTANDARD,
            K::VIEW_PROJECTION_PERSPECTIVE,
            K::VIEW_PROJECTION_RESERVED,
        ] {
            assert!(!SourceGraphShadowRasterState::is_orthographic_view(key));
        }
        assert!(SourceGraphShadowRasterState::is_orthographic_view(
            K::VIEW_PROJECTION_ORTHOGRAPHIC
        ));
    }
    #[test]
    fn resolved_bias_rejects_nonfinite_and_negative_clamp() {
        for slope in [f32::NAN, f32::INFINITY, f32::NEG_INFINITY] {
            assert!(SourceGraphShadowRasterState::resolved(-1, slope, 0.0).is_err());
        }
        for clamp in [f32::NAN, f32::INFINITY, -0.1] {
            assert!(SourceGraphShadowRasterState::resolved(-1, -2.5, clamp).is_err());
        }
    }
    #[test]
    fn resolved_bias_has_distinct_cache_identity_and_canonical_zero() {
        let a = SourceGraphShadowRasterState::resolved(-1, -2.5, 0.0).unwrap();
        assert_ne!(
            a,
            SourceGraphShadowRasterState::resolved(-1, -2.0, 0.0).unwrap()
        );
        assert_eq!(
            SourceGraphShadowRasterState::resolved(0, -0.0, -0.0).unwrap(),
            SourceGraphShadowRasterState::resolved(0, 0.0, 0.0).unwrap()
        );
    }
    #[test]
    fn explicit_orthographic_shadow_retains_every_other_depth_field() {
        let mut d = descriptor();
        let mut expected = d.depth_stencil.clone().unwrap();
        expected.bias = DepthBiasState {
            constant: -1,
            slope_scale: -2.5,
            clamp: 0.0,
        };
        SourceGraphShadowRasterState::resolved(-1, -2.5, 0.0)
            .unwrap()
            .apply(&mut d, true, true, true)
            .unwrap();
        assert_eq!(d.depth_stencil, Some(expected));
    }
    #[test]
    fn camera_and_perspective_passes_keep_their_original_bias() {
        for flags in [
            (false, true, true),
            (true, false, true),
            (true, true, false),
        ] {
            let mut d = descriptor();
            let before = d.depth_stencil.clone();
            let primitive_before = d.primitive;
            SourceGraphShadowRasterState::resolved(-1, -2.5, 0.0)
                .unwrap()
                .with_front_face(FrontFace::Cw)
                .apply(&mut d, flags.0, flags.1, flags.2)
                .unwrap();
            assert_eq!(d.depth_stencil, before);
            assert_eq!(d.primitive, primitive_before);
        }
    }
    #[test]
    fn incompatible_depth_contract_is_refused_without_mutation() {
        let policy = SourceGraphShadowRasterState::resolved(-1, -2.5, 0.0)
            .unwrap()
            .with_front_face(FrontFace::Cw);
        for variant in 0..4 {
            let mut d = descriptor();
            let depth = d.depth_stencil.as_mut().unwrap();
            match variant {
                0 => depth.format = TextureFormat::Depth16Unorm,
                1 => depth.depth_compare = Some(CompareFunction::LessEqual),
                2 => depth.depth_write_enabled = Some(false),
                _ => d.depth_stencil = None,
            }
            let before = d.depth_stencil.clone();
            let primitive_before = d.primitive;
            assert!(policy.apply(&mut d, true, true, true).is_err());
            assert_eq!(d.depth_stencil, before);
            assert_eq!(d.primitive, primitive_before);
        }
    }
    #[test]
    fn reflected_invalid_bits_are_rechecked_before_descriptor_mutation() {
        let bad = SourceGraphShadowRasterState {
            constant: -1,
            slope_bits: f32::NAN.to_bits(),
            clamp_bits: 0,
            clockwise_front_face: Some(true),
        };
        let mut d = descriptor();
        let before = d.depth_stencil.clone();
        assert!(bad.apply(&mut d, true, true, true).is_err());
        assert_eq!(d.depth_stencil, before);
    }
    #[test]
    fn explicit_orientation_changes_only_front_face_and_keeps_cull_mode() {
        let mut d = descriptor();
        d.primitive.cull_mode = Some(bevy::render::render_resource::Face::Back);
        let mut expected = d.primitive;
        expected.front_face = FrontFace::Cw;
        let policy = SourceGraphShadowRasterState::resolved(-1, -2.5, 0.0)
            .unwrap()
            .with_front_face(FrontFace::Cw);
        policy.apply(&mut d, true, true, true).unwrap();
        assert_eq!(d.primitive, expected);
        assert_eq!(d.depth_stencil.unwrap().bias, policy.bias().unwrap());
    }
    #[test]
    fn front_face_is_in_cache_identity() {
        let base = SourceGraphShadowRasterState::resolved(-1, -2.5, 0.0).unwrap();
        let states: std::collections::HashSet<_> = [
            base,
            base.with_front_face(FrontFace::Cw),
            base.with_front_face(FrontFace::Ccw),
        ]
        .into_iter()
        .collect();
        assert_eq!(states.len(), 3);
    }
    #[test]
    fn unresolved_orientation_keeps_existing_front_face() {
        let mut d = descriptor();
        d.primitive.front_face = FrontFace::Cw;
        let before = d.primitive;
        let policy = SourceGraphShadowRasterState::resolved(-1, -2.5, 0.0).unwrap();
        assert_eq!(policy.front_face(), None);
        policy.apply(&mut d, true, true, true).unwrap();
        assert_eq!(d.primitive, before);
    }
}
