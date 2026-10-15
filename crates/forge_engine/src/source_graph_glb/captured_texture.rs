//! Convert the checked wire descriptor using the existing Bevy storage adapter.
//! No PNG fallback, mip regeneration, image registration or render claim.
use crate::source_graph::SourceGraphSamplerSettings;
use crate::source_graph::{
    SourceGraphImportedMip, SourceGraphImportedTexture, SourceGraphImportedTextureFormat,
    SourceGraphImportedTextureMetadata, prepare_source_graph_imported_texture,
};
use forge_assets::source_graph_bundle::{SourceGraphImportedStorageFormat, SourceGraphTexture};
#[allow(
    clippy::cast_possible_truncation,
    reason = "The validated source sampler projects mip bias to its original GPU Float32 ABI."
)]
pub fn prepare_captured_source_graph_texture(
    texture: &SourceGraphTexture,
    views: &[&[u8]],
) -> Result<SourceGraphImportedTexture, String> {
    texture
        .validate_integrity(views)
        .map_err(|e| format!("{e:?}"))?;
    let storage = texture
        .imported_storage
        .as_ref()
        .ok_or("Captured texture storage is required; PNG fallback is forbidden")?;
    let format = match storage.format {
        SourceGraphImportedStorageFormat::Bc1RgbaUnorm => {
            SourceGraphImportedTextureFormat::Bc1RgbaUnorm
        }
        SourceGraphImportedStorageFormat::Bc1RgbaUnormSrgb => {
            SourceGraphImportedTextureFormat::Bc1RgbaUnormSrgb
        }
        SourceGraphImportedStorageFormat::Bc3RgbaUnorm => {
            SourceGraphImportedTextureFormat::Bc3RgbaUnorm
        }
        SourceGraphImportedStorageFormat::Bc3RgbaUnormSrgb => {
            SourceGraphImportedTextureFormat::Bc3RgbaUnormSrgb
        }
        SourceGraphImportedStorageFormat::Bc4RUnorm => SourceGraphImportedTextureFormat::Bc4RUnorm,
        SourceGraphImportedStorageFormat::Rgb8Unorm => SourceGraphImportedTextureFormat::Rgb8Unorm,
        SourceGraphImportedStorageFormat::Rgba8Unorm => {
            SourceGraphImportedTextureFormat::Rgba8Unorm
        }
        SourceGraphImportedStorageFormat::Rgb10a2Unorm => {
            SourceGraphImportedTextureFormat::Rgb10a2Unorm
        }
    };
    let mips = storage
        .mips
        .iter()
        .map(|m| {
            Ok(SourceGraphImportedMip {
                width: m.width,
                height: m.height,
                offset: usize::try_from(m.offset).map_err(|_| "Mip offset overflow")?,
                bytes: usize::try_from(m.bytes).map_err(|_| "Mip byte count overflow")?,
                sha256: m.sha256,
            })
        })
        .collect::<Result<Vec<_>, String>>()?;
    let s = &storage.sampler;
    let metadata = SourceGraphImportedTextureMetadata {
        storage_sha256: storage.storage.sha256,
        width: storage.width,
        height: storage.height,
        format,
        mips,
        sampler: SourceGraphSamplerSettings {
            filter_mode: s.filter_mode,
            wrap_u: s.wrap[0],
            wrap_v: s.wrap[1],
            wrap_w: s.wrap[2],
            anisotropy: u16::from(s.anisotropy),
            mip_bias: s.mip_bias as f32,
            source_mipmaps_enabled: s.source_mipmaps_enabled,
        },
    };
    let bytes = views
        .get(storage.storage.buffer_view)
        .ok_or("Captured storage bufferView absent")?;
    prepare_source_graph_imported_texture(bytes, &metadata)
}
