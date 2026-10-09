//! Explicit preconvolved linear SH9 irradiance, independent of material identity.
//! This packet does not qualify sky acquisition, BRDF, exposure or rendered parity.
use bevy::{
    prelude::{Mat4, Reflect, Vec4},
    render::render_resource::ShaderType,
};

#[derive(Clone, Debug, Reflect, ShaderType)]
pub struct SourceGraphDiffuseProbeUniforms {
    direction_source_from_target: Mat4,
    coefficients: [Vec4; 7],
    control: Vec4,
}

impl Default for SourceGraphDiffuseProbeUniforms {
    fn default() -> Self {
        Self {
            direction_source_from_target: Mat4::IDENTITY,
            coefficients: [Vec4::ZERO; 7],
            control: Vec4::ZERO,
        }
    }
}

impl SourceGraphDiffuseProbeUniforms {
    /// Input coefficients already include cosine convolution in linear source units.
    /// The explicit rigid basis may reflect axes; translation never affects normals.
    /// Nonfinite, scaled and sheared mappings are refused rather than normalized.
    pub fn from_preconvolved(
        coefficients: [Vec4; 7],
        source_from_target: Mat4,
    ) -> Result<Self, &'static str> {
        if coefficients.iter().any(|v| !v.is_finite())
            || !source_from_target.is_finite()
            || source_from_target.x_axis.w != 0.0
            || source_from_target.y_axis.w != 0.0
            || source_from_target.z_axis.w != 0.0
            || source_from_target.w_axis.w != 1.0
        {
            return Err("Diffuse probe requires finite coefficients and an affine source basis");
        }
        let axes = [
            source_from_target.x_axis.truncate(),
            source_from_target.y_axis.truncate(),
            source_from_target.z_axis.truncate(),
        ];
        if axes.iter().any(|v| (v.length_squared() - 1.0).abs() > 1e-4)
            || axes[0].dot(axes[1]).abs() > 1e-4
            || axes[0].dot(axes[2]).abs() > 1e-4
            || axes[1].dot(axes[2]).abs() > 1e-4
        {
            return Err("Diffuse probe source basis must be rigid, including explicit reflections");
        }
        Ok(Self {
            direction_source_from_target: Mat4::from_cols(
                source_from_target.x_axis,
                source_from_target.y_axis,
                source_from_target.z_axis,
                Vec4::W,
            ),
            coefficients,
            control: Vec4::X,
        })
    }
}

pub const SOURCE_GRAPH_DIFFUSE_PROBE_WGSL: &str = include_str!("shaders/source_diffuse_probe.wgsl");

#[cfg(test)]
mod tests {
    use super::*;
    use bevy::{prelude::Vec3, render::render_resource::encase::UniformBuffer};
    #[test]
    fn admits_reflection_and_strips_translation_without_changing_coefficients() {
        let coefficients = std::array::from_fn(|i| Vec4::splat(i as f32));
        let basis = Mat4::from_scale_rotation_translation(
            Vec3::new(-1.0, 1.0, 1.0),
            bevy::prelude::Quat::IDENTITY,
            Vec3::new(0.0, -1024.0, 0.0),
        );
        let probe =
            SourceGraphDiffuseProbeUniforms::from_preconvolved(coefficients, basis).unwrap();
        assert_eq!(probe.coefficients, coefficients);
        assert_eq!(
            probe.direction_source_from_target,
            Mat4::from_scale(Vec3::new(-1.0, 1.0, 1.0))
        );
        let mut encoded = UniformBuffer::new(Vec::<u8>::new());
        encoded.write(&probe).unwrap();
        let bytes = encoded.into_inner();
        assert_eq!(bytes.len(), 192);
        assert_eq!(&bytes[64..80], &[0_u8; 16]);
        assert_eq!(f32::from_le_bytes(bytes[176..180].try_into().unwrap()), 1.0);
    }
    #[test]
    fn rejects_unknown_units_from_scale_shear_and_nonfinite_data() {
        let coefficients = [Vec4::ZERO; 7];
        assert!(
            SourceGraphDiffuseProbeUniforms::from_preconvolved(
                coefficients,
                Mat4::from_scale(Vec3::splat(2.0))
            )
            .is_err()
        );
        let mut shear = Mat4::IDENTITY;
        shear.y_axis.x = 0.25;
        assert!(SourceGraphDiffuseProbeUniforms::from_preconvolved(coefficients, shear).is_err());
        let mut invalid = coefficients;
        invalid[3].y = f32::NAN;
        assert!(
            SourceGraphDiffuseProbeUniforms::from_preconvolved(invalid, Mat4::IDENTITY).is_err()
        );
        assert_eq!(
            SourceGraphDiffuseProbeUniforms::default().control,
            Vec4::ZERO
        );
    }
}
