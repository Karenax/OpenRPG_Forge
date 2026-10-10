//! Preserve measured importer storage. CPU preparation is not GPU/render admission.
use crate::source_graph::{SourceGraphSamplerSettings, source_graph_image_sampler};
use bevy::{asset::RenderAssetUsages, image::ImageSampler, prelude::Image};
use forge_core::{ContentHash, bytes_sha256};
use wgpu::{Extent3d, TextureDimension, TextureFormat};

#[derive(Clone, Copy, Debug)]
pub enum SourceGraphImportedTextureFormat {
    Bc1RgbaUnorm,
    Bc1RgbaUnormSrgb,
    Bc3RgbaUnorm,
    Bc3RgbaUnormSrgb,
    Bc4RUnorm,
    Rgb8Unorm,
    Rgba8Unorm,
}

impl SourceGraphImportedTextureFormat {
    fn native(self) -> (TextureFormat, usize, bool) {
        match self {
            Self::Bc1RgbaUnorm => (TextureFormat::Bc1RgbaUnorm, 8, true),
            Self::Bc1RgbaUnormSrgb => (TextureFormat::Bc1RgbaUnormSrgb, 8, true),
            Self::Bc3RgbaUnorm => (TextureFormat::Bc3RgbaUnorm, 16, true),
            Self::Bc3RgbaUnormSrgb => (TextureFormat::Bc3RgbaUnormSrgb, 16, true),
            Self::Bc4RUnorm => (TextureFormat::Bc4RUnorm, 8, true),
            Self::Rgb8Unorm => (TextureFormat::Rgba8Unorm, 3, false),
            Self::Rgba8Unorm => (TextureFormat::Rgba8Unorm, 4, false),
        }
    }
}

#[derive(Clone, Debug)]
pub struct SourceGraphImportedMip {
    pub width: u32,
    pub height: u32,
    pub offset: usize,
    pub bytes: usize,
    pub sha256: ContentHash,
}

pub struct SourceGraphImportedTextureMetadata {
    pub storage_sha256: ContentHash,
    pub width: u32,
    pub height: u32,
    pub format: SourceGraphImportedTextureFormat,
    /// Ordered complete mip chain, copied from an independently verified capture.
    pub mips: Vec<SourceGraphImportedMip>,
    pub sampler: SourceGraphSamplerSettings,
}

pub struct SourceGraphImportedTexture {
    image: Image,
    /// The host must enable `TEXTURE_COMPRESSION_BC` on its actual device.
    pub requires_texture_compression_bc: bool,
}

impl SourceGraphImportedTexture {
    pub(crate) fn set_scene_sampler(&mut self, sampler: bevy::image::ImageSamplerDescriptor) {
        self.image.sampler = ImageSampler::Descriptor(sampler);
    }
    #[must_use]
    pub fn image(&self) -> &Image {
        &self.image
    }

    /// Exact CPU storage, not proof of device support, orientation, normal
    /// decoding, material parity or placement. The host must qualify those.
    #[must_use]
    pub fn into_unqualified_image(self) -> Image {
        self.image
    }
}

pub fn prepare_source_graph_imported_texture(
    bytes: &[u8],
    metadata: &SourceGraphImportedTextureMetadata,
) -> Result<SourceGraphImportedTexture, String> {
    if bytes.len() > 512 * 1024 * 1024
        || metadata.width == 0
        || metadata.height == 0
        || metadata.width > 16384
        || metadata.height > 16384
        || metadata.mips.is_empty()
        || metadata.mips.len() > 32
    {
        return Err("Imported texture size/mip allocation limit".into());
    }
    if bytes_sha256(bytes) != metadata.storage_sha256 {
        return Err("Imported texture full storage SHA mismatch".into());
    }
    let mip_count = u32::try_from(metadata.mips.len()).map_err(|_| "Mip count overflow")?;
    let sampler =
        source_graph_image_sampler(metadata.sampler, metadata.width, metadata.height, mip_count)?;
    let (format, block_bytes, compressed) = metadata.format.native();
    let block = if compressed { 4 } else { 1 };
    // WebGPU base compressed extents must be complete blocks. Smaller mip
    // levels still retain one block; no padding, decoding or re-encoding here.
    if compressed && (!metadata.width.is_multiple_of(4) || !metadata.height.is_multiple_of(4)) {
        return Err("Compressed base extent requires separate qualification".into());
    }
    let mut offset = 0usize;
    for (level, mip) in metadata.mips.iter().enumerate() {
        let width = (metadata.width >> level).max(1);
        let height = (metadata.height >> level).max(1);
        let expected = (width.div_ceil(block) as usize)
            .checked_mul(height.div_ceil(block) as usize)
            .and_then(|n| n.checked_mul(block_bytes))
            .ok_or("Imported mip byte count overflow")?;
        if mip.width != width
            || mip.height != height
            || mip.offset != offset
            || mip.bytes != expected
        {
            return Err("Imported mip dimensions, order or block storage differs".into());
        }
        let end = offset
            .checked_add(expected)
            .ok_or("Imported mip offset overflow")?;
        let data = bytes
            .get(offset..end)
            .ok_or("Truncated imported mip storage")?;
        if bytes_sha256(data) != mip.sha256 {
            return Err("Imported mip SHA mismatch".into());
        }
        offset = end;
    }
    if offset != bytes.len() {
        return Err("Trailing imported texture storage".into());
    }
    let mut image = Image::new_uninit(
        Extent3d {
            width: metadata.width,
            height: metadata.height,
            depth_or_array_layers: 1,
        },
        TextureDimension::D2,
        format,
        RenderAssetUsages::MAIN_WORLD | RenderAssetUsages::RENDER_WORLD,
    );
    image.texture_descriptor.mip_level_count = mip_count;
    image.data = Some(
        if matches!(metadata.format, SourceGraphImportedTextureFormat::Rgb8Unorm) {
            // Source RGB24 bytes/mip hashes remain native. WebGPU uses RGBA8;
            // opaque alpha is independently measured on the original RGB images.
            let converted_len = bytes
                .len()
                .checked_div(3)
                .and_then(|n| n.checked_mul(4))
                .ok_or("RGB8 expansion overflow")?;
            if converted_len > 512 * 1024 * 1024 {
                return Err("Expanded RGB8 image allocation limit".into());
            }
            let mut rgba = Vec::with_capacity(converted_len);
            for pixel in bytes.as_chunks::<3>().0 {
                rgba.extend_from_slice(&[pixel[0], pixel[1], pixel[2], 255]);
            }
            rgba
        } else {
            bytes.to_vec()
        },
    );
    image.sampler = ImageSampler::Descriptor(sampler);
    Ok(SourceGraphImportedTexture {
        image,
        requires_texture_compression_bc: compressed,
    })
}

