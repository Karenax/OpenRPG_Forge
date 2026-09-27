//! Scene-owned HDR environment data, usable by native and WebGPU adapters.
use bevy::{
    asset::RenderAssetUsages,
    image::{ImageFilterMode, ImageSampler, ImageSamplerDescriptor},
    prelude::*,
    render::render_resource::{
        Extent3d, TextureDimension, TextureFormat, TextureViewDescriptor, TextureViewDimension,
    },
};

/// Decode six RGBA16F faces and their mip chains in Bevy's layer-major order.
/// No filesystem access, generated replacement or implicit black environment.
pub fn cube_rgba16f(size: u32, mip_levels: u32, bytes: &[u8]) -> Result<Image, String> {
    if !size.is_power_of_two() || size > 1024 || mip_levels == 0 || mip_levels > size.ilog2() + 1 {
        return Err("Invalid environment cube dimensions".into());
    }
    let expected: usize = (0..mip_levels)
        .map(|level| ((size >> level).pow(2) * 6 * 8) as usize)
        .sum();
    if bytes.len() != expected {
        return Err("Invalid environment cube mip payload length".into());
    }
    // Binary16 infinities and NaNs have an all-ones exponent. No float decoder
    // dependency or numerical conversion is needed; preserve the acquired bits.
    if bytes
        .as_chunks::<2>()
        .0
        .iter()
        .any(|value| u16::from_le_bytes(*value) & 0x7c00 == 0x7c00)
    {
        return Err("Nonfinite environment radiance".into());
    }
    let mut image = Image::new_fill(
        Extent3d {
            width: size,
            height: size,
            depth_or_array_layers: 6,
        },
        TextureDimension::D2,
        &[0; 8],
        TextureFormat::Rgba16Float,
        RenderAssetUsages::RENDER_WORLD,
    );
    image.data = Some(bytes.to_vec());
    image.texture_descriptor.mip_level_count = mip_levels;
    image.texture_view_descriptor = Some(TextureViewDescriptor {
        dimension: Some(TextureViewDimension::Cube),
        ..default()
    });
    image.sampler = ImageSampler::Descriptor(ImageSamplerDescriptor {
        mag_filter: ImageFilterMode::Linear,
        min_filter: ImageFilterMode::Linear,
        mipmap_filter: ImageFilterMode::Linear,
        ..default()
    });
    Ok(image)
}
