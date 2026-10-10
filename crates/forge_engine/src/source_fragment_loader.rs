//! Recognize Forge's required private extension, then use Bevy's full glTF loader.
//! Original GLB bytes remain pinned on disk. The known-extension list is adapted
//! only in memory; topology/resources/other requirements still use validation.
use bevy::{
    asset::{AssetLoader, AsyncReadExt, LoadContext, io::Reader},
    gltf::{Gltf, GltfLoader, GltfLoaderSettings},
    prelude::TypePath,
};
use forge_assets::source_fragment::{SOURCE_FRAGMENT_EXTENSION, SourceFragmentContract};

#[derive(TypePath)]
pub(super) struct SourceFragmentGlbLoader {
    pub inner: GltfLoader,
}
impl AssetLoader for SourceFragmentGlbLoader {
    type Asset = Gltf;
    type Settings = GltfLoaderSettings;
    type Error = std::io::Error;
    async fn load(
        &self,
        reader: &mut dyn Reader,
        settings: &GltfLoaderSettings,
        context: &mut LoadContext<'_>,
    ) -> Result<Gltf, Self::Error> {
        let mut bytes = Vec::new();
        reader
            .take(512 * 1024 * 1024 + 1)
            .read_to_end(&mut bytes)
            .await?;
        if bytes.len() > 512 * 1024 * 1024 {
            return Err(std::io::Error::other(
                "GLB exceeds source import allocation limit",
            ));
        }
        let adapted = prepare_source_glb(&bytes).map_err(std::io::Error::other)?;
        // Recognizing one implemented extension never disables full validation.
        let mut effective: GltfLoaderSettings =
            serde_json::from_value(serde_json::to_value(settings).map_err(std::io::Error::other)?)
                .map_err(std::io::Error::other)?;
        effective.validate = true;
        GltfLoader::load_gltf(&self.inner, &adapted, context, &effective)
            .await
            .map_err(std::io::Error::other)
    }
    fn extensions(&self) -> &[&str] {
        &["glb"]
    }
}

#[expect(
    clippy::too_many_lines,
    reason = "Ordered source validation and GPU preparation remain a single auditable operation"
)]
pub fn prepare_source_glb(raw: &[u8]) -> Result<Vec<u8>, String> {
    let header: [u8; 20] = raw
        .get(..20)
        .ok_or("truncated GLB")?
        .try_into()
        .map_err(|_| "GLB header")?;
    let json_length = forge_assets::governed_glb_json_length(
        &header,
        u64::try_from(raw.len()).map_err(|e| e.to_string())?,
    )
    .map_err(|e| e.to_string())?;
    let end = 20 + json_length;
    let json = raw.get(20..end).ok_or("GLB JSON bounds")?;
    let mut document: serde_json::Value =
        serde_json::from_slice(json).map_err(|e| e.to_string())?;
    let required = document
        .get("extensionsRequired")
        .and_then(serde_json::Value::as_array)
        .is_some_and(|v| {
            v.iter()
                .any(|x| x.as_str() == Some(SOURCE_FRAGMENT_EXTENSION))
        });
    if !required {
        return Ok(raw.to_vec());
    }
    if !document
        .get("extensionsUsed")
        .and_then(serde_json::Value::as_array)
        .is_some_and(|used| {
            used.iter()
                .any(|name| name.as_str() == Some(SOURCE_FRAGMENT_EXTENSION))
        })
    {
        return Err("required source fragment extension missing from extensionsUsed".into());
    }
    forge_assets::validate_governed_glb_json(json).map_err(|e| e.to_string())?;
    let materials = document
        .get("materials")
        .and_then(serde_json::Value::as_array)
        .ok_or("required source materials missing")?;
    if materials.is_empty() {
        return Err("empty required source materials".into());
    }
    for material in materials {
        let value = material
            .get("extensions")
            .and_then(|e| e.get(SOURCE_FRAGMENT_EXTENSION))
            .ok_or("required source fragment missing on material")?;
        let contract: SourceFragmentContract =
            serde_json::from_value(value.clone()).map_err(|e| e.to_string())?;
        contract.validate().map_err(str::to_owned)?;
        let mut images = Vec::new();
        images.extend(contract.normal_png.as_ref());
        images.extend(contract.base_png.as_ref().map(|base| &base.image));
        if let Some(program) = &contract.world_blend {
            images.extend([
                &program.blend_mask,
                &program.noise_texture,
                &program.noise_mask,
            ]);
        }
        if !images.is_empty() {
            let chunk_header = raw
                .get(end..end + 8)
                .ok_or("source image BIN header missing")?;
            if &chunk_header[4..8] != b"BIN\0" {
                return Err("source image requires GLB BIN chunk".into());
            }
            let length = usize::try_from(u32::from_le_bytes(
                chunk_header[..4].try_into().map_err(|_| "BIN length")?,
            ))
            .map_err(|e| e.to_string())?;
            let bin_end = (end + 8)
                .checked_add(length)
                .ok_or("source image BIN overflow")?;
            let bin = raw.get(end + 8..bin_end).ok_or("source image BIN bounds")?;
            let views = document
                .get("bufferViews")
                .and_then(serde_json::Value::as_array)
                .ok_or("source image bufferViews missing")?;
            for image in images {
                source_png_view_bytes(bin, views, image)?;
            }
        }
    }
    let required = document
        .get_mut("extensionsRequired")
        .and_then(serde_json::Value::as_array_mut)
        .ok_or("required list")?;
    required.retain(|x| x.as_str() != Some(SOURCE_FRAGMENT_EXTENSION));
    document["extensions"][SOURCE_FRAGMENT_EXTENSION] =
        serde_json::json!({"schema_version":1,"required":true});
    let mut new_json = serde_json::to_vec(&document).map_err(|e| e.to_string())?;
    new_json.resize(new_json.len().next_multiple_of(4), b' ');
    let mut output = Vec::with_capacity(raw.len() + new_json.len());
    output.extend_from_slice(&raw[..12]);
    output.extend_from_slice(
        &u32::try_from(new_json.len())
            .map_err(|e| e.to_string())?
            .to_le_bytes(),
    );
    output.extend_from_slice(b"JSON");
    output.extend_from_slice(&new_json);
    output.extend_from_slice(raw.get(end..).ok_or("GLB tail")?);
    let length = u32::try_from(output.len()).map_err(|e| e.to_string())?;
    output[8..12].copy_from_slice(&length.to_le_bytes());
    Ok(output)
}

