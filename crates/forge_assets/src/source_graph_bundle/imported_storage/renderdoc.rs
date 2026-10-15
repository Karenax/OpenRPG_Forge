//! Original frame exports retain their capture JSON and source binding proof.
//! This admits bytes and identities, not capture execution or visual parity.
use super::{
    ImportedStorageFormat, ImportedTextureStorage, NativeCaptureProtocol, StorageError, read_blob,
};
use forge_core::ContentHash;
use serde_json::Value;

impl ImportedTextureStorage {
    fn binding_document(&self, views: &[&[u8]]) -> Result<Value, StorageError> {
        let blob = self.source_binding.as_ref().ok_or(StorageError::Capture)?;
        let bytes = read_blob(blob, views)?;
        if bytes.len() > 16 * 1024 * 1024 {
            return Err(StorageError::Bounds);
        }
        serde_json::from_slice(bytes).map_err(|_| StorageError::Capture)
    }

    fn bound_row<'a>(&self, document: &'a Value) -> Result<&'a Value, StorageError> {
        let guid = self.source_guid().ok_or(StorageError::Capture)?;
        let rows = document["textures"]
            .as_array()
            .ok_or(StorageError::Capture)?;
        let mut rows = rows
            .iter()
            .filter(|row| row["source_guid"].as_str() == Some(guid));
        let row = rows.next().ok_or(StorageError::Capture)?;
        if rows.next().is_some() {
            return Err(StorageError::Capture);
        }
        Ok(row)
    }

    pub(crate) fn validate_bound_source(
        &self,
        views: &[&[u8]],
        png: ContentHash,
        meta: ContentHash,
    ) -> Result<(), StorageError> {
        if self.native_capture.as_ref().is_none_or(|identity| {
            identity.protocol != NativeCaptureProtocol::RenderDocOriginalMips
        }) {
            return Ok(());
        }
        let document = self.binding_document(views)?;
        let row = self.bound_row(&document)?;
        let hash = |name: &str| {
            row[name]
                .as_str()
                .and_then(|s| s.parse::<ContentHash>().ok())
                .ok_or(StorageError::Capture)
        };
        if hash("source_sha256")? != png || hash("source_meta_sha256")? != meta {
            return Err(StorageError::Capture);
        }
        Ok(())
    }

    #[expect(
        clippy::too_many_lines,
        reason = "Original capture, binding, sampler and every mip are admitted atomically"
    )]
    pub(super) fn validate_renderdoc_capture(
        &self,
        capture: &Value,
        views: &[&[u8]],
    ) -> Result<(), StorageError> {
        let document = self.binding_document(views)?;
        let source = self.bound_row(&document)?;
        let pins = document["pins"].as_object().ok_or(StorageError::Capture)?;
        let capture_hash = self.capture.sha256.to_string();
        let mut capture_paths = pins.iter().filter(|(path, pin)| {
            path.rsplit_once('.')
                .is_some_and(|(_, extension)| extension.eq_ignore_ascii_case("json"))
                && pin.as_str() == Some(capture_hash.as_str())
        });
        let (capture_path, _) = capture_paths.next().ok_or(StorageError::Capture)?;
        if capture_paths.next().is_some() {
            return Err(StorageError::Capture);
        }
        let slash = capture_path
            .rfind(['/', '\\'])
            .ok_or(StorageError::Capture)?;
        let parent = &capture_path[..=slash];
        let original_capture = capture["capture"].as_str().ok_or(StorageError::Capture)?;
        if document["schema_version"].as_u64() != Some(1)
            || capture["schema_version"].as_u64() != Some(1)
            || pins
                .get(original_capture)
                .and_then(Value::as_str)
                .and_then(|s| s.parse::<ContentHash>().ok())
                .is_none()
            || capture["source_shaders_modified"].as_bool() != Some(false)
        {
            return Err(StorageError::Capture);
        }
        let row = capture["textures"]
            .as_array()
            .and_then(|rows| rows.get(self.capture_case_index as usize))
            .ok_or(StorageError::Capture)?;
        let format = match self.format {
            ImportedStorageFormat::Bc1RgbaUnorm => "BC1_UNORM",
            ImportedStorageFormat::Bc1RgbaUnormSrgb => "BC1_SRGB",
            ImportedStorageFormat::Bc3RgbaUnorm => "BC3_UNORM",
            ImportedStorageFormat::Bc3RgbaUnormSrgb => "BC3_SRGB",
            _ => return Err(StorageError::Capture),
        };
        if row["exported"].as_bool() != Some(true)
            || row["view_srgb"].as_bool()
                != Some(matches!(
                    self.format,
                    ImportedStorageFormat::Bc1RgbaUnormSrgb
                        | ImportedStorageFormat::Bc3RgbaUnormSrgb
                ))
            || row["depth"].as_u64() != Some(1)
            || row["array_size"].as_u64() != Some(1)
            || row["samples"].as_u64() != Some(1)
            || row["resource_name"] != source["native_resource_name"]
            || row["view_format"].as_str() != Some(format)
            || source["view_format"].as_str() != Some(format)
            || row["width"].as_u64() != Some(u64::from(self.width))
            || row["height"].as_u64() != Some(u64::from(self.height))
            || source["width"] != row["width"]
            || source["height"] != row["height"]
            || row["mip_count"].as_u64() != Some(self.mips.len() as u64)
            || source["storage"]["sha256"].as_str()
                != Some(self.storage.sha256.to_string().as_str())
            || row["reflection_index"] != source["native_fragment_texture_slot"]
        {
            return Err(StorageError::Capture);
        }
        // Effective frame sampling (including forced anisotropy) stays in the
        // capture. The importer sampler is separately checked by its metadata.
        let sampler = capture["samplers"]
            .as_array()
            .and_then(|rows| {
                rows.iter()
                    .find(|s| s["reflection_index"] == row["reflection_index"])
            })
            .ok_or(StorageError::Capture)?;
        if sampler != &source["native_sampler"] {
            return Err(StorageError::Sampler);
        }
        let mips = row["mips"].as_array().ok_or(StorageError::Capture)?;
        let source_mips = source["mips"].as_array().ok_or(StorageError::Capture)?;
        if mips.len() != self.mips.len() || source_mips.len() != self.mips.len() {
            return Err(StorageError::Capture);
        }
        for (level, ((native, binding), mip)) in
            mips.iter().zip(source_mips).zip(&self.mips).enumerate()
        {
            let file = native["file"].as_str().ok_or(StorageError::Capture)?;
            if file.contains(['/', '\\', ':']) || file.is_empty() || file == "." || file == ".." {
                return Err(StorageError::Capture);
            }
            let hash = mip.sha256.to_string();
            if native["level"].as_u64() != Some(level as u64)
                || native["width"].as_u64() != Some(u64::from(mip.width))
                || native["height"].as_u64() != Some(u64::from(mip.height))
                || native["bytes"].as_u64() != Some(mip.bytes)
                || binding["width"] != native["width"]
                || binding["height"] != native["height"]
                || binding["bytes"] != native["bytes"]
                || binding["offset"].as_u64() != Some(mip.offset)
                || binding["sha256"].as_str() != Some(hash.as_str())
                || pins.get(&format!("{parent}{file}")).and_then(Value::as_str)
                    != Some(hash.as_str())
            {
                return Err(StorageError::Capture);
            }
        }
        Ok(())
    }
}
