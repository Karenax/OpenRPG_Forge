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
    /// Schema 2 requires explicit protocol and native source identity.
    /// Legacy schema 1 keeps its original capture and serialized shape.
    #[serde(default, skip_serializing_if = "Option::is_none")]
    pub native_capture: Option<NativeCaptureIdentity>,
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
#[derive(Clone, Copy, Debug, PartialEq, Eq, Serialize, Deserialize)]
pub enum NativeCaptureProtocol {
    #[serde(rename = "unity_isolated_readable_native_mips.v1")]
    IsolatedReadableMips,
    #[serde(rename = "unity_native_gpu_mip_readback.v1")]
    GpuMipReadback,
}
#[derive(Clone, Debug, PartialEq, Eq, Serialize, Deserialize)]
#[serde(deny_unknown_fields)]
pub struct NativeCaptureIdentity {
    pub protocol: NativeCaptureProtocol,
    pub source_guid: String,
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
        if !matches!(
            (self.schema_version, self.native_capture.is_some()),
            (1, false) | (2, true)
        ) {
            return Err(StorageError::Schema);
        }
        if self.width == 0
            || self.height == 0
            || self.width > 16384
            || self.height > 16384
            || self.mips.is_empty()
            || self.mips.len() > 32
            || self.mips.len() > (32 - self.width.max(self.height).leading_zeros()) as usize
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
        if block == 4 && (!self.width.is_multiple_of(4) || !self.height.is_multiple_of(4)) {
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
        if let Some(identity) = &self.native_capture {
            self.validate_native_capture(&capture, identity, data.len())?;
            return Ok(data);
        }
        self.validate_legacy_capture(&capture, data.len())?;
        Ok(data)
    }

    fn validate_legacy_capture(
        &self,
        capture: &serde_json::Value,
        bytes: usize,
    ) -> Result<(), StorageError> {
        let s = &self.sampler;
        let case = capture
            .get("cases")
            .and_then(|v| v.as_array())
            .and_then(|v| v.get(self.capture_case_index as usize))
            .ok_or(StorageError::Capture)?;
        if case["width"].as_u64() != Some(u64::from(self.width))
            || case["height"].as_u64() != Some(u64::from(self.height))
            || case["mip_count"].as_u64() != Some(self.mips.len() as u64)
            || case["raw_whole_bytes"].as_u64() != Some(bytes as u64)
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
        Ok(())
    }
    #[must_use]
    pub fn source_guid(&self) -> Option<&str> {
        self.native_capture.as_ref().map(|v| v.source_guid.as_str())
    }

