//! Renderer-neutral wire contract for autonomous GLB source-graph materials.
//! Integrity admission does not qualify shader execution, source texture import,
//! mesh attributes, camera mappings, or rendered equivalence.
use forge_core::{ContentHash, bytes_sha256};
use serde::{Deserialize, Serialize};
use std::collections::BTreeSet;
#[path = "source_graph_bundle/imported_storage.rs"]
mod imported_storage;
pub use imported_storage::{
    ImportedStorageFormat as SourceGraphImportedStorageFormat,
    ImportedStorageMip as SourceGraphImportedStorageMip,
    ImportedTextureStorage as SourceGraphImportedTextureStorage, NativeCaptureIdentity,
    NativeCaptureProtocol, ResolvedStorageSampler as SourceGraphResolvedStorageSampler,
    StorageError as SourceGraphImportedStorageError,
};

pub const SOURCE_GRAPH_EXTENSION: &str = "OPENRPG_source_graph";

/// Declaration-ordered authored slots. Keep the original seven bindings stable
/// and leave scene/scalar bindings 120..=124 untouched.
pub const SOURCE_GRAPH_TEXTURE_BINDINGS: [(u32, u32); 10] = [
    (101, 102),
    (103, 104),
    (105, 106),
    (107, 108),
    (109, 110),
    (111, 112),
    (113, 114),
    (115, 116),
    (117, 118),
    (125, 126),
];
pub const SOURCE_GRAPH_MAX_TEXTURES: usize = SOURCE_GRAPH_TEXTURE_BINDINGS.len();

/// Wire-version admission is independent of actual device resource limits.
/// Schema 6 expands authored slots without changing the source uniform ABI.
#[must_use]
pub const fn source_graph_texture_capacity(schema_version: u32) -> Option<usize> {
    match schema_version {
        2..=5 => Some(7),
        6 => Some(SOURCE_GRAPH_MAX_TEXTURES),
        _ => None,
    }
}

#[derive(Clone, Debug, PartialEq, Serialize, Deserialize)]
#[serde(deny_unknown_fields)]
pub struct SourceGraphBlob {
    pub buffer_view: usize,
    pub sha256: ContentHash,
}

#[derive(Clone, Debug, PartialEq, Serialize, Deserialize)]
#[serde(deny_unknown_fields)]
pub struct SourceGraphTexture {
    pub png: SourceGraphBlob,
    /// Complete original Unity `TextureImporter` metadata, embedded verbatim.
    /// Sampler fields below alone cannot describe resize, compression or normals.
    pub source_meta: SourceGraphBlob,
    pub source_meta_sha256: ContentHash,
    pub is_srgb: bool,
    pub source_mipmaps: bool,
    /// Preserve serialized Unity defaults (-1); resolve them before native use.
    pub filter_mode: i32,
    pub wrap: [i32; 3],
    pub anisotropy: i32,
    pub mip_bias: f64,
    /// Version 3 material transport retains measured storage; no PNG-only fallback.
    /// Absence remains valid for historical version 2 integrity inspection.
    #[serde(default, skip_serializing_if = "Option::is_none")]
    pub imported_storage: Option<SourceGraphImportedTextureStorage>,
}

#[derive(Clone, Debug, PartialEq, Serialize, Deserialize)]
#[serde(deny_unknown_fields)]
#[allow(
    clippy::struct_excessive_bools,
    reason = "Independent captured render-pass switches are preserved by the versioned pipeline contract"
)]
pub struct SourceGraphPipelineState {
    /// Renderer channel bits RED=1, GREEN=2, BLUE=4, ALPHA=8.
    /// Mandatory in schema 4; absent in schemas 2/3, whose renderer wrote ALL.
    /// Producers translate named source channels; Unity numeric masks differ.
    #[serde(default, skip_serializing_if = "Option::is_none")]
    pub color_write_mask: Option<u32>,
    pub color_blend: [u8; 2],
    pub alpha_blend: [u8; 2],
    pub forward_cull: u8,
    pub prepass_cull: u8,
    pub depth_compare: u8,
    pub write_depth: bool,
    pub transparent: bool,
    pub alpha_test: bool,
    pub alpha_to_coverage: bool,
    pub render_queue: i32,
    pub disabled_passes: Vec<String>,
}

