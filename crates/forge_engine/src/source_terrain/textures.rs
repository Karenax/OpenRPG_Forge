//! Source-format terrain textures, prepared before mutation of renderer assets.
use bevy::{
    asset::RenderAssetUsages,
    image::{ImageAddressMode, ImageFilterMode, ImageSampler, ImageSamplerDescriptor},
    prelude::*,
    render::render_resource::{Extent3d, TextureDimension, TextureFormat, TextureUsages},
};
use forge_assets::source_terrain_package::TerrainPackage;

/// Splat and detail textures shared across visible terrain patches.
/// Normal/AO is staged separately because authored relief regenerates its mips.
pub struct PreparedSurface {
    splat: Image,
    detail: Image,
    linear_detail: Image,
}

#[derive(Resource, Clone)]
pub struct ResidentSurface {
    pub splat: Handle<Image>,
    pub detail: Handle<Image>,
    pub linear_detail: Handle<Image>,
}

impl PreparedSurface {
    pub fn from_package(package: &TerrainPackage) -> Result<Self, String> {
        let dimensions = package.texture_dimensions();
        Self::with_dimensions(
            package
                .file("terrain-splat.bin")
                .ok_or("Missing terrain splat")?,
            dimensions.splat,
            package
                .file("terrain-detail.bin")
                .ok_or("Missing terrain detail")?,
            dimensions.detail,
        )
    }

    /// The caller establishes provenance; this checks the complete source mip layout.
    pub fn new(splat: &[u8], detail: &[u8]) -> Result<Self, String> {
        Self::with_dimensions(splat, [2048, 2048], detail, [512, 512])
    }

    /// Prepare checked, independently sized texture chains. The caller still
    /// admits their provenance and the material must use the same terrain domain.
    pub fn with_dimensions(
        splat: &[u8],
        splat_size: [u32; 2],
        detail: &[u8],
        detail_size: [u32; 2],
    ) -> Result<Self, String> {
        // Validate both inputs before allocating the first image.
        validate(splat, splat_size)?;
        validate(detail, detail_size)?;
        let splat = mipped(splat, splat_size);
        let mut detail = mipped(detail, detail_size);
        let sampler = ImageSamplerDescriptor {
            address_mode_u: ImageAddressMode::Repeat,
            address_mode_v: ImageAddressMode::Repeat,
            mag_filter: ImageFilterMode::Linear,
            min_filter: ImageFilterMode::Linear,
            mipmap_filter: ImageFilterMode::Linear,
            anisotropy_clamp: 4,
            ..default()
        };
        detail.sampler = ImageSampler::Descriptor(sampler.clone());
        let mut linear_detail = detail.clone();
        linear_detail.sampler = ImageSampler::Descriptor(ImageSamplerDescriptor {
            anisotropy_clamp: 1,
            ..sampler
        });
        Ok(Self {
            splat,
            detail,
            linear_detail,
        })
    }

    pub fn stage(self, images: &mut Assets<Image>) -> ResidentSurface {
        ResidentSurface {
            splat: images.add(self.splat),
            detail: images.add(self.detail),
            linear_detail: images.add(self.linear_detail),
        }
    }
}

/// Source normal/AO channels, optionally replaced by the authored terrain bake.
/// Readiness of regenerated mips belongs to the render graph, not this CPU image.
pub fn normal_image(bytes: &[u8]) -> Result<Image, String> {
    normal_image_with_dimensions(bytes, [2048, 2048])
}

/// Prepare a full normal/AO chain for the admitted terrain dimensions.
pub fn normal_image_with_dimensions(bytes: &[u8], size: [u32; 2]) -> Result<Image, String> {
    validate(bytes, size)?;
    let mut image = mipped(bytes, size);
    image.texture_descriptor.usage |= TextureUsages::RENDER_ATTACHMENT;
    Ok(image)
}

pub(super) fn validate(bytes: &[u8], size: [u32; 2]) -> Result<(), String> {
    if size.iter().any(|&axis| !(1..=4096).contains(&axis)) {
        return Err("Terrain texture dimensions must be between 1 and 4096".into());
    }
    let levels = size[0].max(size[1]).ilog2() + 1;
    let expected: usize = (0..levels)
        .map(|level| ((size[0] >> level).max(1) * (size[1] >> level).max(1) * 4) as usize)
        .sum();
    if bytes.len() != expected {
        return Err(format!(
            "Invalid {}x{} terrain mip chain: {} bytes, expected {expected}",
            size[0],
            size[1],
            bytes.len()
        ));
    }
    Ok(())
}

fn mipped(bytes: &[u8], size: [u32; 2]) -> Image {
    let mut image = Image::new(
        Extent3d {
            width: size[0],
            height: size[1],
            depth_or_array_layers: 1,
        },
        TextureDimension::D2,
        bytes[..(size[0] * size[1] * 4) as usize].to_vec(),
        TextureFormat::Rgba8Unorm,
        RenderAssetUsages::default(),
    );
    image.data = Some(bytes.to_vec());
    image.texture_descriptor.mip_level_count = size[0].max(size[1]).ilog2() + 1;
    image.sampler = ImageSampler::Descriptor(ImageSamplerDescriptor {
        address_mode_u: ImageAddressMode::ClampToEdge,
        address_mode_v: ImageAddressMode::ClampToEdge,
        mag_filter: ImageFilterMode::Linear,
        min_filter: ImageFilterMode::Linear,
        mipmap_filter: ImageFilterMode::Linear,
        ..default()
    });
    image
}

#[cfg(test)]
mod tests {
    use super::*;

    #[test]
    fn rectangular_non_power_of_two_chains_keep_every_level() {
        // 3x5, 1x2, 1x1; the detail uses an independent 2x2, 1x1 chain.
        let splat: Vec<_> = (0_u8..72).collect();
        let detail: Vec<_> = (0_u8..20).collect();
        let prepared = PreparedSurface::with_dimensions(&splat, [3, 5], &detail, [2, 2]).unwrap();
        assert_eq!(prepared.splat.texture_descriptor.size.width, 3);
        assert_eq!(prepared.splat.texture_descriptor.size.height, 5);
        assert_eq!(prepared.splat.texture_descriptor.mip_level_count, 3);
        assert_eq!(prepared.splat.data.as_deref(), Some(splat.as_slice()));
        assert_eq!(prepared.detail.texture_descriptor.mip_level_count, 2);
        assert_eq!(prepared.detail.data.as_deref(), Some(detail.as_slice()));
        let normal = normal_image_with_dimensions(&splat, [3, 5]).unwrap();
        assert_eq!(normal.data.as_deref(), Some(splat.as_slice()));
        assert!(
            normal
                .texture_descriptor
                .usage
                .contains(TextureUsages::RENDER_ATTACHMENT)
        );
    }

    #[test]
    fn invalid_dimensions_and_incomplete_chains_are_rejected() {
        assert!(normal_image_with_dimensions(&[], [0, 1]).is_err());
        assert!(normal_image_with_dimensions(&[], [4097, 1]).is_err());
        assert!(normal_image_with_dimensions(&[0; 71], [3, 5]).is_err());
        assert!(normal_image_with_dimensions(&[0; 73], [3, 5]).is_err());
        assert!(PreparedSurface::with_dimensions(&[0; 72], [3, 5], &[0; 19], [2, 2]).is_err());
    }
}
