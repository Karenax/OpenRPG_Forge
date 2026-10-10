//! Phase-only base for authored source stages. Native PBR texture slots are
//! never sampled by these stages and must not consume WebGPU sampler limits.
use bevy::{
    material::OpaqueRendererMethod,
    mesh::MeshVertexBufferLayoutRef,
    pbr::{
        Material, MaterialPipeline, MaterialPipelineKey, StandardMaterialKey,
        StandardMaterialUniform,
    },
    prelude::*,
    render::{
        render_asset::RenderAssets,
        render_resource::{
            AsBindGroup, AsBindGroupShaderType, RenderPipelineDescriptor,
            SpecializedMeshPipelineError,
        },
        texture::GpuImage,
    },
    shader::ShaderRef,
};

#[derive(Asset, AsBindGroup, Clone, Debug, Reflect)]
#[uniform(0, StandardMaterialUniform)]
#[bind_group_data(StandardMaterialKey)]
pub struct SourceGraphPhaseMaterial {
    pub alpha_mode: AlphaMode,
}

impl SourceGraphPhaseMaterial {
    fn standard(&self) -> StandardMaterial {
        StandardMaterial {
            alpha_mode: self.alpha_mode,
            opaque_render_method: OpaqueRendererMethod::Forward,
            ..StandardMaterial::default()
        }
    }
}
impl Default for SourceGraphPhaseMaterial {
    fn default() -> Self {
        Self {
            alpha_mode: AlphaMode::Opaque,
        }
    }
}
impl From<&SourceGraphPhaseMaterial> for StandardMaterialKey {
    fn from(value: &SourceGraphPhaseMaterial) -> Self {
        Self::from(&value.standard())
    }
}
impl AsBindGroupShaderType<StandardMaterialUniform> for SourceGraphPhaseMaterial {
    fn as_bind_group_shader_type(
        &self,
        images: &RenderAssets<GpuImage>,
    ) -> StandardMaterialUniform {
        self.standard().as_bind_group_shader_type(images)
    }
}
impl Material for SourceGraphPhaseMaterial {
    fn fragment_shader() -> ShaderRef {
        StandardMaterial::fragment_shader()
    }
    fn prepass_fragment_shader() -> ShaderRef {
        StandardMaterial::prepass_fragment_shader()
    }
    fn deferred_fragment_shader() -> ShaderRef {
        StandardMaterial::deferred_fragment_shader()
    }
    fn alpha_mode(&self) -> AlphaMode {
        self.alpha_mode
    }
    fn opaque_render_method(&self) -> OpaqueRendererMethod {
        OpaqueRendererMethod::Forward
    }
    fn specialize(
        pipeline: &MaterialPipeline,
        descriptor: &mut RenderPipelineDescriptor,
        layout: &MeshVertexBufferLayoutRef,
        key: MaterialPipelineKey<Self>,
    ) -> Result<(), SpecializedMeshPipelineError> {
        StandardMaterial::specialize(
            pipeline,
            descriptor,
            layout,
            MaterialPipelineKey::<StandardMaterial> {
                mesh_key: key.mesh_key,
                bind_group_data: key.bind_group_data,
            },
        )
    }
}

#[cfg(test)]
mod tests {
    use super::*;
    #[test]
    fn source_phase_base_preserves_standard_phase_and_specialization_key() {
        for alpha_mode in [AlphaMode::Opaque, AlphaMode::Mask(0.0), AlphaMode::Blend] {
            let base = SourceGraphPhaseMaterial { alpha_mode };
            let standard = StandardMaterial {
                alpha_mode,
                opaque_render_method: OpaqueRendererMethod::Forward,
                ..StandardMaterial::default()
            };
            assert_eq!(Material::alpha_mode(&base), Material::alpha_mode(&standard));
            assert!(
                StandardMaterialKey::from(&base) == StandardMaterialKey::from(&standard),
                "The phase-only material must preserve the standard specialization key"
            );
            assert_eq!(
                Material::opaque_render_method(&base),
                Material::opaque_render_method(&standard)
            );
        }
    }
}