#[derive(Clone, Debug, PartialEq, Serialize, Deserialize)]
#[serde(deny_unknown_fields)]
pub struct SourceGraphMaterialContract {
    pub schema_version: u32,
    pub source_program_sha256: ContentHash,
    pub source_material_sha256: ContentHash,
    pub forward: SourceGraphBlob,
    pub prepass: SourceGraphBlob,
    pub libraries: Vec<SourceGraphBlob>,
    pub textures: Vec<SourceGraphTexture>,
    pub state: SourceGraphPipelineState,
    pub requires_original_vertex_color: bool,
    /// Unknown source packing stays None until independently qualified.
    pub normal_decode_modes: [Option<SourceGraphNormalDecodeMode>; 8],
    /// Schema 5 carries compiler-ordered scalar values for uniform binding 120.
    /// Earlier schemas cannot silently supply zero for a program requiring it.
    #[serde(default, skip_serializing_if = "Option::is_none")]
    pub scalar_parameters: Option<SourceGraphScalarParameterContract>,
}

#[path = "source_graph_bundle/scalar_parameters.rs"]
mod scalar_parameters;
pub use scalar_parameters::SourceGraphScalarParameterContract;

#[derive(Clone, Copy, Debug, PartialEq, Eq, Serialize, Deserialize)]
#[serde(rename_all = "snake_case")]
pub enum SourceGraphNormalDecodeMode {
    Rgb,
    ReconstructZ,
    /// Explicitly qualified RG-or-AG packing; the original alpha participates in X.
    RgOrAg,
}

impl SourceGraphNormalDecodeMode {
    /// Source shader ABI. This conversion does not qualify a texture's packing.
    #[must_use]
    pub const fn shader_value(self) -> f32 {
        match self {
            Self::Rgb => 0.0,
            Self::ReconstructZ => 1.0,
            Self::RgOrAg => 2.0,
        }
    }
}

#[derive(Clone, Copy, Debug, PartialEq, Eq)]
pub enum SourceGraphContractError {
    Schema,
    MissingBufferView,
    ChangedBlob,
    InvalidShader,
    TextureLayout,
    PrivateNamespace,
    InvalidPng,
    InvalidMetadata,
    ImportSettings,
    PipelineState,
    UnqualifiedNormalDecode,
    ScalarParameters,
    ImportedStorage(SourceGraphImportedStorageError),
}

pub(crate) fn validate_texture_integrity(
    texture: &SourceGraphTexture,
    views: &[&[u8]],
) -> Result<(), SourceGraphContractError> {
    if texture.source_meta.sha256 != texture.source_meta_sha256 {
        return Err(SourceGraphContractError::InvalidMetadata);
    }
    let meta_bytes = blob(&texture.source_meta, views)?;
    let meta =
        std::str::from_utf8(meta_bytes).map_err(|_| SourceGraphContractError::InvalidMetadata)?;
    if meta_bytes.len() > 1024 * 1024
        || meta.contains('\0')
        || !meta.lines().any(|line| line == "TextureImporter:")
    {
        return Err(SourceGraphContractError::InvalidMetadata);
    }
    let png = blob(&texture.png, views)?;
    if png.len() < 33
        || &png[..8] != b"\x89PNG\r\n\x1a\n"
        || &png[12..16] != b"IHDR"
        || png[16..20] == [0; 4]
        || png[20..24] == [0; 4]
    {
        return Err(SourceGraphContractError::InvalidPng);
    }
    if !(-1..=2).contains(&texture.filter_mode)
        || texture.wrap.iter().any(|x| !(-1..=3).contains(x))
        || texture.anisotropy < -1
        || !texture.mip_bias.is_finite()
    {
        return Err(SourceGraphContractError::ImportSettings);
    }
    if let Some(storage) = &texture.imported_storage {
        if let Some(expected_guid) = storage.source_guid() {
            // A native row may not be attached to a different purchased texture,
            // even when an attacker recomputes the outer metadata hashes.
            let mut guids = meta.lines().filter_map(|line| line.strip_prefix("guid: "));
            if guids.next() != Some(expected_guid) || guids.next().is_some() {
                return Err(SourceGraphContractError::InvalidMetadata);
            }
        }
        storage
            .validate(views)
            .map_err(SourceGraphContractError::ImportedStorage)?;
        let srgb = matches!(
            storage.format,
            SourceGraphImportedStorageFormat::Bc1RgbaUnormSrgb
                | SourceGraphImportedStorageFormat::Bc3RgbaUnormSrgb
        );
        let sampler = &storage.sampler;
        if texture.is_srgb != srgb
            || texture.source_mipmaps != sampler.source_mipmaps_enabled
            || (texture.filter_mode >= 0 && texture.filter_mode != i32::from(sampler.filter_mode))
            || texture
                .wrap
                .iter()
                .zip(sampler.wrap)
                .any(|(original, resolved)| *original >= 0 && *original != i32::from(resolved))
            || (texture.anisotropy > 0 && texture.anisotropy != i32::from(sampler.anisotropy))
            || (texture.anisotropy == 0 && sampler.anisotropy != 1)
            || !shader_mip_bias_matches(texture.mip_bias, sampler.mip_bias)
        {
            return Err(SourceGraphContractError::ImportSettings);
        }
    }
    Ok(())
}

