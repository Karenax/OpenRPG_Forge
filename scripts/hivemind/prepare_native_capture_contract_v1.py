"""Prepare isolated common storage extension while engine tests pin live sources."""
from pathlib import Path
from datetime import datetime,timezone
import json,hashlib,subprocess
R=Path(__file__).parents[2];W=Path('F:/OpenRPG_ImportWork');ROOT=W/'native-texture-capture-contract-candidate-v1'
def sha(p):
    with Path(p).open('rb') as f:return hashlib.file_digest(f,'sha256').hexdigest()
def main():
    assert not ROOT.exists();ROOT.mkdir()
    source=R/'crates/forge_assets/src/source_graph_bundle/imported_storage.rs'
    text=source.read_text();(ROOT/'imported_storage.before.rs').write_bytes(source.read_bytes())
    anchor='pub struct ImportedTextureStorage {'
    text=text.replace(anchor,anchor+'''
    /// Schema 2 requires explicit protocol and native source identity.
    /// Legacy schema 1 keeps its original capture and serialized shape.
    #[serde(default, skip_serializing_if = "Option::is_none")]
    pub native_capture: Option<NativeCaptureIdentity>,''',1)
    anchor='#[derive(Clone, Copy, Debug, PartialEq, Eq)]\npub enum StorageError'
    types='''#[derive(Clone, Copy, Debug, PartialEq, Eq, Serialize, Deserialize)]
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
'''
    assert text.count(anchor)==1;text=text.replace(anchor,types+anchor)
    text=text.replace('if self.schema_version != 1 {','if !matches!((self.schema_version, self.native_capture.is_some()), (1, false) | (2, true)) {',1)
    anchor='        let case = capture\n'
    assert text.count(anchor)==1
    text=text.replace(anchor,'''        if let Some(identity) = &self.native_capture {
            self.validate_native_capture(&capture, identity, data.len())?;
            return Ok(data);
        }
'''+anchor,1)
    anchor='}\n#[cfg(test)]'
    assert text.count(anchor)==1
    helper='''    pub fn source_guid(&self) -> Option<&str> {
        self.native_capture.as_ref().map(|v| v.source_guid.as_str())
    }

    fn validate_native_capture(&self, capture: &serde_json::Value, identity: &NativeCaptureIdentity, bytes: usize) -> Result<(), StorageError> {
        use NativeCaptureProtocol::{GpuMipReadback, IsolatedReadableMips};
        let guid = &identity.source_guid;
        if guid.len() != 32 || !guid.bytes().all(|b| b.is_ascii_digit() || (b'a'..=b'f').contains(&b)) {
            return Err(StorageError::Capture);
        }
        let expected_status = match identity.protocol {
            IsolatedReadableMips => "eight_compressed_native_mip_chains_extracted_in_isolated_readable_copy",
            GpuMipReadback => "ten_native_imported_texture_profiles_captured_native_storage_refusals_retained",
        };
        // These literal status values identify source capture protocols, never assets.
        // Global GPU readback refusal remains retained; only complete individual rows qualify storage integrity.
        if capture["status"].as_str() != Some(expected_status) {
            return Err(StorageError::Capture);
        }
        let row = capture["rows"].as_array().and_then(|rows| rows.get(self.capture_case_index as usize)).ok_or(StorageError::Capture)?;
        let srgb = matches!(self.format, ImportedStorageFormat::Bc1RgbaUnormSrgb | ImportedStorageFormat::Bc3RgbaUnormSrgb);
        let s = &self.sampler;
        if row["guid"].as_str() != Some(guid.as_str())
            || row["width"].as_u64() != Some(u64::from(self.width))
            || row["height"].as_u64() != Some(u64::from(self.height))
            || row["graphics_format"].as_str() != Some(self.format.native_name())
            || !(row["error"].is_null() || row["error"].as_str() == Some("")) {
            return Err(StorageError::Capture);
        }
        match identity.protocol {
            IsolatedReadableMips => {
                let before = row["settings_before"].as_str().ok_or(StorageError::Capture)?;
                let settings: serde_json::Value = serde_json::from_str(before).map_err(|_| StorageError::Capture)?;
                if row["settings_restored"].as_str() != Some(before)
                    || row["settings_restored_equal"].as_bool() != Some(true)
                    || row["gpu_samples_bit_exact"].as_bool() != Some(true)
                    || row["private_readability_override"].as_bool() != Some(true)
                    || settings["m_IsReadable"].as_i64() != Some(0)
                    || settings["mipmaps"]["m_sRGBTexture"].as_i64() != Some(i64::from(srgb))
                    || settings["mipmaps"]["m_EnableMipMap"].as_i64() != Some(i64::from(s.source_mipmaps_enabled))
                    || row["mip_count"].as_u64() != Some(self.mips.len() as u64)
                    || row["whole_bytes"].as_u64() != Some(bytes as u64)
                    || row["filter_mode"].as_u64() != Some(u64::from(s.filter_mode))
                    || row["aniso"].as_u64() != Some(u64::from(s.anisotropy))
                    || row["mip_bias"].as_f64() != Some(s.mip_bias)
                    || ["wrap_u","wrap_v","wrap_w"].iter().zip(s.wrap).any(|(key, v)| row[*key].as_u64() != Some(u64::from(v))) {
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
                    || ["wrap_u","wrap_v","wrap_w"].iter().zip(s.wrap).any(|(key, v)| row[*key].as_str() != Some(wraps[v as usize])) {
                    return Err(StorageError::Capture);
                }
            }
        }
        let mips = row["mips"].as_array().ok_or(StorageError::Capture)?;
        if mips.len() != self.mips.len() { return Err(StorageError::Capture); }
        for (level, (native, mip)) in mips.iter().zip(&self.mips).enumerate() {
            if native["level"].as_u64() != Some(level as u64)
                || native["width"].as_u64() != Some(u64::from(mip.width))
                || native["height"].as_u64() != Some(u64::from(mip.height))
                || native["bytes"].as_u64() != Some(mip.bytes)
                || (identity.protocol == GpuMipReadback && (native["native_readback_ok"].as_bool() != Some(true)
                    || !(native["error"].is_null() || native["error"].as_str() == Some("")))) {
                return Err(StorageError::Capture);
            }
        }
        Ok(())
    }
'''
    text=text.replace(anchor,helper+anchor,1)
    text=text.replace('schema_version: 1,','schema_version: 1,\n            native_capture: None,',1)
    target=ROOT/'imported_storage.candidate.rs';target.write_text(text,encoding='utf-8')
    rustfmt=Path('C:/Users/karen/.rustup/toolchains/1.98.0-x86_64-pc-windows-msvc/bin/rustfmt.exe')
    subprocess.run([str(rustfmt),'--edition','2024',str(target)],check=True)
    result={'status':'isolated_native_capture_contract_candidate_parsed_not_installed','prepared_utc':datetime.now(timezone.utc).isoformat(),
        'input_pins':{str(source):sha(source),str(Path(__file__)):sha(__file__)},'candidate':str(target),'candidate_sha256':sha(target),
        'live_common_sources_modified':False,'tests_passed':False,'actual_resources_admitted':False,'full_import':False,'complete_assets_delivered_increment':0,
        'remaining':'Add refusal tests and native resource CLI, bind native GUID to original meta in parent validation, install only after current compiler terminates, then run actual tests and ten resource admission'}
    (ROOT/'PREPARED.v1.json').write_text(json.dumps(result,indent=2),encoding='utf-8');print(json.dumps(result))
if __name__=='__main__':main()
