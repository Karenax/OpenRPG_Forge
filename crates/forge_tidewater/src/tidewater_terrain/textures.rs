//! Source-format terrain textures, prepared before mutation of renderer assets.
use bevy::{
    asset::RenderAssetUsages,
    image::{ImageAddressMode, ImageFilterMode, ImageSampler, ImageSamplerDescriptor},
    prelude::*,
    render::render_resource::{Extent3d, TextureDimension, TextureFormat, TextureUsages},
};
use forge_tidewater_assets::tidewater_terrain::TerrainPackage;

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
        Self::new(
            package
                .file("terrain-splat.bin")
                .ok_or("Missing terrain splat")?,
            package
                .file("terrain-detail.bin")
                .ok_or("Missing terrain detail")?,
        )
    }

    /// The caller establishes provenance; this checks the complete source mip layout.
    pub fn new(splat: &[u8], detail: &[u8]) -> Result<Self, String> {
        // Validate both inputs before allocating the first image.
        validate(splat, 2048)?;
        validate(detail, 512)?;
        let splat = mipped(splat, 2048);
        let mut detail = mipped(detail, 512);
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
    validate(bytes, 2048)?;
    let mut image = mipped(bytes, 2048);
    image.texture_descriptor.usage |= TextureUsages::RENDER_ATTACHMENT;
    Ok(image)
}

fn validate(bytes: &[u8], side: u32) -> Result<(), String> {
    let expected: usize = (0..=side.ilog2())
        .map(|level| ((side >> level).pow(2) * 4) as usize)
        .sum();
    if bytes.len() != expected {
        return Err(format!(
            "Invalid {side} terrain mip chain: {} bytes, expected {expected}",
            bytes.len()
        ));
    }
    Ok(())
}

fn mipped(bytes: &[u8], side: u32) -> Image {
    let mut image = Image::new(
        Extent3d {
            width: side,
            height: side,
            depth_or_array_layers: 1,
        },
        TextureDimension::D2,
        bytes[..(side * side * 4) as usize].to_vec(),
        TextureFormat::Rgba8Unorm,
        RenderAssetUsages::default(),
    );
    image.data = Some(bytes.to_vec());
    image.texture_descriptor.mip_level_count = side.ilog2() + 1;
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