/// The imported sampler uses f32 on the GPU. Compare that exact projection,
/// retaining the original numerical equality of positive and negative zero.
#[allow(
    clippy::cast_possible_truncation,
    reason = "The native shader sampler explicitly consumes f32 mip bias"
)]
fn shader_mip_bias_matches(original: f64, resolved: f64) -> bool {
    let original = original as f32;
    let resolved = resolved as f32;
    original.to_bits() == resolved.to_bits()
        || (original.abs().to_bits() == 0 && resolved.abs().to_bits() == 0)
}

#[cfg(test)]
mod sampler_projection_tests {
    use super::shader_mip_bias_matches;

    #[test]
    fn sampler_admission_preserves_native_precision_and_signed_zero() {
        assert!(shader_mip_bias_matches(0.0, -0.0));
        assert!(shader_mip_bias_matches(1.0, 1.0 + f64::EPSILON));
        assert!(!shader_mip_bias_matches(
            1.0,
            f64::from(f32::from_bits(1.0_f32.to_bits() + 1))
        ));
    }
}

fn blob<'a>(
    blob: &SourceGraphBlob,
    views: &'a [&[u8]],
) -> Result<&'a [u8], SourceGraphContractError> {
    let bytes = views
        .get(blob.buffer_view)
        .copied()
        .ok_or(SourceGraphContractError::MissingBufferView)?;
    if bytes_sha256(bytes) != blob.sha256 {
        return Err(SourceGraphContractError::ChangedBlob);
    }
    Ok(bytes)
}

/// Exact shared scene dependency, separate from the seven authored image slots.
/// Declaring it does not provide the image or qualify source lighting.
pub const SOURCE_GRAPH_PREINTEGRATED_BRDF_DECLARATION: &str = "@group(#{MATERIAL_BIND_GROUP}) @binding(122) var source_preintegrated_brdf_image:texture_2d<f32>;";
pub const SOURCE_GRAPH_NATIVE_SRGB_THRESHOLDS_DECLARATION: &str = "@group(#{MATERIAL_BIND_GROUP}) @binding(128) var source_native_srgb_thresholds: texture_2d<f32>;";
pub const SOURCE_GRAPH_DEFERRED_SRGB_DECLARATION: &str = "@group(#{MATERIAL_BIND_GROUP}) @binding(127) var source_deferred_srgb_lookup: texture_2d<f32>;";
pub const SOURCE_GRAPH_SKY_CUBE_DECLARATION: &str = "@group(#{MATERIAL_BIND_GROUP}) @binding(123) var source_sky_cube_image:texture_cube_array<f32>;";
pub const SOURCE_GRAPH_SKY_SAMPLER_DECLARATION: &str =
    "@group(#{MATERIAL_BIND_GROUP}) @binding(124) var source_sky_cube_sampler:sampler;";

/// Canonical authored slots and explicit scene resources, shared with runtime
/// registration. Scene resources never contribute to the authored slot count.
pub fn stage_texture_count(source: &str) -> Result<usize, SourceGraphContractError> {
    let mut count = 0;
    let mut samplers = 0;
    let mut native_srgb_seen = false;
    let mut deferred_srgb_seen = false;
    let mut scene_brdf_seen = false;
    let mut scene_cube_seen = false;
    let mut scene_cube_sampler_seen = false;
    for line in source.lines() {
        let line = line.trim();
        if line.contains("@binding(") && line.contains("texture_") {
            if line == SOURCE_GRAPH_NATIVE_SRGB_THRESHOLDS_DECLARATION {
                if native_srgb_seen {
                    return Err(SourceGraphContractError::TextureLayout);
                }
                native_srgb_seen = true;
                continue;
            }
            if line == SOURCE_GRAPH_DEFERRED_SRGB_DECLARATION {
                if deferred_srgb_seen {
                    return Err(SourceGraphContractError::TextureLayout);
                }
                deferred_srgb_seen = true;
                continue;
            }
            if line == SOURCE_GRAPH_SKY_CUBE_DECLARATION {
                if scene_cube_seen {
                    return Err(SourceGraphContractError::TextureLayout);
                }
                scene_cube_seen = true;
                continue;
            }
            if line == SOURCE_GRAPH_PREINTEGRATED_BRDF_DECLARATION {
                if scene_brdf_seen {
                    return Err(SourceGraphContractError::TextureLayout);
                }
                scene_brdf_seen = true;
                continue;
            }
            let binding = SOURCE_GRAPH_TEXTURE_BINDINGS
                .get(count)
                .ok_or(SourceGraphContractError::TextureLayout)?
                .0;
            let expected = format!(
                "@group(#{{MATERIAL_BIND_GROUP}}) @binding({binding}) var t{count}:texture_2d<f32>;"
            );
            if line != expected {
                return Err(SourceGraphContractError::TextureLayout);
            }
            count += 1;
        }
        if line.contains("@binding(") && line.contains("sampler") {
            if line == SOURCE_GRAPH_SKY_SAMPLER_DECLARATION {
                if scene_cube_sampler_seen {
                    return Err(SourceGraphContractError::TextureLayout);
                }
                scene_cube_sampler_seen = true;
                continue;
            }
            let binding = SOURCE_GRAPH_TEXTURE_BINDINGS
                .get(samplers)
                .ok_or(SourceGraphContractError::TextureLayout)?
                .1;
            let expected = format!(
                "@group(#{{MATERIAL_BIND_GROUP}}) @binding({binding}) var s{samplers}:sampler;"
            );
            if line != expected {
                return Err(SourceGraphContractError::TextureLayout);
            }
            samplers += 1;
        }
    }
    if samplers != count || scene_cube_seen != scene_cube_sampler_seen {
        return Err(SourceGraphContractError::TextureLayout);
    }
    Ok(count)
}

