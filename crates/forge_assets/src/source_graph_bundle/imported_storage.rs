//! Candidate extension of the existing source-graph texture transport.
//! Integrity does not qualify source capture execution or rendered equivalence.
use crate::source_graph_bundle::SourceGraphBlob;
use forge_core::{ContentHash, bytes_sha256};
use serde::{Deserialize, Serialize};
#[derive(Clone, Copy, Debug, PartialEq, Eq, Serialize, Deserialize)]
#[serde(rename_all = "snake_case")]
pub enum ImportedStorageFormat {
    Bc1RgbaUnorm,
    Bc1RgbaUnormSrgb,
    Bc3RgbaUnorm,
    Bc3RgbaUnormSrgb,
    Bc4RUnorm,
    Rgb8Unorm,
    Rgba8Unorm,
}
impl ImportedStorageFormat {
    fn layout(self) -> (u32, u64) {
        match self {
            Self::Bc1RgbaUnorm | Self::Bc1RgbaUnormSrgb | Self::Bc4RUnorm => (4, 8),
            Self::Bc3RgbaUnorm | Self::Bc3RgbaUnormSrgb => (4, 16),
            Self::Rgb8Unorm => (1, 3),
            Self::Rgba8Unorm => (1, 4),
        }
    }
    fn native_name(self) -> &'static str {
        match self {
            Self::Bc1RgbaUnorm => "RGBA_DXT1_UNorm",
            Self::Bc1RgbaUnormSrgb => "RGBA_DXT1_SRGB",
            Self::Bc3RgbaUnorm => "RGBA_DXT5_UNorm",
            Self::Bc3RgbaUnormSrgb => "RGBA_DXT5_SRGB",
            Self::Bc4RUnorm => "R_BC4_UNorm",
            Self::Rgb8Unorm => "R8G8B8_UNorm",
            Self::Rgba8Unorm => "R8G8B8A8_UNorm",
        }
    }
}
#[derive(Clone, Debug, PartialEq, Serialize, Deserialize)]
#[serde(deny_unknown_fields)]
pub struct ImportedStorageMip {
    pub width: u32,
    pub height: u32,
    pub offset: u64,
    pub bytes: u64,
    pub sha256: ContentHash,
}
#[derive(Clone, Debug, PartialEq, Serialize, Deserialize)]
#[serde(deny_unknown_fields)]
pub struct ResolvedStorageSampler {
    pub filter_mode: u8,
    pub wrap: [u8; 3],
    pub anisotropy: u8,
    pub mip_bias: f64,
    pub source_mipmaps_enabled: bool,
}
#[derive(Clone, Debug, PartialEq, Serialize, Deserialize)]
#[serde(deny_unknown_fields)]
pub struct ImportedTextureStorage {
    pub schema_version: u32,
    pub storage: SourceGraphBlob,
    /// Entire original native capture JSON, not rewritten or stripped.
    pub capture: SourceGraphBlob,
    pub capture_case_index: u32,
    pub width: u32,
    pub height: u32,
    pub format: ImportedStorageFormat,
    pub mips: Vec<ImportedStorageMip>,
    pub sampler: ResolvedStorageSampler,
}
#[derive(Clone, Copy, Debug, PartialEq, Eq)]
pub enum StorageError {
    Schema,
    Bounds,
    Hash,
    Dimensions,
    MipLayout,
    Sampler,
    Capture,
}
fn read_blob<'a>(blob: &SourceGraphBlob, views: &'a [&[u8]]) -> Result<&'a [u8], StorageError> {
    let bytes = views
        .get(blob.buffer_view)
        .copied()
        .ok_or(StorageError::Bounds)?;
    if bytes_sha256(bytes) != blob.sha256 {
        return Err(StorageError::Hash);
    }
    Ok(bytes)
}
impl ImportedTextureStorage {
    pub fn validate<'a>(&self, views: &'a [&[u8]]) -> Result<&'a [u8], StorageError> {
        if self.schema_version != 1 {
            return Err(StorageError::Schema);
        }
        if self.width == 0
            || self.height == 0
            || self.width > 16384
            || self.height > 16384
            || self.mips.is_empty()
            || self.mips.len() > 32
            || (self.mips.len() as u32) > 32 - self.width.max(self.height).leading_zeros()
        {
            return Err(StorageError::Dimensions);
        }
        let s = &self.sampler;
        if s.filter_mode > 2
            || s.wrap.iter().any(|v| *v > 3)
            || !(1..=16).contains(&s.anisotropy)
            || !s.mip_bias.is_finite()
            || (!s.source_mipmaps_enabled && self.mips.len() != 1)
        {
            return Err(StorageError::Sampler);
        }
        let data = read_blob(&self.storage, views)?;
        if data.len() > 512 * 1024 * 1024 {
            return Err(StorageError::Bounds);
        }
        let (block, block_bytes) = self.format.layout();
        if block == 4 && (self.width % 4 != 0 || self.height % 4 != 0) {
            return Err(StorageError::Dimensions);
        }
        let mut offset = 0u64;
        for (level, mip) in self.mips.iter().enumerate() {
            let w = (self.width >> level).max(1);
            let h = (self.height >> level).max(1);
            let count = u64::from(w.div_ceil(block))
                .checked_mul(u64::from(h.div_ceil(block)))
                .and_then(|n| n.checked_mul(block_bytes))
                .ok_or(StorageError::Bounds)?;
            if mip.width != w || mip.height != h || mip.offset != offset || mip.bytes != count {
                return Err(StorageError::MipLayout);
            }
            let end = offset.checked_add(count).ok_or(StorageError::Bounds)?;
            let range = usize::try_from(offset).map_err(|_| StorageError::Bounds)?
                ..usize::try_from(end).map_err(|_| StorageError::Bounds)?;
            let slice = data.get(range).ok_or(StorageError::Bounds)?;
            if bytes_sha256(slice) != mip.sha256 {
                return Err(StorageError::Hash);
            }
            offset = end;
        }
        if offset != data.len() as u64 {
            return Err(StorageError::MipLayout);
        }
        let capture = read_blob(&self.capture, views)?;
        if capture.len() > 16 * 1024 * 1024 {
            return Err(StorageError::Bounds);
        }
        let capture: serde_json::Value =
            serde_json::from_slice(capture).map_err(|_| StorageError::Capture)?;
        let case = capture
            .get("cases")
            .and_then(|v| v.as_array())
            .and_then(|v| v.get(self.capture_case_index as usize))
            .ok_or(StorageError::Capture)?;
        if case["width"].as_u64() != Some(u64::from(self.width))
            || case["height"].as_u64() != Some(u64::from(self.height))
            || case["mip_count"].as_u64() != Some(self.mips.len() as u64)
            || case["raw_whole_bytes"].as_u64() != Some(data.len() as u64)
            || case["graphics_format"].as_str() != Some(self.format.native_name())
            || case["expected_identity_matches"].as_bool() != Some(true)
            || !(case["error"].is_null() || case["error"].as_str() == Some(""))
        {
            return Err(StorageError::Capture);
        }
        let settings: serde_json::Value = serde_json::from_str(
            case["imported_settings_json"]
                .as_str()
                .ok_or(StorageError::Capture)?,
        )
        .map_err(|_| StorageError::Capture)?;
        let srgb = matches!(
            self.format,
            ImportedStorageFormat::Bc1RgbaUnormSrgb | ImportedStorageFormat::Bc3RgbaUnormSrgb
        );
        if case["filter_mode"].as_u64() != Some(u64::from(s.filter_mode))
            || case["wrap_u"].as_u64() != Some(u64::from(s.wrap[0]))
            || case["wrap_v"].as_u64() != Some(u64::from(s.wrap[1]))
            || case["wrap_w"].as_u64() != Some(u64::from(s.wrap[2]))
            || case["anisotropy"].as_u64() != Some(u64::from(s.anisotropy))
            || case["mip_bias"].as_f64() != Some(s.mip_bias)
            || case["importer_srgb"].as_bool() != Some(srgb)
            || settings["mipmaps"]["m_EnableMipMap"].as_i64()
                != Some(i64::from(s.source_mipmaps_enabled))
        {
            return Err(StorageError::Capture);
        }
        let mips = case["mips"].as_array().ok_or(StorageError::Capture)?;
        if mips.len() != self.mips.len() {
            return Err(StorageError::Capture);
        }
        for (level, (native, mip)) in mips.iter().zip(&self.mips).enumerate() {
            if native["level"].as_u64() != Some(level as u64)
                || native["width"].as_u64() != Some(u64::from(mip.width))
                || native["height"].as_u64() != Some(u64::from(mip.height))
                || native["bytes"].as_u64() != Some(mip.bytes)
            {
                return Err(StorageError::Capture);
            }
        }
        Ok(data)
    }
}
#[cfg(test)]
pub(crate) mod tests {
    use super::*;
    pub(crate) fn fixture() -> (Vec<u8>, Vec<u8>, ImportedTextureStorage) {
        let data = vec![7u8; 24];
        let mips: Vec<_> = (0..3)
            .map(|level| ImportedStorageMip {
                width: 4 >> level,
                height: 4 >> level,
                offset: level * 8,
                bytes: 8,
                sha256: bytes_sha256(&data[level as usize * 8..level as usize * 8 + 8]),
            })
            .collect();
        let capture=serde_json::to_vec(&serde_json::json!({"cases":[{"width":4,"height":4,"mip_count":3,"raw_whole_bytes":24,"graphics_format":"R_BC4_UNorm","expected_identity_matches":true,"error":"","filter_mode":1,"wrap_u":1,"wrap_v":1,"wrap_w":1,"anisotropy":1,"mip_bias":0.0,"importer_srgb":false,"imported_settings_json":serde_json::json!({"mipmaps":{"m_EnableMipMap":1}}).to_string(),"mips":[{"level":0,"width":4,"height":4,"bytes":8},{"level":1,"width":2,"height":2,"bytes":8},{"level":2,"width":1,"height":1,"bytes":8}]}]})).unwrap();
        let value = ImportedTextureStorage {
            schema_version: 1,
            storage: SourceGraphBlob {
                buffer_view: 0,
                sha256: bytes_sha256(&data),
            },
            capture: SourceGraphBlob {
                buffer_view: 1,
                sha256: bytes_sha256(&capture),
            },
            capture_case_index: 0,
            width: 4,
            height: 4,
            format: ImportedStorageFormat::Bc4RUnorm,
            mips,
            sampler: ResolvedStorageSampler {
                filter_mode: 1,
                wrap: [1; 3],
                anisotropy: 1,
                mip_bias: 0.0,
                source_mipmaps_enabled: true,
            },
        };
        (data, capture, value)
    }
    #[test]
    fn complete_bc4_tail_roundtrip_keeps_exact_bytes() {
        let (d, c, v) = fixture();
        let wire = serde_json::to_vec(&v).unwrap();
        let v: ImportedTextureStorage = serde_json::from_slice(&wire).unwrap();
        assert_eq!(v.validate(&[&d, &c]).unwrap(), d);
    }
    #[test]
    fn altered_mip_refuses_even_with_new_full_hash() {
        let (mut d, c, mut v) = fixture();
        d[8] ^= 1;
        v.storage.sha256 = bytes_sha256(&d);
        assert_eq!(v.validate(&[&d, &c]), Err(StorageError::Hash));
    }
    #[test]
    fn overflow_offset_missing_view_and_trailing_bytes_refuse() {
        let (d, c, mut v) = fixture();
        v.mips[1].offset = u64::MAX;
        assert_eq!(v.validate(&[&d, &c]), Err(StorageError::MipLayout));
        let (d, c, mut v) = fixture();
        v.storage.buffer_view = 9;
        assert_eq!(v.validate(&[&d, &c]), Err(StorageError::Bounds));
        let (mut d, c, mut v) = fixture();
        d.push(0);
        v.storage.sha256 = bytes_sha256(&d);
        assert_eq!(v.validate(&[&d, &c]), Err(StorageError::MipLayout));
    }
    #[test]
    fn duplicate_one_pixel_mips_and_infinite_bias_refuse() {
        let (d, c, mut v) = fixture();
        v.mips.push(v.mips[2].clone());
        assert_eq!(v.validate(&[&d, &c]), Err(StorageError::Dimensions));
        let (d, c, mut v) = fixture();
        v.sampler.mip_bias = f64::INFINITY;
        assert_eq!(v.validate(&[&d, &c]), Err(StorageError::Sampler));
    }
    #[test]
    fn wrong_native_case_and_format_refuse() {
        let (d, c, mut v) = fixture();
        v.capture_case_index = 1;
        assert_eq!(v.validate(&[&d, &c]), Err(StorageError::Capture));
        let (d, c, mut v) = fixture();
        v.format = ImportedStorageFormat::Bc3RgbaUnorm;
        assert_eq!(v.validate(&[&d, &c]), Err(StorageError::MipLayout));
    }
    #[test]
    fn unknown_encoding_and_fields_refuse() {
        let (_, _, v) = fixture();
        let mut raw = serde_json::to_value(v).unwrap();
        raw["format"] = "automatic".into();
        assert!(serde_json::from_value::<ImportedTextureStorage>(raw.clone()).is_err());
        raw["format"] = "bc4_r_unorm".into();
        raw["auto_repair"] = true.into();
        assert!(serde_json::from_value::<ImportedTextureStorage>(raw).is_err());
    }
    #[test]
    fn valid_but_changed_sampler_and_source_mipmap_flag_refuse() {
        let (d, c, mut v) = fixture();
        v.sampler.wrap[0] = 0;
        assert_eq!(v.validate(&[&d, &c]), Err(StorageError::Capture));
        let (d, c, mut v) = fixture();
        v.sampler.filter_mode = 2;
        assert_eq!(v.validate(&[&d, &c]), Err(StorageError::Capture));
        let (d, c, mut v) = fixture();
        v.sampler.mip_bias = 0.5;
        assert_eq!(v.validate(&[&d, &c]), Err(StorageError::Capture));
    }
}