#[cfg(test)]
mod tests {
    use super::*;
    fn fixture() -> (Vec<u8>, SourceGraphImportedTextureMetadata) {
        let data: Vec<u8> = (0..24).collect();
        let mips = (0..3)
            .map(|level| SourceGraphImportedMip {
                width: 4 >> level,
                height: 4 >> level,
                offset: level * 8,
                bytes: 8,
                sha256: bytes_sha256(&data[level * 8..level * 8 + 8]),
            })
            .collect();
        let metadata = SourceGraphImportedTextureMetadata {
            storage_sha256: bytes_sha256(&data),
            width: 4,
            height: 4,
            format: SourceGraphImportedTextureFormat::Bc1RgbaUnormSrgb,
            mips,
            sampler: SourceGraphSamplerSettings {
                filter_mode: 1,
                wrap_u: 0,
                wrap_v: 1,
                wrap_w: 0,
                anisotropy: 1,
                mip_bias: 0.0,
                source_mipmaps_enabled: true,
            },
        };
        (data, metadata)
    }
    #[test]
    fn compressed_storage_keeps_every_block_mip_and_srgb_format() {
        let (data, metadata) = fixture();
        let prepared = prepare_source_graph_imported_texture(&data, &metadata).unwrap();
        assert!(prepared.requires_texture_compression_bc);
        assert_eq!(prepared.image().data.as_ref(), Some(&data));
        assert_eq!(
            prepared.image().texture_descriptor.format,
            TextureFormat::Bc1RgbaUnormSrgb
        );
        assert_eq!(prepared.image().texture_descriptor.mip_level_count, 3);
    }
    #[test]
    fn altered_blocks_missing_mips_offsets_and_trailing_bytes_refuse() {
        let (mut data, mut metadata) = fixture();
        data[0] ^= 1;
        assert!(prepare_source_graph_imported_texture(&data, &metadata).is_err());
        metadata.storage_sha256 = bytes_sha256(&data);
        assert!(prepare_source_graph_imported_texture(&data, &metadata).is_err());
        let (data, mut metadata) = fixture();
        metadata.mips.pop();
        assert!(prepare_source_graph_imported_texture(&data, &metadata).is_err());
        let (data, mut metadata) = fixture();
        metadata.mips[1].offset = 0;
        assert!(prepare_source_graph_imported_texture(&data, &metadata).is_err());
        let (mut data, mut metadata) = fixture();
        data.push(0);
        metadata.storage_sha256 = bytes_sha256(&data);
        assert!(prepare_source_graph_imported_texture(&data, &metadata).is_err());
    }

    #[test]
    fn bc4_storage_retains_unorm_format_and_sub_block_mip_tail() {
        let (data, mut metadata) = fixture();
        metadata.format = SourceGraphImportedTextureFormat::Bc4RUnorm;
        let prepared = prepare_source_graph_imported_texture(&data, &metadata).unwrap();
        assert!(prepared.requires_texture_compression_bc);
        assert_eq!(prepared.image().data.as_ref(), Some(&data));
        assert_eq!(
            prepared.image().texture_descriptor.format,
            TextureFormat::Bc4RUnorm
        );
        assert_eq!(prepared.image().texture_descriptor.mip_level_count, 3);
        assert!(matches!(
            &prepared.image().sampler,
            ImageSampler::Descriptor(_)
        ));
    }

    #[test]
    fn bc4_rejects_truncated_tail_altered_mip_and_wrong_block_format() {
        let (mut data, mut metadata) = fixture();
        metadata.format = SourceGraphImportedTextureFormat::Bc4RUnorm;
        data.pop();
        metadata.storage_sha256 = bytes_sha256(&data);
        assert!(prepare_source_graph_imported_texture(&data, &metadata).is_err());
        let (mut data, mut metadata) = fixture();
        metadata.format = SourceGraphImportedTextureFormat::Bc4RUnorm;
        data[8] ^= 1;
        metadata.storage_sha256 = bytes_sha256(&data);
        assert!(prepare_source_graph_imported_texture(&data, &metadata).is_err());
        let (data, mut metadata) = fixture();
        metadata.format = SourceGraphImportedTextureFormat::Bc3RgbaUnorm;
        assert!(prepare_source_graph_imported_texture(&data, &metadata).is_err());
    }
}