#[cfg(test)]
mod ten_texture_layout_tests {
    use super::*;
    fn declarations() -> String {
        SOURCE_GRAPH_TEXTURE_BINDINGS.iter().enumerate().map(|(i, (texture, sampler))|
            format!("@group(#{{MATERIAL_BIND_GROUP}}) @binding({texture}) var t{i}:texture_2d<f32>;\n@group(#{{MATERIAL_BIND_GROUP}}) @binding({sampler}) var s{i}:sampler;")
        ).collect::<Vec<_>>().join("\n")
    }
    #[test]
    fn schema6_ten_authored_slots_preserve_scene_bindings_and_legacy_wire_limits() {
        let source = format!(
            "{}\n{}\n{}\n{}",
            declarations(),
            SOURCE_GRAPH_PREINTEGRATED_BRDF_DECLARATION,
            SOURCE_GRAPH_SKY_CUBE_DECLARATION,
            SOURCE_GRAPH_SKY_SAMPLER_DECLARATION
        );
        assert_eq!(stage_texture_count(&source).unwrap(), 10);
        for schema in 2..=5 {
            assert_eq!(source_graph_texture_capacity(schema), Some(7));
        }
        assert_eq!(source_graph_texture_capacity(6), Some(10));
        assert_eq!(source_graph_texture_capacity(7), None);
        assert_eq!(
            &SOURCE_GRAPH_TEXTURE_BINDINGS[..7],
            &[
                (101, 102),
                (103, 104),
                (105, 106),
                (107, 108),
                (109, 110),
                (111, 112),
                (113, 114)
            ]
        );
    }
    #[test]
    fn source_slots_reject_scene_collisions_missing_images_and_eleventh_slot() {
        let source = declarations();
        for changed in [
            source.replace("@binding(125)", "@binding(119)"),
            source.replace("@binding(126)", "@binding(120)"),
            source.replace("var t8:", "var t9:"),
            source.replace(
                "@group(#{MATERIAL_BIND_GROUP}) @binding(126) var s9:sampler;",
                "",
            ),
            format!(
                "{source}\n@group(#{{MATERIAL_BIND_GROUP}}) @binding(127) var t10:texture_2d<f32>;\n@group(#{{MATERIAL_BIND_GROUP}}) @binding(128) var s10:sampler;"
            ),
        ] {
            assert!(stage_texture_count(&changed).is_err());
        }
    }
}

#[cfg(test)]
mod scene_sky_texture_layout_tests {
    use super::*;
    fn cube() -> String {
        format!("{SOURCE_GRAPH_SKY_CUBE_DECLARATION}\n{SOURCE_GRAPH_SKY_SAMPLER_DECLARATION}")
    }
    #[test]
    fn scene_cube_and_brdf_preserve_all_seven_authored_slots() {
        let mut source = format!(
            "{}\n{}",
            cube(),
            SOURCE_GRAPH_PREINTEGRATED_BRDF_DECLARATION
        );
        assert_eq!(stage_texture_count(&source).unwrap(), 0);
        for i in 0..7 {
            use std::fmt::Write as _;
            let texture_binding = 101 + 2 * i;
            let sampler_binding = 102 + 2 * i;
            write!(source, "\n@group(#{{MATERIAL_BIND_GROUP}}) @binding({texture_binding}) var t{i}:texture_2d<f32>;\n@group(#{{MATERIAL_BIND_GROUP}}) @binding({sampler_binding}) var s{i}:sampler;").unwrap();
            assert_eq!(stage_texture_count(&source).unwrap(), i + 1);
        }
    }
    #[test]
    fn cube_sampler_must_be_paired_exactly_once() {
        assert!(stage_texture_count(SOURCE_GRAPH_SKY_CUBE_DECLARATION).is_err());
        assert!(stage_texture_count(SOURCE_GRAPH_SKY_SAMPLER_DECLARATION).is_err());
        for declaration in [
            SOURCE_GRAPH_SKY_CUBE_DECLARATION,
            SOURCE_GRAPH_SKY_SAMPLER_DECLARATION,
        ] {
            assert!(stage_texture_count(&format!("{}\n{declaration}", cube())).is_err());
        }
    }
    #[test]
    fn changed_cube_type_binding_name_and_sampler_refused() {
        let source = cube();
        for (before, after) in [
            ("123", "125"),
            ("124", "126"),
            ("source_sky_cube_image", "unknown_cube"),
            ("source_sky_cube_sampler", "unknown_sampler"),
            ("texture_cube_array<f32>", "texture_cube<f32>"),
            ("texture_cube_array<f32>", "texture_cube_array<u32>"),
            (":sampler;", ":sampler_comparison;"),
        ] {
            assert!(
                stage_texture_count(&source.replace(before, after)).is_err(),
                "{before} -> {after}"
            );
        }
    }
}

