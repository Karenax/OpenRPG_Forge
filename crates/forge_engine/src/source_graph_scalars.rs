//! Separate scalar binding; existing source basis/time uniform ABI stays intact.
use bevy::{
    prelude::{Reflect, Vec4},
    render::render_resource::ShaderType,
};
use forge_assets::source_material_parameters::SourceMaterialScalarParameters;

#[derive(Clone, Debug, Default, Reflect, ShaderType)]
pub struct SourceGraphScalarUniforms {
    pub lanes: [Vec4; 16],
}

impl SourceGraphScalarUniforms {
    /// The compiler's declaration order controls slots. Undeclared properties
    /// cannot be introduced by a renderer or a material name.
    pub fn from_parameters(parameters: &SourceMaterialScalarParameters) -> Self {
        Self {
            lanes: parameters.vec4_lanes().map(Vec4::from_array),
        }
    }
}

#[cfg(test)]
mod tests {
    use super::*;
    use bevy::render::render_resource::encase::UniformBuffer;

    #[test]
    fn bevy_uniform_encoder_matches_domain_packet_at_all_slot_boundaries() {
        let parameters = SourceMaterialScalarParameters::from_compiler_slots(
            (0_u8..64)
                .map(|i| (format!("slot_{i}"), f32::from(i)))
                .collect(),
        )
        .unwrap();
        let uniform = SourceGraphScalarUniforms::from_parameters(&parameters);
        let mut encoded = UniformBuffer::new(Vec::<u8>::new());
        encoded.write(&uniform).unwrap();
        assert_eq!(
            encoded.into_inner(),
            parameters.little_endian_uniform_bytes()
        );
    }
}
