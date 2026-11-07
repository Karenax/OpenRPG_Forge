//! Recognize Forge's required private extension, then use Bevy's full glTF loader.
//! Original GLB bytes remain pinned on disk. The known-extension list is adapted
//! only in memory; topology/resources/other requirements still use validation.
use bevy::{
    asset::{AssetLoader, AsyncReadExt, LoadContext, io::Reader},
    gltf::{Gltf, GltfLoader, GltfLoaderSettings},
    prelude::TypePath,
};
#[cfg(test)]
use forge_assets::source_fragment::{SOURCE_FRAGMENT_EXTENSION, SourceFragmentContract};
#[cfg(test)]
use std::borrow::Cow;

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
        let adapted =
            forge_assets::source_fragment_glb::prepare(&bytes).map_err(std::io::Error::other)?;
        // Recognizing one implemented extension never disables full validation.
        let mut effective: GltfLoaderSettings =
            serde_json::from_value(serde_json::to_value(settings).map_err(std::io::Error::other)?)
                .map_err(std::io::Error::other)?;
        effective.validate = true;
        GltfLoader::load_gltf(&self.inner, &adapted.bytes, context, &effective)
            .await
            .map_err(std::io::Error::other)
    }
    fn extensions(&self) -> &[&str] {
        &["glb"]
    }
}

pub fn prepare_source_glb(raw: &[u8]) -> Result<Vec<u8>, String> {
    Ok(forge_assets::source_fragment_glb::prepare(raw)?
        .bytes
        .into_owned())
}

#[cfg(test)]
mod tests {
    use super::*;
    use forge_assets::source_fragment::SourceBackfaceMode;

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
    fn ordinary_glb_load_borrows_the_original_buffer_but_source_adaptation_is_owned() {
        let ordinary = glb(&serde_json::json!({"asset":{"version":"2.0"}}));
        assert!(matches!(
            forge_assets::source_fragment_glb::prepare(&ordinary)
                .unwrap()
                .bytes,
            Cow::Borrowed(_)
        ));
        let source = glb(&document());
        assert!(matches!(
            forge_assets::source_fragment_glb::prepare(&source)
                .unwrap()
                .bytes,
            Cow::Owned(_)
        ));
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

    #[test]
    fn source_graph_cannot_reach_the_ordinary_or_source_fragment_loader() {
        let mut graph = document();
        graph["extensionsUsed"] = serde_json::json!(["OPENRPG_source_graph"]);
        graph["extensionsRequired"] = serde_json::json!(["OPENRPG_source_graph"]);
        graph["materials"][0]["extensions"] = serde_json::json!({"OPENRPG_source_graph": {}});
        assert!(prepare_source_glb(&glb(&graph)).is_err());
        graph.as_object_mut().unwrap().remove("extensionsRequired");
        assert!(prepare_source_glb(&glb(&graph)).is_err());
        graph["extensionsUsed"] = serde_json::json!([]);
        assert!(prepare_source_glb(&glb(&graph)).is_err());
    }
}