#[cfg(test)]
mod scene_brdf_texture_layout_tests {
    use super::*;

    #[test]
    fn shared_scene_image_does_not_replace_authored_slots() {
        assert_eq!(
            stage_texture_count(SOURCE_GRAPH_PREINTEGRATED_BRDF_DECLARATION).unwrap(),
            0
        );
        let source = format!(
            "@group(#{{MATERIAL_BIND_GROUP}}) @binding(101) var t0:texture_2d<f32>;\n@group(#{{MATERIAL_BIND_GROUP}}) @binding(102) var s0:sampler;\n{SOURCE_GRAPH_PREINTEGRATED_BRDF_DECLARATION}"
        );
        assert_eq!(stage_texture_count(&source).unwrap(), 1);
        assert!(stage_texture_count(&source.replace("var t0:", "var t1:")).is_err());
    }

    #[test]
    fn duplicate_or_changed_scene_declarations_are_refused() {
        let declaration = SOURCE_GRAPH_PREINTEGRATED_BRDF_DECLARATION;
        assert!(stage_texture_count(&format!("{declaration}\n{declaration}")).is_err());
        for changed in [
            declaration.replace("122", "123"),
            declaration.replace("source_preintegrated_brdf_image", "unknown_scene_image"),
            declaration.replace("texture_2d<f32>", "texture_2d<u32>"),
        ] {
            assert!(stage_texture_count(&changed).is_err());
        }
    }

    #[test]
    fn shared_scene_image_does_not_admit_an_extra_sampler() {
        let source = format!(
            "{SOURCE_GRAPH_PREINTEGRATED_BRDF_DECLARATION}\n@group(#{{MATERIAL_BIND_GROUP}}) @binding(123) var scene_sampler:sampler;"
        );
        assert!(stage_texture_count(&source).is_err());
    }
}

impl SourceGraphMaterialContract {
    /// Require a deliberate decoder choice for every actual uniform read.
    /// A choice still needs original importer/platform and pixel evidence.
    pub fn validate_normal_decode_modes(
        &self,
        views: &[&[u8]],
    ) -> Result<(), SourceGraphContractError> {
        self.validate_integrity(views)
    }

