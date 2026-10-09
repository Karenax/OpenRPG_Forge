//! Preserve acquired scanned mip chains when constructing Bevy-owned images.
use bevy::{
    asset::RenderAssetUsages,
    image::{ImageAddressMode, ImageFilterMode, ImageSampler, ImageSamplerDescriptor},
    prelude::*,
    render::render_resource::{Extent3d, TextureDimension, TextureFormat},
};
use forge_assets::tidewater_debris::scanned::{
    ScannedPackage,
    atlases::{self, Atlas, Format},
};

/// Prepare source albedo/normal/ARM images before inserting any renderer assets.
///
/// # Errors
/// Refuses invalid acquired atlas layouts; no replacement image is allocated.
pub fn prepare(package: &ScannedPackage) -> Result<[Image; 3], String> {
    let [albedo, normal, arm] = atlases::prepare(package)?;
    Ok([image(&albedo)?, image(&normal)?, image(&arm)?])
}

fn image(atlas: &Atlas<'_>) -> Result<Image, String> {
    let base = atlas.mip_offsets[1];
    let base_pixels = atlas
        .data
        .get(..base)
        .ok_or("Scanned mip-zero pixels absent")?;
    let format = match atlas.format {
        Format::SrgbRgba8 => TextureFormat::Rgba8UnormSrgb,
        Format::LinearRgba8 => TextureFormat::Rgba8Unorm,
    };
    let mut image = Image::new(
        Extent3d {
            width: atlas.width,
            height: atlas.height,
            depth_or_array_layers: 1,
        },
        TextureDimension::D2,
        base_pixels.to_vec(),
        format,
        RenderAssetUsages::default(),
    );
    // One 2D layer: the acquired contiguous chain has the same mip order in
    // Bevy's layer-major upload. Do not generate or filter replacement levels.
    image.data = Some(atlas.data.to_vec());
    image.texture_descriptor.mip_level_count = atlas.mip_count;
    image.sampler = ImageSampler::Descriptor(ImageSamplerDescriptor {
        address_mode_u: ImageAddressMode::ClampToEdge,
        address_mode_v: ImageAddressMode::ClampToEdge,
        address_mode_w: ImageAddressMode::ClampToEdge,
        mag_filter: ImageFilterMode::Linear,
        min_filter: ImageFilterMode::Linear,
        mipmap_filter: ImageFilterMode::Linear,
        anisotropy_clamp: atlas.anisotropy,
        ..default()
    });
    Ok(image)
}
