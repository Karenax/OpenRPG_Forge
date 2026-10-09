//! Preserve the acquired pebble patch and density pixels in Bevy resources.
use super::pebble_patch::Patch;
use crate::tidewater_mesh::VillageMeshBytes;
use bevy::{
    asset::RenderAssetUsages,
    image::{ImageAddressMode, ImageFilterMode, ImageSampler, ImageSamplerDescriptor},
    prelude::*,
    render::render_resource::{Extent3d, TextureDimension, TextureFormat},
};

pub struct Prepared {
    pub mesh: Mesh,
    pub density: Image,
}

/// Construct resources before publishing any renderer assets or field instances.
/// Source slot seeds, vertex order, triangles and density pixels remain unchanged.
///
/// # Errors
/// Rejects malformed patch buffers through the same decoder used at acquisition.
/// The consuming scene must provide the acquired terrain and bounded cell buffer.
pub fn prepare(patch: &Patch) -> Result<Prepared, String> {
    let positions = bytes(&patch.positions);
    let normals = bytes(&patch.normals);
    let slots = bytes(&patch.slots);
    let source_indices: Vec<_> = patch.indices.iter().flat_map(|v| v.to_le_bytes()).collect();
    // Patch is public data: validate again before allocating graphics resources.
    Patch::decode(
        &positions,
        &normals,
        &slots,
        &source_indices,
        &patch.density_rgba8,
    )?;
    let indices: Vec<_> = patch
        .indices
        .iter()
        .flat_map(|v| u32::from(*v).to_le_bytes())
        .collect();
    // These shared mesh attributes are not read by the original pebble program.
    let uv = vec![0; patch.positions.len() * 8];
    let tint = bytes(&vec![[1.0_f32; 3]; patch.positions.len()]);
    let mesh = VillageMeshBytes {
        positions: &positions,
        normals: &normals,
        uv: &uv,
        tint: &tint,
        data: &slots,
        indices: &indices,
    }
    .into_mesh()?;
    let mut density = Image::new(
        Extent3d {
            width: 1024,
            height: 1024,
            depth_or_array_layers: 1,
        },
        TextureDimension::D2,
        patch.density_rgba8.clone(),
        TextureFormat::Rgba8Unorm,
        RenderAssetUsages::default(),
    );
    density.sampler = ImageSampler::Descriptor(ImageSamplerDescriptor {
        address_mode_u: ImageAddressMode::ClampToEdge,
        address_mode_v: ImageAddressMode::ClampToEdge,
        address_mode_w: ImageAddressMode::ClampToEdge,
        mag_filter: ImageFilterMode::Linear,
        min_filter: ImageFilterMode::Linear,
        mipmap_filter: ImageFilterMode::Nearest,
        anisotropy_clamp: 1,
        ..default()
    });
    Ok(Prepared { mesh, density })
}

fn bytes<const N: usize>(values: &[[f32; N]]) -> Vec<u8> {
    values
        .iter()
        .flatten()
        .flat_map(|value| value.to_le_bytes())
        .collect()
}

#[cfg(test)]
mod tests {
    use super::*;
    use bevy::mesh::{Indices, VertexAttributeValues};
    use forge_assets::tidewater_debris::{self, DebrisPackage};

    #[test]
    #[ignore = "Requires the acquired Debris package; CPU resource construction, no GPU draw"]
    fn acquired_patch_preserves_graphics_resources() {
        let root = std::path::PathBuf::from(std::env::var("FORGE_DEBRIS_GEOMETRY").unwrap());
        let package = DebrisPackage::load(
            &std::fs::read(root.join("package.json")).unwrap(),
            tidewater_debris::package_hash(),
            |name, _| std::fs::read(root.join(name)).map_err(|error| error.to_string()),
        )
        .unwrap();
        let mut patch = super::super::pebbles::prepare(&package, -1024.0)
            .unwrap()
            .patch;
        let prepared = prepare(&patch).unwrap();
        for (attribute, expected) in [
            (Mesh::ATTRIBUTE_POSITION, &patch.positions),
            (Mesh::ATTRIBUTE_NORMAL, &patch.normals),
        ] {
            let Some(VertexAttributeValues::Float32x3(actual)) = prepared.mesh.attribute(attribute)
            else {
                panic!("Source vector attribute layout differs");
            };
            assert_eq!(bytes(actual), bytes(expected));
        }
        let Some(VertexAttributeValues::Float32x4(actual)) =
            prepared.mesh.attribute(crate::tidewater_mesh::DATA)
        else {
            panic!("Source slot layout differs");
        };
        assert_eq!(bytes(actual), bytes(&patch.slots));
        let Some(Indices::U32(actual)) = prepared.mesh.indices() else {
            panic!("Source triangle layout differs");
        };
        assert_eq!(
            actual,
            &patch
                .indices
                .iter()
                .copied()
                .map(u32::from)
                .collect::<Vec<_>>()
        );
        assert_eq!(prepared.density.data.as_ref(), Some(&patch.density_rgba8));
        assert_eq!(
            prepared.density.texture_descriptor.format,
            TextureFormat::Rgba8Unorm
        );
        assert_eq!(prepared.density.texture_descriptor.mip_level_count, 1);
        let ImageSampler::Descriptor(sampler) = prepared.density.sampler else {
            panic!("Source density sampler absent");
        };
        assert_eq!(sampler.mag_filter, ImageFilterMode::Linear);
        assert_eq!(sampler.min_filter, ImageFilterMode::Linear);
        assert_eq!(sampler.address_mode_u, ImageAddressMode::ClampToEdge);
        assert_eq!(sampler.address_mode_v, ImageAddressMode::ClampToEdge);
        assert_eq!(sampler.anisotropy_clamp, 1);
        let seed = patch.slots[0][2];
        patch.slots[0][2] = f32::NAN;
        assert!(prepare(&patch).is_err());
        patch.slots[0][2] = seed;
        patch.density_rgba8.pop();
        assert!(prepare(&patch).is_err());
    }
}