    fn validate_normal_decode_reads(
        &self,
        views: &[&[u8]],
    ) -> Result<(), SourceGraphContractError> {
        for stage in [&self.forward, &self.prepass]
            .into_iter()
            .chain(&self.libraries)
        {
            let source = std::str::from_utf8(blob(stage, views)?)
                .map_err(|_| SourceGraphContractError::InvalidShader)?;
            for part in source.split("source_graph.normal_decode_modes[").skip(1) {
                let (group, tail) = part
                    .split_once("].")
                    .ok_or(SourceGraphContractError::InvalidShader)?;
                let group: usize = group
                    .parse()
                    .map_err(|_| SourceGraphContractError::InvalidShader)?;
                // A vector read consumes every selected profile, not just its
                // first component. Reject unknown members and dynamic selectors.
                let member: String = tail
                    .chars()
                    .take_while(|c| c.is_ascii_alphanumeric() || *c == '_')
                    .collect();
                if member.is_empty() || member.len() > 4 {
                    return Err(SourceGraphContractError::InvalidShader);
                }
                if group > 1 {
                    return Err(SourceGraphContractError::UnqualifiedNormalDecode);
                }
                for component in member.chars() {
                    let channel = "xyzw"
                        .find(component)
                        .ok_or(SourceGraphContractError::InvalidShader)?;
                    if self.normal_decode_modes[group * 4 + channel].is_none() {
                        return Err(SourceGraphContractError::UnqualifiedNormalDecode);
                    }
                }
            }
        }
        Ok(())
    }
    /// Check referenced bytes and native binding ABI before constructing assets.
    /// Callers must also qualify original import settings, frames and uniforms.
    pub fn validate_integrity(&self, views: &[&[u8]]) -> Result<(), SourceGraphContractError> {
        let texture_capacity = source_graph_texture_capacity(self.schema_version)
            .ok_or(SourceGraphContractError::Schema)?;
        if (self.schema_version == 2 && self.textures.iter().any(|t| t.imported_storage.is_some()))
            || (matches!(self.schema_version, 3..=6)
                && self.textures.iter().any(|t| t.imported_storage.is_none()))
        {
            return Err(SourceGraphContractError::Schema);
        }
        if (self.schema_version < 4 && self.state.color_write_mask.is_some())
            || (self.schema_version >= 4 && self.state.color_write_mask.is_none())
            || (self.schema_version < 5 && self.scalar_parameters.is_some())
        {
            return Err(SourceGraphContractError::Schema);
        }
        if self.textures.len() > texture_capacity || self.libraries.len() > 16 {
            return Err(SourceGraphContractError::TextureLayout);
        }
        for stage in [&self.forward, &self.prepass] {
            let bytes = blob(stage, views)?;
            let source =
                std::str::from_utf8(bytes).map_err(|_| SourceGraphContractError::InvalidShader)?;
            if source.contains('\0')
                || source.len() > 1_048_576
                || source
                    .lines()
                    .any(|l| l.trim().starts_with("#define_import_path"))
            {
                return Err(SourceGraphContractError::InvalidShader);
            }
            if stage_texture_count(source)? != self.textures.len() {
                return Err(SourceGraphContractError::TextureLayout);
            }
        }
        let mut namespaces = BTreeSet::new();
        for library in &self.libraries {
            let source = std::str::from_utf8(blob(library, views)?)
                .map_err(|_| SourceGraphContractError::InvalidShader)?;
            let declarations: Vec<_> = source
                .lines()
                .filter_map(|line| line.trim().strip_prefix("#define_import_path "))
                .collect();
            if declarations.len() != 1
                || !declarations[0].starts_with("forge_source_graph::")
                || declarations[0].len() > 256
                || declarations[0].split("::").any(|part| {
                    part.is_empty() || !part.chars().all(|c| c.is_ascii_alphanumeric() || c == '_')
                })
                || !namespaces.insert(declarations[0])
            {
                return Err(SourceGraphContractError::PrivateNamespace);
            }
        }
        // Preserve the atomic private dependency batch used by the native loader.
        for shader in [&self.forward, &self.prepass]
            .into_iter()
            .chain(&self.libraries)
        {
            let source = std::str::from_utf8(blob(shader, views)?)
                .map_err(|_| SourceGraphContractError::InvalidShader)?;
            for line in source.lines() {
                if let Some(import) = line.trim().strip_prefix("#import ") {
                    let name = import.split_whitespace().next().unwrap_or_default();
                    if name.starts_with("forge_source_graph::") && !namespaces.contains(name) {
                        return Err(SourceGraphContractError::PrivateNamespace);
                    }
                }
            }
        }
        for texture in &self.textures {
            validate_texture_integrity(texture, views)?;
        }
        self.validate_normal_decode_reads(views)?;
        self.read_scalar_parameters(views)?;
        let state = &self.state;
        if state.color_write_mask.is_some_and(|mask| mask > 15)
            || state.forward_cull > 2
            || state.prepass_cull > 2
            || !(1..=8).contains(&state.depth_compare)
            || state
                .color_blend
                .iter()
                .chain(&state.alpha_blend)
                .any(|x| *x > 10)
            || state.disabled_passes.len() > 64
            || state
                .disabled_passes
                .iter()
                .any(|s| s.is_empty() || s.len() > 128 || s.chars().any(char::is_control))
        {
            return Err(SourceGraphContractError::PipelineState);
        }
        Ok(())
    }
}

#[cfg(test)]
mod normal_decode_tests {
    use super::*;