    fn validate_native_capture(
        &self,
        capture: &serde_json::Value,
        identity: &NativeCaptureIdentity,
        bytes: usize,
    ) -> Result<(), StorageError> {
        use NativeCaptureProtocol::{GpuMipReadback, IsolatedReadableMips};
        let guid = &identity.source_guid;
        if guid.len() != 32
            || !guid
                .bytes()
                .all(|b| b.is_ascii_digit() || (b'a'..=b'f').contains(&b))
        {
            return Err(StorageError::Capture);
        }
        // Batch status is retained verbatim as diagnostic evidence. It may report
        // failures in other rows; storage integrity is determined by this row's
        // explicit protocol, identity, restoration/readback flags and mip chain.
        // This does not qualify capture execution or renderer equivalence.
        let row = capture["rows"]
            .as_array()
            .and_then(|rows| rows.get(self.capture_case_index as usize))
            .ok_or(StorageError::Capture)?;
        let srgb = matches!(
            self.format,
            ImportedStorageFormat::Bc1RgbaUnormSrgb | ImportedStorageFormat::Bc3RgbaUnormSrgb
        );
        let s = &self.sampler;
        if row["guid"].as_str() != Some(guid.as_str())
            || row["width"].as_u64() != Some(u64::from(self.width))
            || row["height"].as_u64() != Some(u64::from(self.height))
            || row["graphics_format"].as_str() != Some(self.format.native_name())
            || !(row["error"].is_null() || row["error"].as_str() == Some(""))
        {
            return Err(StorageError::Capture);
        }
        match identity.protocol {
            IsolatedReadableMips => {
                let before = row["settings_before"]
                    .as_str()
                    .ok_or(StorageError::Capture)?;
                let settings: serde_json::Value =
                    serde_json::from_str(before).map_err(|_| StorageError::Capture)?;
                if row["settings_restored"].as_str() != Some(before)
                    || row["settings_restored_equal"].as_bool() != Some(true)
                    || row["gpu_samples_bit_exact"].as_bool() != Some(true)
                    || row["private_readability_override"].as_bool() != Some(true)
                    || settings["m_IsReadable"].as_i64() != Some(0)
                    || settings["mipmaps"]["m_sRGBTexture"].as_i64() != Some(i64::from(srgb))
                    || settings["mipmaps"]["m_EnableMipMap"].as_i64()
                        != Some(i64::from(s.source_mipmaps_enabled))
                    || row["mip_count"].as_u64() != Some(self.mips.len() as u64)
                    || row["whole_bytes"].as_u64() != Some(bytes as u64)
                    || row["filter_mode"].as_u64() != Some(u64::from(s.filter_mode))
                    || row["aniso"].as_u64() != Some(u64::from(s.anisotropy))
                    || row["mip_bias"].as_f64() != Some(s.mip_bias)
                    || ["wrap_u", "wrap_v", "wrap_w"]
                        .iter()
                        .zip(s.wrap)
                        .any(|(key, v)| row[*key].as_u64() != Some(u64::from(v)))
                {
                    return Err(StorageError::Capture);
                }
            }
            GpuMipReadback => {
                let filters = ["Point", "Bilinear", "Trilinear"];
                let wraps = ["Repeat", "Clamp", "Mirror", "MirrorOnce"];
                if row["mipmap_count"].as_u64() != Some(self.mips.len() as u64)
                    || row["srgb"].as_bool() != Some(srgb)
                    || row["mipmap_enabled"].as_bool() != Some(s.source_mipmaps_enabled)
                    || row["filter_mode"].as_str() != Some(filters[s.filter_mode as usize])
                    || row["aniso_level"].as_u64() != Some(u64::from(s.anisotropy))
                    || row["mip_map_bias"].as_f64() != Some(s.mip_bias)
                    || ["wrap_u", "wrap_v", "wrap_w"]
                        .iter()
                        .zip(s.wrap)
                        .any(|(key, v)| row[*key].as_str() != Some(wraps[v as usize]))
                {
                    return Err(StorageError::Capture);
                }
            }
        }
        let mips = row["mips"].as_array().ok_or(StorageError::Capture)?;
        if mips.len() != self.mips.len() {
            return Err(StorageError::Capture);
        }
        for (level, (native, mip)) in mips.iter().zip(&self.mips).enumerate() {
            if native["level"].as_u64() != Some(level as u64)
                || native["width"].as_u64() != Some(u64::from(mip.width))
                || native["height"].as_u64() != Some(u64::from(mip.height))
                || native["bytes"].as_u64() != Some(mip.bytes)
                || (identity.protocol == GpuMipReadback
                    && (native["native_readback_ok"].as_bool() != Some(true)
                        || !(native["error"].is_null() || native["error"].as_str() == Some(""))))
            {
                return Err(StorageError::Capture);
            }
        }
        Ok(())
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
                sha256: bytes_sha256(
                    &data[usize::try_from(level).unwrap() * 8
                        ..usize::try_from(level).unwrap() * 8 + 8],
                ),
            })
            .collect();
        let capture=serde_json::to_vec(&serde_json::json!({"cases":[{"width":4,"height":4,"mip_count":3,"raw_whole_bytes":24,"graphics_format":"R_BC4_UNorm","expected_identity_matches":true,"error":"","filter_mode":1,"wrap_u":1,"wrap_v":1,"wrap_w":1,"anisotropy":1,"mip_bias":0.0,"importer_srgb":false,"imported_settings_json":serde_json::json!({"mipmaps":{"m_EnableMipMap":1}}).to_string(),"mips":[{"level":0,"width":4,"height":4,"bytes":8},{"level":1,"width":2,"height":2,"bytes":8},{"level":2,"width":1,"height":1,"bytes":8}]}]})).unwrap();
        let value = ImportedTextureStorage {
            schema_version: 1,
            native_capture: None,
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

#[cfg(test)]
mod native_protocol_tests {
    use super::*;
    fn fixture(protocol: NativeCaptureProtocol) -> (Vec<u8>, Vec<u8>, ImportedTextureStorage) {
        let (data, _, mut value) = super::tests::fixture();
        let guid = "0123456789abcdef0123456789abcdef";
        value.schema_version = 2;
        value.native_capture = Some(NativeCaptureIdentity {
            protocol,
            source_guid: guid.into(),
        });
        let settings =
            serde_json::json!({"m_IsReadable":0,"mipmaps":{"m_EnableMipMap":1,"m_sRGBTexture":0}})
                .to_string();
        let mips:Vec<_>=value.mips.iter().enumerate().map(|(i,m)|serde_json::json!({"level":i,"width":m.width,"height":m.height,"bytes":m.bytes,"native_readback_ok":true,"error":""})).collect();
        let mut row = serde_json::json!({"guid":guid,"width":4,"height":4,"graphics_format":"R_BC4_UNorm","error":"","mips":mips});
        let fields = match protocol {
            NativeCaptureProtocol::IsolatedReadableMips => {
                serde_json::json!({"settings_before":settings,"settings_restored":settings,"settings_restored_equal":true,"gpu_samples_bit_exact":true,"private_readability_override":true,"mip_count":3,"whole_bytes":24,"filter_mode":1,"wrap_u":1,"wrap_v":1,"wrap_w":1,"aniso":1,"mip_bias":0.0})
            }
            NativeCaptureProtocol::GpuMipReadback => {
                serde_json::json!({"mipmap_count":3,"srgb":false,"mipmap_enabled":true,"filter_mode":"Bilinear","wrap_u":"Clamp","wrap_v":"Clamp","wrap_w":"Clamp","aniso_level":1,"mip_map_bias":0.0})
            }
        };
        row.as_object_mut()
            .unwrap()
            .extend(fields.as_object().unwrap().clone());
        let capture =
            serde_json::to_vec(&serde_json::json!({"status":"other_rows_refused","rows":[row]}))
                .unwrap();
        value.capture.sha256 = bytes_sha256(&capture);
        (data, capture, value)
    }
    #[test]
    fn complete_native_rows_survive_batch_refusals_for_both_protocols() {
        for protocol in [
            NativeCaptureProtocol::IsolatedReadableMips,
            NativeCaptureProtocol::GpuMipReadback,
        ] {
            let (data, capture, value) = fixture(protocol);
            assert_eq!(value.validate(&[&data, &capture]).unwrap(), data);
            let wire = serde_json::to_vec(&value).unwrap();
            let reopened: ImportedTextureStorage = serde_json::from_slice(&wire).unwrap();
            assert_eq!(reopened, value);
        }
    }
    #[test]
    fn native_row_refusals_remain_refusals_after_outer_rehash() {
        for protocol in [
            NativeCaptureProtocol::IsolatedReadableMips,
            NativeCaptureProtocol::GpuMipReadback,
        ] {
            let (data, capture, mut value) = fixture(protocol);
            let mut raw: serde_json::Value = serde_json::from_slice(&capture).unwrap();
            match protocol {
                NativeCaptureProtocol::IsolatedReadableMips => {
                    raw["rows"][0]["settings_restored_equal"] = serde_json::json!(false);
                }
                NativeCaptureProtocol::GpuMipReadback => {
                    raw["rows"][0]["mips"][1]["native_readback_ok"] = serde_json::json!(false);
                }
            }
            let refused = serde_json::to_vec(&raw).unwrap();
            value.capture.sha256 = bytes_sha256(&refused);
            assert_eq!(
                value.validate(&[&data, &refused]),
                Err(StorageError::Capture)
            );
        }
    }
    #[test]
    fn legacy_native_protocol_mixing_and_missing_identity_refuse() {
        let (data, capture, mut value) = fixture(NativeCaptureProtocol::GpuMipReadback);
        value.schema_version = 1;
        assert_eq!(
            value.validate(&[&data, &capture]),
            Err(StorageError::Schema)
        );
        value.schema_version = 2;
        value.native_capture = None;
        assert_eq!(
            value.validate(&[&data, &capture]),
            Err(StorageError::Schema)
        );
    }
}
