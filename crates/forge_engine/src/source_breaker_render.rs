//! Shared transparent breaking-lip material and vertex layout.
//! Resource acquisition remains with the caller.
use bevy::{
    asset::RenderAssetUsages,
    image::{ImageAddressMode, ImageFilterMode, ImageSampler, ImageSamplerDescriptor},
    mesh::{Indices, MeshVertexAttribute, MeshVertexBufferLayoutRef},
    pbr::{MaterialPipeline, MaterialPipelineKey},
    prelude::*,
    render::{
        render_resource::{
            AsBindGroup, Extent3d, PrimitiveTopology, RenderPipelineDescriptor, TextureDimension,
            TextureFormat, VertexFormat,
        },
        storage::ShaderBuffer,
    },
    shader::ShaderRef,
};

pub const SHEET: MeshVertexAttribute =
    MeshVertexAttribute::new("BreakerSheet", 0x7457_0006, VertexFormat::Float32x4);
pub const LIP_SHADER: Handle<Shader> =
    bevy::asset::uuid_handle!("1932f5f6-32f7-4596-9b92-3b148404ea03");
pub const COMMON_LIP_SHADER: &str = include_str!("source_breaker_kernels/breaker-lip.wgsl");
const SOURCE_HEADER: &str = "// SPDX-License-Identifier: MIT\n// Adapted from Tidewater breaking lip, copyright 2026 DRG Software Solutions LLC.\n// Revision 1438b1abfcaee3267092b75573014f4d9b4a983c; see third_party/tidewater/LICENSE.\n";

/// Reuse the shared source only when an admitted shader matches exactly.
#[must_use]
pub fn select_common_shader(source: &str) -> &str {
    let shared = COMMON_LIP_SHADER
        .strip_prefix(SOURCE_HEADER)
        .expect("common lip shader attribution");
    if source == shared { shared } else { source }
}

/// Build the shared source ribbon mesh from a validated station count.
#[must_use]
pub fn lip_mesh(stations: u32) -> Option<Mesh> {
    lip_mesh_ranges(&[crate::source_breaker_geometry::LipRange {
        start: 0,
        count: stations,
    }])
}

/// Build one mesh from separate packed contours without connecting their ribbons.
#[must_use]
pub fn lip_mesh_ranges(ranges: &[crate::source_breaker_geometry::LipRange]) -> Option<Mesh> {
    let topology = crate::source_breaker_geometry::lip_topology_ranges(ranges)?;
    let mut mesh = Mesh::new(
        PrimitiveTopology::TriangleList,
        RenderAssetUsages::default(),
    );
    mesh.insert_attribute(
        Mesh::ATTRIBUTE_POSITION,
        vec![[0_f32; 3]; topology.sheet_ids.len()],
    );
    mesh.insert_attribute(SHEET, topology.sheet_ids);
    mesh.insert_indices(Indices::U32(topology.indices));
    Some(mesh)
}

/// Prepare the source lace sampler from already admitted RGBA8 mip bytes.
pub fn lace_image(side: u32, mips: u32, data: Vec<u8>) -> Result<Image, &'static str> {
    if side == 0 || mips == 0 || mips > u32::BITS - side.leading_zeros() {
        return Err("Invalid lace texture dimensions");
    }
    let mut expected = 0_u64;
    for mip in 0..mips {
        let mip_width = u64::from(side >> mip);
        let bytes = mip_width
            .checked_mul(mip_width)
            .and_then(|n| n.checked_mul(4))
            .ok_or("Lace texture size overflow")?;
        expected = expected
            .checked_add(bytes)
            .ok_or("Lace texture size overflow")?;
    }
    if usize::try_from(expected).ok() != Some(data.len()) {
        return Err("Lace texture mip size differs");
    }
    let mut image = Image::new_uninit(
        Extent3d {
            width: side,
            height: side,
            depth_or_array_layers: 1,
        },
        TextureDimension::D2,
        TextureFormat::Rgba8Unorm,
        RenderAssetUsages::default(),
    );
    image.data = Some(data);
    image.texture_descriptor.mip_level_count = mips;
    image.sampler = ImageSampler::Descriptor(ImageSamplerDescriptor {
        address_mode_u: ImageAddressMode::Repeat,
        address_mode_v: ImageAddressMode::Repeat,
        mag_filter: ImageFilterMode::Linear,
        min_filter: ImageFilterMode::Linear,
        mipmap_filter: ImageFilterMode::Linear,
        anisotropy_clamp: 8,
        ..default()
    });
    Ok(image)
}