    #[test]
    fn integrity_admission_refuses_an_unqualified_normal_shader_read() {
        let bytes = b"fn decode() -> f32 { return source_graph.normal_decode_modes[1].w; }";
        let hash = bytes_sha256(bytes);
        let mut contract = SourceGraphMaterialContract {
            schema_version: 2,
            source_program_sha256: hash,
            source_material_sha256: hash,
            forward: SourceGraphBlob {
                buffer_view: 0,
                sha256: hash,
            },
            prepass: SourceGraphBlob {
                buffer_view: 0,
                sha256: hash,
            },
            libraries: vec![],
            textures: vec![],
            state: SourceGraphPipelineState {
                color_write_mask: None,
                color_blend: [1, 0],
                alpha_blend: [1, 0],
                forward_cull: 2,
                prepass_cull: 2,
                depth_compare: 4,
                write_depth: true,
                transparent: false,
                alpha_test: false,
                alpha_to_coverage: false,
                render_queue: 2000,
                disabled_passes: vec![],
            },
            requires_original_vertex_color: false,
            normal_decode_modes: [None; 8],
            scalar_parameters: None,
        };
        assert_eq!(
            contract.validate_integrity(&[bytes]),
            Err(SourceGraphContractError::UnqualifiedNormalDecode)
        );
        contract.normal_decode_modes[7] = Some(SourceGraphNormalDecodeMode::RgOrAg);
        assert_eq!(contract.validate_integrity(&[bytes]), Ok(()));
        assert_eq!(contract.normal_decode_modes[0], None);
    }

    #[test]
    fn integrity_admission_requires_every_swizzled_normal_profile() {
        let bytes = b"fn decode() -> vec3<f32> { return source_graph.normal_decode_modes[0].xyz; }";
        let hash = bytes_sha256(bytes);
        let mut contract = SourceGraphMaterialContract {
            schema_version: 2,
            source_program_sha256: hash,
            source_material_sha256: hash,
            forward: SourceGraphBlob {
                buffer_view: 0,
                sha256: hash,
            },
            prepass: SourceGraphBlob {
                buffer_view: 0,
                sha256: hash,
            },
            libraries: vec![],
            textures: vec![],
            state: SourceGraphPipelineState {
                color_write_mask: None,
                color_blend: [1, 0],
                alpha_blend: [1, 0],
                forward_cull: 2,
                prepass_cull: 2,
                depth_compare: 4,
                write_depth: true,
                transparent: false,
                alpha_test: false,
                alpha_to_coverage: false,
                render_queue: 2000,
                disabled_passes: vec![],
            },
            requires_original_vertex_color: false,
            normal_decode_modes: [None; 8],
            scalar_parameters: None,
        };
        assert_eq!(
            contract.validate_integrity(&[bytes]),
            Err(SourceGraphContractError::UnqualifiedNormalDecode)
        );
        contract.normal_decode_modes[0] = Some(SourceGraphNormalDecodeMode::RgOrAg);
        assert_eq!(
            contract.validate_integrity(&[bytes]),
            Err(SourceGraphContractError::UnqualifiedNormalDecode)
        );
        contract.normal_decode_modes[1] = Some(SourceGraphNormalDecodeMode::RgOrAg);
        assert_eq!(
            contract.validate_integrity(&[bytes]),
            Err(SourceGraphContractError::UnqualifiedNormalDecode)
        );
        contract.normal_decode_modes[2] = Some(SourceGraphNormalDecodeMode::RgOrAg);
        assert_eq!(contract.validate_integrity(&[bytes]), Ok(()));
        contract.normal_decode_modes[0] = None;
        assert_eq!(
            contract.validate_integrity(&[bytes]),
            Err(SourceGraphContractError::UnqualifiedNormalDecode)
        );
        assert_eq!(contract.normal_decode_modes[3], None);
    }

    #[test]
    fn normal_decode_wire_modes_keep_the_source_shader_abi() {
        for (mode, name, value) in [
            (SourceGraphNormalDecodeMode::Rgb, "rgb", 0.0_f32),
            (
                SourceGraphNormalDecodeMode::ReconstructZ,
                "reconstruct_z",
                1.0,
            ),
            (SourceGraphNormalDecodeMode::RgOrAg, "rg_or_ag", 2.0),
        ] {
            assert_eq!(mode.shader_value().to_bits(), value.to_bits());
            assert_eq!(serde_json::to_value(mode).unwrap(), name);
            assert_eq!(
                serde_json::from_value::<SourceGraphNormalDecodeMode>(serde_json::json!(name))
                    .unwrap(),
                mode
            );
        }
        assert!(
            serde_json::from_value::<SourceGraphNormalDecodeMode>(serde_json::json!("automatic"))
                .is_err()
        );
    }
}

