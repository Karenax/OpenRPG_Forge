//! Required scene images are supplied by the host, never fabricated importers.
use super::{
    SourceGraphContractError, SourceGraphMaterialContract, SourceGraphResolvedStorageSampler,
};
use serde::{Deserialize, Serialize};

#[derive(Clone, Copy, Debug, PartialEq, Eq, Serialize, Deserialize)]
#[serde(rename_all = "snake_case")]
pub enum SourceGraphSceneTextureFormat {
    Rgba8Unorm,
    Rgb10a2Unorm,
    Rgba16Float,
    Rgba32Float,
}

#[derive(Clone, Copy, Debug, PartialEq, Eq, Serialize, Deserialize)]
#[serde(tag = "kind", rename_all = "snake_case", deny_unknown_fields)]
pub enum SourceGraphSceneTextureDimensions {
    Viewport,
    Fixed { width: u32, height: u32 },
}

impl SourceGraphSceneTextureDimensions {
    pub fn resolve(self, viewport: [u32; 2]) -> Result<[u32; 2], SourceGraphContractError> {
        let size = match self {
            Self::Viewport => viewport,
            Self::Fixed { width, height } => [width, height],
        };
        if size.iter().any(|n| *n == 0 || *n > 16384) {
            return Err(SourceGraphContractError::SceneTexture);
        }
        Ok(size)
    }
}

#[derive(Clone, Debug, PartialEq, Serialize, Deserialize)]
#[serde(deny_unknown_fields)]
pub struct SourceGraphSceneTexture {
    pub slot: usize,
    pub resource_key: String,
    pub format: SourceGraphSceneTextureFormat,
    pub dimensions: SourceGraphSceneTextureDimensions,
    pub sampler: SourceGraphResolvedStorageSampler,
}

impl SourceGraphSceneTexture {
    pub fn validate(&self) -> Result<(), SourceGraphContractError> {
        let s = &self.sampler;
        if self.resource_key.is_empty()
            || self.resource_key.len() > 128
            || !self
                .resource_key
                .bytes()
                .all(|b| b.is_ascii_alphanumeric() || b"._-".contains(&b))
            || s.filter_mode > 2
            || s.wrap.iter().any(|v| *v > 3)
            || !(1..=16).contains(&s.anisotropy)
            || !s.mip_bias.is_finite()
            || s.source_mipmaps_enabled
        {
            return Err(SourceGraphContractError::SceneTexture);
        }
        self.dimensions.resolve([1, 1])?;
        Ok(())
    }
}

impl SourceGraphMaterialContract {
    #[must_use]
    pub fn texture_binding_count(&self) -> usize {
        self.textures.len() + self.scene_textures.len()
    }

    pub(super) fn validate_scene_textures(&self) -> Result<(), SourceGraphContractError> {
        if !self.scene_textures.is_empty() && self.schema_version != 7 {
            return Err(SourceGraphContractError::SceneTexture);
        }
        let mut slots = std::collections::BTreeSet::new();
        for texture in &self.scene_textures {
            texture.validate()?;
            if texture.slot < self.textures.len()
                || texture.slot >= self.texture_binding_count()
                || !slots.insert(texture.slot)
            {
                return Err(SourceGraphContractError::SceneTexture);
            }
        }
        Ok(())
    }
}

#[cfg(test)]
mod tests {
    use super::*;
    fn declaration(slot: usize) -> SourceGraphSceneTexture {
        SourceGraphSceneTexture {
            slot,
            resource_key: "source.decal.buffer0".into(),
            format: SourceGraphSceneTextureFormat::Rgba8Unorm,
            dimensions: SourceGraphSceneTextureDimensions::Viewport,
            sampler: SourceGraphResolvedStorageSampler {
                filter_mode: 0,
                wrap: [1; 3],
                anisotropy: 1,
                mip_bias: 0.0,
                source_mipmaps_enabled: false,
            },
        }
    }
    #[test]
    fn scene_slots_are_explicit_dense_and_cannot_override_authored_images() {
        let source = b"@group(#{MATERIAL_BIND_GROUP}) @binding(101) var t0:texture_2d<f32>;\n@group(#{MATERIAL_BIND_GROUP}) @binding(102) var s0:sampler;";
        let hash = forge_core::bytes_sha256(source);
        let mut material: SourceGraphMaterialContract = serde_json::from_value(serde_json::json!({
            "schema_version":7,"source_program_sha256":hash,"source_material_sha256":hash,
            "forward":{"buffer_view":0,"sha256":hash},"prepass":{"buffer_view":0,"sha256":hash},
            "libraries":[],"textures":[],"scene_textures":[declaration(0)],
            "state":{"color_write_mask":15,"color_blend":[1,0],"alpha_blend":[1,0],
                "forward_cull":2,"prepass_cull":2,"depth_compare":4,"write_depth":true,
                "transparent":false,"alpha_test":false,"alpha_to_coverage":false,
                "render_queue":2000,"disabled_passes":[]},
            "requires_original_vertex_color":false,"normal_decode_modes":[null,null,null,null,null,null,null,null]
        })).unwrap();
        assert!(material.validate_integrity(&[source]).is_ok());
        material.scene_textures[0].slot = 1;
        assert!(material.validate_integrity(&[source]).is_err());
        material.scene_textures[0].slot = 0;
        material.scene_textures.push(declaration(0));
        assert!(material.validate_integrity(&[source]).is_err());
        material.scene_textures.pop();
        material.schema_version = 6;
        assert!(material.validate_integrity(&[source]).is_err());
        assert!(
            SourceGraphSceneTextureDimensions::Viewport
                .resolve([0, 640])
                .is_err()
        );
        assert!(
            SourceGraphSceneTextureDimensions::Fixed {
                width: u32::MAX,
                height: 1
            }
            .resolve([640, 640])
            .is_err()
        );
    }
}
