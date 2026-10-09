//! Acquired mip-major RGBA8 views, ready for the existing renderer image adapter.
use super::ScannedPackage;
use forge_core::{ContentHash, bytes_sha256};
use serde::Deserialize;
use std::collections::BTreeMap;

#[derive(Clone, Copy, Debug, PartialEq, Eq)]
pub enum Format {
    SrgbRgba8,
    LinearRgba8,
}

/// Borrowed immutable pixels; conversion must preserve every acquired mip level.
pub struct Atlas<'a> {
    pub data: &'a [u8],
    pub format: Format,
    pub width: u32,
    pub height: u32,
    pub mip_count: u32,
    pub mip_offsets: [usize; 13],
    /// All acquired maps use clamp-to-edge and linear/trilinear filtering.
    pub anisotropy: u16,
}

#[derive(Deserialize)]
struct Sampler {
    address: String,
    mag: String,
    min: String,
    mipmap: String,
    anisotropy: u16,
}

#[derive(Deserialize)]
struct Texture {
    file: String,
    bytes: usize,
    sha256: ContentHash,
    width: u32,
    height: u32,
    mips: u32,
    format: String,
    sampler: Sampler,
    level_sha256: Vec<ContentHash>,
}

#[derive(Deserialize)]
struct Document {
    textures: BTreeMap<String, Texture>,
}

/// Return albedo, normal and ARM in source order, without decoding or rebuilding pixels.
///
/// # Errors
/// Refuses changed formats, sampler, mip sizes, hashes or absent acquired buffers.
pub fn prepare(package: &ScannedPackage) -> Result<[Atlas<'_>; 3], String> {
    let document: Document = serde_json::from_value(
        package
            .document("atlases")
            .ok_or("Scanned atlas document absent")?
            .clone(),
    )
    .map_err(|e| e.to_string())?;
    if document.textures.len() != 3 {
        return Err("Scanned atlas population differs".into());
    }
    let texture = |name| {
        document
            .textures
            .get(name)
            .ok_or("Scanned atlas map absent")
    };
    Ok([
        atlas(package, texture("albedo")?, Format::SrgbRgba8)?,
        atlas(package, texture("normal")?, Format::LinearRgba8)?,
        atlas(package, texture("arm")?, Format::LinearRgba8)?,
    ])
}

fn atlas<'a>(
    package: &'a ScannedPackage,
    texture: &Texture,
    format: Format,
) -> Result<Atlas<'a>, String> {
    let sampler = &texture.sampler;
    let expected = match format {
        Format::SrgbRgba8 => "rgba8unorm-srgb",
        Format::LinearRgba8 => "rgba8unorm",
    };
    if texture.width != 2048
        || texture.height != 2048
        || texture.mips != 12
        || texture.bytes != 22_369_620
        || texture.level_sha256.len() != 12
        || texture.format != expected
        || sampler.address != "clamp-to-edge"
        || sampler.mag != "linear"
        || sampler.min != "linear"
        || sampler.mipmap != "linear"
        || sampler.anisotropy != 8
    {
        return Err("Scanned atlas image contract differs".into());
    }
    let data = package
        .file(&format!("atlases/{}", texture.file))
        .ok_or("Scanned atlas pixels absent")?;
    if data.len() != texture.bytes || bytes_sha256(data) != texture.sha256 {
        return Err("Scanned atlas payload differs".into());
    }
    let mut mip_offsets = [0_usize; 13];
    for level in 0..12 {
        let side = 2048_usize >> level;
        let end = mip_offsets[level] + side * side * 4;
        let mip = data
            .get(mip_offsets[level]..end)
            .ok_or("Truncated scanned mip chain")?;
        if bytes_sha256(mip) != texture.level_sha256[level] {
            return Err("Scanned mip hash differs".into());
        }
        mip_offsets[level + 1] = end;
    }
    if mip_offsets[12] != data.len() {
        return Err("Scanned mip chain has trailing data".into());
    }
    Ok(Atlas {
        data,
        format,
        width: 2048,
        height: 2048,
        mip_count: 12,
        mip_offsets,
        anisotropy: 8,
    })
}

#[cfg(test)]
mod tests {
    use super::*;

    #[test]
    #[ignore = "Requires the acquired standalone scanned atlas payloads"]
    fn acquired_mips_keep_source_colour_formats_and_sampler() {
        let root = std::path::PathBuf::from(std::env::var("FORGE_DEBRIS_SCANNED_FIXTURE").unwrap());
        let raw = std::fs::read(root.join("package.json")).unwrap();
        let package = ScannedPackage::load(&raw, |name, _| {
            std::fs::read(root.join(name)).map_err(|e| e.to_string())
        })
        .unwrap();
        let atlases = prepare(&package).unwrap();
        assert_eq!(atlases[0].format, Format::SrgbRgba8);
        assert_eq!(atlases[1].format, Format::LinearRgba8);
        assert_eq!(atlases[2].format, Format::LinearRgba8);
        for atlas in &atlases {
            assert_eq!(atlas.mip_offsets[1], 2048 * 2048 * 4);
            assert_eq!(atlas.mip_offsets[12], 22_369_620);
            assert_eq!(atlas.mip_offsets[12] - atlas.mip_offsets[11], 4);
            assert_eq!(atlas.mip_count, 12);
            assert_eq!(atlas.anisotropy, 8);
        }
    }
}