/// Hash-check declared image bytes before recognizing the private extension.
/// PNG decoding and renderer admission remain separate, mandatory checks.
fn source_png_view_bytes<'a>(
    bin: &'a [u8],
    views: &[serde_json::Value],
    image: &forge_assets::source_fragment::SourcePngBufferView,
) -> Result<&'a [u8], String> {
    let view = views
        .get(usize::try_from(image.buffer_view).map_err(|e| e.to_string())?)
        .ok_or("source PNG bufferView missing")?;
    if view.get("buffer").and_then(serde_json::Value::as_u64) != Some(0) {
        return Err("source PNG requires buffer zero".into());
    }
    let offset = match view.get("byteOffset") {
        None => 0,
        Some(value) => value.as_u64().ok_or("source PNG offset must be unsigned")?,
    };
    let length = view
        .get("byteLength")
        .and_then(serde_json::Value::as_u64)
        .ok_or("source PNG length missing")?;
    if length == 0 || length > 64 * 1024 * 1024 {
        return Err("source PNG length outside image allocation limit".into());
    }
    let offset = usize::try_from(offset).map_err(|e| e.to_string())?;
    let length = usize::try_from(length).map_err(|e| e.to_string())?;
    let end = offset
        .checked_add(length)
        .ok_or("source PNG view overflow")?;
    let bytes = bin.get(offset..end).ok_or("source PNG view outside BIN")?;
    if forge_core::bytes_sha256(bytes).to_string() != image.sha256 {
        return Err("source PNG SHA mismatch".into());
    }
    Ok(bytes)
}

#[cfg(test)]
mod tests {
    use super::*;
    use forge_assets::source_fragment::SourceBackfaceMode;

    #[test]
    fn source_png_views_require_exact_pinned_bounded_bin_bytes() {
        let bin = [0x81, 0x20, 0xff, 0x42];
        let image = forge_assets::source_fragment::SourcePngBufferView {
            buffer_view: 0,
            sha256: forge_core::bytes_sha256(&bin).to_string(),
        };
        let valid = serde_json::json!({"buffer":0,"byteLength":4});
        assert_eq!(
            source_png_view_bytes(&bin, std::slice::from_ref(&valid), &image).unwrap(),
            bin
        );
        for invalid in [
            serde_json::json!({"buffer":1,"byteLength":4}),
            serde_json::json!({"buffer":0,"byteOffset":-1,"byteLength":4}),
            serde_json::json!({"buffer":0,"byteOffset":1,"byteLength":4}),
            serde_json::json!({"buffer":0,"byteLength":0}),
            serde_json::json!({"buffer":0,"byteLength":67_108_865}),
            serde_json::json!({"buffer":0,"byteOffset":18_446_744_073_709_551_615_u64,"byteLength":4}),
            serde_json::json!({"buffer":0}),
        ] {
            assert!(source_png_view_bytes(&bin, &[invalid], &image).is_err());
        }
        let mut wrong = image.clone();
        wrong.sha256 = "a".repeat(64);
        assert!(source_png_view_bytes(&bin, std::slice::from_ref(&valid), &wrong).is_err());
        wrong.buffer_view = 1;
        assert!(source_png_view_bytes(&bin, &[valid], &wrong).is_err());
    }