#[derive(Asset, TypePath, AsBindGroup, Debug, Clone)]
pub struct LipMaterial {
    #[uniform(0)]
    pub frame: [UVec4; 45],
    #[storage(1, read_only)]
    #[dependency]
    pub crest: Handle<ShaderBuffer>,
    #[texture(2)]
    #[sampler(3)]
    #[dependency]
    pub lace: Handle<Image>,
    #[uniform(4)]
    pub atmosphere: [Vec4; 4],
    #[uniform(5)]
    pub sky: [UVec4; 2],
    #[texture(6)]
    #[sampler(8)]
    #[dependency]
    pub transmittance: Handle<Image>,
    #[texture(7)]
    #[dependency]
    pub sky_lut: Handle<Image>,
    #[uniform(9)]
    pub material: [UVec4; 3],
    #[uniform(10)]
    pub clouds: [Vec4; 5],
    #[texture(11)]
    #[sampler(12)]
    #[dependency]
    pub cloud_panorama: Option<Handle<Image>>,
    #[texture(13, filterable = false)]
    #[dependency]
    pub cloud_shadow: Option<Handle<Image>>,
}
impl Material for LipMaterial {
    fn vertex_shader() -> ShaderRef {
        LIP_SHADER.into()
    }
    fn fragment_shader() -> ShaderRef {
        LIP_SHADER.into()
    }
    fn alpha_mode(&self) -> AlphaMode {
        AlphaMode::Premultiplied
    }
    fn enable_prepass() -> bool {
        false
    }
    fn enable_shadows() -> bool {
        false
    }
    fn specialize(
        _: &MaterialPipeline,
        descriptor: &mut RenderPipelineDescriptor,
        layout: &MeshVertexBufferLayoutRef,
        _: MaterialPipelineKey<Self>,
    ) -> Result<(), bevy::material::specialize::SpecializedMeshPipelineError> {
        descriptor.vertex.buffers = vec![layout.0.get_layout(&[
            Mesh::ATTRIBUTE_POSITION.at_shader_location(0),
            SHEET.at_shader_location(1),
        ])?];
        descriptor.primitive.cull_mode = None;
        if let Some(depth) = &mut descriptor.depth_stencil {
            depth.depth_write_enabled = Some(false);
        }
        Ok(())
    }
}

#[cfg(test)]
mod tests {
    use super::*;
    use bevy::mesh::VertexAttributeValues;

    #[test]
    fn common_lip_shader_preserves_distinct_variants() {
        let common = COMMON_LIP_SHADER.strip_prefix(SOURCE_HEADER).unwrap();
        let copy = common.to_owned();
        let selected = select_common_shader(&copy);
        assert!(!std::ptr::eq(selected.as_ptr(), copy.as_ptr()));
        assert_eq!(selected, common);
        let variant = format!("{common}\n// variant");
        assert!(std::ptr::eq(
            select_common_shader(&variant).as_ptr(),
            variant.as_ptr()
        ));
        assert_eq!(
            forge_core::bytes_sha256(common.as_bytes()).to_string(),
            "428d7a40ace62bddfd0e434d71cc19743371f7b8a77d1c949892791e5c84360b"
        );
    }

    #[test]
    fn lip_mesh_preserves_source_ribbon_attributes_and_indices() {
        assert!(lip_mesh(1).is_none());
        let mesh = lip_mesh(3).unwrap();
        let expected = crate::source_breaker_geometry::lip_topology(3).unwrap();
        assert_eq!(mesh.count_vertices(), expected.sheet_ids.len());
        assert!(
            matches!(mesh.attribute(SHEET), Some(VertexAttributeValues::Float32x4(ids)) if ids == &expected.sheet_ids)
        );
        assert!(
            matches!(mesh.indices(), Some(Indices::U32(indices)) if indices == &expected.indices)
        );
        let ranges = [
            crate::source_breaker_geometry::LipRange { start: 0, count: 3 },
            crate::source_breaker_geometry::LipRange { start: 3, count: 3 },
        ];
        let combined = lip_mesh_ranges(&ranges).unwrap();
        let expected = crate::source_breaker_geometry::lip_topology_ranges(&ranges).unwrap();
        assert!(
            matches!(combined.attribute(SHEET), Some(VertexAttributeValues::Float32x4(ids)) if ids == &expected.sheet_ids)
        );
        assert!(
            matches!(combined.indices(), Some(Indices::U32(indices)) if indices == &expected.indices)
        );
        assert!(lip_mesh_ranges(&[]).is_none());
    }

    #[test]
    fn lace_mips_keep_bytes_and_source_sampler_and_reject_incomplete_data() {
        let data: Vec<_> = (0..84).collect();
        let image = lace_image(4, 3, data.clone()).unwrap();
        assert_eq!(image.data, Some(data));
        assert_eq!(image.texture_descriptor.mip_level_count, 3);
        assert_eq!(image.texture_descriptor.format, TextureFormat::Rgba8Unorm);
        assert!(
            matches!(image.sampler, ImageSampler::Descriptor(ref sampler)
            if sampler.address_mode_u == ImageAddressMode::Repeat
                && sampler.address_mode_v == ImageAddressMode::Repeat
                && sampler.mag_filter == ImageFilterMode::Linear
                && sampler.min_filter == ImageFilterMode::Linear
                && sampler.mipmap_filter == ImageFilterMode::Linear
                && sampler.anisotropy_clamp == 8)
        );
        assert!(lace_image(4, 3, vec![0; 83]).is_err());
        assert!(lace_image(4, 4, vec![0; 84]).is_err());
        assert!(lace_image(0, 1, Vec::new()).is_err());
    }
}
