//! Exact-source PNG base-level preparation, not imported-texture qualification.
use bevy::{
    asset::RenderAssetUsages,
    color::Srgba,
    image::{CompressedImageFormats, ImageSampler, ImageType},
    prelude::*,
    render::render_resource::TextureFormat,
};
use forge_core::{ContentHash, bytes_sha256};
#[derive(Copy, Clone, Debug)]
pub struct SourceGraphPngMetadata {
    pub source_sha256: ContentHash,
    pub width: u32,
    pub height: u32,
    pub bit_depth: u8,
    pub color_type: u8,
    pub is_srgb: bool,
}
pub struct SourceGraphPngBase {
    image: Image,
    pub requires_filterable_float32: bool,
}
impl SourceGraphPngBase {
    #[must_use]
    pub fn image(&self) -> &Image {
        &self.image
    }
    /// A base level for further qualified processing. It has no source mip chain,
    /// compression/resize/normal packing qualification or runtime GPU admission.
    #[must_use]
    pub fn into_unqualified_base_image(self) -> Image {
        self.image
    }
}
/// Preserve original eight-bit channels and transfer them through a native sRGB
/// format when declared. Sixteen-bit samples cannot use `Rgba16Unorm` as sRGB:
/// normalize without eight-bit quantization and linearize RGB before filtering.
/// Alpha stays linear. Float32 filtering must be admitted by the actual device.
pub fn decode_source_graph_png_base(
    bytes: &[u8],
    metadata: SourceGraphPngMetadata,
) -> Result<SourceGraphPngBase, String> {
    if bytes_sha256(bytes) != metadata.source_sha256 {
        return Err("Source PNG full SHA mismatch".into());
    }
    if bytes.len() < 33 || &bytes[..8] != b"\x89PNG\r\n\x1a\n" || &bytes[12..16] != b"IHDR" {
        return Err("Source PNG header is missing".into());
    }
    let width = u32::from_be_bytes(bytes[16..20].try_into().unwrap());
    let height = u32::from_be_bytes(bytes[20..24].try_into().unwrap());
    if width == 0
        || height == 0
        || width != metadata.width
        || height != metadata.height
        || bytes[24] != metadata.bit_depth
        || bytes[25] != metadata.color_type
    {
        return Err("Source PNG dimensions or channel metadata changed".into());
    }
    if !matches!(
        (metadata.bit_depth, metadata.color_type),
        (8, 0 | 6) | (16, 6)
    ) {
        return Err("Source PNG channel layout requires qualification".into());
    }
    let mut image = Image::from_buffer(
        bytes,
        ImageType::Extension("png"),
        CompressedImageFormats::NONE,
        metadata.is_srgb,
        ImageSampler::Default,
        RenderAssetUsages::default(),
    )
    .map_err(|e| format!("Source PNG decode failed: {e}"))?;
    if image.texture_descriptor.size.width != width
        || image.texture_descriptor.size.height != height
        || image.texture_descriptor.mip_level_count != 1
    {
        return Err("Native PNG base dimensions changed".into());
    }
    let pixel_count = (width as usize)
        .checked_mul(height as usize)
        .ok_or("Source PNG pixel count overflow")?;
    let required_float32 = metadata.bit_depth == 16;
    if required_float32 {
        if image.texture_descriptor.format != TextureFormat::Rgba16Unorm {
            return Err("Native source PNG lost sixteen-bit channel storage".into());
        }
        let original = image.data.take().ok_or("Native source PNG pixels absent")?;
        if original.len()
            != pixel_count
                .checked_mul(8)
                .ok_or("Source PNG byte count overflow")?
        {
            return Err("Native sixteen-bit PNG payload size changed".into());
        }
        let mut linear = Vec::with_capacity(
            pixel_count
                .checked_mul(16)
                .ok_or("Source float PNG byte count overflow")?,
        );
        for pixel in original.as_chunks::<8>().0 {
            for channel in 0..4 {
                let value = f32::from(u16::from_le_bytes([
                    pixel[2 * channel],
                    pixel[2 * channel + 1],
                ])) / 65535.0;
                let value = if metadata.is_srgb && channel < 3 {
                    Srgba::gamma_function(value)
                } else {
                    value
                };
                linear.extend_from_slice(&value.to_le_bytes());
            }
        }
        image.data = Some(linear);
        image.texture_descriptor.format = TextureFormat::Rgba32Float;
    } else {
        let expected = if metadata.is_srgb {
            TextureFormat::Rgba8UnormSrgb
        } else {
            TextureFormat::Rgba8Unorm
        };
        if image.texture_descriptor.format != expected
            || image.data.as_ref().map(Vec::len) != pixel_count.checked_mul(4)
        {
            return Err("Native eight-bit PNG channels or color format changed".into());
        }
    }
    Ok(SourceGraphPngBase {
        image,
        requires_filterable_float32: required_float32,
    })
}
#[cfg(all(test, feature = "private-source-fixtures"))]
mod tests {
    use super::*;
    use serde_json::Value;
    use std::collections::BTreeMap;
    fn originals() -> BTreeMap<String, Value> {
        let manifest: Value = serde_json::from_str(include_str!(concat!(
            env!("OPENRPG_PRIVATE_SOURCE_FIXTURES"),
            "/crates/forge_engine/source_graph_fixtures/PROGRAM-ARTIFACTS.v2.json"
        )))
        .unwrap();
        let mut rows = BTreeMap::new();
        for material in manifest["materials"].as_array().unwrap() {
            for binding in material["original_texture_bindings"].as_array().unwrap() {
                rows.entry(binding["original_png"].as_str().unwrap().to_owned())
                    .or_insert_with(|| binding.clone());
            }
        }
        assert_eq!(rows.len(), 104);
        rows
    }
    fn metadata(b: &Value) -> SourceGraphPngMetadata {
        SourceGraphPngMetadata {
            source_sha256: ContentHash::parse(b["original_png_sha256"].as_str().unwrap()).unwrap(),
            width: b["source_png_width"].as_u64().unwrap() as u32,
            height: b["source_png_height"].as_u64().unwrap() as u32,
            bit_depth: b["source_png_bit_depth"].as_u64().unwrap() as u8,
            color_type: b["source_png_color_type"].as_u64().unwrap() as u8,
            is_srgb: b["serialized_srgb_texture"].as_u64().unwrap() == 1,
        }
    }
    #[test]
    fn source_graph_png_all104_original_base_levels_keep_channels_and_explicit_formats() {
        let receipts: Value = serde_json::from_str(include_str!(concat!(
            env!("OPENRPG_PRIVATE_SOURCE_FIXTURES"),
            "/crates/forge_engine/source_graph_fixtures/PIXEL-RECEIPTS.v1.json"
        )))
        .unwrap();
        let receipts: BTreeMap<_, _> = receipts
            .as_array()
            .unwrap()
            .iter()
            .map(|r| (r["path"].as_str().unwrap().to_owned(), r.clone()))
            .collect();
        let mut eight = 0;
        let mut sixteen = 0;
        for (path, b) in originals() {
            let bytes = std::fs::read(&path).unwrap();
            let meta = metadata(&b);
            let prepared = decode_source_graph_png_base(&bytes, meta).unwrap();
            let image = prepared.image();
            assert_eq!(image.texture_descriptor.mip_level_count, 1);
            assert_eq!(prepared.requires_filterable_float32, meta.bit_depth == 16);
            if meta.bit_depth == 8 {
                eight += 1;
                assert_eq!(
                    bytes_sha256(image.data.as_ref().unwrap()).to_string(),
                    receipts[&path]["native_channel_sha256"].as_str().unwrap()
                );
            } else {
                sixteen += 1;
                assert_eq!(image.texture_descriptor.format, TextureFormat::Rgba32Float);
                assert_eq!(
                    image.data.as_ref().unwrap().len(),
                    meta.width as usize * meta.height as usize * 16
                );
            }
        }
        assert_eq!((eight, sixteen), (102, 2));
    }
    #[test]
    fn source_graph_png_two_original16bit_gradients_recover_precision_and_leave_alpha_linear() {
        let mut count = 0;
        for (path, b) in originals() {
            let meta = metadata(&b);
            if meta.bit_depth != 16 {
                continue;
            }
            assert!(meta.is_srgb);
            count += 1;
            let bytes = std::fs::read(path).unwrap();
            let original = Image::from_buffer(
                &bytes,
                ImageType::Extension("png"),
                CompressedImageFormats::NONE,
                true,
                ImageSampler::Default,
                RenderAssetUsages::default(),
            )
            .unwrap();
            let prepared = decode_source_graph_png_base(&bytes, meta).unwrap();
            let linear = prepared.image().data.as_ref().unwrap();
            let raw = original.data.as_ref().unwrap();
            let mut transferred = 0;
            for (base, pixel) in raw.chunks_exact(8).zip(linear.chunks_exact(16)) {
                for c in 0..4 {
                    let source = u16::from_le_bytes([base[2 * c], base[2 * c + 1]]);
                    let value = f32::from_le_bytes(pixel[4 * c..4 * c + 4].try_into().unwrap());
                    assert!(value.is_finite() && (0.0..=1.0).contains(&value));
                    if c == 3 {
                        assert_eq!((value * 65535.0).round() as u16, source);
                    } else {
                        let nonlinear = if value <= 0.0031308 {
                            12.92 * value
                        } else {
                            1.055 * value.powf(1.0 / 2.4) - 0.055
                        };
                        let recovered = (nonlinear * 65535.0).round() as i32;
                        assert!((recovered - source as i32).abs() <= 1);
                        if (value - source as f32 / 65535.0).abs() > 1e-4 {
                            transferred += 1;
                        }
                    }
                }
            }
            assert!(transferred > 0);
        }
        assert_eq!(count, 2);
    }
    #[test]
    fn source_graph_png_changed_sha_or_header_metadata_is_refused_before_image_registration() {
        let originals = originals();
        let (path, b) = originals.first_key_value().unwrap();
        let bytes = std::fs::read(path).unwrap();
        let mut meta = metadata(b);
        meta.width += 1;
        assert!(decode_source_graph_png_base(&bytes, meta).is_err());
        let mut meta = metadata(b);
        meta.source_sha256 = bytes_sha256(b"changed");
        assert!(decode_source_graph_png_base(&bytes, meta).is_err());
        let invalid = b"not a source PNG";
        let mut meta = metadata(b);
        meta.source_sha256 = bytes_sha256(invalid);
        assert!(decode_source_graph_png_base(invalid, meta).is_err());
    }
}