    fn contract() -> SourceFragmentContract {
        SourceFragmentContract {
            schema_version: 1,
            source_program_sha256: "a".repeat(64),
            linear_rgb_rows: [
                [1.0, 0.0, 0.0, -0.1],
                [0.0, 1.0, 0.0, 0.0],
                [0.0, 0.0, 1.0, 0.0],
            ],
            metallic: [0.0, 0.0, 1.0, 0.0, 0.0],
            occlusion: [1.0, 0.0, 0.0, 0.0, 0.0],
            perceptual_roughness: [0.0, 3.0, 0.0, 0.0, 0.0],
            normal_strength: 1.0,
            target_tangent_space_y_sign: -1.0,
            normal_png: None,
            world_blend: None,
            surface_uv_scale: None,
            base_color_power: None,
            base_png: None,
            double_sided_normal_mode: SourceBackfaceMode::SingleSided,
        }
    }

    fn document() -> serde_json::Value {
        serde_json::json!({
            "asset":{"version":"2.0"},
            "extensionsUsed":[SOURCE_FRAGMENT_EXTENSION],
            "extensionsRequired":[SOURCE_FRAGMENT_EXTENSION],
            "materials":[{"extensions":{SOURCE_FRAGMENT_EXTENSION:contract()}}],
            "buffers":[{"byteLength":4}],
        })
    }

    fn glb(document: &serde_json::Value) -> Vec<u8> {
        let mut json = serde_json::to_vec(document).unwrap();
        json.resize(json.len().next_multiple_of(4), b' ');
        let mut bytes = b"glTF\x02\0\0\0".to_vec();
        bytes.extend_from_slice(&u32::try_from(20 + json.len() + 12).unwrap().to_le_bytes());
        bytes.extend_from_slice(&u32::try_from(json.len()).unwrap().to_le_bytes());
        bytes.extend_from_slice(b"JSON");
        bytes.extend_from_slice(&json);
        bytes.extend_from_slice(b"\x04\0\0\0BIN\0\x81\x20\xff\x42");
        bytes
    }

    #[test]
    fn source_extension_admission_preserves_bin_and_all_material_coefficients() {
        let raw = glb(&document());
        let adapted = prepare_source_glb(&raw).unwrap();
        let source = bevy::gltf::gltf::Gltf::from_slice_without_validation(&raw).unwrap();
        let admitted = bevy::gltf::gltf::Gltf::from_slice(&adapted).unwrap();
        assert_eq!(source.blob, admitted.blob);
        assert_eq!(source.blob.as_deref(), Some(&[0x81, 0x20, 0xff, 0x42][..]));
        let material = admitted.materials().next().unwrap();
        let value = material.extension_value(SOURCE_FRAGMENT_EXTENSION).unwrap();
        let restored: SourceFragmentContract = serde_json::from_value(value.clone()).unwrap();
        assert_eq!(restored, contract());
        assert_eq!(
            admitted.extension_value(SOURCE_FRAGMENT_EXTENSION).unwrap()["required"],
            true
        );
        assert_eq!(admitted.extensions_required().count(), 0);
        assert_eq!(raw, glb(&document()));
    }

    #[test]
    fn source_extension_admission_never_accepts_missing_or_invalid_contracts_or_uris() {
        for material in [
            serde_json::json!({}),
            serde_json::json!({"extensions":{SOURCE_FRAGMENT_EXTENSION:{"schema_version":99}}}),
        ] {
            let mut invalid = document();
            invalid["materials"][0] = material;
            assert!(prepare_source_glb(&glb(&invalid)).is_err());
        }
        let mut external = document();
        external["buffers"][0]["uri"] = "secret.bin".into();
        assert!(prepare_source_glb(&glb(&external)).is_err());
        let mut malformed = glb(&document());
        malformed[8..12].copy_from_slice(&u32::MAX.to_le_bytes());
        assert!(prepare_source_glb(&malformed).is_err());
        assert!(prepare_source_glb(b"truncated").is_err());
        let mut undeclared = document();
        undeclared.as_object_mut().unwrap().remove("extensionsUsed");
        assert!(prepare_source_glb(&glb(&undeclared)).is_err());
        undeclared["extensionsUsed"] = serde_json::json!(["UNRELATED_extension"]);
        assert!(prepare_source_glb(&glb(&undeclared)).is_err());
    }

    #[test]
    fn source_extension_admission_keeps_other_required_extensions_subject_to_full_validation() {
        let mut unknown = document();
        unknown["extensionsRequired"]
            .as_array_mut()
            .unwrap()
            .push("UNKNOWN_required".into());
        unknown["extensionsUsed"]
            .as_array_mut()
            .unwrap()
            .push("UNKNOWN_required".into());
        let adapted = prepare_source_glb(&glb(&unknown)).unwrap();
        let parsed = bevy::gltf::gltf::Gltf::from_slice_without_validation(&adapted).unwrap();
        assert_eq!(
            parsed.extensions_required().collect::<Vec<_>>(),
            vec!["UNKNOWN_required"]
        );
        assert!(bevy::gltf::gltf::Gltf::from_slice(&adapted).is_err());
        let mut ordinary = document();
        ordinary
            .as_object_mut()
            .unwrap()
            .remove("extensionsRequired");
        let raw = glb(&ordinary);
        assert_eq!(prepare_source_glb(&raw).unwrap(), raw);
    }
}
