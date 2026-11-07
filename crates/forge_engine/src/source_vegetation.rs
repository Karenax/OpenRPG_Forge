//! Shared source vegetation GPU layouts. Authored data and source qualification stay outside this adapter.
use bevy::{
    mesh::MeshVertexAttribute,
    prelude::*,
    render::render_resource::{ShaderType, VertexFormat},
};
pub mod canonical;
pub mod captured;
pub mod grass;
pub mod motion;
pub mod picking;
pub mod scene;
pub mod textures;
pub mod world;
#[derive(Clone, Debug, ShaderType)]
pub struct Record {
    pub i_pos: Vec4,
    pub i_dat: Vec4,
}
pub const VEG: MeshVertexAttribute =
    MeshVertexAttribute::new("SourceVegetation", 0x7457_0001, VertexFormat::Float32x4);
pub const MAT: MeshVertexAttribute =
    MeshVertexAttribute::new("SourceMaterial", 0x7457_0002, VertexFormat::Float32x4);
pub const LOBE: MeshVertexAttribute =
    MeshVertexAttribute::new("SourceLobe", 0x7457_0003, VertexFormat::Float32x4);

#[derive(Clone, Debug, ShaderType)]
pub struct Params {
    pub i_pos: Vec4,
    pub i_dat: Vec4,
    pub wind: Vec4,
    pub gust_lod: Vec4,
    pub sun_night: Vec4,
    pub lod_end_frame: Vec4,
}

#[derive(Clone, Copy, Debug, PartialEq, Eq, Hash)]
pub struct PlantKey(u8);
impl PlantKey {
    #[must_use]
    pub const fn new(kind: u8) -> Self {
        Self(kind)
    }
    #[must_use]
    pub const fn kind(self) -> u8 {
        self.0
    }
}

const SOURCE_DERIVATIVE_DIRECTIVE: &str = "diagnostic( off, derivative_uniformity );\n";

/// Root vertices can extend below the local trunk origin. The acquired bark
/// phase raises normalized height to a fractional power, which is undefined
/// for those negative coordinates. Extend only that power term to zero below
/// the origin; retain the source phase, noise and positive-height equations.
pub(crate) fn with_valid_bark_power_domain(source: &str) -> Result<String, String> {
    const ORIGINAL: &str = "pow( yn, 2.2 )";
    if source.matches("fn vegPalmBark(").count() != 2 || source.matches(ORIGINAL).count() != 2 {
        return Err("Source plant bark power domain seam changed".into());
    }
    Ok(source.replace(ORIGINAL, "pow( max( yn, 0.0 ), 2.2 )"))
}

#[must_use]
pub fn with_source_derivative_directive(source: String) -> String {
    if source.starts_with(SOURCE_DERIVATIVE_DIRECTIVE) {
        source
    } else {
        format!("{SOURCE_DERIVATIVE_DIRECTIVE}{source}")
    }
}

#[cfg(test)]
mod tests {
    use super::*;
    use bevy::render::storage::ShaderBuffer;

    #[test]
    fn bark_extension_is_finite_below_origin_and_exact_in_source_domain() {
        for y in [-0.2_f32, -0.004, 0.0, 0.004, 0.5, 1.0, 2.0] {
            let extended = y.max(0.0).powf(2.2);
            assert!(extended.is_finite());
            if y >= 0.0 {
                assert_eq!(extended.to_bits(), y.powf(2.2).to_bits());
            }
        }
        assert!((-0.004_f32).powf(2.2).is_nan());
        let branch = "fn vegPalmBark() { let phase=pow( yn, 2.2 ); }\n";
        let original = branch.repeat(2);
        let derived = with_valid_bark_power_domain(&original).unwrap();
        assert_eq!(
            derived.replace("pow( max( yn, 0.0 ), 2.2 )", "pow( yn, 2.2 )"),
            original
        );
        assert!(with_valid_bark_power_domain(branch).is_err());
        assert!(with_valid_bark_power_domain(&derived).is_err());
    }

    #[test]
    fn shared_storage_and_uniform_layouts_keep_the_source_wire_sizes() {
        let record = ShaderBuffer::from(vec![Record {
            i_pos: Vec4::ONE,
            i_dat: Vec4::ZERO,
        }]);
        let params = ShaderBuffer::from(Params {
            i_pos: Vec4::ZERO,
            i_dat: Vec4::ZERO,
            wind: Vec4::X,
            gust_lod: Vec4::ZERO,
            sun_night: Vec4::ZERO,
            lod_end_frame: Vec4::ZERO,
        });
        assert_eq!(record.data.as_ref().unwrap().len(), 32);
        assert_eq!(params.data.as_ref().unwrap().len(), 96);
    }
}