#[cfg(test)]
mod captured_storage_contract_tests {
    use super::*;
    fn fixture() -> (Vec<Vec<u8>>, SourceGraphTexture) {
        let (d, c, storage) = imported_storage::tests::fixture();
        let mut png = vec![0u8; 33];
        png[..8].copy_from_slice(b"\x89PNG\r\n\x1a\n");
        png[12..16].copy_from_slice(b"IHDR");
        png[16..20].copy_from_slice(&4u32.to_be_bytes());
        png[20..24].copy_from_slice(&4u32.to_be_bytes());
        let meta = b"fileFormatVersion: 2\nTextureImporter:\n".to_vec();
        let t = SourceGraphTexture {
            png: SourceGraphBlob {
                buffer_view: 2,
                sha256: bytes_sha256(&png),
            },
            source_meta: SourceGraphBlob {
                buffer_view: 3,
                sha256: bytes_sha256(&meta),
            },
            source_meta_sha256: bytes_sha256(&meta),
            is_srgb: false,
            source_mipmaps: true,
            filter_mode: -1,
            wrap: [-1; 3],
            anisotropy: -1,
            mip_bias: 0.0,
            imported_storage: Some(storage),
        };
        (vec![d, c, png, meta], t)
    }
    #[test]
    fn historical_texture_wire_omits_capture_and_stays_readable() {
        let (v, mut t) = fixture();
        t.imported_storage = None;
        let raw = serde_json::to_value(&t).unwrap();
        assert!(raw.get("imported_storage").is_none());
        let t: SourceGraphTexture = serde_json::from_value(raw).unwrap();
        let views: Vec<_> = v.iter().map(Vec::as_slice).collect();
        assert_eq!(validate_texture_integrity(&t, &views), Ok(()));
    }
    #[test]
    fn captured_texture_uses_existing_blob_integrity() {
        let (v, t) = fixture();
        let raw = serde_json::to_vec(&t).unwrap();
        let roundtrip: SourceGraphTexture = serde_json::from_slice(&raw).unwrap();
        let views: Vec<_> = v.iter().map(Vec::as_slice).collect();
        assert_eq!(validate_texture_integrity(&roundtrip, &views), Ok(()));
    }
    #[test]
    fn captured_corruption_refuses_without_png_fallback() {
        let (mut v, mut t) = fixture();
        v[0][8] ^= 1;
        t.imported_storage.as_mut().unwrap().storage.sha256 = bytes_sha256(&v[0]);
        let views: Vec<_> = v.iter().map(Vec::as_slice).collect();
        assert_eq!(
            validate_texture_integrity(&t, &views),
            Err(SourceGraphContractError::ImportedStorage(
                SourceGraphImportedStorageError::Hash
            ))
        );
    }
    #[test]
    fn changed_color_mipmap_or_explicit_sampler_refuses() {
        let (v, mut t) = fixture();
        let views: Vec<_> = v.iter().map(Vec::as_slice).collect();
        t.is_srgb = true;
        assert_eq!(
            validate_texture_integrity(&t, &views),
            Err(SourceGraphContractError::ImportSettings)
        );
        t.is_srgb = false;
        t.source_mipmaps = false;
        assert_eq!(
            validate_texture_integrity(&t, &views),
            Err(SourceGraphContractError::ImportSettings)
        );
        t.source_mipmaps = true;
        t.filter_mode = 0;
        assert_eq!(
            validate_texture_integrity(&t, &views),
            Err(SourceGraphContractError::ImportSettings)
        );
    }
}

impl SourceGraphTexture {
    /// Validate original PNG/meta and declared captured storage together.
    /// This is integrity admission, not GPU or source-render equivalence.
    pub fn validate_integrity(&self, views: &[&[u8]]) -> Result<(), SourceGraphContractError> {
        validate_texture_integrity(self, views)
    }
}

#[cfg(test)]
mod deferred_srgb_contract_tests {
    use super::*;
    #[test]
    fn shared_decoder_is_not_authored_texture_and_rejects_malformed_or_duplicate_binding() {
        assert_eq!(
            stage_texture_count(SOURCE_GRAPH_DEFERRED_SRGB_DECLARATION).unwrap(),
            0
        );
        assert!(
            stage_texture_count(&format!(
                "{SOURCE_GRAPH_DEFERRED_SRGB_DECLARATION}\n{SOURCE_GRAPH_DEFERRED_SRGB_DECLARATION}"
            ))
            .is_err()
        );
        assert!(
            stage_texture_count(&SOURCE_GRAPH_DEFERRED_SRGB_DECLARATION.replace("127", "128"))
                .is_err()
        );
    }
}

#[cfg(test)]
mod native_srgb_encoder_contract_tests {
    use super::*;
    #[test]
    fn native_srgb_scene_texture_does_not_count_as_an_authored_slot() {
        assert_eq!(
            stage_texture_count(SOURCE_GRAPH_NATIVE_SRGB_THRESHOLDS_DECLARATION).unwrap(),
            0
        );
    }
    #[test]
    fn native_srgb_scene_texture_rejects_duplicates_renaming_and_wrong_binding() {
        let d = SOURCE_GRAPH_NATIVE_SRGB_THRESHOLDS_DECLARATION;
        for value in [
            format!("{d}\n{d}"),
            d.replace("binding(128)", "binding(129)"),
            d.replace("source_native_srgb_thresholds", "renamed"),
        ] {
            assert!(stage_texture_count(&value).is_err());
        }
    }
}
